using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

var clients = new List<StreamWriter>();
var gate = new SemaphoreSlim(1);
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
    await writer.WriteLineAsync($"welcome, you are user {id}");
    await gate.WaitAsync();
    clients.Add(writer);
    gate.Release();
    Interlocked.Increment(ref online);

    while (reader.ReadLine() is string line)
        await BroadcastAsync($"user {id}: {line}");

    await gate.WaitAsync();
    clients.Remove(writer);
    gate.Release();
    Interlocked.Decrement(ref online);
    client.Dispose();
}

async Task BroadcastAsync(string message)
{
    await gate.WaitAsync();
    try
    {
        foreach (var writer in clients)
            await writer.WriteLineAsync(message);
    }
    finally
    {
        gate.Release();
    }
}

void Stats()
{
    while (true)
    {
        Thread.Sleep(1000);
        int count = Volatile.Read(ref online);
        using var me = Process.GetCurrentProcess();
        int threads = me.Threads.Count;
        long mib = me.WorkingSet64 / 1024 / 1024;
        Console.WriteLine(
            $"clients {count,6}  threads {threads,6}  memory {mib,4} MiB");
    }
}
