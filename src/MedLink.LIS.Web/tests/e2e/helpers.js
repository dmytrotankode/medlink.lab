/**
 * Допоміжні функції E2E: перемикання «Працюю як…», очікування завантаження, вибір у q-select.
 */
const { expect } = require('@playwright/test');

const ROLE_NAMES = {
  REGISTRAR: /Реєстратор/i,
  PHLEBOTOMIST: /Медсестра|пункту забору/i,
  LOGISTICS_COURIER: /Кур/i,
  LAB_TECHNICIAN: /лаборант/i,
  LAB_DOCTOR: /Лікар-лаборант/i,
  LAB_ADMIN: /Адміністратор/i
};

/** Перемикає співробітника у хедері за роллю (підпис ролі в опції селекта). */
async function switchRole (page, role) {
  const select = page.locator('.header-employee');
  await select.click();
  const option = page.locator('.q-menu .q-item').filter({ hasText: ROLE_NAMES[role] }).first();
  await expect(option).toBeVisible();
  await option.click();
  await page.waitForTimeout(400);
}

/** Обирає опцію q-select за текстом */
async function pickOption (page, selectLocator, text) {
  await selectLocator.click();
  const opt = page.locator('.q-menu .q-item').filter({ hasText: text }).first();
  await expect(opt).toBeVisible();
  await opt.click();
}

/** Чекає, поки зникнуть індикатори завантаження таблиць */
async function waitIdle (page) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(300);
}

async function expectNotify (page, text) {
  await expect(page.locator('.q-notification').filter({ hasText: text }).first()).toBeVisible({ timeout: 8000 });
}

module.exports = { switchRole, pickOption, waitIdle, expectNotify };
