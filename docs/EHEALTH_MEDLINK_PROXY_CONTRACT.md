# Контракт ЛІС MedLink ↔ сервіс MedLink eHealth (проксі до ЄСОЗ)

Версія: 09.11.2026. Гілка `cl/ehproxy`.

**Вимога замовника:** методи ЄСОЗ викликаються лише через сервіс MedLink eHealth (далі — проксі). ЛІС не звертається до `api.ehealth.gov.ua` напряму, не зберігає токени й секрети ЄСОЗ.

## 0. Що це за документ і наскільки йому можна вірити

Контракт **виведено з коду**, а не знято з працюючого проксі:

- SDK МІС evomis: `evomis/src/App.EhealthSdk` (клієнт, який викликає проксі);
- код реле проксі (лише контролери й маршрути): `ehealth-proxy-server-medinet[dev]/src`.

Реальних викликів не було, знімків відповідей немає. Усе, що нижче позначено **[?]**, — припущення, яке треба підтвердити командою проксі до першого живого виклику. Фікстури `tests/MedLink.LIS.Tests/Fixtures/ehealth/*.json` відтворюють саме цю виведену форму.

## 1. Схема

```
ЛІС (браузер) ──► API ЛІС ──(ClientId, UserId)──► проксі MedLink eHealth ──(токен користувача)──► ЄСОЗ
                          ◄── job / дані / конверт помилки ◄──
```

- Токен доступу користувача зберігає **проксі**, ключ — пара (`ClientId`, `UserId`). ЛІС токена не бачить і не передає.
- Підпис КЕП виконується на боці користувача; готовий CAdES (`signed_data`, base64) ЛІС передає в тілі `diagnostic_report_package`.
- Адреса проксі — конфіг `Ehealth:Proxy:BaseUrl` (порожня в `appsettings.json`).

## 2. Загальні правила

| Пункт | Значення |
|---|---|
| Базова адреса | `{Ehealth:Proxy:BaseUrl}/` + шлях з розділу 3. Префікса `/api` немає **[?]** |
| Заголовки (лише імена) | `ClientId` — id закладу в ЄСОЗ; `UserId` — id користувача (працівника ЛІС); `Content-Type: application/json`. `Authorization` **не надсилається**. Службові `x-aid`, `PSK`, `API-Key`, `Response-Format` ЛІС не використовує **[?]** |
| Джерело `UserId` | контекст задачі (`userId` у payload outbox) → поточний працівник → `Ehealth:Proxy:ServiceUserId` → у Mock/Replay `system`; інакше помилка конфігурації |
| Секрети | лише конфіг/змінні середовища `Ehealth__Proxy__*`; в `appsettings.json` порожні |
| Таймаути/повтори | таймаути в `Ehealth:*`; повтори лише для GET |
| Логи | `signed_data` і `code` авторизації маскуються |

## 3. Методи

Позначення: SDK — файл у `App.EhealthSdk`; джерело точних імен класів уточнити при звірці **[?]**.

### 3.1. Електронні направлення (ЕН)

| Операція ЛІС | Метод і шлях | Тіло | Відповідь |
|---|---|---|---|
| Пошук | `GET patients/service_requests?requisition={номер}` (+ `status`, `page_size` **[?]**) | — | `{data:[…], paging}` |
| Деталі | `GET patients/service_requests/{id}` | — | `{data:{…}}` |
| Use (прийняти в роботу) | `PATCH patients/service_requests/{id}/actions/use` | `{used_by_employee, used_by_legal_entity, program}`; `used_by_division` не надсилається | job (3.3) |
| Complete (погасити) | `PATCH patients/service_requests/{id}/actions/complete` | `{completed_with[, program_service]}` (посилання на звіт) | job |
| Release | `PATCH patients/service_requests/{id}/actions/release` | без тіла (`status_reason` не надсилається) | job |

Use без програми, значення `status_reason` та точні обов'язкові поля — **[?]**, див. `ESOZ_LAB_INTERACTIONS.md` розд. 5.

### 3.2. Звіт

| Операція | Метод і шлях | Тіло | Відповідь |
|---|---|---|---|
| Пакет звіту | `POST patients/{patientId}/diagnostic_report_package` | `{signed_data: base64 CAdES, …}`; всередині підписаного пакета `diagnostic_report` (з `effective_period{start,end}`, `paper_referral.note`) та `observations[]` — будує `EhealthPackageBuilder` | job |
| Скасування звіту | `PATCH patients/{patientId}/diagnostic_report_package` | `{signed_data}` (підписаний пакет скасування) | job |
| Перевірка звіту перед повтором | `GET patients/{patientId}/diagnostic_reports/{id}` | — | дані звіту або 404 |

Порядок: пакет → job `processed` → **окремий** `complete` ЕН (окрема задача outbox). Помилка → скасування звіту й новий пакет з новими id.

### 3.3. Job

`GET jobs/{id}` → `{data:{status:"pending"|"processed"|"failed", eta, links:[…], error?}}`.

- Прийняті записи повертають тіло job із HTTP **200/201** (не 202) **[?]**: `{data:{status:"pending", eta, links:[{entity:"job", href:".../Jobs/<id>"}]}}`. Клієнт розпізнає job за змістом (`EhealthJobReader.LooksLikeJob`).
- `failed` може приходити з HTTP 200; причина — у конверті помилки.
- ЛІС опитує job з витримками (`EhealthOutbox`).

### 3.4. Довідники та обладнання

| Метод і шлях | Призначення |
|---|---|
| `GET dictionaries?name={ім'я}` **[?]** | довідники (категорії звіту, LOINC/UCUM — коли з'являться) |
| `GET equipments?…` **[?]** | обладнання закладу (для `used_references`) |

Каталог послуг (`services`, `service_groups`) у проксі не знайдено — див. розд. 6.

### 3.5. Авторизація користувача

| Крок | Метод і шлях | Результат |
|---|---|---|
| 1. Посилання | `GET auth/EhealthUILink/{role}?isReorganized={bool}` | адреса порталу авторизації або параметри для неї |
| 2. Вхід користувача | браузер відкриває `{AuthPortalUrl}?client_id=…&redirect_uri=…&scope=…` | ЄСОЗ повертає на `RedirectUri` (сторінка ЛІС `/laboratory/ehealth-callback`) з `?code=…` |
| 3. Обмін | `POST auth` `{code, role_name}` | проксі міняє код на токен і зберігає під (`ClientId`,`UserId`) |
| 4. Перевірка | `GET auth/hasAccessToken` | текст `true`/`false` (приймаються також JSON-форми) |
| 5. Оновлення | `GET auth/update` | оновлений токен на проксі |
| 6. Вихід | `GET auth/logout` | токен видалено |

Ендпоїнти ЛІС: `POST /api/v1/lab/ehealth/session/link`, `…/session/callback`, `…/session/refresh`, `DELETE …/session`, `GET …/session`. Стани UI «Сесія ЄСОЗ»: `none` (кнопка «Увійти в ЄСОЗ»), `active`, `unavailable`, `notConfigured`.

## 4. Помилки

Конверт може прийти й з HTTP 200:

```json
{ "HasException": true, "InternalMessage": "…", "StatusCode": 401,
  "EhealthResponse": { "error": { "type": "token_expired", "message": "…", "invalid": [] }, "meta": {} } }
```

| Ознака | Клас у ЛІС | Поведінка |
|---|---|---|
| `token_not_found`, `token_expired`, `invalid_grant`, `invalid_client`, HTTP 401 | Auth | «Потрібен вхід в ЄСОЗ» |
| HTTP 403 | Forbidden | немає прав |
| HTTP 404 | NotFound | |
| HTTP 409 | Conflict | |
| HTTP 422 / `invalid[]` | Validation | показ полів |
| HTTP 5xx, мережа | Transient | повтор за витримками (для GET) / повтор задачі outbox |
| відсутні `BaseUrl`/`ClientId`/`UserId` | Config | помилка налаштування |

## 5. Режими ЛІС

| `Ehealth:Mode` | Значення |
|---|---|
| `Mock` (типово) | двійник проксі в пам'яті (`EhealthMockHandler`); вхід по тестовому коду `mock-…`; жодної мережі |
| `Replay` | відповіді з фікстур (`EhealthReplayHandler`) |
| `MedLinkProxy` | реальний проксі; кожна операція вимкнена прапорцем `Ehealth:Proxy:Operations:*`, доки її не ввімкнено |
| `Live` | **застаріло**: прямий OpenAPI зі статичним токеном; лише для довідки, суперечить вимозі замовника |

Конфіг: `Ehealth__Proxy__BaseUrl`, `__ClientId`, `__Role` (DOCTOR), `__AuthPortalUrl`, `__RedirectUri`, `__IsReorganized`, `__ServiceUserId`, `__Operations__{Search,Details,Use,Complete,Release,Submit,Cancel,…}`.

## 6. Чого у проксі немає (вимоги до команди проксі)

Нічого з цього не вигадано в ЛІС; відсутнє в SDK/реле, що вивчені:

1. **Зразки біоматеріалу (Specimen)**: створення/скасування/обробка. Потрібні для чутливих ЕН (Q&A ЄСОЗ).
2. **Каталог послуг** (`services`, `service_groups`) та інтервенцій.
3. **Перевірка наявності токена за конкретною роллю** й чітка помилка «токена немає» (зараз виводиться з `token_*`).
4. **Метод пошуку працівника/ролі** для ідентифікатора працівника в ЄСОЗ.
5. **Явний контракт** (OpenAPI/Swagger проксі) та знімки реальних відповідей: 200 чи 202, префікс `/api`, параметр `response_type`.
6. Use без програми та значення `status_reason` (3.1) — підтвердити.

## 7. Що потрібно від команди проксі та замовника

- URL проксі (test і prod) і спосіб доступу: allowlist IP/VPN.
- `ClientId` закладу в ЄСОЗ; тип клієнта (МІС чи заклад) — впливає на скоупи.
- `AuthPortalUrl` та зареєстрований `RedirectUri` ЛІС (`…/laboratory/ehealth-callback`).
- Тестовий користувач ЄСОЗ з роллю лаборанта/лікаря та КЕП (віджет — Q-21).
- Передача секретів — лише по захищеному каналу, у змінні середовища, не в репозиторій.
- Відповіді на питання розд. 6.

## 8. Ризики

- Контракт не перевірено на живому проксі; розбіжності (код відповіді, префікс, імена полів) виправляються в `EhealthClient` і фікстурах.
- Токен на проксі прив'язаний до `UserId`: фонові задачі без користувача працюють лише при `ServiceUserId` з токеном.
- Підпис КЕП у браузері не реалізовано (Mock-підпис заборонено в режимі `MedLinkProxy`).

## 9. Джерела в репозиторії

`src/MedLink.LIS.Api/Services/Ehealth/`: `EhealthClient.cs`, `EhealthOptions.cs`, `EhealthJobReader.cs`, `EhealthPackageBuilder.cs`, `EhealthOutbox.cs`, `EhealthSignService.cs`, `EhealthMockHandler.cs`, `EhealthReplayHandler.cs`; `Services/ReferralService.cs`; `Controllers/EhealthSignController.cs`; тести — `tests/MedLink.LIS.Tests/Api/EhealthContractTests.cs`, фікстури — `Fixtures/ehealth/`.
