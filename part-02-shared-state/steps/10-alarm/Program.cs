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

Console.WriteLine($"process id:   {Environment.ProcessId}");
Console.WriteLine("swapping seats 1 and 2 ...");
var ana = new Thread(() => SwapMany(hall[1], hall[2]));
var ben = new Thread(() => SwapMany(hall[2], hall[1]));
ana.Name = "Ana";
ben.Name = "Ben";
ana.Start();
ben.Start();
ana.Join();
ben.Join();
Console.WriteLine("swaps done");

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

static void SwapMany(Seat a, Seat b)
{
    for (int i = 0; i < 1_000_000; i++)
    {
        if (!Swap(a, b))
        {
            string me = Thread.CurrentThread.Name!;
            Console.WriteLine($"ALARM: {me} held seat {a.Number}" +
                $" and waited 1 s for seat {b.Number}");
            return;
        }
    }
    Console.WriteLine($"{Thread.CurrentThread.Name}: all swaps done");
}

static bool Swap(Seat a, Seat b)
{
    lock (a.Gate)
    {
        if (!b.Gate.TryEnter(TimeSpan.FromSeconds(1)))
            return false;
        try
        {
            (a.Owner, b.Owner) = (b.Owner, a.Owner);
        }
        finally
        {
            b.Gate.Exit();
        }
        return true;
    }
}

class Seat(int number)
{
    public int Number { get; } = number;
    public int? Owner { get; set; }
    public Lock Gate { get; } = new();
}
