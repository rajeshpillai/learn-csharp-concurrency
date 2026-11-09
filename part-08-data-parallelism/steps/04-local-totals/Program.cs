using System.Diagnostics;

string[] regions = ["Africa", "Asia", "Europe", "India",
                    "LatAm", "MidEast", "NorthAm", "Oceania"];

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
    int locals = 0;
    var threads = new HashSet<int>();
    Parallel.For(0, rows.Length,
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
                locals++;
                threads.Add(Environment.CurrentManagedThreadId);
            }
        });
    Console.WriteLine($"  {locals} local totals, " +
                      $"from {threads.Count} threads");
    return totals;
}
