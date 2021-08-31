using System.Threading;

namespace Scriptable.LinuxAPI.Services {

    public interface IShellProvider {
        IShell GetShell(ShellType type, CancellationToken cancellationToken = default);
    }
}
