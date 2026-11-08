using System.Diagnostics;

var pool = new MyPool(Environment.ProcessorCount);
var clock = Stopwatch.StartNew();

MyTask<int> receipt = pool.Run(() => CountPrimes(2_000_000));
Console.WriteLine($"job added. Done yet? {receipt.IsCompleted}");
Console.WriteLine($"primes below 2 million: {receipt.Result:N0}");
Console.WriteLine($"done yet? {receipt.IsCompleted}");
Console.WriteLine($"main thread waited {clock.ElapsedMilliseconds} ms");

MyTask<int> broken = pool.Run<int>(() =>
    throw new InvalidOperationException("the database is down"));
try
{
    Console.WriteLine(broken.Result);
}
catch (Exception e)
{
    Console.WriteLine($"caught: {e.Message}: {e.InnerException?.Message}");
}
Console.WriteLine("the main thread is still running");

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

    public MyTask<T> Run<T>(Func<T> work)
    {
        var task = new MyTask<T>();
        Add(() =>
        {
            try
            {
                task.SetResult(work());
            }
            catch (Exception e)
            {
                task.SetException(e);
            }
        });
        return task;
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

// A receipt for one job: empty at first, then a result or an error.
class MyTask<T>
{
    readonly object gate = new();
    bool done;
    T? result;
    Exception? error;

    public bool IsCompleted
    {
        get { lock (gate) return done; }
    }

    public void SetResult(T value) => Finish(value, null);

    public void SetException(Exception e) => Finish(default, e);

    void Finish(T? value, Exception? e)
    {
        lock (gate)
        {
            if (done) throw new InvalidOperationException("already done");
            result = value;
            error = e;
            done = true;
            Monitor.PulseAll(gate);
        }
    }

    public T Result
    {
        get
        {
            lock (gate)
            {
                while (!done)
                {
                    Monitor.Wait(gate);
                }
                if (error != null)
                {
                    throw new Exception("the job failed", error);
                }
                return result!;
            }
        }
    }
}
