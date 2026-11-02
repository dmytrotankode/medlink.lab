// Скріншоти екранів ТЗ розд. 21, 23 (Playwright, Microsoft Edge) → docs/screenshots/app_*.jpg.
// Використання: запустити ЛІС (run.cmd) і виконати `node tools/video/screens_r11_mis.mjs [http://localhost:5055]`.
import { createRequire } from 'module';
import path from 'path';
import { fileURLToPath } from 'url';

const require = createRequire(import.meta.url);
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..', '..');
const { chromium } = require(path.join(ROOT, 'src', 'MedLink.LIS.Web', 'node_modules', 'playwright-core'));
const BASE = process.argv[2] || 'http://localhost:5055';
const OUT = path.join(ROOT, 'docs', 'screenshots');
const TECH = '0e000000-0000-0000-0000-000000000003';
const sleep = ms => new Promise(r => setTimeout(r, ms));

const browser = await chromium.launch({ channel: 'msedge', headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'uk-UA', deviceScaleFactor: 1 });
await context.addInitScript(id => { try { localStorage.setItem('medlink.lis.employeeId', id); } catch (e) {} }, TECH);
const page = await context.newPage();

async function shot (url, file, prepare) {
  await page.goto(BASE + url, { waitUntil: 'networkidle' });
  await sleep(800);
  if (prepare) { await prepare(); await sleep(900); }
  await page.screenshot({ path: path.join(OUT, file), type: 'jpeg', quality: 82 });
  console.log('saved', file);
}

await shot('/laboratory/refs', 'app_incoming_referrals.jpg', async () => { await page.getByText('НП-2611-0108', { exact: true }).first().click(); await page.waitForSelector('[data-testid=distributionPanel]'); await sleep(1500); await page.evaluate(() => window.scrollTo(0, 0)); });
await shot('/laboratory/refs', 'app_mis_emulator.jpg', async () => { await page.getByText('Емулятор МІС').first().click(); await sleep(1200); });
await shot('/laboratory/analyzer-services', 'app_analyzer_services_matrix.jpg');
await shot('/laboratory/analyzer-services?service=DEMO-LAB-CBC', 'app_service_card.jpg');
await shot('/laboratory/work', 'app_work_by_analyzer.jpg', async () => { await page.getByText('Mindray BC-780').first().click(); });
await shot('/laboratory/work', 'app_work_by_service.jpg', async () => {
  await page.getByRole('tab', { name: /Дослідження/ }).click(); await sleep(1200);
  await page.getByText('Загальний аналіз крові (розгорнутий)').first().click();
});
await shot('/laboratory/reports?patient=0b000000-0000-0000-0000-000000000001', 'app_cumulative_report.jpg', async () => { await sleep(1500); });
await shot('/laboratory/ehealth', 'app_ehealth_submissions.jpg', async () => {
  await page.locator('[data-testid=ehealthChip]').click(); await sleep(600);
});
await browser.close();
