using System.Diagnostics;

const int Jobs = 10_000;

var clock = Stopwatch.StartNew();
long total = 0;
for (int i = 0; i < Jobs; i++)
{
    total += CountPrimes(1_000);
}

Console.WriteLine($"jobs:   {Jobs:N0}, one after another");
Console.WriteLine($"primes: {total:N0}");
Console.WriteLine($"time:   {clock.ElapsedMilliseconds:N0} ms");

// One job: a small piece of CPU work, like resizing one picture.
static int CountPrimes(int limit)
{
    int count = 0;
    for (int n = 2; n < limit; n++)
    {
        bool prime = true;
        for (int d = 2; d * d <= n; d++)
        {
            if (n % d == 0) { prime = false; break; }
        }
        if (prime) count++;
    }
    return count;
}
