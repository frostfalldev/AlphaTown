using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityEngine.TestTools
{
    /// <summary>
    /// Mirrors Unity's LogAssert closely enough to keep the tests honest: an unexpected error or
    /// exception fails the test, and an expectation that never fires fails it too. Without both
    /// halves, tests that assert on error paths would quietly pass for the wrong reason.
    /// </summary>
    public static class LogAssert
    {
        public static bool ignoreFailingMessages { get; set; }

        public static void Expect(LogType type, Regex pattern) =>
            LogCapture.Expect(type, pattern.ToString());

        public static void Expect(LogType type, string message) =>
            LogCapture.Expect(type, Regex.Escape(message));

        public static void NoUnexpectedReceived() => LogCapture.AssertClean();
    }

    /// <summary>Collects what was logged so the runner can enforce Unity's rules after each test.</summary>
    public static class LogCapture
    {
        public sealed class Entry
        {
            public LogType Type;
            public string Message;
        }

        static readonly List<Entry> Unexpected = new List<Entry>();
        static readonly List<Entry> All = new List<Entry>();
        static readonly List<KeyValuePair<LogType, string>> Expectations =
            new List<KeyValuePair<LogType, string>>();

        /// <summary>Everything logged during the current test, printed only if it fails.</summary>
        public static IReadOnlyList<Entry> Logged => All;

        public static void Record(LogType type, object message)
        {
            var text = message == null ? string.Empty : message.ToString();
            All.Add(new Entry { Type = type, Message = text });

            for (var i = 0; i < Expectations.Count; i++)
            {
                if (Expectations[i].Key != type) continue;
                if (!Regex.IsMatch(text, Expectations[i].Value)) continue;

                Expectations.RemoveAt(i);
                return;
            }

            // Unity applies the ignore flag as each message is written, not at the end of the
            // test — so a block that flips it on and off again really does swallow what it wraps.
            if (LogAssert.ignoreFailingMessages) return;

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Unexpected.Add(new Entry { Type = type, Message = text });
        }

        public static void Expect(LogType type, string pattern) =>
            Expectations.Add(new KeyValuePair<LogType, string>(type, pattern));

        public static void Reset()
        {
            All.Clear();
            Unexpected.Clear();
            Expectations.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>Returns null when the test's logs were clean, or a description of what was not.</summary>
        public static string Describe()
        {
            if (Expectations.Count > 0)
                return "expected a " + Expectations[0].Key + " log matching /" + Expectations[0].Value +
                       "/ but none was written";

            if (Unexpected.Count == 0) return null;

            return "unexpected " + Unexpected[0].Type + " log: " + Unexpected[0].Message;
        }

        public static void AssertClean()
        {
            var problem = Describe();
            if (problem != null) throw new Exception(problem);
        }
    }
}
