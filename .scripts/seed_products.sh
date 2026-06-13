#!/usr/bin/env bash
# Idempotent product seeder. Re-runs produce the same UUIDs so order seeds
# can reference them by id. Uses PUT /products/{id} (upsert-by-id).
#
# Usage:
#   bash .scripts/seed_products.sh
#   PRODUCTS_URL=http://localhost:5102 bash .scripts/seed_products.sh

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

BASE_URL="${PRODUCTS_URL:-http://localhost:5102}"

if ! command -v curl >/dev/null 2>&1; then
  echo "ERROR: curl is not on PATH. Install it or open a Git Bash terminal." >&2
  exit 127
fi

# Quick reachability probe — fails fast with a clear message if the service is down.
if ! curl -sS --max-time 3 -o /dev/null "${BASE_URL}/products?skip=0&take=1"; then
  echo "ERROR: cannot reach ${BASE_URL}. Is Services.Products running?" >&2
  exit 1
fi

# Fixed UUIDs — DO NOT change once referenced by other seed scripts.
IDS=(
  "22222222-2222-2222-2222-222222222201"
  "22222222-2222-2222-2222-222222222202"
  "22222222-2222-2222-2222-222222222203"
  "22222222-2222-2222-2222-222222222204"
  "22222222-2222-2222-2222-222222222205"
  "22222222-2222-2222-2222-222222222206"
  "22222222-2222-2222-2222-222222222207"
  "22222222-2222-2222-2222-222222222208"
  "22222222-2222-2222-2222-222222222209"
  "22222222-2222-2222-2222-222222222210"
  "22222222-2222-2222-2222-222222222211"
  "22222222-2222-2222-2222-222222222212"
  "22222222-2222-2222-2222-222222222213"
  "22222222-2222-2222-2222-222222222214"
  "22222222-2222-2222-2222-222222222215"
  "22222222-2222-2222-2222-222222222216"
  "22222222-2222-2222-2222-222222222217"
  "22222222-2222-2222-2222-222222222218"
  "22222222-2222-2222-2222-222222222219"
  "22222222-2222-2222-2222-222222222220"
)

NAMES=(
  "Wireless Mouse"
  "Mechanical Keyboard"
  "27-inch 4K Monitor"
  "USB-C Hub"
  "Laptop Stand"
  "Noise-Cancelling Headphones"
  "External SSD 1TB"
  "Webcam 1080p"
  "Desk Lamp"
  "Wireless Charger"
  "Bluetooth Speaker"
  "Smart Watch"
  "Tablet 10-inch"
  "Power Bank 20000mAh"
  "Gaming Chair"
  "Standing Desk"
  "Mesh Wi-Fi Router"
  "NAS 4-bay"
  "Action Camera"
  "Drone Mini"
)

SKUS=(
  "MOUSE-001"
  "KEYB-001"
  "MON-27-4K"
  "HUB-USBC-001"
  "STAND-LAP-001"
  "HDPN-NC-001"
  "SSD-EXT-1TB"
  "WEBCAM-1080"
  "LAMP-DESK-001"
  "CHARGE-WLS-001"
  "SPK-BT-001"
  "SW-001"
  "TAB-10-001"
  "PB-20K-001"
  "CHAIR-GMG-001"
  "DESK-STD-001"
  "ROUTER-MESH-001"
  "NAS-4B-001"
  "CAM-ACT-001"
  "DRONE-MINI-001"
)

PRICES=(
  "499.90"
  "1299.00"
  "8999.00"
  "599.00"
  "349.50"
  "2499.00"
  "1799.90"
  "799.00"
  "299.00"
  "199.90"
  "899.00"
  "3499.00"
  "5999.00"
  "799.90"
  "4999.00"
  "7499.00"
  "2999.00"
  "9999.00"
  "2299.00"
  "4499.00"
)

echo "Seeding products to ${BASE_URL} ..."

failures=0
for i in "${!IDS[@]}"; do
  id="${IDS[$i]}"
  name="${NAMES[$i]}"
  sku="${SKUS[$i]}"
  price="${PRICES[$i]}"

  body=$(cat <<EOF
{"name":"${name}","sku":"${sku}","price":${price}}
EOF
)

  status=$(curl -sS -o /tmp/seed_product_resp.json -w "%{http_code}" \
    -X PUT "${BASE_URL}/products/${id}" \
    -H "Content-Type: application/json" \
    -d "${body}") || status="000"

  case "${status}" in
    200) echo "  [200 updated] ${id}  ${sku}  ${name}" ;;
    201) echo "  [201 created] ${id}  ${sku}  ${name}" ;;
    *)
      echo "  [FAILED ${status}] ${id}  ${sku}  ${name}"
      if [[ -s /tmp/seed_product_resp.json ]]; then
        echo "    response:"
        sed 's/^/      /' /tmp/seed_product_resp.json
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

echo "Done. Product IDs:"
printf '  %s\n' "${IDS[@]}"
