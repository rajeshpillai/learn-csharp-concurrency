# Part 5: async and await

The code from the video, in the order it is written. Every step was run, and what it printed is in
`output/`: that is exactly what the terminals in the video show.

You need .NET 10 or newer, a terminal, and `nc` (netcat). The video used .NET 11 (RC 3) on Linux;
the code was also built and run on .NET 10.0.302. `global.json` asks for .NET 11 and rolls
forward; delete it to use whatever SDK you have.

## Follow along

```bash
dotnet new console -n chat
cd chat
# copy Directory.Build.props from this folder: it makes every build a Release build
dotnet run
```

Then, in another terminal, connect a client and type a line:

```bash
nc localhost 5000
```

Type each step's `Program.cs` as the video does, and run it again. If something doesn't match,
compare with the step: `diff Program.cs ../steps/06-async/Program.cs`.

| Step | What changes |
|---|---|
| `01-one-client` | `TcpListener`, one client at a time, a blocking `ReadLine`: an echo server |
| `02-thread-per-client` | a new thread for every client |
| `03-chat` | a chat room: a list of clients, a `Lock`, `Broadcast` |
| `04-stats` | a stats thread: clients, `Process.Threads.Count`, `WorkingSet64` |
| `05-await-in-lock` | the server made async. **Does not compile, on purpose**: `await` inside `lock` |
| `06-async` | `SemaphoreSlim` instead of the lock: the async server |
| `07-blocking-read` | one `ReadLine` without `await`. **On purpose**: it serves one client at a time again |
| `08-async-void` | `async void Send` so a broadcast never waits. **On purpose**: a slow client crashes the server |
| `09-outbox` | an outbox per client: a queue, a `SemaphoreSlim` that counts, one write loop. Has a leak, shown in the video |
| `10-cancel` | `CancellationToken` through every await, a button per client linked to Control C |
| `11-timeout` | `CancelAfter`: a write that takes over 5 seconds disconnects the client |

`load/` is the load tool: `dotnet run -- 10000` opens 10,000 connections from one thread, reads
each welcome line, holds them for 10 seconds (or `dotnet run -- 10000 30` for 30), and closes them.

`probes/which-thread` accepts one client and prints which thread runs the code before and after
each `await`.

## Limits on your machine

Every connection is a file descriptor. `ulimit -n` shows how many a process may open; .NET raises
the soft limit to the hard limit when it starts. If the load tool says "Too many open files", raise
the limit in that terminal (`ulimit -n 20000`, as far as the hard limit allows).

On Linux, `/proc/sys/net/core/somaxconn` caps the queue of connections waiting to be accepted
(4096 here). When it is full, new connection attempts are dropped and the client tries again a
second later.

## Reproduce every number

```bash
./run.sh          # about 4 minutes; Linux, uses nc (OpenBSD netcat), ss, ps and /proc
./run.sh --gaps   # only the two captures that are not runs
```

`run.sh` drives each client as a real `nc` process and writes what its terminal shows to
`output/NN-<name>.txt`. In those files, a line starting with `> ` is a line that was typed.

Numbers depend on your machine. The shape should hold: a thread per client costs a thread per
client; the async server keeps a handful of threads however many clients wait.

## This is teaching code

It shows how waiting works underneath. A real chat server would use ASP.NET Core (Kestrel) or
SignalR, which already do all of this, with limits on queues and many more checks.
