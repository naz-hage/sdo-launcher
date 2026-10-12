# YAML Launcher - Implementation Roadmap & Quick Start

**Date:** September 13, 2026  
**Status:** Phase 1 foundation implemented; orchestration phases remain planned  
**Audience:** sdo-launcher Development Team

---

## Design Summary

Created a unified YAML-based executable launcher framework that:
- Eliminates Process boilerplate code across test-framework, sdoooo, and sdo-launcher
- Supports single executable and multi-task pipelines (sequential and parallel)
- Includes digital signature verification and timeout management
- Enables variable substitution for cross-environment portability
- Provides extensible API for future features

**Documentation Locations:**
- `yaml-launcher-design.md` - Complete design specification
- `yaml-schema-reference.md` - YAML schema and examples

---

## Quick Implementation Roadmap

### Phase 1: Foundation (completed)
**Focus:** Core parsing and single executable launch

```csharp
// Goal: Make this work
var loader = new YamlLauncherConfigLoader();
var executor = new StepExecutor();

var config = await loader.LoadFromFileAsync("single-step.yaml");
var result = await executor.LaunchAsync(config, stepIndex: 0);
```

**Tasks:**
- [x] Add YamlDotNet 18.0.0 NuGet dependency
- [x] Create LauncherConfig, StepConfig, ExecutionSettings, result, assertion, and extraction models
- [x] Implement YamlLauncherConfigLoader with YAML parsing and validation
- [x] Implement StepExecutor for single-step execution
- [x] Add unit tests for YAML parsing and validation
- [x] Add StepExecutor tests for output, errors, timeouts, environment, and working directories

The Phase 1 implementation targets .NET 10.0. The current test suite reports 141 tests: 139 passed, 2 skipped for platform/admin-dependent scenarios, and 0 failed. The generated repository-wide Cobertura report is not a Phase 1-only coverage measurement.

**Files to Create:**
```
launcher/YamlLauncher/
├── ILauncherConfigLoader.cs
├── YamlLauncherConfigLoader.cs
├── IStepExecutor.cs
├── StepExecutor.cs
└── Models/
    ├── LauncherConfig.cs
    ├── StepConfig.cs
    ├── ExecutionSettings.cs
    ├── LaunchResult.cs
    └── ExecutionResult.cs

tests/YamlLauncher.Tests/
├── YamlConfigLoaderTests.cs
├── SingleStepTests.cs
└── fixtures/single-step.yaml
```

### Phase 2: Integration (Week 2)
**Focus:** Extend existing Launcher.cs without breaking changes

```csharp
// Goal: Support both old and new approaches
// Old approach still works
var process = new Process { StartInfo = ... };
process.LockVerifyStart(verbose);

// New approach also works  
await executor.LaunchAsync("app.yaml");
```

**Tasks:**
- [ ] Refactor Launcher.LockVerifyStart() to use new framework internally
- [ ] Refactor Launcher.LockStart() to use new framework internally
- [ ] Add LaunchAsync(string yamlPath) extension method
- [ ] Integrate signature verification with StepExecutor
- [ ] Ensure backward compatibility tests pass
- [ ] Update Launcher.cs documentation

### Phase 3: Sequential Pipeline (Week 3)
**Focus:** Multi-task orchestration with dependencies

```csharp
// Goal: Make this work
await launcher.LaunchAsync("multi-app-pipeline.yaml");

// Results show each task status and dependencies honored
```

**Tasks:**
- [ ] Create DependencyResolver (build DAG of tasks)
- [ ] Create SequentialExecutionOrchestrator
- [ ] Implement task ordering based on dependencies
- [ ] Implement continueOnError and stopOnFirstError handling
- [ ] Add comprehensive tests for pipeline scenarios
- [ ] Test edge cases (circular dependencies, missing deps)

**Files to Create:**
```
launcher/YamlLauncher/
├── DependencyResolver.cs
└── SequentialExecutionOrchestrator.cs

tests/YamlLauncher.Tests/
├── SequentialPipelineTests.cs
└── fixtures/
    ├── pipeline-simple.yaml
    ├── pipeline-with-errors.yaml
    └── pipeline-complex.yaml
```

### Phase 4: Parallel Execution (Week 3-4)
**Focus:** Concurrent task execution with safety

```csharp
// Goal: Make this work with maxConcurrency control
// Three test tasks run in parallel (or sequential order)
await launcher.LaunchAsync("parallel-tests.yaml");
```

**Tasks:**
- [ ] Create ParallelExecutionOrchestrator
- [ ] Implement maxConcurrency throttling
- [ ] Add thread-safe result collection
- [ ] Implement global timeout for all tasks
- [ ] Add stress tests (many concurrent tasks)
- [ ] Performance benchmarking

**Files to Create:**
```
launcher/YamlLauncher/
└── ParallelExecutionOrchestrator.cs

tests/YamlLauncher.Tests/
├── ParallelExecutionTests.cs
├── ConcurrencyTests.cs
└── fixtures/
    ├── parallel-independent.yaml
    └── parallel-stress-test.yaml
```

### Phase 5: Variable Substitution (Week 4)
**Focus:** Cross-environment portability

```yaml
# YAML can reference variables
variables:
  InstallPath: "C:\\Program Files\\MyApp"

task:
  path: "$(InstallPath)\\app.exe"
  arguments: "--config $(InstallPath)\\config.json"
```

**Tasks:**
- [ ] Create VariableSubstitutionEngine
- [ ] Implement predefined variables (InstallPath, Version, etc.)
- [ ] Implement custom variables from YAML
- [ ] Handle variable references in paths, arguments, env vars
- [ ] Add recursive substitution (variables referencing variables)
- [ ] Add tests for all substitution patterns

**Files to Create:**
```
launcher/YamlLauncher/
└── VariableSubstitutionEngine.cs

tests/YamlLauncher.Tests/
└── VariableSubstitutionTests.cs
```

### Phase 6: Advanced Features (Week 5+)
**Focus:** Production readiness

**Tasks:**
- [ ] Add retry policies for transient failures
- [ ] Add pre/post execution hooks
- [ ] Add output filtering and log parsing
- [ ] Add YAML schema validation with helpful error messages
- [ ] Add verbose logging for debugging
- [ ] Performance optimization and profiling

---

## Usage Patterns

### Pattern 1: Single App Deployment

**YAML File** (`deploy-myapp.yaml`):
```yaml
version: "1.0"
description: "Deploy MyApp to production"

task:
  path: "C:\\installers\\myapp-setup.exe"
  arguments: "--install --path C:\\Program Files\\MyApp"
  timeout: 120000
  verifySignature: true

execution:
  verbose: true
```

**C# Code**:
```csharp
var executor = new StepExecutor();
var result = await executor.LaunchAsync(\"deploy-myapp.yaml\");

if (result.Success)
    Console.WriteLine("✓ Deployment complete!");
else
    Console.WriteLine($"✗ Deployment failed: {result.ErrorMessage}");
```

### Pattern 2: CI/CD Pipeline

**YAML File** (`ci-pipeline.yaml`):
```yaml
version: "1.0"
description: "Build, test, and package"

tasks:
  - name: "restore"
    path: "dotnet.exe"
    arguments: "restore"
    timeout: 300000
  
  - name: "build"
    path: "dotnet.exe"
    arguments: "build --configuration Release"
    dependencies: ["restore"]
    timeout: 300000
  
  - name: "test"
    path: "dotnet.exe"
    arguments: "test --no-build"
    dependencies: ["build"]
    timeout: 600000
  
  - name: "pack"
    path: "dotnet.exe"
    arguments: "pack --no-build"
    dependencies: ["test"]
    timeout: 120000

execution:
  mode: "sequential"
  verbose: true
  stopOnFirstError: true
```

### Pattern 3: Test Matrix (Parallel)

**YAML File** (`test-matrix.yaml`):
```yaml
version: "1.0"
description: "Run tests on multiple frameworks in parallel"

tasks:
  - name: "test_net6"
    path: "dotnet.exe"
    arguments: "test --framework net6.0 --no-build"
    timeout: 300000
  
  - name: "test_net7"
    path: "dotnet.exe"
    arguments: "test --framework net7.0 --no-build"
    timeout: 300000
  
  - name: "test_net8"
    path: "dotnet.exe"
    arguments: "test --framework net8.0 --no-build"
    timeout: 300000

execution:
  mode: "parallel"
  maxConcurrency: 3
  stopOnFirstError: false
  verbose: true
```

### Pattern 4: Complex Deployment

**YAML File** (`production-deployment.yaml`):
```yaml
version: "1.0"
description: "Production deployment with validation"

variables:
  AppName: "SuperApp"
  CompanyPath: "C:\\Program Files\\MyCompany"
  AppPath: "$(CompanyPath)\\$(AppName)"
  Version: "2.0.0"

tasks:
  - name: "backup_current"
    path: "C:\\scripts\\backup.exe"
    arguments: "--source $(AppPath) --destination C:\\backups\\$(Version)"
    timeout: 300000
    continueOnError: false
  
  - name: "download_release"
    path: "C:\\tools\\downloader.exe"
    arguments: "--repo mycompany/superapp --version $(Version) --output app.zip"
    dependencies: ["backup_current"]
    timeout: 600000
    continueOnError: false
  
  - name: "extract_release"
    path: "powershell.exe"
    arguments: "-Command Expand-Archive -Path app.zip -DestinationPath extract -Force"
    dependencies: ["download_release"]
    timeout: 120000
    continueOnError: false
  
  - name: "install_release"
    path: "powershell.exe"
    arguments: "-Command Copy-Item extract\\* -Destination $(AppPath) -Recurse -Force"
    dependencies: ["extract_release"]
    timeout: 120000
    verifySignature: false
    continueOnError: false
  
  - name: "run_migrations"
    path: "$(AppPath)\\migrate.exe"
    arguments: "--database prod --version $(Version)"
    dependencies: ["install_release"]
    timeout: 300000
    environment:
      ENV: "production"
    continueOnError: false
  
  - name: "health_check"
    path: "C:\\tools\\health-check.exe"
    arguments: "--app-path $(AppPath) --timeout 30"
    dependencies: ["run_migrations"]
    timeout: 60000
    continueOnError: false
  
  - name: "cleanup_extract"
    path: "powershell.exe"
    arguments: "-Command Remove-Item -Path extract -Recurse -Force"
    dependencies: ["health_check"]
    timeout: 30000
    continueOnError: true  # Always cleanup, even if checks fail

execution:
  mode: "sequential"
  stopOnFirstError: false
  timeout: 1800000  # 30 minutes total
  verbose: true
```

---

## Integration with test-framework

### Current Pattern
```csharp
var executor = new CliTestExecutor("sdo.exe", verbose: true);
var result = await executor.ExecuteAsync("install", new[] { "--name", "MyApp" });
```

### New Pattern (YAML-based)
```yaml
# tests/sdo-scenarios.yaml
version: "1.0"

tasks:
  - name: "test_install_with_name"
    path: "sdo.exe"
    arguments: "install --name MyApp"
    timeout: 120000
    verifySignature: true
  
  - name: "test_list_after_install"
    path: "sdo.exe"
    arguments: "list"
    dependencies: ["test_install_with_name"]
```

```csharp
var executor = new StepExecutor();
var result = await executor.LaunchAsync(\"tests/sdo-scenarios.yaml\");

// Same result object, but organized differently
foreach (var execResult in result.Results)
{
    Console.WriteLine($"{execResult.Name}: {(execResult.Success ? "PASS" : "FAIL")}");
}
```

---

## Integration with sdo Commands

### Before (Current - Manual JSON)
```json
{
  "Name": "deploy-tool",
  "InstallCommand": "powershell.exe",
  "InstallArgs": "-Command Expand-Archive -Path $(Version).zip -DestinationPath $(InstallPath) -Force",
  "InstallPath": "C:\\Program Files\\MyApp"
}
```

### After (YAML-based Orchestration)
```yaml
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
    arguments: "list --json tools.json"
    dependencies: ["install"]
```

---

## Testing Strategy

### Unit Tests
- YAML parsing (valid/invalid YAML)
- Model validation
- Variable substitution patterns
- Dependency resolution (DAG building)

### Integration Tests
- Single executable launch
- Sequential pipeline with dependencies
- Parallel execution
- Error handling (timeouts, failed tasks)
- Signature verification

### Performance Tests
- Large number of parallel tasks
- Variable substitution performance
- YAML parsing overhead
- Memory usage with long-running processes

### E2E Tests
- Real deployments with actual executables
- CI/CD pipeline scenarios
- Complex multi-stage deployments

---

## Backward Compatibility

### Guarantee
Existing Launcher.cs public API remains unchanged and fully functional.

```csharp
// This code continues to work forever
var process = new Process { StartInfo = ... };
var result = process.LockVerifyStart(verbose);  // ✓ Still works
var result = process.LockStart(verbose);        // ✓ Still works
```

### Migration Path (Not Required)
Teams can migrate to YAML-based approach gradually:

```csharp
// Phase 1: Use new API alongside old API
var executor = new StepExecutor();
var yamlResult = await executor.LaunchAsync(\"app.yaml\");

// Phase 2: Deprecate old Process-based code
// Phase 3: Remove old code (major version bump)
```

---

## Documentation Deliverables

**Already Created:**
- ✅ `yaml-launcher-design.md` - Complete design specification
- ✅ `yaml-schema-reference.md` - YAML syntax and schema

**Future Documentation Deliverables:**
- `usage-examples.md` - 20+ real-world examples
- `api-reference.md` - Complete C# API documentation
- `troubleshooting-guide.md` - Common issues and solutions
- `migration-guide.md` - How to migrate from Process to YAML
- `performance-tuning.md` - Optimization tips
- `sample-configurations/` - Template YAML files

---

## NuGet Package Planning

### Package Details
- **Name:** `Ntools.Launcher.Yaml`
- **Version:** 1.0.0-beta1 (for initial release)
- **Dependencies:** YamlDotNet, .NET 6.0+
- **License:** MIT

### Package Contents
- `Ntools.Launcher.dll` (extended with YAML APIs)
- `Ntools.Launcher.Yaml/` namespace with all new classes
- XML documentation for IntelliSense
- Sample YAML configurations

---

## Success Metrics

After implementation, measure:
- Code reuse: % of Process creation replaced with YAML
- Developer time: Reduction in deployment configuration time
- Maintainability: Reduction in deployment script complexity
- Adoption: # of projects using YAML launcher within 6 months
- Test coverage: 90%+ coverage of core modules
- Performance: < 5ms overhead vs direct Process creation

---

## Next Steps

1. Implement dependency resolution and sequential orchestration
2. Add cross-step variable substitution and assertion evaluation
3. Add integration and migration examples for consuming repositories
4. Keep the existing Process-based API backward compatible
5. **Beta Release** - Early feedback from internal teams (2 weeks)
6. **GA Release** - Official NuGet package release (1 week)

**Estimated Total Timeline:** 8-10 weeks

