// Writes a CSV of sales for the analytics engine to read.
//
//   dotnet run -- 10000000 sales.csv        10 million rows
//
// The same arguments always give the same file: the random seed is fixed.

int count = int.Parse(args[0]);
string path = args[1];

var rng = new Random(42);
string[] regions = ["Africa", "Asia", "Europe", "India",
                    "LatAm", "MidEast", "NorthAm", "Oceania"];
var first = new DateOnly(2025, 1, 1);

using (var output = new StreamWriter(path))
{
    output.WriteLine("date,region,product,quantity,price_cents");
    for (int i = 0; i < count; i++)
    {
        string date = first.AddDays(rng.Next(365)).ToString("yyyy-MM-dd");
        string region = regions[rng.Next(regions.Length)];
        string product = $"P{rng.Next(1, 1000):D4}";
        int quantity = rng.Next(1, 21);
        int cents = rng.Next(99, 50_000);
        output.WriteLine($"{date},{region},{product},{quantity},{cents}");
    }
}
long mib = new FileInfo(path).Length / (1024 * 1024);
Console.WriteLine($"wrote {count:N0} rows to {path}, {mib:N0} MiB");
