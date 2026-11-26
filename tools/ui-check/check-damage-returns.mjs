// Version 3.0, stage 2, batches 2-3/2-4: "Record damage" on a purchase (one pop-up listing every line with its damaged
// pieces; Enter moves down, a refused amount keeps the pop-up open, Ctrl+S saves, Esc asks only when something was changed),
// the damage in the purchase's activity panel and on the product page, a damaged purchase line cannot be deleted, lowering
// the damage again ("not damaged after all", Hiten 26 Nov 2026); then Sales Returns: menu item, Alt+N, customer + Enter saves,
// a line (minus refused), Record damage on the return, "Given in exchange" (quantities-only pop-up), Esc back to the list.
// Setup: tools/ui-check/setup-damage-returns.py on a fresh test copy (writes damage-returns.json next to the database; set
// DAMAGE_JSON to its path). It CHANGES the test copy (records damage, makes a sales return): never aim it at real data.
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

const b = await launch(9338);
const dialogOpen = `!!document.querySelector('.mud-dialog .damage-entry')`;
const setQty = (i, v) => b.eval(`(() => { const el = document.querySelector('#damage-qty-${i}'); el.focus(); el.value = '${v}'; el.dispatchEvent(new Event('input', { bubbles: true })); })()`);
const activeId = () => b.eval(`document.activeElement ? document.activeElement.id : ''`);
const snackHas = async (text) => { for (let i = 0; i < 20; i++) { if ((await b.snack()).includes(text)) return true; await sleep(250); } return false; };
async function pickFromList(text) {
  await b.type(text);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300); await b.key('Enter'); await sleep(2000);
}

try {
  await login(b);
  const [l0, l1] = want.lines;

  log('--- Record damage on a purchase');
  await b.goto(`/purchase/${want.purchaseId}`);
  await b.waitFor(`!!document.querySelector('#purchase-record-damage')`, 15000); await sleep(800);
  await b.click('#purchase-record-damage');
  await b.waitFor(dialogOpen, 10000); await sleep(1200);
  expect('one row per purchase line', await b.eval(`document.querySelectorAll('.damage-table tbody tr').length`), want.lines.length);
  expect('cursor starts in the first "Damaged" box', await activeId(), 'damage-qty-0');
  expect('it shows what is damaged now (0)', await b.eval(`document.querySelector('#damage-qty-0').value`), '0');
  expect('received shown', await b.eval(`document.querySelector('.damage-table tbody tr').children[4].innerText.trim()`), String(l0.qty));
  await setQty(0, 1);
  await b.key('Enter'); await sleep(400);
  expect('Enter moves down to the next line', await activeId(), 'damage-qty-1');
  await setQty(1, l1.qty + 1); await sleep(300);
  await b.click('#damage-save'); await sleep(800);
  truthy('more than came in is refused with a plain message', await snackHas('can be damaged'));
  truthy('the pop-up stays open', await b.eval(dialogOpen));
  await setQty(1, ''); await sleep(300);
  await b.eval(`(() => { const s = document.querySelectorAll('.damage-reason')[0]; s.selectedIndex = 2; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(300);
  const reason = await b.eval(`document.querySelectorAll('.damage-reason')[0].value`);
  await b.shot(`${shots}damage-popup.png`);
  await b.eval(`document.querySelector('#damage-qty-1').focus()`);
  await b.key('s', 2); await sleep(1500);
  expect('Ctrl+S saves and closes the pop-up', await b.eval(dialogOpen), false);
  truthy('"Damage recorded: 1 more damaged."', await snackHas('Damage recorded: 1 more damaged.'));

  await b.click('#purchase-record-damage');
  await b.waitFor(dialogOpen, 10000); await sleep(1200);
  expect('the box now shows 1 damaged', await b.eval(`document.querySelector('#damage-qty-0').value`), '1');
  await b.key('Escape'); await sleep(800);
  expect('Esc with nothing typed closes at once', await b.eval(dialogOpen), false);
  await b.click('#purchase-record-damage');
  await b.waitFor(dialogOpen, 10000); await sleep(1200);
  await setQty(0, 2); await sleep(200);
  await b.key('Escape'); await sleep(800);
  truthy('Esc after typing asks first', await b.eval(`document.body.innerText.includes('Close without saving?')`));
  await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim().toUpperCase() === 'CLOSE WITHOUT SAVING').click()`); await sleep(1000);
  expect('…and closes on yes', await b.eval(dialogOpen), false);
  expect('Esc on the purchase still goes back to the list', await (async () => { await b.key('Escape'); await sleep(1200); return (await b.path()).toLowerCase(); })(), '/purchases');

  log('--- the purchase history and the product page');
  await b.goto(`/purchase/${want.purchaseId}`);
  await b.waitFor(`!!document.querySelector('#purchase-record-damage')`, 15000); await sleep(800);
  await b.eval(`[...document.querySelectorAll('button, a')].find(x => x.innerText.trim().toUpperCase() === 'SEE ACTIVITY LOG').click()`); await sleep(1500);
  const panel = await b.eval(`document.body.innerText`);
  truthy('purchase panel shows "Damage recorded"', panel.includes('Damage recorded') && panel.includes(`${l0.productId}`) && panel.includes(`(${reason})`));
  await b.key('Escape'); await sleep(800);
  await b.eval(`document.querySelector('table.lines-table tbody tr .variant-delete-img').click()`); await sleep(1200);
  truthy('a damaged purchase line cannot be deleted', await snackHas('Damage is recorded on this line'));

  await b.goto(`/product/${l0.productGuid}`); await sleep(3000);
  const fig = await b.eval(`(() => { const r = [...document.querySelectorAll('.mud-table tbody tr')].find(r => r.children[1] && r.children[1].innerText.trim() === '${l0.variantId}'); const t = r && r.querySelector('.stock-toggle'); return t ? t.title.split(' – ')[0] : null; })()`);
  expect('the product page shows the damaged piece behind the arrow', fig, '+ 1 damaged');

  log('--- not damaged after all');
  await b.goto(`/purchase/${want.purchaseId}`);
  await b.waitFor(`!!document.querySelector('#purchase-record-damage')`, 15000); await sleep(800);
  await b.click('#purchase-record-damage');
  await b.waitFor(dialogOpen, 10000); await sleep(1200);
  await setQty(0, 0); await sleep(400);
  truthy('"was 1" shows beside the box', (await b.eval(`document.querySelector('.damage-table tbody tr .damage-was')?.innerText`) || '').includes('was 1'));
  expect('the reason reads "Not damaged after all"', await b.eval(`document.querySelector('.damage-table tbody tr .damage-not')?.innerText.trim()`), 'Not damaged after all');
  expect('the summary says so', await b.eval(`document.querySelector('.damage-count').innerText.trim()`), '1 not damaged after all');
  await b.shot(`${shots}damage-lowered.png`);
  await b.click('#damage-save'); await sleep(1500);
  truthy('"Damage recorded: 1 not damaged after all."', await snackHas('1 not damaged after all'));
  await b.goto(`/product/${l0.productGuid}`); await sleep(3000);
  expect('the product page has no damage left for it', await b.eval(`(() => { const r = [...document.querySelectorAll('.mud-table tbody tr')].find(r => r.children[1] && r.children[1].innerText.trim() === '${l0.variantId}'); return !!(r && r.querySelector('.stock-toggle')); })()`), false);

  log('--- Sales Returns');
  truthy('menu has Sales Returns', await b.eval(`[...document.querySelectorAll('.mud-nav-link')].some(a => a.innerText.trim().startsWith('Sales Returns'))`));
  await b.goto('/salesreturns'); await sleep(2000);
  await b.key('n', 1); await sleep(1800);
  expect('Alt+N opens a new sales return', (await b.path()).toLowerCase(), '/salesreturn');
  truthy('cursor in Customer', (await b.active()).includes('focus-customer'));
  await pickFromList('AAI MATHA');
  truthy('Enter on the customer saves the return (voucher given)', Number(await b.eval(`document.querySelector('#voucher').value`)) > 0);
  truthy('cursor goes to the line product box', (await b.active()).includes('focus-line-product'));
  expect('no rate boxes on a return', await b.eval(`[...document.querySelectorAll('.lineform label')].some(l => l.textContent.trim() === 'Rate')`), false);

  await pickFromList(l0.productId);
  await b.eval(`(() => { const s = document.querySelector('#variantid'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(400);
  const qtyBox = `[...document.querySelectorAll('label')].find(l => l.textContent.trim() === 'Qty').parentElement.querySelector('input')`;
  await b.eval(`(() => { const i = ${qtyBox}; i.focus(); i.select(); })()`); await b.type('-2'); await b.key('Enter'); await sleep(1200);
  truthy('a minus quantity is refused on a return', await snackHas('must be more than 0'));
  expect('nothing added', await b.eval(`document.querySelectorAll('table.lines-table tbody tr td.c-qty').length`), 0);
  await b.eval(`(() => { const i = ${qtyBox}; i.select(); })()`); await b.type('2'); await b.key('Enter'); await sleep(2500);
  expect('the line is added', await b.eval(`document.querySelectorAll('table.lines-table tbody tr td.c-qty').length`), 1);
  expect('returned 2', await b.eval(`document.querySelector('#totalquantity').value`), '2');

  await b.click('#salesreturn-record-damage');
  await b.waitFor(dialogOpen, 10000); await sleep(1200);
  expect('the pop-up says Returned', await b.eval(`document.querySelectorAll('.damage-table thead th')[4].innerText.trim().toUpperCase()`), 'RETURNED');
  await setQty(0, 1); await sleep(200);
  await b.click('#damage-save'); await sleep(1500);
  truthy('damage on the return recorded', await snackHas('Damage recorded: 1 more damaged.'));

  log('--- Given in exchange');
  await b.click('#salesreturn-exchange');
  await b.waitFor(`!!document.querySelector('.mud-dialog .bulk-entry')`, 10000); await sleep(1200);
  expect('the pop-up asks only product, variant and quantity', await b.eval(`[!!document.querySelector('#bulk-rate'), !!document.querySelector('#bulk-discount'), !!document.querySelector('#bulk-remarks')]`), [false, false, false]);
  await pickFromList(l0.productId);
  await b.eval(`(() => { const s = document.querySelector('#bulk-variant'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(400);
  await b.eval(`document.querySelector('#bulk-quantity').focus()`); await b.type('2'); await b.key('Enter'); await sleep(800);
  expect('one row in the pop-up', await b.eval(`document.querySelectorAll('.bulk-row').length`), 1);
  await b.key('s', 2); await sleep(2000);
  truthy('"Given in exchange: 2 pieces."', await snackHas('Given in exchange: 2 pieces.'));
  expect('given 2', await b.eval(`document.querySelector('#totalgiven').value`), '2');
  expect('the "Given in exchange" list shows it', await b.eval(`document.querySelectorAll('.exchange-row').length`), 1);
  await b.shot(`${shots}sales-return.png`);

  await b.key('Escape'); await sleep(1500);
  expect('Esc goes back to the Sales Returns list', (await b.path()).toLowerCase(), '/salesreturns');
  await sleep(1000);
  truthy('the list shows the return: 2 returned, 2 given', await b.eval(`[...document.querySelectorAll('tbody tr')].some(r => r.innerText.includes('AAI MATHA') && [...r.children].filter(td => td.innerText.trim() === '2').length >= 2)`));
  await b.shot(`${shots}sales-returns-list.png`);
} catch (e) {
  failures++;
  log('ERROR', e.message);
  await b.shot(`${shots}damage-error.png`);
} finally {
  b.close();
}

log(failures === 0 ? '\nALL DAMAGE AND RETURN CHECKS PASS' : `\n${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
