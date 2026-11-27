# Browser checks

Small scripts that drive a hidden Chrome window against a **test copy** of the app and check what a person would see and
do: where the cursor goes after each key press, what each screen shows, and that refused entries say why. They catch what
the xUnit tests cannot see. Development only; not part of the app or the build.

**Never point these at the real data** – they create purchases, sales, orders, returns, damage and test users.

## The whole suite (once per stage, and before any install)

```
bash tools/ui-check/run-full-suite.sh
```

It builds nothing (build first: `dotnet build Inventory/Inventory.csproj`), makes its own test copies of
`Inventory\bin\Debug\net10.0\Inventory.db` in a scratch folder (`FULLSUITE_DIR`, default `%TEMP%\inventory-fullsuite`),
prepares them with the setup scripts, starts the app on port 5299, runs every check, and stops the app again. It prints one
line per check (exit code, FAIL/ERROR lines, last line); each check's full output is in `<dir>/<check>.log`. `check-users`
and `check-roles` change users and roles, so each gets its own fresh copy. Last run: stage 2, 26 Nov 2026 – all 21 checks pass.

## One check by hand

1. Build the app (`dotnet build Inventory/Inventory.csproj`).
2. Copy `Inventory\bin\Debug\net10.0\Inventory.db` to `<scratch>\data\Inventory.db` and run the setup script the check
   needs **before starting the copy** (the app turns plain-text passwords into hashes at start-up). Every setup script sets
   the Admin password to `UiTest#2026`; `setup-full-suite.py` prepares everything the shared checks need.
3. Start a second copy of the app on port 5299 using that folder (PowerShell):
   ```
   $env:ASPNETCORE_ENVIRONMENT='Development'; $env:DataFolder='<scratch>\data'; $env:BackupFolder='<scratch>\data\Backups'
   Start-Process dotnet -ArgumentList 'bin\Debug\net10.0\Inventory.dll','--urls','http://localhost:5299' -WorkingDirectory Inventory -WindowStyle Minimized
   ```
4. Run the check (Node 22+ and Chrome must be installed): `$env:SCRATCH='<scratch>\'; node tools\ui-check\check-keyboard-only.mjs`
5. Stop the test copy afterwards by its process id (the `dotnet.exe` listening on 5299) – never by name.

The checks use names from the May 2026 data (supplier BALAJI / CAPITAL, customer AAI MATHA, products such as LAZER/CUTTING,
AJ0120103 1 NO HAATICHAP, AJ0120128 1 NO LED FRAME).

## Setup scripts

- `setup-full-suite.py <db>` – Admin and Hiten passwords, the users and roles of check-access, check-rates and the
  activity-log checks; prints the customer id and purchase id to pass as LOG_CUSTOMER / LOG_PURCHASE (and CLASH_…).
- `setup-activity-log.py <db>` – the two users of the activity-log checks (called by setup-full-suite).
- `setup-stock-figures.py <db>` – stage 2: damage on product AJ0120103 written straight into the copy (run it after the
  stage 2 upgrade is on the copy; it writes the Admin password in plain text, so restart the copy afterwards); writes
  `stock-figures.json` for STOCK_JSON.
- `setup-damage-returns.py <db>` – stage 2: Admin password and the newest purchase with two or more lines; writes
  `damage-returns.json` for DAMAGE_JSON.

## Scripts

- `cdp.mjs` – tiny Chrome DevTools driver (launch, log in, type, keys, click, where is the cursor).
- `run-full-suite.sh` – the whole suite (above).
- `check-keyboard-only.mjs` – a whole purchase line by keyboard; Sale customer/product via pop-up; Scan Mass Products; Purchase Order Enter order.
- `check-entry-flows.mjs` – purchase with two lines, sales order, purchase order, Esc back.
- `check-popups-and-sale.mjs` – Purchase Order cursor after a row, Sale cursor rules, pop-up search bar / arrows / click / Esc.
- `check-save-and-focus.mjs` – new product cursor in Name, Ctrl+S on a new customer ("saved row visible in list = false" is
  expected: the list is paged and the test record sorts onto a later page).
- `check-bulk-entry.mjs` – Bulk Entry on Purchase Orders, Purchases and Sales Orders.
- `check-alt-b.mjs` – Alt+B opens Bulk Entry / Scan Mass Products.
- `check-line-rules.mjs` – quantity and discount rules on every screen that makes a line (version 3.0, 1a-1).
- `check-import.mjs` – the Import pop-up for customers, suppliers, products and purchases.
- `check-users.mjs`, `check-roles.mjs` – the Users and Roles screens (each on its own fresh copy).
- `check-access.mjs` – screens and menu follow the role; disabled users.
- `check-rates.mjs` – rates hidden both ways (read-only).
- `check-edit-clash.mjs` – two browsers on one customer and one purchase ("someone else changed this").
- `check-activity-log.mjs`, `check-activity-screen.mjs` – the activity panel and the Activity log screen (the second erases the copy's log).
- `check-labels.mjs` – labels on paper (positions, PDF), from a purchase and a product.
- `check-backup-folder.mjs` – Settings → Change folder… (not in the suite: it changes the backup folder).
- `check-stock-figures.mjs` – stage 2: sellable pieces with the damage arrow and breakdown (STOCK_JSON).
- `check-damage-returns.mjs` – stage 2: Record damage (and lowering it), Sales Returns, Given in exchange (DAMAGE_JSON).
- `check-damage-report.mjs` – stage 2: the damage report's row drop-downs and actions, Sent → purchase return, Purchase Returns (DAMAGE_JSON).
- `check-stock-approval.mjs` – stage 2: the reasons list, the Stock changes screen, the approval switch and "Waiting for approval".
- `check-places.mjs` – stage 3: Settings → Places and racks (add places and racks, levels, deleting refused while in use), the
  address box (0203B + Enter, drop-downs, a wrong address explained) and racks per variant on a product page (★ main, ✕,
  "Same for all variants") (PRODUCT_ID = a product with several variants; changes the test copy).
- `check-whereis.mjs` – stage 3, 3-7: Put away on a purchase (a rack per line by typing or scanning, a wrong rack explained) and
  "Where is it?" (an item by its label, a rack level and a whole rack, ✕ on an emptied rack) (setup-damage-returns.py; DAMAGE_JSON).
- `check-moves.mjs` – stage 3, 3-5/3-6: the two switches, "Send to …" on a purchase in the godown, receiving (a scan, a typed
  quantity), one piece missing – red on the move, in the list and the menu – written off, a starting count, Esc back
  (setup-damage-returns.py; DAMAGE_JSON; changes the copy).
- `check-place-flow.mjs` – stage 3, 3-3/3-4: purchases arrive in the godown and sales leave from the shop, "Arrives in" on a
  purchase, the Sale "From" box (skipped by Tab, needs "Sell from another place"), the Place box on Stock changes, stock per
  place behind the arrow, places per user, a godown-only user (setup-full-suite.py; FLOW_PRODUCT = AJ0120128's id; changes the copy).
- `check-look.mjs` – every screen at 1366 and 1920 px, light and dark; fails on a sideways scroll bar, a product name
  wrapping to two lines, or a control sticking out of its box. Screenshots to `<SCRATCH>shots`.
