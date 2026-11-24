// Version 3.0, batch 1b-4: screens and the menu follow each person's roles.
// Needs, in the test copy, before the test app starts (plain-text passwords are hashed at start-up):
//   role UiSales with screen.sales + screen.customers; user UiClerk (password UiClerk#2026) holding it;
//   user UiGone (password UiGone#2026), disabled, holding the Admin role.
// The script changes UiSales in the database while UiClerk is logged in, to prove a role change applies on the next screen.
// Development only. Never point this at the real data.
import { launch, login, sleep } from './cdp.mjs';
import { DatabaseSync } from 'node:sqlite';

const log = (...a) => console.log(...a);
let failures = 0;
function expect(what, actual, wanted) {
  const ok = actual === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${actual}${ok ? '' : `  (expected ${wanted})`}`);
}

const dbPath = (process.env.SCRATCH || '') + 'data\\Inventory.db';
const menu = b => b.eval(`document.querySelector('.mud-drawer')?.innerText ?? ''`);
const denied = b => b.eval(`!!document.querySelector('#access-denied')`);
const importShown = b => b.eval(`[...document.querySelectorAll('button')].some(x => x.innerText.trim() === 'Import')`);
async function open(b, path) {
  await b.goto(path);
  await sleep(1800);
}
async function loginAs(b, name, password) {
  await b.goto('/login');
  await b.waitFor(`document.querySelector('#login-name')`);
  await b.click('#login-name'); await b.type(name);
  await b.click('#login-password'); await b.type(password);
  await b.key('Enter');
  await sleep(2500);
}

let b;
try {
  log('--- Admin sees everything, as before');
  b = await launch();
  await login(b);
  const adminMenu = await menu(b);
  for (const item of ['Dashboard', 'Settings', 'Reports', 'Purchases', 'Sales', 'Customers']) {
    expect(`Admin menu has ${item}`, adminMenu.includes(item), true);
  }
  await open(b, '/purchases');
  expect('Admin opens Purchases', await denied(b), false);
  expect('Admin sees Import', await importShown(b), true);
  b.close();

  log('--- a Sales clerk sees only Sales and Customers');
  b = await launch();
  await loginAs(b, 'UiClerk', 'UiClerk#2026');
  expect('clerk lands on the welcome text', await b.eval(`!!document.querySelector('#access-welcome')`), true);
  const clerkMenu = await menu(b);
  expect('clerk menu has Sales', clerkMenu.includes('Sales'), true);
  expect('clerk menu has Customers', clerkMenu.includes('Customers'), true);
  for (const item of ['Dashboard', 'Settings', 'Reports', 'Purchases', 'Suppliers', 'Products']) {
    expect(`clerk menu hides ${item}`, clerkMenu.includes(item), false);
  }
  await open(b, '/sales');
  expect('clerk opens Sales', await denied(b), false);
  await open(b, '/customers');
  expect('clerk opens Customers', await denied(b), false);
  expect('clerk does not see Import', await importShown(b), false);
  for (const path of ['/purchases', '/purchase', '/settings', '/users', '/roles', '/reports', '/product']) {
    await open(b, path);
    expect(`typed address ${path} is refused`, await denied(b), true);
  }

  log('--- a role change applies on the next screen');
  const db = new DatabaseSync(dbPath);
  const roleId = db.prepare(`select Id from Roles where Name = 'UiSales'`).get().Id;
  db.prepare(`insert into RolePermissions (RoleId, Permission) values (?, 'screen.purchases')`).run(roleId);
  db.close();
  await open(b, '/purchases');
  expect('clerk opens Purchases after the role gained it', await denied(b), false);
  expect('clerk menu now has Purchases', (await menu(b)).includes('Purchases'), true);
  b.close();

  log('--- a disabled user cannot log in');
  b = await launch();
  await loginAs(b, 'UiGone', 'UiGone#2026');
  expect('still on the login page', await b.path(), '/login');
  expect('says the user is disabled', (await b.eval(`document.body.innerText`)).includes('This user is disabled'), true);
  b.close();
  b = null;

  log(failures === 0 ? '\nALL ACCESS CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`);
} catch (e) {
  log('ERROR', e.message);
  if (b) { await b.shot((process.env.SCRATCH || '.') + '\\access-error.png'); }
  failures++;
} finally {
  if (b) { b.close(); }
}

process.exit(failures === 0 ? 0 : 1);
