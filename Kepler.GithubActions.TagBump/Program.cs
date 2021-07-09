using System;
using System.Diagnostics;
using System.Net.Security;

namespace Kepler.GithubActions.TagBump {
    class Program {
        static void Main(string[] args) {


            new Process() {
                StartInfo = new ProcessStartInfo() {
                    FileName = "git"
                }
            }
            Process.Start(new ProcessStartInfo() {
                FileName = "/bin/sh",
                ArgumentList = { "-c", "git describe --tags"},

            })
        }

        public NewGitProcess() => new ProcessStartInfo("git") {
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
            UseShellExecute = true,

        }

        public int Run(string[] args) {
            bash = new Bash();
        }
    }
}
