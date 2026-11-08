// What SetMinThreads does to plain CPU work, with no blocking at all.
//   dotnet run -- 12      the default on this laptop
//   dotnet run -- 200
using System.Diagnostics;

const int Jobs = 2_000;
int min = args.Length > 0 ? int.Parse(args[0]) : 12;

ThreadPool.GetMinThreads(out _, out int io);
ThreadPool.SetMinThreads(min, io);

using var done = new CountdownEvent(Jobs);
var clock = Stopwatch.StartNew();
for (int i = 0; i < Jobs; i++)
{
    ThreadPool.QueueUserWorkItem(_ =>
    {
        CountPrimes(20_000);
        done.Signal();
    });
}
done.Wait();

var me = Process.GetCurrentProcess();
Console.WriteLine($"min threads {min}: " +
    $"{clock.ElapsedMilliseconds:N0} ms, " +
    $"{ThreadPool.ThreadCount} pool threads");
Console.WriteLine($"CPU time used: " +
    $"{me.TotalProcessorTime.TotalMilliseconds:N0} ms");

static int CountPrimes(int limit)
{
    int count = 0;
    for (int n = 2; n < limit; n++)
    {
        bool prime = true;
        for (int d = 2; d * d <= n; d++)
        {
            if (n % d == 0) { prime = false; break; }
        }
        if (prime) count++;
    }
    return count;
}
