using System.Collections.Concurrent;

int windows = args.Length > 0 ? int.Parse(args[0]) : 12;
int buyers = args.Length > 1 ? int.Parse(args[1]) : 10_000;
int places = args.Length > 2 ? int.Parse(args[2]) : 500;
var tickets = new ConcurrentDictionary<int, DateTime>();
var sold = new int[windows];
var gate = new Lock();

var threads = new List<Thread>();
for (int k = 0; k < windows; k++)
{
    int w = k;
    var thread = new Thread(() =>
    {
        for (int b = w; b < buyers; b += windows)
            if (Buy(b))
                sold[w]++;
    });
    threads.Add(thread);
    thread.Start();
}
foreach (var thread in threads)
    thread.Join();

Console.WriteLine($"windows:      {windows}");
Console.WriteLine($"buyers:       {buyers:N0}");
Console.WriteLine($"places:       {places:N0}");
Console.WriteLine($"tickets sold: {sold.Sum():N0}");
Console.WriteLine($"in the dict:  {tickets.Count:N0}");

bool Buy(int buyer)
{
    lock (gate)
    {
        if (tickets.Count >= places)
            return false;
        return tickets.TryAdd(buyer, DateTime.UtcNow);
    }
}
