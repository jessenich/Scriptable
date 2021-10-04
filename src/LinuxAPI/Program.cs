using System;
using System.Threading;
using System.Threading.Tasks;
using Scriptable.LinuxAPI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

#pragma warning disable 649
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
// ReSharper disable ArrangeTypeMemberModifiers

namespace Scriptable.LinuxAPI {
    public static class Program {
        private static readonly object _lock = new();

        private static readonly ILoggerFactory _hostLoggerFactory;
        private static readonly ILogger _hostLogger;
        private static readonly CancellationTokenSource _appCancellationTokenSource;
        private static readonly SigTermCancellationHandler _signalHandler;
        private static readonly object? _cancellationRequesterReference;

        static Program() {
            lock (_lock) {
                _hostLoggerFactory = LoggerFactory.Create(cfg => cfg
                    .AddDebug()
                    .AddSimpleConsole(consoleCfg => {
                        consoleCfg.ColorBehavior = LoggerColorBehavior.Enabled;
                        consoleCfg.SingleLine = false;
                        consoleCfg.IncludeScopes = true;
                        consoleCfg.UseUtcTimestamp = true;
                    }).AddSystemdConsole(systemCfg => {
                        systemCfg.IncludeScopes = true;
                        systemCfg.UseUtcTimestamp = true;
                    }));

                _hostLogger = _hostLoggerFactory.CreateLogger("HostLogger");
                _signalHandler = new SigTermCancellationHandler(_hostLoggerFactory.CreateLogger<SigTermCancellationHandler>());
                _signalHandler.CancellationToken.Register(_ => _appCancellationTokenSource!.Cancel(true), new CancellationRequestState() {
                    Requester = new DefaultEventSource() {
                        Identifier = nameof(_cancellationRequesterReference),
                        Reference = _cancellationRequesterReference
                    },
                    TokenSource = _appCancellationTokenSource
                });

                _appCancellationTokenSource = new CancellationTokenSource();

                AppDomain.MonitoringIsEnabled = true;
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                    if (!e.IsTerminating)
                        return;

                    _hostLogger.LogCritical($"Unhandled Exception Thrown: {e.ExceptionObject}");
                    _appCancellationTokenSource.Cancel(true);
                };
            }
        }

        public static Task Main() {

            var builder = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureHostConfiguration(cfg => cfg.AddEnvironmentVariables("SCRIPTABLE_"))
                .ConfigureServices((hostContext, services) => {
                    services.AddTransient<IShellProvider>(_ => new ShellProvider())
                        .AddLogging(builder => builder
                            .AddConfiguration(hostContext.Configuration.GetSection("Logging"))
                            .AddDebug()
                            .AddSimpleConsole(cfg => {
                                cfg.ColorBehavior = LoggerColorBehavior.Enabled;
                                cfg.IncludeScopes = true;
                                cfg.SingleLine = true;
                                cfg.UseUtcTimestamp = true;
                            })
                            .AddSystemdConsole(cfg => {
                                    cfg.IncludeScopes = true;
                                    cfg.UseUtcTimestamp = true;
                                }
                            ));
                });
            var host = builder.Build();
            return host.StartAsync(_appCancellationTokenSource.Token);
        }
    }
}
