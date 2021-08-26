namespace LinuxAPI {
    public class Command {
        public string FilePath { get; init; } = null!;
        public IEnumerable<string>? Arguments { get; init; }
        RunOptions? Options { get; init; }
    }
}
