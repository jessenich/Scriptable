namespace Scriptable.Signals {
    internal static class UnixProcessSignaler {
        public static bool TrySignal(int processId, int signal) {
            return NativeMethods.kill(processId, signal) == 0;
        }
    }
}