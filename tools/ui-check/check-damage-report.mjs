// Version 3.0, stage 2, batches 2-5/2-6: the damage report and purchase returns. Records damage on a purchase, then on the
// damage report: rows with the purchase's supplier, Repaired (part of a row, with Enter on the confirmation), Assign supplier,
// different suppliers refused, Supplier and Status changed in the row itself (Hiten, 26 Nov 2026), Ready to send and the
// Show filter, Sent → a purchase return, "See activity log" in the bottom bar; the Purchase Returns list and
// page (damaged goods with where they were found, X puts them back on the report, good stock through the line editor),
// Alt+N and Esc. Setup: tools/ui-check/setup-damage-returns.py on a fresh test copy (DAMAGE_JSON = its damage-returns.json).
// It CHANGES the test copy: never aim it at real data.
import { launch, login, sleep } from './cdp.mjs';
import { readFileSync } from 'node:fs';

const log = (...a) => console.log(...a);
const shots = process.env.SCRATCH || '.';
const want = JSON.parse(readFileSync(process.env.DAMAGE_JSON, 'utf8'));
let failures = 0;
function expect(what, actual, wanted) {
  const ok = JSON.stringify(actual) === JSON.stringify(wanted);
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}
function truthy(what, actual) { expect(what, !!actual, true); }

const b = await launch(9340);
const snackHas = async (text) => { for (let i = 0; i < 20; i++) { if ((await b.snack()).includes(text)) return true; await sleep(250); } return false; };
const setQty = (sel, v) => b.eval(`(() => { const el = document.querySelector('${sel}'); el.focus(); el.value = '${v}'; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); })()`);
async function pickFromList(text) {
  await b.type(text);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300); await b.key('Enter'); await sleep(2000);
}
// A row of the damage report: by product ID and variant.
const lotRow = (pid, vid) => `[...document.querySelectorAll('.damage-report-table tbody tr.damage-lot')].find(r => r.children[1].innerText.trim() === '${pid}' && r.children[3].innerText.trim() === '${vid}')`;
// Supplier and Status are drop-downs in the row (the chosen option is what shows); "No supplier yet" is plain text.
const cellText = `(td => { const s = td.querySelector('select'); return (s ? s.options[s.selectedIndex].text : td.innerText).trim(); })`;
const lotInfo = (pid, vid) => b.eval(`(() => { const r = ${lotRow(pid, vid)}; const t = ${cellText}; return r ? { pieces: r.children[4].innerText.trim(), supplier: t(r.children[8]), status: t(r.children[9]) } : null; })()`);
const rowSelect = (pid, vid, cls, value) => b.eval(`(() => { const s = (${lotRow(pid, vid)}).querySelector('${cls}'); s.value = ${JSON.stringify(value)}; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
const tick = (pid, vid, on = true) => b.eval(`(() => { const c = (${lotRow(pid, vid)}).querySelector('.damage-tick'); if (c.checked !== ${on}) c.click(); })()`);
const confirmWithEnter = async () => { await b.waitFor(`!!document.querySelector('.mud-dialog')`, 5000); await sleep(600); await b.key('Enter'); await sleep(1800); };

try {
  await login(b);
  const [l0, l1] = want.lines;

  log('--- damage on a purchase');
  await b.goto(`/purchase/${want.purchaseId}`);
  await b.waitFor(`!!document.querySelector('#purchase-record-damage')`, 15000); await sleep(800);
  await b.click('#purchase-record-damage');
  await b.waitFor(`!!document.querySelector('.mud-dialog .damage-entry')`, 10000); await sleep(1200);
  await setQty('#damage-qty-0', 3); await setQty('#damage-qty-1', 2); await sleep(300);
  await b.click('#damage-save'); await sleep(1500);
  truthy('damage recorded', await snackHas('5 more damaged'));
  const supplierName = await b.eval(`document.querySelector('.focus-supplier input').value`);

  log('--- the damage report');
  truthy('menu has Damage report', await b.eval(`[...document.querySelectorAll('.mud-nav-link')].some(a => a.innerText.trim().startsWith('Damage report'))`));
  await b.goto('/damage'); await b.waitFor(`!!document.querySelector('.damage-report-table')`, 15000); await sleep(1000);
  expect('row 1: 3 pieces, the purchase supplier, "Assigned"', await lotInfo(l0.productId, l0.variantId), { pieces: '3', supplier: supplierName, status: 'Assigned' });
  expect('row 2: 2 pieces', (await lotInfo(l1.productId, l1.variantId))?.pieces, '2');

  await tick(l0.productId, l0.variantId);
  await b.eval(`(() => { const i = (${lotRow(l0.productId, l0.variantId)}).querySelector('.damage-take'); i.value = '1'; i.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(400);
  expect('the bar counts 1 ticked row, 1 piece', await b.eval(`document.querySelector('.damage-ticked-count').innerText.trim()`), '1 row ticked, 1 piece');
  await b.click('#damage-repaired'); await confirmWithEnter();
  truthy('"Repaired: 1 back in stock."', await snackHas('Repaired: 1 back in stock.'));
  expect('row 1 now 2 pieces, untouched otherwise', await lotInfo(l0.productId, l0.variantId), { pieces: '2', supplier: supplierName, status: 'Assigned' });

  await tick(l1.productId, l1.variantId);
  await b.click('#damage-assign');
  await b.waitFor(`!!document.querySelector('.mud-dialog .pick-supplier')`, 5000); await sleep(800);
  await pickFromList('CAPITAL');
  truthy('supplier assigned', await snackHas('assigned to 1 row'));
  const other = (await lotInfo(l1.productId, l1.variantId))?.supplier;
  truthy('row 2 now has the new supplier', other && other !== supplierName);

  await tick(l0.productId, l0.variantId); await tick(l1.productId, l1.variantId);
  await b.click('#damage-sent'); await confirmWithEnter();
  truthy('two suppliers at once are refused', await snackHas('different suppliers'));

  log('--- Supplier and Status in the row itself');
  await tick(l0.productId, l0.variantId, false); await tick(l1.productId, l1.variantId, false);
  const firstSupplierId = await b.eval(`(${lotRow(l0.productId, l0.variantId)}).querySelector('.damage-row-supplier').value`);
  await rowSelect(l1.productId, l1.variantId, '.damage-row-supplier', firstSupplierId); await sleep(1500);
  truthy('row supplier saved at once', await snackHas('assigned.'));
  expect('row 2 back to the purchase supplier', (await lotInfo(l1.productId, l1.variantId))?.supplier, supplierName);
  await rowSelect(l1.productId, l1.variantId, '.damage-row-status', 'ready'); await sleep(1500);
  expect('row 2 Status → Ready to send', (await lotInfo(l1.productId, l1.variantId))?.status, 'Ready to send');
  await rowSelect(l1.productId, l1.variantId, '.damage-row-status', 'assigned'); await sleep(1500);
  expect('row 2 Status → back to Assigned', (await lotInfo(l1.productId, l1.variantId))?.status, 'Assigned');
  expect('no "Not ready" button (Hiten)', await b.eval(`!!document.querySelector('#damage-not-ready')`), false);
  await b.eval(`(() => { const i = (${lotRow(l1.productId, l1.variantId)}).querySelector('.damage-take'); i.value = '1'; i.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(300);
  await rowSelect(l1.productId, l1.variantId, '.damage-row-status', 'writeoff');
  await b.waitFor(`!!document.querySelector('.mud-dialog')`, 5000); await sleep(600);
  await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim().toUpperCase() === 'CANCEL').click()`); await sleep(1200);
  expect('Status → Written off, cancelled: the row still says Assigned', (await lotInfo(l1.productId, l1.variantId))?.status, 'Assigned');
  await rowSelect(l1.productId, l1.variantId, '.damage-row-status', 'writeoff'); await confirmWithEnter();
  truthy('Status → Written off (Qty 1): "Written off: 1."', await snackHas('Written off: 1.'));
  expect('row 2 now 1 piece', (await lotInfo(l1.productId, l1.variantId))?.pieces, '1');

  await tick(l0.productId, l0.variantId);
  await b.click('#damage-ready'); await sleep(1500);
  expect('row 1 ready to send', (await lotInfo(l0.productId, l0.variantId))?.status, 'Ready to send');
  await b.eval(`(() => { const s = document.querySelector('#damage-show'); s.value = 'ready'; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(600);
  expect('Show "Ready to send" lists only it', await b.eval(`document.querySelectorAll('.damage-report-table tbody tr.damage-lot').length`), 1);
  await b.shot(`${shots}damage-report.png`);
  await tick(l0.productId, l0.variantId);
  await b.click('#damage-sent'); await confirmWithEnter();
  truthy('"Purchase return … made"', await snackHas('made: 2 pieces to'));
  await b.eval(`(() => { const s = document.querySelector('#damage-show'); s.value = 'all'; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(600);
  expect('row 1 has left the report', await lotInfo(l0.productId, l0.variantId), null);
  await b.click('#activity-log-open');
  await b.waitFor(`!!document.querySelector('#activity-panel')`, 10000); await sleep(1500);
  const panel = await b.eval(`document.querySelector('#activity-panel').innerText`);
  expect('"See activity log" in the bottom bar shows the repair, the purchase return and the supplier change',
    ['Repair 1 of', 'Purchase return 1 of', 'Return supplier'].filter(t => !panel.includes(t)), []);
  expect('the panel is headed "Damage report"', await b.eval(`document.querySelector('#activity-title')?.innerText.trim()`), 'Activity – Damage report');
  await b.shot(`${shots}damage-activity.png`);
  await b.key('Escape'); await sleep(800);

  log('--- Esc from a purchase opened from the report');
  await b.eval(`(${lotRow(l1.productId, l1.variantId)}).querySelector('.c-from a').click()`); await sleep(2500);
  truthy('the From link opens the purchase', (await b.path()).toLowerCase().startsWith('/purchase/'));
  await b.key('Escape'); await sleep(1500);
  expect('Esc goes back to the damage report', (await b.path()).toLowerCase(), '/damage');

  log('--- Purchase Returns');
  truthy('menu has Purchase Returns', await b.eval(`[...document.querySelectorAll('.mud-nav-link')].some(a => a.innerText.trim().startsWith('Purchase Returns'))`));
  await b.goto('/purchasereturns'); await sleep(2500);
  truthy('the list shows it: 2 from damage', await b.eval(`[...document.querySelectorAll('tbody tr')].some(r => r.innerText.includes(${JSON.stringify(supplierName)}) && r.children[3].innerText.trim() === '2')`));
  await b.eval(`[...document.querySelectorAll('tbody tr')].find(r => r.innerText.includes(${JSON.stringify(supplierName)})).querySelector('a').click()`); await sleep(2500);
  expect('one damaged line on it', await b.eval(`document.querySelectorAll('.fromdamage-row').length`), 1);
  truthy('it says where it was found', (await b.eval(`document.querySelector('.fromdamage-row .c-from').innerText`)).startsWith(`Purchase ${want.voucher} of`));
  expect('no rate boxes for good stock', await b.eval(`[...document.querySelectorAll('.lineform label')].some(l => l.textContent.trim() === 'Rate')`), false);
  truthy('cursor in the line product box', (await b.active()).includes('focus-line-product'));
  await pickFromList(l1.productId);
  await b.eval(`(() => { const s = document.querySelector('#variantid'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(400);
  const qtyBox = `[...document.querySelectorAll('label')].find(l => l.textContent.trim() === 'Qty').parentElement.querySelector('input')`;
  await b.eval(`(() => { const i = ${qtyBox}; i.focus(); i.select(); })()`); await b.type('1'); await b.key('Enter'); await sleep(2500);
  expect('good stock line added', await b.eval(`document.querySelector('#totalgood').value`), '1');
  await b.shot(`${shots}purchase-return.png`);
  await b.eval(`document.querySelector('.fromdamage-remove').click()`); await sleep(2000);
  truthy('X: "Back on the damage report."', await snackHas('Back on the damage report.'));
  expect('from damage now 0', await b.eval(`document.querySelector('#totalfromdamage') ? document.querySelector('#totalfromdamage').value : 'gone'`), '0');
  await b.key('Escape'); await sleep(1500);
  expect('Esc back to the Purchase Returns list', (await b.path()).toLowerCase(), '/purchasereturns');
  await b.key('n', 1); await sleep(1800);
  expect('Alt+N opens a new purchase return', (await b.path()).toLowerCase(), '/purchasereturn');
  truthy('cursor in Supplier', (await b.active()).includes('focus-supplier'));

  await b.goto('/damage'); await b.waitFor(`!!document.querySelector('.damage-report-table')`, 15000); await sleep(1000);
  expect('row 1 is back on the report with its 2 pieces', (await lotInfo(l0.productId, l0.variantId))?.pieces, '2');
} catch (e) {
  failures++;
  log('ERROR', e.message);
  await b.shot(`${shots}damage-report-error.png`);
} finally {
  b.close();
}

log(failures === 0 ? '\nALL DAMAGE REPORT CHECKS PASS' : `\n${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
