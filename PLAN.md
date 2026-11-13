# Improvement Plan – Inventory App

**Purpose:** working plan for small, safe batches. A new session starts here, then reads `REVIEW.md` "Open items" only if it needs a finding's detail.
**Owner:** Hiten (business owner, not a developer). Plain English, business impact first, no code in explanations. He tests in the browser at http://localhost:5259 and says "push" when happy.
**Last updated:** 13 Nov 2026, session 8 (compressed). Latest commit on `main`: `e075af7`. Build 0 errors / 333 warnings; 150 tests pass.

---

## 0. Start here

### State
- **Pushed and working:** batches 1–7, 9–14, 15 part 1 and 15 part 2 (all tested by Hiten). Table in section 5.
- **Session 8 (`e075af7`), NOT yet tested by Hiten in the browser:**
  - Old CSV/Excel Import removed (buttons, help pop-ups, code, ClosedXML/CsvHelper). Batch 8 = build a new one from scratch.
  - Old user role removed (`Users.Role` column dropped by migration `RemoveUserRole`). Batch 16 starts from a clean slate.
  - Cancel changes nothing on: Sale line edit, Purchase / Sales Order line form, Purchase Orders row edit.
  - Sale summary: amounts, header discount and total rounded to 2 decimals.
  - Purchase Orders: ticks cleared when the list reloads.
  - Variant photo deleted while the variant is open no longer comes back on Save.
  - Reports → By Area search no longer crashes on a blank area.
  - Start-up duplicate-ID log check ignores case.
  - **Alt+B approved by Hiten:** opens Bulk Entry (Purchase Orders, Purchases, Sales Orders) or Scan Mass Products (Sale). Hiten's approval is in; the AI has not run `tools/ui-check/check-alt-b.mjs`.
- **Nothing uncommitted** after `e075af7`. Check `git status` before starting.

### Next steps, in order
1. **Hiten's browser test of session 8** (list below). Fix whatever he reports first.
2. **Batch 15 – data features, one decision at a time** (explain in plain English with options and a recommendation; build only after his answer):
   - a. **Created / last changed time stamps (M13).** Question open since 13 Nov. Options: **A** record only (nothing on screen); **B** record and show a small grey line at the bottom of each form, e.g. "Created 13-11-2026 10:42 · Last changed 14-11-2026 09:15" – **recommended**; **C** skip and pin. Needs a migration. "Created by / changed by" waits for batch 16.
   - b. **Two people editing the same record (RowVersion, D3):** "someone else changed this, please reload".
   - c. **Stock adjustments with a reason (D5):** StockMovement ledger + screen. Agree reasons, who may adjust, and whether stock stays calculated.
   - d. **Discount naming (internal):** `Discount` → `DiscountPercent` (lines) / `DiscountAmount` (headers). Nothing visible.
   - Oversell warning (D4) is **pinned** – see section 0 "Pinned decisions".
3. **Batch 16 – users and roles** (pinned; clean slate). User management screen (add, disable, reset another user's password, set role); decide roles and what each may do (starting proposal: Admin-only = Settings backup/restore/delete, deleting masters and transactions, bulk deletes, the new Import). Enforce in services, not only by hiding buttons. Decide whether `/photos/{id}` needs login (open since batch 14: local PC only, random IDs).
4. **Batch 8 – new Import** (deferred to last by Hiten). Design with him first: parse CSV/XLSX → validate (required fields, duplicate IDs in file and DB, category exists) → preview with errors → import all-or-nothing in one transaction. Mobiles separated by `;`. Acceptance: importing the same file twice creates nothing the second time and says why. Add tests for the validation.
5. **Open review findings N5–N13** (REVIEW.md, "Session 8 findings"). Each changes what the operator sees, so each needs Hiten's yes before it is built. Smaller items to fold in where they fit: B9, B11, B12, S5, S6, D6.
6. **Final rollout** (section 6) only when Hiten says "ready".

**Inserted 13 Nov (session 9): Batch 17 – UI refresh.** Hiten asked for a modern look; mock-up approved in principle (second draft at https://claude.ai/artifact/Fdw5mrPdduQt2W9Dz9NWdx). Plan in section 7. Runs before step 2 if Hiten confirms the second draft.

### Pinned decisions (do not touch until Hiten raises them)
- **Users and roles** → step 3 above.
- **Old duplicate voucher numbers** (14 days with duplicates in the May data). Options: keep old numbers and exempt them from the unique rule (only new/edited vouchers checked), or renumber (breaks the match with paper bills). Until decided: no voucher unique index, no retry, no re-check when only the date changes.
- **Several photos and videos per variant.** Today: one JPEG per variant, in the database, served from `/photos/{id}`. Suggested later: a small index table in the DB, files in a media folder next to the DB, streaming for video, the media folder added to backups. Limits to agree with Hiten.
- **Oversell warn/block on Sale** (D4). Options: A warn ("Only 3 in stock. Sell 5 anyway?"), B block, C show "In stock" next to Quantity, D nothing. Recommended A + C, Sale only. Hiten: "not now".
- **Blank product/variant rows in old data:** ignore the old ones, block new ones (decided 12 Nov; done in batch 5). One known sale of 19-05-2026 uses a blank product: it cannot be re-saved with that line.

### Declined – do not offer again
- Keyboard offers (a)–(e) and the Sales Orders "Set ticked to" offer (13 Nov).
- Bulk Entry: a row not added with Enter is not saved – **keep as it is** (Hiten, session 8).
- Warnings: Hiten asked whether the 333 must be fixed. Advice given: not necessary (hints only). Keep the rule "no new warnings". Only mention if he asks.

### Hiten's test list for session 8
1. Log in as Hiten and as Admin.
2. Customers and Products: no Import buttons; Add and Search in place.
3. Saved sale: pencil on a line, change product and quantity, press X – row unchanged. Repeat and save – new values show.
4. Purchase or sales-order line: edit, type Quantity/Rate/Discount, press X – old numbers stay. Repeat and save – new numbers show.
5. Purchase Orders: start a row edit, change a cell, cancel – old value returns. Tick two orders, add a new order – ticks cleared.
6. Sale summary with a discount (e.g. 333.33 at 15%): amounts 2 decimals (283.33); same total after save and reopen.
7. Alt+B on Purchase Orders, Purchases (after supplier), Sales Orders (after customer) and Sale: Bulk Entry / Scan Mass pop-up opens; Esc closes it.

---

## 1. Context

### The app
- Small-business inventory and sales system. Blazor **Interactive Server** on **.NET 10**, **EF Core 10 + SQLite**, **MudBlazor 9.12** (Bootstrap CSS kept only for page layout), Serilog daily logs in `<DataFolder>\logs`.
- **In daily use** on one counter PC (still the old May 2026 build). Purchases add stock; sales via barcode scanner remove it; reports. Stock is **calculated** (purchased minus sold per variant), never stored.
- Installed app: self-contained exe, web server on `http://localhost:5000`, browser opens. Data in `%ProgramData%\InventoryApp\Inventory.db`, backups in `...\InventoryApp\Backups` (`DataFolder` / `BackupFolder` in `appsettings.json`). Daily automatic backup using SQLite's online backup, integrity-checked.
- Login: users in `Users` table, PBKDF2 hashes (`Authentication/PasswordService.cs`). Every page except Login requires login. Session-storage login per tab.
- Data access: `AddDbContextFactory`; each repository method opens its own short-lived context. Reads `AsNoTracking`.
- Database rules: unique Product/Customer/Supplier ID and Variant ID per product, **ignoring case**; triggers refuse blank master records and lines that do not point to a real product + one of its variants. Messages start with `RULE:` (`Service/DbErrors.cs`).
- Totals: purchase and sales-order header totals recalculated on every line/header save (`Domain/TransactionTotals.cs`); sales orders store their discount.
- Git: `https://github.com/HitenH/Inventory2`, branch `main`.

### Layout (the parts that matter)
```
Inventory/
  Program.cs                  startup: DataPaths, DbContextFactory, migrations + UserBootstrap, backup scheduler
  Domain/AppDbContext.cs      tables, unique indexes; SaveChanges undoes pending changes on failure
  Domain/Repository/*.cs      repositories (+ Abstract/); IDbContextFactory; SimpleRepositoryEF<T> for simple tables
  Domain/TransactionTotals.cs purchase / sales-order totals
  Service/Shortcuts.cs        the one list of Alt keys; Esc/Alt targets; BulkEntry = 'B'
  Service/BulkEntry.cs        Bulk Entry rules; BulkEntryKind PurchaseOrder/Purchase/SalesOrder
  Service/LineAmounts.cs      line maths (rounded to 2 decimals)
  Service/BackupService.cs, BackupScheduler.cs, DataPaths.cs, DbErrors.cs, ReportPeriods.cs, ListPaging.cs, Photos.cs, ColumnSorter.cs
  wwwroot/shortcuts.js        all keyboard and cursor handling in the browser
  Shared/TransactionLineEditor + TransactionLines.cs   ONE line editor for Purchase and Sales Order
  Shared/Selectors/           LookupAutocomplete + LookupSettings = ONE customer/supplier/product picker
  Shared/Variant.razor        variant + photo editor on the Product page
  Shared/DateBox.razor        the one date box
  MudBlazorComponents/        BulkEntryDialog, LookupDialog, ConfirmationDialog + DialogServiceExtensions, ScanMassProducts, ErrorDetailsDialog
  Models/                     view models; ModelMapping.cs = ToModel()/ToEntity(); Clone() on line / sale-variant / PO models
  Migrations/                 latest: RemoveUserRole (20261113120000)
Inventory.Tests/              150 xUnit v3 tests; TestDatabase.cs builds a fresh DB with the real migrations each test
tools/ui-check/               browser checks (hidden Chrome) against a TEST copy on port 5299; README there
```

### Keyboard and cursor (read before touching it)
- `shortcuts.js`: Alt+letter → screen; **Ctrl+S** clicks the first visible `[data-shortcut="save"]` (pop-up first); **Esc** goes back unless a dialog/popover is open; Enter in `.lookup-box` never submits; Enter in a typing box (Quantity, Rate, Discount) clicks `[data-enter-submit]` = **saves the line at once (Hiten's choice)**; Enter on a drop-down moves on; **Alt+B** clicks the visible `[data-shortcut="bulk"]` button (ignored while a pop-up is open).
- `inventoryShortcuts.focus(selector)` puts the cursor in a box, retries while the screen loads, re-focuses for about 1 s if the cursor is dropped (Blazor/MudBlazor redraw boxes or the dialog focus trap takes it), and only the latest call wins.
- Search boxes (`LookupAutocomplete`): `Class` and `EnterPressed` decide where the cursor goes after a pick. Pop-up `LookupDialog`: cursor in its search bar, Up/Down highlight, Enter or click picks.
- Bulk Entry: product stays, cursor back to Product after each row; Esc asks first when rows exist; Save all saves every row in one transaction.
- Verify any keyboard or cursor change with `tools/ui-check` on a test copy first. Blind attempts failed four times in session 6.

---

## 2. Rules for every change
1. One change set = one commit when Hiten says "push". Message starts with `Batch N:` or a plain summary.
2. **Never change what the operator sees or does** unless the batch says so and Hiten agreed. Tell him about any small improvement you made along the way.
3. Build: 0 errors, no new warnings (baseline 333). Run the tests (all must pass). Add a test for every bug fixed and for every shared piece of logic.
4. Database, schema or data-layer changes: test on a **copy** of the real data only. Migrations: stop the app → build → `dotnet ef migrations add <Name> --no-build` → check `Up()` is not empty. If a migration rebuilds Products/Customers/Suppliers/Variants, re-create the NOCASE unique indexes.
5. If a batch breaks something and the fix is not obvious within an hour, revert it (`git revert`) and write what happened.
6. Development-only mode (Hiten, 12 Nov): nothing goes to the counter PC until he says "ready".
7. Smoke test after each batch (section 3 checklist) on a copy of the real data.
8. Hiten rewinds chats sometimes – files go back too. If he says something "still broken" that was fixed, grep first that the fix is on disk.

---

## 3. Smoke test checklist
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

---

## 4. Done batches (one line each; details in git history)
| Batch | What | Commit |
|---|---|---|
| 1 | Passwords hashed, seed removed, Change Password page | f033639 |
| 2 | Safe backups (online backup, daily, restore with safety copy), data moved to ProgramData | c67d620 |
| 3 | Login on every page; redirect to login | 2d0e9a6 |
| 4 | No `async void`; Sales Summary input checks; sale needs customer | a106bf7 |
| 5 | Unique IDs ignoring case; blank rows refused; line must match product + variant | 50a1ecf |
| 6 | Short-lived DB contexts; phone sync per record | 50a1ecf |
| 7 | Totals recalculated per save; sales-order discount stored; empty sale refused | see git log |
| 9 | .NET 10, EF Core 10, MudBlazor 9.12, Serilog file logs, dead config removed | pushed |
| 10 | Test project + GitHub Actions | 0f44264 / 2c7bd85 |
| 11 | Dead code removed | pushed |
| 12 | Duplicates merged (line editor, picker, sorter, confirm dialog); AutoMapper removed | pushed |
| 13 | One UI library (MudBlazor only); date and number boxes | pushed |
| 14 | Performance: aggregate totals, 100-row paging, photos via `/photos/{id}`, SQL report sums | pushed |
| 15 part 1 | Keyboard-friendly entry, shortcuts, dark mode, saved names on reopened vouchers | pushed |
| 15 part 2 | "Open" default status, Bulk Entry pop-up, supplier names on PO list, set status of ticked POs | pushed |
| 8 / 16 prep | Session 8 (above) | e075af7 |

**Notes kept from done batches:**
- Backups before batch 2 were file copies in WAL mode and may be incomplete. Do not rely on them.
- Sales-order discount is stored (batch 7 back-fill). Customer and supplier totals are database sums.
- Stock report and stock-in-hand share one query. Keep it that way.
- Carried forward: `VariantModel.Image` still holds the photo record and the product page loads photo bytes on the server. Revisit with the pinned photo decision. `Mobile` lists are kept (phone sync relies on them).

---

## 5. Dev environment (so you do not rediscover it)
- **Test database:** copy of the real data to `Inventory\bin\Debug\net10.0\Inventory.db` from `C:\Users\Hiten\Desktop\Inventory App\website (1)\Inventory.db` (May 2026). **Never write to the Desktop file.** Backups in `bin\Debug\net10.0\Backups`. Logs in `bin\Debug\net10.0\logs`.
- **Run:** `cd Inventory` → `dotnet run --launch-profile http` → http://localhost:5259. Users: **Hiten** (User) and **Admin** (Admin). Hiten knows the passwords.
- **Stop the dev app by process id** (the one listening on 5259: `netstat -ano | findstr 5259`). **Never `Stop-Process -Name Inventory`**: it also kills Hiten's second copy from `H:\New Songs\Test New Version\Inventory.exe` (port 5000).
- **Build without stopping the app:** `dotnet build Inventory.sln -nologo -v q --artifacts-path <scratch>`, and run tests with the same `--artifacts-path` (`dotnet test --project Inventory.Tests --no-build --artifacts-path <scratch>`).
- **Test copy for browser checks:** copy the DB to a scratch folder, set Admin's password (`UiTest#2026`), start a second copy on port 5299 with `ASPNETCORE_ENVIRONMENT=Development`, `DataFolder` and `BackupFolder` set to the scratch folder. Then run scripts in `tools/ui-check` with `SCRATCH` set. Stop that copy by its process id.
- A 2023 .NET 7 copy at `C:\Users\Hiten\source\repos\Inventory` also uses port 5259. If login "fails", check `netstat`.
- Session 8's new build has a migration. An older build cannot log in on a database the new build has opened.

---

## 7. Batch 17 – UI refresh (paint and layout only; behaviour untouched)

**Hiten's rules (13 Nov):** all "X" marks red; every button filled, three colours only (theme colour = main action, grey = other actions, red = Delete/Clear), uniform on every page; Quantity/Rate/Amount/Discount columns narrow, Product Name gets the spare width; nothing scrolls sideways (entry row wraps inside its card instead); theme colour + Light/Dark/"same as Windows" chosen on Settings, remembered per computer; no line-count footer; Sale scan boxes grouped with the line-entry card, **centred**; the active field/cell/button gets a 2 px border in the text colour (black in light mode, white in dark). **Nothing else changes.** Hiten said "Go" on 13 Nov (third draft).

**Revert point:** tag `before-ui-refresh` (= the commit made just before step 1 below). Undo everything with `git checkout before-ui-refresh -- .` (or `git revert` of the batch commits). Each step is its own commit so a single step can be undone.

**Do-not-touch list for every agent** (keyboard and cursor depend on them): `wwwroot/shortcuts.js`; ids `button-submit`, `button-close`, `productid`, `variantid`, `bulk-entry-open`; attributes `data-shortcut`, `data-enter-submit`; classes `enter-moves-on`, `lookup-box`, `focus-*`, `sale-line-variant`, `box-center`; `@ref`, `@onclick`, `@bind`, `OnValidSubmit` and any C# in `.razor.cs`. Change only `class` attributes for layout, CSS, and the theme.

| Step | What | Who | Check |
|---|---|---|---|
| 0 | Commit PLAN/REVIEW, tag `before-ui-refresh` | Fable (me) | `git tag` shows it |
| 1 | Theme: `Service/AppTheme.cs` (MudTheme palettes ×5 colours, light+dark, Inter font, 6 px radius); Settings "Appearance" card; choice saved in browser localStorage via `main.js`; MainLayout applies it. `site.css`: Bootstrap `.btn-*` mapped to the 3 button colours via CSS variables from the theme; red X rule. | Haiku agent | build 0 errors, 150 tests pass, screenshots light+dark |
| 2 | Tables: shared `.lines-table` CSS (narrow number columns, name `auto` + ellipsis + `title`), applied to Sale, Purchase/Sales Order line editor, Bulk Entry, Purchase Orders rows. Products/Customers/Suppliers lists: toolbar + search in one bar. Class attributes and CSS only. | Haiku agent | screenshots at 1366 and 1920 px: no horizontal scroll, no wrapped names, no overlap |
| 3 | Entry rows: replace `col-1`/`col-2` with two flex groups (A: actions, Sl., product, variant; B: numbers, remarks) that wrap. Sale: move the scan `EditForm` into the line card (markup move only). | Sonnet agent | `tools/ui-check` keyboard scripts incl. `check-alt-b.mjs`; cursor lands in product box; Enter saves line; scanner flow |
| 4 | Review every diff against the do-not-touch list; smoke test (section 3) on a test copy; hand to Hiten | Fable (me) | Hiten's browser test |

## 6. Final rollout to the counter PC (only when Hiten says "ready")
1. Take a fresh copy of the live `Inventory.db` from the counter PC and keep it off that PC. Run the duplicate-ID, voucher and blank-row checks on it. The new version refuses to start and logs duplicate IDs if any exist.
2. Run the smoke test (section 3) on that copy with the final build.
3. Publish, install on the counter PC, keep the old program folder for rollback.
4. Hand steps for Hiten:
   - Choose the backup folder (second drive, USB, NAS or synced folder); set `BackupFolder` in the installed `appsettings.json`.
   - After the first start, check the log: the DB is copied to `%ProgramData%\InventoryApp\` and `Inventory.db-HAS-MOVED.txt` is left beside the old file.
   - Log in as Hiten and as Admin with the old passwords (they are hashed on first start), then change both via **Change Password**. Keep them outside Git.
   - Old backups made before batch 2 may be incomplete (WAL). Keep them; do not rely on them.
