# Inventory App - version 2.0

This is the one document that explains the app as it stands today ("version 2.0"). The long-range plan for version 3.0 is in [ROADMAP.md](ROADMAP.md) and is not repeated here.

Contents: 1 What the app is and does - 2 Version history - 3 Technology - 4 How it is put together - 5 How the main flows work - 6 Keyboard and cursor behaviour - 7 Look and feel rules - 8 Build, run and test on the development PC - 9 Rules for every change - 10 Rolling out to the counter PC - 11 Known limits and small issues - 12 Decisions on record - 13 History in git

---

## 1. What the app is and does

### In plain English

This is a stock and sales program for a small shop. It runs on one PC at the counter. You open it in a web browser (Chrome), but nothing goes over the internet: the program and the data both live on that PC.

You use it to:

- keep a list of **customers**, **suppliers** and **products** (a product can have several **variants**, for example the same item from different makers, each with its own ID, name and optional photo);
- record **purchases** (goods coming in from a supplier), which make stock go up;
- record **sales** at the counter by scanning barcodes with a scanner, which make stock go down;
- keep **purchase orders** and **sales orders** (promises or requests that are not yet purchases or sales);
- look at **reports**;
- take **backups** of all the data, and restore one if something goes wrong.

**How stock works.** The program never stores a "stock" number. For every variant, stock in hand is worked out each time it is needed: everything purchased minus everything sold. This means stock can never drift out of step with the purchases and sales, but it also means a correction has to be made by entering a purchase or a sale. (Version 3.0 plans to change this; see ROADMAP.md.)

The app is in daily use on the counter PC. The rule for all work is: improve it, do not break it.

### The screens

- **Dashboard** (home): a welcome line and the list of keyboard shortcuts.
- **Customers** and **Suppliers**: list with search, add, edit, delete (blocked when the customer or supplier already has sales or purchases). Each can have up to three mobile numbers. There is an **Import** button on each list (section 5).
- **Categories**: kept from the Products screen (the Esc key on a category page goes back to Products).
- **Products**: list with search and an **Import** button. The product page holds the product details and its variants (Variant ID, name, reorder level, a JPEG photo). Photos are resized on upload.
- **Purchase Orders**: one editable table. Each row is one order line (supplier, product, variant, quantity, dates, status Open / Completed / Partly). Status can be changed for all ticked rows at once; completed orders can be deleted in one go. Bulk Entry adds many rows at once.
- **Purchases**: list of vouchers and a purchase page: supplier, date, lines, discount, total, remarks. Has an **Import** button (opening stock).
- **Sales Orders**: same idea as purchases, for customers.
- **Sale**: the counter screen. Pick the customer, scan product and variant barcodes (or use **Scan Mass Products** to scan many in a row), then press **Summarize**.
- **Sales Summary**: after Summarize you set the rate and discount per line and an overall discount, then Save. The total is stored here.
- **Reports** (tabs): Customer (By Area, Customers), Supplier, Products (Reorder Level Table, Variant Report Table, Variants List Table), Purchases Orders, Sales Orders, Sales & Purchases (totals for a chosen period such as Today, Last 7 days, This month, This financial year starting April).
- **Settings**: Appearance (theme colour and Light/Dark), backup list with Backup, Restore and Delete buttons, and where the database and backup folder are.
- **Change Password**: every user changes their own password.
- **Login**: every page except Login needs a login. There are two users, Hiten and Admin. The owner knows the passwords.

### Import (customers, suppliers, products, purchases)

Import reads a CSV or Excel file, shows a preview with every problem listed, and only then, if the file is clean, saves everything in one go. A sample file for each kind can be downloaded from the import window. Details and rules are in section 5. At the moment **every user can use Import** (there are no roles yet).

---

## 2. Version history

- **1.0** - the May 2026 build, installed on the counter PC and still running there.
- **2.0** - this version, concluded 14 Nov 2026 (git tag `v2.0`), the November 2026 improvement work: security (hashed passwords, login on every page), data safety (database rules, one-transaction saves, safe daily backups), upgrade to .NET 10, automatic tests, clean-up of duplicated code, speed, keyboard entry, a new look with themes, and Import.
- **3.0** - planned, not started: see [ROADMAP.md](ROADMAP.md). Several decisions in section 12 are scheduled there.

---

## 3. Technology

Taken from `Inventory/Inventory.csproj` and `global.json`.

| Part | What | Why it is kept |
|---|---|---|
| Framework | .NET 10 (`net10.0`), ASP.NET Core **Blazor Server** (interactive server mode: the pages run on the PC, the browser only shows them over a live connection) | One language and one program for everything. Fine for one local PC. |
| Database access | Entity Framework Core 10.0.15 with SQLite | EF Core is the library that turns C# objects into database rows. SQLite is a single-file database: no database server to install, and a backup is one file. |
| Screen parts | MudBlazor 9.12.0 (the only UI library). Bootstrap CSS files remain only for page layout. | One consistent set of tables, dialogs, buttons. |
| Excel files | ClosedXML 0.105.0 (reads `.xlsx` for Import) | The CSV reader is the app's own code. |
| Logging | Serilog (Serilog.AspNetCore 10.0.0, Serilog.Sinks.File 7.0.0) | A daily log file that can be sent to a developer. |
| Tests | xUnit v3 (4.0.3) on Microsoft.Testing.Platform (set in `global.json`), test project `Inventory.Tests` | Automatic checks. |
| Build server | GitHub Actions (`.github/workflows/build.yml`): on every push or pull request to `main`, on Windows, restore, build in Release, run all tests | Catches a broken build early. |

The decision to keep this stack (rather than rebuild) was made in the first review (see section 13). Nullable reference types and implicit usings are switched on. The build shows a few hundred "hint" warnings (mostly about nullable values); the rule is to add no new ones.

---

## 4. How it is put together

### Folder map

```
Inventory.sln
global.json                   makes `dotnet test` use the Microsoft.Testing.Platform runner
ROADMAP.md                    plan for version 3.0
Inventory/                    the app
  Program.cs                  start-up: log file, database factory, repositories, migrations, user set-up, backup scheduler, routes
  appsettings.json            DataFolder, BackupFolder, BackupDailyAt, BackupKeepDaily, FileMaxSize, CustomerNumberMobile, SupplierNumberMobile
  appsettings.Development.json  development: data and backups next to the built program
  Properties/launchSettings.json  "http" profile = http://localhost:5259 (Development)
  Authentication/             login: CustomAuthenticationProvider, PasswordService, UserBootstrap, UserSession
  Domain/AppDbContext.cs      the tables, unique indexes, relationships
  Domain/Entities/            one class per table (Customer, Supplier, Product, Variant, Image, Category, Mobile, Purchase,
                              PurchaseVariant, PurchaseOrder, SalesOrder, SalesOrderVariant, Sales, SalesVariant, SalesSummary, User)
  Domain/Repository/          one repository per area (+ Abstract/ interfaces); SimpleRepositoryEF<T> for simple tables
  Domain/TransactionTotals.cs recalculates purchase and sales-order totals
  Domain/TransactionLineHydrator.cs  fills in names on loaded lines
  Models/                     view models used by the screens; ModelMapping.cs converts model <-> entity (ToModel / ToEntity)
  Pages/                      one .razor (+ .razor.cs) per screen; Pages/Reports/ holds the report tabs
  Shared/                     parts used by several screens (see "one of each" below), MainLayout, NavMenu
  Shared/Selectors/           the picker (LookupAutocomplete, LookupSettings, and thin Customer/Supplier/Product wrappers)
  MudBlazorComponents/        pop-ups: BulkEntryDialog, ScanMassProducts, LookupDialog, ConfirmationDialog (+ DialogServiceExtensions),
                              ImportDialog, ErrorDetailsDialog, ImagePopup, LoadingComponent, PagesHeader
  Service/                    helpers: Shortcuts, BulkEntry, LineAmounts, BackupService, BackupScheduler, DataPaths, DbErrors,
                              ReportPeriods, ListPaging, Photos, ColumnSorter, DateConversion, AppTheme, AppThemeState, MobileService
  Service/Import/             the import feature (section 5)
  Migrations/                 database changes, oldest to newest; latest: 20261113120000_RemoveUserRole
  wwwroot/shortcuts.js        all keyboard and cursor handling in the browser
  wwwroot/main.js             theme saving, floating-label helper
  wwwroot/css/                theme.css (colours, buttons), tables.css (tables, entry rows, labels), site.css
Inventory.Tests/              xUnit tests; TestDatabase.cs builds a fresh database with the real migrations for each test
tools/ui-check/               browser checks against a test copy (section 8); has its own README
```

### The layers

Pages (screens) talk to services and repositories; repositories talk to the database. A page never builds its own database query when a repository method exists. Business rules live as low as possible: totals and line rules in the data layer, the strictest data rules in the database itself. Some rules are still inside screens (see section 11).

### The "one of each" shared pieces

These exist once on purpose. Fix or change them in one place; never copy them into a screen.

- **Line editor**: `Shared/TransactionLineEditor.razor(.cs)` with `Shared/TransactionLines.cs`. Used by both Purchase and Sales Order (via `PurchaseVariantComponent` / `SalesOrderVariantComponent`).
- **Picker** (search box that finds a customer, supplier or product, with a "pick from list" pop-up): `Shared/Selectors/LookupAutocomplete` with `LookupSettings`, and `MudBlazorComponents/LookupDialog`.
- **Date box**: `Shared/DateBox.razor`.
- **Confirm dialog**: `MudBlazorComponents/ConfirmationDialog` with `DialogServiceExtensions`.
- **Sorter** for table columns: `Service/ColumnSorter.cs`.
- **Stock query**: `ProductRepositoryEF` (`GetProductStockReportAsync`, `GetStockInHandByVariantIdsAsync`, one shared "purchased minus sold" query). The Stock report and stock-in-hand on screens share it. Keep it that way.
- **Shortcuts list**: `Service/Shortcuts.cs`, used by the key handler, the menu hints and the Dashboard table, so they cannot disagree.
- **Import column list**: `Service/Import/ImportTemplates.cs`. The reader, the checks, the sample files and the screen all read it.
- **Line maths**: `Service/LineAmounts.cs` (amount = rate x quantity; amount after discount; all rounded to 2 decimals).
- **Bulk Entry rules**: `Service/BulkEntry.cs` (kinds PurchaseOrder, Purchase, SalesOrder).
- **Paging and report periods**: `Service/ListPaging.cs` (100 rows per page by default; 10/20/50/100/200 offered), `Service/ReportPeriods.cs`.

### Data access pattern

The database is registered with `AddDbContextFactory`. Every repository method opens its own short-lived database context, does its work and closes it. (A "context" is the object that holds a database connection and the records currently loaded.) Reads use `AsNoTracking` (read-only, no change tracking) wherever the data is only shown. This avoids "a second operation was started" errors and stale data between screens. `AppDbContext.SaveChanges` undoes the pending changes in memory if a save fails, so one failed save cannot make later saves fail. Each save of a voucher (purchase, sale, order) is one database transaction: it all saves or none of it does.

### Database rules

These are enforced by the database, not just by the screens:

- **Unique IDs ignoring case**: Product ID, Customer ID, Supplier ID, and Variant ID per product (`C001` and `c001` count as the same). Done with `COLLATE NOCASE` unique indexes. If a migration rebuilds the Products, Customers, Suppliers or Variants table, the NOCASE indexes must be created again by hand in that migration (EF would drop the NOCASE part).
- **Triggers**: customers, suppliers, products and variants must have an ID and a name (a variant must also belong to a product); every purchase, sales-order and sale line must point to a real product and one of its variants. Names: `TR_<table>_NoBlank_Insert/Update`, `TR_<table>_RealItem_Insert/Update`.
- **`RULE:` messages**: a trigger refuses the save with a text that starts `RULE:`. `Service/DbErrors.cs` finds that text and shows it to the operator in plain words (`RuleMessage`, `SaveMessage`, `IsUniqueViolation`).
- **Start-up check**: if a database update fails at start, `DbErrors.FindDuplicateIds` writes every duplicate ID (ignoring case) to the log, and the app refuses to start with the data untouched.
- **Not enforced**: voucher numbers are not unique (see section 12, old data has duplicates). Empty IDs in the old data are allowed more than once.

Voucher numbers: the next number for a date is the last number of that date plus one (Purchase, Sale and Import all use this).

### Totals and line maths

- A line: amount = rate x quantity (2 decimals); amount after discount = amount minus its percentage discount (2 decimals). Discount on a line is a whole percentage 0 to 100.
- Purchase and sales-order header total = sum of line "amount after discount" minus the header discount amount. `Domain/TransactionTotals.cs` recalculates it inside the same transaction as every line or header change. Sales orders store their discount.
- Sales: stock leaves when the sale is saved; the Sales Summary screen then fixes rates, discounts and the stored total.
- Customer and supplier totals, and report sums, are calculated by the database (SQL sums), not loaded row by row.
- Names in the code: line discounts are `DiscountPercent`, header discounts are `DiscountAmount`. The database column is still called `Discount` (the property carries `[Column("Discount")]`), so no migration was needed.

### Login and passwords

- Users are rows in the `Users` table. Passwords are stored **hashed** with ASP.NET Core's PasswordHasher (PBKDF2 with a random salt; `Authentication/PasswordService.cs`). Minimum length 8.
- At start-up (`Authentication/UserBootstrap.cs`): any old plain-text password is hashed in place (so old logins keep working on first start). If the Users table is empty, an `Admin` user is created with a random password, printed in the console, written to the log and to `initial-admin-password.txt` in the data folder.
- The login is kept in the browser's session storage (ProtectedSessionStorage), per browser tab, and is checked inside the live connection (`AuthorizeRouteView` in `Routes.razor` sends you to `/login`). Because of that, `Program.cs` marks the Razor routes `AllowAnonymous` on purpose; do not "fix" that.
- There are no roles yet. The old role column was removed (migration `RemoveUserRole`).
- `/photos/{id}` and `/import-sample/{kind}` do not need a login (local PC only; photo IDs are random). Revisit when roles come.

### Photos

One JPEG per variant, stored in the database (so backups contain it). Uploads must be JPEG, are resized, and are limited by `FileMaxSize` (400 MB). The browser fetches each photo from `/photos/{id}` (`Service/Photos.cs`) with a fingerprint so unchanged photos are not re-sent. A photo not yet saved is shown inline.

### Backups

All in `Service/BackupService.cs` and `BackupScheduler.cs`. Every backup uses SQLite's online backup, is integrity-checked, and only gets its final name after the check passes (a plain file copy would miss data still held in the `-wal` file).

| File name starts with | Kind | Made by | Removed automatically? |
|---|---|---|---|
| `backup_` | Manual | the Backup button on Settings | never |
| `daily_` | Automatic (daily) | the scheduler | only the newest 30 are kept (`BackupKeepDaily`) |
| `before-restore_` | Saved before a restore | Restore | never |
| `before-import_` | Safety copy before a purchases import (listed as Manual) | Purchases import | never |

- **Daily schedule**: 30 seconds after the app starts, a daily backup is made if none exists for today (the counter PC is often off at night, so this is the one that usually runs); and every day at `BackupDailyAt` (default 02:00) while the app is running.
- **Restore** (Settings, with a confirmation): checks the chosen backup is healthy, saves the live database as `before-restore_*.db`, writes the backup into the live database, then says "restart the app". The scheduler makes no backups until the restart. Only files inside the backup folder can be restored or deleted.
- **Before-import copy**: a purchases import makes a `before-import_*` copy first and is refused if that copy fails.
- Backups made before the safe-backup work (1.0 build) were plain file copies in WAL mode and may be incomplete. Keep them, do not rely on them.

### Logs

Serilog writes one file per day, `inventory-<date>.log`, in `<DataFolder>\logs`, kept 30 days (also shown in the console window). Framework chatter is limited to warnings. Screen errors are caught by the `ErrorBoundary` in `MainLayout` and logged. Start-up logs the paths of the database, backups and logs.

### Theme

`Service/AppTheme.cs` and `AppThemeState.cs`: five colours, each in Light and Dark, Inter font, 6 px corner radius; colour codes live in one place (`AppTheme.LightHex`). Chosen on Settings (Appearance: colour and Light / Dark / same as Windows), applied at once, remembered per computer in the browser (`inventoryTheme` in `main.js`, storage key `inventory-theme`). `MainLayout` hands the theme state down. The top-bar Light/Dark switch also still exists and saves the choice. `wwwroot/css/theme.css` turns the Bootstrap buttons into three colours, makes X marks red and sets the focus ring.

---

## 5. How the main flows work

**Purchase entry.** Open Purchases, New (Alt+Shift+P). Pick the supplier and date (a voucher number is given when saved). Add lines in the line editor: product (picker), variant, quantity, rate, discount percent, remarks; Enter in the last box saves the line at once. A line can be edited or deleted. Each line save recalculates the header total. Bulk Entry (Alt+B, after the supplier is chosen) adds many lines in one pop-up. Remarks / narration sit in the totals bar and are saved by Update and by Save Total Amount. Stock goes up as soon as a line is saved.

**Sale by scanner and Summarize.** Open Sale (Alt+S, then New). Pick the customer (a sale needs one). Scan the product barcode then the variant barcode and press Enter: the line is added and the cursor returns to the scan box for the next item. Scan Mass Products (Alt+B) lets you scan many codes in a row and adds them all. Lines can be edited or deleted. **Summarize** saves the sale (the voucher number is the next one for the date), reduces stock, and opens Sales Summary. There the rate (default is the product's rate), per-line discount percent and overall discount are set, amounts are shown to 2 decimals, and Save stores the total. A sale cannot be saved empty. Opening a saved sale again shows the stored values.

**Sales order.** Like a purchase, for a customer: header, lines (line editor), discount, total, status; Bulk Entry works the same. Completed orders can be deleted from the list.

**Purchase order.** One table of rows. Enter a supplier, product, variant, quantity, dates (status defaults to Open), press Enter to add. Edit a row in place (Cancel returns the old values). Tick rows to set their status together, or to delete completed orders. Supplier names show in the list. Bulk Entry (Alt+B) adds many rows, with the supplier chosen once.

**Bulk Entry.** A pop-up on Purchase Orders, Purchases and Sales Orders. The product stays after each row and the cursor goes back to Product. A row is only part of the batch once added with Enter; a row not added is not saved (decided, keep it). Quantity must be a whole number above 0, discount 0 to 100. Esc asks first when rows exist. Save all (Ctrl+S) saves every row in one transaction.

**Scan Mass Products (Sale).** Pop-up listing scanned Product ID and Variant ID pairs; Submit adds them as sale lines. Codes that are not found are dropped without a message (known, section 11).

**Reports.** Reports home has tabs (Customer, Supplier, Products, Purchases Orders, Sales Orders, Sales & Purchases). Sums are computed in SQL. Periods come from `ReportPeriods`. The stock figure uses the shared stock query.

**Import** (`Service/Import/`, pop-up `MudBlazorComponents/ImportDialog.razor`; "Import" button on the Customers, Suppliers, Products and Purchases lists; each list reloads after an import):

1. Choose a `.csv` or `.xlsx` file (5 MB at most, 5,000 rows at most; CSV is read as UTF-8 with a fall-back to Latin-1; the CSV reader is the app's own code; Excel via ClosedXML).
2. The app shows a preview: every problem is listed with its row and column; problem rows are shown first (at most 200 preview rows are displayed).
3. If there is no problem, Import saves everything in one transaction. After a click the database is checked again before writing.
4. The pop-up cannot be closed with Esc while importing.

Files and column order come only from `ImportTemplates.cs`. The sample for each kind is downloadable from `/import-sample/customers|suppliers|products|purchases`.

*Rules for all four kinds:*
- Add only. It never updates existing records; an ID that already exists is a problem.
- All or nothing: one bad row means nothing is imported. Importing the same file twice creates nothing the second time and says why.
- Headings must match the sample: any order, upper/lower case and spaces ignored; an unknown, repeated or missing heading stops the import; a column with data but no heading is a problem.
- IDs are unique ignoring case, at most 50 characters.

*Customers and Suppliers:* ID and Name required; Contact Person, Address, Area, Remarks optional. Mobiles separated by `;`, at most 3 (the `CustomerNumberMobile` / `SupplierNumberMobile` settings), digits, spaces and `-` with an optional leading `+`, 6 to 15 digits, no number repeated.

*Products:* one row per variant, with the product details repeated and identical on every row. Product ID and Product Name required. Rate optional (empty = 0; digits and a decimal point only, not negative). Description optional. Category ID optional but must already exist. Variant ID and Variant Name go together (a row without a variant is allowed only if that product has just that one row); the same Variant ID twice in one product is a problem. Reorder Level optional whole number, 0 or more (blank = none), only on a row with a Variant ID. Existing products are never changed and variants are never added to an existing product.

*Purchases (mainly for opening stock):* one row per item line: Date, Supplier ID, Product ID, Variant ID, Quantity required; Rate, Discount %, Remarks optional. Rows with the same Date and Supplier ID make one purchase. Date is day-first (01-04-2026; `/` and yyyy-MM-dd also accepted). Supplier, product and variant must exist. Quantity is a whole number above 0. Empty Rate = 0. Discount % is a whole number 0 to 100 (empty = 0). Remarks must be the same on every row of a purchase. The same product and variant twice in one purchase is a problem (add the quantities on one row). Double-import guard: another purchase from the same supplier on the same date is allowed; only an exact copy (same supplier, date, items and quantities) is refused. The voucher number is the next one for that date; header discount is 0. A `before-import_*.db` backup is taken first, and the import is refused if that backup fails.

Sales and orders import is not wanted for now.

---

## 6. Keyboard and cursor behaviour

These are easy to break. All handling is in `wwwroot/shortcuts.js`, with the screen list in `Service/Shortcuts.cs`.

**Keys**
- **Alt + letter** opens a screen: H Dashboard, C Customers, U Suppliers, I Products, Q Purchase Orders, P Purchases, O Sale Orders, S Sales, R Reports, G Settings. **Alt+Shift+letter** opens its "new" form (where there is one). **Alt+N** = new record of the screen you are on. Alt+F, Alt+E and Alt+D are avoided on purpose (Chrome's menu and address bar). Ctrl+Alt (AltGr) is ignored so special characters still type.
- **Ctrl+S** clicks the first visible `[data-shortcut="save"]` button (a pop-up's own Save first).
- **Esc** goes back (single record to its list; list to Dashboard; category to Products; nothing on Dashboard and Login), unless a dialog or popover is open.
- **Enter** in a search box (`.lookup-box`) never submits. Enter in a typing box (Quantity, Rate, Discount) in an area marked `enter-moves-on` clicks `[data-enter-submit]`, which **saves the line at once** (owner's choice). Enter on a drop-down moves on.
- **Alt+B** clicks the visible `[data-shortcut="bulk"]` button: Bulk Entry on Purchase Orders, Purchases and Sales Orders (Purchases after a supplier is chosen, Sales Orders after a customer), Scan Mass Products on Sale. Ignored while a pop-up is open.

**Cursor**
- `inventoryShortcuts.focus(selector)` puts the cursor in a box, retries while the screen loads, re-focuses for about 1 second if the cursor is dropped (Blazor/MudBlazor redraws boxes, or a dialog's focus trap takes it), and only the latest call wins.
- Search boxes (`LookupAutocomplete`): `Class` and `EnterPressed` decide where the cursor goes after a pick. The pick-from-list pop-up (`LookupDialog`) puts the cursor in its search bar; Up/Down highlight, Enter or click picks.
- Sale: after the customer is chosen, the cursor goes to the line's Product ID box (`.focus-line-product`), then after a product to the Variant drop-down (`.sale-line-variant`). After a scan the cursor returns to the scan box.
- Bulk Entry: product stays, cursor back to Product after each row.
- Cancel on a line form or a Purchase Orders row edit changes nothing.

**Do-not-touch list** (keyboard and cursor depend on these; for any look-only change, change `class` attributes, CSS and theme only):
- the file `wwwroot/shortcuts.js`;
- ids: `button-submit`, `button-close`, `productid`, `variantid`, `bulk-entry-open`;
- attributes: `data-shortcut` (`save`, `bulk`), `data-enter-submit`;
- classes: `enter-moves-on`, `lookup-box`, anything starting `focus-` (for example `focus-line-product`), `sale-line-variant`, `box-center`;
- `@ref`, `@onclick`, `@bind`, `OnValidSubmit` and any C# in a `.razor.cs` file.

**Always check** a keyboard or cursor change with `tools/ui-check` on a test copy before the owner sees it (section 8). Blind attempts failed four times. The scripts: `check-keyboard-only`, `check-entry-flows`, `check-popups-and-sale`, `check-save-and-focus`, `check-bulk-entry`, `check-alt-b`, `check-import`, and `check-look` (every screen at 1366 and 1920 px, light and dark; fails on sideways scroll, a wrapped product name, or a control sticking out). `check-save-and-focus` prints "saved row visible in list = false": the list is paged and the test record sorts onto a later page; the row is saved, this is not a failure.

---

## 7. Look and feel rules the owner set

- All "X" marks (cancel, delete icons) are red.
- Every button is filled, in **three colours only**: the theme colour for the main action, grey for other actions, red for Delete / Clear. Same on every page.
- Quantity, Rate, Amount, Discount columns are narrow; Product Name takes the spare width.
- Nothing scrolls sideways. Entry rows wrap inside their card instead (`.lineform` with `.lineform-a` and `.lineform-b`). Tables use `.lines-table` with column classes (`c-qty`, `c-rate`, ...).
- Floating labels: a small label sits inside an empty box and moves onto the border when the box is focused or filled (CSS in `tables.css`; `main.js` gives those inputs `placeholder=" "`). Pickers do the same, past the pick button. Drop-downs and file boxes keep the label on the border.
- The theme colour and Light / Dark / same as Windows are chosen on Settings and remembered per computer.
- No line-count footer on tables. Sale scan boxes sit centred in one card with the line entry row (`.sale-entry`). Bulk Entry button lives in the totals bar.
- Pick-from-list pop-ups are wide (960 px) so names are not cut. The screen uses the full width.

---

## 8. Build, run and test on the development PC

- **Test database.** A copy of the real data (May 2026) is at `Inventory\bin\Debug\net10.0\Inventory.db`, copied from `C:\Users\Hiten\Desktop\Inventory App\website (1)\Inventory.db`. **Never write to the Desktop file.** Backups in `bin\Debug\net10.0\Backups`, logs in `bin\Debug\net10.0\logs` (Development uses the build folder, see `appsettings.Development.json`).
- **Run.** `cd Inventory`, then `dotnet run --launch-profile http`, then open http://localhost:5259. Users: Hiten (normal) and Admin. The owner knows the passwords.
- **Never launch with `--artifacts-path`.** In Development the data folder is the output folder, so a different output folder makes a brand-new empty database and login fails (this happened). Plain `dotnet run` only.
- **Stop the app by process id**, the one listening on 5259 (`netstat -ano | findstr 5259`). **Never stop it by name** (`Stop-Process -Name Inventory`): that also kills the owner's second copy running from `H:\New Songs\Test New Version\Inventory.exe` on port 5000.
- **Build while the app is running.** Use a scratch output folder so the running copy is not locked: `dotnet build Inventory.sln -nologo -v q --artifacts-path <scratch>`, and run tests with the same folder: `dotnet test --project Inventory.Tests --no-build --artifacts-path <scratch>`. (Building with `--artifacts-path` is fine; only launching with it is not.)
- **Port clash.** An old 2023 .NET 7 copy at `C:\Users\Hiten\source\repos\Inventory` also uses port 5259. If login "fails" or the page looks wrong, check `netstat` first.
- **Test copy for browser checks (port 5299).** Copy the DB to a scratch folder, set Admin's password to `UiTest#2026` (the app hashes plain text at start-up), start a second copy on port 5299 with `ASPNETCORE_ENVIRONMENT=Development`, `DataFolder` and `BackupFolder` pointing at the scratch folder, then run scripts in `tools/ui-check` with `SCRATCH` set (steps and commands are in `tools/ui-check/README.md`; needs Node 22+ and Chrome). Stop that copy by its process id. These scripts create purchases, sales, orders and a test customer: never aim them at real data. They use names from the May data (supplier BALAJI / CAPITAL, customer AAI MATHA, product LAZER/CUTTING).
- **Migrations.** Stop the app, build, then `dotnet ef migrations add <Name> --no-build`, then check that `Up()` is not empty. Check `dotnet ef migrations has-pending-model-changes` after a rename. If the migration rebuilds Products, Customers, Suppliers or Variants, re-create the NOCASE unique indexes. An older build cannot log in on a database a newer build has migrated.
- **Tests.** 289 automatic tests (xUnit v3; all passing when version 2.0 was concluded). Each test builds a fresh database with the real migrations (`Inventory.Tests/TestDatabase.cs`). The build baseline is 0 errors and 333 warnings.
- **Baseline of the daily PC.** The 1.0 build installed on the counter PC still runs; nothing from 2.0 is installed until section 10.

---

## 9. Rules for every change

1. One change set = one commit, made when the owner says "push". Message starts with `Batch N:` or a plain summary.
2. **Never change what the operator sees or does** unless the batch says so and the owner agreed. Tell him about any small improvement made along the way.
3. Build: 0 errors, no new warnings. Run all tests; all must pass. Add a test for every bug fixed and every shared piece of logic.
4. Database, schema or data-layer changes: test on a **copy** of the real data only. Migration steps are in section 8.
5. If a batch breaks something and the fix is not obvious within an hour, revert it (`git revert`) and write down what happened.
6. Development-only mode: nothing goes to the counter PC until the owner says "ready".
7. Smoke test after each batch (below) on a copy of the real data.
8. The owner sometimes rewinds a chat, and files go back with it. If he says something is "still broken" that was fixed, check on disk that the fix is there first.
9. Explain to the owner in plain English with business impact first, give options and a recommendation, and build after his answer. He tests in the browser at http://localhost:5259 and says "push" when happy. He does not use Git.

**Smoke-test checklist**
- [ ] Login with both users; wrong password rejected; `/customers` while logged out redirects to login.
- [ ] Customers and Suppliers: list, search, open, edit phone, save, reopen; add; delete blocked when they have sales.
- [ ] Products: open, add variant with photo, edit reorder level, delete a variant without transactions.
- [ ] Purchase: new, supplier, two lines, edit one, delete one, save total; reopen; voucher assigned.
- [ ] Sale: customer, scan product + variant + Enter, second line, Summarize, rate and discount, Save; reopen; values right.
- [ ] Stock: sold variant down, purchased variant up.
- [ ] Sales order and purchase order: create, line, total; mark completed; delete completed.
- [ ] Reports: every tab opens; totals change with the period.
- [ ] Settings: create backup; it appears in the list.
- [ ] Keyboard: Alt+S / Alt+P; new purchase by keyboard only; search pop-up arrows + Enter; Esc back; Ctrl+S saves; dark mode readable.
- [ ] After a change to Import: import a sample file twice (the second time must create nothing and say why).

---

## 10. Installing / rolling out to the counter PC

This is the next thing that will happen, and only when the owner says "ready".

**Installed layout.** A self-contained program that starts a web server on `http://localhost:5000` and opens the browser (`Program.cs` does this outside Development). Defaults when `DataFolder` and `BackupFolder` in the installed `appsettings.json` are empty:

- data: `%ProgramData%\InventoryApp\Inventory.db`
- backups: `%ProgramData%\InventoryApp\Backups`
- logs: `%ProgramData%\InventoryApp\logs`
- `DataFolder` = where the database lives (and logs and `initial-admin-password.txt`); `BackupFolder` = where backups go. Relative paths count from the program folder; `%VARIABLES%` are expanded. Other settings: `BackupDailyAt` (default `02:00`), `BackupKeepDaily` (30), `FileMaxSize`, `CustomerNumberMobile`, `SupplierNumberMobile`.
- On the first start of the new version, if the data folder has no database and the program folder has one, the database is **copied** (with SQLite's online backup, checked) to the data folder, and old `.db` backups are copied to the new backup folder. The old file is left untouched, and `Inventory.db-HAS-MOVED.txt` is written beside it.

**Steps**
1. Take a fresh copy of the live `Inventory.db` from the counter PC and keep it off that PC. Run the duplicate-ID, voucher and blank-row checks on it. The new version refuses to start, and logs the duplicate IDs, if any exist.
2. Run the smoke test (section 9) on that copy with the final build.
3. Publish, install on the counter PC, and keep the old program folder for rollback.
4. Steps for the owner:
   - Choose the backup folder (a second drive, USB, NAS or a synced folder) and set `BackupFolder` in the installed `appsettings.json`.
   - After the first start, check the log: the database was copied to `%ProgramData%\InventoryApp\` and `Inventory.db-HAS-MOVED.txt` is beside the old file.
   - Log in as Hiten and as Admin with the old passwords (they are hashed on first start), then change both on **Change Password**. Keep passwords out of Git.
   - Old backups made before the safe-backup work may be incomplete (WAL mode). Keep them; do not rely on them.

---

## 11. Known limits and known small issues

Known, accepted, nothing due. Do not "fix" any of these without asking the owner; each one changes what the operator sees, so each needs his yes first.

**Limits**
- Blazor Server sends every click over a live connection. Fine for one local PC; a phone or tablet in the aisles would need another front end (version 3.0 thinking is in ROADMAP.md).
- The app only speaks plain HTTP. Fine on one local PC; it needs HTTPS if it ever leaves that machine.
- SQLite allows one writer at a time: fine for one PC, limited for many users. Lists show 100 rows per page; paging done by the database itself would only matter once lists reach tens of thousands of rows.
- Stock is calculated, so there is no way to record a reason for a correction (stock adjustments, a "damaged" state) - see ROADMAP.md.
- Variant photo: one JPEG per variant, stored in the database. `VariantModel.Image` still holds the photo record and the product page loads photo bytes on the server.
- The `Mobile` lists on customer and supplier models are kept (phone sync relies on them).
- Import is open to every user until roles exist.
- The build shows 333 hint warnings (mostly nullable). Not a fault; add no new ones.
- Two people editing the same record at the same moment is not detected (the last save wins). One person at one PC is the normal case.
- Some business rules are still in screens rather than the data layer (an optional clean-up).
- Pickers may reload their lists more than needed (mostly fixed; not re-checked).

**Small issues**
- Purchase Orders store free-text supplier, product and variant IDs: no links to the real records and no pickers.
- "Not found" in the Product, Purchase, Sale and Sales Order repositories returns an empty record instead of nothing.
- Some error messages show technical text: delete errors on Supplier and Customer ("GetSupplier error: ...", "Show Full") and some raw database error text in snackbars.
- A failed save or delete on the Purchase Orders and Sales Orders lists is only written to the log; nothing is shown on screen.
- Sale: Summarize with no items silently goes back to the list. Scan Mass Products silently drops codes that are not found and its "Scanned:" message is always blank.
- Line forms (Sale, Purchase, Sales Order) accept quantity 0 or negative and a discount over 100 (Bulk Entry refuses them). Clearing the product box keeps the old product behind it. A very fast double Enter could add a line twice.
- A voucher with a discount but no lines is saved with a total of minus the discount, which lowers supplier and report totals.
- A sale that is never summarised reduces stock but its stored total stays 0, so customer and period totals miss it.
- Report searches (Customers, Sales Orders, Purchase Orders, reorder level, variants) are case-sensitive. The Customers report search matches almost everything because it searches the Mobiles list text. Sorting by Image or Stock In Hand on the product page does not work, and Image may error.
- Reports, Sales & Purchases: "Last 1 / 3 / 6 / 12 months" include one day more than "Last 7 days" implies.
- The browser may open before the app is ready (refresh fixes it). The first-run Admin password is written to the log and to a text file that is never removed. `before-restore_` and `before-import_` copies and stray `.tmp` files are never cleaned up. A non-JPEG variant photo is ignored without a message.
- One old sale (19-05-2026) has a blank product line and cannot be re-saved as a whole because of the "line must be a real product" rule. Known, leave it.
- The Purchase Orders list at 1366 px wide shows cut-off names ("1 NO LED FRA..."): twelve columns cannot all be wide, and a hover title was not added. Sales Order summary headings (TOTAL QUANTITY / DISCOUNT / TOTAL AMOUNT) are still large. At 1366 px the Purchase entry row wraps its number boxes onto a second line by design. The owner's own screen width is unknown.
- Import look notes: long IDs wrap in the preview at 1366 px; the success message can linger onto the next page.
- Not built (by choice): importing updates to existing records, adding variants to an existing product by import, `.xls` files, importing sales or orders.
- Purchases import, minor (found in the final review of 2.0): an absurdly large Rate passes the preview and then fails at save with only "Import failed. Nothing was saved." (nothing is saved, so it is safe); after an import the Purchases list reloads in full even if a search is still typed in the box; the exact-copy guard does not recognise a copy when the earlier purchase holds the same product and variant on two separate lines; a date typed into Excel on a PC set to month-first dates is stored by Excel as the other date and cannot be detected (the CSV sample avoids this).
- Purchase Orders entry row at 1366 px: the last digit of the Due Date is partly hidden behind its clear (X) button, and the "Order Status" label is shortened. Sales Summary: the Save bar floats over the last visible rows of a long list (scroll to see them).

---

## 12. Decisions on record

**Pinned (do not touch until the owner raises them; each has its place in ROADMAP.md, which is the current word on them):**
- **Users and roles.** There are no roles. Plan: a user-management screen (add, disable, reset another user's password, set role), decide what each role may do (starting idea: only Admin may restore or delete backups, delete masters and transactions, do bulk deletes and use Import), enforce it in the services and not only by hiding buttons, and decide whether `/photos/{id}` needs a login.
- **Old duplicate voucher numbers.** 14 days in the May data have duplicate voucher numbers. Options: keep old numbers and exempt them from the unique rule (only new or edited vouchers checked), or renumber (breaks the match with paper bills). Until decided: no unique rule on voucher numbers, no retry, no re-check when only the date changes.
- **Several photos and videos per variant.** Today: one JPEG per variant in the database. Suggested later: a small index table in the database, the files in a media folder next to the database, streaming for video, and the media folder included in backups. Limits to agree with the owner.
- **Selling more than is in stock.** Options: A warn ("Only 3 in stock. Sell 5 anyway?"), B block, C show "In stock" next to Quantity, D nothing. Recommended: A plus C, on Sale only. The owner said "not now".
- **Created / last-changed time stamps** on records. Options: A record only; B record and show a small grey line at the bottom of each form (recommended); C skip. Needs a migration. "Created by / changed by" waits for roles. Question still open.
- **Two people editing the same record** ("someone else changed this, please reload"), and **stock adjustments with a reason** (a movement ledger and a screen: agree reasons, who may adjust, whether stock stays calculated). Both open for the owner's decision; stock movements are the core of ROADMAP.md.
- **Blank product / variant rows in old data:** ignore the old ones, block new ones (decided 12 Nov 2026).

**Declined - do not offer again:**
- The keyboard extras offered on 13 Nov 2026 (the owner declined them) and the Sales Orders "Set ticked to" offer.
- Bulk Entry: a row that is not added with Enter is not saved. Keep as it is.
- Fixing the build warnings. Advice given: not necessary. Keep the rule "no new warnings". Mention only if the owner asks.
- Rebuilding the app on another stack. The first review's verdict was to keep this stack and keep improving.

The Import rules in section 5 (add only, all or nothing, the column lists, the double-import guard) were proposed by the builders; the owner tested Import and accepted version 2.0 with them on 14 Nov 2026. Ask before changing them.

---

## 13. Where to look in git for history

- Repository: https://github.com/HitenH/Inventory2, branch `main`. Each completed batch is a commit with a message starting `Batch N:`; `git log --oneline` reads as the history.
- Tags (`git tag`): `before-ui-refresh` (just before the new look; undo it all with `git checkout before-ui-refresh -- .`), `ui-refresh-round2`, `ui-refresh-round4`, and **`v2.0`** (version 2.0 as concluded on 14 Nov 2026; the version number is also set in `Inventory/Inventory.csproj`).
- The old working files `PLAN.md` (batch plan, session notes, per-step history of the UI refresh, the test lists of past sessions) and `REVIEW.md` (findings with codes such as N7 or B9) were deleted when this README was written; they stay in git history. The **original 10 Nov 2026 review** (health score 4.5 out of 10, full findings) is in commit `13ea0be`, file `REVIEW.md`. The latest PLAN.md and REVIEW.md are in the last commit before this README was added.
