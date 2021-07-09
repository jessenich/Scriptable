﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Scriptable.Shell.Streams;
using Scriptable.Shell.Utilities;
using SystemTask = System.Threading.Tasks.Task;

namespace Scriptable.Shell {
    internal sealed class ProcessCommand : Command {
        private readonly bool _disposeOnExit;

        /// <summary>
        /// Used for <see cref="ToString"/>
        /// </summary>
        private readonly string _fileName, _arguments;

        internal ProcessCommand(
            ProcessStartInfo startInfo,
            bool throwOnError,
            bool disposeOnExit,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Encoding? standardInputEncoding) {
            this._disposeOnExit = disposeOnExit;
            this._fileName = startInfo.FileName;
            this._arguments = startInfo.Arguments;
            this._process = new Process {StartInfo = startInfo, EnableRaisingEvents = true};

            var processMonitoringTask = CreateProcessMonitoringTask(this._process);

            this._process.SafeStart(out var processStandardInput, out var processStandardOutput, out var processStandardError);

            var ioTasks = new List<Task>(2);
            if (processStandardOutput != null) {
                this._standardOutputReader = new InternalProcessStreamReader(processStandardOutput);
                ioTasks.Add(this._standardOutputReader.Task);
            }

            if (processStandardError != null) {
                this._standardErrorReader = new InternalProcessStreamReader(processStandardError);
                ioTasks.Add(this._standardErrorReader.Task);
            }

            if (processStandardInput != null) {
                // unfortunately, changing the encoding can't be done via ProcessStartInfo so we have to do it manually here.
                // See https://github.com/dotnet/corefx/issues/20497

                var wrappedStream = PlatformCompatibilityHelper.WrapStandardInputStreamIfNeeded(processStandardInput.BaseStream);
                var standardInputEncodingToUse = standardInputEncoding ?? processStandardInput.Encoding;
                var streamWriter = wrappedStream == processStandardInput.BaseStream && Equals(standardInputEncodingToUse, processStandardInput.Encoding)
                    ? processStandardInput
                    : new StreamWriter(wrappedStream, standardInputEncodingToUse);
                this._standardInput = new ProcessStreamWriter(streamWriter);
            }

            // according to https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.id?view=netcore-1.1#System_Diagnostics_Process_Id,
            // this can throw PlatformNotSupportedException on some older windows systems in some StartInfo configurations. To be as
            // robust as possible, we thus make this a best-effort attempt
            try {
                this._processIdOrExceptionDispatchInfo = this._process.Id;
            }
            catch (PlatformNotSupportedException processIdException) {
                this._processIdOrExceptionDispatchInfo = ExceptionDispatchInfo.Capture(processIdException);
            }

            // we only set up timeout and cancellation AFTER starting the process. This prevents a race
            // condition where we immediately try to kill the process before having started it and then proceed to start it.
            // While we could avoid starting at all in such cases, that would leave the command in a weird state (no PID, no streams, etc)
            var processTask = ProcessHelper.CreateProcessTask(this._process, processMonitoringTask, throwOnError, timeout, cancellationToken);
            this._task = this.CreateCombinedTask(processTask, ioTasks);
        }

        private async Task<CommandResult> CreateCombinedTask(Task<int> processTask, IReadOnlyList<Task> ioTasks) {
            int exitCode;
            try {
                // first, wait for the process to exit. This can throw
                exitCode = await processTask.ConfigureAwait(false);
            }
            finally {
                if (this._disposeOnExit)
                    // clean up the process AFTER we capture the exit code
                    this._process.Dispose();
            }

            await SystemTask.WhenAll(ioTasks).ConfigureAwait(false);
            return new CommandResult(exitCode, this);
        }

        private readonly Process _process;

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

        private IReadOnlyList<Process>? _processes;
        public override IReadOnlyList<Process> Processes => this._processes ??= new ReadOnlyCollection<Process>(new[] {this.Process});

        private readonly object _processIdOrExceptionDispatchInfo;

        public override int ProcessId {
            get {
                this.ThrowIfDisposed();

                if (this._processIdOrExceptionDispatchInfo is ExceptionDispatchInfo exceptionDispatchInfo) exceptionDispatchInfo.Throw();

                return (int) this._processIdOrExceptionDispatchInfo;
            }
        }

        private IReadOnlyList<int>? _processIds;
        public override IReadOnlyList<int> ProcessIds => this._processIds ??= new ReadOnlyCollection<int>(new[] {this.ProcessId});

        private readonly ProcessStreamWriter? _standardInput;
        public override ProcessStreamWriter StandardInput => this._standardInput ?? throw new InvalidOperationException("Standard input is not redirected");

        private readonly InternalProcessStreamReader? _standardOutputReader;
        public override ProcessStreamReader StandardOutput => this._standardOutputReader ?? throw new InvalidOperationException("Standard output is not redirected");

        private readonly InternalProcessStreamReader? _standardErrorReader;
        public override ProcessStreamReader StandardError => this._standardErrorReader ?? throw new InvalidOperationException("Standard error is not redirected");

        private readonly Task<CommandResult> _task;
        public override Task<CommandResult> Task => this._task;

        public override string ToString() {
            return this._fileName + " " + this._arguments;
        }

        public override void Kill() {
            this.ThrowIfDisposed();

            ProcessHelper.TryKillProcess(this._process);
        }

        /// <summary>
        /// Creates a <see cref="SystemTask"/> which watches for the given <paramref name="process"/> to exit.
        /// This must be configured BEFORE starting the process since otherwise there is a race between subscribing
        /// to the exited event and the event firing.
        /// </summary>
        private static Task CreateProcessMonitoringTask(Process process) {
            var taskBuilder = new TaskCompletionSource<bool>();
            // note: calling TrySetResult here on the off chance that a bug causes this event to fire twice.
            // Apparently old versions of mono had such a bug. The issue is that any exception in this event
            // can down the process since it fires on an unprotected threadpool thread
            process.Exited += (o, e) => taskBuilder.TrySetResult(false);

            return taskBuilder.Task;
        }

        protected override void DisposeInternal() {
            this._process.Dispose();
        }
    }
}