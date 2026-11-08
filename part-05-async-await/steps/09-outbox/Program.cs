using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

var clients = new List<Outbox>();
var gate = new Lock();
int lastId = 0;
int online = 0;

var listener = new TcpListener(IPAddress.Loopback, 5000);
listener.Start();
Console.WriteLine("listening on port 5000");
new Thread(Stats) { IsBackground = true }.Start();

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    _ = ServeAsync(client);
}

async Task ServeAsync(TcpClient client)
{
    int id = Interlocked.Increment(ref lastId);
    using var stream = client.GetStream();
    var reader = new StreamReader(stream);
    var writer = new StreamWriter(stream) { AutoFlush = true };
    var outbox = new Outbox(writer);
    var writing = outbox.WriteLoopAsync();
    outbox.Send($"welcome, you are user {id}");
    lock (gate) clients.Add(outbox);
    Interlocked.Increment(ref online);

    while (await reader.ReadLineAsync() is string line)
        Broadcast($"user {id}: {line}");

    await writing;
    lock (gate) clients.Remove(outbox);
    Interlocked.Decrement(ref online);
    Console.WriteLine($"user {id} left");
    client.Dispose();
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

    public async Task WriteLoopAsync()
    {
        while (true)
        {
            await waiting.WaitAsync();
            string message;
            lock (gate) message = messages.Dequeue();
            await writer.WriteLineAsync(message);
        }
    }
}
