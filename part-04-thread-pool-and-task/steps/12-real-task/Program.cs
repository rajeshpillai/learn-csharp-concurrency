using System.Diagnostics;

Thread.CurrentThread.Name = "main";
var clock = Stopwatch.StartNew();

Task<int> receipt = Task.Run(() => CountPrimes(2_000_000));
_ = receipt.ContinueWith(t => Console.WriteLine(
    $"{clock.ElapsedMilliseconds,4} ms: {t.Result:N0} primes, " +
    $"printed by {Thread.CurrentThread.Name}"));
Console.WriteLine($"{clock.ElapsedMilliseconds,4} ms: main is free, " +
    $"status {receipt.Status}");
Console.WriteLine($"result: {receipt.Result:N0}, status {receipt.Status}");

var pool = new MyPool(4);
Task<int> ours = pool.Run(() => CountPrimes(1_000));
Console.WriteLine($"from our pool: {ours.Result}, status {ours.Status}");
Thread.Sleep(100);    // let the continuation print

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

    public int Queued
    {
        get { lock (gate) return jobs.Count; }
    }

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

    public Task<T> Run<T>(Func<T> work)
    {
        var receipt = new TaskCompletionSource<T>();
        Add(() =>
        {
            try
            {
                receipt.SetResult(work());
            }
            catch (Exception e)
            {
                receipt.SetException(e);
            }
        });
        return receipt.Task;
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
