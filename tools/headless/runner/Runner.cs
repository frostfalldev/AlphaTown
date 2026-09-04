using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine.TestTools;

/// <summary>
/// A small NUnit-attribute test runner, so the EditMode suite can run without a Unity Editor.
///
/// It exists because the whole simulation was written and shipped without its tests ever having
/// been executed — Unity is the only thing that can normally run them, and there is no Unity here.
/// Assertions come from the real NUnit assembly; this only handles discovery, fixture lifecycle,
/// and Unity's LogAssert rules, which a stock NUnit runner knows nothing about.
/// </summary>
internal static class Runner
{
    sealed class Failure
    {
        public string Fixture;
        public string Test;
        public string Message;
        public string Trace;
        public List<string> Logs = new List<string>();
    }

    static int Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : "AlphaTown.Tests.EditMode.dll";
        var filter = args.Length > 1 ? args[1] : null;

        var assembly = Assembly.LoadFrom(path);
        var failures = new List<Failure>();
        var passed = 0;
        var skipped = 0;

        var stopwatch = Stopwatch.StartNew();

        foreach (var fixture in assembly.GetTypes().OrderBy(t => t.FullName))
        {
            if (fixture.IsAbstract || fixture.IsInterface) continue;

            var tests = fixture.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute"))
                .OrderBy(m => m.Name)
                .ToArray();

            if (tests.Length == 0) continue;

            var setUps = Lifecycle(fixture, "SetUpAttribute");
            var tearDowns = Lifecycle(fixture, "TearDownAttribute");

            foreach (var test in tests)
            {
                if (filter != null && fixture.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    test.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    skipped++;
                    continue;
                }

                LogCapture.Reset();
                var failure = RunOne(fixture, test, setUps, tearDowns);

                if (failure == null) { passed++; Console.Write("."); }
                else { failures.Add(failure); Console.Write("F"); }

                if ((passed + failures.Count) % 72 == 0) Console.WriteLine();
            }
        }

        stopwatch.Stop();
        Console.WriteLine();
        Console.WriteLine();

        foreach (var failure in failures)
        {
            Console.WriteLine("FAILED  " + failure.Fixture + "." + failure.Test);
            Console.WriteLine("        " + failure.Message.Replace("\n", "\n        "));

            foreach (var log in failure.Logs) Console.WriteLine("        | " + log);

            if (!string.IsNullOrEmpty(failure.Trace))
            {
                var line = failure.Trace.Split('\n').FirstOrDefault(l => l.Contains("AlphaTown"));
                if (line != null) Console.WriteLine("        at" + line.Trim().Substring(2));
            }

            Console.WriteLine();
        }

        Console.WriteLine(failures.Count == 0
            ? "PASSED  " + passed + " tests in " + stopwatch.Elapsed.TotalSeconds.ToString("0.00") + "s"
            : "FAILED  " + failures.Count + " of " + (passed + failures.Count) + " tests");

        if (skipped > 0) Console.WriteLine("        " + skipped + " skipped by filter");

        return failures.Count == 0 ? 0 : 1;
    }

    static MethodInfo[] Lifecycle(Type fixture, string attributeName) =>
        fixture.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == attributeName))
            .ToArray();

    static Failure RunOne(Type fixture, MethodInfo test, MethodInfo[] setUps, MethodInfo[] tearDowns)
    {
        var failure = new Failure { Fixture = fixture.Name, Test = test.Name };

        try
        {
            var instance = Activator.CreateInstance(fixture);

            foreach (var setUp in setUps) setUp.Invoke(instance, null);

            try
            {
                test.Invoke(instance, null);
            }
            finally
            {
                foreach (var tearDown in tearDowns) tearDown.Invoke(instance, null);
            }

            // Unity fails a test that logs an unexpected error, or that set an expectation which
            // never fired. Skipping this would let error-path tests pass for the wrong reason.
            var logProblem = LogCapture.Describe();
            if (logProblem != null)
            {
                failure.Message = logProblem;
                Attach(failure);
                return failure;
            }

            return null;
        }
        catch (TargetInvocationException wrapped)
        {
            var inner = wrapped.InnerException ?? wrapped;
            failure.Message = inner.GetType().Name + ": " + inner.Message;
            failure.Trace = inner.StackTrace;
            Attach(failure);
            return failure;
        }
        catch (Exception exception)
        {
            failure.Message = exception.GetType().Name + ": " + exception.Message;
            failure.Trace = exception.StackTrace;
            Attach(failure);
            return failure;
        }
    }

    static void Attach(Failure failure)
    {
        foreach (var entry in LogCapture.Logged)
        {
            failure.Logs.Add("[" + entry.Type + "] " + entry.Message);
        }
    }
}
