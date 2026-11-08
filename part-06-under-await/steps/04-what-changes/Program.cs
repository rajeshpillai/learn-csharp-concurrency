using System.Diagnostics;

// Two things we can measure: the live stack, and what each call costs.
await Chain.OuterAsync(show: true);

const int Calls = 100_000;
for (int i = 0; i < 1_000; i++)
    await Chain.OuterAsync(show: false); // warm up first

long before = GC.GetTotalAllocatedBytes(precise: true);
var clock = Stopwatch.StartNew();
for (int i = 0; i < Calls; i++)
    await Chain.OuterAsync(show: false);
clock.Stop();
long bytes = GC.GetTotalAllocatedBytes(precise: true) - before;
double ns = clock.Elapsed.TotalNanoseconds / Calls;
Console.WriteLine($"{Calls:N0} calls, each: {bytes / Calls} bytes, {ns:N0} ns");

// Three async methods, one inside the other. Only the inner one waits.
static class Chain
{
    public static async Task OuterAsync(bool show)
    {
        await MiddleAsync(show);
    }

    static async Task MiddleAsync(bool show)
    {
        await InnerAsync(show);
    }

    static async Task InnerAsync(bool show)
    {
        if (show)
            PrintStack();
        await Task.Yield(); // wait once: the rest runs later
    }

    static void PrintStack()
    {
        var frames = new StackTrace().GetFrames();
        foreach (var frame in frames[1..])
        {
            var m = frame.GetMethod()!;
            Console.WriteLine($"  {m.DeclaringType?.Name}.{m.Name}");
        }
        Console.WriteLine($"{frames.Length - 1} frames on the stack");
    }
}
