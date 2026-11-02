// Скріншоти екранів ТЗ розд. 22 «Патоморфологія» (Playwright, Microsoft Edge) → docs/screenshots/app_path_*.jpg.
// Використання: запустити ЛІС (run.cmd) і виконати `node tools/video/screens_r11_pathology.mjs [http://localhost:5055]`.
import { createRequire } from 'module';
import path from 'path';
import { fileURLToPath } from 'url';

const require = createRequire(import.meta.url);
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..', '..');
const { chromium } = require(path.join(ROOT, 'src', 'MedLink.LIS.Web', 'node_modules', 'playwright-core'));
const BASE = process.argv[2] || 'http://localhost:5055';
const OUT = path.join(ROOT, 'docs', 'screenshots');
const DOCTOR = '0e000000-0000-0000-0000-000000000002';
const sleep = ms => new Promise(r => setTimeout(r, ms));

const browser = await chromium.launch({ channel: 'msedge', headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'uk-UA', deviceScaleFactor: 1 });
await context.addInitScript(id => { try { localStorage.setItem('medlink.lis.employeeId', id); } catch (e) {} }, DOCTOR);
const page = await context.newPage();
const api = async p => (await page.request.get(BASE + '/api/v1/lab' + p, { headers: { 'X-MedLink-Employee-Id': DOCTOR } })).json();

async function shot (url, file, prepare) {
  await page.goto(BASE + url, { waitUntil: 'networkidle' });
  await sleep(900);
  if (prepare) { await prepare(); await sleep(900); }
  await page.screenshot({ path: path.join(OUT, file), type: 'jpeg', quality: 82 });
  console.log('saved', file);
}

const cases = (await api('/pathology/cases')).items;
const id = no => cases.find(c => c.caseNo === no).id;

await shot('/laboratory/pathology', 'app_path_journal.jpg');
await shot('/laboratory/pathology/intake', 'app_path_intake.jpg', async () => {
  await page.getByText('Гриценко Петро', { exact: false }).first().click();
  await page.locator('[data-testid^=scan-]').first().click();
});
await shot('/laboratory/pathology/intake', 'app_path_intake_unlabeled.jpg', async () => {
  await page.getByText('Коваль Ганна', { exact: false }).first().click();
});
await shot(`/laboratory/pathology/cases/${id('ПГД-26-0418')}?tab=report`, 'app_path_report.jpg');
await shot(`/laboratory/pathology/cases/${id('ПГД-26-0412')}?tab=blocks`, 'app_path_blocks.jpg');
await shot('/laboratory/pathology/cito', 'app_path_cito.jpg');
await shot(`/laboratory/pathology/cases/${id('ПГД-26-0420')}?tab=ref`, 'app_path_cito_case.jpg', async () => {
  await page.locator('[data-testid=citoNoticeBtn]').click();
});
await shot(`/laboratory/pathology/cases/${id('ПГД-26-0405')}?tab=print`, 'app_path_form014.jpg', async () => { await sleep(800); });
await shot('/laboratory/pathology/archive', 'app_path_archive.jpg');
await browser.close();
