#!/usr/bin/env bash
# Starts all 4 services with `dotnet run -c Release` + ASPNETCORE_ENVIRONMENT=Production.
# Logs from every service are streamed into the current terminal with a per-service prefix
# so you can watch what comes up and what does not. Ctrl+C stops everything cleanly.
#
# stop_all.sh discovers running services by their bound port, so no PID tracking is
# needed here. Anything that ends up listening on 5100–5103 will be stopped by it.
#
# Works on Windows (Git Bash / MSYS) and macOS / Linux.
#
# Usage:
#   bash .scripts/start_all.sh

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

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) IS_WINDOWS=1 ;;
  *)                    IS_WINDOWS=0 ;;
esac

# Service name -> project folder -> port. Order matters: write-source services first,
# listener last so the change stream attaches after the others are accepting writes.
SERVICES=(
  "customers:Services.Customers:5101"
  "products:Services.Products:5102"
  "orders:Services.Orders:5103"
  "listener:Services.ReadModelBuilder:5100"
)

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet is not on PATH." >&2
  exit 127
fi

shutdown_all() {
  echo
  echo ">>> Shutting down services (closing log pipelines)..."
  # Closing our background pipelines tears down the dotnet hosts. On Windows we also
  # nudge anything still listening on our ports via stop_all.sh as a belt-and-suspenders.
  jobs -p | xargs -r kill 2>/dev/null || true
  if (( IS_WINDOWS )); then
    bash "${SCRIPT_DIR}/stop_all.sh" </dev/null >/dev/null 2>&1 || true
  fi
}
trap 'shutdown_all' INT TERM

prefix_stream() {
  local name="$1"
  local pad
  pad=$(printf '%-10s' "${name}")
  while IFS= read -r line; do
    printf '[%s] %s\n' "${pad}" "${line}"
  done
}

echo "Starting services from ${REPO_ROOT}"
echo

for entry in "${SERVICES[@]}"; do
  IFS=':' read -r name proj port <<< "${entry}"
  proj_dir="${REPO_ROOT}/src/${proj}"

  if [[ ! -d "${proj_dir}" ]]; then
    echo "[${name}] missing project at ${proj_dir}" >&2
    continue
  fi

  echo "[${name}] starting (${proj}) on http://localhost:${port} ..."

  # --no-launch-profile bypasses launchSettings.json (which would force Development).
  # --urls pins the bind address; without it Kestrel falls back to http://localhost:5000
  # and every service would collide there.
  (
    cd "${proj_dir}"
    ASPNETCORE_ENVIRONMENT=Production \
      DOTNET_ENVIRONMENT=Production \
      exec dotnet run -c Release --no-launch-profile --urls "http://localhost:${port}" 2>&1
  ) | prefix_stream "${name}" &

  # A tiny stagger so the per-service prefixed startup logs don't fully interleave.
  sleep 0.5
done

echo
echo ">>> All services launched. Streaming logs. Press Ctrl+C to stop everything,"
echo "    or run 'bash .scripts/stop_all.sh' from another terminal."
echo

wait
