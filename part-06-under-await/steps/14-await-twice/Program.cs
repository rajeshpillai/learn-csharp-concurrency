using System.Net;
using System.Net.Sockets;

// A ValueTask from the socket, awaited twice. On purpose.
var listener = new TcpListener(IPAddress.Loopback, 5006);
listener.Start();
new Thread(Talk).Start();

using var client = await listener.AcceptTcpClientAsync();
var stream = client.GetStream();
var buffer = new byte[100];

Task<int> task = stream.ReadAsync(buffer, 0, buffer.Length);
Console.WriteLine($"Task, first await:       {await task} bytes");
Console.WriteLine($"Task, second await:      {await task} bytes");

ValueTask<int> read = stream.ReadAsync(buffer);
Console.WriteLine($"ValueTask, first await:  {await read} bytes");
try
{
    Console.WriteLine($"ValueTask, second await: {await read} bytes");
}
catch (InvalidOperationException e)
{
    Console.WriteLine($"ValueTask, second await: {e.GetType().Name}");
    Console.WriteLine($"  {e.Message}");
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
