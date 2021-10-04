using System;

namespace Scriptable.LinuxAPI {
    public class SigTermShutdownEventArgs : GracefulShutdownEventArgs {
        public ConsoleSpecialKey Modifier { get; init; }
    }
}
