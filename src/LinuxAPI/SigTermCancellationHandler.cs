using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Scriptable.LinuxAPI {
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class SigTermCancellationHandler {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ConsoleCancelEventHandler _sigTermEventHandler;
        private readonly ILogger<SigTermCancellationHandler> _logger;
        public CancellationToken CancellationToken => this._cancellationTokenSource.Token;

        public SigTermShutDownHandler SigTermShutdownHandler { get; }
        public AsyncSigTermShutdownHandler AsyncSigTermShutdownHandler { get; }

        private ConsoleSpecialKey? modifier;

        public SigTermCancellationHandler(ILogger<SigTermCancellationHandler> logger, CancellationTokenSource? source = null) {
            this._cancellationTokenSource = source ?? new CancellationTokenSource();
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Console.CancelKeyPress += this._sigTermEventHandler += this.CtrlCHandler;
            this.CancellationToken.Register(this.OnCancellation);
        }

        protected virtual void OnCancellation() {
            this.GracefulShutdownHandler(this,  new SigTermShutdownEventArgs() {
                CancellationToken = this.CancellationToken,
                Modifier = this.modifier ?? ConsoleSpecialKey.ControlC,
                ShuttingDown = true
            });
        }

        protected virtual void CtrlCHandler(object? sender, ConsoleCancelEventArgs eventArgs) {
            // Dont fire event again for subsequent signals.
            if (this._cancellationTokenSource.IsCancellationRequested || !eventArgs.Cancel)
                return;

            this._cancellationTokenSource.Cancel();
        }

        protected virtual void GracefulShutdownHandler(object? sender, SigTermShutdownEventArgs eventArgs) {
            var invocationTask  = this.AsyncSigTermShutdownHandler?.Invoke(this, eventArgs);
            if (invocationTask?.Status == TaskStatus.Created)
                invocationTask.Start();

            this.SigTermShutdownHandler?.Invoke(this, eventArgs);
            invocationTask?.GetAwaiter().GetResult();
        }
    }
}
