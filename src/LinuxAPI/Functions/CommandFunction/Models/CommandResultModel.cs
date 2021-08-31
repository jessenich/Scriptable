// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Scriptable.LinuxAPI.Functions.CommandFunction.Models {

    public readonly struct CommandResultModel {
        public bool Success { get; init; }
        public int ExitCode { get; init; }
        public string StdOut { get; init; }
        public string StdErr { get; init; }
    }
}
