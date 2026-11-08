using System.Diagnostics;

int count = args.Length > 0 ? int.Parse(args[0]) : 1000;
var me = Process.GetCurrentProcess();

Report("before");

var clock = Stopwatch.StartNew();
for (int i = 0; i < count; i++)
{
    var thread = new Thread(() => Thread.Sleep(Timeout.Infinite));
    thread.IsBackground = true;
    thread.Start();
}
clock.Stop();

long ms = clock.ElapsedMilliseconds;
Report($"after {count:N0} threads, created in {ms:N0} ms");

void Report(string label)
{
    me.Refresh();
    long mib = me.WorkingSet64 / 1024 / 1024;
    double gib = me.VirtualMemorySize64 / 1024.0 / 1024 / 1024;
    Console.WriteLine(label);
    Console.WriteLine($"  threads in process : {me.Threads.Count:N0}");
    Console.WriteLine($"  memory in use      : {mib:N0} MiB");
    Console.WriteLine($"  address space      : {gib:N1} GiB");
}
