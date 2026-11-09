using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

string input = args.Length > 0 ? args[0] : "../access.log";
var meter = new Meter();
meter.Start();

int workers = args.Length > 1 ? int.Parse(args[1]) : 4;
var lines = Channel.CreateBounded<string[]>(10);
var entries = Channel.CreateBounded<Entry[]>(10);
var statuses = new long[600];

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};
using var stop =
    CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);

var reading = Task.Run(async () =>
{
    try
    {
        var batches = ReadLines(input, stop.Token).Chunk(100);
        await foreach (string[] batch in batches)
        {
            await lines.Writer.WriteAsync(batch, stop.Token);
            meter.Read += batch.Length;
        }
        lines.Writer.Complete();
    }
    catch (Exception e)
    {
        Console.WriteLine($"reading stopped: {e.Message}");
        lines.Writer.Complete(e);
        stop.Cancel();
        throw;
    }
});

var parsing = Task.Run(async () =>
{
    try
    {
        await foreach (var batch in lines.Reader.ReadAllAsync(stop.Token))
        {
            Entry[] parsed = Array.ConvertAll(batch, Parse);
            await entries.Writer.WriteAsync(parsed, stop.Token);
            meter.Parsed += batch.Length;
        }
        entries.Writer.Complete();
    }
    catch (Exception e)
    {
        Console.WriteLine($"parsing stopped: {e.Message}");
        entries.Writer.Complete(e);
        stop.Cancel();
        throw;
    }
});

using var output = new StreamWriter("access.json");
var gate = new Lock();
DateTime last = default;
long late = 0;

var options = new ParallelOptions
{
    MaxDegreeOfParallelism = workers,
    CancellationToken = stop.Token
};
var writing = Task.Run(async () =>
{
    try
    {
        var all = entries.Reader.ReadAllAsync(stop.Token);
        await Parallel.ForEachAsync(all, options, (batch, token) =>
        {
            string[] json = Array.ConvertAll(batch, Format);
            lock (gate)
            {
                for (int i = 0; i < batch.Length; i++)
                {
                    output.WriteLine(json[i]);
                    statuses[batch[i].Status]++;
                    if (batch[i].Time < last) late++;
                    else last = batch[i].Time;
                }
                meter.Written += batch.Length;
            }
            return ValueTask.CompletedTask;
        });
    }
    catch (Exception e)
    {
        Console.WriteLine($"writing stopped: {e.Message}");
        stop.Cancel();
        throw;
    }
});

try
{
    await Task.WhenAll(reading, parsing, writing);
}
catch
{
    Console.WriteLine("the pipeline did not finish");
}
meter.Done(statuses);
Console.WriteLine($"{workers} writers, {late:N0} lines out of order");
Console.WriteLine($"reading {reading.Status}, parsing {parsing.Status}, " +
                  $"writing {writing.Status}");

static async IAsyncEnumerable<string> ReadLines(string path,
    [EnumeratorCancellation] CancellationToken token)
{
    using var file = new StreamReader(path);
    while (await file.ReadLineAsync(token) is string line)
        yield return line;
}

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
