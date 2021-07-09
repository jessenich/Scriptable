using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Scriptable.Shell.Streams;
using Scriptable.Shell.Utilities;

namespace Scriptable.Shell {
    internal sealed class AttachedCommand : Command {
        private const string StreamPropertyExceptionMessage =
            "This property cannot be used when attaching to already running process.";

        private readonly Process _process;
        private readonly Task<CommandResult> _commandResultTask;
        private readonly bool _disposeOnExit;
        private readonly Lazy<ReadOnlyCollection<Process>> _processes;

        internal AttachedCommand(
            Process process,
            bool throwOnError,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            bool disposeOnExit) {
            this._process = process;
            this._disposeOnExit = disposeOnExit;
            var processMonitoringTask = CreateProcessMonitoringTask(process);
            var processTask = ProcessHelper.CreateProcessTask(this._process, processMonitoringTask, throwOnError, timeout, cancellationToken);

            this._commandResultTask = processTask.ContinueWith(
                continuedTask => {
                    if (disposeOnExit) this._process.Dispose();
                    return new CommandResult(continuedTask.Result, this);
                },
                TaskContinuationOptions.ExecuteSynchronously
            );

            this._processes = new Lazy<ReadOnlyCollection<Process>>(() => new ReadOnlyCollection<Process>(new[] {this._process}));
        }

        public override Process Process {
            get {
                this.ThrowIfDisposed();
                Throw<InvalidOperationException>.If(
                    this._disposeOnExit,
                    ProcessHelper.ProcessNotAccessibleWithDisposeOnExitEnabled
                );
                return this._process;
            }
        }

        public override IReadOnlyList<Process> Processes {
            get {
                this.ThrowIfDisposed();
                return this._processes.Value;
            }
        }

        public override int ProcessId {
            get {
                this.ThrowIfDisposed();

                return this._process.Id;
            }
        }

        public override IReadOnlyList<int> ProcessIds => new ReadOnlyCollection<int>(new[] {this.ProcessId});

        public override ProcessStreamWriter StandardInput => throw new InvalidOperationException(StreamPropertyExceptionMessage);

        public override ProcessStreamReader StandardOutput => throw new InvalidOperationException(StreamPropertyExceptionMessage);

        public override ProcessStreamReader StandardError => throw new InvalidOperationException(StreamPropertyExceptionMessage);

        public override void Kill() {
            this.ThrowIfDisposed();

            ProcessHelper.TryKillProcess(this._process);
        }

        public override Task<CommandResult> Task => this._commandResultTask;

        protected override void DisposeInternal() {
            this._process.Dispose();
        }

        private static Task CreateProcessMonitoringTask(Process process) {
            var taskBuilder = new TaskCompletionSource<bool>();

            try {
                process.EnableRaisingEvents = true;
            }
            // EnableRaisingEvents will throw if the process has already exited; to account for
            // that race condition we return a completed task in that case
            catch (InvalidOperationException) when (process.HasExited) {
                taskBuilder.SetResult(false);
                return taskBuilder.Task;
            }

            process.Exited += (sender, e) => taskBuilder.TrySetResult(false);

            // we must account for the race condition where the process exits between enabling events and
            // subscribing to Exited. Therefore, we do exit check after the subscription to account
            // for this
            if (process.HasExited) taskBuilder.TrySetResult(false);

            return taskBuilder.Task;
        }
    }
}