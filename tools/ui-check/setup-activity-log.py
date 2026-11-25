# Test-copy setup for check-activity-log.mjs (version 3.0, batches 1c-3/1c-4). Development only: never run it on the real data.
# Usage: python tools/ui-check/setup-activity-log.py <scratch>\data\Inventory.db   (before starting the test copy)
# Prints the customer id and purchase id to pass as LOG_CUSTOMER and LOG_PURCHASE.
import sqlite3
import sys

db = sqlite3.connect(sys.argv[1])
c = db.cursor()
c.execute("pragma foreign_keys = on")  # so running this twice also removes the old role links
c.execute("update Users set Password='UiTest#2026' where Name='Admin'")  # the app hashes plain text at start-up


def role(name, switches):
    c.execute("delete from Roles where Name=?", (name,))
    c.execute("insert into Roles (Name, IsAdmin) values (?, 0)", (name,))
    role_id = c.lastrowid
    for key in switches:
        c.execute("insert into RolePermissions (RoleId, Permission) values (?, ?)", (role_id, key))
    return role_id


def user(name, role_id):
    c.execute("delete from Users where Name=?", (name,))
    c.execute("insert into Users (Name, Password, IsDisabled) values (?, 'UiTest#2026', 0)", (name,))
    c.execute("insert into UserRoles (UserId, RoleId) values (?, ?)", (c.lastrowid, role_id))


user("UiLogViewer", role("UiLogNoRates", ["screen.dashboard", "screen.customers", "screen.purchases", "screen.purchase-orders", "activity.see"]))
user("UiNoLog", role("UiNoLog", ["screen.dashboard", "screen.customers"]))
db.commit()
print(c.execute("select Id from Customers where Name like 'AAI MATHA JEWELLERY%' order by Name").fetchone()[0])
print(c.execute("select p.Id from Purchases p where exists(select 1 from PurchaseVariant v where v.PurchaseEntityId=p.Id) order by p.Date desc limit 1").fetchone()[0])
