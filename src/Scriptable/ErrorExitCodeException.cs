using System.Diagnostics;

namespace Scriptable {
    /// <summary>
    /// Represents a process that failed with a non-zero exit code. This will be thrown by a <see cref="ShellCommand"/>
    /// created with <see cref="Shell.ShellOptions.ThrowOnError"/> called
    /// </summary>
    public sealed class ErrorExitCodeException : ApplicationException {
        internal ErrorExitCodeException(Process process)
            : this(process, $"Process {process.Id} ({process.StartInfo.FileName} {process.StartInfo.Arguments}) exited with non-zero value {process.SafeGetExitCode()}") {

        }

        public ErrorExitCodeException() : this((string?)null, null) {
        }

        public ErrorExitCodeException(string? message) : base(message) {
        }

        public ErrorExitCodeException(Process process, string? message) : this(process, message, null) {
        }

        public ErrorExitCodeException(string? message, Exception? innerException) : base(message, innerException) {
            this.ExitCode = int.MinValue;
        }

        public ErrorExitCodeException(Process process, string? message, Exception? innerException) : base(message, innerException) {
            this.ExitCode = process.SafeGetExitCode();
        }

        /// <summary>
        /// The exit code of the process
        /// </summary>
        public int ExitCode { get; }
    }

    public sealed class ShellCommandExecutionException : ApplicationException {
        internal ShellCommandExecutionException(IShell shell)
            : this(shell, $"Process {shell.} ({process.StartInfo.FileName} {process.StartInfo.Arguments}) exited with non-zero value {process.SafeGetExitCode()}") {

        }

        public ShellCommandExecutionException() : this((string?)null, null) {
        }

        public ShellCommandExecutionException(string? message) : base(message) {
        }

        public ShellCommandExecutionException(IShell shell, string? message) : this(shell, message, null) {
        }

        public ShellCommandExecutionException(string? message, Exception? innerException) : base(message, innerException) {

        }

        public ShellCommandExecutionException(IShell shell, string? message, Exception? innerException) : base(message, innerException) {

        }
    }
}
