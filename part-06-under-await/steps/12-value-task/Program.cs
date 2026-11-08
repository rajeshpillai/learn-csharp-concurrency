// A user's name: from a cache almost every time, from the database once.
var cache = new Dictionary<int, string>();

for (int id = 0; id < 100; id++) // fill the cache first
    await NameAsTask(id);

const int Calls = 1_000_000;
long start = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < Calls; i++)
    await NameAsTask(i % 100);
long middle = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < Calls; i++)
    await NameAsValueTask(i % 100);
long end = GC.GetAllocatedBytesForCurrentThread();

long task = (middle - start) / Calls;
long valueTask = (end - middle) / Calls;
Console.WriteLine($"{Calls:N0} calls, every one a cache hit");
Console.WriteLine($"Task<string>:      {task} bytes a call");
Console.WriteLine($"ValueTask<string>: {valueTask} bytes a call");

async Task<string> NameAsTask(int id)
{
    if (cache.TryGetValue(id, out var name))
        return name; // no waiting
    await Task.Delay(10); // pretend to ask the database
    return cache[id] = $"user {id}";
}

async ValueTask<string> NameAsValueTask(int id)
{
    if (cache.TryGetValue(id, out var name))
        return name;
    await Task.Delay(10);
    return cache[id] = $"user {id}";
}
