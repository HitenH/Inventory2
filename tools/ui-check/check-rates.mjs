// Version 3.0, batch 1b-5: purchase and sale rates are left out of the page for people without the switch.
// Needs, in the test copy (plain-text passwords are hashed at start-up):
//   UiBuyer (password UiBuyer#2026) holding the starter role Purchase, UiSeller (UiSeller#2026) holding Sales,
//   UiNoRates (UiNoRates#2026) holding a role that opens every screen but has neither rate switch.
// Read-only: it opens screens and changes nothing. Development only; never point it at the real data.
import { launch, login, sleep } from './cdp.mjs';
import { DatabaseSync } from 'node:sqlite';

const log = (...a) => console.log(...a);
let failures = 0;
function expect(what, actual, wanted) {
  const ok = actual === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${actual}${ok ? '' : `  (expected ${wanted})`}`);
}

// Records to open, taken from the test copy itself.
const db = new DatabaseSync((process.env.SCRATCH || '') + 'data\\Inventory.db');
const one = sql => Object.values(db.prepare(sql).get() ?? {})[0];
const purchaseId = one(`select PurchaseEntityId from PurchaseVariant where Amount > 0 order by rowid desc limit 1`);
const salesOrderId = one(`select SalesOrderEntityId from SalesOrderVariants order by rowid desc limit 1`);
const saleId = one(`select SalesEntityId from SalesSummary order by rowid desc limit 1`);
const productId = one(`select Id from Products where Rate > 0 limit 1`);
db.close();

// Every heading, field label and card title on the screen that names a money figure.
const MONEY = ['Rate', 'Amount', 'Disc %', 'After Disc.', 'Discount', 'Total Amount', 'Total Sales', 'Total Purchases'];
const moneyOnScreen = b => b.eval(`(() => {
  const words = ${JSON.stringify(MONEY)}.map(w => w.toUpperCase());
  const texts = [...document.querySelectorAll('th, label, .mud-input-label, h6')].map(e => e.innerText.trim().toUpperCase());
  return [...new Set(texts.filter(t => words.includes(t)))].sort().join(',');
})()`);
const denied = b => b.eval(`!!document.querySelector('#access-denied')`);

async function loginAs(b, name, password) {
  await b.goto('/login');
  await b.waitFor(`document.querySelector('#login-name')`);
  await b.click('#login-name'); await b.type(name);
  await b.click('#login-password'); await b.type(password);
  await b.key('Enter');
  await sleep(2500);
}
async function screen(b, who, path, wantMoney) {
  await b.goto(path);
  await sleep(2500);
  if (await denied(b)) { expect(`${who} ${path} opens`, false, true); return; }
  const money = await moneyOnScreen(b);
  expect(`${who} ${path} shows money (${money || 'none'})`, money.length > 0, wantMoney);
}
async function reportTab(b, who, tab, inner, wantMoney) {
  await b.goto('/reports');
  await sleep(2000);
  await b.eval(`[...document.querySelectorAll('.mud-tab')].find(t => t.innerText.trim().toUpperCase() === ${JSON.stringify(tab.toUpperCase())}).click()`);
  await sleep(1500);
  if (inner) {
    await b.eval(`[...document.querySelectorAll('.mud-tab')].find(t => t.innerText.trim().toUpperCase() === ${JSON.stringify(inner.toUpperCase())}).click()`);
    await sleep(1500);
  }
  const money = await moneyOnScreen(b);
  expect(`${who} report ${tab}${inner ? ' / ' + inner : ''} shows money (${money || 'none'})`, money.length > 0, wantMoney);
}

// Bulk Entry on the purchase: its Rate and Discount boxes follow the purchase-rates switch too.
async function bulkEntry(b, who, wantRates) {
  await b.goto(`/purchase/${purchaseId}`);
  await sleep(2500);
  await b.click('[data-shortcut="bulk"]');
  await b.waitFor(`document.querySelector('#bulk-quantity')`);
  await sleep(500);
  expect(`${who} Bulk Entry shows Rate`, await b.eval(`!!document.querySelector('#bulk-rate')`), wantRates);
  expect(`${who} Bulk Entry shows Discount`, await b.eval(`!!document.querySelector('#bulk-discount')`), wantRates);
  await b.key('Escape'); await sleep(800);
}

const purchase = `/purchase/${purchaseId}`, salesOrder = `/salesorder/${salesOrderId}`;
const summary = `/salessummary/${saleId}`, product = `/product/${productId}`;
log('records used:', { purchase, salesOrder, summary, product });

let b;
try {
  log('--- Admin sees every figure (proves the check can see them)');
  b = await launch(); await login(b);
  for (const p of [purchase, salesOrder, summary, '/products', product, '/suppliers', '/customers', '/purchaseorder']) {
    await screen(b, 'Admin', p, true);
  }
  await reportTab(b, 'Admin', 'Sales & Purchases', null, true);
  await bulkEntry(b, 'Admin', true);
  b.close();

  log('--- Purchase role: purchase figures only');
  b = await launch(); await loginAs(b, 'UiBuyer', 'UiBuyer#2026');
  await screen(b, 'UiBuyer', purchase, true);
  await screen(b, 'UiBuyer', '/purchaseorder', true);
  await screen(b, 'UiBuyer', '/suppliers', true);
  await screen(b, 'UiBuyer', '/products', false);   // the product rate is the selling price
  await screen(b, 'UiBuyer', product, false);
  b.close();

  log('--- Sales role: sale figures only');
  b = await launch(); await loginAs(b, 'UiSeller', 'UiSeller#2026');
  await screen(b, 'UiSeller', salesOrder, true);
  await screen(b, 'UiSeller', summary, true);
  await screen(b, 'UiSeller', '/customers', true);
  await screen(b, 'UiSeller', '/products', true);
  await screen(b, 'UiSeller', product, true);
  b.close();

  log('--- every screen, no rate switches: no figures anywhere');
  b = await launch(); await loginAs(b, 'UiNoRates', 'UiNoRates#2026');
  for (const p of [purchase, salesOrder, summary, '/products', product, '/suppliers', '/customers', '/purchaseorder']) {
    await screen(b, 'UiNoRates', p, false);
  }
  await reportTab(b, 'UiNoRates', 'Customer', 'By Area', false);
  await reportTab(b, 'UiNoRates', 'Customer', 'Customers', false);
  await reportTab(b, 'UiNoRates', 'Supplier', null, false);
  await reportTab(b, 'UiNoRates', 'Purchases Orders', null, false);
  await reportTab(b, 'UiNoRates', 'Sales & Purchases', null, false);
  expect('Sales & Purchases says there is nothing to show', await b.eval(`!!document.querySelector('#report-no-rates')`), true);
  await bulkEntry(b, 'UiNoRates', false);
  b.close();
  b = null;

  log(failures === 0 ? '\nALL RATE CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`);
} catch (e) {
  log('ERROR', e.message);
  if (b) { await b.shot((process.env.SCRATCH || '.') + '\\rates-error.png'); }
  failures++;
} finally {
  if (b) { b.close(); }
}

process.exit(failures === 0 ? 0 : 1);
