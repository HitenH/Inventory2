import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const b = await launch();

async function pickFromList(text) {
  await b.type(text);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300);
  await b.key('Enter'); await sleep(2000);
}
async function pickFromPopup(boxSelector, text) {
  await b.click(boxSelector + ' button'); await sleep(1500);
  await b.type(text); await sleep(800);
  await b.key('Enter');
}
// Where is the cursor, sampled every 150 ms for 3 s: shows any flashing back and forth.
async function samples() {
  const seen = [];
  for (let i = 0; i < 20; i++) { seen.push(await b.active()); await sleep(150); }
  const compact = seen.map(s => s.replace(/INPUT#mudinput\w+ /, 'input ')).filter((s, i, a) => i === 0 || s !== a[i - 1]);
  return compact.join('  ->  ');
}
const lineRows = () => b.eval(`[...document.querySelectorAll('table.table tbody tr')].filter(r => !r.innerText.includes('Not Found')).map(r => r.innerText.replace(/\\s+/g, ' ').trim())`);

try {
  await login(b);

  log('--- Purchase: Enter in a typing box saves the line at once');
  await b.goto('/purchase'); await sleep(1500);
  await pickFromList('BALAJI');                      // Enter saves the purchase, cursor to Product
  await pickFromList('LAZER');                       // Enter -> Variant
  await b.key('ArrowDown'); await sleep(300);
  await b.key('Enter'); await sleep(500);
  log('after Enter on variant (expect quantity):', await b.active());
  await b.type('2'); await b.key('Enter'); await sleep(2500);
  log('Enter on quantity: rows =', JSON.stringify(await lineRows()), '| snack =', await b.snack());
  log('cursor after line added:', await samples());
  await pickFromList('CUTTING');
  await b.key('ArrowDown'); await sleep(300);
  await b.key('Enter'); await sleep(500);
  await b.type('3'); await b.key('Tab'); await sleep(300); await b.type('40'); await b.key('Enter'); await sleep(2500);
  log('Enter on rate: rows =', JSON.stringify(await lineRows()));
  log('cursor after line added:', await samples());

  log('--- Sale: customer via pop-up');
  await b.goto('/sale'); await sleep(2000);
  await pickFromPopup('.focus-customer', 'MATHA');
  log('cursor over 3 s:', await samples());
  log('customer shown =', await b.eval(`document.querySelector('.focus-customer input').value`));

  log('--- Sale: product via pop-up');
  await pickFromPopup('.focus-line-product', 'LAZER');
  log('cursor over 3 s:', await samples());
  await b.key('ArrowDown'); await sleep(300);
  await b.key('Enter'); await sleep(500);
  log('after Enter on variant:', await b.active());
  await b.key('Enter'); await sleep(2000);
  log('after Enter on quantity: rows =', JSON.stringify(await lineRows()));
  log('cursor after line added:', await samples());

  log('--- Sale: Scan Mass Products pop-up');
  await b.click('a.btn-primary'); await sleep(2000);
  log('on opening: cursor in Product ID =', await b.eval(`document.activeElement.classList.contains('scan-mass-product')`), '| over 3 s:', await samples());
  await b.type('AJ0120302'); await b.key('Tab'); await b.type('B'); await b.key('Enter'); await sleep(1500);
  log('after first scan: cursor in Product ID =', await b.eval(`document.activeElement.classList.contains('scan-mass-product')`),
      '| scanned rows =', await b.eval(`document.querySelectorAll('.mud-dialog tbody tr').length`));
  await b.type('AJ0120302'); await b.key('Tab'); await b.type('B'); await b.key('Enter'); await sleep(1500);
  log('after second scan: cursor in Product ID =', await b.eval(`document.activeElement.classList.contains('scan-mass-product')`),
      '| scanned rows =', await b.eval(`document.querySelectorAll('.mud-dialog tbody tr').length`));
  await b.key('Escape'); await sleep(800);

  log('--- New purchase: supplier via pop-up');
  await b.goto('/purchase'); await sleep(1500);
  await pickFromPopup('.focus-supplier', 'CAPITAL');
  await sleep(1500);
  log('voucher =', await b.eval(`document.querySelector('#voucher').value`), '| cursor over 3 s:', await samples());

  log('--- Purchase order: Enter on quantity adds the row');
  await b.goto('/purchaseorder'); await sleep(1500);
  const before = await b.eval(`document.querySelectorAll('.mud-table-body tr').length`);
  await pickFromList('BALAJI');
  await pickFromList('LAZER');
  await b.key('Enter'); await sleep(400);
  log('after Enter on variant (expect quantity):', await b.active());
  await b.type('5'); await b.key('Enter'); await sleep(2500);
  log('rows before/after =', before, '/', await b.eval(`document.querySelectorAll('.mud-table-body tr').length`), '| cursor:', await samples());
} catch (e) {
  log('ERROR', e.message);
  await b.shot(process.env.SCRATCH + '\\error.png');
} finally {
  b.close();
}
