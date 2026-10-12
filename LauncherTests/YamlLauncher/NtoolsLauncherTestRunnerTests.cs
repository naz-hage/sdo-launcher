#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using YamlLauncher.TestRunners;

namespace LauncherTests.YamlLauncher;

[TestClass]
[DoNotParallelize]
public class NtoolsLauncherTestRunnerTests
{
    [TestMethod]
    public async Task RunTestAsync_ExtractsAndSubstitutesVariables_AndEvaluatesMigratedAssertions()
    {
        using var metadata = TemporaryMetadata.Create(@"version: '1.0'
description: 'Migrated runner behavior'
steps:
  - name: extract
    path: 'cmd.exe'
    arguments: '/c echo Item: 42'
    extractVariables:
      - name: item_id
        pattern: 'Item: (\d+)'
        groupIndex: 1
    assertions:
      - type: exit_code
        value: '0'
      - type: output_contains
        value: 'Item: 42'
      - type: output_matches
        pattern: 'Item: \d+'
  - name: use-extracted-value
    path: 'cmd.exe'
    arguments: '/c echo Selected {item_id}'
    assertions:
      - type: exit_code
        value: '0'
      - type: output_contains
        value: 'Selected 42'
");

        var runner = new NtoolsLauncherTestRunner(metadataPath: metadata.Path);

        var result = await runner.RunTestAsync("Test_MigratedRunnerBehavior");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task RunTestAsync_StopOnFirstError_DoesNotExecuteLaterSteps()
    {
        using var metadata = TemporaryMetadata.Create(@"version: '1.0'
execution:
  stopOnFirstError: true
steps:
  - name: fail
    path: 'cmd.exe'
    arguments: '/c exit 1'
    assertions:
      - type: exit_code
        value: '0'
  - name: should-not-run
    path: 'cmd.exe'
    arguments: '/c echo ran > {marker}'

");

        var markerPath = Path.Combine(metadata.Path, "marker.txt");
        var yamlPath = Path.Combine(metadata.Path, "Test_MigratedFailFast.yaml");
        var yaml = await File.ReadAllTextAsync(yamlPath);
        await File.WriteAllTextAsync(yamlPath, yaml.Replace("{marker}", markerPath.Replace("\\", "/"), StringComparison.Ordinal));

        var runner = new NtoolsLauncherTestRunner(metadataPath: metadata.Path);

        var result = await runner.RunTestAsync("Test_MigratedFailFast");

        Assert.IsFalse(result);
        Assert.IsFalse(File.Exists(markerPath));
    }

    private sealed class TemporaryMetadata : IDisposable
    {
        private TemporaryMetadata(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryMetadata Create(string yaml)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sdo-launcher-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            File.WriteAllText(System.IO.Path.Combine(path, "Test_MigratedRunnerBehavior.yaml"), yaml);
            File.WriteAllText(System.IO.Path.Combine(path, "Test_MigratedFailFast.yaml"), yaml);
            return new TemporaryMetadata(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}