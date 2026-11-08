# Part 2: Shared State

The code from the video, in the order it is written. Every step was run, and what it printed is in
`output/`: that is exactly what the terminals in the video show.

You need .NET 10 or newer and a terminal. The video used .NET 11 (RC 3) on Linux; every step was
also built and run on .NET 10.0.302. `global.json` asks for .NET 11 and rolls forward; delete it to
use whatever SDK you have.

## Follow along

```bash
dotnet new console -n boxoffice
cd boxoffice
# copy Directory.Build.props from this folder: it makes every build a Release build
dotnet run
dotnet run -- 12 1000000 50000    # windows, buyers, seats (steps 03 to 10, 13, 14)
```

Then type each step's `Program.cs` as the video does, and run it again. If something doesn't match,
compare with the step: `diff Program.cs ../steps/05-lock/Program.cs`.

| Step | What changes |
|---|---|
| `01-one-window` | one window sells 500 seats to 10,000 buyers, no threads |
| `02-thread-per-buyer` | a thread per buyer: still 500, because the threads never overlap |
| `03-twelve-windows` | 12 threads share `seatsLeft`. **Oversells, on purpose**: sometimes in the hall, every run in the stadium |
| `04-interlocked` | `Interlocked.Decrement`: no lost subtractions, but the check is separate. **Still oversells** |
| `05-lock` | `lock` on a `System.Threading.Lock`: exact every time |
| `06-contention` | the same, printing `Monitor.LockContentionCount` |
| `07-seat-locks` | a `Seat` class with a lock per seat |
| `08-swap-deadlock` | two threads swap seats in opposite order. **Hangs, on purpose** |
| `09-lock-order` | the fix: always lock the lower seat number first |
| `10-alarm` | the deadlocking order again, with `Lock.TryEnter(TimeSpan)` as an alarm |
| `11-map-lock` | "how many seats are left?" with one `Lock`: 11 readers, 1 writer |
| `12-map-rwlock` | the same with `ReaderWriterLockSlim` |
| `13-dictionary` | `ConcurrentDictionary`, `ContainsKey` then the indexer. **Double books, on purpose** |
| `14-try-add` | `TryAdd`: one call does the whole decision |
| `15-standing` | standing tickets: `Count` then `TryAdd`. **Oversells, on purpose** |
| `16-standing-lock` | the same two calls inside a `lock` |

Step 08 never ends. Stop it with Ctrl+C. While it hangs, look inside it from a second terminal:

```bash
dotnet tool install -g dotnet-stack
dotnet-stack report -p <the process id it printed>
```

To see the machine code for `seatsLeft--` (step 03) or `Interlocked.Decrement` (step 04), from
the step's folder after a build:

```bash
DOTNET_TieredCompilation=0 DOTNET_JitDisasm='*b__1' ./bin/Release/net11.0/boxoffice
```

## Reproduce every number

```bash
./run.sh          # about 2 minutes; Linux; needs dotnet-stack for step 08
./run.sh --gaps   # only the two captures that are not measurements
```

Races change from run to run, so where the video says "I ran it N times", `run.sh` ran it N
times. Times depend on your machine and its state: measure a few times. The shape should hold:
twelve threads changing one number without a lock lose updates; a lock makes it exact and turns
the threads into a queue; a lock per seat lets them work side by side.

## This is teaching code

It shows how shared state goes wrong, and how locks fix it. A real booking system keeps its seats
in a database, and lets the database's transactions do this work.
