// Процес PO: матриця призначень → прийом → розділення (аліквоти) → етапи → результати → прогресивний портал.
// Потребує реального API з ендпоінтами /dictionaries/order-matrix, /samples/{barcode}/split|stages|tree, /sections.
const { test, expect } = require('@playwright/test');
const { switchRole, waitIdle, expectNotify } = require('./helpers');

test.describe.configure({ mode: 'serial' });

let orderUrl = '';
let barcode = '';
let patientId = '';

test('1. Створення замовлення через матрицю призначень', async ({ page }) => {
  await page.goto('/laboratory/order-matrix');
  await waitIdle(page);
  await switchRole(page, 'REGISTRAR');
  const patient = page.locator('.q-select').first();
  await patient.locator('input').fill('Ко');
  await page.locator('.q-menu .q-item').first().click();
  const matrix = page.getByTestId('orderMatrix');
  await expect(matrix).toBeVisible();
  const rows = matrix.locator('[data-testid^="matrix-"]');
  await expect(rows.first()).toBeVisible();
  await rows.nth(0).click();
  await rows.nth(1).click();
  await expect(page.getByTestId('matrixCount')).toContainText('2');
  await expect(page.getByTestId('matrixTotal')).not.toContainText('0,00');
  await page.getByTestId('matrixSubmit').click();
  await expectNotify(page, /створено/i);
  await page.waitForURL(/\/laboratory\/orders\/[^/]+/);
  orderUrl = page.url();
  await page.getByTestId('tab-samples').click();
  barcode = (await page.locator('[data-testid="orderCard"] .q-tab-panel .mono.text-weight-bold').first().innerText()).trim();
  expect(barcode).toMatch(/\d{6,}/);
});

test('2. Забір і прийом проби з картки замовлення', async ({ page }) => {
  await page.goto(orderUrl);
  await waitIdle(page);
  await switchRole(page, 'PHLEBOTOMIST');
  await page.getByTestId('action-COLLECT').click();
  const dlg = page.getByTestId('collectDialog');
  for (const chk of ['chk-id', 'chk-fasting', 'chk-order', 'chk-mixing']) await dlg.getByTestId(chk).click();
  await dlg.getByTestId('collectConfirm').click();
  await expectNotify(page, /забрано/i);
  await switchRole(page, 'LAB_TECHNICIAN');
  await waitIdle(page);
  await page.getByTestId('action-RECEIVE').click();
  await page.getByTestId('receiveDialog').getByTestId('receiveConfirm').click();
  await expectNotify(page, /прийнято/i);
});

test('3. Обробка зразка: дерево, розділення на аліквоти, етапи', async ({ page }) => {
  await page.goto(`/laboratory/samples/${barcode}`);
  await waitIdle(page);
  await switchRole(page, 'LAB_TECHNICIAN');
  await expect(page.getByTestId('sampleTree')).toBeVisible();
  await page.getByTestId('splitBtn').click();
  const dlg = page.getByTestId('splitDialog');
  await dlg.getByTestId('splitCount').locator('input').fill('2');
  await dlg.getByTestId('splitConfirm').click();
  await expectNotify(page, /дочірніх зразків/i);
  await waitIdle(page);
  await expect(page.getByTestId('sampleTree').locator('.q-tree__node')).toHaveCount(3, { timeout: 10000 });
  // етап
  const stage = page.getByTestId('stageSelect');
  if (await stage.isVisible().catch(() => false)) {
    await page.getByTestId('stageConfirm').click();
    await expectNotify(page, /зафіксовано/i);
  }
});

test('4. Результати та прогресивний кабінет пацієнта', async ({ page }) => {
  await page.goto(orderUrl);
  await waitIdle(page);
  await switchRole(page, 'LAB_TECHNICIAN');
  await page.getByTestId('tab-tests').click();
  const firstRow = page.locator('[data-testid^="test-row-"]').first();
  await firstRow.dblclick();
  const editor = page.getByTestId('resultEditor');
  await expect(editor).toBeVisible();
  if (await editor.getByTestId('reportEditor').isVisible().catch(() => false)) {
    await editor.getByTestId('reportEditor').locator('textarea').nth(2).fill('E2E: висновок патогістолога');
  } else if (await editor.getByTestId('resultNumeric').isVisible().catch(() => false)) {
    await editor.getByTestId('resultNumeric').locator('input').fill('5,4');
  } else {
    await editor.getByTestId('resultDropdown').click(); await page.locator('.q-menu .q-item').first().click();
  }
  await editor.getByTestId('resultSave').click();
  await expectNotify(page, /збережено/i);
  await switchRole(page, 'LAB_DOCTOR');
  await waitIdle(page);
  await page.getByTestId('verifyBatch').click();
  await waitIdle(page);
  // портал: часткова готовність
  const portalLink = page.locator('.q-btn-dropdown').last();
  await portalLink.click();
  await page.locator('.q-menu .q-item').filter({ hasText: 'Кабінет пацієнта' }).click();
  await page.waitForURL(/\/portal\/([^/]+)\/orders/);
  patientId = page.url().match(/\/portal\/([^/]+)\/orders/)[1];
  await expect(page.locator('[data-testid^="portal-progress-"]').first()).toBeVisible();
  await page.locator('[data-testid^="portal-order-"]').first().click();
  await expect(page.getByTestId('portalResultProgress')).toBeVisible();
  expect(patientId).toBeTruthy();
});
