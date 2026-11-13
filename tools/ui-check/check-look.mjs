// Batch 17 (UI refresh): opens each screen at 1366 and 1920 px wide, light and dark, takes a screenshot and measures
//   - a sideways scroll bar (owner's rule: everything must fit on the screen),
//   - product names wrapping to a second line in the line tables,
//   - anything sticking out of its card / entry row (overlapping text).
// Usage: $env:SCRATCH='<scratch>'; node tools\ui-check\check-look.mjs   (test copy on port 5299, see README)
// Record ids are from the May 2026 test data (a purchase with 57 lines, a sale with 165, a sales order with 2).
import { launch, login, sleep } from './cdp.mjs';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';

const SCREENS = {
  purchase: '/purchase/3285A89A-D17A-453A-AB74-0CD874DF058E',
  'purchase-new': '/purchase',
  sale: '/sale/BF00895C-D5ED-498A-9D65-F07790DF7F9C',
  'sale-new': '/sale',
  salesorder: '/salesorder/D79A3BAA-A960-48B1-AF9A-31E3E71754CF',
  products: '/products',
  customers: '/customers',
  purchases: '/purchases',
  purchaseorder: '/purchaseorder',
  salesorders: '/salesorders',
  settings: '/settings',
  reports: '/reports',
};
const WIDTHS = [1366, 1920];
const MODES = ['Light', 'Dark'];

const MEASURE = `(() => {
  const de = document.documentElement;
  const main = document.querySelector('.mud-main-content');
  const hScroll = de.scrollWidth > de.clientWidth + 1 || (main && main.scrollWidth > main.clientWidth + 1);
  // A name cell taller than 1.8 lines of its own text has wrapped (the row height itself does not matter).
  const wrapped = [...document.querySelectorAll('.lines-table td.c-name, .mud-table-root td')]
    .filter(td => { if (!td.textContent.trim() || td.querySelector('button, input, .mud-button-root')) return false; const r = document.createRange(); r.selectNodeContents(td); const h = r.getBoundingClientRect().height; return h > 1.8 * parseFloat(getComputedStyle(td).lineHeight || '20'); }).length;
  const outside = [];
  for (const box of document.querySelectorAll('.card-header, .lineform, .mud-toolbar, .mud-paper, table')) {
    const r = box.getBoundingClientRect(); if (!r.width) continue;
    for (const el of box.querySelectorAll('input, select, button, label, td, th, .mud-input-control')) {
      const e = el.getBoundingClientRect(); if (!e.width) continue;
      if (e.right > r.right + 2 || e.left < r.left - 2) { outside.push((el.id || el.className || el.tagName).toString().slice(0, 40)); if (outside.length > 5) break; }
    }
  }
  const beyond = [...document.querySelectorAll('body *')].filter(el => { const e = el.getBoundingClientRect(); return e.width && e.right > de.clientWidth + 2 && getComputedStyle(el).position !== 'fixed'; }).length;
  return { hScroll, wrapped, outside, beyond, dark: !!document.querySelector('.app-dark') };
})()`;

const scratch = process.env.SCRATCH || process.cwd();
const shots = join(scratch, 'shots'); mkdirSync(shots, { recursive: true });
const b = await launch();
const rows = [];
try {
  await login(b);
  for (const mode of MODES) {
    // The theme choice lives in the browser (Settings -> Appearance). Set it the way the app saves it, then reload.
    await b.eval(`localStorage.setItem('inventory-theme', JSON.stringify({ colour: 'Teal', mode: '${mode}' }))`);
    for (const w of WIDTHS) {
      await b.send('Emulation.setDeviceMetricsOverride', { width: w, height: 950, deviceScaleFactor: 1, mobile: false });
      for (const [name, path] of Object.entries(SCREENS)) {
        await b.goto(path); await sleep(2500);
        const m = await b.eval(MEASURE);
        const file = join(shots, `${name}-${w}-${mode.toLowerCase()}.png`);
        await b.shot(file);
        rows.push({ screen: name, width: w, mode, ...m, outside: m.outside.join(',') });
      }
    }
  }
} finally { b.close(); }

const bad = rows.filter(r => r.hScroll || r.wrapped || r.outside || r.beyond || (r.mode === 'Dark') !== r.dark);
console.table(rows.map(r => ({ screen: r.screen, width: r.width, mode: r.mode, dark: r.dark, hScroll: r.hScroll, wrappedNames: r.wrapped, beyondScreen: r.beyond, outsideBox: r.outside })));
console.log(bad.length ? `PROBLEMS: ${bad.length} (see rows above). Screenshots in ${shots}` : `OK: no sideways scroll, no wrapped names, nothing outside its box. Screenshots in ${shots}`);
process.exit(bad.length ? 1 : 0);
