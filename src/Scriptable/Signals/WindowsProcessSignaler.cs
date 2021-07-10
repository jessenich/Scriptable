﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Scriptable.Utilities;

namespace Scriptable.Signals {
    internal static class WindowsProcessSignaler {
        // This class forks its signaling approach based on whether or not the target process shares a console with the current
        // process. This is needed because Windows signals hit all processes on the same console. Signaling processes
        // requires mucking with global state, so signaling processes on a different console is safer since we can do it from our
        // embedded ProcessSignaler exe and thus isolate those modifications

        /// <summary>
        /// Since signaling from the current process requires mucking with global state (CTRL handlers), we limit to one
        /// concurrent access.
        /// </summary>
        private static readonly SemaphoreSlim SignalFromCurrentProcessLock = new(1, 1);

        public static async Task<bool> TrySignalAsync(int processId, NativeMethods.CtrlType signal) {
            if (HasSameConsole(processId)) return await SendSignalFromCurrentProcess(processId, signal).ConfigureAwait(false);

            var exeFile = await DeploySignalerExeAsync().ConfigureAwait(false);
            try {
                var command = Command.Run(exeFile, new object[] {processId, (int) signal});
                return (await command.Task.ConfigureAwait(false)).Success;
            }
            finally {
                File.Delete(exeFile);
            }
        }

        internal static bool HasSameConsole(int processId) {
            // see https://docs.microsoft.com/en-us/windows/console/getconsoleprocesslist
            // for instructions on calling this method

            uint processListCount = 1;
            uint[] processIdListBuffer;
            do {
                processIdListBuffer = new uint[processListCount];
                processListCount = NativeMethods.GetConsoleProcessList(processIdListBuffer, processListCount);
            } while (processListCount > processIdListBuffer.Length);

            checked {
                return processIdListBuffer.Take((int) processListCount)
                                          .Contains(checked((uint) processId));
            }
        }

        private static async Task<bool> SendSignalFromCurrentProcess(int processId, NativeMethods.CtrlType signal) {
            await SignalFromCurrentProcessLock.WaitAsync().ConfigureAwait(false);
            try {
                using var waitForSignalSemaphore = new SemaphoreSlim(0, 1);
                NativeMethods.ConsoleCtrlDelegate handler = receivedSignal => {
                    if (receivedSignal == signal) {
                        waitForSignalSemaphore.Release();
                        // if we're signaling another process on the same console, we return true
                        // to prevent the signal from bubbling. If we're signaling ourselves, we
                        // allow it to bubble since presumably that's what the caller wanted
                        return processId != ProcessHelper.CurrentProcessId;
                    }

                    return false;
                };
                if (!NativeMethods.SetConsoleCtrlHandler(handler, true)) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                try {
                    if (!NativeMethods.GenerateConsoleCtrlEvent(signal, NativeMethods.AllProcessesWithCurrentConsoleGroup)) return false;

                    // Wait until the signal has reached our handler and been handled to know that it is safe to
                    // remove the handler.
                    // Timeout here just to ensure we don't hang forever if something weird happens (e. g. someone
                    // else registers a handler concurrently with us).
                    return await waitForSignalSemaphore.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                }
                finally {
                    if (!NativeMethods.SetConsoleCtrlHandler(handler, false)) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            }
            finally {
                SignalFromCurrentProcessLock.Release();
            }
        }

        private static async Task<string> DeploySignalerExeAsync() {
            const string signalerExeNameWithoutExtension = "MedallionShell.ProcessSignaler";
            var exePath = Path.Combine(Path.GetTempPath(), $"{signalerExeNameWithoutExtension}_{Guid.NewGuid():N}.exe");
            await using var resourceStream = Helpers.GetMedallionShellAssembly().GetManifestResourceStream(signalerExeNameWithoutExtension + ".exe");
            await using var fileStream = new FileStream(exePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, Constants.ByteBufferSize, true);

            if (resourceStream?.Length == 0)
                throw new EndOfStreamException("Unexpected end of stream found at position 0");

            await resourceStream!.CopyToAsync(fileStream).ConfigureAwait(false);
            return exePath;
        }
    }
}
