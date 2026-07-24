using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Minimal reflection-based NUnit runner: executes the project's real EditMode
// tests against the real domain code, entirely outside Unity.
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
                object inst = Activator.CreateInstance(fixture);
                try
                {
                    setup?.Invoke(inst, null);
                    m.Invoke(inst, null);
                    pass++;
                    Console.WriteLine($"  PASS  {m.Name}");
                }
                catch (TargetInvocationException tie) when (tie.InnerException is IgnoreException)
                {
                    ignored++;
                    Console.WriteLine($"  SKIP  {m.Name} (Assert.Ignore)");
                }
                catch (TargetInvocationException tie)
                {
                    fail++;
                    var msg = tie.InnerException?.Message?.Replace("\n", " | ").Trim() ?? "?";
                    if (msg.Length > 160) msg = msg.Substring(0, 160);
                    Console.WriteLine($"  FAIL  {m.Name}: {msg}");
                }
            }
        }

        Console.WriteLine($"\n======== TOTAL: {pass} passed, {fail} failed, {ignored} ignored ========");
        return fail == 0 ? 0 : 1;
    }
}
