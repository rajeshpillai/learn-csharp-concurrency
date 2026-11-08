using static Here;

// Which user is this code working for? Two ways to remember it.
var user = new AsyncLocal<string?>();
var userOnThread = new ThreadLocal<string?>();

Task[] calls = [Handle("alice"), Handle("bob"), Handle("cara")];
Report("main");
await Task.WhenAll(calls);

async Task Handle(string name)
{
    user.Value = name;
    userOnThread.Value = name;
    Report($"{name}, before");
    await Task.Delay(100);
    Report($"{name}, after");
}

void Report(string who)
{
    string a = user.Value ?? "-";
    string t = userOnThread.Value ?? "-";
    Console.WriteLine($"{Where()}  {who,-14} async: {a,-6} thread: {t}");
}

static class Here
{
    public static string Where()
    {
        var me = Thread.CurrentThread;
        string kind = me.IsThreadPoolThread ? "pool" : me.Name ?? "main";
        return $"thread {me.ManagedThreadId,2} ({kind})";
    }
}
