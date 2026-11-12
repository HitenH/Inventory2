# Browser checks (keyboard and cursor behaviour)

Small scripts that drive a hidden Chrome window against a **test copy** of the app and print where the cursor
is after each key press. They were used to find and confirm the keyboard/cursor fixes of November 2026
(things the xUnit tests cannot see). Development only; not part of the app or the build.

**Never point these at the real data** – they create purchases, sales, orders and a test customer.

## One-time setup per run

1. Build the app (`dotnet build Inventory`).
2. Make a test copy of the data in a scratch folder and give the Admin user a known test password
   (the app hashes plain-text passwords at startup):
   ```
   copy Inventory\bin\Debug\net10.0\Inventory.db <scratch>\data\Inventory.db
   python -c "import sqlite3; c=sqlite3.connect(r'<scratch>\data\Inventory.db'); c.execute(\"update Users set Password='UiTest#2026' where Name='Admin'\"); c.commit()"
   ```
3. Start a second copy of the app on port 5299 using that folder (PowerShell):
   ```
   $env:ASPNETCORE_ENVIRONMENT='Development'; $env:DataFolder='<scratch>\data'; $env:BackupFolder='<scratch>\data\Backups'
   Start-Process dotnet -ArgumentList 'bin\Debug\net10.0\Inventory.dll','--urls','http://localhost:5299' -WorkingDirectory Inventory -WindowStyle Minimized
   ```
4. Run a check (Node 22+ and Chrome must be installed): `$env:SCRATCH='<scratch>'; node tools\ui-check\check-keyboard-only.mjs`
5. Stop the test copy afterwards: the `dotnet.exe` process whose command line contains `5299`.

The checks use names from the May 2026 data (supplier BALAJI / CAPITAL, customer AAI MATHA, product LAZER/CUTTING).

## Scripts

- `cdp.mjs` – tiny Chrome DevTools driver (launch, log in, type, keys, click, where is the cursor).
- `check-keyboard-only.mjs` – a whole purchase line by keyboard; Sale customer/product via pop-up (samples the cursor
  for 3 s to catch flashing); Scan Mass Products cursor; new purchase via pop-up; Purchase Order Enter order.
- `check-entry-flows.mjs` – purchase with two lines, sales order, purchase order, Esc back.
- `check-popups-and-sale.mjs` – Purchase Order cursor after a row, Sale cursor rules, pop-up search bar / arrows / click / Esc.
- `check-save-and-focus.mjs` – new product cursor in Name, Ctrl+S on a new customer.
