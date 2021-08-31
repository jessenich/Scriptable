namespace Scriptable.LinuxAPI.Services {
    public class DefaultEventSource : IEventSource {
        public string? Identifier { get; init; }
        public object? Reference { get; init; }
    }
}
