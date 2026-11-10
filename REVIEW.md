# Inventory App – Technical Review

**Reviewed:** 10 November 2026
**Scope:** The working copy in this folder, including the 49 modified files and 2 new files that have not been committed to Git yet. Every source file was read. The project was compiled to confirm it builds (0 errors, 563 warnings). The app was not run against live data.
**Rule followed:** Review only. No project files were changed. This report is the only file created. Compiling wrote to the `bin/` and `obj/` build folders, which Git ignores.

---

## Executive summary (read this if nothing else)

**Health score: 4.5 / 10.** The app works, builds cleanly, and covers the business flow end-to-end. Underneath, it has several problems that can lose money or data: passwords stored in plain text and published in the source code, no database rules preventing duplicate IDs or duplicate voucher numbers, saves that are not protected by transactions, a backup feature that can produce corrupt backups, and import features that are very likely broken. The code also carries a lot of duplicated and dead material that makes every future change slower and riskier.

**Verdict on the technology: keep it and improve it. Do not rebuild.** Blazor + .NET + a SQL database is a sound, mainstream choice for an internal inventory system. The problems are in how the code was written, not in the tools chosen. Fixing them is a few weeks to a few months of focused work; rebuilding would be several months with a real risk of breaking what works today.

**Five things to do first**

1. Remove the plain-text passwords from the code and database, change them, and store hashed passwords instead.
2. Add database rules (unique indexes) so duplicate product IDs, customer IDs, supplier IDs and voucher numbers become impossible.
3. Make the backup feature safe (use the database engine's own backup, store backups off the machine, run it automatically).
4. Fix how the app talks to the database in Blazor (one short-lived connection per operation), which is the root cause of several crashes and workarounds.
5. Commit the uncommitted work to Git today. Five months of changes exist only in this folder.

---

## PART 1 – Understanding the app

### What it does

This is a small-business inventory and sales system. It tracks what you buy from suppliers, what you sell to customers, and how much stock you have left for every product variant. It is built to be used with a barcode scanner at a sales counter: the operator scans a product and variant, presses Enter, and the item is added to the sale.

### Main features

| Area | What the user can do |
|---|---|
| **Customers** | Add, edit, delete, search. Up to 3 mobile numbers each. Import from CSV/Excel. See total sales per customer. |
| **Suppliers** | Same as customers, plus total purchases per supplier. |
| **Products** | Add, edit, delete, search, assign a category. Import from CSV/Excel. |
| **Variants** | Each product has variants (size, colour, etc.) with their own ID, optional photo, reorder level, and calculated stock in hand. |
| **Purchase orders** | A simple list of items you intend to order from suppliers, with status (Open / Completed / Partly). |
| **Purchases** | Record goods received: supplier, voucher number, date, line items with quantity, rate, discount, and header-level discount and total. **This is what adds to stock.** |
| **Sales orders** | Customer orders not yet fulfilled, same structure as purchases. Does not affect stock. |
| **Sales** | The scanning screen. Scan product + variant (or pick from a list, or mass-scan in a dialog), then a summary page where you set rate and discount per product and a header discount, and save. **This is what reduces stock.** |
| **Reports** | Sales by customer area, customer list with totals, supplier list with totals, stock list, items below reorder level, per-product variant stock, purchase order list, sales vs purchases totals for a chosen period. |
| **Settings** | Create a backup, restore a backup, delete a backup. |
| **Login** | Two accounts (one "User", one "Admin") seeded into the database. |

### How stock is calculated

Stock is **not** stored as a number. It is calculated every time as: total quantity purchased minus total quantity sold, per variant. This is a good design choice. It means two people recording sales at the same time cannot corrupt a stored stock counter, and the count is always consistent with the transaction history. The downside is that there is no record of *who* changed stock or *why*, and editing or deleting a past purchase silently rewrites today's stock figure.

### How it is deployed

The app is published as a self-contained Windows program. When started, it launches a small web server on the local machine only (`http://localhost:5000`) and opens the default browser pointing at it. In practice this makes it a **single-computer desktop application that happens to use web technology.** Other computers cannot reach it unless the configuration is changed. The database is a single SQLite file stored in the same folder as the program.

### How the code is organised

One project, `Inventory`, about 7,200 lines of C# and 4,500 lines of Razor (screen markup), plus database migrations. No test project.

| Folder | Purpose | Assessment |
|---|---|---|
| `Pages/` | One screen per file, with a matching `.razor.cs` "code-behind" file holding the logic. | Reasonable, but the code-behind files contain business rules that belong in a service layer. |
| `Shared/` | Reusable pieces: the purchase/sales-order line editors, variant editor, pickers, old popups. | Mixed quality. Several components are unused leftovers. |
| `MudBlazorComponents/` | Dialog boxes (confirm, pick customer/product/supplier, loading, scan). | Fine. |
| `Domain/` | The database context, the tables (Entities), and a Repository class per table that reads and writes it. | The repository pattern is applied mechanically (one per table, mostly identical). There is no layer that expresses business operations like "record a purchase". |
| `Models/` | Screen-side copies of the entities ("view models"). | Many are near-duplicates of the entities and some leak entity types back into screens, defeating the purpose. |
| `Service/` | Backup, phone-number helper, shared line-total maths, a date converter. | Small. The right idea; needs to grow. |
| `Authentication/` | Custom login state kept in browser session storage. | Simple and works, but see security findings. |
| `Migrations/` | Database schema history (4 migrations, Jan 2025). | In sync with the code as far as I could verify by reading. |

### Technologies

| Layer | Technology | Version | Notes |
|---|---|---|---|
| Runtime | .NET | 9.0 | **Out of support since 12 May 2026.** No more security patches. |
| Web/UI framework | Blazor, Interactive Server mode | .NET 9 | UI runs on the server; browser is a thin display connected over a live WebSocket (SignalR). |
| UI component libraries | MudBlazor **and** Blazor.Bootstrap **and** Bootstrap CSS | 7.15 / 1.10 / 5.3 | Three overlapping UI toolkits in one app. |
| Database access | Entity Framework Core | 9.0 | Standard Microsoft data layer. |
| Database | SQLite | file-based | One file, no server to install. Great for one machine; limited for many simultaneous users. |
| Object mapping | AutoMapper | 12 | Copies data between entities and view models. |
| Spreadsheets | ClosedXML, CsvHelper | | Excel and CSV import. |
| Charts | Chart.js | 4.4 | Loaded on every page but never used. |
| Hosting | Self-contained Windows exe, browser auto-opened | | Single machine, HTTP only, localhost only. |

---

## PART 2 – Code review

Each issue has a severity, the file(s), what is wrong, why it matters to the business, and the recommended fix.

Severity scale:
- **Critical** – can lose or expose business data today, or is a serious security hole.
- **High** – wrong numbers, crashes that lose work, or a security gap that should be closed soon.
- **Medium** – will cause pain as the business grows or when the code is changed.
- **Low** – quality and tidiness.

### 1. Architecture – is it well organised?

**Overall:** The skeleton is sensible (screens, components, data layer, models, services). The execution is uneven. The two biggest structural problems are:

**A1 · High · Business logic lives inside screens, not in a service layer.**
Files: `Inventory/Pages/Purchase.razor.cs`, `Inventory/Pages/Sale.razor.cs`, `Inventory/Pages/SalesOrder.razor.cs`, `Inventory/Pages/SalesSummary.razor.cs`, `Inventory/Shared/PurchaseVariantComponent.razor.cs`, `Inventory/Shared/SalesOrderVariantComponent.razor.cs`.
*What's wrong:* The rules for "how a purchase is recorded" (assign voucher number, save header, save lines, recompute totals) are spread across a page, a child component and two repositories, with the page coordinating them step by step.
*Why it matters:* There is no single place that guarantees a purchase is saved completely and correctly. Every bug fix has to be made in several places. It also makes automated testing impossible, because the logic cannot run without a browser.
*Fix:* Introduce application services such as `PurchaseService.RecordPurchase(...)`, `SaleService.RecordSale(...)`, `StockService.GetStock(...)`. Pages call one method; the service owns the transaction and the rules.

**A2 · Critical · The database connection is shared for the whole user session.**
Files: `Inventory/Program.cs` (line 44, `AddDbContext`), every repository in `Inventory/Domain/Repository/`.
*What's wrong:* In Blazor Server, a "scoped" database context lives as long as the user's browser tab is open, and every component on the page shares the same one. Multiple components load data at the same time (for example the product picker, the line editor and the page itself all start loading when a purchase opens). Entity Framework does not allow two operations on one context at once, and it also keeps every record it has ever loaded in memory for the whole session.
*Why it matters:* This produces intermittent crashes ("A second operation was started on this context"), "an entity with the same key is already being tracked" errors, stale data (you edit a product on one screen, another screen still shows the old value), and growing memory use. The file `Inventory/Domain/TransactionLineHydrator.cs` exists specifically to work around "EF tracking conflicts" (its own comment says so). That is treating the symptom.
*Fix:* Register `AddDbContextFactory<AppDbContext>()` and have each service/repository method create a context, do its work, and dispose it. This is Microsoft's documented guidance for Blazor Server. Once done, the hydrator and most `AsNoTracking()` sprinkling can go.

**A3 · Medium · One repository per table, mostly copy-pasted.**
Files: `Inventory/Domain/Repository/*.cs` (14 repositories and 14 interfaces).
*What's wrong:* `ImageRepositoryEF`, `MobileRepositoryEF`, `CategoryRepositoryEF`, `PurchaseOrderRepositoryEF`, `PurchaseVariantRepositoryEF`, `SalesOrderVariantRepositoryEF` are near-identical Create/Read/Update/Delete wrappers. `UserRepositoryEF` throws "not implemented" for three of its six methods.
*Why it matters:* 28 files of boilerplate to maintain, with no business meaning. Changes (like adding transactions) must be repeated 14 times.
*Fix:* Either a single generic repository, or (better) drop repositories and let the services use the context directly. Keep the few query methods that have real logic (stock report, totals).

**A4 · Medium · View models duplicate entities and leak them.**
Files: `Inventory/Models/*.cs`.
*What's wrong:* `CustomerModel` carries `List<SalesOrderEntity>` and `List<SalesEntity>`; `ProductModel` carries `CategoryEntity`, `List<VariantEntity>`, `List<PurchaseVariant>`; `SalesVariantModel` carries both `ProductEntity` and `VariantEntity`. The screens therefore depend on database types anyway.
*Why it matters:* Any change to a table ripples into screens, and AutoMapper silently copies large object graphs (including image bytes) into the UI.
*Fix:* Keep view models flat (IDs and display strings only). Load lookups on demand.

**A5 · Medium · Three UI frameworks at once.**
Files: `Inventory/App.razor`, `Inventory/_Imports.razor`, `Inventory/Inventory.csproj`.
*What's wrong:* MudBlazor is used for layout, tables and dialogs; Blazor.Bootstrap is used for AutoComplete, DateInput, NumberInput and Modal; raw Bootstrap classes are used throughout the forms.
*Why it matters:* Inconsistent look and behaviour, CSS conflicts, three sets of documentation to learn, larger download. MudBlazor already has every component that Blazor.Bootstrap is being used for.
*Fix:* Standardise on MudBlazor; remove Blazor.Bootstrap and most Bootstrap markup over time.

**What I would change, in order:** (1) context factory, (2) service layer with transactions, (3) collapse repositories, (4) flatten view models, (5) single UI library. Each step is independently valuable and can be done one feature at a time.

### 2. Bugs and risky code

**B1 · High · Voucher numbers can be duplicated.**
Files: `Inventory/Pages/Purchase.razor.cs` (line 245), `Inventory/Pages/Sale.razor.cs` (line 305), `Inventory/Pages/SalesOrder.razor.cs` (line 867), and the `GetLastVoucherIdByDate` methods in the three repositories.
*What's wrong:* The app reads the highest voucher number for the day, adds one, then saves. Two people saving at the same moment both get the same number. Separately, when editing, the "does this voucher already exist" check only runs if the voucher *number* changed, so changing only the *date* can create a clash. There is no database rule preventing it.
*Why it matters:* Duplicate voucher numbers break reconciliation with paper records and accounting.
*Fix:* Unique index on (Date, VoucherId) for each of Purchases, Sales, SalesOrders; assign the number inside the same transaction as the save and retry once on conflict.

**B2 · High · A sale cannot have all its lines removed on edit.**
File: `Inventory/Domain/Repository/SaleRepositoryEF.cs` (line 1028, `if (sale.SalesVariants is { Count: > 0 })`).
*What's wrong:* If the operator reopens a sale, removes every item, and saves, the repository keeps the old lines because the new list is empty.
*Why it matters:* The items stay sold in the system and stock stays reduced, while the operator believes the sale is now empty. Wrong stock count.
*Fix:* Treat an empty list as "delete all lines", or block the save with a clear message.

**B3 · High · Typing in the Sales Summary can crash the page and lose the sale.**
File: `Inventory/Pages/SalesSummary.razor.cs` (lines 1572-1597, `RateChanged`, `DiscountChanged`).
*What's wrong:* The rate and discount fields are parsed with `decimal.Parse` / `int.Parse` inside `async void` handlers. Clearing the field, typing a comma, or pasting text throws an exception that cannot be caught.
*Why it matters:* The whole page connection dies ("An error has occurred. Reload"), and the unsaved summary is gone. This is the final step of every sale.
*Fix:* Use `TryParse` or typed numeric inputs; never use `async void`.

**B4 · High · Ten `async void` event handlers.**
Files: `Inventory/Pages/Product.razor.cs:144`, `Inventory/Pages/PurchaseOrder.razor.cs:246`, `Inventory/Pages/Sale.razor.cs:276,304`, `Inventory/Pages/SalesOrders.razor.cs:148`, `Inventory/Pages/SalesSummary.razor.cs:154,172`, `Inventory/Pages/Supplier.razor.cs:157`, `Inventory/Shared/PurchaseVariantComponent.razor.cs:195`, `Inventory/Shared/SalesOrderVariantComponent.razor.cs:192`. Also `Inventory/Pages/Purchases.razor.cs:644` overrides a lifecycle method as `async void`.
*What's wrong:* `async void` means "fire and forget". Errors inside cannot be caught by the page and will tear down the connection; the UI may also not refresh when the work finishes.
*Why it matters:* Random "app has stopped responding" moments; deletes that appear to do nothing.
*Fix:* Change to `async Task`. Blazor event handlers support this directly.

**B5 · High · Import of customers and products is very likely broken, and definitely unsafe.**
Files: `Inventory/Pages/Customers.razor.cs` (lines 1078-1136), `Inventory/Pages/Products.razor.cs` (lines 492-565).
*What's wrong:*
- The in-app help says the `Mobiles` column holds numbers "comma-separated", but the comma is also the CSV column separator. A row with two numbers will shift every later column.
- The CSV reader is pointed at `CustomerModel` / `ProductModel`, which contain nested lists and entity objects. CsvHelper cannot map those; it will throw on header validation.
- Products: the help says Category is a code like `CAT001`; the code reads it as a GUID (`GetValue<Guid?>`). Any real category code will throw.
- No duplicate check: the same file imported twice creates every customer twice. The UI form prevents duplicate IDs; the importer bypasses that check.
- Each row is saved separately, so a failure half-way leaves a partial import. Errors go to `Console.WriteLine` (invisible).
*Why it matters:* Duplicate master data is the number one cause of wrong reports and confused operators, and it is hard to clean up later.
*Fix:* An import service with: a preview step, validation per row (required fields, duplicate IDs against the DB and within the file), a single transaction (all or nothing), a downloadable error report, and a simple one-number-per-column or semicolon-separated format for mobiles. Test with a real file.

**B6 · High · Totals stored on purchases and sales orders go stale.**
Files: `Inventory/Pages/Purchase.razor.cs` (`AddTotalAmount`), `Inventory/Pages/SalesOrder.razor.cs` (`AddTotalAmount`), `Inventory/Domain/Repository/SupplierRepositoryEF.cs` (`GetPurchaseTotalsBySupplierAsync`), `Inventory/Pages/Reports/SalesPurchasesReports.razor`.
*What's wrong:* The header total is only written when the user clicks "Save Total Amount". Adding, editing or deleting a line afterwards does not update it. Supplier totals and the Sales vs Purchases report sum these stored totals.
*Why it matters:* Reports show wrong money figures without any indication.
*Fix:* Recompute and store the total server-side on every line change, or compute totals from lines in reports (preferable; one source of truth).

**B7 · Medium · Sale page does not require a customer.**
File: `Inventory/Pages/Sale.razor.cs` (`AddOrder`, line 321 uses `customer.Id`).
*What's wrong:* `SalesOrder` checks that a customer was chosen; `Sale` does not. With no customer, the save fails with a cryptic database foreign-key error.
*Fix:* Same guard as `SalesOrder`, with a friendly message.

**B8 · Medium · Enter-key scanning is fire-and-forget.**
File: `Inventory/Pages/Sale.razor.cs` (lines 377-381, `CheckIfClickEnter` calls `AddVariant()` without awaiting).
*Why it matters:* Scan errors are silently dropped and the table may not refresh until the next action. For a barcode workflow, that is exactly the moment where a scan is missed.
*Fix:* `await AddVariant();` and make the handler `async Task`.

**B9 · Medium · "Not found" returns an empty record instead of nothing.**
Files: `Inventory/Domain/Repository/ProductRepositoryEF.cs` (line 627), `PurchaseRepositoryEF.cs` (788), `SaleRepositoryEF.cs` (999), `SalesOrderRepositoryEF.cs` (1192).
*What's wrong:* `GetById` returns `new ProductEntity()` when nothing matches. Callers check for `null`, which never happens (e.g. `Inventory/Pages/Product.razor.cs` line 101).
*Why it matters:* Opening a stale link shows a blank form; saving it creates a new record instead of showing "not found".
*Fix:* Return `null` and handle it, or redirect with a message.

**B10 · Medium · Customer/supplier phone numbers are handled by a global sweep.**
Files: `Inventory/Pages/Customer.razor.cs` (lines 732-738), `Inventory/Pages/Supplier.razor.cs`, `Inventory/Service/MobileService.cs`.
*What's wrong:* On update, the phone list is replaced wholesale, then `DeleteEmptyNumbers` deletes **every** phone row in the database with an empty number, across all customers and suppliers. Rows removed from a customer are not deleted; their owner link is set to null, leaving orphans.
*Why it matters:* Orphan rows accumulate; the pattern is fragile and will produce surprising deletes as soon as someone touches it.
*Fix:* Compare old vs new numbers for that one customer and add/remove accordingly, inside the same save.

**B11 · Medium · Purchase Orders store free text instead of links.**
File: `Inventory/Domain/Entities/PurchaseOrderEntity.cs`, `Inventory/Pages/PurchaseOrder.razor` (inline row edit lets the user type any Supplier ID / Product ID / Variant ID).
*Why it matters:* Renaming a product orphans its purchase orders; typos go unnoticed; reports cannot join purchase orders to real products.
*Fix:* Foreign keys to Supplier, Product and Variant, with pickers instead of text fields.

**B12 · Medium · Deleting a master record uses exceptions for flow control and shows the stack trace.**
Files: `Inventory/Domain/Repository/CustomerRepositoryEF.cs` (lines 307-328), `SupplierRepositoryEF.cs`, `ProductRepositoryEF.cs`, `VariantRepositoryEF.cs`.
*What's wrong:* "Cannot delete, it has sales" is thrown as an exception, then caught and re-thrown as `new Exception(ex.ToString())`. The UI shows the full internal stack trace to the operator.
*Fix:* Return a result object ("blocked because 12 sales reference this customer") and show a plain message.

**B13 · Low · Stale, dead and inconsistent code (see §6).**

### 3. Data integrity – what happens with two users at once?

**Good news first.** Because stock is *calculated* from purchase lines minus sale lines rather than stored as a counter, two operators recording sales at the same instant cannot corrupt a stock number. This is the single best design decision in the app.

**The problems:**

**D1 · High · Multi-step saves are not transactions.**
Files: `Inventory/Pages/Purchase.razor.cs`, `Inventory/Shared/PurchaseVariantComponent.razor.cs`, `Inventory/Domain/Repository/PurchaseRepositoryEF.cs`, `PurchaseVariantRepositoryEF.cs` (and the sales-order equivalents).
*What's wrong:* Recording a purchase is: save header (one transaction), then save each line separately (one transaction each), then save totals (another). Updating a customer is: save customer, then run the phone sweep. If the app crashes, the machine loses power, or the database is locked mid-way, you get half a purchase in the system.
*Why it matters:* Half a purchase means stock is under-counted for the missing lines and the supplier total is wrong. Nobody is told.
*Fix:* One service method per business operation that does all the writes under one `SaveChanges` (EF wraps it in a transaction automatically) or an explicit `BeginTransaction`. Note that `SaleRepositoryEF.Create` already saves header and lines together; purchases and sales orders should follow that model.

**D2 · High · No database-level uniqueness.**
File: `Inventory/Domain/AppDbContext.cs` (no `HasIndex(...).IsUnique()` for business keys), `Inventory/Migrations/AppDbContextModelSnapshot.cs` (confirms only the variant image link is unique).
*What's wrong:* ProductId, CustomerId, SupplierId, VariantId-within-product and voucher-per-day are enforced only by form validation. Two users, an import, or a race can create duplicates.
*Why it matters:* The barcode scanning flow looks up products **by ProductId text** (`GetByProductId`). With duplicates, the scanner picks whichever row comes first; the sale lands on the wrong product.
*Fix:* Unique indexes via a migration, and catch `DbUpdateException` to show "ID already exists".

**D3 · Medium · Last writer wins on edits.**
Files: all entities (no `RowVersion` / concurrency token).
*What's wrong:* Two people open the same customer, both edit, both save. The second save silently overwrites the first.
*Fix:* Add a `RowVersion` column and let EF raise a concurrency conflict; show "someone else changed this record, please reload".

**D4 · Medium · Overselling is allowed silently.**
File: `Inventory/Pages/Sale.razor.cs`.
*What's wrong:* Nothing checks that the quantity being sold exists in stock. Stock goes negative with no warning.
*Fix:* Check stock at save time and warn (or block, as a configurable rule).

**D5 · Medium · No audit trail of stock changes.**
*What's wrong:* Editing or deleting a purchase from months ago silently changes today's stock. There is no record of who did what, when.
*Why it matters:* Impossible to explain a stock discrepancy or detect misuse.
*Fix:* An append-only `StockMovement` table (date, user, variant, quantity +/-, reason, reference voucher). Compute stock from it; never edit it, only add reversing entries.

**D6 · Medium · SQLite and multiple simultaneous users.**
*What's wrong:* SQLite allows one writer at a time. The app does not enable WAL (write-ahead logging) mode, which lets readers continue while someone writes. Today this is a non-issue because only one PC can reach the app. If the app is opened to a LAN, concurrent saves will occasionally fail with "database is locked".
*Fix:* Enable WAL (`PRAGMA journal_mode=WAL`) and a busy timeout; fine for up to roughly 5–10 light users. Beyond that, move to SQL Server Express or PostgreSQL (EF Core makes this a configuration change plus a migration).

### 4. Security

**S1 · Critical · Passwords in plain text, in the database and in the source code.**
Files: `Inventory/Domain/AppDbContext.cs` (lines 33-37), `Inventory/Migrations/20250109205444_addSqlLite.cs` (lines 386-392), `Inventory/Migrations/AppDbContextModelSnapshot.cs`, `Inventory/Pages/Login.razor.cs` (line 61 compares plain strings).
*What's wrong:* Two real usernames and passwords are hard-coded as seed data. The code is in a GitHub repository (`HitenH/Inventory2`); the passwords are in its history forever, even if removed now. Login compares the typed password directly to the stored one.
*Why it matters:* Anyone who has ever had access to the repository has both logins. If either password is reused anywhere else (email, banking), that is exposed too.
*Fix:* Change both passwords immediately. Store only a salted hash (ASP.NET Core's `PasswordHasher<T>` is a one-line change). Remove the seed data and create the first admin through a setup screen or command. If the repository is public, make it private.

**S2 · High · Half the app does not require login.**
Files missing `[Authorize]`: `Inventory/Pages/Customers.razor.cs`, `Customer.razor.cs`, `Products.razor.cs`, `Product.razor.cs`, `Settings.razor`, `Pages/Reports/*.razor`. (Purchases, Sales, Sales Orders, Suppliers and Category are protected.)
*What's wrong:* Anyone who can reach the URL can view and edit customers and products, run reports, and **restore or delete backups** from Settings without logging in. The menu hides the links, but typing the address works.
*Why it matters:* Today the risk is contained because only the local PC can connect. The day the app is opened to the network, this becomes a serious hole.
*Fix:* Add `@attribute [Authorize]` to `_Imports.razor` so everything is protected by default, and `[AllowAnonymous]` on Login and the home page.

**S3 · High · Roles exist but are never checked.**
Files: `Inventory/Authentication/CustomAuthenticationProvider.cs` (sets a Role claim), no `[Authorize(Roles=...)]` anywhere.
*Why it matters:* "User" and "Admin" are identical. Any logged-in user can delete customers, delete all completed orders, restore a backup (overwriting the live database), or wipe backups.
*Fix:* Restrict destructive actions and Settings to Admin.

**S4 · Medium · Authentication wiring is misleading.**
File: `Inventory/Program.cs` (lines 26-35, 58).
*What's wrong:* `AddAuthentication` is configured with ASP.NET Identity scheme names but no Identity handler is registered, so the middleware does nothing. `AddHttpContextAccessor` is unused. `ProtectedSessionStorage` is re-registered as Transient on top of the framework's own registration.
*Why it matters:* The next developer will assume Identity is in place. It is not.
*Fix:* Either adopt ASP.NET Core Identity properly (recommended; gives hashing, lockout, password rules for free) or remove the dead configuration.

**S5 · Low · Login is per browser tab, HTTPS is off in production, HSTS is pointless over HTTP.**
Files: `Inventory/Authentication/CustomAuthenticationProvider.cs` (session storage), `Inventory/Program.cs` (lines 16-19, 73-80).
*Notes:* Acceptable for a single local machine. If the app ever leaves the machine, it needs HTTPS with a certificate and cookie-based auth.

**S6 · Low · Internal error text shown to users.** Delete failures show stack traces (see B12). Error messages from the database are shown raw in several snackbars.

**Data exposure:** No personal data is sent anywhere external. Customer names, addresses and phone numbers live only in the local SQLite file and its backups. The main exposure risk is the unencrypted database file and backup copies sitting in the program folder (see M1).

### 5. Performance – thousands of products, many users

**P1 · Medium · Every list screen loads the whole table into memory and renders every row.**
Files: `Inventory/Pages/Customers.razor.cs`, `Products.razor.cs`, `Suppliers.razor.cs`, `Purchases.razor.cs`, `Sales.razor.cs`, `SalesOrders.razor.cs`, `PurchaseOrder.razor.cs`; all `MudTable`s without paging.
*What this means:* A few thousand rows is fine. Tens of thousands of sales or purchases will make those screens take seconds to open and the browser sluggish, because the full table is sent over the live connection and drawn as HTML.
*Fix:* Server-side paging (`MudTable ServerData`) or `Virtualize`, and search in the database, not in memory.

**P2 · Medium · The customer list loads every sale of every customer.**
File: `Inventory/Domain/Repository/CustomerRepositoryEF.cs` (`GetAll`, `.Include(c => c.Sales)`), used by `Customers.razor.cs`, `CustomerDialog.razor`, `CustomerPopup.razor.cs`, `CustomerReports.razor`.
*What this means:* Opening the customer page, or the customer picker on the sales screen, pulls the entire sales history into memory just to show a total per customer. This is the first thing that will become noticeably slow as sales accumulate.
*Fix:* Do what the supplier page already does: one aggregate query (`GetPurchaseTotalsBySupplierAsync` is the right pattern).

**P3 · Medium · Product photos stored in the database and loaded with every product query.**
Files: `Inventory/Domain/Entities/Image.cs` (`byte[] ImageData`), `Inventory/Domain/Repository/ProductRepositoryEF.cs` (`GetById` includes images), `Inventory/Shared/Variant.razor` (renders as base64 text).
*What this means:* Each photo is up to a few hundred KB; a product with 10 variants ships a few MB over the live connection every time it opens, embedded as text in the HTML. The database file also grows quickly and backups get slow.
*Fix:* Store images on disk (or blob storage) and serve them through a small endpoint with caching; keep only the path in the database.

**P4 · Medium · Saving one variant rewrites the whole product.**
Files: `Inventory/Shared/Variant.razor.cs` (lines 753, 806), `Inventory/Domain/Repository/ProductRepositoryEF.cs` (`Update` uses `context.Products.Update(product)`).
*What this means:* Adding a single variant marks the product, all its variants and all their image blobs as changed and writes them all again.
*Fix:* Add/update the one variant row.

**P5 · Medium · Reports load all sales and all purchases into memory and filter with `dynamic`.**
File: `Inventory/Pages/Reports/SalesPurchasesReports.razor` (`FilterByTime`).
*Fix:* `WHERE Date BETWEEN ...` with `SUM` in the database. Also, the filter labels are wrong: "The Last 30 Days" is actually "since this day last month", "This Year" is actually "last 12 months", "This Week" is "last 7 days".

**P6 · Low · Pickers reload their full list every time a parameter changes.**
Files: `Inventory/MudBlazorComponents/CustomerDialog.razor`, `ProductsDialog.razor`, `SuppliersDialog.razor` (`OnParametersSetAsync`), `Inventory/Shared/ProductPopup.razor.cs` (also an arbitrary `Task.Delay(200)`).

**Many users at once:** Blazor Server keeps each user's screen state in server memory (typically tens to a few hundred KB per user; more here because full tables are held). On one decent PC, 10–30 simultaneous users is comfortable *after* P1 and P2 are fixed. The bigger constraint is SQLite's single-writer rule (D6). With fixes, the honest ceiling for this stack on one machine is roughly 10 concurrent operators on SQLite, or 50+ on SQL Server/PostgreSQL.

### 6. Unused or duplicate code – what can be removed safely

**Unused and safe to delete**

| Item | Files | Evidence |
|---|---|---|
| Old popup pickers (replaced by MudBlazor dialogs) | `Inventory/Shared/CustomerPopup.razor(.cs)`, `SupplierPopup.razor(.cs)`, `ProductPopup.razor(.cs)` | Not referenced by any page. `SupplierPopup.SortItem` also compares against the typo `"SuptomerId"` so it never sorted. |
| Old image modal | `Inventory/Shared/ModalWindowShowImage.razor` | Not referenced. |
| Layout code-behind | `Inventory/Shared/MainLayout.razor.cs` | `Logout`, `ShowMaster`, `ShowTransaction` unused; `NavMenu.razor` has its own Logout. |
| Chart.js and its plugin | `Inventory/App.razor` (lines 35-36), `Inventory/libman.json`, `wwwroot/lib/Chart.js`, `wwwroot/lib/chartjs-plugin-datalabels` | Loaded on every page; no chart anywhere. |
| Dead authentication config | `Inventory/Program.cs` lines 26-35 (`AddAuthentication`), line 30 (`AddHttpContextAccessor`), line 58 (`ProtectedSessionStorage` transient) | See S4. |
| Dead SQL Server connection string | `Inventory/appsettings.json` (`ConnectionString` → "ThisWeird") and the commented block in `Program.cs` | SQLite is used. |
| Error page | `Inventory/Pages/Error.cshtml(.cs)` | `UseExceptionHandler("/Error")` points at a Razor Page, but Razor Pages are never registered, so the route does not exist. Remove both or register Razor Pages. |
| Inline row-edit leftovers | `Inventory/Pages/SalesOrders.razor.cs` (`EditOrder`, `BackupItem`, `ResetItemToOriginalValues`, `salesOrderModelBackup`, `orderStatuses`, `isSortAscending`, `isSelected`); the commented `RowEditingTemplate` in `SalesOrders.razor` | `EditOrder` updates a brand-new empty entity, so even if called it would save nothing. |
| Sort code never wired to the UI | `Inventory/Pages/Purchases.razor.cs` (`SortItem`, `isSortAscending`) | MudTable does its own sorting. |
| Demo code copied from MudBlazor docs | All of `Inventory/Pages/Reports/*.razor`: `_events` lists, `RowClicked`, `RowRightClicked`, `SelectedItemsChanged`, `_sortNameByLength`; `Alt="Swedish Farm House"` in `Variant.razor` and `VariantPageForReports.razor` | Serialises rows to JSON into a list nobody reads. |
| Leftover fields | `VariantPageForReports.razor` (`variantModel`, `image`, `editContext`, `variantInputRef`, `messageStore`, `bclearinputfile`, `snackbar`, `ReturnFocusToInput`), `PurchaseOrderReports.razor` (`purchaseOrderEntity`, `purchaseOrderModel*`, `selectedOrders`, `selectedProduct`, `selectedSupplier`), `SalesPurchasesReports.razor` (`sales`, `salesAfterSearch`, `_searchString`, `isCustomRangeDialogOpen`, `customStartDate`, `customEndDate`), `CustomerReports.razor` (`ShowLoadingDialogAsync`, `CloseDialog`, `dialogReference`) | Compiler warnings confirm. |
| Not-implemented methods | `Inventory/Domain/Repository/UserRepositoryEF.cs` (`GetAll`, `Update`, `Delete` throw) | Remove from interface. |
| Misc | `Product.razor.cs` (`variant`, `GetVariant`, `modalWindowComponenRef` never assigned so update/error dialogs never show), `SalesModel.SalesVariants`, `ProductModel.PurchaseVariants`, `VariantModel.PurchaseVariants/SalesVariants`, `Snackbar snackbar` fields everywhere, `using DocumentFormat.OpenXml.Spreadsheet` in `Variant.razor.cs`, empty `OnAfterRenderAsync` in `SalesSummary.razor.cs`, `SalesOrderReports.razor` (routable at `/reports/salesorders` but not linked from the Reports menu – decide keep or drop) | |
| Empty CI folder | `.github/workflows/` | No workflow files. |

**Duplicated – merge into one**

| Duplication | Files | Suggested single home |
|---|---|---|
| Purchase line editor vs Sales-order line editor (~90% identical, 340 lines each) | `Inventory/Shared/PurchaseVariantComponent.razor(.cs)`, `SalesOrderVariantComponent.razor(.cs)` | One generic `TransactionLineEditor` with a line model. |
| Purchase page vs Sales Order page | `Inventory/Pages/Purchase.razor(.cs)`, `SalesOrder.razor(.cs)` | Shared header component + service. |
| Customers page vs Suppliers page; Customer vs Supplier edit pages | `Inventory/Pages/Customers*`, `Suppliers*`, `Customer*`, `Supplier*` | Shared "party" components; the Suppliers page already has the better implementation (aggregate totals, null-safe search). |
| Three pickers and three autocompletes | `Inventory/MudBlazorComponents/CustomerDialog.razor`, `ProductsDialog.razor`, `SuppliersDialog.razor`; `Inventory/Shared/Selectors/*Autocomplete.razor` (all three name their data provider `CustomersDataProvider`) | One generic `LookupAutocomplete<T>`. |
| Nine-branch `SortItem` methods | `PurchaseVariantComponent.razor.cs`, `SalesOrderVariantComponent.razor.cs`, `Purchases.razor.cs`, the three popups | One 5-line generic sort helper, or let MudTable sort. |
| Confirm-delete dialog boilerplate (same 10 lines in 12 places) | Every delete action | One `ConfirmAsync("...")` helper. |
| CSV/Excel import | `Customers.razor.cs`, `Products.razor.cs` | One `ImportService`. |
| Stock calculation appears twice | `ProductRepositoryEF.GetProductStockReportAsync`, `GetStockInHandByVariantIdsAsync` | One query. |
| Line-total maths appears in `LineAmounts` **and** again by hand in `SalesSummary.razor.cs` (`AmountChanged`, `AmountAfterDiscountChanged`) | | Use `LineAmounts` everywhere. |

Rough estimate: 25–30% of the codebase can go without changing any behaviour.

### 7. Code quality – readability, consistency, maintainability

- **Naming.** Typos baked into APIs: `VariantEntytiId`, `IVariantIdExist`, `modalWindowComponenRef`, `RenameDiscoint` (migration), `"AmmountAfterDiscount"`, `"SuptomerId"`. `PurchaseVariant` is an entity but lacks the `Entity` suffix the others have. Data providers for suppliers and products are called `CustomersDataProvider`. These cost nothing to fix now and cost confusion every day they remain.
- **Discount means two things.** On a line, `Discount` is a percentage (int). On a header, `Discount` is a money amount (decimal). On a sales order it is not stored at all but reverse-calculated from the total (`SalesOrder.razor.cs` lines 849-854). Name them `DiscountPercent` and `DiscountAmount` and store both.
- **Mixed styles.** Some files use file-scoped namespaces, some block-scoped; some pages load in `OnInitializedAsync`, others in `OnAfterRenderAsync(firstRender)`, one in `async void OnAfterRender`. Some validation is DataAnnotations, most is hand-rolled `ValidationMessageStore` code duplicated per page.
- **563 compiler warnings.** Mostly nullable-reference warnings because `Nullable` is enabled but the code ignores it (`string Name` with no initialiser, `[Inject] private X Y { get; set; }` without `= default!`). The noise hides the few warnings that matter (unused fields, never-assigned references). Clean up, then turn on `TreatWarningsAsErrors` to keep it clean.
- **Logging style.** `Logger.LogError("Something: " + ex.Message)` discards the stack trace. The newer code correctly uses `Logger.LogError(ex, "Something")`. Standardise on the latter.
- **Namespaces vs folders.** `MudBlazorComponents/` is `Inventory.MudBlazorComponents` but `_Imports.razor` imports `MudBlazorComponents`; a repository (`VariantRepositoryEF.cs`) has `using Inventory.Pages;`. Data layer should never depend on UI.
- **Positive notes.** The recent uncommitted work is clearly better than the older code: `AsNoTracking` on reads, `LineAmounts` shared maths, aggregate supplier totals, null-safe search on Suppliers/Products, `LogError(ex, ...)`. The direction is right; it needs to be applied everywhere and finished.

### 8. Missing pieces

| Piece | Status | Why it matters | Recommendation |
|---|---|---|---|
| **Automated tests** | None. No test project. | Every change is verified by hand, so regressions slip through (several bugs above are of the "worked before" kind). | Start with unit tests for `LineAmounts`, stock calculation, voucher assignment, and import validation. Then integration tests against an in-memory SQLite for each service method. |
| **Error handling** | Inconsistent. Many `catch` blocks swallow errors with a log line and silently navigate away (`Category.razor.cs` navigates even on failure; `Sale.EditOrder` navigates to the list on error, losing the edit). | The operator does not know a save failed. | One policy: show a clear message, keep the user on the page, log with full details. Add an `ErrorBoundary` around `@Body`. |
| **Logging** | Console only. Most messages lose the stack trace. The production exe shows a console window; close it and the log is gone. | When something goes wrong at the counter, there is nothing to look at afterwards. | Add Serilog with a rolling file sink (e.g. `logs/inventory-YYYYMMDD.log`, keep 30 days). Log who did what for every save/delete. |
| **Backups** | Manual button; copies the live SQLite file; stores next to the database; restore overwrites while the app is running. | A file copy of an open SQLite database can be inconsistent (uncommitted journal). Restoring over an open database can corrupt it. A disk failure takes database and backups together. | Use `SqliteConnection.BackupDatabase()` (the engine's online backup). Run automatically daily (hosted background service). Copy to a second location (USB/NAS/cloud folder). Verify each backup opens. Restore only after closing connections, then restart. **Critical.** |
| **Database location** | `Program.cs` line 43: next to the exe. Publish profile `FolderProfile.pubxml` has `DeleteExistingFiles=false` today, but one checkbox change on publish wipes the business data. | Data should never live in the program folder. | Move to `%ProgramData%\Inventory\` (configurable), with backups in a different folder/drive. |
| **Audit trail** | None. No created/modified by/at on any table. No stock movement log. | Cannot answer "who changed this?" | Add `CreatedAt/By`, `ModifiedAt/By` to transactional tables; add a `StockMovement` ledger (see D5). |
| **Input validation** | Partial. No length limits on text, no "quantity must be positive", no "rate must be ≥ 0", no customer required on Sale. | Garbage in, wrong totals out. | DataAnnotations on models + the same rules enforced in services. |
| **Runtime support** | .NET 9 (out of support since May 2026); MudBlazor 7.15 (8.x is current); EF Core 9. On this machine the .NET 9 runtime is not even installed, which is why the EF tooling could not run. | No security fixes; newer libraries fix bugs you are working around. | Move to .NET 10 LTS (supported until November 2028), EF Core 10, MudBlazor 8. Mostly mechanical. |
| **Source control hygiene** | Last commit 3 June 2025. Working copy has 49 modified + 2 new files. Folder name "(before customer search modification)" suggests whole-folder copies are being used as versioning. Empty `.github/workflows`. | Months of work exist on one disk only. | Commit now. Work on branches. Add a GitHub Actions workflow that builds and runs tests on every push. |
| **Documentation** | None (no README, no setup guide). | Nobody but the original developer can install or run it. | A one-page README: how to build, publish, where data lives, how to restore. |
| **Configuration** | Phone-slot counts in `appsettings.json`; database path and port hard-coded. | Cannot move the database or open the app to the LAN without recompiling. | Put path, URL and backup folder in configuration. |

---

## PART 3 – Is this the right tech stack?

### Honest assessment of Blazor for this app

**It is a good fit.** Here is why, and where it is not.

**Strengths for an inventory system**
- **One language.** Everything is C#. One developer, one skill set, one debugger. For a small business with limited development budget, that is the biggest cost saver.
- **Mature, supported, hireable.** .NET and Blazor are Microsoft's mainstream stack with a 10-year horizon. C#/.NET developers are plentiful and not expensive relative to niche stacks.
- **Rich UI components for free.** MudBlazor gives tables, dialogs, autocomplete, date pickers and theming out of the box. Line-of-business screens are exactly what it is built for.
- **Blazor Server suits a counter PC.** The screen logic runs on the server (here, the same machine), so it works on weak hardware and old browsers, and barcode-scanner "keyboard wedge" input works naturally in the browser.
- **EF Core + SQL** is the standard, well-understood data story. Switching from SQLite to SQL Server or PostgreSQL later is a configuration change plus a migration, not a rewrite.
- **The code already does the hardest thing right:** stock is derived from transactions, not stored as a counter.

**Weaknesses for this use case**
- **Blazor Server needs a constant connection.** Every click goes to the server over a WebSocket. On a flaky Wi-Fi tablet or over the internet, the app freezes and shows "reconnecting". On one PC over localhost, this never bites. It will the day you want a phone or tablet in the warehouse.
- **It is a web app pretending to be a desktop app.** Launching a web server and a browser is a workable trick, but it means a console window, a browser tab the user can close by accident, per-tab login, and no offline mode. A true desktop build (Blazor Hybrid via .NET MAUI, or a Windows Service hosting the web app with the browser as a client) would be cleaner.
- **SQLite caps the number of simultaneous users** (roughly 5–10 light writers with WAL). Fine for one shop; not for several branches.
- **Three UI frameworks** is a self-inflicted problem, not a Blazor problem, but it does make the UI look and behave inconsistently.
- **No mobile story.** If staff need to scan on a phone, you need either Blazor WebAssembly/PWA, a MAUI app, or a different frontend.

### Keep, improve, or rebuild?

| Option | Cost (dev effort) | Time | Risk | Long-term benefit |
|---|---|---|---|---|
| **A. Patch only** – fix the Critical/High items, leave structure | ~2–4 weeks | 1 month | Low. Nothing visible changes. | Low. Debt remains; every new feature stays slow and risky. |
| **B. Keep and improve (recommended)** – fix Critical/High, then refactor feature-by-feature: context factory, service layer, remove duplication, single UI library, tests, .NET 10 | ~2–3 months of a competent .NET developer, can be part-time and incremental | 3–4 months | Medium, mitigated by doing one feature at a time with tests, and by keeping all data and workflows as they are. | High. You end up with a maintainable app on a supported platform, same look and feel, same data. |
| **C. Rebuild from scratch** on the same or a different stack | ~4–6 months minimum for parity, plus data migration | 6+ months | High. Rebuilds routinely miss "small" behaviours users rely on (voucher-per-day, scan flow, 3 mobiles). Business runs two systems during transition. | Only pays off if requirements change fundamentally (multi-branch, mobile-first, offline, integrations). |
| **D. Buy off-the-shelf** (Zoho Inventory, inFlow, Odoo, Sortly, Vyapar, etc.) | Subscription (typically a few tens of USD per user per month) + 2–4 weeks setup and data import | 1–2 months | Low technical risk; medium business risk (workflow must bend to the product; data lives with a vendor). | High if your workflow is standard. Low if the voucher-per-day / scan-summary flow is genuinely specific to how you operate. |

**My recommendation: B.** The domain is small (about 15 tables), the code is ~12,000 lines, and the most expensive design decision (derived stock) is already right. A disciplined refactor costs a fraction of a rewrite and carries far less risk. Do option A's items first inside option B so the dangerous problems close within weeks.

**When I would change that recommendation:**
- If you tell me multiple shops or warehouses need to share one system → still B, but swap SQLite for PostgreSQL/SQL Server early and host it on a small server or cloud VM.
- If staff need phones/tablets in the aisles → B, plus either move to Blazor WebAssembly/PWA for the scanning screen, or keep Blazor Server on a reliable LAN with a cheap Windows mini-PC as the server.
- If you need accounting/tax integration, multi-currency, or a formal audit trail soon and your flow is not unusual → seriously evaluate D first. Custom software is only worth maintaining when it does something the market does not.
- If there is no developer available to maintain it at all → D.

### If you did rebuild, the alternatives in plain terms

| Alternative | Trade-off |
|---|---|
| **Blazor Hybrid (.NET MAUI)** – a real Windows desktop app using the same Razor screens | Keeps C# and most UI code; works offline; no browser/console quirks. Loses "open it from any PC with a browser". |
| **Blazor WebAssembly + ASP.NET API** | Runs in the browser, tolerates bad connections, can be a PWA on phones. More code (an API layer), heavier first load, more complex auth. |
| **React/Vue + ASP.NET API** | Largest talent pool for the frontend, best mobile web experience. Two languages, two build systems, roughly double the frontend effort. |
| **Keep the backend regardless.** | Whatever the frontend, .NET + EF Core + SQL is the right backend. Do not change it. |

### Questions I need answered to make the recommendation firm

1. **Users:** How many people use it, and how many at the same time? Is it one counter PC today?
2. **Locations:** One shop, or several sites that should share stock data?
3. **Devices:** Any need for phones or tablets (e.g. scanning in a storeroom)? What barcode scanners are in use?
4. **Hosting:** Is the current "run the exe on one PC" setup intended, or a stopgap? Is there a server or cloud account available?
5. **Volume:** Roughly how many products/variants, customers, and sales lines per day? How many years of history must stay online?
6. **Who maintains it:** Is there an ongoing developer (the commit history mentions working "with Hiten"), a contractor, or nobody? How many hours per month are realistic?
7. **Budget:** Rough range for the next 6 months of development, and appetite for a monthly SaaS fee instead.
8. **Compliance:** Do you need tax invoices (e.g. GST/VAT formats), accounting export, or a formal audit trail for stock adjustments?
9. **Integrations:** Accounting software, e-commerce, supplier price lists?
10. **Offline:** Must sales continue if the internet or LAN is down?
11. **Data sensitivity:** Is customer personal data subject to any regulation you must meet (data protection law in your country)?
12. **Repository visibility:** Is the GitHub repository public? (This decides how urgently the password finding must be treated.)

---

## PART 4 – Summary

### Overall health score: 4.5 / 10

| Dimension | Score | Comment |
|---|---|---|
| Functionality | 7 | Covers the whole buy–stock–sell cycle; builds; scanning flow is thoughtful. |
| Correctness | 4 | Duplicate vouchers/IDs possible, stale totals, sale-edit bug, crash-prone inputs, broken import. |
| Data integrity | 4 | Derived stock is excellent; everything else (transactions, uniqueness, concurrency, audit) is missing. |
| Security | 2 | Plain-text passwords in source; half the pages unprotected; roles unused. Mitigated only by being localhost-only. |
| Performance | 5 | Fine today; several patterns will degrade visibly with growth. |
| Maintainability | 4 | Heavy duplication, dead code, three UI toolkits, no tests, 563 warnings. |
| Operations | 3 | Unsafe backup, data in program folder, console-only logs, out-of-support runtime, uncommitted work. |

### Top 10 things to fix first (priority order)

1. **Rotate and hash passwords; remove seeds from code.** (`AppDbContext.cs`, migrations, `Login.razor.cs`) – Critical, 1 day.
2. **Commit the uncommitted work and set up branches + a build workflow.** – Critical to protect 5 months of work, 1 hour.
3. **Make backups safe and automatic; move the database out of the program folder.** (`BackupService.cs`, `Program.cs`) – Critical, 2–3 days.
4. **Add unique indexes** for ProductId, CustomerId, SupplierId, (ProductId, VariantId), and (Date, VoucherId) on the three voucher tables; handle the conflict message. (`AppDbContext.cs` + migration) – High, 1–2 days.
5. **Switch to `AddDbContextFactory` and short-lived contexts** per operation; delete the hydrator workaround. (`Program.cs`, repositories) – Critical root cause, 3–5 days.
6. **Wrap each business operation in one transaction** via a service layer: record purchase, record sale, update customer. Fix the empty-lines-on-edit bug and the stale header totals at the same time. – High, 1–2 weeks.
7. **Protect every page with `[Authorize]` by default and restrict destructive actions to Admin.** – High, half a day.
8. **Remove all `async void`, use `TryParse` on Sales Summary inputs, await the Enter-key scan.** – High, 1 day.
9. **Fix or disable the import feature** until it has validation, duplicate checks and a transaction. – High, 2–3 days (or 10 minutes to hide the buttons).
10. **Upgrade to .NET 10 LTS, EF Core 10, MudBlazor 8; add Serilog file logging.** – High, 2–3 days.

### Roadmap

**Now (weeks 1–2) – stop the bleeding**
- Items 1, 2, 3, 7, 8, 9 above.
- Change both passwords today even before code changes.
- Take a verified backup to an external location before any other work.
- Hide the Import buttons until fixed.

**Next (months 1–3) – fix the foundations, one feature at a time**
- Items 4, 5, 6, 10.
- Introduce `PurchaseService`, `SaleService`, `SalesOrderService`, `StockService`, `ImportService`; move logic out of pages into them; one transaction each.
- Add a test project: unit tests for `LineAmounts`, stock maths, voucher assignment, import validation; integration tests per service on in-memory SQLite.
- Add `RowVersion` to editable tables; add created/modified stamps.
- Add a `StockMovement` ledger and a stock-adjustment screen (with reason) so corrections stop being "edit the old purchase".
- Replace `Customers.GetAll` sales-loading with an aggregate query; add paging to list screens.
- Delete the unused code listed in §6; merge the duplicated line editors and pickers.
- Standardise on MudBlazor; remove Blazor.Bootstrap and Chart.js.
- Fix report date-filter labels and move report sums into SQL.
- Write the README.

**Later (months 3–6+) – grow with the business**
- Depending on your answers to the Part 3 questions: move images out of the database; enable WAL or move to PostgreSQL/SQL Server; host on a small server with HTTPS if more than one PC needs access; consider Blazor Hybrid or a PWA scanning screen for mobile.
- Proper user management (create/disable users, change password, lockout) via ASP.NET Core Identity.
- Scheduled off-site backups with restore drills.
- Invoice/receipt printing and accounting export if needed.
- Turn on `TreatWarningsAsErrors` once warnings are cleared; add code analysis to the build.

---

## Appendix – how this review was done and what was not verified

- Read every `.cs`, `.razor`, `.json`, `.csproj`, publish profile and migration snapshot in the project (build output excluded).
- Compiled the project: **0 errors, 563 warnings**.
- Searched the code for transactions, concurrency tokens, authorisation attributes, `async void`, and unused components to confirm claims above.
- Compared the migration snapshot's columns for Sales and Purchases against the entity classes by reading: they match. The automated check (`dotnet ef migrations has-pending-model-changes`) could not run because the .NET 9 runtime is not installed on this machine.
- **Not verified at runtime:** the import failures (B5) are based on reading the code and library behaviour; they should be confirmed with a real file before deciding how much to invest in the fix. No live database was inspected, so I cannot say whether duplicates or orphan rows already exist. A quick SQL check for duplicate ProductId/CustomerId/SupplierId and duplicate (Date, VoucherId) is recommended before adding the unique indexes, because the migration will fail if duplicates are present.
