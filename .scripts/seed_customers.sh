#!/usr/bin/env bash
# Idempotent customer seeder. Re-runs produce the same UUIDs so other seed scripts
# (orders, etc.) can reference them by id. Uses PUT /customers/{id} which is
# upsert-by-id on Services.Customers.
#
# Usage:
#   bash .scripts/seed_customers.sh
#   CUSTOMERS_URL=http://localhost:5101 bash .scripts/seed_customers.sh

# Keep the terminal window open when the script is launched by double-click / file
# manager so any error is readable. Only pauses if stdin is a TTY (skips CI).
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

# NOTE: no `set -e` on purpose — we want the loop to continue past individual
# failures so you can see WHICH ids failed. Each curl call has its own status check.
set -uo pipefail

BASE_URL="${CUSTOMERS_URL:-http://localhost:5101}"

if ! command -v curl >/dev/null 2>&1; then
  echo "ERROR: curl is not on PATH. Install it or open a Git Bash terminal." >&2
  exit 127
fi

# Quick reachability probe — fails fast with a clear message if the service is down.
if ! curl -sS --max-time 3 -o /dev/null "${BASE_URL}/customers?skip=0&take=1"; then
  echo "ERROR: cannot reach ${BASE_URL}. Is Services.Customers running?" >&2
  exit 1
fi

# Fixed UUIDs — DO NOT change once you've started using them in other seed scripts.
IDS=(
  "11111111-1111-1111-1111-111111111101"
  "11111111-1111-1111-1111-111111111102"
  "11111111-1111-1111-1111-111111111103"
  "11111111-1111-1111-1111-111111111104"
  "11111111-1111-1111-1111-111111111105"
)

NAMES=(
  "Ali Yilmaz"
  "Ayse Demir"
  "Mehmet Kaya"
  "Fatma Sahin"
  "Mustafa Celik"
)

EMAILS=(
  "ali.yilmaz@example.com"
  "ayse.demir@example.com"
  "mehmet.kaya@example.com"
  "fatma.sahin@example.com"
  "mustafa.celik@example.com"
)

PHONES=(
  "+905300000001"
  "+905300000002"
  "+905300000003"
  "+905300000004"
  "+905300000005"
)

echo "Seeding customers to ${BASE_URL} ..."

failures=0
for i in "${!IDS[@]}"; do
  id="${IDS[$i]}"
  name="${NAMES[$i]}"
  email="${EMAILS[$i]}"
  phone="${PHONES[$i]}"

  body=$(cat <<EOF
{"name":"${name}","email":"${email}","phone":"${phone}"}
EOF
)

  status=$(curl -sS -o /tmp/seed_customer_resp.json -w "%{http_code}" \
    -X PUT "${BASE_URL}/customers/${id}" \
    -H "Content-Type: application/json" \
    -d "${body}") || status="000"

  case "${status}" in
    200) echo "  [200 updated] ${id}  ${name}" ;;
    201) echo "  [201 created] ${id}  ${name}" ;;
    *)
      echo "  [FAILED ${status}] ${id}  ${name}"
      if [[ -s /tmp/seed_customer_resp.json ]]; then
        echo "    response:"
        sed 's/^/      /' /tmp/seed_customer_resp.json
        echo
      fi
      failures=$(( failures + 1 ))
      ;;
  esac
done

echo
if (( failures > 0 )); then
  echo "Done with ${failures} failure(s)."
  exit 1
fi

echo "Done. Customer IDs:"
printf '  %s\n' "${IDS[@]}"
