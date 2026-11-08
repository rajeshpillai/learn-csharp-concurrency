#!/usr/bin/env bash
# Runs every step of part 6 and saves what it printed to output/.
# These files are exactly what the terminals in the video show.
#
#   ./run.sh          about 1 minute
#   ./run.sh --gaps   only the captures that are not measurements
#
# Two tools are used besides the SDK. Install each once:
#   dotnet tool install -g ilspycmd       (a decompiler: shows what the compiler wrote)
#   dotnet tool install -g dotnet-stack   (prints every thread's stack in a running program)
#
# Step 08 hangs on purpose. It is started in the background, inspected with dotnet-stack, and
# stopped after 5 seconds. Steps 04 and 05 are timed: run.sh keeps the middle of three runs (by the
# time per call), and lists all three in 05-runs.txt.
set -euo pipefail
cd "$(dirname "$0")"
out=output
mkdir -p "$out"
export PATH="$HOME/.dotnet/tools:$PATH"

build() { dotnet build "$1" -v q -nologo --disable-build-servers >/dev/null; }

# Run a step's built program from inside its folder.
run() {
  local dir=$1; shift
  (cd "$dir" && ./bin/Release/net11.0/readloop "$@")
}

# Three runs of a measuring step: the middle one (by the "N ns" figure) is saved as NAME.txt,
# and the last line of all three is added to 05-runs.txt.
three_runs() {
  local dir=$1 name=$2 logs=()
  for i in 1 2 3; do logs+=("$(run "$dir")"); done
  for i in 0 1 2; do
    printf '%-20s run %d: %s\n' "$name" $((i + 1)) "$(tail -1 <<<"${logs[$i]}")" >> "$out/05-runs.txt"
  done
  for i in 0 1 2; do
    echo "$(grep -o '[0-9,]* ns' <<<"${logs[$i]}" | tr -d , | grep -o '[0-9]*') $i"
  done | sort -n | sed -n 2p | { read -r _ k; echo "${logs[$k]}"; } > "$out/$name.txt"
}

# The decompiler, the way a viewer runs it, from the step's folder. Tabs become two spaces.
decompile() {
  local dir=$1
  (cd "$dir" && ilspycmd -ds AsyncAwait=false bin/Release/net11.0/readloop.dll) | expand -t 2
}

{
  dotnet --version
  dotnet --list-runtimes | grep NETCore.App
  uname -sr
  lscpu | grep -E 'Model name|^CPU\(s\)|Thread\(s\) per core|Core\(s\) per socket'
  ilspycmd --version 2>/dev/null | head -1 || echo "ilspycmd: not installed"
  echo "dotnet-stack $(dotnet-stack --version 2>/dev/null || echo 'not installed')"
} > "$out/00-version.txt"

# What `dotnet new console` makes, and what installing ilspycmd prints (into a throwaway folder,
# so an existing install is left alone). Not measurements.
gaps() {
  local tmp; tmp=$(mktemp -d)
  {
    ( cd "$tmp" && dotnet new console -n readloop ) 2>&1 | sed "s#$tmp/##g"
  } > "$out/00-new-console.txt"
  {
    echo "\$ dotnet tool install -g ilspycmd"
    dotnet tool install --tool-path "$tmp/tools" ilspycmd 2>&1 | sed "s#$tmp/tools#~/.dotnet/tools#g"
  } > "$out/03-install.txt"
  find "$tmp" -delete
}
if [ "${1:-}" = --gaps ]; then gaps; echo "done: 2 gap files in $out/"; exit 0; fi

gaps

for s in steps/*/; do build "$s"; done

# The read loop from part 5, then the same loop as a state machine written by hand.
run steps/01-read-loop > "$out/01-read-loop.txt"
run steps/02-by-hand > "$out/02-by-hand.txt"

# What the compiler wrote for step 01: the whole file, and the read loop's part of it.
decompile steps/01-read-loop > "$out/03-decompiled-full.txt"
{
  echo "\$ ilspycmd -ds AsyncAwait=false bin/Release/net11.0/readloop.dll"
  sed -n '/private struct <<<Main>\$>g__ServeAsync/,/^    void IAsyncStateMachine.MoveNext/p' \
    "$out/03-decompiled-full.txt" | head -n -1
} > "$out/03-decompiled.txt"
# And what is left of the ServeAsync method itself.
sed -n '/\[AsyncStateMachine(typeof(<<<Main>\$>g__ServeAsync/,/^  }$/p' \
  "$out/03-decompiled-full.txt" > "$out/03-decompiled-stub.txt"

# The same read loop with runtime async switched on in the project file.
run steps/03-runtime-async > "$out/03-runtime-async.txt"
decompile steps/03-runtime-async > "$out/03-decompiled-on-full.txt"
{
  echo "\$ ilspycmd -ds AsyncAwait=false bin/Release/net11.0/readloop.dll"
  grep -B1 -A30 'static Task<int> ServeAsync' "$out/03-decompiled-on-full.txt" \
    | sed '/^    }$/q'
} > "$out/03-decompiled-on.txt"

# What changes: the live stack, and the cost of a call that waits once. Off, then on.
rm -f "$out/05-runs.txt"
three_runs steps/04-what-changes 04-what-changes
three_runs steps/05-what-changes-on 05-what-changes-on

# Our own awaitable.
run steps/06-doorbell > "$out/06-doorbell.txt"

# Our own SynchronizationContext: a message loop, like a UI thread.
run steps/07-message-loop > "$out/07-message-loop.txt"

# .Result on the message loop's thread: it hangs. Look at its stack, then stop it.
s=steps/08-deadlock
( run $s > "$out/08-deadlock.txt" ) &
for i in $(seq 50); do grep -q 'waiting for a line' "$out/08-deadlock.txt" 2>/dev/null && break; sleep 0.1; done
sleep 2
pid=$(awk '/process id:/{print $3}' "$out/08-deadlock.txt")
{
  echo "\$ dotnet-stack report -p $pid"
  dotnet-stack report -p "$pid"
} > "$out/08-stack.txt" 2>&1
sleep 3
kill "$pid" 2>/dev/null || true
wait || true
echo "(still running after 5 seconds: stopped with kill)" >> "$out/08-deadlock.txt"

# ConfigureAwait(false) in the read loop: no hang, but the ui still stands still. Then await.
run steps/09-configure-await > "$out/09-configure-await.txt"
run steps/10-await-all-the-way > "$out/10-await-all-the-way.txt"

# AsyncLocal and ThreadLocal across an await.
run steps/11-async-local > "$out/11-async-local.txt"

# Task against ValueTask, for a call that almost never waits. Off, then on.
run steps/12-value-task > "$out/12-value-task.txt"
run steps/13-value-task-on > "$out/13-value-task-on.txt"

# A ValueTask awaited twice.
run steps/14-await-twice > "$out/14-await-twice.txt"

echo "done: $(ls "$out" | wc -l) files in $out/"
