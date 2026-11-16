// Settings → Change folder (backup folder picker). TEST copy only; uses folders under SCRATCH.
import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const scratch = process.env.SCRATCH;
if (!scratch) throw new Error('Set SCRATCH');
const shots = scratch + '\\shots'; (await import('node:fs')).mkdirSync(shots, { recursive: true });
const b = await launch();
const text = sel => b.eval(`(document.querySelector(${JSON.stringify(sel)})?.innerText || '').trim()`);
const clickButton = label => b.eval(`(() => { const x = [...document.querySelectorAll('button')].find(e => e.innerText.trim() === ${JSON.stringify(label)}); if (!x) return false; x.click(); return true; })()`);
async function setPath(path) {
  await b.click('.mud-dialog input'); await sleep(100);
  await b.eval(`document.querySelector('.mud-dialog input').select()`);
  await b.type(path); await sleep(300);
  await b.key('Enter'); await sleep(800);
}

try {
  await login(b);
  await b.goto('/settings'); await sleep(1500);
  log('folder shown:', await b.eval(`[...document.querySelectorAll('p')].find(p => p.innerText.startsWith('Backup folder'))?.innerText`));
  log('alerts:', await b.eval(`[...document.querySelectorAll('.mud-alert')].map(a => a.innerText.slice(0, 60))`));
  log('backups listed:', await b.eval(`document.querySelectorAll('tbody tr').length`));
  await b.shot(shots + '\\settings-before.png');

  log('=== open picker');
  log('clicked:', await clickButton('Change folder…')); await sleep(1200);
  log('dialog title:', await text('.mud-dialog .mud-dialog-title'));
  log('drives:', await b.eval(`[...document.querySelectorAll('.mud-dialog .mud-dialog-content button')].slice(0, 6).map(x => x.innerText)`));
  log('path box:', await b.eval(`document.querySelector('.mud-dialog input').value`));
  await b.shot(shots + '\\picker.png');

  log('=== database folder is refused');
  await setPath(scratch + '\\data');
  await clickButton('Use this folder'); await sleep(1000);
  log('snack:', await b.snack(), '| dialog closed:', !(await b.eval(`!!document.querySelector('.mud-dialog')`)));

  log('=== make a new folder and choose it');
  await clickButton('Change folder…'); await sleep(1200);
  await setPath(scratch);
  log('folders listed:', await b.eval(`document.querySelectorAll('.mud-dialog .mud-list-item').length`));
  await b.click('.mud-dialog input[placeholder="New folder name"]'); await b.type('USB Backups'); await sleep(300);
  await clickButton('Make new folder'); await sleep(1000);
  log('path box now:', await b.eval(`document.querySelector('.mud-dialog input').value`));
  await clickButton('Use this folder'); await sleep(1200);
  log('question:', (await text('.mud-dialog')).slice(0, 160));
  await b.shot(shots + '\\copy-question.png');
  await clickButton('Copy them'); await sleep(2000);
  log('snack:', await b.snack());
  log('folder shown:', await b.eval(`[...document.querySelectorAll('p')].find(p => p.innerText.startsWith('Backup folder'))?.innerText`));
  log('backups listed:', await b.eval(`document.querySelectorAll('tbody tr').length`));

  log('=== Backup button writes to the new folder');
  await clickButton('Backup'); await sleep(2500);
  log('snack:', await b.snack());
  log('backups listed:', await b.eval(`document.querySelectorAll('tbody tr').length`));
  await b.shot(shots + '\\settings-after.png');
} catch (e) {
  log('ERROR', e.message);
} finally {
  b.close();
}
