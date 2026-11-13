# Inventory App – Review: current status and open items

**Original review:** 10 Nov 2026 (health score **4.5 / 10**). Full text is in git history (commit `13ea0be`, file REVIEW.md). This file keeps only what still matters.
**Updated:** 13 Nov 2026, session 8. Current health score: **about 8 / 10**. Batch plan and what to do next: `PLAN.md`.
**Verdict (unchanged):** keep the stack (Blazor Server + .NET + EF Core + SQLite) and keep improving it. A rebuild is not recommended.

---

## 1. What is fixed (summary)
- **Security:** passwords hashed (no plain text in code or DB); every page needs login; backups and data moved out of the program folder.
- **Data integrity:** unique IDs ignoring case; blank master records and foreign lines refused by the database; each save is one transaction; purchase and sales-order totals always recalculated; sales-order discount stored; empty sale refused; stale per-tab database context removed.
- **Operations:** online SQLite backup, daily automatic, integrity-checked, safe restore; .NET 10, EF Core 10, MudBlazor 9.12; Serilog daily logs; 150 automatic tests and GitHub Actions.
- **Code:** one line editor, one picker, one confirm helper, one sorter, one stock query; AutoMapper and Blazor.Bootstrap removed; dead code removed; `async void` gone; Import (old) and roles (old) removed in session 8.
- **Performance:** database sums for totals; 100-row paging; photos served from `/photos/{id}`; report sums in SQL; one-row variant saves.
- **Usability (owner's requests, tested):** keyboard entry and Alt-keys, dark mode readable, saved names on reopened vouchers, Bulk Entry, Open default status, status change for ticked orders. Alt+B approved (session 8).
- **Session 8:** cancel changes nothing on sale and line forms and PO rows; Sale summary 2 decimals; Purchase Orders ticks cleared on reload; deleted variant photo no longer returns; By Area search null fix; duplicate-ID log check ignores case.
- **Session 9 (batch 17, UI refresh, awaiting owner's test):** one theme (5 colours, light/dark, chosen on Settings, remembered per PC); three button colours; red X marks; cards with small labels; Product Name gets the spare width in every table; entry rows wrap instead of scrolling sideways; Sale scan boxes grouped with the line row. Behaviour, keyboard and data untouched (verified by the ui-check scripts and the new `check-look.mjs`).

---

## 2. Open items – owner decisions needed first
| Code | Item | Status |
|---|---|---|
| M13 | Created / last changed time stamps | **Question open since 13 Nov.** Options A / B (recommended) / C in PLAN.md step 2a. |
| D3 | Two people editing the same record (RowVersion) | Open; needs owner decision (batch 15b). |
| D5 | Stock adjustments with a reason (StockMovement ledger) | Open; needs owner decision (batch 15c). |
| – | Discount naming (`DiscountPercent` / `DiscountAmount`) | Internal rename; nothing visible (batch 15d). |
| D4 | Selling more than in stock | **Pinned by owner** ("not now"). Recommended later: warn + show stock, never block. |
| D2 / B1 | Duplicate voucher numbers in old data | **Pinned by owner.** Options in PLAN.md "Pinned decisions". |
| S3 | Users and roles | Old role removed (session 8). **Batch 16**, clean slate. |
| B5 | CSV/Excel import | Old one removed (session 8). **Batch 8**: build a new one with preview and all-or-nothing import. Deferred to last by owner. |
| – | Several photos/videos per variant | **Pinned by owner.** |

## 3. Open items – smaller (fold in where they fit)
| Code | Item |
|---|---|
| B9 | "Not found" returns an empty record instead of nothing (Product, Purchase, Sale, Sales Order repositories). |
| B11 | Purchase Orders store free-text supplier / product / variant IDs (no links, no pickers). |
| B12 | Delete errors show technical exception text (e.g. Supplier snackbar "GetSupplier error: …", Customer "Show Full"). |
| S5 | HTTP only (fine for one local PC; needs HTTPS if the app ever leaves the machine). |
| S6 | Raw database error text in some snackbars. |
| D6 | SQLite allows one writer at a time (fine for one PC; limited for many users). Server-side paging only if lists reach tens of thousands of rows. |
| A1 | Business rules partly still in screens (totals and line rules are now in the data layer). Optional. |
| P6 | Pickers reload their list (largely fixed in batch 12; not re-checked). |

## 4. Session 8 review findings – open (owner's yes needed; each changes what the operator sees)
| # | Where | Problem | Suggested fix |
|---|---|---|---|
| N5 | Purchase Orders and Sales Orders lists | A failed save or delete is only written to the log; nothing is shown. | Show the error message. |
| N6 | Sale | Summarize with no items silently goes back to the list. Scan Mass Products silently drops codes that are not found; its "Scanned:" message is always blank. | Show messages; list skipped codes. |
| N7 | Sale / Purchase / Sales Order line form | Accepts quantity 0 or negative and discount over 100 (Bulk Entry refuses them). Clearing the product box keeps the old product behind it. A very fast double Enter could add a line twice. | Same checks as Bulk Entry; a "saving" guard. |
| N8 | `Domain/TransactionTotals.cs` | A voucher with a discount but no lines is saved with total = minus the discount, which lowers supplier and report totals. | Total 0 when there are no lines. |
| N9 | Sale that is never summarised | Its items reduce stock, but the stored total stays 0, so customer and period totals miss it. | Fill the total on save, or flag it in the list. |
| N10 | Report searches (Customers, Sales Orders, Purchase Orders, reorder level, variants) | Case-sensitive; Customers report search matches almost everything (it searches the Mobiles list text). Sorting by Image / Stock In Hand on the product page does not work; Image may error. | Case-insensitive search on the right fields; fix the sort. |
| N11 | Reports → Sales & Purchases, "Last 1 / 3 / 6 / 12 months" | Include one day more than "Last 7 days" implies. | Owner to choose. |
| N12 | Housekeeping | Browser may open before the app is ready (refresh fixes it). First-run Admin password is written to the log and to a text file that is never removed. "before-restore" safety copies and stray `.tmp` files are never cleaned up. A non-JPEG variant photo is ignored without a message. | Small fixes. |
| N13 | Old sale with a blank product line (19-05-2026) | Cannot be re-saved as a whole because of the batch 5 rule. | **Known – leave.** |

Closed: **N1, N2, N4** fixed in session 8. **N3** closed by the owner (Bulk Entry keeps "not added = not saved").

## 5. Known limits (accepted, do not "fix" without asking)
- Blazor Server: each click goes through a live connection. Fine on one local PC; a phone or tablet in the aisles would need another front end (Blazor WebAssembly/PWA or a mini server).
- Variant photo: `VariantModel.Image` still holds the photo record and the product page loads photo bytes on the server. Revisit with the pinned photos decision.
- `Mobile` lists on Customer and Supplier models are kept (phone sync depends on them).
- Old backups from before batch 2 may be incomplete (WAL mode). Keep, do not trust.
- Warnings: 333 hints (mostly nullable). Not a fault; leave them, add no new ones.
- Future options if the business grows (multiple sites, phones in the aisles, accounting or tax integration): see the 10 Nov review in git history. Not needed now.
