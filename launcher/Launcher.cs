using Launcher.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Ntools
{
    public static class Launcher 
    {
        /// <summary>
        /// Locks the executable, verifies its digital signature, and starts it. Bare executable names are resolved from PATH.
        /// </summary>
        /// <param name="process">The Process object representing the executable to launch.</param>
        /// <param name="verbose">Whether or not to output additional verbose messages.</param>
        /// <returns>A ResultHelper object containing the exit code and, if redirectStandardOutput is true, any output from the executable.</returns>
        public static ResultHelper LockVerifyStart(this Process process, bool verbose)
        {
            ResultHelper result;

            // verify that working directory is path rooted and exists
            if (!Path.IsPathRooted(process.StartInfo.WorkingDirectory) ||
                !Directory.Exists(process.StartInfo.WorkingDirectory))
            {
                return ResultHelper.Fail(-1, $"Working directory '{process.StartInfo.WorkingDirectory}' is not path rooted");
            }

            var executable = ResolveExecutablePath(process.StartInfo);
            if (executable is null)
            {
                var expectedPath = Path.Combine(process.StartInfo.WorkingDirectory, process.StartInfo.FileName);
                return ResultHelper.Fail(-1, $"File '{expectedPath}' not found");
            }

            // Lock the file for reading
            using (FileStream fileStream = new(executable, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Check for valid digital signature
                if (!SignatureVerifier.VerifyDigitalSignature(fileStream.Name))
                {
                    result = ResultHelper.Fail(-1, $"File '{executable}' is not digitally signed");
                }
                else
                {
                    process.StartInfo.FileName = executable;
                    // Start the process.
                    result = process.Start(verbose);
                }
            }

            return result;
        }

        /// <summary>
        /// Locks the executable and starts it. Bare executable names are resolved from PATH.
        /// </summary>
        /// <param name="process">The Process object representing the executable to launch.</param>
        /// <param name="verbose">Whether or not to output additional verbose messages.</param>
        /// <returns>A ResultHelper object containing the exit code and, if redirectStandardOutput is true, any output from the executable.</returns>
        public static ResultHelper LockStart(this Process process, bool verbose)
        {
            ResultHelper result;

            // verify that working directory is path rooted and exists
            if (!Path.IsPathRooted(process.StartInfo.WorkingDirectory) ||
                !Directory.Exists(process.StartInfo.WorkingDirectory))
            {
                return ResultHelper.Fail(-1, $"Working directory {process.StartInfo.WorkingDirectory} is not path rooted");
            }

            var executable = ResolveExecutablePath(process.StartInfo);
            if (executable is null)
            {
                var expectedPath = Path.Combine(process.StartInfo.WorkingDirectory, process.StartInfo.FileName);
                return ResultHelper.Fail(-1, $"File {expectedPath} not found");
            }

            // Lock the file for reading
            using (FileStream fileStream = new(executable, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                process.StartInfo.FileName = executable;
                // Start the process.
                result = process.Start(verbose);
            }


            return result;
        }

        private static string ResolveExecutablePath(ProcessStartInfo startInfo)
        {
            var fileName = startInfo.FileName;
            var workingDirectory = startInfo.WorkingDirectory;
            var localPath = Path.GetFullPath(fileName, workingDirectory);
            if (File.Exists(localPath))
            {
                return localPath;
            }

            if (Path.IsPathRooted(fileName) || !string.IsNullOrEmpty(Path.GetDirectoryName(fileName)))
            {
                return null;
            }

            var pathValue = startInfo.Environment.TryGetValue("PATH", out var path)
                ? path
                : string.Empty;
            var extensions = GetExecutableExtensions(startInfo);

            foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var pathDirectory = directory.Trim().Trim('"');
                if (pathDirectory.Length == 0)
                {
                    continue;
                }

                foreach (var extension in extensions)
                {
                    var candidate = Path.GetFullPath(Path.Combine(pathDirectory, fileName + extension), workingDirectory);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static string[] GetExecutableExtensions(ProcessStartInfo startInfo)
        {
            if (Path.HasExtension(startInfo.FileName))
            {
                return [string.Empty];
            }

            var pathExtValue = startInfo.Environment.TryGetValue("PATHEXT", out var pathExt)
                ? pathExt
                : null;
            var extensions = string.IsNullOrWhiteSpace(pathExtValue)
                ? new[] { ".COM", ".EXE", ".BAT", ".CMD" }
                : pathExtValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            return new[] { string.Empty }.Concat(extensions.Select(extension =>
                extension.StartsWith('.') ? extension : $".{extension}")).ToArray();
        }

        /// <summary>
        /// Starts the process and handles the output redirection or waiting for the process to exit.
        /// </summary>
        /// <param name="process">The Process object representing the executable to launch.</param>
        /// <param name="verbose">Whether or not to output additional verbose messages.</param>
        /// <returns>A ResultHelper object containing the exit code and, if redirectStandardOutput is true, any output from the executable.</returns>
        private static ResultHelper Start(this Process process, bool verbose)
        {
            var result = ResultHelper.New();

            // preserve current directory
            var currentDir = Directory.GetCurrentDirectory();
            
            try
            {
                // Set the working directory to the working directory.
                Directory.SetCurrentDirectory(process.StartInfo.WorkingDirectory);
                if (verbose)
                {
                    ConsoleHelper.WriteVerbose ($"Working Directory when launching Process: {Directory.GetCurrentDirectory()}");
                }

                if (process.Start())
                {
                    // If redirectStandardOutput is true, read any output from the executable and add it to the result object's Output property.
                    if (process.StartInfo.RedirectStandardOutput && process.StartInfo.RedirectStandardError)
                    {
                        result.Output.AddRange(process.StandardOutput.ReadToEnd()
                                        .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                        result.Output.AddRange(process.StandardError.ReadToEnd()
                                       .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                    }
                    else
                    {
                        // If redirectStandardOutput is false, wait for the process to exit and add the exit code to the result object's Code property.
                        process.WaitForExit();
                    }
                }
                else
                {
                    result = ResultHelper.Fail(-1, $"Failed to start {process.StartInfo.FileName}");
                }

                // display exit code and process.output
                if (verbose)
                {
                    ConsoleHelper.WriteVerbose ($" -Code: {process.ExitCode}");
                    ConsoleHelper.WriteVerbose ($" -Output:");
                    foreach (var line in result.Output)
                    {
                        ConsoleHelper.WriteVerbose ($"   {line}");
                    }
                }

                result.Code = process.ExitCode;
                if (result.Code != 0 && !process.StartInfo.RedirectStandardOutput)
                {
                    result.Output.Add($"ProcessStart Exception: {process.ExitCode}");
                }

                // restore current directory
                Directory.SetCurrentDirectory(currentDir);
                if (verbose)
                {
                    ConsoleHelper.WriteVerbose ($"Working Directory After launching Process: {Directory.GetCurrentDirectory()}");
                }

            }
            catch (Exception ex)
            {
                result = ResultHelper.Fail(-1, $"Exception: {ex.Message}");
            }

            // check if current directory is restored
            if (Directory.GetCurrentDirectory() != currentDir)
            {
                if (result.Code == 0)
                {
                    result = ResultHelper.Fail(-1, $"Unable to restore current directory to {currentDir}");
                }
                else
                {
                    result.Output.Add($"Unable to restore current directory to {currentDir}");
                }
            }
            
            return result;
        }

        /// <summary>
        /// Launches a process with the specified parameters.
        /// </summary>
        /// <param name="parameters">The Parameters object containing the launch parameters.</param>
        /// <returns>A ResultHelper object containing the exit code and, if redirectStandardOutput is true, any output from the executable.</returns>
        
        /// <summary>
        /// Launch in thread and exit
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="workingDir"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static ResultHelper LaunchInThread(string workingDir, string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDir,
                Arguments = arguments,
                UseShellExecute = true,
                RedirectStandardOutput = false,
                CreateNoWindow = true,
            };

            try
            {
                var executable = $"{workingDir}\\{fileName}";
                if (!File.Exists(executable))
                {
                    return ResultHelper.Fail(message: $"File {executable} not found");
                }
                new Thread(() =>
                {
                    try
                    {
                        Thread.CurrentThread.IsBackground = true;
                        Process.Start(startInfo);
                        ConsoleHelper.WriteInfo($"Started {startInfo.FileName} {startInfo.Arguments}");
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }).Start();

                return ResultHelper.Success();
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Exception: {ex.Message}");
                return ResultHelper.Fail(message: ex.Message);
            }
        }
    }
}
