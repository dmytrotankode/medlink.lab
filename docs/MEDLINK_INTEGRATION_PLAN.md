# План інтеграції Лабораторії (Lab API) зі стендом MedLink

Дата: 09.11.2026. Умовні позначки: **[К]** перевірено в коді, **[С]** перевірено на сервері 94.130.180.117, **[П]** припущення. 192.168.255.1 і gitlab.phrm.pro не використовувались (VPN), їхні дані — **[П]**.

## 1. Що розгорнуто на сервері

| Об'єкт | Стан |
|---|---|
| Контейнери [С] | `medlink-api` (образ `medlink-api:local`, host-мережа, 2 ГБ/2 CPU, up 4 тижні), `medlink-pg` (postgres:14, 127.0.0.1:5434, БД `evomis`, 336 таблиць у `public`, інших схем немає), `medlink-redis` (127.0.0.1:6380). Поруч — `medlink-lab-v2-pg` (:5444) і `-redis` (:6391), Lab V2 на :5191, старий lab :5190 |
| `/srv/medlink` [С] | `src/` (копія вихідників evomis БЕЗ .git, від 08.10), `Dockerfile.api` (SDK 6.0 → `dotnet publish App.Api` → aspnet 6.0), `ctl/medlink-api-setup.sh` (ідемпотентний запуск), `appsettings.Docker-Local.json`, `dump/` + `pgdata/`, `www/index.html` (заглушка) |
| Джерело [К/С] | `C:\__MEDLINK___\__MEDLINK\evomis`, гілка `phrm-test`, HEAD 7bfa219e5f (22.07.2026). `AuthenticationExtensions.cs` на сервері й локально ідентичні (md5). Інших файлів не порівнював — **[П]**, що `src` = знімок phrm-test |
| Оновлення [С/П] | вручну: скопіювати зміни в `/srv/medlink/src` → `docker build -f Dockerfile.api -t medlink-api:local .` → `ctl/medlink-api-setup.sh` (пересоздає контейнер; секрети в `/etc/medlink/api.env`, не чіпати) |
| nginx [С] | `mis.med-one.avowiz.com`: 80→443, **basic-auth** (htpasswd), `/api/` → 127.0.0.1:5100, `/api/simplex/` → 404, `/` → `root /srv/medlink/www` (SPA-fallback). Фронтенду МІС на сервері **немає**, лише заглушка |
| API живе [С] | `/swagger` 200, `/api/employees` 401, Hangfire працює. Інтеграції (eHealth, Simplex-sync, TerraLab, пошта, Inbox) вимкнені/на заглушку 127.0.0.1:9 |
| БД [С] | 2356 `org_employee`, 1314 `cmn_person.user_id`, 24 `sys_profile`, 2084 `sys_user_profile`; ролі PG: `postgres`, `medone_ro`. Дані — копія, чи знеособлена — **[П]** |

### Автентифікація API (важливо)
- **[К]** `AddHybridJwtAndReferenceTokenAuthentication`: якщо `Identity:UseRefreshTokenFlow=false` — лише JwtBearer (Authority, audiences `JwtBearerAudience`, валідація issuer/aud/lifetime). Якщо `true` — policy-схема: токен із крапкою → JWT-схема; без крапки (reference) → Redis `IDS:ACTIVE_TOKEN:{sub}` / `IDS:TOKEN_BY_HASH:*` (`UseRedisActiveTokenValidation`), з fallback на OAuth introspection (`IntrospectionClientId/Secret`, потрібен секрет від Identity).
- **[С]** Стенд: `Authority=http://127.0.0.1:9`, `UseRefreshTokenFlow=false`, `UseRedisActiveTokenValidation=false` → **жоден токен зараз не валідується** (метадані недоступні), усі `/api` дають 401. Локального Identity на стенді немає.
- **Чи прийме токен id-test.medlink.healthcare:** метадані з сервера доступні (**[С]** `/.well-known/openid-configuration` = 200, issuer `https://id-test.medlink.healthcare`). Якщо поставити `Authority=https://id-test.medlink.healthcare`, `JwtBearerAudience` = `MisApiScope;IdentityApiScope` — JWT-токени клієнта `MisFront` пройдуть **[П]** (потрібно перевірити реальним токеном, власник логіниться сам). Reference-токени (без крапки) вимагають `UseRefreshTokenFlow=true` + Redis, який наповнює сам Identity — наш Redis його не бачить; тоді лише introspection з секретом від команди Identity **[П]**. Далі профіль обирається `POST /api/account/try-authenticate|authenticate` **[К]**.

## 2. Що зроблено в LABA_V2 (гілка claude/lab-v2, коміт 96e16f3) і що переносити в LABA

Усе нижче **[К]**; тести V2 — 363/363 на PG, вхід через реальний id-test **не перевірявся** (немає даних).

| № | Файл (src/MedLink.LIS.Api/…) | Що | Прив'язка до tenancy |
|---|---|---|---|
| 1 | `Configuration/HybridTokenAuthOptions.cs`, `SessionControlOptions.cs`, `ActiveTokenHashHelper.cs`, `ApiUnauthorizedResponseWriter.cs` | опції, хеш токена | ні |
| 2 | `Configuration/RedisActiveTokenValidator.cs`, `RedisActiveTokenAuthenticationHandler.cs` | Redis-схема як в evomis | перевірити виклик try-authenticate (профіль) |
| 3 | `Configuration/AuthenticationExtensions.cs` | гібрид JWT/introspection/Redis | ні (NuGet: AspNet.Security.OAuth.Introspection 2.0.0, JwtBearer, StackExchangeRedis) |
| 4 | `Controllers/adm/AuthConfigController.cs` | `/api/auth/config`: способи входу | ні |
| 5 | `Controllers/adm/StandAuthController.cs` | тестовий вхід стенда (Redis-токен) | тягне `RunAsTenant*` — переписати без нього, лише для стенда |
| 6 | `Services/MedLinkBridge/MedLinkDirectory.cs` | read-only SQL до `public.*` | ні (тільки Npgsql) — переносити першим |
| 7 | `MedLinkSyncService.cs` | профіль→роль, upsert працівника/підрозділу | **так** (`TnDivision`, `IdMembership`, `RunAsTenant`): брати лише `RoleFor` і upsert у `LabEmployeeSettings` |
| 8 | Web: `boot/oidc.js`, `security-oidc.js` | OIDC PKCE у SPA | ні |
| 9 | `tests/…/Api/MedLinkBridgeTests.cs` (5 тестів) | адаптувати | тенант-ізоляцію прибрати |

**Порядок у LABA:** (1) пакети+п.1–3; (2) новий `Auth:Mode=Oidc` у `CurrentEmployeeMiddleware`: claim `sub` → `cmn_person.user_id` → employee; `None/Header` лишити для відкату; (3) п.6 + `Integrations:MedLink:*`; (4) спрощений п.7; (5) п.4, 8; (6) п.5 лише для стенда. Нинішня LABA сама пише в `cmn_person`/`mis_patient_card`/`mis_diagnostic_report` (`MedLinkPeople`, `DbSet<MisDiagnosticReport>`) — це суперечить «public лише читання»: пацієнтів треба створювати через API МІС, а результати — подіями (п. 4).

## 3. Сутності МІС для читання лабораторією [С — колонки з БД]

| Таблиця | Потрібні колонки |
|---|---|
| `cmn_person` | id, user_id (=sub Identity), last_name, name, middle_name, birthday, gender_id, phone, email, ipn |
| `org_employee` | id, person_id, organization_id, department_id, position_type_id, is_locked, record_state |
| `org_department` | id, organization_id, caption, code, parent_id |
| `mis_patient_card` | id, person_id, organization_id, reg_number, caption, birthday, gender_id, card_status, allergy_history |
| `sys_user_profile` (user_id = **id працівника**, перевірено: 2084/2084) → `sys_profile` (caption, type_id) → `sys_profile_role` → `sys_role` | профіль безпеки працівника |
| `cmn_enum_record` | стать, типи (за enum_type+code) |
| `ehe_outgoing_medical_referral` | направлення: reg_number, patient_card_id, employee_id, service_catalog_service_id, status_id, performer_organization_id, ehealth_id |
| `mis_diagnostic_report`, `mis_observation*` | лише для звірки результатів |
| `mis_simplex_exec_lab_service`, `mis_simplex_lab_referral_service_transfer` | наявний канал Simplex (знати, не дублювати) |

**Ролі.** Профілі МІС — «Адміністратор», «Головний лікар», «Сімейний лікар», «Працівник реєстратури», «Лаборант eHealth вторинки», «Спеціаліст/Асистент eHealth вторинки» тощо (24 шт.) **[С]**. Пропозиція (конфіг `Integrations:MedLink:ProfileRoles`, **[П]** — узгодити з замовником):

| Профіль МІС | Роль лабораторії |
|---|---|
| Адміністратор, Адміністратор своєї організації, Адміністратор eHealth (+вторинки) | LAB_ADMIN |
| Лаборант eHealth вторинки | LAB_TECHNICIAN |
| Головний лікар (+eHealth), Спеціаліст eHealth вторинки | LAB_DOCTOR |
| Працівник реєстратури (+eHealth вторинки), Реєстратор eHealth | REGISTRAR |
| Асистент eHealth вторинки | PHLEBOTOMIST |
| решта (Сімейний лікар тощо) | без доступу (лише направляють); `DefaultLabRole` порожній |

Окремого профілю «лікар-лаборант»/«кур'єр» немає — потрібно завести в МІС (п. 4).

## 4. Підключення модуля до стенду mis.med-one.avowiz.com

| Питання | Рішення |
|---|---|
| (а) Де Lab API | окремий контейнер `medlink-lab` (.NET 8, образ з LABA) у host-мережі, `127.0.0.1:5200` (порт вільний **[С]**: зайняті 5037, 5080, 5090, 5100, 5181, 5190, 5191). У nginx-сервері mis.med-one: `location /lab/ { proxy_pass http://127.0.0.1:5200/; }` + `/lab/api/` (PathBase `/lab`, як у V2 `PathBase`), під тим самим basic-auth. `/api/` лишається за medlink-api |
| (б) Меню/плитка | **Фронту МІС на сервері немає** (`/srv/medlink/www` — заглушка; збірка фронту — окремий репозиторій, у `evomis` лише API **[К]**). Варіанти: 1) розгорнути збірку фронту МІС (потрібен артефакт від команди MedLink) і додати пункт «Лабораторія» → `/lab/`; 2) до цього — посилання/плитка в статичній сторінці `www/index.html` на `/lab/`. Додавання пункту у фронт МІС — задача їхнього фронт-репозиторію **[П]**; ЛІС відкривається в тій самій вкладці через SSO (тот самий id-test, клієнт з redirect `/lab/…`) |
| (в) БД | у `evomis`: схема `lab` (власник `lab_migrator`), ролі `lab_app` (CRUD на `lab`, `USAGE` на public + `SELECT` на перелічені в п.3 таблиці, **без** INSERT/UPDATE/DELETE) і `lab_migrator` (DDL лише в `lab`). Скрипт-прототип є у V2 `tools/deploy/medlink-lab-v2-install.sh` (там `GRANT SELECT ON ALL TABLES IN SCHEMA public` — звузити до переліку). EF `HasDefaultSchema("lab")`, історія міграцій у `lab`. `medone_ro` не використовувати **[С]**. Бекап `pg_dump evomis` перед першою міграцією |
| (г) Токени | Потрібен id-test для всього ланцюжка: у `appsettings.Docker-Local.json` змінити `Identity:Authority`/audiences (див. п.1); Lab API використовує ту саму схему (Authority id-test, audience `MisApiScope`/окремий `LabApiScope`). Локального Identity на стенді немає. Потрібно від MedLink: дозвіл використати клієнт `MisFront` або новий `LabFront` з redirect `https://mis.med-one.avowiz.com/lab/…`; для reference-токенів — секрет introspection **[П]** |

## 5. Зміни на боці MedLink для наскрізної взаємодії

| Зміна | Суть | Обсяг |
|---|---|---|
| Події результатів у МІС | **Є готовий канал [К]:** `POST /api/TerraLab/set_status|set_results|set_original_results` (AllowAnonymous!) → `TerraLabService` оновлює направлення. Варіант A: Lab API емулює TerraLab-вебхук (ExtId=GUID направлення без дефісів) — 0 змін у MedLink, але без автентифікації і лише для направлень TerraLab. Варіант B (рекомендую): новий контролер `LabResultsController` з `X-Api-Key` (за зразком `EventManagerApiKeyAuthorizationFilter`) → створює `mis_diagnostic_report` + `mis_observation` + оновлює статус `ehe_outgoing_medical_referral` через існуючі сервіси `DiagnosticReportControllerService` | B: ~3–5 дн. (+ тести) |
| API направлень | читання — прямо з БД (`ehe_outgoing_medical_referral`); зміна статусів «прийнято/в роботі/виконано» — тим самим новим контролером; створення направлень лишається в МІС | ~2 дн. |
| Проксі eHealth | `App.EhealthSdk` вже ходить на `Ehealth:ApiUri`/`EhealthSdkOptions.BaseUrl` з парою (ClientId, UserId) **[К]**; на стенді Ehealth вимкнено (заглушка :9). Потрібно: адреса проксі MedLink eHealth (не отримана), ClientId для Lab. Контракт ЛІС↔проксі — `docs/EHEALTH_MEDLINK_PROXY_CONTRACT.md` (виведений з коду, живих викликів не було) | 1–2 дн. налаштування + узгодження |
| Схема lab і права | див. 4(в) | ~1 дн. |
| Профілі безпеки | додати профілі «Лікар-лаборант», «Лаборант», «Кур'єр» + права в `sys_profile_*` (сід-міграція evomis) | ~1 дн. |
| Чи пише лабораторія в `public` | ні; пацієнтів створює МІС (API `PatientCard`), Lab лише шукає | — |

## 6. Кроки, порядок, ризики

1. Бекап `evomis`; ролі `lab_app/lab_migrator`, схема `lab`, EF-міграції (Lab ще порожній) — низький ризик.
2. Контейнер `medlink-lab` :5200 + nginx `/lab/` (за basic-auth). Режим `Auth:Mode=Header` лише в закритому контурі.
3. Перенесення auth (п.2 порядок 1–3) + Auth:Mode=Oidc; на medlink-api встановити Authority id-test. **Ризик:** потрібен реальний токен і клієнт; власник входить сам; без нього — не перевірено. Зміна Authority на medlink-api зачепить будь-кого, хто використовує стенд (зараз усе 401).
4. Міст читання (п.6 LABA_V2) + мапінг ролей; перевірка на 2356 працівниках.
5. Подія результату: спершу варіант A на тестовому направленні (швидко), потім B.
6. Плитка/меню «Лабораторія»: спершу статична, потім у фронті МІС.
7. eHealth через проксі — після отримання адреси та клієнта.

**Ризики:** (1) фронт МІС недоступний у репозиторії; (2) `src` на сервері — знімок без .git, оновлення ручне, розбіжності з phrm-test можливі; (3) TerraLab-вебхуки відкриті без автентифікації — не виставляти назовні (nginx блокує лише `/api/simplex/`); (4) LABA нині пише в таблиці МІС — переробити; (5) повністю відсутня перевірка id-test реальним токеном; (6) пам'ять сервера: два PG, два Redis, .NET 6 + .NET 8 + стенди.

## 7. Реалізація в основній лінії (гілка cl/oidc, 09.11.2026)

Статус: **[К]** — зібрано й покрито тестами на SQLite; **[Н]** — не перевірялось (потрібні реальний Identity, база МІС або PostgreSQL).

### 7.1. Режим входу `Auth:Mode=Oidc` (None і Header не змінено)
- Перевірка токена: JWT Bearer від Identity (`Identity:Authority`, `Identity:JwtBearerAudience`, типово `IdentityApiScope;MisApiScope`). Authority і audience читаються з остаточної конфігурації, секретів у репозиторії немає. **[К]** (підписаний тестовий JWT: 401 без токена, прострочений, чужий audience/issuer/підпис; 200 із чинним).
- Reference-токени (опційно, `Identity:UseRefreshTokenFlow=true`): introspection (`Identity:IntrospectionClientId`, секрет — лише змінна середовища `Identity__IntrospectionClientSecret` або `REFERENCE_TOKEN_API_SECRET`) і/або Redis активних токенів (`Identity:UseRedisActiveTokenValidation`, `Identity:RedisConnectionString`). Код перенесено з Lab V2 без змін логіки. **[Н]**
- `CurrentEmployeeMiddleware`: токен → `sub` → співробітник. Заголовок `X-MedLink-Employee-Id` у режимі Oidc **не визначає особу** (лише обирає одну з власних посад особи, якщо їх кілька). `Lab:DefaultEmployeeId` не використовується (немає токена → 401). **[К]**
- Без входу лишаються: `/health`, `/verify/*`, `/api/v1/lab/auth/*`, `/api/v1/lab/connector/*` (власний `X-MedLink-ApiKey`), `/api/v1/lab/portal/*` (див. ризик нижче), статика SPA; додаткові префікси — `Auth:PublicPaths`. Swagger у режимі Oidc вимкнено у Production. Редагування Guide без входу вимкнено (`Guide:EditorRoles`).
- Без моста (немає `Medlink:ConnectionString`): співробітник береться з власної бази за `org_employee.id` = `sub` (або claim з `Auth:Oidc:EmployeeIdClaim`). **[К]**

### 7.2. Міст до МІС (лише читання)
- `Medlink:ConnectionString` — окремий Npgsql-рядок до бази МІС (роль лише з `SELECT` на таблиці з розділу 3). Порожньо — міст вимкнено. `Medlink:Schema` (типово `public`), `Medlink:OrganizationId` (опційно обмежити організацією).
- Ланцюг: `sub` → `cmn_person.user_id` → `org_employee` особи → `sys_user_profile`/`sys_profile` → роль ЛІС за таблицею `Medlink:ProfileRoles` (ключ — назва профілю, регістр не важливий; якщо профілів кілька — найвища роль: адмін › лікар › лаборант › реєстратор › медсестра › кур'єр). Таблиця — у `appsettings.json` за розділом 3 (назви профілів **[П]**, звірити з МІС). `Medlink:DefaultLabRole` порожній → 403 «Для вашого профілю в МІС немає доступу до Лабораторії…». `Medlink:CacheSeconds` (60) — затримка, з якою зміна профілю в МІС діє в ЛІС. **[К]** (підміна каталогу МІС); справжні SQL до `public` **[Н]**.
- Запити каталогу: лише `SELECT`, параметризовані; тест `Mis_directory_issues_only_select_statements` забороняє будь-який модифікуючий SQL у файлі каталогу.
- Проєкція: при першому вході працівник (особа, заклад, підрозділ, роль) копіюється у власну схему ЛІС з тим самим id — потрібно для зовнішніх ключів і аудиту (`CreatedBy` = id працівника МІС). Картка пацієнта МІС підтягується проєкцією при створенні замовлення з `patientId`.

### 7.3. Запис у public заборонено: де основна лінія писала і що зроблено
Усі записи нижче йдуть через `IMisRecords` (`Services/MedLinkBridge/MisRecords.cs`). Без моста (автономна ЛІС) поведінка не змінилась; з мостом (`Medlink:ConnectionString` задано):

| Місце | Таблиця | Рішення в режимі моста |
|---|---|---|
| `PatientService.CreateAsync`, `OrderService.BuildOrderAsync` (`newPatient`) | `cmn_person`, `mis_patient_card` | 409 «Створення картки пацієнта у лабораторії недоступне… виконайте дію в МІС» |
| `PatientService.UpdateAsync` / `DeleteAsync` | те саме | 409 (редагування й видалення карток — у МІС) |
| `OrgDictionaryService.SaveEmployeeAsync` (новий співробітник) | `cmn_person`, `org_employee` | 409; працівників веде МІС (оновлення посади/підпису існуючого дозволене — це дані ЛІС) |
| `OrderService.ReleaseAsync` | `mis_diagnostic_report` | висновок не створюється; результат йде подією МІС (`MisReferralService.EmitFor`, `diagnosticReportId` = null). Підпис ЄСОЗ у ЛІС без висновку не можливий — формує МІС |
| `EhealthOutbox` (копія висновку після анулювання) | `mis_diagnostic_report` | не виконується: без висновку `old == null` |
| `SeedService` | усі | демо-співробітники, пацієнти, направлення й поліси не створюються (також `DemoDataSeeder` вимкнено) |

Фізично запису в `public` немає в жодному режимі: ЛІС пише лише у власну схему (7.4), а підключення `Medlink:ConnectionString` має роль із `SELECT`. Таблиці `cmn_person`/`mis_patient_card`/`org_employee` у схемі ЛІС — **проєкції** (копії) для зовнішніх ключів; їх наповнює лише `IMisRecords.ImportPatientAsync`/`ProjectEmployeeAsync` зі значень МІС.

Не зроблено: подія результату в МІС (канал B з розділу 5) — до появи `LabResultsController`; пошук пацієнтів МІС у формі замовлення (каталог має `SearchPatientsAsync`, UI-вибору ще немає — поки `patientId` картки МІС).

### 7.4. Власна схема PostgreSQL
- `Database:Schema=lab` додає `Search Path=lab` (лише вона, без `public`) до рядка підключення ЛІС; Guide працює у своїй схемі `guide`. Старт: `CREATE SCHEMA IF NOT EXISTS lab`; якщо схема порожня — таблиці створює EF (`EnsureCreated` у базі з таблицями МІС нічого б не створив), решту додає `SchemaUpgrader` (він і `PgSchemaMigrator` працюють через `current_schema()`). На час версіонованих скриптів `search_path` = «lab, public» (потрібен для `pg_trgm`), після — повертається.
- DDL-експорт (`--export-pg-ddl`) і модель EF не змінено: `db/postgres/medlink_lis_schema.sql` лишається актуальним (`PostgresDdlTests` зелений). **[Н]** на справжньому PostgreSQL (локально PG немає): перевірено рядок підключення (тест), решта — за кодом.
- Права: `lab_migrator` — `CREATE` на базі та власник схеми `lab`; `lab_app` — CRUD на `lab`; обидві ролі — `USAGE` на `public` + `SELECT` лише на таблиці розділу 3.

### 7.5. SPA
- `GET /api/v1/lab/auth/config` → `{mode, runAs, oidc:{authority, clientId, scope, callbackPath}, bridge:{enabled}}` (без секретів). Клієнт OIDC — `Auth:Oidc:ClientId` (типово `MisFront`, до отримання окремого клієнта), `Auth:Oidc:Scope`, redirect URI = `<адреса ЛІС>/<PathBase>/callback` (його потрібно зареєструвати в Identity).
- `oidc-client` 1.7.1, код + PKCE; токен додається до запитів; 401 → повторний вхід; кнопка «Вийти». «Працюю як…» лишається лише в None/Header. Посилання на PDF/друк/експорт API відкриваються з токеном (перехоплення кліку на `a[href]` і `window.open`). Вибір профілю за потоком МІС (`Account/authenticate`) не переносився — роль береться з профілів у базі МІС.
- **[Н]** вхід через справжній Identity не перевірявся: потрібні клієнт і redirect URI.

### 7.6. Як увімкнути (приклад змінних середовища)
```
Auth__Mode=Oidc
Identity__Authority=https://id-test.medlink.healthcare
Identity__JwtBearerAudience=IdentityApiScope;MisApiScope
Auth__Oidc__ClientId=<клієнт ЛІС або MisFront>
Medlink__ConnectionString=Host=...;Database=evomis;Username=lab_app;Password=...   # роль лише SELECT
Database__Provider=Postgres
Database__ConnectionString=Host=...;Database=evomis;Username=lab_migrator;Password=...
Database__Schema=lab
Lab__SeedDemoData=false
PathBase=/lab
```


## 8. Стенд mis.med-one (розгорнуто 09.11.2026, режим Header)

**Розгорнуто [С]:** Lab API (гілка `cl/mis`) як служба `medlink-lab-mis` на `127.0.0.1:5200`, каталог `/srv/medlink-lab-mis` (`app/` — збірка, `app.prev/` — попередня), користувач `medlinklab`. nginx: сніпет `/etc/nginx/snippets/medlink-lab-mis.conf` підключено в `sites-available/mis.med-one.avowiz.com` перед `location /`; `/lab/` → `127.0.0.1:5200` за тим самим basic-auth, що й сайт (`/api/` і решта не змінювались). Поруч не чіпались: `medlink-lab` (5190), `medlink-lab-v2` (5191), `medlink-api`, контейнери `medlink-*`.

**БД `evomis` (контейнер `medlink-pg`):** схема `lab` (власник `lab_migrator`, 132 таблиці створив сам застосунок при старті), ролі `lab_migrator` і `lab_app` — LOGIN, не superuser/createdb/createrole; `lab_app` — CRUD на `lab`; обидві ролі — `USAGE` на `public` і `SELECT` лише на 10 таблиць розділу 3. Додатково знято `CREATE` на схемі `public` у ролі `PUBLIC` (у PG14 вона є за замовчуванням, інакше `lab_*` могли б створювати об'єкти в `public`; відкат: `GRANT CREATE ON SCHEMA public TO PUBLIC`). `lab_migrator` має `CREATE` на базі (для `CREATE SCHEMA IF NOT EXISTS` при старті).

**Змінні служби (drop-in, 0600 root):** `Database__Provider=Postgres`, `Database__ConnectionString` (lab_migrator, `Search Path=lab`), `Database__Schema=lab`, `Lab__SeedDemoData=false`, `PathBase=/lab`, `Auth__Mode=Header` (режим Oidc — окремий крок), `Lab__PublicBaseUrl=https://mis.med-one.avowiz.com/lab`, `Lab__VerifySecret`, `Medlink__ConnectionString` (lab_app, SELECT-only).

**Де секрети (лише шляхи):** `/srv/medlink-lab-mis/secrets.env` (паролі ролей і VerifySecret, 0600 root); `/etc/systemd/system/medlink-lab-mis.service.d/{postgres,app,auth}.conf` (0600 root). Бекап БД до розгортання: `/srv/medlink/backups/evomis_20261109.dump` (pg_dump -Fc, 280 МБ, 0600).

**Оновити:** з кореня репозиторію (Git Bash), гілка `cl/mis`:
```
MSYS_NO_PATHCONV=1 MSYS2_ENV_CONV_EXCL=LIS_PUBLIC_PATH LIS_PUBLIC_PATH=/lab/ powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build.ps1 -SkipInstall
dotnet publish src/MedLink.LIS.Api/MedLink.LIS.Api.csproj -c Release -r linux-x64 --self-contained false -o OUT/app
tar -czf OUT/lab-mis-app.tgz -C OUT/app . && scp -i ~/.ssh/medone_ed25519 OUT/lab-mis-app.tgz root@94.130.180.117:/tmp/lab-mis-app.tgz
ssh -i ~/.ssh/medone_ed25519 root@94.130.180.117 'bash -s' < tools/deploy/medlink-lab-mis-install.sh
```
Скрипт `tools/deploy/medlink-lab-mis-install.sh` ідемпотентний: паролі й наявний `auth.conf` не перезаписує, попередню збірку зберігає в `app.prev`.

**Відкат:** застосунок — `systemctl stop medlink-lab-mis; rm -rf /srv/medlink-lab-mis/app; mv /srv/medlink-lab-mis/app.prev /srv/medlink-lab-mis/app; systemctl start medlink-lab-mis`. Повне зняття: `systemctl disable --now medlink-lab-mis`; прибрати рядок `include snippets/medlink-lab-mis.conf;` із сайту (копія до правки: `/root/mis.med-one.avowiz.com.bak.*`), `nginx -t && systemctl reload nginx`; БД — `DROP SCHEMA lab CASCADE; DROP ROLE lab_app; DROP ROLE lab_migrator;` (перед `DROP ROLE` — `REVOKE ... ` або `DROP OWNED BY`), або `pg_restore` з бекапу.

**Перевірено [С]:** `/lab/health` 200 (db ok); `lab_app` у `public`: INSERT/UPDATE/DELETE/CREATE TABLE — `permission denied`, SELECT дозволено; у `lab` `lab_app` не може DDL; `medlink-api`, `/lab` на med-one.avowiz.com, :5190 — без змін (health до і після). **Не перевірено:** `/lab/` через nginx із basic-auth (пароль у власника; без нього 401), вхід у SPA, режим Oidc. **Увага для кроку Oidc:** `Authorization: Bearer` від SPA конфліктує з basic-auth сайту (той самий заголовок) — для `/lab/` потрібно або зняти basic-auth, або передавати токен іншим способом.
