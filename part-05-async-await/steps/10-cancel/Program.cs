using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

var clients = new List<Outbox>();
var gate = new Lock();
int lastId = 0;
int online = 0;

var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

var listener = new TcpListener(IPAddress.Loopback, 5000);
listener.Start();
Console.WriteLine("listening on port 5000");
new Thread(Stats) { IsBackground = true }.Start();

try
{
    while (true)
    {
        var client = await listener.AcceptTcpClientAsync(shutdown.Token);
        _ = ServeAsync(client, shutdown.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine($"stopping, {Volatile.Read(ref online)} clients");
}
while (Volatile.Read(ref online) > 0)
    await Task.Delay(100);
Console.WriteLine("stopped");

async Task ServeAsync(TcpClient client, CancellationToken stop)
{
    int id = Interlocked.Increment(ref lastId);
    using var done = CancellationTokenSource.CreateLinkedTokenSource(stop);
    using var stream = client.GetStream();
    var reader = new StreamReader(stream);
    var writer = new StreamWriter(stream) { AutoFlush = true };
    var outbox = new Outbox(writer);
    outbox.Send($"welcome, you are user {id}");
    lock (gate) clients.Add(outbox);
    Interlocked.Increment(ref online);

    var reading = ReadLoopAsync(reader, id, done.Token);
    var writing = outbox.WriteLoopAsync(done.Token);
    await Task.WhenAny(reading, writing);
    done.Cancel();
    try
    {
        await Task.WhenAll(reading, writing);
    }
    catch (Exception)
    {
        // Why looks at how each loop ended
    }

    lock (gate) clients.Remove(outbox);
    Interlocked.Decrement(ref online);
    Console.WriteLine($"user {id} left: {Why(writing, reading)}");
    client.Dispose();
}

async Task ReadLoopAsync(
    StreamReader reader, int id, CancellationToken token)
{
    while (await reader.ReadLineAsync(token) is string line)
        Broadcast($"user {id}: {line}");
}

string Why(params Task[] loops)
{
    foreach (var loop in loops)
        if (loop.IsFaulted)
            return loop.Exception.InnerException!.Message;
    if (shutdown.IsCancellationRequested) return "server stopping";
    return "disconnected";
}

void Broadcast(string message)
{
    lock (gate)
    {
        foreach (var outbox in clients)
            outbox.Send(message);
    }
}

void Stats()
{
    while (true)
    {
        Thread.Sleep(1000);
        int count = Volatile.Read(ref online);
        int queued;
        lock (gate) queued = clients.Sum(outbox => outbox.Count);
        using var me = Process.GetCurrentProcess();
        int threads = me.Threads.Count;
        long mib = me.WorkingSet64 / 1024 / 1024;
        Console.WriteLine($"clients {count,6}  threads {threads,6}" +
            $"  memory {mib,4} MiB  queued {queued,7}");
    }
}

class Outbox(StreamWriter writer)
{
    readonly Queue<string> messages = new();
    readonly SemaphoreSlim waiting = new(0);
    readonly Lock gate = new();

    public int Count
    {
        get { lock (gate) return messages.Count; }
    }

    public void Send(string message)
    {
        lock (gate) messages.Enqueue(message);
        waiting.Release();
    }

    public async Task WriteLoopAsync(CancellationToken token)
    {
        while (true)
        {
            await waiting.WaitAsync(token);
            string message;
            lock (gate) message = messages.Dequeue();
            await writer.WriteLineAsync(message.AsMemory(), token);
        }
    }
}
