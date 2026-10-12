# sdo-launcher

The `sdo-launcher` is a NuGet package library that simplifies common tasks related to launching executables, downloading files, executing shell commands, and checking process elevation. This library is designed to be easy to use, while providing robust functionality for a variety of tasks. Full Documentation can be found [here](https://naz-hage.github.io/sdo-launcher/).

## Prerequisites

- .NET 10.0 or later

## Features

- **Launcher:** A class with methods to launch a process and wait for it to complete. This includes methods for locking a file and verifying its digital signature before launching, and launching a process in a separate thread.
- **ResultHelper:** A helper class for retrieving the result `Code` and `Output` of the launched executable.

- **Nfile:** A class for downloading files from trusted HTTPS hosts, checking allowed extensions, recording file size, and inspecting the downloaded file signature.
- **ResultDownload:** A helper class for retrieving the result of a downloaded file.

- **CurrentProcess:** A class that provides a method to determine if the current process is elevated.

- **ShellUtility:** A helper class for executing shell commands and retrieving the full path of a file from the Path environment variable.

- **YAML Launcher Framework:** YAML parsing, validation, and single-step execution with output capture, timeouts, environment variables, verbose logging, and optional signature checks.

## Installation

The `sdo-launcher` package is available on nuget.org. To install the package, run the following command in the Package Manager Console:

```bash
Install-Package sdo-launcher
```

## Usage

### Launcher Class

The `Launcher` class provides methods for launching a process. This includes methods for locking a file and verifying its digital signature before launching, and launching a process in a separate thread.

Here's an example of how to use the `Launcher` class:

```csharp
using Ntools;

var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
        FileName = "robocopy.exe",
        Arguments = "/?",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = false,
        UseShellExecute = false
    }
};

var result = process.LockStart(true);
if (result.IsSuccess())
{
    Console.WriteLine("Success");
}
else
{
    Console.WriteLine($"Code: {result.Code}");
    foreach (var line in result.Output)
    {
        Console.WriteLine(line);
    }
}
```
The Launcher class exposes `LockVerifyStart` method which is useful in scenarios where you need to start a process and verify that the file is digitally signed before launching. Here are some scenarios where you might use this method:

1. **Securely launching executables**: If you need to start an executable and want to ensure that it is digitally signed before launching, you can use this function to verify the digital signature of the file before starting the process.

2. **Preventing unauthorized executables**: In scenarios where you want to prevent unauthorized executables from running on your system, you can use this function to verify the digital signature of the file before launching it.


### Nfile Class

The `Nfile` class provides a method for downloading files from configured trusted HTTPS hosts.

```csharp
using Ntools;

 try
 {
    Nfile.SetTrustedHosts(new List<string> { "example.com" });
    Nfile.SetAllowedExtensions(new List<string> { ".zip" });
    var result = await Nfile.DownloadAsync("https://example.com/file.zip", "C:\\temp\\file.zip");
    if (result.IsSuccess())
    {
        Console.WriteLine("Success");
    }
    else
    {
        Console.WriteLine($"Code: {result.Code}");
        Console.WriteLine(result.GetFirstOutput());
    }
 }
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
```
The `DownloadAsync` method is useful in scenarios where you need to download a file from the web. Here are some scenarios where you might use this method:

1. **Downloading files from the web**: If you need to download a file from the web, you can use this function to do so.

2. **Checking for virus**: In scenarios where you want to check the downloaded file for viruses, you can use this function to download the file and then check it using a virus scanning service such as VirusTotal.

3. **Verifying file signature**: If you need to verify the digital signature of the downloaded file, you can use this function to do so.

### ShellUtility Class

The `ShellUtility` class provides a method for retrieving the full path of a file from the Path environment variable. Here's an example of how to use the `ShellUtility` class:

```csharp
using Ntools;

var fullPath = ShellUtility.GetFullPathOfFile("notepad.exe");
Console.WriteLine(fullPath);
```

### CurrentProcess Class

The `CurrentProcess` class provides a method to determine if the current process is elevated. Here's an example of how to use the `CurrentProcess` class:

```csharp
using Ntools;

var isElevated = CurrentProcess.IsElevated();
Console.WriteLine(isElevated);
```

### YAML Launcher Framework

The YAML Launcher framework provides a declarative way to configure and execute a selected step (application, task, or script) with validated configuration, output capture, timeouts, environment variables, expected exit codes, and optional signature checks. The configuration models also define fields for later orchestration features.

#### Basic YAML Configuration

Create a YAML file to define your execution steps:

```yaml
version: '1.0'
description: 'Build and deploy application'
execution:
  mode: Sequential
  verbose: true
  stopOnFirstError: true
steps:
  - name: 'restore'
    path: 'dotnet'
    arguments: 'restore'
    
  - name: 'build'
    path: 'dotnet'
    arguments: 'build -c Release'
    dependencies:
      - 'restore'
    assertions:
      - type: 'exitCode'
        expectedValue: '0'
```

#### Loading and Executing YAML Configuration

```csharp
using YamlLauncher.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// Load YAML configuration
var yaml = File.ReadAllText("launcher-config.yaml");
var deserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();

var config = deserializer.Deserialize<LauncherConfig>(yaml);

var loader = new YamlLauncherConfigLoader();
var config = await loader.LoadFromFileAsync("launcher-config.yaml");
var executor = new StepExecutor(config.Execution?.Verbose ?? false);
var result = await executor.LaunchAsync(config, stepIndex: 0);

// Result contains the exit code and captured standard output/error.
Console.WriteLine(result.Results[0].ExitCode);
```

Steps can also be selected by name or several zero-based indices:

```csharp
// The name must match exactly one configured step.
var namedResult = await executor.LaunchAsync(config, "build");

// Steps execute sequentially in the requested order (2, then 0).
var selectedResult = await executor.LaunchAsync(config, 2, 0);
foreach (var execution in selectedResult.Results ?? [])
{
    Console.WriteLine($"{execution.StepName}: {execution.ExitCode}");
}
```

Name selection fails if no step matches or if multiple steps have the same
name. Index selection validates every requested index before starting any
process, so an invalid selection cannot start an unintended step.

#### Key Features

- **Multiple Aliases**: Use `steps:`, `tasks:`, or `apps:` interchangeably in YAML
- **Configuration aliases**: Use `steps:`, `tasks:`, or `apps:` for the same step collection
- **Flexible step execution**: Execute one step by index or unique name, or execute multiple steps sequentially by index
- **Process controls**: Apply working directories, environment variables, expected exit codes, and timeouts
- **Output capture**: Capture standard output and standard error in `ExecutionResult`
- **Validation**: Report invalid YAML and configuration errors with actionable messages
- **Verbose logging**: Emit `[LAUNCHER]`-prefixed execution details when enabled
- **YAML Deserialization**: Full support for YamlDotNet serialization/deserialization

#### Model Classes

The framework includes comprehensive model classes for type-safe configuration:

- `LauncherConfig`: Main configuration container with version, description, and execution settings
- `StepConfig`: Individual step configuration with path, arguments, dependencies, assertions, and variable extraction
- `ExecutionSettings`: Execution mode, verbosity, concurrency, and timeout settings
- `Assertion`: Result validation rules (exitCode, stdout, JSON paths, patterns)
- `VariableExtraction`: Rules for extracting variables from execution results
- `LaunchResult`, `ExecutionResult`: Result objects containing execution outcomes

For detailed architecture and design information, see [yaml-launcher-design.md](../docs/planning/yaml-launcher-design.md). Dependency orchestration, variable substitution between steps, and full assertion evaluation remain planned extensions.
