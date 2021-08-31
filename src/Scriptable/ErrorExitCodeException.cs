using System;
using System.Diagnostics;

namespace Scriptable {
    /// <summary>
    /// Represents a process that failed with a non-zero exit code. This will be thrown by a <see cref="Command"/>
    /// created with <see cref="Shell.Options.ThrowOnError"/> called
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194:Implement exception constructors.", Justification = "No Need for any other constructors")]
    public sealed class ErrorExitCodeException : Exception {
        internal ErrorExitCodeException(Process process)
            : base(string.Format("Process {0} ({1} {2}) exited with non-zero value {3}", process.Id, process.StartInfo.FileName, process.StartInfo.Arguments, process.SafeGetExitCode())) {
            this.ExitCode = process.SafeGetExitCode();
        }

        /// <summary>
        /// The exit code of the process
        /// </summary>
        public int ExitCode { get; }
    }
}
