#!/usr/bin/env bash
# Runs every step of part 1 and saves what it printed to output/.
# These files are exactly what the terminals in the video show.
#
#   ./run.sh          about 2 minutes
#
# Timings move with the machine, the power plug and the temperature. Where the video quotes a
# time, run.sh takes the middle of three runs.
set -euo pipefail
cd "$(dirname "$0")"
out=output
mkdir -p "$out"

build() { dotnet build "$1" -v q -nologo >/dev/null; }

# Run a step's built program from inside its folder, so its image lands there.
#   run DIR [taskset cpus|-] [args...]
run() {
  local dir=$1 cpus=$2; shift 2
  local name; name=$(basename "$(ls "$dir"/*.csproj)" .csproj)
  if [ "$cpus" = - ]; then (cd "$dir" && "./bin/Release/net11.0/$name" "$@")
  else (cd "$dir" && taskset -c "$cpus" "./bin/Release/net11.0/$name" "$@"); fi
}

# The middle of three runs, by the "rendered ... in N ms" line.
median_run() {
  local dir=$1; shift
  for i in 1 2 3; do
    local log; log=$(run "$dir" - "$@")
    echo "$(grep -o 'in [0-9]* ms' <<<"$log" | grep -o '[0-9]*') $(base64 -w0 <<<"$log")"
  done | sort -n | sed -n 2p | cut -d' ' -f2 | base64 -d
}

png() { ffmpeg -v error -y -i "$1/mandelbrot.ppm" "$out/$2.png"; }

# Single raw runs: the real command and its real output, shown before the summary tables that
# the rest of this script assembles, plus the message `dotnet new console` prints.
#
#   ./run.sh --gaps   captures only these five files and leaves the rest of output/ alone
gaps() {
  local s=steps/05-interleaved
  run $s - 4 > "$out/05-run-4.txt"
  ( cd $s && /usr/bin/time -f "%e s wall, %U s cpu" ./bin/Release/net11.0/mandel 12 ) > "$out/05-time-12.txt" 2>&1
  run steps/01-one-thread 0 > "$out/05-taskset-0.txt"
  run steps/01-one-thread 6 > "$out/05-taskset-6.txt"
  local tmp; tmp=$(mktemp -d)
  ( cd "$tmp" && dotnet new console -n mandel ) 2>&1 | sed "s#$tmp/##g" > "$out/00-new-console.txt"
  find "$tmp" -delete
}

if [ "${1:-}" = --gaps ]; then
  load=$(cut -d' ' -f1 /proc/loadavg)
  if awk -v l="$load" 'BEGIN { exit !(l > 1.5) }' && [ "${FORCE:-}" != 1 ]; then
    echo "load average $load: something heavy is running, so these timings would not match." >&2
    echo "wait for it, or FORCE=1 ./run.sh --gaps" >&2
    exit 1
  fi
  build steps/01-one-thread
  build steps/05-interleaved
  gaps
  echo "done: 5 gap files in $out/"
  exit 0
fi

{
  dotnet --version
  dotnet --list-runtimes | grep NETCore.App
  uname -sr
  lscpu | grep -E 'Model name|^CPU\(s\)|Thread\(s\) per core|Core\(s\) per socket'
  echo "performance cores: cpus $(cat /sys/devices/cpu_core/cpus 2>/dev/null || echo n/a)"
  echo "efficiency cores:  cpus $(cat /sys/devices/cpu_atom/cpus 2>/dev/null || echo n/a)"
  echo "governor: $(cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor)"
  echo "stack size (ulimit -s): $(ulimit -s) KiB"
} > "$out/00-version.txt"

for s in steps/*/; do build "$s"; done
build probes/thread-cost
build probes/see-threads

s=steps/01-one-thread
median_run $s > "$out/01-one-thread.txt";  png $s 01-one-thread
s=steps/02-two-threads
median_run $s > "$out/02-two-threads.txt"

# Names we give threads in C# show up in the operating system's own list of threads.
p=probes/see-threads
{
  run $p - > "$out/.see.tmp" & sleep 1.5
  cat "$out/.see.tmp"
  echo
  echo "\$ ps -T -o spid,comm -p $(head -1 "$out/.see.tmp" | awk '{print $2}')"
  ps -T -o spid,comm -p "$(head -1 "$out/.see.tmp" | awk '{print $2}')"
  wait
} > "$out/02-see-threads.txt"
rm -f "$out/.see.tmp"
s=steps/03-bands
median_run $s > "$out/03-bands.txt"
# The loop variable bug: the image changes from run to run, so run it three times and count
# the rows no thread ever drew (a row that is all zero bytes).
s=steps/04-loop-variable
missing() { python3 -c '
import sys; d=open(sys.argv[1],"rb").read(); px=d[d.index(b"255\n")+4:]; W=1920
rows=[y for y in range(1080) if not any(px[y*W*3:(y+1)*W*3])]
print(f"rows never drawn: {len(rows)} of 1080", ("first: "+" ".join(map(str,rows[:6]))) if rows else "")' "$1/mandelbrot.ppm"; }
{
  for i in 1 2 3; do echo "--- run $i"; run $s -; missing $s; done
} > "$out/04-loop-variable.txt";  png $s 04-loop-variable

s=steps/05-interleaved
median_run $s > "$out/05-interleaved.txt"

# How many threads is best? Step 04 with 1 to 12 threads, the middle of three each.
{
  echo "threads  ms"
  for n in 1 2 4 6 8 10 12 24 48 120; do
    printf '%7d  %s\n' "$n" "$(median_run $s "$n" | grep -o 'in [0-9]* ms' | grep -o '[0-9]*')"
  done
} > "$out/05-sweep.txt"

# The same image, the same work: total CPU time (user) against wall time, by thread count.
{
  echo "threads  wall s  cpu s"
  for n in 1 2 4 8 12; do
    ( cd $s && /usr/bin/time -f "%e %U" ./bin/Release/net11.0/mandel "$n" 2>&1 >/dev/null ) \
      | awk -v n="$n" '{printf "%7d  %6.2f  %5.2f\n", n, $1, $2}'
  done
} > "$out/05-cputime.txt"

# The clock speed of every logical CPU: idle, one thread busy, all twelve busy.
clocks() { awk '/MHz/{printf "%5d", $4} END{print ""}' /proc/cpuinfo; }
{
  echo "MHz per logical CPU (cpu0 ... cpu11)"
  printf 'idle        '; clocks
  run $s 0 1 >/dev/null & sleep 1.5
  printf '1 thread    '; clocks; wait
  run $s - 12 >/dev/null & sleep 0.4
  printf '12 threads  '; clocks; wait
} > "$out/05-clocks.txt"

# One thread, pinned: a performance core (cpu0) against an efficiency core (cpu6).
one() { run steps/01-one-thread "$1" | grep -o '[0-9]* ms'; }
{
  echo "one thread, three runs each"
  echo "performance core (cpu0): $(one 0) | $(one 0) | $(one 0)"
  echo "efficiency core  (cpu6): $(one 6) | $(one 6) | $(one 6)"
} > "$out/05-cores.txt"

p=probes/thread-cost
for n in 1000 10000; do run $p - "$n" > "$out/06-thread-cost-$n.txt"; done

s=steps/06-no-join
run $s - > "$out/06-no-join.txt";  png $s 06-no-join
s=steps/07-background
{ run $s -; echo "exit code $?"; } > "$out/07-background.txt";  png $s 07-background

# One thread throws: the whole process ends, and nothing is saved.
s=steps/08-crash
{ run $s - 2>&1 && echo "exit code 0" || echo "exit code $?"; } | sed "s#$PWD/##" > "$out/08-crash.txt"
# The same bug, caught inside the thread.
s=steps/09-catch
{ run $s -; echo "exit code $?"; } > "$out/09-catch.txt"

gaps

echo "done: $(ls "$out" | wc -l) files in $out/"
