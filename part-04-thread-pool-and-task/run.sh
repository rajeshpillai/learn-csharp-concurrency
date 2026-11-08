#!/usr/bin/env bash
# Runs every step of part 4 and saves what it printed to output/.
# These files are exactly what the terminals in the video show.
#
#   ./run.sh          about 4 minutes: several steps wait on purpose
#   ./run.sh --gaps   only the capture that is not a measurement
#
# Where the video quotes a time for a fast step, run.sh takes the middle of three runs.
# The slow steps (04, 05, 07, 14) are timed by the clock on the wall, so one run each, and
# step 14 is run three times to show the spread.
set -euo pipefail
cd "$(dirname "$0")"
out=output
mkdir -p "$out"

build() { dotnet build "$1" -v q -nologo >/dev/null; }

# Run a step's built program from inside its folder.
#   run DIR [args...]
run() {
  local dir=$1; shift
  local exe; exe=$(ls "$dir"/bin/Release/net11.0/ | grep -v '\.' | head -1)
  (cd "$dir" && "./bin/Release/net11.0/$exe" "$@")
}

# The middle of three runs, by the "time: N ms" line.
median_run() {
  local dir=$1; shift
  for i in 1 2 3; do
    local log; log=$(run "$dir" "$@")
    echo "$(grep -o 'time: *[0-9,]* ms' <<<"$log" | tr -d , | grep -o '[0-9]*') \
$(base64 -w0 <<<"$log")"
  done | sort -n | sed -n 2p | cut -d' ' -f2 | base64 -d
}

# The same for the probe, whose line reads "min threads N: T ms, ...".
median_cpu() {
  local dir=$1; shift
  for i in 1 2 3; do
    local log; log=$(run "$dir" "$@")
    echo "$(grep -o ': [0-9,]* ms,' <<<"$log" | tr -d ,: | grep -o '[0-9]*') \
$(base64 -w0 <<<"$log")"
  done | sort -n | sed -n 2p | cut -d' ' -f2 | base64 -d
}

{
  dotnet --version
  dotnet --list-runtimes | grep NETCore.App
  uname -sr
  lscpu | grep -E 'Model name|^CPU\(s\)|Thread\(s\) per core|Core\(s\) per socket'
  echo "performance cores: cpus $(cat /sys/devices/cpu_core/cpus 2>/dev/null || echo n/a)"
  echo "efficiency cores:  cpus $(cat /sys/devices/cpu_atom/cpus 2>/dev/null || echo n/a)"
} > "$out/00-version.txt"

# What `dotnet new console` makes. Not a measurement.
gaps() {
  local tmp; tmp=$(mktemp -d)
  {
    ( cd "$tmp" && dotnet new console -n jobrunner ) 2>&1 | sed "s#$tmp/##g"
    echo "\$ cat Program.cs"
    sed "1s/^\xEF\xBB\xBF//" "$tmp/jobrunner/Program.cs"
  } > "$out/00-new-console.txt"
  find "$tmp" -delete
}
if [ "${1:-}" = --gaps ]; then gaps; echo "done: 1 gap file in $out/"; exit 0; fi

gaps

for s in steps/*/ probes/*/; do build "$s"; done

# 10,000 small jobs: one after another, a new thread each, then our own pool.
median_run steps/01-one-thread > "$out/01-one-thread.txt"
median_run steps/02-thread-per-job > "$out/02-thread-per-job.txt"
median_run steps/03-my-pool > "$out/03-my-pool.txt"

# 200 requests that each wait one second for a "database", on our 12 pool threads.
run steps/04-blocking-jobs > "$out/04-blocking-jobs.txt"

# The same 200 requests on the real ThreadPool.
run steps/05-real-pool > "$out/05-real-pool.txt"

# Requests that never finish: how fast does the real pool add threads?
run steps/06-stuck-jobs > "$out/06-stuck-jobs.txt"

# The knob: SetMinThreads(200), the same 200 requests.
run steps/07-min-threads > "$out/07-min-threads.txt"
# And what the knob does to plain CPU work.
median_cpu probes/min-threads-cpu 12 > "$out/07-cpu-12.txt"
median_cpu probes/min-threads-cpu 200 > "$out/07-cpu-200.txt"

# Our own Task: a receipt with a result slot, then continuations.
run steps/08-my-task > "$out/08-my-task.txt"
run steps/09-continue-with > "$out/09-continue-with.txt"

# Pool threads that wait for jobs queued behind them: starved, for good.
run steps/10-starved > "$out/10-starved.txt"
# The same requests with ContinueWith: nobody waits.
run steps/11-no-blocking > "$out/11-no-blocking.txt"

# The real Task, and TaskCompletionSource inside our pool.
run steps/12-real-task > "$out/12-real-task.txt"

# The real pool, a request that waits on Task.Run: the waiting thread runs it itself.
run steps/13-real-wait > "$out/13-real-wait.txt"

# The real pool, a request that waits on a reply it cannot run itself.
run steps/14-real-starved > "$out/14-real-starved.txt"
for i in 1 2 3; do
  log=$(run steps/14-real-starved)
  printf 'run %d: %s, %s\n' "$i" "$(grep '^all' <<<"$log")" \
    "$(grep '^pool threads' <<<"$log")"
done > "$out/14-runs.txt"

# The three endings, and where each exception comes out.
run steps/15-three-endings > "$out/15-three-endings.txt"

# WhenAny, WhenAll, WhenEach.
run steps/16-when-all > "$out/16-when-all.txt"

# The load that starved step 14, with ContinueWith instead of waiting.
run steps/17-final > "$out/17-final.txt"
for i in 1 2 3; do
  log=$(run steps/17-final)
  printf 'run %d: %s, %s\n' "$i" "$(grep '^all' <<<"$log")" \
    "$(grep '^pool threads' <<<"$log")"
done > "$out/17-runs.txt"

echo "done: $(ls "$out" | wc -l) files in $out/"
