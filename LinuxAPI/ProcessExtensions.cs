using System.Collections.Specialized;
using System.Diagnostics;

namespace LinuxAPI {
    public static class ProcessExtensions {
        public static void AddEnvironmentVariables(this ProcessStartInfo process, string prefix) {
            var envVars = new StringDictionary();
            foreach (var envVar in Environment.GetEnvironmentVariables()) {
                if (((string?)envVar)?.Substring(0, 11) == prefix) {
                    process.EnvironmentVariables.Add(envVar.ToString()![12..], string.Empty);
                }
            }
        }
    }
}
