using System.Runtime.CompilerServices;
using static Here;

// Our own awaitable: no Task anywhere.
var bell = new Doorbell();
var postman = new Thread(() =>
{
    Thread.Sleep(500);
    Console.WriteLine($"{Where()}  ding dong");
    bell.Ring("the postman");
});
postman.Name = "postman";
postman.Start();

Console.WriteLine($"{Where()}  waiting at the door");
string who = await bell;
Console.WriteLine($"{Where()}  it was {who}");

string again = await bell; // it has rung already: no waiting
Console.WriteLine($"{Where()}  still {again}");

// Anything with GetAwaiter() can follow await.
class Doorbell : INotifyCompletion
{
    readonly object gate = new();
    string? visitor;
    Action? next;

    public Doorbell GetAwaiter() => this;

    public bool IsCompleted
    {
        get { lock (gate) return visitor != null; }
    }

    public void OnCompleted(Action continuation)
    {
        lock (gate)
        {
            if (visitor == null)
            {
                next = continuation; // run it when the bell rings
                return;
            }
        }
        continuation(); // it rang while we were busy
    }

    public string GetResult() => visitor!;

    public void Ring(string who)
    {
        Action? run;
        lock (gate)
        {
            visitor = who;
            run = next;
            next = null;
        }
        run?.Invoke(); // the rest of the method, on this thread
    }
}

static class Here
{
    public static string Where()
    {
        var me = Thread.CurrentThread;
        string kind = me.IsThreadPoolThread ? "pool" : me.Name ?? "main";
        return $"thread {me.ManagedThreadId} ({kind})";
    }
}
