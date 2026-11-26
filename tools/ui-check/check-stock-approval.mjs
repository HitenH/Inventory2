// Version 3.0, stage 2, batches 2-7/2-8: the reasons list (from Settings; Esc back to Settings), the Stock changes screen
// (Alt+N, Ctrl+S saves the header and the cursor goes to the line, a line added, damage found in the shop on the damage
// report, a count correction of -1 shown as -1), and approval: the Admin's switch in Settings, a stock change waiting
// (its banner), the "Waiting for approval" list, Approve. Switches approval off again at the end.
// Setup: a fresh test copy with the Admin test password (setup-damage-returns.py does it). It CHANGES the test copy.
import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const shots = process.env.SCRATCH || '.';
let failures = 0;
function expect(what, actual, wanted) {
  const ok = JSON.stringify(actual) === JSON.stringify(wanted);
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}
function truthy(what, actual) { expect(what, !!actual, true); }

const b = await launch(9343);
const snackHas = async (text) => { for (let i = 0; i < 20; i++) { if ((await b.snack()).includes(text)) return true; await sleep(250); } return false; };
const choose = (sel, value) => b.eval(`(() => { const s = document.querySelector('${sel}'); s.value = ${JSON.stringify(value)}; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
async function pickFromList(text) {
  await b.type(text);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300); await b.key('Enter'); await sleep(2000);
}
const qtyBox = `[...document.querySelectorAll('label')].find(l => l.textContent.trim() === 'Qty').parentElement.querySelector('input')`;
async function addLine(product, qty) {
  await b.eval(`document.querySelector('.focus-line-product input').focus()`);
  await pickFromList(product);
  await b.eval(`(() => { const s = document.querySelector('#variantid'); s.selectedIndex = 1; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(400);
  await b.eval(`(() => { const i = ${qtyBox}; i.focus(); i.select(); })()`); await b.type(String(qty)); await b.key('Enter'); await sleep(2500);
}
async function newEntry(type) {
  await b.goto('/stockchanges'); await sleep(2000);
  await b.key('n', 1); await sleep(1800);
  await choose('#stockchange-type', type); await sleep(300);
  await b.key('s', 2); await sleep(3000);
}
async function setApproval(on) {
  await b.goto('/settings'); await b.waitFor(`!!document.querySelector('.settings-approval input')`, 10000); await sleep(1000);
  const now = await b.eval(`document.querySelector('.settings-approval input').checked`);
  if (now !== on) {
    await b.eval(`document.querySelector('.settings-approval input').click()`);
    await b.waitFor(`!!document.querySelector('.mud-dialog')`, 5000); await sleep(600);
    await b.key('Enter'); await sleep(1500);
  }
  return b.eval(`document.querySelector('.settings-approval input').checked`);
}

try {
  await login(b);
  const product = 'AJ0120128';

  log('--- the reasons list');
  await b.goto('/settings'); await b.waitFor(`!!document.querySelector('.settings-approval input')`, 10000); await sleep(1000);
  await b.eval(`document.querySelector('#settings-reasons').click()`); await sleep(2500);
  expect('Settings → Reasons', (await b.path()).toLowerCase(), '/reasons');
  truthy('cursor in "New reason"', (await b.active()).includes('reason-new'));
  await b.type('Rat damage'); await b.key('Enter'); await sleep(1500);
  truthy('Enter adds it', (await b.eval(`[...document.querySelectorAll('.reason-name')].map(i => i.value)`)).includes('Rat damage'));
  await b.key('Escape'); await sleep(1500);
  expect('Esc goes back to Settings', (await b.path()).toLowerCase(), '/settings');

  log('--- the Stock changes screen');
  truthy('menu has Stock changes', await b.eval(`[...document.querySelectorAll('.mud-nav-link')].some(a => a.innerText.trim().startsWith('Stock changes'))`));
  await b.goto('/stockchanges'); await sleep(2000);
  await b.key('n', 1); await sleep(1800);
  expect('Alt+N opens a new stock change', (await b.path()).toLowerCase(), '/stockchange');
  truthy('cursor in Type', (await b.active()).includes('stockchange-type'));
  truthy('the new reason is offered', (await b.eval(`[...document.querySelectorAll('#stockchange-reason option')].map(o => o.value)`)).includes('Rat damage'));
  await choose('#stockchange-reason', 'Rat damage'); await sleep(300);
  await b.key('s', 2); await sleep(3000);
  truthy('Ctrl+S saves it (voucher given)', Number(await b.eval(`document.querySelector('#voucher').value`)) > 0);
  truthy('cursor goes to the line product box', (await b.active()).includes('focus-line-product'));
  expect('the type is fixed once saved', await b.eval(`document.querySelector('#stockchange-type').disabled`), true);
  await addLine(product, 2);
  expect('a line added', await b.eval(`document.querySelector('#totalquantity').value`), '2');
  await b.shot(`${shots}stock-change.png`);
  await b.goto('/damage'); await sleep(2500);
  truthy('the damage report lists it as damaged in the shop', await b.eval(`[...document.querySelectorAll('.damage-report-table tbody tr')].some(r => r.innerText.includes('${product}') && r.innerText.includes('Damaged in the shop') && r.innerText.includes('Rat damage'))`));

  await newEntry('CountCorrection');
  await addLine(product, -1);
  expect('a count correction of -1 is shown as -1', await b.eval(`document.querySelector('table.lines-table tbody tr td.c-qty').innerText.trim()`), '-1');

  log('--- approval');
  expect('Admin switches approval on', await setApproval(true), true);
  truthy('"Stock changes now need approval."', await snackHas('now need approval'));
  await newEntry('WrittenOff');
  await addLine(product, 1);
  truthy('the entry shows "Waiting for approval"', (await b.eval(`document.querySelector('#approval-banner')?.innerText || ''`)).includes('Waiting for approval'));
  await b.shot(`${shots}stock-change-waiting.png`);
  truthy('menu has Waiting for approval', await b.eval(`[...document.querySelectorAll('.mud-nav-link')].some(a => a.innerText.trim().startsWith('Waiting for approval'))`));
  await b.goto('/approvals'); await sleep(2500);
  const rows = await b.eval(`document.querySelectorAll('.approval-row').length`);
  truthy('it is on the list', rows >= 1 && (await b.eval(`document.querySelector('.approvals-table').innerText`)).includes('out of stock'));
  await b.shot(`${shots}approvals.png`);
  await b.eval(`document.querySelector('.approval-row .approval-approve').click()`); await sleep(2000);
  truthy('Approve: "approved: its stock has moved."', await snackHas('its stock has moved'));
  expect('one fewer waiting', await b.eval(`document.querySelectorAll('.approval-row').length`), rows - 1);

  expect('approval switched off again', await setApproval(false), false);
} catch (e) {
  failures++;
  log('ERROR', e.message);
  await b.shot(`${shots}stock-approval-error.png`);
} finally {
  b.close();
}

log(failures === 0 ? '\nALL STOCK CHANGE AND APPROVAL CHECKS PASS' : `\n${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
