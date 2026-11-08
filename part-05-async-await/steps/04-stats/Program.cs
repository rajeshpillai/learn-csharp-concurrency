using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

var clients = new List<StreamWriter>();
var gate = new Lock();
int lastId = 0;
int online = 0;

var listener = new TcpListener(IPAddress.Loopback, 5000);
listener.Start();
Console.WriteLine("listening on port 5000");
new Thread(Stats) { IsBackground = true }.Start();

while (true)
{
    var client = listener.AcceptTcpClient();
    new Thread(() => Serve(client)).Start();
}

void Serve(TcpClient client)
{
    int id = Interlocked.Increment(ref lastId);
    using var stream = client.GetStream();
    var reader = new StreamReader(stream);
    var writer = new StreamWriter(stream) { AutoFlush = true };
    writer.WriteLine($"welcome, you are user {id}");
    lock (gate) clients.Add(writer);
    Interlocked.Increment(ref online);

    while (reader.ReadLine() is string line)
        Broadcast($"user {id}: {line}");

    lock (gate) clients.Remove(writer);
    Interlocked.Decrement(ref online);
    client.Dispose();
}

void Broadcast(string message)
{
    lock (gate)
    {
        foreach (var writer in clients)
            writer.WriteLine(message);
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
