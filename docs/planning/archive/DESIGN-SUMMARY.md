# YAML Launcher Design Summary: Unified Framework with Backward Compatibility

**Date:** May 25, 2026  
**Status:** Design baseline; Phase 1 foundation implemented, later phases remain planned  
**Audience:** Architecture Review, Development Teams

---

## Problem Statement

Three codebases (test-framework, sdo, sdo-launcher) independently solve similar problems:
- **test-framework:** Metadata-driven execution with output capture, assertions, variable extraction
- **sdo:** Currently uses JSON manifests for app installation with verification (will migrate to YAML)
- **sdo-launcher:** Process orchestration with signature verification

**Current State:** 
- Each has its own approach to orchestration
- test-framework's output assertions/variable extraction not available in sdo or sdo-launcher
- No unified way to describe multi-step deployments across repositories
- Duplicated logic for assertion validation, error handling, result passing

**Goal:** Create unified YAML-based launcher that:
1. Supports all existing functionality from three codebases
2. Adds new capabilities (parallel execution, output assertions, variable passing)
3. Maintains 100% backward compatibility
4. Enables cross-repository orchestration

---

## Solution Architecture

### Layered Design

```
┌─────────────────────────────────────────────────────┐
│  YAML Schema                                         │
│  ✅ Backward Compatible (all new features optional) │
└─────────────────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────────────────┐
│  Execution Models                                    │
│  • Simple: Just run executable                      │
│  • Validated: With assertions                       │
│  • Piped: Variables extracted and passed            │
│  • Orchestrated: Complex multi-step pipelines       │
└─────────────────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────────────────┐
│  Core Engines                                        │
│  • YamlLauncherConfigLoader (parsing)               │
│  • StepExecutor (single step)                       │
│  • SequentialExecutionOrchestrator (sequential)     │
│  • ParallelExecutionOrchestrator (parallel)         │
│  • AssertionValidator (output validation)           │
│  • VariableExtractor (regex-based extraction)       │
│  • VariableSubstitutionEngine ($(var) replacement)  │
└─────────────────────────────────────────────────────┘
```

### Backward Compatibility Strategy

#### Level 1: Schema Backward Compatibility
- **Old Code:** All existing YAML configs continue to work unchanged
- **New Code:** Can use optional new properties (assertions, extractVariables, expectedReturnCode)
- **Parser:** Handles both old and new YAML seamlessly

#### Level 2: API Backward Compatibility
- **Existing API:** Current `Launcher.LockVerifyStart()`, `LockStart()`, `Start()` methods remain unchanged
  - All public method signatures stay exactly the same
  - Legacy code requires zero modifications
- **New Planned API:** `IStepExecutor.LaunchAsync(config)` introduced in Phase 1
  - New interface for internal refactoring
  - Existing methods will delegate to this internally in Phase 2
- **Result Models:** Extended with optional fields (AssertionResults, ExtractedVariables)
  - Old code ignores new fields automatically
  - New code can use them for advanced features

#### Level 3: Runtime Behavior Compatibility
- **Simple Case:** Old YAML with no assertions → behaves exactly like before
- **New Case:** New YAML with assertions → validates and fails if assertions fail
- **Mixed:** Sequential pipeline → respects both old and new style tasks

---

## Core Design Decisions

### Decision 1: Optional Features & Alias Support

**Every new feature is optional, and schema supports multiple naming conventions:**
```yaml
# Canonical form (recommended)
steps:
  - path: "app.exe"
    arguments: "--version"

# Alternative: tasks (preferred for test-framework context)
tasks:
  - path: "app.exe"
    arguments: "--version"
    expectedReturnCode: 0
    assertions:
      - type: "output_contains"
        value: "Version"

# Alternative: apps (preferred for sdo context)
apps:
  - path: "app.exe"
    arguments: "--version"
```

**All three forms (`steps`, `tasks`, `apps`) are equivalent and parse to the same internal model.**

**Why?** Allows gradual adoption. Teams can migrate at their own pace.

### Decision 2: Extracted Variables as Dictionary

**Variables flow via execution context:**
```yaml
steps:
  - name: "step1"
    path: "get-version.exe"
    extractVariables:
      - name: "my_version"
        pattern: "Version: (.+)"
        groupIndex: 1
  
  - name: "step2"
    path: "install.exe"
    arguments: "--version $(my_version)"  # Uses extracted value
    dependencies: ["step1"]
```

**Why?** Simple, testable, works for both sequential and parallel with proper ordering.

### Decision 3: Assertion Types Match test-framework

**Assertion types reuse test-framework's proven approach:**
- `output_contains` - Substring search
- `output_not_contains` - Negative assertion
- `output_matches` - Regex pattern
- `exit_code` - Return code validation
- `file_exists` - File presence check
- `custom_json` - JSONPath validation

**Why?** Test-framework already has well-designed, battle-tested assertion types. No need to reinvent.

### Decision 4: Dependency Resolution via Names

**Steps reference dependencies by name (not step number):**
```yaml
steps:
  - name: "download"
    path: "downloader.exe"
  
  - name: "install"
    dependencies: ["download"]  # By name, not index
    path: "installer.exe"
```

**Why?** Easier to reorder, comment out, or insert steps without breaking references.

### Decision 5: Sequential is Default Mode

**Default execution: sequential with dependency ordering:**
```yaml
# No explicit dependencies needed
steps:
  - path: "step1.exe"
  - path: "step2.exe"
  - path: "step3.exe"
```

**Why?** Matches test-framework behavior (steps numbered 1, 2, 3...). Explicit dependencies required only for complex ordering.

---

## YAML Schema Extensions

### Minimal Change (Most Backward Compatible)

```yaml
version: "1.0"  # Already required

# Canonical form
steps:
  - path: "app.exe"
    # Add these optional properties:
    expectedReturnCode: 0          # NEW
    assertions: []                  # NEW
    extractVariables: []            # NEW

# Also valid (test-framework context)
tasks:
  - path: "app.exe"
    expectedReturnCode: 0

# Also valid (sdo context)
apps:
  - path: "app.exe"
    expectedReturnCode: 0
```

### What Stays Unchanged
- `path`, `arguments`, `workingDirectory`, `timeout` properties
- `environment` variable handling
- `verifySignature` digital signature verification
- `dependencies` ordering (already in schema)
- Parallel/sequential execution modes

### What Gets Added
- `expectedReturnCode` - Alternative to implicit 0 assumption
- `assertions` - Output validation rules
- `extractVariables` - Regex-based output capture

---

## Implementation Phases

### Phase 1: Foundation (Weeks 1-2) ✓ **Design Complete**
- Models: StepConfig, AssertionConfig, VariableExtractionConfig
- Parser: YamlLauncherConfigLoader (supports `steps`, `tasks`, `apps` aliases)
- Single step executor: StepExecutor
- Result: LaunchResult with assertion/extraction tracking

### Phase 2: Integration (Week 2) ✓ **Design Complete**
- Refactor existing Launcher.LockVerifyStart() to use new framework
- Ensure backward compatibility
- All legacy code continues to work

### Phase 3: Sequential Pipeline (Week 3) ✓ **Design Complete**
- DependencyResolver - Build execution DAG
- SequentialExecutionOrchestrator - Execute with ordering
- Variable substitution in arguments

### Phase 4: Parallel Execution (Week 3-4) ✓ **Design Complete**
- ParallelExecutionOrchestrator - Concurrent execution
- Thread-safe result collection
- Global timeout management

### Phase 5: Advanced Features (Week 4+) ✓ **Design Complete**
- Variable substitution engine (predefined + custom)
- Retry policies and exponential backoff
- Pre/post execution hooks
- Output filtering

---

## Key Features & Capabilities

### Feature Matrix

| Feature | Description | Use Case |
|---------|-------------|----------|
| **Simple Execution** | Run executable, capture output | Basic app launch |
| **Return Code Validation** | Assert exit code matches expectation | Verify success |
| **Output Assertions** | Validate output contains/matches strings | Test verification |
| **Variable Extraction** | Regex patterns extract values from output | Flow data between steps |
| **Variable Substitution** | `$(variable)` replaced in arguments/paths | Cross-step communication |
| **Sequential Pipeline** | Steps run in order with dependencies | Multi-step deployments |
| **Parallel Execution** | Steps run concurrently (with ordering) | Speed up test runs |
| **Digital Signatures** | Verify executable signature before launch | Security validation |
| **Timeout Management** | Per-task and global timeouts | Prevent hangs |
| **Error Handling** | continueOnError for resilience | Cleanup even if failed |

---

## Test-Framework Compatibility

### Current test-framework Pattern
```yaml
# test-framework metadata format
commands:
  - step: 1
    name: "Get version"
    command: "app.exe"
    args: ["--version"]
    expected_exit_code: 0
    variable_extractions:
      - name: "version"
        pattern: "Version: (\\d+)"
        group_index: 1
    assertions:
      - type: "output_contains"
        value: "Version"
```

### Equivalent YAML Launcher Format
```yaml
# YAML launcher format (unified)
# Note: Can also use 'tasks:' or 'apps:' - all equivalent
steps:
  - name: "Get version"
    path: "app.exe"
    arguments: "--version"
    expectedReturnCode: 0
    extractVariables:
      - name: "version"
        pattern: "Version: (\\d+)"
        groupIndex: 1
    assertions:
      - type: "output_contains"
        value: "Version"
```

**Migration:** Minimal - just rename properties (snake_case → camelCase).

---

## sdo Command Compatibility

### Current sdo Pattern (to migrate)
```bash
sdo install --json tools.json        # Current: JSON manifest (deprecated)
sdo install --name MyApp             # Install from default location
sdo list                             # List available apps
```

### New Unified YAML Format
```yaml
# Simple case: Single step
step:
  path: "sdo.exe"
  arguments: "install --name MyApp"

# Complex deployments: Multi-step orchestration
steps:  # Canonical form; can also use 'tasks' or 'apps'
  - name: "download"
    path: "downloader.exe"
    arguments: "--version 1.0.0"
  
  - name: "install"
    path: "installer.exe"
    dependencies: ["download"]
    expectedReturnCode: 0
    assertions:
      - type: "output_contains"
        value: "Installation complete"
```

**Unified Approach:**
1. **YAML is the single standard format** across all three repositories
2. **Auto-conversion tooling provided** to migrate existing JSON manifests to YAML
3. **Phase 1:** Implement YAML launcher with sdo support
4. **Phase 2:** Provide `json-to-yaml` migration utility
5. **Phase 3:** Gradually deprecate JSON format (keep read support for backward compat)
6. **Result:** All three repos speak same language (YAML), simpler maintenance

---

## sdo-launcher Compatibility

### Current Process-Based Pattern
```csharp
var process = new Process { StartInfo = ... };
process.LockVerifyStart(verbose: true);  // Existing API
```

### New YAML-Based Pattern
```csharp
var executor = new StepExecutor();
var result = await executor.LaunchAsync("deploy.yaml");  // New API
```

### Compatibility Guarantee
```csharp
// This continues to work forever (unchanged)
process.LockVerifyStart(verbose: true);

// This also works (new parallel API)
await launcher.LaunchAsync("deploy.yaml");

// Both approaches coexist in same codebase
```

**Migration:** Optional. Teams can adopt YAML gradually while keeping existing code.

---

## Assertion Types

### Type: output_contains
```yaml
assertions:
  - type: "output_contains"
    value: "Success"
    caseInsensitive: false
```
Checks if stdout contains substring.

### Type: output_not_contains
```yaml
assertions:
  - type: "output_not_contains"
    value: "Error"
    caseInsensitive: true
```
Checks that stdout does NOT contain substring.

### Type: output_matches
```yaml
assertions:
  - type: "output_matches"
    pattern: "^Completed in \\d+ ms$"
```
Checks stdout against regex pattern.

### Type: exit_code
```yaml
assertions:
  - type: "exit_code"
    value: 0
```
Validates process exit code (redundant with expectedReturnCode, but explicit).

### Type: file_exists
```yaml
assertions:
  - type: "file_exists"
    value: "$(InstallPath)\\app.exe"
```
Checks file exists on disk (supports variable substitution).

### Type: custom_json
```yaml
assertions:
  - type: "custom_json"
    jsonPath: "$.status"
    expectedValue: "success"
```
Parses JSON output and validates with JSONPath.

---

## Error Messages & Diagnostics

### Assertion Failure Example
```
❌ Task 'install' failed
   Expected return code: 0
   Actual return code: 1
   
   Assertion 1 FAILED: output_contains
   Expected substring: "Installation complete"
   
   Actual output (first 500 chars):
   ─────────────────────────────────────────
   Preparing installation...
   Error: Insufficient permissions
   ─────────────────────────────────────────
   
   Suggestion: Run installer with elevated privileges
```

### Variable Extraction Failure
```
⚠️  Task 'extract-version' warning
   Variable extraction: version
   Pattern: Version: (\d+\.\d+\.\d+)
   
   Pattern did not match output
   Output searched:
   ─────────────────────────────────────────
   Usage: app --help
   ─────────────────────────────────────────
   
   Note: Variable not extracted, skipping dependent tasks
```

---

## Success Criteria

### Functional Success
- ✅ All current YAML launcher configs work unchanged
- ✅ All test-framework scenarios can be expressed in YAML launcher
- ✅ All sdo deployments can be orchestrated in YAML launcher
- ✅ All sdo-launcher orchestrations can be described in YAML launcher

### API Success
- ✅ IStepExecutor interface unchanged
- ✅ Process.LockVerifyStart() continues to work
- ✅ CliTestExecutor.ExecuteAsync() continues to work
- ✅ Zero breaking changes

### User Success
- ✅ Developers can describe complex deployments in YAML (not code)
- ✅ Test scenarios more maintainable and reusable
- ✅ Variables flow naturally between steps (no manual passing)
- ✅ Clear, actionable error messages on assertion failures

### Quality Success
- ✅ 90%+ unit test coverage
- ✅ Comprehensive integration tests
- ✅ E2E tests with real executables
- ✅ Performance tests for parallel execution

---

## Timeline & Deliverables

### Week 1-2: Phase 1 (Foundation)
- Model classes (LauncherConfig, ExecutableConfig, etc.)
- YAML parsing with YamlDotNet
- Single executable execution
- Unit tests

**Deliverable:** `YamlLauncherConfigLoader` and `StepExecutor` working

### Week 2: Phase 2 (Integration)
- Refactor existing Launcher.cs
- Backward compatibility verified
- All legacy tests passing

**Deliverable:** Existing code continues to work unchanged

### Week 3: Phase 3 (Sequential Pipeline)
- DependencyResolver class
- SequentialExecutionOrchestrator class
- Variable substitution in arguments
- Comprehensive tests

**Deliverable:** Multi-step deployments with variable passing

### Week 3-4: Phase 4 (Parallel Execution)
- ParallelExecutionOrchestrator class
- Thread-safe result collection
- Stress tests with many concurrent tasks

**Deliverable:** Parallel test matrix execution

### Week 4+: Phase 5 (Advanced Features)
- Advanced variable substitution
- Retry policies
- Pre/post hooks
- Output filtering

**Deliverable:** Production-ready advanced features

**Total Timeline:** 8-10 weeks

---

## Documentation Deliverables

✅ **Completed Design Phase:**
1. yaml-launcher-design.md - Complete architecture specification
2. yaml-schema-reference.md - YAML schema and syntax guide
3. implementation-roadmap.md - Phased implementation plan
4. test-framework-integration.md - Integration with test-framework
5. integration-examples.md - Real-world examples for all three repos
6. DESIGN-SUMMARY.md (this file) - Architecture overview

📝 **To Create During Implementation:**
1. API-reference.md - C# API documentation
2. migration-guide.md - How to migrate from old approaches
3. troubleshooting-guide.md - Common issues and solutions
4. performance-tuning.md - Optimization tips
5. sample-configurations/ - Template YAML files

---

## Risk Mitigation

### Risk 1: Breaking Changes
**Mitigation:** All new properties optional. Existing code path unchanged.
- Old YAML → works exactly as before
- New YAML → opt-in new features
- Legacy APIs → remain unchanged

### Risk 2: Performance Regression
**Mitigation:** No additional overhead for simple case (no assertions).
- Assertion validation only if assertions specified
- Variable extraction only if extractVariables specified
- Benchmarking in Phase 4 to validate overhead < 5%

### Risk 3: Adoption Barrier
**Mitigation:** Gradual migration path.
- Phase 1: Just core functionality (no assertions needed)
- Phase 2: Integration (legacy code continues)
- Phase 3-5: Optional advanced features

### Risk 4: Complex Dependency Graphs
**Mitigation:** DependencyResolver validates DAG before execution.
- Circular dependency detection
- Missing dependency errors
- Clear error messages

---

## Questions & Answers

### Q: How is this different from test-framework's metadata?
**A:** This unifies test-framework's approach with sdo's and sdo-launcher's patterns. Test-framework has assertions/extraction; this extends those to all three codebases.

### Q: Will existing test-framework tests still work?
**A:** Yes. Can coexist. MetadataTestExecutor can be compatibility layer or gradually migrated to StepExecutor.

### Q: What about sdo commands that don't fit this pattern?
**A:** Those continue using `sdo install --name MyApp`. YAML launcher is for complex orchestrations, not simple cases.

### Q: Do I have to rewrite my code?
**A:** No. All existing code continues to work. YAML launcher is additive, not replacement.

### Q: What about $(variable) conflicts with environment variables?
**A:** Precedence: Extracted variables > Environment variables > Predefined variables. Explicit rules prevent conflicts.

### Q: Can I use this without assertions?
**A:** Absolutely. Assertions are completely optional. YAML launcher works fine for simple execution.

---

## Next Steps

1. **Architecture Review** (1 day)
   - Review this design with team
   - Approve scope and timeline
   - Identify any concerns

2. **Phase 1 Kickoff** (Day 1 of Week 1)
   - Create model classes
   - Set up YAML parsing
   - Begin unit tests

3. **Continuous Integration** (Ongoing)
   - Weekly sync-ups
   - Integration with existing codebases
   - Test coverage validation

4. **Beta Release** (After Week 4)
   - Early feedback from internal teams
   - Real-world scenario testing
   - Documentation updates

5. **GA Release** (Week 5-6)
   - Official NuGet package
   - Public documentation
   - Team training

---

## Rolling Adoption Strategy: Unified Single Design Across All Repositories

### Final Goal: Single YAML Launcher Framework

This design **enables complete replacement** of existing code in all three repositories with the unified YAML launcher approach. The final requirement is:

> **Within 6 months: All executable orchestration across sdo-launcher, sdo, and test-framework uses the single YAML launcher framework.**

---

### Phase Breakdown: Replacing Legacy Code

#### Phase 1-2: sdo-launcher Transition (Weeks 1-4)
**Current State:** Process.LockVerifyStart() and manual orchestration in C#

**Transition Strategy:**
1. **Week 1-2 (Foundation):** New YAML launcher deployed alongside existing code
   - Existing: `process.LockVerifyStart(verbose)` continues to work
   - New: `await launcher.LaunchAsync("deployment.yaml")` available
   - Status: **Coexistence**

2. **Week 2-4 (Integration & Refactoring):**
   - All internal sdo-launcher orchestrations migrated to YAML
   - Launcher.cs refactored to use new framework internally
   - External API remains unchanged (backward compatible)
   - Status: **Internal Migration Complete**

3. **Post-Week 4 (Documentation):**
   - Deprecation notice: Process-based approach marked for removal in v2.0
   - Migration guide provided for external users
   - New projects default to YAML approach
   - Status: **Legacy Path Documented**

**Requirement:** By end of Week 4, 100% of sdo-launcher's new functionality uses YAML launcher.

---

#### Phase 2-3: sdo Integration (Weeks 2-6)
**Current State:** JSON manifest for simple installs, no complex orchestration

**Transition Strategy:**
1. **Week 2-3:** YAML launcher deployed for sdo with migration tooling
   - New: `sdo install --yaml deployment.yaml` command added
   - Tooling: `sdo migrate --json-to-yaml tools.json` converts JSON manifests to YAML
   - Old: `sdo install --json tools.json` still works (internally uses YAML after conversion)
   - Status: **Single Format Foundation**

2. **Week 3-4:** All complex deployments use YAML
   - Multi-step deployments (download, verify, install, register) use YAML
   - Simple single-app installs can still use JSON (auto-converted)
   - Status: **Unified Internal Format**

3. **Week 5-6:** JSON becomes optional/legacy
   - JSON files automatically converted to YAML at read time
   - `sdo install --json tools.json` transparently converts to YAML
   - Users see no change; implementation unified on YAML
   - Status: **Transparent YAML Foundation**

4. **Post-Week 6:** YAML is primary format
   - Deprecation: JSON input marked for v2.0 removal
   - Migration guide provided for all JSON users
   - New sdo features only support YAML
   - Status: **Pure YAML Format**

**Requirement:** By end of Week 6, all sdo configurations standardized on YAML launcher (with backward-compatible JSON-to-YAML conversion).

---

#### Phase 3-4: test-framework Integration (Weeks 3-8)
**Current State:** Separate MetadataTestExecutor with assertions and extraction

**Transition Strategy:**
1. **Week 3-4:** Compatibility layer established
   - MetadataTestExecutor delegates to StepExecutor
   - Existing test-framework YAML continues to work
   - Behind the scenes: Using new framework
   - Status: **Invisible Migration**

2. **Week 4-6:** New test scenarios use unified schema
   - New tests written in unified YAML format
   - Old tests migrated gradually as maintained
   - Property name migration: snake_case → camelCase
   - Status: **Mixed Mode**

3. **Week 6-8:** Complete consolidation
   - All test-framework scenarios use unified schema
   - MetadataTestExecutor becomes thin wrapper
   - Single assertion/extraction engine across framework
   - Status: **Engine Consolidated**

4. **Post-Week 8:** Pure YAML framework
   - Deprecation: MetadataTestExecutor marked as legacy
   - New tests use StepExecutor directly
   - Status: **Single Framework**

**Requirement:** By end of Week 8, all test-framework test scenarios use unified YAML launcher.

---

### Unified End State (Weeks 1-12)

#### Architecture After Adoption

```
Before (Current - 3 Separate Systems):
┌──────────────────────┐  ┌──────────────┐  ┌─────────────────────┐
│ sdo-launcher      │  │ sdo           │  │ test-framework      │
│ Process API          │  │ JSON Manifest│  │ MetadataTestExecutor│
│ Orchestration Logic  │  │ Commands     │  │ Assertions Engine   │
└──────────────────────┘  └──────────────┘  └─────────────────────┘

After (Unified - Single System):
┌─────────────────────────────────────────────────────────────────┐
│                   YAML Launcher Framework                        │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ Unified YAML Schema                                      │  │
│  │ (assertions, extraction, orchestration, signatures)      │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ Core Engines                                             │  │
│  │ • YamlLauncherConfigLoader (parsing)                     │  │
│  │ • StepExecutor (execution)                            │  │
│  │ • SequentialExecutionOrchestrator (pipelines)            │  │
│  │ • ParallelExecutionOrchestrator (concurrent)             │  │
│  │ • AssertionValidator (output validation)                 │  │
│  │ • VariableExtractor (regex extraction)                   │  │
│  └──────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│ Layered Usage (All using same framework, different interfaces)  │
├──────────────────────┬──────────────────┬──────────────────────┤
│ sdo-launcher      │ sdo               │ test-framework       │
│ IStepExecutor       │ sdoo install       │ StepExecutor         │
│ LaunchAsync(yaml)    │ sdo deploy        │ MetadataTestExecutor │
│ Legacy: Launcher.cs  │ JSON auto-conv   │ Legacy: Metadata     │
└──────────────────────┴──────────────────┴──────────────────────┘
```

---

### Phased Rollout Timeline

```
Week 1-2: Phase 1 - Foundation
├─ YamlLauncherConfigLoader working
├─ StepExecutor for single steps
├─ sdo-launcher: Available alongside Process API
└─ Status: Parallel systems

Week 2-4: Phase 2 - sdo-launcher Integration
├─ Refactor sdo-launcher to use YAML internally
├─ Process.LockVerifyStart() delegates to StepExecutor
├─ sdo: Added `sdo install --yaml`
├─ test-framework: Compatibility layer in place
└─ Status: Internal migration underway

Week 3-6: Phase 3 - Sequential & sdo Full Integration
├─ DependencyResolver and SequentialExecutionOrchestrator
├─ sdo complex deployments use YAML launcher
├─ JSON becomes transparent wrapper
├─ test-framework: New scenarios in unified schema
└─ Status: Two systems consolidating

Week 4-8: Phase 4 - Parallel & test-framework Complete
├─ ParallelExecutionOrchestrator
├─ All test-framework scenarios migrated
├─ MetadataTestExecutor thin wrapper
├─ Unified assertion/extraction engine
└─ Status: Converging to single framework

Week 4-12: Phase 5+ - Advanced Features & Documentation
├─ Variable substitution, retries, hooks, etc.
├─ Cross-repository orchestration tested
├─ Documentation for all three repos
├─ User migration guides published
└─ Status: Production ready

Month 4-6: Adoption & Deprecation
├─ Internal teams fully migrated
├─ External users adopt new framework
├─ Deprecation notices published (v1.1+)
├─ Legacy path marked for v2.0 removal
└─ Status: Single framework dominant
```

---

### Code Replacement Examples

#### sdo-launcher: From Process API to YAML

**Before (Current):**
```csharp
public async Task DeployAsync()
{
    var downloader = new Process { StartInfo = ... };
    downloader.LockVerifyStart(verbose: true);
    
    var installer = new Process { StartInfo = ... };
    installer.LockVerifyStart(verbose: true);
    
    var verifier = new Process { StartInfo = ... };
    verifier.LockVerifyStart(verbose: true);
}
```

**After (Unified YAML):**
```csharp
public async Task DeployAsync()
{
    var executor = new StepExecutor();
    var result = await executor.LaunchAsync("deploy.yaml");
    
    if (!result.Success)
        throw new DeploymentException(result.ErrorMessage);
}
```

**YAML Configuration:**
```yaml
version: "1.0"
steps:  # Or use 'tasks' for test context or 'apps' for deployment
  - name: "download"
    path: "downloader.exe"
    verifySignature: true
    expectedReturnCode: 0
  
  - name: "install"
    path: "installer.exe"
    verifySignature: true
    dependencies: ["download"]
    expectedReturnCode: 0
  
  - name: "verify"
    path: "verifier.exe"
    dependencies: ["install"]
    expectedReturnCode: 0
```

**Result:** 15 lines of C# code → 3 lines of C# code + declarative YAML

---

#### sdoo: From JSON to Unified YAML

**Before (Current):**
```json
{
  "Name": "MyApp",
  "InstallCommand": "powershell.exe",
  "InstallArgs": "-Command Expand-Archive..."
}
```

**After (Transparent):**
```bash
sdo install --yaml myapp-deployment.yaml
# Internally: JSON auto-converted or YAML directly used
```

**YAML Equivalent:**
```yaml
version: "1.0"
steps:  # Can also use 'tasks' or 'apps'
  - name: "download-release"
    path: "gh.exe"
    arguments: "release download..."
    expectedReturnCode: 0
  
  - name: "extract"
    path: "powershell.exe"
    arguments: "-Command Expand-Archive..."
    dependencies: ["download-release"]
```

**Result:** Isolated commands → Multi-step orchestration

---

#### test-framework: From Metadata to Unified

**Before (Current):**
```yaml
# test-framework metadata format
commands:
  - step: 1
    command: "sdo.exe"
    args: ["wi", "list", "--json"]
    variable_extractions:
      - name: "id"
        pattern: '"id":\s*(\d+)'
```

**After (Unified):**
```yaml
# YAML launcher format (same capabilities)
steps:  # Canonical form
  - name: "list-items"
    path: "sdo.exe"
    arguments: "wi list --json"
    extractVariables:
      - name: "id"
        pattern: '"id":\s*(\d+)'
```

**Result:** Separate framework → Part of unified ecosystem

---

### Single Design Requirements

#### Requirement 1: Complete Code Replacement Capability ✅
**Status:** Design enables this
- All features from three codebases supported in YAML launcher
- Legacy code can be removed without functionality loss
- New code can be written entirely in YAML

#### Requirement 2: Backward Compatibility During Transition ✅
**Status:** Design guarantees this
- Old code runs alongside new code
- No breaking changes to public APIs
- Gradual migration path for each repo

#### Requirement 3: Single Unified Framework ✅
**Status:** Design achieves this
- One YAML schema for all three repos
- Single assertion engine
- Single extraction engine
- Single orchestration engine

#### Requirement 4: Cross-Repository Orchestration ✅
**Status:** Design supports this
- Can orchestrate sdo → test-framework scenarios
- Can orchestrate test-framework → sdo-launcher deployments
- Variables flow across repository boundaries

#### Requirement 5: Simplified Codebase Maintenance ✅
**Status:** Design reduces duplication
- One assertion validator (not three)
- One variable extractor (not two)
- One orchestration engine (not three different approaches)
- One documentation set covering all use cases

---

### Success Criteria for Rolling Adoption

**Month 1 (Weeks 1-4):**
- ✅ sdo-launcher fully uses YAML launcher internally
- ✅ sdoo supports YAML deployments
- ✅ test-framework uses unified framework internally
- ✅ All existing code continues to work
- ✅ Zero user-facing breaking changes

**Month 2 (Weeks 5-8):**
- ✅ 80% of new test-framework scenarios in unified schema
- ✅ 90% of sdo-launcher orchestrations in YAML
- ✅ sdo complex deployments use YAML by default
- ✅ Cross-repository orchestration working
- ✅ Documentation updated for unified approach

**Month 3-6 (Weeks 9-12+):**
- ✅ 100% of new code uses unified framework
- ✅ 80% of old code migrated to unified framework
- ✅ Deprecation notices published
- ✅ Public documentation complete
- ✅ User adoption of unified framework established

---

### Rollout Communication Plan

**Week 1:** Internal announcement
- Design review completed
- Unified framework benefits explained
- Timeline shared with all teams

**Week 4:** Phase 1 completion
- sdo-launcher uses YAML internally
- Demo shows code reduction (15 → 3 lines)
- Migration guide published

**Week 8:** Phase 3 completion
- test-framework fully consolidated
- sdo deployments unified
- Cross-repository examples available

**Week 12:** Unified framework launched
- GA release of ntools.Launcher.Yaml NuGet package
- Public documentation complete
- Training complete for all teams

---

## References

- `yaml-launcher-design.md` - Detailed architecture
- `yaml-schema-reference.md` - Complete YAML syntax
- `implementation-roadmap.md` - Implementation tasks
- `test-framework-integration.md` - test-framework specifics
- `integration-examples.md` - Real-world examples
- `test-framework` documentation - Reference implementation maintained in the separate test-framework repository

---

## Approval

- [ ] Architecture Review Approved
- [ ] Timeline Approved  
- [ ] Scope Approved
- [ ] Risk Mitigation Approved

**Reviewers:**
- [ ] Development Team Lead
- [ ] Architecture Lead
- [ ] QA Lead
- [ ] Product Owner

