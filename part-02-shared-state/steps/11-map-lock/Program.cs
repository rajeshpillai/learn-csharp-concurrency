using System.Diagnostics;

const int Views = 200_000;

int readers = args.Length > 0 ? int.Parse(args[0]) : 11;
int seats = args.Length > 1 ? int.Parse(args[1]) : 500;
var taken = new bool[seats];
var gate = new Lock();

var clock = Stopwatch.StartNew();
var threads = new List<Thread>();
for (int k = 0; k < readers; k++)
{
    var reader = new Thread(() =>
    {
        for (int i = 0; i < Views; i++)
            SeatsLeft();
    });
    threads.Add(reader);
    reader.Start();
}
var writer = new Thread(() =>
{
    for (int i = 0; i < Views; i++)
        Toggle(i % seats);
});
threads.Add(writer);
writer.Start();
foreach (var thread in threads)
    thread.Join();

Console.WriteLine($"readers:    {readers} x {Views:N0} views");
Console.WriteLine($"writer:     1 x {Views:N0} changes");
Console.WriteLine($"seats:      {seats:N0}");
Console.WriteLine($"seats left: {SeatsLeft():N0}");
Console.WriteLine($"time:       {clock.ElapsedMilliseconds} ms");

int SeatsLeft()
{
    lock (gate)
    {
        int free = 0;
        foreach (bool t in taken)
            if (!t)
                free++;
        return free;
    }
}

void Toggle(int seat)
{
    lock (gate)
    {
        taken[seat] = !taken[seat];
    }
}
