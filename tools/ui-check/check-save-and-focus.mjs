import { launch, login, sleep } from './cdp.mjs';

const log = (...a) => console.log(...a);
const b = await launch();
try {
  await login(b);
  await b.goto('/product'); await sleep(1500);
  log('new product: active =', await b.active(), '| in product-name =', await b.eval(`!!document.activeElement.closest('.product-name')`));

  await b.goto('/customer'); await sleep(1500);
  await b.type('ZZ UI TEST CUSTOMER');
  await b.key('Tab');
  await b.type('ZZUITEST1');          // Customer ID, still in the box when Ctrl+S is pressed
  await b.key('s', 2);                // Ctrl+S
  await sleep(2500);
  log('Ctrl+S on new customer: path =', await b.path(), '| snack =', await b.snack());
  log('saved row visible in list =', await b.eval(`document.body.innerText.includes('ZZUITEST1')`));
} catch (e) {
  log('ERROR', e.message);
} finally {
  b.close();
}
