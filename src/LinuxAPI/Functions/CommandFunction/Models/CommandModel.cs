namespace Scriptable.LinuxAPI.Functions.CommandFunction.Models {
    public class CommandModel {
        public string FilePath { get; init; } = null!;
        public object[] Arguments { get; init; } = null;
        public RunOptionsModel? Options { get; init; } = null;
    }
}
