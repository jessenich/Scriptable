using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Scriptable.Streams;
using Scriptable.Utilities;

namespace Scriptable {
    internal sealed class PipedCommand : Command {
        private readonly Command _first, _second;
        private readonly Task<CommandResult> _task;

        internal PipedCommand(Command first, Command second) {
            this._first = first;
            this._second = second;

            var pipeStreamsTask = PipeAsync(this._first.StandardOutput, this._second.StandardInput);
            this._task = this.CreateTask(pipeStreamsTask);
        }

        private async Task<CommandResult> CreateTask(Task pipeStreamsTask) {
            await pipeStreamsTask.ConfigureAwait(false);
            return await this._second.Task.ConfigureAwait(false);
        }

        public override Process Process => this._second.Process;

        private IReadOnlyList<Process>? _processes;
        public override IReadOnlyList<Process> Processes => this._processes ??= this._first.Processes.Concat(this._second.Processes).ToList().AsReadOnly();

        public override int ProcessId => this._second.ProcessId;

        private IReadOnlyList<int>? _processIds;
        public override IReadOnlyList<int> ProcessIds => this._processIds ??= this._first.ProcessIds.Concat(this._second.ProcessIds).ToList().AsReadOnly();

        public override Task<CommandResult> Task => this._task;

        public override ProcessStreamWriter StandardInput => this._first.StandardInput;

        public override ProcessStreamReader StandardOutput => this._second.StandardOutput;

        public override ProcessStreamReader StandardError => this._second.StandardError;

        public override void Kill() {
            this._first.Kill();
            this._second.Kill();
        }

        public override string ToString() {
            return this._first + " | " + this._second;
        }

        protected override void DisposeInternal() {
            this._first.As<IDisposable>().Dispose();
            this._second.As<IDisposable>().Dispose();
        }

        private static async Task PipeAsync(ProcessStreamReader source, ProcessStreamWriter destination) {
            // NOTE: we use PipeFrom() since this will automatically flush any characters written to the
            // TextWriter APIs of destination first. However, we wrap with a using to ensure that source is
            // disposed rather than just source.BaseStream (which is all we pass to PipeFrom)
            using (source) {
                await destination.PipeFromAsync(source.BaseStream).ConfigureAwait(false);
            }
        }
    }
}
