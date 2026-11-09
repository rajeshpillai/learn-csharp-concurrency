using System.Collections.Concurrent;
using System.Diagnostics;

string[] regions = ["Africa", "Asia", "Europe", "India",
                    "LatAm", "MidEast", "NorthAm", "Oceania"];
int threads = args.Length > 0 ? int.Parse(args[0])
                               : Environment.ProcessorCount;
Console.WriteLine($"threads: {threads}");
var options = new ParallelOptions { MaxDegreeOfParallelism = threads };

var load = Stopwatch.StartNew();
string[] rows = File.ReadLines("sales.csv").Skip(1).ToArray();
Console.WriteLine($"loaded {rows.Length:N0} rows " +
                  $"in {load.ElapsedMilliseconds:N0} ms");

var build = Stopwatch.StartNew();
int[] region = new int[rows.Length];
int[] cents = new int[rows.Length];
Parallel.For(0, rows.Length, i =>
{
    ReadOnlySpan<char> line = rows[i];
    Span<Range> f = stackalloc Range[5];
    line.Split(f, ',');
    region[i] = Find(line[f[1]]);
    cents[i] = int.Parse(line[f[3]]) * int.Parse(line[f[4]]);
});
Console.WriteLine($"built columns in {build.ElapsedMilliseconds:N0} ms");

int europe = Find("Europe");
double ms = Time(Europe);
double gbs = 8.0 * rows.Length / ms / 1e6;
Console.WriteLine($"Europe {Europe() / 100m,18:N2}");
Console.WriteLine($"query: {ms:F2} ms, {gbs:F1} GB/s");

long Europe()
{
    long total = 0;
    var chunks = Partitioner.Create(0, rows.Length, 100_000);
    Parallel.ForEach(chunks, options,
        () => 0L,
        (range, state, local) => local + Scalar(range.Item1, range.Item2),
        local => Interlocked.Add(ref total, local));
    return total;
}

long Scalar(int first, int end)
{
    long sum = 0;
    for (int i = first; i < end; i++)
        if (region[i] == europe) sum += cents[i];
    return sum;
}

double Time(Func<long> query, int times = 100)
{
    for (int k = 0; k < times; k++) query();
    var clock = Stopwatch.StartNew();
    for (int k = 0; k < times; k++) query();
    return clock.Elapsed.TotalMilliseconds / times;
}

int Find(ReadOnlySpan<char> name)
{
    for (int r = 0; r < regions.Length; r++)
        if (name.SequenceEqual(regions[r])) return r;
    return -1;
}
