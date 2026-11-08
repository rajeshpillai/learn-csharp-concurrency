#!/usr/bin/env bash
# Runs every step of part 2 and saves what it printed to output/.
# These files are exactly what the terminals in the video show.
#
#   ./run.sh          about 3 minutes
#
# Races change from run to run, so where the video says "I ran it N times", this script ran it
# N times. Where the video quotes a time, run.sh takes the middle of three runs.
#
# Step 08 hangs on purpose. It is started in the background, inspected with dotnet-stack, and
# stopped after 10 seconds. Install the tool once with: dotnet tool install -g dotnet-stack
set -euo pipefail
cd "$(dirname "$0")"
out=output
mkdir -p "$out"
export PATH="$HOME/.dotnet/tools:$PATH"

STADIUM="12 1000000 50000"

build() { dotnet build "$1" -v q -nologo >/dev/null; }

# Run a step's built program from inside its folder.
#   run DIR [args...]
run() {
  local dir=$1; shift
  (cd "$dir" && ./bin/Release/net11.0/boxoffice "$@")
}

# The middle of three runs, by the "time: N ms" line.
median_run() {
  local dir=$1; shift
  for i in 1 2 3; do
    local log; log=$(run "$dir" "$@")
    echo "$(grep -o 'time: *[0-9]* ms' <<<"$log" | grep -o '[0-9]*') $(base64 -w0 <<<"$log")"
  done | sort -n | sed -n 2p | cut -d' ' -f2 | base64 -d
}
ms() { grep -o 'time: *[0-9]* ms' | grep -o '[0-9]*'; }
field() { sed -n "s/^$1 *//p"; }

# N runs in a row, one line each: the lines of the output named in $3, side by side.
#   runs N DIR "label1|label2" [args...]
runs() {
  local n=$1 dir=$2 labels=$3; shift 3
  for i in $(seq "$n"); do
    local log; log=$(run "$dir" "$@")
    printf 'run %2d:' "$i"
    IFS='|' read -ra want <<<"$labels"
    for l in "${want[@]}"; do printf '  %s %s' "$l" "$(field "$l" <<<"$log")"; done
    echo
  done
}

{
  dotnet --version
  dotnet --list-runtimes | grep NETCore.App
  uname -sr
  lscpu | grep -E 'Model name|^CPU\(s\)|Thread\(s\) per core|Core\(s\) per socket'
  echo "performance cores: cpus $(cat /sys/devices/cpu_core/cpus 2>/dev/null || echo n/a)"
  echo "efficiency cores:  cpus $(cat /sys/devices/cpu_atom/cpus 2>/dev/null || echo n/a)"
  echo "dotnet-stack $(dotnet-stack --version 2>/dev/null || echo 'not installed')"
} > "$out/00-version.txt"

# The two captures that are not timings: what `dotnet new console` makes, and what installing
# dotnet-stack prints (into a throwaway folder, so an existing install is left alone).
#   ./run.sh --gaps   captures only these two files and leaves the rest of output/ alone
gaps() {
  local tmp; tmp=$(mktemp -d)
  {
    ( cd "$tmp" && dotnet new console -n boxoffice ) 2>&1 | sed "s#$tmp/##g"
    echo "\$ cat Program.cs"
    sed "1s/^\xEF\xBB\xBF//" "$tmp/boxoffice/Program.cs"
  } > "$out/00-new-console.txt"
  dotnet tool install --tool-path "$tmp/tools" dotnet-stack 2>&1 \
    | sed "s#$tmp/tools#~/.dotnet/tools#g" > "$out/11-install.txt"
  find "$tmp" -delete
}
if [ "${1:-}" = --gaps ]; then gaps; echo "done: 2 gap files in $out/"; exit 0; fi

gaps

for s in steps/*/; do build "$s"; done

# One window: one thread sells every ticket.
run steps/01-one-window > "$out/01-one-window.txt"

# One thread per buyer: 10,000 threads, and it still sells exactly 500.
s=steps/02-thread-per-buyer
run $s > "$out/02-thread-per-buyer.txt"
runs 5 $s "tickets sold:|seats left:|time:" > "$out/02-runs.txt"

# Twelve windows share one counter. The hall (500 seats), then the stadium.
s=steps/03-twelve-windows
run $s > "$out/03-hall.txt"
runs 10 $s "tickets sold:|seats left:" > "$out/03-hall-runs.txt"
run $s 1 1000000 50000 > "$out/03-stadium-1.txt"
run $s $STADIUM > "$out/03-stadium.txt"
runs 5 $s "tickets sold:|seats left:" $STADIUM > "$out/03-stadium-runs.txt"

# The machine code the JIT makes for the window's loop: read, subtract, write.
asm() {
  (cd "$1" && DOTNET_TieredCompilation=0 DOTNET_JitDisasm='*b__1' \
     ./bin/Release/net11.0/boxoffice)
}
asm $s > "$out/03-asm.txt"

# Interlocked.Decrement: no lost subtractions, but the check is still separate.
s=steps/04-interlocked
run $s $STADIUM > "$out/04-interlocked.txt"
runs 5 $s "tickets sold:|seats left:" $STADIUM > "$out/04-runs.txt"
asm $s > "$out/04-asm.txt"

# The lock: right every time.
s=steps/05-lock
run $s $STADIUM > "$out/05-lock.txt"
runs 5 $s "tickets sold:|seats left:" $STADIUM > "$out/05-runs.txt"
run $s 1 1000000 50000 > "$out/05-lock-1.txt"

# What the lock costs: the same sale, by number of windows, middle of three.
{
  echo "windows   no lock   lock"
  for w in 1 2 4 8 12; do
    a=$(median_run steps/03-twelve-windows $w 1000000 50000 | ms)
    b=$(median_run steps/05-lock $w 1000000 50000 | ms)
    printf '%7d  %5s ms  %5s ms\n' "$w" "$a" "$b"
  done
} > "$out/05-times.txt"

# How often a window found the door shut and had to sleep until it opened.
s=steps/06-contention
run $s $STADIUM > "$out/06-contention.txt"
{
  echo "windows   time     waited"
  for w in 1 2 4 8 12; do
    log=$(median_run $s $w 1000000 50000)
    printf '%7d  %4s ms  %9s\n' "$w" "$(ms <<<"$log")" \
      "$(grep '^waited:' <<<"$log" | awk '{print $2}')"
  done
} > "$out/06-waits.txt"

# A lock per seat.
s=steps/07-seat-locks
run $s $STADIUM > "$out/07-seat-locks.txt"
{
  echo "windows   one lock   lock per seat"
  for w in 1 2 4 8 12; do
    a=$(median_run steps/05-lock $w 1000000 50000 | ms)
    b=$(median_run $s $w 1000000 50000 | ms)
    printf '%7d  %6s ms  %9s ms\n' "$w" "$a" "$b"
  done
} > "$out/07-times.txt"

# Swaps in opposite order: a deadlock. The program hangs; dotnet-stack shows where.
s=steps/08-swap-deadlock
( run $s > "$out/08-deadlock.txt" ) &
for i in $(seq 50); do grep -q 'swapping' "$out/08-deadlock.txt" 2>/dev/null && break; sleep 0.1; done
sleep 2
pid=$(awk '/process id:/{print $3}' "$out/08-deadlock.txt")
{
  echo "\$ dotnet-stack report -p $pid"
  dotnet-stack report -p "$pid"
} > "$out/08-stack.txt" 2>&1
{ echo "\$ ps -o pid,stat,time,comm -p $pid"; ps -o pid,stat,time,comm -p "$pid"; } > "$out/08-ps.txt"
sleep 8
kill "$pid" 2>/dev/null || true
wait || true
echo "(still running after 10 seconds: stopped with kill)" >> "$out/08-deadlock.txt"

# Lock ordering: the lower seat number first, always.
run steps/09-lock-order > "$out/09-lock-order.txt"
runs 3 steps/09-lock-order "swaps done in" > "$out/09-runs.txt"

# The alarm: TryEnter with a timeout, on the deadlocking order.
run steps/10-alarm > "$out/10-alarm.txt"

# "How many seats are left?": one Lock against a ReaderWriterLockSlim, middle of three.
run steps/11-map-lock > "$out/11-map-lock.txt"
run steps/12-map-rwlock > "$out/12-map-rwlock.txt"
{
  echo "readers  seats    Lock   ReaderWriterLockSlim"
  for seats in 500 2000; do
    for r in 1 11; do
      a=$(median_run steps/11-map-lock $r $seats | ms)
      b=$(median_run steps/12-map-rwlock $r $seats | ms)
      printf '%7d  %5d  %4s ms  %7s ms\n' "$r" "$seats" "$a" "$b"
    done
  done
} > "$out/12-table.txt"
run steps/12-map-rwlock 11 2000 > "$out/12-map-rwlock-2000.txt"

# ConcurrentDictionary: check, then act, in two calls.
s=steps/13-dictionary
run $s $STADIUM > "$out/13-dictionary.txt"
runs 5 $s "tickets sold:|seats booked:" $STADIUM > "$out/13-runs.txt"
s=steps/14-try-add
run $s $STADIUM > "$out/14-try-add.txt"
runs 5 $s "tickets sold:|seats booked:" $STADIUM > "$out/14-runs.txt"

# Standing tickets: Count, then TryAdd. Each call is safe; the pair is not.
s=steps/15-standing
run $s > "$out/15-standing.txt"
runs 10 $s "tickets sold:|in the dict:" > "$out/15-runs.txt"
s=steps/16-standing-lock
run $s > "$out/16-standing-lock.txt"
runs 10 $s "tickets sold:|in the dict:" > "$out/16-runs.txt"

echo "done: $(ls "$out" | wc -l) files in $out/"
