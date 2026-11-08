const int Requests = 100;

var pool = new MyPool(Environment.ProcessorCount);
using var done = new CountdownEvent(Requests);

for (int i = 0; i < Requests; i++)
{
    int id = i;
    pool.Add(() =>
    {
        MyTask<int> user = pool.Run(() => LoadUser(id));
        Render(user.Result);    // this pool thread waits here
        done.Signal();
    });
}

if (!done.Wait(5_000))
{
    Console.WriteLine($"after 5 s: {Requests - done.CurrentCount} " +
        $"of {Requests} requests done, {pool.Queued} jobs queued");
}

// Pretend work: look up one user, then build their page.
static int LoadUser(int id)
{
    CountPrimes(1_000);
    return id;
}

static void Render(int user) => CountPrimes(1_000);

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
    readonly List<Action<MyTask<T>>> next = new();
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
        foreach (var then in next)
        {
            then(this);
        }
    }

    public void ContinueWith(Action<MyTask<T>> then)
    {
        lock (gate)
        {
            if (!done)
            {
                next.Add(then);
                return;
            }
        }
        then(this);
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
