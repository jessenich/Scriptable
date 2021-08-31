using System;
using System.Threading;
using Scriptable.LinuxAPI.Extensions;

namespace Scriptable.LinuxAPI.Services {

    public class ShellProvider : IShellProvider {

        public IShell GetShell(ShellType type, CancellationToken cancellationToken = default) {
            var shell = string.Empty;
            var shellArgs = string.Empty;

            switch (type) {
                case ShellType.Bash:
                    shell = "/bin/bash";
                    shellArgs = "-c";
                    break;
                case ShellType.Pwsh:
                    shell = "/bin/pwsh";
                    shellArgs = "-NoProfile -NoLogo -Command";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }

            return new Shell(opts => opts
                    .ThrowOnError(true)
                    .Timeout(TimeSpan.FromSeconds(20))
                    .Encoding(Encodings.UTF8NoBOMWithThrow)
                    .CancellationToken(cancellationToken)
                    .StartInfo(init => {
                        init.FileName = shell;
                        init.Arguments = shellArgs;
                        init.CreateNoWindow = true;
                        init.UseShellExecute = true;
                        init.AddEnvironmentVariables("SCRIPTABLE_");
                        init.RedirectStandardError = true;
                        init.RedirectStandardInput = true;
                        init.RedirectStandardInput = true;
                    })
                    .DisposeOnExit(true));
        }
    }
}
