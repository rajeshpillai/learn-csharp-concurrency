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

long[] totals = Query();
for (int r = 0; r < regions.Length; r++)
    Console.WriteLine($"{regions[r],-8} {totals[r] / 100m,18:N2}");
Console.WriteLine($"query: {Time(() => Query()[0]):F2} ms");

long[] Query()
{
    long[] totals = new long[regions.Length];
    var gate = new Lock();
    var chunks = Partitioner.Create(0, rows.Length, 100_000);
    Parallel.ForEach(chunks, options,
        () => new long[regions.Length],
        (range, state, local) =>
        {
            for (int i = range.Item1; i < range.Item2; i++)
                local[region[i]] += cents[i];
            return local;
        },
        local =>
        {
            lock (gate)
            {
                for (int r = 0; r < regions.Length; r++)
                    totals[r] += local[r];
            }
        });
    return totals;
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
