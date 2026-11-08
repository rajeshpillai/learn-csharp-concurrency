using System.Diagnostics;

var clock = Stopwatch.StartNew();
void Log(string s) =>
    Console.WriteLine($"{clock.ElapsedMilliseconds,4} ms: {s}");

Task<string>[] Lookups() =>
[
    Lookup("user", 300),
    Lookup("orders", 100),
    Lookup("prices", 200),
];

Task<string>[] three = Lookups();
Task<string> first = Task.WhenAny(three).Result;
Log($"WhenAny: {first.Result} came back first");
string[] names = Task.WhenAll(three).Result;
Log($"WhenAll: {string.Join(", ", names)}");

clock.Restart();
await foreach (Task<string> t in Task.WhenEach(Lookups()))
{
    Log($"WhenEach: {t.Result}");
}

clock.Restart();
Task<string>[] broken = [Lookup("user", 100), Fail("stock", 50),
                         Fail("prices", 80)];
try { Task.WhenAll(broken).Wait(); }
catch (AggregateException e)
{
    Log($"Wait(): {e.InnerExceptions.Count} errors inside");
}
try { await Task.WhenAll(broken); }
catch (Exception e)
{
    Log($"await: {e.GetType().Name}: {e.Message}");
}

// A receipt that is ready after a delay. No thread waits for it.
static Task<string> Lookup(string name, int ms) =>
    Task.Delay(ms).ContinueWith(_ => name);

static Task<string> Fail(string name, int ms) =>
    Task.Delay(ms).ContinueWith<string>(_ =>
        throw new InvalidOperationException($"{name} is down"));
