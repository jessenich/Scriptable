using System;
using System.Diagnostics;

namespace Scriptable {
    /// <summary>
    /// Represents a process that failed with a non-zero exit code. This will be thrown by a <see cref="Command"/>
    /// created with <see cref="Shell.Options.ThrowOnError"/> called
    /// </summary>
    public sealed class ErrorExitCodeException : Exception {
        internal ErrorExitCodeException(Process process) 
            : base($"Process {process.Id} ({process.StartInfo.FileName} {process.StartInfo.Arguments}) exited with non-zero value {process.SafeGetExitCode()}") {
            this.ExitCode = process.SafeGetExitCode();
        }

        /// <summary>
        /// The exit code of the process
        /// </summary>
        public int ExitCode { get; private set; }
    }
}