// Two kinds of work over the same array of longs, on 1 to 12 threads.
//   sum:     one addition for every number read. Mostly memory work.
//   compute: 16 multiplications for every number read. Processor work.
// The array is much bigger than the CPU's cache, so every number comes
// from main memory.
using System.Collections.Concurrent;
using System.Diagnostics;

int[] counts = [1, 2, 4, 6, 8, 10, 12];
long[] data = new long[32 * 1024 * 1024];
for (int i = 0; i < data.Length; i++) data[i] = i;
double gb = data.Length * 8 / 1e9;
Console.WriteLine($"{data.Length:N0} longs, {gb:F2} GB");
Console.WriteLine("threads  sum ms   GB/s  speedup   compute ms  speedup");
double sum1 = 0, compute1 = 0;
foreach (int threads in counts)
{
    double sum = Time(() => Run(data, threads, Sum));
    double compute = Time(() => Run(data, threads, Compute));
    if (threads == 1) (sum1, compute1) = (sum, compute);
    Console.WriteLine($"{threads,7} {sum,7:F2} {gb / sum * 1000,6:F1} " +
                      $"{sum1 / sum,7:F1}x {compute,12:F2} " +
                      $"{compute1 / compute,7:F1}x");
}

static long Sum(long[] data, int first, int end)
{
    long total = 0;
    for (int i = first; i < end; i++)
        total += data[i];
    return total;
}

static long Compute(long[] data, int first, int end)
{
    long total = 0;
    for (int i = first; i < end; i++)
    {
        uint x = (uint)data[i];
        for (int k = 0; k < 16; k++)
            x = x * 1_103_515_245 + 12_345;
        total += x;
    }
    return total;
}

static long Run(long[] data, int threads,
                Func<long[], int, int, long> work)
{
    long total = 0;
    var options = new ParallelOptions { MaxDegreeOfParallelism = threads };
    var chunks = Partitioner.Create(0, data.Length, data.Length / 96);
    Parallel.ForEach(chunks, options,
        () => 0L,
        (range, state, local) =>
            local + work(data, range.Item1, range.Item2),
        local => Interlocked.Add(ref total, local));
    return total;
}

static double Time(Func<long> job)
{
    var clock = Stopwatch.StartNew();
    int times = 0;
    while (clock.ElapsedMilliseconds < 300) { job(); times++; }
    clock.Restart();
    for (int k = 0; k < times; k++) job();
    return clock.Elapsed.TotalMilliseconds / times;
}
