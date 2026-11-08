const int Requests = 200;

int started = 0;
for (int i = 0; i < Requests; i++)
{
    ThreadPool.QueueUserWorkItem(_ =>
    {
        Interlocked.Increment(ref started);
        Thread.Sleep(Timeout.Infinite);    // the database never answers
    });
}

for (int s = 1; s <= 12; s++)
{
    Thread.Sleep(1_000);
    int now = Volatile.Read(ref started);
    Console.WriteLine($"{s,3} s: {now,3} started, " +
        $"{ThreadPool.ThreadCount,3} pool threads");
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
