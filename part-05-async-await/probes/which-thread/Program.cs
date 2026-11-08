using System.Net;
using System.Net.Sockets;

// One client, three lines. Does each await have to wait,
// and which thread runs the code after it?
var listener = new TcpListener(IPAddress.Loopback, 5000);
listener.Start();
Console.WriteLine($"{Where()}  waiting for a client");

var client = await listener.AcceptTcpClientAsync();
Console.WriteLine($"{Where()}  client connected");
var reader = new StreamReader(client.GetStream());
for (int i = 1; i <= 3; i++)
{
    var reading = reader.ReadLineAsync();
    string state = reading.IsCompleted ? "already here" : "not yet";
    Console.WriteLine($"{Where()}  line {i}: {state}");
    string? line = await reading;
    Console.WriteLine($"{Where()}  got \"{line}\"");
}

string Where()
{
    var me = Thread.CurrentThread;
    string kind = me.IsThreadPoolThread ? "pool" : "main";
    return $"thread {me.ManagedThreadId} ({kind})";
}
