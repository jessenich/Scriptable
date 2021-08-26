using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using Scriptable;

namespace LinuxAPI {
    public class Program {
        private static readonly object _lock = new object();

        private static CancellationTokenSource AppCancellationTokenSource { get; }
        private static CancellationTokenSource SigTermCancellationTokenSource { get; }
        private static object? CancellationRequesterReference { get; set; }

        static Program() {
            lock (_lock) {
                SigTermCancellationTokenSource = new CancellationTokenSource();
                SigTermCancellationTokenSource.Token.Register((state) => {
                    var cancellationRequestState = (CancellationRequestState)state;

                }, new CancellationRequestState() {
                    Requester = new DefaultEventSource() {
                        Identifier = nameof(SigTermCancellationTokenSource),
                        Reference = SigTermCancellationTokenSource
                    },
                    TokenSource = SigTermCancellationTokenSource
                });
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0053:Use expression body for lambda expressions", Justification = "Frequent Editing")]
        public static Task Main() {
            Console.CancelKeyPress += Console_CancelKeyPress;

            var builder = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureHostConfiguration(cfg => cfg.AddEnvironmentVariables("SCRIPTABLE_"))
                .ConfigureServices((ctx, svcs) => {
                    ctx.HostingEnvironment.ContentRootPath = "~/";
                    svcs.AddSingleton(AppCancellationTokenSource)
                        .AddLogging(builder => {
                            builder
                                .AddConfiguration(ctx.Configuration.GetSection("Logging"))
                                .AddDebug()
                                .AddSimpleConsole(cfg => {
                                    cfg.ColorBehavior = LoggerColorBehavior.Enabled;
                                    cfg.IncludeScopes = true;
                                    cfg.SingleLine = true;
                                    cfg.UseUtcTimestamp = true;
                                });
                        })
                        .AddTransient(svcProvider => {
                            var cancellationToken = svcProvider.GetService<CancellationTokenSource>()?.Token ?? default;
                            return
                            return new Shell(shellOpts => {
                                shellOpts.CancellationToken(cancellationToken)
                                         .Encoding(Encoding.UTF8)
                                         .StartInfo(init => {
                                             init.CreateNoWindow = true;
                                             init.AddEnvironmentVariables("SCRIPTABLE_");
                                             init.LoadUserProfile = false;
                                             init.WorkingDirectory = ctx.HostingEnvironment.ContentRootPath;
                                             init.RedirectStandardError = true;
                                             init.RedirectStandardOutput = true;
                                             init.RedirectStandardInput = true;
                                         });
                            });
                        });
                });
            var host = builder.Build();
            var cancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += Console_CancelKeyPress;
            return host.WaitForShutdownAsync(SigTermCancellationTokenSource.Token);
        }

        private delegate Shell ShellServiceFactory(string fileName, string arguments, CancellationToken cancellationToken = default);

        private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e) {
            if (e.Cancel) {
                CancellationRequesterReference = sender;
                SigTermCancellationTokenSource.Cancel(true);
            }
        }
    }

    public interface IBashShell : IShell {

    }

    public interface IPwshShell : IShell {

    }
}
