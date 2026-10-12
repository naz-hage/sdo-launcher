# Integration Examples: test-framework, sdooo, sdo-launcher

This document shows concrete examples of how test-framework scenarios, sdooo deployments, and sdo-launcher orchestrations can be unified under the YAML launcher schema with assertion and result passing support.

---

## Example 1: test-framework Scenario (SDO Multi-Step Test)

### Current test-framework YAML (metadata-driven testing)

**File:** `sdo-e2e-test/metadata/test-wi-workflow.yaml`

```yaml
# test-framework format (existing)
name: "Validate SDO Work Item Workflow"
description: "Start work, create branch, verify, cleanup"

commands:
  - step: 1
    name: "List Work Items"
    command: "sdo.exe"
    args: ["wi", "list", "--top", "1", "--json"]
    expected_exit_code: 0
    variable_extractions:
      - name: "work_item_id"
        pattern: '"id":\s*(\d+)'
        group_index: 1
    assertions:
      - type: "exit_code"
        value: 0

  - step: 2
    name: "Start Work on Item"
    command: "sdo.exe"
    args: ["wi", "start", "{work_item_id}", "--verbose"]
    expected_exit_code: 0
    variable_extractions:
      - name: "branch_name"
        pattern: "Branch: (\\S+)"
        group_index: 1
    assertions:
      - type: "output_contains"
        value: "Branch:"
      - type: "output_not_contains"
        value: "error"

  - step: 3
    name: "Verify Branch Exists"
    command: "git.exe"
    args: ["branch", "-a"]
    expected_exit_code: 0
    assertions:
      - type: "git_branch_exists"
        branch: "{branch_name}"

  - step: 4
    name: "Cleanup Branch"
    command: "git.exe"
    args: ["branch", "-D", "{branch_name}"]
    expected_exit_code: 0
```

### Equivalent YAML Launcher Schema (New - With Full Compatibility)

```yaml
# yaml-launcher format (unified approach)
# Note: Use 'tasks:' for test-framework context or 'apps:' for sdoo context
version: "1.0"
description: "Validate SDO Work Item Workflow"

steps:
  - name: "list-work-items"
    path: "sdo.exe"
    arguments: "wi list --top 1 --json"
    expectedReturnCode: 0
    assertions:
      - type: "exit_code"
        value: 0
    extractVariables:
      - name: "work_item_id"
        pattern: '"id":\s*(\d+)'
        groupIndex: 1
        description: "Extract work item ID from JSON output"

  - name: "start-work"
    path: "sdo.exe"
    arguments: "wi start $(work_item_id) --verbose"
    dependencies: ["list-work-items"]
    expectedReturnCode: 0
    assertions:
      - type: "output_contains"
        value: "Branch:"
        description: "Verify branch creation message"
      - type: "output_not_contains"
        value: "error"
        caseInsensitive: true
        description: "Ensure no error messages"
    extractVariables:
      - name: "branch_name"
        pattern: "Branch: (\\S+)"
        groupIndex: 1
        description: "Extract branch name from output"

  - name: "verify-branch"
    path: "git.exe"
    arguments: "branch -a"
    dependencies: ["start-work"]
    expectedReturnCode: 0
    assertions:
      - type: "output_contains"
        value: "$(branch_name)"
        description: "Verify branch exists in git output"

  - name: "cleanup-branch"
    path: "git.exe"
    arguments: "branch -D $(branch_name)"
    dependencies: ["verify-branch"]
    expectedReturnCode: 0
    continueOnError: true
    description: "Delete feature branch (always run for cleanup)"

execution:
  mode: "sequential"
  stopOnFirstError: false
  verbose: true
```

### Benefits of Unified Approach

1. **Same execution engine:** Uses YAML launcher instead of separate MetadataTestExecutor
2. **Variables flow naturally:** `$(work_item_id)` and `$(branch_name)` available across steps
3. **Assertions are standardized:** `output_contains`, `output_not_contains` work everywhere
4. **Cleaner syntax:** Consistent property names (camelCase, no underscores)
5. **Better error messages:** Unified validation engine provides detailed assertion failure info

---

## Example 2: sdo Installation with Verification

### Current Pattern (sdo with Manual JSON)

**File:** `tools.json`
```json
{
  "Name": "MyDeploymentTool",
  "InstallCommand": "powershell.exe",
  "InstallArgs": "-Command Expand-Archive -Path $(Version).zip -DestinationPath $(InstallPath) -Force",
  "InstallPath": "C:\\Program Files\\MyDeploymentTool",
  "Version": "1.2.0"
}
```

**Usage:**
```powershell
sdo install --json tools.json
```

### Equivalent YAML Launcher Schema (New)

```yaml
version: "1.0"
description: "Deploy MyDeploymentTool with verification"

variables:
  AppName: "MyDeploymentTool"
  InstallPath: "C:\\Program Files\\MyDeploymentTool"
  Version: "1.2.0"
  DownloadUrl: "https://releases.example.com/$(AppName)-$(Version).zip"

steps:  # Or use 'tasks' or 'apps' - all equivalent
  - name: "download-release"
    path: "powershell.exe"
    arguments: '-Command "Invoke-WebRequest -Uri $(DownloadUrl) -OutFile $(Version).zip"'
    expectedReturnCode: 0
    assertions:
      - type: "file_exists"
        value: "$(Version).zip"
        description: "Download completed successfully"

  - name: "verify-signature"
    path: "signtool.exe"
    arguments: 'verify /pa $(Version).zip'
    dependencies: ["download-release"]
    expectedReturnCode: 0
    assertions:
      - type: "output_contains"
        value: "Successfully verified"
        description: "Digital signature valid"

  - name: "extract-release"
    path: "powershell.exe"
    arguments: '-Command "Expand-Archive -Path $(Version).zip -DestinationPath $(InstallPath) -Force"'
    dependencies: ["verify-signature"]
    expectedReturnCode: 0
    assertions:
      - type: "file_exists"
        value: "$(InstallPath)\\app.exe"
        description: "Application extracted successfully"

  - name: "run-installer"
    path: "$(InstallPath)\\installer.exe"
    arguments: "--install --path $(InstallPath) --silent"
    dependencies: ["extract-release"]
    expectedReturnCode: 0
    timeout: 300000
    assertions:
      - type: "output_contains"
        value: "Installation complete"
        description: "Installer finished"
    extractVariables:
      - name: "install_version"
        pattern: "Installed version: ([\\d.]+)"
        groupIndex: 1
        description: "Extract actual installed version"

  - name: "verify-installation"
    path: "$(InstallPath)\\app.exe"
    arguments: "--version"
    dependencies: ["run-installer"]
    expectedReturnCode: 0
    assertions:
      - type: "output_contains"
        value: "Version $(install_version)"
        description: "Verify installed version matches"

  - name: "register-paths"
    path: "powershell.exe"
    arguments: '-Command "[Environment]::SetEnvironmentVariable(''PATH'', \'$([Environment]::GetEnvironmentVariable(''PATH'')) + '';$(InstallPath)'\'', ''Machine'')"'
    dependencies: ["verify-installation"]
    expectedReturnCode: 0
    continueOnError: true
    description: "Add to system PATH (may fail if already added)"

execution:
  mode: "sequential"
  stopOnFirstError: false
  verbose: true
```

### Benefits of Unified Approach

1. **Multi-step deployment:** Not just `sdo install --name MyApp`, but complete orchestration
2. **Verification at each step:** Download, signature check, extract, install, verify all validated
3. **Extracted values:** Actual installed version extracted and verified in final step
4. **Flexible:** Can add pre/post steps, rollback logic, notifications
5. **Auditable:** Complete trace of what happened at each step with assertion results

---

## Example 3: sdo-launcher Process Orchestration

### Current Pattern (Direct Process.LockVerifyStart)

**C# Code:**
```csharp
public async Task DeployAsync()
{
    // Download
    var downloadProcess = CreateDownloadProcess();
    downloadProcess.LockVerifyStart(verbose: true);
    
    // Install
    var installProcess = CreateInstallProcess();
    installProcess.LockVerifyStart(verbose: true);
    
    // Verify
    var verifyProcess = CreateVerifyProcess();
    verifyProcess.LockVerifyStart(verbose: true);
}
```

### Equivalent YAML Launcher Schema (New)

```yaml
version: "1.0"
description: "Complex sdo-launcher orchestration"

variables:
  WorkspaceRoot: "C:\\workspace"
  BuildOutputPath: "$(WorkspaceRoot)\\build\\output"
  PublishPath: "$(WorkspaceRoot)\\publish"
  ToolsPath: "C:\\tools"

steps:  # Or use 'tasks' or 'apps'
  - name: "clean-build"
    path: "powershell.exe"
    arguments: "-Command Remove-Item -Path $(BuildOutputPath) -Recurse -Force -ErrorAction SilentlyContinue"
    expectedReturnCode: 0
    continueOnError: true
    description: "Clean previous build output"

  - name: "build"
    path: "dotnet.exe"
    arguments: "build --configuration Release --output $(BuildOutputPath)"
    workingDirectory: "$(WorkspaceRoot)"
    dependencies: ["clean-build"]
    expectedReturnCode: 0
    timeout: 600000
    assertions:
      - type: "output_contains"
        value: "Build succeeded"
        description: "Build completed successfully"
    extractVariables:
      - name: "build_version"
        pattern: "AssemblyVersion: (\\d+\\.\\d+\\.\\d+)"
        groupIndex: 1
        description: "Extract assembly version"

  - name: "run-tests"
    path: "dotnet.exe"
    arguments: "test --configuration Release --no-build --logger trx"
    workingDirectory: "$(WorkspaceRoot)"
    dependencies: ["build"]
    expectedReturnCode: 0
    timeout: 600000
    assertions:
      - type: "output_contains"
        value: "passed"
        caseInsensitive: true
        description: "Tests passed"
      - type: "output_not_contains"
        value: "failed"
        caseInsensitive: true
        description: "No test failures"

  - name: "publish"
    path: "dotnet.exe"
    arguments: "publish --configuration Release --output $(PublishPath)"
    workingDirectory: "$(WorkspaceRoot)"
    dependencies: ["run-tests"]
    expectedReturnCode: 0
    timeout: 300000
    assertions:
      - type: "output_contains"
        value: "succeeded"
        description: "Publish succeeded"

  - name: "sign-executable"
    path: "signtool.exe"
    arguments: "sign /f cert.pfx /p password /t http://timestamp.server $(PublishPath)\\app.exe"
    dependencies: ["publish"]
    expectedReturnCode: 0
    timeout: 120000
    assertions:
      - type: "output_contains"
        value: "Successfully signed"
        description: "Code signed successfully"
    verifySignature: true
    continueOnError: false

  - name: "package"
    path: "powershell.exe"
    arguments: '-Command "Compress-Archive -Path $(PublishPath)\\* -DestinationPath app-$(build_version).zip -Force"'
    dependencies: ["sign-executable"]
    expectedReturnCode: 0
    assertions:
      - type: "file_exists"
        value: "app-$(build_version).zip"
        description: "Package created"

  - name: "upload-artifacts"
    path: "powershell.exe"
    arguments: "-Command \"Copy-Item app-$(build_version).zip -Destination $(ToolsPath)\\releases\\\""
    dependencies: ["package"]
    expectedReturnCode: 0
    continueOnError: false
    description: "Upload to releases folder"

  - name: "cleanup"
    path: "powershell.exe"
    arguments: "-Command \"Remove-Item -Path $(PublishPath) -Recurse -Force\""
    dependencies: ["upload-artifacts"]
    expectedReturnCode: 0
    continueOnError: true
    description: "Cleanup temporary files"

execution:
  mode: "sequential"
  stopOnFirstError: false
  verbose: true
  timeout: 3600000  # 1 hour global timeout
```

### Benefits of Unified Approach

1. **Pure YAML instead of C# code:** No need to write deployment logic in code
2. **Reusable:** Same YAML can be run from CLI, CI/CD, or programmatically
3. **Auditable:** Complete deployment trace with assertion results
4. **Version tracking:** Extracted build_version used in all downstream steps
5. **Error resilience:** continueOnError allows cleanup even if build fails
6. **Timeout management:** Global timeout plus per-step timeouts
7. **Digital signatures:** Built-in signature verification via verifySignature property

---

## Example 4: Parallel Testing (All Frameworks Simultaneously)

### Current Approach (Manual Threading)

```csharp
public async Task RunTestsAsync()
{
    var tasks = new List<Task>
    {
        RunTestsAsync("net6.0"),
        RunTestsAsync("net7.0"),
        RunTestsAsync("net8.0")
    };
    
    await Task.WhenAll(tasks);
}
```

### YAML Launcher Schema (Declarative)

```yaml
version: "1.0"
description: "Test suite on multiple frameworks in parallel"

variables:
  ProjectPath: "C:\\src\\MyProject"
  TestProject: "MyProject.Tests.csproj"

steps:  # Or use 'tasks' for test context
  - name: "restore"
    path: "dotnet.exe"
    arguments: "restore"
    workingDirectory: "$(ProjectPath)"
    expectedReturnCode: 0
    timeout: 300000

  - name: "build"
    path: "dotnet.exe"
    arguments: "build --configuration Release --no-restore"
    workingDirectory: "$(ProjectPath)"
    dependencies: ["restore"]
    expectedReturnCode: 0
    timeout: 300000

  - name: "test_net6"
    path: "dotnet.exe"
    arguments: "test $(TestProject) --framework net6.0 --no-build --logger trx"
    workingDirectory: "$(ProjectPath)"
    dependencies: ["build"]
    expectedReturnCode: 0
    timeout: 600000
    assertions:
      - type: "output_contains"
        value: "passed"
        caseInsensitive: true

  - name: "test_net7"
    path: "dotnet.exe"
    arguments: "test $(TestProject) --framework net7.0 --no-build --logger trx"
    workingDirectory: "$(ProjectPath)"
    dependencies: ["build"]
    expectedReturnCode: 0
    timeout: 600000
    assertions:
      - type: "output_contains"
        value: "passed"
        caseInsensitive: true

  - name: "test_net8"
    path: "dotnet.exe"
    arguments: "test $(TestProject) --framework net8.0 --no-build --logger trx"
    workingDirectory: "$(ProjectPath)"
    dependencies: ["build"]
    expectedReturnCode: 0
    timeout: 600000
    assertions:
      - type: "output_contains"
        value: "passed"
        caseInsensitive: true

execution:
  mode: "sequential"  # restore → build → all tests in parallel
  stopOnFirstError: false
  verbose: true
```

**Execution Plan:**
- Step 1: `restore` runs
- Step 2: `build` runs (after restore)
- Step 3-5: `test_net6`, `test_net7`, `test_net8` run **in parallel** (all after build, but concurrent)
- Each test can take 10 minutes; parallel execution cuts total from 30 min to ~10 min

---

## Example 5: Cross-Repository Coordination

### Complex Scenario: Build, Test, Deploy Chain

```yaml
version: "1.0"
description: "Build ntools, test sdo, deploy to staging"

variables:
  NtoolsRepo: "C:\\repos\\ntools"
  SdoRepo: "C:\\repos\\sdo-e2e-test"
  StagingPath: "\\\\staging.example.com\\apps"
  Version: "2.0.0"

steps:  # Or use 'apps' for deployment context
  - name: "ntools_build"
    path: "dotnet.exe"
    arguments: "build --configuration Release"
    workingDirectory: "$(NtoolsRepo)"
    expectedReturnCode: 0
    timeout: 300000
    assertions:
      - type: "output_contains"
        value: "Build succeeded"
    extractVariables:
      - name: "ntools_version"
        pattern: "Version: ([\\d.]+)"
        groupIndex: 1

  - name: "ntools_package"
    path: "dotnet.exe"
    arguments: "pack --configuration Release"
    workingDirectory: "$(NtoolsRepo)"
    dependencies: ["ntools_build"]
    expectedReturnCode: 0
    timeout: 120000
    assertions:
      - type: "file_exists"
        value: "$(NtoolsRepo)\\bin\\Release\\ntools.$(ntools_version).nupkg"

  - name: "sdo_tests"
    path: "dotnet.exe"
    arguments: "test --configuration Release"
    workingDirectory: "$(SdoRepo)"
    expectedReturnCode: 0
    timeout: 900000  # Long timeout for e2e tests
    assertions:
      - type: "output_contains"
        value: "passed"

  - name: "deploy_staging"
    path: "powershell.exe"
    arguments: "-Command \"Copy-Item $(NtoolsRepo)\\bin\\Release\\* -Destination $(StagingPath) -Recurse -Force\""
    dependencies: ["ntools_package", "sdo_tests"]  # Both must complete first
    expectedReturnCode: 0
    timeout: 120000
    continueOnError: false

  - name: "verify_deployment"
    path: "powershell.exe"
    arguments: "-Command \"& '$(StagingPath)\\ntools.exe' --version | Select-Object -First 1\""
    dependencies: ["deploy_staging"]
    expectedReturnCode: 0
    assertions:
      - type: "output_contains"
        value: "$(ntools_version)"
        description: "Staging has correct version"

execution:
  mode: "sequential"
  stopOnFirstError: true
  verbose: true
  timeout: 2000000  # 33 minutes total
```

**Execution Flow:**
1. Build ntools (extract version)
2. Package ntools (uses extracted version in filename)
3. Run SDO tests (can run in parallel with packaging)
4. Deploy to staging (after both complete)
5. Verify version in staging

---

## Schema Comparison Table

| Capability | test-framework | sdo | sdo-launcher | YAML Launcher (Unified) |
|-----------|---|---|---|---|
| Execute executable | ✅ | ✅ | ✅ | ✅ |
| Capture output | ✅ | ✅ | ✅ | ✅ |
| Check return code | ✅ | ❓ | ✅ | ✅ |
| Output assertions | ✅ | ❌ | ❌ | ✅ |
| Variable extraction | ✅ | ❌ | ❌ | ✅ |
| Variable passing | ✅ | ❌ | ❌ | ✅ |
| Sequential pipelines | ✅ | ✅ (implicit) | ✅ (manual) | ✅ |
| Parallel execution | ❌ | ❌ | ❌ | ✅ |
| Digital signatures | ❌ | ✅ | ✅ | ✅ |
| Timeout management | ✅ | ✅ | ✅ | ✅ |
| Dependency resolution | ✅ | ✅ (implicit) | ✅ (manual) | ✅ (explicit DAG) |
| **Unified format** | **No** | **No** | **No** | **✅ Yes** |

---

## Migration Path by Repository

### test-framework
1. Continue using existing metadata format for now
2. Gradually migrate test scenarios to YAML launcher format
3. Use MetadataTestExecutor → StepExecutor compatibility layer
4. Phase out separate metadata files, consolidate into YAML launcher

### sdo
1. Current JSON manifest format remains for backward compatibility
2. New complex deployments described in YAML launcher format
3. `sdo deploy --yaml deployment-plan.yaml` new command
4. JSON approach becomes "simple case" of YAML launcher

### sdo-launcher
1. New IStepExecutor interface consumes YAML launcher configs
2. Existing Process.LockVerifyStart() calls can be replaced with YAML
3. Complex multi-step orchestrations now declarative instead of imperative
4. Pure code-based approach becomes optional for legacy code

---

## Success Metrics

After implementation:
- ✅ 90%+ of test-framework scenarios expressible in YAML launcher
- ✅ Complex sdo deployments simplified from code to YAML config
- ✅ sdo-launcher users have declarative alternative to imperative code
- ✅ Variables flowing between steps reduces configuration duplication
- ✅ Unified assertion language across all three codebases
- ✅ 100% backward compatibility - existing code/configs still work
- ✅ Zero breaking changes to public APIs

