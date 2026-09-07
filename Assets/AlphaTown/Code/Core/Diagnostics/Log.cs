using System;
using System.Diagnostics;

namespace AlphaTown.Core.Diagnostics
{
    /// <summary>
    /// Logging facade. Info calls are compiled out of release builds entirely — the
    /// [Conditional] attribute removes the call site, so the interpolated string is never
    /// built and never allocates on device.
    ///
    /// Warnings and errors always compile in: if it matters in the editor it matters in the wild.
    /// </summary>
    public static class Log
    {
        const string Editor = "UNITY_EDITOR";
        const string Development = "DEVELOPMENT_BUILD";
        /// <summary>Define this in Player Settings to keep verbose logs in a release build.</summary>
        const string Verbose = "ALPHATOWN_VERBOSE_LOGS";

        [Conditional(Editor), Conditional(Development), Conditional(Verbose)]
        public static void Info(string message) => UnityEngine.Debug.Log(message);

        [Conditional(Editor), Conditional(Development), Conditional(Verbose)]
        public static void Info(string category, string message) =>
            UnityEngine.Debug.Log("[" + category + "] " + message);

        public static void Warn(string message) => UnityEngine.Debug.LogWarning(message);

        public static void Warn(string category, string message) =>
            UnityEngine.Debug.LogWarning("[" + category + "] " + message);

        public static void Error(string message) => UnityEngine.Debug.LogError(message);

        public static void Error(string category, string message) =>
            UnityEngine.Debug.LogError("[" + category + "] " + message);

        public static void Exception(Exception exception) => UnityEngine.Debug.LogException(exception);
    }
}
