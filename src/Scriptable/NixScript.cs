using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Scriptable {
    public enum ExecutionEnvironment {
        LinuxShell,
        MacShell,
        WindowsCmd,
        WindowsPowerShell,
        WindowsSubsystem,
        MinGW
    }

    /// <summary>Handles boilerplate for Bash commands and stores output information.</summary>
    public class NixScript {
        private static bool IsLinux { get; }
        private static bool IsMacOS { get; }
        private static bool IsWindows { get; }
        private static string BashPath { get; }

        /// <summary>Determines whether bash is running in a native OS (Linux/MacOS).</summary>
        /// <returns>True if in *nix, else false.</returns>
        public static bool Native { get; }

        /// <summary>Determines if using Windows and if Linux subsystem is installed.</summary>
        /// <returns>True if in Windows and bash detected.</returns>
        public static bool Subsystem => IsWindows && File.Exists(@"C:\Windows\System32\bash.exe");

        public ScriptResult? PreviousInvocationResult { get; private set; }

        public ScriptResult? InvocationResult { get; private set; }

        static NixScript() {
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            Environment.GetEnvironmentVariable("PATH")?.Split(":");

            Native = IsLinux || IsMacOS;
            BashPath = Native ? "bash" : "bash.exe";
        }

        /// <summary>Execute a new Bash command.</summary>
        /// <param name="input">The command to execute.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult? Command(string input, bool redirect = true) {
            if (!Native && !Subsystem)
                throw new PlatformNotSupportedException();

            using (var bash = new Process {StartInfo = this.BashInfo(input, redirect)}) {
                bash.Start();

                if (redirect)
                    this.PreviousInvocationResult = new ScriptResult(
                        bash.StandardOutput.ReadToEnd(true),
                        bash.StandardError.ReadToEnd(true),
                        bash.ExitCode,
                        true
                    );
                else
                    this.InvocationResult = new ScriptResult(null, null, bash.ExitCode, false);

                bash.WaitForExit();
                bash.Close();
            }

            return redirect ? this.PreviousInvocationResult?.Clone() as ScriptResult : this.InvocationResult;
        }

        private ProcessStartInfo BashInfo(string input, bool redirectOutput) {
            return new() {
                FileName = BashPath,
                Arguments = $"-c \"{input}\"",
                RedirectStandardInput = false,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
                UseShellExecute = false,
                CreateNoWindow = true,
                ErrorDialog = false
            };
        }

        /// <summary>Echo the given string to standard output.</summary>
        /// <param name="input">The string to print.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Echo(string input, bool redirect = false) {
            return this.Command($"echo {input}", redirect);
        }

        /// <summary>Echo the given string to standard output.</summary>
        /// <param name="input">The string to print.</param>
        /// <param name="flags">Optional `echo` arguments.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Echo(string input, string flags, bool redirect = false) {
            return this.Command($"echo {flags} {input}", redirect);
        }

        /// <summary>Echo the given string to standard output.</summary>
        /// <param name="input">The string to print.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Echo(object input, bool redirect = false) {
            return this.Command($"echo {input}", redirect);
        }

        /// <summary>Echo the given string to standard output.</summary>
        /// <param name="input">The string to print.</param>
        /// <param name="flags">Optional `echo` arguments.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Echo(object input, string flags, bool redirect = false) {
            return this.Command($"echo {flags} {input}", redirect);
        }

        /// <summary>Search for `pattern` in each file in `location`.</summary>
        /// <param name="pattern">The pattern to match.</param>
        /// <param name="location">The files or directory to search.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Grep(string pattern, string location, bool redirect = true) {
            return this.Command($"grep {pattern} {location}", redirect);
        }

        /// <summary>Search for `pattern` in each file in `location`.</summary>
        /// <param name="pattern">The pattern to match.</param>
        /// <param name="location">The files or directory to search.</param>
        /// <param name="flags">Optional `grep` arguments.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        public ScriptResult Grep(string pattern, string location, string flags, bool redirect = true) {
            return this.Command($"grep {pattern} {flags} {location}", redirect);
        }

        /// <summary>List information about files in the current directory.</summary>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Ls(bool redirect = true) {
            return this.Command("ls", redirect);
        }

        /// <summary>List information about files in the current directory.</summary>
        /// <param name="flags">Optional `ls` arguments.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Ls(string flags, bool redirect = true) {
            return this.Command($"ls {flags}", redirect);
        }

        /// <summary>List information about the given files.</summary>
        /// <param name="flags">Optional `ls` arguments.</param>
        /// <param name="files">Files or directory to search.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Ls(string flags, string files, bool redirect = true) {
            return this.Command($"ls {flags} {files}", redirect);
        }

        /// <summary>Move `source` to `directory`.</summary>
        /// <param name="source">The file to be moved.</param>
        /// <param name="directory">The destination directory.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Mv(string source, string directory, bool redirect = true) {
            return this.Command($"mv {source} {directory}", redirect);
        }

        /// <summary>Move `source` to `directory`.</summary>
        /// <param name="source">The file to be moved.</param>
        /// <param name="directory">The destination directory.</param>
        /// <param name="flags">Optional `mv` arguments.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Mv(string source, string directory, string flags, bool redirect = true) {
            return this.Command($"mv {flags} {source} {directory}", redirect);
        }

        /// <summary>Copy `source` to `directory`.</summary>
        /// <param name="source">The file to be copied.</param>
        /// <param name="directory">The destination directory.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Cp(string source, string directory, bool redirect = true) {
            return this.Command($"cp {source} {directory}", redirect);
        }

        /// <summary>Copy `source` to `directory`.</summary>
        /// <param name="source">The file to be copied.</param>
        /// <param name="directory">The destination directory.</param>
        /// <param name="flags">Optional `cp` arguments.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Cp(string source, string directory, string flags, bool redirect = true) {
            return this.Command($"cp {flags} {source} {directory}", redirect);
        }

        /// <summary>Remove or unlink the given file.</summary>
        /// <param name="file">The file(s) to be removed.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Rm(string file, bool redirect = true) {
            return this.Command($"rm {file}", redirect);
        }

        /// <summary>Remove or unlink the given file.</summary>
        /// <param name="file">The file(s) to be removed.</param>
        /// <param name="flags">Optional `rm` arguments.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Rm(string file, string flags, bool redirect = true) {
            return this.Command($"rm {flags} {file}", redirect);
        }

        /// <summary>Concatenate `file` to standard input.</summary>
        /// <param name="file">The source file.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Cat(string file, bool redirect = true) {
            return this.Command($"cat {file}", redirect);
        }

        /// <summary>Concatenate `file` to standard input.</summary>
        /// <param name="file">The source file.</param>
        /// <param name="flags">Optional `cat` arguments.</param>
        /// <param name="redirect">Print output to terminal if false.</param>
        /// <returns>A `BashResult` containing the command's output information.</returns>
        public ScriptResult Cat(string file, string flags, bool redirect = true) {
            return this.Command($"cat {flags} {file}", redirect);
        }
    }

    public static class Extensions {
        public static string ReadToEnd(this StreamReader streamReader, bool trimEmptyTail = true) {
            var streamResult = streamReader.ReadToEnd();
            return trimEmptyTail ? streamResult.TrimEnd(Environment.NewLine.ToCharArray()) : streamResult;
        }

        public static async Task<string> ReadToEndAsync(this StreamReader streamReader, bool trimEmptyTail = true) {
            var streamResult = await streamReader.ReadToEndAsync();
            return trimEmptyTail ? streamResult.TrimEnd(Environment.NewLine.ToCharArray()) : streamResult;
        }
    }
}