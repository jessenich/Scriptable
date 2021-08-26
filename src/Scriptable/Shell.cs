using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using Scriptable.Utilities;

namespace Scriptable {
    /// <summary>
    /// Represents an object which can be used to dispatch <see cref="ShellCommand"/>s
    /// </summary>
    public sealed class Shell : IShell {
        internal Action<ShellOptions> Configuration { get; }

        /// <summary>
        /// Creates a shell whose commands will receive the given configuration options
        /// </summary>
        public Shell(Action<ShellOptions> options) {
            Throw.IfNull(options, nameof(options));
            this.Configuration = options;
        }

        #region ---- Instance API ----

        /// <summary>
        /// Executes the given <paramref name="executable"/> with the given <paramref name="arguments"/> and
        /// <paramref name="options"/>
        /// </summary>
        public ShellCommand Run(string executable, IEnumerable<object>? arguments = null, Action<ShellOptions>? options = null) {
            Throw.If(string.IsNullOrEmpty(executable), "executable is required");

            var finalOptions = this.GetOptions(options);

            var processStartInfo = new ProcessStartInfo {
                Arguments = arguments != null
                    ? finalOptions.CommandLineSyntax.CreateArgumentString(arguments!.Select(arg => Convert.ToString(arg, CultureInfo.InvariantCulture))!)
                    : string.Empty,
                CreateNoWindow = true,
                FileName = executable,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            if (finalOptions.ProcessStreamEncoding != null)
                processStartInfo.StandardOutputEncoding = processStartInfo.StandardErrorEncoding = finalOptions.ProcessStreamEncoding;

            finalOptions.StartInfoInitializers.ForEach(a => a(processStartInfo));
            ShellCommand command = new ProcessCommand(
                processStartInfo,
                finalOptions.ThrowExceptionOnError,
                finalOptions.DisposeProcessOnExit,
                finalOptions.ProcessTimeout,
                finalOptions.ProcessCancellationToken,
                finalOptions.ProcessStreamEncoding
            );
            foreach (var initializer in finalOptions.CommandInitializers) {
                command = initializer(command);
                if (command == null)
                    throw new InvalidOperationException($"{nameof(ShellCommand)} initializer passed to {nameof(ShellOptions)}.{nameof(ShellOptions.Command)} must not return null!");
            }

            return command;
        }

        /// <summary>
        /// Tries to attach to an already running process, given its <paramref name="processId"/>,
        /// giving <paramref name="attachedCommand" /> representing the process and returning
        /// true if this succeeded, otherwise false.
        /// </summary>
        public bool TryAttachToProcess(int processId, [NotNullWhen(true)] out ShellCommand? attachedCommand) {
            return this.TryAttachToProcess(processId, null, out attachedCommand);
        }

        /// <summary>
        /// Tries to attach to an already running process, given its <paramref name="processId"/>
        /// and <paramref name="options"/>,  giving <paramref name="attachedCommand" /> representing
        /// the process and returning true if this succeeded, otherwise false.
        /// </summary>
        public bool TryAttachToProcess(int processId, Action<ShellOptions>? options, [NotNullWhen(true)] out ShellCommand? attachedCommand) {
            var finalOptions = this.GetOptions(options);
            if (finalOptions.ProcessStreamEncoding != null || finalOptions.StartInfoInitializers.Count != 0)
                throw new InvalidOperationException(
                    "Setting encoding or using StartInfo initializers is not available when attaching to an already running process.");

            attachedCommand = null;
            Process? process = null;
            try {
                process = Process.GetProcessById(processId);

                // Since simply getting (Safe)Handle from the process enables us to read
                // the exit code later, and handle itself is disposed when the whole class
                // is disposed, we do not need its value. Hence the bogus call to GetType().
                process.Handle.GetType();
                process.SafeHandle.GetType();
            }
            catch (Exception e) when (IsIgnorableAttachingException(e)) {
                process?.Dispose();
                return false;
            }
            catch (Exception e) when (e is Win32Exception || e is NotSupportedException) {
                throw new InvalidOperationException(
                    "Could not attach to the process from reasons other than it had already exited. See inner exception for details.",
                    e);
            }

            attachedCommand = new AttachedCommand(
                process,
                finalOptions.ThrowExceptionOnError,
                finalOptions.ProcessTimeout,
                finalOptions.ProcessCancellationToken,
                finalOptions.DisposeProcessOnExit);
            return true;
        }

        /// <summary>
        /// Executes the given <paramref name="executable"/> with the given <paramref name="arguments"/>
        /// </summary>
        public ShellCommand Run(string executable, params object[] arguments) {
            Throw.IfNull(arguments, "arguments");

            return this.Run(executable, arguments.AsEnumerable());
        }

        #endregion

        /// <summary>
        /// A <see cref="Shell"/> that uses default options
        /// </summary>
        public static Shell Default { get; } = new(_ => { });

        private ShellOptions GetOptions(Action<ShellOptions>? additionalConfiguration) {
            var builder = new ShellOptions();
            this.Configuration.Invoke(builder);
            additionalConfiguration?.Invoke(builder);
            return builder;
        }

        // process has already exited or ID is invalid or
        // process exited after its creation but before taking its handle
        private static bool IsIgnorableAttachingException(Exception exception) => exception is ArgumentException or InvalidOperationException;

        public sealed class ShellOptions {
            internal ShellOptions() {
                this.RestoreDefaults();
            }

            internal List<Action<ProcessStartInfo>> StartInfoInitializers { get; private set; } = default!; // assigned in RestoreDefaults
            internal List<Func<ShellCommand, ShellCommand>> CommandInitializers { get; private set; } = default!;     // assigned in RestoreDefaults
            internal CommandLineSyntax CommandLineSyntax { get; private set; } = default!;                  // assigned in RestoreDefaults
            internal bool ThrowExceptionOnError { get; private set; }
            internal bool DisposeProcessOnExit { get; private set; }
            internal TimeSpan ProcessTimeout { get; private set; }
            internal Encoding? ProcessStreamEncoding { get; private set; }
            internal CancellationToken ProcessCancellationToken { get; private set; }

            /// <summary>
            /// Restores all settings to the default value
            /// </summary>
            public ShellOptions RestoreDefaults() {
                this.StartInfoInitializers = new List<Action<ProcessStartInfo>>();
                this.CommandInitializers = new List<Func<ShellCommand, ShellCommand>>();
                this.CommandLineSyntax = PlatformCompatibilityHelper.GetDefaultCommandLineSyntax();
                this.ThrowExceptionOnError = false;
                this.DisposeProcessOnExit = true;
                this.ProcessTimeout = System.Threading.Timeout.InfiniteTimeSpan;
                this.ProcessStreamEncoding = null;
                this.ProcessCancellationToken = System.Threading.CancellationToken.None;
                return this;
            }

            /// <summary>
            /// Specifies a function which can modify the <see cref="ProcessStartInfo"/>. Multiple such functions
            /// can be specified this way
            /// </summary>
            public ShellOptions StartInfo(Action<ProcessStartInfo> initializer) {
                Throw.IfNull(initializer, nameof(initializer));

                this.StartInfoInitializers.Add(initializer);
                return this;
            }

            /// <summary>
            /// Specifies a function which can modify the <see cref="Scriptable.ShellCommand"/>. Multiple such functions
            /// can be specified this way
            /// </summary>
            public ShellOptions Command(Action<ShellCommand> initializer) {
                Throw.IfNull(initializer, nameof(initializer));
                this.Command(c => {
                    initializer(c);
                    return c;
                });
                return this;
            }

            /// <summary>
            /// Specifies a function which can project the <see cref="Scriptable.ShellCommand"/> to a new <see cref="Scriptable.ShellCommand"/>.
            /// Intended to be used with <see cref="Scriptable.ShellCommand"/>-producing "pipe" functions like <see cref="Scriptable.ShellCommand.RedirectTo(System.Collections.Generic.ICollection{char})"/>
            /// </summary>
            public ShellOptions Command(Func<ShellCommand, ShellCommand> initializer) {
                Throw.IfNull(initializer, nameof(initializer));
                this.CommandInitializers.Add(initializer);
                return this;
            }

            /// <summary>
            /// Sets the initial working directory of the <see cref="Scriptable.ShellCommand"/> (defaults to the current working directory)
            /// </summary>.
            public ShellOptions WorkingDirectory(string path) {
                return this.StartInfo(psi => psi.WorkingDirectory = path);
            }

            /// <summary>
            /// Adds or overwrites an environment variable to be passed to the <see cref="Scriptable.ShellCommand"/>
            /// </summary>
            public ShellOptions EnvironmentVariable(string name, string value) {
                Throw.If(string.IsNullOrEmpty(name), "name is required");

                return this.StartInfo(psi => psi.EnvironmentVariables[name] = value);
            }

            /// <summary>
            /// Adds or overwrites a set of environmental variables to be passed to the <see cref="Scriptable.ShellCommand"/>
            /// </summary>
            public ShellOptions EnvironmentVariables(IEnumerable<KeyValuePair<string, string>> environmentVariables) {
                Throw.IfNull(environmentVariables, "environmentVariables");

                var environmentVariablesList = environmentVariables.ToList();
                return this.StartInfo(psi => environmentVariablesList.ForEach(kvp => psi.EnvironmentVariables[kvp.Key] = kvp.Value));
            }

            /// <summary>
            /// If specified, a non-zero exit code will cause the <see cref="Scriptable.ShellCommand"/>'s <see cref="Task"/> to fail
            /// with <see cref="ErrorExitCodeException"/>. Defaults to false
            /// </summary>
            public ShellOptions ThrowOnError(bool value = true) {
                this.ThrowExceptionOnError = value;
                return this;
            }

            /// <summary>
            /// If specified, the underlying <see cref="Process"/> object for the command will be disposed when the process exits.
            /// This means that there is no need to dispose of a <see cref="Scriptable.ShellCommand"/>.
            ///
            /// This also means that <see cref="Scriptable.ShellCommand.Process"/> cannot be reliably accessed,
            /// since it may exit at any time.
            ///
            /// Defaults to true
            /// </summary>
            public ShellOptions DisposeOnExit(bool value = true) {
                this.DisposeProcessOnExit = value;
                return this;
            }

            /// <summary>
            /// Specifies the <see cref="CommandLineSyntax"/> to use for escaping arguments. Defaults to
            /// an appropriate value for the current platform
            /// </summary>
            [Obsolete("The default should work across platforms")]
            public ShellOptions Syntax(CommandLineSyntax syntax) {
                Throw.IfNull(syntax, "syntax");

                this.CommandLineSyntax = syntax;
                return this;
            }

            /// <summary>
            /// Specifies a timeout after which the process should be killed. Defaults to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>
            /// </summary>
            public ShellOptions Timeout(TimeSpan timeout) {
                Throw<ArgumentOutOfRangeException>.If(timeout < TimeSpan.Zero && timeout != System.Threading.Timeout.InfiniteTimeSpan, "timeout");

                this.ProcessTimeout = timeout;
                return this;
            }

            /// <summary>
            /// Specifies an <see cref="Encoding"/> to be used for StandardOutput, StandardError, and StandardInput.
            ///
            /// By default, <see cref="Process"/> will use <see cref="Console.OutputEncoding"/> for output streams and <see cref="Console.InputEncoding"/>
            /// for input streams.
            ///
            /// Note that the output encodings can be set individually via <see cref="ProcessStartInfo"/>. If you need to specify different encodings
            /// for different streams, use this method to set the StandardInput encoding and then use <see cref="StartInfo(Action{ProcessStartInfo})"/>
            /// to further override the two output encodings
            /// </summary>
            public ShellOptions Encoding(Encoding encoding) {
                Throw.IfNull(encoding, nameof(encoding));

                this.ProcessStreamEncoding = encoding;
                return this;
            }

            /// <summary>
            /// Specifies a <see cref="System.Threading.CancellationToken"/> which will abort the command when canceled.
            /// When a command is aborted, the underlying process will be killed as if using <see cref="Scriptable.ShellCommand.Kill"/>
            /// </summary>
            public ShellOptions CancellationToken(CancellationToken cancellationToken) {
                this.ProcessCancellationToken = cancellationToken;
                return this;
            }
        }
    }
}
