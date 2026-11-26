#!/usr/bin/env bash
# The full browser suite (all checks of README section 6) on test copies: one shared copy prepared by the setup scripts,
# and a fresh copy each for check-users and check-roles. Never touches real data. Usage: bash tools/ui-check/run-full-suite.sh
# Prints one line per check (exit code, FAIL/ERROR lines, last line); each check's output is kept in <dir>/<check>.log.
REPO="/c/Users/Hiten/source/repos/Inventory2 (before customer search modification)"
# Where the test copies go (a scratch folder; set FULLSUITE_DIR to choose another).
SCR="${FULLSUITE_DIR:-${TEMP:-/tmp}/inventory-fullsuite}"
cd "$REPO" || exit 1
rm -rf "$SCR"; mkdir -p "$SCR"

stop_app() {
  for pid in $(netstat -ano | grep -E ":5299 .*LISTENING" | awk '{print $5}' | sort -u); do taskkill //PID "$pid" //F > /dev/null 2>&1; done
  sleep 2
}
fresh_copy() {  # $1 = folder
  mkdir -p "$1/data/Backups" "$1/shots"
  python -c "import sqlite3; s=sqlite3.connect('file:Inventory/bin/Debug/net10.0/Inventory.db?mode=ro',uri=True); d=sqlite3.connect(r'$1/data/Inventory.db'); s.backup(d); d.close()"
}
start_app() {  # $1 = folder
  (cd Inventory && ASPNETCORE_ENVIRONMENT=Development DataFolder="$1/data" BackupFolder="$1/data/Backups" dotnet bin/Debug/net10.0/Inventory.dll --urls http://localhost:5299 > "$1/app.log" 2>&1 &)
  for i in $(seq 1 80); do curl -s -o /dev/null http://localhost:5299/login && return 0; sleep 1; done
  echo "APP DID NOT START"; return 1
}
run() {  # $1 = script, rest = env
  local name="$1"; shift
  local out; out=$(env "$@" node "tools/ui-check/$name" 2>&1)
  local code=$?
  echo "$out" > "$SCR/$name.log"
  local fails; fails=$(echo "$out" | grep -cE "^FAIL|ERROR")
  echo "$name: exit $code, FAIL/ERROR lines $fails | $(echo "$out" | tail -1)"
}

stop_app
A="$SCR/shared"
fresh_copy "$A"
python tools/ui-check/setup-damage-returns.py "$A/data/Inventory.db" > /dev/null
IDS=$(python tools/ui-check/setup-full-suite.py "$A/data/Inventory.db" | tail -2)
CUST=$(echo "$IDS" | head -1 | tr -d '\r'); PUR=$(echo "$IDS" | tail -1 | tr -d '\r')
echo "ids: customer=$CUST purchase=$PUR"
start_app "$A" || exit 1   # first start: upgrades the copy (the stock-figures setup needs the new tables)
python tools/ui-check/setup-stock-figures.py "$A/data/Inventory.db" > /dev/null
stop_app                    # that setup writes the Admin test password in plain text: a restart turns it into a hash
start_app "$A" || exit 1
S="$A/"
run check-keyboard-only.mjs SCRATCH="$S"
run check-entry-flows.mjs SCRATCH="$S"
run check-popups-and-sale.mjs SCRATCH="$S"
run check-save-and-focus.mjs SCRATCH="$S"
run check-bulk-entry.mjs SCRATCH="$S"
run check-alt-b.mjs SCRATCH="$S"
run check-line-rules.mjs SCRATCH="$S"
run check-import.mjs SCRATCH="$S"
run check-access.mjs SCRATCH="$S"
run check-rates.mjs SCRATCH="$S"
run check-edit-clash.mjs SCRATCH="$S" CLASH_CUSTOMER="$CUST" CLASH_PURCHASE="$PUR"
run check-activity-log.mjs SCRATCH="$S" LOG_CUSTOMER="$CUST" LOG_PURCHASE="$PUR"
run check-labels.mjs SCRATCH="$S" LOG_PURCHASE="$PUR"
run check-stock-figures.mjs SCRATCH="$S" STOCK_JSON="$A/data/stock-figures.json"
run check-damage-returns.mjs SCRATCH="$S" DAMAGE_JSON="$A/data/damage-returns.json"
run check-damage-report.mjs SCRATCH="$S" DAMAGE_JSON="$A/data/damage-returns.json"
run check-stock-approval.mjs SCRATCH="$S"
run check-places.mjs SCRATCH="$S" PRODUCT_ID=56671BCA-9D74-4363-A048-6B80076A892F   # stage 3: places, racks, racks per variant
run check-place-flow.mjs SCRATCH="$S"   # stage 3, 3-3/3-4: purchases arrive in the godown, Sale "From", places per user
run check-moves.mjs SCRATCH="$S" DAMAGE_JSON="$A/data/damage-returns.json"   # stage 3, 3-5/3-6: moves, receiving, missing pieces
run check-look.mjs SCRATCH="$S"
run check-activity-screen.mjs SCRATCH="$S" LOG_CUSTOMER="$CUST" LOG_PURCHASE="$PUR"
stop_app

for chk in check-users.mjs check-roles.mjs; do
  F="$SCR/${chk%.mjs}"
  fresh_copy "$F"
  python -c "import sqlite3; c=sqlite3.connect(r'$F/data/Inventory.db'); c.execute(\"update Users set Password='UiTest#2026' where Name='Admin'\"); c.commit()"
  start_app "$F" || exit 1
  run "$chk" SCRATCH="$F/"
  stop_app
done
echo "FULL SUITE DONE"
