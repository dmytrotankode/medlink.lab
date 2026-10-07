// UI smoke: усі маршрути відкриваються без помилок JS (працює і з mock API: E2E_MOCK=1, і з реальним).
const { test, expect } = require('@playwright/test');

const ROUTES = [
  ['Дашборд', '/laboratory/dashboard', /Дашборд лабораторії/],
  ['Замовлення', '/laboratory/orders', /Реєстрація направлень/],
  ['Матриця', '/laboratory/order-matrix', /Матриця призначень/],
  ['Пункт забору', '/laboratory/phlebotomy', /Пункт забору/],
  ['Логістика', '/laboratory/logistics', /Логістика зразків/],
  ['Робочий стіл', '/laboratory/workstation', /Робочий стіл лаборанта/],
  ['Валідація', '/laboratory/validation', /Валідація та паніка/],
  ['ВКЯ', '/laboratory/quality-control', /контроль якості/i],
  ['Біобанк', '/laboratory/biobank', /Біобанк/],
  ['Реагенти', '/laboratory/reagents', /Склад реагентів/],
  ['Мікробіологія', '/laboratory/microbiology', /Мікробіологія/],
  ['TAT', '/laboratory/analytics-tat', /Аналітика TAT/],
  ['Норми', '/laboratory/norms', /Довідник послуг/],
  ['Довідники', '/laboratory/dictionaries', /Довідники лабораторії/],
  ['Показники', '/laboratory/dictionaries/tests', /Показники/],
  ['Аналізатори', '/laboratory/analyzers', /Аналізатори та коннектори/],
  ['Журнал обміну', '/laboratory/analyzers/messages', /Журнал обміну/],
  ['Імпорт', '/laboratory/import', /Імпорт результатів/],
  ['Підрозділи', '/laboratory/sections', /Підрозділи лабораторії/],
  ['Журнал відділення', '/laboratory/sections/journal', /Журнал відділення/],
  ['Налаштування', '/laboratory/admin/settings', /Налаштування лабораторії/],
  ['Співробітники', '/laboratory/admin/users', /Співробітники/],
  ['Аудит', '/laboratory/audit', /Аудит/],
  ['Портал', '/portal', /Кабінет пацієнта/]
];

for (const [name, route, heading] of ROUTES) {
  test(`сторінка «${name}» відкривається`, async ({ page }) => {
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    page.on('console', m => { if (m.type() === 'error' && !/Failed to load resource|status code|net::ERR/.test(m.text())) errors.push(m.text()); });
    await page.goto(route);
    await page.waitForLoadState('networkidle');
    await expect(page.locator('body')).toContainText(heading);
    expect(errors, `JS errors on ${route}`).toEqual([]);
  });
}

test('хедер: перемикач «Працюю як…», індикатори шлюзу та паніки', async ({ page }) => {
  await page.goto('/laboratory/dashboard');
  await page.waitForLoadState('networkidle');
  await expect(page.locator('.header-employee')).toBeVisible();
  await expect(page.locator('.header-chip').filter({ hasText: /Шлюз/ })).toBeVisible();
  await expect(page.locator('.header-chip').filter({ hasText: /Паніка/ })).toBeVisible();
  await expect(page.locator('.q-drawer').first()).toBeVisible();
  await expect(page.locator('.q-drawer')).toContainText('Робочий стіл лаборанта');
});
