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

long[] Query() =>
    rows.Select(line => line.Split(','))
        .GroupBy(f => f[1], f => long.Parse(f[3]) * long.Parse(f[4]))
        .OrderBy(g => g.Key)
        .Select(g => g.Sum())
        .ToArray();
