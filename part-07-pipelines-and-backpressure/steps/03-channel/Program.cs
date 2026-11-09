using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

string input = args.Length > 0 ? args[0] : "../access.log";
var meter = new Meter();
meter.Start();

var lines = Channel.CreateUnbounded<string>();
var entries = Channel.CreateUnbounded<Entry>();
var statuses = new long[600];

var reading = Task.Run(async () =>
{
    using var file = new StreamReader(input);
    while (file.ReadLine() is string line)
    {
        await lines.Writer.WriteAsync(line);
        meter.Read++;
    }
    lines.Writer.Complete();
});

var parsing = Task.Run(async () =>
{
    while (await lines.Reader.WaitToReadAsync())
    {
        while (lines.Reader.TryRead(out var line))
        {
            await entries.Writer.WriteAsync(Parse(line));
            meter.Parsed++;
        }
    }
    entries.Writer.Complete();
});

var writing = Task.Run(async () =>
{
    using var output = new StreamWriter("access.json");
    while (await entries.Reader.WaitToReadAsync())
    {
        while (entries.Reader.TryRead(out var entry))
        {
            output.WriteLine(Format(entry));
            statuses[entry.Status]++;
            meter.Written++;
        }
    }
});

await Task.WhenAll(reading, parsing, writing);
meter.Done(statuses);

static Entry Parse(string line)
{
    string[] p = line.Split(' ');
    var time = DateTime.Parse(p[0], CultureInfo.InvariantCulture,
                              DateTimeStyles.AdjustToUniversal);
    return new Entry(time, p[1], p[2], p[3],
                     int.Parse(p[4]), int.Parse(p[5]), int.Parse(p[6]));
}

// The log store must not keep client addresses: hash the IP.
static string Format(Entry e)
{
    byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(e.Ip));
    var safe = e with { Ip = Convert.ToHexString(hash, 0, 8) };
    return JsonSerializer.Serialize(safe);
}

record Entry(DateTime Time, string Ip, string Method, string Path,
             int Status, int Bytes, int Ms);

class Meter
{
    public long Read, Parsed, Written;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Lock gate = new();
    bool finished;

    public void Start()
    {
        var thread = new Thread(() =>
        {
            while (true) { Thread.Sleep(500); Print(); }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    void Print()
    {
        lock (gate)
        {
            if (!finished) Line();
        }
    }

    void Line()
    {
        long mib = Environment.WorkingSet / (1024 * 1024);
        Console.WriteLine($"{clock.Elapsed.TotalSeconds,4:F1} s" +
                          $"  read {Volatile.Read(ref Read),9:N0}" +
                          $"  parsed {Volatile.Read(ref Parsed),9:N0}" +
                          $"  written {Volatile.Read(ref Written),9:N0}" +
                          $"  memory {mib,5:N0} MiB");
    }

    public void Done(long[] statuses)
    {
        lock (gate)
        {
            finished = true;
            Line();
        }
        for (int s = 0; s < statuses.Length; s++)
            if (statuses[s] > 0)
                Console.WriteLine($"status {s}: {statuses[s],9:N0}");
        long peak = Process.GetCurrentProcess().PeakWorkingSet64;
        var paused = GC.GetTotalPauseDuration();
        Console.WriteLine($"done in {clock.Elapsed.TotalSeconds:F1} s, " +
                          $"peak memory {peak / (1024 * 1024):N0} MiB, " +
                          $"GC pauses {paused.TotalSeconds:F1} s");
    }
}
