using System.Threading;

namespace Scriptable.LinuxAPI.Services {
    public class CancellationRequestState {
        public CancellationTokenSource? TokenSource { get; init; }
        public IEventSource? Requester { get; init; }
    }
}
