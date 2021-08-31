using System.Runtime.Serialization;

namespace Scriptable.LinuxAPI.Services {

    public enum ShellType {
        [EnumMember(Value = "bash")]
        Bash,

        [EnumMember(Value = "pwsh")]
        Pwsh
    }
}
