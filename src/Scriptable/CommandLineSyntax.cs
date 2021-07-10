using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Scriptable {
    /// <summary>
    /// Acts as an abstract class for handling the escaping of arguments when passed to a <see cref="Process"/>.
    /// </summary>
    public abstract class CommandLineSyntax {
        /// <summary>
        /// Creates a combined argument string that can be used with <see cref="ProcessStartInfo.Arguments"/>. The combined
        /// string should escape all arguments appropriately so that the process receives the same strings as passed into this method
        /// </summary>
        /// <param name="arguments">the arguments to the process</param>
        /// <returns>the argument string</returns>
        public abstract string CreateArgumentString(IEnumerable<string> arguments);

        internal static string CreateArgumentString(IEnumerable<string> arguments, Action<string, StringBuilder> appendArgument) {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            if (appendArgument == null)
                throw new ArgumentNullException(nameof(appendArgument));

            var builder = new StringBuilder();
            var isFirstArgument = true;
            foreach (var argument in arguments) {
                if (argument == null) throw new ArgumentException("must not contain null", nameof(arguments));

                if (isFirstArgument)
                    isFirstArgument = false;
                else
                    builder.Append(' ');
                appendArgument(argument, builder);
            }

            return builder.ToString();
        }
    }
}
