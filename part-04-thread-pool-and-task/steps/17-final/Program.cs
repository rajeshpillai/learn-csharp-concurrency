using System.Diagnostics;

const int Requests = 200;

using var done = new CountdownEvent(Requests);
var clock = Stopwatch.StartNew();

for (int i = 0; i < Requests; i++)
{
    int id = i;
    _ = Task.Run(() =>
    {
        Task<int> user = AskDatabase(id);
        _ = user.ContinueWith(t =>
        {
            Render(t.Result);    // ready: nobody waits
            done.Signal();
        });
    });
}

while (!done.Wait(2_000))
{
    Console.WriteLine($"{clock.Elapsed.TotalSeconds,4:F0} s: " +
        $"{Requests - done.CurrentCount,3} done, " +
        $"{ThreadPool.ThreadCount,3} pool threads");
}
Console.WriteLine($"all {Requests} done: " +
    $"{clock.ElapsedMilliseconds:N0} ms");
Console.WriteLine($"pool threads: {ThreadPool.ThreadCount}");

// Like a real database call: the reply arrives later, from a pool thread.
static Task<int> AskDatabase(int id)
{
    var reply = new TaskCompletionSource<int>();
    ThreadPool.QueueUserWorkItem(_ => reply.SetResult(LoadUser(id)));
    return reply.Task;
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
