// Version 3.0, batch 1b-2: the Users screen in a real browser (Settings -> Users, add user, roles with an end date,
// password reset, disable / enable, and the "last Admin" safety rules).
// Development only. Never point this at the real data - it adds and disables users.
// The test copy has two users, Hiten and Admin, both holding the Admin role; this script logs in as Admin.
import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const b = await launch();
let failures = 0;

function expect(what, actual, wanted) {
  const ok = actual === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${actual}${ok ? '' : `  (expected ${wanted})`}`);
}
function expectIncludes(what, text, part) {
  const ok = String(text).includes(part);
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(text)}${ok ? '' : `  (expected to contain ${JSON.stringify(part)})`}`);
}

// MudBlazor boxes are set the way typing arrives: value, then input and change events.
const setNth = (selector, n, value) => b.eval(`(() => {
  const i = document.querySelectorAll(${JSON.stringify(selector)})[${n}];
  if (!i) throw new Error('no box ${selector} #${n}');
  const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
  set.call(i, ${JSON.stringify(value)});
  i.dispatchEvent(new Event('input', { bubbles: true }));
  i.dispatchEvent(new Event('change', { bubbles: true }));
})()`);

const tableText = () => b.eval(`document.querySelector('.mud-table')?.innerText ?? ''`);
const rowText = name => b.eval(`(() => { const r = [...document.querySelectorAll('.mud-table tbody tr')].find(r => r.querySelector('td')?.innerText.trim() === ${JSON.stringify(name)}); return r ? r.innerText.replace(/\\s+/g, ' ') : ''; })()`);
const clickInRow = (name, cls) => b.eval(`(() => {
  const r = [...document.querySelectorAll('.mud-table tbody tr')].find(r => r.querySelector('td')?.innerText.trim() === ${JSON.stringify(name)});
  const btn = r && r.querySelector(${JSON.stringify('.' + cls)});
  if (!btn) throw new Error('no ${cls} button for ${name}');
  btn.click();
})()`);
const dialogError = () => b.eval(`document.querySelector('.mud-dialog #user-dialog-error')?.innerText ?? ''`);
const dialogOpen = () => b.eval(`!!document.querySelector('.mud-dialog')`);
const tick = label => b.eval(`(() => {
  const l = [...document.querySelectorAll('.mud-dialog label.mud-checkbox')].find(l => l.innerText.trim() === ${JSON.stringify(label)});
  if (!l) throw new Error('no tick box ${label}');
  l.querySelector('input[type=checkbox]').click();
})()`);

try {
  await login(b);

  log('--- open Users from Settings');
  await b.goto('/settings'); await sleep(1500);
  await b.waitFor(`document.querySelector('#settings-users')`);
  await b.click('#settings-users');
  await b.waitFor(`location.pathname === '/users'`);
  await b.waitFor(`document.querySelector('#user-add-open')`);
  expect('opened /users from Settings', await b.path(), '/users');
  expectIncludes('list shows Admin with its role', await rowText('Admin'), 'Admin Admin Active');

  log('--- add user: refused short password and duplicate name');
  await b.click('#user-add-open');
  await b.waitFor(`document.querySelector('.mud-dialog input')`);
  await sleep(500);
  await setNth('.mud-dialog input[type=text]', 0, 'UiUser1');
  await setNth('.mud-dialog input[type=password]', 0, 'short');
  await sleep(300);
  await b.click('#user-save'); await sleep(1200);
  expectIncludes('short password refused', await dialogError(), 'at least 8');
  expect('dialog stays open after a refusal', await dialogOpen(), true);

  await setNth('.mud-dialog input[type=text]', 0, 'admin');
  await setNth('.mud-dialog input[type=password]', 0, 'LongEnough1');
  await sleep(300);
  await b.click('#user-save'); await sleep(1200);
  expectIncludes('duplicate name (different case) refused', await dialogError(), 'already exists');

  log('--- add user with a temporary role');
  await setNth('.mud-dialog input[type=text]', 0, 'UiUser1');
  await tick('Admin');
  await b.waitFor(`document.querySelector('.mud-dialog .mud-picker input')`);
  await sleep(300);
  await setNth('.mud-dialog .mud-picker input', 0, '31-12-2026');
  await b.eval(`document.querySelector('.mud-dialog .mud-picker input').blur()`);
  await sleep(500);
  await b.click('#user-save'); await sleep(2000);
  expect('dialog closed after adding', await dialogOpen(), false);
  expectIncludes('Snackbar says user added', await b.snack(), 'UiUser1 added');
  expectIncludes('new user shows temporary role', await rowText('UiUser1'), 'Admin (to 31 Dec 2026) Active');

  log('--- reset password');
  await clickInRow('UiUser1', 'user-password');
  await b.waitFor(`document.querySelector('.mud-dialog input[type=password]')`);
  await sleep(500);
  await setNth('.mud-dialog input[type=password]', 0, 'abc');
  await b.click('#user-save'); await sleep(1200);
  expectIncludes('short new password refused', await dialogError(), 'at least 8');
  await setNth('.mud-dialog input[type=password]', 0, 'BrandNew#2026');
  await b.click('#user-save'); await sleep(1500);
  expect('password dialog closed', await dialogOpen(), false);
  expectIncludes('Snackbar says password changed', await b.snack(), 'Password changed for UiUser1');

  log('--- disable and enable');
  await clickInRow('UiUser1', 'user-disable');
  await b.waitFor(`document.querySelector('.mud-dialog')`);
  await sleep(500);
  await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim() === 'Disable').click()`);
  await sleep(1500);
  expectIncludes('user shows Disabled', await rowText('UiUser1'), 'Disabled');
  await clickInRow('UiUser1', 'user-enable'); await sleep(1500);
  expectIncludes('user shows Active again', await rowText('UiUser1'), 'Active');

  log('--- last Admin safety rules (as Admin, with Hiten and UiUser1 disabled)');
  for (const name of ['Hiten', 'UiUser1']) {
    await clickInRow(name, 'user-disable');
    await b.waitFor(`document.querySelector('.mud-dialog')`);
    await sleep(500);
    await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim() === 'Disable').click()`);
    await sleep(1500);
    expectIncludes(`${name} disabled (another Admin remains)`, await rowText(name), 'Disabled');
  }
  await clickInRow('Admin', 'user-disable');
  await b.waitFor(`document.querySelector('.mud-dialog')`);
  await sleep(500);
  await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim() === 'Disable').click()`);
  await sleep(1500);
  expectIncludes('cannot disable yourself', await b.snack(), 'cannot disable yourself');
  expectIncludes('Admin still Active', await rowText('Admin'), 'Active');

  await clickInRow('Admin', 'user-roles');
  await b.waitFor(`document.querySelector('.mud-dialog label.mud-checkbox')`);
  await sleep(500);
  await tick('Admin'); await sleep(300);
  await b.click('#user-save'); await sleep(1500);
  expectIncludes('last Admin cannot lose the Admin role', await dialogError(), 'last active Admin');
  expectIncludes('Admin still holds the role', (await b.eval(`document.querySelector('.mud-dialog')?.innerText ?? ''`)), 'Admin');
  await b.key('Escape'); await sleep(800);
  expectIncludes('Admin row unchanged', await rowText('Admin'), 'Admin Admin Active');

  log(failures === 0 ? '\nALL USER CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`);
} catch (e) {
  log('ERROR', e.message);
  await b.shot((process.env.SCRATCH || '.') + '\\users-error.png');
  failures++;
} finally {
  b.close();
}

process.exit(failures === 0 ? 0 : 1);
