using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Scriptable.LinuxAPI.Services;

namespace Scriptable.LinuxAPI {
    public static class Program {
        private static readonly object _lock = new object();

        private static readonly ILogger _hostLogger;
        private static readonly CancellationTokenSource _appCancellationTokenSource;
        private static readonly CancellationTokenSource _sigTermCancellationTokenSource;
        private static readonly object? _cancellationRequesterReference;

        static Program() {
            lock (_lock) {
                _hostLogger = LoggerFactory.Create(cfg => cfg
                    .AddDebug()
                    .AddSimpleConsole(consoleCfg => {
                        consoleCfg.ColorBehavior = LoggerColorBehavior.Enabled;
                        consoleCfg.SingleLine = false;
                        consoleCfg.IncludeScopes = true;
                        consoleCfg.UseUtcTimestamp = true;
                    }).AddSystemdConsole(systemCfg => {
                        systemCfg.IncludeScopes = true;
                        systemCfg.UseUtcTimestamp = true;
                    })).CreateLogger("HostLogger");

                _sigTermCancellationTokenSource = new CancellationTokenSource();
                _sigTermCancellationTokenSource.Token.Register(_ => _appCancellationTokenSource!.Cancel(true), new CancellationRequestState() {
                    Requester = new DefaultEventSource() {
                        Identifier = nameof(_cancellationRequesterReference),
                        Reference = _cancellationRequesterReference
                    },
                    TokenSource = _sigTermCancellationTokenSource
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
            Console.CancelKeyPress += Console_CancelKeyPress;

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
            Console.CancelKeyPress += Console_CancelKeyPress;
            return host.StartAsync(_appCancellationTokenSource.Token);
        }


        private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e) {
            if (!e.Cancel)
                return;

            _cancellationRequesterReference = sender;
            _sigTermCancellationTokenSource.Cancel(true);
        }
    }
}
