namespace LinuxAPI {
    public class RunOptions {
        public string Shell { get; init; } = "/bin/bash";
        public IEnumerable<string>? Arguments { get; init; }
        public string? WorkingDirectory { get; init; }
    }
}
