// Alt+B opens Bulk Entry (Purchase Orders, Purchases, Sales Orders) and Scan Mass Products (Sale). TEST copy only.
import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const b = await launch();
const dialogTitle = () => b.eval(`(document.querySelector('.mud-dialog')?.innerText || '').trim().slice(0, 30)`);
const dialogs = () => b.eval(`document.querySelectorAll('.mud-dialog').length`);
async function pickFromList(text) {
  await b.type(text);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300);
  await b.key('Enter'); await sleep(2000);
}

try {
  await login(b);

  log('=== Purchase Orders');
  await b.goto('/purchaseorder'); await sleep(2000);
  await b.key('b', 1); await sleep(1500);
  log('Alt+B: bulk open =', await b.eval(`!!document.querySelector('.mud-dialog .bulk-entry')`), '| cursor =', await b.active());
  await b.key('b', 1); await sleep(800);
  log('Alt+B again inside pop-up: dialogs =', await dialogs());
  await b.key('Escape'); await sleep(800);
  log('Esc closed:', (await dialogs()) === 0);

  log('=== Purchase');
  await b.goto('/purchase'); await sleep(1500);
  await b.key('b', 1); await sleep(1000);
  log('Alt+B before supplier: dialogs =', await dialogs(), '| path =', await b.path());
  await b.click('.focus-supplier input'); await sleep(200);
  await pickFromList('BALAJI'); await sleep(500);
  log('button shown =', await b.eval(`!!document.querySelector('[data-shortcut=bulk]')`), '| visible =', await b.eval(`document.querySelector('[data-shortcut=bulk]')?.offsetParent !== null`));
  await b.key('b', 1); await sleep(1500);
  log('Alt+B after supplier: bulk open =', await b.eval(`!!document.querySelector('.mud-dialog .bulk-entry')`), '| cursor =', await b.active());
  await b.key('Escape'); await sleep(800);

  log('=== Sales Order');
  await b.goto('/salesorder'); await sleep(1500);
  await b.click('.focus-customer input'); await sleep(200);
  await pickFromList('MATHA'); await sleep(500);
  await b.key('b', 1); await sleep(1500);
  log('Alt+B: bulk open =', await b.eval(`!!document.querySelector('.mud-dialog .bulk-entry')`));
  await b.key('Escape'); await sleep(800);

  log('=== Sale');
  await b.goto('/sale'); await sleep(2000);
  await b.key('b', 1); await sleep(1500);
  log('Alt+B: dialog =', JSON.stringify(await dialogTitle()), '| dialogs =', await dialogs(), '| cursor =', await b.active());
  await b.key('Escape'); await sleep(800);
  log('Esc closed:', (await dialogs()) === 0);

  log('=== Legend');
  await b.goto('/'); await sleep(1500);
  await b.click('button[aria-label="Keyboard shortcuts"]'); await sleep(1000);
  log('legend line =', JSON.stringify(await b.eval(`[...document.querySelectorAll('.mud-popover-open p')].map(p => p.innerText).find(t => t.startsWith('Alt+B')) || ''`)));
  log('legend has Alt+B:', await b.eval(`document.body.innerText.includes('Alt+B opens Bulk Entry')`));
} catch (e) {
  log('FAILED:', e.message);
  await b.shot((process.env.SCRATCH || '.') + '/alt-b-fail.png');
} finally {
  b.close();
}
