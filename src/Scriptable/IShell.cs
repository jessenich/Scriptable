namespace Scriptable;

public interface IShell {
    ShellCommand Run(string executable, IEnumerable<object>? arguments = null, Action<Shell.ShellOptions>? options = null);
    ShellCommand Run(string executable, params object[] arguments);
    bool TryAttachToProcess(int processId, Action<Shell.ShellOptions>? options, [NotNullWhen(true)] out ShellCommand? attachedCommand);
    bool TryAttachToProcess(int processId, [NotNullWhen(true)] out ShellCommand? attachedCommand);
}
