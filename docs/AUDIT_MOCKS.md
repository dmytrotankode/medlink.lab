# Аудит: що в системі працює не по-справжньому з базою

Дата: 2026-11-09. Режим: лише читання коду (збірка і запуск не виконувались).
Охоплено: `src/MedLink.LIS.Api`, `src/MedLink.LIS.Web/src`, `db/`, `tests/`, коннектор і Core (поверхнево).

Критичність:
- **К** — підміна реальних даних (екран/метод віддає не те, що в БД);
- **С** — зовнішня інтеграція в режимі імітації (працює, але нічого назовні не робить);
- **Н** — безобидно / сид / навмисна можливість.

## Сводка

| Клас | Кількість | Суть |
|---|---|---|
| К | 1 | Визначення «поточного співробітника» без автентифікації (за замовчуванням адміністратор); портал пацієнта без перевірки особи |
| С | 8 | ЕСОЗ у режимі Mock (клієнт, сесія, КЕП, відправка звіту, словник інтервенцій), черга сповіщень без відправника, події для МІС без доставки, підпис-віджет |
| Н | 9 | Симулятор повідомлень приладу, резервний парсер, сидери (довідники та демо-дані), `Math.random` для UUID, таймери опитування, тестовий друк |
| SQLite-залежності | 10 | Див. розділ 4 |

Головне: **екранів із захардкодженими масивами даних у фронті немає** — усе читається через API (`labApiService.js`). Підміна даних на фронті відсутня; її немає і в сервісах API, що повертали б константи замість БД. Невеликі «підроблені» об'єкти є лише для попереднього розрахунку (`BillingService`, прев'ю ціни). Основні проблеми — **режим Mock для ЕСОЗ** (залежить від нього підпис і відправка звіту), **відсутній відправник сповіщень**, **відсутня автентифікація** і **жорстка прив'язка до SQLite** (PostgreSQL у проєкті не підключено).

## 1. Фронт (Vue2/Quasar): моки, заглушки, демо

Пошук по `mock|stub|fake|заглушк|демо|demo|simul|TODO|Math.random|setTimeout|setInterval` у `pages`, `components`, `store`, `services`, `boot`, `utils`.

| Файл:рядок | Що це | Де проявляється | Крит. |
|---|---|---|---|
| `components/orders/EhealthSubmitPanel.vue` | У режимах Mock/Replay підпис створює сервер (`POST /orders/{id}/ehealth/mock-sign`): структурно коректний CAdES-X Long із вкладеним пакетом, але **не криптографічний**; у Live кнопка підпису повідомляє, що віджет КЕП не підключено | Картка замовлення → відправка/анулювання звіту в ЕСОЗ. Справжнього КЕП (JS-віджет, ключ) немає | С |
| `components/common/EhealthSessionChip.vue:11,32,39` | Показує «тестовий режим», тестовий сертифікат, підказку з кодом 2FA | Шапка застосунку, вхід в ЕСОЗ | С |
| `pages/portal/PortalHome.vue:7,33` | Банер «Демо-режим без автентифікації», список усіх пацієнтів («Пацієнти (демо)») | Портал пацієнта: будь-хто бачить результати будь-якого пацієнта | К |
| `store/modules/context.js:5-60` + `boot/axios.js` | «Працюю як…»: співробітник вибирається у `localStorage`, іде заголовком `X-MedLink-Employee-Id` | Усі екрани: ролі, права, підписи в аудиті | К |
| `pages/laboratory/analyzers/AnalyzersPage.vue:42,124,128-161`, `services/labApiService.js:165` | Симулятор повідомлення приладу (парсить вручну введений текст, нічого не зберігає) | Аналізатори → «Симулятор» | Н |
| `pages/laboratory/norms/ResolverPlayground.vue` | «Симулятор підбору норми» — виклик реального API каскаду норм | Норми | Н |
| `pages/laboratory/mis/IncomingReferralsPage.vue:320` | `Math.random` лише як запасний варіант `crypto.randomUUID` | Створення направлення МІС | Н |
| `pages/admin/LabSettings.vue:71,146`, `services/printAgentService.js` | «Тестовий друк» на локальний агент `http://localhost:5088`; без агента — повідомлення «агент не запущено» | Налаштування лабораторії, друк етикеток | Н |
| `store/modules/laboratory.js:62`, `LabDashboard`, `AnalyzerMessages.vue:92`, `PathologyCasesPage.vue:140`, `EhealthSessionChip.vue:74`, `CreateOrderDialog.vue:336,341`, `utils/format.js:129` | `setInterval`/`setTimeout` — опитування API та debounce, не імітація | Дашборд, повідомлення приладів, патоморфологія | Н |
| `components/baseElements/menuDrawer.vue`, `dictionarySchemas.js`, `*Columns`, `statusOptions`, `referralTypeOptions` | Статичні масиви: пункти меню, колонки таблиць, схеми форм довідників, переліки статусів | Скрізь; це метадані інтерфейсу, не дані | Н |

Сторінки без прямого `$api`: `EmployeesAdmin`, `DictionariesHome`, `DictionaryPage` (працюють через `dictionary-crud-table`/store), `WorklistDetail` (беремо `allowedActions` з API, поле `source: 'api'`) — підміни немає.

## 2. API: заглушки, режими імітації, TODO

`TODO`, `NotImplemented`, `FIXME` у коді API, Core, коннектора — **не знайдено**. Знайдено такі місця:

| Файл:рядок | Що це | Сценарій / екран | Крит. |
|---|---|---|---|
| `appsettings.json` (секція `Ehealth`) | `Ehealth:Mode = "Mock"` типово; режими Mock \| Replay \| Live; у Live кожна операція вимкнена прапорцем `Ehealth:Live:Operations:*`, доки її не ввімкнено; токена/ключа в конфігурації немає | Усе, що стосується ЕСОЗ | С |
| `Services/Ehealth/EhealthMockHandler.cs`, `EhealthReplayHandler.cs` | Mock — двійник контракту OpenAPI в пам'яті (реєстр направлень, звітів, job; валідація пакета й підпису; ін'єкція збоїв для тестів), а не дзеркало БД; демо-направлення з дзеркала копіюються в реєстр лише при першому зверненні (`Ehealth:Mock:SeedFromMirror`); стан втрачається при перезапуску. Replay відповідає фікстурами `tests/MedLink.LIS.Tests/Fixtures/ehealth/*.json` | Перевірка направлення в реєстрі, взяття в роботу, відправка звіту, погашення, звільнення | С |
| `Services/Ehealth/EhealthClient.cs` | Клієнт за OpenAPI: шляхи `/api/...`, id job з `links`, класифікація 401/403/404/409/422/5xx, повтори лише для GET. У Live потрібен `AccessToken` з конфігурації; механізму отримання/оновлення токена (вхід користувача, OAuth) немає. **Live на preprod не перевірявся** | Перехід на Live | С |
| `Services/Ehealth/EhealthSignService.cs` | У Live сесійні методи (вхід L1/L2) повертають 503 `ehealth-live-oauth-not-implemented`; prepare/submit/retry/annul працюють з токеном із конфігурації, але віджета КЕП у браузері немає | Вхід в ЕСОЗ (L1/L2), підпис, відправка | С |
| `EhealthSignService.cs` (`DemoTotpCode`) | Код 2FA захардкоджений `123456`, TOTP-секрет `DEMODEMODEMO` | Підтвердження входу | С |
| `EhealthSignService.cs` (`TestCertificateAsync`, `CheckSignerAsync`), `Ehealth/EhealthSignature.cs` | Тестовий сертифікат КЕП: ДРФО з картки персони або детермінований номер, серійний `DEMO-xxxx-KEP`; `CheckSignerAsync` порівнює ДРФО з очікуваним. Підпис перевіряється **структурно** (`CadesInspector`: CMS SignedData, CAdES-X Long, збіг вкладеного вмісту з пакетом); криптографічну чинність перевіряє лише ЕСОЗ | Підпис КЕП при відправці/анулюванні | С |
| `Services/Ehealth/EhealthOutbox.cs` | Відправка — задача `ehe_outbox` і фоновий воркер: реальні виклики клієнта (у Mock/Replay — до двійника/фікстур), опитування job, витримки, відновлення після збою. `SimulateNetworkError` лише ставить у Mock збій 503, щоб показати повтор | Картка замовлення → «Відправити в ЕСОЗ», «Відправка в ЕСОЗ» | С (Mock/Replay) |
| `Services/MisCatalogService.cs:132-143` | `SyncAsync` лише оновлює `SyncedOn` у всіх записах і повертає `source = "MOCK"`; реального завантаження словника інтервенцій немає | МІС → Каталог інтервенцій → «Синхронізувати» | К |
| `Data/Seed/MisCatalogSeeder.cs:97,124-157` | Словник інтервенцій — демо (`DEMO-…`, `SourceVersion = demo-2026.10`), демо-прилади без коннектора | Каталог «Послуга ЕСОЗ × аналізатори», маршрутизація | Н (сид) |
| `Services/OrderService.cs:464-469`, `Services/ProgressiveReleaseService.cs:39-48` | Сповіщення пацієнтам створюються зі статусом `QUEUED`; **відправника (SMS/Email/Viber/Push) та фонової служби немає**, статус `SENT` і `SentAt` ніде не виставляються. Єдиний `BackgroundService` — `RetentionCleanupService` | Видача результатів, поетапна видача; `GET /portal/{patientId}/notifications` | С |
| `Services/MisReferralService.cs:266-270,305` | Зворотні події для МІС (`ResultReady`, `Completed`, …) лише пишуться в `lab_mis_event_log`; доставки назовні (webhook/API МІС) немає. `ListAsync` названо «емулятор МІС» | Направлення МІС, журнал подій | С |
| `Services/ReferralService.cs` | ЕСОЗ — джерело істини (пошук `GET /api/service_requests?requisition=`), дзеркало `ehe_incoming_medical_referral` — кеш; `use` лише для направлень із програмою; `complete` — окрема задача після реєстрації звіту; у Mock/Replay — двійник/фікстури | Вхідні е-направлення | С |
| `Controllers/OrdersSamplesController.cs:78-85`, `MiscControllers.cs:160` | Якщо PDF (QuestPDF) недоступний — повертається HTML із заголовком `X-MedLink-Pdf-Fallback` | Друк бланків результатів | Н |
| `Infrastructure/CurrentEmployee.cs:49`, `Program.cs:185` (`CurrentEmployeeMiddleware`), `appsettings.json:21` | Автентифікації немає; без заголовка береться `Lab:DefaultEmployeeId` (адміністратор). Роль визначається з БД, але ідентичність нічим не підтверджена | Весь API, `PortalController` (`MiscControllers.cs:129-149`) | К |
| `Program.cs:133`, `Services/Parsing/AnalyzerMessageParserFallback.cs:44` | Локальний резервний парсер ASTM/HL7/TEXT «замінюється на Core.Protocols однією реєстрацією» — підміну не виконано | Прийом повідомлень приладів `ConnectorService`, симулятор | Н/С |
| `Services/AnalyzerService.cs:182`, `Controllers/AnalyzersConnectorsController.cs:38` | `POST analyzers/{id}/simulate` — розбір тексту без збереження | Симулятор | Н |
| `Services/BillingService.cs:197-209` | Тимчасовий `LabOrder` (`fake`) для попереднього розрахунку знижок/платника, у БД не зберігається | Прев'ю ціни при створенні замовлення | Н |
| `appsettings.json:24-26`, `Lab:VerifySecret = "change-me-…"` | Секрет перевірки бланків за QR — значення-заглушка; `PublicBaseUrl = http://localhost:5055` | QR на бланках результатів | С |
| `Program.cs:73` | CORS `AllowAnyOrigin` | Весь API | Н |

Поза API: коннектор (`src/MedLink.LabConnector`) — TCP/Serial сесії ASTM/HL7/TEXT реалізовані; «заглушка» лише в коментарі про джерело замовлень у тестах (`Sessions/SessionBase.cs:17`).

## 3. Сидери (Data/Seed)

`Program.cs:151-171`: при кожному старті послідовно `SeedService` → `MisCatalogSeeder` → `RecomputeReadiness` → (якщо `Lab:SeedDemoData`, типово **true**, також у `appsettings.json`) `DemoDataSeeder` → `PathologyDemoSeeder`. Усе ідемпотентне, «лише у порожні таблиці» (перевірка `AnyAsync` по кожній таблиці окремо).

| Сидер / ресурс | Що засівається | Екрани, які були б порожніми без сиду | Крит. |
|---|---|---|---|
| `SeedService.SeedOrgAsync` + `org.json` (3 підрозділи, 6 співробітників, 6 пацієнтів, 2 направлення, 6 послуг) і системні `cmn_enum_record` (стать, типи підрозділів, посади, статуси направлень) | Організація, співробітники і ролі, пацієнти, перелічення | **Обов'язково для роботи**: без співробітників неможливий «Працюю як…», без перелічень падають FK; співробітники/пацієнти в проді мають надходити з ядра МІС | Н (але залежність) |
| `settings.json` | Налаштування лабораторії, шаблони бланків | Налаштування, бланки | Н |
| `biomaterials`, `tube_types`, `method_types`, `sections` | Довідники біоматеріалів, тари, методик, секцій | Довідники, створення замовлення, план пробірок | Н (обов'язково) |
| `tests` (59), `profiles` (6), `reference_layers` (152), `reflex_rules` | Показники, профілі, каскад норм, рефлекс-правила | Створення замовлення, норми, валідація | Н (обов'язково) |
| `microbiology` | Мікроорганізми, антибіотики, пороги EUCAST | Мікробіологія | Н (обов'язково) |
| `analyzers` + `analyzer_types.json` (Core) | 2 аналізатори, 1 коннектор, типи приладів | Аналізатори, маршрутизація | Н |
| `performers`, `billing`, `qc_materials`, лічильники, `pathology` | Виконавці, прайс/платники/пакети, матеріали ВКЯ, нумератори, довідники патоморфології | Тарифікація, ВКЯ, патоморфологія | Н |
| `MisCatalogSeeder` + `mis_catalog.json` (23 тести, 9 профілів, 14 прив'язок аналізаторів, 25 інтервенцій, 3 пакети) | Демо-каталог «Послуга ЕСОЗ × аналізатори» | МІС → Каталог, маршрутизація, пакети | Н (демо) |
| `DemoDataSeeder` (`Data/Seed/DemoDataSeeder.cs:42-240`) | 8 замовлень різних станів (CITO, delta-check, рефлекс, вагітність, мікробіологія), паніка, маніфест, 3 серії ВКЯ, штативи біобанку, реагенти, 6 направлень МІС. Виконується лише коли `lab_order` порожня; для наявної БД дозасіває лише МІС-демо. Дані «зсуваються в минуле» (`Backdate`) для аналітики TAT | Дашборд, робочий лист, валідація/паніка, ВКЯ, біобанк, реагенти, логістика, аналітика TAT, МІС-журнал | Н (демо, але без нього вони порожні) |
| `PathologyDemoSeeder` | 6 справ патоморфології, 3 вхідні направлення | Патоморфологія | Н (демо) |
| `SeedGuideBundlesAsync` (`Program.cs:174`) | Інструкції Guide | Модуль Guide | Н |

Висновок: на реальному об'єкті слід вимкнути `Lab:SeedDemoData`, але лишити довідниковий сід (`SeedService`) або замінити його імпортом. Співробітників, пацієнтів, організацію з `org.json` у проді має давати ядро МІС, а не ЛІС.

## 4. Залежність від SQLite

| Файл:рядок | Що це | Що зламається на PostgreSQL | Крит. |
|---|---|---|---|
| `MedLink.LIS.Api.csproj:16` | Є лише `Microsoft.EntityFrameworkCore.Sqlite`; пакета `Npgsql.EntityFrameworkCore.PostgreSQL` немає | Немає провайдера | К |
| `Program.cs:18-28`, `appsettings*.json` (`Database:SqlitePath`) | `UseSqlite` захардкоджено; рядка підключення до PostgreSQL і вибору провайдера в конфігурації немає | Потрібен перемикач провайдера та connection string | К |
| `Program.cs:32-36` | Модуль Guide також `UseSqlite` (окремий файл); сама бібліотека `GuideDbContext` уже вміє Npgsql | Потрібен `UseNpgsql` + схема `guide` (`db/postgres/guide`) | С |
| `Program.cs:157` | `EnsureCreatedAsync()` — створення схеми з моделі, міграцій EF немає (`Migrations/` відсутня) | Схема на PG має створюватись скриптом (`db/postgres/medlink_lis_schema.sql`, `db/migrations/R1.1`), а не EnsureCreated; зміни моделі не накочуються | К |
| `Program.cs:159` | `ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;")` без перевірки провайдера | Помилка синтаксису при старті на PG | К |
| `Data/SchemaUpgrader.cs:21-80` | Дооновлення схеми через `sqlite_master`, `pragma_table_info`, `ALTER TABLE ADD COLUMN`, `GenerateCreateScript`; на не-SQLite повертає порожній результат (захист є), але **тоді змін схеми ніхто не виконує** | Додані в моделі колонки/таблиці не з'являться; потрібні SQL-міграції. Окрім того, `BackupIfIncompatibleAsync` перейменовує файл БД — для PG сенсу не має | К |
| `db/migrations/R1.1/` (001–007) | SQL-міграції зупинились на `007_ch22_pathology.sql` (02.11); `medlink_lis_schema.sql` згенеровано 08.11. Між ними — зміни моделі пізніше 007 (напр. ціни методик) без окремих скриптів | Немає впорядкованого журналу застосованих міграцій | С |
| `Services/NumeratorService.cs:46-67`, `SectionJournalService.cs:146-147` | Сирий SQL `UPDATE … SET current_value = current_value + 1` + окреме читання значення, **без транзакції**. У SQLite записи серіалізуються, у PG два паралельні запити можуть прочитати те саме значення (дубль номера) | Нумерація замовлень, маніфестів, партій, штрихкодів, журналів секцій | К |
| `Data/PostgresDdlGenerator.cs:114` + модель | Ключі в моделі — `string` (GUID як текст), у DDL для PG генеруються `uuid`. Без `HasColumnType("uuid")`/конвертера Npgsql передаватиме `text`-параметри → `operator does not exist: uuid = text`; те саме для `created_by/modified_by` | Усі запити за ключами | К |
| `Data/LisDbContext.cs:272-287` | Конвертер `DateTime → Utc` та `NUMERIC(18,2)` для decimal: на PG `timestamp without time zone` + `Kind=Utc` працює лише з legacy-режимом Npgsql (`Npgsql.EnableLegacyTimestampBehavior`), в DDL це заявлено, у коді не вмикається | Помилка запису дат з `Kind=Utc` у `timestamp` | С |
| `Services/*` (напр. `OrderService.cs:81`, `WorklistService.cs:100`, `PatientSettingsService.cs:26`, `MisCatalogService.cs:111`, `ReferralService.cs:232`, `WorkReportsService.cs:88`) | `Contains(s)` у запитах EF → `LIKE`; у SQLite нечутливе до регістру для ASCII, у PG — чутливе | Пошук за номером/ПІБ/кодом працюватиме по-іншому (потрібні `ILike`/`ToLower`) | Н |
| `BillingService.cs:93,155` та ін. | `.Date` для DateTime у фільтрах EF — на PG транслюється, але ряд запитів сума/групування `decimal` у SQLite виконується на клієнті; поведінку потрібно перевірити | Тарифікація, рахунки, звіти | Н |
| `tests/MedLink.LIS.Tests/*` (`ApiIntegrationTests.cs:22`, `PostgresDdlTests.cs:12`, `MedLinkAlignmentTests.cs`), `MedLink.Guide.Tests` | Усі тести працюють на SQLite (файл/в пам'яті); PostgreSQL у CI не перевіряється, `PostgresDdlTests` лише генерує скрипт | Немає гарантії працездатності на PG | С |
| `Dockerfile`, `docker-compose.yml`, `run.cmd` | Перевірити, що не монтується лише SQLite-том (не аналізувалось детально) | — | Н |

## Що потрібно зробити для переходу на PostgreSQL

1. Додати пакет `Npgsql.EntityFrameworkCore.PostgreSQL`; у конфігурацію — `Database:Provider` (`Sqlite`|`Postgres`) і `ConnectionStrings:Lis`; у `Program.cs` вибирати `UseSqlite`/`UseNpgsql` за провайдером (так само для Guide: `UseNpgsql` + схема `guide`).
2. Вирішити питання ключів: або задати колонкам `*_id`, `id`, `created_by`, `modified_by` тип `uuid` у моделі (`HasColumnType("uuid")` або `Guid`-конвертер), або генерувати DDL з `text`. Вибір має збігатися з `PostgresDdlGenerator`.
3. Увімкнути `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` (узгоджено з `timestamp without time zone`) або перейти на `timestamptz` з `Kind=Utc`; перевірити всі місця з `DateTime.Now`/`.Date`.
4. У `Program.cs` обгорнути SQLite-специфіку умовою провайдера: `PRAGMA journal_mode=WAL`, `BackupIfIncompatibleAsync`, `SchemaUpgrader.UpgradeAsync`. Для PG замінити `EnsureCreatedAsync()` на застосування SQL-скриптів/міграцій (перевірка наявності таблиць, версія схеми).
5. Ввести керований журнал міграцій: або EF Migrations (`dotnet ef migrations add`), або таблиця `schema_version` + скрипти з `db/migrations`; дописати міграції для змін моделі після `007` (звірити модель з `medlink_lis_schema.sql`). Перевірити, що `db/postgres/medlink_lis_schema.sql` перегенеровано після останніх змін.
6. Переписати нумератори (`NumeratorService`, `SectionJournalService`): один атомарний `UPDATE … RETURNING current_value` (або sequence) у транзакції, з повтором при колізії унікального індексу штрихкоду.
7. Замінити пошук `Contains` на нечутливий до регістру (`EF.Functions.ILike`) там, де користувач очікує пошук без урахування регістру; перевірити агрегати `decimal` і порядок сортування рядків (collation PG відрізняється від SQLite, особливо для кирилиці).
8. Не вимагати `org_*`/`cmn_*`/`mis_*`/`ehe_*` від сидера у проді: ці таблиці вже існують у ядрі МІС. Вимкнути `Lab:SeedDemoData`; довідниковий сід (`SeedService`) виконувати як окремий одноразовий імпорт (`db/dictionaries/*.sql` вже є), без `AnyAsync` на кожному старті.
9. Перенести дані SQLite → PG одноразовим скриптом (довідники, розрахункові лічильники `lab_numerator`/`lab_counter` — з ненульовими значеннями), звірити кількість рядків за таблицями.
10. Додати інтеграційні тести на PG (Testcontainers): запуск `PostgresDdlTests` реальним скриптом, `ApiIntegrationTests` на PG; прогнати паралельне створення замовлень для перевірки нумерації.
11. Окремо від PG, але перед промислом: підключити автентифікацію (замість `Lab:DefaultEmployeeId`/заголовка), справжній відправник сповіщень, справжній режим `Ehealth:Mode=Live` з отриманням токена та перевіркою КЕП, замінити `Lab:VerifySecret` та `PublicBaseUrl`, обмежити CORS.


---

## Оновлення: закрито в гілці cl/notify

| Було | Стало |
|------|-------|
| Сповіщення пацієнтам `QUEUED` без відправника | `NotificationDispatcher` + `NotificationWorker`, провайдери Email (SMTP) / SMS / Viber / Telegram (HTTP-шлюз), повтори, згода, статуси; канал без налаштувань → «Не налаштовано» |
| `lab_mis_event_log` лише пишеться (статус завжди `DELIVERED`) | Статус `QUEUED` при створенні; `MisDeliveryEngine` доставляє webhook із HMAC-підписом, повторами, ідемпотентним ключем; режим `Disabled` → `NOT_CONFIGURED` (історичні записи зі статусом `DELIVERED` без `DeliveredAt` створені до цієї зміни, фактичної доставки не було) |
| `Lab:VerifySecret`, `PublicBaseUrl` — заглушки без попередження | Попередження при старті (лог) і в `/health` (`configWarnings`) поза Development |
| Довіра до заголовка співробітника без перемикача | `Auth:Mode` (None за замовчуванням, Header), попередження в `/health`; вхід — `docs/AUTH_PLAN.md` |

Лишається: реальні облікові дані SMTP/шлюзів і URL МІС (замовник), автентифікація, режим ЕСОЗ Live.
## Доповнення 09.11.2026: ЄСОЗ через проксі MedLink

- Режими: `Mock` (двійник проксі, типово) | `Replay` (фікстури) | `MedLinkProxy` (реальний проксі, поопераційні прапорці `Ehealth:Proxy:Operations:*`) | `Live` (прямий OpenAPI, застарілий).
- Імітація входу з 2FA (код 123456) прибрана: вхід — посилання → код → обмін; у Mock двійник приймає тестовий код `mock-…`. Таблиця `lab_ehealth_session` більше не використовується.
- Mock-підпис КЕП заборонено при `MedLinkProxy`; тестовий сертифікат лише в Mock/Replay.
- Двійник і фікстури відтворюють **виведений** контракт (SDK + код реле); знімків реального проксі немає. Опис — `docs/EHEALTH_MEDLINK_PROXY_CONTRACT.md`.
