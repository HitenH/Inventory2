// Version 3.0, stage 2, batches 2-1/2-2: stock figures show the sellable pieces only; with damaged or waiting pieces a small
// arrow sits beside the number and a click opens the breakdown (Hiten, 25 Nov 2026: nothing more until clicked). Checks the
// product page, the Variants List report (Damaged column), the Reorder Level report (no Damaged column, Hiten) and the
// Variant Report (rows and total), Esc still goes back after the click, and a variant without stock changes shows only its
// number. Screenshots (light and dark) to <SCRATCH>stock-*.png.
// Setup: the stage 2 upgrade on the test copy, then tools/ui-check/setup-stock-figures.py on it (writes stock-figures.json
// next to the database; set STOCK_JSON to its path). Development only; it changes no data.
import { launch, login, sleep } from './cdp.mjs';
import { readFileSync } from 'node:fs';

const log = (...a) => console.log(...a);
const shots = process.env.SCRATCH || '.';
const want = JSON.parse(readFileSync(process.env.STOCK_JSON, 'utf8'));
let failures = 0;
function expect(what, actual, wanted) {
  const ok = JSON.stringify(actual) === JSON.stringify(wanted);
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}

// The variant table on the product page: the row whose Variant ID cell is `code`.
const productRow = code => `[...document.querySelectorAll('.mud-table tbody tr')].find(r => r.children[1] && r.children[1].innerText.trim() === '${code}')`;
// What a stock figure inside `el` shows: the number, whether the arrow is there, its hover text, and the breakdown if open.
const figure = el => `(() => { const r = ${el}; if (!r) return null; const f = r.querySelector('.stock-fig'); if (!f) return null;
  const t = f.querySelector('.stock-toggle'); const d = f.querySelector('.stock-breakdown');
  return { sellable: f.querySelector('.stock-sellable')?.innerText.trim(), arrow: !!t, hover: t ? t.title.split(' – ')[0] : null,
           breakdown: d ? d.innerText.replace(/\\s+/g, ' ').trim() : null }; })()`;
const click = el => `(${el}).querySelector('.stock-toggle').click()`;

const b = await launch(9334);
try {
  await login(b);
  log('--- product page');
  await b.goto(`/product/${want.productGuid}`);
  await b.waitFor(`!!(${productRow('G')})`, 15000); await sleep(800);
  expect('G: sellable pieces and an arrow, nothing else until clicked', await b.eval(figure(productRow('G'))),
    { sellable: String(want.G.sellable), arrow: true, hover: `+ ${want.G.damaged} damaged · ${want.G.waiting} waiting`, breakdown: null });
  expect('L: sellable pieces and an arrow', await b.eval(figure(productRow('L'))),
    { sellable: String(want.L.sellable), arrow: true, hover: `+ ${want.L.damaged} damaged`, breakdown: null });
  expect('B: no stock changes, just the number (as before)', await b.eval(figure(productRow('B'))),
    { sellable: String(want.B.sellable), arrow: false, hover: null, breakdown: null });

  await b.eval(click(productRow('G'))); await sleep(500);
  expect('G: click opens the breakdown', (await b.eval(figure(productRow('G')))).breakdown,
    `Sellable ${want.G.sellable} Damaged ${want.G.damaged} Waiting approval ${want.G.waiting}`);
  expect('the button says it is open', await b.eval(`(${productRow('G')}).querySelector('.stock-toggle').getAttribute('aria-expanded')`), 'true');
  await b.shot(`${shots}stock-product-light.png`);
  expect('no sideways scroll on the product page', await b.eval(`document.documentElement.scrollWidth <= document.documentElement.clientWidth`), true);
  await b.eval(click(productRow('G'))); await sleep(400);
  expect('a second click closes it', (await b.eval(figure(productRow('G')))).breakdown, null);

  log('--- keyboard still works after the click');
  await b.eval(`(${productRow('G')}).querySelector('.stock-toggle').focus()`);
  await b.key('Escape'); await sleep(1200);
  expect('Esc goes back to the product list', (await b.path()).toLowerCase(), '/products');

  log('--- dark mode');
  await b.eval(`(() => { try { localStorage.setItem('inventory-theme', JSON.stringify({ colour: 'Teal', mode: 'Dark' })); } catch { } })()`);
  await b.goto(`/product/${want.productGuid}`);
  await b.waitFor(`!!(${productRow('G')})`, 15000); await sleep(800);
  await b.eval(click(productRow('G'))); await sleep(500);
  await b.shot(`${shots}stock-product-dark.png`);
  await b.eval(`(() => { try { localStorage.removeItem('inventory-theme'); } catch { } })()`);

  log('--- Reports, Products');
  const openProductsTab = async (tab) => {
    await b.goto('/reports');
    await b.waitFor(`[...document.querySelectorAll('.mud-tab')].some(t => t.innerText.trim().toUpperCase() === 'PRODUCTS')`, 15000);
    await b.eval(`[...document.querySelectorAll('.mud-tab')].find(t => t.innerText.trim().toUpperCase() === 'PRODUCTS').click()`); await sleep(800);
    await b.waitFor(`[...document.querySelectorAll('.mud-tab')].some(t => t.innerText.trim().toUpperCase() === '${tab.toUpperCase()}')`, 15000);
    await b.eval(`[...document.querySelectorAll('.mud-tab')].find(t => t.innerText.trim().toUpperCase() === '${tab.toUpperCase()}').click()`); await sleep(1500);
  };
  const headNames = `[...[...document.querySelectorAll('.mud-table-head')].pop().querySelectorAll('th')].map(h => h.innerText.trim())`;
  const gridRow = (code) => `(() => { const names = ${headNames};
    const rows = [...document.querySelectorAll('.mud-table-body tr')].map(r => [...r.children].map(td => td.innerText.trim()));
    const row = rows.find(r => r[names.indexOf('Product ID')] === '${want.productId}' && r[names.indexOf('Variant ID')] === '${code}');
    return row ? { stock: row[names.indexOf('Stock In Hand')], damaged: row[names.indexOf('Damaged')] } : null; })()`;

  await openProductsTab('Variants List Table');
  await b.eval(`(() => { const i = [...document.querySelectorAll('input[placeholder="Search"]')].filter(i => i.offsetParent).pop(); i.focus(); })()`);
  await b.type(want.productId); await sleep(1500);
  expect('Variants List: G stock in hand and damaged', await b.eval(gridRow('G')), { stock: String(want.G.sellable), damaged: String(want.G.damaged) });
  expect('Variants List: B no damage', await b.eval(gridRow('B')), { stock: String(want.B.sellable), damaged: '0' });
  await b.shot(`${shots}stock-variants-list.png`);

  await openProductsTab('Reorder Level Table');
  expect('Reorder Level table has no Damaged column (Hiten)', await b.eval(`${headNames}.includes('Damaged')`), false);

  log('--- Variant Report Table');
  await openProductsTab('Variant Report Table');
  await b.eval(`(() => { const i = [...document.querySelectorAll('.mud-tabs-panels input[type=text], .mud-tabs-panels input:not([type])')].filter(i => i.offsetParent).pop(); i.focus(); i.select(); })()`);
  await b.type(want.productId);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`, 10000);
  await b.key('ArrowDown'); await sleep(300); await b.key('Enter'); await sleep(2000);
  const reportRow = code => `[...document.querySelectorAll('.mud-table tbody tr')].find(r => r.children[0] && r.children[0].innerText.trim() === '${code}')`;
  expect('Variant Report: G row', await b.eval(figure(reportRow('G'))),
    { sellable: String(want.G.sellable), arrow: true, hover: `+ ${want.G.damaged} damaged · ${want.G.waiting} waiting`, breakdown: null });
  const totalLine = `[...document.querySelectorAll('h6, .mud-typography-h6')].find(e => e.innerText.trim().startsWith('Total Stock In Hand'))`;
  const total = await b.eval(figure(totalLine));
  expect('Variant Report: total has the arrow, damaged only on a click', { arrow: total?.arrow, hover: total?.hover, breakdown: total?.breakdown },
    { arrow: true, hover: '+ 4 damaged · 2 waiting', breakdown: null });
  await b.eval(click(totalLine)); await sleep(400);
  const opened = (await b.eval(figure(totalLine)))?.breakdown || '';
  expect('Variant Report: total breakdown on a click', /^Sellable \d+ Damaged 4 Waiting approval 2$/.test(opened), true);
  log('     ', opened);
  await b.shot(`${shots}stock-variant-report.png`);
} finally {
  b.close();
}

log(failures === 0 ? '\nALL STOCK FIGURE CHECKS PASS' : `\n${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
