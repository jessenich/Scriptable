using System;
using System.Threading;

namespace Scriptable.LinuxAPI {
    public abstract class GracefulShutdownEventArgs {
        public bool ShuttingDown { get; init; }
        public Exception? UnhandledException { get; init; }
        public CancellationToken? CancellationToken { get; init; }
    }
}
