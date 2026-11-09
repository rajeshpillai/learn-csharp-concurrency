#!/usr/bin/env bash
# Runs every step of part 7 and saves what it printed to output/.
# These files are exactly what the terminals in the video show.
#
#   ./run.sh          about 8 minutes; Linux (uses /dev/shm)
#   ./run.sh --gaps   only the capture that is not a run (dotnet new)
#
# Each output file starts with the command as it is typed in the video, after a "$ ". The video
# types `dotnet run`; this script runs the program that `dotnet run` builds, so that the build is
# not part of any timing. "^C" in a file is where Control C was pressed: the script sends the same
# signal (SIGINT) at that moment.
#
# Where the files go. The script works in a folder in /dev/shm, a RAM disk, laid out as in the
# video: gen/ and logs/ side by side, access.log and bad.log next to them. The logs (about 265 MiB
# each) and every access.json (about 540 MiB) are written there, not to the SSD: on this laptop a
# 540 MiB file sometimes took 20 seconds to flush when it was closed, which would put the disk's
# mood into every timing. In RAM, the writer's time is its own work: hashing and JSON.
#
# LINES, BAD_AT and CAPTURE_DIR override the log size, the broken line and the output folder, for a
# quick trial run (LINES=400000 BAD_AT=200000 CAPTURE_DIR=/tmp/out ./run.sh).
set -euo pipefail
cd "$(dirname "$0")"
here=$PWD
out=${CAPTURE_DIR:-$here/output}
mkdir -p "$out"
TFM=${TFM:-net11.0}
LINES=${LINES:-4000000}
BAD_AT=${BAD_AT:-2000000}
# The memory limit of the "container" runs: 512 MiB, written in hex as .NET expects it.
LIMIT=0x20000000
tmp=$(mktemp -d)
work=$(mktemp -d /dev/shm/pipeline-XXXXXX)
mkdir -p "$work/gen" "$work/logs"
trap 'rm -rf "$tmp" "$work"' EXIT

# --disable-build-servers: a build server left running would keep the CPU lock (flock) open.
build() { dotnet build "$1" -v q -nologo --disable-build-servers >/dev/null; }
logs() { echo "$here/steps/$1/bin/Release/$TFM/logs"; }

# run FILE STEP [ARGS...]: one run of a step in logs/, its output in FILE, the typed command first.
# SIGINT is set back to its default (env --default-signal), so that Control C reaches the program
# even when this script itself runs in the background.
run() {
  local file=$1 step=$2; shift 2
  echo "\$ dotnet run${*:+ -- $*}" > "$file"
  ( cd "$work/logs" && rm -f access.json &&
    exec env --default-signal=INT "$(logs "$step")" "$@" ) >> "$file" 2>&1
  rm -f "$work/logs/access.json"
}
# limited FILE STEP [ARGS...]: the same, with the GC heap limited to $LIMIT. Records the exit code.
limited() {
  local file=$1 step=$2; shift 2
  echo "\$ DOTNET_GCHeapHardLimit=$LIMIT dotnet run${*:+ -- $*}" > "$file"
  set +e
  ( cd "$work/logs" && rm -f access.json &&
    exec env --default-signal=INT DOTNET_GCHeapHardLimit=$LIMIT "$(logs "$step")" "$@" ) \
    >> "$file" 2>&1
  local code=$?
  set -e
  sed -i "s#$here/steps/$step/##g" "$file"
  echo "(exit code $code)" >> "$file"
  rm -f "$work/logs/access.json"
}
# ctrl_c FILE SECONDS STEP [ARGS...]: a run, and Control C after SECONDS.
ctrl_c() {
  local file=$1 secs=$2 step=$3; shift 3
  echo "\$ dotnet run${*:+ -- $*}" > "$file"
  ( cd "$work/logs" && rm -f access.json &&
    exec env --default-signal=INT "$(logs "$step")" "$@" ) >> "$file" 2>&1 &
  local pid=$!
  sleep "$secs"
  echo "^C" >> "$file"
  kill -INT "$pid" 2>/dev/null || true
  set +e; wait "$pid"; local code=$?; set -e
  echo "(exit code $code)" >> "$file"
  rm -f "$work/logs/access.json"
}
# Numbers from a run's output.
secs() { grep -o 'done in [0-9.]* s' "$1" | grep -o '[0-9.]*'; }
gcp() { grep -o 'GC pauses [0-9.]* s' "$1" | grep -o '[0-9.]*'; }
late() { grep -o '[0-9,]* lines out of order' "$1" | grep -o '^[0-9,]*' | tr -d ,; }
commas() { echo "$1" | sed ':a;s/\B[0-9]\{3\}\>/,&/;ta'; }
# The middle of three values.
middle() { printf '%s\n' "$@" | sort -n | sed -n 2p; }

gaps() {
  ( cd "$tmp" && dotnet new console -n logs ) 2>&1 | sed "s#$tmp/##g" > "$out/00-new-console.txt"
  rm -rf "$tmp/logs"
}
if [ "${1:-}" = --gaps ]; then gaps; echo "done: gap file in $out/"; exit 0; fi

{
  dotnet --version
  dotnet --list-runtimes | grep NETCore.App
  uname -sr
  lscpu | grep -E 'Model name|^CPU\(s\)|Thread\(s\) per core|Core\(s\) per socket'
  free -m | head -2
} > "$out/00-version.txt"
gaps

for s in steps/*/ probes/*/; do build "$s"; done

# ── the logs ───────────────────────────────────────────────────────────────
gen() { ( cd "$work/gen" && "$here/steps/00-gen/bin/Release/$TFM/gen" "$@" ); }
{ echo "\$ dotnet run -- $LINES ../access.log"; gen "$LINES" ../access.log; } > "$out/00-gen.txt"
{ echo '$ head -3 ../access.log'; head -3 "$work/access.log"; } > "$out/00-head.txt"
{ echo "\$ dotnet run -- $LINES ../bad.log $BAD_AT"; gen "$LINES" ../bad.log "$BAD_AT"; } \
  > "$out/00-gen-bad.txt"
{
  echo "\$ sed -n '$((BAD_AT - 1)),$((BAD_AT + 1))p' ../bad.log"
  sed -n "$((BAD_AT - 1)),$((BAD_AT + 1))p" "$work/bad.log"
} > "$out/00-bad-line.txt"

# ── 01: one thread, every stage in turn ────────────────────────────────────
run "$out/01-sequential.txt" 01-sequential
# What the output looks like (a run of its own: run deletes the file it wrote).
( cd "$work/logs" && "$(logs 01-sequential)" > /dev/null &&
  { echo '$ head -2 access.json'; head -2 access.json; } > "$out/01-json.txt" &&
  rm -f access.json )

# ── 02: three tasks and two ConcurrentQueues ───────────────────────────────
run "$out/02-queues.txt" 02-queues
# The same program in a 512 MiB "container".
limited "$out/02-limit.txt" 02-queues

# ── 03: unbounded channels ─────────────────────────────────────────────────
run "$out/03-channel.txt" 03-channel

# ── 04: bounded channels, one line at a time ───────────────────────────────
run "$out/04-bounded.txt" 04-bounded

# ── 05: bounded channels, boxes of 100 lines ───────────────────────────────
run "$out/05-batches.txt" 05-batches

# ── 06: DropOldest ─────────────────────────────────────────────────────────
run "$out/06-drop-oldest.txt" 06-drop-oldest

# ── 07: several writers ────────────────────────────────────────────────────
run "$out/07-fan-out.txt" 07-fan-out ../access.log 4

# ── 08: await foreach, and the hang ────────────────────────────────────────
run "$out/08-async-streams.txt" 08-async-streams ../access.log 4
# Reading only, File.ReadLines against our async ReadLines (the probe runs in a folder beside logs/).
{
  echo '$ dotnet run'
  ( cd "$work/logs" && "$here/probes/read-speed/bin/Release/$TFM/readspeed" )
} > "$out/08-read-speed.txt"
ctrl_c "$out/08-hang.txt" 8 08-async-streams ../bad.log 4

# ── 09: Complete(exception) ────────────────────────────────────────────────
ctrl_c "$out/09-complete.txt" 8 09-complete ../bad.log 4

# ── 10: a linked token stops every stage ───────────────────────────────────
run "$out/10-bad.txt" 10-cancel ../bad.log 4
ctrl_c "$out/10-ctrl-c.txt" 2 10-cancel ../access.log 4

# ── 11: Parallel.ForEachAsync ──────────────────────────────────────────────
run "$out/11-parallel.txt" 11-parallel ../access.log 4
run "$out/11-bad.txt" 11-parallel ../bad.log 4

# The same runs several times, the middle of three each.
{
  echo "step             seconds, three runs   middle   GC pauses (middle)"
  for s in 01-sequential 02-queues 03-channel 04-bounded 05-batches; do
    t=(); g=()
    for i in 1 2 3; do run "$tmp/s.txt" "$s"; t+=("$(secs "$tmp/s.txt")"); g+=("$(gcp "$tmp/s.txt")"); done
    printf '%-16s %-21s %6s   %s s\n' "$s" "${t[*]}" "$(middle "${t[@]}")" "$(middle "${g[@]}")"
  done
} > "$out/05-three-runs.txt"
# Our writers, 1 to 8 of them (step 07).
{
  echo "writers   seconds, three runs   middle   lines out of order (middle)"
  for n in 1 2 4 8; do
    a=(); la=()
    for i in 1 2 3; do
      run "$tmp/a.txt" 07-fan-out ../access.log "$n"; a+=("$(secs "$tmp/a.txt")"); la+=("$(late "$tmp/a.txt")")
    done
    printf '%7d   %-21s %6s   %s\n' "$n" "${a[*]}" "$(middle "${a[@]}")" "$(commas "$(middle "${la[@]}")")"
  done
} > "$out/07-table.txt"
# Step 07 against step 08: the same pipeline, read with File.ReadLines or with our async ReadLines.
{
  echo "step                seconds, three runs   middle"
  for s in 07-fan-out 08-async-streams; do
    t=()
    for i in 1 2 3; do run "$tmp/s.txt" "$s" ../access.log 4; t+=("$(secs "$tmp/s.txt")"); done
    printf '%-19s %-21s %6s\n' "$s" "${t[*]}" "$(middle "${t[@]}")"
  done
} > "$out/08-three-runs.txt"
# Our writers against Parallel.ForEachAsync: steps 10 and 11 differ only in the writing stage.
{
  echo "writers   our writers (10): seconds, out of order   Parallel.ForEachAsync (11): seconds, out of order"
  for n in 1 2 4 8; do
    a=(); b=(); la=(); lb=()
    for i in 1 2 3; do
      run "$tmp/a.txt" 10-cancel ../access.log "$n"
      a+=("$(secs "$tmp/a.txt")"); la+=("$(late "$tmp/a.txt")")
      run "$tmp/b.txt" 11-parallel ../access.log "$n"
      b+=("$(secs "$tmp/b.txt")"); lb+=("$(late "$tmp/b.txt")")
    done
    printf '%7d   %16s s, %-24s   %26s s, %s\n' "$n" "$(middle "${a[@]}")" \
      "$(commas "$(middle "${la[@]}")")" "$(middle "${b[@]}")" "$(commas "$(middle "${lb[@]}")")"
  done
} > "$out/11-table.txt"

# ── the final run: the finished pipeline in the same 512 MiB "container" ───
limited "$out/11-limit.txt" 11-parallel ../access.log 4

echo "done: $(ls "$out" | wc -l) files in $out/"
