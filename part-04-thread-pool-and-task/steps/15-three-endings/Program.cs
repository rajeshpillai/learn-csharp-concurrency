TaskScheduler.UnobservedTaskException += (_, e) =>
{
    var inner = e.Exception.InnerException;
    Console.WriteLine($"unobserved: {inner?.Message}");
};

using var stop = new CancellationTokenSource();
stop.CancelAfter(100);

Task<int> ok = Task.Run(() => CountPrimes(1_000));
Task<int> bad = Task.Run(() => AskDatabase());
Task<int> cancelled = Task.Run(() => Search(stop.Token), stop.Token);

Task[] all = [ok, bad, cancelled];
while (!all.All(t => t.IsCompleted))
{
    Thread.Sleep(10);
}
Console.WriteLine($"ok:        {ok.Status}");
Console.WriteLine($"bad:       {bad.Status}");
Console.WriteLine($"cancelled: {cancelled.Status}");

Show("bad.Result", () => _ = bad.Result);
Show("bad.Wait()", () => bad.Wait());
Show("cancelled.Wait()", () => cancelled.Wait());
try { await bad; }
catch (Exception e) { Print("await bad", e); }
try { await cancelled; }
catch (Exception e) { Print("await cancelled", e); }

Forget();
Thread.Sleep(100);
GC.Collect();
GC.WaitForPendingFinalizers();
Console.WriteLine("the program is still running");

static int AskDatabase() =>
    throw new InvalidOperationException("the database is down");

static int Search(CancellationToken token)
{
    while (true)
    {
        token.ThrowIfCancellationRequested();
        Thread.Sleep(10);
    }
}

static void Forget()
{
    Task.Run(() => AskDatabase());
}

static void Show(string what, Action action)
{
    try { action(); }
    catch (Exception e) { Print(what, e); }
}

static void Print(string what, Exception e)
{
    Console.WriteLine($"{what}: throws {e.GetType().Name}");
    if (e is AggregateException box)
    {
        Exception inner = box.InnerExceptions[0];
        Console.WriteLine($"    inside: {inner.GetType().Name}");
    }
}

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
