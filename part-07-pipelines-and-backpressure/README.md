# Part 7: Pipelines and backpressure

The code from the video, in the order it is written. Every step was run, and what it printed is in
`output/`: that is exactly what the terminals in the video show.

You need .NET 10 or newer and a terminal. The video used .NET 11 (RC 3) on Linux; the code was
also built and run on .NET 10.0.302. `global.json` asks for .NET 11 and rolls forward; delete it to
use whatever SDK you have.

## Follow along

Make a folder for the lesson. Copy `steps/00-gen` into it as `gen`, and make the log one folder up
from the generator (about 265 MiB):

```bash
cd gen
dotnet run -- 4000000 ../access.log
```

Then, next to `gen`, make the pipeline's project:

```bash
dotnet new console -n logs
cd logs
# copy Directory.Build.props from this folder: it makes every build a Release build
dotnet run
```

The program reads `../access.log` unless you give it another file, and writes `access.json` in
its own folder. Type each step's `Program.cs` as the video does, and run it again. If something
doesn't match, compare with the step: `diff Program.cs ../steps/07-fan-out/Program.cs`.

| Step | What changes |
|---|---|
| `00-gen` | the log generator: `dotnet run -- 4000000 ../access.log`, or add a line number to break that line |
| `01-sequential` | one thread reads, parses, formats and writes each line in turn; the time of each stage |
| `02-queues` | three tasks and two `ConcurrentQueue`s, polling. **On purpose**: memory climbs past a gibibyte |
| `03-channel` | `Channel.CreateUnbounded`, `WriteAsync`, `Complete`, `WaitToReadAsync`/`TryRead`: the same climb |
| `04-bounded` | `Channel.CreateBounded(1000)`: memory stays flat, but one line per hand-off is no faster than one thread |
| `05-batches` | boxes of 100 lines (`File.ReadLines(...).Chunk(100)`, `Array.ConvertAll`), 10 boxes per channel: flat and faster |
| `06-drop-oldest` | `BoundedChannelFullMode.DropOldest`. **On purpose**: lines are lost, and nothing says so |
| `07-fan-out` | several writers on one channel, a `Lock` around the shared file; counts lines written out of order |
| `08-async-streams` | `ReadAllAsync` and `await foreach`; our own async iterator, `ReadLines`. On `bad.log` it hangs, as shown in the video |
| `09-complete` | `Complete(exception)` passes an error downstream. **On purpose**: on `bad.log` the reader still hangs |
| `10-cancel` | a token for the whole pipeline, linked to Control C with `CreateLinkedTokenSource`; the first error stops every stage |
| `11-parallel` | the writers as `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` |

`probes/read-speed` only reads the log in boxes of 100, with `File.ReadLines` and with the async
`ReadLines` from step 08, three times each, and prints how long each took.

Arguments: `dotnet run -- <log file> <writers>`, for example `dotnet run -- ../bad.log 4` (steps 07
to 11 take the number of writers; 4 if you leave it out).

## The memory limit

The video runs two steps in a 512 MiB "container" by limiting the GC heap:

```bash
DOTNET_GCHeapHardLimit=0x20000000 dotnet run
```

The value is a number of bytes, in hexadecimal. Inside a real container, .NET sets a limit like
this one from the container's own memory limit.

## Reproduce every number

```bash
./run.sh          # about 8 minutes; Linux
./run.sh --gaps   # only the capture that is not a run (dotnet new)
```

`run.sh` works in a folder in `/dev/shm` (a RAM disk), laid out like the video's: `gen/` and `logs/`
side by side, `access.log` and `bad.log` next to them. It generates the logs, runs every step, and
deletes the folder at the end. Each `access.json` (about 540 MiB) is written to RAM, so the timings
measure the program, not the disk. It runs the programs that `dotnet run` builds, so that the build
is not part of any timing.

Numbers depend on your machine. The shape should hold: an unbounded queue grows with the gap
between the stages; a bounded channel keeps memory flat; extra writers help until another stage is
the slowest.

## This is teaching code

It shows how a pipeline works underneath. Two things a real log pipeline would do differently:
it would hash addresses with a secret key (a plain hash of an IPv4 address can be reversed by
trying every address), and it would read the batch size and the channel sizes from its settings
instead of fixing them in the code.
