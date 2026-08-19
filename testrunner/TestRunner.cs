using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

#nullable enable

// Minimal reflection-based NUnit runner: executes the project's real EditMode
// tests against the real domain code, entirely outside Unity.
// Supports [Test], [TestCase(...)] (expanded per case), [SetUp], [Ignore],
// and Assert.Ignore. Each TestCase is counted as an individual test.
public static class TestRunnerMain
{
    public static int Main()
    {
        int pass = 0, fail = 0, ignored = 0;
        var asm = Assembly.GetExecutingAssembly();
        var fixtures = asm.GetTypes()
            .Where(t => t.Namespace == "ChromaVale.Tests" && t.GetMethods().Any(m => m.GetCustomAttribute<TestAttribute>() != null))
            .OrderBy(t => t.Name);

        foreach (var fixture in fixtures)
        {
            Console.WriteLine($"\n== {fixture.Name} ==");
            var setup = fixture.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<SetUpAttribute>() != null);

            foreach (var m in fixture.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null))
            {
                if (m.GetCustomAttribute<IgnoreAttribute>() != null)
                {
                    ignored++;
                    Console.WriteLine($"  SKIP  {m.Name} [Ignore]");
                    continue;
                }

                var cases = m.GetCustomAttributes<TestCaseAttribute>().ToArray();
                if (cases.Length > 0)
                {
                    foreach (var tc in cases)
                    {
                        if (tc.Ignore != null)
                        {
                            ignored++;
                            Console.WriteLine($"  SKIP  {m.Name}({FormatArgs(tc.Arguments)}) [Ignore]");
                            continue;
                        }
                        RunOne(fixture, setup, m, tc.Arguments, ref pass, ref fail, ref ignored);
                    }
                }
                else if (m.GetParameters().Any(p => p.GetCustomAttribute<ValuesAttribute>() != null))
                {
                    // Expand [Values(...)] combinatorially (cartesian product across parameters).
                    var paramLists = m.GetParameters()
                        .Select(p =>
                        {
                            var va = p.GetCustomAttribute<ValuesAttribute>();
                            if (va != null)
                            {
                                var data = ReadValuesAttribute(va);
                                if (data.Length > 0) return data;
                                // Bare [Values] on an enum param → all enum values.
                                if (p.ParameterType.IsEnum)
                                    return Enum.GetValues(p.ParameterType).Cast<object>().ToArray();
                            }
                            return new object[] { null! };
                        })
                        .ToArray();
                    foreach (var combo in Cartesian(paramLists))
                        RunOne(fixture, setup, m, combo, ref pass, ref fail, ref ignored);
                }
                else
                {
                    RunOne(fixture, setup, m, null, ref pass, ref fail, ref ignored);
                }
            }
        }

        Console.WriteLine($"\n======== TOTAL: {pass} passed, {fail} failed, {ignored} ignored ========");
        return fail == 0 ? 0 : 1;
    }

    private static void RunOne(Type fixture, MethodInfo? setup, MethodInfo m, object[]? args,
        ref int pass, ref int fail, ref int ignored)
    {
        string label = args == null ? m.Name : $"{m.Name}({FormatArgs(args)})";
        object inst = Activator.CreateInstance(fixture);
        try
        {
            setup?.Invoke(inst, null);
            m.Invoke(inst, args);
            pass++;
            Console.WriteLine($"  PASS  {label}");
        }
        catch (TargetInvocationException tie) when (tie.InnerException is IgnoreException)
        {
            ignored++;
            Console.WriteLine($"  SKIP  {label} (Assert.Ignore)");
        }
        catch (TargetInvocationException tie)
        {
            fail++;
            var msg = tie.InnerException?.Message?.Replace("\n", " | ").Trim() ?? "?";
            if (msg.Length > 160) msg = msg.Substring(0, 160);
            Console.WriteLine($"  FAIL  {label}: {msg}");
        }
    }

    private static string FormatArgs(object[]? args)
    {
        if (args == null || args.Length == 0) return "";
        return string.Join(", ", args.Select(a => a?.ToString() ?? "null"));
    }

    /// <summary>
    /// Reads the value list from a ValuesAttribute. NUnit 3.x exposes no public
    /// accessor (IParameterDataSource.GetData takes NUnit's internal IParameterInfo),
    /// so read the stable private field `_data` (object?[]), with `data` as a
    /// fallback. A null here would be coerced by MethodInfo.Invoke to default(enum),
    /// silently corrupting every [Values] test — hence return empty, not null.
    /// </summary>
    private static object[] ReadValuesAttribute(ValuesAttribute va)
    {
        var f = va.GetType().GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)
             ?? va.GetType().GetField("data", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f != null && f.GetValue(va) is object[] arr) return arr;
        return Array.Empty<object>();
    }

    /// <summary>Cartesian product of per-parameter value lists (for [Values] expansion).</summary>
    private static IEnumerable<object[]> Cartesian(object[][] lists)
    {
        if (lists.Length == 0)
        {
            yield return Array.Empty<object>();
            yield break;
        }

        foreach (var first in lists[0])
        {
            if (lists.Length == 1)
            {
                yield return new[] { first };
                continue;
            }

            foreach (var rest in Cartesian(lists.Skip(1).ToArray()))
            {
                var combo = new object[1 + rest.Length];
                combo[0] = first;
                Array.Copy(rest, 0, combo, 1, rest.Length);
                yield return combo;
            }
        }
    }
}
