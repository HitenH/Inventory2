# Plan for version 3.0 – from one counter PC to a multi-user, multi-location system

**Status:** agreed direction, 14 Nov 2026 (session 10). **Built so far:** stage 0 (live 16 Nov 2026), stage 1a and stage 1b (25 Nov 2026), on `main` but **not installed** – the counter PC still runs version 2.0 until stage 1 is finished. **Now: stage 1c** (being built). See "Where we are" below.
**Versions:** 1.0 = the May 2026 build. 2.0 = the current app, concluded 14 Nov 2026 and described in `README.md` (what it does, how it is built, how to run, test and install it, known limits). 3.0 = this plan.
**How this fits:** `README.md` explains the app as it is. This file is the only plan. Nothing is due on version 2.0. When a stage starts, its open questions (section 9) are asked and its batches are written into a "Current stage" section added at the top of this file.
**Written for:** Hiten (plain English) and any future session picking this up.
**This plan will change.** It is reviewed at the start of every stage; what we learn in one stage may reorder the next.

**Contents:** 1 Where we are going · 2 The big changes underneath · 3 How it will be built · 4 Stages · 5 Today's screens and old open items · 6 Technology decisions for later · 7 Things outside the software · 8 Risks · 9 Open questions (parked) · 10 Decided · 11 Not in this plan · 12 Words used

---

# CURRENT STAGE: Stage 1 – Foundation (opened 24 Nov 2026)

Stage 0 is done (version 2.0 live on the counter PC since 16 Nov 2026). Hiten opened stage 1 on 24 Nov 2026 and asked that it be done **in the order written**: 1a, then 1b, 1c, 1d.

**Baseline taken before any stage 1 code was written (24 Nov 2026):** build 0 errors / 333 warnings; 294 tests pass. Any later failure is stage 1's, not inherited.

**Rollback:** the live counter PC runs the code tagged `live-16-nov-2026`. Before any stage 1 build is installed there, a copy of the live database is taken first (README section 10, "Going back"). Hiten confirmed on 24 Nov 2026 that he also keeps his own database backups in two physical places away from the running drive.

## Where we are (25 Nov 2026, end of session 11) – read this first

- **Done and pushed:** 1a-1 line rules (`Service/LineRules.cs`; negative quantities allowed for returns/damage, zero refused), 1a-2 double-Enter guard, 1a-4 "not found" + plain messages (partly), and all of **1b**: roles and permissions data (`Service/Permissions.cs`, `Service/Access.cs`), Users screen, Roles screen, screens and menu follow the role (`Service/AccessService.cs`, `Shared/MainLayout.razor`, `Permissions.ForPath`), rate hiding both ways, three starter roles. Last commit: batch 1b-5.
- **Parked by Hiten until the end of stage 1:** amounts/totals (1a-3), the proper damage/returns workflow, CSV imports refusing quantity ≤ 0. See "Parked" below.
- **Now: stage 1c – who and when.** Describe step done 25 Nov 2026 (decisions and batches in "Stage 1c" below). 1c-1 to 1c-4 pushed; 1c-5 (Activity log screen) built and browser-checked, not yet pushed. The full browser suite for stage 1c was run on 25 Nov 2026 (see "Stage 1c"). Next: Hiten tries it, then stage **1d – shop network**.
- **1d – shop network: PARKED by Hiten, 25 Nov 2026** ("I only have 1 system as of now"). It comes back when a second PC, tablet or phone is added. The old rule "stage 1 is installed only when 1d is done" is therefore open: what to do next (install 1a–1c on the counter PC, or go on to another stage) is Hiten's choice.
- **Counts now:** build 0 errors / 329 warnings (baseline was 333); **426 tests** pass (after 1c-5).
- **Install rule:** copy the live database before installing anything (README section 10). Stage 1 adds new tables and columns (`UsersAndRoles`, `StarterRoles`, `RecordStamps`, `ActivityLog`, `ActivityLogDetail` upgrades): an older build cannot open the upgraded database. The upgrade was tried on a copy of real data: both users became Admin, nothing else changed.
- **Working agreements with Hiten:** plain English, no jargon; he answers questions and says **"push"** to approve – nothing is committed or pushed before that. Show mock-ups (ASCII is fine) before building a new screen. Senior-developer role: delegate large batches to a cheaper agent with a precise brief and review its work, or do it directly when Hiten asks ("do it yourself" for 1b-3 to 1b-5). Stop any agent that loops.

## Stage 1a – Move the rules into one place (nothing new on screen)

**Why this comes first.** Today the rule "a line must have a sensible quantity" is written inside the Purchase and Sale screens, and Bulk Entry holds its own separate copy of it. With one PC that is survivable. Hiten has said the shop will later run "a lot of devices" – checker's tablet, picker's phone, salesman's screen. Every one of those would otherwise need its own copy of every rule, and the first copy anybody forgets writes bad stock into the data from a device nobody is watching. So the rules move into one place before any new screen is added.

| Batch | What | Changes what the operator sees? |
|---|---|---|
| 1a-1 **DONE 24 Nov 2026** | One rule book for voucher lines (`Service/LineRules.cs`): a real product and variant, quantity a whole number that is not zero, discount 0–100. Sale, Purchase, Sales Order and Bulk Entry all ask it. Also closed two holes found while building it: **no screen checked the discount at all** (not even Bulk Entry, contrary to the README), and the quantity box in the Sale lines table wrote any number straight onto an already-added line. | **Yes** – quantity 0 or a discount outside 0–100 are refused with a plain message. Negative quantities were refused too at first; Hiten reversed that the same day because returns and damage are recorded as negative lines for now. |
| 1a-2 **DONE 24 Nov 2026** | Double-Enter guard: a very fast second Enter can add the same line twice today. The second one is ignored. | No (removes an accident) |
| 1a-3 **PARKED 24 Nov 2026** (amounts are not used yet; see "Parked" below) | Totals that cannot go odd: a voucher with a discount but no lines currently stores a **negative** total, which quietly lowers supplier and report figures; a sale that is never summarised keeps a stored total of 0 although its stock has gone. | No |
| 1a-4 **DONE 24 Nov 2026 (partly)** | Honest "not found" and plain messages: four repositories return a blank record instead of saying "not found" (which is how a blank screen appears instead of a message), and technical error text like "GetSupplier error: …" is replaced with ordinary words. **Done 24 Nov 2026 (partly):** the edit screens for Purchase, Sale, Sales Order, Sales Summary and Product now say "not found" (or "could not be opened") and go back to their list, instead of showing a blank form. Save and update messages read "Could not save the purchase: …" and similar. **Open:** the repositories still return a blank record; the wording sweep covers only the messages touched so far. | Only better messages |

**How each batch is checked (Hiten, 25 Nov 2026):** its own tests, build clean (no new warnings), all tests pass, and **its own new browser check** on a test copy. The **full** `tools/ui-check` suite (keyboard, entry flows, pop-ups, save and focus, Bulk Entry, Alt+B, line rules, access, rates, look) runs **once per stage** and always before anything is installed on the counter PC – not after every batch. The dev app on port 5259 is restarted only when Hiten wants to try something. Keyboard and scanner behaviour does not change.

**1a-1 as built and checked (24 Nov 2026):** 312 tests pass (18 new for the rule book); build 0 errors and **330** warnings, three fewer than the 333 baseline because the null guards added here also cleared three old ones. A new browser check, `tools/ui-check/check-line-rules.mjs`, proves in Chrome that each bad value is refused on Purchase, Sales Order, Sale, the Sale lines table and Bulk Entry, and that a good line still saves. All eight existing `tools/ui-check` scripts still pass, `check-look` included. Not yet tested by Hiten.

**Parked until the end of stage 1 (Hiten, 24 Nov 2026):**
- **Amounts:** Hiten does not use the amount columns; this is an inventory tool for now. Negative voucher totals are allowed (a return can legitimately be negative), so the total clamp from batch 1a-3 was rolled back. Still open from 1a-3: a sale whose Sales Summary is never saved keeps a total of 0. Come back to the amounts at the end.
- **Damage and returns:** they are recorded as negative quantity lines for now, which Hiten says is not the right workflow. The proper record belongs with stock movements (version 3.0). Revisit then.
- **CSV imports** still refuse a quantity of 0 or below (import checker). Not changed in batch 1a-1; decide with the rest.

## Stage 1b – Users and permissions – **DONE 25 Nov 2026**

As written in section 4 (stage 1). Stage 1b batches (opened 24 Nov 2026):

| Batch | What | Visible? |
|---|---|---|
| 1b-1 **built 24 Nov 2026** | Data and rule book only: permission switches for today's screens (`Service/Permissions.cs`), roles, user-role links with an optional end date (temporary cover), pair rules, `Users.IsDisabled`; "may this person do this?" in `Service/Access.cs`. At start-up every existing user gets an Admin role (every switch, including future ones), so nobody loses access. Migration `UsersAndRoles` only adds tables; tried on a copy of real data (users, products, purchases and sales unchanged). 326 tests pass, 329 warnings. | No |
| 1b-2 **built 25 Nov 2026** | Users screen (`/users`, from Settings): add, disable/enable, set a password (the owner sets passwords; no forced change), assign roles with an optional end date (temporary cover). No delete, only disable (owner, 24 Nov 2026). Rules in `Service/UserAdmin.cs`: you cannot disable yourself; the last active Admin cannot be disabled or lose the Admin role. The screen checks "Manage users and roles"; the service itself trusts the screen until 1b-4 enforces permissions everywhere. 340 tests pass; `tools/ui-check/check-users.mjs` and `check-look` pass. | Yes |
| 1b-3 **built 25 Nov 2026** | Roles screen (`/roles`, from Settings): add, rename, change switches, delete. Rules in `Service/RoleAdmin.cs`: names unique ignoring capitals; the Admin role cannot be changed or deleted; a role that anyone holds cannot be deleted (owner, 25 Nov 2026: refuse rather than quietly remove it from people). The pair-rule editor waits until the workflow tasks exist (stage 5): today's switches only open screens, so there is nothing to pair yet. Temporary access is on the Users screen. 351 tests pass; `tools/ui-check/check-roles.mjs` and `check-look` pass. | Yes |
| 1b-4 **built 25 Nov 2026** | Screens and the menu follow the person's roles. One check in the page layout asks `Permissions.ForPath` for every screen, so a typed-in address is caught too; a screen missing from that list opens only for Admin, and a test fails if any screen of the app is unlisted. Without the Dashboard switch the home page shows a welcome line instead. Import buttons follow "Import data". Disabled users cannot log in (told only after a correct password) and see nothing if disabled while logged in. Roles are read again on every screen change. 371 tests pass; `tools/ui-check/check-access.mjs` and every existing browser check pass. | Yes |
| 1b-5 **built 25 Nov 2026** | Rate hiding both ways and three starter roles. Without "See purchase rates": no rate, amount, discount or total on Purchase, Purchase Orders, Bulk Entry there, Supplier totals and the purchase reports. Without "See sale rates": the same on Sales Orders, Sales Summary, Customer and area totals, the sales reports, and the Products rate (the selling price, owner 25 Nov 2026). Hidden figures are left out of the page, and search boxes ignore them. Starter roles Purchase, Sales and Reports are added once by the `StarterRoles` upgrade (a deleted one does not come back). The start-up step now looks for an Admin role rather than any role, so the counter PC still makes everyone Admin after the starter roles appear: tried on a copy of real data (both users Admin; users, products, purchases, sales unchanged). 374 tests pass; `tools/ui-check/check-rates.mjs` and every existing browser check pass. | Yes |

**Stage 1 open questions, asked 24 Nov 2026 (section 9, Q10 and Q12):**
- **Q10 (needed for 1b): answered 24 Nov 2026** – the roles and tasks are in section 2.1.
- **Q12 (needed for 1b/1c): answered 24 Nov 2026** – one person may hold several roles, and the Admin decides per pair of tasks whether one person may do both on the same entry (section 2.1). Still open: who stands in when an approver is away.

## Stage 1c – Who and when (opened 25 Nov 2026)

**Decided with Hiten (Describe step, 25 Nov 2026):**
- **Activity log – what goes in:** every change to a record (create, edit, delete) with **before → after** values, including rate and discount changes (Hiten will use amounts later); users and roles (added, disabled/enabled, password set, roles given/removed, temporary access, role switches); backups (restore, delete, folder change); imports (what and how many rows); logins, successful and failed.
- **Who may read it:** a new switch, "See activity log" (Admin has it automatically). Rate hiding applies inside the log too: someone who cannot see purchase rates never sees a purchase-rate change in it, and the same for sale rates.
- **Kept** until erased. Nobody can edit or delete single entries. **Only the Admin role** gets an "Erase whole log" button (one confirmation; erases everything, leaving no trace – Hiten's choice). No separate switch.
- **Created by / changed by** is recorded on every record but **not shown on the forms** (Hiten: do not crowd the screen). Instead a **"See activity log" button** on Customer, Supplier, Product, Purchase, Sale, Sales Summary and Sales Order (a small history icon per row on Purchase Orders) opens a side panel with that record's full history. Records made before 3.0 show "Not recorded (before version 3.0)".
- **Two people, same record:** the second save is refused with "<name> changed this <record> at <time> while you had it open. Reload to see their changes." One button reloads; what was typed is lost and re-entered. Two people working on *different* records are never affected.
- **Checking pace (Hiten):** automatic tests every batch; one browser check per **pair** of batches; the full browser suite at the end of 1c.
- **Full browser suite for stage 1c, run 25 Nov 2026: all 16 checks pass** (keyboard-only, entry flows, pop-ups and sale, save and focus, Bulk Entry, Alt+B, line rules, import, access, rates, edit clash, activity log panel, look at 1366 / 1920 px light and dark, Activity log screen; users and roles each on a fresh copy). One test copy for the shared checks is prepared by `tools/ui-check/setup-full-suite.py`.

| Batch | What | Visible? | Browser check |
|---|---|---|---|
| 1c-1 **built 25 Nov 2026** | "Created by / changed by / when" on customers, suppliers, products, categories, purchase orders, purchases, sales orders, sales, users and roles, written at every save in one place (`Domain/RecordStamps.cs`); a change to a line, variant, photo, phone number, role switch or user's roles stamps the record it belongs to. Upgrade `RecordStamps` only adds 40 empty columns; tried on a copy of real data: record counts and stock totals unchanged, old records blank. 383 tests pass; 329 warnings. | No | with 1c-2 |
| 1c-2 **built 25 Nov 2026** | "Someone else changed this, reload" on every edit screen, Purchase Orders table, Users and Roles (`Domain/EditClash.cs`, checked inside the save step). A line change counts as a change to its voucher. Saving a record or line someone else deleted now says so (it used to do nothing and look saved). No database change. 399 tests pass (16 new); 329 warnings. Browser check for the pair, `tools/ui-check/check-edit-clash.mjs` (two browsers, Admin and Hiten, on one customer and one purchase): all pass; the stamps land in the database (customer last changed by Hiten, purchase by Admin). Known small thing: the screen's own red "Could not save…" notice repeats the message next to the Reload bar. | Only on a clash | ✔ done (two browsers) |
| 1c-3 **built 25 Nov 2026** | Activity log written inside every save (`Domain/ActivityLogWriter.cs`, table `ActivityLog`, upgrade `ActivityLog` only adds that table): every create / field change (before → after) / delete in plain words, logged on the record it belongs to; rate lines marked purchase or sale; one line per import; logins and refused logins, backup restore / delete / folder change (`Service/ActivityLogService.cs`). A refused save writes nothing; passwords are never written. Tried on a copy of real data: only the new table appears, nothing else changes. | No | with 1c-4 |
| 1c-4 **built 25 Nov 2026** | "See activity log" button and side panel (`Shared/ActivityLogButton.razor`, `MudBlazorComponents/ActivityLogDialog.razor`) on Customer, Supplier, Product, Purchase, Sale, Sales Summary, Sales Order, and a history icon per Purchase Orders row. New switch "See activity log". Owner, 25 Nov 2026, after trying it: lines added to a new purchase or sales order before leaving its screen fold into its one "Created" entry ("2 lines added while creating, 5 pieces", lines behind "show lines"); later, everything one person does to one record in one sitting (same screen, no pause over 30 minutes) is one entry, and more than three lines added in it, one by one or by Bulk Entry, fold the same way; an empty amount and 0 are the same (no "Discount: (blank) → 0"); lines name the product as well as its code. Upgrade `ActivityLogDetail` adds two columns to the log. 420 tests pass (21 new); 329 warnings. Browser check for the pair, `tools/ui-check/check-activity-log.mjs`: all pass (before → after with the person, "older history not recorded" on old records, Esc closes and stays, a purchase discount hidden from a user without purchase rates, the row icon does not start a row edit, no button without the switch, a new purchase made on the screen shows one folded "Created" entry, four lines added one by one to an existing purchase show as one folded entry). | Yes | ✔ done |
| 1c-5 **built 25 Nov 2026** | Activity log screen: a **menu item "Activity log"** shown only with the "See activity log" switch (Hiten, 25 Nov 2026); opens on the **last 7 days**, newest first; filters From / To, Person, Kind, Search; one row per sitting, folding and rate hiding as in the panel; a link to the record if it still exists; 100 rows a page; "Erase whole log" for the Admin role only (one confirmation, no trace). Colours: Hiten chose **C** – a thin stripe per kind of record and a pale name pill per person; after trying it he asked for **coloured record names instead of the stripe** (Sale, Purchase, Customer … each in its own soft colour), kept with the pills (tints by the order users were added, after Admin and Hiten first came out in nearly the same gold), never red, light and dark versions; "if it does not look pretty we will change it". 426 tests pass; 329 warnings; `tools/ui-check/check-activity-screen.mjs` passes (menu, filters, colours light and dark, record link, viewer without rates, no switch = no menu and address refused, erase). | Yes | ✔ + full suite |

---

## 1. Where we are going

One app, one database, many people. Each person logs in and sees only the screens their job needs, on a PC, a tablet or a phone.

- **Purchase side and sales side are separate.** Purchase staff do not see sales, sales staff do not see purchases. Purchase rates can be hidden from sales staff and sale rates from purchase staff. The Admin switches all of this on and off per role; nothing is fixed in the program.
- **Goods inwards** runs in steps, each done by a different person:
  1. Supplier's invoice is entered ("dispatched from supplier").
  2. Goods arrive. The checker counts them and enters what actually came.
  3. Barcode labels are printed from the checker's count at the click of a button, and fixed to the goods.
  4. The purchase manager sees the invoice and the count side by side, with differences marked, and approves.
  5. The goods are put away in their locations. If they go somewhere other than the usual place, that is recorded.
- **Goods outwards** runs in steps too:
  1. An order is taken (touch screen or tablet; the salesman can be anywhere).
  2. The picker gets a list: product, variant, quantity, and where to find it.
  3. The picked goods are scanned to prove the right items were picked.
  4. The order becomes a sale automatically and is approved.
  5. The dispatch team scans again before the goods leave.
- **Stock has a breakdown.** A product shows 100; click it and see 50 in stock, 45 arrived (not yet approved), 5 damaged. Goods can be moved to "damaged" with a reason.
- **Stock has places.** More than one shop or godown, and areas inside each (rack, shelf). One item can sit in several places. Each product has a usual place.
- **Stock checking on a phone.** The checker sees product, variant and the quantity the system expects, ticks what is right and reports what is not.
- **Everyone has a "my work" list.** Each person opens the app and sees what is waiting for them: receipts to count, receipts to approve, orders to pick, orders to dispatch. Without this, work would sit unnoticed between two people.

---

## 2. The big changes underneath

These make section 1 possible. They are also where the risk is.

### 2.1 Permissions
Every screen, every action (approve, delete, correct stock) and every sensitive figure (purchase rate, sale rate) gets a switch. The Admin builds roles from these switches and gives each user one or more roles and the locations they work in. The rules are enforced inside the program, not only by hiding buttons. A hidden rate is not sent to that person's screen at all, so it cannot be dug out.

**Roles agreed 24 Nov 2026 (Hiten).** The tasks below are fixed in the program; which tasks make up a role, who holds it, and which tasks one person may not do on the same entry are all set by the Admin in the app, never hard-coded. A user may hold one role or several. For a single owner now, one person holds all of them; roles can be moved to other people as staff grow, without rebuilding anything.

Tasks:
1. **Supplier invoice entry** – enters the supplier's invoice when goods are dispatched.
2. **Goods receiving** – counts the arriving goods, ideally without seeing the invoice quantities.
3. **Label printing** – prints labels from the receiving count.
4. **Purchase approval** – compares the invoice with the count and approves the purchase.
5. **Order taking** – enters customer orders at the counter, from a salesman, or from a tablet.
6. **Order picking** – collects the items on the pick list.
7. **Pick verification** – scans each picked item to confirm it matches the order.
8. **Sales invoicing** – raises the invoice and approves the sale.
9. **Dispatch check** – scans the items again just before they leave.
10. **Put-away** – assigns arriving stock to its shelf or location.
11. **Stock transfer** – requests moves between locations and shelves.
12. **Transfer approval** – approves those moves (split from 11, so one person can ask and another approve).
13. **Physical stock check** – counts stock on the shelves and reports differences.
14. **Stock adjustment approval** – approves damage and corrections (added; the roadmap already requires it).

**Pair rule.** The Admin can block any pair of tasks from being done by the same person on the same entry (for example goods receiving and purchase approval). Default: nothing is blocked.

**Cover when an approver is away (Hiten, 24 Nov 2026).** Either the Admin gives temporary access to an existing member of staff, or the entry waits in place for the approver to return. Temporary access is switched on and off by the Admin and recorded in the activity record.

### 2.2 Stock becomes a record of movements
Today stock is simply "purchased minus sold". That cannot show "arrived", "damaged" or "which shelf". In future every change of stock is written down as a movement: what, how many, from where, to where, why, who, when. Stock in any state or place is the sum of its movements.

Stock states the plan allows for:

| State | Meaning | Can it be sold? |
|---|---|---|
| Arrived | Counted by the checker, not yet approved | No (unless Hiten decides otherwise, Q5) |
| In stock | Approved and available | Yes |
| Reserved | Promised on an order, not yet picked | No – already promised |
| Picked | Picked and verified, waiting for dispatch | No |
| Damaged | Set aside as damaged | No |
| In transit | On its way between two locations | No |

"Reserved", "Picked" and "In transit" arrive with stages 6 and 3; the screen only shows the states in use.

The existing purchases and sales are converted once into movements. The totals before and after must match exactly for every variant before the change goes live. **This is the riskiest step in the whole roadmap** because the counter depends on these numbers every day.

### 2.3 Entries have stages
A receipt is entered → counted → labelled → approved → put away. An order is taken → picked → verified → sold → dispatched. Each stage records who did it and when, and only the right role can move it on. Entries can also be sent back a step with a reason (the manager returns a count to the checker) or cancelled; nothing disappears silently. Part-deliveries and part-dispatches are allowed for from the start.

### 2.4 The app leaves the single PC
First to other PCs, tablets and phones on the shop network. Later to salesmen and other locations over the internet. That needs a secure connection, stronger login rules and a decision on where the app lives (section 6).

---

## 3. How it will be built (structure and safeguards)

No software is error-free. The aim is that mistakes are caught before they reach the counter, and that a mistake that does get through cannot damage the data.

### 3.1 Structure: three layers
1. **Screens** (PC, tablet, phone, later the salesman's app). They only show things and collect input. They hold no business rules.
2. **The rule book** in the middle. Every rule lives here once: who may do what, which stage may follow which, how stock moves, how totals are worked out. Every screen goes through it, so a phone cannot do what a PC is not allowed to do.
3. **The database**, with its own last-line rules (unique IDs, no stock line without a real product, and so on) in case layers 1 and 2 ever fail.

Today some rules still sit in the screens. **Before stage 1 adds new screens, those rules move into the middle layer** – a tidy-up with nothing visible changing. When the salesman's app comes (stage 7), it gets a "door" into the same rule book instead of its own copy of the rules.

### 3.2 Design rules
- **Stock movements are never edited or deleted.** A correction is a new movement that reverses the old one. The full history always exists and stock can always be re-added from scratch.
- **Stages are a fixed list with fixed allowed steps** (for example counted → approved, never entered → approved). The rule book refuses anything else and records who moved it and when.
- **Permissions are checked in the rule book**, not by hiding buttons.
- **One place for each thing:** one list of permissions, one list of stages, one stock calculation. Screens and reports all read the same one.
- **Additions, not rewrites.** New features are added beside the working screens. A working screen is replaced only when its replacement has been in use and trusted.
- **Every new feature can be switched off** by the Admin for a role, so a problem in a new flow never stops the counter.

### 3.3 Data clashes (several people at once)
- **Two people change the same record:** the second one to save is told "someone else changed this, reload" instead of silently overwriting.
- **All or nothing:** each action (approve a receipt, complete a pick) is saved completely or not at all. No half-saved entries.
- **Two people after the same stock:** stock is checked again at the moment of saving, inside the same all-or-nothing step, so the last piece cannot be promised twice.
- **Two people take the same job:** when a picker or checker opens a job it is marked as theirs; others see "being done by …".
- **Double taps and weak signal:** a repeated "save" of the same entry is recognised and saved once.
- **Numbers** (voucher, order, receipt) are given by the database, never by a screen, so two users can never get the same one.
- **One clock:** all dates and times come from the main PC, not from each phone.

### 3.4 Catching errors
- **Automatic tests** for every rule (216 today; each stage adds its own). They run on every change.
- **Browser checks** for screens and keyboard/scanner behaviour, as today; phone-sized checks are added in stage 4.
- **Nightly self-check:** the app adds up all movements and compares with the stock figures; any difference is logged and shown to the Admin.
- **Parallel run for stage 2:** the new stock calculation runs next to the old one for a period, and both must agree for every variant before the old one is switched off.
- **Trial on a copy of the real data**, then a small pilot with one or two users, before everyone uses it.
- **Backup before every upgrade**, and the previous version kept so we can go back the same day.
- **Plain messages:** when something is refused, the person is told why in ordinary words; the technical detail goes to the log.

### 3.5 Speed
- Running stock totals are kept up to date as movements are saved, not re-added from the whole history each time (and verified by the nightly self-check).
- Lists load a page at a time; phones get small photos and only the columns they need.
- "My work" lists refresh by themselves; nobody has to press reload to see new work.
- The database choice is reviewed at stage 7 (section 6.3).

### 3.6 Security once more than one device connects
- Secure (encrypted) connection; one account per person, no shared logins; lock-out after repeated wrong passwords; automatic sign-out when idle on shared devices.
- The Admin can disable a user or a lost phone at once.
- A record of sensitive actions (approvals, stock corrections, permission changes, deletes), with a screen for the Admin to read it.
- Backups copied off the main PC automatically, and a restore practised at least once before stage 1 goes live.

### 3.7 Steps inside every stage
1. **Describe** – Hiten explains the real-world work; the stage's open questions (section 9) are answered here.
2. **Mock-up** – pictures of the new screens to agree before anything is built.
3. **Design** – data and rules worked out; anything that affects the business explained in plain English.
4. **Build** in small batches, each with its tests.
5. **Check** – automatic tests, browser checks, review.
6. **Trial** – Hiten tests on a copy of the real data.
7. **Pilot** – one or two real users for a few days.
8. **Go live** – backup, install, old version kept. A one-page "how to" for each role.
9. **Watch** – a week of use, fix what comes up, review this roadmap, then the next stage.

---

## 4. Stages

**Rule:** each stage goes live and is used before the next starts. Never again several months of unreleased work.

**Size guide:** Large = comparable to several of the earlier batches together. Medium = one or two. The whole roadmap is more work than everything done on the app so far.

### Stage 0 – Go live with today's version (Hiten) – **DONE**
- Version 2.0 is checked, pushed and tagged `v2.0` (14 Nov 2026).
- **Installed on the counter PC on 16 Nov 2026** (version 2.0 with the backup-folder addition). In daily use since, working as Hiten wants (confirmed 24 Nov 2026).
- The exact code now running is tagged `live-16-nov-2026`. **Before installing any stage of this plan on the counter PC: copy the live database first.** An older build cannot open a database a newer build has upgraded, so the tag alone is not a way back - see `README.md` section 10, "Going back".
- **Done when:** the counter PC runs the current version for a week without problems. *(Met.)*

### Stage 1 – Foundation: users, permissions, shop network (Large)
- **1a. Tidy-up (nothing visible):** move the remaining rules from screens into the rule book, and fix the known weak spots that sit in the same code on the way (details in `README.md`, known issues):
  - the line form on Sale / Purchase / Sales Order accepts quantity 0 or negative and discount over 100, and a very fast double Enter could add a line twice;
  - a voucher with a discount but no lines gets a negative total;
  - a sale that is never summarised keeps a stored total of 0;
  - "not found" returns an empty record instead of nothing;
  - some error messages show technical text.
- **1b. Users and permissions:** user management screen (add, disable, reset password); roles built from switches, a user may hold several roles, the Admin sets the pair rule (section 2.1; default none blocked); menus and screens follow the role; purchase/sales split; rate hiding both ways; users tied to locations (one location for now).
- **1c. Who and when:** "created by / changed by / when" on every record; "someone else changed this" protection; the Admin's activity record.
- **1d. Shop network:** the app opens from other PCs on the shop network over a secure connection; lock-out, idle sign-out; the pinned duplicate-voucher-number decision is settled here, because numbers must now be given by the database.
- **Done when:** a purchase user and a sales user work at the same time on two PCs, each seeing only their side, and the Admin can change what a role sees without a programmer.

### Stage L – Label printing (Medium; can be done any time after stage 1)
- A screen to print labels for a product variant and a quantity, in Hiten's existing QR format, at one click.
- Later reused by goods inwards (stage 5).
- Needs the printer details first (Q1).
- **Done when:** a day's labels are printed from the app instead of by hand.

### Stage 2 – Stock breakdown and damage (Large; the riskiest)
- Stock recorded as movements; old purchases and sales converted.
- Click a stock figure to see In stock / Arrived / Damaged.
- Screen to move goods to damaged, back, or write them off, with a reason.
- Stock corrections with a reason and an approval.
- Parallel run and nightly self-check (section 3.4).
- **Done when:** old and new stock figures agree for every variant for the whole parallel-run period, and reports show the same numbers as before.

### Stage 3 – Locations (Medium)
- Shops/godowns and the areas inside them; a usual place for each product variant.
- Stock shown per place; moving stock between areas and between locations (with "in transit").
- Users limited to their locations.
- Existing stock is placed in one starting location; areas are filled in gradually (or by a first stock check in stage 4).
- **Done when:** every variant's stock by place adds up to its total, and a move between two places shows correctly at both.

### Stage 4 – Stock checking on a phone (Medium; the first phone screen)
- A check list by area: product, variant, expected quantity; tick or enter the real count.
- Differences go to a manager; stock changes only when approved.
- Scanning with the phone camera or a hand scanner (Q11).
- Also the trial that tells us how phones behave on the shop Wi-Fi (feeds section 6.2).
- **Done when:** one full area is checked on a phone and its differences approved, without paper.

### Stage 5 – Goods inwards (Large)
- Supplier invoice entry; optional match to the Purchase Order (Q8; Purchase Orders must first point to real suppliers and products – today they hold free text).
- Checker's count on tablet or phone, blind if agreed (Q3); short, excess and damaged-on-arrival recorded.
- Labels printed from the count (stage L).
- Manager's side-by-side comparison; approve, send back, or approve with differences noted.
- Approval creates the Purchase and moves stock from Arrived to In stock.
- Put-away list with usual places; a different place can be recorded.
- "My work" lists for each role.
- **Done when:** a real delivery goes from invoice to shelf through four different logins, and the Purchase and stock are right at the end.

### Stage 6 – Goods outwards (Large)
- Order entry on a touch screen inside the premises.
- Ordered stock is reserved; the pinned "selling more than in stock" decision is settled here.
- Pick list ordered by location; picker marks picked or short (Q6).
- Scan to verify the pick; mismatches shown at once.
- Automatic Sale from the verified order; approval; dispatch scan; part-dispatch allowed for.
- Counter sales continue beside this if wanted (Q2).
- **Done when:** a real order goes from tablet to dispatch through the scans, and a deliberately wrong pick is caught.

### Stage 7 – Outside the premises (Medium to Large, depending on section 6)
- Decide where the app lives (6.1), how the salesman's screen is built (6.2) and the database (6.3).
- Salesman's screen: stock in hand, photos, new order, from anywhere. The pinned "several photos per variant" decision is settled here.
- Other locations connected.
- **Done when:** a salesman outside takes an order on a phone with a weak signal and it reaches the picker once, complete.

---

## 5. Today's screens and the items carried over from version 2.0

**Screens**
- **Purchase** stays, and becomes the result of an approved goods-inwards entry. Direct purchase entry stays for a role that is allowed to use it (useful while the new flow is being introduced, and as a fallback).
- **Sale** stays for the counter. A sale created from an order arrives ready-filled.
- **Purchase Orders and Sales Orders** stay. Sales Orders gain the picking stages.
- **Reports** keep working; stock reports gain the breakdown and the location. Each report gets a permission.
- **Import, backups, settings** become Admin-only by default.
- **Keyboard and scanner behaviour at the counter does not change** unless Hiten asks.

**Where the items left open in version 2.0 now belong** (each is described in `README.md`)

| Item | Now in |
|---|---|
| Users and roles | Stage 1b |
| Created / changed time stamps; two people editing the same record | Stage 1c |
| Old duplicate voucher numbers (pinned: keep old numbers and exempt them, or renumber) | Must be decided in stage 1d |
| Secure connection (HTTPS) | Stage 1d |
| Line-form checks, double-save guard, empty-voucher total, unsummarised sale total, "not found" handling, technical error text, rules still in screens | Stage 1a |
| Stock adjustments with a reason | Stage 2 |
| Purchase Orders hold free-text supplier / product / variant | Stage 5 |
| Selling more than in stock (pinned; earlier advice: warn and show stock, do not block) | Must be decided in stage 6 |
| Several photos and videos per variant (pinned) | Must be decided in stage 7 |
| Database allows one writer at a time | Section 6.3 |
| Import: open to every user; adds only, never updates | Stage 1b (Admin-only by default); updating by import only if asked |
| Silent failures on the order lists and on Sale, case-sensitive report searches, report period edges, housekeeping of old safety copies | Small fixes unrelated to this plan; nothing due; any time with Hiten's yes |

**Declined by Hiten – do not offer again:** the extra keyboard offers and the Sales Orders "Set ticked to" offer (13 Nov 2026); changing Bulk Entry so that a row not added with Enter is saved; fixing the 333 build hints (rule stays: no new ones).

---

## 6. Technology decisions for later (not needed until the stage arrives)

1. **Where the app lives once salesmen and other locations connect (stage 7).**
   - **A. Stays on the main PC in the shop, reached through a secure private link.** Your data stays with you; the shop keeps working when the internet is down. Outside users depend on that PC and the shop's internet being up. *Recommended to start.*
   - **B. Moves to a rented server on the internet.** Reachable from anywhere, professionally kept running. But every location, including the counter, then needs working internet to sell, and there is a monthly cost.
   - Stages 1–6 work the same either way.

2. **The salesman's screen.** Inside the premises the same app works on a tablet. On mobile internet with a weak signal the current kind of screen can freeze and reconnect. The salesman's screen will probably be built in a lighter way that survives a weak signal and holds an order until the signal returns. Decide at stage 7, using what stage 4 showed.

3. **The database.** The current one (SQLite) is fine for 6–7 people in one building. Once several locations and outside salesmen write to it all day, moving to a larger database (PostgreSQL) is the safe choice. The app is built so this is a contained job. Review at the start of stage 7, or earlier if people ever see "busy, try again".

4. **Barcode printing.** The method depends on the printer and how it is connected (Q1).

---

## 7. Things outside the software (Hiten's side)

- **Main PC:** once others depend on it, it must stay on during working hours. A battery backup (UPS) and a backup disk or second place for backups are strongly advised from stage 1.
- **Wi-Fi:** phones and tablets need coverage in every aisle and godown corner where they are used (stage 4 onwards).
- **Devices:** tablets/phones for checkers, pickers and order takers; scanners where the phone camera is too slow (Q11).
- **Label printer:** details needed for stage L.
- **Fallback:** agree what staff do on paper if the main PC or network is down for an hour, and who enters it afterwards.
- **People:** each new flow changes someone's daily work. A short hand-over per role at go-live (step 8) and a named person staff report problems to.
- **Internet link and possibly a small monthly cost** from stage 7.

---

## 8. Risks

| Risk | How it is kept small |
|---|---|
| Stock numbers go wrong when stock becomes movements (stage 2) | Parallel run, nightly self-check, trial on real data, same-day rollback |
| Too much built before anything is used | One stage live before the next starts |
| A new flow blocks daily selling | Old screens stay; new features can be switched off per role |
| Staff skip steps or share logins | Stages enforced by the rule book; one account per person; "my work" lists; activity record |
| Main PC fails | UPS, automatic off-PC backups, practised restore |
| Phones unreliable on Wi-Fi or mobile signal | Small phone trial in stage 4 before the big flows depend on it; lighter salesman screen in stage 7 |
| Data opened to the internet | Nothing leaves the shop network until stage 7; secure link, lock-out, per-user access |
| The plan is wrong somewhere | Reviewed at the start and end of each stage |

---

## 9. Open questions for Hiten

**Parked until each stage starts (Hiten, 14 Nov).** The stage in brackets is when the answer is needed; they are asked in that stage's "Describe" step (section 3.7).

1. **Labels (stage L):** printer make and model, and how it is connected (USB to which PC, or network)? What exactly is inside the QR code and printed on the label? Is it one identical label per variant, or does every single piece get its own number?
2. **Counter (stage 6):** do walk-in counter sales continue as today, next to the order → pick → dispatch flow?
3. **Checker's count (stage 5):** should the checker count "blind" (without seeing the invoice quantities)? Recommended yes: it is the only way the count is a real check.
4. **Damage (stage 2):** what happens to damaged goods afterwards: returned to the supplier, repaired and put back in stock, sold cheaper, thrown away? Who may move goods to damaged?
5. **Arrived goods (stage 5):** can goods that have arrived but are not yet approved be promised on an order, or only approved stock?
6. **Short picks (stage 6):** if the picker finds fewer than ordered, does the order go out short, wait, or go back to the salesman to decide?
7. **Locations (stage 3):** how many shops/godowns to start with, and are they on the same network or far apart? Do goods move between them?
8. **Purchase Orders (stage 5):** should the supplier's invoice be matched against the Purchase Order you placed?
9. **Returns (later):** goods returned by customers and goods sent back to suppliers are not in this plan yet. Needed?
10. **Roles (stage 1): answered 24 Nov 2026** – see section 2.1. The people in each role will be set in the app.
11. **Scanning on phones (stage 4):** phone camera, or hand scanners paired with the phone/tablet?
12. **Approvals (stage 1): answered 24 Nov 2026** – the Admin decides per pair of tasks whether one person may do both on the same entry (section 2.1). Cover when an approver is away: the Admin gives temporary access to existing staff, or the entry waits in place (section 2.1).
13. **Differences on arrival (stage 5):** when the count is short or over the invoice, what happens in the business – pay as invoiced, pay as counted, claim from the supplier?

---

## 10. Decided (14 Nov 2026)

- Version 2.0 is concluded (14 Nov 2026): nothing further is due on it. It goes on the counter PC before any of this is built.
- This is a plan only; building starts stage by stage on Hiten's word.
- Salesmen can be anywhere. About 6–7 people at the same time.
- Received goods are counted separately ("arrived") until approved; damaged goods are counted separately.
- Rate visibility is controlled by the Admin in both directions, never fixed in the program.
- Multiple locations, with areas inside them; one item can be in several places; each product has a usual place and a different actual place can be recorded.
- A picked and verified order becomes a sale automatically; scanning is the check at picking and again at dispatch.
- Barcode labels are printed from the app from the checker's count, in the existing QR format.
- The open questions wait until the stage that needs them.

## 11. Not in this plan (say so if any is wanted)

Accounts and payments (who owes what), tax/GST invoices, customer and supplier returns (Q9), price lists per customer, messages to customers (SMS/WhatsApp), a public online shop, payroll or attendance.

## 12. Words used

- **Role** – a named set of permission switches (e.g. "Checker").
- **Movement** – one written record of stock changing state or place.
- **Stage** – where an entry is in its flow (counted, approved, picked …).
- **Rule book** – the middle layer of the program that holds every business rule once.
- **Parallel run** – old and new calculation running together to prove they agree.
- **Pilot** – a few real users trying a stage before everyone does.
- **My work list** – the list of jobs waiting for the person who is logged in.
