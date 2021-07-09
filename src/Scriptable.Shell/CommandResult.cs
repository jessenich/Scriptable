﻿using System;

namespace Scriptable.Shell {
    /// <summary>
    /// The result of a <see cref="Command"/>
    /// </summary>
    public sealed class CommandResult {
        private readonly Lazy<string> _standardOutput, _standardError;

        internal CommandResult(int exitCode, Command command)
            : this(exitCode, () => command.StandardOutput.ReadToEnd(), () => command.StandardError.ReadToEnd()) { }

        internal CommandResult(int exitCode, Func<string> standardOutput, Func<string> standardError) {
            this.ExitCode = exitCode;
            this._standardOutput = new Lazy<string>(standardOutput);
            this._standardError = new Lazy<string>(standardError);
        }

        /// <summary>
        /// The exit code of the command's process
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// Returns true iff the exit code is 0 (indicating success)
        /// </summary>
        public bool Success => this.ExitCode == 0;

        /// <summary>
        /// If available, the full standard output text of the command
        /// </summary>
        public string StandardOutput => this._standardOutput.Value;

        /// <summary>
        /// If available, the full standard error text of the command
        /// </summary>
        public string StandardError => this._standardError.Value;
    }
}