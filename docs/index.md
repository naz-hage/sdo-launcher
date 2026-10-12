## sdo-launcher

A NuGet package library for launching executables, downloading files, executing shell commands, and checking process elevation.

Used by [ntools](https://github.com/naz-hage/sdo) repo:

- **Nbackup** launches `robocopy` and wait for it to complete, ensuring that the backup process is fully completed before the program continues.
- **Nbuild** launches `msbuild` to build and test .NET projects.  It also builds selected development tools for the project.  It utilizes the `NFile` class to download the tools from the web.
- **Ngit** launches `git` to perform git operations.

### Core Classes

- **Launcher** - Launch processes with digital signature verification and file locking
- **NFile** - Download files with integrity, signature, and VirusTotal checks
- **ShellUtility** - Execute shell commands and resolve file paths
- **CurrentProcess** - Check process elevation status
- **ResultHelper** - Handle process execution results
- **ResultDownload** - Handle download operation results
- **YAML Launcher Framework** - YAML parsing, validation, and single-step process execution with output capture, timeouts, environment variables, and optional signature checks

The package targets .NET 10.0 and uses YamlDotNet 18.0.0 for YAML configuration support. See the [YAML Launcher usage guide](usage.md#yaml-launcher-framework) for the current API and supported configuration fields.