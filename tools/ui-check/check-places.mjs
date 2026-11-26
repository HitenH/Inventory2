// Version 3.0, stage 3, batches 3-1/3-2: Settings → Places and racks (Shop and Godown made by the upgrade; add a place with
// Enter, add racks in a range, change a rack's levels, delete refused while a rack holds items, delete an empty place, Esc
// back to Settings), and on a product page the Racks column and the "Where … is kept" pop-up: an address typed as 0203B
// with Enter, the same picked from the drop-downs, a wrong address explained, ★ main rack per place, ✕ takes a rack off,
// "Same for all variants". Setup: a test copy with the Admin test password; PRODUCT_ID = a product with two or more
// variants (the run prints one). It CHANGES the test copy.
import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const shots = process.env.SCRATCH || '.';
const productId = process.env.PRODUCT_ID;
let failures = 0;
function expect(what, actual, wanted) {
  const ok = JSON.stringify(actual) === JSON.stringify(wanted);
  if (!ok) { failures++; }
  log(`${ok ? 'ok  ' : 'FAIL'} ${what}: ${JSON.stringify(actual)}${ok ? '' : `  (expected ${JSON.stringify(wanted)})`}`);
}
function truthy(what, actual) { expect(what, !!actual, true); }

const b = await launch(9351);
const snackHas = async (text) => { for (let i = 0; i < 20; i++) { if ((await b.snack()).includes(text)) return true; await sleep(250); } return false; };
const choose = (sel, value) => b.eval(`(() => { const s = document.querySelector(${JSON.stringify(sel)}); s.value = ${JSON.stringify(String(value))}; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
const setBox = (sel, value) => b.eval(`(() => { const i = document.querySelector(${JSON.stringify(sel)}); i.value = ${JSON.stringify(String(value))}; i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true })); })()`);
const placeNames = () => b.eval(`[...document.querySelectorAll('.place-row')].map(r => r.querySelector('.place-number').value + ' ' + r.querySelector('.place-name').value)`);
const rackRows = () => b.eval(`[...document.querySelectorAll('.rack-row')].map(r => r.querySelector('.c-rack-no').innerText.trim() + ' ' + r.querySelector('.rack-levels').selectedOptions[0].text)`);
const showRacksOf = (name) => b.eval(`[...document.querySelectorAll('.place-row')].find(r => r.querySelector('.place-name').value === ${JSON.stringify(name)}).querySelector('.place-show-racks').click()`);
async function confirmDialog(button) {
  await b.waitFor(`[...document.querySelectorAll('.mud-dialog button')].some(x => x.innerText.trim() === ${JSON.stringify(button)})`, 5000);
  await b.eval(`[...document.querySelectorAll('.mud-dialog button')].find(x => x.innerText.trim() === ${JSON.stringify(button)}).click()`);
  await sleep(1500);
}
async function addRacks(from, to, top) {
  await setBox('#rack-from', from); await setBox('#rack-to', to); await choose('#rack-top', top); await sleep(300);
  await b.eval(`document.querySelector('#rack-add').click()`); await sleep(1500);
}
const chips = () => b.eval(`[...document.querySelectorAll('.variant-racks-dialog .vr-row')].map(c => (c.classList.contains('vr-main') ? '*' : '') + c.querySelector('.vr-address').innerText.trim())`);
const addressBox = `.variant-racks-dialog .address-text`;
async function typeAddress(text) {
  await b.eval(`(() => { const i = document.querySelector('${addressBox}'); i.focus(); i.select(); })()`);
  await b.type(text); await b.key('Enter'); await sleep(1500);
}

try {
  await login(b);

  log('--- Settings → Places and racks');
  await b.goto('/settings'); await b.waitFor(`!!document.querySelector('#settings-places')`, 10000); await sleep(800);
  await b.eval(`document.querySelector('#settings-places').click()`); await sleep(2500);
  expect('Settings → Places and racks', (await b.path()).toLowerCase(), '/places');
  expect('the upgrade made the shop and the godown', await placeNames(), ['01 Shop', '02 Godown']);
  truthy('cursor in "New place"', (await b.active()).includes('place-new'));
  await b.type('Store room'); await b.key('Enter'); await sleep(1500);
  expect('Enter adds a place with the next number', await placeNames(), ['01 Shop', '02 Godown', '03 Store room']);

  await showRacksOf('Godown'); await sleep(800);
  truthy('godown racks card', (await b.eval(`document.querySelector('.racks-title')?.innerText || ''`)).includes('Godown (02)'));
  await addRacks(1, 5, 5);
  truthy('"5 racks added."', await snackHas('5 racks added'));
  expect('racks 02-01 … 02-05 with levels A to E', await rackRows(), ['02-01 E', '02-02 E', '02-03 E', '02-04 E', '02-05 E']);
  expect('the next free number is suggested', await b.eval(`document.querySelector('#rack-from').value`), '6');
  await b.eval(`(() => { const s = [...document.querySelectorAll('.rack-row')][4].querySelector('.rack-levels'); s.value = '3'; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(1500);
  expect('rack 02-05 now has levels A to C', (await rackRows())[4], '02-05 C');
  await showRacksOf('Shop'); await sleep(800);
  await addRacks(1, 2, 4);
  expect('shop racks', await rackRows(), ['01-01 D', '01-02 D']);
  await b.shot(`${shots}places.png`);

  log('--- deleting');
  await b.eval(`[...document.querySelectorAll('.place-row')].find(r => r.querySelector('.place-name').value === 'Store room').querySelector('.place-delete').click()`);
  await confirmDialog('Delete');
  expect('an empty place can be deleted', await placeNames(), ['01 Shop', '02 Godown']);
  expect('the shop has no delete button', await b.eval(`!![...document.querySelectorAll('.place-row')][0].querySelector('.place-delete')`), false);
  expect('the number of a place with racks is locked', await b.eval(`[...document.querySelectorAll('.place-number')].map(i => i.disabled)`), [true, true]);

  await b.key('Escape'); await sleep(1500);
  expect('Esc goes back to Settings', (await b.path()).toLowerCase(), '/settings');

  if (!productId) {
    throw new Error('PRODUCT_ID not set');
  }

  log('--- the product page: where each variant is kept');
  await b.goto(`/product/${productId}`); await b.waitFor(`!!document.querySelector('tr.variant-line')`, 10000); await sleep(1000);
  expect('no Racks column (Hiten: do not crowd the page)', await b.eval(`[...document.querySelectorAll('th')].some(t => t.innerText.trim().toUpperCase() === 'RACKS')`), false);
  expect('no icons beside the reorder level', await b.eval(`!!document.querySelector('tr.variant-line .variant-labels-link, tr.variant-line .variant-racks-open')`), false);
  // Hiten, 26 Nov 2026: clicking a variant line opens a strip with named buttons. The line is on the page a moment
  // before it answers, so click again until the strip opens.
  for (let i = 0; i < 6 && !(await b.eval(`!!document.querySelector('td.variant-more')`)); i++) {
    await b.eval(`document.querySelector('tr.variant-line td.c-vname-p').click()`); await sleep(1500);
  }
  expect('the strip has the named buttons', await b.eval(`[...document.querySelectorAll('td.variant-more .mud-button-root')].map(x => x.innerText.trim())`), ['Print labels', 'Where it is kept']);
  expect('…and says the variant is on no rack yet', await b.eval(`document.querySelector('td.variant-more .variant-more-racks').innerText.trim()`), 'Not on any rack yet.');
  await b.eval(`document.querySelector('td.variant-more .variant-racks-open').click()`); await sleep(1500);
  await b.waitFor(`!!document.querySelector('.variant-racks-dialog')`, 5000); await sleep(1200);
  truthy('cursor in the address box', await b.eval(`document.activeElement?.classList.contains('address-text')`));
  await typeAddress('0203B');
  expect('0203B + Enter adds 02-03-B as the godown main rack', await chips(), ['*02-03-B']);
  truthy('the box is empty again', (await b.eval(`document.querySelector('${addressBox}').value`)) === '');

  await typeAddress('2-9-a');
  expect('a rack that does not exist is explained', await b.eval(`document.querySelector('.address-problem')?.innerText || ''`), 'Godown has no rack 09.');
  await typeAddress('0205E');
  expect('a level that does not exist is explained', await b.eval(`document.querySelector('.address-problem')?.innerText || ''`), 'Rack 02-05 has levels A–C.');
  await typeAddress('03-01-A');
  expect('a place that does not exist is explained', await b.eval(`document.querySelector('.address-problem')?.innerText || ''`), 'There is no place 03.');
  await typeAddress('02-05-c');
  expect('02-05-c (dashes, small letter) is added as a second godown rack', await chips(), ['*02-03-B', '02-05-C']);

  await choose('.variant-racks-dialog .address-place', 1); await sleep(400);
  await choose('.variant-racks-dialog .address-rack', await b.eval(`[...document.querySelectorAll('.variant-racks-dialog .address-rack option')].find(o => o.text === '01').value`)); await sleep(400);
  await choose('.variant-racks-dialog .address-level', 'B'); await sleep(400);
  expect('the drop-downs write the address into the box', await b.eval(`document.querySelector('${addressBox}').value`), '01-01-B');
  expect('…and say it in full words', await b.eval(`document.querySelector('.vr-choice')?.innerText.trim()`), '01-01-B = Shop, Rack 1, Level B');
  await b.eval(`document.querySelector('#variant-rack-add').click()`); await sleep(1500);
  expect('Place / Rack / Level + Add: the shop main rack', await chips(), ['*01-01-B', '*02-03-B', '02-05-C']);
  expect('each rack in full words', await b.eval(`[...document.querySelectorAll('.variant-racks-dialog .vr-description')].map(d => d.innerText.trim())`),
    ['Rack 1, Level B', 'Rack 3, Level B', 'Rack 5, Level C']);
  expect('…in a section per place', await b.eval(`[...document.querySelectorAll('.variant-racks-dialog .vr-place-title')].map(t => t.innerText.trim())`), ['Shop (01)', 'Godown (02)']);
  await b.shot(`${shots}variant-racks.png`);

  await b.eval(`[...document.querySelectorAll('.variant-racks-dialog .vr-row')].find(c => c.innerText.includes('02-05-C')).querySelector('.vr-make-main').click()`); await sleep(1500);
  expect('☆ makes 02-05-C the godown main rack', await chips(), ['*01-01-B', '*02-05-C', '02-03-B']);
  truthy('the ✕ is red', await b.eval(`(() => { const [r, g, bl] = getComputedStyle(document.querySelector('.vr-remove')).color.match(/\\d+/g).map(Number); return r > 150 && g < 110 && bl < 110; })()`));
  await b.eval(`[...document.querySelectorAll('.variant-racks-dialog .vr-row')].find(c => c.innerText.includes('02-05-C')).querySelector('.vr-remove').click()`); await sleep(1500);
  expect('✕ takes the main rack off; the next one becomes main', await chips(), ['*01-01-B', '*02-03-B']);

  truthy('"Same for all variants" offered', await b.eval(`!!document.querySelector('#variant-racks-copy')`));
  await b.eval(`document.querySelector('#variant-racks-copy').click()`);
  await confirmDialog('Copy');
  truthy('"Racks copied to …"', await snackHas('Racks copied to'));
  await b.key('Escape'); await sleep(1500);
  expect('Esc closes the pop-up and stays on the product', (await b.path()).toLowerCase(), `/product/${productId}`.toLowerCase());
  const strip = () => b.eval(`[...document.querySelectorAll('td.variant-more .variant-more-rack')].map(x => x.innerText.replace(/\\s+/g, ' ').trim())`);
  expect('the open strip lists the racks in words, place by place', await b.eval(`[...document.querySelectorAll('td.variant-more .variant-more-place')].map(x => x.innerText.replace(/\\s+/g, ' ').trim())`),
    ['Shop: ★ 01-01-B Rack 1, Level B', 'Godown: ★ 02-03-B Rack 3, Level B']);
  await b.eval(`[...document.querySelectorAll('tr.variant-line td.c-vname-p')][1].click()`); await sleep(1500);
  expect('another line opens (the first closes) with the copied racks', [await b.eval(`document.querySelectorAll('td.variant-more').length`), await strip()],
    [1, ['★ 01-01-B Rack 1, Level B', '★ 02-03-B Rack 3, Level B']]);
  await b.shot(`${shots}product-racks.png`);

  log('--- a rack that holds items stays');
  await b.goto('/places'); await sleep(2000);
  await showRacksOf('Godown'); await sleep(800);
  truthy('rack 02-03 shows its items', Number(await b.eval(`[...document.querySelectorAll('.rack-row')][2].querySelector('.c-rack-items').innerText`)) >= 2);
  await b.eval(`[...document.querySelectorAll('.rack-row')][2].querySelector('.rack-delete').click()`);
  await confirmDialog('Delete');
  truthy('deleting it is refused', await snackHas('still holds'));
  expect('rack 02-03 is still there', (await rackRows())[2], '02-03 E');
} catch (e) {
  failures++;
  log('ERROR', e.message);
  await b.shot(`${shots}places-error.png`);
} finally {
  b.close();
}

log(failures === 0 ? '\nALL PLACES AND RACKS CHECKS PASS' : `\n${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
