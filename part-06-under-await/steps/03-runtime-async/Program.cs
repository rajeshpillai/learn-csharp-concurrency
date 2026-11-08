using System.Net;
using System.Net.Sockets;
using static Here;

// The read loop from part 5's chat server, with one client.
var listener = new TcpListener(IPAddress.Loopback, 5006);
listener.Start();
new Thread(Talk).Start();

Console.WriteLine($"{Where()}  waiting for a client");
using var client = await listener.AcceptTcpClientAsync();
int count = await ServeAsync(client);
Console.WriteLine($"{Where()}  {count} lines, the client left");

async Task<int> ServeAsync(TcpClient client)
{
    var reader = new StreamReader(client.GetStream());
    int count = 0;
    Console.WriteLine($"{Where()}  waiting for a line");
    while (await reader.ReadLineAsync() is string line)
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

static class Here
{
    public static string Where()
    {
        var me = Thread.CurrentThread;
        string kind = me.IsThreadPoolThread ? "pool" : me.Name ?? "main";
        return $"thread {me.ManagedThreadId} ({kind})";
    }
}
