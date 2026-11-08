using System.Diagnostics;

const int Seats = 500;
const int Buyers = 10_000;

int seatsLeft = Seats;
var got = new bool[Buyers];

var clock = Stopwatch.StartNew();
var threads = new List<Thread>();
for (int b = 0; b < Buyers; b++)
{
    int buyer = b;
    var thread = new Thread(() => got[buyer] = TryBuy());
    threads.Add(thread);
    thread.Start();
}
foreach (var thread in threads)
    thread.Join();

Console.WriteLine($"tickets sold: {got.Count(g => g)}");
Console.WriteLine($"seats left:   {seatsLeft}");
Console.WriteLine($"time:         {clock.ElapsedMilliseconds} ms");

bool TryBuy()
{
    if (seatsLeft > 0)
    {
        seatsLeft--;
        return true;
    }
    return false;
}
