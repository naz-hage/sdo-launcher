---
name: sdo-launcher-build-instructions
applyTo: "**"
description: "Build and test instructions for sdo-launcher project. Use when: working with the sdo-launcher project - always route build commands to 'sdo build' and test commands to 'sdo test'."
---

# sdo-launcher Build & Test Instructions

## Build Command

When the user asks to build, compile, or check the project:

1. **Always use**: `sdo build` from the `c:\source\sdo-launcher` directory
2. **Never use**: `dotnet build` directly
3. The `sdo build` command uses the custom Nbuild build system defined in `sdo.targets`

Example: If user says "build the project", run:
```bash
cd c:\source\sdo-launcher
sdo build
```

## Test Command

When the user asks to run tests, execute tests, or verify functionality:

1. **Always use**: `sdo test` from the `c:\source\sdo-launcher` directory
2. **Never use**: `dotnet test` directly
3. The `sdo test` command uses the custom Nbuild test system with code coverage collection
4. Test results include:
   - Pass/fail count
   - Code coverage metrics
   - Test execution times

Example: If user says "run tests", run:
```bash
cd c:\source\sdo-launcher
sdo test
```

## Project Structure

- **Source Code**: `launcher/`, `LauncherTests/`
- **Build System**: `sdo.targets`
- **Configuration**: `sdo-launcher.sln`
- **Models**: `launcher/YamlLauncher/Models/`
- **Implementation**: `launcher/YamlLauncher/`

## Test Files Location

- **Unit Tests**: `LauncherTests/YamlLauncher/`
- **All tests must follow**: `*Tests.cs` naming convention
- **Test Framework**: MSTest (.NET 10.0)

## Default Behavior

- When building: Always check compilation first
- When testing: Always run full test suite via `sdo test`
- Code coverage target: 90%+ for implementation files
