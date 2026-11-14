import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const b = await launch();

async function pickFromList(text) {
  await b.type(text);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown');
  await sleep(300);
  await b.key('Enter');
  await sleep(2000);
}

async function addLine(qty) {
  await b.eval(`(() => { const s = document.querySelector('#variantid'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(400);
  await b.eval(`(() => { const label = [...document.querySelectorAll('label')].find(l => l.textContent.trim() === 'Qty'); label.parentElement.querySelector('input').focus(); })()`);
  await b.type(String(qty));
  await b.key('Tab');
  await sleep(400);
  await b.click('.btn-order-add');
  await sleep(2500);
}

try {
  await login(b);

  log('--- Purchase, two lines');
  await b.goto('/purchase'); await sleep(1500);
  await pickFromList('BALAJI');
  await pickFromList('LAZER');
  await addLine(2);
  log('after line 1: active =', await b.active());
  await pickFromList('CUTTING');
  log('product picked for line 2: active =', await b.active());
  await addLine(3);
  log('after line 2: rows =', await b.eval(`document.querySelectorAll('table.table tbody tr').length`), '| active =', await b.active(), '| snack =', await b.snack());

  log('--- Sales order');
  await b.goto('/salesorder'); await sleep(1500);
  log('new sales order: active =', await b.active());
  await pickFromList('AAI MATHA');
  log('after Enter on customer: snack =', await b.snack(), '| active =', await b.active());
  await pickFromList('LAZER');
  log('after Enter on product: active =', await b.active());
  await addLine(1);
  log('after line 1: rows =', await b.eval(`document.querySelectorAll('table.table tbody tr').length`), '| active =', await b.active(), '| snack =', await b.snack());

  log('--- Purchase order');
  await b.goto('/purchaseorder'); await sleep(1500);
  log('open: active =', await b.active());
  await pickFromList('BALAJI');
  log('after Enter on supplier: active =', await b.active(), '| error box =', await b.eval(`document.body.innerText.includes('Something went wrong')`));
  await pickFromList('LAZER');
  log('after Enter on product: active =', await b.active(), '| error box =', await b.eval(`document.body.innerText.includes('Something went wrong')`));

  log('--- Esc and Ctrl+S');
  await b.goto('/customer'); await sleep(1500);
  log('new customer: active =', await b.active());
  await b.key('Escape'); await sleep(1200);
  log('Esc 1 -> ', await b.path());
  await b.key('Escape'); await sleep(1200);
  log('Esc 2 -> ', await b.path());
  await b.goto('/purchase'); await sleep(1500);
  await b.type('BALA');
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('Escape'); await sleep(800);
  log('Esc with list open: path =', await b.path(), '| list still open =', await b.eval(`!!document.querySelector('.mud-popover-open .mud-list-item')`));
} catch (e) {
  log('ERROR', e.message);
  await b.shot(process.env.SCRATCH + '\\error.png');
} finally {
  b.close();
}
