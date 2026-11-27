// Version 3.0, stage 3, batch 3-7: Put away on a purchase (type or scan a rack per line, Enter adds it and moves down, a wrong
// rack explained) and "Where is it?" (an item by its label: stock per place and racks in words; a rack: one level or the whole
// rack; ✕ takes an item off an emptied rack; arranging rack by rack – scan items onto a shown level, "Moving here" rearranges). Setup: setup-damage-returns.py on a fresh copy (DAMAGE_JSON = its
// damage-returns.json: the newest purchase with two lines). It CHANGES the test copy.
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

const b = await launch(9391);
const snackHas = async (text) => { for (let i = 0; i < 20; i++) { if ((await b.snack()).includes(text)) return true; await sleep(250); } return false; };
const choose = (sel, value) => b.eval(`(() => { const s = document.querySelector(${JSON.stringify(sel)}); s.value = ${JSON.stringify(String(value))}; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
const setBox = (sel, value) => b.eval(`(() => { const i = document.querySelector(${JSON.stringify(sel)}); i.value = ${JSON.stringify(String(value))}; i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true })); })()`);
async function confirmDialog(button) {
  await b.waitFor(`[...document.querySelectorAll('.mud-dialog button')].some(x => x.innerText.trim() === ${JSON.stringify(button)})`, 5000);
  await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim() === ${JSON.stringify(button)}).click()`);
  await sleep(1500);
}
async function typeInto(sel, text) {
  await b.eval(`(() => { const i = document.querySelector(${JSON.stringify(sel)}); i.focus(); i.select(); })()`);
  await b.type(text); await b.key('Enter'); await sleep(1500);
}

try {
  await login(b);

  log('--- racks in the godown');
  await b.goto('/places'); await b.waitFor(`!!document.querySelector('.place-row')`, 10000); await sleep(800);
  await b.eval(`[...document.querySelectorAll('.place-row')].find(r => r.querySelector('.place-name').value === 'Godown').querySelector('.place-show-racks').click()`); await sleep(800);
  await setBox('#rack-from', 1); await setBox('#rack-to', 3); await choose('#rack-top', 3); await sleep(300);
  await b.eval(`document.querySelector('#rack-add').click()`); await sleep(1500);
  truthy('racks 02-01 … 02-03 added', await snackHas('3 racks added'));

  log('--- Put away on a purchase in the godown');
  await b.goto(`/purchase/${data.purchaseId}`); await b.waitFor(`!!document.querySelector('#purchase-place')`, 10000); await sleep(1500);
  await choose('#purchase-place', 2);
  truthy('the purchase now counts in the godown', await snackHas('now counts in Godown'));
  await b.waitFor(`!!document.querySelector('#purchase-put-away')`, 8000); await sleep(400);
  await b.eval(`document.querySelector('#purchase-put-away').click()`);
  await b.waitFor(`!!document.querySelector('#put-away-0')`, 8000); await sleep(1200);
  truthy("cursor in the first line's rack box", (await b.active()).includes('put-away-0'));
  const rows = await b.eval(`document.querySelectorAll('.put-away-table tbody tr').length`);
  expect('every line is marked "no rack yet"', await b.eval(`document.querySelectorAll('.put-away-table tr.put-away-none').length`), rows);
  await b.type('0201a'); await b.key('Enter'); await sleep(1500);
  truthy('Enter adds it', await snackHas('rack 02-01-A added'));
  truthy('…and moves down', (await b.active()).includes('put-away-1'));
  await b.type('0299A'); await b.key('Enter'); await sleep(1200);
  truthy('a rack that does not exist is explained', await snackHas('Godown has no rack 99.'));
  await typeInto('#put-away-1', '02-02-B');
  expect('the first two lines show their rack (main ★), the rest still none', await b.eval(`[...document.querySelectorAll('.put-away-table .put-away-racks')].map(td => td.innerText.replace(/\\s+/g, ' ').trim())`),
    ['★ 02-01-A', '★ 02-02-B', ...Array(rows - 2).fill('No rack yet')]);
  await b.shot(`${shots}put-away.png`);
  await b.eval(`document.querySelector('#put-away-done').click()`); await sleep(1200);

  log('--- Where is it? – an item');
  truthy('menu has "Where is it?"', await b.eval(`[...document.querySelectorAll('.mud-nav-link')].some(a => a.innerText.trim().startsWith('Where is it?'))`));
  await b.goto('/whereis'); await b.waitFor(`!!document.querySelector('#whereis-product')`, 10000); await sleep(1200);
  truthy('cursor in the scan box', (await b.active()).includes('whereis-product'));
  await b.type(line1.productId); await b.key('Tab'); await sleep(200);
  await b.type(line1.variantId); await b.key('Enter'); await sleep(1800);
  expect('one item', await b.eval(`document.querySelectorAll('.whereis-item').length`), 1);
  expect('a column per place', await b.eval(`[...document.querySelectorAll('.whereis-items th')].map(t => t.innerText.trim().toUpperCase()).filter(t => t === 'SHOP' || t === 'GODOWN')`), ['SHOP', 'GODOWN']);
  expect('its rack in words', await b.eval(`document.querySelector('.whereis-item .whereis-racks').innerText.replace(/\\s+/g, ' ').trim()`), 'Godown: ★ 02-01-A Rack 1, Level A');
  await b.shot(`${shots}whereis-item.png`);

  log('--- Where is it? – a rack');
  await typeInto('#whereis-rack', '0201');
  truthy('the whole rack', (await b.eval(`document.querySelector('.whereis-rack-title').innerText`)).includes('all levels'));
  truthy('…holds the item', (await b.eval(`document.querySelector('.whereis-contents').innerText`)).includes(line1.productId));
  await typeInto('#whereis-rack', '02-01-A');
  expect('one level, in words', await b.eval(`document.querySelector('.whereis-rack-title').innerText.trim()`), '02-01-A – Godown, Rack 1, Level A');
  await b.eval(`document.querySelector('.whereis-take-off').click()`);
  await confirmDialog('Take off');
  expect('emptied: nothing is kept here now', await b.eval(`!!document.querySelector('.whereis-rack-empty')`), true);
  await b.shot(`${shots}whereis-rack.png`);

  log('--- arranging rack by rack: scan the rack, then the items');
  truthy('with one level shown, the cursor waits in "Put items here"', (await b.active()).includes('whereis-put-product'));
  await b.type(line2.productId); await b.key('Tab'); await sleep(200);
  await b.type(line2.variantId); await b.key('Enter'); await sleep(1500);
  expect('scanned item put on the rack', await b.eval(`document.querySelector('.move-last-scan')?.innerText.trim()`), `${line2.productId} / ${line2.variantId} put on 02-01-A.`);
  expect('the rack now holds it', await b.eval(`document.querySelectorAll('.whereis-content').length`), 1);
  await typeInto('#whereis-rack', '0203A');
  await b.eval(`document.querySelector('.whereis-moving input').click()`); await sleep(400);
  await b.eval(`document.querySelector('#whereis-put-product').focus()`);
  await b.type(line2.productId); await b.key('Tab'); await sleep(200);
  await b.type(line2.variantId); await b.key('Enter'); await sleep(1500);
  expect('"Moving here" rearranges it', await b.eval(`document.querySelector('.move-last-scan')?.innerText.trim()`), `${line2.productId} / ${line2.variantId} moved to 02-03-A.`);
  await b.eval(`document.querySelector('#whereis-product').focus()`);
  await b.type(line2.productId); await b.key('Tab'); await sleep(200);
  await b.type(line2.variantId); await b.key('Enter'); await sleep(1800);
  expect('…and it is now only on 02-03-A in the godown', await b.eval(`document.querySelector('.whereis-item .whereis-racks').innerText.replace(/\\s+/g, ' ').trim()`), 'Godown: ★ 02-03-A Rack 3, Level A');
  await b.shot(`${shots}whereis-arranged.png`);

  await typeInto('#whereis-rack', '0204A');
  truthy('a rack that does not exist is explained', await snackHas('Godown has no rack 04.'));
} catch (e) {
  failures++;
  log('ERROR', e.message);
  await b.shot(`${shots}whereis-error.png`);
} finally {
  b.close();
}

log(failures === 0 ? '\nALL PUT AWAY AND WHERE IS IT CHECKS PASS' : `\n${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
