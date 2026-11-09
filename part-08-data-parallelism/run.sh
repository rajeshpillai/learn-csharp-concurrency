#!/usr/bin/env bash
# Runs every step of part 8 and saves what it printed to output/.
# These files are exactly what the terminals in the video show.
#
#   ./run.sh          about 10 minutes
#   ./run.sh --gaps   only the captures that are not measurements
#
# The data: run.sh writes sales.csv (10 million rows, 307 MiB) with steps/00-gen into
# $DATA (default: data/, which git ignores), reads it once so it is in the page cache, and
# runs every step from that folder. Loading it takes about 1.3 GiB of memory.
#
# Timed steps print three runs of the query, so the first run's compile time can be seen.
# Sweeps run each thread count three times and keep the middle one (by the last run's time).
# Every number depends on the machine; see 00-version.txt for this one.
set -euo pipefail
cd "$(dirname "$0")"
here=$PWD
out=$here/output
mkdir -p "$out"
DATA=${DATA:-$here/data}
mkdir -p "$DATA"

build() { dotnet build "$1" -v q -nologo --disable-build-servers >/dev/null; }

# Run a step's built program from the data folder, with the step's arguments.
run() {
  local step=$1; shift
  (cd "$DATA" && "$here/steps/$step/bin/Release/net11.0/csv" "$@")
}

# The time a run printed last: "run 3: 1,234 ms" or "query: 2.12 ms" or "scalar: ...".
last_ms() { grep -oE '(run 3|query|scalar|simd): +[0-9,.]+ ms' | tail -1 | grep -oE '[0-9,.]+ ms' | tr -d ', ms'; }

# Three processes of the same command; print the one in the middle by its last time.
middle() {
  local logs=() i
  for i in 1 2 3; do logs+=("$("$@")"); done
  for i in 0 1 2; do echo "$(last_ms <<<"${logs[$i]}") $i"; done \
    | sort -g | sed -n 2p | { read -r _ k; echo "${logs[$k]}"; }
}

{
  dotnet --version
  dotnet --list-runtimes | grep NETCore.App
  uname -sr
  lscpu | grep -E 'Model name|^CPU\(s\)|Thread\(s\) per core|Core\(s\) per socket|L2 cache|L3 cache'
  free -h | head -2
} > "$out/00-version.txt"

gaps() {
  local tmp; tmp=$(mktemp -d)
  ( cd "$tmp" && dotnet new console -n csv ) 2>&1 | sed "s#$tmp/##g" > "$out/00-new-console.txt"
  find "$tmp" -delete
}
if [ "${1:-}" = --gaps ]; then gaps; echo "done: 1 gap file in $out/"; exit 0; fi
gaps

for s in steps/*/ probes/*/; do build "$s"; done

# The data: generate it, look at it, check the room it needs, and read it into the page cache.
{
  echo "\$ dotnet run -- 10000000 sales.csv"
  (cd "$DATA" && "$here/steps/00-gen/bin/Release/net11.0/gen" 10000000 sales.csv)
} > "$out/00-gen.txt"
{
  echo "\$ head -4 sales.csv"
  head -4 "$DATA/sales.csv"
} > "$out/00-head.txt"
{
  echo "\$ df -h ."
  (cd "$DATA" && df -h . | sed 's/[[:space:]]*$//')
  echo "\$ free -h"
  free -h | head -2
} > "$out/00-room.txt"
{
  echo "\$ dd if=sales.csv of=/dev/null bs=1M"
  (cd "$DATA" && dd if=sales.csv of=/dev/null bs=1M 2>&1 | tail -1)
  echo "\$ dd if=sales.csv of=/dev/null bs=1M"
  (cd "$DATA" && dd if=sales.csv of=/dev/null bs=1M 2>&1 | tail -1)
} > "$out/00-cache.txt"

# One thread, LINQ. Then Parallel.For: wrong, then right, then with local totals.
run 01-linq > "$out/01-linq.txt"
run 02-parallel-for > "$out/02-parallel-for.txt"
run 02-parallel-for > "$out/02-parallel-for-again.txt"
run 03-interlocked > "$out/03-interlocked.txt"
run 04-local-totals > "$out/04-local-totals.txt"

# PLINQ: GroupBy, then Aggregate. Then order: without AsOrdered, and with it.
run 05-plinq > "$out/05-plinq.txt"
run 06-plinq-aggregate > "$out/06-plinq-aggregate.txt"
run 07-unordered > "$out/07-unordered.txt"
run 08-ordered > "$out/08-ordered.txt"

# How many threads: one real run with 4, then the sweep, middle of three each.
sweep() {
  local step=$1 name=$2 n
  for n in 1 2 4 6 8 10 12; do
    echo "== $n threads"
    middle run "$step" "$n"
  done > "$out/$name-sweep-full.txt"
  {
    echo "threads  $3"
    for n in 1 2 4 6 8 10 12; do
      awk -v n="$n" '/^== /{on = ($2 == n)} on' "$out/$name-sweep-full.txt" \
        | last_ms_all "$n"
    done
  } > "$out/$name-sweep.txt"
}
# A table row: the thread count, then each time the run printed last, with GB/s if it has one.
last_ms_all() {
  local n=$1 text row; text=$(cat)
  row=$(printf '%7s' "$n")
  if grep -qE '^(query|scalar|simd): .* GB/s' <<<"$text"; then
    while read -r ms gbs; do row+=$(printf '  %8s ms %6s GB/s' "$ms" "$gbs"); done \
      < <(grep -E '^(query|scalar|simd): ' <<<"$text" | sed -E 's/^[a-z]+: +([0-9.]+) ms, ([0-9.]+) GB\/s/\1 \2/')
  else
    row+=$(printf '  %8s ms' "$(last_ms <<<"$text")")
  fi
  echo "$row"
}
run 09-threads > "$out/09-threads.txt"
{
  echo "\$ dotnet run -- 4"
  run 09-threads 4
} > "$out/09-threads-4.txt"
sweep 09-threads 09 "run 3"

# Memory: a sum over a big array next to a calculation over the same array.
for i in 1 2 3; do (cd probes/bandwidth && ./bin/Release/net11.0/bandwidth); done > "$out/probe-bandwidth-runs.txt"
awk '/^33,554,432/{k++} k==2' "$out/probe-bandwidth-runs.txt" > "$out/probe-bandwidth.txt"

# The cores: the same query, pinned with taskset to chosen logical CPUs.
taskset_run() { (cd "$DATA" && taskset -c "$1" "$here/steps/11-span/bin/Release/net11.0/csv"); }
{
  echo "\$ taskset -c 0 ./bin/Release/net11.0/csv"
  taskset_run 0
} > "$out/11-taskset-0.txt"
{
  echo "\$ taskset -c 4 ./bin/Release/net11.0/csv"
  taskset_run 4
} > "$out/11-taskset-4.txt"
{
  for cpus in 0 4 0,1 0,2 4,5 0-3 4-11 0,2,4-11 0-11; do
    echo "== cpus $cpus"
    middle taskset_run "$cpus"
  done
} > "$out/11-taskset-full.txt"
{
  echo "cpus        threads  query (run 3)"
  awk '/^== cpus/{c=$3} /^threads:/{t=$2} /^run 3:/{gsub(",","",$3); printf "%-11s %7s  %8s ms\n", c, t, $3}' \
    "$out/11-taskset-full.txt"
} > "$out/11-taskset.txt"

# Allocations, then Span.
run 10-allocations > "$out/10-allocations.txt"
run 11-span > "$out/11-span.txt"
sweep 11-span 11 "run 3"

# Columns, then SIMD.
run 12-columns > "$out/12-columns.txt"
sweep 12-columns 12 "     query"
run 13-simd > "$out/13-simd.txt"
sweep 13-simd 13 "     scalar               simd"

# When parallel is slower.
run 14-tiny-items > "$out/14-tiny-items.txt"
run 15-ordering > "$out/15-ordering.txt"
run 16-shared-total > "$out/16-shared-total.txt"

# The finished engine, then the same engine on one thread.
run 17-final > "$out/17-final.txt"
{
  echo "\$ dotnet run -- 1"
  run 17-final 1
} > "$out/17-final-1.txt"

echo "done: $(ls "$out" | wc -l) files in $out/"
