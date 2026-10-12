---
applyTo: "**"
---

# Issue/Task Update Instructions

## Overview

This project uses a standardized process for tracking issue implementation status. Issue files are located in `.temp/` folder with the naming pattern `<number>-issue.md`.

## Issue File Format

Each issue file contains:
- **Title**: Issue/task name
- **Description**: What needs to be done
- **Acceptance Criteria**: Checklist of requirements ([ ] or [x])
- **Status**: Current state (🔄 Incomplete, ✅ Done, 🚀 In Progress)
- **Next Steps**: What remains to be done

## Updating Issue Status

Use the **update-issue skill** to automatically verify and update issue status:

```
update issue 35
update task 36
update issue #40
```

The skill will:
1. Read the issue file from `.temp/<number>-issue.md`
2. Check your code changes against the acceptance criteria
3. Mark completed items with `[x]`
4. Identify incomplete items with `[ ]`
5. Update the status (Done ✅ or Incomplete 🔄)
6. List next steps for remaining work

## Workflow

1. **Implement features/fixes** according to acceptance criteria
2. **Commit changes** to your feature branch
3. **Run tests** to verify implementation (`sdo test`)
4. **Update issue** to track progress (`update issue <number>`)
5. **Review changes** in the issue file
6. **Create pull request** when issue is marked Done ✅

## File Location

All issue files must be in: `c:\source\sdo-launcher\.temp\`

Naming pattern: `<number>-issue.md` (e.g., `35-issue.md`, `36-issue.md`)

## Manual Updates

If the skill cannot auto-detect completion:
1. Open `.temp/<number>-issue.md`
2. Update acceptance criteria manually: `[x] Completed item`
3. Update status section
4. Add/remove next steps as needed
5. Save file

## Examples

**Issue 35 - YamlLauncherConfigLoader:**
```
update issue 35
```
Checks if YamlLauncherConfigLoader.cs exists, validates tests pass, marks criteria complete.

**Issue 36 - StepExecutor:**
```
update issue 36
```
Checks if StepExecutor.cs exists, validates all 14 tests pass, updates status to Done.

## Best Practices

- ✅ Update issues after each significant code change
- ✅ Verify tests pass before marking criteria complete
- ✅ Keep acceptance criteria specific and testable
- ✅ Include file paths in status updates for traceability
- ✅ Review next steps before starting new work

- ❌ Don't manually edit status without updating acceptance criteria
- ❌ Don't mark complete without test coverage
- ❌ Don't leave next steps vague
- ❌ Don't forget to commit issue file changes

## Integration with Build System

The issue update process complements the custom build system:
- `sdo build` - Compile code
- `sdo test` - Run tests and verify implementation
- `update issue <number>` - Track progress

This workflow ensures automated tracking of implementation status without manual status updates.
