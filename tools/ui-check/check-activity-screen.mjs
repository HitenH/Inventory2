// Version 3.0, batch 1c-5: the Activity log screen (menu item, filters, colours, links, rate hiding, erase).
// Development only. Never point this at the real data - it changes a customer and ERASES the test copy's activity log.
//
// Test copy setup: the same as check-activity-log.mjs (python tools/ui-check/setup-activity-log.py <db>, then start
// the test copy), and pass the customer id it prints: $env:LOG_CUSTOMER='<customer id>'.
// Screenshots (light and dark) go to <SCRATCH>.
import { launch, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const customerId = process.env.LOG_CUSTOMER;
if (!customerId) { console.error('Set LOG_CUSTOMER (see the top of this file).'); process.exit(2); }
const shots = process.env.SCRATCH || '.';

let failures = 0;
function expect(what, actual, wanted) {
  const ok = actual === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}
function expectTrue(what, ok, detail = '') {
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}${ok ? '' : `  ${detail}`}`);
}

async function loginAs(b, name) {
  await b.eval('(() => { try { sessionStorage.clear(); } catch { } })()');   // log out (nothing to clear on the first, blank page)
  await b.goto('/login');
  await b.waitFor(`document.querySelector('#login-name')`);
  await b.click('#login-name'); await b.type(name);
  await b.click('#login-password'); await b.type('UiTest#2026');
  await b.key('Enter');
  await b.waitFor(`location.pathname === '/'`);
  await sleep(1000);
}

const setBox = (b, selector, value) => b.eval(`(() => {
  const i = document.querySelector(${JSON.stringify(selector)});
  const set = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(i), 'value').set;
  set.call(i, ${JSON.stringify(value)});
  i.dispatchEvent(new Event('input', { bubbles: true }));
  i.dispatchEvent(new Event('change', { bubbles: true }));
})()`);
const rowCount = b => b.eval(`document.querySelectorAll('#activity-table tbody tr.act-row').length`);
const rowsText = b => b.eval(`[...document.querySelectorAll('#activity-table tbody tr.act-row')].map(r => r.innerText.replace(/\\s+/g, ' ')).join(' | ')`);
const menuHasLog = b => b.eval(`[...document.querySelectorAll('.mud-nav-link')].some(a => a.innerText.trim() === 'Activity log')`);
async function openActivity(b) {
  await b.eval(`[...document.querySelectorAll('.mud-nav-link')].find(a => a.innerText.trim() === 'Activity log').click()`);
  await b.waitFor(`location.pathname === '/activity' && document.querySelector('#activity-table')`, 10000);
  await sleep(800);
}
async function chooseKind(b, label) {
  await b.eval(`(() => { const e = document.querySelector('#activity-kind'); (e.closest('.mud-select') ?? e).setAttribute('data-kind-box', '1'); })()`);
  await b.click('[data-kind-box] .mud-input-slot, [data-kind-box] .mud-input');
  await b.waitFor(`document.querySelector('.mud-popover-open .mud-list-item')`);
  await b.eval(`[...document.querySelectorAll('.mud-popover-open .mud-list-item')].find(i => i.innerText.trim() === ${JSON.stringify(label)}).click()`);
  await sleep(1200);
}

const b = await launch(9333);
try {
  await loginAs(b, 'Admin');

  log('--- make something to see: change a customer\'s area');
  await b.goto(`/customer/${customerId}`);
  await b.waitFor(`document.querySelector('#area')`, 10000); await sleep(800);
  const oldArea = await b.eval(`document.querySelector('#area').value`);
  const newArea = oldArea === 'SCREEN CHECK' ? 'SCREEN CHECK 2' : 'SCREEN CHECK';
  await setBox(b, '#area', newArea);
  await b.click('#button-submit'); await sleep(1500);

  log('--- Admin: menu item, rows, colours');
  expect('menu shows "Activity log"', await menuHasLog(b), true);
  await openActivity(b);
  expectTrue('rows are listed', (await rowCount(b)) >= 2, `rows: ${await rowCount(b)}`);
  const text = await rowsText(b);
  expectTrue('the customer change is there, before → after', text.includes(`Area: ${oldArea || '(blank)'} → ${newArea}`), text.slice(0, 300));
  expectTrue('logins are there', text.includes('Logged in'));
  // Hiten, 25 Nov 2026: the kind of record is the colour of its name (no stripe).
  const nameColour = await b.eval(`getComputedStyle(document.querySelector('#activity-table .act-rec-party')).color`);
  const plainColour = await b.eval(`getComputedStyle(document.querySelector('#activity-table tbody td:last-child')).color`);
  expectTrue('a customer name is written in its own colour', nameColour !== plainColour, `${nameColour} / ${plainColour}`);
  expect('no stripe on the left any more', await b.eval(`getComputedStyle(document.querySelector('#activity-table tr.act-row td')).borderLeftWidth`) === '4px', false);
  const pill = await b.eval(`getComputedStyle(document.querySelector('#activity-table .act-person:not([data-plain])')).backgroundColor`);
  expectTrue('a person has a tinted pill', !/rgba\(0, 0, 0, 0\)/.test(pill), pill);
  expect('Admin sees "Erase whole log"', await b.eval(`!!document.querySelector('#activity-erase')`), true);
  expect('no sideways scroll', await b.eval(`document.documentElement.scrollWidth <= document.documentElement.clientWidth`), true);
  await b.shot(`${shots}/activity-screen-light.png`);

  log('--- filters');
  await chooseKind(b, 'Customers');
  const kinds = await b.eval(`[...document.querySelectorAll('#activity-table tbody tr.act-row')].map(r => [...r.classList].find(c => c.startsWith('act-kind-')))`);
  expectTrue('Kind = Customers shows only customer rows', kinds.length > 0 && kinds.every(k => k === 'act-kind-party'), JSON.stringify(kinds));
  await chooseKind(b, 'Everything');
  await b.click('#activity-search'); await b.type(newArea); await sleep(1500);
  const found = await rowsText(b);
  expectTrue('Search finds the change', (await rowCount(b)) >= 1 && found.includes(newArea), found.slice(0, 200));

  log('--- the record link opens the customer');
  await b.eval(`document.querySelector('#activity-table .act-record-link').click()`);
  await b.waitFor(`location.pathname.startsWith('/customer/')`, 8000);
  expect('link opened the customer', (await b.path()).toLowerCase(), `/customer/${customerId}`.toLowerCase());

  log('--- dark mode');
  await openActivity(b);
  await b.eval(`document.querySelector('.mud-appbar .mud-switch input').click()`); await sleep(1000);
  const darkPill = await b.eval(`getComputedStyle(document.querySelector('#activity-table .act-person:not([data-plain])')).backgroundColor`);
  expectTrue('pill is darker in dark mode', darkPill !== pill, `${pill} / ${darkPill}`);
  await b.shot(`${shots}/activity-screen-dark.png`);
  await b.eval(`document.querySelector('.mud-appbar .mud-switch input').click()`); await sleep(800);

  log('--- a viewer without rates: menu item, no erase, no rate lines');
  await loginAs(b, 'UiLogViewer');
  expect('viewer menu shows "Activity log"', await menuHasLog(b), true);
  await openActivity(b);
  expect('viewer has no "Erase whole log"', await b.eval(`!!document.querySelector('#activity-erase')`), false);
  expectTrue('viewer sees no rates or discounts', !/Discount|Rate /.test(await rowsText(b)), (await rowsText(b)).slice(0, 300));

  log('--- a user without the switch: no menu item, address refused');
  await loginAs(b, 'UiNoLog');
  expect('no "Activity log" in the menu', await menuHasLog(b), false);
  await b.goto('/activity'); await sleep(2000);
  expect('typed address is refused', await b.eval(`!!document.querySelector('#access-denied')`), true);

  log('--- Admin erases the whole log');
  await loginAs(b, 'Admin');
  await openActivity(b);
  await b.click('#activity-erase');
  await b.waitFor(`document.querySelector('.mud-dialog')`);
  const question = await b.eval(`document.querySelector('.mud-dialog').innerText`);
  expectTrue('it asks first, with the number of entries', /Erase the whole activity log\? All \d+ entr/.test(question), question);
  await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim() === 'Erase').click()`);
  await sleep(2000);
  expect('nothing is left', await rowCount(b), 0);
  expect('and it says so', await b.eval(`!!document.querySelector('#activity-none')`), true);
} catch (e) {
  failures++;
  log('FAIL (stopped):', e.message);
} finally {
  b.close();
}

log(failures ? `\n${failures} problem(s)` : '\nall checks passed');
process.exit(failures ? 1 : 0);
