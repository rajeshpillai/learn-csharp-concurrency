#!/usr/bin/env bash
# Runs every step of part 3 and saves what it printed to output/.
# These files are exactly what the terminals in the video show.
#
#   ./run.sh          about 6 minutes
#
# Timings move with the machine, the power plug and the temperature. Where the video quotes a
# time, run.sh takes the middle of three runs. Linux only: it uses taskset to choose cores.
set -euo pipefail
cd "$(dirname "$0")"
out=output
mkdir -p "$out"

load=$(cut -d' ' -f1 /proc/loadavg)
if awk -v l="$load" 'BEGIN { exit !(l > 1.5) }' && [ "${FORCE:-}" != 1 ]; then
  echo "load average $load: something heavy is running, so these timings would not match." >&2
  echo "wait for it, or FORCE=1 ./run.sh" >&2
  exit 1
fi

build() { dotnet build "$1" -v q -nologo >/dev/null; }

# Run a step's built program.   run DIR [taskset cpus|-] [args...]
run() {
  local dir=$1 cpus=$2; shift 2
  local name; name=$(basename "$(ls "$dir"/*.csproj)" .csproj)
  if [ "$cpus" = - ]; then "$dir/bin/Release/net11.0/$name" "$@"
  else taskset -c "$cpus" "$dir/bin/Release/net11.0/$name" "$@"; fi
}

ms() { grep -o 'time: [0-9]* ms' | grep -o '[0-9]*'; }

# The middle of three runs, by the "time: N ms" line.   median DIR cpus|- [args...]
median() {
  local dir=$1; shift
  for i in 1 2 3; do
    local log; log=$(run "$dir" "$@")
    echo "$(ms <<<"$log") $(base64 -w0 <<<"$log")"
  done | sort -n | sed -n 2p | cut -d' ' -f2 | base64 -d
}

# The machine code the JIT made for a worker's loop: the optimized (OSR) version only.
asm() {
  DOTNET_JitDisasm='*b__0' run "$@" 2>&1 | sed -n '/(Tier1-OSR)/,/^; Total bytes/p'
}

{
  dotnet --version
  dotnet --list-runtimes | grep NETCore.App
  uname -sr
  lscpu | grep -E 'Model name|^CPU\(s\)|Thread\(s\) per core|Core\(s\) per socket|L1d|L2|L3'
  echo "performance cores: cpus $(cat /sys/devices/cpu_core/cpus 2>/dev/null || echo n/a)"
  echo "efficiency cores:  cpus $(cat /sys/devices/cpu_atom/cpus 2>/dev/null || echo n/a)"
  echo "cache line: $(cat /sys/devices/system/cpu/cpu0/cache/index0/coherency_line_size) bytes"
  for c in 0 2 4 8; do
    echo "cpu$c shares its L2 with cpus $(cat /sys/devices/system/cpu/cpu$c/cache/index2/shared_cpu_list)"
  done
  echo "governor: $(cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor)"
} > "$out/00-version.txt"
# The cache line size, as the video types it.
cat /sys/devices/system/cpu/cpu0/cache/index0/coherency_line_size > "$out/00-line-size.txt"

tmp=$(mktemp -d)
( cd "$tmp" && dotnet new console -n metrics ) 2>&1 | sed "s#$tmp/##g" > "$out/00-new-console.txt"
find "$tmp" -delete

for s in steps/*/ probes/*/; do build "$s"; done

# 01: the plain counter. Fast, and wrong with more than one thread.
s=steps/01-plain
run $s - 1 > "$out/01-plain-1.txt"
{ for i in 1 2 3; do run $s - 10; done; } > "$out/01-plain-10.txt"
asm $s - 1 > "$out/01-asm.txt"

# 02: lock. 03: Interlocked. One thread, then ten.
for s in steps/02-lock steps/03-interlocked; do
  n=$(basename $s)
  median $s - 1 > "$out/$n-1.txt"
  median $s - 10 > "$out/$n-10.txt"
done
asm steps/03-interlocked - 1 > "$out/03-asm.txt"

# One hot counter, 1 to 12 threads.
s=steps/03-interlocked
{
  echo "threads  ms"
  for n in 1 2 3 4 6 8 10 12; do printf '%7d  %s\n' "$n" "$(median $s - "$n" | ms)"; done
} > "$out/03-sweep.txt"

# Where the threads run changes the cost: two threads, pinned to chosen cpus.
#   0,1  the two hyperthreads of one performance core: one L1 cache between them
#   0,2  two performance cores
#   4,5  two efficiency cores in the same cluster: they share an L2 cache
#   4,8  two efficiency cores in different clusters
run $s 0 1 > "$out/03-taskset-1.txt"
run $s 0,2 2 > "$out/03-taskset-2.txt"
{
  echo "cpus   what they share          ms"
  printf '0      (one thread)             %s\n' "$(median $s 0 1 | ms)"
  printf '0,1    one core, 2 hyperthreads %s\n' "$(median $s 0,1 2 | ms)"
  printf '4,5    one L2 (E-core cluster)  %s\n' "$(median $s 4,5 2 | ms)"
  printf '0,2    only L3                  %s\n' "$(median $s 0,2 2 | ms)"
  printf '4,8    only L3                  %s\n' "$(median $s 4,8 2 | ms)"
  printf '0,4    only L3 (P + E)          %s\n' "$(median $s 0,4 2 | ms)"
} > "$out/03-placement.txt"
# Ten threads, one on each of the ten cores (no two on one core's hyperthreads).
median $s 0,2,4-11 10 > "$out/03-ten-cores.txt"

# 04 to 06: one counter cell per thread.
for s in steps/04-striped steps/05-padded steps/06-skip-cell-zero probes/pad-128; do
  n=$(basename $s)
  median $s - 1 > "$out/$n-1.txt"
  median $s - 10 > "$out/$n-10.txt"
done
s=steps/06-skip-cell-zero
{
  echo "threads  ms"
  for n in 1 2 3 4 6 8 10 12; do printf '%7d  %s\n' "$n" "$(median $s - "$n" | ms)"; done
} > "$out/06-sweep.txt"
median $s 0,2,4-11 10 > "$out/06-ten-cores.txt"

# Every counter so far, at 1 and 10 threads, from the files above.
{
  echo "counter               1 thread   10 threads"
  for n in 01-plain 02-lock 03-interlocked 04-striped 05-padded 06-skip-cell-zero; do
    if [ $n = 01-plain ]; then a=$(ms < "$out/01-plain-1.txt"); z=$(head -2 "$out/01-plain-10.txt" | ms)
    else a=$(ms < "$out/$n-1.txt"); z=$(ms < "$out/$n-10.txt"); fi
    printf '%-20s %8s ms %9s ms\n' "$n" "$a" "$z"
  done
} > "$out/06-compare.txt"

# 07: the latency histogram, with pretend work of up to 50 spins per request.
s=steps/07-histogram
median $s - 1 50 > "$out/07-histogram-1.txt"
median $s - 10 50 > "$out/07-histogram-10.txt"

# 08: a stop flag the worker never sees in Release. Debug, for comparison.
s=steps/08-stop-flag
{ /usr/bin/time -f "took %e s" "$s/bin/Release/net11.0/metrics"; } > "$out/08-stop-flag.txt" 2>&1
dotnet build $s -c Debug -v q -nologo >/dev/null
"$s/bin/Debug/net11.0/metrics" > "$out/08-debug.txt"
asm $s - > "$out/08-asm.txt"

# 09: Volatile.Read and Volatile.Write.
s=steps/09-volatile
{ /usr/bin/time -f "took %e s" "$s/bin/Release/net11.0/metrics"; } > "$out/09-volatile.txt" 2>&1
asm $s - > "$out/09-asm.txt"

# 10 to 12: a write, then a read of the other variable, on two threads at once.
for s in steps/10-reorder steps/11-volatile-reorder steps/12-barrier; do
  { for i in 1 2 3; do run $s -; done; } > "$out/$(basename $s).txt"
done
asm steps/10-reorder - > "$out/10-asm.txt"
asm steps/12-barrier - > "$out/12-asm.txt"
# Message passing: the reorder x86 does not do.
run probes/message-passing - > "$out/13-message-passing.txt"

echo "done: $(ls "$out" | wc -l) files in $out/"
