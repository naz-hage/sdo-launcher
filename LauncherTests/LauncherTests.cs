using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;

namespace Ntools.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class LauncherTests
    {
        private const string ExcecutableToLaunch = "LauncherTest.exe";

        [TestMethod]
        public void ProcessTestRobocopy()
        {
            // Arrange
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
                    FileName = "robocopy.exe",
                    Arguments = "/?",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = false,
                    UseShellExecute = false
                }
            };

            // Act
            var result = process.LockStart(true);

            // Assert
            Assert.AreEqual(16, result.Code);
            Assert.IsGreaterThan(100, result.Output.Count);
        }

        [TestMethod]
        public void LaunchInThreadTest()
        {
            var result = Ntools.Launcher.LaunchInThread(
                           workingDir: Directory.GetCurrentDirectory(),
                           fileName: ExcecutableToLaunch,
                           arguments: "pass"
                           );
            Assert.AreEqual(0, result.Code);
            Assert.AreEqual("Success", result.Output[0]);

            result = Ntools.Launcher.LaunchInThread(
               workingDir: Directory.GetCurrentDirectory(),
               fileName: "test1.exe",
               arguments: "fail"
               );
            Assert.AreEqual(int.MinValue, result.Code);
            Assert.Contains("not found", result.Output[0]);
        }

        [TestMethod]
        public void LockStarPassTest()
        {
            // Arrange
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    FileName = ExcecutableToLaunch,
                    Arguments = "pass",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = false
                }
            };

            // Act
            var result = process.LockStart(true);

            // Assert
            Assert.AreEqual(0, result.Code);
            Assert.HasCount(2, result.Output);
            Assert.Contains("pass", result.GetFirstOutput());
        }

        [TestMethod]
        public void LockStartResolvesExecutableFromPath()
        {
            var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WorkingDirectory = workingDirectory,
                        FileName = "cmd",
                        Arguments = "/c exit 0",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };
                process.StartInfo.Environment["PATH"] = systemDirectory;

                var result = process.LockStart(false);

                Assert.AreEqual(0, result.Code, string.Join(Environment.NewLine, result.Output));
                Assert.IsTrue(string.Equals(
                    Path.Combine(systemDirectory, "cmd.exe"),
                    process.StartInfo.FileName,
                    StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void LockStartFailTest()
        {
            // Arrange
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    FileName = ExcecutableToLaunch,
                    Arguments = "fail",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = false
                }
            };

            // Act
            var result = process.LockStart(true);

            // Assert
            Assert.AreEqual(-100, result.Code);
            Assert.HasCount(5, result.Output);
            Assert.Contains("fail", result.Output);
            Assert.Contains("error", result.Output);
            Assert.Contains("rejected", result.Output);
        }

        [TestMethod()]
        public void LockVerifyStartTest()
        {
            // Arrange
            var process = new Process();
            process.StartInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            process.StartInfo.FileName = $"robocopy.exe";  // must be path rooted
            process.StartInfo.Arguments = "/?";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = false;

            var verbose = false;
   
            // Act
            var result = process.LockVerifyStart(verbose);


            Console.WriteLine($"Output: {result.GetFirstOutput()}");
            // Assert
            Assert.AreEqual(-1, result.Code);
            Assert.Contains("is not digitally signed", result.GetFirstOutput());
        }

        [TestMethod]
        public void LockStartWithDirectoryNotExist()
        {
            // Arrange
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = @"C:\DoesNotExist",
                    FileName = $"robocopy.exe",
                    Arguments = "/?",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = false
                }
            };
            var verbose = true;

            // Act with LockVerifyStart
            Console.WriteLine("LockVerifyStart");
            var result = process.LockVerifyStart(verbose);

            Console.WriteLine($"Output: {result.GetFirstOutput()}");

            // Assert
            Assert.AreEqual(-1, result.Code);

            // Act with LockVerifyStart
            Console.WriteLine("LockStart");
            result = process.LockVerifyStart(verbose);

            Console.WriteLine($"Output: {result.GetFirstOutput()}");

            // Assert
            Assert.AreEqual(-1, result.Code);
        }

        [TestMethod]
        public void LockStartWithFileNotExist()
        {
            // Arrange
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
                    FileName = $"DoesNotExist.exe",
                    Arguments = "/?",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = false
                }
            };
            var verbose = true;

            // Act
            Console.WriteLine("LockVerifyStart");
            var result = process.LockVerifyStart(verbose);

            Console.WriteLine($"Output: {result.GetFirstOutput()}");

            // Assert
            Assert.AreEqual(-1, result.Code);

            // Act with LockVerifyStart
            Console.WriteLine("LockStart");
            result = process.LockVerifyStart(verbose);

            Console.WriteLine($"Output: {result.GetFirstOutput()}");

            // Assert
            Assert.AreEqual(-1, result.Code);
        }
    }
}