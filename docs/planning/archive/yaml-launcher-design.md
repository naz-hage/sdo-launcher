# YAML-Based Steps Launcher Design Plan

**Date:** May 25, 2026  
**Purpose:** Design a unified YAML-based executable launcher framework to extend sdo-launcher NuGet package  
**Scope:** Single file containing one or many executables with exe and arguments, supporting sequential or parallel execution patterns

---

## 1. Executive Summary

This design creates a new API layer for sdo-launcher that accepts YAML configuration files instead of Process objects. The framework unifies executable launching patterns across three codebases (test-framework, sdooo, sdo-launcher) into a single, extensible design.

**Key Benefits:**
- Declarative YAML-based configuration eliminates boilerplate Process creation code
- Support for single and multiple executable sequences with dependency management
- Extensible design supports future features (retry policies, timeout handling, hooks)
- Reusable NuGet package for downstream projects
- Single source of truth for application deployment

---

## 2. Existing Patterns Analysis

### 2.1 Current Executable Launching Patterns

#### Pattern A: test-framework/CliTestExecutor
```csharp
var executor = new CliTestExecutor("sdo.exe", verbose: true);
var result = await executor.ExecuteAsync("install", new[] { "--name", "MyApp" });
```
- **Strengths:** Handles environment vars, output capture, working directory
- **Weakness:** Hard-coded arguments in test code, no declarative configuration
- **Use Case:** E2E testing, CLI validation

#### Pattern B: sdo-launcher/Launcher
```csharp
var process = new Process { StartInfo = ... };
var result = process.LockVerifyStart(verbose);  // with signature verification
```
- **Strengths:** Digital signature verification, file locking, atomic operations
- **Weakness:** Manual Process setup, ceremony-heavy
- **Use Case:** Production executable launches with security requirements

#### Pattern C: sdoo Command/ntools.json
```json
{
  "Name": "MyApp",
  "AppFileName": "$(InstallPath)\\app.exe",
  "InstallArgs": "-Command Expand-Archive -Path $(Version).zip"
}
```
- **Strengths:** Variable substitution, manifest-based, declarative
- **Weakness:** Installation-focused, not general executable orchestration
- **Use Case:** Application installation/deployment

---

## 3. Proposed YAML Schema

### 3.1 Single Step (Basic)

```yaml
# single-step-launcher.yaml (or use 'tasks:' or 'apps:')
version: "1.0"
description: "Deploy MyApp with signature verification"

steps:
  - path: "C:\\Program Files\\MyCompany\\myapp.exe"
    workingDirectory: "C:\\Temp"
    arguments: "--config settings.json --verbose"
    
    # Optional: Environment variable overrides
    environment:
      LOG_LEVEL: "DEBUG"
      CUSTOM_PATH: "$(InstallPath)"
    
    # Optional: Execution settings
    timeout: 30000  # milliseconds
    verifySignature: true  # Requires digital signing
    redirectOutput: true
  
execution:
  name: "DeployApp"
  verbose: true
```

### 3.2 Multiple Steps (Sequence)

```yaml
# multi-step-launcher.yaml (or use 'tasks:' or 'apps:')
version: "1.0"
description: "Sequential deployment pipeline"

steps:
  - name: "prepare"
    path: "C:\\scripts\\prepare.bat"
    workingDirectory: "C:\\Temp"
    arguments: "--env prod"
    timeout: 60000
    continueOnError: false  # Pipeline stops if this fails
  
  - name: "install"
    path: "C:\\Program Files\\installer.exe"
    workingDirectory: "C:\\Temp"
    arguments: "--package myapp.zip --destination $(InstallPath)"
    dependencies: ["prepare"]  # Run after 'prepare' succeeds
    timeout: 120000
    verifySignature: true
  
  - name: "verify"
    path: "C:\\Program Files\\validator.exe"
    workingDirectory: "C:\\Temp"
    arguments: "--check $(InstallPath)\\myapp.exe"
    dependencies: ["install"]
    timeout: 30000
  
  - name: "cleanup"
    path: "C:\\scripts\\cleanup.bat"
    workingDirectory: "C:\\Temp"
    arguments: "--dir $(DownloadsPath)"
    dependencies: ["verify"]
    continueOnError: true  # Always cleanup, even if verify fails

execution:
  mode: "sequential"  # sequential | parallel
  verbose: true
  stopOnFirstError: false  # Don't stop entire pipeline, but mark result
  
variables:
  InstallPath: "C:\\Program Files\\MyCompany"
  DownloadsPath: "C:\\sdo-downloads"
```

### 3.3 Parallel Executables (Independent Tasks)

```yaml
# parallel-launcher.yaml
version: "1.0"
description: "Independent parallel verification"

tasks:
  - name: "test_unit"
    path: "dotnet.exe"
    arguments: "test --no-build"
  
  - name: "test_integration"
    path: "dotnet.exe"
    arguments: "test --filter Category=Integration"
  
  - name: "lint"
    path: "dotnet.exe"
    arguments: "format --verify-no-changes"

execution:
  mode: "parallel"
  timeout: 300000  # Global timeout for all
  maxConcurrency: 3
  verbose: true
```

---

## 4. Core API Design

### 4.1 New Public APIs for sdo-launcher

```csharp
namespace Ntools.Launcher
{
    /// <summary>
    /// Loads and parses YAML executable configuration files.
    /// </summary>
    public interface ILauncherConfigLoader
    {
        LauncherConfig LoadFromFile(string yamlFilePath);
        LauncherConfig LoadFromString(string yamlContent);
    }

    /// <summary>
    /// Orchestrates executable launching from YAML configurations.
    /// </summary>
    public interface IStepExecutor
    {
        Task<LaunchResult> LaunchAsync(LauncherConfig config);
        Task<LaunchResult> LaunchAsync(string yamlFilePath);
    }

    /// <summary>
    /// Configuration model parsed from YAML.
    /// </summary>
    public class LauncherConfig
    {
        public string Version { get; set; }
        public string Description { get; set; }
        
        // Single or multiple executables
        public StepConfig? Step { get; set; }
    public List<StepConfig>? Steps { get; set; }
    public List<StepConfig>? Tasks { get; set; }  // Alias for test-framework
    public List<StepConfig>? Apps { get; set; }    // Alias for sdo
        
        // Execution settings
        public ExecutionSettings Execution { get; set; }
        
        // Variable substitution
        public Dictionary<string, string>? Variables { get; set; }
    }

    /// <summary>
    /// Individual step configuration.
    /// </summary>
    public class StepConfig
    {
        public string? Name { get; set; }
        public string Path { get; set; }
        public string? Arguments { get; set; }
        public string? WorkingDirectory { get; set; }
        
        public Dictionary<string, string>? Environment { get; set; }
        
        public int Timeout { get; set; } = 30000;  // milliseconds
        public bool VerifySignature { get; set; } = false;
        public bool RedirectOutput { get; set; } = true;
        
        // Dependency and flow control
        public List<string>? Dependencies { get; set; }
        public bool ContinueOnError { get; set; } = false;
    }

    /// <summary>
    /// Execution mode and settings.
    /// </summary>
    public class ExecutionSettings
    {
        public string Mode { get; set; } = "sequential";  // sequential | parallel
        public bool Verbose { get; set; } = false;
        public bool StopOnFirstError { get; set; } = true;
        public int MaxConcurrency { get; set; } = 1;
    }

    /// <summary>
    /// Result of executable launch operation.
    /// </summary>
    public class LaunchResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public List<ExecutionResult> Results { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Result of individual executable execution.
    /// </summary>
    public class ExecutionResult
    {
        public string Name { get; set; }
        public int ExitCode { get; set; }
        public string StdOut { get; set; }
        public string StdErr { get; set; }
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
```

### 4.2 Usage Examples

#### Example 1: Single Step (Extends current Launcher)
```csharp
var loader = new YamlLauncherConfigLoader();
var executor = new StepExecutor();

var config = loader.LoadFromFile("single-app-launcher.yaml");
var result = await executor.LaunchAsync(config);

if (result.Success)
{
    Console.WriteLine($"✓ {config.Description} completed in {result.Duration.TotalSeconds}s");
}
else
{
    Console.WriteLine($"✗ Failed: {result.ErrorMessage}");
}
```

#### Example 2: Multiple Sequential (Pipeline)
```csharp
var result = await executor.LaunchAsync("multi-app-launcher.yaml");

foreach (var execResult in result.Results)
{
    var status = execResult.Success ? "✓" : "✗";
    Console.WriteLine($"{status} {execResult.Name}: exit code {execResult.ExitCode}");
    if (!string.IsNullOrEmpty(execResult.StdOut))
        Console.WriteLine($"  Output: {execResult.StdOut}");
}
```

#### Example 3: Parallel (Independent Tasks)
```csharp
var result = await launcher.LaunchAsync("parallel-launcher.yaml");

// All tasks run concurrently, results include all outputs
var failedCount = result.Results.Count(r => !r.Success);
Console.WriteLine($"Completed: {result.Results.Count - failedCount}/{result.Results.Count} tasks successful");
```

---

## 5. Implementation Strategy

### 5.1 Phase 1: Core Framework (Foundation)
**Timeline:** Week 1

**Deliverables:**
- YAML schema definition with comments
- `ILauncherConfigLoader` with YAML parsing (using YamlDotNet NuGet)
- `IStepExecutor` for single step execution
- `LauncherConfig`, `StepConfig`, `LaunchResult` models
- Unit tests for YAML parsing and single execution

**Dependencies:**
- YamlDotNet NuGet package

### 5.2 Phase 2: Extend Launcher.cs (Integration)
**Timeline:** Week 2

**Deliverables:**
- Refactor `Launcher.LockVerifyStart()` to use new framework
- Refactor `Launcher.LockStart()` to use new framework
- Add `LaunchAsync(string yamlPath)` extension methods
- Signature verification integration with new framework
- Integration tests with real executables

### 5.3 Phase 3: Sequential Pipeline (Orchestration)
**Timeline:** Week 2-3

**Deliverables:**
- Dependency resolution (DAG builder for execution order)
- Sequential execution orchestrator
- `continueOnError` and `stopOnFirstError` support
- Pipeline result aggregation and reporting

### 5.4 Phase 4: Parallel Execution (Concurrency)
**Timeline:** Week 3

**Deliverables:**
- Parallel execution engine with `maxConcurrency` control
- Thread-safe result collection
- Timeout management for parallel tasks
- Stress tests with concurrent executables

### 5.5 Phase 5: Advanced Features (Extensions)
**Timeline:** Week 4+

**Deliverables:**
- Retry policies (exponential backoff)
- Pre/post execution hooks
- Output filtering and parsing
- Template variable substitution patterns
- Schema validation and error reporting

---

## 6. Integration Points

### 6.1 sdo-launcher NuGet Package

**Current:** Manual Process creation and signature verification  
**New:** YAML-based configuration + signature verification built-in

```csharp
// Before
var process = new Process { StartInfo = new ProcessStartInfo(...) };
var result = process.LockVerifyStart(verbose);

// After
var executor = new StepExecutor();
var result = await executor.LaunchAsync("app-config.yaml");
```

### 6.2 test-framework Integration

**Current:** `CliTestExecutor` with hard-coded arguments  
**New:** Declare test scenarios in YAML, reuse across test suites

```yaml
# tests/cli-scenarios.yaml
version: "1.0"
tasks:
  - name: "test_install_with_name"
    path: "sdo.exe"
    arguments: "install --name MyApp"
    verifySignature: true
  
  - name: "test_install_with_json"
    path: "sdo.exe"
    arguments: "install --json manifest.json"
    verifySignature: true
```

### 6.3 sdo Installation Scenarios

**Current:** Hardcoded in ntools.json with limited orchestration  
**New:** Complex installation pipelines with pre/post steps

```yaml
# deployment/installation-pipeline.yaml
version: "1.0"
tasks:
  - name: "download"
    path: "sdo.exe"
    arguments: "download --json tools.json"
  
  - name: "install"
    path: "sdo.exe"
    arguments: "install --json tools.json"
    dependencies: ["download"]
    verifySignature: true
  
  - name: "verify"
    path: "sdo.exe"
    arguments: "list"
    dependencies: ["install"]
```

---

## 7. YAML Schema Details

### 7.1 Variable Substitution Patterns

```yaml
# Predefined variables (runtime-injected)
$(InstallPath)       # Target installation directory
$(DownloadsPath)     # Temporary downloads location
$(Version)           # Application version
$(WorkspaceRoot)     # Project/workspace root

# Custom variables (user-defined in YAML)
Variables:
  CustomVar: "value"
  EnvironmentPath: "$(InstallPath)\\bin"
```

### 7.2 Timeout and Error Handling

```yaml
# Global timeout overrides individual task timeout
execution:
  timeout: 300000  # All tasks must complete within this

# Per-task timeout
executable:
  timeout: 60000   # This task times out after 60s
  
# Error handling strategies
  continueOnError: false   # Skip remaining tasks if this fails
  
execution:
  stopOnFirstError: false  # Collect all errors, don't stop pipeline
```

### 7.3 Environment Variable Management

```yaml
# Inherit parent environment + override/add
environment:
  LOG_LEVEL: "DEBUG"
  PATH: "$(PATH);$(CustomPath)"  # Extend existing PATH
  CUSTOM_VAR: "custom_value"     # New variable
```

---

## 8. File Organization

```
sdo-launcher/
├── launcher/
│   ├── Launcher.cs                    (existing, refactored)
│   ├── YamlLauncher/
│   │   ├── ILauncherConfigLoader.cs
│   │   ├── YamlLauncherConfigLoader.cs
   │   ├── IStepExecutor.cs
   │   ├── StepExecutor.cs
│   │   ├── SequentialExecutionOrchestrator.cs
│   │   ├── ParallelExecutionOrchestrator.cs
│   │   ├── DependencyResolver.cs
│   │   └── VariableSubstitutionEngine.cs
│   └── Models/
│       ├── LauncherConfig.cs
│       ├── StepConfig.cs
│       ├── ExecutionSettings.cs
│       ├── LaunchResult.cs
│       └── ExecutionResult.cs
├── docs/
│   ├── yaml-launcher-design.md        (this file)
│   ├── yaml-schema-reference.md
│   ├── usage-examples.md
│   └── migration-guide.md
└── tests/
    ├── YamlLauncher.Tests/
    │   ├── YamlConfigLoaderTests.cs
    │   ├── SingleStepTests.cs
    │   ├── SequentialPipelineTests.cs
    │   ├── ParallelExecutionTests.cs
    │   └── fixtures/
    │       ├── single-app.yaml
    │       ├── pipeline.yaml
    │       └── parallel.yaml
```

---

## 9. Migration Path

### 9.1 From Current Process Model

```csharp
// Current code
var process = new Process { StartInfo = new ProcessStartInfo(...) };
process.LockVerifyStart(verbose);

// Transitional (works both ways)
var config = new StepConfig { Path = "...", Arguments = "..." };
var executor = new StepExecutor();
await executor.LaunchAsync(config);

// Future (YAML-based)
await launcher.LaunchAsync("app-config.yaml");
```

### 9.2 Backward Compatibility

- Existing `Launcher` extension methods remain unchanged
- New YAML-based APIs live in separate `YamlLauncher` namespace
- No breaking changes to public API

---

## 10. Success Criteria

- [ ] YAML schema supports single and multiple steps
- [ ] Signature verification integrated into YAML launcher
- [ ] Sequential pipelines with dependency resolution
- [ ] Parallel execution with concurrency control
- [ ] Variable substitution working correctly
- [ ] 90%+ code coverage on core modules
- [ ] Documentation with 10+ usage examples
- [ ] NuGet package published
- [ ] test-framework integration complete
- [ ] sdo deployment scenarios tested

---

## 11. Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| YAML parsing complexity | Use well-tested YamlDotNet library; thorough schema validation |
| Signature verification failures | Fallback to non-signed mode; log detailed diagnostics |
| Timeout handling edge cases | Comprehensive unit tests; configurable timeout behavior |
| Backward compatibility | Keep existing Launcher API; no breaking changes |
| Performance with parallel tasks | Load testing; configurable max concurrency |

---

## 12. Future Extensions (Not in Scope)

- Docker container execution
- Remote execution (SSH, WinRM)
- Distributed orchestration (Kubernetes-like)
- GUI launcher for YAML file editing
- CI/CD pipeline integration helpers
- Monitoring and alerting hooks

