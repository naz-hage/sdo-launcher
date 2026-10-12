# Steps/Tasks/Apps Terminology Migration - Status Update

**Date:** May 25, 2026  
**Status:** Core terminology updates completed across design and work items

---

## Summary of Changes

### ✅ Completed Updates

#### 1. DESIGN-SUMMARY.md
- Added comprehensive "Decision 1: Optional Features & Alias Support" section
- Updated YAML examples to show `steps:`, `tasks:`, and `apps:` as equivalent forms
- Clarified that all three forms parse to the same internal model
- Updated Phase 1 description to mention alias support
- Feature matrix updated to reference "Steps" instead of "Tasks"

#### 2. yaml-launcher-design.md
- Changed title from "Executable Launcher" to "Steps Launcher"
- Updated section 3.1 from "Single Executable (Basic)" to "Single Step (Basic)"
- Updated section 3.2 from "Multiple Executables (Sequence)" to "Multiple Steps (Sequence)"
- Updated section 3.3 from "Parallel Executables" to "Parallel Steps"
- Updated YAML examples to use canonical `steps:` form with note about `tasks:` and `apps:` aliases
- Updated model to show:
  - `StepConfig` class name
  - Three alias properties: `Steps`, `Tasks` (test-framework), `Apps` (sdooo)
- Updated section headers to reference "steps" terminology
- Updated code examples to use `StepConfig` and reference canonical naming

#### 3. yaml-schema-reference.md
- Updated "Quick Reference" section to show `steps:`, `tasks:`, and `apps:` as equivalent
- Updated section headers for minimal and full configurations
- Expanded full configuration YAML example with all new features (assertions, extractVariables, etc.)
- Updated root-level schema table to document three alias properties
- Added note: "Use ONE of `steps`, `tasks`, or `apps` - all are equivalent"

#### 4. Work Items (.temp/*.md)
- wi-02: Updated to reference `StepConfig` and `steps/tasks/apps` aliases
- wi-02: Added schema support documentation
- wi-04: Renamed from "ExecutableLauncher" to "StepExecutor"
- wi-04: Updated all references to use new naming
- wi-06: Updated algorithm description to reference "steps" instead of "executables"

---

## Still Need Review/Update

### 1. implementation-roadmap.md
- Contains multiple references to "executables"
- Need to update examples and descriptions to use `steps:`
- Update Phase deliverables list

### 2. test-framework-integration.md
- Reference implementations showing `tasks:` examples
- May need clarification about aliases
- ExecutableConfig → StepConfig references

### 3. integration-examples.md
- Real-world examples using old terminology
- Need YAML updates to show canonical form with aliases

### 4. ARCHITECTURAL-DECISIONS.md
- Decision 4 specifically about "Dependency Resolution via Names" and "Executables"
- May need wording updates for consistency

### 5. DESIGN-PACKAGE-INDEX.md
- Index file may reference old terminology

---

## Design Philosophy: Aliases Approach

### Why This Works
1. **Canonical form (`steps:`)** - Clear, neutral, industry-standard
2. **Test-framework alias (`tasks:`)** - Natural for sequential test context
3. **sdooo alias (`apps:`)** - Natural for application deployment context
4. **Parser support** - All three parse to identical internal model (List<StepConfig>)
5. **Backward compatibility** - Old YAML with `executables:` can upgrade gradually

### User Experience
- **New users:** Learn canonical `steps:` form
- **test-framework developers:** Can use familiar `tasks:` in their configs
- **sdo developers:** Can use familiar `apps:` in their configs
- **Unified internally:** No duplication of code or logic

### Model Structure
```csharp
public class LauncherConfig
{
    public List<StepConfig>? Steps { get; set; }
    public List<StepConfig>? Tasks { get; set; }    // Alias
    public List<StepConfig>? Apps { get; set; }     // Alias
}
```

All three properties parse to the same execution engine.

---

## Next Steps

1. **Review completed changes** in updated design documents
2. **Update remaining documents** following same pattern:
   - Replace `executables:` with `steps:`
   - Show aliases where appropriate for context
   - Update `ExecutableConfig` → `StepConfig` class names
3. **Document alias parsing** in YamlLauncherConfigLoader implementation guide
4. **Add to work items:** Requirement to support all three alias forms in YAML parser
5. **Test:** Verify YAML parser handles all three forms identically

---

## File Locations

```
c:\source\sdo-launcher\docs\planning\
├── DESIGN-SUMMARY.md                    ✅ Updated
├── yaml-launcher-design.md              ✅ Updated  
├── yaml-schema-reference.md             ✅ Updated
├── implementation-roadmap.md            ⏳ Needs update
├── test-framework-integration.md        ⏳ Needs review
├── integration-examples.md              ⏳ Needs update
├── ARCHITECTURAL-DECISIONS.md           ⏳ Needs review
└── DESIGN-PACKAGE-INDEX.md              ⏳ Needs review

c:\source\sdo-launcher\.temp\
├── wi-01-add-yamldotnet-dependency.md           ✅ Current
├── wi-02-create-model-classes.md               ✅ Updated
├── wi-03-implement-yaml-config-loader.md       ⏳ Needs update
├── wi-04-implement-executable-launcher.md      ✅ Updated
├── wi-05-phase1-unit-tests.md                  ⏳ Needs update
├── wi-06-dependency-resolver.md                ✅ Updated
├── wi-07-sequential-orchestrator.md            ⏳ Needs update
├── wi-08-refactor-lockverifystart.md           ⏳ Needs update
├── wi-09-assertion-validator.md                ⏳ Needs update
├── wi-10-variable-extractor.md                 ⏳ Needs update
└── wi-11-variable-substitution-engine.md       ⏳ Needs update
```

---

## Terminology Translation Reference

| Old Term | New Term | Context |
|----------|----------|---------|
| `executable:` / `executables:` | `steps:` / `tasks:` / `apps:` | YAML configuration |
| `ExecutableConfig` | `StepConfig` | C# model class |
| "Single Executable" | "Single Step" | Feature description |
| "Multiple Executables" | "Multiple Steps" or "Step Sequence" | Feature description |
| "Executable launching" | "Step execution" or "Task execution" | Operation description |

---

**Prepared by:** Design Team  
**Review:** Check that terminology is consistent across all three contexts (canonical, test-framework, sdo)
