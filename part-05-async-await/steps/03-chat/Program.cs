using System.Net;
using System.Net.Sockets;

var clients = new List<StreamWriter>();
var gate = new Lock();
int lastId = 0;

var listener = new TcpListener(IPAddress.Loopback, 5000);
listener.Start();
Console.WriteLine("listening on port 5000");

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
    Console.WriteLine($"user {id} joined");

    while (reader.ReadLine() is string line)
        Broadcast($"user {id}: {line}");

    lock (gate) clients.Remove(writer);
    Console.WriteLine($"user {id} left");
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
