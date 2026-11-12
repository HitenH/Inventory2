import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const b = await launch();

async function pickFromList(text) {
  await b.type(text);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300);
  await b.key('Enter'); await sleep(2000);
}
const shownName = sel => b.eval(`document.querySelector('${sel} input')?.value`);
const dialogOpen = () => b.eval(`!!document.querySelector('.mud-dialog-container')`);

try {
  await login(b);

  log('--- 1. Purchase order: cursor after adding a row');
  await b.goto('/purchaseorder'); await sleep(1500);
  await pickFromList('BALAJI');
  await pickFromList('LAZER');
  await b.click('.btn-order-add'); await sleep(2500);
  log('after add: active =', await b.active(), '| snack =', await b.snack(), '| error box =', await b.eval(`document.body.innerText.includes('Something went wrong')`));

  log('--- 2. Sale');
  await b.goto('/sale'); await sleep(2000);
  log('load: active =', await b.active());
  await pickFromList('AAI MATHA');
  log('after customer: active =', await b.active(), '| customer shown =', await shownName('.focus-customer'));
  await pickFromList('LAZER');
  log('after Enter on product: active =', await b.active(), '| class =', await b.eval(`document.activeElement.className`));
  await b.eval(`(() => { const s = document.querySelector('.sale-line-variant'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(400);
  await b.click('.btn-order-add'); await sleep(2000);
  log('after line add: rows =', await b.eval(`[...document.querySelectorAll('table.table tbody tr')].filter(r => !r.innerText.includes('Not Found')).length`), '| active =', await b.active());

  log('--- 3+4. Search pop-up (supplier, on a new purchase)');
  await b.goto('/purchase'); await sleep(1500);
  await b.click('.focus-supplier button'); await sleep(1500);
  log('pop-up open =', await dialogOpen(), '| cursor in search bar =', await b.eval(`document.activeElement.classList.contains('lookup-dialog-search')`),
      '| Select buttons left =', await b.eval(`[...document.querySelectorAll('.mud-dialog button')].filter(x => x.innerText.trim() === 'SELECT' || x.innerText.trim() === 'Select').length`));
  const first = await b.eval(`document.querySelector('.lookup-row-active')?.innerText`);
  await b.key('ArrowDown'); await sleep(400);
  await b.key('ArrowDown'); await sleep(400);
  await b.key('ArrowUp'); await sleep(400);
  const hl = await b.eval(`document.querySelector('.lookup-row-active')?.innerText`);
  log('highlight starts on', JSON.stringify(first), '-> after Down,Down,Up on', JSON.stringify(hl));
  await b.key('Enter'); await sleep(2500);
  log('after Enter: pop-up open =', await dialogOpen(), '| supplier shown =', await shownName('.focus-supplier'), '| path =', await b.path(), '| active =', await b.active());

  log('--- typing in the pop-up search, then clicking a row (customer, on a new sales order)');
  await b.goto('/salesorder'); await sleep(1500);
  await b.click('.focus-customer button'); await sleep(1500);
  await b.type('MATHA'); await sleep(800);
  const rows = await b.eval(`document.querySelectorAll('.lookup-dialog tbody tr.lookup-row').length`);
  const second = await b.eval(`document.querySelectorAll('.lookup-dialog tbody tr.lookup-row')[1]?.innerText`);
  log('rows after typing MATHA =', rows, '| clicking row 2:', JSON.stringify(second));
  await b.click('.lookup-dialog tbody tr.lookup-row:nth-child(2) td'); await sleep(2000);
  log('after click: pop-up open =', await dialogOpen(), '| customer shown =', await shownName('.focus-customer'), '| active =', await b.active());

  log('--- Esc in the pop-up closes only the pop-up');
  await b.click('.focus-customer button'); await sleep(1500);
  await b.key('Escape'); await sleep(1000);
  log('after Esc: pop-up open =', await dialogOpen(), '| path =', await b.path());
} catch (e) {
  log('ERROR', e.message);
  await b.shot(process.env.SCRATCH + '\\error.png');
} finally {
  b.close();
}
