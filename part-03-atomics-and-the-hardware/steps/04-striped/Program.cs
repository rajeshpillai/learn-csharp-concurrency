using System.Diagnostics;

int threads = args.Length > 0 ? int.Parse(args[0]) : 1;
const long Total = 120_000_000;
long each = Total / threads;

var requests = new Counter();
var clock = Stopwatch.StartNew();

var workers = new List<Thread>();
for (int k = 0; k < threads; k++)
{
    var worker = new Thread(() =>
    {
        for (long i = 0; i < each; i++)
            requests.Increment();
    });
    workers.Add(worker);
    worker.Start();
}
foreach (var worker in workers) worker.Join();

long ms = clock.ElapsedMilliseconds;
double ns = ms * 1e6 / Total;
Console.WriteLine($"threads: {threads}, count: {requests.Value:N0}");
Console.WriteLine($"time: {ms} ms, {ns:F1} ns per increment");

class Counter
{
    readonly long[] _cells = new long[Environment.ProcessorCount];

    public void Increment()
    {
        int slot = Environment.CurrentManagedThreadId % _cells.Length;
        Interlocked.Increment(ref _cells[slot]);
    }

    public long Value
    {
        get
        {
            long sum = 0;
            for (int i = 0; i < _cells.Length; i++)
                sum += Interlocked.Read(ref _cells[i]);
            return sum;
        }
    }
}
