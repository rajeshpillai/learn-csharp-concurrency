#!/usr/bin/env bash
# Runs every step of part 5 and saves what it printed to output/.
# These files are exactly what the terminals in the video show.
#
#   ./run.sh          about 4 minutes; Linux, uses nc (OpenBSD netcat), ss and /proc
#   ./run.sh --gaps   only the two captures that are not runs (dotnet new, ulimit)
#
# How a chat client is captured. Each client is a real `nc localhost 5000`, fed from a pipe, so
# this script can type into it. Its transcript (output/NN-<name>.txt) is what its terminal would
# show, with one convention: a line starting with "> " is a line that was typed, and a terminal
# shows it without the "> ". "^C" and "^Z" stand for those keys; the script sends the same
# signals (SIGINT to the server, SIGTERM to nc, SIGSTOP to a client that stops reading).
#
# Every server is started with SIGINT at its default, so that Control C reaches it even though
# this script runs it in the background (a script's background jobs ignore SIGINT otherwise).
set -euo pipefail
cd "$(dirname "$0")"
here=$PWD
out=$here/output
mkdir -p "$out"
TFM=${TFM:-net11.0}
tmp=$(mktemp -d)
PIDS=()
cleanup() { for p in "${PIDS[@]}"; do kill -9 "$p" 2>/dev/null || true; done; rm -rf "$tmp"; }
trap cleanup EXIT

FLOOD="this is one chat message, about fifty bytes long"

# --disable-build-servers: a build server left running would keep the CPU lock (flock) open.
build() { dotnet build "$1" -v q -nologo --disable-build-servers >/dev/null; }

# The server for a step, in the background, its output in $1.log. Sets SRV.
server() {
  local step=$1
  ( cd "steps/$step" && exec env --default-signal=INT ./bin/Release/$TFM/chat ) \
    > "$tmp/$step.log" 2>&1 &
  SRV=$!; PIDS+=("$SRV")
  for i in $(seq 50); do grep -q listening "$tmp/$step.log" 2>/dev/null && break; sleep 0.1; done
}
# Stop it with Control C and wait for it to exit (or kill it if it does not).
stop_server() {
  kill -INT "$SRV" 2>/dev/null || true
  for i in $(seq 50); do kill -0 "$SRV" 2>/dev/null || break; sleep 0.1; done
  kill -9 "$SRV" 2>/dev/null || true
  wait "$SRV" 2>/dev/null || true
}
load() { (cd load && ./bin/Release/$TFM/load "$@"); }

# ── clients ────────────────────────────────────────────────────────────────
declare -A FD NC SEEN FILE
# connect NAME FILE: a new nc client, its transcript in FILE
connect() {
  local name=$1 file=$2 fd
  rm -f "$tmp/$name.in" "$tmp/$name.out"; mkfifo "$tmp/$name.in"; : > "$tmp/$name.out"
  nc localhost 5000 < "$tmp/$name.in" > "$tmp/$name.out" & NC[$name]=$!; PIDS+=("$!")
  exec {fd}>"$tmp/$name.in"; FD[$name]=$fd
  SEEN[$name]=0; FILE[$name]=$file
  echo '$ nc localhost 5000' > "$file"
  sleep 0.4; catch_up
}
# Copy what each client received since last time into its transcript.
catch_up() {
  local name n
  for name in "${!FD[@]}"; do
    n=$(wc -l < "$tmp/$name.out")
    if [ "$n" -gt "${SEEN[$name]}" ]; then
      sed -n "$((SEEN[$name] + 1)),${n}p" "$tmp/$name.out" >> "${FILE[$name]}"
      SEEN[$name]=$n
    fi
  done
}
# say NAME TEXT: type a line into a client
say() {
  catch_up
  echo "> $2" >> "${FILE[$1]}"
  echo "$2" >&"${FD[$1]}"
  sleep 0.4; catch_up
}
# quit NAME: Control C in that client's terminal
quit() {
  catch_up
  echo "^C" >> "${FILE[$1]}"
  kill "${NC[$1]}" 2>/dev/null || true
  local fd=${FD[$1]}; exec {fd}>&-
  unset "FD[$1]"
  sleep 0.4; catch_up
}
# drop NAME: end a client without writing anything in its transcript
drop() {
  kill -9 "${NC[$1]}" 2>/dev/null || true
  if [ -n "${FD[$1]:-}" ]; then local fd=${FD[$1]}; exec {fd}>&-; unset "FD[$1]"; fi
}
# pause NAME: Control Z, the client stops reading
pause() {
  catch_up
  printf '^Z\n[1]+  Stopped                 nc localhost 5000\n' >> "${FILE[$1]}"
  kill -STOP "${NC[$1]}"
}
# The flood: one client sends a million lines as fast as it can, and reads nothing back.
flood() {
  yes "$FLOOD" | head -n 1000000 | nc -N localhost 5000 > /dev/null
}
FLOOD_CMD="yes \"$FLOOD\" | head -n 1000000 | nc -N localhost 5000 > /dev/null"

# ── measurements ───────────────────────────────────────────────────────────
# What the threads of a process are waiting in, counted, and the names they carry.
wchan() { ps -L -o wchan:24= -p "$1" | sort | uniq -c | sort -rn; }
names() { ps -L -o comm= -p "$1" | sort | uniq -c | sort -rn; }
# Memory the kernel keeps for thread stacks, for the whole machine, in KiB.
kstack() { awk '/^KernelStack:/{print $2}' /proc/meminfo; }
# Connection attempts the kernel dropped because the accept queue was full.
overflows() { awk '/^TcpExt:/{ if (!h) { split($0, k); h = 1 } else { for (i in k) if (k[i] == "ListenOverflows") print $i } }' /proc/net/netstat; }
closewait() { ss -tn state close-wait '( sport = :5000 )' | tail -n +2 | wc -l; }
# Wait until a file has a line matching a pattern.
wait_for() { for i in $(seq 600); do grep -q "$2" "$1" 2>/dev/null && return; sleep 0.1; done; echo "timeout waiting for $2 in $1" >&2; }

# ── the captures that are not runs ─────────────────────────────────────────
gaps() {
  {
    ( cd "$tmp" && dotnet new console -n chat ) 2>&1 | sed "s#$tmp/##g"
    echo "\$ cat Program.cs"
    sed "1s/^\xEF\xBB\xBF//" "$tmp/chat/Program.cs"
  } > "$out/00-new-console.txt"
  rm -rf "$tmp/chat"
  {
    echo '$ ulimit -n'; ulimit -n
    echo '$ cat /proc/sys/net/core/somaxconn'; cat /proc/sys/net/core/somaxconn
  } > "$out/00-limits.txt"
}
if [ "${1:-}" = --gaps ]; then gaps; echo "done: gap files in $out/"; exit 0; fi

{
  dotnet --version
  dotnet --list-runtimes | grep NETCore.App
  uname -sr
  lscpu | grep -E 'Model name|^CPU\(s\)|Thread\(s\) per core|Core\(s\) per socket'
  echo "nc: $(nc -h 2>&1 | head -1)"
} > "$out/00-version.txt"
gaps

for s in steps/*/ load probes/*/; do
  case $s in steps/05-*) continue ;; esac
  build "$s"
done

# ── 01: one client at a time ───────────────────────────────────────────────
server 01-one-client
connect alice "$out/01-alice.txt"
say alice "hello"
connect bob "$out/01-bob.txt"
say bob "hi, anyone?"
sleep 1; catch_up
cp "$out/01-bob.txt" "$out/01-bob-waiting.txt"
quit alice
sleep 0.5; catch_up
quit bob
stop_server; cp "$tmp/01-one-client.log" "$out/01-server.txt"

# ── 02: a thread per client ────────────────────────────────────────────────
server 02-thread-per-client
connect alice "$out/02-alice.txt"
say alice "hello"
connect bob "$out/02-bob.txt"
say bob "hi, anyone?"
quit alice; quit bob
stop_server; cp "$tmp/02-thread-per-client.log" "$out/02-server.txt"

# ── 03: a chat room ────────────────────────────────────────────────────────
server 03-chat
connect alice "$out/03-alice.txt"
connect bob "$out/03-bob.txt"
say alice "hi, is anyone here?"
say bob "hi alice, I'm here"
connect cara "$out/03-cara.txt"
say cara "hello both"
quit bob
say alice "bye bob"
quit alice; quit cara
stop_server; cp "$tmp/03-chat.log" "$out/03-server.txt"

# ── 04: a thread per client, 10,000 clients ────────────────────────────────
# The limit first: what the load tool does when a process may only open 1,024 files.
server 04-stats
{
  echo '$ ulimit -n 1024'
  echo '$ dotnet run -- 2000'
  ( ulimit -S -n 1024; ulimit -H -n 1024; load 2000 1 ) 2>&1 | head -2 || true
} > "$out/04-limit.txt"
stop_server

server 04-stats
sleep 1.2
k0=$(kstack); o0=$(overflows)
load 10000 8 > "$tmp/load.txt" & L=$!
wait_for "$tmp/load.txt" welcomed
sleep 2
{ echo "\$ ps -L -o wchan= -p $SRV | sort | uniq -c"; wchan "$SRV"; } > "$out/04-wchan.txt"
{ echo "\$ ps -L -o comm= -p $SRV | sort | uniq -c"; names "$SRV"; } > "$out/04-names.txt"
k1=$(kstack); o1=$(overflows)
wait $L; cp "$tmp/load.txt" "$out/04-load.txt"
sleep 1.5
stop_server; cp "$tmp/04-stats.log" "$out/04-server.txt"
{
  echo "kernel stacks before: $((k0 / 1024)) MiB"
  echo "kernel stacks during: $((k1 / 1024)) MiB"
  echo "kernel stacks added:  $(((k1 - k0) / 1024)) MiB"
  echo "connections dropped:  $((o1 - o0))"
} > "$out/04-kernel.txt"

# ── 05: await inside a lock does not compile ───────────────────────────────
{
  echo '$ dotnet build'
  (cd steps/05-await-in-lock && dotnet build -v q -nologo --disable-build-servers 2>&1 || true) \
    | grep -E 'error|Error|Build FAILED' | sed "s#$here/steps/05-await-in-lock/##g" \
    | sed 's# \[chat.csproj\]##' | awk '!seen[$0]++'
} > "$out/05-build.txt"

# ── 06: async: the same chat room first, then 10,000 clients ────────────────
server 06-async
connect alice "$out/06-chat-alice.txt"
connect bob "$out/06-chat-bob.txt"
say alice "hi, is anyone here?"
say bob "hi alice, I'm here"
quit bob
quit alice
stop_server

server 06-async
sleep 1.2
k0=$(kstack); o0=$(overflows)
load 10000 8 > "$tmp/load.txt" & L=$!
wait_for "$tmp/load.txt" welcomed
sleep 2
{ echo "\$ ps -L -o wchan= -p $SRV | sort | uniq -c"; wchan "$SRV"; } > "$out/06-wchan.txt"
{ echo "\$ ps -L -o comm= -p $SRV | sort | uniq -c"; names "$SRV"; } > "$out/06-names.txt"
k1=$(kstack); o1=$(overflows)
wait $L; cp "$tmp/load.txt" "$out/06-load.txt"
sleep 1.5
stop_server; cp "$tmp/06-async.log" "$out/06-server.txt"
{
  echo "kernel stacks before: $((k0 / 1024)) MiB"
  echo "kernel stacks during: $((k1 / 1024)) MiB"
  echo "kernel stacks added:  $(((k1 - k0) / 1024)) MiB"
  echo "connections dropped:  $((o1 - o0))"
} > "$out/06-kernel.txt"

# Side by side: the same clients, both servers. Threads and memory are read from the server's
# own stats line while every client is connected; "welcomed" is when the last client got its
# welcome line.
side() {
  local step=$1 n=$2
  server "$step"; sleep 1.2
  load "$n" 4 > "$tmp/load.txt" & L=$!
  wait_for "$tmp/load.txt" welcomed
  sleep 2.2
  local st; st=$(grep "^clients *$n " "$tmp/$step.log" | tail -1)
  wait $L
  stop_server
  printf '%s %s %s\n' "$(awk '{print $4}' <<<"$st")" "$(awk '{print $6}' <<<"$st")" \
    "$(grep -o 'welcomed: *[0-9]* in [0-9]*' "$tmp/load.txt" | awk '{print $4}')"
}
{
  echo "clients   threads: one each / async   memory MiB: one each / async   welcomed ms: one each / async"
  for n in 1000 5000 10000; do
    a=($(side 04-stats $n)); b=($(side 06-async $n))
    printf '%7d   %7s / %-5s   %10s / %-5s   %9s / %s\n' "$n" "${a[0]}" "${b[0]}" "${a[1]}" "${b[1]}" "${a[2]}" "${b[2]}"
  done
} > "$out/06-table.txt"

# Which thread runs the code after await?
( cd probes/which-thread && exec ./bin/Release/$TFM/which ) > "$tmp/which.log" 2>&1 & W=$!; PIDS+=("$W")
wait_for "$tmp/which.log" "waiting for a client"
connect alice "$out/06-which-alice.txt"
say alice "one"; say alice "two"; say alice "three"
sleep 0.5; catch_up
wait "$W" || true
drop alice
cp "$tmp/which.log" "$out/06-which.txt"
# The same three lines, sent together: the second and third are already there.
( cd probes/which-thread && exec ./bin/Release/$TFM/which ) > "$tmp/which.log" 2>&1 & W=$!; PIDS+=("$W")
wait_for "$tmp/which.log" "waiting for a client"
printf 'one\ntwo\nthree\n' | nc -N localhost 5000 > /dev/null || true
wait "$W" || true
{ echo "\$ printf 'one\\ntwo\\nthree\\n' | nc -N localhost 5000"; } > "$out/06-which-together-cmd.txt"
cp "$tmp/which.log" "$out/06-which-together.txt"

# One slow reader: alice stops reading (Control Z), a flood fills her socket, and the server's
# broadcast waits for her. bob counts what reaches him; dave says hello.
slow_reader() {
  local p=$1
  connect alice "$out/$p-alice.txt"
  pause alice
  rm -f "$tmp/received.txt"
  nc localhost 5000 > "$tmp/received.txt" < /dev/null & B=$!; PIDS+=("$B")
  sleep 0.5
  echo "\$ $FLOOD_CMD" > "$out/$p-flood.txt"
  flood & F=$!; PIDS+=("$F")
  : > "$out/$p-count.txt"
  for i in 1 2 3 4 5 6; do
    sleep 1
    echo "after $i s: $(wc -l < "$tmp/received.txt") lines" >> "$out/$p-count.txt"
  done
}
server 06-async
slow_reader 06
connect dave "$out/06-dave.txt"
say dave "hello? is anyone there?"
sleep 1; catch_up
{ echo '$ wc -l received.txt'; echo "$(wc -l < "$tmp/received.txt") received.txt"; } > "$out/06-received.txt"
grep -c "hello? is anyone there" "$tmp/received.txt" > "$out/06-dave-arrived.txt" || true
stop_server; cp "$tmp/06-async.log" "$out/06-slow-server.txt"
drop alice; drop dave
kill -9 "$B" "$F" 2>/dev/null || true

# ── 07: one blocking call inside the async server ──────────────────────────
server 07-blocking-read
connect alice "$out/07-alice.txt"
say alice "hello"
connect bob "$out/07-bob.txt"
say bob "hi, anyone?"
sleep 1; catch_up
cp "$out/07-bob.txt" "$out/07-bob-waiting.txt"
quit alice
sleep 0.5; catch_up
quit bob
sleep 1.2
stop_server; cp "$tmp/07-blocking-read.log" "$out/07-server.txt"

# ── 08: async void ─────────────────────────────────────────────────────────
server 08-async-void
slow_reader 08
set +e; wait "$SRV"; code=$?; set -e
{ sed "s#$here/steps/08-async-void/##g" "$tmp/08-async-void.log"; echo "(exit code $code)"; } > "$out/08-server.txt"
drop alice; kill -9 "$B" "$F" 2>/dev/null || true

# ── 09: an outbox per client ───────────────────────────────────────────────
server 09-outbox
slow_reader 09
connect dave "$out/09-dave.txt"
say dave "hello? is anyone there?"
sleep 1; catch_up
grep -c "hello? is anyone there" "$tmp/received.txt" > "$out/09-dave-arrived.txt" || true
kill -CONT "${NC[alice]}"
sleep 2
{ echo '$ wc -l received.txt'; echo "$(wc -l < "$tmp/received.txt") received.txt"; } > "$out/09-received.txt"
drop alice; kill -9 "$B" "$F" 2>/dev/null || true
quit dave
sleep 1.5
stop_server; cp "$tmp/09-outbox.log" "$out/09-slow-server.txt"

# The writer that never ends: a client leaves, and nothing is cleaned up.
server 09-outbox
connect alice "$out/09-leak-alice.txt"
say alice "hi"
quit alice
sleep 0.6
load 1000 1 > "$out/09-leak-load.txt"
sleep 2.2
{ echo "\$ ss -tn state close-wait '( sport = :5000 )' | tail -n +2 | wc -l"; closewait; } > "$out/09-closewait.txt"
stop_server; cp "$tmp/09-outbox.log" "$out/09-leak-server.txt"

# ── 10: cancellation ───────────────────────────────────────────────────────
server 10-cancel
connect alice "$out/10-alice.txt"
say alice "hi"
quit alice
sleep 0.6
load 1000 1 > "$out/10-load.txt"
sleep 2.2
{ echo "\$ ss -tn state close-wait '( sport = :5000 )' | tail -n +2 | wc -l"; closewait; } > "$out/10-closewait.txt"
cp "$tmp/10-cancel.log" "$out/10-server.txt"
# A client that disconnects in the middle of a write: alice stops reading, the flood fills her
# socket, then her terminal is closed (kill -9) while the server is still writing to her.
slow_reader 10-midsend
drop alice
sleep 1.5
kill -9 "$B" "$F" 2>/dev/null || true
sleep 1.2
cp "$tmp/10-cancel.log" "$out/10-midsend-server.txt"
# Control C with 1,000 clients connected.
load 1000 6 > "$tmp/load.txt" & L=$!
wait_for "$tmp/load.txt" welcomed
sleep 1.5
stop_server
wait $L || true
cp "$tmp/10-cancel.log" "$out/10-stop-server.txt"

# ── 11: a timeout on every write ───────────────────────────────────────────
server 11-timeout
slow_reader 11
sleep 4
drop alice; kill -9 "$B" "$F" 2>/dev/null || true
sleep 1.2
stop_server; cp "$tmp/11-timeout.log" "$out/11-slow-server.txt"

# The final run: 10,000 clients held open, two people chatting, then Control C while the
# 10,000 are still connected.
server 11-timeout
sleep 1.2
load 10000 20 > "$tmp/load.txt" & L=$!
wait_for "$tmp/load.txt" welcomed
connect alice "$out/11-final-alice.txt"
connect bob "$out/11-final-bob.txt"
say alice "ten thousand people are here"
say bob "and it still answers straight away"
sleep 1.5; catch_up
quit alice; quit bob
sleep 2.2
stop_server; cp "$tmp/11-timeout.log" "$out/11-final-server.txt"
wait $L || true; cp "$tmp/load.txt" "$out/11-final-load.txt"

echo "done: $(ls "$out" | wc -l) files in $out/"
