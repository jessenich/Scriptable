using System;
using System.Diagnostics;
using System.IO;

namespace Scriptable.Shell.Utilities {
    internal static class Log {
        [Conditional("TESTING")]
        public static void WriteLine(string format, params object[] args) {
            var text = string.Format("{0:h:m:ss.fff}: ", DateTime.Now) + string.Format(format, args);

            using (var fs = new FileStream("log.txt", FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(fs)) {
                writer.WriteLine(text);
            }
        }
    }
}