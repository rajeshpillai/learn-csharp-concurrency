using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using static Here;

// The read loop says ConfigureAwait(false). The ui still uses .Result.
var listener = new TcpListener(IPAddress.Loopback, 5006);
listener.Start();
new Thread(Talk).Start();

Thread.CurrentThread.Name = "ui";
var ui = new MessageLoop();

// The ui also draws the window: a frame every 100 ms.
using var timer = new Timer(_ => ui.Post(Tick, null), null, 100, 100);
void Tick(object? state) => Console.WriteLine($"{Where()}  tick");
ui.Run(async () =>
{
    Console.WriteLine($"{Where()}  waiting for a client");
    using var client = await listener.AcceptTcpClientAsync();
    int count = ServeAsync(client).Result; // blocks the ui thread
    Console.WriteLine($"{Where()}  {count} lines, the client left");
});

async Task<int> ServeAsync(TcpClient client)
{
    var reader = new StreamReader(client.GetStream());
    int count = 0;
    Console.WriteLine($"{Where()}  waiting for a line");
    while (await reader.ReadLineAsync().ConfigureAwait(false)
           is string line)
    {
        count++;
        Console.WriteLine($"{Where()}  got \"{line}\"");
    }
    return count;
}

// A client on its own thread: it connects, then sends three lines.
void Talk()
{
    Thread.Sleep(200);
    using var me = new TcpClient("127.0.0.1", 5006);
    var writer = new StreamWriter(me.GetStream()) { AutoFlush = true };
    foreach (var line in new[] { "one", "two", "three" })
    {
        Thread.Sleep(200);
        writer.WriteLine(line);
    }
}

// One thread runs every job, in order. Await posts its jobs here.
class MessageLoop : SynchronizationContext
{
    readonly Queue<Action> jobs = new();
    readonly object gate = new();

    public override void Post(SendOrPostCallback work, object? state)
    {
        lock (gate)
        {
            jobs.Enqueue(() => work(state));
            Monitor.Pulse(gate);
        }
    }

    public void Run(Func<Task> start)
    {
        SetSynchronizationContext(this);
        Task task = start();
        while (!task.IsCompleted)
        {
            Action job;
            lock (gate)
            {
                while (jobs.Count == 0)
                    Monitor.Wait(gate);
                job = jobs.Dequeue();
            }
            job();
        }
        task.GetAwaiter().GetResult(); // throws if it failed
    }
}

static class Here
{
    static readonly Stopwatch clock = Stopwatch.StartNew();

    public static string Where()
    {
        var me = Thread.CurrentThread;
        string kind = me.IsThreadPoolThread ? "pool" : me.Name ?? "main";
        long ms = clock.ElapsedMilliseconds;
        return $"{ms,4} ms  thread {me.ManagedThreadId} ({kind})";
    }
}
