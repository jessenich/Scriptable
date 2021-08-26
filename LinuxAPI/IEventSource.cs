namespace LinuxAPI {
    public interface IEventSource {
        string? Identifier { get; }
        CancellationTokenSource? Reference { get; }
    }
}
