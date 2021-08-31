using System.Text;

namespace Scriptable.LinuxAPI.Services {
    internal static class Encodings {
        internal static readonly Encoding UTF8NoBOMWithThrow = new UTF8Encoding(false, true);
    }
}
