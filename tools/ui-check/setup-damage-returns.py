# Test-copy setup for check-damage-returns.mjs (version 3.0, stage 2, batches 2-3/2-4). Development only: never run it on the real data.
# Usage: python tools/ui-check/setup-damage-returns.py <scratch>\data\Inventory.db   (before starting the test copy)
# Sets the Admin test password and picks the newest purchase that has at least two lines (all above 0 pieces);
# writes <folder>\damage-returns.json with its id and lines for the check.
import json
import os
import sqlite3
import sys

path = sys.argv[1]
db = sqlite3.connect(path)
c = db.cursor()
c.execute("update Users set Password='UiTest#2026' where Name='Admin'")  # the app hashes plain text at start-up
db.commit()

purchase = c.execute("""
    select p.Id, p.VoucherId, p.Date from Purchases p
    where (select count(*) from PurchaseVariant v where v.PurchaseEntityId = p.Id and v.Quantity > 1) >= 2
      and not exists (select 1 from PurchaseVariant v where v.PurchaseEntityId = p.Id and coalesce(v.Quantity, 0) <= 0)
    order by p.Date desc limit 1""").fetchone()
lines = c.execute("""
    select v.Id, v.SerialNumber, v.Quantity, pr.Id, pr.ProductId, pr.Name, va.VariantId
    from PurchaseVariant v join Products pr on pr.Id = v.ProductEntityId join Variants va on va.Id = v.VariantEntityId
    where v.PurchaseEntityId = ? order by v.SerialNumber""", (purchase[0],)).fetchall()
out = {
    "purchaseId": purchase[0], "voucher": purchase[1], "date": purchase[2],
    "lines": [{"lineId": l[0], "serial": l[1], "qty": l[2], "productGuid": l[3], "productId": l[4], "productName": l[5], "variantId": l[6]} for l in lines],
}
target = os.path.join(os.path.dirname(os.path.abspath(path)), "damage-returns.json")
with open(target, "w") as f:
    json.dump(out, f, indent=1)
print(json.dumps(out)[:400])
print("written", target)
