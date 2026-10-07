# MedLink LIS 4.0 — фронтенд (`src/MedLink.LIS.Web`)

Лабораторний модуль МІС **MedLink / evomis**. Стек строго як у `evomis/src/App.View`:
Quasar CLI v1 (`@quasar/app` 1.9.6, webpack 4) · Quasar Framework **1.15.3** · Vue **2.6.14** · Vue Router 3 · Vuex 3 · axios.
Токени evomis у `src/css/quasar.variables.styl` (`$primary #4274A7`, градієнт `#0178BC→#318F94→#5EC58C`, темний drawer `#212121`),
шрифт Source Sans Pro локально (`@fontsource/source-sans-pro`), іконки Material + Font Awesome 5 з `@quasar/extras` — **жодних CDN у рантаймі**.
Усі тексти UI — українською. API-контракт: `docs/API_CONTRACT.md`.

## Швидкий старт

```bash
cd src/MedLink.LIS.Web
npm install                 # Node 22: NODE_OPTIONS=--openssl-legacy-provider задано у скриптах через cross-env
npm run dev                 # quasar dev → http://localhost:8080, проксі /api, /health, /verify → http://localhost:5055
npm run build               # dist/spa
```

`package.json` містить `overrides` для двох пакетів, що не збираються на Node 22 і не використовуються в рантаймі
(`zlib` → `browserify-zlib`, `node-sass` → `sass`; стилі — stylus).

### Збірка у wwwroot API
З кореня репозиторію: `./build.sh` (Linux/macOS) або `.\build.ps1` (Windows) — `npm install`, `quasar build`,
очищення `src/MedLink.LIS.Api/wwwroot/` (крім `.gitkeep`) і копіювання `dist/spa/*`. Вміст `wwwroot` у git не комітиться (`.gitignore`).

### Smoke без бекенду
```bash
node tools/mock-api.js 5099                         # mock API /api/v1/lab/* (демо-дані, мутації — echo)
API_TARGET=http://localhost:5099 node tools/serve-dist.js 8086   # статика dist/spa + проксі /api
node tools/serve-dist.js 8085                       # без API: /api → 502 → у SPA банер «API недоступне»
```

## Контекст користувача: `X-MedLink-Employee-Id`
Автентифікації **немає** (розділ 0 контракту). Поточний співробітник обирається у хедері («Працюю як…»),
зберігається у Vuex `context.employeeId` (+ `localStorage`) і додається до кожного запиту інтерсептором `src/boot/axios.js`:

```
X-MedLink-Employee-Id: <org_employee.id>
```

Якщо заголовка немає — сервер підставляє `Lab:DefaultEmployeeId`. Ролі (`LAB_ADMIN`, `LAB_DOCTOR`, `LAB_TECHNICIAN`,
`PHLEBOTOMIST`, `LOGISTICS_COURIER`, `REGISTRAR`) — довідкові: UI рендерить кнопки дій за `allowedActions`
(DTO замовлення / worklist або `GET /orders/{id}/transitions`), недозволені — вимкнені з підказкою; 403 ProblemDetails
від API показується у сповіщенні (`error.userMessage`).

## Структура
```
src/
  boot/            axios.js (інтерсептори), components.js (глобальні компоненти/фільтри), fonts.js
  css/             quasar.variables.styl (токени evomis), app.styl (глобальні стилі: header, drawer, картки, таблиці…)
  layouts/         baseLayout/BaseLayout.vue (хедер, перемикач співробітника, індикатори), portalLayout/
  components/      baseElements/menuDrawer.vue · common/ (StatusChip, FlagMarker, BarcodeSvg, PageHeader, ConfirmDialog,
                   EmptyState, ApiErrorBanner, LabelSticker, LabelsDialog, HtmlPreviewDialog, PatientSelect, JsonDiffViewer)
                   orders/ (OrderStepper, OrderActionsBar, AuditTimeline, OrderMatrix) · samples/ · results/ · charts/
  pages/laboratory/  дашборд, orders/ (черга, картка, створення, матриця), пункт забору, логістика, workstation/,
                   validation/, qc/, біобанк, реагенти, microbiology/, TAT, norms/ (каталог послуг, картка, резолвер),
                   analyzers/ (коннектори, аналізатори, журнал обміну, симулятор), sections/ (підрозділи, журнал), samples/
  pages/dictionaries/ dictionarySchemas.js (схема → форма) · DictionaryCrudTable.vue · DictionaryFormDialog.vue
  pages/admin/     налаштування (принтер етикеток, бланки, retention), співробітники, аудит
  pages/portal/    кабінет пацієнта (вибір пацієнта, список зі степером/прогресом, результат, тренд SVG, сповіщення, /verify)
  router/          routes.js, laboratoryRoutes.js, portalRoutes.js
  store/modules/   context.js (employeeId/employees/lab/labSettings), laboratory.js (зведення, коннектори, polling 30 с), dictionaries.js (кеш)
  services/        labApiService.js (одна функція на ендпоінт), http.js, printAgentService.js (localhost:5088)
  mixins/          apiMixin.js (стан завантаження/помилок), labelPrintMixin.js («Друк етикетки» → агент → fallback SVG)
  utils/           format.js (dd.MM.yyyy HH:mm, десяткова кома), statuses.js (кольори/підписи статусів і прапорців), orderActions.js
tools/             mock-api.js, serve-dist.js
tests/e2e/         Playwright: ui-smoke, dictionaries (CRUD), process (ролі end-to-end), matrix-processing
```

## E2E (Playwright)
```bash
npm run e2e:mock      # ui-smoke на mock API (піднімає tools/mock-api.js + serve-dist автоматично)
npm run e2e           # усі спеки проти API на http://localhost:5055 (API віддає wwwroot) — потрібен бекенд із сідом
E2E_BASE=http://localhost:8080 npm run e2e   # проти quasar dev
```
Браузери: `PLAYWRIGHT_BROWSERS_PATH`/`CHROME` (executablePath) — див. `playwright.config.js`.

## Перенесення в evomis (`evomis/src/App.View`)
1. Скопіювати `src/pages/laboratory`, `src/pages/dictionaries`, `src/pages/admin`, `src/pages/portal`,
   `src/components/{common,orders,samples,results,charts}`, `src/mixins`, `src/utils`,
   `src/store/modules/laboratory.js` (+ `context.js`, `dictionaries.js` або змапити на існуючі модулі evomis),
   `src/services/labApiService.js`, `src/services/http.js`, `src/services/printAgentService.js`.
2. Зареєструвати `src/router/laboratoryRoutes.js` у `routes.js` МІС (масив маршрутів під `/laboratory`, layout замінити на evomis `BaseLayout`),
   додати `portalRoutes.js` за потреби.
3. Додати у `components/baseElements/menuDrawer.vue` evomis групи з цього `menuDrawer.vue` («Процеси», «Довідники», «Інтеграції», «Адміністрування»).
4. У boot-файлі axios evomis додати інтерсептор заголовка `X-MedLink-Employee-Id` (у бойовому evomis — із сесії IdentityServer) та
   глобальну реєстрацію компонентів із `src/boot/components.js`.
5. `process.env.API_BASE` → `/api/v1/lab` (quasar.conf.js → `build.env`).

## Локальний агент друку етикеток
Кнопки «Друк етикетки» (пункт забору, картка замовлення/проби, обробка зразка) беруть ZPL з `GET /samples/{barcode}/label`
і надсилають `POST http://localhost:5088/print/zpl { zpl, printer }` агенту у складі MedLink LabConnector; принтер — із
`GET /settings/lab` (`labelPrinterHost/labelPrinterPort` або `labelPrinterName`). Якщо агент недоступний — сповіщення
«Агент друку не запущено на цьому ПК — встановіть MedLink LabConnector або надрукуйте SVG» та fallback на друк SVG.
«Тестовий друк» на сторінці налаштувань → `POST /print/test`.
