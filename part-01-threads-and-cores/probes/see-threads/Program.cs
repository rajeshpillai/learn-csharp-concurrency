Thread.CurrentThread.Name = "main";
Console.WriteLine($"process {Environment.ProcessId}");
Show();

var workers = new List<Thread>();
for (int k = 1; k <= 3; k++)
{
    var thread = new Thread(() =>
    {
        Show();
        Thread.Sleep(3000);
    });
    thread.Name = $"worker-{k}";
    workers.Add(thread);
    thread.Start();
}
foreach (var thread in workers)
    thread.Join();

static void Show()
{
    string? name = Thread.CurrentThread.Name;
    int id = Environment.CurrentManagedThreadId;
    Console.WriteLine($"  {name,-9} managed thread id {id}");
}
