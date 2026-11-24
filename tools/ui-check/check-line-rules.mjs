// Version 3.0, stage 1a: proves in a real browser that the one rule book (Service/LineRules.cs)
// is actually reached from every screen that makes a voucher line.
//
// Before stage 1a all of these saved happily: a quantity of 0 or a negative on Purchase, Sales Order
// and Sale, a discount over 100 anywhere (which makes the line amount NEGATIVE and quietly lowers the
// voucher, supplier and report totals), and a quantity typed straight into the Sale lines table.
//
// Development only. Never point this at the real data - it creates purchases, sales and orders.
import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const b = await launch();
let failures = 0;

function expect(what, actual, wanted) {
  const ok = actual === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${actual}${ok ? '' : `  (expected ${wanted})`}`);
}

async function pickFromList(text) {
  await b.type(text);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown');
  await sleep(300);
  await b.key('Enter');
  await sleep(2000);
}

// The line editor shared by Purchase and Sales Order. Qty/Rate/Discount are MudBlazor boxes
// with no ids, so they are found by their floating label ("Qty", "Rate", "Disc %").
// The box is cleared first: these are re-used between attempts, and typing into one that still
// held a value is what made this script report a false failure the first time it ran.
async function typeInBox(label, value) {
  await b.eval(`(() => {
    const l = [...document.querySelectorAll('label')].find(l => l.textContent.trim() === ${JSON.stringify(label)});
    if (!l) throw new Error('no box labelled ${label}');
    const i = l.parentElement.querySelector('input');
    i.focus(); i.value = ${JSON.stringify(String(value))};
    i.dispatchEvent(new Event('input', { bubbles: true }));
    i.dispatchEvent(new Event('change', { bubbles: true }));
  })()`);
  await sleep(500);
}

async function chooseFirstVariant() {
  await b.eval(`(() => { const s = document.querySelector('#variantid'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(400);
}

// Only real line rows: an empty table shows one "Not Found!" row, which must not be counted.
const lineCount = () => b.eval(`[...document.querySelectorAll('table.table tbody tr')].filter(r => !r.innerText.includes('Not Found')).length`);

try {
  await login(b);

  // ---------------------------------------------------------------- Purchase
  log('--- Purchase line: quantity and discount');
  await b.goto('/purchase'); await sleep(1500);
  await pickFromList('BALAJI');
  await pickFromList('LAZER');
  await chooseFirstVariant();

  await typeInBox('Qty', 0);
  const before = await lineCount();
  await b.click('.btn-order-add'); await sleep(1500);
  expect('quantity 0 is refused', (await b.snack()).includes('Quantity cannot be zero.'), true);
  expect('quantity 0 added no line', await lineCount(), before);

  // Negative quantities are allowed on purpose (returns and damage are recorded that way for now).
  await typeInBox('Qty', -5);
  await b.click('.btn-order-add'); await sleep(2000);
  expect('a negative quantity line is saved', await lineCount(), before + 1);

  // A saved line clears the form and puts the cursor back in Product, so pick the product again.
  await pickFromList('LAZER');
  await chooseFirstVariant();

  // A discount over 100 is the one that costs money: it makes the line amount negative.
  await typeInBox('Qty', 2);
  await typeInBox('Rate', 100);
  await typeInBox('Disc %', 500);
  await b.click('.btn-order-add'); await sleep(1500);
  expect('discount 500 is refused', (await b.snack()).includes('Discount must be between 0 and 100 percent.'), true);
  expect('discount 500 added no line', await lineCount(), before + 1);

  // ... and a sensible line still saves, so the rules have not blocked normal work.
  await typeInBox('Disc %', 10);
  await b.click('.btn-order-add'); await sleep(2500);
  expect('a good line still saves', await lineCount(), before + 2);
  const saved = await b.eval(`(() => { const r = [...document.querySelectorAll('table.table tbody tr')].filter(r => !r.innerText.includes('Not Found')); return r.length ? r[r.length - 1].innerText.replace(/\\s+/g, ' ').trim() : ''; })()`);
  log('     saved line =', saved);
  expect('its amount is not negative', saved.includes('-'), false);

  // ------------------------------------------------------------- Sales Order
  log('--- Sales Order line: same rules, same rule book');
  await b.goto('/salesorder'); await sleep(1500);
  await pickFromList('AAI MATHA');
  await pickFromList('LAZER');
  await chooseFirstVariant();
  await typeInBox('Qty', 0);
  await b.click('.btn-order-add'); await sleep(1500);
  expect('quantity 0 is refused', (await b.snack()).includes('Quantity cannot be zero.'), true);
  await typeInBox('Qty', 1);
  await typeInBox('Disc %', 200);
  await b.click('.btn-order-add'); await sleep(1500);
  expect('discount 200 is refused', (await b.snack()).includes('Discount must be between 0 and 100 percent.'), true);

  // --------------------------------------------------------------- Sale
  log('--- Sale line form and the quantity box in the lines table');
  await b.goto('/sale'); await sleep(1500);
  await pickFromList('AAI MATHA');
  await pickFromList('LAZER');
  await sleep(500);
  await b.eval(`(() => { const s = document.querySelector('.sale-line-variant'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(400);
  await b.eval(`document.querySelector('#quantity').focus()`);
  await b.eval(`(() => { const q = document.querySelector('#quantity'); q.value = '0'; q.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(400);
  await b.key('Enter'); await sleep(1500);
  expect('sale quantity 0 is refused', (await b.snack()).includes('Quantity cannot be zero.'), true);

  // Now add a good line, then try to ruin it by typing into the table's own quantity box.
  // The box must be focused again: the refusal above left the cursor elsewhere, and Enter goes
  // to whatever holds the cursor.
  await b.eval(`(() => { const q = document.querySelector('#quantity'); q.focus(); q.value = '2';
    q.dispatchEvent(new Event('input', { bubbles: true })); q.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(600);
  await b.eval(`document.querySelector('#quantity').focus()`);
  await b.key('Enter'); await sleep(2500);
  const saleRows = await b.eval(`document.querySelectorAll('.qty-box').length`);
  expect('a good sale line was added', saleRows >= 1, true);

  await b.eval(`(() => { const q = document.querySelector('.qty-box'); q.value = '0'; q.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(1500);
  expect('0 typed into the lines table is refused', (await b.snack()).includes('Quantity cannot be zero.'), true);
  expect('the line kept its real quantity', await b.eval(`document.querySelector('.qty-box').value`), '2');

  // ----------------------------------------------------------- Bulk Entry
  log('--- Bulk Entry: the discount box used to accept anything');
  await b.goto('/purchase'); await sleep(1500);
  await pickFromList('BALAJI');
  await b.click('[data-shortcut="bulk"]');
  await b.waitFor(`document.querySelector('#bulk-quantity')`);
  await sleep(500);
  await pickFromList('LAZER');
  await b.eval(`(() => { const s = document.querySelector('#bulk-variant'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(300);
  await b.eval(`(() => { const q = document.querySelector('#bulk-quantity'); q.value = '2'; q.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await b.eval(`(() => { const d = document.querySelector('#bulk-discount'); d.value = '500'; d.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(400);
  await b.click('#bulk-add');
  await sleep(1200);
  // Real rows carry .bulk-row; an empty list shows a "No rows yet." placeholder row.
  const bulkRows = await b.eval(`document.querySelectorAll('.mud-dialog tr.bulk-row').length`);
  expect('bulk discount 500 is refused', (await b.snack()).includes('Discount must be between 0 and 100 percent.'), true);
  expect('bulk discount 500 added no row', bulkRows, 0);
  log('     cursor sent to =', await b.eval(`document.activeElement.id`));

  log(failures === 0 ? '\nALL LINE RULE CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`);
} catch (e) {
  log('ERROR', e.message);
  await b.shot(process.env.SCRATCH + '\\line-rules-error.png');
  failures++;
} finally {
  b.close();
}

process.exit(failures === 0 ? 0 : 1);
