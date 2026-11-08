using System.Diagnostics;

const int Requests = 200;

using var done = new CountdownEvent(Requests);
var clock = Stopwatch.StartNew();

for (int i = 0; i < Requests; i++)
{
    ThreadPool.QueueUserWorkItem(_ =>
    {
        Thread.Sleep(1_000);    // waiting for the database
        done.Signal();
    });
}
long healthAt = 0;
ThreadPool.QueueUserWorkItem(_ => healthAt = clock.ElapsedMilliseconds);

while (!done.Wait(2_000))
{
    Console.WriteLine($"{clock.Elapsed.TotalSeconds,4:F0} s: " +
        $"{Requests - done.CurrentCount,3} done, " +
        $"{ThreadPool.PendingWorkItemCount,3} queued, " +
        $"{ThreadPool.ThreadCount,3} pool threads");
}

var me = Process.GetCurrentProcess();
Console.WriteLine($"all {Requests} done: " +
    $"{clock.ElapsedMilliseconds:N0} ms");
Console.WriteLine($"health check waited: {healthAt:N0} ms");
Console.WriteLine($"CPU time used: " +
    $"{me.TotalProcessorTime.TotalMilliseconds:N0} ms");
Console.WriteLine($"pool threads at the end: {ThreadPool.ThreadCount}");
Console.WriteLine($"memory in use: {me.WorkingSet64 / 1048576} MiB");

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
