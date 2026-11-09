// Writes a web server access log for the pipeline to read.
//
//   dotnet run -- 4000000 ../access.log           4 million lines
//   dotnet run -- 4000000 ../bad.log 2000000      line 2,000,000 broken
//
// The same arguments always give the same file: the random seed is fixed.

int count = int.Parse(args[0]);
string path = args[1];
int badAt = args.Length > 2 ? int.Parse(args[2]) : -1;

var rng = new Random(42);
string[] paths = ["/api/users/", "/api/orders/", "/api/products/",
                  "/api/cart", "/login", "/health", "/static/app.js"];
int[] weights = [30, 25, 20, 10, 5, 5, 5];
var time = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);

using (var output = new StreamWriter(path))
{
    for (int i = 1; i <= count; i++)
    {
        if (i == badAt)
        {
            output.WriteLine("this line is broken");
            continue;
        }
        time = time.AddMilliseconds(rng.Next(1, 30));
        string ip = $"10.{rng.Next(4)}.{rng.Next(256)}.{rng.Next(1, 255)}";
        string url = Pick(paths, weights);
        if (url.EndsWith('/')) url += rng.Next(1, 100_000);
        string method = rng.Next(100) switch
        {
            < 80 => "GET", < 95 => "POST", _ => "DELETE"
        };
        int status = rng.Next(1000) switch
        {
            < 900 => 200, < 950 => 304, < 990 => 404, _ => 500
        };
        int bytes = status == 304 ? 0 : rng.Next(200, 20_000);
        // Most requests are quick, a few are slow.
        int ms = 1 + (int)(-Math.Log(1 - rng.NextDouble()) * 40);
        string stamp = time.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        output.WriteLine($"{stamp} {ip} {method} {url} " +
                         $"{status} {bytes} {ms}");
    }
}
long mib = new FileInfo(path).Length / (1024 * 1024);
Console.WriteLine($"wrote {count:N0} lines to {path}, {mib:N0} MiB");

string Pick(string[] items, int[] odds)
{
    int r = rng.Next(odds.Sum());
    for (int k = 0; k < items.Length; k++)
    {
        if (r < odds[k]) return items[k];
        r -= odds[k];
    }
    return items[^1];
}
