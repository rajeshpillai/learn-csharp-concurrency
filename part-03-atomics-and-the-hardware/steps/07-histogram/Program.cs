using System.Diagnostics;

int threads = args.Length > 0 ? int.Parse(args[0]) : 1;
int work = args.Length > 1 ? int.Parse(args[1]) : 0;
const int Total = 4_800_000;
int each = Total / threads;

var latency = new Histogram();
long retries = 0;
var clock = Stopwatch.StartNew();

var workers = new List<Thread>();
for (int k = 0; k < threads; k++)
{
    var worker = new Thread(() =>
    {
        var random = new Random();
        long mine = 0;
        for (int i = 0; i < each; i++)
        {
            long start = Stopwatch.GetTimestamp();
            // Pretend to handle a request: spin for a short while.
            Thread.SpinWait(random.Next(work + 1));
            var took = Stopwatch.GetElapsedTime(start);
            mine += latency.Record(took.TotalMicroseconds);
        }
        Interlocked.Add(ref retries, mine);
    });
    workers.Add(worker);
    worker.Start();
}
foreach (var worker in workers) worker.Join();

long ms = clock.ElapsedMilliseconds;
Console.WriteLine($"threads: {threads}, requests: {Total:N0}");
Console.WriteLine($"time: {ms} ms");
latency.Print();
Console.WriteLine($"retries: {retries:N0}");

class Histogram
{
    readonly long[] _buckets = new long[16];
    double _sum;
    double _max;

    // Returns how many times it had to try again.
    public int Record(double us)
    {
        int b = us < 1 ? 0 : 1 + (int)Math.Log2(us);
        b = Math.Min(b, _buckets.Length - 1);
        Interlocked.Increment(ref _buckets[b]);
        return AddToSum(us) + RaiseMax(us);
    }

    int AddToSum(double us)
    {
        int retries = 0;
        double seen = _sum;
        while (true)
        {
            double was =
                Interlocked.CompareExchange(ref _sum, seen + us, seen);
            if (was == seen) return retries;
            seen = was;
            retries++;
        }
    }

    int RaiseMax(double us)
    {
        int retries = 0;
        double seen = _max;
        while (us > seen)
        {
            double was =
                Interlocked.CompareExchange(ref _max, us, seen);
            if (was == seen) break;
            seen = was;
            retries++;
        }
        return retries;
    }

    public void Print()
    {
        long count = _buckets.Sum();
        long top = _buckets.Max();
        for (int i = 0; i < _buckets.Length; i++)
        {
            long n = _buckets[i];
            if (n == 0) continue;
            string range = i == 0
                ? "under 1"
                : $"{1 << (i - 1)}-{1 << i}";
            string bar = new string('#', (int)(40 * n / top));
            Console.WriteLine($"{range,11} us {bar,-40} {n:N0}");
        }
        Console.WriteLine(
            $"mean {_sum / count:F2} us, max {_max:F1} us");
    }
}
