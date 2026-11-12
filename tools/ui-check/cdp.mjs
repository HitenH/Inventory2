// Minimal Chrome DevTools Protocol driver for UI checks against the TEST copy of the app (port 5299).
import { spawn } from 'node:child_process';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

export const BASE = 'http://localhost:5299';
const sleep = ms => new Promise(r => setTimeout(r, ms));
export { sleep };

export async function launch() {
  const dir = mkdtempSync(join(process.env.SCRATCH || tmpdir(), 'chrome-'));
  const port = 9333;
  const chrome = spawn('C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe', [
    '--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${dir}`,
    '--no-first-run', '--no-default-browser-check', '--window-size=1600,1000', 'about:blank'], { stdio: 'ignore' });
  let targets;
  for (let i = 0; i < 50; i++) {
    try { targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); if (targets.find(t => t.type === 'page')) break; } catch { }
    await sleep(200);
  }
  const page = targets.find(t => t.type === 'page');
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise(r => ws.addEventListener('open', r));
  let id = 0; const pending = new Map();
  ws.addEventListener('message', ev => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) { const { res, rej } = pending.get(m.id); pending.delete(m.id); m.error ? rej(new Error(JSON.stringify(m.error))) : res(m.result); }
  });
  const send = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pending.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
  await send('Page.enable'); await send('Runtime.enable');

  const b = {
    send,
    async eval(expr) {
      const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true });
      if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || JSON.stringify(r.exceptionDetails));
      return r.result.value;
    },
    async goto(path) { await send('Page.navigate', { url: BASE + path }); await sleep(1500); },
    async waitFor(expr, ms = 8000) {
      for (let t = 0; t < ms; t += 100) { if (await b.eval(`!!(${expr})`)) return true; await sleep(100); }
      throw new Error('Timed out waiting for: ' + expr);
    },
    async key(key, mods = 0) {
      const map = { Enter: [13, 'Enter', '\r'], Escape: [27, 'Escape', ''], ArrowDown: [40, 'ArrowDown', ''], ArrowUp: [38, 'ArrowUp', ''], Tab: [9, 'Tab', ''] };
      let code, k, text;
      if (map[key]) { [code, k, text] = map[key]; } else { k = key; code = key.toUpperCase().charCodeAt(0); text = mods ? '' : key; }
      const codeName = map[key] ? k : 'Key' + key.toUpperCase();
      await send('Input.dispatchKeyEvent', { type: text ? 'keyDown' : 'rawKeyDown', key: k, code: codeName, windowsVirtualKeyCode: code, modifiers: mods, text });
      await send('Input.dispatchKeyEvent', { type: 'keyUp', key: k, code: codeName, windowsVirtualKeyCode: code, modifiers: mods });
    },
    async type(text) { for (const ch of text) { await send('Input.insertText', { text: ch }); await sleep(60); } },
    async click(selector) {
      const box = await b.eval(`(() => { const e = document.querySelector(${JSON.stringify(selector)}); if (!e) return null; e.scrollIntoView({block:'center'}); const r = e.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`);
      if (!box) throw new Error('No element ' + selector);
      for (const type of ['mouseMoved', 'mousePressed', 'mouseReleased'])
        await send('Input.dispatchMouseEvent', { type, x: box.x, y: box.y, button: 'left', clickCount: 1 });
    },
    active() { return b.eval(`(() => { const a = document.activeElement; if (!a) return 'none'; const box = a.closest('[class*="focus-"]'); return a.tagName + '#' + (a.id||'') + (box ? ' in .' + [...box.classList].filter(c => c.startsWith('focus-')).join('.') : ''); })()`); },
    snack() { return b.eval(`[...document.querySelectorAll('.mud-snackbar')].map(s => s.innerText).join(' | ')`); },
    path() { return b.eval('location.pathname'); },
    async shot(file) { const r = await send('Page.captureScreenshot', { format: 'png' }); (await import('node:fs')).writeFileSync(file, Buffer.from(r.data, 'base64')); },
    close() { try { ws.close(); } catch { } chrome.kill(); },
  };
  return b;
}

export async function login(b) {
  await b.goto('/login');
  await b.waitFor(`document.querySelector('#login-name')`);
  await b.click('#login-name'); await b.type('Admin');
  await b.click('#login-password'); await b.type('UiTest#2026');
  await b.key('Enter');
  await b.waitFor(`location.pathname === '/'`);
  await sleep(1000);
}
