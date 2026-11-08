# Part 4: The Thread Pool and Task

The code from the video, in the order it is written. Every step was run, and what it printed is in
`output/`: that is exactly what the terminals in the video show.

You need .NET 10 or newer and a terminal. The video used .NET 11 (RC 3) on Linux; every step was
also built and run on .NET 10.0.302. `global.json` asks for .NET 11 and rolls forward; delete it to
use whatever SDK you have.

## Follow along

```bash
dotnet new console -n jobrunner
cd jobrunner
# copy Directory.Build.props from this folder: it makes every build a Release build
dotnet run
```

Then type each step's `Program.cs` as the video does, and run it again. If something doesn't match,
compare with the step: `diff Program.cs ../steps/08-my-task/Program.cs`.

| Step | What changes |
|---|---|
| `01-one-thread` | 10,000 small jobs (count the primes below 1,000), one after another |
| `02-thread-per-job` | a new `Thread` for every job: slower than one thread |
| `03-my-pool` | `MyPool`: a `Queue<Action>`, 12 threads that never leave, `Monitor.Wait` and `Pulse`; `CountdownEvent` |
| `04-blocking-jobs` | 200 requests that each wait 1 s. **Starves the pool, on purpose**: 17 rounds of 12 |
| `05-real-pool` | the same on the real pool: `ThreadPool.QueueUserWorkItem`, `ThreadCount`, `PendingWorkItemCount` |
| `06-stuck-jobs` | requests that never finish: how fast the real pool adds threads |
| `07-min-threads` | `GetMinThreads` and `SetMinThreads(200)` |
| `08-my-task` | `MyTask<T>`: a receipt with a result slot, `Result`, errors carried back; `MyPool.Run` |
| `09-continue-with` | `ContinueWith`: notes run when the receipt is filled |
| `10-starved` | requests that wait on a job behind them in the queue. **Stops for good, on purpose** |
| `11-no-blocking` | the same requests with `ContinueWith`: nobody waits |
| `12-real-task` | `Task.Run`, `Status`, and `TaskCompletionSource` inside `MyPool.Run` |
| `13-real-wait` | the real pool, a request waiting on `Task.Run`: the waiting thread runs it itself |
| `14-real-starved` | the real pool, a request waiting on a reply it cannot run itself. **Starves, on purpose** |
| `15-three-endings` | success, failure and cancellation; `.Result`, `.Wait()`, `await`; unobserved errors |
| `16-when-all` | `Task.WhenAny`, `Task.WhenAll`, `Task.WhenEach`, and errors from `WhenAll` |
| `17-final` | step 14's load with `ContinueWith` |

`probes/min-threads-cpu` is a separate small program: what `SetMinThreads` does to plain CPU
work. Run it from its own folder with `dotnet run -- 12` and `dotnet run -- 200`.

Steps 04, 05 and 14 take 15 to 30 seconds each. That is the point of them.

## Reproduce every number

```bash
./run.sh          # about 4 minutes; Linux
./run.sh --gaps   # only the capture that is not a measurement
```

Times depend on your machine and its state: measure a few times. Where the video quotes a time
for a fast step, `run.sh` takes the middle of three runs. The thread pool numbers depend on the
.NET version: the video's are from .NET 11 RC 3, and the rules behind them are in the runtime's
`PortableThreadPool` source. The shape should hold: a thread per small job is slower than one
thread; a pool of a few threads is faster; a pool thread that waits is a thread nobody can use;
and a pool thread that waits for the pool can stop the whole program.

## This is teaching code

`MyPool` and `MyTask` show how the real `ThreadPool` and `Task` work underneath. They have none
of the real ones' features: work stealing, thread injection, cancellation, schedulers. Use
`Task.Run` and the real pool in your own code.
