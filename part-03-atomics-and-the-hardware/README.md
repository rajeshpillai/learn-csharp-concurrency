# Part 3: Atomics and the Hardware

The code from the video, in the order it is written. Every step was run, and what it printed is in
`output/`: that is exactly what the terminals in the video show.

You need .NET 10 or newer and a terminal. The video used .NET 11 (RC 3) on Linux, on an Intel
i5-1235U laptop (2 performance cores with hyperthreading, 8 efficiency cores); the code was also
built and run on .NET 10.0.302. `global.json` asks for .NET 11 and rolls forward; delete it to use
whatever SDK you have.

## Follow along

```bash
dotnet new console -n metrics
cd metrics
# copy Directory.Build.props from this folder: it makes every build a Release build
dotnet run -- 1      # one thread
dotnet run -- 10     # ten threads
```

Then type each step's `Program.cs` as the video does, and run it again. If something doesn't match,
compare with the step: `diff Program.cs ../steps/06-skip-cell-zero/Program.cs`.

| Step | What changes |
|---|---|
| `01-plain` | a request counter with `_value++` and no protection. **Loses counts with 10 threads, on purpose** |
| `02-lock` | the same counter behind a `Lock` (C# 13): correct, and 10 threads are slower than 1 |
| `03-interlocked` | `Interlocked.Increment`: one `lock inc` instruction. Faster, and 10 threads are still slower than 1 |
| `04-striped` | one cell per thread, summed on read. Still slow: the cells share cache lines |
| `05-padded` | each cell padded to 64 bytes with `[StructLayout(Size = 64)]`. Better, not yet right |
| `06-skip-cell-zero` | cell 0 left empty, away from the array's length. 10 threads now beat 1 |
| `07-histogram` | a latency histogram with no lock: `Interlocked.CompareExchange` loops for a `double` sum and a max, with the retries counted |
| `08-stop-flag` | a `bool` flag the worker never sees in a Release build. **Hangs on purpose** (3 s limit) |
| `09-volatile` | `Volatile.Read` and `Volatile.Write`: the worker sees the flag |
| `10-reorder` | two threads each write one variable, then read the other: on x86 both can read 0 |
| `11-volatile-reorder` | the same with `volatile` fields: still reorders |
| `12-barrier` | `Thread.MemoryBarrier()` between the write and the read: never reorders |

Two small programs in `probes/`: `pad-128` is step 06 with 128-byte cells instead of 64, and
`message-passing` checks the reorder that x86 does not do (a flag seen before the data it guards).

## See the machine code

The video shows what the JIT compiler made of a worker's loop. Any step will print it:

```bash
DOTNET_JitDisasm='*b__0' dotnet run -- 1
```

`run.sh` keeps only the optimized version (the one marked `Tier1-OSR`) in `output/*-asm.txt`.

## Reproduce every number

```bash
./run.sh          # about 5 minutes; Linux, uses taskset and /usr/bin/time
```

Numbers depend heavily on your machine, and on its state: a laptop on battery or still hot can be
twice as slow. Where two threads run matters too: `taskset -c 0,1` puts them on one core's two
hyperthreads, `taskset -c 0,2` on two different cores. Check your own layout with
`lscpu --extended` and `/sys/devices/system/cpu/cpu0/cache/index2/shared_cpu_list`.

The reorder counts in steps 10 and 11 change a lot from run to run; what matters is that they are
not zero, and that step 12 is. On an Arm machine, `message-passing` may also report stale reads.

## This is teaching code

It shows how atomics, caches and the memory model work underneath. For real metrics in .NET, look at
`System.Diagnostics.Metrics` and a measured library before writing your own, and only stripe a
counter once a profiler says it is hot.
