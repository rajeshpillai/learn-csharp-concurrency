using System.Net;
using System.Net.Sockets;
using static Here;
using System.Runtime.CompilerServices;

// The same read loop, with no async and no await: a state machine by hand.
var listener = new TcpListener(IPAddress.Loopback, 5006);
listener.Start();
new Thread(Talk).Start();

Console.WriteLine($"{Where()}  waiting for a client");
using var client = await listener.AcceptTcpClientAsync();
int count = await Serve(client);
Console.WriteLine($"{Where()}  {count} lines, the client left");

Task<int> Serve(TcpClient client)
{
    var machine = new ReadLoop();
    machine.reader = new StreamReader(client.GetStream());
    Console.WriteLine($"{Where()}  waiting for a line");
    machine.MoveNext();
    return machine.receipt.Task;
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

// The bookmark: where the method is, and the locals it still needs.
class ReadLoop
{
    public int state;
    public StreamReader reader = null!;
    public int count;
    public int runs; // how many times MoveNext has run
    public TaskAwaiter<string?> awaiter;
    public TaskCompletionSource<int> receipt = new();

    public void MoveNext()
    {
        runs++;
        try
        {
            while (true)
            {
                switch (state)
                {
                    case 0: // ask for the next line
                        awaiter = reader.ReadLineAsync().GetAwaiter();
                        state = 1;
                        if (!awaiter.IsCompleted)
                        {
                            awaiter.OnCompleted(MoveNext);
                            return; // give the thread back
                        }
                        break;
                    case 1: // a line is here, or the client left
                        string? line = awaiter.GetResult();
                        if (line is null)
                        {
                            string ran = $"MoveNext ran {runs} times";
                            Console.WriteLine($"{Where()}  {ran}");
                            receipt.SetResult(count);
                            return;
                        }
                        count++;
                        Console.WriteLine($"{Where()}  got \"{line}\"");
                        state = 0;
                        break;
                }
            }
        }
        catch (Exception e)
        {
            receipt.SetException(e);
        }
    }
}
