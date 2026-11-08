# Part 6: Under await

The code from the video, in the order it is written. Every step was run, and what it printed is in
`output/`: that is exactly what the terminals in the video show.

You need .NET 10 or newer and a terminal. The video used .NET 11 (RC 3) on Linux; every step was
also built and run on .NET 10.0.302, and behaves the same there, except the three steps that switch
on runtime async (`03`, `05`, `13`): .NET 10 refuses to build them, because the helpers they need
are a preview there. `global.json` asks for .NET 11 and rolls forward; delete it to use whatever
SDK you have.

## Follow along

```bash
dotnet new console -n readloop
cd readloop
# copy Directory.Build.props from this folder: it makes every build a Release build
dotnet run
```

Then type each step's `Program.cs` as the video does, and run it again. If something doesn't match,
compare with the step: `diff Program.cs ../steps/02-by-hand/Program.cs`.

Every step is a small program that talks to itself: a client thread (`Talk`) connects to port 5006
and sends three lines. If that port is busy on your machine, change both numbers.

| Step | What changes |
|---|---|
| `01-read-loop` | part 5's read loop, `ServeAsync`, with one client. Which thread runs each line? |
| `02-by-hand` | the same loop with no `async` and no `await`: a `ReadLoop` class with a `state` field and `MoveNext` |
| `03-runtime-async` | step 01 with `<Features>runtime-async=on</Features>` in the project file (.NET 11) |
| `04-what-changes` | three async methods: the live call stack, and bytes and time per call |
| `05-what-changes-on` | step 04 with runtime async switched on |
| `06-doorbell` | our own awaitable: `GetAwaiter`, `IsCompleted`, `OnCompleted`, `GetResult` |
| `07-message-loop` | our own `SynchronizationContext`: one thread runs every job, like a UI thread |
| `08-deadlock` | the UI thread waits with `.Result`. **It hangs forever, on purpose**: stop it with Ctrl+C |
| `09-configure-await` | `ConfigureAwait(false)` in the read loop: no hang, but the UI thread still stands still |
| `10-await-all-the-way` | the UI thread awaits instead of blocking |
| `11-async-local` | `AsyncLocal<T>` against `ThreadLocal<T>` across an await |
| `12-value-task` | `Task<string>` against `ValueTask<string>` for a cache hit: bytes per call |
| `13-value-task-on` | step 12 with runtime async switched on |
| `14-await-twice` | a `ValueTask` from a socket, awaited twice. **The second await fails, on purpose** |

## Two tools

```bash
dotnet tool install -g ilspycmd       # a decompiler: what the compiler wrote for your async method
dotnet tool install -g dotnet-stack   # every thread's stack in a running program (step 08)
```

To see the state machine for step 01, from its folder after `dotnet build`:

```bash
ilspycmd -ds AsyncAwait=false bin/Release/net11.0/readloop.dll
```

`-ds AsyncAwait=false` tells the decompiler not to turn the state machine back into `async` and
`await`. The whole output is in `output/03-decompiled-full.txt`.

## Reproduce every number

```bash
./run.sh          # about a minute; Linux, uses both tools above
./run.sh --gaps   # only the captures that are not measurements
```

Numbers depend on your machine and its state. The bytes per call should match closely; the time
per call will not, but the gap between the two versions should keep its shape.

## This is teaching code

It shows how await works underneath. You don't need to write state machines or message loops by
hand in real code: write `async` and `await`, put `ConfigureAwait(false)` in library code, never
block on async code, and reach for `ValueTask` only when you have measured a reason to.
