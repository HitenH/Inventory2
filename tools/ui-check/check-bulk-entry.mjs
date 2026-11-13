// Bulk Entry pop-up (Nov 2026) on Purchase Orders, Purchases and Sales Orders, plus the Supplier Name column
// and the "Open" default status. Run against the TEST copy only (see README).
import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const b = await launch();

async function pickFromList(text) {
  await b.type(text);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300);
  await b.key('Enter'); await sleep(2000);
}
// Next variant in the drop-down (n steps down), then Enter = move to Quantity.
async function variant(n) {
  if ((await b.active()).includes('focus-bulk-product')) { await b.key('Tab'); await sleep(300); }
  for (let i = 0; i < n; i++) { await b.key('ArrowDown'); await sleep(150); }
  await b.key('Enter'); await sleep(400);
}
const dialogRows = () => b.eval(`[...document.querySelectorAll('.mud-dialog tr.bulk-row')].map(r => r.innerText.replace(/\\s+/g, ' ').trim())`);
const dialogOpen = () => b.eval(`!!document.querySelector('.mud-dialog .bulk-entry')`);
const lineRows = () => b.eval(`[...document.querySelectorAll('table.table tbody tr')].filter(r => !r.innerText.includes('Not Found')).map(r => r.innerText.replace(/\\s+/g, ' ').trim())`);

try {
  await login(b);

  log('=== Purchase Orders');
  await b.goto('/purchaseorder'); await sleep(2000);
  log('status box =', await b.eval(`document.querySelector('#orderstatus').value`));
  log('label =', await b.eval(`[...document.querySelectorAll('label')].map(l => l.innerText).filter(t => t.startsWith('Supplier')).join(',')`));
  log('headers =', await b.eval(`[...document.querySelectorAll('.mud-table-head th')].map(t => t.innerText.trim()).filter(Boolean).join(' | ')`));
  log('first rows =', JSON.stringify(await b.eval(`[...document.querySelectorAll('.mud-table-body tr')].slice(0,3).map(r => r.innerText.replace(/\\s+/g,' ').trim())`)));
  const before = await b.eval(`document.querySelectorAll('.mud-table-body tr').length`);

  await b.click('#bulk-entry-open'); await sleep(1500);
  log('opened; cursor =', await b.active());
  await b.key('Escape'); await sleep(800);
  log('Esc with no rows closes it:', !(await dialogOpen()));

  await b.click('#bulk-entry-open'); await sleep(1500);
  await pickFromList('BALAJI');
  log('after supplier Enter, cursor =', await b.active());
  await pickFromList('LAZER');
  log('after product Enter, cursor =', await b.active(), '| rate =', await b.eval(`document.querySelector('#bulk-rate').value`));
  await variant(1);
  log('after variant Enter, cursor =', await b.active());
  await b.type('5'); await b.key('Enter'); await sleep(1200);
  log('row 1 added; cursor =', await b.active(), '| rows =', JSON.stringify(await dialogRows()));
  await b.key('Tab'); await sleep(300);
  log('Tab from Product goes to:', await b.active());
  await b.key('Tab', 8); await sleep(300);   // Shift+Tab back to Product
  await b.key('Enter'); await sleep(800);
  log('Enter on Product (product unchanged) goes to:', await b.active());
  await variant(2); await b.type('3'); await b.key('Enter'); await sleep(1200);
  await variant(1); await b.type('1'); await b.key('Enter'); await sleep(1200);   // same variant again -> two rows
  log('rows =', JSON.stringify(await dialogRows()));

  // Change the product.
  await b.click('.focus-bulk-product input'); await sleep(300);
  await b.key('a', 2); await sleep(100);
  await pickFromList('CUTTING');
  await variant(1); await b.type('10'); await b.key('Enter'); await sleep(1200);
  log('after product change rows =', JSON.stringify(await dialogRows()));
  log('footer =', await b.eval(`document.querySelector('.bulk-count').innerText`));

  // Missing quantity -> message, nothing added.
  await variant(1); await b.key('Enter'); await sleep(1000);
  log('no quantity: snack =', await b.snack(), '| rows =', (await dialogRows()).length, '| cursor =', await b.active());

  // Remove the last row with its X.
  await b.eval(`[...document.querySelectorAll('.mud-dialog tr.bulk-row')].pop().querySelector('button').click()`); await sleep(600);
  log('after X: rows =', (await dialogRows()).length);

  // Esc with rows asks first.
  await b.click('#bulk-quantity'); await b.key('Escape'); await sleep(1000);
  log('Esc with rows: confirm shown =', await b.eval(`document.body.innerText.includes('Close without saving?')`));
  await b.eval(`[...[...document.querySelectorAll('.mud-dialog')].pop().querySelectorAll('button')].find(x => /^cancel$/i.test(x.innerText.trim())).click()`); await sleep(800);
  log('after Cancel on confirm: still open =', await dialogOpen(), '| rows =', (await dialogRows()).length);

  // Esc again, then Enter: closes without saving (the button is ready for Enter).
  await b.click('#bulk-quantity'); await b.key('Escape'); await sleep(1000);
  log('confirm: cursor on =', await b.eval(`document.activeElement.innerText`));
  await b.key('Enter'); await sleep(1200);
  log('Enter closed it without saving:', !(await dialogOpen()), '| list rows =', await b.eval(`document.querySelectorAll('.mud-table-body tr').length`));
  // Open again with fresh rows for the save test.
  await b.click('#bulk-entry-open'); await sleep(1500);
  await pickFromList('BALAJI'); await pickFromList('LAZER');
  await variant(1); await b.type('5'); await b.key('Enter'); await sleep(1200);
  await variant(2); await b.type('3'); await b.key('Enter'); await sleep(1200);
  await variant(1); await b.type('1'); await b.key('Enter'); await sleep(1200);
  // Ctrl+S saves all.
  await b.click('#bulk-quantity'); await b.key('s', 2); await sleep(2500);
  log('Ctrl+S: dialog closed =', !(await dialogOpen()), '| snack =', await b.snack(), '| path =', await b.path());
  const after = await b.eval(`document.querySelectorAll('.mud-table-body tr').length`);
  log('list rows', before, '->', after);
  log('new rows =', JSON.stringify(await b.eval(`[...document.querySelectorAll('.mud-table-body tr')].filter(r => r.innerText.includes('LAZER')).slice(0,4).map(r => r.innerText.replace(/\\s+/g,' ').trim())`)));
  log('cursor =', await b.active());

  log('--- Set status of ticked orders');
  await b.click('#set-status-apply'); await sleep(800);
  log('nothing ticked: snack =', await b.snack());
  const boxes = `[...document.querySelectorAll('.mud-table-body tr')].filter(r => r.innerText.includes('LAZER') && r.innerText.includes('Open'))`;
  await b.eval(`${boxes}.slice(0,2).forEach(r => r.querySelector('input[type=checkbox]').click())`); await sleep(600);
  log('select value =', await b.eval(`document.querySelector('#set-status').value`));
  await b.click('#set-status-apply'); await sleep(1200);
  log('confirm text =', await b.eval(`[...document.querySelectorAll('.mud-dialog')].pop().innerText.replace(/\\s+/g,' ')`), '| cursor on =', await b.eval(`document.activeElement.innerText`));
  await b.key('Enter'); await sleep(2000);
  log('after Enter: snack =', await b.snack());
  log('LAZER rows now =', JSON.stringify(await b.eval(`[...document.querySelectorAll('.mud-table-body tr')].filter(r => r.innerText.includes('LAZER')).map(r => r.innerText.replace(/\\s+/g,' ').trim().split(' ').slice(-1)[0])`)));

  log('=== Purchase');
  await b.goto('/purchase'); await sleep(1500);
  log('Bulk Entry button before a supplier is picked:', await b.eval(`!!document.querySelector('#bulk-entry-open')`));
  await b.click('.focus-supplier input'); await sleep(200);
  await pickFromList('BALAJI'); await sleep(500);
  await b.click('#bulk-entry-open'); await sleep(1500);
  log('opened; cursor =', await b.active(), '| has discount =', await b.eval(`!!document.querySelector('#bulk-discount')`), '| has remarks =', await b.eval(`!!document.querySelector('#bulk-remarks')`));
  await pickFromList('LAZER');
  log('rate after product =', JSON.stringify(await b.eval(`document.querySelector('#bulk-rate').value`)));
  await variant(1); await b.type('2'); await b.key('Tab'); await b.type('100'); await b.key('Tab'); await b.type('10'); await b.key('Enter'); await sleep(1200);
  log('cursor =', await b.active());
  await variant(2); await b.type('4'); await b.key('Enter'); await sleep(1200);
  log('rows =', JSON.stringify(await dialogRows()));
  await b.click('#bulk-save'); await sleep(3000);
  log('saved: snack =', await b.snack(), '| lines =', JSON.stringify(await lineRows()));
  log('total =', await b.eval(`[...document.querySelectorAll('input')].filter(i => /total/i.test(i.id)).map(i => i.id + '=' + i.value).join(',')`));
  log('cursor =', await b.active());

  log('=== Sales Order');
  await b.goto('/salesorder'); await sleep(1500);
  log('status box =', await b.eval(`document.querySelector('#orderstatus').value`));
  await b.click('.focus-customer input'); await sleep(200);
  await pickFromList('MATHA'); await sleep(500);
  await b.click('#bulk-entry-open'); await sleep(1500);
  log('has discount =', await b.eval(`!!document.querySelector('#bulk-discount')`), '| has remarks =', await b.eval(`!!document.querySelector('#bulk-remarks')`));
  await pickFromList('LAZER');
  await variant(1); await b.type('1'); await b.key('Tab'); await b.type('50'); await b.key('Tab'); await b.key('Tab'); await b.type('gift'); await b.key('Enter'); await sleep(1200);
  log('rows =', JSON.stringify(await dialogRows()));
  await b.click('#bulk-save'); await sleep(3000);
  log('saved: snack =', await b.snack(), '| lines =', JSON.stringify(await lineRows()));
} catch (e) {
  log('FAILED:', e.message);
  await b.shot((process.env.SCRATCH || '.') + '/bulk-fail.png');
} finally {
  b.close();
}
