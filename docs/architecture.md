# NTools Launcher Architecture

Core library functions:

```mermaid
graph TB
    subgraph "NTools Launcher Library"
        subgraph "Core Classes"
            L[Launcher<br/>Process launching<br/>with security checks]
            NF[Nfile<br/>Secure file downloading<br/>with integrity checks]
            SU[ShellUtility<br/>Shell command execution<br/>Path resolution]
            CP[CurrentProcess<br/>Process elevation<br/>checking]
        end

        subgraph "Result Classes"
            RH[ResultHelper<br/>Process execution results<br/>Output handling]
            RD[ResultDownload<br/>Download operation results<br/>File validation results]
        end

        subgraph "Security Components"
            SV[SignatureVerifier<br/>Digital signature<br/>verification]
            VT[VirusTotalChecker<br/>Virus scanning<br/>integration]
        end
    end

    subgraph "External Dependencies"
        SC[System.Security.Cryptography<br/>Cryptographic operations]
        SP[System.Security.Principal<br/>Process elevation checks]
        SJ[System.Text.Json<br/>JSON processing]
        NP[System.Net<br/>HTTP operations]
        IO[System.IO<br/>File system operations]
    end

    L --> SV
    L --> RH
    NF --> RD
    NF --> SV
    SU --> RH

    L --> SC
    NF --> SC
    SV --> SC
    VT --> SJ
    NF --> NP
    L --> IO
    CP --> SP

    style L fill:#e1f5fe
    style NF fill:#f3e5f5
    style SU fill:#e8f5e8
    style CP fill:#fff3e0
    style RH fill:#fce4ec
    style RD fill:#f1f8e9
    style SV fill:#e0f2f1
    style VT fill:#f9fbe7
```

## Core Components

### Launcher Class
- **Purpose**: Main class for launching processes with security features
- **Key Features**:
  - Process launching with optional digital signature verification
  - File locking during execution
  - Asynchronous process launching
  - Thread-safe operations
- **Methods**:
  - `LaunchAsync()` - Launch process asynchronously
  - `LaunchAndWait()` - Launch and wait for completion
  - `LockAndLaunch()` - Lock file and launch with verification

### YAML Launcher Stages

`LauncherConfig` contains the canonical `steps` collection and an optional
`stages` collection. Each `StageConfig` names an ordered list of existing
steps. `StepExecutor.LaunchStageAsync` resolves and validates every reference
before executing the steps sequentially, returning the same aggregated
`LaunchResult` used by multi-step index execution.

### Nfile Class
- **Purpose**: Secure file downloading with integrity and security checks
- **Key Features**:
  - HTTP/HTTPS file downloads
  - Digital signature verification
  - File size validation
  - HTTPS certificate validation through `HttpClientHandler`
  - Allowed extension and trusted-host checks
- **Methods**:
  - `DownloadAsync()` - Download and validate a file
  - `SetTrustedHosts()` - Configure permitted hosts
  - `SetAllowedExtensions()` - Configure permitted extensions

`VirusTotalChecker` is a separate opt-in component and is not called by
`Nfile.DownloadAsync`.

### ShellUtility Class
- **Purpose**: Execute shell commands and resolve file paths
- **Key Features**:
  - Command execution with output capture
  - Path environment variable resolution
  - Error handling and result reporting
- **Methods**:
  - `ExecuteCommand()` - Execute shell commands
  - `GetFullPath()` - Resolve full file paths from PATH

### CurrentProcess Class
- **Purpose**: Check process elevation status
- **Key Features**:
  - Windows administrator privilege detection
  - Cross-platform compatibility considerations
- **Methods**:
  - `IsElevated()` - Check if current process is elevated

## Security Components

### SignatureVerifier Class
- **Purpose**: Digital signature verification for executables and files
- **Key Features**:
  - Authenticode signature validation
  - Certificate chain verification
  - Timestamp validation
- **Dependencies**: System.Security.Cryptography.Pkcs

### VirusTotalChecker Class
- **Purpose**: Virus scanning integration with VirusTotal API
- **Key Features**:
  - API key authentication
  - File hash submission and scanning
  - Result parsing and reporting
- **Dependencies**: System.Net, System.Text.Json

## Result Classes

### ResultHelper Class
- **Purpose**: Handle results from process execution operations
- **Properties**:
  - `Code` - Exit code from executed process
  - `Output` - List of output lines
  - `Success` - Boolean success indicator
- **Methods**:
  - `GetFirstOutput()` - Get first line of output
  - `HasOutput()` - Check if output exists

### ResultDownload Class
- **Purpose**: Handle results from file download operations
- **Properties**:
  - `FileSize` - Size of downloaded file
  - `X509Certificate2` - Certificate if verified
  - `Code` - Result code
  - `Output` - Operation output
- **Methods**:
  - `IsSuccess()` - Check if download succeeded
  - `DisplayCertificate()` - Show certificate information

## File Structure

```
sdo-launcher/
├── sdo-launcher.sln           # Main solution file
├── mkdocs.yml                    # Documentation configuration
├── README.md                     # Project documentation
├── targets.md                    # Build targets documentation
├── coverage.cobertura.xml        # Test coverage report
├── sdo.targets                # MSBuild targets
├── unit-tests.targets            # Unit test targets
├── e2e-tests.targets             # E2E test targets
│
├── launcher/                     # Main library project
│   ├── Launcher.csproj
│   ├── Launcher.cs               # Main Launcher class
│   ├── Nfile.cs                  # File download functionality
│   ├── ShellUtility.cs           # Shell command execution
│   ├── CurrentProcess.cs         # Process elevation checking
│   ├── ResultHelper.cs           # Process result handling
│   ├── ResultDownload.cs         # Download result handling
│   ├── SignatureVerifier.cs      # Digital signature verification
│   ├── VirusTotalChecker.cs      # VirusTotal integration
│   │
│   └── YamlLauncher/             # YAML Launcher framework
│       └── Models/               # Configuration and result models
│           ├── LauncherConfig.cs         # Main configuration
│           ├── StepConfig.cs             # Step configuration
│           ├── ExecutionSettings.cs      # Execution settings
│           ├── Assertion.cs              # Assertion rules
│           ├── VariableExtraction.cs     # Variable extraction rules
│           ├── LaunchResult.cs           # Execution result
│           ├── ExecutionResult.cs        # Step result
│           ├── AssertionResult.cs        # Assertion outcome
│           └── ExtractionResult.cs       # Extraction outcome
│
├── LauncherTest/                 # Test project
│   ├── LauncherTest.csproj
│   └── LauncherTest.cs           # Basic tests
│
├── LauncherTests/                # Comprehensive test suite
│   ├── LauncherTests.csproj
│   ├── LauncherTests.cs          # Launcher class tests
│   ├── NFileTests.cs             # NFile class tests
│   ├── ResultDownloadTests.cs    # ResultDownload tests
│   ├── ResultHelperTests.cs      # ResultHelper tests
│   ├── ShellUtilityTests.cs      # ShellUtility tests
│   ├── SignatureVerifierTests.cs # SignatureVerifier tests
│   └── VirusTotalCheckerTests.cs # VirusTotalChecker tests
│
├── docs/                         # Documentation
│   ├── index.md
│   ├── installation.md
│   ├── usage.md
│   ├── devguide/
│   │   ├── setup.md
│   │   ├── dependencies.md
│   │   ├── build-and-test.md
│   │   └── targets.md
│   └── revisions/
│
└── .github/                      # GitHub configuration
    ├── workflows/
    │   └── dotnet-desktop.yml    # CI/CD pipeline
    └── validation/               # Configuration validation
```

## Dependencies

### Runtime Dependencies
- **.NET 10.0**: Target framework for execution
- **Windows OS**: Primary platform support
- **Internet Access**: Required for VirusTotal API and file downloads

### Development Dependencies
- **MSTest v4.0.2**: Testing framework
- **Microsoft.NET.Test.Sdk v18.0.1**: Test SDK
- **System.Security.Cryptography.Pkcs v10.0.1**: Cryptographic operations
- **System.Text.Json v10.0.1**: JSON processing
- **System.Security.Principal.Windows v5.0.0**: Process elevation checks
- **YamlDotNet v18.0.0**: YAML serialization/deserialization for launcher configuration

## Security Considerations

### Digital Signature Verification
- All executable launches can optionally verify Authenticode signatures
- Certificate chain validation ensures trust hierarchy
- Timestamp validation prevents replay attacks

### File Download Security
- HTTPS-only downloads (configurable)
- File hash verification against known good values
- VirusTotal integration for malware detection
- File size validation to prevent resource exhaustion

### Process Elevation
- Secure checking of administrator privileges
- No elevation escalation - only detection
- Cross-platform considerations for future expansion

## Testing Strategy

### Unit Tests
- Comprehensive test coverage for all public methods
- Mock external dependencies (VirusTotal API, file system)
- Test both success and failure scenarios
- Parallel test execution with `[DoNotParallelize]` where needed

### Integration Tests
- End-to-end process launching scenarios
- Real file download testing (with safe URLs)
- Shell command execution validation

### Security Testing
- Digital signature validation testing
- Certificate chain testing
- VirusTotal API integration testing

## Build and Deployment

### Build Process
- MSBuild-based compilation using nbuild system
- Multi-target framework support (.NET 10.0)
- NuGet package generation for distribution
- Test execution with coverage reporting

### Distribution
- **NuGet Package**: `sdo-launcher` on nuget.org
- **GitHub Releases**: Source code and documentation
- **Documentation Site**: MkDocs-generated static site

## YAML Launcher Framework Architecture

### Overview
The YAML Launcher Framework extends sdo-launcher with a declarative configuration system for orchestrating complex executable execution workflows without ceremony-heavy Process setup code.

### Model Classes (YamlLauncher.Models namespace)

#### Configuration Models
These models represent the YAML configuration structure:

**LauncherConfig**
- Main container for complete launcher configuration
- Properties: version, description, execution settings, variables, steps
- Alias properties: `steps`, `tasks`, `apps` (all equivalent for backward compatibility)
- Supports YAML deserialization with YamlDotNet

**StepConfig**
- Individual step/task/app configuration
- Properties: path, arguments, name, dependencies, continueOnError, expectedReturnCode
- Nested support for assertions and variable extraction
- Replaces legacy ExecutableConfig pattern

**ExecutionSettings**
- Controls execution behavior
- Properties: mode (Sequential/Parallel), verbose, stopOnFirstError, maxConcurrency, timeout
- ExecutionMode enum with Sequential (0) and Parallel (1) values

**Assertion** & **VariableExtraction**
- Assertion: Validates execution results (exitCode, stdout, JSON paths, regex patterns)
- VariableExtraction: Extracts values from results (pattern, groupIndex, caseInsensitive)

#### Result Models
These models represent execution outcomes:

**LaunchResult**
- Overall execution result container
- Properties: success, results (list of ExecutionResult), extractedVariables, errorMessage

**ExecutionResult**
- Result of a single step execution
- Properties: name, success, exitCode, standardOutput, standardError
- Nested: assertions (List<AssertionResult>), extractedVariables (List<ExtractionResult>)

**AssertionResult** & **ExtractionResult**
- AssertionResult: Assertion validation outcome (type, passed, message, expectedValue, actualValue)
- ExtractionResult: Variable extraction outcome (name, value, success, message)

### Integration Points

```
┌─────────────────────────────────────────────────────────────┐
│              YAML Configuration File                        │
│     (Using YamlDotNet deserializer with camelCase)         │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ Deserialize
                 ▼
┌─────────────────────────────────────────────────────────────┐
│         LauncherConfig Model                                │
│  ├─ ExecutionSettings (Sequential/Parallel)                │
│  ├─ List<StepConfig> (with dependencies, assertions)      │
│  └─ Dictionary<string, string> Variables                  │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ Validate and execute selected step
                 ▼
┌─────────────────────────────────────────────────────────────┐
│      YAML Launcher Services                                 │
│  YamlLauncherConfigLoader -> StepExecutor -> Launcher.Services.ConsoleHelper │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ Results
                 ▼
┌─────────────────────────────────────────────────────────────┐
│         LaunchResult Model                                  │
│  ├─ List<ExecutionResult>                                 │
│  │  ├─ AssertionResults                                   │
│  │  └─ ExtractionResults                                  │
│  └─ Dictionary<string, string> ExtractedVariables         │
└─────────────────────────────────────────────────────────────┘
```

### Alias Properties (Backward Compatibility)

LauncherConfig supports three property names that all reference the same underlying `steps` collection:

```csharp
config.Steps  // Canonical form (recommended)
config.Tasks  // Alias for test-framework integration
config.Apps   // Alias for sdo integration context
```

This enables:
- test-framework projects to use "tasks" in YAML
- sdo projects to use "apps" in YAML
- All configurations to share the same model layer

### YAML Schema Support

All three property names work interchangeably:

```yaml
version: '1.0'
execution:
  mode: Sequential

# Any of these work:
steps:      # Modern, recommended
  - name: step1
    path: /usr/bin/cmd

tasks:      # test-framework compatibility
  - name: task1
    path: /usr/bin/test

apps:       # sdo/ntools compatibility
  - name: app1
    path: /usr/bin/deploy
```

### Key Features

- **Type-Safe Configuration**: C# models validated by `LauncherConfigValidator`
- **YAML Native**: Native YAML support via YamlDotNet
- **Flexible Step Execution**: `StepExecutor` selects a step by unique name or zero-based index, executes multiple indexed steps sequentially in requested order, captures output, validates exit codes, applies environment and working-directory settings, and enforces timeouts
- **Console Output**: `Launcher.Services.ConsoleHelper` provides colored status output directly to the console; configurable logging sinks are not currently supported
- **Extensible Design**: Model-based architecture leaves room for dependency orchestration, variable substitution, retries, and hooks

## Future Enhancements

### Platform Support
- Linux and macOS support expansion
- Cross-platform process launching
- Platform-specific security features

### Feature Additions
- Additional antivirus integrations
- Advanced process monitoring
- Download resume capabilities
- Batch operation support

### Performance Optimizations
- Async operation improvements
- Memory usage optimization
- Concurrent download support</content>
<parameter name="filePath">c:\source\sdo-launcher\docs\architecture.md