using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

string input = args.Length > 0 ? args[0] : "../access.log";
var meter = new Meter();
meter.Start();

var statuses = new long[600];
TimeSpan readTime = default, parseTime = default, writeTime = default;

using (var file = new StreamReader(input))
using (var output = new StreamWriter("access.json"))
{
    while (true)
    {
        long t = Stopwatch.GetTimestamp();
        string? line = file.ReadLine();
        readTime += Stopwatch.GetElapsedTime(t);
        if (line is null) break;
        meter.Read++;

        t = Stopwatch.GetTimestamp();
        Entry entry = Parse(line);
        parseTime += Stopwatch.GetElapsedTime(t);
        meter.Parsed++;

        t = Stopwatch.GetTimestamp();
        output.WriteLine(Format(entry));
        statuses[entry.Status]++;
        writeTime += Stopwatch.GetElapsedTime(t);
        meter.Written++;
    }
}

meter.Done(statuses);
Console.WriteLine($"reading {readTime.TotalSeconds:F1} s, " +
                  $"parsing {parseTime.TotalSeconds:F1} s, " +
                  $"writing {writeTime.TotalSeconds:F1} s");

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
