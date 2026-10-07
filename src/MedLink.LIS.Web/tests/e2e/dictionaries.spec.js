// CRUD smoke для кожного довідника: створити → побачити у таблиці → відредагувати → видалити.
// Потребує реального API (http://localhost:5055) з сідом; з mock API мутації — echo (тест перевіряє лише UI-потік).
const { test, expect } = require('@playwright/test');
const { waitIdle, expectNotify } = require('./helpers');

const stamp = Date.now().toString().slice(-6);
const DICTS = [
  { name: 'biomaterials', fill: { code: `E2EB${stamp}`, name: `E2E біоматеріал ${stamp}` } },
  { name: 'tube-types', fill: { code: `E2ET${stamp}`, name: `E2E пробірка ${stamp}`, colorCode: '#ff00aa' } },
  { name: 'method-types', fill: { code: `E2EM${stamp}`, name: `E2E методика ${stamp}` } },
  { name: 'analyzer-types', fill: { code: `E2EA${stamp}`, name: `E2E модель ${stamp}` } },
  { name: 'tests', fill: { code: `E2E${stamp}`, name: `E2E показник ${stamp}`, unit: 'ммоль/л' } },
  { name: 'profiles', fill: { code: `E2EP${stamp}`, name: `E2E послуга ${stamp}` } },
  { name: 'organisms', fill: { code: `E2EO${stamp}`, name: `E2E organism ${stamp}` } },
  { name: 'antibiotics', fill: { code: `E2EAB${stamp}`, name: `E2E антибіотик ${stamp}` } },
  { name: 'departments', fill: { code: `E2ED${stamp}`, name: `E2E відділення ${stamp}` } },
  { name: 'employees', fill: { fullName: `E2E Співробітник ${stamp}` }, select: { labRole: /Лаборант/ } }
];

for (const d of DICTS) {
  test(`довідник ${d.name}: create / edit / delete`, async ({ page }) => {
    await page.goto(`/laboratory/dictionaries/${d.name}`);
    await waitIdle(page);
    await expect(page.locator('[data-testid="dictTable"]')).toBeVisible();

    // create
    await page.getByTestId('dictCreate').click();
    const form = page.getByTestId(`dictForm-${d.name}`);
    await expect(form).toBeVisible();
    for (const [field, value] of Object.entries(d.fill)) {
      await form.getByTestId(`f-${field}`).locator('input').first().fill(value);
    }
    if (d.select) {
      for (const [field, text] of Object.entries(d.select)) {
        await form.getByTestId(`f-${field}`).click();
        await page.locator('.q-menu .q-item').filter({ hasText: text }).first().click();
      }
    }
    await form.getByTestId('dictSave').click();
    await expectNotify(page, /створено|оновлено/i);
    await waitIdle(page);

    // find the created row by search
    const searchValue = d.fill.code || d.fill.fullName;
    await page.getByTestId('dictSearch').locator('input').fill(searchValue);
    const row = page.locator('[data-testid="dictTable"] tbody tr').filter({ hasText: searchValue }).first();
    await expect(row).toBeVisible();

    // edit
    await row.locator('[data-testid^="edit-"]').click();
    await expect(form).toBeVisible();
    const nameField = form.getByTestId(d.fill.name ? 'f-name' : 'f-fullName').locator('input').first();
    await nameField.fill(`${d.fill.name || d.fill.fullName} (ред.)`);
    await form.getByTestId('dictSave').click();
    await expectNotify(page, /оновлено|створено/i);

    // delete with confirm
    await waitIdle(page);
    await page.getByTestId('dictSearch').locator('input').fill(searchValue);
    const row2 = page.locator('[data-testid="dictTable"] tbody tr').filter({ hasText: searchValue }).first();
    await expect(row2).toBeVisible();
    await row2.locator('[data-testid^="delete-"]').click();
    await page.locator('.q-dialog').getByRole('button', { name: /Видалити/ }).click();
    await expectNotify(page, /видалено/i);
  });
}

test('картка послуги: вкладки Головна / Показники / Норми / Лабораторія / Резолвер', async ({ page }) => {
  await page.goto('/laboratory/norms');
  await waitIdle(page);
  const card = page.locator('[data-testid^="profile-"]').first();
  await expect(card).toBeVisible();
  await card.click();
  const dlg = page.getByTestId('serviceCard');
  await expect(dlg).toBeVisible();
  for (const tab of ['sc-tests', 'sc-norms', 'sc-lab', 'sc-resolver', 'sc-main']) await dlg.getByTestId(tab).click();
  await dlg.getByTestId('sc-resolver').click();
  await page.getByTestId('rpResolve').click();
  await expect(page.getByTestId('rpResult')).toBeVisible();
  await expect(page.getByTestId('rpTrace')).toBeVisible();
});
