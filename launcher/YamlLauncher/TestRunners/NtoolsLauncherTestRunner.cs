#nullable enable

using Launcher.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlLauncher.Models;

namespace YamlLauncher.TestRunners;

/// <summary>
/// Test runner for sdo-launcher YAML configuration files.
/// Loads YAML configurations, validates them, executes steps, and extracts variables from output.
/// </summary>
public class NtoolsLauncherTestRunner
{
    private readonly bool _verbose;
    private readonly string _metadataPath;

    public NtoolsLauncherTestRunner(bool verbose = false, string? metadataPath = null)
    {
        _verbose = verbose;
        _metadataPath = metadataPath ?? Path.Combine(AppContext.BaseDirectory, "metadata");
    }

    /// <summary>
    /// Runs a test by name from the sdo-launcher YAML metadata.
    /// </summary>
    public async Task<bool> RunTestAsync(string testName)
    {
        try
        {
            var yamlFile = FindYamlFile(testName);

            if (yamlFile == null)
            {
                WriteError($"Test YAML file not found for test: {testName}");
                return false;
            }

            ConsoleHelper.WriteLine($"{'='} Running sdo-launcher Test: {testName} {'='}");
            // display solid line to separate test output from previous console output
            ConsoleHelper.SolidLine();

            // Load the YAML configuration
            var loader = new YamlLauncherConfigLoader();
            LauncherConfig config;

            try
            {
                config = await loader.LoadFromFileAsync(yamlFile);
                WriteInfo("[✓] YAML Configuration loaded successfully");
                WriteInfo($"    Version: {config.Version}");
                WriteInfo($"    Steps: {config.Steps?.Count ?? 0}");
                WriteInfo($"    Description: {config.Description}");
            }
            catch (Exception ex)
            {
                WriteError($"Failed to load YAML: {ex.Message}");
                if (ex is LauncherConfigException lcex)
                {
                    WriteError($"    Line: {lcex.LineNumber}, Column: {lcex.ColumnNumber}");
                }
                return false;
            }

            // Validate the configuration
            WriteInfo("--- Configuration Validation ---");
            try
            {
                var validator = new LauncherConfigValidator();
                validator.Validate(config);
                WriteInfo("[✓] Configuration is valid");
            }
            catch (Exception ex)
            {
                WriteError($"Configuration validation failed: {ex.Message}");
                return false;
            }

            // Execute the steps
            WriteInfo("--- Executing Steps ---");
            var executor = new StepExecutor(verbose: _verbose);
            bool allSuccess = true;
            var passedSteps = 0;
            var failedSteps = 0;
            var variables = new Dictionary<string, string>();

            if (config.Steps != null)
            {
                for (int i = 0; i < config.Steps.Count; i++)
                {
                    var step = config.Steps[i];
                        
                    // Substitute variables in arguments for this step
                    if (variables.Count > 0 && !string.IsNullOrEmpty(step.Arguments))
                    {
                        step.Arguments = SubstituteVariables(step.Arguments, variables);
                    }

                    try
                    {
                        var result = await executor.LaunchAsync(config, i);

                        // Get the execution result - it should be the last one in the Results collection
                        var executionResult = result.Results?.LastOrDefault();
                        if (executionResult == null)
                        {
                            WriteError("No execution result returned");
                            allSuccess = false;
                            failedSteps++;
                            continue;
                        }

                        // Extract variables from step output
                        ExtractVariables(step, executionResult, variables);

                        var assertionsPassed = EvaluateAssertions(step, executionResult);
                        var stepPassed = executionResult.Success && assertionsPassed;

                        WriteInfo($"Exit Code: {executionResult.ExitCode}");
                        WriteInfo($"Duration: {executionResult.DurationMs}ms");
                        
                        if (stepPassed)
                        {
                            passedSteps++;
                            WriteSuccess($"PASS: {step.Name ?? $"Step {i + 1}"}");
                        }
                        else
                        {
                            failedSteps++;
                            var stepLabel = $"Step {i + 1} ('{step.Name ?? "unnamed"}')";
                            var failureMessage = string.IsNullOrWhiteSpace(executionResult.ErrorMessage)
                                ? $"FAIL: {stepLabel}"
                                : $"FAIL: {stepLabel}: {executionResult.ErrorMessage}";
                            WriteError(failureMessage);
                            allSuccess = false;
                        }

                        if (!allSuccess && config.Execution?.StopOnFirstError == true)
                        {
                            WriteError("Stopping because stopOnFirstError is enabled");
                            break;
                        }

                        // Show extracted variables
                        if (executionResult.ExtractedVariables?.Count > 0)
                        {
                            WriteInfo("Variables extracted:");
                            foreach (var varResult in executionResult.ExtractedVariables)
                            {
                                WriteInfo($"  - {varResult.Name} = {varResult.Value}");
                            }
                        }

                        // Show stdout/stderr if verbose
                        if (_verbose)
                        {
                            if (!string.IsNullOrEmpty(executionResult.StandardOutput))
                            {
                                WriteVerbose($"Output:\n{string.Join("\n", executionResult.StandardOutput.Split('\n').Take(5))}");
                            }
                            if (!string.IsNullOrEmpty(executionResult.StandardError))
                            {
                                WriteVerbose($"Error:\n{string.Join("\n", executionResult.StandardError.Split('\n').Take(5))}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failedSteps++;
                        ConsoleHelper.WriteError($"Step {i + 1} ('{step.Name ?? "unnamed"}') failed: {ex.Message}");
                        allSuccess = false;

                        if (config.Execution?.StopOnFirstError == true)
                        {
                            ConsoleHelper.WriteError("Stopping because stopOnFirstError is enabled");
                            break;
                        }
                    }
                }
            }

            // Summary
            ConsoleHelper.WriteInfo("--- Execution Summary ---");
            ConsoleHelper.WriteSuccess($"Passed: {passedSteps}");
            if (failedSteps > 0)
            {
                ConsoleHelper.WriteError($"Failed: {failedSteps}");
            }
            if (allSuccess)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Unexpected error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Runs all discovered YAML test configurations in deterministic name order.
    /// </summary>
    public async Task<bool> RunAllTestsAsync()
    {
        var testNames = GetYamlTestFiles()
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (testNames.Length == 0)
        {
            ConsoleHelper.WriteWarning("No YAML test files found");
            return true;
        }

        var allSuccess = true;
        var passedTests = 0;
        var failedTests = 0;
        var failedTestNames = new List<string>();
        foreach (var testName in testNames)
        {
            if (!await RunTestAsync(testName!))
            {
                allSuccess = false;
                failedTests++;
            failedTestNames.Add(testName!);
                ConsoleHelper.WriteError($"Failed: {testName}");
            }
            else
            {
                passedTests++;
                ConsoleHelper.WriteSuccess($"Passed: {testName}");
            }
        }

        ConsoleHelper.WriteInfo($"Execution summary: {passedTests + failedTests} total");
        ConsoleHelper.WriteSuccess($"Passed: {passedTests}");
        if (failedTests > 0)
        {
            ConsoleHelper.WriteError($"Failed: {failedTests}");
            ConsoleHelper.WriteError("Failed tests:");
            foreach (var failedTestName in failedTestNames)
            {
                ConsoleHelper.WriteError($"  - {failedTestName}");
            }
        }

        // Write a solid line to separate the summary from any further output
        ConsoleHelper.SolidLine();

        return allSuccess;
    }

    /// <summary>
    /// Lists all available sdo-launcher YAML test files.
    /// </summary>
    public void ListTests()
    {
        try
        {
            if (!Directory.Exists(_metadataPath))
            {
                ConsoleHelper.WriteInfo("No metadata directory found");
                return;
            }

            var yamlFiles = GetYamlTestFiles();

            if (yamlFiles.Length == 0)
            {
                ConsoleHelper.WriteInfo("No YAML test files found");
                return;
            }

            ConsoleHelper.WriteInfo($"Available tests ({yamlFiles.Length}):");

            foreach (var file in yamlFiles.OrderBy(file => file, StringComparer.Ordinal))
            {
                var testName = Path.GetFileNameWithoutExtension(file);
                ConsoleHelper.WriteInfo($"  • {testName}");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error listing tests: {ex.Message}");
        }
    }

    private string? FindYamlFile(string testName)
    {
        foreach (var extension in new[] { ".yaml", ".ntools.yml", ".yml" })
        {
            var candidate = Path.Combine(_metadataPath, $"{testName}{extension}");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private string[] GetYamlTestFiles()
    {
        if (!Directory.Exists(_metadataPath))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(_metadataPath)
            .Where(file =>
            {
                var fileName = Path.GetFileName(file);
                return fileName.StartsWith("Test_", StringComparison.OrdinalIgnoreCase) &&
                    (fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                     fileName.EndsWith(".ntools.yml", StringComparison.OrdinalIgnoreCase) ||
                     fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)) ||
                    fileName.StartsWith("Validate_", StringComparison.OrdinalIgnoreCase) &&
                    (fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                     fileName.EndsWith(".ntools.yml", StringComparison.OrdinalIgnoreCase) ||
                     fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));
            })
            .ToArray();
    }

    /// <summary>
    /// Substitutes variables in text using {varName} syntax.
    /// </summary>
    private string SubstituteVariables(string text, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(text) || variables.Count == 0)
            return text;

        var result = text;
        foreach (var variable in variables)
        {
            result = result.Replace($"{{{variable.Key}}}", variable.Value);
        }
        return result;
    }

    /// <summary>
    /// Extracts variables from step execution output using configured patterns.
    /// Based on test-framework MetadataTestExecutor logic with bounds checking.
    /// </summary>
    private void ExtractVariables(StepConfig step, ExecutionResult result, Dictionary<string, string> variables)
    {
        // Check if there are any variable extractions configured
        if (step.ExtractVariables == null || step.ExtractVariables.Count == 0)
        {
            WriteVerbose("No variable extractions configured for this step");
            return;
        }

        var output = result.StandardOutput ?? string.Empty;
        
        if (string.IsNullOrEmpty(output))
        {
            WriteVerbose("No output to extract variables from");
            return;
        }

        WriteVerbose($"Extracting {step.ExtractVariables.Count} variable(s)");

        foreach (var extraction in step.ExtractVariables)
        {
            try
            {
                if (string.IsNullOrEmpty(extraction.Pattern))
                {
                    WriteError($"Extraction '{extraction.Name}': No pattern defined");
                    continue;
                }

                if (string.IsNullOrEmpty(extraction.Name))
                {
                    WriteError("Extraction has no variable name defined");
                    continue;
                }

                WriteVerbose($"Pattern from YAML: '{extraction.Pattern}' (length={extraction.Pattern.Length})");
                WriteVerbose($"Pattern bytes: {string.Join(", ", extraction.Pattern.Select(c => $"'{c}'({(int)c})"))}");
                WriteVerbose($"Attempting to extract '{extraction.Name}' using pattern: {extraction.Pattern}, groupIndex: {extraction.GroupIndex}");

                var regexOptions = extraction.CaseInsensitive ? RegexOptions.IgnoreCase | RegexOptions.Multiline : RegexOptions.Multiline;
                var match = Regex.Match(output, extraction.Pattern, regexOptions);

                if (match.Success)
                {
                    WriteVerbose($"Match found! Groups count: {match.Groups.Count}");
                    WriteVerbose($"Match position: Index={match.Index}, Length={match.Length}, Value='{match.Value}'");
                    var contextStart = Math.Max(0, match.Index - 20);
                    var contextLength = Math.Min(60, output.Length - contextStart);
                    WriteVerbose($"Context: '{output.Substring(contextStart, contextLength)}'");
                    for (int g = 0; g < match.Groups.Count; g++)
                    {
                        WriteVerbose($"  Group[{g}] = '{match.Groups[g].Value}'");
                    }
                    WriteVerbose($"Full output length: {output.Length}");
                    WriteVerbose($"Full output:\n{output}");

                    // **CRITICAL FIX**: Check bounds before accessing group, just like test-framework does
                    var value = match.Groups.Count > extraction.GroupIndex
                        ? match.Groups[extraction.GroupIndex].Value
                        : match.Value;

                    if (!string.IsNullOrEmpty(value))
                    {
                        variables[extraction.Name] = value;
                        WriteSuccess($"Extracted: {extraction.Name} = {value}");
                    }
                    else
                    {
                        WriteInfo("Pattern matched but extracted value is empty");
                    }
                }
                else
                {
                    WriteError("Pattern did not match output");
                    if (_verbose)
                    {
                        var preview = output.Length > 200 ? output.Substring(0, 200) + "..." : output;
                        WriteVerbose($"   Output: {preview}");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteError($"Error extracting variable {extraction.Name}: {ex.Message}");
            }
        }
    }

    private bool EvaluateAssertions(StepConfig step, ExecutionResult result)
    {
        if (step.Assertions == null || step.Assertions.Count == 0)
        {
            return true;
        }

        var output = string.Concat(result.StandardOutput, result.StandardError);
        var allPassed = true;

        foreach (var assertion in step.Assertions)
        {
            var comparison = assertion.CaseInsensitive
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var type = assertion.Type?.Trim().ToLowerInvariant();
            var expected = assertion.Type?.Trim().Equals("output_matches", StringComparison.OrdinalIgnoreCase) == true
                ? assertion.Pattern ?? "<missing>"
                : assertion.Value ?? "<missing>";
            var passed = type switch
            {
                "exit_code" => int.TryParse(assertion.Value, out var expectedExitCode) &&
                    result.ExitCode == expectedExitCode,
                "output_contains" => assertion.Value != null &&
                    output.Contains(assertion.Value, comparison),
                "output_matches" => !string.IsNullOrEmpty(assertion.Pattern) &&
                    Regex.IsMatch(
                        output,
                        assertion.Pattern,
                        assertion.CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None),
                _ => false
            };

            if (passed)
            {
                WriteSuccess($"Assertion passed: {assertion.Type}; expected '{expected}'");
            }
            else
            {
                WriteError($"Assertion failed: {assertion.Type}; expected '{expected}'");
                allPassed = false;
            }
        }

        return allPassed;
    }

    private static void WriteInfo(string message) => ConsoleHelper.WriteInfo(message);

    private static void WriteSuccess(string message) => ConsoleHelper.WriteSuccess(message);

    private static void WriteError(string message) => ConsoleHelper.WriteError(message);

    private void WriteVerbose(string message)
    {
        if (_verbose)
        {
            ConsoleHelper.WriteVerbose(message);
        }
    }
}
