using System.Collections.Generic;
using Scriptable.LinuxAPI.Services;

namespace Scriptable.LinuxAPI.Functions.CommandFunction.Models {
    public class RunOptionsModel {
        public ShellType Shell { get; init; } = ShellType.Bash;
        public IEnumerable<string>? Arguments { get; init; }
        public string? WorkingDirectory { get; init; }
    }
}
