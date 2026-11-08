using System.Diagnostics;

int windows = args.Length > 0 ? int.Parse(args[0]) : 12;
int buyers = args.Length > 1 ? int.Parse(args[1]) : 10_000;
int seats = args.Length > 2 ? int.Parse(args[2]) : 500;
var hall = new Seat[seats];
for (int i = 0; i < seats; i++)
    hall[i] = new Seat(i);
var sold = new int[windows];

var clock = Stopwatch.StartNew();
var threads = new List<Thread>();
for (int k = 0; k < windows; k++)
{
    int w = k;
    var thread = new Thread(() =>
    {
        for (int b = w; b < buyers; b += windows)
            if (Book(hall[b % seats], b))
                sold[w]++;
    });
    threads.Add(thread);
    thread.Start();
}
foreach (var thread in threads)
    thread.Join();

int taken = hall.Count(s => s.Owner is not null);
Console.WriteLine($"windows:      {windows}");
Console.WriteLine($"buyers:       {buyers:N0}");
Console.WriteLine($"seats:        {seats:N0}");
Console.WriteLine($"tickets sold: {sold.Sum():N0}");
Console.WriteLine($"seats taken:  {taken:N0}");
Console.WriteLine($"time:         {clock.ElapsedMilliseconds} ms");

static bool Book(Seat seat, int buyer)
{
    lock (seat.Gate)
    {
        if (seat.Owner is not null)
            return false;
        seat.Owner = buyer;
        return true;
    }
}

class Seat(int number)
{
    public int Number { get; } = number;
    public int? Owner { get; set; }
    public Lock Gate { get; } = new();
}
