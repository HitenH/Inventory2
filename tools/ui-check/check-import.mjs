// Import feature (batch 8): Import pop-up on Customers, Suppliers, Products and Purchases. Run against the TEST copy only (see README).
// Needs: SCRATCH env var, Python (sqlite3; openpyxl for the .xlsx check, falls back to CSV without it).
// Usage: $env:SCRATCH='<scratch>'; node tools\ui-check\check-import.mjs
import { launch, login, sleep } from './cdp.mjs';
import { mkdirSync, writeFileSync, existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';

const scratch = process.env.SCRATCH || process.cwd();
const shots = join(scratch, 'shots'); mkdirSync(shots, { recursive: true });
const files = join(scratch, 'importfiles'); mkdirSync(files, { recursive: true });
const dbPath = join(scratch, 'data', 'Inventory.db');
const stamp = Date.now().toString().slice(-8);
let failures = 0;
const check = (name, ok, evidence = '') => { if (!ok) failures++; console.log(`${ok ? 'OK  ' : 'FAIL'} ${name}${evidence ? ' | ' + evidence : ''}`); };

// Read-only query against the test copy; returns rows as JSON.
function q(sql, ...args) {
  const code = `import sqlite3,sys,json;c=sqlite3.connect('file:'+sys.argv[1]+'?mode=ro',uri=True);print(json.dumps(c.execute(sys.argv[2],json.loads(sys.argv[3])).fetchall()))`;
  return JSON.parse(execFileSync('python', ['-I', '-c', code, dbPath, sql, JSON.stringify(args)], { encoding: 'utf8' }).trim());
}
const csv = (name, lines) => { const f = join(files, name); writeFileSync(f, lines.join('\r\n') + '\r\n'); return f; };

const CUST_H = 'Customer ID,Name,Contact Person,Address,Area,Remarks,Mobiles';
const SUPP_H = 'Supplier ID,Name,Contact Person,Address,Area,Remarks,Mobiles';
const PROD_H = 'Product ID,Product Name,Rate,Description,Category ID,Variant ID,Variant Name,Reorder Level';

const b = await launch();
const dlg = '.mud-dialog';
const dialogOpen = () => b.eval(`!!document.querySelector('${dlg} .import-dialog')`);
const dialogTitle = () => b.eval(`(document.querySelector('${dlg} .mud-dialog-title')||{}).innerText||''`);
const dialogText = () => b.eval(`(document.querySelector('${dlg}')||{innerText:''}).innerText.replace(/\\s+/g,' ')`);
async function clickText(text, scope = 'body') {
  const ok = await b.eval(`(() => { const e = [...document.querySelectorAll('${scope} button, ${scope} a, ${scope} label')].find(x => x.innerText.trim().toLowerCase().startsWith(${JSON.stringify(text.toLowerCase())})); if (!e) return false; e.setAttribute('data-ck','1'); return true; })()`);
  if (!ok) throw new Error('No button ' + text);
  await b.click('[data-ck="1"]'); await b.eval(`document.querySelector('[data-ck]')?.removeAttribute('data-ck')`);
}
const importBtn = () => b.eval(`(() => { const e = [...document.querySelectorAll('${dlg} .mud-dialog-actions button')].find(x => /^import/i.test(x.innerText.trim())); return e ? { text: e.innerText.trim(), disabled: e.disabled } : null; })()`);
const clickImport = () => b.eval(`[...document.querySelectorAll('${dlg} .mud-dialog-actions button')].find(x => /^import/i.test(x.innerText.trim())).click()`);
async function openImport(path) {
  await b.goto(path); await sleep(1500);
  await b.waitFor(`[...document.querySelectorAll('button')].some(x => x.innerText.trim() === 'Import')`);
  await b.eval(`[...document.querySelectorAll('button')].find(x => x.innerText.trim() === 'Import').click()`);
  await b.waitFor(`document.querySelector('${dlg} .import-dialog')`); await sleep(600);
}
async function chooseFile(f) {
  await b.setFiles('#import-file-input', [f]);
  await b.waitFor(`[...document.querySelectorAll('${dlg} .mud-dialog-actions button')].some(x => /^import/i.test(x.innerText.trim())) || document.querySelector('.mud-snackbar')`, 15000);
  await sleep(800);
}
const preview = () => b.eval(`(() => ({ alert: (document.querySelector('${dlg} .mud-alert')||{}).innerText||'', fileIssues: [...document.querySelectorAll('${dlg} .import-file-issues li')].map(x=>x.innerText.trim()), problems: document.querySelectorAll('${dlg} tr.import-row-problem').length, rows: [...document.querySelectorAll('${dlg} .import-preview tbody tr')].map(r => r.innerText.replace(/\\s+/g,' ').trim()) }))()`);
const problemMsgs = () => b.eval(`[...document.querySelectorAll('${dlg} tr.import-row-problem .import-problem')].map(x => x.innerText.trim())`);
const widths = () => b.eval(`(() => { const dc = document.querySelector('${dlg} .mud-dialog-content'); const ps = document.querySelector('.import-preview-scroll'); const de = document.documentElement; return { dialog: dc ? [dc.scrollWidth, dc.clientWidth] : null, scroll: ps ? [ps.scrollWidth, ps.clientWidth] : null, page: [de.scrollWidth, de.clientWidth] }; })()`);
const closeDialog = async () => { await clickText('Close', dlg + ' .mud-dialog-actions'); await sleep(800); };
async function search(text) {
  await b.eval(`(() => { const i = document.querySelector('input[placeholder="Enter search term"]'); i.value=''; i.focus(); })()`);
  await b.type(text); await sleep(1200);
}
const listText = () => b.eval(`[...document.querySelectorAll('.mud-table-body tr')].map(r => r.innerText.replace(/\\s+/g,' ').trim()).join(' || ')`);
const shot = n => b.shot(join(shots, `import-${n}.png`));
const noScroll = w => w.dialog[0] <= w.dialog[1] && w.scroll && w.scroll[0] <= w.scroll[1] && w.page[0] <= w.page[1];

try {
  await login(b);
  await b.send('Emulation.setDeviceMetricsOverride', { width: 1366, height: 900, deviceScaleFactor: 1, mobile: false });
  await b.eval(`localStorage.setItem('inventory-theme', JSON.stringify({ colour: 'Teal', mode: 'Light' }))`);

  // 1 + 2: the pop-up on each list page, and the sample file
  const kinds = [['customers', 'Customers', '/customers'], ['suppliers', 'Suppliers', '/suppliers'], ['products', 'Products', '/products'], ['purchases', 'Purchases', '/purchases']];
  for (const [k, title, path] of kinds) {
    await openImport(path);
    const toolbar = await b.eval(`[...document.querySelectorAll('.mud-toolbar button, .mud-toolbar a')].map(x => x.innerText.trim())`);
    const t = await dialogTitle();
    const headings = await b.eval(`[...document.querySelectorAll('${dlg} table.import-columns tbody tr td.import-col-name')].map(td => td.firstChild.textContent.trim())`);
    const hasLink = await b.eval(`[...document.querySelectorAll('${dlg} a')].some(a => a.innerText.includes('Download sample file (CSV)'))`);
    const hasChoose = await b.eval(`[...document.querySelectorAll('${dlg} label')].some(a => a.innerText.includes('Choose file'))`);
    const idx = toolbar.findIndex(x => x.toLowerCase().startsWith('add new'));
    check(`1 ${title}: Import button next to Add New, pop-up "Import ${title}" with columns, sample link, Choose file`,
      t.trim() === `Import ${title}` && idx >= 0 && toolbar.slice(idx + 1, idx + 4).includes('Import') && headings.length > 0 && hasLink && hasChoose, `title="${t.trim()}" toolbar=${JSON.stringify(toolbar)} columns=${headings.length}`);
    await shot(`1-${k}-light`);
    if (k === 'customers') {
      await b.eval(`localStorage.setItem('inventory-theme', JSON.stringify({ colour: 'Teal', mode: 'Dark' }))`);
      await openImport(path);
      await shot('1-customers-dark');
      check('1 Customers pop-up in dark theme (screenshot taken, dark active)', await b.eval(`!!document.querySelector('.app-dark')`));
      await b.eval(`localStorage.setItem('inventory-theme', JSON.stringify({ colour: 'Teal', mode: 'Light' }))`);
      await openImport(path);
    }
    const res = await b.eval(`(async () => { const r = await fetch('/import-sample/${k}'); const t = await r.text(); return { status: r.status, cd: r.headers.get('content-disposition'), first: t.split(/\\r?\\n/)[0] }; })()`);
    check(`2 ${title}: sample file 200, attachment *-import-sample.csv, first line = pop-up headings`,
      res.status === 200 && /attachment/i.test(res.cd || '') && /-import-sample\.csv/i.test(res.cd || '') && res.first === headings.join(','), `status=${res.status} cd=${res.cd} first="${res.first}"`);
    const before = await b.eval('location.href');
    await clickText('Download sample file', dlg); await sleep(1500);
    check(`2 ${title}: clicking the sample link keeps pop-up open, URL unchanged`, (await dialogOpen()) && (await b.eval('location.href')) === before, await b.eval('location.href'));
    await closeDialog();
  }

  const existing = q('select CustomerId from Customers where CustomerId is not null and CustomerId != "" limit 1')[0][0];
  const custCount0 = q('select count(*) from Customers')[0][0];

  // 3: bad customers file
  await openImport('/customers');
  const bad = csv('customers-bad.csv', [CUST_H,
    `${existing},Existing Id Row,,,,,9876543210`,
    `ZZIMP-${stamp}-N,,,,,,9876543211`,
    `ZZIMP-${stamp}-D,Dup A,,,,,9876543212`,
    `ZZIMP-${stamp}-D,Dup B,,,,,9876543213`,
    `ZZIMP-${stamp}-M,Mobile Letters,,,,,98AB543210`,
    `ZZIMP-${stamp}-OK,Fine Row,,,,,9876543214`]);
  await chooseFile(bad);
  let p = await preview(), ib = await importBtn(), w = await widths();
  const redMsgs = await problemMsgs();
  const has3 = s => redMsgs.some(m => m.includes(s));
  const okRowLast = /Fine Row/.test(p.rows[p.rows.length - 1] || '') && !/Fine Row/.test(p.rows.slice(0, 5).join(' '));
  check('3 bad customers file: red alert, 5 problem rows first, messages for exists / no Name / duplicate / mobile, Import disabled',
    /problem\(s\) found/.test(p.alert) && p.problems === 5 && okRowLast && has3('already exists') && has3('Name is required') && has3('is also on') && has3('Mobile number') && ib && ib.disabled,
    `alert="${p.alert.replace(/\s+/g, ' ')}" problemRows=${p.problems} messages=${JSON.stringify(redMsgs)} importBtn=${JSON.stringify(ib)}`);
  check('3 bad file: no sideways scroll (dialog, preview, page)', noScroll(w), JSON.stringify(w));
  await shot('3-bad-customers');

  // 4: wrong headings
  await clickText('Choose another file', dlg); await sleep(600);
  check('6 Choose another file returns to step 1', await b.eval(`!!document.querySelector('${dlg} #import-file-input')`) && !(await importBtn()));
  const wrongH = csv('customers-wrong-headings.csv', ['Customer ID,Phone,Address', 'ZZIMP-X,9876543210,Somewhere']);
  await chooseFile(wrongH);
  p = await preview(); ib = await importBtn();
  check('4 wrong headings: bullet messages (Phone not recognised, Name missing), Import disabled',
    p.fileIssues.some(x => x.includes("'Phone'") && x.includes('not recognised')) && p.fileIssues.some(x => x.includes("'Name' is missing")) && ib && ib.disabled, JSON.stringify(p.fileIssues));
  await shot('4-wrong-headings');

  // 5: wrong file type
  await clickText('Choose another file', dlg); await sleep(600);
  const txt = join(files, 'notes.txt'); writeFileSync(txt, 'just some notes\r\n');
  await chooseFile(txt);
  p = await preview(); ib = await importBtn();
  const snack5 = await b.snack();
  check('5 wrong file type notes.txt: message shown, Import disabled', (p.fileIssues.length > 0 || /csv|xlsx|type/i.test(p.alert + snack5)) && (!ib || ib.disabled),
    `alert="${p.alert.replace(/\s+/g, ' ')}" issues=${JSON.stringify(p.fileIssues)} snack="${snack5}" import=${JSON.stringify(ib)}`);
  await shot('5-wrong-type');

  // 6: re-choose, same file twice
  if (await importBtn()) { await clickText('Choose another file', dlg); await sleep(600); }
  await chooseFile(bad);
  const a1 = (await preview()).problems;
  await clickText('Choose another file', dlg); await sleep(600);
  await chooseFile(bad);
  const a2 = (await preview()).problems;
  check('6 same file chosen twice in a row is re-checked each time', a1 === 5 && a2 === 5, `problem rows ${a1}, ${a2}`);
  await closeDialog();

  // 7: good customers file
  await openImport('/customers');
  const id1 = `ZZIMP-${stamp}-1`, id2 = `ZZIMP-${stamp}-2`, id3 = `ZZIMP-${stamp}-3`;
  const good = csv('customers-good.csv', [CUST_H,
    `${id1},Zz Import One,Contact One,1 Test Road,Testville,,9876543210;9123456780`,
    `${id2},Zz Import Two,,,,,9988776655`,
    `${id3},Zz Import Three,,,,,`]);
  await chooseFile(good);
  p = await preview(); ib = await importBtn();
  check('7 good customers file: green "Ready to import: 3 customers", button "Import 3 customers" enabled',
    /Ready to import: 3 customers/.test(p.alert) && ib && ib.text === 'Import 3 customers' && !ib.disabled, `alert="${p.alert.trim()}" import=${JSON.stringify(ib)}`);
  await shot('7-good-customers');
  await clickImport(); await sleep(2500);
  const sn = await b.snack();
  check('7 Import click: snackbar "Imported 3 customers.", pop-up closes', /Imported 3 customers\./.test(sn) && !(await dialogOpen()), `snack="${sn}"`);
  await search('Zz Import'); await shot('7-list-after-import');
  const lt = await listText();
  check('7 new customers visible in the list', ['Zz Import One', 'Zz Import Two', 'Zz Import Three'].every(n => lt.includes(n)), lt.slice(0, 200));
  const rows = q('select CustomerId,Name,ContactPerson,Area,(select group_concat(Phone,"|") from Mobiles m where m.CustomerEntityId=c.Id) from Customers c where CustomerId like ?', `ZZIMP-${stamp}-%`);
  const r1 = rows.find(r => r[0].toUpperCase() === id1.toUpperCase());
  check('7 DB: 3 rows, customer 1 has the 2 mobiles', rows.length === 3 && r1 && r1[4] && r1[4].split('|').sort().join() === '9123456780,9876543210', JSON.stringify(rows));
  await openImport('/customers');
  await chooseFile(good);
  p = await preview(); ib = await importBtn();
  const msgs7 = await problemMsgs();
  const exists = msgs7.filter(x => /already exists/.test(x)).length;
  check('7 same file again: every row "already exists", Import disabled, DB still exactly 3 new',
    exists === 3 && ib && ib.disabled && q('select count(*) from Customers where CustomerId like ?', `ZZIMP-${stamp}-%`)[0][0] === 3 && q('select count(*) from Customers')[0][0] === custCount0 + 3,
    `alreadyExists=${exists} alert="${p.alert.replace(/\s+/g, ' ')}"`);
  await shot('7-second-import-exists');
  await closeDialog();

  // 11: keyboard safety
  await openImport('/customers');
  const url0 = await b.eval('location.href');
  await b.key('Escape'); await sleep(1000);
  const stillOpen = await dialogOpen();
  check('11 Escape with pop-up open: stays on the list page', (await b.eval('location.href')) === url0, `url=${await b.eval('location.href')} dialogStillOpen=${stillOpen}`);
  if (stillOpen) await closeDialog();
  await sleep(500);
  await search(`ZZIMP-${stamp}-2`);
  const lt2 = await listText();
  check('11 after Close the search box still works', /Zz Import Two/i.test(lt2) && !/Zz Import One/i.test(lt2), lt2.slice(0, 120));

  // 8: suppliers xlsx
  const sid1 = `ZZSUP-${stamp}-1`, sid2 = `ZZSUP-${stamp}-2`;
  const xlsx = join(files, 'suppliers-good.xlsx');
  let usedXlsx = true;
  try {
    const code = `import sys,openpyxl
wb=openpyxl.Workbook();ws=wb.active
ws.append(sys.argv[2].split(","))
ws.append([sys.argv[3],"Zz Supplier One","Ann","1 Road","Town","",919876543210])
ws.append([sys.argv[4],"Zz Supplier Two","","","","","9123456780;9988776655"])
wb.save(sys.argv[1])`;
    execFileSync('python', ['-I', '-c', code, xlsx, SUPP_H, sid1, sid2]);
  } catch { usedXlsx = false; }
  const suppFile = usedXlsx ? xlsx : csv('suppliers-good.csv', [SUPP_H, `${sid1},Zz Supplier One,Ann,1 Road,Town,,919876543210`, `${sid2},Zz Supplier Two,,,,,9123456780;9988776655`]);
  await openImport('/suppliers');
  await chooseFile(suppFile);
  p = await preview(); ib = await importBtn();
  const row1 = p.rows.find(r => r.includes('Zz Supplier One')) || '';
  check(`8 Suppliers good ${usedXlsx ? '.xlsx' : 'CSV (openpyxl missing)'}: ready 2 suppliers, number cell shows 919876543210 not 9.19877E+11`,
    /Ready to import: 2 suppliers/.test(p.alert) && row1.includes('919876543210') && !/E\+/.test(row1) && ib && ib.text === 'Import 2 suppliers' && !ib.disabled, `alert="${p.alert.trim()}" row="${row1}"`);
  await shot('8-good-suppliers');
  await clickImport(); await sleep(2500);
  const sn8 = await b.snack();
  check('8 Suppliers import: snackbar "Imported 2 suppliers.", pop-up closed', /Imported 2 suppliers\./.test(sn8) && !(await dialogOpen()), sn8);
  await search('Zz Supplier'); await shot('8-list-after-import');
  const lt3 = await listText();
  const srows = q('select SupplierId,Name,(select group_concat(Phone,"|") from Mobiles m where m.SupplierEntityId=s.Id) from Suppliers s where SupplierId like ?', `ZZSUP-${stamp}-%`);
  const s1 = srows.find(r => r[0].toUpperCase() === sid1.toUpperCase());
  check('8 Suppliers in list and DB (12-digit mobile stored as 919876543210)', lt3.includes('Zz Supplier One') && lt3.includes('Zz Supplier Two') && srows.length === 2 && s1 && s1[2] === '919876543210', JSON.stringify(srows));

  // 9: products good
  const pa = `ZZPROD-${stamp}-A`, pb = `ZZPROD-${stamp}-B`;
  const pgood = csv('products-good.csv', [PROD_H,
    `${pa},Zz Prod Alpha,250,Alpha desc,,${pa}-V1,First Variant,5`,
    `${pa},Zz Prod Alpha,250,Alpha desc,,${pa}-V2,Second Variant,`,
    `${pb},Zz Prod Beta,99.50,,,,,`]);
  await openImport('/products');
  await chooseFile(pgood);
  p = await preview(); ib = await importBtn();
  check('9 Products good: "Ready to import: 2 products with 2 variants", enabled', /Ready to import: 2 products with 2 variants/.test(p.alert) && ib && !ib.disabled && ib.text === 'Import 2 products', `alert="${p.alert.trim()}" import=${JSON.stringify(ib)}`);
  await shot('9-good-products');
  await clickImport(); await sleep(2500);
  const sn9 = await b.snack();
  check('9 Products import: snackbar "Imported 2 products.", closed', /Imported 2 products\./.test(sn9) && !(await dialogOpen()), sn9);
  await search('Zz Prod');
  const lt4 = await listText();
  const prods = q('select ProductId,Name,Rate,Description,CategoryId from Products where ProductId like ?', `ZZPROD-${stamp}-%`);
  const vars = q('select p.ProductId,v.VariantId,v.Name,v.ReorderLevel from Variants v join Products p on p.Id=v.ProductEntityId where p.ProductId like ? order by v.VariantId', `ZZPROD-${stamp}-%`);
  const upper = s => String(s).toUpperCase();
  check('9 Products in list and DB (2 products, 2 variants on A, reorder 5 / NULL, none on B)',
    lt4.includes('Zz Prod Alpha') && lt4.includes('Zz Prod Beta') && prods.length === 2 && vars.length === 2 && vars.every(v => upper(v[0]) === upper(pa)) &&
    vars[0][3] === 5 && vars[1][3] === null && prods.every(r => r[4] === null),
    JSON.stringify({ prods, vars }));
  await b.eval(`(() => { const r = [...document.querySelectorAll('.mud-table-body tr')].find(x => x.innerText.includes('Zz Prod Alpha')); r.querySelector('a, button').setAttribute('data-ck','1'); })()`);
  await b.click('[data-ck="1"]'); await sleep(2500);
  await shot('9-product-alpha-page');
  const pageText = await b.eval(`document.body.innerText.replace(/\\s+/g,' ')`);
  check('9 Product A page shows its two variants', /First Variant/i.test(pageText) && /Second Variant/i.test(pageText), await b.path());

  // 10: products bad
  await openImport('/products');
  const pbad = csv('products-bad.csv', [PROD_H,
    `ZZPB-${stamp}-1,Bad Rate,"1,250",,,,,`,
    `ZZPB-${stamp}-2,Bad Category,10,,NOSUCHCAT,,,`,
    `ZZPB-${stamp}-3,Diff Name,10,,,V1,One,`,
    `ZZPB-${stamp}-3,Different Name,12,,,V2,Two,`,
    `ZZPB-${stamp}-4,Dup Variant,10,,,V1,One,`,
    `ZZPB-${stamp}-4,Dup Variant,10,,,V1,Uno,`]);
  await chooseFile(pbad);
  p = await preview(); ib = await importBtn(); w = await widths();
  const msgs = await problemMsgs();
  const has = s => msgs.some(m => m.includes(s));
  check('10 Products bad: rate "1,250", unknown category, differing details, duplicate variant: each message shown, Import disabled',
    has("Rate '1,250'") && has('does not exist') && has('differs from row') && has("Variant ID 'V1' is also on") && ib && ib.disabled, JSON.stringify(msgs));
  check('10 bad products: no sideways scroll', noScroll(w), JSON.stringify(w));
  await shot('10-bad-products');
  await closeDialog();

  // 12: Purchases (opening stock). Uses a real supplier, product and two of its variants from the test copy, and a date with no purchases.
  await openImport('/purchases');
  const introText = await dialogText();
  check('12 Purchases pop-up intro line says stock goes up and a backup is taken',
    introText.includes('Import adds new purchases. Stock goes up as soon as they are imported, and a backup is taken first.'), introText.slice(0, 200));
  await shot('12-purchases-popup');
  await closeDialog();

  const pick = q(`select s.SupplierId, p.ProductId, v1.VariantId, v2.VariantId, v1.Id, v2.Id from Products p
    join Variants v1 on v1.ProductEntityId = p.Id join Variants v2 on v2.ProductEntityId = p.Id and v2.Id > v1.Id, Suppliers s limit 400`)
    .find(r => r.slice(0, 4).every(x => /^[A-Za-z0-9]+$/.test(String(x))) && r[2].toLowerCase() !== r[3].toLowerCase());
  const [sup, prod, var1, var2, vid1, vid2] = pick;
  const supName = q('select Name from Suppliers where SupplierId = ?', sup)[0][0];
  // A day in April 2019 with no purchases yet (01 normally), so the check can be run again on the same test copy.
  let D = '01';
  for (let day = 1; day <= 28 && q(`select count(*) from Purchases where Date like '2019-04-${String(day).padStart(2, '0')}%'`)[0][0] > 0; day++) D = String(day + 1).padStart(2, '0');
  const PURCH_H = 'Date,Supplier ID,Product ID,Variant ID,Quantity,Rate,Discount %,Remarks';
  const remark = `ZZIMP-${stamp}`;
  const purchaseCount = () => q('select count(*) from Purchases')[0][0];
  const stockOf = vid => q('select coalesce((select sum(Quantity) from PurchaseVariant where VariantEntityId=?),0) - coalesce((select sum(Quantity) from SalesVariants where VariantEntityId=?),0)', vid, vid)[0][0];
  const backupDir = join(scratch, 'data', 'Backups');
  const beforeImportBackups = () => existsSync(backupDir) ? readdirSync(backupDir).filter(f => f.startsWith('before-import_') && f.endsWith('.db')) : [];
  console.log(`   using supplier ${sup}, product ${prod}, variants ${var1} and ${var2}`);
  check(`12 the chosen date ${D}-04-2019 has no purchases yet`, q(`select count(*) from Purchases where Date like '2019-04-${D}%'`)[0][0] === 0);

  // bad file
  await openImport('/purchases');
  const pbadFile = csv('purchases-bad.csv', [PURCH_H,
    `${D}-04-2019,NOSUCH${stamp},${prod},${var1},5,,,`,
    `${D}-04-2019,${sup},${prod},NOVAR,5,,,`,
    `02-04-2019,${sup},${prod},${var1},0,,,`,
    `31-13-2019,${sup},${prod},${var1},5,,,`,
    `03-04-2019,${sup},${prod},${var1},2,,,`,
    `03-04-2019,${sup},${prod},${var1},3,,,`]);
  await chooseFile(pbadFile);
  p = await preview(); ib = await importBtn(); w = await widths();
  const pmsgs = await problemMsgs();
  const hasP = s => pmsgs.some(m => m.includes(s));
  check('12 bad purchases file: unknown supplier, unknown variant, quantity 0, bad date, duplicate line (6 rows), Import disabled',
    /problem\(s\) found/.test(p.alert) && p.problems === 6 && hasP(`Supplier ID 'NOSUCH${stamp}' does not exist. Add the supplier first.`) &&
    hasP(`Variant ID 'NOVAR' does not exist for product '${prod}'.`) && hasP("Quantity '0' must be a whole number greater than 0.") &&
    hasP("Date '31-13-2019' is not a valid date. Use day-month-year, like 01-04-2026.") && hasP('of the same purchase. Add the quantities together on one row.') && ib && ib.disabled,
    `alert="${p.alert.replace(/\s+/g, ' ')}" problemRows=${p.problems} messages=${JSON.stringify(pmsgs)}`);
  check('12 bad purchases file: no sideways scroll (dialog, preview, page)', noScroll(w), JSON.stringify(w));
  await shot('12-bad-purchases');
  await closeDialog();

  // good file
  const total = 10 * 250 + (4 * 99.5 - 4 * 99.5 * 0.10);   // 2858.2
  const goodP = csv('purchases-good.csv', [PURCH_H,
    `${D}-04-2019,${sup},${prod},${var1},10,250,,${remark}`,
    `${D}-04-2019,${sup},${prod},${var2},4,99.5,10,${remark}`]);
  const stock1Before = stockOf(vid1), stock2Before = stockOf(vid2), purchasesBefore = purchaseCount(), backupsBefore = beforeImportBackups().length;
  await openImport('/purchases');
  await chooseFile(goodP);
  p = await preview(); ib = await importBtn(); w = await widths();
  check('12 good purchases file: "Ready to import: 1 purchase with 2 lines", button "Import 1 purchase" enabled',
    /Ready to import: 1 purchase with 2 lines/.test(p.alert) && ib && ib.text === 'Import 1 purchase' && !ib.disabled, `alert="${p.alert.trim()}" import=${JSON.stringify(ib)}`);
  check('12 good purchases file: no sideways scroll', noScroll(w), JSON.stringify(w));
  await shot('12-good-purchases');
  await clickImport(); await sleep(3000);
  const sn12 = await b.snack();
  check('12 Purchases import: snackbar "Imported 1 purchase.", pop-up closes', /Imported 1 purchase\./.test(sn12) && !(await dialogOpen()), `snack="${sn12}"`);

  const hdr = q(`select p.Id, p.VoucherId, p.Remarks, p.TotalAmountProduct, p.Discount from Purchases p join Suppliers s on s.Id = p.SupplierEntityId where p.Date like '2019-04-${D}%' and s.SupplierId = ?`, sup);
  const lns = hdr.length ? q('select SerialNumber, Quantity, ProductRate, Amount, Discount, AmountAfterDiscount from PurchaseVariant where PurchaseEntityId = ? order by SerialNumber', hdr[0][0]) : [];
  check('12 DB: one purchase, voucher number 1, remarks, total 2858.2; two lines numbered 1 and 2 with the right amounts',
    purchaseCount() === purchasesBefore + 1 && hdr.length === 1 && hdr[0][1] === 1 && hdr[0][2] === remark && Math.abs(Number(hdr[0][3]) - total) < 0.001 &&
    lns.length === 2 && lns[0][0] === 1 && lns[0][1] === 10 && Number(lns[0][3]) === 2500 && lns[1][0] === 2 && lns[1][1] === 4 && Number(lns[1][3]) === 398 && lns[1][4] === 10 && Math.abs(Number(lns[1][5]) - 358.2) < 0.001,
    JSON.stringify({ hdr, lns }));
  const newBackups = beforeImportBackups();
  check('12 a before-import backup file was created in the Backups folder', newBackups.length === backupsBefore + 1, JSON.stringify(newBackups));
  check('12 stock of both variants rose by the quantities (10 and 4)', stockOf(vid1) === stock1Before + 10 && stockOf(vid2) === stock2Before + 4,
    `variant ${var1}: ${stock1Before} -> ${stockOf(vid1)}; variant ${var2}: ${stock2Before} -> ${stockOf(vid2)}`);

  // the stock report on screen
  await b.goto('/reports');
  await b.waitFor(`document.querySelector('.mud-tab')`);
  const tab = name => b.eval(`[...document.querySelectorAll('.mud-tab')].find(x => x.innerText.trim().toLowerCase() === ${JSON.stringify(name)}).click()`);
  await tab('products'); await sleep(800); await tab('variants list table'); await sleep(2000);
  await b.eval(`(() => { const i = document.querySelector('input[placeholder="Search"]'); i.focus(); })()`);
  await b.type(prod); await sleep(1500);
  const stockRows = await b.eval(`[...document.querySelectorAll('.mud-table-body tr')].map(r => r.innerText.replace(/\\s+/g,' ').trim())`);
  const rowFor = v => stockRows.find(r => r.includes(prod) && new RegExp('\\b' + v + '\\b').test(r)) || '';
  check('12 stock report on screen shows the new stock for both variants', rowFor(var1).trim().endsWith(String(stockOf(vid1))) && rowFor(var2).trim().endsWith(String(stockOf(vid2))),
    `${rowFor(var1)} || ${rowFor(var2)}`);
  await shot('12-stock-report');

  // the new purchase in the list, and opened on the normal Purchase screen
  await b.goto('/purchases'); await sleep(1500);
  await b.waitFor(`document.querySelector('input[placeholder="Enter search term"]')`);
  await search('2019');
  const lp = await listText();
  check('12 imported purchase appears in the Purchases list (remarks and supplier name)', lp.includes(remark) && lp.includes(supName), lp.slice(0, 300));
  await shot('12-purchases-list');
  await b.eval(`(() => { const r = [...document.querySelectorAll('.mud-table-body tr')].find(x => x.innerText.includes(${JSON.stringify(remark)})); r.querySelector('a, button').setAttribute('data-ck','1'); })()`);
  await b.click('[data-ck="1"]'); await sleep(3500);
  await shot('12-purchase-opened');
  // The total and voucher are in boxes, so the boxes' values are read too.
  const pageTxt = await b.eval(`document.body.innerText.replace(/\\s+/g,' ') + ' INPUTS: ' + [...document.querySelectorAll('input')].map(i => i.value).join(' | ')`);
  check('12 imported purchase opens on the normal Purchase screen with its lines and total', /\/purchase\//.test(await b.path()) && pageTxt.includes(prod) && /2,?858\.2/.test(pageTxt) && /\b2,?500(\.0+)?\b/.test(pageTxt) && /358\.2/.test(pageTxt),
    `${await b.path()} :: ${pageTxt.slice(pageTxt.indexOf('INPUTS'))}`);

  // the same file again: the double-import guard
  await openImport('/purchases');
  await chooseFile(goodP);
  p = await preview(); ib = await importBtn();
  const gmsgs = await problemMsgs();
  check('12 same purchases file again: both rows refused by the guard (voucher 1), Import disabled, nothing added',
    p.problems === 2 && gmsgs.every(m => m.includes(`A purchase from supplier '${sup}' dated ${D}-04-2019 with exactly the same items and quantities already exists (voucher 1). Importing it again would count the stock twice.`)) && ib && ib.disabled && purchaseCount() === purchasesBefore + 1,
    `problemRows=${p.problems} messages=${JSON.stringify(gmsgs)}`);
  await shot('12-second-import-guard');
  await closeDialog();
} catch (e) {
  failures++;
  console.log('FAIL script error:', e.message);
  try { await b.shot(join(shots, 'import-script-error.png')); } catch { }
} finally {
  b.close();
}
console.log(failures ? `PROBLEMS: ${failures}. Screenshots in ${shots}` : `OK: all import checks passed. Screenshots in ${shots}`);
process.exit(failures ? 1 : 0);
