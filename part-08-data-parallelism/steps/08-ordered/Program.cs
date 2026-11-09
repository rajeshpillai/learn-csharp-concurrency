using System.Diagnostics;

var load = Stopwatch.StartNew();
string[] rows = File.ReadLines("sales.csv").Skip(1).ToArray();
Console.WriteLine($"loaded {rows.Length:N0} rows " +
                  $"in {load.ElapsedMilliseconds:N0} ms");

for (int run = 1; run <= 3; run++)
{
    var clock = Stopwatch.StartNew();
    List<string> europe = Query();
    Console.WriteLine($"run {run}: {clock.ElapsedMilliseconds:N0} ms, " +
                      $"{europe.Count:N0} rows, first {europe[0]}");
}

List<string> Query()
{
    var europe = new List<string>();
    var query = rows
        .AsParallel()
        .AsOrdered()
        .Where(line => line.Split(',')[1] == "Europe");
    foreach (string line in query)
        europe.Add(line);
    return europe;
}
