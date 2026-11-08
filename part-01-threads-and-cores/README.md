# Part 1: Threads and Cores

The code from the video, in the order it is written. Every step was run, and what it printed is in
`output/`: that is exactly what the terminals in the video show.

You need .NET 10 or newer and a terminal. The video used .NET 11 (RC 3) on Linux; the code was also
built and run on .NET 10.0.302. `global.json` asks for .NET 11 and rolls forward; delete it to use
whatever SDK you have.

## Follow along

```bash
dotnet new console -n mandel
cd mandel
# copy Directory.Build.props from this folder: it makes every build a Release build
dotnet run
```

Then type each step's `Program.cs` as the video does, and run it again. If something doesn't match,
compare with the step: `diff Program.cs ../steps/05-interleaved/Program.cs`.

| Step | What changes |
|---|---|
| `01-one-thread` | draw the Mandelbrot set with one thread, and time it |
| `02-two-threads` | `new Thread`, `Start`, `Join`: top half and bottom half |
| `03-bands` | one band of rows per thread, `Environment.ProcessorCount` threads |
| `04-loop-variable` | rows dealt like cards. **The lambda uses the loop variable `k`: a bug, on purpose** |
| `05-interleaved` | the fix: copy `k` into `start` inside the loop |
| `06-no-join` | the `Join` loop removed, on purpose: the picture is saved too early |
| `07-background` | `IsBackground = true`: the program ends and takes the threads with it |
| `08-crash` | `<=` instead of `<`: one thread throws, and the whole process ends. **On purpose** |
| `09-catch` | the same bug, caught inside the thread |

Two small programs in `probes/`: `see-threads` names three threads so you can find them with
`ps -T -p <pid>`, and `thread-cost` creates 1,000 or 10,000 threads and measures what they cost.

The image is written as `mandelbrot.ppm`. Most image viewers open it; `ffmpeg -i mandelbrot.ppm
mandelbrot.png` converts it.

## Reproduce every number

```bash
./run.sh          # about 3 minutes; Linux, uses taskset and /usr/bin/time
./run.sh --gaps   # the single raw runs shown before the summary tables
```

Numbers depend heavily on your machine, and on its state: a laptop on battery or still hot can be
twice as slow. Measure a few times. The shape should hold: uneven work leaves threads idle, more
threads help up to about the number of cores, and threads have a real cost.

## This is teaching code

It shows how threads work underneath. For real CPU work in .NET, you would usually reach for
`Parallel.For` or the thread pool, which later parts of this series build up to.
