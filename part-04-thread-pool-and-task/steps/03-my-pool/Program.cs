using System.Diagnostics;

const int Jobs = 10_000;

var pool = new MyPool(Environment.ProcessorCount);
using var done = new CountdownEvent(Jobs);

var clock = Stopwatch.StartNew();
long total = 0;
for (int i = 0; i < Jobs; i++)
{
    pool.Add(() =>
    {
        int found = CountPrimes(1_000);
        Interlocked.Add(ref total, found);
        done.Signal();
    });
}
done.Wait();

int threads = Process.GetCurrentProcess().Threads.Count;
Console.WriteLine($"jobs:   {Jobs:N0}, on {pool.Size} pool threads");
Console.WriteLine($"primes: {total:N0}");
Console.WriteLine($"time:   {clock.ElapsedMilliseconds:N0} ms");
Console.WriteLine($"threads in this process: {threads}");

// One job: a small piece of CPU work, like resizing one picture.
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

// A queue of jobs, and a few threads that take jobs from it forever.
class MyPool
{
    readonly Queue<Action> jobs = new();
    readonly object gate = new();

    public int Size { get; }

    public MyPool(int size)
    {
        Size = size;
        for (int i = 0; i < size; i++)
        {
            var worker = new Thread(Work);
            worker.Name = $"pool-{i}";
            worker.IsBackground = true;
            worker.Start();
        }
    }

    public void Add(Action job)
    {
        lock (gate)
        {
            jobs.Enqueue(job);
            Monitor.Pulse(gate);
        }
    }

    void Work()
    {
        while (true)
        {
            Action job;
            lock (gate)
            {
                while (jobs.Count == 0)
                {
                    Monitor.Wait(gate);
                }
                job = jobs.Dequeue();
            }
            job();
        }
    }
}
