using System.Reflection;

namespace Scriptable.Utilities {
    internal static class Helpers {
        public static T As<T>(this T @this) => @this;

        public static Assembly GetScriptableShellMedallion() => Assembly.GetExecutingAssembly();
    }
}
