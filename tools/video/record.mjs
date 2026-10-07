// Запис відеоогляду в Microsoft Edge (Playwright, channel msedge): сцени зі scenes.json, тривалість кожної ≥ озвучки.
// Результат: artifacts/video/raw/*.webm + timeline.json (час початку кожної сцени від старту відео, мс).
import { createRequire } from 'module';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const require = createRequire(import.meta.url);
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..', '..');
const { chromium } = require(path.join(ROOT, 'src', 'MedLink.LIS.Web', 'node_modules', 'playwright-core'));
const BASE = process.env.VIDEO_BASE || 'http://localhost:5077';
const OUT = path.join(ROOT, 'artifacts', 'video');
const scenes = JSON.parse(fs.readFileSync(path.join(__dirname, 'scenes.json'), 'utf8'));
const durations = JSON.parse(fs.readFileSync(path.join(OUT, 'audio', 'durations.json'), 'utf8'));
const prepared = JSON.parse(fs.readFileSync(path.join(OUT, 'prepared.json'), 'utf8'));
const ADMIN = '0e000000-0000-0000-0000-000000000001';
const sleep = ms => new Promise(r => setTimeout(r, ms));

const browser = await chromium.launch({ channel: 'msedge', headless: false, args: ['--window-size=1400,900', '--lang=uk-UA'] });
const context = await browser.newContext({ viewport: { width: 1366, height: 768 }, recordVideo: { dir: path.join(OUT, 'raw'), size: { width: 1366, height: 768 } }, locale: 'uk-UA' });
await context.addInitScript(id => { try { localStorage.setItem('medlink.lis.employeeId', id); } catch (e) {} }, ADMIN);
const page = await context.newPage();
const t0 = Date.now();
const timeline = [];

const go = async (route) => { await page.goto(BASE + route, { waitUntil: 'networkidle' }).catch(() => {}); await sleep(1200); };
const smoothScroll = async (px, steps = 8) => { for (let i = 0; i < steps; i++) { await page.mouse.wheel(0, px / steps); await sleep(180); } };
const vmDialog = () => page.evaluate(() => {
  const find = v => { if (v.$options.name === 'CreateOrderDialog') return v; for (const c of v.$children) { const r = find(c); if (r) return r; } return null; };
  return !!find(document.querySelector('#q-app').__vue__);
});
const clickText = async (text) => { const el = page.getByText(text, { exact: false }).first(); if (await el.count()) { await el.scrollIntoViewIfNeeded().catch(() => {}); await el.click({ timeout: 3000 }).catch(() => {}); } };

const actions = {
  intro: async () => { await go('/laboratory/dashboard'); },
  dashboard: async () => { await sleep(4000); await smoothScroll(500); await sleep(3000); await smoothScroll(-500); },
  create_ereferral: async () => {
    await go('/laboratory/orders?create=1'); await sleep(1500);
    await page.evaluate(async () => {
      const find = v => { if (v.$options.name === 'CreateOrderDialog') return v; for (const c of v.$children) { const r = find(c); if (r) return r; } return null; };
      const vm = find(document.querySelector('#q-app').__vue__);
      vm.form.referralType = 'EHEALTH';
      await new Promise(r => setTimeout(r, 1200));
      vm.ehealthNumber = '0001-2026-A7K3';
      await new Promise(r => setTimeout(r, 1500));
      await vm.lookupReferral();
    });
  },
  tube_plan: async () => {
    await sleep(1500);
    const card = page.locator('[data-testid=tubePlanCard]');
    if (await card.count()) await card.scrollIntoViewIfNeeded();
    await sleep(9000);
    await clickText('Створити замовлення');
    await sleep(2500);
  },
  order_card: async () => {
    const url = page.url();
    if (!/orders\/[0-9a-f-]{36}/.test(url)) {
      const id = await page.evaluate(async () => { const r = await fetch('/api/v1/lab/orders?pageSize=1', { headers: { 'X-MedLink-Employee-Id': '0e000000-0000-0000-0000-000000000001' } }); return (await r.json()).items[0].id; });
      await go('/laboratory/orders/' + id);
    }
    await sleep(3000); await smoothScroll(450); await sleep(3500); await smoothScroll(450);
  },
  insurance: async () => {
    await go('/laboratory/orders/' + prepared.insuranceOrderId);
    const b = page.locator('[data-testid=orderBilling]');
    await sleep(2500); if (await b.count()) await b.scrollIntoViewIfNeeded();
  },
  phlebotomy: async () => {
    await go('/laboratory/phlebotomy'); await sleep(1500);
    const input = page.locator('[data-testid=scanInput] input, input[data-testid=scanInput]').first();
    await input.click().catch(() => {}); await page.keyboard.type(prepared.barcode, { delay: 120 }); await page.keyboard.press('Enter');
    await sleep(4000); await smoothScroll(400);
  },
  workstation: async () => { await go('/laboratory/workstation'); await sleep(5000); await smoothScroll(400); },
  validation: async () => { await go('/laboratory/validation'); await sleep(4000); await smoothScroll(300); },
  sendout: async () => {
    await go('/laboratory/send-out'); await sleep(5000);
    await clickText('Реєстри'); await sleep(2000);
    await clickText('SO-'); await sleep(4000);
    await clickText('Лабораторії ('); await sleep(2000); await clickText('Сінево Україна');
  },
  clients: async () => { await go('/laboratory/clients'); await sleep(6000); await smoothScroll(300); },
  referrals: async () => { await go('/laboratory/referrals'); },
  pricing: async () => { await go('/laboratory/pricing'); await sleep(6000); await clickText('Прайс-листи'); await sleep(2000); await clickText('УНІКА — прайс'); },
  qc: async () => { await go('/laboratory/quality-control'); },
  analyzers: async () => { await go('/laboratory/analyzers'); await sleep(6000); await smoothScroll(300); },
  tat: async () => { await go('/laboratory/analytics-tat'); },
  outro: async () => { await go('/laboratory/dashboard'); }
};

for (const s of scenes) {
  const start = Date.now() - t0;
  timeline.push({ id: s.id, startMs: start });
  const minMs = Math.round((durations[s.id] || 10) * 1000) + 700;
  try { await actions[s.id]?.(); } catch (e) { console.error('scene', s.id, e.message); }
  const left = start + minMs - (Date.now() - t0);
  if (left > 0) await sleep(left);
  console.log('scene', s.id, 'done at', ((Date.now() - t0) / 1000).toFixed(1), 's');
}
const videoPath = await page.video().path();
await context.close();
await browser.close();
fs.writeFileSync(path.join(OUT, 'timeline.json'), JSON.stringify({ video: videoPath, scenes: timeline }, null, 1));
console.log('video', videoPath);
