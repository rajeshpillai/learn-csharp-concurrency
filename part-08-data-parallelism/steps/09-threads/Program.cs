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

long[] result = [];
for (int run = 1; run <= 3; run++)
{
    var clock = Stopwatch.StartNew();
    result = Query();
    Console.WriteLine($"run {run}: {clock.ElapsedMilliseconds:N0} ms");
}
for (int r = 0; r < regions.Length; r++)
    Console.WriteLine($"{regions[r],-8} {result[r] / 100m,18:N2}");

long[] Query()
{
    long[] totals = new long[regions.Length];
    var gate = new Lock();
    Parallel.For(0, rows.Length, options,
        () => new long[regions.Length],
        (i, state, local) =>
        {
            string[] f = rows[i].Split(',');
            int r = Array.IndexOf(regions, f[1]);
            local[r] += long.Parse(f[3]) * long.Parse(f[4]);
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
