namespace LinuxAPI {
    public class CancellationRequestState {
        public CancellationTokenSource? TokenSource { get; init; }
        public IEventSource? Requester { get; init; }
    }
}
