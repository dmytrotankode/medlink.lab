// @ts-check
/**
 * Playwright E2E для MedLink LIS 4.0.
 *  E2E_BASE   — адреса SPA (за замовчуванням http://localhost:5055 — API віддає wwwroot; або quasar dev http://localhost:8080)
 *  E2E_MOCK=1 — підняти mock API (tools/mock-api.js) + статичний сервер dist/spa і ганяти ui-smoke без бекенду
 *  CHROME     — executablePath Chromium (напр. /opt/pw-browsers/chromium-1194/chrome-linux/chrome), якщо браузери не встановлені стандартно
 */
const { defineConfig, devices } = require('@playwright/test');

const mock = process.env.E2E_MOCK === '1';
const baseURL = process.env.E2E_BASE || (mock ? 'http://localhost:8086' : 'http://localhost:5055');

module.exports = defineConfig({
  testDir: './tests/e2e',
  timeout: 60000,
  expect: { timeout: 10000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  use: {
    baseURL,
    locale: 'uk-UA',
    viewport: { width: 1440, height: 900 },
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    launchOptions: process.env.CHROME ? { executablePath: process.env.CHROME } : {}
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: mock
    ? [
        { command: 'node tools/mock-api.js 5099', port: 5099, reuseExistingServer: true },
        { command: 'API_TARGET=http://localhost:5099 node tools/serve-dist.js 8086', port: 8086, reuseExistingServer: true }
      ]
    : undefined
});
