# Improvement Plan – Inventory App

**Purpose:** This file is the working plan for improving the Inventory app in small, safe batches. It is written so that a new chat session (or a new developer) can pick up the next batch with no other context. The full findings are in `REVIEW.md`; this file is the short version plus the exact tasks.

**Owner:** Hiten (business owner, not a developer). Explain changes in plain English. Ask before anything that changes how the app behaves for the operator.

**Last updated:** 12 November 2026 (end of session 3 – batches 7, 9, 10, 11 done; next chat starts batch 12)

---

## 0. START HERE – where we stopped (handover for the next chat)

**Read this section, then sections 1–3, then the batch you are about to do.** Hiten wants short, plain-English updates; he tests in the browser and says "push" when happy.

**State on 12 Nov 2026:**
- **Batches 1–6: done, tested by Hiten, pushed** to GitHub `main` (last pushed commit `50a1ecf` "Batch 5 + 6").
- **Batch 7: done, tested by Hiten (including the discount follow-up), pushed** (commit "Batch 7: ..."; see `git log`).
- **Batch 9: done.** Step 1 (.NET 10, EF Core 10, log files, error box, dead config) done, tested by Hiten, pushed ("Batch 9 step 1"); step 2 (MudBlazor 7.15 → 9.12) done, tested by Hiten, pushed. **Batch 10 (tests): done, pushed (`0f44264`). GitHub Actions workflow pushed (`2c7bd85`) after Hiten re-signed in via browser; first run green.** **Batch 11 (dead code): done, pushed (`259a644`).** **Uncommitted fix (12 Nov, end of session 3): Reports → Customers/Suppliers tabs crashed (error box) when a customer/supplier has no phone number – the Mobiles column now shows blank. Ask Hiten to check it, then include it in the next commit.** **Batch 12 (merge duplicated code, AutoMapper removed): done, tested by Hiten, pushed ("Batch 12 part 1" + "Batch 12 part 2").** **Next: batch 13 (one UI library).** **After 9:** Then 10 … 15, then 16 (users and roles), then **8 (Import) – moved to the very end by Hiten on 12 Nov 2026**, then the one-time rollout (section 6). Until batch 8 is done, Import keeps working as today (batch 5 rules stop duplicate/blank IDs).
- **Development-only mode:** nothing goes to the counter PC until Hiten says everything is ready (rule 8).
- **Pinned for later (do not touch until Hiten raises them):** (1) users and roles → batch 16; (1b) Import → batch 8, deferred to last; (2) old duplicate voucher numbers → see "Pinned decisions" below section 5.

**How the dev environment is set up (so you do not rediscover it):**
- Test database = a copy of the real data: `Inventory\bin\Debug\net10.0\Inventory.db`, copied from `C:\Users\Hiten\Desktop\Inventory App\website (1)\Inventory.db` (the published app folder; data up to May 2026). Never write to the Desktop file; copy it. (Batch 9 moved the build folder from `net9.0` to `net10.0`; the test DB and Backups were copied across. Logs: `bin\Debug\net10.0\logs`.)
- Run: `cd Inventory` then `dotnet run --launch-profile http` → http://localhost:5259. Development keeps data in the build folder and backups in `bin\Debug\net9.0\Backups` (`appsettings.Development.json`). Users: **Hiten** (role User) and **Admin** (role Admin); Hiten knows the passwords.
- **Stop the running app before building.** Otherwise the exe is locked, and `dotnet ef migrations add` silently produces an EMPTY migration from the old build. Order: stop app → `dotnet build` → `dotnet ef migrations add <Name> --no-build` → check the generated `Up()` is not empty.
- If login "fails" in the browser, check nothing else owns port 5259 (an old 2023 .NET 7 copy at `C:\Users\Hiten\source\repos\Inventory` uses the same port): `netstat -ano | findstr 5259`.
- **Automatic tests (batch 10):** `dotnet test --project Inventory.Tests` from the repo root (stop the app first – a running app locks the build). Each test gets its own new database built with the real migrations (`Inventory.Tests/TestDatabase.cs`). `global.json` switches `dotnet test` to the new Microsoft.Testing.Platform runner (required by .NET 10 SDK with xUnit v3). GitHub Actions (`.github/workflows/build.yml`, Windows) builds and runs them on every push to `main`. **Add a test for every bug fixed from now on.**
- Build baseline: **0 errors, 364 warnings** (after batch 12) – do not add new ones.

---

## 1. Context a new session needs

### The app
- Small-business inventory and sales system. Blazor **Interactive Server** on **.NET 10** (from batch 9), **EF Core 10 + SQLite**, Serilog log files in `<DataFolder>\logs`, **MudBlazor 9.12** (plus leftover Blazor.Bootstrap and Bootstrap). Projects: `Inventory/` (app) and `Inventory.Tests/` (xUnit v3, batch 10).
- **In daily production use** on one counter PC (still running the old May 2026 build). The operator records purchases (adds stock), sales via barcode scanning (removes stock), and runs reports. Stock is *calculated* as purchased minus sold per variant, never stored.
- Deployed as a self-contained Windows exe that starts a web server on `http://localhost:5000` and opens the browser. **From batch 2 the installed app keeps its data in `%ProgramData%\InventoryApp\Inventory.db`** (backups in `...\InventoryApp\Backups`; configurable via `DataFolder` / `BackupFolder` in `appsettings.json`). On first start it copies the old `Inventory.db` from the program folder (never deletes it). Automatic daily backup (`BackupScheduler`); backups use SQLite's online backup and are integrity-checked.
- Login: users in the `Users` table, passwords hashed (PBKDF2, `Authentication/PasswordService.cs`); Change Password page. Every page except Home and Login requires login (batch 3). Roles exist but are not enforced (batch 16).
- Data access (batch 6): `AddDbContextFactory`; every repository method opens its own short-lived context. Reads are `AsNoTracking`; updates load the row fresh and copy changes.
- Data rules in the database (batch 5): unique Product/Customer/Supplier ID and Variant ID per product, **ignoring case**; triggers refuse new blank products/variants/customers/suppliers, and lines that do not point to a real product + one of its variants. Trigger messages start with `RULE:` (`Service/DbErrors.cs`).
- Totals (batch 7): purchase and sales-order header totals are recalculated on every line/header save (`Domain/TransactionTotals.cs`); sales orders store their discount.
- Git: `https://github.com/HitenH/Inventory2`, branch `main`.

### Layout
```
Inventory/
  Program.cs                 startup: DataPaths, DbContextFactory, migrations + UserBootstrap at startup, backup scheduler
  Domain/AppDbContext.cs     tables, relationships, unique indexes; SaveChanges undoes pending changes on failure
  Domain/Entities/*.cs       tables
  Domain/Repository/*.cs     one repository per table (+ Abstract/ interfaces); all use IDbContextFactory
  Domain/TransactionLineHydrator.cs   fills product/variant/customer names on transaction lines (kept as a helper)
  Domain/TransactionTotals.cs         recalculates purchase / sales-order totals (batch 7)
  Service/                   BackupService, BackupScheduler, DataPaths (+SqliteBackup), DbErrors, LineAmounts, MobileService, DateOnlyConverter
  Authentication/            CustomAuthenticationProvider (session-storage login), PasswordService, UserBootstrap, UserSession
  Pages/                     screens (.razor + .razor.cs code-behind); Pages/Reports/; Login, ChangePassword, Settings
  Shared/                    line editors, Variant editor, RedirectToLogin, Selectors/ autocompletes, old popups (unused)
  MudBlazorComponents/       dialogs: Confirmation, Customer/Products/Suppliers pickers, Loading, ScanMassProducts
  Models/                    view models
  Migrations/                latest: RemoveSeededUsers, UniqueBusinessIds, CaseInsensitiveIdsNoBlanks, StoreSalesOrderDiscount
  appsettings.json           DataFolder, BackupFolder, BackupDailyAt, BackupKeepDaily, CustomerNumberMobile, SupplierNumberMobile, FileMaxSize, unused ConnectionString
  appsettings.Development.json  DataFolder "." and BackupFolder "Backups" (build folder) for development
```

### Build and run
```
cd Inventory
dotnet build                          # must be 0 errors (547 warnings now; do not add new ones)
dotnet run --launch-profile http      # Development, http://localhost:5259
dotnet ef migrations add <Name> --no-build   # only after stopping the app and building
```
The .NET 10 SDK (10.0.403) is installed on the development PC. `dotnet-ef` tool is 10.0.15 (`dotnet tool restore`).

---

## 2. Rules for every batch

1. **One batch = one Git commit** (plus small follow-up commits if a check fails). Commit message starts with `Batch N:`.
2. **Never change what the operator sees or does** unless the batch says so and Hiten agreed.
3. **Build after every change.** 0 errors. Do not introduce new warnings.
4. **Before any batch that touches the database file, schema or data layer** (batches 2, 5, 6, 7, 9): work only on a copy of the database. In development-only mode that copy is `bin\Debug\net9.0\Inventory.db` (from the Desktop file); the live counter-PC copy is taken at rollout (section 6).
5. **Smoke test after every batch** (section 3) on a copy of the real database, not on the counter PC. Hiten does the browser test; "push" from him = commit + push.
6. **If a batch breaks something and the fix is not obvious within an hour, revert the batch** (`git revert <commit>`), publish the previous version, and write what happened in the status log.
7. **Update section 5 (status log)** at the end of each batch: what was done, what was skipped, anything Hiten must do by hand.
8. **Development-only mode (Hiten's decision, 12 Nov 2026):** do not deploy anything to the counter PC batch by batch. All batches are built and smoke-tested on the development PC against a *copy* of the real database (`Desktop\Inventory App\website (1)\Inventory.db` copied to `Inventory\bin\Debug\net9.0\Inventory.db`). The next batch may start once the previous one passes the smoke test here. The counter PC is updated **once**, when Hiten says everything is ready (see section 6).

---

## 3. Smoke test checklist (run after every batch)

Use a copy of the real database. Tick each item.

- [ ] App starts, browser opens, login page shows. Login works with both users. Wrong password is rejected.
- [ ] Customers: list loads, search works, open one, edit a phone number, save, reopen and confirm. Add a new customer. Delete attempt on a customer with sales is blocked with a message.
- [ ] Suppliers: same as customers.
- [ ] Products: list loads, open one, add a variant with a photo, edit its reorder level, delete a variant that has no transactions.
- [ ] Purchase: create a new purchase, pick supplier, add two lines, edit one line, delete one line, save total amount. Reopen: lines and total correct. Voucher number assigned.
- [ ] Sale: open new sale, pick customer, scan/type a product ID + variant ID + Enter, add a second line via the picker, Summarize, set a rate and discount, Save. Reopen from Sales list: values correct.
- [ ] Stock: Products → the variant just sold shows stock reduced by the quantity; the variant just purchased shows stock increased.
- [ ] Sales order: create, add a line, save totals. Purchase order: add a row, mark completed, delete completed.
- [ ] Reports page: every tab opens without error; Sales & Purchases totals change when the period changes.
- [ ] Settings: create a backup; the file appears in the list.
- [ ] Log out, try to open `/customers` directly: redirected/denied (from batch 3 onward).

---

## 4. Batches

Each batch lists: goal, why, exact tasks, files, acceptance, and risks. REVIEW.md item codes in brackets.

### Batch 1 – Passwords: hash them, remove them from source  [S1]
**Goal:** No plain-text passwords in the database or in the code. Both passwords changed.
**Why:** The two logins are visible to anyone who has ever seen the repository.
**Tasks:**
1. Add a `PasswordHash` handling using `Microsoft.AspNetCore.Identity.PasswordHasher<UserEntity>` (already available in ASP.NET Core; no new package). Keep the existing `Password` column but store the hash in it.
2. Remove the `HasData(...)` seed in `Domain/AppDbContext.cs` (lines ~33-37). Add a migration that removes nothing from the DB (the two user rows must survive) – the migration will only drop the seed definition.
3. On startup (`Program.cs`, after `Migrate()`), run a one-time upgrade: for each user whose `Password` is not yet a hash (does not start with the hasher's format marker, or simply length < 40), hash it in place. This keeps the current passwords working on the first run.
4. `Pages/Login.razor.cs`: replace the `user.Password != loginModel.Password` comparison with `hasher.VerifyHashedPassword(...)`. Show "Invalid username or password" on failure (today it silently clears the field).
5. Add a **Change Password** page (`/changepassword`): current password, new password twice, min 8 characters. Logged-in user changes their own password. Link it in `Shared/NavMenu.razor` under the user's name.
6. Hiten changes both passwords through that page after deployment, and records them outside Git.
**Files:** `Domain/AppDbContext.cs`, `Migrations/` (new), `Program.cs`, `Pages/Login.razor(.cs)`, new `Pages/ChangePassword.razor(.cs)`, `Shared/NavMenu.razor`.
**Acceptance:** Login works with old passwords once, then with new ones after change. `Users` table shows hashes. `grep -r "J031289\|Admin@"` in the source returns only migration history files (which cannot be rewritten without force-pushing; note in log).
**Risks:** Low. If the upgrade step runs twice it must not double-hash (check the marker). Do not force-push history unless Hiten explicitly wants the repository history rewritten.

### Batch 2 – Safe backups and a safe data folder  [Backups, M1]
**Goal:** Backups cannot be corrupt, run automatically, live somewhere else; the database no longer sits in the program folder.
**Tasks:**
1. `Service/BackupService.cs`: replace `File.Copy` with SQLite's online backup: open a `SqliteConnection` to the live DB, open one to the backup path, call `source.BackupDatabase(destination)`. Verify the backup opens (`PRAGMA integrity_check`) before reporting success.
2. Restore: before copying over the live file, call `SqliteConnection.ClearAllPools()`; after restore, show "Restart the application now" and refuse further DB work until restarted. Keep a copy of the pre-restore DB as `Inventory.before-restore-<timestamp>.db`.
3. Data folder: read `DataFolder` and `BackupFolder` from `appsettings.json`. Defaults: `%ProgramData%\InventoryApp\` for the DB and `%ProgramData%\InventoryApp\Backups\` for backups. **One-time migration on startup:** if the DB does not exist at the new path but does exist next to the exe, copy it to the new path (never delete the old one). Log what happened.
4. Add a hosted background service that creates a backup once a day (configurable `BackupDailyAt`, default 02:00, and also on app start if none exists for today). Keep the last 30 daily backups; delete older ones.
5. Settings page: show the backup folder path and the last successful backup time.
6. Hiten: set `BackupFolder` to a second drive, USB, NAS or synced cloud folder on the counter PC.
**Files:** `Service/BackupService.cs`, `Program.cs`, `appsettings.json`, `Pages/Settings.razor`, new `Service/BackupScheduler.cs`.
**Acceptance:** Backup file passes integrity check; daily backup appears without clicking; app started on a PC with the DB in the old location picks it up and keeps running; restore + restart works on a copy.
**Risks:** Medium – touches the data file location. Do step 3 last within the batch and test on a copy of the deployed folder first.

### Batch 3 – Login on every page, Admin-only destructive actions  [S2, S3]
**Tasks:**
1. `_Imports.razor`: add `@using Microsoft.AspNetCore.Authorization` and `@attribute [Authorize]`. Add `@attribute [AllowAnonymous]` to `Pages/Login.razor` and `Pages/Index.razor`. Remove the now-redundant `[Authorize]` attributes from code-behind files (optional, harmless to leave).
2. `Routes.razor`: give `AuthorizeRouteView` a `NotAuthorized` section that redirects to `/login` (so a direct URL sends the operator to login instead of a bare "Not authorized" text).
3. ~~Admin-only actions~~ – **moved to Batch 16** (Hiten, 12 Nov 2026: he is the only user today; roles to be designed after the last batch, when a team is coming).
**Files:** `_Imports.razor`, `Routes.razor`, `Pages/Login.razor`, `Pages/Index.razor`, the pages listed above.
**Acceptance:** Logged out → every page except `/` and `/login` redirects to login. Logged in as User → admin-only buttons hidden/disabled. Logged in as Admin → everything works.
**Risks:** Low. If the "User" role is used by the main operator, over-restricting will block daily work; that is why the list must be confirmed.

### Batch 4 – Crash and small-correctness fixes  [B3, B4, B7, B8]
**Tasks:**
1. Change every `async void` to `async Task` (Blazor handles both): `Pages/Product.razor.cs:144 DeleteProduct`, `Pages/PurchaseOrder.razor.cs:246 EditRowPurchaseOrder`, `Pages/Sale.razor.cs:276 UpdateSalesVariant` and `:304 OpenScanMassDialogAsync`, `Pages/SalesOrders.razor.cs:148 EditOrder`, `Pages/SalesSummary.razor.cs:154 RateChanged` and `:172 DiscountChanged`, `Pages/Supplier.razor.cs:157 DeleteSupplier`, `Shared/PurchaseVariantComponent.razor.cs:195 UpdatePurchaseVariant`, `Shared/SalesOrderVariantComponent.razor.cs:192 UpdateSalesOrderVariant`. Also `Pages/Purchases.razor.cs:644`: change `async void OnAfterRender` to `override async Task OnAfterRenderAsync`.
2. `Pages/SalesSummary.razor.cs`: `RateChanged`/`DiscountChanged` use `decimal.TryParse` / `int.TryParse` with `CultureInfo.InvariantCulture`; on failure keep the previous value and show a snackbar.
3. `Pages/Sale.razor.cs`: `CheckIfClickEnter` becomes `async Task` and `await AddVariant();`.
4. `Pages/Sale.razor.cs AddOrder`: if `customer.Id == Guid.Empty` show "Please select a customer" and return (same as `SalesOrder.razor.cs`).
5. `Pages/Category.razor.cs CreateCategory`: do not navigate away on failure; show the error.
**Acceptance:** Typing "abc" or clearing the rate on Sales Summary shows a message instead of the red "An error has occurred" banner. Enter-key scanning still adds lines. Saving a sale with no customer shows the message.
**Risks:** Very low.

### Batch 5 – Unique rules in the database  [D2, B1]
**Tasks:**
1. **First, check the live data** for duplicates (run against a copy):
   ```sql
   SELECT ProductId, COUNT(*) FROM Products GROUP BY ProductId HAVING COUNT(*)>1;
   SELECT CustomerId, COUNT(*) FROM Customers GROUP BY CustomerId HAVING COUNT(*)>1;
   SELECT SupplierId, COUNT(*) FROM Suppliers GROUP BY SupplierId HAVING COUNT(*)>1;
   SELECT ProductEntityId, VariantId, COUNT(*) FROM Variants GROUP BY ProductEntityId, VariantId HAVING COUNT(*)>1;
   SELECT Date, VoucherId, COUNT(*) FROM Purchases GROUP BY Date, VoucherId HAVING COUNT(*)>1;
   SELECT Date, VoucherId, COUNT(*) FROM Sales GROUP BY Date, VoucherId HAVING COUNT(*)>1;
   SELECT Date, VoucherId, COUNT(*) FROM SalesOrders GROUP BY Date, VoucherId HAVING COUNT(*)>1;
   ```
   If any rows come back, Hiten decides how to merge/rename them before the migration.
2. `Domain/AppDbContext.cs`: add `HasIndex(...).IsUnique()` for `Products.ProductId`, `Customers.CustomerId`, `Suppliers.SupplierId`, `Variants (ProductEntityId, VariantId)`, and `(Date, VoucherId)` on `Purchases`, `Sales`, `SalesOrders`. New migration.
3. Voucher assignment (`Purchase.razor.cs`, `Sale.razor.cs`, `SalesOrder.razor.cs`): on `DbUpdateException` with a unique violation, re-read the last voucher and retry once; then show a message.
4. Edit screens: re-check voucher uniqueness when the **date** changes, not only the number.
5. Catch unique violations on customer/supplier/product/variant save and show "This ID already exists".
**Acceptance:** Migration applies on a copy of the live DB. Creating a second product with an existing ProductId is refused with a clear message. Two browser tabs saving a purchase at the same second get different vouchers.
**Risks:** Medium – migration fails if duplicates exist; hence step 1.

### Batch 6 – Short-lived database contexts  [A2]
**Goal:** Remove the root cause of "second operation on this context" crashes and stale data.
**Tasks:**
1. `Program.cs`: replace `AddDbContext<AppDbContext>` with `AddDbContextFactory<AppDbContext>` (same options). Keep the startup `Migrate()` by creating a context from the factory.
2. Every repository in `Domain/Repository/`: constructor takes `IDbContextFactory<AppDbContext>`; every method does `await using var context = await factory.CreateDbContextAsync();`.
3. `Pages/Customer.razor.cs` and `Pages/Supplier.razor.cs` inject `AppDbContext` directly for `MobileService.DeleteEmptyNumbers(context)`; change `IMobileService` to take the factory (or move the sweep into the repository Update – see batch 7).
4. With short-lived contexts, `GetById` methods can use plain `Include(...)` again; delete `Domain/TransactionLineHydrator.cs` and the `HydrateSaleDetailsAsync` method in `SaleRepositoryEF.cs`. Keep `AsNoTracking()` on read-only list queries.
5. Update methods that relied on long-lived tracking (`context.X.Update(entity)` on entities loaded earlier): load the row inside the method, copy the changed fields, save. `ProductRepositoryEF.Update`, `CustomerRepositoryEF.Update`, `SupplierRepositoryEF.Update`, `VariantRepositoryEF.Update`, `CategoryRepositoryEF.Update`, `ImageRepositoryEF.Update`, and `Shared/Variant.razor.cs` (which adds to `Product.Variants` then calls `ProductRepository.Update(Product)`).
**Acceptance:** Full smoke test passes. Open a product in two tabs, edit in one, refresh the other: new value shows. No "second operation" errors while a purchase page loads with the product picker.
**Risks:** Medium-high: touches every data file. Do it in two commits if needed (factory + repositories first, then hydrator removal and Update rewrites). This is the batch where the smoke test matters most.

### Batch 7 – One transaction per business operation  [D1, B6, B2, B10]
**Tasks:**
1. New `Service/PurchaseService.cs`: `AddLine`, `UpdateLine`, `DeleteLine`, `UpdateHeader`, each in one context/transaction and each **recomputing and saving `TotalAmountProduct`** (sum of lines' `AmountAfterDiscount` minus header `Discount`). Pages call the service; the "Save Total Amount" button stays but becomes a no-op safety (or is removed after Hiten agrees).
2. Same for `SalesOrderService`.
3. `SaleRepositoryEF.Update`: when the incoming line list is empty, delete all existing lines (today it keeps them). Confirm with Hiten whether an empty sale should instead be refused.
4. Customer/Supplier update: compute added/removed phone numbers for that one record and apply them in the same save; delete `MobileService.DeleteEmptyNumbers` (global sweep).
5. Supplier totals and Sales vs Purchases report: compute from lines, not from stored header totals (or trust the now-always-updated header).
**Acceptance:** Delete a purchase line → supplier total on Suppliers page changes immediately without pressing Save Total. Edit a sale, remove all lines, save → stock restored. Kill the app mid-save in a test → no half-saved purchase.
**Risks:** Medium. Behaviour change (totals auto-update) must be agreed with Hiten.

### Batch 8 – Import: hide now, fix later  [B5]  (DEFERRED: do last, after batch 16 – Hiten, 12 Nov 2026)
**Quick step (was planned with batch 3):** hide the Import buttons on Customers and Products behind Admin, or remove them until fixed. *Not done: roles were moved to batch 16, so in batch 8 either do the full fix or ask Hiten whether to hide the buttons for everyone meanwhile.* Note: batch 5 rules now refuse duplicate/blank IDs, so a bad import fails per row instead of creating duplicates – the full fix (preview, all-or-nothing, clear errors) is still needed.
**Full fix (its own batch):** new `Service/ImportService.cs`: parse CSV/XLSX into rows → validate (required fields, duplicate IDs in file and in DB, category must exist) → show a preview with error list → import all-or-nothing in one transaction. Use `;` for multiple mobiles. Update the help tooltips to match.
**Acceptance:** Importing the same file twice creates nothing the second time and says why.

### Batch 9 – Supported platform and real logs  [Runtime, Logging]
**Tasks:**
1. `Inventory.csproj`: `net10.0`; EF Core packages 10.x; `Microsoft.EntityFrameworkCore.Design/Tools` 10.x; MudBlazor 8.x (note Nov 2026: latest is 9.x – decide 8 vs 9 in step 2) (check breaking changes list; `MudDialogInstance` → `IMudDialogInstance`, `DialogService.Show` → `ShowAsync`, etc.); AutoMapper 12 → keep or move to 13 (licence change in 15+; stay ≤14). `dotnet-tools.json` dotnet-ef 10.x.
2. Add Serilog (`Serilog.AspNetCore`, `Serilog.Sinks.File`): rolling daily file in `<DataFolder>\logs\`, keep 30 days. Replace `Logger.LogError("..." + ex.Message)` with `Logger.LogError(ex, "...")` everywhere (grep `ex.Message)`).
3. Remove dead config: `AddAuthentication(...)` block, `AddHttpContextAccessor`, transient `ProtectedSessionStorage`, `ConnectionString` in appsettings, `Error.cshtml(.cs)` and `UseExceptionHandler("/Error")` (replace with Blazor `ErrorBoundary` around `@Body` in `MainLayout.razor`).
4. Publish profiles: update `TargetFramework`.
**Acceptance:** Builds and runs on .NET 10; log file appears; an error on a page shows a friendly boundary instead of killing the layout.
**Risks:** Medium (library upgrades). Do MudBlazor 8 as a separate commit from the .NET upgrade.

### Batch 10 – Tests and CI
**Tasks:** New `Inventory.Tests` project (xUnit). Unit tests: `LineAmounts`, stock calculation (`GetStockInHandByVariantIdsAsync` on in-memory SQLite), voucher assignment, import validation. Integration tests for `PurchaseService`/`SaleService`. GitHub Actions workflow in `.github/workflows/build.yml`: restore, build, test on push.
**Acceptance:** `dotnet test` green; the Actions badge green on `main`.

### Batch 11 – Remove dead code  [REVIEW §6 "Unused"]
Delete, in one commit each group, building between: `Shared/CustomerPopup.*`, `SupplierPopup.*`, `ProductPopup.*`, `ModalWindowShowImage.razor`; `Shared/MainLayout.razor.cs`; Chart.js + datalabels (`App.razor`, `libman.json`, `wwwroot/lib/...`); `SalesOrders.razor.cs` edit-row leftovers; `Purchases.razor.cs SortItem`; demo code in `Pages/Reports/*.razor` (`_events`, `RowClicked`, `RowRightClicked`, `SelectedItemsChanged`, `_sortNameByLength`, "Swedish Farm House" alt text); leftover fields listed in REVIEW §6; `UserRepositoryEF` not-implemented members; `Product.razor.cs GetVariant/variant`; unused model properties. Decide with Hiten whether `/reports/salesorders` (unlinked) should be linked or deleted.
**Acceptance:** Build 0 errors; warning count goes down; smoke test unchanged.

### Batch 12 – Merge duplicated code  [REVIEW §6 "Duplicated", A3, A4]
One commit per merge: `PurchaseVariantComponent` + `SalesOrderVariantComponent` → one line editor; three pickers + three autocompletes → one generic lookup; the nine-branch `SortItem` methods → one helper or MudTable sorting; confirm-dialog boilerplate → `ConfirmAsync` helper; `SalesSummary` maths → use `LineAmounts`; `GetProductStockReportAsync` and `GetStockInHandByVariantIdsAsync` → one query. Then flatten view models (remove entity-typed properties). Then collapse trivial repositories.
**Added in batch 9:** remove AutoMapper (35 `Mapper.Map` uses in 22 files) – replace with plain property copies; then delete the `NuGetAuditSuppress` line for GHSA-rvv3-g6hj-g44x in `Inventory.csproj` (the fix needs AutoMapper 15.1.1+, which is paid).
**Risks:** Medium: UI components are being replaced; smoke test every merge.

### Batch 13 – One UI library  [A5, M7]
Replace Blazor.Bootstrap `AutoComplete` → `MudAutocomplete`, `DateInput` → `MudDatePicker`, `NumberInput` → `MudNumericField`, `Modal` (`Shared/ModalWindow.razor`) → `MudDialog`. Remove the `Blazor.Bootstrap` package and its script/css from `App.razor`. Keep Bootstrap CSS only if layout still depends on it; otherwise remove. Cosmetic changes must be shown to Hiten before publishing.

### Batch 14 – Performance for growth  [P1–P5]
Customer totals via aggregate query (copy the supplier pattern) and stop including `Sales` in `CustomerRepositoryEF.GetAll`; paging on all list tables (`MudTable ServerData` or page size); images served from disk via an endpoint instead of base64 in the page; report sums in SQL with correct period labels; single-row variant updates.

### Batch 15 – Data features needing Hiten's decisions  [D3, D4, D5, M13]
`RowVersion` concurrency on editable tables; oversell warning or block on Sale; `StockMovement` ledger + stock-adjustment screen with reason; `CreatedAt/By`, `ModifiedAt/By`; rename `Discount` to `DiscountPercent` (lines) / `DiscountAmount` (headers) and store the sales-order discount. Each needs a short conversation first.

### Batch 16 – Users and roles (PINNED: do after batch 15, before the final rollout)  [S3, S4]
**Why last:** Hiten is the only user today; a team is expected later. Decided 12 Nov 2026.
**Tasks (agree the details with Hiten first):**
1. User management screen (Admin only): add user, disable user, reset another user's password, set role.
2. Decide the roles and what each may do. Starting proposal: Admin-only = Settings (backup/restore/delete backup), Import, deleting customers/suppliers/products/variants, deleting purchases/sales/sales orders, "Delete Selected"/"Delete Completed" on Sales Orders and Purchase Orders. Use `[Authorize(Roles = "Admin")]` on pages and `<AuthorizeView Roles="Admin">` around buttons; enforce in services too, not only by hiding buttons.
3. Record who did what: fill `CreatedBy/ModifiedBy` (batch 15) from the logged-in user.
4. Consider moving to ASP.NET Core Identity (lockout, password rules) if the team grows beyond a few people.
**Acceptance:** A "User" account cannot see or trigger admin-only actions, even by typing the URL; Admin can do everything.

---

## 5. Status log

| Batch | Status | Commit | Date | Notes / things Hiten must do by hand |
|---|---|---|---|---|
| 0 | Done | 13ea0be | 2026-11-11 | Review written (REVIEW.md), five months of uncommitted work pushed. |
| 1 | Done | f033639 | 2026-11-11 | **Hiten: after deploying, log in as each user and change both passwords via the new "Change Password" menu item; store them outside Git.** Done: `PasswordService` (PBKDF2 via ASP.NET `PasswordHasher`), `UserBootstrap` hashes legacy plain-text rows at startup and creates a random-password Admin on an empty DB (password printed to console + `initial-admin-password.txt` next to the DB), seed removed from `AppDbContext` and from the 2025 migration, empty migration `RemoveSeededUsers`, login shows "Invalid user name or password", `UserRepositoryEF` fully implemented, `/changepassword` page. Verified: build 0 errors / 563 warnings (unchanged); startup tested on a fresh DB for both paths. **Not verified:** logging in through the browser UI (no browser in the dev session). Old passwords still appear in the 2025 migration *Designer* snapshot files and in Git history; removing them needs a history rewrite (force-push), which was not done. |
| 2 | Done | c67d620 | 2026-11-12 | **At rollout, Hiten: choose the backup folder (second drive / USB / NAS / cloud) and set `BackupFolder` in `appsettings.json` of the installed app.** Done: new `Service/DataPaths.cs` (DataFolder/BackupFolder from config; installed default `%ProgramData%\InventoryApp` + `\Backups`; Development uses the build folder via `appsettings.Development.json`, so ProgramData on the dev PC is never touched); one-time copy of `Inventory.db` from the program folder using the SQLite backup API, integrity-checked, old backups copied, `Inventory.db-HAS-MOVED.txt` left next to the old file (old file never deleted); `BackupService` rewritten (SQLite online backup → `.tmp` → `PRAGMA integrity_check` → rename; restore = integrity check, `before-restore_*.db` safety copy, backup API into the live DB, `RestartRequired` flag + red bar in `MainLayout`); `BackupScheduler` hosted service (daily backup 30 s after start if none today, and at `BackupDailyAt` 02:00; keeps newest `BackupKeepDaily`=30 `daily_*` files; manual `backup_*` never pruned); Settings shows DB path, backup folder, last backup, type/date/size, and Restore now asks for confirmation. **Found:** the DB is in WAL mode, so the old `File.Copy` backups could miss recent changes – old backups on the counter PC should not be trusted as complete. Verified: build 0 errors, warnings 563 → 555; move simulated (all 16 table counts identical, second start does not move again); 13-check harness (WAL row captured, restore, safety copy, pruning, damaged-file refusal) all pass; Hiten tested backup + restore + restart in the browser. **Seen, pre-existing (batch 3):** refreshing/typing the URL of an `[Authorize]` page logs "No DefaultChallengeScheme found". |
| 3 | Done | 2d0e9a6 | 2026-11-12 | Scope reduced to "login on every page"; role restrictions moved to batch 16 (Hiten). Done: `@attribute [Authorize]` in `_Imports.razor`; `[AllowAnonymous]` on Login and Index; `Routes.razor` NotAuthorized → new `Shared/RedirectToLogin.razor` (keeps `returnUrl`); Login returns to that page (local paths only); `MapRazorComponents(...).AllowAnonymous()` so the plain HTTP request is not challenged – this fixed the "No DefaultChallengeScheme found" error on refresh (login lives in session storage, so the check must happen inside the Blazor circuit). Code-behind `[Authorize]` attributes left in place (harmless). Verified: build 0 errors; all 21 routes return 200 on direct request with 0 challenge errors in the log. Browser redirect test handed to Hiten. |
| 4 | Done | a106bf7 | 2026-11-12 | Done: all `async void` removed (deletes on Product/Supplier, Sale mass-scan, SalesOrders.EditOrder → `async Task`; line "edit" clicks → plain `void`; `Purchases` → `OnAfterRenderAsync`; PurchaseOrder `RowEditCommit` needs `Action<object>`, so a sync wrapper starts an async method that catches and shows errors). Sales Summary rate/discount use `TryParse` (invariant culture); empty = 0; rejects negative rate and discount outside 0–100 with a message, input redrawn with the old value via `@key`. Sale: Enter-scan awaited and wrapped in try/catch; **new:** warning when a scanned product/variant is not found (was silent); Save and Update refuse with "Please select a customer." Product delete failure now shows a snackbar (modal ref was never assigned). Category save stays on the page and shows the error on failure. Verified: build 0 errors, warnings 555 → 549. |
| 5 | Done (partly – vouchers pinned) | 50a1ecf (with 6) | 2026-11-12 | Duplicate check on the May 2026 Desktop copy: **no** duplicate Product/Customer/Supplier/Variant IDs; **duplicate vouchers exist** (Sales: 12 days / 25 sales, e.g. 01-07-2025 V1 ×4; Purchases: 3 days / 6 purchases) – **Hiten: leave them, pinned (see "Pinned decisions")**; blank rows found (1 product + 6 variants with no ID; the blank product + 1 blank variant are used by the sale of 19-05-2026 V1 CASH SALES, 1 line, qty 1) – **pinned**. Done: unique indexes on `Products.ProductId`, `Customers.CustomerId`, `Suppliers.SupplierId`, `Variants(ProductEntityId, VariantId)` (migration `UniqueBusinessIds`; NULLs allowed). **Hiten follow-up:** IDs unique **ignoring upper/lower case**, and **no new blank records** – migration `CaseInsensitiveIdsNoBlanks` recreates the 4 indexes with `COLLATE NOCASE` (same names, so EF's model still matches; **if a future migration rebuilds Products/Customers/Suppliers/Variants, re-create these NOCASE indexes**) and adds triggers: products/customers/suppliers need ID + name; variants need ID + name + product; sale/purchase/sales-order lines must point to a real product and one of *its* variants (insert and when product/variant changes). Old rows untouched. Trigger messages start with `RULE:` and are shown as plain text (`DbErrors.RuleMessage`). Form duplicate checks in the 4 repositories now ignore case and outer spaces; forms trim IDs/names and treat spaces-only as empty. Barcode lookup (`GetByProductId`) is unchanged (exact match). `AppDbContext.SaveChanges*` undoes pending changes when a save fails, so one refused save no longer poisons every later save in the tab. `Service/DbErrors.cs`: friendly "This X ID already exists" message; Customer/Supplier/Product/Variant saves show it and **stay on the page** (Customer/Supplier used to log silently and leave as if saved). Startup: if the DB update fails, the log lists each duplicate ID that must be fixed (data unchanged). **Not done (pinned):** voucher unique index, voucher retry, re-check on date change. Verified: build 0 errors, warnings 549 (unchanged); 21-check harness for the follow-up (case, blanks, line rule, and normal product/sale/purchase saves still work) plus the earlier 14-check harness on copies (migration on real copy, refusal + message, next save works, update reverted, variant rule, blank IDs allowed, failing DB unchanged + duplicate listed). |
| 6 | Done | 50a1ecf (with 5) | 2026-11-12 | Done: `AddDbContextFactory`; every repository method opens its own short-lived context (startup migration uses the factory too). Reads are `AsNoTracking` (product `GetById` uses `AsNoTrackingWithIdentityResolution` to keep the old object shape). Updates load the row fresh and copy changes: **Customer/Supplier** also sync that record's phone numbers (update kept, add new, delete cleared) – the global `MobileService.DeleteEmptyNumbers` sweep and the pages' direct `AppDbContext` injection are gone (this was batch 7 task 4, needed now). **Product.Update** syncs product fields, category, and variants + photos (new variant IDs and photo IDs are written back to the screen objects; a replaced photo's old row is deleted instead of orphaned; only changed rows are written, so P4 is largely fixed); variants are never deleted here. Product.Create attaches the existing category (no duplicate insert). Deletes and bulk deletes load by ID first. Variant.razor clears a deleted photo from the screen object so the next save cannot restore it. **Deviation from plan:** `TransactionLineHydrator` kept (now just a helper on the short-lived context) instead of replacing with `Include`, so sale/purchase/sales-order screens get exactly the same object shapes. Verified: build 0 errors, warnings 549 → 547; 32-check harness on a fresh copy of the real data (customer/supplier edit with phones, variant add/edit/photo replace/photo delete/duplicate refusal/delete, product create with category, real product edit, stale data across tabs, 5 parallel loads, sales create/edit/bulk delete, purchase/PO/sales-order edits and bulk deletes, master deletes and the delete block). |
| 7 | Done | see `git log` "Batch 7" | 2026-11-12 | **Hiten decided:** totals auto-update, "Save Total Amount" button kept; removing every item from a sale is refused ("A sale must have at least one item. To cancel the whole sale, use Delete."). Done: `Domain/TransactionTotals.cs`; purchase and sales-order **line** add/edit/delete save the line and the recalculated header total (sum of lines' AmountAfterDiscount − header discount) in one transaction; header saves (`PurchaseRepository.Update`, `SalesOrderRepository.Update`) recalculate too. **Sales orders now store their discount** (new column `SalesOrders.Discount`, migration `StoreSalesOrderDiscount` back-fills the value the screen showed: lines − total when total > 0, else 0); the screen used to work it out backwards, so adding a line to a saved order made the discount jump – fixed. Sale: page and `SaleRepositoryEF.Update` refuse emptying a sale (null list = items unchanged); a failed sale edit now stays on the page with a message (used to navigate away). **Bug found and fixed:** `SalesOrderEntity.Customer` had `= new()`, so saving a new sales order tried to insert an empty customer (blocked by the batch 5 rule → creating sales orders failed in the dev build; before batch 5 it would have silently created blank customers – none exist in the May copy). Data check: no stale purchase totals in the May copy (90 never-saved totals all have lines worth 0), so no historic correction needed; supplier totals and the Sales vs Purchases report can trust stored totals from now on. **Deviation from plan:** no separate PurchaseService/SalesOrderService – the line repositories already were the single place pages call, so the transaction + total lives there (pages unchanged). Task 4 (phones) was done in batch 6. Verified: build 0 errors, warnings 547; 17-check harness on a fresh copy (old-order discount back-fill, totals after add/edit/delete/discount, supplier total moves immediately, Save Total still consistent, refused line changes nothing, sales-order discount stays fixed when lines are added, empty sale refused, summary save unaffected). **Follow-up after Hiten's browser test:** (a) Purchase and Sales order: the voucher discount box only lived on screen, so adding/editing a line or pressing Update reloaded the voucher and reset it to the last saved value – it is now saved as soon as it is typed (`UpdateDiscount` in both repositories, same transaction + total recalculation; the page waits for that save before reloading; Update also sends the on-screen discount). Header `Update` and `UpdateDiscount` now open the write transaction before reading, so a discount and a line saved at the same moment cannot produce a wrong total. (b) Sale summary: "Back To Scanning Area" was a plain link that discarded typed rates, line discounts, final discount and remarks – it now saves the summary first (scanned items untouched: `SalesVariants = null`); Save on failure now stays on the page instead of leaving. Verified: build 0 errors / 547 warnings; 10-check harness on a fresh copy (discount saved, kept after adding a line, 20 simultaneous discount+line saves give the right total, sales order same, sale summary kept with items unchanged). |
| 8 | Deferred to last (Hiten) | | | Do after batch 16, before the rollout. Hiten still to choose: full fix with preview (A) or hide the buttons (B). |
| 9 | Done (both steps tested by Hiten, pushed) | | 2026-11-12 | Step 1: `net10.0`; EF Core Sqlite/Design/Tools 10.0.15 and `dotnet-ef` 10.0.15 (chose .NET 10 LTS, supported to Nov 2028, over the newer 11.0 which has shorter support); publish profiles updated (they are git-ignored, local only). Serilog (`Serilog.AspNetCore` 10.0.0, `Serilog.Sinks.File` 7.0.0): daily file `<DataFolder>\logs\inventory-YYYYMMDD.log`, 30 kept, still shown in the console; Microsoft/EF chatter limited to warnings; unhandled crash logged. 24 `LogError("..." + ex.Message)` → `LogError(ex, "...")`. Removed: cookie `AddAuthentication` block + `UseAuthentication`, `AddHttpContextAccessor`, transient `ProtectedSessionStorage`, `ConnectionString` in appsettings, `Pages/Error.cshtml(.cs)` + `UseExceptionHandler("/Error")`. `ErrorBoundary` around `@Body` in `MainLayout.razor` (message + Try again; cleared when another screen opens). **AutoMapper 12 security advisory** (GHSA-rvv3-g6hj-g44x, deep-recursion DoS; fix only in paid 15.1.1+) newly flagged by the .NET 10 tools: not reachable (maps only our own records, local PC) → suppressed in csproj; AutoMapper removal added to batch 12. Verified: build 0 errors / 547 warnings; EF reports no pending model changes; all pages respond; log file written; 10-check data harness passes on .NET 10 against a fresh copy of the May data (all migrations applied). **Step 2:** MudBlazor 7.15.0 → 9.12.0 (went straight to 9: it is current and 8 is no longer the supported line; read both migration guides, issues #9953 and #12666). Changes: `MudDialogInstance` → `IMudDialogInstance` (7 dialogs); `DialogService.Show` → `ShowAsync` (2 image popups); `WatchSystemPreference` → `WatchSystemDarkModeAsync`; `MudTabs PanelClass` → `TabPanelsClass` (3 report pages); Import buttons (Customers, Products): `ActivatorContent` → `CustomContent` + `OnClick=fileUpload.OpenFilePickerAsync` (v9 no longer opens the picker by itself) and an empty `SelectedTemplate` (v9 would otherwise show a list of chosen file names). Checked, no change needed: no `MudGlobal`, no MudMenu/charts, the only action snackbar already requires interaction. Verified: build 0 errors / 547 warnings, no MudBlazor analyzer warnings; pages and MudBlazor css/js load. **Hiten to check by eye:** buttons, dialogs, drop-downs, date pickers, tabs, dark mode switch, Import button opens the file picker. |
| 10 | Done, pushed (`0f44264`, workflow `2c7bd85`, first Actions run green) | | 2026-11-12 | New `Inventory.Tests` (xUnit v3 4.0.3, net10.0), 32 tests in 5 groups: **Calculations** (line amount/discount rounding, password hash/verify/rules); **Stock and vouchers** (stock = purchased − sold, stock report agrees, deleting a sale restores stock, voucher numbers per day); **Transactions** (batch 7: purchase total follows line add/edit/delete + discount, supplier total moves, header Update keeps discount, 15 simultaneous discount+line saves correct, new sales order saves with discount kept, empty sale refused, summary save keeps items); **Data rules** (batch 5: duplicate Product/Customer/Supplier ID refused ignoring case, variant ID unique per product only, blank product/customer refused with readable message, line with another product's variant refused and nothing half-saved, customer with sales cannot be deleted); **Database** (migrations match the code; backup holds latest data and restore brings it back with a safety copy; damaged backup refused, DB untouched). Checked the tests can fail: removing the discount from the total made 2 tests fail. `global.json` (test runner), `.github/workflows/build.yml` (windows-latest, .NET 10, restore/build Release/test). **Not done:** import validation tests (batch 8 deferred – add them with batch 8); no 'service' classes exist (batch 7 put the logic in repositories), so the integration tests target the repositories. Build: 0 errors, 547 warnings (tests add none). |
| 11 | Done, pushed | | 2026-11-12 | Deleted: `Shared/CustomerPopup`, `SupplierPopup`, `ProductPopup` (.razor + .cs), `ModalWindowShowImage.razor`, `Shared/MainLayout.razor.cs` (menu has its own Logout), Chart.js + datalabels (files, `App.razor` script tags, `libman.json`). Removed: Sales Orders list row-edit leftovers (`EditOrder`, `BackupItem`, `ResetItemToOriginalValues`, commented `RowEditingTemplate`, unused fields and `CustomerRepository`); Purchases `SortItem`; demo code in 7 report grids (`_events`, `RowClicked`, `RowRightClicked`, `SelectedItemsChanged`, `_sortNameByLength`); Product page dead message window (`modalWindowComponenRef` never assigned – update/delete messages never appeared; the delete error snackbar from batch 4 stays), `variant`/`GetVariant`; leftover fields in PurchaseOrder/SalesOrder/SalesPurchases/Variant/Customer reports; unreachable "Custom" period branch (no such option on screen); `CustomerReports` unused loading dialog; empty `SalesSummary.OnAfterRenderAsync`; `Snackbar snackbar` fields; duplicate `@using` lines; `DocumentFormat.OpenXml` using. "Swedish Farm House" photo text → "Photo of variant <ID>". **Improved:** 8 `catch (Exception ex)` blocks that swallowed errors (customer/product/supplier pickers and searches, product import save, area sales report) now log them. **Kept:** `UserRepositoryEF` (fully implemented in batch 1), `Sale.selectedProduct` (used: picker starts empty), `ModalWindow.razor` (used by Customer/Supplier – batch 13). **Moved to batch 12:** unused model properties (`SalesModel.SalesVariants`, `ProductModel.PurchaseVariants`, `VariantModel.PurchaseVariants/SalesVariants`) – AutoMapper maps by name, remove together with AutoMapper. Verified: build 0 errors, warnings 547 → 458; 32 tests pass. **Hiten decided:** Sales Orders report added as a Reports tab (after Purchase Orders; its separate `/reports/salesorders` address removed, like the other report tabs); no "product saved" confirmation needed (left as is). |
| 12 | Done, pushed | "Batch 12 part 1" + "part 2" | 2026-11-12 | **Done (steps 1–7, each tested by Hiten):** (1) `SalesSummary` maths → `LineAmounts`. (2) Stock report and stock-in-hand share one purchased/sold query (`GetPurchasedAndSoldAsync`). (3) `DialogServiceExtensions.ConfirmAsync/ConfirmDeleteAsync` replace 10 copies (Settings too). (4) `Service/ColumnSorter<T>` replaces the nine-branch `SortItem` methods (same click behaviour). (5) **AutoMapper removed:** `Models/ModelMapping.cs` plain copies (same shapes as before, checked side by side with a temporary test), package + `NuGetAuditSuppress` gone, `dotnet list package --vulnerable` clean; unused `SalesModel.SalesVariants`, `ProductModel.PurchaseVariants`, `VariantModel.PurchaseVariants/SalesVariants` removed. (6) Purchase + Sales Order line editors → `Shared/TransactionLineEditor` + `TransactionLines` (one small class per voucher type); `LineEditorModel` replaces two models. (7) Customer/Supplier/Product pickers → `LookupAutocomplete` + `LookupDialog` configured by `LookupSettings` (`Lookups.Customer/Supplier/Product`); old wrappers kept so pages are unchanged. **Small improvements (told Hiten):** Sales Order line errors now shown; friendly message when no variant picked; Purchase edit-then-cancel no longer blanks the row; picker search ignores stray spaces and empty fields; Sales Order "Product Name" column now sorts by name (sorted by ID). **Found by Hiten, fixed:** the "x" clear button in every search box was blank since before batch 12 – the Bootstrap Icons font was never loaded; `bootstrap-icons` 1.11.3 now stored in `wwwroot/lib` (libman.json) and linked in `App.razor`. Verified: build 0 errors, warnings 458 → 377; 51 tests pass (19 new: ColumnSorter, ModelMapping, LineEditor, LookupSearch). **Part 2 (step 8, tested by Hiten):** 8a `CustomerModel.Sales/SalesOrders` → `TotalSales` (sum filled by `ToModel`; Customers list + Reports → Customers use it); `SupplierModel.Purchases` removed. 8b `ProductModel.Category` removed, `ProductModel.Variants` is now `List<VariantModel>`; `LineEditorModel.Product` removed (line editor keeps a `ProductModel`; editing an existing line still has no variant drop-down, as before); `SalesVariantModel.Product` is a `ProductModel`, `.Variant` removed; unused `SalesSummaryModel` deleted. **Kept on purpose:** `VariantModel.Image` (photo save is tied to the product save – revisit in batch 14 with P3 'images in DB') and `Mobile` lists on Customer/Supplier (phone sync relies on them). 8c `SimpleRepositoryEF<T>` shared by Category/Image/PurchaseOrder repositories (interfaces unchanged); unused `MobileRepository` deleted. Variant/line/transaction repositories kept (they hold rules and transactions). Verified: build 0 errors, warnings 377 → 364; 54 tests pass (new: customer total, product mapping, SimpleRepository). |
| 13 | Not started | | | Hiten: approve the new look of inputs. |
| 14 | Not started | | | |
| 15 | Not started | | | Needs decisions on each item. |
| 16 | Not started (pinned) | | | Users and roles – after batch 15, before final rollout. Hiten decides roles then. |

**2026-11-12:** Batch 1 login verified in the browser by Hiten on a copy of the real database (users Hiten and Admin; plain-text passwords upgraded to hashes on first start, old passwords still work). Earlier "cannot log in" was an old 2023 .NET 7 copy of the app (`C:\Users\Hiten\source\repos\Inventory`) running on the same port 5259 – check `netstat -ano | findstr 5259` if login fails again. The .NET 9 runtime is now installed on the dev PC, so the roll-forward commands in section 1 are no longer needed.

---

## Pinned decisions (Hiten asked to park these; revisit after batch 15, with batch 16)

1. **Users and roles** – see Batch 16.
2. **Duplicate voucher numbers in old data** (12 Nov 2026). Options discussed: keep old numbers and exempt them (mark as "old duplicate", unique rule only for new/edited vouchers), or renumber extras (breaks match with paper bills). Until decided: no voucher unique index, no retry-on-conflict, no re-check when only the date changes (batch 5 steps 3–4 for vouchers).
3. ~~Blank product/variant rows~~ – **decided 12 Nov 2026: ignore the old ones, block new ones** (done in batch 5, migration `CaseInsensitiveIdsNoBlanks`). For reference: 1 product + 6 variants with no ID/name; the blank product + one blank variant are used by the sale of 19-05-2026, voucher 1, CASH SALES (1 line, qty 1). Note: that one sale can no longer be re-saved with its blank line (the line rule refuses it).

---

## 6. Final rollout to the counter PC (only when Hiten says "ready")

1. Take a fresh copy of the live `Inventory.db` from the counter PC and keep it off that PC.
   Run the batch 5 duplicate-ID queries on it first: the new version refuses to start (and logs which IDs) if a Product/Customer/Supplier ID or a Variant ID within a product is duplicated. Re-run the voucher and blank-row checks too – the counter PC has months more data than the May copy.
2. Run the full smoke test (section 3) on *that* fresh copy with the final build.
3. Publish, install on the counter PC, keep the previous program folder intact for rollback.
4. Hand-steps collected from the status log:
   - Choose the backup folder (second drive / USB / NAS / synced cloud folder) and set `BackupFolder` in the installed `appsettings.json` (batch 2).
   - After the first start, check the log/console: the database is copied from the program folder to `%ProgramData%\InventoryApp\` and a file `Inventory.db-HAS-MOVED.txt` is left next to the old one (batch 2). Old backups made with the pre-batch-2 button may be incomplete (WAL) – keep them but do not rely on them.
   - Log in as **Hiten** and as **Admin** with the old passwords (they are hashed on first start), then change both via **Change Password**; keep them outside Git (batch 1).
   - If the app refuses to start, the log lists duplicate Product/Customer/Supplier/Variant IDs that must be fixed first (batch 5).

**How to start a batch in a new session:** read sections 1–3 of this file, read the batch, run `dotnet build` to confirm the starting point is clean, take the database backup if the batch requires it, then work through the tasks, build, smoke test, commit as `Batch N: ...`, update this table, push.
