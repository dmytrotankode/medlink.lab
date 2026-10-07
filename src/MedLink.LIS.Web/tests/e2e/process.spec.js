// Наскрізний процес із перемиканням ролей (потребує реального API на http://localhost:5055 із сідом):
// реєстратор створює замовлення → медсестра забирає → кур’єр маніфест/прийом → лаборант вводить результати →
// лікар верифікує (панічний дзвінок) → видача → бланк → кабінет пацієнта.
const { test, expect } = require('@playwright/test');
const { switchRole, pickOption, waitIdle, expectNotify } = require('./helpers');

test.describe.configure({ mode: 'serial' });

let orderNumber = '';
let orderUrl = '';
const barcodes = [];

test('1. Реєстратор створює замовлення (списки + автопідбір пробірок)', async ({ page }) => {
  await page.goto('/laboratory/orders');
  await waitIdle(page);
  await switchRole(page, 'REGISTRAR');
  await page.getByRole('button', { name: /Нове направлення/ }).click();
  const dlg = page.locator('.q-dialog').filter({ hasText: 'Нове направлення' });
  await expect(dlg).toBeVisible();
  // пацієнт: пошук
  const patient = dlg.locator('.q-select').first();
  await patient.locator('input').fill('Ко');
  await page.locator('.q-menu .q-item').first().click();
  // профіль
  await pickOption(page, dlg.locator('.q-select').filter({ hasText: 'Профілі' }), /./);
  await page.keyboard.press('Escape');
  await expect(dlg.locator('text=План пробірок').or(dlg.locator('text=Автопідбір пробірок'))).toBeVisible();
  await dlg.getByRole('button', { name: /Створити замовлення/ }).click();
  await expectNotify(page, /створено/i);
  await page.waitForURL(/\/laboratory\/orders\/[^/]+/);
  orderUrl = page.url();
  await expect(page.getByTestId('orderCard')).toBeVisible();
  orderNumber = (await page.locator('.page-header__title span').first().innerText()).replace('Замовлення', '').trim();
  await page.getByTestId('tab-samples').click();
  const codes = await page.locator('[data-testid="orderCard"] .q-tab-panel .mono.text-weight-bold').allInnerTexts();
  codes.forEach(c => barcodes.push(c.trim()));
  expect(barcodes.length).toBeGreaterThan(0);
  await expect(page.getByTestId('orderStatus')).toContainText(/Нове/);
  // дії за роллю: COLLECT дозволено або ні — кнопка рендериться завжди
  await expect(page.getByTestId('action-COLLECT')).toBeVisible();
});

test('2. Медсестра забирає матеріал за чек-листом і друкує етикетку', async ({ page }) => {
  await page.goto(`/laboratory/phlebotomy?barcode=${barcodes[0]}`);
  await waitIdle(page);
  await switchRole(page, 'PHLEBOTOMIST');
  await expect(page.getByTestId('sampleCard')).toBeVisible();
  for (const bc of barcodes) {
    await page.getByTestId('scanInput').locator('input').fill(bc);
    await page.getByTestId('scanInput').locator('input').press('Enter');
    await waitIdle(page);
    await page.getByTestId('collectBtn').click();
    const dlg = page.getByTestId('collectDialog');
    for (const chk of ['chk-id', 'chk-fasting', 'chk-order', 'chk-mixing']) await dlg.getByTestId(chk).click();
    await dlg.getByTestId('collectConfirm').click();
    await expectNotify(page, /забрано/i);
  }
  await expect(page.getByTestId('labelPreview')).toBeVisible();
});

test('3. Кур’єр створює маніфест і приймає його з температурою', async ({ page }) => {
  await page.goto('/laboratory/logistics');
  await waitIdle(page);
  await switchRole(page, 'LOGISTICS_COURIER');
  await page.getByTestId('createManifestBtn').click();
  const dlg = page.getByTestId('manifestDialog');
  await dlg.getByTestId('courierName').locator('input').fill('E2E Кур’єр');
  await dlg.getByTestId('tempDispatch').locator('input').fill('4');
  const bcSelect = dlg.getByTestId('manifestBarcodes');
  for (const bc of barcodes) { await bcSelect.locator('input').fill(bc); await page.keyboard.press('Enter'); }
  await dlg.getByTestId('manifestSave').click();
  await expectNotify(page, /створено/i);
  await waitIdle(page);
  const row = page.locator('[data-testid="manifestsTable"] tbody tr').filter({ hasText: 'E2E Кур’єр' }).first();
  await expect(row).toBeVisible();
  await row.locator('button').filter({ has: page.locator('i:text("inbox")') }).click();
  const rdlg = page.getByTestId('receiveManifestDialog');
  await rdlg.getByTestId('tempReceipt').locator('input').fill('5,5');
  await rdlg.getByTestId('receiveManifestConfirm').click();
  await expectNotify(page, /прийнято/i);
});

test('4. Лаборант вводить результати на робочому столі (inline, Enter)', async ({ page }) => {
  await page.goto(`/laboratory/workstation?search=${encodeURIComponent(orderNumber)}`);
  await waitIdle(page);
  await switchRole(page, 'LAB_TECHNICIAN');
  const rows = page.locator('[data-testid^="wl-row-"]');
  await expect(rows.first()).toBeVisible();
  const n = await rows.count();
  for (let i = 0; i < n; i++) {
    const row = rows.nth(i);
    const status = await row.innerText();
    if (/Верифіковано|Відхилено/.test(status)) continue;
    await row.dblclick();
    const input = page.locator('[data-testid="inlineInput"] input, [data-testid="inlineDropdown"]').first();
    await expect(input).toBeVisible();
    const isDropdown = await page.locator('[data-testid="inlineDropdown"]').count();
    if (isDropdown) { await input.click(); await page.locator('.q-menu .q-item').first().click(); await page.keyboard.press('Enter'); } else { await input.fill(i === 0 ? '26,4' : '5,2'); await input.press('Enter'); }
    await waitIdle(page);
  }
  await expect(page.locator('[data-testid^="wl-row-"]').first()).not.toContainText('—');
});

test('5. Лікар верифікує з коментарем для критичного значення та реєструє CITO-дзвінок', async ({ page }) => {
  await page.goto('/laboratory/validation');
  await waitIdle(page);
  await switchRole(page, 'LAB_DOCTOR');
  const table = page.getByTestId('reviewTable');
  const rows = table.locator('tbody tr').filter({ hasText: orderNumber });
  const count = await rows.count();
  for (let i = 0; i < count; i++) {
    const row = table.locator('tbody tr').filter({ hasText: orderNumber }).first();
    await row.locator('[data-testid^="verify-"]').click();
    const dlg = page.getByTestId('verifyDialog');
    await expect(dlg).toBeVisible();
    await dlg.getByTestId('verifyComment').locator('textarea, input').first().fill('E2E: критичне значення підтверджено, лікаря повідомлено');
    await dlg.getByTestId('verifyConfirm').click();
    await expectNotify(page, /верифіковано/i);
    await waitIdle(page);
    // якщо з’явився діалог паніки — реєструємо дзвінок із read-back
    const panic = page.getByTestId('panicCallDialog');
    if (await panic.isVisible().catch(() => false)) {
      await panic.getByTestId('panicDoctor').locator('input').fill('Д-р Романенко');
      await panic.getByTestId('panicPhone').locator('input').fill('+380501112233');
      await panic.getByTestId('panicReadback').click();
      await panic.getByTestId('panicSave').click();
      await expectNotify(page, /зафіксовано/i);
    }
  }
  await page.getByTestId('tab-calls').click();
  await expect(page.getByTestId('callsTable')).toBeVisible();
});

test('6. Видача результатів, бланк та кабінет пацієнта', async ({ page }) => {
  await page.goto(orderUrl);
  await waitIdle(page);
  await switchRole(page, 'LAB_DOCTOR');
  const release = page.getByTestId('action-RELEASE');
  await expect(release).toBeVisible();
  if ((await release.getAttribute('data-allowed')) === '1') {
    await release.click();
    await page.locator('.q-dialog').getByRole('button', { name: /^Видати$/ }).click();
    await expectNotify(page, /видано/i);
    await waitIdle(page);
    await expect(page.getByTestId('orderStatus')).toContainText(/Видано/);
  }
  // бланк
  await page.getByTestId('reportVariant').click();
  await page.locator('.q-menu .q-item').filter({ hasText: 'Остаточний' }).click();
  await expect(page.locator('iframe[title="Попередній перегляд"]')).toBeVisible();
  await page.keyboard.press('Escape');
  // процес та аудит
  await page.getByTestId('tab-process').click();
  await expect(page.locator('.q-timeline')).toBeVisible();
  // портал
  await page.locator('button:has-text("more_horiz")').click().catch(() => {});
  await page.goto('/portal');
  await waitIdle(page);
  await page.locator('[data-testid="portalHome"] .q-list .q-item').first().click();
  await page.waitForURL(/\/portal\/[^/]+\/orders/);
  const item = page.locator(`[data-testid="portal-order-${orderNumber}"]`);
  await expect(item).toBeVisible();
  await item.click();
  await expect(page.getByTestId('portalResult')).toBeVisible();
});
