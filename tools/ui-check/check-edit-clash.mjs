// Version 3.0, batches 1c-1 and 1c-2: two people in two browsers on the same record.
// The second save is refused with "Admin changed this ... while you had it open", a Reload button shows the
// other person's change, and saving again then works. Saving twice from the same screen is never a clash.
// Development only. Never point this at the real data - it changes a customer and a purchase.
//
// Test copy setup (besides the usual steps in README.md): give Hiten the test password too, and pass the
// customer and purchase to use (the customer AAI MATHA JEWELLERY and the newest purchase with lines), for example:
//   python -c "import sqlite3; c=sqlite3.connect(r'<scratch>\data\Inventory.db'); c.execute(\"update Users set Password='UiTest#2026' where Name in ('Admin','Hiten')\"); c.commit(); print(c.execute(\"select Id from Customers where Name='AAI MATHA JEWELLERY'\").fetchone()[0]); print(c.execute('select p.Id from Purchases p where exists(select 1 from PurchaseVariant v where v.PurchaseEntityId=p.Id) order by p.Date desc limit 1').fetchone()[0])"
//   $env:CLASH_CUSTOMER='<customer id>'; $env:CLASH_PURCHASE='<purchase id>'
// Use the ids exactly as printed. After the run, the test copy's Customers row shows ChangedBy = Hiten.
import { launch, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const customerId = process.env.CLASH_CUSTOMER;
const purchaseId = process.env.CLASH_PURCHASE;
if (!customerId || !purchaseId) { console.error('Set CLASH_CUSTOMER and CLASH_PURCHASE (see the top of this file).'); process.exit(2); }

let failures = 0;
function expect(what, actual, wanted) {
  const ok = actual === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}
function expectIncludes(what, text, part) {
  const ok = String(text).includes(part);
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(text)}${ok ? '' : `  (expected to contain ${JSON.stringify(part)})`}`);
}

async function loginAs(b, name) {
  await b.goto('/login');
  await b.waitFor(`document.querySelector('#login-name')`);
  await b.click('#login-name'); await b.type(name);
  await b.click('#login-password'); await b.type('UiTest#2026');
  await b.key('Enter');
  await b.waitFor(`location.pathname === '/'`);
  await sleep(1000);
}

// A text box set the way typing arrives (value, then input and change events).
const setBox = (b, selector, value) => b.eval(`(() => {
  const i = document.querySelector(${JSON.stringify(selector)});
  if (!i) throw new Error('no box ${selector}');
  const set = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(i), 'value').set;
  set.call(i, ${JSON.stringify(value)});
  i.dispatchEvent(new Event('input', { bubbles: true }));
  i.dispatchEvent(new Event('change', { bubbles: true }));
})()`);
const boxValue = (b, selector) => b.eval(`document.querySelector(${JSON.stringify(selector)})?.value ?? null`);
const banner = b => b.eval(`document.querySelector('#record-changed-text')?.innerText ?? ''`);
const open = async (b, path, ready) => { await b.goto(path); await b.waitFor(ready, 10000); await sleep(800); };

const admin = await launch(9333);
const hiten = await launch(9334);
try {
  await loginAs(admin, 'Admin');
  await loginAs(hiten, 'Hiten');

  log('--- customer: both open it, Admin saves first');
  const customerPath = `/customer/${customerId}`;
  const nameReady = `document.querySelector('#name')?.value`;
  await open(admin, customerPath, nameReady);
  await open(hiten, customerPath, nameReady);
  const original = await boxValue(admin, '#name');
  await setBox(admin, '#name', original + ' A');
  await admin.click('#button-submit'); await sleep(1500);
  expect('Admin saw no clash', await banner(admin), '');

  log('--- Hiten saves the old screen: refused, with Reload');
  await setBox(hiten, '#name', original + ' H');
  await hiten.click('#button-submit');
  await hiten.waitFor(`document.querySelector('#record-changed-text')`, 8000).catch(() => { });
  expectIncludes('Hiten is told who changed it', await banner(hiten), 'Admin changed this customer at ');
  expectIncludes('... and what to do', await banner(hiten), 'while you had it open. Reload to see their changes.');
  expect('Reload button is there', await hiten.eval(`!!document.querySelector('#record-changed-reload')`), true);
  await hiten.shot((process.env.SCRATCH || '.') + '/clash-customer.png');

  log('--- Reload shows Admin\'s change; saving again works');
  await hiten.click('#record-changed-reload'); await sleep(2500);
  await hiten.waitFor(nameReady, 10000);
  expect('after Reload Hiten sees Admin\'s name', await boxValue(hiten, '#name'), original + ' A');
  expect('banner gone after Reload', await banner(hiten), '');
  await setBox(hiten, '#name', original);
  await hiten.click('#button-submit'); await sleep(1500);
  expect('Hiten\'s save after Reload goes through', await banner(hiten), '');
  await open(admin, customerPath, nameReady);
  expect('the name is back to the original', await boxValue(admin, '#name'), original);

  log('--- purchase: both open it, Admin updates the remarks');
  const purchasePath = `/purchase/${purchaseId}`;
  const remarksReady = `document.querySelector('#Remarks')`;
  await open(admin, purchasePath, remarksReady);
  await open(hiten, purchasePath, remarksReady);
  const updateButton = 'button.btn-warning#button-submit';
  await setBox(admin, '#Remarks', 'clash check by Admin');
  await admin.click(updateButton); await sleep(1500);
  expect('Admin saw no clash on the purchase', await banner(admin), '');
  await setBox(admin, '#Remarks', 'clash check by Admin, saved twice');
  await admin.click(updateButton); await sleep(1500);
  expect('saving twice from the same screen is not a clash', await banner(admin), '');

  log('--- Hiten updates the old purchase screen: refused');
  await setBox(hiten, '#Remarks', 'clash check by Hiten');
  await hiten.click(updateButton); await sleep(1500);
  expectIncludes('Hiten is told Admin changed the purchase', await banner(hiten), 'Admin changed this purchase at ');
  await hiten.shot((process.env.SCRATCH || '.') + '/clash-purchase.png');
  await hiten.click('#record-changed-reload'); await sleep(2500);
  await hiten.waitFor(remarksReady, 10000); await sleep(800);
  expect('after Reload Hiten sees Admin\'s remarks', await boxValue(hiten, '#Remarks'), 'clash check by Admin, saved twice');

  log('--- the banner goes away when another screen is opened');
  await setBox(admin, '#Remarks', 'clash check, Admin again');
  await admin.click(updateButton); await sleep(1500);
  await setBox(hiten, '#Remarks', 'late');
  await hiten.click(updateButton); await sleep(1500);
  expectIncludes('clash shown again', await banner(hiten), 'Admin changed this purchase');
  await hiten.click('a[href="customers"], a[href="/customers"]'); await sleep(2000);
  expect('banner cleared on another screen', await banner(hiten), '');
} catch (e) {
  failures++;
  log('FAIL (stopped):', e.message);
} finally {
  admin.close();
  hiten.close();
}

log(failures ? `\n${failures} problem(s)` : '\nall checks passed');
process.exit(failures ? 1 : 0);
