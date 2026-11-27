# Inventory App

This is the one document that explains the app as it stands today: version 3.0, built in stages on top of version 2.0. What is installed where, and the plan for what comes next, are in [ROADMAP.md](ROADMAP.md) ("Where we are") and are not repeated here.

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

**How stock works.** The program never stores a "stock" number. For every variant, stock in hand is worked out each time it is needed from all the entries: purchased − sold ± stock changes (damage, repairs, returns, corrections, transfers between places). Stock in hand means the **sellable** pieces; damaged pieces, pieces waiting for approval and pieces on the way between places sit behind a small arrow. Since stage 3 stock is also counted **per place** (Shop, Godown). See section 4, "Stock query".

The app is in daily use on the counter PC. The rule for all work is: improve it, do not break it.

### The screens

- **Dashboard** (home): a welcome line and the list of keyboard shortcuts.
- **Customers** and **Suppliers**: list with search, add, edit, delete (blocked when the customer or supplier already has sales or purchases). Each can have up to three mobile numbers. There is an **Import** button on each list (section 5).
- **Categories**: kept from the Products screen (the Esc key on a category page goes back to Products).
- **Products**: list with search and an **Import** button. The product page holds the product details and its variants (Variant ID, name, reorder level, a JPEG photo, stock). Clicking a variant line opens a strip with **Print labels** and **Where it is kept**.
- **Labels**: print product labels on A4 sheets (also from a purchase or a product). **Where is it?**: find an item's racks and stock per place, see what is on a rack, arrange racks.
- **Purchase Orders**: one editable table. Each row is one order line (supplier, product, variant, quantity, dates, status Open / Completed / Partly). Status can be changed for all ticked rows at once; completed orders can be deleted in one go. Bulk Entry adds many rows at once.
- **Purchases**: list of vouchers and a purchase page: supplier, date, place it arrives in, lines, discount, total, remarks; Record damage, Print labels, Put away, Send to Shop. Has an **Import** button (opening stock).
- **Sales Orders**: same idea as purchases, for customers.
- **Sale**: the counter screen. Pick the customer, scan product and variant barcodes (or use **Scan Mass Products** to scan many in a row), then press **Summarize**. A "From" box (Shop) is skipped by the cursor.
- **Sales Summary**: after Summarize you set the rate and discount per line and an overall discount, then Save. The total is stored here.
- **Sales Returns**, **Purchase Returns**, **Damage report**, **Stock changes**, **Waiting for approval**: stock changes of stage 2 (section 4).
- **Stock Transfer**: goods sent between places, received, missing pieces, items below zero (stage 3).
- **Reports** (tabs): Customer (By Area, Customers), Supplier, Products (Reorder Level Table, Variant Report Table, Variants List Table), Purchases Orders, Sales Orders, Sales & Purchases (totals for a chosen period such as Today, Last 7 days, This month, This financial year starting April).
- **Activity log**: every change, login and backup, for people with the "See activity log" switch.
- **Settings**: Appearance (theme colour and Light/Dark), Users and Roles, Reasons and the approval switch, Places and racks, Label settings, backup list with Backup, Restore and Delete, where the database is, and the backup folder (**Change folder…**).
- **Change Password**: every user changes their own password.
- **Login**: every page except Login needs a login. Each user sees only what their roles allow, and only the stock of their places.

### Import (customers, suppliers, products, purchases)

Import reads a CSV or Excel file, shows a preview with every problem listed, and only then, if the file is clean, saves everything in one go. A sample file for each kind can be downloaded from the import window. Details and rules are in section 5. Since version 3.0 batch 1b-4 the Import buttons only appear for roles with the "Import data" switch (Admin has it).

---

## 2. Version history

- **1.0** - the May 2026 build. It ran on the counter PC until 2.0 replaced it on 16 Nov 2026.
- **2.0** - concluded 14 Nov 2026 (git tag `v2.0`): security (hashed passwords, login on every page), data safety (database rules, one-transaction saves, safe daily backups), .NET 10, automatic tests, clean-up, speed, keyboard entry, a new look with themes, Import, and (16 Nov 2026) choosing the backup folder in the app. Installed on the counter PC on 16 Nov 2026 (tag `live-16-nov-2026`).
- **3.0** - built in stages (ROADMAP.md):
  - **Installed on the counter PC (26 Nov 2026)**, package from commit `f910f58`: stage 1a (line rules, double-Enter guard, "not found" messages), 1b (users, roles, permissions, rate hiding), 1c (record stamps, "someone else changed this", activity log), labels L-1/L-2.
  - **Pushed, not installed:** stage 2 (stock breakdown and damage, last commit `4013c7d`) and stage 3 (locations, last commit `195ec58`). Next: install both together, then stage 1d (shop network).

---

## 3. Technology

Taken from `Inventory/Inventory.csproj` and `global.json`.

| Part | What | Why it is kept |
|---|---|---|
| Framework | .NET 10 (`net10.0`), ASP.NET Core **Blazor Server** (interactive server mode: the pages run on the PC, the browser only shows them over a live connection) | One language and one program for everything. Fine for one local PC. |
| Database access | Entity Framework Core 10.0.15 with SQLite | EF Core is the library that turns C# objects into database rows. SQLite is a single-file database: no database server to install, and a backup is one file. |
| Screen parts | MudBlazor 9.12.0 (the only UI library). Bootstrap CSS files remain only for page layout. | One consistent set of tables, dialogs, buttons. |
| Excel files | ClosedXML 0.105.0 (reads `.xlsx` for Import) | The CSV reader is the app's own code. |
| QR codes | QRCoder 1.6.0 (MIT licence; draws the QR codes on product labels, offline) | Version 3.0, stage L. Fixed version; nothing goes to the internet. |
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
  Domain/Entities/            one class per table (customers, suppliers, products, variants, purchases, sales, orders, users and roles,
                              activity log, label layouts, stock changes, places and racks)
  Domain/Repository/          one repository per area (+ Abstract/ interfaces); SimpleRepositoryEF<T> for simple tables
  Domain/TransactionTotals.cs recalculates purchase and sales-order totals
  Domain/TransactionLineHydrator.cs  fills in names on loaded lines
  Models/                     view models used by the screens; ModelMapping.cs converts model <-> entity (ToModel / ToEntity)
  Pages/                      one .razor (+ .razor.cs) per screen; Pages/Reports/ holds the report tabs
  Shared/                     parts used by several screens (see "one of each" below), MainLayout, NavMenu
  Shared/Selectors/           the picker (LookupAutocomplete, LookupSettings, and thin Customer/Supplier/Product wrappers)
  MudBlazorComponents/        pop-ups (Bulk Entry, Scan Mass Products, pick-from-list, confirmation, import, record damage, racks,
                              put away, send to shop, places per user, activity panel, folder picker, …)
  Service/                    rule books and helpers (line rules, access, stock changes, places, transfers, labels, activity log,
                              backups, shortcuts, report periods, paging, photos, theme, …)
  Service/Import/             the import feature (section 5)
  Migrations/                 database changes, oldest to newest; latest: Moves (stage 3, batch 3-5)
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
- **Stock query**: `Domain/StockQuery.cs`, the one place stock is added up, used through `ProductRepositoryEF` (`GetProductStockReportAsync`, `GetStockByVariantIdsAsync`, `GetPlaceStockAsync`). Nothing is stored. Per variant (`StockFigures`): **sellable** = purchased − sold + approved stock-change pieces moved into Sellable or InTransit − those moved out (pieces on the way between places count as sellable, `OnTheWay` says how many); **damaged** = approved pieces moved into Damaged − those moved out; **waiting** = pieces on entries waiting for approval (not moved yet, so still inside stock in hand). Purchases and sales are never converted (Hiten's "way B"), so without stock changes the figures are exactly version 2.0's. Per place: `SellableByPlaceAsync` (the shop holds whatever is not elsewhere or on the way, so places always add up) and `ForVariantsInPlacesAsync` (only the person's places). An index on (variant, quantity) of the purchase and sale lines keeps it fast. Shown by one component, `Shared/StockFigure.razor`: the sellable number, a small arrow when there is a breakdown (damaged, waiting, on the way, or more than one place), the breakdown only on a click, a place below zero in red. The Variants List report has a Damaged column; the Reorder Level report does not (Hiten). "Stock In Hand" everywhere means the sellable pieces.
- **Shortcuts list**: `Service/Shortcuts.cs`, used by the key handler, the menu hints and the Dashboard table, so they cannot disagree.
- **Import column list**: `Service/Import/ImportTemplates.cs`. The reader, the checks, the sample files and the screen all read it.
- **Line maths**: `Service/LineAmounts.cs` (amount = rate x quantity; amount after discount; all rounded to 2 decimals).
- **Line rules**: `Service/LineRules.cs` (version 3.0, batch 1a-1) - the one place that says what a valid voucher line is: a real product and variant, quantity a whole number that is not zero (negative allowed for now, for returns and damage), discount 0 to 100. Purchase, Sale, Sales Order and Bulk Entry all ask it. A new screen or device must ask it too, never carry its own copy.
- **Access rules**: `Service/Access.cs` with the switch list in `Service/Permissions.cs` (version 3.0, batch 1b-1) - "may this person do this?" and the pair rule. Roles, user-role links (optional end date for temporary cover) and pair rules are in the database; `Authentication/RoleBootstrap.cs` gives every existing user an Admin role the first time. Enforced since batch 1b-4: `Shared/MainLayout.razor` checks every screen against `Permissions.ForPath` (the one list of which switch opens which address; an unlisted screen opens only for Admin, and `ScreenAccessTests` fails if a screen is missing), `Shared/NavMenu.razor` hides what the person cannot open, `MudBlazorComponents/ImportButton.razor` follows "Import data", and login refuses disabled users. `Service/AccessService.cs` reads the person's roles again on every screen change. A new screen must be added to `Permissions.ForPath`. Rates (batch 1b-5): `MainLayout` hands the person's `UserAccess` down to every screen and pop-up; a screen leaves out rates, amounts, discounts and totals unless `SeesPurchaseRates` / `SeesSaleRates` (or `SeesRates(kind)` for a voucher) allows them, and search boxes ignore them too. The Products rate counts as a sale rate. A new screen that shows money must ask the same. Starter roles Purchase, Sales and Reports come from the `StarterRoles` upgrade. Checked by `tools/ui-check/check-rates.mjs` (read-only; needs three test users, see the top of the script). Checked by `tools/ui-check/check-access.mjs` (needs a sales clerk and a disabled user in the test copy; see the top of the script).
- **Users**: `Pages/Users.razor` (`/users`, button on Settings) with the rules in `Service/UserAdmin.cs` (version 3.0, batch 1b-2): add users, set passwords, assign roles with an optional end date, disable/enable. Users are never deleted. You cannot disable yourself, and the last active Admin cannot be disabled or lose the Admin role. `tools/ui-check/check-users.mjs` changes users, so run it only on a fresh test copy.
- **Roles**: `Pages/Roles.razor` (`/roles`, button on Settings) with the rules in `Service/RoleAdmin.cs` (version 3.0, batch 1b-3): add, rename, change switches, delete. The Admin role cannot be changed or deleted; a role anyone holds cannot be deleted. Checked by `tools/ui-check/check-roles.mjs` (adds roles and a user: fresh test copy only).
- **Who and when**: `Domain/RecordStamps.cs` (version 3.0, batch 1c-1) - every save stamps "created by / changed by / when" (`IStamped`: `CreatedAt`, `CreatedBy`, `ChangedAt`, `ChangedBy`) on customers, suppliers, products, categories, purchase orders, purchases, sales orders, sales, users and roles, from the person logged in on that browser tab (`CurrentUser`, filled by `AccessService` on every screen change) and this PC's clock. A change to a part (a line, a variant, a photo, a phone number, a role's switches, a user's roles) stamps the record it belongs to. Saving without a real change stamps nothing; the created stamp is never overwritten. Blank on records made before 3.0; a time without a name means the app itself (start-up steps). Screens never set these. The database factory is per browser tab (scoped) for this reason.
- **Two people, same record**: `Domain/EditClash.cs` with the check inside `RecordStamps` (version 3.0, batch 1c-2). Each browser tab remembers the version ("changed at") of the records its screen opened (`SeenVersions`; the repositories mark them: `GetById` of customer, supplier, product, purchase, sale and sales order, the Purchase Orders table, the Users and Roles lists). At save, a record someone else saved in between refuses the whole save with `RecordChangedException` ("Admin changed this purchase at 15:07 while you had it open. Reload to see their changes."), and `MainLayout` shows that message with a **Reload** button above the screen (cleared by Reload, its X, or another screen). A change to a line counts as a change to its voucher, so a stale voucher screen cannot add lines either. Saving again from the same screen is not a clash (the tab keeps the version it wrote). Saving a record or line someone else deleted says so instead of pretending to save. Users and Roles return the message as a refusal. Checked by `tools/ui-check/check-edit-clash.mjs` (two browsers).
- **Activity log**: table `ActivityLog` (`Domain/Entities/ActivityEntry.cs`), written by `Domain/ActivityLogWriter.cs` inside every save, in the same save as the data (version 3.0, batch 1c-3). Every create, change (each field, before → after) and delete, in plain words, against the record it belongs to (a purchase line is logged on its purchase, a variant or photo on its product, a phone number on its customer, a switch on its role, a role given on its user). A record created in one save gets one "Created" line with a summary (lines, pieces) rather than every line. Lines are named by product code and name and variant code ("AJ0120101 1 NO FRAME (NS-9) / G"; one small lookup per product per save). Owner, 25 Nov 2026: everything added to a new record before leaving its screen counts as creating it (`SeenVersions.MarkCreating`, ended by `MainLayout` on every screen change), so the panel shows one "Created" entry with "N lines added while creating, P pieces" and the lines behind "show lines"; later, all changes one person makes to one record in one sitting (same screen, no pause over 30 minutes; `SeenVersions.SittingFor`) are one entry, shown with its time span ("12:30–12:45"), and more than three lines added in it, one by one or by Bulk Entry, are folded the same way. An empty amount and 0 count as the same (no "Discount: (blank) → 0" lines) (`ActivityEntry.IsLineDetail` / `Pieces`, `ActivityLogService.FoldAbove`). A screen that replaces all lines at once (Sale, Sales Summary) only logs the real differences. Passwords are never written ("Password set"). Lines with a rate, amount or discount carry `RateSide` (purchase or sale). An import writes one line (`AppDbContext.ActivityNote`). Logins (and refused logins, with the typed name), backup restore and delete, and backup-folder changes are written by `Service/ActivityLogService.cs`. A refused save (clash) writes nothing. Reading (batch 1c-4): `Shared/ActivityLogButton.razor` ("See activity log" on Customer, Supplier, Product, Purchase, Sale, Sales Summary and Sales Order; a history icon in the Remarks cell of each Purchase Orders row) opens `MudBlazorComponents/ActivityLogDialog.razor` as a panel on the right, newest first. Needs the switch "See activity log" (`activity.see`, Admin group); rate lines follow the rate switches and are never sent to a person who may not see them; a record without a "Created" line shows "Older history: not recorded (before version 3.0)". Checked by `tools/ui-check/check-activity-log.mjs` (setup: `tools/ui-check/setup-activity-log.py`).
- **Activity log screen**: `Pages/ActivityLog.razor` (`/activity`, menu item "Activity log" shown only with the "See activity log" switch; version 3.0, batch 1c-5). Opens on the last 7 days, newest first; filters From / To, Person, Kind, Search (`ActivityLogService.SearchAsync`); one row per sitting, folded and rate-hidden as in the panel, so a hidden rate cannot be found by searching either; the record name links to the record while it exists and the person may open it. Colours (owner, 25 Nov 2026): the record name is written in a soft colour for its kind – in light mode sale green, sales order olive, purchase orange, purchase order gold, customer and supplier purple, product magenta, users and roles warm grey (no blue or teal on white, owner's eye); in dark mode sale blue, sales order purple, purchase orange, purchase order gold, customer and supplier teal, product green, users and roles plum (`act-rec-*` in `site.css`) and a pale name pill per person (tints in the order users were added, `PersonHuesAsync`; never red; light and dark versions). "Erase whole log" only for the Admin role (`UserAccess.HasAdminRole`): one confirmation with the number of entries, then everything goes straight from the database, leaving no trace. Checked by `tools/ui-check/check-activity-screen.mjs` (it erases the test copy's log at the end).
- **Labels**: `Pages/Labels.razor` (`/labels`, menu under Masters, **Alt+L**) with the rules in `Service/Labels.cs`. Pick product, variant (or all its variants) and copies; Enter moves Product → Variant → Copies, and Enter in Copies adds the line and goes back to Product; **Ctrl+P** presses Print (`data-shortcut="print"`). A sheet fills **from the bottom-right corner backwards**; "Already used on this sheet" skips labels used by earlier prints and is kept in the database per label set-up (table `LabelSheetUse`); pressing Print counts the labels as printed (`Labels.UsedAfterPrint`: 10 + 20 → 30; 30 + 40 → 5 on the next sheet). **Named label set-ups** (table `LabelLayouts`, `Service/LabelLayoutService.cs`, "A4 65 Label" first): sheet, label size, gaps, margins, content position, QR and text sizes; chosen on the Labels page, kept on **Label settings** (`/labelsettings`, switch "Change label settings") with an actual-size preview; numbers that do not fit are refused (`LabelSheetLayout.Problems`); the one in use cannot be deleted. From a saved purchase (**Alt+L** or Print labels: `/labels?purchase=<id>`, one line per purchase line, copies = quantity, minus lines left out) or a product's variant strip (`/labels?product=<id>&variant=<id>`, cursor in Copies); Esc goes back to where Labels was opened from. Every print is in the activity log (kind "Labels"). **The QR holds product ID, Tab, variant ID** (`Labels.QrText`) – the scanner types it into the Sale screen, so never change it. The inside of a label is in `site.css` (`.label-*`; @media print shows only the sheets). This module is for **A4 ready-label sheets**; a sticker printer comes with L-3 (ROADMAP). Switch "Print labels". Checked by `tools/ui-check/check-labels.mjs`.
- **Stock changes – damage and sales returns**: `Service/StockChangeService.cs` (version 3.0, stage 2, batches 2-3/2-4) is the rule book; the plain rules are `StockChangeRules` (damaged pieces of a line between what was already repaired/written off/sent back and what came in; a returned or exchanged quantity above 0; a purchase or return line with damage cannot be deleted, lowered below it or changed to another item; a purchase or return with damage cannot be deleted). **Record damage** (switch "Record damage"): one button in the totals bar of a saved purchase or sales return opens `MudBlazorComponents/RecordDamageDialog.razor`, every line listed with Received/Returned and a Damaged box showing how many are damaged now; raise it (reason per line defaulting to "Reason for all") or lower it when pieces are not damaged after all (owner, 26 Nov 2026; reason `StockReasons.NotDamaged`), a note; Enter moves down, Ctrl+S saves, Esc asks only when something was changed. One Damage stock change of the differences per save (nothing earlier is changed), linked to the purchase (`PurchaseEntityId`, line `PurchaseVariantId`, return supplier = the purchase's supplier) or the return (`SourceStockChangeId`, `SourceLineId`). **Sales Returns** (switch "Open the Sales Returns screen"; menu under Transactions; `/salesreturns`, `/salesreturn/{id}`; no Alt + letter chosen – `Shortcuts.ScreensWithoutKey` gives it Alt+N and Esc): laid out like a purchase, lines through the shared line editor (`SalesReturnLines`: quantities only, `HasRates` false, `ExtraProblem` "more than 0"), pieces go Outside → Sellable. **Given in exchange** (owner, 26 Nov 2026): the Bulk Entry pop-up with `QuantitiesOnly` (no rate, discount or remarks; more than 0) adds lines Sellable → Outside to the same return (`AddExchangeLinesAsync`), listed under "Given in exchange" with an X to remove; Record damage and the line editor see only the returned lines. Activity log: a stock change is logged as "Sales return 3 of 26-11-2026" / "Damage 1 of …"; damage also writes "Damage recorded (…): …" into the purchase's or return's own history; kind "Returns and damage" on the Activity log screen. Approval (batch 2-8): `StockChangeService.NewStatusAsync` decides whether a new stock change counts at once or waits, from the Admin's switch on Settings (`AppSettings` key `StockApproval`, off unless switched on); `NeedsApprovalAgainAsync` sends an approved or rejected entry back to waiting when its lines change while approval is on.
- **Damage report and purchase returns**: `Service/DamagedGoodsService.cs` (version 3.0, stage 2, batches 2-5/2-6). The damage report (`Pages/DamageReport.razor`, `/damage`, switch "Open the Damage report") shows one row per purchase line or returned line still lying in damage: damage lines (approved) grouped by `PurchaseVariantId ?? SourceLineId ?? Id`, net of "not damaged after all", minus what later stock changes took from them (their `SourceLineId` points to a damage line). Each row's Supplier and Status are drop-downs (owner, 26 Nov 2026): Status "Assigned" / "Ready to send" saves at once, "Repaired" / "Written off" take the row's Qty after a confirmation. The From link opens the purchase or sales return with `?from=damage`, so Esc there comes back to the report (`Shortcuts.CameFromDamageReport`). Actions on ticked rows: Assign supplier (`MudBlazorComponents/PickSupplierDialog.razor`; sets `ReturnSupplierEntityId` on the group's damage lines), Ready to send (`ReadyToSend`; needs a supplier; no "Not ready" button – the owner removed it), Repaired (kind Repaired, Damaged → Sellable), Written off (WriteOff, Damaged → Outside), Sent (one PurchaseReturn per supplier, Damaged → Outside; different suppliers refused). A Qty box per row takes fewer pieces than the whole row. "See activity log" sits in the bottom bar like on every screen; it opens one panel with the damage history (`ActivityLogService.ForDamageReportAsync`: activity entries of stock changes labelled Damage, Repair, Write-off or Purchase return, each line led by its record's name). Purchase Returns (`Pages/PurchaseReturns.razor`, `Pages/PurchaseReturn.razor`, switch "Open the Purchase Returns screen"; Alt+N and Esc via `Shortcuts.ScreensWithoutKey`): damaged goods sent back are listed with where they were found (`SourcesAsync`; X puts them back on the report), good stock sent back goes through the line editor (`PurchaseReturnLines`: quantities only, Sellable → Outside). Deleting a purchase return puts its damaged goods back on the report. After a purchase return nothing more is tracked: what comes back is a new purchase (owner).
- **Stock changes screen, reasons list, approval**: `Service/StockEntryService.cs` (version 3.0, stage 2, batches 2-7/2-8). The Stock changes screen (`Pages/StockEntries.razor`, `Pages/StockEntry.razor`, `/stockchanges`, `/stockchange/{id}`, switch "Open the Stock changes screen") makes entries of a type – Damaged in the shop, Count correction (3 = found more, -2 = found fewer), Written off, Opening stock (`StockEntryType`, `MoveOf`) – with a date, a reason from the list (`StockChangeEntity.Reason`, copied onto every line) and lines through the line editor (`StockEntryLines`). The reasons list (`Pages/Reasons.razor`, `/reasons`, from Settings; switch "Change settings and backups") adds, renames, reorders and takes reasons off the list (never deleted). Approval: the Admin's switch on Settings (`SetApprovalAsync`, Admin role only), the "Waiting for approval" list (`Pages/Approvals.razor`, `/approvals`, switch "Approve stock changes"; `WaitingAsync`, `DecideAsync`), and `Shared/ApprovalBanner.razor` on returns and stock change entries.
- **Places and racks** (stage 3): stock is counted per **place** (table `Places`; the upgrade makes 01 Shop and 02 Godown); racks and levels are only signposts ("where to look"), never counted. Every purchase and sale has a `PlaceId`; a stock change has `PlaceId` / `ToPlaceId` and its lines `FromPlaceId` / `ToPlaceId` for their sellable side (`StockPlaces.FillPlaces` gives a line without one its entry's place, inside every save). Purchases, Sales and StockChangeLines carry the place as a plain number column on purpose (a database link would have rebuilt those tables); triggers `TR_*_RealPlace_*` refuse a place that does not exist and `TR_Places_InUse_Delete` a place holding entries (or the shop). **Addresses** (`Service/RackAddress.cs`): place – rack – level, shown `02-03-B`, typed or scanned as `0203B`, level A = the bottom. **Places and racks screen** (`Pages/PlacesAndRacks.razor`, `/places`, from Settings; Esc back to Settings), rules in `Service/PlaceService.cs`; it also holds where purchases arrive and sales leave from (`PlaceSettings`: AppSettings `PurchasesArriveIn` = Godown, `SalesLeaveFrom` = Shop) and the transfer switches. **Address box** (`Shared/AddressBox.razor`, the one of its kind): type/scan or Place / Rack / Level drop-downs, each filling the other. **Racks per variant** (table `VariantRacks`): on the product page a click on a variant line opens a strip with "Print labels" and "Where it is kept"; the latter opens `MudBlazorComponents/VariantRacksDialog.razor`, racks in full words grouped per place (`RackAddress.Describe`); changing needs the switch "Set where items are kept (racks)" (`stock.racks`): one main rack per place (★; the first added becomes main, the next takes over when it is removed), "Same for all variants". Rack changes go into the product's history without stamping the product (no edit clash). Checked by `tools/ui-check/check-places.mjs`.
- **Places per user and where stock goes** (stage 3): table `UserPlaces` (none = every place; the Admin role always every place), set with "Places" on the Users screen (`UserAdmin.SetPlacesAsync`); `UserAccess.Places` / `SeesPlace`; every stock figure and stock report counts only the person's places; the Variant Report splits Stock In Hand per place (and On the way) with a bold Total and has a Place filter. New purchases take the purchase place (`PurchaseRepositoryEF.Create`; imports stay in the shop); the Sale's "From" box (`#sale-from`, `tabindex=-1`, switch `sales.other-place`); every stock change entry has a place (purchase damage: the purchase's place; sales returns, exchanges and repairs: the sales place; good stock sent back: the Purchase Return's "From"; the Stock changes screen: its Place box). Only the Admin changes the place of a saved entry (`PlaceService.ChangePurchasePlaceAsync`, `ChangeSalePlaceAsync`, `ChangeEntryPlaceAsync`; logged "Place: Godown → Shop"). Checked by `tools/ui-check/check-place-flow.mjs`.
- **Stock transfers** (stage 3; called "moves" in the code): `Service/MoveService.cs` is the rule book. A transfer is a stock change of kind `Move` (two steps: Sellable in `PlaceId` → `InTransit`, then received InTransit → Sellable in `ToPlaceId`, `SourceLineId` = the sent line), `MoveAtOnce` (the transit switch off) or `StartingCount` (shop → godown, always one step). AppSettings `MovesInTransit` (on unless "off") and `MoveApproval` (off), Admin only. A new transfer stays on the screen until **Send** (`SendAsync` saves header and lines in one go; `FindItemAsync` reads a scan); "Send to Shop" on a purchase (`MudBlazorComponents/SendToShopDialog.razor`, `SendFromPurchaseAsync`). `ReceiveAsync` (ticks per line and for all; `ReceivedAt` / `ReceivedBy`); afterwards pieces neither received nor written off are missing (`MoveState.Missing`; `MissingCountAsync` feeds the red count in the menu); `WriteOffMissingAsync` makes a WriteOff entry under the stock approval switch. `BelowZeroAsync` lists items below zero per place. Screens `Pages/Moves.razor` (`/transfers`: Pieces missing / On the way / Received, Below zero) and `Pages/Move.razor` (`/transfer/{id}`). Switches `stock.transfer-send`, `stock.transfer-receive` (the screens open with either; `Permissions.StockTransfers` is not a switch) and `stock.transfer-writeoff` (Admin by default). Scan boxes work like the Sale screen's (`@bind-value` plus Enter on key-up). Checked by `tools/ui-check/check-moves.mjs`.
- **Put away, Where is it?, racks clearing themselves** (stage 3): "Put away" on a saved purchase (`MudBlazorComponents/PutAwayDialog.razor`, `PlaceService.PutAwayLinesAsync`, `AddVariantRackByAddressAsync`). **Where is it?** (`Pages/WhereIsIt.razor`, `/whereis`, menu under Masters; viewing needs `screen.where-is`, changing racks `stock.racks`): an item's stock per place and racks; a rack level or whole rack (`RackContentsAsync`), typed, scanned or chosen from lists (Place | Rack | Level), with ✕ to take an item off; with one level shown, items are put on it by scan or by hand (Product → Variant or All variants → Add), "Moving here" takes them off their other racks in that place (`PutOnRackAsync`). **Racks clear themselves** outside the sales place (`Domain/RackCleanup.cs`, run inside every save by `AppDbContext`; it never blocks or undoes the save): a save that takes an item's stock there from above 0 to 0 or below takes it off all its racks there and writes it into the product's history (by the app, no name); racks set before any stock arrived stay; the shop's racks change only by hand. Checked by `tools/ui-check/check-whereis.mjs` and `RackCleanupTests`.
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
- Roles and permissions arrived in version 3.0 stage 1b (see "Access rules" in section 4). The old single role column was removed earlier (migration `RemoveUserRole`).
- `/photos/{id}` and `/import-sample/{kind}` do not need a login (local PC only; photo IDs are random). To revisit in stage 1d, when other PCs connect.

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
- **Choosing the backup folder** (Settings → Change folder…, `MudBlazorComponents/FolderPickerDialog.razor`): lists this PC's drives (USB drives labelled) and folders, can make a new folder or take a pasted path. The app first checks it can write there; a folder holding `Inventory.db` is refused (the database would show up as a backup). It offers to copy the existing backups (the originals stay). The choice is saved in `<DataFolder>\backup-folder.txt` and wins over `BackupFolder` in `appsettings.json`, so it survives program updates; "Use the standard folder" deletes that file. If the folder cannot be reached (USB unplugged), the app still starts, Settings shows a red warning, and the daily backup is retried every hour.
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

**Bulk Entry.** A pop-up on Purchase Orders, Purchases and Sales Orders. The product stays after each row and the cursor goes back to Product. A row is only part of the batch once added with Enter; a row not added is not saved (decided, keep it). Quantity must be a whole number and not zero, discount 0 to 100. Esc asks first when rows exist. Save all (Ctrl+S) saves every row in one transaction.

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
- **Alt + letter** opens a screen: H Dashboard, C Customers, U Suppliers, I Products, L Labels (on a saved purchase: Labels for that purchase), Q Purchase Orders, P Purchases, O Sale Orders, S Sales, R Reports, G Settings. **Alt+Shift+letter** opens its "new" form (where there is one). **Alt+N** = new record of the screen you are on (also on the screens without a letter of their own: Sales Returns, Purchase Returns, Stock changes, Stock Transfer; `Shortcuts.ScreensWithoutKey`). Alt+F, Alt+E and Alt+D are avoided on purpose (Chrome's menu and address bar). Ctrl+Alt (AltGr) is ignored so special characters still type.
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

**Always check** a keyboard or cursor change with `tools/ui-check` on a test copy before the owner sees it (section 8). Blind attempts failed four times. What each check covers is listed in `tools/ui-check/README.md`. `check-save-and-focus` prints "saved row visible in list = false": the list is paged and the test record sorts onto a later page; this is not a failure.

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
- **Stop the app by process id**, the one listening on its port (`netstat -ano | findstr 5259`). **Never stop it by name** (`Stop-Process -Name Inventory`): that also kills the live app running from the `H:` drive on port 5000.
- **Build while the app is running.** Use a scratch output folder so the running copy is not locked: `dotnet build Inventory.sln -nologo -v q --artifacts-path <scratch>`, and run tests with the same folder: `dotnet test --project Inventory.Tests --no-build --artifacts-path <scratch>`. (Building with `--artifacts-path` is fine; only launching with it is not.)
- **Port clash.** An old 2023 .NET 7 copy at `C:\Users\Hiten\source\repos\Inventory` also uses port 5259. If login "fails" or the page looks wrong, check `netstat` first.
- **Full browser suite (once per stage and before any install).** Build first, then `bash tools/ui-check/run-full-suite.sh` (details in `tools/ui-check/README.md`). Nothing else may be built or restarted while it runs – a busy PC makes checks fail on timing. Last run: end of stage 3, 27 Nov 2026 – all 24 checks pass (the rates check after a fix to the test helper, run on its own).
- **Test copy for browser checks (port 5299).** Copy the DB to a scratch folder, set Admin's password to `UiTest#2026` (the app hashes plain text at start-up), start a second copy on port 5299 with `ASPNETCORE_ENVIRONMENT=Development`, `DataFolder` and `BackupFolder` pointing at the scratch folder, then run scripts in `tools/ui-check` with `SCRATCH` set (steps and commands are in `tools/ui-check/README.md`; needs Node 22+ and Chrome). Stop that copy by its process id. These scripts create purchases, sales, orders and a test customer: never aim them at real data. They use names from the May data (supplier BALAJI / CAPITAL, customer AAI MATHA, product LAZER/CUTTING).
- **Migrations.** Stop the app, build, then `dotnet ef migrations add <Name> --no-build`, then check that `Up()` is not empty. Check `dotnet ef migrations has-pending-model-changes` after a rename. If the migration rebuilds Products, Customers, Suppliers or Variants, re-create the NOCASE unique indexes. An older build cannot log in on a database a newer build has migrated.
- **Tests.** 593 automatic tests (xUnit v3; all passing 27 Nov 2026, end of stage 3). Each test builds a fresh database with the real migrations (`Inventory.Tests/TestDatabase.cs`), then empties the activity log and removes the three starter roles unless a test asks to keep them (`new TestDatabase(keepStarterRoles: true)`). `TestDatabase.OpenTab(name)` plays one person in one browser tab (for the clash tests). Build baseline: 0 errors, 328 warnings – add no new ones.
- **The counter PC** runs version 3.0 from commit `f910f58` (stages 1a–1c, L-1, L-2) since 26 Nov 2026. It is also this development PC.

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
9. Explain to the owner in plain English with business impact first, give options and a recommendation, and build after his answer. He tests in the browser on a test copy started for him (or the dev app at http://localhost:5259) and says "push" when happy. He does not use Git.

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

## 10. Installing the app, and how to go back

**Installed layout.** A self-contained program (published for win-x64) that starts a web server on `http://localhost:5000` and opens the browser (`Program.cs` does this outside Development). Defaults when `DataFolder` and `BackupFolder` in the installed `appsettings.json` are empty:

- data: `%ProgramData%\InventoryApp\Inventory.db`; backups: `%ProgramData%\InventoryApp\Backups`; logs: `%ProgramData%\InventoryApp\logs`
- `DataFolder` = where the database lives (and logs and `initial-admin-password.txt`); `BackupFolder` = where backups go unless one is chosen on Settings (saved in `<DataFolder>\backup-folder.txt`, which wins). Relative paths count from the program folder; `%VARIABLES%` are expanded. Other settings: `BackupDailyAt` (default `02:00`), `BackupKeepDaily` (30), `FileMaxSize`, `CustomerNumberMobile`, `SupplierNumberMobile`.
- A newer version upgrades the database on its first start (migrations). Upgrades only add tables and columns; each is tried on a copy of real data first.

**Steps for every install**
1. Run the full browser suite on a test copy with the final build (section 8).
2. Take a fresh copy of the live `Inventory.db` and keep it off the program folder. **This copy is the way back.**
3. Try the new version on another copy of it: the upgrade runs, the app starts, and every item's stock is the same before and after.
4. Publish a self-contained package (with a short "how to install" note and empty data settings, so the installed data folder is used).
5. The owner installs it, keeping the previous program folder; smoke test at the counter (section 9).

**Going back.** Two things go back together: the **program** and the **database**. **An older build cannot open a database a newer build has upgraded** - it refuses to start or the login fails. So go back with the previous program folder **and** the database copy from step 2. Version 2.0 is tagged `live-16-nov-2026`; the counter PC's current 3.0 package was made from commit `f910f58`. Keep both until the new version has run well for a few weeks.

---

## 11. Known limits and known small issues

Known, accepted, nothing due. Do not "fix" any of these without asking the owner; each one changes what the operator sees, so each needs his yes first.

**Limits**
- Blazor Server sends every click over a live connection. Fine for one local PC; a phone or tablet in the aisles would need another front end (version 3.0 thinking is in ROADMAP.md).
- The app only speaks plain HTTP. Fine on one local PC; it needs HTTPS if it ever leaves that machine.
- SQLite allows one writer at a time: fine for one PC, limited for many users. Lists show 100 rows per page; paging done by the database itself would only matter once lists reach tens of thousands of rows.
- Variant photo: one JPEG per variant, stored in the database. `VariantModel.Image` still holds the photo record and the product page loads photo bytes on the server.
- The `Mobile` lists on customer and supplier models are kept (phone sync relies on them).
- The build shows 328 hint warnings (mostly nullable). Not a fault; add no new ones.
- Two people editing the same record: since version 3.0 batch 1c-2 the second save is refused with a Reload message (section 4). The screen's own red "Could not save…" notice appears as well, repeating the same words.
- Some business rules are still in screens rather than the data layer (an optional clean-up).
- Pickers may reload their lists more than needed (mostly fixed; not re-checked).

**Small issues**
- Purchase Orders store free-text supplier, product and variant IDs: no links to the real records and no pickers.
- "Not found" in the Product, Purchase, Sale and Sales Order repositories still returns an empty record instead of nothing. The edit screens now catch it and say "not found" (version 3.0, batch 1a-4), so a blank form no longer appears.
- Some error messages show technical text: delete errors on Supplier and Customer ("GetSupplier error: ...", "Show Full") and some raw database error text in snackbars.
- A failed save or delete on the Purchase Orders and Sales Orders lists is only written to the log; nothing is shown on screen.
- Sale: Summarize with no items silently goes back to the list. Scan Mass Products silently drops codes that are not found and its "Scanned:" message is always blank.
- Clearing the product box keeps the old product behind it.
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

**Pinned (do not touch until the owner raises them; each has its stage in ROADMAP.md section 5):**
- **Old duplicate voucher numbers.** 14 days in the May data have duplicate voucher numbers. Options: keep old numbers and exempt them from the unique rule, or renumber (breaks the match with paper bills). Until decided: no unique rule on voucher numbers. Stage 1d.
- **Several photos and videos per variant.** Today: one JPEG per variant in the database. Suggested later: a small index table, the files in a media folder next to the database (included in backups), streaming for video. Stage 7.
- **Selling more than is in stock.** Options: A warn, B block, C show "In stock" next to Quantity, D nothing. Recommended: A plus C, on Sale only. The owner said "not now"; stage 3 deliberately allows selling with 0 in the shop. Stage 6.
- **Blank product / variant rows in old data:** ignore the old ones, block new ones (decided 12 Nov 2026).
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
