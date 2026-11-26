# Plan for version 3.0 – from one counter PC to a multi-user, multi-location system

**Status:** agreed direction, 14 Nov 2026 (session 10); **next: stage 3 (locations), to be opened in a fresh chat** (26 Nov 2026). **Built so far:** stage 0 (live 16 Nov 2026), stages 1a, 1b, 1c and L-1/L-2 (25 Nov 2026) – **installed: the counter PC runs version 3.0** (Hiten, 26 Nov 2026; the package made 25 Nov 2026 from commit `f910f58`). 1d parked while there is one PC; L-3 waits for the sticker printer. **Stage 2** (stock breakdown and damage): 2-1 and 2-2 pushed (`d66af1f`), 2-3 and 2-4 pushed (`663098e`), 2-5 and 2-6 pushed 26 Nov 2026, 2-7 and 2-8 pushed 26 Nov 2026 – **stage 2 complete**; not installed. See "Where we are" below.
**Versions:** 1.0 = the May 2026 build. 2.0 = the current app, concluded 14 Nov 2026 and described in `README.md` (what it does, how it is built, how to run, test and install it, known limits). 3.0 = this plan.
**How this fits:** `README.md` explains the app as it is. This file is the only plan. Nothing is due on version 2.0. When a stage starts, its open questions (section 9) are asked and its batches are written into a section for that stage near the top of this file (see stage 1, L and 2).
**Written for:** Hiten (plain English) and any future session picking this up.
**This plan will change.** It is reviewed at the start of every stage; what we learn in one stage may reorder the next.

**Contents:** 1 Where we are going · 2 The big changes underneath · 3 How it will be built · 4 Stages · 5 Today's screens and old open items · 6 Technology decisions for later · 7 Things outside the software · 8 Risks · 9 Open questions (parked) · 10 Decided · 11 Not in this plan · 12 Words used

---

# WHERE WE ARE (26 Nov 2026, end of session 14) – read this first

**On the counter PC:** version 3.0 – the package made 25 Nov 2026 from commit `f910f58` (stages 1a, 1b, 1c, L-1, L-2), installed by Hiten (confirmed 26 Nov 2026). **Stage 2 is pushed but not installed.**

**Done and pushed (all on `main`, last commit `4013c7d`):**
- **Stage 1a** (rules in one place) – line rules (negative quantities allowed, zero refused), double-Enter guard, "not found" and plain messages (partly). 1a-3 parked.
- **Stage 1b** (users and permissions) – Users and Roles screens, screens and menu follow the role, rate hiding both ways, three starter roles.
- **Stage 1c** (who and when) – record stamps, "someone else changed this, reload", the activity log (panel per record and the Activity log screen).
- **Stage L, L-1 and L-2** (labels) – A4 sheets of 65, named label set-ups, labels from a purchase or a product.
- **Stage 2** (stock breakdown and damage) – **complete**, see "Stage 2" below: stock changes beside purchases and sales (nothing old converted); stock in hand = sellable pieces with damaged behind an arrow; Record damage on purchases and sales returns (and lowering it again); Sales Returns with Given in exchange; the damage report (supplier, status, Repaired, Written off, Sent, "See activity log"); Purchase Returns; the Stock changes screen (damage in the shop, count corrections, write-offs, opening stock) and the reasons list; approval (Admin's switch in Settings, off; "Waiting for approval"). Commits `d66af1f`, `663098e`, `d38a114`, `4013c7d`.

**Counts now:** build 0 errors / 329 warnings (baseline was 333); **525 tests** pass; **full browser suite for stage 2 (21 checks) passed 26 Nov 2026** (`tools/ui-check/run-full-suite.sh`).

**Now (26 Nov 2026, session 15): stage 3 – Locations – being built.** Describe step and PC mock-ups done with Hiten; answers and batches 3-1 … 3-7 are in the "Stage 3" section below. **3-1 and 3-2 pushed 26 Nov 2026** (`fe3cfa0`; `check-places` and `check-labels` run afterwards: pass). **3-3 and 3-4 pushed 26 Nov 2026** (with Hiten's later changes: purchase return "From", only the Admin changes a saved place, Variant Report split per place – those last changes were built but not tested, at Hiten's request). Next 3-5 + 3-6 (moves, receiving, missing pieces). Before the end-of-stage full suite: fix `setup-stock-figures.py` (its stock change lines need a place) and `check-access.mjs` (wait for its first test browser to close before opening the next). The questions below were the Describe step's starting list. Installing stage 2 on the counter PC stays open for whenever Hiten chooses (recommended soon: it is a large change best used for a while; steps under "Install rule" below).

**Stage 3 – questions for the Describe step** (section 4 "Stage 3" and Q7; ask in plain English, a few at a time, with a suggestion each):
1. How many shops and godowns are there now, and are they in the same building / on the same network? Do goods move between them?
2. Areas inside each place: rack only, or rack and shelf (and how are they named or numbered today)?
3. A usual place for each product variant: one per variant, or one per variant per shop/godown?
4. Purchases and sales: which place does a purchase arrive in and a sale leave from – one default place per screen or user, or chosen on each voucher?
5. Moving stock between areas and between places: who may do it, does it need approval (the stage 2 approval switch can cover it), and should goods between two buildings show as "in transit"?
6. Starting point: put all existing stock in one starting place (for example "Main shop") and fill in areas gradually or with a first stock count (stage 4)?
7. Should users be limited to their places (a godown person sees only the godown)?
8. Damage, returns and the damage report per place?
Design note for stage 3 (same "way B" as stage 2): stock stays worked out, never stored – each entry (purchase, sale, stock change, and a new transfer entry) carries the place it happens in; old entries get the starting place; `Domain/StockQuery.cs` adds up per place. Read "Stage 2" below and README section 4 ("Stock query", "Stock changes …", "Damage report …", "Stock changes screen …") before designing.

**Parked (Hiten's decision, comes back later):**
- **1d – shop network:** parked 25 Nov 2026 ("I only have 1 system as of now"); back when a second PC, tablet or phone is added.
- **Amounts and totals** (1a-3; returns and exchanges carry quantities only for now), and CSV imports refusing quantity ≤ 0.
- **L-3 – sticker printer settings:** when Hiten has the sticker printer (make, model, label size).
- **Labels tied to purchases** (extra labels only with permission): with goods inwards, stage 5.
- **Import of purchases and places** (Hiten, 26 Nov 2026: "we will need to make changes to the import of purchases later"): today an imported purchase (opening stock) counts in the shop; to be changed later (for example a Place column in the file).
- **Year-end close** into a fresh database (see "To do – Year-end close"; first close would be 31 March 2027).
- Offers not yet answered: an Alt + letter for Sales Returns / Purchase Returns / Stock changes / Damage report (none chosen; Alt+N and Esc work); whether the starter roles Purchase and Sales get the new stage 2 switches (today only Admin has them).

**Install rule:** copy the live database before installing anything (README section 10). The upgrades not yet on the counter PC: `StockChanges` (stage 2-1: new tables and two indexes) and `StockApproval` (2-8: a column and a table); both only add, and 2-1 was tried on a copy of real data (every item's stock identical). An older build cannot open an upgraded database. Steps: full browser suite on a test copy; copy the live database (the counter PC is this PC: live 3.0 runs from `H:` on port 5000 – never touch `H:` or stop it without Hiten's yes); try the upgrade on that copy and compare every item's stock before and after; make the package; Hiten installs; smoke test at the counter.

**Working agreements with Hiten:** plain English, no jargon; he answers questions and says **"push"** to approve – nothing is committed or pushed before that. Show mock-ups (ASCII is fine) before building a new screen. Build step by step or two batches at a time (Hiten, 25 Nov 2026). Tests every batch; one browser check per two batches; the full browser suite once per stage and before any install. Senior-developer role: honest opinions and a recommendation; delegate large batches to a cheaper agent with a precise brief and review its work, or do it directly when Hiten asks.

---

# Stage 1 – Foundation (opened 24 Nov 2026; 1a, 1b, 1c done; 1d parked)

Stage 0 is done (version 2.0 live on the counter PC since 16 Nov 2026). Hiten opened stage 1 on 24 Nov 2026 and asked that it be done **in the order written**: 1a, then 1b, 1c, 1d.

**Baseline taken before any stage 1 code was written (24 Nov 2026):** build 0 errors / 333 warnings; 294 tests pass. Any later failure is stage 1's, not inherited.

**Rollback:** the counter PC runs version 3.0 (package from commit `f910f58`); version 2.0 is tagged `live-16-nov-2026`. Before anything newer is installed there, a copy of the live database is taken first (README section 10, "Going back"). Hiten confirmed on 24 Nov 2026 that he also keeps his own database backups in two physical places away from the running drive.

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

## Stage 1c – Who and when – **DONE 25 Nov 2026**

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


# Stage L – Label printing (opened 25 Nov 2026, before stage 1 is installed: Hiten chose to build another stage first) – **L-1 and L-2 DONE 25 Nov 2026**; L-3 waits for the sticker printer

**Describe step (Hiten, 25 Nov 2026):**
- **Today:** a laser printer, A4 sheets of pre-cut labels, labels typed in Word (file `1 NO HAATICHAP AJ0120103 GANESH.docx`, given by Hiten). Measured from it: A4, **5 × 13 = 65 labels of 40 × 21 mm**, top margin 12 mm, left about 4 mm; font Calibri.
- **One label:** product name bold 8 pt, centred on the left; variant name 10 pt underlined below it; on the right a **QR code 12.5 mm** square with the product ID in 7 pt under it.
- **QR content: Product ID, Tab, Variant ID** – the scanner types it into the Sale screen (product box, Tab to the variant box). Must stay exactly this.
- **One identical label per product + variant**, as many copies as needed.
- **A4 sheets are temporary:** Hiten will move to a dedicated barcode sticker printer; the layout is kept in one place so it can be added later. (First no "start at an empty label" feature; after the first printer test Hiten asked for it after all, with labels filling from the bottom.)
- **"Print labels" from a purchase:** later, once the labels are right (Hiten).

| Batch | What | Visible? |
|---|---|---|
| L-1 **DONE 25 Nov 2026** (pushed `5dd356d`) | Labels screen at `/labels` (menu place decided in L-2): pick product, variant or "All variants", copies; a list; Print opens the browser's print window with A4 sheets laid out as the Word file. QR made in the app by QRCoder 1.6.0 (MIT, offline; Hiten agreed). New switch "Print labels". 435 tests pass (9 new); 329 warnings; `tools/ui-check/check-labels.mjs` passes (sheet 210 × 297 mm, labels at 4 / 12 mm, 40 × 21 mm, QR 12.5 mm). After Hiten's first printer test (photo, 25 Nov 2026): the content sat at the very top of each cut label – moved 1.8 mm down inside the label (the sheet itself was right); copies editable in the list; a sheet fills from the bottom-right backwards, with "Already used on this sheet" for part-used sheets (Hiten changed his mind on this: needed until the sticker printer comes). 439 tests pass; check-labels passes. After the second printer test (photo): content a little bigger (name 9 pt, variant 11 pt, QR 13 mm, ID 7.5 pt), 2.2 mm down and 0.5 mm left; the page remembers where the last print ended on this PC (Hiten: 10, then 20 → 30 used; then 40 → 5 on a new sheet) – pressing Print counts as printed, no question afterwards (Hiten removed it; the "Already used" box is corrected by hand if a print fails); Enter in Copies adds the line and goes back to Product. Then (Hiten): Enter moves Product → Variant → Copies, and the boxes empty after a line is added; **sizes and margins are no longer fixed in the program but kept as named label set-ups** ("A4 65 Label" first; Hiten creates new ones and switches between them; Label settings page behind the new switch "Change label settings"; upgrade `LabelLayouts`). 449 tests pass; check-labels passes. Tests and check-labels were run before the push. **Open: Hiten's third printer test and the scan test.** | Yes |
| L-2 **DONE 25 Nov 2026** (pushed `19ee5ed`) | "Print labels" button on a purchase (one label per piece received). Hiten, 25 Nov 2026: one line per purchase line as entered (the same item twice stays twice), copies = quantity, minus lines left out; also Alt+L on a purchase. With it: "Labels" in the menu under Masters after Products (Alt+L); a label icon next to each variant's reorder level on the product page (product and variant chosen, cursor in Copies); Ctrl+P on the Labels page presses its Print button; "Already used on this sheet" kept in the database (table LabelSheetUse); every print written to the activity log (a purchase's print in its panel). After trying it (Hiten): **Esc on Labels goes back to the purchase or product it was opened from** (from the menu still to the Dashboard). 455 tests pass; 329 warnings; `tools/ui-check/check-labels.mjs` passes (menu place, Alt+L, product icon to Copies, a purchase's lines, Ctrl+P, count kept after reopening, print in the purchase's panel, Esc back); keyboard checks still pass. Upgrade `LabelSheetUse` only adds a table. | Yes |
| Future (Hiten, 25 Nov 2026) | **Labels tied to purchases:** in the future workflow labels can only be printed for what a purchase (later: the checker's count) brings in; **extra or repeat labels need permission from someone higher up**. Reason: duplicate labels, theft, and a cheaper product's label stuck on a dearer one. Groundwork already in place: the "Print labels" switch and every print in the activity log. | With goods inwards (stage 5) |
| L-3 | **Label printer settings (Hiten, 25 Nov 2026):** this module as built is for **A4 ready-label sheets**. A Settings page is to let the Admin switch the A4-sheet printing off and choose a different printer with its own settings – first the dedicated sticker printer (needs its make, model and label size); the layout is already kept in one place (`LabelSheetLayout`) for this. | When Hiten has the sticker printer |


# Stage 2 – Stock breakdown and damage (opened 25 Nov 2026) – **DONE 26 Nov 2026** (pushed, not installed)

**What the data shows today (copy of real data, read only, 25 Nov 2026):** supplier "CAPITAL" stands in for things that are not purchases – opening stock, customer returns ("RAJA BABU RETURN" …) and 442 minus lines with no reason (stock corrections). 175 minus lines on sales are customer returns ("RETURN GOODS", "EXCHANGE OF GODS"). So a customer return is recorded two different ways, and most corrections carry no reason.

**How stock is worked out (kept):** stock is never stored; it is added up every time from the entries (today purchased − sold). Hiten asked whether this is good and whether it holds with many devices and locations: yes – it cannot drift, it extends to locations by recording "where" on each entry, and the "last piece sold twice" risk is handled by checking stock inside the save (stage 6), as with any method. Timed on the copy of real data: all items 0.4 s, one item 0.006 s; with a lookup index on (variant, quantity) about 35 times faster (0.01 s / 0.0002 s). Stored running totals or a yearly closing figure only if, years from now, the full report gets heavy.

**Decided with Hiten (Describe step, 25 Nov 2026):**
- **Way B – nothing old is converted.** Purchases and sales stay exactly as they are and stay the record of what came and went. A new kind of entry, **"Stock change"**, is added beside them. Stock = purchased − sold ± stock changes. Today's figures cannot move, and no parallel run is needed. (The old plan – copy all 45,000 lines into a new movements book – is dropped.) Later stages add their own kinds of entry the same way (transfers in stage 3, receipts in stage 5).
- **Kinds of stock change (all happen in the shop):** set aside as damaged; repaired, back to stock; written off (thrown away or lost); count correction (found more / found fewer); customer return; supplier return; opening stock.
- **Damaged goods afterwards:** repaired (back to stock) or sent back to the supplier.
- **Customer returns and supplier returns** get their own proper entries (no more CAPITAL purchases or minus sale lines for them). Amounts stay parked, as agreed in stage 1.
- **Reasons:** a fixed list the Admin can edit, plus a note.
- **Approval:** built in, with a switch; **switched off** for now (a stock change counts at once). When switched on, nothing moves until it is approved, and the waiting entry is shown as "waiting for approval".
- **Old entries** (CAPITAL lines, minus sale lines): left untouched.
- **What the stock figure shows:** the **sellable** pieces, with damaged pieces counted **separately** beside it ("13 + 3 damaged"); clicking or expanding a stock figure shows the breakdown. A separate stock breakdown report in Reports.
- **Speed:** the lookup index for stock is added in the first batch, and the new stock change entries get the same index from day one.

**Decided with Hiten (mock-up step, 25 Nov 2026):**
- **Damage is recorded where it is found.** On a saved purchase, one **"Record damage"** button (in the totals bar, not one per line: the line table is already crowded) opens a pop-up listing **every line of that purchase** (product, variant, quantity received, already damaged) with an empty "Damaged now" box per line; fill in any lines and save once – all damages of that purchase in one go. It makes one stock change entry linked to the purchase and its lines (waiting for approval when the switch is on). The same pop-up is used on a Sales Return.
- **Damage report = the list of what is lying in damage**, one row per damaged lot until it is dealt with (product, variant, quantity, date, where it came from, reason, supplier, status). Actions on ticked rows: **assign supplier** (filled in automatically when the damage came from a purchase), **Ready to send**, **Sent** – which makes a **Purchase Return** voucher and takes the pieces off the damaged count – **Repaired** (back to sellable), **Written off**.
- **One Purchase Return per supplier per sending**, holding everything ticked together. A purchase return can also be made directly (good stock sent back: comes off sellable).
- **After a purchase return nothing is tracked.** A replacement or repaired piece that comes back is simply a new purchase.
- **Sales Return** is its own voucher (customer, lines; damaged pieces recorded with the same pop-up).
- **The approval switch covers every stock change**, Purchase Returns and Sales Returns included: when it is on, stock waits for approval.

**Defaults proposed (25 Nov 2026; Hiten may change):** reason per line in the damage pop-up, defaulting to one chosen at the top; a damaged quantity cannot exceed what that line brought in minus what is already damaged; Enter moves down the "Damaged now" column, Ctrl+S saves, Esc asks first when quantities were typed; returns carry quantities only for now (amounts parked); approval offers Approve or Reject (a rejected entry stays in the activity log, stock never moved); new switches: record damage / stock changes, sales returns, purchase returns, approve stock changes, damage report, change the reasons list and the approval switch (Admin by default).

**Batches (proposed 25 Nov 2026; each: its own tests, build clean, browser check per two batches):**

| Batch | What | Visible? |
|---|---|---|
| 2-1 **built 25 Nov 2026** | Groundwork: the stock lookup index, the stock change tables (`StockChanges`, `StockChangeLines` – a damaged lot is a line moved into Damaged, with its return supplier and "ready to send" – and `StockChangeReasons` with six starting reasons), stock = purchased − sold ± approved changes, split into sellable, damaged and waiting (`Domain/StockQuery.cs`, the one place). Database rules: a line must be a real product and variant, more than zero pieces, and a real move. Products, variants, customers and suppliers with stock changes cannot be deleted. The approval switch's own setting comes with 2-8. Upgrade `StockChanges` tried on a copy of real data: all 693 variants' stock identical (12,429 pieces), no old table changed by a single row; adding up all stock 374 ms → 23 ms. 470 tests pass (15 new); 329 warnings. | No |
| 2-2 **built 25 Nov 2026** | The stock figure on the product page and the Variant Report (rows and total) shows the sellable pieces; when there are damaged or waiting pieces a small arrow sits beside the number, and only a click opens Sellable / Damaged / Waiting approval (Hiten, after trying the first version with "+ 3 damaged" written beside it) (`Shared/StockFigure.razor`, one component). Waiting pieces have not moved, so they are still in stock in hand. The Variants List report gains a Damaged column; the Reorder Level report does not (Hiten) and compares the sellable pieces. 474 tests pass; `tools/ui-check/check-stock-figures.mjs` (setup `setup-stock-figures.py`) and `check-look` pass. | Yes |
| 2-3 **built 26 Nov 2026** | "Record damage" button in a saved purchase's totals bar (switch "Record damage", Admin by default) opens one pop-up (`MudBlazorComponents/RecordDamageDialog.razor`) listing every line with Received and a **Damaged** box showing how many are damaged now; raise a number to record more damage (reason per line, defaulting to "Reason for all"), **lower it when pieces turn out not to be damaged after all** (Hiten, 26 Nov 2026: "on closer look it isn't"); one note; saved as one Damage stock change of the differences (Sellable → Damaged, or Damaged → Sellable "Not damaged after all") linked to the purchase and its lines, its return supplier the purchase's supplier. Nothing earlier is changed, so the history stays complete. Checked again at the moment of saving (never more than came in; never below pieces already repaired, written off or sent back – batches 2-5/2-6). A purchase line with damage cannot be deleted, lowered below the damaged pieces or changed to another item; the purchase cannot be deleted. The damage shows in the purchase's activity panel ("Damage recorded …") and as its own entry. Rules in `Service/StockChangeService.cs` (`StockChangeRules`). | Yes |
| 2-4 **built 26 Nov 2026** | Sales Returns: menu item under Transactions (switch "Open the Sales Returns screen", Admin by default; no Alt + letter chosen, Alt+N and Esc work), list `/salesreturns` and page `/salesreturn/{id}` laid out like a purchase (customer + Enter saves, voucher numbered per day, the same line editor with quantities only, more than 0 required), Record damage on the return with the same pop-up. **Given in exchange** (Hiten, 26 Nov 2026): a button on the return opens the Bulk Entry pop-up in a quantities-only form; the goods given to the customer go out of sellable stock on the same voucher and are listed under "Given in exchange" (X removes a line); the list shows Returned and Given. Hiten's example adds up: 100 in stock, 10 come back and are marked damaged, 10 given in exchange → 90 sellable + 10 damaged. Activity log kind "Returns and damage" (cocoa brown); a line given in exchange is logged as "Exchange line". 498 tests pass (24 new); `tools/ui-check/check-damage-returns.mjs` (setup `setup-damage-returns.py`), `check-line-rules`, `check-bulk-entry` and `check-look` pass. | Yes |
| 2-5 **built 26 Nov 2026** | Damage report (`/damage`, menu "Damage report" under Transactions, switch "Open the Damage report"; `Pages/DamageReport.razor`, rules in `Service/DamagedGoodsService.cs`): one row per purchase line or returned line still lying in damage (net of "not damaged after all" and of what was already dealt with) – product, variant, pieces, a Qty box to take fewer, from (link to the purchase or return; damage date on hover), reasons, supplier, status (No supplier yet / Supplier assigned / Ready to send); filters Show, Supplier, Search. Each row's **Supplier** and **Status** are drop-downs in the row itself (Hiten, 26 Nov 2026: in-line edits as well as ticking rows): Status "Assigned" / "Ready to send" (or "No supplier yet") is saved at once; Status "Repaired" or "Written off" takes the row's Qty after a confirmation (cancel leaves the row as it was). A purchase or sales return opened from the report's From link goes back to the report with Esc (`?from=damage`, `Shortcuts.CameFromDamageReport`). Tick rows, then **Assign supplier** (pop-up picker), **Ready to send** (needs a supplier), **Repaired** (back to sellable), **Written off** (leaves stock) – each with a confirmation (Enter confirms) – or **Sent**. No "Not ready" button (Hiten: removed; the row's Status drop-down takes a row back to "Assigned"). **See activity log** in the bottom bar, as on every screen: one panel with the history of damage, supplier changes, ready to send, repairs, write-offs and purchase returns (`ActivityLogService.ForDamageReportAsync`). Every action re-checks at the moment of saving and is in the activity log. | Yes |
| 2-6 **built 26 Nov 2026** | Purchase Returns (`/purchasereturns`, `/purchasereturn/{id}`, menu under Transactions, switch "Open the Purchase Returns screen"; Alt+N and Esc, no letter): **Sent** on the damage report makes one purchase return for the ticked rows' supplier (different suppliers refused: one supplier at a time) and takes the pieces off the damaged count; the page lists "Damaged goods sent back" with where each was found (X: not sent after all, back on the damage report) and "Good stock sent back" through the line editor (quantities only, more than 0); a purchase return can also be made directly. Deleting one puts damaged goods back on the report. After a purchase return nothing more is tracked (Hiten). 511 tests pass (13 new); `tools/ui-check/check-damage-report.mjs` (setup `setup-damage-returns.py`) passes; `check-look` now also covers Sales Returns, Purchase Returns and the damage report and passes. | Yes |
| 2-7 **built 26 Nov 2026** | **Stock changes** screen (`/stockchanges`, `/stockchange/{id}`, menu under Transactions, switch "Open the Stock changes screen"; Alt+N and Esc, no letter; `Pages/StockEntries.razor`, `Pages/StockEntry.razor`, rules in `Service/StockEntryService.cs`): an entry has a type – **Damaged in the shop** (Sellable → Damaged; shows on the damage report as "Damaged in the shop, entry N", its From link opens the entry), **Count correction** (type 3 for found more, -2 for found fewer), **Written off** (good stock out), **Opening stock** (in) – fixed once saved, a date, a **Reason** from the list (copied onto every line) and remarks; lines through the line editor (quantities only, Remarks = a note per line). Shop damage already repaired, written off or sent back cannot be lowered below that, changed to another item or deleted. **Reasons list** (`/reasons`, button "Reasons" on Settings; Esc back to Settings): add (Enter), rename, move up/down, take off the list / put back (never deleted; at least one stays); written to the activity log. Upgrade `StockApproval` adds `StockChanges.Reason` and the `AppSettings` table. | Yes |
| 2-8 **built 26 Nov 2026** | **Approval switch** in Settings that only the Admin may change (Hiten, 25-26 Nov 2026: "turned off with a switch"), and the "Waiting for approval" list (Approve / Reject). With the switch on, **every stock change waits for approval before it moves stock**: damage recorded or lowered on a purchase or sales return, sales returns and their exchanges, purchase returns, and the damage report's Repaired, Written off and Sent (Hiten, 26 Nov 2026: "even these items need approval"); stock changes from the stock change screen (2-7) too. Assigning a supplier and "Ready to send" move no stock and need no approval. The damage report shows pieces waiting for approval as such. `StockChangeService.NewStatusAsync()` is the one place this is decided (setting `StockApproval` in the `AppSettings` table, off unless switched on; switching is confirmed and written to the activity log). Changing the lines of an approved or rejected entry while approval is on sends it for approval again. **Waiting for approval** (`/approvals`, menu under Transactions, switch "Approve stock changes"; `Pages/Approvals.razor`): oldest first, each entry with who made it, what it belongs to and its lines in plain words ("P1 / V1: 4 out of stock"); **Approve** (stock moves now, approver and time recorded) or **Reject** (confirmation; stock never moves). Waiting or rejected entries carry a notice on their page (`Shared/ApprovalBanner.razor`); the damage report shows waiting pieces per row and keeps waiting repairs aside so they cannot be taken twice; stock figures show waiting pieces behind the arrow. 525 tests pass (14 new); `tools/ui-check/check-stock-approval.mjs` passes; `check-look` covers the new screens. | Yes |


# Stage 3 – Locations (opened 26 Nov 2026) – 3-1 to 3-4 pushed

**What the data shows today (copy of real data, read only, 26 Nov 2026):** nothing records where goods are kept – no place, rack or shelf anywhere in the data. Stage 3 starts from nothing; old entries get a starting place (way B, as stage 2: nothing converted).

**Decided with Hiten (Describe step, round 1, 26 Nov 2026):**
- **Places today:** one shop with 3–4 rooms / store places, and one godown, all in the same building. Goods move between them very often. The godown will get a PC or a mobile device (that device needs stage 1d, shop network, which is parked – to be brought back when the device comes; until then moves are entered at the counter PC).
- **In transit:** built, with a switch; when switched off it appears nowhere (Hiten). Off for now, since everything is in one building – a move counts at once.
- **Way A chosen:** the app counts stock per place only; racks and levels are **signposts for the pickers** ("where to look"), not counted per shelf. Counter sales stay as today.
- **Address naming:** Location – Rack (number) – Level (letter), e.g. Godown 1 – Rack 1 – Level A. Nothing is named today.
- **Many to many:** one variant can sit on several racks in the shop and several in the godown; one rack level holds many variants.

**Round 1 follow-ups and round 2 (Hiten, 26 Nov 2026):**
- **The shop is one unit** for now (its rooms and the office are not counted separately). Places that count stock: the shop and the godown.
- **Signposts serve pickers and arrangers** (put-away) alike.
- **Address numbers only, except the level letter:** location number – rack number – level letter, e.g. `01-03-B` / `0103B`; QR form still open (Hiten unsure about `01 Tab 03 Tab B`).
- **Network:** the counter PC acts as the server and the godown device connects to it; the godown user's role opens only the screens the godown needs (stage 1b roles already do this). A central server replacing the counter PC was raised as an option (section 6.1); one database either way, so nothing to sync.
- **Purchases arrive in the godown; sales leave from the shop.** Today goods are fetched from the godown and sold; from now a move is recorded **when goods leave the godown and again when they arrive** in the shop (a two-step move).
- **Moves:** a "Move stock" switch, with its own approval switch, off (as suggested).

**Round 3 and follow-ups (Hiten, 26 Nov 2026):**
- **Choosing an address by hand:** Location, Rack, Level picked one by one; the code (`01-03-B`) is put together by the app. Two-digit racks (99 per location) are enough.
- **Stage 3 goes live only when the godown PC and mobile devices are there** – plan as if they exist (so stage 1d, shop network, is needed before go-live). One Admin PC can still control everything.
- **Level A is the bottom level.** **One main rack per item**, others "also at".
- **Starting point:** all existing stock starts in the shop; the godown is filled by "starting count" moves, shop → godown, rack by rack.
- **Users and places:** a user can be limited to one place or several; what they see follows it (a godown person sees only godown stock, a manager both). Some users may move stock, some not.
- **Damage needs no location** (damage report as today); Hiten "not sure yet" on returns per place.
- **Open question from Hiten:** goods arrive in the godown, are checked, some are arranged there and some sent to the shop – how does a purchase get its location? (Proposed flow given 26 Nov 2026, awaiting his answer.)

**Agreed (Hiten, 26 Nov 2026, before mock-ups):**
- **Three ways to give an address, same result:** type `0103B`, scan the rack label, or pick Location / Rack / Level from drop-downs. The rack QR holds the same text a person types (`0103B`); dashes are accepted too.
- **PC screens first.** Phone screens are a later piece of work of their own (Hiten: "we have to do the entire thing"). Stage 1d (shop network) is still needed for the godown PC.
- **Damage (Hiten's correction):** a place is chosen only when damage is found in the shop or godown. Damage on a purchase belongs to the godown; a damaged sales return goes straight to damage (the shop count is not touched); an exchange is given from the shop; a repaired piece goes back to the **shop**.
- **Purchase flow agreed:** (1) a purchase lands in the godown; (2) checking and arranging sets the signposts only; (3) "Send to shop" on the purchase opens a move filled with its lines – pieces are "on the way"; (4) the shop receives and confirms, differences flagged. A move can also be made without a purchase.
- **Put-away:** after a purchase the goods are arranged; if an item's rack is full the user chooses another rack and assigns it to the item.

**Mock-ups (PC) shown 26 Nov 2026:** Places and racks (Settings), the address box, racks per variant on the product page, Put away and Send to shop on a purchase, Moves (send / arriving from godown / receive), Where is it?, per-place stock behind the arrow, places per user, Place box on the Stock changes screen. Hiten's answers:
- **A missing piece on receive is a red flag** and must be highlighted until it is sorted out.
- **Selling with 0 in the shop is allowed** (in a hurry goods come from the godown without the move being recorded).
- **Bulk orders sometimes go straight from the godown:** a **"From" box on the Sale** (Shop by default, the cursor skips it, scanner flow unchanged), changed to Godown for such orders; one place per sale. Only some (senior) users may choose Godown – a switch the Admin controls. No three-step transfer → receive → sale.
- **Pickers work from a sales order** – that is stage 6; **no pick list in stage 3** (Hiten: stick to the plan).
- **Emptied racks:** the app does not count per rack, so it cannot know a rack is empty; Hiten asked how an emptied rack is recorded (a 100-piece order clears 3 of 4 racks). Proposed 26 Nov 2026: tick "emptied" beside the godown racks on the move as goods are sent, and an "Empty now" button on Where is it?; the main rack stays as the item's home, only extra racks are removed; all godown racks greyed by themselves when the godown count is 0. Hiten (26 Nov 2026): not sure – godown racks change all the time, shop racks are more or less fixed. **Decided: settle it in batch 3-7**, after he has used racks on the test copy. Proposal to try then: shop racks fixed (main + also at, removed only by hand); godown racks "currently at" only (no main), ticked "emptied" on a move and cleared by themselves when the godown count reaches 0.
- **Order (Hiten, 26 Nov 2026):** finish stage 3, then **install stages 2 and 3 together** on the counter PC, then stage 1d (shop network). Until 1d, moves are entered on the counter PC.

**Batches (proposed 26 Nov 2026; Hiten said "start" the same day; each: its own tests, build clean, browser check per two batches; full suite at the end):**

| Batch | What | Visible? |
|---|---|---|
| 3-1 **built 26 Nov 2026** | Groundwork: tables `Places` (01 Shop, 02 Godown made by the upgrade), `Racks`, `VariantRacks`; every purchase and sale carries its place (`PlaceId`, old ones and today's screens: the Shop), stock change lines carry the place of their sellable side (`FromPlaceId` / `ToPlaceId`; stage 2 screens still make lines without one, and the save puts them in the Shop – `StockPlaces.FillStartingPlace` – until 3-4 gives each its own place). `StockQuery.SellableByPlaceAsync`: places always add up to the sellable figure (entries outside the shop are added up, the shop holds the rest – ~0 ms on real data). Database rules: a place must exist; a place holding entries (and the shop) cannot be deleted; a move line Sellable → Sellable is allowed only between two different places (stage 2's "from ≠ to" rule widened for 3-5). Upgrade `Places` only adds (no table rebuilt); tried on a copy of real data: all 675 items' stock identical (12,429 pieces), all in the Shop, no old table changed by a row. | No |
| 3-2 **built 26 Nov 2026** | **Places and racks** (`/places`, button on Settings, switch "Change settings and backups"; Esc back to Settings; `Pages/PlacesAndRacks.razor`, rules in `Service/PlaceService.cs`): add a place (Enter; next free number), rename, change its number while it has no racks, delete only when empty; per place add racks in a range ("From 1 To 5, levels A to E"; racks already there are skipped; the next free number is suggested), change a rack's top level, delete a rack – refused while a rack or level holds items. The transit switch moves to 3-5 (nothing to switch before moves exist). **Address box** (`Shared/AddressBox.razor`, `Service/RackAddress.cs`): type or scan `0203B` / `02-03-B` / `2-3-b`, or pick Place / Rack / Level – each fills the other; a wrong address says plainly why ("Godown has no rack 09.", "Rack 02-05 has levels A–C."). **Product page** (Hiten, after trying it: "why crowd the product page?"): no Racks column. Then (Hiten, on the uneven icons): **clicking a variant line opens a strip under it** (a small arrow before the Variant ID shows it can open; one line open at a time) with named buttons **Print labels** and **Where it is kept** and the racks in words ("Kept on: ★ 01-01-B Shop, Rack 1, Level B …"); the label icon left the Reorder Level column, which now holds only the number; Edit / Delete, the stock arrow and the photo keep their own clicks. **Where it is kept** opens the "Where … is kept" pop-up (`MudBlazorComponents/VariantRacksDialog.razor`), one row per rack **in full words** ("01-01-B  Shop, Rack 1, Level B  ★ main"). With the new switch "Set where items are kept (racks)" (Admin by default): add (Enter adds; the chosen address is spelled out under the box), ☆ make main, ✕ takes off, "Same for all variants" (confirmation; their own racks are replaced); without it the pop-up only shows the racks. Each rack line in the pop-up has a small gap and a border of its own, and the racks are **grouped in a section per place** with a heading ("Shop (01)", "Godown (02)"; lines then read "01-01-B  Rack 1, Level B"); the strip groups them the same way ("Shop: ★ 01-01-B Rack 1, Level B …") (Hiten). Not re-tested after these last look changes (Hiten: "do not run any tests"); `check-places` and `check-labels` were updated for the strip and run with the next pair. Small rules chosen: the first rack added in a place becomes its main rack; when the main rack is taken off, the next one of that place becomes main. Rack changes go into the product's history (activity log) without stamping the product, so setting racks never clashes with someone editing it; place and rack changes are logged as settings. 561 tests pass (36 new); 329 warnings. Browser check `tools/ui-check/check-places.mjs` (34 checks) passes; `check-look` (now with Places), `check-save-and-focus` and `check-labels` pass. | Yes |
| 3-3 **built 26 Nov 2026** | **Places per user** (table `UserPlaces`, upgrade `PlacesForUsers`): a "Places" column and button on the Users screen (`MudBlazorComponents/UserPlacesDialog.razor`, rules `UserAdmin.SetPlacesAsync`): tick the places a user works in and sees; all ticked = every place, also ones added later (stored as none); at least one; the **Admin role always has every place**; given / taken away is in the user's activity log. `UserAccess.Places` / `SeesPlace`. **Stock per place:** a person limited to some places sees the sellable pieces of those places only – product page, Variant Report, Variants List, Reorder Level (`StockQuery.ForVariantsInPlacesAsync`; damaged and waiting pieces have no place and show as before); the stock arrow also opens when the pieces are in two or more places and lists them ("in Shop 30 / in Godown 5", `PlaceStock`, `GetPlaceStockAsync`). **Variant Report:** a Place filter (All (my) places / one place) when the person sees more than one place. | Yes |
| 3-4 **built 26 Nov 2026** | **Where purchases arrive and sales leave from:** two drop-downs on Places and racks (AppSettings `PurchasesArriveIn` = Godown, `SalesLeaveFrom` = Shop; `PlaceSettings`; logged). **New purchases arrive in the godown** (`PurchaseRepositoryEF.Create`; saved purchases keep their place); the purchase shows "Arrives in Godown" (read only, skipped by the cursor). Purchases **imported** from a file still count in the shop (opening stock). **Sale "From"** box beside the date: Shop, `tabindex=-1` so the scanner flow is unchanged; changing it needs the new switch "Sell from another place (e.g. the godown)" (`sales.other-place`, Admin by default; offers the places the person works in); the sale keeps its place through Sales Summary. **Stock changes screen:** a Place box (the person's places, Shop first; fixed once saved). **Damage and returns (Hiten's rules)**: each stock change entry has a place (`StockChanges.PlaceId`) its lines take (`StockPlaces.FillPlaces`): damage on a purchase from the place the purchase arrived in; sales returns and exchanges in the shop (a damaged return goes in and straight out, so the shop count is as it was); repaired pieces back to the shop; good stock sent back to a supplier from the godown; written off / sent back damaged goods have no place. Upgrade `PlacesForUsers` tried on a copy of real data: every item's stock identical (12,429 pieces), no row changed. 571 tests pass (10 new); 329 warnings. Browser check `tools/ui-check/check-place-flow.mjs` (19 checks) passes; the full suite runs for this pair (the Sale and Purchase screens changed). **Then (Hiten, 26 Nov 2026):** (1) good stock sent back can leave from the shop or the godown – a **"From"** box on the Purchase Return (Godown unless changed; returns made by "Sent" on the damage report start in the godown too); (2) **only the Admin changes the place of a saved entry, everywhere** – purchase "Arrives in", sale "From", Stock changes "Place", purchase return "From" – saved at once, and what belongs to it moves with it (damage recorded on a purchase moves with the purchase; an entry's lines move with the entry) (`PlaceService.ChangePurchasePlaceAsync`, `ChangeSalePlaceAsync`, `ChangeEntryPlaceAsync`; logged as "Place: Godown → Shop"); a sale saved again never changes its place; (3) **Variant Report:** with all places shown, Stock In Hand is split into a column per place and a **Total in bold**; the Place filter stays, and with one place chosen there is one column. Built only, not tested (Hiten: "do not run any tests"). | Yes |
| 3-5 | Moves: "Send to shop" on a purchase (filled with its lines) and free moves; pieces "on the way"; switch "Move stock" and its own approval switch (off); transit switch off = one-step move; "Starting count" moves (shop → godown, one step). | Yes |
| 3-6 | Receiving in the shop: "Arriving from godown", confirm received (scan or type); a missing piece is a red flag on the move, the Moves list and the menu until received late or written off; shop figure red below zero and a "Shop below zero" list. | Yes |
| 3-7 | Put away on a purchase; emptied racks; "Where is it?" (item → racks, rack → items). Then the full browser suite. | Yes |

After stage 3: install stages 2 and 3 together (full browser suite, upgrade tried on a copy of the live database, every item's stock compared), then stage 1d (shop network, its own short Describe step) so the godown PC can connect.


# To do – Year-end close (Hiten, 25 Nov 2026)

At every year end, the year's closing stock becomes the **opening stock of a fresh database**; the old database is kept in storage. The new year starts with that opening stock, with all other data carried forward (customers, suppliers, products, users, settings and the like) and the **amount balances carried forward as opening figures**. Not scheduled yet. Points to settle when it is built:
- The financial year starts in April (the reports already use this), so the **first close would be 31 March 2027**: build and try it on a copy of real data before then if it is wanted that year.
- Opening stock carries sellable and damaged pieces separately (needs stage 2).
- "Amount balances": the app records no payments yet (accounts and payments are outside this plan, section 11), so what balance is carried forward is to be agreed.
- What else moves forward: open purchase orders and sales orders, entries waiting for approval, the activity log, label sheet use.
- Whether last year's data needs to be opened from the app (read only) for reports and lookups, and whether last year's entries are locked.
- Voucher numbers restart or continue.

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

~~The existing purchases and sales are converted once into movements.~~ **Changed 25 Nov 2026 (Hiten chose way B):** the existing purchases and sales are not converted; they stay the record of what came and went, and new kinds of entry (stock changes, later transfers and receipts) are added beside them. Stock in any state or place is still the sum of all these entries, and today's figures cannot move, which removes what used to be the riskiest step in the roadmap.

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
- **Changed 25 Nov 2026 (way B, see "Stage 2" at the top):** old purchases and sales are not converted; "Stock change" entries are added beside them, so no parallel run is needed.
- Click a stock figure to see In stock / Damaged (Arrived comes with stage 5).
- Screen to move goods to damaged, back, or write them off, with a reason; customer and supplier returns.
- Stock corrections with a reason and an approval (approval switch, off for now).
- **Done when:** every variant's stock is unchanged by the upgrade, and damage, repairs, returns and corrections are entered with a reason instead of CAPITAL purchases and minus lines.

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
