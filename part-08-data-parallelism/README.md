# Part 8: Data parallelism

The code from the video, in the order it is written. Every step was run, and what it printed is in
`output/`: that is exactly what the terminals in the video show.

You need .NET 10 or newer and a terminal. The video used .NET 11 (RC 3) on Linux; every step was
also built and run on .NET 10.0.302, and behaves the same there. `global.json` asks for .NET 11 and
rolls forward; delete it to use whatever SDK you have.

## The data

The engine reads `sales.csv`: 10 million rows of made-up sales, 307 MiB. Make it with the
generator, then copy or move it next to your project:

```bash
cd steps/00-gen
dotnet run -- 10000000 sales.csv
```

The same arguments always give the same file (the random seed is fixed), so your totals should
match the ones in the video to the cent. Prices are whole cents, so the sums are exact in any order.

Loading the file takes about 1.3 GiB of memory. If your machine has less to spare, make a smaller
file (`dotnet run -- 2000000 sales.csv`); the totals and times will be different, the shape the same.

## Follow along

```bash
dotnet new console -n csv
cd csv
# copy Directory.Build.props from this folder: it makes every build a Release build
# put sales.csv in this folder
dotnet run
dotnet run -- 4        # from step 06 on: the number of threads
```

Type each step's `Program.cs` as the video does, and run it again. If something doesn't match,
compare with the step: `diff Program.cs ../steps/04-local-totals/Program.cs`.

| Step | What changes |
|---|---|
| `00-gen` | the generator: `date,region,product,quantity,price_cents` |
| `01-linq` | one thread, LINQ: `Split`, `GroupBy`, `Sum`. Revenue per region, three runs |
| `02-parallel-for` | `Parallel.For`, one shared `long[]` of totals. **Wrong on purpose**: a race |
| `03-interlocked` | `Interlocked.Add` on the shared totals: right |
| `04-local-totals` | `Parallel.For` with `localInit` and `localFinally`: a total per worker, merged at the end |
| `05-plinq` | the LINQ query with `AsParallel()`. **Slower on purpose**: PLINQ's `GroupBy` |
| `06-plinq-aggregate` | PLINQ `Aggregate` with a seed per worker, `WithDegreeOfParallelism` |
| `07-unordered` | the Europe rows, read with `foreach` as PLINQ produces them: the order changes |
| `08-ordered` | the same with `AsOrdered()`: file order, and what it costs |
| `09-threads` | `ParallelOptions.MaxDegreeOfParallelism` from the command line |
| `10-allocations` | how many bytes one query allocates, and how many garbage collections |
| `11-span` | parsing with `ReadOnlySpan<char>` and `Split` into a `Span<Range>`: nothing allocated |
| `12-columns` | parse once into two `int[]` columns; the Europe query over chunks (`Partitioner.Create`) |
| `13-simd` | the same query with `Vector256`: eight rows per instruction |
| `14-tiny-items` | `Parallel.For` one row at a time, and on 1,000 rows. **Slower on purpose** |
| `15-ordering` | LINQ, PLINQ, and PLINQ with `AsOrdered` on a cheap query. **Slower on purpose** |
| `16-shared-total` | one total shared by every thread, with `Interlocked.Add`. **Slower on purpose** |
| `17-final` | the finished engine: columns, chunks, local totals. The same answer as step 01 |

`probes/bandwidth` runs two loops over the same 256 MiB array on 1 to 12 threads: a sum (memory
work) and a calculation (processor work). It shows where this machine's memory stops keeping up.

## Reproduce every number

```bash
./run.sh          # about 10 minutes; Linux (uses taskset, dd, df, free)
./run.sh --gaps   # only the capture that is not a measurement
```

`run.sh` writes `sales.csv` into `data/` (or `$DATA`), reads it once so it is in the page cache,
and runs every step from there. Each timed step prints three runs of the query; the first run also
includes compiling the code. Sweeps run each thread count three times and keep the middle one.

Numbers depend on your machine. This one is a laptop with an i5-1235U: 10 cores (2 performance
cores with two hyperthreads each, 8 efficiency cores), 12 logical CPUs, one 12 MiB L3 cache. The
shape should hold elsewhere: allocation-heavy code scales worse than allocation-free code, unequal
cores cap the speedup below the core count, and a loop that only reads memory stops getting faster
when the memory can't deliver more.

`taskset -c` pins a program to chosen logical CPUs. On this machine CPUs 0 to 3 are the two
performance cores (0 and 1 share a core, 2 and 3 share a core) and 4 to 11 are the efficiency
cores. Check yours with `lscpu -e`.

## This is teaching code

It shows how data parallelism works underneath. A real analytics engine (DuckDB, ClickHouse,
Apache Arrow) stores columns compressed, on disk, in chunks, with vectorized operators for every
type, and has years of testing behind it.
