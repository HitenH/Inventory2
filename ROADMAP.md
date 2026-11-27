# Plan for version 3.0 – from one counter PC to a multi-user, multi-location system

**Written for:** Hiten (plain English) and any future session picking this up.
**How this fits:** `README.md` explains the app as it is. This file is the only plan: what is done, what Hiten decided, and what comes next. Build details (file names, test counts) are in `README.md` and git history, not here.
**This plan will change.** It is reviewed at the start of every stage.

**Contents:** Where we are · Decisions per finished stage (1, L, 2, 3) · Year-end close · 1 Where we are going · 2 The big changes underneath · 3 How it is built · 4 Stages · 5 Today's screens and old open items · 6 Technology decisions for later · 7 Things outside the software · 8 Risks · 9 Open questions · 10 Decided · 11 Not in this plan · 12 Words used

---

# WHERE WE ARE (27 Nov 2026) – read this first

**On the counter PC:** version 3.0 as packaged on 25 Nov 2026 (commit `f910f58`): stages 1a, 1b, 1c and labels L-1/L-2. Installed by Hiten, confirmed 26 Nov 2026.

**Finished and pushed, NOT installed:**
- **Stage 2 – stock breakdown and damage** (complete 26 Nov 2026, last commit `4013c7d`).
- **Stage 3 – locations** (complete 27 Nov 2026, last commit `195ec58`). End-of-stage checks: all 593 automatic tests pass; the full browser suite (24 checks) passes – the rates check after a fix to the test helper, run on its own.

**Next, in Hiten's order:**
1. **Install stages 2 and 3 together** on the counter PC (see "Install rule" below).
2. **Stage 1d – shop network**, so the godown PC can connect. Stage 3 is only used for real once the godown PC is there.
3. Then the next stage of section 4 (Hiten chooses).

**Install rule (every install):**
1. Full browser suite on a test copy (done for stages 2 and 3).
2. Copy the live database off the counter PC first. An older build cannot open a database a newer build has upgraded, so this copy is the way back (README section 10).
3. Try the upgrade on that copy and compare every item's stock before and after. Upgrades waiting for the counter PC: `StockChanges`, `StockApproval` (stage 2), `Places`, `PlacesForUsers`, `Moves` (stage 3). All of them only add; each was tried on a copy of real data with every item's stock unchanged.
4. Make the package; Hiten installs it; smoke test at the counter.

The counter PC is also the development PC: the live app runs from the `H:` drive on port 5000. Never touch `H:` or stop that app without Hiten's yes.

**Parked (Hiten's decision, comes back later):**
- **1d – shop network:** needed before the godown PC can be used (see above).
- **Amounts and totals** (1a-3): returns and exchanges carry quantities only; a voucher with a discount but no lines and a sale never summarised still give odd totals. CSV imports still refuse a quantity of 0 or below.
- **L-3 – sticker printer settings:** when Hiten has the sticker printer (make, model, label size).
- **Labels tied to purchases** (extra labels only with permission): with goods inwards, stage 5.
- **Import of purchases and places:** an imported purchase (opening stock) counts in the shop; to change later (for example a Place column in the file).
- **Year-end close** into a fresh database (section below; first close would be 31 March 2027).
- **Phone screens** for stage 3 (godown, pickers): a separate piece of work after the PC screens.
- **Not yet answered:** an Alt + letter for Sales Returns, Purchase Returns, Stock changes, Damage report, Stock Transfer and Where is it? (Alt+N and Esc work on them today); whether the starter roles Purchase and Sales get the stage 2 and 3 switches (today only Admin has them); Hiten's cut-off message "A person with …" (stage 3, stock transfers).

**Working agreements with Hiten:** plain English, no jargon. He answers questions and says **"push"** to approve – nothing is committed or pushed before that. Every stage starts with a **Describe** step (questions a few at a time, each with a suggestion) and **mock-ups** (ASCII is fine) before any code. Build step by step or two batches at a time. Automatic tests every batch; one browser check per two batches; the **full browser suite once per stage and before any install** – never while anything else is being built or restarted. Honest opinions with a recommendation.

---

# Decisions per finished stage

What Hiten decided, kept because it explains why the app behaves as it does. How each part is built is in `README.md` section 4.

## Stage 1 – Foundation (1a, 1b, 1c done 24–25 Nov 2026; 1d parked)

**1a – rules in one place.** One rule book for voucher lines: a real product and variant, quantity a whole number that is not zero (negative allowed – returns used to be entered that way), discount 0–100. A very fast double Enter adds a line only once. Edit screens say "not found" instead of showing a blank form. Still open: the repositories return a blank record, and the plain-words sweep of error messages is only partly done.

**1b – users and permissions.**
- Every screen and sensitive figure has a switch; the Admin builds roles from switches; a user may hold several roles, with an optional end date (temporary cover when someone is away).
- Users are never deleted, only disabled. You cannot disable yourself; the last active Admin cannot be disabled.
- The Admin role cannot be changed or deleted; a role that anyone holds cannot be deleted.
- Rates are hidden both ways: without "See purchase rates" no purchase rate, amount, discount or total anywhere; the same for sale rates (the product's selling price counts as a sale rate). Hidden figures are never sent to the screen.
- Starter roles: Purchase, Sales, Reports.
- The pair-rule editor (which two tasks one person may not do on the same entry) waits for stage 5, when there are tasks to pair.

**1c – who and when.**
- Every record keeps who created and last changed it and when – **not shown on the forms** (Hiten: do not crowd the screen), but through a **"See activity log"** button that opens the record's history.
- The activity log holds every change with before → after values, user and role changes, backups, imports and logins. Rate changes follow the rate switches. Kept until the Admin role erases the whole log (one confirmation, no trace – Hiten's choice). Nobody can edit or delete single entries.
- One person's changes to one record in one sitting are one entry; lines added while creating a record fold into its "Created" entry.
- Two people on the same record: the second save is refused with "<name> changed this <record> at <time> while you had it open. Reload to see their changes."
- Activity log screen: colours per kind of record and a name pill per person (never red).

**1d – shop network (parked).** The app opens from other PCs on the shop network over a secure connection, with lock-out and idle sign-out. The counter PC acts as the server; one database, nothing to sync. The old duplicate voucher numbers must be decided here (section 5).

## Stage L – Label printing (L-1 and L-2 done 25 Nov 2026; L-3 waits for the sticker printer)

- Labels are printed on **A4 sheets of 65 ready-cut labels (40 × 21 mm)** on a laser printer, laid out as Hiten's Word file: product name bold, variant name underlined, QR code on the right with the product ID under it.
- **The QR holds Product ID, Tab, Variant ID** – the scanner types it straight into the Sale screen. Never change it.
- One identical label per product and variant; as many copies as needed.
- A sheet fills from the bottom-right backwards; "Already used on this sheet" skips used labels, and the app remembers where the last print ended. Pressing Print counts as printed (no question afterwards).
- Sizes and margins are **named label set-ups** the Admin can add and change (Label settings), not fixed in the program.
- "Print labels" from a purchase (one line per purchase line, copies = quantity) and from a product's variant; Alt+L; Ctrl+P prints; Esc goes back to where Labels was opened from. Every print is in the activity log.
- **L-3:** a Settings page to switch A4 printing off and use the sticker printer with its own settings.
- **Later rule (stage 5):** labels only for what a purchase brings in; extra or repeat labels need permission (against duplicate labels, theft and swapped labels).

## Stage 2 – Stock breakdown and damage (done 26 Nov 2026; pushed, not installed)

- **Way B – nothing old is converted.** Purchases and sales stay exactly as they are. A new kind of entry, the **stock change**, sits beside them. Stock = purchased − sold ± stock changes, still worked out every time, never stored. Old CAPITAL purchases and minus sale lines are left untouched.
- **Stock in hand = the sellable pieces.** Damaged pieces and pieces waiting for approval sit behind a small arrow; only a click opens the breakdown. The Variants List report has a Damaged column; the Reorder Level report does not.
- **Record damage** on a saved purchase or sales return: one pop-up with every line; raise a number to record damage, lower it when pieces are "not damaged after all". Nothing earlier is changed, so the history stays complete.
- **Sales Returns** are their own voucher, with **Given in exchange** (goods given to the customer go out on the same voucher). An exchange does not mark anything damaged by itself.
- **Damage report:** one row per damaged lot still lying in damage, with Supplier and Status drop-downs in the row, and actions Assign supplier, Ready to send, Repaired, Written off and **Sent** (makes one Purchase Return per supplier). No "Not ready" button (Hiten removed it). After a purchase return nothing is tracked – what comes back is a new purchase.
- **Purchase Returns** also send good stock back directly.
- **Stock changes screen:** damaged in the shop, count correction (found more or fewer), written off, opening stock – each with a reason from a list the Admin keeps (reasons are taken off the list, never deleted) and a note.
- **Approval:** a switch only the Admin can change, **off** for now. When on, every stock change waits (damage, returns, exchanges, repairs, write-offs, sent, stock change entries) on "Waiting for approval" (Approve or Reject). Assigning a supplier and Ready to send need no approval.

## Stage 3 – Locations (done 27 Nov 2026; pushed, not installed)

**Places and racks**
- Places that count stock: **the shop (01)** – one unit, its rooms not counted separately – and **the godown (02)**, in the same building. More places can be added.
- **Way A:** stock is counted **per place** only. Racks and levels are **signposts** for pickers and arrangers ("where to look"), never counted.
- **Address:** place number – rack number – level letter, shown `02-03-B`, typed or scanned as `0203B` (a rack label's QR holds the same text). Level A is the bottom; up to 99 racks per place. Three ways give the same result: type, scan, or pick Place / Rack / Level from lists.
- A variant can be on many racks, a rack level holds many variants; **one main rack (★) per place**.
- Racks are shown in full words ("Shop, Rack 1, Level B"), grouped per place, behind a click on the variant line – the product page is not crowded.
- **Starting point:** all existing stock starts in the shop; the godown is filled by **starting counts** (shop → godown).

**Who sees what**
- A user is limited to the places they work in, and **sees stock of those places only** (a godown person sees godown stock, a manager both). The Admin role always sees every place.
- Only the **Admin** changes the place of a saved entry (purchase, sale, stock change, purchase return), and what belongs to it moves with it.

**Where stock goes**
- **Purchases arrive in the godown; sales leave from the shop** (both set on Places and racks). Imported purchases count in the shop.
- The Sale has a **"From"** box: Shop, skipped by the cursor so the scanner flow is unchanged; choosing the godown (bulk orders) needs the switch "Sell from another place". **Selling with 0 in the shop is allowed** (goods fetched in a hurry); the shop then shows below zero in red until the transfer is recorded, and below-zero items are listed.
- **Damage:** the place is chosen only when damage is found. Damage on a purchase belongs to the godown; a damaged sales return goes straight to damage; an exchange is given from the shop; repaired pieces go back to the shop. Good stock sent back to a supplier leaves from the shop or the godown (Purchase Return "From").
- **Variant Report:** Stock In Hand split per place (Shop | Godown | On the way) with the **Total in bold**; a Place filter (one place = one column).

**Stock Transfer** (called "move" inside the program)
- One "Stock Transfer" link in the menu; the New transfer button is on the page. Sections: **Pieces missing**, **On the way**, **Received**, and Below zero.
- **Nothing is saved until Send** – no empty records. Scan a label (one piece) or pick an item; quantity boxes stay open (scan 1, type 100).
- **Two steps** (switch, on): goods leave one place and are **on the way** until the other place receives them. **Pieces on the way are sellable**, just reported separately. With the switch off a transfer counts at once. Transfers have their own approval switch (off).
- **Receiving:** scan or type per line, a tick per line and one for all lines. A **missing piece is a red flag** – red on the transfer, in the list and beside the menu link – until it is received or written off.
- Separate switches: **Send**, **Receive**, and **Write off missing pieces** (Admin only for now).
- "Send to Shop" on a purchase in the godown makes a transfer filled with its lines.
- **No pick list in stage 3** – pickers work from sales orders in stage 6.

**Put away and Where is it?**
- **Put away** on a purchase: type or scan a rack per line.
- **Where is it?** (menu under Masters): find an item by its label (stock per place and racks in words), or show a rack or one level with what is kept there. **Arrange rack by rack:** show a level, then scan items (or pick by hand: Product → Variant or All variants → Add); "Moving here" takes the item off its other racks in that place and makes this its main rack. Racks can be chosen from lists (Place | Rack | Level on one line). ✕ takes an item off a rack. Viewing and changing are separate permissions.
- **Racks clear themselves outside the shop** ("Yes to your reco"): when a save takes an item's stock in the godown from above 0 to 0 or below, it is taken off its godown racks (written in its history). Shop racks change only by hand; racks set before any stock arrived stay.

---

# To do – Year-end close (Hiten, 25 Nov 2026; not scheduled)

At every year end, the year's closing stock becomes the **opening stock of a fresh database**; the old database is kept in storage. Customers, suppliers, products, users and settings carry forward, and **amount balances carry forward as opening figures**. To settle when it is built:
- The financial year starts in April, so the **first close would be 31 March 2027**; build and try it on a copy of real data before then if wanted that year.
- Opening stock per place, sellable and damaged separately.
- "Amount balances": the app records no payments yet (section 11), so what is carried forward is to be agreed.
- What else moves forward: open purchase and sales orders, entries waiting for approval, transfers on the way, the activity log, label sheet use.
- Whether last year's data can be opened read-only for reports, and whether voucher numbers restart.

---

## 1. Where we are going

One app, one database, many people. Each person logs in and sees only the screens their job needs, on a PC, a tablet or a phone.

- **Purchase side and sales side are separate**, rates hidden both ways, all switched by the Admin per role. *(Done in stage 1b.)*
- **Goods inwards** in steps, each by a different person: supplier's invoice entered → goods counted by the checker → labels printed from the count → manager compares invoice and count and approves → goods put away.
- **Goods outwards** in steps: order taken (counter, tablet, salesman anywhere) → picker's list with where to find each item → picked goods scanned → order becomes a sale → dispatch scans again.
- **Stock has a breakdown** (sellable, damaged, waiting, on the way). *(Done in stage 2.)*
- **Stock has places** (shops, godowns, racks). *(Done in stage 3.)*
- **Stock checking on a phone:** the checker sees what the system expects, ticks what is right and reports what is not.
- **Everyone has a "my work" list** of what is waiting for them, so work never sits unnoticed between two people.

---

## 2. The big changes underneath

### 2.1 Permissions *(built in stage 1b)*
Every screen, action and sensitive figure has a switch; the Admin builds roles from them and gives each user roles and places. Rules are enforced inside the program, not only by hiding buttons.

**Tasks agreed 24 Nov 2026** (which tasks make a role, and who holds it, is set in the app):
1. Supplier invoice entry 2. Goods receiving (ideally without seeing the invoice quantities) 3. Label printing 4. Purchase approval 5. Order taking 6. Order picking 7. Pick verification 8. Sales invoicing 9. Dispatch check 10. Put-away 11. Stock transfer 12. Transfer approval 13. Physical stock check 14. Stock adjustment approval.

**Pair rule:** the Admin can block any pair of tasks from being done by the same person on the same entry (default: nothing blocked; editor comes with stage 5). **Cover when an approver is away:** temporary access given by the Admin, or the entry waits.

### 2.2 How stock is worked out (way B)
Stock is never stored: it is added up from the entries each time – purchases, sales and stock changes (damage, returns, corrections, transfers; later receipts). It cannot drift, extends to places by recording "where" on each entry, and old data never had to be converted. An index keeps it fast (all items ≈ 0.01 s on real data). Stored running totals only if, years from now, the full report gets heavy.

| State | Meaning | Sellable? |
|---|---|---|
| Sellable | In stock in a place | Yes |
| On the way | Sent from one place, not yet received | Yes, reported separately (Hiten) |
| Damaged | Set aside as damaged | No |
| Waiting | On an entry waiting for approval (has not moved yet) | Still counted where it was |
| Arrived | Counted by the checker, not yet approved (stage 5) | Q5 |
| Reserved / Picked | Promised on an order / picked and verified (stage 6) | No |

### 2.3 Entries have stages
A receipt is entered → counted → labelled → approved → put away. An order is taken → picked → verified → sold → dispatched. Each stage records who and when; only the right role moves it on; entries can be sent back a step with a reason or cancelled; part-deliveries and part-dispatches allowed.

### 2.4 The app leaves the single PC
First to other PCs, tablets and phones on the shop network (stage 1d), later to salesmen and other locations over the internet (stage 7).

---

## 3. How it is built (structure and safeguards)

### 3.1 Three layers
1. **Screens** only show things and collect input.
2. **The rule book** in the middle holds every rule once; every screen and device goes through it.
3. **The database** has its own last-line rules in case the other two ever fail.

### 3.2 Design rules
- **One place for each thing:** one list of permissions, one stock calculation, one line rule book.
- **Additions, not rewrites.** New features sit beside working screens.
- **Every new feature can be switched off** for a role, so a problem never stops the counter.
- **History is never lost:** a correction is a new entry; the activity log keeps the rest.

### 3.3 Several people at once
- Two people change the same record: the second is told to reload *(built, 1c)*.
- Each action saves completely or not at all.
- Two people after the last piece: stock is checked again inside the save (stage 6).
- A job opened by a picker or checker is marked as theirs (stages 5–6).
- A repeated "save" (double tap, weak signal) is saved once.
- Voucher numbers come from the database, never a screen (stage 1d).
- One clock: the main PC's.

### 3.4 Catching errors
Automatic tests for every rule (each stage adds its own); browser checks for screens, keyboard and scanner (phone-sized checks from stage 4); every upgrade tried on a copy of real data with stock compared; a pilot before everyone uses a new flow; backup before every upgrade with the previous version kept; plain messages when something is refused.

### 3.5 Security once more than one device connects (stage 1d onwards)
Encrypted connection; one account per person; lock-out after wrong passwords; sign-out when idle on shared devices; the Admin can disable a user or a lost phone at once; backups copied off the main PC; a restore practised before others depend on it.

### 3.6 Steps inside every stage
1. **Describe** – Hiten explains the real work; the stage's open questions (section 9) are answered.
2. **Mock-up** – pictures of the screens, agreed before building.
3. **Build** in small batches, each with its tests.
4. **Check** – tests, browser checks, the full suite at the end.
5. **Trial** on a copy of the real data, then **pilot** with one or two users.
6. **Go live** – backup, install, old version kept; a one-page "how to" per role.
7. **Watch** – a week of use, fix what comes up, review this plan.

---

## 4. Stages

**Rule:** each stage goes live and is used before the next starts. *(Stages 2 and 3 are installed together, by Hiten's choice.)*

| Stage | What | State |
|---|---|---|
| 0 | Version 2.0 on the counter PC | Done 16 Nov 2026 |
| 1a–1c | Rules in one place; users, roles, permissions; who and when | Done, installed |
| L-1, L-2 | Label printing on A4 sheets | Done, installed |
| 2 | Stock breakdown and damage | Done, not installed |
| 3 | Locations | Done, not installed |
| 1d | Shop network | Next after the install |
| L-3 | Sticker printer | When the printer is chosen |

### Stage 1d – Shop network
The app opens from other PCs on the shop network over a secure connection; lock-out and idle sign-out; `/photos` and `/import-sample` behind the login; the duplicate-voucher-number decision. **Done when:** two people work at the same time on two PCs, each seeing only their side.

### Stage 4 – Stock checking on a phone (Medium; the first phone screen)
A check list by place and rack: product, variant, expected quantity; tick or enter the real count. Differences go to a manager; stock changes only when approved (a stage 2 count correction). Phone camera or hand scanner (Q11). Also the trial of phones on the shop Wi-Fi. **Done when:** one full area is checked on a phone and its differences approved, without paper.

### Stage 5 – Goods inwards (Large)
Supplier invoice entry, optionally matched to the Purchase Order (Q8; Purchase Orders must first point to real suppliers and products). Checker's count on a tablet or phone, blind if agreed (Q3); short, excess and damaged-on-arrival recorded. Labels from the count. Manager's side-by-side comparison; approve, send back, or approve with differences (Q13). Approval creates the Purchase. Put-away with the usual racks. "My work" lists. **Done when:** a real delivery goes from invoice to shelf through four logins, and the Purchase and stock are right.

### Stage 6 – Goods outwards (Large)
Order entry on a touch screen; ordered stock reserved (the "selling more than in stock" decision is settled here); **pick list ordered by rack**; picker marks picked or short (Q6); scan to verify; automatic Sale from the verified order; dispatch scan; part-dispatch. Counter sales continue beside it (Q2). **Done when:** a real order goes from tablet to dispatch through the scans, and a deliberately wrong pick is caught.

### Stage 7 – Outside the premises (Medium to Large)
Decide where the app lives (6.1), the salesman's screen (6.2) and the database (6.3). Salesman's screen: stock, photos, new order, from anywhere (several photos per variant settled here). Other locations connected. **Done when:** a salesman outside takes an order on a phone with a weak signal and it reaches the picker once, complete.

---

## 5. Today's screens and items still open from version 2.0

- **Purchase** stays, and becomes the result of an approved goods-inwards entry (direct entry stays for a role allowed to use it).
- **Sale** stays for the counter; a sale made from an order arrives ready-filled.
- **Purchase Orders and Sales Orders** stay; Sales Orders gain picking stages.
- **Keyboard and scanner behaviour at the counter does not change** unless Hiten asks.

| Still open | Where it belongs |
|---|---|
| Old duplicate voucher numbers (keep and exempt, or renumber) | Stage 1d |
| Secure connection (HTTPS) | Stage 1d |
| Amounts: empty-voucher total, unsummarised sale total | Parked (1a-3) |
| "Not found" still a blank record in four repositories; some technical error text | Any time (1a-4 leftovers) |
| Purchase Orders hold free-text supplier / product / variant | Stage 5 |
| Selling more than in stock (earlier advice: warn and show stock, do not block) | Stage 6 |
| Several photos and videos per variant | Stage 7 |
| Database allows one writer at a time | Section 6.3 |
| Small fixes (silent failures on order lists and Sale, case-sensitive report searches, report period edges) | Any time, with Hiten's yes (README section 11) |

**Declined by Hiten – do not offer again:** the extra keyboard offers and the Sales Orders "Set ticked to" offer (13 Nov 2026); saving a Bulk Entry row that was not added with Enter; fixing the build hint warnings (rule stays: no new ones).

---

## 6. Technology decisions for later

1. **Where the app lives once salesmen and other locations connect (stage 7).** A: stays on the shop's main PC, reached through a secure private link – data stays with you, the shop works when the internet is down (*recommended to start*). B: a rented internet server – reachable anywhere, but every location needs internet to sell, and a monthly cost. Stages 1–6 work the same either way. (A central server replacing the counter PC was raised for stage 1d; one database either way.)
2. **The salesman's screen.** On weak mobile signal the current kind of screen can freeze; the salesman's screen will probably be built lighter, holding an order until the signal returns. Decide at stage 7, using what stage 4 shows.
3. **The database.** SQLite is fine for 6–7 people in one building. With several locations and outside salesmen writing all day, a larger database (PostgreSQL) is the safe choice. Review at stage 7, or earlier if anyone sees "busy, try again".

---

## 7. Things outside the software (Hiten's side)

- **Main PC:** must stay on in working hours once others depend on it; a UPS and a backup disk or second backup place are strongly advised.
- **Godown PC and phones:** needed before stage 3 is used for real (with stage 1d).
- **Wi-Fi** in every aisle and godown corner where phones are used (stage 4 on).
- **Scanners** where a phone camera is too slow (Q11); rack labels with QR codes (`0203B`).
- **Sticker printer** details for L-3.
- **Fallback:** what staff do on paper if the main PC or network is down, and who enters it afterwards.
- **People:** a short hand-over per role at go-live and a named person to report problems to.
- **Internet link** and possibly a small monthly cost from stage 7.

---

## 8. Risks

| Risk | How it is kept small |
|---|---|
| Stock numbers go wrong | Stock is always worked out from entries (never stored); every upgrade tried on real data with stock compared; same-day rollback with the database copy |
| Too much built before anything is used | One stage live before the next (stages 2 and 3 together by choice) |
| A new flow blocks daily selling | Old screens stay; new features switch off per role |
| Staff skip steps or share logins | Rules in the rule book; one account per person; "my work" lists; activity log |
| Main PC fails | UPS, off-PC backups, practised restore |
| Phones unreliable on Wi-Fi or mobile signal | Phone trial in stage 4; lighter salesman screen in stage 7 |
| Data opened to the internet | Nothing leaves the shop network until stage 7 |

---

## 9. Open questions for Hiten

Asked in the Describe step of the stage that needs them. Answered ones (labels, damage, locations, returns, roles, approvals) are now in "Decisions per finished stage".

- **Q2 Counter (stage 6):** do walk-in counter sales continue next to the order → pick → dispatch flow?
- **Q3 Checker's count (stage 5):** count "blind" (without seeing the invoice quantities)? Recommended yes.
- **Q5 Arrived goods (stage 5):** can goods arrived but not approved be promised on an order?
- **Q6 Short picks (stage 6):** does a short order go out short, wait, or go back to the salesman?
- **Q8 Purchase Orders (stage 5):** match the supplier's invoice against the Purchase Order?
- **Q11 Scanning on phones (stage 4):** phone camera, or hand scanners paired with the phone?
- **Q13 Differences on arrival (stage 5):** pay as invoiced, pay as counted, or claim from the supplier?

---

## 10. Decided (14 Nov 2026)

- Building goes stage by stage on Hiten's word.
- Salesmen can be anywhere; about 6–7 people at the same time.
- Rate visibility is controlled by the Admin both ways, never fixed in the program.
- A picked and verified order becomes a sale automatically; scanning is the check at picking and at dispatch.
- Barcode labels are printed from the app, in the existing QR format.

## 11. Not in this plan (say so if any is wanted)

Accounts and payments (who owes what), tax/GST invoices, price lists per customer, messages to customers (SMS/WhatsApp), a public online shop, payroll or attendance.

## 12. Words used

- **Role** – a named set of permission switches.
- **Stock change** – an entry beside purchases and sales that moves pieces between sellable, damaged, on the way and outside, or between places.
- **Way B** – stock worked out from all entries each time; old purchases and sales never converted.
- **Place** – somewhere stock is counted (Shop, Godown). **Rack / level** – a signpost inside a place, not counted.
- **Stock Transfer** – goods sent from one place to another (inside the program: a "move").
- **Rule book** – the middle layer that holds every business rule once.
- **Pilot** – a few real users trying a stage before everyone does.
- **My work list** – the jobs waiting for the person logged in.
