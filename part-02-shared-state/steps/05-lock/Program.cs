using System.Diagnostics;

int windows = args.Length > 0 ? int.Parse(args[0]) : 12;
int buyers = args.Length > 1 ? int.Parse(args[1]) : 10_000;
int seats = args.Length > 2 ? int.Parse(args[2]) : 500;
int seatsLeft = seats;
var sold = new int[windows];
var gate = new Lock();

var clock = Stopwatch.StartNew();
var threads = new List<Thread>();
for (int k = 0; k < windows; k++)
{
    int w = k;
    var thread = new Thread(() =>
    {
        for (int b = w; b < buyers; b += windows)
            if (TryBuy())
                sold[w]++;
    });
    threads.Add(thread);
    thread.Start();
}
foreach (var thread in threads)
    thread.Join();

Console.WriteLine($"windows:      {windows}");
Console.WriteLine($"buyers:       {buyers:N0}");
Console.WriteLine($"seats:        {seats:N0}");
Console.WriteLine($"tickets sold: {sold.Sum():N0}");
Console.WriteLine($"seats left:   {seatsLeft:N0}");
Console.WriteLine($"time:         {clock.ElapsedMilliseconds} ms");

bool TryBuy()
{
    lock (gate)
    {
        if (seatsLeft > 0)
        {
            seatsLeft--;
            return true;
        }
        return false;
    }
}
