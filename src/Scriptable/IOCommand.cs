using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Scriptable.Utilities;

namespace Scriptable {
    internal sealed class IOCommand : Command {
        private readonly Command _command;

        private readonly StandardIOStream _standardIOStream;

        // for toString
        private readonly object _sourceOrSink;

        public IOCommand(Command command, Task ioTask, StandardIOStream standardIOStream, object sourceOrSink) {
            this._command = command;
            this.Task = this.CreateTask(ioTask);
            this._standardIOStream = standardIOStream;
            this._sourceOrSink = sourceOrSink;
        }

        private async Task<CommandResult> CreateTask(Task ioTask) {
            await ioTask.ConfigureAwait(false);
            var innerResult = await this._command.Task.ConfigureAwait(false);

            // We wrap the inner command's result so that we can apply our stream availability error
            // checking (the Ignore() calls). However, we use the inner result's string values since
            // accessing those consumes the stream and we want both this result and the inner result
            // to have the value.
            return new CommandResult(
                innerResult.ExitCode,
                () => {
                    Ignore(this.StandardOutput);
                    return innerResult.StandardOutput;
                },
                () => {
                    Ignore(this.StandardError);
                    return innerResult.StandardError;
                }
            );

            void Ignore(object ignored) { }
        }

        public override System.Diagnostics.Process Process => this._command.Process;

        public override IReadOnlyList<System.Diagnostics.Process> Processes => this._command.Processes;

        public override int ProcessId => this._command.ProcessId;
        public override IReadOnlyList<int> ProcessIds => this._command.ProcessIds;

        public override Streams.ProcessStreamWriter StandardInput => this._standardIOStream != StandardIOStream.In
            ? this._command.StandardInput
            : throw new InvalidOperationException($"{nameof(this.StandardInput)} is unavailable because it is already being piped from {this._sourceOrSink}");

        public override Streams.ProcessStreamReader StandardOutput => this._standardIOStream != StandardIOStream.Out
            ? this._command.StandardOutput
            : throw new InvalidOperationException($"{nameof(this.StandardOutput)} is unavailable because it is already being piped to {this._sourceOrSink}");

        public override Streams.ProcessStreamReader StandardError => this._standardIOStream != StandardIOStream.Error
            ? this._command.StandardError
            : throw new InvalidOperationException($"{nameof(this.StandardError)} is unavailable because it is already being piped to {this._sourceOrSink}");

        public override Task<CommandResult> Task { get; }

        public override void Kill() {
            this._command.Kill();
        }

        public override string ToString() {
            return $"{this._command} {ToString(this._standardIOStream)} {this._sourceOrSink}";
        }

        protected override void DisposeInternal() {
            this._command.As<IDisposable>().Dispose();
        }

        private static string ToString(StandardIOStream standardIoStream) {
            return standardIoStream switch {
                StandardIOStream.In    => "<",
                StandardIOStream.Out   => ">",
                StandardIOStream.Error => "2>",
                _                      => throw new InvalidOperationException("should never get here")
            };
        }
    }

    internal enum StandardIOStream {
        In,
        Out,
        Error
    }
}
