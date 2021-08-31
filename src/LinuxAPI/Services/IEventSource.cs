namespace Scriptable.LinuxAPI.Services {
    public interface IEventSource {
        string? Identifier { get; }
        object? Reference { get; }
    }
}
