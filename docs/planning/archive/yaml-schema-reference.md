# YAML Launcher Schema Reference

This document provides the complete YAML schema specification for the sdo-launcher executable configuration format.

---

## Quick Reference

### Minimal Configuration (Single Step with Canonical Form)

```yaml
version: "1.0"
steps:
  - path: "C:\\Program Files\\MyApp\\app.exe"
execution:
  verbose: false
```

*Also valid using aliases:*
```yaml
# Test-framework context
tasks:
  - path: "app.exe"

# sdooooo context
apps:
  - path: "app.exe"
```

### Full Configuration (All Options)

```yaml
version: "1.0"
description: "Complete example with all options"

steps:                         # Or use 'tasks' or 'apps' (all equivalent)
  - name: "deploy"
    path: "C:\\app.exe"
    arguments: "--config prod"
    workingDirectory: "C:\\work"
    timeout: 30000
    verifySignature: true
    redirectOutput: true
    expectedReturnCode: 0
    environment:
      LOG_LEVEL: "DEBUG"
    assertions:
      - type: "output_contains"
        value: "Success"
    extractVariables:
      - name: "version"
        pattern: "v(.+)"
        groupIndex: 1

execution:
  mode: "sequential"
  verbose: true
  
variables:
  AppPath: "C:\\Program Files\\MyApp"
```

---

## Complete Schema

### Root Level

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `version` | string | Yes | - | Schema version (currently "1.0") |
| `description` | string | No | "" | Human-readable description of the configuration |
| `steps` | StepConfig[] | * | - | Execution steps (canonical form, mutually exclusive with `tasks` or `apps`) |
| `tasks` | StepConfig[] | * | - | Execution steps (test-framework alias, equivalent to `steps`) |
| `apps` | StepConfig[] | * | - | Execution steps (sdo alias, equivalent to `steps`) |
| `execution` | ExecutionSettings | Yes | - | Execution mode and options |
| `variables` | Dictionary<string, string> | No | {} | Custom variable substitutions |

*Note: Use ONE of `steps`, `tasks`, or `apps` - all are equivalent*

---

## StepConfig Object

```yaml
- name: "string"                    # Optional: Identifier for this step
  path: "string"                    # Required: Full path or name of executable
  arguments: "string"               # Optional: Command-line arguments
  workingDirectory: "string"        # Optional: Working directory for process
  
  environment:                      # Optional: Environment variable overrides
    KEY: "value"
    ANOTHER_KEY: "$(EXISTING_VAR)"  # Can reference existing env vars
  
  timeout: 30000                    # Optional: Milliseconds before timeout (0 = no timeout)
  verifySignature: false            # Optional: Require valid digital signature (Windows only)
  redirectOutput: true              # Optional: Capture stdout/stderr
  
  dependencies: ["step1", "step2"] # Optional: Step names to complete first (sequential only)
  continueOnError: false            # Optional: Continue to next step even if this fails
```

### StepConfig Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `name` | string | No | auto-generated | Unique identifier for this step |
| `path` | string | Yes | - | Full path to executable or executable name (searches PATH) |
| `arguments` | string | No | "" | Command-line arguments (supports variable substitution) |
| `workingDirectory` | string | No | current dir | Working directory for process (supports variable substitution) |
| `environment` | object | No | {} | Environment variable overrides (each value supports substitution) |
| `timeout` | int | No | 30000 | Timeout in milliseconds; 0 = infinite |
| `verifySignature` | bool | No | false | Require valid digital signature (Windows only) |
| `redirectOutput` | bool | No | true | Capture stdout and stderr to results |
| `dependencies` | string[] | No | [] | Step names that must complete first (sequential mode only) |
| `continueOnError` | bool | No | false | Continue pipeline even if this step fails |

---

## ExecutionSettings Object

```yaml
execution:
  mode: "sequential"        # parallel | sequential
  verbose: true             # Enable detailed logging
  stopOnFirstError: true    # Stop entire pipeline on first failure
  maxConcurrency: 4         # Max parallel tasks (parallel mode only)
  timeout: 300000           # Global timeout for all tasks (0 = infinite)
```

### ExecutionSettings Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `mode` | string | No | "sequential" | Execution mode: "sequential" or "parallel" |
| `verbose` | bool | No | false | Enable verbose logging output |
| `stopOnFirstError` | bool | No | true | Stop entire pipeline/batch on first failure |
| `maxConcurrency` | int | No | 1 | Maximum parallel tasks (parallel mode only) |
| `timeout` | int | No | 300000 | Global timeout in milliseconds (0 = infinite) |

---

## Variable Substitution

### Predefined Variables (Injected at Runtime)

```yaml
$(InstallPath)     # Target installation directory
$(DownloadsPath)   # Temporary downloads directory
$(Version)         # Application version
$(WorkspaceRoot)   # Project or workspace root directory
$(TempPath)        # System temp directory
$(EnvironmentPath) # Custom environment-specific path
$(PATH)            # Current PATH environment variable
$(HOME)            # User home directory
$(USERNAME)        # Current username
$(COMPUTERNAME)    # Computer name
```

### Custom Variables

```yaml
variables:
  MyCustomVar: "C:\\Custom\\Path"
  AppConfig: "$(InstallPath)\\config.json"  # Can reference other variables

task:
  path: "$(MyCustomVar)\\app.exe"
  workingDirectory: "$(AppConfig)"
```

### Environment Variable Reference

```yaml
environment:
  LOG_PATH: "$(TEMP)\\logs"           # Reference system env var
  CUSTOM_PATH: "$(CustomVar)"         # Reference YAML custom var
  COMBINED: "$(PATH);C:\\extra\\bin"  # Extend existing PATH
```

---

## Execution Modes

### Sequential Mode (Default)

Tasks execute one after another. Next task starts only when previous completes.

```yaml
execution:
  mode: "sequential"
  stopOnFirstError: true   # Stop on first failure
```

**Dependency Behavior:**
- `dependencies` field specifies which tasks must complete first
- If not specified, tasks execute in order defined
- If dependency fails and `continueOnError: false`, dependent tasks skipped

### Parallel Mode

Multiple tasks execute concurrently up to `maxConcurrency` limit.

```yaml
execution:
  mode: "parallel"
  maxConcurrency: 4
  stopOnFirstError: false  # Collect all results
```

**Note:** `dependencies` field ignored in parallel mode; use `mode: "sequential"` for task ordering.

---

## Error Handling Scenarios

### Scenario 1: Fail Fast (Sequential)
```yaml
execution:
  mode: "sequential"
  stopOnFirstError: true

tasks:
  - name: "task1"
    continueOnError: false  # Task stops pipeline on failure
  - name: "task2"          # Won't execute if task1 fails
```
**Result:** task2 skipped if task1 fails

### Scenario 2: Resilient Pipeline (Sequential)
```yaml
execution:
  mode: "sequential"
  stopOnFirstError: false  # Pipeline continues

tasks:
  - name: "backup"
    continueOnError: true   # Pipeline continues even if backup fails
  - name: "deploy"         # Always executes
  - name: "cleanup"        # Always executes (cleanup task)
```
**Result:** All tasks execute regardless of failures

### Scenario 3: Parallel with Failures
```yaml
execution:
  mode: "parallel"
  maxConcurrency: 3
  stopOnFirstError: false

tasks:
  - name: "test_unit"
  - name: "test_integration"
  - name: "lint"
```
**Result:** All three run in parallel; failures don't stop others

---

## Timeout Behavior

### Task-Level Timeout
```yaml
task:
  path: "long-running-task.exe"
  timeout: 60000  # This task times out after 60 seconds
```

### Global Timeout
```yaml
execution:
  timeout: 300000  # All tasks must complete within 5 minutes
```

### No Timeout
```yaml
task:
  timeout: 0  # Task never times out
  
execution:
  timeout: 0  # No global timeout
```

---

## Signature Verification

### Single Executable (Signed)
```yaml
task:
  path: "C:\\Program Files\\MyApp\\app.exe"
  verifySignature: true  # Fails if not properly signed
  
execution:
  verbose: true  # Shows signature verification details
```

### Multiple Executables (Mixed)
```yaml
tasks:
  - name: "core_app"
    path: "C:\\Program Files\\core.exe"
    verifySignature: true   # Must be signed
  
  - name: "helper_script"
    path: "C:\\scripts\\helper.bat"
    verifySignature: false  # Optional
```

---

## Complete Examples

### Example 1: Simple Single Executable

```yaml
version: "1.0"
description: "Deploy MyApp"

task:
  path: "C:\\Programs\\installer.exe"
  arguments: "--install --path C:\\MyApp"
  workingDirectory: "C:\\Temp"
  timeout: 120000
  verifySignature: true

execution:
  verbose: true
```

### Example 2: Installation Pipeline

```yaml
version: "1.0"
description: "Three-stage installation pipeline"

tasks:
  - name: "download"
    path: "C:\\tools\\downloader.exe"
    arguments: "--url https://example.com/app.zip --output app.zip"
    timeout: 300000
    continueOnError: false
  
  - name: "extract"
    path: "C:\\Windows\\System32\\tar.exe"
    arguments: "-xf app.zip"
    dependencies: ["download"]
    workingDirectory: "C:\\Temp"
    timeout: 60000
    continueOnError: false
  
  - name: "install"
    path: "C:\\Temp\\app\\installer.exe"
    arguments: "--silent --destination $(InstallPath)"
    dependencies: ["extract"]
    environment:
      INSTALL_MODE: "production"
    timeout: 120000
    verifySignature: true
    continueOnError: false
  
  - name: "cleanup"
    path: "C:\\Windows\\System32\\cmd.exe"
    arguments: "/c rmdir /s /q C:\\Temp\\app"
    dependencies: ["install"]
    continueOnError: true  # Cleanup never fails the pipeline

execution:
  mode: "sequential"
  verbose: true
  stopOnFirstError: false
  timeout: 600000  # 10 minute total

variables:
  InstallPath: "C:\\Program Files\\MyCompany\\MyApp"
```

### Example 3: Parallel Testing

```yaml
version: "1.0"
description: "Run tests in parallel"

tasks:
  - name: "unit_tests"
    path: "dotnet.exe"
    arguments: "test --filter Category=Unit --no-build"
    timeout: 120000
  
  - name: "integration_tests"
    path: "dotnet.exe"
    arguments: "test --filter Category=Integration --no-build"
    timeout: 300000
  
  - name: "code_analysis"
    path: "dotnet.exe"
    arguments: "format --verify-no-changes"
    timeout: 60000

execution:
  mode: "parallel"
  maxConcurrency: 3
  stopOnFirstError: false
  verbose: true
```

### Example 4: With Custom Variables

```yaml
version: "1.0"
description: "Deployment with variable substitution"

variables:
  CompanyName: "ACME"
  AppName: "SuperApp"
  Environment: "production"
  InstallRoot: "C:\\Program Files"
  AppPath: "$(InstallRoot)\\$(CompanyName)\\$(AppName)"
  LogPath: "$(TEMP)\\$(AppName)\\logs"

tasks:
  - name: "prepare_directories"
    path: "C:\\Windows\\System32\\cmd.exe"
    arguments: "/c mkdir $(AppPath) && mkdir $(LogPath)"
    continueOnError: false
  
  - name: "copy_binaries"
    path: "C:\\Windows\\System32\\robocopy.exe"
    arguments: ".\\_build $(AppPath)\\bin /S /E"
    workingDirectory: "C:\\deployment"
    continueOnError: false
  
  - name: "set_config"
    path: "C:\\tools\\config-updater.exe"
    arguments: "--app $(AppPath) --env $(Environment) --logs $(LogPath)"
    environment:
      CONFIG_PATH: "$(AppPath)\\config"
    continueOnError: false

execution:
  mode: "sequential"
  verbose: true
```

---

## Validation Rules

- `version` must be "1.0"
- Either `executable` OR `executables` required (not both, not neither)
- Executable `path` must be valid and accessible
- `timeout` values must be non-negative integers
- `maxConcurrency` must be >= 1
- Task `name` values must be unique within executables array
- Dependency names must refer to existing tasks
- Variable names should be alphanumeric with underscores

---

## Best Practices

1. **Always name tasks** for clear logging and error messages
2. **Use variables** for paths to enable cross-environment portability
3. **Set appropriate timeouts** to catch hung processes
4. **Enable signature verification** for production executables
5. **Use `continueOnError: true` for cleanup tasks** to ensure they always run
6. **Keep descriptions informative** for maintenance and documentation
7. **Test YAML locally** before deploying to production
8. **Log output** by keeping `redirectOutput: true` for debugging

