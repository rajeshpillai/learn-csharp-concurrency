using System.Diagnostics;

string[] regions = ["Africa", "Asia", "Europe", "India",
                    "LatAm", "MidEast", "NorthAm", "Oceania"];
int threads = args.Length > 0 ? int.Parse(args[0])
                               : Environment.ProcessorCount;
Console.WriteLine($"threads: {threads}");

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

long[] Query() =>
    rows.AsParallel()
        .WithDegreeOfParallelism(threads)
        .Aggregate(
            () => new long[regions.Length],
            (local, line) =>
            {
                string[] f = line.Split(',');
                int r = Array.IndexOf(regions, f[1]);
                local[r] += long.Parse(f[3]) * long.Parse(f[4]);
                return local;
            },
            (all, local) =>
            {
                for (int r = 0; r < regions.Length; r++)
                    all[r] += local[r];
                return all;
            },
            all => all);
