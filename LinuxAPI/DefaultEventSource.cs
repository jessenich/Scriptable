namespace LinuxAPI {
    public class DefaultEventSource : IEventSource {
        public string? Identifier { get; init; }
        public CancellationTokenSource? Reference { get; init; }
    }
}
