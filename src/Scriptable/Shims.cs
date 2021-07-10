using System;
using System.Diagnostics.CodeAnalysis;

namespace Scriptable {
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "Shim")]
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute {
        public NotNullWhenAttribute(bool returnValue) {
            this.ReturnValue = returnValue;
        }

        public bool ReturnValue { get; }
    }
}