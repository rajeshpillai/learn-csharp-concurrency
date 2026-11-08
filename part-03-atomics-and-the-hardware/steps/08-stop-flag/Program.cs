bool stop = false;

var worker = new Thread(() =>
{
    long spins = 0;
    while (!stop)
        spins++;
    Console.WriteLine($"worker: saw stop after {spins:N0} spins");
});
worker.IsBackground = true;
worker.Start();

Thread.Sleep(1000);
Console.WriteLine("main: setting stop = true");
stop = true;

if (worker.Join(3000))
    Console.WriteLine("main: the worker stopped");
else
    Console.WriteLine("main: waited 3 s, the worker is still running");
