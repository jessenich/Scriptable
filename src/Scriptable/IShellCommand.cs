using System.Diagnostics;

using Scriptable.Streams;

namespace Scriptable;

public interface IShellCommand {
    Process Process { get; }
    IReadOnlyList<Process> Processes { get; }
    int ProcessId { get; }
    IReadOnlyList<int> ProcessIds { get; }
    CommandResult Result { get; }
    ProcessStreamReader StandardError { get; }
    ProcessStreamWriter StandardInput { get; }
    ProcessStreamReader StandardOutput { get; }
    Task<CommandResult> Task { get; }

    IEnumerable<string> GetOutputAndErrorLines();
    void Kill();
    ShellCommand PipeTo(ShellCommand second);
    ShellCommand RedirectFrom(FileInfo file);
    ShellCommand RedirectFrom(IEnumerable<char> chars);
    ShellCommand RedirectFrom(IEnumerable<string> lines);
    ShellCommand RedirectFrom(Stream stream);
    ShellCommand RedirectFrom(TextReader reader);
    ShellCommand RedirectStandardErrorTo(FileInfo file);
    ShellCommand RedirectStandardErrorTo(ICollection<char> chars);
    ShellCommand RedirectStandardErrorTo(ICollection<string> lines);
    ShellCommand RedirectStandardErrorTo(Stream stream);
    ShellCommand RedirectStandardErrorTo(TextWriter writer);
    ShellCommand RedirectTo(FileInfo file);
    ShellCommand RedirectTo(ICollection<char> chars);
    ShellCommand RedirectTo(ICollection<string> lines);
    ShellCommand RedirectTo(Stream stream);
    ShellCommand RedirectTo(TextWriter writer);
    Task<bool> TrySignalAsync(CommandSignal signal);
    void Wait();
}