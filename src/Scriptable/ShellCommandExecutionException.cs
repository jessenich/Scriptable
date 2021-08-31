using System;
using System.Diagnostics;

namespace Scriptable {
    public sealed class ShellCommandExecutionException : ApplicationException {
        internal ShellCommandExecutionException(Process process)
            : base($"{process.StartInfo.FileName} {process.StartInfo.Arguments}) exited with non-zero value {process.SafeGetExitCode().ToString()}") {

        }
    }
}
