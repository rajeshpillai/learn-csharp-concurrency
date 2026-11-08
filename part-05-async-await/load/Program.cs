using System.Diagnostics;
using System.Net.Sockets;

// Opens many connections to the chat server, from one thread, and holds
// them open. A socket is not a thread: one thread can own thousands.
int count = args.Length > 0 ? int.Parse(args[0]) : 10_000;
int seconds = args.Length > 1 ? int.Parse(args[1]) : 10;

var sockets = new List<TcpClient>();
var readers = new List<StreamReader>();
var clock = Stopwatch.StartNew();
long slowest = 0;
for (int i = 0; i < count; i++)
{
    long start = clock.ElapsedMilliseconds;
    var socket = new TcpClient();
    socket.Connect("127.0.0.1", 5000);
    slowest = Math.Max(slowest, clock.ElapsedMilliseconds - start);
    sockets.Add(socket);
    readers.Add(new StreamReader(socket.GetStream()));
}
Console.WriteLine($"connected: {count} in {clock.ElapsedMilliseconds} ms");
Console.WriteLine($"slowest:   {slowest} ms");

foreach (var reader in readers)
    reader.ReadLine();
Console.WriteLine($"welcomed:  {count} in {clock.ElapsedMilliseconds} ms");

Thread.Sleep(seconds * 1000);
foreach (var socket in sockets)
    socket.Dispose();
Console.WriteLine($"closed:    {count}");
