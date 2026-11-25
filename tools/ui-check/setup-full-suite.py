# Test-copy setup for the full browser suite (README section 6 lists the checks). Development only: never run it on the
# real data. Run it BEFORE starting the test copy: the app hashes these plain-text passwords at start-up.
#   python tools/ui-check/setup-full-suite.py <scratch>\data\Inventory.db
# Prepares what each check's header asks for:
#   Admin and Hiten: password UiTest#2026 (most checks, check-edit-clash);
#   check-access: role UiSales (Sales + Customers) with user UiClerk, and UiGone (disabled, Admin role);
#   check-rates: UiBuyer (starter role Purchase), UiSeller (starter role Sales), UiNoRates (every screen, no rates);
#   check-activity-log / check-activity-screen: UiLogViewer and UiNoLog (setup-activity-log.py).
# check-users and check-roles change users and roles: give each of them its own fresh copy with only the Admin password set.
# Prints the customer id and purchase id for LOG_CUSTOMER / LOG_PURCHASE (and CLASH_CUSTOMER / CLASH_PURCHASE).
import os
import sqlite3
import subprocess
import sys

path = sys.argv[1]
db = sqlite3.connect(path)
c = db.cursor()
c.execute("pragma foreign_keys = on")
c.execute("update Users set Password='UiTest#2026' where Name in ('Admin', 'Hiten')")

screens = ["screen.dashboard", "screen.customers", "screen.suppliers", "screen.products", "screen.purchase-orders",
           "screen.purchases", "screen.sales-orders", "screen.sales", "screen.reports"]


def role(name, switches, is_admin=0):
    c.execute("delete from Roles where Name=?", (name,))
    c.execute("insert into Roles (Name, IsAdmin) values (?, ?)", (name, is_admin))
    role_id = c.lastrowid
    for key in switches:
        c.execute("insert into RolePermissions (RoleId, Permission) values (?, ?)", (role_id, key))
    return role_id


def role_id(name):
    return c.execute("select Id from Roles where Name=?", (name,)).fetchone()[0]


def user(name, password, rid, disabled=0):
    c.execute("delete from Users where Name=?", (name,))
    c.execute("insert into Users (Name, Password, IsDisabled) values (?, ?, ?)", (name, password, disabled))
    c.execute("insert into UserRoles (UserId, RoleId) values (?, ?)", (c.lastrowid, rid))


user("UiClerk", "UiClerk#2026", role("UiSales", ["screen.sales", "screen.customers"]))
user("UiGone", "UiGone#2026", c.execute("select Id from Roles where IsAdmin=1 order by Id").fetchone()[0], disabled=1)
user("UiBuyer", "UiBuyer#2026", role_id("Purchase"))
user("UiSeller", "UiSeller#2026", role_id("Sales"))
user("UiNoRates", "UiNoRates#2026", role("UiAllScreensNoRates", screens))
db.commit()
db.close()

# The two users of the activity-log checks, and the ids to pass on.
subprocess.run([sys.executable, "-I", os.path.join(os.path.dirname(os.path.abspath(__file__)), "setup-activity-log.py"), path], check=True)
