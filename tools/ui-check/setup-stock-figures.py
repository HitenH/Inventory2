# Test-copy setup for check-stock-figures.mjs (version 3.0, stage 2, batches 2-1/2-2). Development only: never run it on the real data.
# Usage: python tools/ui-check/setup-stock-figures.py <scratch>\data\Inventory.db
# Run it after the stage 2 upgrade is on the copy (start the test copy once, or `dotnet ef database update --connection ...`).
# There is no screen yet that records damage (that is batch 2-3), so this writes stock changes straight into the copy:
# product AJ0120103 (1 NO HAATICHAP, the product check-look.mjs opens): variant G 3 damaged (approved) and 2 more waiting
# for approval; variant L 1 damaged; variant B untouched. Writes <folder>\stock-figures.json with the expected figures.
import json
import os
import sqlite3
import sys
import uuid

path = sys.argv[1]
db = sqlite3.connect(path)
c = db.cursor()
c.execute("update Users set Password='UiTest#2026' where Name='Admin'")  # the app hashes plain text at start-up

product_id, product_name, product_guid = c.execute("select ProductId, Name, Id from Products where ProductId='AJ0120103'").fetchone()


def variant(code):
    vid = c.execute("select Id from Variants where ProductEntityId=? and VariantId=?", (product_guid, code)).fetchone()[0]
    stock = c.execute("select (select coalesce(sum(Quantity),0) from PurchaseVariant where VariantEntityId=?)"
                      " - (select coalesce(sum(Quantity),0) from SalesVariants where VariantEntityId=?)", (vid, vid)).fetchone()[0]
    return vid, stock


# Running it twice starts again: remove this script's earlier stock changes.
c.execute("delete from StockChangeLines where StockChangeEntityId in (select Id from StockChanges where Remarks='ui-check stock figures')")
c.execute("delete from StockChanges where Remarks='ui-check stock figures'")


def change(status, lines):
    sid = str(uuid.uuid4()).upper()
    c.execute("insert into StockChanges (Id, Kind, VoucherId, Date, Status, Remarks, CreatedAt, CreatedBy)"
              " values (?, 'Damage', 1, '2026-11-25 00:00:00', ?, 'ui-check stock figures', '2026-11-25 10:00:00', 'Admin')", (sid, status))
    for n, (vid, qty) in enumerate(lines, 1):
        # Stage 3: the sellable side of a line is in a place (the shop, place 1); the database refuses a line without one.
        c.execute("insert into StockChangeLines (Id, StockChangeEntityId, SerialNumber, ProductEntityId, VariantEntityId, Quantity, \"From\", \"To\", FromPlaceId, Reason, ReadyToSend)"
                  " values (?, ?, ?, ?, ?, ?, 'Sellable', 'Damaged', 1, 'Broken in transit', 0)", (str(uuid.uuid4()).upper(), sid, n, product_guid, vid, qty))


g, g_stock = variant("G")
l, l_stock = variant("L")
b, b_stock = variant("B")
change("Approved", [(g, 3), (l, 1)])
change("Waiting", [(g, 2)])
db.commit()

expected = {
    "productGuid": product_guid, "productId": product_id, "productName": product_name,
    "G": {"sellable": g_stock - 3, "damaged": 3, "waiting": 2},
    "L": {"sellable": l_stock - 1, "damaged": 1, "waiting": 0},
    "B": {"sellable": b_stock, "damaged": 0, "waiting": 0},
}
out = os.path.join(os.path.dirname(os.path.abspath(path)), "stock-figures.json")
with open(out, "w") as f:
    json.dump(expected, f, indent=1)
print(json.dumps(expected))
print("written", out)
