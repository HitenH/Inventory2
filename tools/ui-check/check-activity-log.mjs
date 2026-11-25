// Version 3.0, batches 1c-3 and 1c-4: the activity log is written by real screens and read in the
// "See activity log" side panel. Checks: a customer change shows before -> after with the person; an old record
// says its earlier history was not recorded; Esc closes the panel and stays on the screen; a purchase discount
// (a purchase rate) is shown to Admin but not to a user without "See purchase rates"; the Purchase Orders row icon
// opens the panel without starting a row edit; a user without "See activity log" has no button.
// Development only. Never point this at the real data - it changes a customer and a purchase.
//
// Test copy setup (besides the usual steps in README.md), then pass the two ids it prints:
//   python tools/ui-check/setup-activity-log.py <scratch>\data\Inventory.db
//   $env:LOG_CUSTOMER='<customer id>'; $env:LOG_PURCHASE='<purchase id>'
// It sets the test password UiTest#2026 for Admin, and adds role UiLogNoRates (Customers, Purchases, Purchase
// Orders, See activity log; no rates) with user UiLogViewer, and role UiNoLog (Customers) with user UiNoLog.
import { launch, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const customerId = process.env.LOG_CUSTOMER;
const purchaseId = process.env.LOG_PURCHASE;
if (!customerId || !purchaseId) { console.error('Set LOG_CUSTOMER and LOG_PURCHASE (see the top of this file).'); process.exit(2); }
const shots = process.env.SCRATCH || '.';

let failures = 0;
function expect(what, actual, wanted) {
  const ok = actual === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}
function expectIncludes(what, text, part, wanted = true) {
  const ok = String(text).includes(part) === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${wanted ? 'has' : 'has not'} ${JSON.stringify(part)}${ok ? '' : `  (panel: ${JSON.stringify(text)})`}`);
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

const setBox = (b, selector, value) => b.eval(`(() => {
  const i = document.querySelector(${JSON.stringify(selector)});
  if (!i) throw new Error('no box ${selector}');
  const set = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(i), 'value').set;
  set.call(i, ${JSON.stringify(value)});
  i.dispatchEvent(new Event('input', { bubbles: true }));
  i.dispatchEvent(new Event('change', { bubbles: true }));
})()`);
const open = async (b, path, ready) => { await b.goto(path); await b.waitFor(ready, 10000); await sleep(800); };
const panelText = b => b.eval(`document.querySelector('#activity-panel')?.innerText ?? ''`);
async function openPanel(b, selector = '#activity-log-open') {
  await b.click(selector);
  await b.waitFor(`document.querySelector('#activity-panel .activity-group, #activity-panel .activity-earlier, #activity-panel .activity-empty')`, 8000);
  await sleep(300);
  return panelText(b);
}
async function closePanelWithEsc(b) {
  await b.key('Escape');
  await b.waitFor(`!document.querySelector('#activity-panel')`, 5000).catch(() => { });
}

const b = await launch(9333);
try {
  await loginAs(b, 'Admin');

  log('--- customer: change the area, then read the panel');
  const customerPath = `/customer/${customerId}`;
  await open(b, customerPath, `document.querySelector('#area')`);
  const oldArea = await b.eval(`document.querySelector('#area').value`);
  const newArea = oldArea === 'LOG CHECK AREA' ? 'LOG CHECK AREA 2' : 'LOG CHECK AREA';
  await setBox(b, '#area', newArea);
  await b.click('#button-submit'); await sleep(1500);
  await open(b, customerPath, `document.querySelector('#activity-log-open')`);
  let text = await openPanel(b);
  expectIncludes('panel shows the change before -> after', text, `Area: ${oldArea || '(blank)'} → ${newArea}`);
  expectIncludes('panel shows who', text, 'Admin');
  expectIncludes('old record: earlier history not recorded', text, 'Older history: not recorded (before version 3.0).');
  await b.shot(`${shots}/activity-customer.png`);
  await closePanelWithEsc(b);
  expect('Esc closes the panel', await b.eval(`!!document.querySelector('#activity-panel')`), false);
  expect('... and stays on the customer', await b.path(), customerPath);

  log('--- purchase: remarks and discount; Admin sees both');
  const purchasePath = `/purchase/${purchaseId}`;
  const discountBox = `(document.querySelector('label[for=discount]')?.parentElement.querySelector('input'))`;
  await open(b, purchasePath, `document.querySelector('#Remarks') && ${discountBox}`);
  await setBox(b, '#Remarks', 'activity log check');
  await b.click('button.btn-warning#button-submit'); await sleep(1500);
  const oldDiscount = await b.eval(`${discountBox}.value`);
  const newDiscount = parseFloat(oldDiscount) === 7 ? '8' : '7';
  // Typed like a person: the discount box saves as soon as its value is committed.
  // A real click (the screen puts the cursor in the product box for a moment after it opens), then Ctrl+A.
  await sleep(1500);
  await b.click('.totals-field:has(label[for=discount]) input');
  await b.key('a', 2);
  await b.type(newDiscount);
  await b.key('Tab');
  await sleep(2000);
  await open(b, purchasePath, `document.querySelector('#activity-log-open')`);
  text = await openPanel(b);
  expectIncludes('Admin sees the remarks change', text, 'Remarks:');
  expectIncludes('Admin sees the discount change', text, `→ ${newDiscount}`);
  await b.shot(`${shots}/activity-purchase-admin.png`);
  await closePanelWithEsc(b);

  log('--- a new purchase: lines added while creating fold into one "Created" entry (owner, 25 Nov 2026)');
  const pickFromList = async t => {
    await b.type(t);
    await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
    await b.key('ArrowDown'); await sleep(300); await b.key('Enter'); await sleep(2000);
  };
  const addLine = async qty => {
    await b.eval(`(() => { const s = document.querySelector('#variantid'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
    await sleep(400);
    await b.eval(`(() => { const label = [...document.querySelectorAll('label')].find(l => l.textContent.trim() === 'Qty'); label.parentElement.querySelector('input').focus(); })()`);
    await b.type(String(qty)); await b.key('Tab'); await sleep(400);
    await b.click('.btn-order-add'); await sleep(2500);
  };
  await b.goto('/purchase'); await sleep(1500);
  await pickFromList('BALAJI');
  await pickFromList('LAZER');
  await addLine(2);
  await pickFromList('CUTTING');
  await addLine(3);
  text = await openPanel(b);
  expectIncludes('one "Created" entry for the new purchase', text, 'Created: supplier');
  expectIncludes('its lines are summed up', text, '2 lines added while creating, 5 pieces');
  expectIncludes('the lines themselves are folded away', text, 'Line added:', false);
  await b.click('#activity-panel .activity-show-lines'); await sleep(500);
  text = await panelText(b);
  expectIncludes('"show lines" shows them', text, 'Line added:');
  await b.shot(`${shots}/activity-new-purchase.png`);
  await closePanelWithEsc(b);

  log('--- an existing purchase: four lines added one by one in one sitting are one folded entry');
  await open(b, purchasePath, `document.querySelector('#activity-log-open')`);
  await sleep(1500);
  for (const [product, qty] of [['LAZER', 1], ['CUTTING', 2], ['LAZER', 3], ['CUTTING', 4]]) {
    await pickFromList(product);
    await addLine(qty);
  }
  text = await openPanel(b);
  expectIncludes('one entry sums up the four lines', text, '4 lines added, 10 pieces');
  // Only the newest entry: older entries in a copy of real data may be from before folding existed.
  expectIncludes('... with the lines folded away', await b.eval(`document.querySelector('#activity-panel .activity-group')?.innerText ?? ''`), 'Line added:', false);
  await b.shot(`${shots}/activity-sitting.png`);
  await closePanelWithEsc(b);

  log('--- Purchase Orders: the row icon opens the panel, not a row edit');
  await open(b, '/purchaseorder', `document.querySelector('.activity-log-open')`);
  const editorsBefore = await b.eval(`document.querySelectorAll('.mud-table tbody .mud-input-control').length`);
  text = await openPanel(b, '.activity-log-icon .activity-log-open');
  expectIncludes('the panel opened from the row icon', await b.eval(`document.querySelector('#activity-title')?.innerText ?? ''`), 'Activity – Purchase order');
  await closePanelWithEsc(b);
  expect('the row did not switch to editing', await b.eval(`document.querySelectorAll('.mud-table tbody .mud-input-control').length`), editorsBefore);
  expect('still on Purchase Orders', await b.path(), '/purchaseorder');

  log('--- a user without "See purchase rates" does not see the discount line');
  await b.eval('sessionStorage.clear()');   // log out: the login lives in this tab's session storage
  await loginAs(b, 'UiLogViewer');
  await open(b, purchasePath, `document.querySelector('#activity-log-open')`);
  text = await openPanel(b);
  expectIncludes('viewer sees the remarks change', text, 'Remarks:');
  expectIncludes('viewer does not see the discount change', text, 'Discount:', false);
  await b.shot(`${shots}/activity-purchase-viewer.png`);
  await closePanelWithEsc(b);

  log('--- a user without "See activity log" has no button');
  await b.eval('sessionStorage.clear()');   // log out: the login lives in this tab's session storage
  await loginAs(b, 'UiNoLog');
  await open(b, customerPath, `document.querySelector('#area')`);
  expect('no "See activity log" button', await b.eval(`!!document.querySelector('#activity-log-open')`), false);
} catch (e) {
  failures++;
  log('FAIL (stopped):', e.message);
} finally {
  b.close();
}

log(failures ? `\n${failures} problem(s)` : '\nall checks passed');
process.exit(failures ? 1 : 0);
