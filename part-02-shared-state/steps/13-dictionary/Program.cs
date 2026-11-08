using System.Collections.Concurrent;

int windows = args.Length > 0 ? int.Parse(args[0]) : 12;
int buyers = args.Length > 1 ? int.Parse(args[1]) : 10_000;
int seats = args.Length > 2 ? int.Parse(args[2]) : 500;
var bookings = new ConcurrentDictionary<int, int>();
var sold = new int[windows];

var threads = new List<Thread>();
for (int k = 0; k < windows; k++)
{
    int w = k;
    var thread = new Thread(() =>
    {
        for (int b = w; b < buyers; b += windows)
            if (Book(b % seats, b))
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
Console.WriteLine($"seats booked: {bookings.Count:N0}");

bool Book(int seat, int buyer)
{
    if (bookings.ContainsKey(seat))
        return false;
    bookings[seat] = buyer;
    return true;
}
