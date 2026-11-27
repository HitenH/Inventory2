// Version 3.0, stage 3, batches 3-3/3-4: where purchases arrive and sales leave from (Settings → Places and racks: Godown /
// Shop), "Arrives in" on a new purchase, the Sale screen's "From" box (Shop, skipped by Tab; only with "Sell from another
// place"), the Place box on the Stock changes screen (a count correction of +5 in the godown), the stock arrow per place on
// the product page, places per user on the Users screen, and a user limited to the godown seeing godown stock only.
// Setup: setup-full-suite.py on a fresh copy (users Admin and UiNoRates). FLOW_PRODUCT = the id of product AJ0120128.
// It CHANGES the test copy.
import { launch, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const shots = process.env.SCRATCH || '.';
const productGuid = process.env.FLOW_PRODUCT || '12C1D7EA-8BB6-487E-BDD0-D9D8B51CEAAC';
let failures = 0;
function expect(what, actual, wanted) {
  const ok = JSON.stringify(actual) === JSON.stringify(wanted);
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}
function truthy(what, actual) { expect(what, !!actual, true); }

async function loginAs(b, name, password) {
  await b.goto('/login');
  await b.waitFor(`document.querySelector('#login-name')`);
  await b.click('#login-name'); await b.type(name);
  await b.click('#login-password'); await b.type(password);
  await b.key('Enter');
  await b.waitFor(`location.pathname === '/'`, 10000);
  await sleep(1000);
}
const choose = (b, sel, value) => b.eval(`(() => { const s = document.querySelector(${JSON.stringify(sel)}); s.value = ${JSON.stringify(String(value))}; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
const snackHas = async (b, text) => { for (let i = 0; i < 20; i++) { if ((await b.snack()).includes(text)) return true; await sleep(250); } return false; };
async function openAllStockArrows(b) {
  await b.eval(`[...document.querySelectorAll('.stock-toggle')].forEach(t => { if (t.getAttribute('aria-expanded') !== 'true') t.click(); })`); await sleep(1200);
}

let b = await launch(9371);
try {
  await loginAs(b, 'Admin', 'UiTest#2026');

  log('--- Settings → Places and racks: where purchases arrive and sales leave from');
  await b.goto('/places'); await b.waitFor(`!!document.querySelector('#place-purchases')`, 10000); await sleep(800);
  expect('purchases arrive in the godown', await b.eval(`document.querySelector('#place-purchases').selectedOptions[0].text`), '02 Godown');
  expect('sales leave from the shop', await b.eval(`document.querySelector('#place-sales').selectedOptions[0].text`), '01 Shop');

  log('--- a new purchase arrives in the godown');
  await b.goto('/purchase'); await b.waitFor(`!!document.querySelector('#purchase-place')`, 10000); await sleep(1000);
  expect('"Arrives in" shows Godown, read only', await b.eval(`[document.querySelector('#purchase-place').selectedOptions[0]?.text, document.querySelector('#purchase-place').disabled]`), ['Godown', true]);
  truthy('the cursor still starts in Supplier', (await b.active()).includes('focus-supplier'));

  log('--- the Sale screen\'s "From" box');
  await b.goto('/sale'); await b.waitFor(`!!document.querySelector('#sale-from')`, 10000); await sleep(1200);
  expect('From = Shop, skipped by Tab', await b.eval(`[document.querySelector('#sale-from').selectedOptions[0].text, document.querySelector('#sale-from').tabIndex]`), ['Shop', -1]);
  truthy('the cursor still starts in Customer', (await b.active()).includes('focus-customer'));
  expect('the Admin may choose the godown', await b.eval(`[document.querySelector('#sale-from').disabled, [...document.querySelectorAll('#sale-from option')].map(o => o.text)]`), [false, ['Shop', 'Godown']]);

  log('--- the Stock changes screen: a count correction in the godown');
  await b.goto('/stockchange'); await b.waitFor(`!!document.querySelector('#stockchange-place')`, 10000); await sleep(1200);
  expect('Place starts on Shop', await b.eval(`document.querySelector('#stockchange-place').selectedOptions[0].text`), 'Shop');
  await choose(b, '#stockchange-type', 'CountCorrection'); await sleep(300);
  await choose(b, '#stockchange-place', 2); await sleep(300);
  await b.key('s', 2); await sleep(3000);
  // Hiten (3-4): only the Admin may change the place of a saved entry – this check runs as the Admin, so the box stays open.
  expect('saved in the godown; the Admin may still change the place', await b.eval(`[document.querySelector('#stockchange-place').selectedOptions[0].text, document.querySelector('#stockchange-place').disabled]`), ['Godown', false]);
  await b.eval(`document.querySelector('.focus-line-product input').focus()`);
  await b.type('AJ0120128');
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300); await b.key('Enter'); await sleep(2000);
  const variant = await b.eval(`(() => { const s = document.querySelector('#variantid'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); return s.selectedOptions[0].text; })()`); await sleep(400);
  await b.eval(`(() => { const i = [...document.querySelectorAll('label')].find(l => l.textContent.trim() === 'Qty').parentElement.querySelector('input'); i.focus(); i.select(); })()`);
  await b.type('5'); await b.key('Enter'); await sleep(2500);
  expect('a line of 5 added', await b.eval(`document.querySelector('#totalquantity').value`), '5');
  log('     variant:', variant);

  log('--- the product page: stock per place behind the arrow');
  await b.goto(`/product/${productGuid}`); await b.waitFor(`!!document.querySelector('.stock-fig')`, 10000); await sleep(1500);
  await openAllStockArrows(b);
  const places = await b.eval(`[...document.querySelectorAll('.stock-place')].map(s => s.innerText.replace(/\\s+/g, ' ').trim())`);
  truthy('the arrow shows "in Godown 5" beside the shop', places.includes('in Godown 5') && places.some(p => p.startsWith('in Shop ')));
  log('     breakdown:', JSON.stringify(places));
  await b.shot(`${shots}place-stock.png`);

  log('--- Users: places per user');
  await b.goto('/users'); await b.waitFor(`!!document.querySelector('.user-places-open')`, 10000); await sleep(800);
  const rowOf = (name) => `[...document.querySelectorAll('tr')].find(r => r.querySelector('td') && r.querySelector('td').innerText.trim() === '${name}')`;
  expect('everyone starts with all places', await b.eval(`${rowOf('UiNoRates')}.querySelector('.user-places-text').innerText.trim()`), 'All places');
  await b.eval(`${rowOf('UiNoRates')}.querySelector('.user-places-open').click()`);
  await b.waitFor(`!!document.querySelector('.user-place-pick')`, 5000); await sleep(600);
  await b.eval(`[...document.querySelectorAll('.user-place-pick')].find(p => p.innerText.includes('Shop')).querySelector('input').click()`); await sleep(400);
  await b.eval(`document.querySelector('#user-save').click()`); await sleep(1500);
  truthy('"Places saved for UiNoRates."', await snackHas(b, 'Places saved for UiNoRates'));
  expect('UiNoRates: Godown only', await b.eval(`${rowOf('UiNoRates')}.querySelector('.user-places-text').innerText.trim()`), 'Godown');
  await b.shot(`${shots}users-places.png`);
  b.close();

  log('--- a user limited to the godown');
  b = await launch(9372);
  await loginAs(b, 'UiNoRates', 'UiNoRates#2026');
  await b.goto(`/product/${productGuid}`); await b.waitFor(`!!document.querySelector('.stock-fig')`, 10000); await sleep(1500);
  const figures = await b.eval(`[...document.querySelectorAll('.stock-sellable')].map(s => s.innerText.trim())`);
  truthy('only the godown pieces are counted (5 for one variant, 0 for the rest)', figures.filter(f => f === '5').length === 1 && figures.every(f => f === '5' || f === '0'));
  log('     figures:', JSON.stringify(figures));
  await openAllStockArrows(b);
  expect('no shop figure behind any arrow', await b.eval(`[...document.querySelectorAll('.stock-place')].some(s => s.innerText.includes('Shop'))`), false);
  await b.goto('/sale'); await b.waitFor(`!!document.querySelector('#sale-from')`, 10000); await sleep(1200);
  expect('without "Sell from another place" the From box cannot be changed', await b.eval(`[document.querySelector('#sale-from').disabled, document.querySelector('#sale-from').selectedOptions[0].text]`), [true, 'Shop']);
} catch (e) {
  failures++;
  log('ERROR', e.message);
  await b.shot(`${shots}place-flow-error.png`);
} finally {
  b.close();
}

log(failures === 0 ? '\nALL PLACE FLOW CHECKS PASS' : `\n${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
