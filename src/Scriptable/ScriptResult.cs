using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Scriptable {
    /// <summary>
    /// Simple container for the results of a Bash command.
    /// </summary>
    public class ScriptResult : ICloneable {
        /// <summary>
        /// The command's standard input as a string.
        /// </summary>
        public string? Input { get; }

        /// <summary>
        /// The command's standard output as a string. (if redirected)
        /// </summary>
        public string? Output { get; }

        /// <summary>
        /// The command's error output as a string. (if redirected)
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// The command's exit code as an integer.
        /// </summary>
        public int Exit { get; }

        public bool Redirected { get; }

        /// <summary>
        /// An array of the command's output split by newline characters. (if redirected)
        /// </summary>
        public IEnumerable<string?>? OutputLines => this.Output?.Split(Environment.NewLine.ToCharArray()) ??
                                                    Enumerable.Empty<string?>();

        internal ScriptResult(string? input, string? output, string? error, int? exit, bool? redirected) : this(output, error, exit, redirected) {
            this.Input = input?.TrimEnd(Environment.NewLine.ToCharArray());
        }

        internal ScriptResult(string? output, string? error, int? exit, bool? redirected) {
            this.Output = output?.TrimEnd(Environment.NewLine.ToCharArray());
            this.Error = error?.TrimEnd(Environment.NewLine.ToCharArray());
            this.Exit = exit ?? 0;
            this.Redirected = redirected ?? false;
        }

        public object Clone() {
            return new ScriptResult(this.Input, this.Output, this.Error, this.Exit, this.Redirected);
        }
    }
}