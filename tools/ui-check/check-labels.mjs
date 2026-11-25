// Version 3.0, stage L, batch L-1: the Labels page (/labels). Adds labels with the real product picker, checks the list,
// the sheet count, that each label has its QR picture, that printing shows only the sheets at full A4 size with the labels
// where Hiten's Word file puts them, and saves the printed result as a PDF (<SCRATCH>labels.pdf) to look at.
// A user without the "Print labels" switch is refused.
// Development only; it changes no data. Test copy: the usual Admin password; for the refused user run
// tools/ui-check/setup-activity-log.py on the copy first (it adds UiNoLog).
import { launch, sleep } from './cdp.mjs';
import { writeFileSync } from 'node:fs';

const log = (...a) => console.log(...a);
const shots = process.env.SCRATCH || '.';
let failures = 0;
function expect(what, actual, wanted) {
  const ok = actual === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}
function near(what, actual, wanted, tolerance = 1.5) {
  const ok = Math.abs(actual - wanted) <= tolerance;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${actual.toFixed(1)} px${ok ? '' : `  (expected about ${wanted.toFixed(1)})`}`);
}

async function loginAs(b, name) {
  await b.eval('(() => { try { sessionStorage.clear(); } catch { } })()');
  await b.goto('/login');
  await b.waitFor(`document.querySelector('#login-name')`);
  await b.click('#login-name'); await b.type(name);
  await b.click('#login-password'); await b.type('UiTest#2026');
  await b.key('Enter');
  await b.waitFor(`location.pathname === '/'`);
  await sleep(1000);
}
async function addLabels(b, search, copies, allVariants = false) {
  await b.click('.focus-label-product input');
  await b.eval(`(() => { const i = document.querySelector('.focus-label-product input'); i.select(); })()`);
  await b.type(search);
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.key('ArrowDown'); await sleep(300); await b.key('Enter'); await sleep(1500);
  await b.eval(`(() => { const s = document.querySelector('#label-variant'); const o = [...s.options].find(o => ${allVariants ? "o.value === 'all'" : "o.value && o.value !== 'all'"}); s.value = o.value; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(300);
  await b.eval(`(() => { const i = document.querySelector('#label-copies'); i.value = '${copies}'; i.dispatchEvent(new Event('input', { bubbles: true })); })()`);
  await sleep(300);
  await b.click('#label-add'); await sleep(1200);
}

const mmPx = 96 / 25.4;
const b = await launch(9333);
try {
  await loginAs(b, 'Admin');
  await b.goto('/labels');
  await b.waitFor(`document.querySelector('.focus-label-product input')`, 10000); await sleep(800);
  expect('empty list says what to do', await b.eval(`!!document.querySelector('#labels-empty')`), true);

  log('--- add labels');
  await addLabels(b, 'HAATICHAP', 3);
  expect('one line listed', await b.eval(`document.querySelectorAll('#labels-list tbody tr').length`), 1);
  expect('3 labels in the preview', await b.eval(`document.querySelectorAll('#label-print .label').length`), 3);
  expect('each label has its QR picture', await b.eval(`document.querySelectorAll('#label-print .label .label-qr svg path').length`), 3);
  const first = await b.eval(`(() => { const l = document.querySelector('#label-print .label'); return [l.querySelector('.label-name').innerText, l.querySelector('.label-variant').innerText, l.querySelector('.label-id').innerText]; })()`);
  log('     first label:', JSON.stringify(first));
  expect('the label shows the product ID under the QR', first[2].length > 0, true);
  await addLabels(b, 'LAZER', 70);
  expect('73 labels need 2 sheets', await b.eval(`document.querySelector('#labels-total').innerText`), 'Total: 73 labels (2 sheets)');
  expect('2 sheets in the preview', await b.eval(`document.querySelectorAll('#label-print .label-sheet').length`), 2);
  await b.shot(`${shots}/labels-screen.png`);

  log('--- the copies of a line can be changed in the list');
  await b.eval(`(() => { const i = document.querySelectorAll('.label-copies-edit')[1]; i.value = '72'; i.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(800);
  expect('changed copies update the total', await b.eval(`document.querySelector('#labels-total').innerText`), 'Total: 75 labels (2 sheets)');
  await b.eval(`(() => { const i = document.querySelectorAll('.label-copies-edit')[1]; i.value = '70'; i.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(800);

  log('--- printing: only the sheets, full size, labels where the Word file puts them');
  await b.send('Emulation.setEmulatedMedia', { media: 'print' });
  await sleep(500);
  expect('top bar hidden on paper', await b.eval(`getComputedStyle(document.querySelector('.mud-appbar')).display`), 'none');
  expect('list and buttons hidden on paper', await b.eval(`getComputedStyle(document.querySelector('.labels-editor')).display`), 'none');
  const geo = await b.eval(`(() => {
    const s = document.querySelector('.label-sheet').getBoundingClientRect();
    const ls = [...document.querySelectorAll('.label-sheet')[0].querySelectorAll('.label:not(.label-used)')];
    const r = l => l.getBoundingClientRect();
    return { sheetW: s.width, sheetH: s.height, firstL: r(ls[0]).left - s.left, firstT: r(ls[0]).top - s.top,
             secondL: r(ls[1]).left - s.left, sixthT: r(ls[5]).top - s.top, labelW: r(ls[0]).width, qr: ls[0].querySelector('.label-qr').getBoundingClientRect().width,
             qrTop: ls[0].querySelector('.label-qr').getBoundingClientRect().top - r(ls[0]).top };
  })()`);
  near('sheet is A4 wide (210 mm)', geo.sheetW, 210 * mmPx);
  near('sheet is A4 high (297 mm)', geo.sheetH, 297 * mmPx);
  // Owner, 25 Nov 2026: a sheet fills from the bottom-right corner backwards.
  near('first label in the bottom-right corner: 164 mm from the left', geo.firstL, 164 * mmPx);
  near('... and 264 mm from the top (last row)', geo.firstT, 264 * mmPx);
  near('second label to its left (124 mm)', geo.secondL, 124 * mmPx);
  near('sixth label in the row above (243 mm)', geo.sixthT, 243 * mmPx);
  near('content moved down: QR 4.8 mm below the label top (A4 65 Label: 2.2 mm down)', geo.qrTop, 4.8 * mmPx, 1);
  near('a label is 40 mm wide', geo.labelW, 40 * mmPx);
  near('the QR is 13 mm (A4 65 Label)', geo.qr, 13 * mmPx);
  const pdf = await b.send('Page.printToPDF', { preferCSSPageSize: true, printBackground: true });
  writeFileSync(`${shots}/labels.pdf`, Buffer.from(pdf.data, 'base64'));
  log(`     printed result saved: ${shots}/labels.pdf`);
  await b.send('Emulation.setEmulatedMedia', { media: '' });

  log('--- a part-used sheet: 15 already used, printing starts above them');
  await b.eval(`(() => { const i = document.querySelector('#labels-used'); i.value = '15'; i.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(800);
  expect('15 used labels greyed in the preview', await b.eval(`document.querySelectorAll('#label-print .label-used').length`), 15);
  expect('73 labels after 15 used still fit 2 sheets', await b.eval(`document.querySelector('#labels-total').innerText`), 'Total: 73 labels (2 sheets)');
  await b.send('Emulation.setEmulatedMedia', { media: 'print' }); await sleep(400);
  const startTop = await b.eval(`(() => { const s = document.querySelector('.label-sheet').getBoundingClientRect(); return document.querySelector('.label-sheet .label:not(.label-used)').getBoundingClientRect().top - s.top; })()`);
  near('first printed label is in row 10 (201 mm)', startTop, 201 * mmPx);
  expect('used labels are not printed', await b.eval(`getComputedStyle(document.querySelector('.label-used')).display`), 'none');
  await b.send('Emulation.setEmulatedMedia', { media: '' });
  await b.eval(`(() => { const i = document.querySelector('#labels-used'); i.value = '70'; i.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  await sleep(800);
  expect('a used number that leaves nothing is refused', (await b.snack()).includes('Already used must be a whole number from 0 to 64.'), true);
  await b.shot(`${shots}/labels-used.png`);

  log('--- a user without "Print labels" is refused');
  await loginAs(b, 'UiNoLog');
  await b.goto('/labels'); await sleep(2000);
  expect('typed address is refused', await b.eval(`!!document.querySelector('#access-denied')`), true);
} catch (e) {
  failures++;
  log('FAIL (stopped):', e.message);
} finally {
  b.close();
}

log(failures ? `\n${failures} problem(s)` : '\nall checks passed');
process.exit(failures ? 1 : 0);
