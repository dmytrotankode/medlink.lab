# Модуль «Інструкції» (MedLink Guide) — інтерактивні покрокові інструкції

Окремий плагін до будь-якої системи, інтегрований у ЛІС. Не залежить від `LisDbContext` і Quasar.

| Частина | Шлях |
|---|---|
| БД PostgreSQL (схема `guide`, таблиці `gd_*`) | `db/postgres/guide/001_guide_schema.sql` |
| Бекенд (.NET 8 бібліотека, EF Core, API `/api/v1/guide`) | `src/MedLink.Guide` (README — підключення, заголовки, конфіг `Guide:*`) |
| Фронтенд SDK + конструктор + плеєр (чистий JS, Shadow DOM) | `src/MedLink.Guide.Web` (README — API `init`, якорі, конструктор) |
| Автономний демо-хост | `src/MedLink.Guide.DemoHost` → `dotnet run`, http://localhost:5066 |
| Тести | `tests/MedLink.Guide.Tests` (`dotnet test tests/MedLink.Guide.Tests`) |

## Модель даних
`gd_app` (система-хост: `lab`, `evomis`…) → `gd_folder` (дерево розділів) → `gd_scenario` (інструкція; чернетка = `gd_step`, публікація = знімок `gd_scenario_version`) → `gd_step` ← `gd_anchor` (переюзні якорі зі стратегіями пошуку, відбитком і станом здоров'я). `gd_scenario_variable` — тестові дані (`{{змінні}}`), `gd_media` — аудіо/скріншоти, `gd_progress` — проходження, `gd_anchor_report` — телеметрія «не знайдено / знайдено за схожістю».
Конвенції ЛІС: snake_case, uuid, аудит-колонки, `record_state` 2/4, `tenant_id` = `organization_id` (NULL — для всіх).

## Інтеграція в ЛІС (виконано)
- `MedLink.LIS.Api.csproj` — `ProjectReference` на `MedLink.Guide`; `Program.cs` — `AddGuide` (SQLite `App_Data/medlink_guide.db`, `AllowAnonymousEditing`), `EnsureGuideDatabaseAsync`, `SeedGuideBundlesAsync`, `MapGuide`.
- Web: `src/boot/guide.js` (init SDK, навігація через vue-router, заголовки з Vuex), `quasar.conf.js` (boot + alias `@guide`), `BaseLayout.vue` (`#guide-help-slot` — кнопка «?» у шапці).
- На PostgreSQL: у `AddGuide` замінити `UseSqlite` на `UseNpgsql(connectionString)`; схема створюється вбудованим SQL або застосуванням `001_guide_schema.sql`. Для evomis `DefaultTenantId` = `organization_id` з claims, `EditorRoles` — ролі впроваджувачів.

## Типовий процес впроваджувача
1. Відкрити потрібний екран ЛІС → «?» → «＋ Конструктор».
2. 🎯 Наводить на елемент (↑/↓ — обрати батьківський блок), клікає; або ⏺ записує реальні дії.
3. Пише/диктує пояснення, задає тестові дані (`{{patient}}`), зону підсвічування.
4. ▶ Перевіряє, 💾 зберігає, 🚀 публікує. Користувачі бачать інструкцію в дереві «?», вибір → система сама переходить на екран і веде по кроках (⚡ — демо-режим).
5. Якщо інтерфейс змінився й якір не знайдено — звіт потрапляє в `GET /apps/lab/anchors/health`, автор приймає виправлення без перепублікації.

## Готові інструкції ЛІС (сід `src/MedLink.Guide/Seed/lab_processes.json`)
Генерується скриптом `python tools/guide/gen_lab_processes.py` (селектори — `data-testid` зі `src/MedLink.LIS.Web`). Сід ідемпотентний за кодами; нова версія публікується, лише якщо вміст змінився. Для завантаження без перезапуску: `POST /api/v1/guide/apps/lab/import`.

| Розділ | Інструкція | Початковий екран |
|---|---|---|
| Початок роботи | Навігація: бічне меню ЛІС | `/laboratory/dashboard` |
| Початок роботи / Реєстрація направлень | Пошук за штрихкодом у шапці; Створення нового направлення | дашборд / `/laboratory/orders` |
| Преаналітика | Забір біоматеріалу за чек-листом | `/laboratory/phlebotomy` |
| Аналітика та результати | Введення результатів на робочому столі лаборанта | `/laboratory/workstation` |
| Верифікація та критичні значення | Верифікація результатів; Реєстрація повідомлення про критичний результат | `/laboratory/validation` |
| Інтеграція з МІС | Розподіл вхідного направлення з МІС | `/laboratory/refs` |
| Контроль якості | Внесення точки контролю якості (ВКЯ) | `/laboratory/quality-control` |

Кроки, що з'являються лише після дії (діалоги, рядки з даними), позначені `optional` — якщо елемента немає, крок пропускається; обов'язкові кроки показують попередження з кнопкою «Пропустити».
