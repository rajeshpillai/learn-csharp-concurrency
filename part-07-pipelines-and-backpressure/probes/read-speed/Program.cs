// How long does it take just to read the log in boxes of 100 lines,
// with File.ReadLines, and with our async ReadLines?
// No parsing and no writing: reading only.
using System.Diagnostics;

string input = args.Length > 0 ? args[0] : "../access.log";

for (int round = 1; round <= 3; round++)
{
    var clock = Stopwatch.StartNew();
    long count = 0;
    foreach (string[] batch in File.ReadLines(input).Chunk(100))
        count += batch.Length;
    long sync = clock.ElapsedMilliseconds;

    clock.Restart();
    count = 0;
    await foreach (string[] batch in ReadLines(input).Chunk(100))
        count += batch.Length;
    long async = clock.ElapsedMilliseconds;

    Console.WriteLine($"round {round}: {count:N0} lines, " +
                      $"File.ReadLines {sync:N0} ms, " +
                      $"ReadLines {async:N0} ms");
}

static async IAsyncEnumerable<string> ReadLines(string path)
{
    using var file = new StreamReader(path);
    while (await file.ReadLineAsync() is string line)
        yield return line;
}
