// Version 3.0, stage 3, batches 3-5/3-6: stock transfers between places (the code calls them moves). Places and racks shows the two switches (two steps on,
// approval off); the Admin sets a purchase to the godown, "Send to …" opens the pop-up (Enter moves down, Ctrl+S sends) and
// opens the new move "on the way"; the shop receives by scanning one piece and typing another, one piece stays missing –
// a red flag on the move, in the list and in the menu – and is written off; a starting count moves at once; Esc goes back.
// Setup: setup-damage-returns.py on a fresh copy (DAMAGE_JSON = its damage-returns.json: the newest purchase with two lines).
// It CHANGES the test copy.
import { launch, login, sleep } from './cdp.mjs';
import { readFileSync } from 'node:fs';

const log = (...a) => console.log(...a);
const shots = process.env.SCRATCH || '.';
const data = JSON.parse(readFileSync(process.env.DAMAGE_JSON, 'utf8'));
const [line1, line2] = data.lines;
let failures = 0;
function expect(what, actual, wanted) {
  const ok = JSON.stringify(actual) === JSON.stringify(wanted);
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}
function truthy(what, actual) { expect(what, !!actual, true); }

const b = await launch(9381);
const snackHas = async (text) => { for (let i = 0; i < 20; i++) { if ((await b.snack()).includes(text)) return true; await sleep(250); } return false; };
const choose = (sel, value) => b.eval(`(() => { const s = document.querySelector(${JSON.stringify(sel)}); s.value = ${JSON.stringify(String(value))}; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
async function confirmDialog(button) {
  await b.waitFor(`[...document.querySelectorAll('.mud-dialog button')].some(x => x.innerText.trim() === ${JSON.stringify(button)})`, 5000);
  await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim() === ${JSON.stringify(button)}).click()`);
  await sleep(1800);
}
const status = () => b.eval(`document.querySelector('.move-state .move-status')?.innerText.trim()`);

try {
  await login(b);

  log('--- the switches on Places and racks');
  await b.goto('/places'); await b.waitFor(`!!document.querySelector('.places-transit input')`, 10000); await sleep(800);
  expect('two steps on, approval off', await b.eval(`[document.querySelector('.places-transit input').checked, document.querySelector('.places-move-approval input').checked]`), [true, false]);

  log('--- Send to shop from a purchase in the godown');
  await b.goto(`/purchase/${data.purchaseId}`); await b.waitFor(`!!document.querySelector('#purchase-place')`, 10000); await sleep(1500);
  expect('an old purchase counts in the shop: no "Send to" button', await b.eval(`!!document.querySelector('#purchase-send-to-shop')`), false);
  await choose('#purchase-place', 2);
  truthy('the Admin moves it to the godown', await snackHas('now counts in Godown'));
  await b.waitFor(`!!document.querySelector('#purchase-send-to-shop')`, 8000); await sleep(500);
  expect('the button says where to', await b.eval(`document.querySelector('#purchase-send-to-shop').innerText.trim().toUpperCase()`), 'SEND TO SHOP');
  await b.eval(`document.querySelector('#purchase-send-to-shop').click()`);
  await b.waitFor(`!!document.querySelector('#send-qty-0')`, 8000); await sleep(1200);
  truthy('cursor in the first Send box', (await b.active()).includes('send-qty-0'));
  await b.type('3'); await b.key('Enter'); await sleep(500);
  truthy('Enter moves down', (await b.active()).includes('send-qty-1'));
  await b.type('1'); await sleep(300);
  await b.key('s', 2);
  await b.waitFor(`location.pathname.startsWith('/transfer/') && !!document.querySelector('.move-state .move-status')`, 15000); await sleep(800);
  expect('Ctrl+S sends and opens the stock transfer', (await b.path()).startsWith('/transfer/'), true);
  expect('on the way', await status(), 'On the way from Godown to Shop');
  expect('two lines sent', await b.eval(`document.querySelector('#totalquantity').value`), '4');

  log('--- the shop receives: one scanned, one typed, one missing');
  truthy('a receive card', await b.eval(`!!document.querySelector('.move-receive-card')`));
  await b.eval(`document.querySelector('#move-scan-product').focus()`);
  await b.type(line1.productId); await b.key('Tab'); await sleep(200);
  await b.type(line1.variantId); await b.key('Enter'); await sleep(1200);
  expect('a scan counts one piece', await b.eval(`document.querySelectorAll('.move-receive-qty')[0].value`), '1');
  await b.eval(`(() => { const i = document.querySelectorAll('.move-receive-qty')[0]; i.value = '2'; i.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(300);
  await b.eval(`document.querySelectorAll('.transfer-receive-line')[1].click()`); await sleep(500);
  expect('a tick fills "Received now" with what is to come', await b.eval(`document.querySelectorAll('.move-receive-qty')[1].value`), '1');
  await b.eval(`document.querySelector('#move-confirm-received').click()`);
  await confirmDialog('Confirm');
  expect('1 missing', await status(), '1 missing');
  truthy('a red flag on the move', await b.eval(`!!document.querySelector('.move-missing-alert')`));
  await b.shot(`${shots}move-missing.png`);
  await b.goto('/transfers'); await b.waitFor(`!!document.querySelector('.transfers-ontheway')`, 10000); await sleep(1000);
  truthy('the menu shows "1 missing" in red', (await b.eval(`[...document.querySelectorAll('.nav-missing')].map(s => s.innerText).join()`)).includes('1 missing'));
  truthy('the list has a red "Pieces missing" section with it', await b.eval(`!!document.querySelector('.transfers-missing tr.move-missing')`));
  expect('one "Stock Transfer" link in the menu (Hiten: New transfer belongs on the page)', await b.eval(`[...document.querySelectorAll('.mud-nav-link')].filter(a => a.innerText.includes('Stock Transfer') || a.innerText.includes('New transfer')).map(a => a.innerText.split('\\n')[0].trim())`), ['Stock Transfer']);
  truthy('a Below zero section', await b.eval(`[...document.querySelectorAll('h6')].some(h => h.innerText.trim() === 'Below zero')`));
  await b.shot(`${shots}moves.png`);

  log('--- writing the missing piece off');
  await b.eval(`document.querySelector('tr.move-missing a, tr.move-missing .mud-button-root').click()`);
  await b.waitFor(`!!document.querySelector('#move-writeoff')`, 15000); await sleep(500);
  await b.eval(`document.querySelector('#move-writeoff').click()`);
  await confirmDialog('Write off');
  expect('received; nothing missing now', await status(), 'Received in Shop');
  await b.key('Escape'); await sleep(1500);
  expect('Esc goes back to the list', (await b.path()).toLowerCase(), '/transfers');
  truthy('the transfer is now in "Received"', await b.eval(`document.querySelector('.transfers-received').innerText.includes('Godown → Shop')`));
  expect('no red count any more', await b.eval(`document.querySelectorAll('.nav-missing').length`), 0);

  log('--- a new transfer is a list on the screen until Send (a starting count)');
  await b.goto('/transfers'); await b.waitFor(`!!document.querySelector('#move-new')`, 10000); await sleep(800);
  await b.eval(`document.querySelector('#move-new').click()`);
  await b.waitFor(`!!document.querySelector('#transfer-send-product')`, 10000); await sleep(1200);
  truthy('"New transfer" on the page opens it; cursor in the scan box', (await b.active()).includes('transfer-send-product'));
  await choose('#move-kind', 'start'); await sleep(400);
  expect('a starting count goes from the shop to the godown', await b.eval(`[document.querySelector('#move-from').selectedOptions[0].text, document.querySelector('#move-to').selectedOptions[0].text]`), ['Shop', 'Godown']);
  for (let i = 0; i < 2; i++) {
    await b.eval(`document.querySelector('#transfer-send-product').focus()`);
    await b.type(line2.productId); await b.key('Tab'); await sleep(200);
    await b.type(line2.variantId); await b.key('Enter'); await sleep(1200);
  }
  expect('two scans: one line of 2', await b.eval(`document.querySelector('.move-last-scan')?.innerText.trim()`), `${line2.productId} / ${line2.variantId}: 2`);
  truthy('the cursor stays in the scan box for the next label', (await b.active()).includes('transfer-send-product'));
  await b.eval(`(() => { const i = document.querySelector('#transfer-draft-qty-0'); i.focus(); i.select(); })()`);
  await b.type('5'); await sleep(600);
  expect('the quantity box stays open: scanned 2, typed 5', await b.eval(`document.querySelector('#totalquantity').value`), '5');
  await b.eval(`document.querySelector('.focus-transfer-product input').focus()`); await sleep(300);
  await b.type(line1.productId);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300); await b.key('Enter'); await sleep(1500);
  await b.eval(`(() => { const s = document.querySelector('#transfer-add-variant'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(400);
  await b.eval(`(() => { const i = document.querySelector('#transfer-add-qty'); i.focus(); i.select(); })()`);
  await b.type('2'); await b.key('Enter'); await sleep(1200);
  expect('picked and added: two lines', await b.eval(`document.querySelectorAll('.transfer-draft-table tbody tr').length`), 2);
  await b.goto('/transfers'); await b.waitFor(`!!document.querySelector('.transfers-received')`, 10000); await sleep(800);
  expect('nothing was saved before Send', await b.eval(`document.querySelector('.transfers-received').innerText.includes('Shop → Godown')`), false);

  log('--- Send');
  await b.goto('/transfer'); await b.waitFor(`!!document.querySelector('#transfer-send-product')`, 10000); await sleep(1200);
  await choose('#move-kind', 'start'); await sleep(400);
  await b.eval(`document.querySelector('#transfer-send-product').focus()`);
  await b.type(line2.productId); await b.key('Tab'); await sleep(200);
  await b.type(line2.variantId); await b.key('Enter'); await sleep(1200);
  await b.eval(`(() => { const i = document.querySelector('#transfer-draft-qty-0'); i.focus(); i.select(); })()`);
  await b.type('7'); await sleep(500);
  await b.key('s', 2);
  await b.waitFor(`location.pathname.startsWith('/transfer/') && !!document.querySelector('.move-state .move-status')`, 15000); await sleep(800);
  expect('Ctrl+S sends: moved at once', await status(), 'Moved from Shop to Godown');
  expect('pieces sent', await b.eval(`document.querySelector('#totalquantity').value`), '7');
  expect('a number given when sent', Number(await b.eval(`document.querySelector('#voucher').value`)) > 0, true);
  expect('no receive card', await b.eval(`!!document.querySelector('.move-receive-card')`), false);
} catch (e) {
  failures++;
  log('ERROR', e.message);
  await b.shot(`${shots}moves-error.png`);
} finally {
  b.close();
}

log(failures === 0 ? '\nALL MOVE CHECKS PASS' : `\n${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
