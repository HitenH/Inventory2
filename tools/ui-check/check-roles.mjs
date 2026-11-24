// Version 3.0, batch 1b-3: the Roles screen in a real browser (Settings -> Roles, add, edit, delete, and the safety rules:
// the Admin role cannot be changed, a role that people hold cannot be deleted).
// Development only. Never point this at the real data - it adds roles and a user. Logs in as Admin.
import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const b = await launch();
let failures = 0;

function expect(what, actual, wanted) {
  const ok = actual === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${actual}${ok ? '' : `  (expected ${wanted})`}`);
}
function expectIncludes(what, text, part, wanted = true) {
  const ok = String(text).includes(part) === wanted;
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(text)}${ok ? '' : `  (expected ${wanted ? '' : 'NOT '}to contain ${JSON.stringify(part)})`}`);
}

const setNth = (selector, n, value) => b.eval(`(() => {
  const i = document.querySelectorAll(${JSON.stringify(selector)})[${n}];
  if (!i) throw new Error('no box ${selector} #${n}');
  const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
  set.call(i, ${JSON.stringify(value)});
  i.dispatchEvent(new Event('input', { bubbles: true }));
  i.dispatchEvent(new Event('change', { bubbles: true }));
})()`);
const rowText = name => b.eval(`(() => { const r = [...document.querySelectorAll('.mud-table tbody tr')].find(r => r.querySelector('td')?.innerText.trim() === ${JSON.stringify(name)}); return r ? r.innerText.replace(/\\s+/g, ' ') : ''; })()`);
const hasButtonInRow = (name, cls) => b.eval(`(() => { const r = [...document.querySelectorAll('.mud-table tbody tr')].find(r => r.querySelector('td')?.innerText.trim() === ${JSON.stringify(name)}); return !!(r && r.querySelector(${JSON.stringify('.' + cls)})); })()`);
const clickInRow = (name, cls) => b.eval(`(() => {
  const r = [...document.querySelectorAll('.mud-table tbody tr')].find(r => r.querySelector('td')?.innerText.trim() === ${JSON.stringify(name)});
  const btn = r && r.querySelector(${JSON.stringify('.' + cls)});
  if (!btn) throw new Error('no ${cls} button for ${name}');
  btn.click();
})()`);
const dialogError = () => b.eval(`document.querySelector('.mud-dialog #role-dialog-error')?.innerText ?? ''`);
const dialogOpen = () => b.eval(`!!document.querySelector('.mud-dialog')`);
const tick = label => b.eval(`(() => {
  const l = [...document.querySelectorAll('.mud-dialog label.mud-checkbox')].find(l => l.innerText.trim() === ${JSON.stringify(label)});
  if (!l) throw new Error('no tick box ${label}');
  l.querySelector('input[type=checkbox]').click();
})()`);
const confirmDelete = async () => {
  await b.waitFor(`[...document.querySelectorAll('.mud-dialog button')].some(x => x.innerText.trim() === 'Delete')`);
  await sleep(400);
  await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim() === 'Delete').click()`);
  await sleep(1500);
};
const openAdd = async () => {
  await b.click('#role-add-open');
  await b.waitFor(`document.querySelector('.mud-dialog #role-name')`);
  await sleep(500);
};

try {
  await login(b);

  log('--- open Roles from Settings');
  await b.goto('/settings'); await sleep(1500);
  await b.waitFor(`document.querySelector('#settings-roles')`);
  await b.click('#settings-roles');
  await b.waitFor(`location.pathname === '/roles'`);
  await b.waitFor(`document.querySelector('#role-add-open')`);
  expect('opened /roles from Settings', await b.path(), '/roles');
  expectIncludes('Admin shows everything', await rowText('Admin'), 'Everything (cannot be changed)');
  expect('Admin has no Edit button', await hasButtonInRow('Admin', 'role-edit'), false);
  expect('Admin has no Delete button', await hasButtonInRow('Admin', 'role-delete'), false);

  log('--- add role: refusals, then a good one');
  await openAdd();
  await b.click('#role-save'); await sleep(1200);
  expectIncludes('empty name refused', await dialogError(), 'Role name is required');
  await setNth('.mud-dialog #role-name', 0, 'admin');
  await sleep(300);
  await b.click('#role-save'); await sleep(1200);
  expectIncludes('duplicate name (different case) refused', await dialogError(), 'already exists');
  await setNth('.mud-dialog #role-name', 0, 'UiCounter');
  await tick('Open the Sales screen');
  await tick('Open the Customers screen');
  await sleep(300);
  await b.click('#role-save'); await sleep(2000);
  expect('dialog closed after adding', await dialogOpen(), false);
  expectIncludes('Snackbar says role added', await b.snack(), 'UiCounter added');
  expectIncludes('row lists the Sales switch', await rowText('UiCounter'), 'Open the Sales screen');
  expectIncludes('row lists the Customers switch', await rowText('UiCounter'), 'Open the Customers screen');

  log('--- edit role');
  await clickInRow('UiCounter', 'role-edit');
  await b.waitFor(`document.querySelector('.mud-dialog #role-name')`);
  await sleep(500);
  await tick('Open the Customers screen');
  await sleep(300);
  await b.click('#role-save'); await sleep(2000);
  expectIncludes('Customers switch removed', await rowText('UiCounter'), 'Open the Customers screen', false);
  expectIncludes('Sales switch kept', await rowText('UiCounter'), 'Open the Sales screen');

  log('--- a role somebody holds cannot be deleted');
  await b.goto('/users');
  await b.waitFor(`document.querySelector('#user-add-open')`);
  await b.click('#user-add-open');
  await b.waitFor(`document.querySelector('.mud-dialog input')`);
  await sleep(500);
  await setNth('.mud-dialog input[type=text]', 0, 'UiRoleUser');
  await setNth('.mud-dialog input[type=password]', 0, 'LongEnough1');
  await tick('UiCounter');
  await sleep(300);
  await b.click('#user-save'); await sleep(2000);
  expect('user with the role added', await dialogOpen(), false);

  await b.goto('/roles');
  await b.waitFor(`document.querySelector('#role-add-open')`);
  await sleep(800);
  expectIncludes('role shows 1 user', await rowText('UiCounter'), 'UiCounter 1');
  await clickInRow('UiCounter', 'role-delete');
  await confirmDelete();
  expectIncludes('delete refused while held', await b.snack(), '1 user holds this role');
  expectIncludes('role still listed', await rowText('UiCounter'), 'UiCounter');

  log('--- an unused role is deleted');
  await openAdd();
  await setNth('.mud-dialog #role-name', 0, 'UiSpare');
  await sleep(300);
  await b.click('#role-save'); await sleep(2000);
  expectIncludes('empty role says Nothing yet', await rowText('UiSpare'), 'Nothing yet');
  await clickInRow('UiSpare', 'role-delete');
  await confirmDelete();
  expectIncludes('Snackbar says deleted', await b.snack(), 'UiSpare deleted');
  expect('UiSpare gone from the list', await rowText('UiSpare'), '');

  log(failures === 0 ? '\nALL ROLE CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`);
} catch (e) {
  log('ERROR', e.message);
  await b.shot((process.env.SCRATCH || '.') + '\\roles-error.png');
  failures++;
} finally {
  b.close();
}

process.exit(failures === 0 ? 0 : 1);
