# Plan for version 3.0 – from one counter PC to a multi-user, multi-location system

**Status:** agreed direction, 14 Nov 2026 (session 10). **Nothing here is built.** Nothing is built until Hiten agrees the stage.
**Versions:** 1.0 = the May 2026 build. 2.0 = the current app, concluded 14 Nov 2026 and described in `README.md` (what it does, how it is built, how to run, test and install it, known limits). 3.0 = this plan.
**How this fits:** `README.md` explains the app as it is. This file is the only plan. Nothing is due on version 2.0. When a stage starts, its open questions (section 9) are asked and its batches are written into a "Current stage" section added at the top of this file.
**Written for:** Hiten (plain English) and any future session picking this up.
**This plan will change.** It is reviewed at the start of every stage; what we learn in one stage may reorder the next.

**Contents:** 1 Where we are going · 2 The big changes underneath · 3 How it will be built · 4 Stages · 5 Today's screens and old open items · 6 Technology decisions for later · 7 Things outside the software · 8 Risks · 9 Open questions (parked) · 10 Decided · 11 Not in this plan · 12 Words used

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
Every screen, every action (approve, delete, correct stock) and every sensitive figure (purchase rate, sale rate) gets a switch. The Admin builds roles from these switches and gives each user a role and the locations they work in. The rules are enforced inside the program, not only by hiding buttons. A hidden rate is not sent to that person's screen at all, so it cannot be dug out.

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

### Stage 0 – Go live with today's version (Hiten)
- Version 2.0 is checked, pushed and tagged `v2.0` (14 Nov 2026).
- Rollout to the counter PC as described in `README.md` (installing / rolling out).
- **Done when:** the counter PC runs the current version for a week without problems.

### Stage 1 – Foundation: users, permissions, shop network (Large)
- **1a. Tidy-up (nothing visible):** move the remaining rules from screens into the rule book, and fix the known weak spots that sit in the same code on the way (details in `README.md`, known issues):
  - the line form on Sale / Purchase / Sales Order accepts quantity 0 or negative and discount over 100, and a very fast double Enter could add a line twice;
  - a voucher with a discount but no lines gets a negative total;
  - a sale that is never summarised keeps a stored total of 0;
  - "not found" returns an empty record instead of nothing;
  - some error messages show technical text.
- **1b. Users and permissions:** user management screen (add, disable, reset password); roles built from switches; menus and screens follow the role; purchase/sales split; rate hiding both ways; users tied to locations (one location for now).
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
10. **Roles (stage 1):** a first list of job titles and the people in them, so the first set of roles can be proposed.
11. **Scanning on phones (stage 4):** phone camera, or hand scanners paired with the phone/tablet?
12. **Approvals (stage 1):** who stands in when an approver is away? May one person ever do two steps of the same entry (count and approve)?
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
