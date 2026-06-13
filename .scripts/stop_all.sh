#!/usr/bin/env bash
# Stops any process bound to the service ports, regardless of how it was started
# (start_all.sh, VS Code "All" launch, manual `dotnet run`, etc.).
# Works on Windows (Git Bash / MSYS) and macOS / Linux.
#
# Usage:
#   bash .scripts/stop_all.sh

on_exit() {
  local rc=$?
  if (( rc != 0 )); then
    echo
    echo ">>> Script exited with code ${rc}"
  fi
  if [[ -t 0 && -t 1 ]]; then
    echo
    read -rp "Press Enter to close..." _ || true
  fi
  exit "${rc}"
}
trap on_exit EXIT

set -uo pipefail

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) IS_WINDOWS=1 ;;
  *)                    IS_WINDOWS=0 ;;
esac

# name:port pairs — keep in sync with start_all.sh.
SERVICES=(
  "customers:5101"
  "products:5102"
  "orders:5103"
  "listener:5100"
)

# Returns a space-separated list of PIDs listening on the given TCP port.
pids_on_port() {
  local port="$1"

  if (( IS_WINDOWS )); then
    # netstat -ano output: "  TCP    0.0.0.0:5101    0.0.0.0:0    LISTENING    12345"
    # We match LISTENING rows for :<port> (with a leading colon so 51010 doesn't hit 5101)
    # and pull the last whitespace-separated field, which is the PID.
    netstat -ano 2>/dev/null \
      | awk -v p=":${port}" '$0 ~ "LISTENING" && index($2, p) { print $NF }' \
      | sort -u \
      | tr '\n' ' '
    return
  fi

  if command -v lsof >/dev/null 2>&1; then
    lsof -ti "tcp:${port}" -sTCP:LISTEN 2>/dev/null | tr '\n' ' '
    return
  fi

  if command -v ss >/dev/null 2>&1; then
    # ss -ltnp output column "users:((\"dotnet\",pid=12345,fd=200))"
    ss -ltnp 2>/dev/null \
      | awk -v p=":${port}" '$4 ~ p":" || $4 ~ p"$" { print $0 }' \
      | grep -oE 'pid=[0-9]+' \
      | cut -d= -f2 \
      | sort -u \
      | tr '\n' ' '
    return
  fi

  echo ""
}

stop_pid() {
  local pid="$1"

  if (( IS_WINDOWS )); then
    # //T walks the child tree so the dotnet host + any spawned watchers go together.
    taskkill //F //T //PID "${pid}" >/dev/null 2>&1
    return $?
  fi

  if ! kill -0 "${pid}" 2>/dev/null; then
    return 1
  fi

  kill -TERM "${pid}" 2>/dev/null || true
  for _ in 1 2 3 4 5 6 7 8 9 10; do
    if ! kill -0 "${pid}" 2>/dev/null; then return 0; fi
    sleep 0.3
  done
  kill -KILL "${pid}" 2>/dev/null || true
  return 0
}

total_stopped=0
total_idle=0

for entry in "${SERVICES[@]}"; do
  name="${entry%%:*}"
  port="${entry##*:}"

  pids=$(pids_on_port "${port}")
  pids=$(echo "${pids}" | xargs)  # trim

  if [[ -z "${pids}" ]]; then
    echo "  [${name}] port ${port} idle"
    total_idle=$(( total_idle + 1 ))
    continue
  fi

  for pid in ${pids}; do
    if stop_pid "${pid}"; then
      echo "  [${name}] port ${port} stopped (pid ${pid})"
      total_stopped=$(( total_stopped + 1 ))
    else
      echo "  [${name}] port ${port} could not stop pid ${pid}"
    fi
  done
done

echo
echo "Done. Stopped ${total_stopped} process(es), ${total_idle} port(s) already idle."
