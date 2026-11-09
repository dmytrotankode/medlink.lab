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
