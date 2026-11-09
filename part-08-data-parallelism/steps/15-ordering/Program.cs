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
int all = rows.Length;
Show("10 million rows, a for loop", Time(() => Scalar(0, all)));
Show("10 million rows, Parallel.For by row", Time(() => ByRow(all), 10));
Show("10 million rows, chunks of 100,000", Time(Europe));
Show("1,000 rows, a for loop", Time(() => Scalar(0, 1000)));
Show("1,000 rows, Parallel.For by row", Time(() => ByRow(1000)));
Show("rows over 5,000.00, LINQ",
     Time(() => cents.Where(c => c > 500_000).ToArray().Length, 20));
Show("rows over 5,000.00, PLINQ",
     Time(() => cents.AsParallel()
                     .Where(c => c > 500_000).ToArray().Length, 20));
Show("rows over 5,000.00, PLINQ AsOrdered",
     Time(() => cents.AsParallel().AsOrdered()
                     .Where(c => c > 500_000).ToArray().Length, 20));

void Show(string what, double ms) =>
    Console.WriteLine($"{what,-38} {ms * 1000,10:N1} µs");

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

long ByRow(int count)
{
    long total = 0;
    Parallel.For(0, count, options,
        () => 0L,
        (i, state, local) =>
            region[i] == europe ? local + cents[i] : local,
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
