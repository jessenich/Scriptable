using System.Threading.Tasks;

namespace Scriptable.LinuxAPI {
    public delegate void SigTermShutDownHandler(object? sender, SigTermShutdownEventArgs eventArgs);
    public delegate Task AsyncSigTermShutdownHandler(object? sender, SigTermShutdownEventArgs eventArgs);
}
