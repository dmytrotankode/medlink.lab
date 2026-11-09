# Взаємодія лабораторії (ЛІС) з ЕСОЗ (eHealth): перелік, стан реалізації, рекомендований дизайн і план

Дата: 2026-11-09. Режим: дослідження (читання), код ЛІС не змінювався.
Мова: опис — українською/російською, терміни, ендпоінти, поля та скоупи eHealth — англійською (як у специфікації).

Позначки достовірності в тексті:
- **[док]** — є в OpenAPI eHealth (збірка 2026-10-27, `dist/prod`), у ТВ МІС v1.39 (наказ НСЗУ № 464 від 25.08.2026) або в офіційній таблиці скоупів (Confluence ESOZ, сторінка «Права доступу (All Scopes model)», версія 38 від 24.09.2026);
- **[преприд]** — підтверджено живим викликом на preprod ЕСОЗ у роботі над власною референс-бібліотекою інтеграції (ehc/ehm, TypeScript) у жовтні–листопаді 2026;
- **[SDK]** — як зроблено в MedLink (SDK МІС evomis, `src/App.EhealthSdk`, експорт гілки prod від 2026-10-27);
- **[?]** — припущення / потребує перевірки на preprod.

Скорочення шляхів: `SDK/` = `evomis/src/App.EhealthSdk/`; `LIS/` = `LABA/src/MedLink.LIS.Api/`.

---

## 0. Короткий підсумок

1. Реальний мінімальний цикл лабораторії-виконавця в ЕСОЗ: **знайти ЕН за номером → (за потреби) qualify/use → прийняти зразок → отримати результати → Submit Diagnostic Report Package (diagnostic_report + observations, підпис КЕП) → дочекатися job → Complete ЕН (`completed_with` = diagnostic_report)**. Виправлення — **Cancel Diagnostic Report Package** (`entered_in_error`) + новий пакет.
2. У ЛІС зараз **реальний обмін не працює взагалі**: у Live режимі всі методи сесії/підписання/відправки повертають 503 (`EnsureMock`, `LIS/Services/Ehealth/EhealthSignService.cs:117-120`), а «пакет», який будує `PrepareAsync` (рядки 223-249), **не відповідає схемі eHealth** (вигаданий `diagnostic_report_package` з FHIR-подібними полями, а не `{diagnostic_report, observations[]}`).
3. **Шляхи та протоколи клієнта не збігаються з OpenAPI**: ЛІС викликає `patients/service_requests/{id}/actions/use|release|complete` (успадковано від проксі MedLink), а в OpenAPI це `/api/service_requests/{id}/actions/...`; відповідь 202 на preprod **не містить `data.id`** (job приходить у `links[{entity:'job',href}]`) [преприд], а клієнт читає `data.id` (`LIS/Services/Ehealth/EhealthClient.cs:101-105`) → у Live буде NullReference.
4. **Порядок погашення зламаний**: `ReleaseAsync` замовлення викликає `Complete` ЕН із **локальним** id висновку (`LIS/Services/ReferralService.cs:193-203`, виклик `OrderService.cs:459`) ще до того, як diagnostic_report існує в ЕСОЗ. У ЕСОЗ `Complete` припускає, що пов'язаний медичний запис (не `entered_in_error`) уже створений [док: модель статусів ЕН]. Правильно: DR package → job processed → Complete, окремим повторюваним кроком, поза транзакцією видачі.
5. **Вхід «логін/пароль ЕСОЗ у формі ЛІС» (FR-SGN-002) суперечить ТВ 1.2.2** (заборонені проміжні інтерфейси авторизації, крім сторінки `auth.ehealth.gov.ua`). Правильна модель: OAuth Authorization Code із редиректом на сторінку ЕСОЗ (2FA/КЕП виконує сама ЕСОЗ), токени per-user на сервері, зашифровані; пароль ЕСОЗ ЛІС ніколи не бачить.
6. Роль **LABORANT** за офіційною таблицею скоупів має `service_request:read/read_impersonal/makeinprogress/use/complete`, `diagnostic_report:read/write/cancel`, `observation:read/write`, `specimen:*`, `equipment:read/write`, `job:read`, але **не має** `person:read`, `approval:*`, `service_request:write/recall/cancel`, `patient_summary`. Тому лаборант не читає дані пацієнта поза тим, що повертає ЕН (`identity`), не створює згоди й не відкликає ЕН. Чутливі ЕН (напр. ВІЛ) лаборанту доступні лише через **Specimen** [Q&A ЕСОЗ].
7. Рекомендація: **outbox + фоновий воркер** (ідемпотентні операції `SUBMIT_DR`, `POLL_JOB`, `COMPLETE_SR`, `CANCEL_DR`, `REFRESH_SR`), клієнтські UUID v1 для DR/observations (ТВ 1.1.7), зберігання підписаного пакета незмінним, окремі осі статусів, режими `Mock | Replay | Live` з поопераційними перемикачами, контрактні тести з OpenAPI-схем. Відмінності від MedLink обґрунтовано в розд. 3.
8. Без доступу до preprod можна зробити ~70% роботи (транспорт, будівник пакета, валідація за схемою, outbox, воркер, UI, контрактні тести); preprod потрібен для: токенів/скоупів LABORANT, `Use` без програми, реальної форми job, перевірки CAdES-X Long, specimen та equipment (розд. 4).

---

## 1. Взаємодії ЛІС ↔ ЕСОЗ за життєвим циклом направлення

База: `https://api.ehealth.gov.ua`, заголовки на всіх викликах: `Authorization: Bearer <access_token>`, `api-key`, `X-Custom-PSK` [док: параметри в OpenAPI]. Асинхронні операції відповідають 202 + job; результат — `GET /api/jobs/{id}` (скоуп `job:read`).

### 1.1. Авторизація, токени, скоупи

| Поле | Зміст |
|---|---|
| eHealth | Редирект користувача на `auth.ehealth.gov.ua` (login+password, 2FA/КЕП виконує ЕСОЗ), далі обмін code → token: `POST /oauth/tokens` (також refresh) [док OpenAPI general-mis]. `GET /oauth/user` — дані поточного користувача. Скоупи запитуються не ширші за визначені специфікацією (ТВ 1.2.5) |
| Роль у ЕСОЗ | Працівник закладу з типом **LABORANT** (клієнт типу OUTPATIENT — 26 скоупів у таблиці). Допустимо реалізувати лише LABORANT (відповідь ЕСОЗ у Q&A: «можлива реалізація тільки для LABORANT, бо в ТВ є сполучник “або”»), тоді інші ролі можна не тестувати |
| Обов'язкове | `redirect_uri` з чинним TLS на домені, що вміє обробити відповідь (ТВ 1.2.3); access token з контролем `expiry date` і оновленням за refresh token (ТВ 1.2.4); `mis_client_secret` недоступний користувачу в жодному вигляді (ТВ 2.14); одна активна сесія користувача, вихід за 60 хв простою (ТВ 2.12-2.13); 2FA у самій МІС (ТВ 2.11) |
| Відповіді/помилки | 401 — токен прострочено; 403 `Missing allowances: <scope>` — немає скоупу (на preprod бувають «голі» 403 від шлюзу навіть при наявності скоупу в токені, напр. для `GET /api/jobs` без правила `job:read` [преприд]); 422 — валідація |
| MedLink [SDK] | Токени тримає проміжний сервер: SDK викликає `auth/EhealthUILink/{role}`, `auth` (обмін коду), `auth/accessToken`, `auth/hasAccessToken`, `auth/update` (refresh), `auth/logout` (`SDK/Endpoints/AuthEhealthService.cs:24-85`); у кожний запит додаються `ClientId` (id організації в ЕСОЗ) і `UserId` (`SDK/Endpoints/Base/BaseEhealthWebService.cs:29-47`). Проксі підставляє `X-Custom-PSK`/секрети клієнта за `x-aid` |
| ЛІС зараз | `EhealthClient`: один статичний `AccessToken` + `ApiKey` з конфігурації (`EhealthClient.cs:22-31,111-112`), немає `X-Custom-PSK`, немає refresh. `EhealthSignService.LoginAsync/ConfirmAsync` (`:123-167`) — **повна імітація**: пароль «будь-який», 2FA `123456` (`:51,158`), токен — випадкові 24 байти. Живого OAuth немає |
| Висновок | Замінити форму L1/L2 на «Підключитися до ЕСОЗ» (redirect) + окремий крок підпису КЕП; індикатор сесії в шапці лишити |

### 1.2. Конфігурація і довідники

| Поле | Зміст |
|---|---|
| eHealth | `GET /api/configurations/metadata` (не частіше 1/4 год і не рідше 1/добу — ТВ 1.1.8); `GET /api/dictionaries`, `/api/v2/dictionaries`; `GET /api/services` (каталог послуг, дерево до 4 рівнів, кінцеві вузли — послуги); `GET /api/observation_configurations` (типи значень, компоненти, одиниці для кодів спостережень); `GET /api/dictionaries_configurations` |
| Роль | LABORANT має `device_definition:read`, `division:read`, `legal_entity:read`; довідники публічні за api-key/PSK [док] |
| Обов'язкове | Для DR — `code` = послуга каталогу (`eHealth/resources`, code `service`), `category` з `eHealth/diagnostic_report_categories`; для observation — код LOINC (`eHealth/LOINC/observation_codes`), категорія, UCUM-одиниці (`eHealth/ucum/units`), інтерпретація (`eHealth/observation_interpretations`) |
| MedLink [SDK] | `DictionaryEhealthService.GetEhealthDictionariesAsync` (`SDK/Endpoints/DictionaryEhealthService.cs:22-29`); `ObservationsServiceEhealth.GetObservationMeasurementConfigurationAsync` (`:39-46`); пакет `SDK/Synchronization/*` (healthcare services, employees, equipment, drugs) |
| ЛІС зараз | `lab_ehealth_intervention` — **демо-набір `DEMO-…`** (`Data/Seed/MisCatalogSeeder.cs:97,124-157`), «синхронізація (мок)». Тип системи кодів у моделі `eHealth/ICPC2/process` (`Data/Entities/MisIntegrationEntities.cs:115`) для лабораторних ЕН хибний: у ЕСОЗ `code` ЕН/DR — це **послуга каталогу** (`service`), а не ICPC-2. Це закриває відкрите питання Q-16 ТЗ (джерело — `GET /api/services`, ресурс `service`) [док + SDK `DiagnosticReportJsonHelper.cs:23`] |

### 1.3. Приймання ЕН за номером (search) і деталі

| Поле | Зміст |
|---|---|
| eHealth | **Search**: `GET /api/service_requests?requisition=<номер>&status=active&page=&page_size=` (`page_size` 1-100, за замовчуванням 50). Якщо нічого немає — порожній список; без `requisition` — 422 [преприд]. **Details**: `GET /api/patients/{patient_id}/service_requests/{id}`. По зразку: `GET /api/specimens/{specimen_id}/service_requests` |
| Скоупи/роль | `service_request:read` (+ `service_request:read_impersonal` у LABORANT) [док: таблиця скоупів] |
| Корисні поля відповіді | `id`, `requisition`, `status` (active/completed/recalled/entered_in_error), `category` (для лабораторії `laboratory_procedure`), `code.identifier` (послуга), `subject.identifier.value` (id пацієнта — потрібен для шляху DR package), `identity` (last_name, first_name — ініціал, second_name — ініціал, gender, age), `expiration_date`, `program` / `program_processing_status` (new, in_queue, in_progress, completed), `remaining_quantity`, `used_by_legal_entity`, `used_by_employee`, `specimens`, `focus`, `priority`, `permitted_resources`, `note`, `patient_instruction`, `requester_employee/legal_entity` |
| Обмеження | ТВ 3.17.2.3.1: не збагачувати/не розкривати дані, що ідентифікують пацієнта, поза `identity` (у контексті зразка). Чутливі послуги: пошук за номером ЕН для лаборанта може не повернути ЕН; ЕСОЗ рекомендує шлях через **Specimen** (лаборант за зразком отримує навіть чутливі ЕН у знеособленому вигляді) або внутрішній процес, де асистент (з доступом пацієнта/approval) бере ЕН і передає лаборанту лише id і код послуги [Q&A ЕСОЗ] |
| Автоскасування | ЕН `active` із `expiration_date` (365 днів на prod) і без медичного запису з посиланням (`based_on`/`incoming_referral`) переходить у `recalled`, `status_reason = autoexpired` [док: Confluence «Service request autoexpiration»]. Створення DR з `based_on` захищає від автоскасування |
| MedLink [SDK] | `SearchMedicalReferralAsync` → `patients/service_requests{params}` (`SDK/Endpoints/MedicalReferralEhealthService.cs:87-93`); деталі `GetServiceRequestByPatientAsync` (`:55-60`); по епізоду `:47-52` |
| ЛІС зараз | `EhealthClient.SearchServiceRequestsAsync` (`:71-80`): `GET patients/service_requests?requisition=&status=active&page_size=&page=1`. **Шлях не збігається з OpenAPI** (`service_requests`). Але головне: `ReferralService.LookupAsync/ApplyAsync` (`:91-93,111-127,131-158`) спершу шукають ЕН у **локальному дзеркалі** `ehe_incoming_medical_referral` і кидають 404, якщо рядка немає; реєстр ЕСОЗ перевіряється лише для вже відомого рядка (`CheckInRegistryAsync`, `:35-43`). Мок наповнює «реєстр» із цього ж дзеркала (`EhealthMockHandler.cs`, `SearchAsync`) — **коло замкнене**: у Live ЕН, якої немає в БД, не знайти |

### 1.4. Перевірка права обробити ЕН (qualify) — лише для ЕН з програмою

| Поле | Зміст |
|---|---|
| eHealth | `POST /api/service_requests/{id}/actions/qualify`, тіло `{programs:[Reference medical_program]}` (`programs` обов'язкове); скоуп `service_request:use`; 200 + результат по кожній програмі |
| Правила ТВ | 3.17.3.1: LABORANT входить до ролей, що мають перевіряти можливість використання ЕН за програмою; негативний результат → «Результати перевірки не дають змоги використати електронне направлення» + назва параметра; 3.17.3.3.2 — при зміні/вказанні програми спершу qualify |
| MedLink [SDK] | `QualifyServiceRequestById` → `service_requests/{id}/actions/qualify` (`SDK/Endpoints/MedicalReferralEhealthService.cs:122-127`; тіло готує `EhealthMedicalReferralQualifyPrepareJsonService`) |
| ЛІС зараз | **Немає.** Програма в моделі відсутня |

### 1.5. Взяття ЕН у роботу (use)

| Поле | Зміст |
|---|---|
| eHealth | `PATCH /api/service_requests/{id}/actions/use`; тіло: `used_by_legal_entity` (**обов'язково**), `program` (**обов'язково за схемою**), `used_by_employee`, `used_by_division` — усі як `{identifier:{type:{coding:[{system:"eHealth/resources",code}]},value}}`; скоуп `service_request:use`; 202 + job |
| Статуси | Після успішного use для ЕН з програмою `program_processing_status` → `in_progress` (ТВ 3.17.3.3.3). Повторне використання іншим закладом — після `SERVICE_REQUEST_REUSE_AFTER_MINUTES` (ТВ 3.17.3.6). Статус-модель: «program processing status задається лише для ЕН з медичною програмою» |
| Важливо [?] | За схемою `program` обов'язковий. Отже **для лабораторних ЕН без програми use, імовірно, не застосовується** (ТВ 3.17.4.1 допускає погашення і з програмою, і без). Живий результат `use` без програми не підтверджено; на preprod зафіксовано обмеження «use без програми» у стенду [преприд, ENV_PROD_VS_PREPROD_AND_PLAN] |
| MedLink [SDK] | `UseMedicalReferralAsync` → `patients/service_requests/{id}/actions/use` (`SDK/Endpoints/MedicalReferralEhealthService.cs:96-101`); тіло завжди містить `program` (`SDK/Json/Services/EhealthMedicalReferralUsePrepareJsonService.cs:15-27`) |
| ЛІС зараз | `UseServiceRequestAsync` (`EhealthClient.cs:82-87`): `program` необов'язковий; `used_by_division` не передається; викликається в транзакції створення замовлення (`ReferralService.cs:158`) без компенсації при подальшому збої БД |
| Також | `PATCH …/actions/process` («Change status to In progress», скоуп `service_request:makeinprogress`) та `…/actions/release` (скоуп `service_request:use`) — **у OpenAPI позначені «Will be deprecated!»**. Не будувати на них нову логіку (SDK: `MedicalReferralEhealthService.cs:112-117,130-136`; ЛІС: `ReleaseServiceRequestAsync`, `:89-90`) |

### 1.6. Зразок біоматеріалу (Specimen)

| Поле | Зміст |
|---|---|
| eHealth | Створення: `POST /api/patients/{patient_id}/specimens` (скоуп `specimen:write`; LABORANT/ASSISTANT поза пакетом взаємодії — ТВ 3.32.1.1.2), **подписується КЕП** [?: в OpenAPI є `decoded-payload`]; прийом у лабораторії: `PATCH …/specimens/{id}/actions/process` (встановлює `received_time`, `specimen:process`, 202 async); `…/reject` (unsatisfactory, `specimen:reject`, причина `status_reason`); `…/invalidate` (unavailable, `specimen:invalidate`); `…/cancel` (entered_in_error, `specimen:cancel`, КЕП). Пошук: `GET /api/specimens/{accession_identifier}` (людиночитний номер; додатково повертає gender та age — ТВ 3.32.2.3.1), `GET /api/patients/{patient_id}/specimens`, деталі `…/specimens/{id}` |
| Обов'язкові поля Specimen | `id`, `type` (specimen_types), `status` (specimen_statuses), `collection.collector`, `collection.collected_date_time|collected_period`, `container[].identifier`; опційно `parent[]`, `request[]` (ЕН), `condition`, `quantity`, `method`, `body_site`, `fasting_status` |
| Роль | LABORANT має `specimen:read/write/process/cancel/reject/invalidate` [док]. Аліквота (дочірній зразок з `parent`) — самостійний запис; відповідь ЕСОЗ у внутрішньому узагальненні Q&A — окремий КЕП на кожну аліквоту [? — джерело не первинне, перевірити] |
| Зв'язок з DR | `diagnostic_report.specimens[]`, `observation.specimen` (ТВ 3.21.1.2.3, 3.19.1.4.2) |
| MedLink [SDK] | **Немає** (у SDK жодних викликів `specimens`) |
| ЛІС зараз | **Немає.** Біоматеріал є локально (`lab_order_sample`, штрихкоди, аліквоти), але без `ehealth_specimen_id`/`accession_identifier`, без обміну |

### 1.7. Обладнання (Equipment) — аналізатори як `used_references` / `device`

| Поле | Зміст |
|---|---|
| eHealth | Реєстрація: `POST /api/equipment` (прив'язка до division закладу); `PATCH /api/equipment/{id}/actions/change_status` та `…/change_availability_status`; перегляд: `GET /api/equipment`, `GET /api/equipment/{id}`. Скоупи `equipment:read/write` (LABORANT має обидва) |
| Правила ТВ | У `diagnostic_report.used_references` і `observation.device` можна вказати лише обладнання: тип `equipment`, зареєстроване в тому ж закладі, статус `active`, доступність `available` (ТВ 3.21.1.2.3.1, 3.19.1.4.2.1) |
| MedLink [SDK] | `EquipmentsEhealthService` (`SDK/Endpoints/EquipmentsEhealthService.cs:25-60`); DR: `used_references` (`SDK/Json/Services/EhealthDiagnosticReportPrepareJsonService.cs:30-46`), observation: `device` (`:70-73`, `ObservationJsonHelper.cs:~170`) |
| ЛІС зараз | `lab_analyzer` (`Data/Entities/ConnectorEntities.cs:33`) не має `ehealth_equipment_id`, статусів ЕСОЗ, не синхронізується |

### 1.8. Передача результату: Submit Diagnostic Report Package (diagnostic_report + observations)

| Поле | Зміст |
|---|---|
| eHealth | `POST /api/patients/{patient_id}/diagnostic_report_package`, тіло `{ "signed_data": "<base64(CAdES-X Long)>" }`; підписаний вміст — JSON `{ "diagnostic_report": {…}, "observations": […] }`. Діагностичний `decoded-payload` для побудови: `POST …/diagnostic_report_package/decoded-payload` (довідковий). Відповідь **202 + job** [док]. Скоупи: `diagnostic_report:write`, `observation:write` (LABORANT) |
| Вимоги підпису | ТВ 1.3.1-1.3.4: ЕП із атрибутами `certificate-values` і `revocation-values` (CAdES-X Long); вміст у BASE64; **перед підписом видалити U+FEFF (BOM)**; накладання ЕП без переходу на зовнішні ресурси |
| `diagnostic_report` — обов'язкове | `id` (UUID v1, генерується МІС — ТВ 1.1.7), `status = final`, `code` (послуга), `category` (рівно одне значення, узгоджене з категорією ЕН — ТВ 3.21.1.2.2.1), `issued` (UTC), `primary_source = true` (**лише true** у цьому методі), `performer` (employee/care_team Reference або текст), `recorded_by` (employee), `managing_organization` (legal_entity); `division` — опційно |
| `diagnostic_report` — опційне | `effective_date_time|effective_period`, `conclusion`, `conclusion_code`, `results_interpreter` (обов'язковий для категорій, визначених специфікацією), `specimens[]`, `used_references[]` (equipment), `origin_episode` |
| Посилання на направлення | ЕН: `based_on` = Reference `service_request` (+ перевірка відповідності послуги ЕН — ТВ 3.21.1.3.1). Паперове: блок `paper_referral` — **обов'язково** `requester_legal_entity_edrpou`, `requester_employee_name`, `service_request_date`; опційно `requisition`, `requester_legal_entity_name`, `note` (ТВ 3.21.1.3.2) |
| `observations[]` | Обов'язково: `id`, `status` (valid), `categories`, `code` (LOINC), `primary_source = true`, `issued`, одне зі значень `value_quantity|value_codeable_concept|value_string|value_boolean|value_range|value_ratio|value_time|value_date_time|value_period|value_sampled_data`, посилання `diagnostic_report`; `performer[]` (employee); опційно `effective_date_time|effective_period`, `interpretation`, `body_site`, `method`, `reference_ranges[]`, `components[]` (за `observation_configurations`), `comment`, `specimen`, `device` (equipment) |
| Відповіді/помилки | 202 job → `GET /api/jobs/{id}`; 404 (пацієнт); 422 — валідація тіла (list `invalid` за JSON-path, напр. `$.diagnostic_reports[0].performer[0].identifier.value`); 403 — права; 409 — конфлікт (дубль id, ЕН у неприпустимому статусі) |
| Ролі | LABORANT може реєструвати DR лише для послуг категорії `laboratory_procedure` (ТВ 3.21.1.1.1), спостереження — теж лише в складі DR package (ТВ 3.19.1.2). Типи виконавців DR задаються конфігом (DOCTOR, SPECIALIST, ASSISTANT, LABORANT [Q&A ЕСОЗ, конфіг-параметри `ENCOUNTER_PACKAGE_ALLOWED_DIAGNOSTIC_REPORT_PERFORMER_EMPLOYEE_TYPES` та аналогічний для DR package]); виправлено в ЕСОЗ 18.02 після звернення про неможливість кодувати observation з виконавцем-лаборантом [Q&A ЕСОЗ] |
| MedLink [SDK] | `CreateDiagnosticReportRequestAsync` → `patients/{patientId}/diagnostic_report_package` (`SDK/Endpoints/DiagnosticReportEhealthService.cs:20-25`); складання JSON: `EhealthDiagnosticReportPrepareJsonService.PrepareJson` (`SDK/Json/Services/EhealthDiagnosticReportPrepareJsonService.cs:15-61`, observations `:63-82`), `DiagnosticReportJsonHelper.GetBaseRequest` (`SDK/Json/Helpers/DiagnosticReportJsonHelper.cs:15-73`), `SetConclusionAndBasedOn` (`:75-89`), `SetPaperReferral` (`:91-115`), `BuildPerformers` (`:120-156`), `ObservationJsonHelper.GetObservation` (`SDK/Json/Helpers/ObservationJsonHelper.cs:129-330`, `SetValueQuantity` `:117-126`: `comparator "="`, `system "eHealth/ucum/units"`). Підписання в SDK немає (виконується на клієнті), повторів немає |
| ЛІС зараз | `PrepareAsync` (`EhealthSignService.cs:223-249`) будує **не той формат**: `resourceType:"diagnostic_report_package"`, `diagnosticReportId`, `patientId`, `observations:[{code,name,value,valueString,unit,interpretation}]`. Немає `performer/recorded_by/managing_organization/category/status/primary_source/specimens/used_references/based_on|paper_referral`, коди LOINC/UCUM не мапляться в Reference/Coding. `SendAsync` (`:285-313`) не робить HTTP-виклику: пише в журнал рядок `POST patients/{id}/diagnostic_report_package`, генерує `Guid` як job і id звіту, одразу ставить `REGISTERED/COMPLETED` |

### 1.9. Опитування job (результат асинхронної операції)

| Поле | Зміст |
|---|---|
| eHealth | `GET /api/jobs/{id}`, скоуп `job:read`. Статуси: pending (в черзі), processed, failed, failed_with_error тощо; HTTP 200/303/403/404/409/422/500 [док] |
| Правила ТВ | 1.1.3 таймаут синхронних запитів ≥ 15 с; 1.1.4: після отримання job у стані Pending користувачу показується можливість переглянути деталі; повторний виклик по тому ж job — лише коли попередній завершився й стан досі Pending; «Pending» — не успіх |
| Форма 202 [преприд, 08.11] | Відповідь містить `{status, eta, links:[{entity:"job", href:"/Jobs/<id>"}]}`, **`data.id` відсутній** — id треба брати з останнього сегмента `href` (референс-бібліотека: `ehc/src/job-poller.ts`, `extractJobId`). Також `status_code` у job буває рядком («409»), а не числом |
| MedLink [SDK] | `JobEhealthService.GetEhealthJobDetailsAsync` (`SDK/Endpoints/JobEhealthService.cs:41-74`): синхронне очікування в запиті користувача, `Thread.Sleep` 1→2→4→8→16 с (разом ≈ 31 с), далі `EhealthTimoutException`; `failed` → `EhealthInternalErrorException` з локалізованим текстом. Відновлення після таймауту — немає |
| ЛІС зараз | `GetJobAsync` (`EhealthClient.cs:99`) і `Job()` (`:101-105`) читають `data.id/status/entity_id`. Опитування не робиться ніде, крім мок-обробника, що завжди повертає `processed` |

### 1.10. Погашення ЕН (Complete)

| Поле | Зміст |
|---|---|
| eHealth | `PATCH /api/service_requests/{id}/actions/complete`; тіло: `completed_with` (Reference на `diagnostic_report` / encounter / procedure), `program_service` (для ЕН з програмою), `status_reason` (`eHealth/service_request_complete_reasons`, напр. `default`); відповідь 201 (синхронно) або 202 (job, за замовчуванням); скоуп `service_request:complete` |
| Умови | ЕН `active`; хоча б один пов'язаний медичний запис не `entered_in_error` (модель статусів ЕН); при ЕН з програмою — `program_processing_status = in_progress` (ТВ 3.17.4.3) і `completed_with` має посилатися на запис з `based_on` на цю ЕН та послугою з ЕН (ТВ 3.17.4.2); `remaining_quantity > 0` → повідомити користувача й дозволити зупинитися (ТВ 3.17.4.5); погасити може будь-який користувач закладу-виконавця (ТВ 3.17.4.4). `COMPLETED` — кінцевий статус |
| MedLink [SDK] | `CompleteMedicalReferralAsync` → `patients/service_requests/{id}/actions/complete` (`SDK/Endpoints/MedicalReferralEhealthService.cs:104-109`); тіло: `EhealthMedicalReferralCompletePrepareJsonService` (`SDK/Json/Services/EhealthMedicalReferralCompletePrepareJsonService.cs:14-62`; `completed_with` опускається для категорії hospitalization, `program_service` додається завжди, окрім режиму «без програми») |
| ЛІС зараз | Два шляхи, обидва хибні для Live: (а) `ReferralService.CompleteAsync` при видачі (`ReferralService.cs:193-203`, `OrderService.cs:459`) передає **локальний** `report.Id`, у транзакції видачі; (б) «complete» у мок-`SendAsync` лише пишеться в журнал (`EhealthSignService.cs:306-310`). `status_reason` не передається |

### 1.11. Відкликання/виправлення результату (Cancel Diagnostic Report Package)

| Поле | Зміст |
|---|---|
| eHealth | `PATCH /api/patients/{patient_id}/diagnostic_report_package` з підписаним пакетом, де `diagnostic_report.status = entered_in_error`, `cancellation_reason` (`eHealth/cancellation_reasons`), `explanatory_letter`, і **усі** пов'язані observations зі статусом `entered_in_error` (ТВ 3.21.3.3; список беруть `GET /api/patients/{id}/observations?diagnostic_report_id=…` — ТВ 3.21.3.4); 202 job; скоуп `diagnostic_report:cancel` |
| Виправлення | Редагування DR немає: **cancel + новий пакет з новими id і новим підписом** (так і задумано в ТЗ FR-SGN-009). Чи можна після `Complete` ЕН (кінцевий статус) знову погасити ту ж ЕН виправленим звітом [?] — перевірити на preprod; у ТВ про це прямої відповіді немає |
| MedLink [SDK] | `CancelDiagnosticReportRequestAsync` (`SDK/Endpoints/DiagnosticReportEhealthService.cs:36-41`), `EhealthDiagnosticReportCancelPrepareJsonService` (`SDK/Json/Services/EhealthDiagnosticReportCancelPrepareJsonService.cs:15-38`, observations `:41-56`); поля скасування `DiagnosticReportJsonHelper.cs:49-58,67-70` |
| ЛІС зараз | `AnnulAsync` (`EhealthSignService.cs:316-353`): мок. URL у журналі `…/diagnostic_reports/{id}/actions/cancel` **не існує в OpenAPI**; observations не збираються; копія звіту `RegNumber + "-1"` створюється коректно як ідея |

### 1.12. Повторна відправка, ідемпотентність, помилки

| Поле | Зміст |
|---|---|
| eHealth | Ідентифікатори записів створює МІС (UUID v1, ТВ 1.1.7), тому повтор з тим самим `id` безпечно виявляється як «уже існує» (409/422 [?]) — перед повтором після невизначеного результату перевіряти `GET /api/patients/{id}/diagnostic_reports/{id}` |
| MedLink [SDK] | Повторів, черги й ідемпотентності в SDK **немає** (пошук `retry|polly|backoff|idempot` у шарі HTTP порожній); помилка відображається користувачу, операція виконується заново вручну |
| ЛІС зараз | Задумано: зберігання `SignedData`, `Attempts`, `NextAttemptAt`, статуси `AUTH_REQUIRED/ERROR` (`LabEhealthSubmission`, `MisIntegrationEntities.cs:297-322`). **Воркера немає**: `NextAttemptAt` не читає жоден сервіс (у проєкті один `BackgroundService` — `RetentionCleanupService`). Ручний `RetryAsync` лише повторює мок |

### 1.13. Зміна статусу ЕН з боку ЕСОЗ (відкликана / скасована / прострочена)

| Поле | Зміст |
|---|---|
| eHealth | Пасивно: `GET /api/events?entity_type=ServiceRequest&…` (події `status`, `program_processing_status`) — потребує відповідного скоупу (у таблиці LABORANT окремого скоупу на events немає [?]); активно: перечитування `GET /api/patients/{pid}/service_requests/{id}` (скоуп `service_request:read`). Статуси: `active → completed | recalled (автоскасування/відкликання) | entered_in_error` |
| MedLink [SDK] | Приймач подій `ServiceRequestProcessor.ProcessAsync` оновлює `status_id` і `processing_status` дзеркала (вхідне й вихідне ЕН) за потоком подій (`SDK/EventManagerProcessors/Internal/ServiceRequestProcessor.cs:22-230`); джерело подій — окремий компонент MedLink (`MedlinkEventRequest`) |
| ЛІС зараз | Нічого. Відкликана ЕН, взята в роботу, залишається «активною» в ЛІС до ручної дії |

### 1.14. Пацієнт, декларація, права доступу, згода

| Поле | Зміст |
|---|---|
| eHealth | Пацієнт: `GET /api/persons`, `/api/persons/{id}/personal_data`, `/declaration` (скоуп `person:read` — **у LABORANT немає**). Згода (approval): `POST /api/patients/{id}/approvals` (скоуп `approval:create/read/cancel` — немає у LABORANT, є у ASSISTANT/DOCTOR), код підтвердження приходить SMS пацієнту |
| Наслідки для лабораторії | Лаборант працює лише з тим, що віддає ЕН: `subject.identifier.value` (id пацієнта для шляху DR) та `identity` (прізвище, ініціали, стать, вік). Для чутливих ЕН — шлях через Specimen (знеособлено) або внутрішня передача від асистента, що має approval. Не запитувати `person:*` для лаборанта, не зберігати зайві ПД |
| MedLink [SDK] | `PersonEhealthService` (`SDK/Endpoints/PersonEhealthService.cs:26-190`), `ApprovalEhealthService` (`:21-101`) — використовуються лікарськими модулями, не лабораторією |
| ЛІС зараз | Немає. Пацієнти беруться з локальної картки (`MisPatientCard`), перевірки відповідності ЕН пацієнту — за локальним `PatientCardId` (`ReferralService.cs:102`), а не за `subject` із ЕСОЗ |

### 1.15. Довідкові сутності для полів DR: працівники, ролі, підрозділи, організація

| Поле | Зміст |
|---|---|
| eHealth | `GET /api/employees`, `/api/employee_roles`, `/api/divisions`, `/api/v2/legal_entities/{id}`, `/oauth/user` — для зіставлення локальних співробітників з ЕСОЗ-employee (виконавець `performer`, `recorded_by`, `results_interpreter`) і division (`managing_organization`, `division`). Скоупи LABORANT: `employee:read`, `employee_role:read`, `division:read`, `legal_entity:read` |
| MedLink [SDK] | `EmployeeEhealthService`, `DivisionEhealthService`, `LegalEntityEhealthService`; синхронізатори `SDK/Synchronization/Processors/EhealthSynchronizationEmployeesProcessor.cs` та ін. |
| ЛІС зараз | `DivisionId`/`DocumentTypeId` у `MisDiagnosticReport` заповнюються `EmptyGuid` (`OrderService.cs:447-455`); виконавець — рядок `PerformerString` (`IsPerformerString = true`). Для Live потрібні `performer` як Reference employee (або текст — допустимо специфікацією) і справжні `managing_organization`/`division` |

### 1.16. Паперове направлення

Немає окремого виклику ЕСОЗ: DR створюється без `based_on`, але з блоком `paper_referral` (поля в розд. 1.8). ЛІС зберігає `EhePaperMedicalReferral` (`ReferralService.cs:159-176`) — достатньо для заповнення блока; додатково потрібні `requester_legal_entity_edrpou`, `requester_employee_name`, `service_request_date` як обов'язкові (валідація при реєстрації замовлення, а не при відправці).

---

## 2. Таблиця: що є / що лише mock / чого немає

| № | Взаємодія | MedLink [SDK] | ЛІС: є (реальне) | ЛІС: лише mock | ЛІС: немає |
|---|---|---|---|---|---|
| 1 | OAuth-вхід, токени per-user, refresh | через проміжний сервер | — | форма пароль+2FA `123456`, випадковий токен (`EhealthSignService.cs:123-177`) | OAuth code flow, `X-Custom-PSK`, зберігання/оновлення токенів |
| 2 | Конфіг/довідники/каталог послуг | так | таблиці-дзеркала | демо-інтервенції `DEMO-` | синхронізація `GET /api/services`, `observation_configurations`, довідників |
| 3 | Search ЕН за номером | так | HTTP-клієнт (`EhealthClient.cs:71-80`), але шлях ≠ OpenAPI | відповіді мок-обробника з локального дзеркала | прийом ЕН без локального рядка; деталі ЕН; пошук по зразку |
| 4 | Qualify (програма) | так | — | — | усе |
| 5 | Use / Release / Process | так (use/release/process) | клієнт (`:82-97`), виклик при створенні замовлення | стан у пам'яті мок-обробника | `used_by_division`, програма, компенсація при збої, перехід на не-deprecated сценарій |
| 6 | Specimen: create/process/reject/invalidate/cancel/search | немає | локальні зразки | — | усе |
| 7 | Equipment: реєстрація/статус/доступність | так | — | — | усе, поле `ehealth_equipment_id` |
| 8 | Submit DR package (diagnostic_report + observations) | так, без черги | локальний висновок `mis_diagnostic_report`, місце під `SignedData` | `PrepareAsync`/`SendAsync` — вигаданий формат, `Guid` замість job | схема eHealth, КЕП (CAdES-X Long), HTTP-виклик |
| 9 | Job polling | синхронно ≈ 31 с | — | мок завжди `processed` | поллінг у воркері, форма job з `links` |
| 10 | Complete ЕН | так | виклик при видачі з локальним id | мок | порядок (після DR), `status_reason`, ретраї |
| 11 | Cancel DR package | так | копія звіту `-1` | URL, якого немає в OpenAPI | пакет з observations `entered_in_error`, `cancellation_reason` |
| 12 | Черга повторів/ідемпотентність | немає | поля `Attempts`, `NextAttemptAt` | ручний `RetryAsync` | воркер, backoff, класифікація помилок, UUID v1 |
| 13 | Події/перечитування статусу ЕН | так (event manager) | — | — | усе |
| 14 | Пацієнт/декларація/згода | так (для лікарських модулів) | — | — | (для лаборанта — не потрібно, див. 1.14) |
| 15 | Працівники/підрозділи/організація | так | локальні довідники | `EmptyGuid` у DR | зіставлення з ЕСОЗ-id |
| 16 | Журнал обміну | часткове | `lab_ehealth_exchange_log` (`EhealthClient.cs:136-145`) | — | маскування ПД/токенів, зв'язок із записом черги, ретеншн |
| 17 | Режим Live/Mock | — | `Ehealth:Mode` | Mock за замовчуванням; Live → 503 для всього підписання | поопераційні перемикачі, Replay |

---

## 3. Рекомендований дизайн для лабораторії

### 3.1. Принципи (чим відрізняємось від MedLink і чому)

| Принцип | MedLink | Наша лабораторія — рішення | Обґрунтування |
|---|---|---|---|
| Виклики ЕСОЗ у запиті користувача чи у фоні | Синхронно, `Thread.Sleep` до ≈ 31 с, потім таймаут | **Outbox + воркер**; запит користувача лише ставить задачу | Результат — юридичний документ із КЕП, який не можна втратити; ЕСОЗ може бути недоступна годинами (ТЗ FR-SGN-008: повтор до 24 год); лабораторія видає десятки результатів на хвилину в пік |
| Погашення ЕН | `Complete` при видачі з локальним id | **`Complete` лише після `processed` job DR**, окрема задача зі своїми повторами | Умова ЕСОЗ: медичний запис має існувати; розв'язує дві причини збою (DR vs Complete) і не блокує видачу пацієнту |
| Джерело істини про ЕН | Дзеркало БД + проксі | **Дзеркало — кеш**: спочатку `GET /api/service_requests`, потім upsert дзеркала за `ehealth_id` | Лабораторія отримує ЕН ззовні; без локального рядка нічого не знайти (розд. 1.3) |
| Авторизація | Проксі з секретами, `ClientId/UserId` | **OAuth per-user** (redirect на `auth.ehealth.gov.ua`) із шаром `IEhealthTokenProvider`; режим «проксі MedLink» — друга реалізація інтерфейсу | ТВ 1.2.2: власні форми входу заборонені; у складі MedLink проксі вже є — не дублюємо |
| Підпис КЕП | Підпис на клієнті, поза SDK | `IEhealthSigner`: `browser` (віджет, основний), `mock` (тест), далі `cloud/agent`; сервер перевіряє структуру (CAdES-X Long: `certificate-values`/`revocation-values`), BOM, зберігає незмінно | ТВ 1.3.x; ключ лаборанта на сервер не потрапляє |
| Ідемпотентність | Немає | UUID v1 для `diagnostic_report.id`/`observation.id` створюються при побудові пакета й не змінюються; перед повтором після невизначеного результату — `GET diagnostic_reports/{id}` | ТВ 1.1.7; повтор безпечний, дублі неможливі |
| Помилки | Виняток користувачу | **Класифікація**: retryable (мережа/5xx/429/pending) · auth (401 → refresh/`AUTH_REQUIRED`) · permission (403 → `NEEDS_ACTION`, не повторювати) · validation (409/422 → `NEEDS_ACTION` із текстом українською і сирою відповіддю) | Воркер не «довбить» ЕСОЗ даремно (дефект, відомий в аналогічних чергах); лаборант бачить, що саме виправити |
| Протокол | HTTP + мапери в SDK | Типізований клієнт: **одна константа шляху на операцію OpenAPI**, контрактні тести проти схем `dist/*.json` | Уникає розбіжностей на кшталт `patients/service_requests` ↔ `service_requests` |
| Ролі | Лікарські модулі | Лабораторне робоче місце — LABORANT; «Прийом ЕН/зразка» і «Дослідження» розділені правами ЛІС | Скоупи LABORANT вузькі (немає person/approval); чутливі ЕН — через Specimen |

### 3.2. Шари сервісів (.NET, `LIS/Services/Ehealth/`)

```
IEhealthTransport            HTTP: base url, Bearer + api-key + X-Custom-PSK, таймаут ≥ 15 с, request-id, rate limit, лог (з маскуванням)
IEhealthTokenProvider        GetAccessToken(employeeId) / Refresh / Revoke; реалізації: OAuthUserTokenProvider, MisProxyTokenProvider, StaticTokenProvider (лише Mock/dev)
IEhealthApi (типізований)    SearchServiceRequests, GetServiceRequest, Qualify, Use, Complete, SubmitDiagnosticReportPackage, CancelDiagnosticReportPackage,
                             GetJob, GetDiagnosticReport, SearchObservations, specimens/*, equipment/*, GetServices, GetObservationConfigurations …
                             (кожен метод = 1 операція OpenAPI; модель відповіді з полем RawJson)
EhealthJobReader             витяг job id: data.id АБО links[entity=job].href; нормалізація статусів (pending / processed / failed*)
EhealthReferralService       приймання ЕН (search → upsert дзеркала → придатність), refresh статусу, use/release за правилами програми
EhealthPackageBuilder        LabOrder → {diagnostic_report, observations[]}; mapping LOINC/UCUM/interpretation; перевірка готовності (список причин)
IEhealthSigner               Prepare(payload) → canonical bytes (UTF-8, без BOM) → base64; Verify(signed_data) → signer/cert/CAdES-X-Long; провайдери browser|mock
EhealthOutbox + Worker       BackgroundService: вибірка due-задач, виконання, поллінг job, перехід станів, метрики
EhealthExchangeLog           запис кожного HTTP-обміну, посилання на outbox-запис, маскування ПД
```

`IEhealthReferralGateway` (теперішній) лишається як адаптер для старого `ReferralService`, але його реалізація переходить на `EhealthReferralService`.

### 3.3. Режими і безпечний перехід до Live

`Ehealth:Mode = Mock | Replay | Live`:
- **Mock** — для розробки; мок-обробник повинен бути *контрактним двійником*: ті ж шляхи, що в OpenAPI; 201/202; job із `links` без `data.id`; помилки `{error:{type,message}}` і `{meta:{code}}`; 403 `Missing allowances`; валідація тіла за JSON-схемою з OpenAPI; стан поза пам'яттю процесу (SQLite/файл) і не з локального дзеркала;
- **Replay** — HTTP-обробник, що відповідає *записаними фікстурами* (власні знімки відповідей preprod і приклади з OpenAPI) — для CI;
- **Live** — реальні виклики. Додатково `Ehealth:Live:Operations` — прапорці по операціях: `SearchRead` → `Use` → `Complete` → `SubmitDR` → `CancelDR` → `Specimen` → `Equipment`. Увімкнення по одній дає безпечну поетапну обкатку (спершу лише читання).

### 3.4. Модель даних (додатково до наявних `lab_ehealth_submission`, `lab_ehealth_exchange_log`)

Нове або розширене:

| Таблиця / поле | Призначення |
|---|---|
| `lab_ehealth_outbox` | `id`, `kind` (`SUBMIT_DR`, `POLL_JOB`, `COMPLETE_SR`, `CANCEL_DR`, `USE_SR`, `REFRESH_SR`, `SPECIMEN_*`, `EQUIPMENT_*`), `order_id`, `submission_id`, `dedup_key` (kind+entity+version, унікальний), `state` (`NEW`, `READY`, `SIGN_PENDING`, `SENDING`, `WAIT_JOB`, `DONE`, `FAILED`, `NEEDS_ACTION`), `attempts`, `next_try_at`, `locked_by/locked_until`, `last_error_code`, `last_error_text`, `job_id`, `created_on`, `done_on` |
| `lab_ehealth_submission` (розширити) | `report_ehealth_id` (UUID v1, фіксується при побудові), `package_json` (канонічний, без BOM), `package_hash`, `signed_data` (base64, **незмінний**), `signer_drfo/cert_serial/cert_valid_to`, `cades_profile` (результат перевірки CAdES-X Long), `kind` (`SUBMIT`/`CANCEL`), `supersedes_id` (виправлення), `job_id`, `job_status`, `job_response_json` |
| `lab_ehealth_observation_map` | відповідність локального тесту ↔ LOINC ↔ UCUM ↔ `observation_configuration` (тип значення, компоненти) ↔ категорія; позначка покриття |
| `ehe_incoming_medical_referral` (дзеркало) | `+ ehealth_status`, `program_id`, `program_processing_status`, `remaining_quantity`, `used_by_legal_entity`, `last_synced_at`, `raw_json` (знеособлений) |
| `lab_analyzer` | `+ ehealth_equipment_id`, `ehealth_equipment_status`, `ehealth_availability` |
| `lab_order_sample` | `+ ehealth_specimen_id`, `accession_identifier`, `ehealth_status`, `parent_specimen_id` |
| `org_employee` / налаштування закладу | `ehealth_employee_id`, ролі ЕСОЗ, `ehealth_legal_entity_id`, `ehealth_division_id`; `lab_ehealth_connection` (середовище, client_id, посилання на секрети — самі секрети лише в сховищі секретів) |
| `lab_ehealth_session` (змінити) | замість L1/L2-пароля — per-user токени (`access_enc`, `refresh_enc`, `expires_at`, `scopes`), прив'язка до працівника; пароль не існує в моделі |

Усі колонки — snake_case, GUID-рядки, типи, сумісні з PostgreSQL (вимога CLAUDE.md проєкту).

### 3.5. Статуси (три незалежні осі замість однієї `EhealthStatus`)

1. **Направлення (дзеркало)**: `ACTIVE` · `IN_WORK` (use виконано/неприпустиме) · `COMPLETED` · `RECALLED` · `ENTERED_IN_ERROR` · `EXPIRED`; окремо `program_processing_status`.
2. **Результат (submission)**: `NOT_READY` · `READY_TO_SIGN` · `SIGNED` · `SENDING` · `REGISTERED` (job processed) · `REJECTED` (job failed) · `CANCEL_PENDING` · `CANCELLED`.
3. **Синхронізація (outbox)**: `QUEUED` · `RETRY_WAIT` · `NEEDS_AUTH` · `NEEDS_ACTION` · `DONE`.

Наявні значення `NOT_SENT/AUTH_REQUIRED/READY_TO_SIGN/SIGNED/PROCESSING/REGISTERED/COMPLETED/REJECTED/ERROR/ANNULLED` (`MisIntegrationEntities.cs:86-100`) лишаються як обчислюваний підсумковий бейдж для сумісності з UI і ТЗ 21.13.

### 3.6. Послідовність (рекомендована)

1. **Прийом ЕН**: `GET /api/service_requests?requisition=` → перевірки (status=active; `category=laboratory_procedure`; `expiration_date`; `remaining_quantity>0`; `used_by_legal_entity` порожнє або наш/мине час повторного використання; послуга ЕН має профіль у ЛІС) → upsert дзеркала. Якщо ЕН не знайдена — підказка про чутливі послуги і шлях через зразок.
2. **Qualify/Use** — лише якщо в ЕН є `program` (qualify → use з `program`, `used_by_legal_entity`, `used_by_employee`, `used_by_division`); без програми — пропуск [? підтвердити на preprod].
3. **Створення замовлення** — локально; ЕСОЗ-операції ставляться в outbox *після* коміту (`USE_SR`), помилка не відкочує замовлення, а переходить у `NEEDS_ACTION` (з можливістю продовжити).
4. **Прийом зразка**: за потреби `specimens/{id}/actions/process` (`received_time`), `reject/invalidate` при браку.
5. **Результати → видача** (`RELEASED`): будівник пакета формує `diagnostic_report` + `observations` (id UUID v1 фіксуються), валідатор перевіряє готовність.
6. **Підпис КЕП** (браузерний віджет): сервер перевіряє підписувача (ДРФО працівника), CAdES-X Long, BOM; зберігає `signed_data` незмінним.
7. **`SUBMIT_DR`** → 202 → `WAIT_JOB`: воркер опитує з паузою за ТВ 1.1.4 (наступний виклик після завершення попереднього; рекомендовані паузи 2/5/10/30 с, далі кожні 1-5 хв до 24 год).
8. **`COMPLETE_SR`** (після `processed`) → job → `COMPLETED` у дзеркалі. Для ЕН з програмою: `program_service`, `program_processing_status=in_progress`.
9. **Виправлення**: підписаний `CANCEL_DR` (усі observations `entered_in_error`, `cancellation_reason`) → новий пакет (нові id, новий підпис, `supersedes_id`).
10. **Звільнення**: якщо замовлення скасоване до результату — `release` (поки доступний) або просто залишити ЕН у ЕСОЗ без дій (use без програми не фіксує блокування) [?].
11. **Моніторинг ЕН**: періодичний `REFRESH_SR` для ЕН у роботі (відкликана/скасована → попередження в замовленні).

### 3.7. UI: екрани і поля

| Екран | Що показує / що робить | Ключові поля |
|---|---|---|
| **Приймання ЕН** (діалог створення замовлення + окрема сторінка «Е-направлення») | Пошук за номером/QR, картка ЕН з причинами неможливості обробки | номер, статус, категорія, послуга (+ відповідний профіль ЛІС), пацієнт (`identity`: ПІБ-ініціали, стать, вік), строк дії, програма/статус, залишок, хто використовує, чутлива-послуга-підказка |
| **Картка замовлення → блок «ЕСОЗ»** | Стрічка етапів: ЕН взято → зразок прийнято → результат виданий → підписано → відправлено → job → зареєстровано → ЕН погашено | бейдж за трьома осями, id DR/job, час кожного етапу, кнопки: «Підписати й відправити», «Повторити», «Скасувати/виправити звіт», «Оновити статус ЕН», текст помилки українською + «показати відповідь ЕСОЗ» |
| **Відправка в ЕСОЗ** (поточна `EhealthSubmissionsPage`) | Робоча черга: фільтри за станом/помилкою/терміном; масовий повтор; вік найстаршої задачі | номер замовлення, пацієнт, стан outbox, спроби, наступна спроба, код помилки, відповідальний |
| **Налаштування → ЕСОЗ** | З'єднання і готовність | середовище, client_id, посилання на секрети (лише статус/last4), `legal_entity`, `division`, перевірка скоупів токена, покриття мапінгів (послуги/LOINC/UCUM/обладнання/співробітники), останній успішний обмін, вік черги |
| **Обладнання** | Реєстрація аналізатора в ЕСОЗ | ehealth_equipment_id, статус/доступність, кнопки реєстрації/зміни |
| **Співробітники** | Зіставлення | ehealth_employee_id, роль LABORANT, дані сертифіката (серійний номер, строк, ДРФО) |
| **Журнал обміну** | Аудит HTTP | час, операція, endpoint, код, тривалість, correlation з outbox, ПД замасковано |
| **Вхід в ЕСОЗ** (шапка) | Індикатор сесії + «Підключитися» (redirect) + «Вийти» | стан токена, термін, скоупи; пароль не вводиться |

### 3.8. Безпека і відповідність

- Токени та секрети клієнта — лише на сервері, шифровано (AES-GCM) із ключем поза БД; у відповіді API/логах — лише статус і last4 (ТВ 2.14).
- Журнал обміну: тіла запитів містять ПД → зберігати зашифровано або маскувати, обмежити ретеншн, не писати токени (дефект, якого уникає дизайн).
- Підпис особистий (FR-SGN-011): ДРФО підписувача має збігатися з працівником; делегування заборонене.
- Лише TLS, CSP з дозволом для скриптів віджета підпису, захист від brute-force (5 спроб/15 хв, блок 5 хв — ТВ 2.10).
- `mis_client_secret` ніколи не потрапляє у фронтенд.

---

## 4. Покроковий план реалізації

Оцінки — людино-дні (чорновий діапазон), ризик: Н — низький, С — середній, В — високий.

| Етап | Зміст | Оцінка | Ризик | Потрібен preprod? |
|---|---|---|---|---|
| **0. Узгодження і виправлення вимог** | Оновити ТЗ: прибрати форму пароля ЕСОЗ (FR-SGN-002), закрити Q-16 (довідник = каталог `service`), Q-19 (OAuth per-user), порядок «DR → Complete», use лише для ЕН з програмою [?]; додати Specimen/Equipment у R-плани | 1-2 | Н | ні |
| **1. Контракт і транспорт** | `IEhealthTransport`, `IEhealthApi` з правильними шляхами, читання job із `links`, класифікація помилок, `X-Custom-PSK`, таймаут ≥ 15 с, маскований журнал; Replay-обробник; контрактні тести проти схем `dist/prod/medical-events.json` (JSON-схеми запитів) та фікстур | 4-5 | Н | ні |
| **2. Прийом ЕН без залежності від дзеркала** | Search → upsert дзеркала; деталі ЕН; перевірки придатності; refresh статусу; qualify/use за наявності програми; звільнення; виправити `LookupAsync/ApplyAsync`; read-only live-режим | 4-5 | С | для живої перевірки — токен LABORANT і тестова ЕН |
| **3. Будівник пакета DR + observations** | Мапінг: послуга каталогу, категорія DR, LOINC/UCUM/interpretation, `reference_ranges`, `components` за `observation_configurations`, `performer/recorded_by/managing_organization/division`, `specimens`, `used_references`, `based_on` / `paper_referral`; перевірки готовності; golden-file тести; валідація за схемою OpenAPI | 5-7 | В (покриття LOINC/одиниць у довідниках лабораторії; типи значень) | ні (схеми й приклади локально); перевірка на preprod — етап 9 |
| **4. Підпис КЕП** | Інтерфейс підписувача; інтеграція браузерного віджета; серверна перевірка CAdES-X Long, ДРФО, BOM; зберігання `signed_data`; Mock-підписувач; прибрати `btoa('CAdES-DEMO…')` | 4-6 | В (вибір віджета/ЦСК — відкрите Q-21; формат довгострокового підпису) | для валідації формату бажані тестові сертифікати |
| **5. Outbox і воркер** | Таблиці, машини станів, `BackgroundService`, блокування (PG `FOR UPDATE SKIP LOCKED`; у SQLite — один воркер), backoff, поллінг job, `COMPLETE_SR` після `processed`, ідемпотентність, метрики; міграція з поточних `lab_ehealth_submission` | 5-6 | С | ні (на Replay/Mock) |
| **6. Авторизація OAuth** | Redirect на `auth.ehealth.gov.ua`, обробка callback, збереження per-user токенів, refresh, відкликання; провайдер «проксі MedLink»; зняття L1/L2-логіки | 4-5 | С-В (клієнт МІС vs клієнт закладу: різні набори скоупів і redirect_uri; шлюзові 403) | **так** для завершення; решта — локально |
| **7. Specimen та Equipment** | Поля й мапінг; `specimens` create/process/reject/invalidate/cancel (підписані дії); `equipment` реєстрація/статуси; пошук ЕН «за зразком» як шлях для чутливих ЕН | 6-8 | С-В | так (обидва) |
| **8. UI і журнал** | Екрани розд. 3.7, i18n (ru/uk/en за правилами проєкту), стрічка етапів, масові дії, налаштування, метрики черги | 5-7 | Н | ні |
| **9. Live-обкатка на preprod** | Сценарії S1-S10 (нижче), фіксація відповідей у фікстури, вмикання операцій по одній | 4-6 | В (залежить від доступів) | **так** |
| **10. Виправлення/відкликання, події** | Cancel DR package, виправлені звіти, `REFRESH_SR`/events, повідомлення про відклик ЕН | 3-4 | С | частково |

Разом ≈ 40-55 днів; етапи 0-5, 8 (≈ 30 днів) виконуються без доступу до ЕСОЗ.

### 4.1. Що робити без доступу до preprod

1. **Контрактні тести на фікстурах**: витягти з OpenAPI приклади й схеми (`components/schemas`: `Submit_DR_Package_DecodedPayload`, відповіді Search/Details/Job); сформувати набір Replay-фікстур (успіх, 202 з `links`, `failed`, 403 `Missing allowances`, 409/422 з list `invalid`, 404, 5xx/429, таймаут).
2. **Побудова пакета й golden-тести**: еталонні JSON для типових аналізів (загальний аналіз крові, біохімія, мікробіологія з компонентами), перевірка схемою.
3. **Воркер/outbox** на Replay — емуляція збоїв (мережа, 401, 403, 422, job pending→processed, job failed) і перевірка ідемпотентності (повтор після «невідомого» результату).
4. **Live-режим за прапорами**: код Live повністю присутній, але вимкнений; `EnsureMock` видалити; при `Mode=Live` і порожній конфігурації — зрозуміла діагностика «з'єднання не налаштовано».
5. **Мок як двійник**: переписати `EhealthMockHandler` на шляхи OpenAPI та форму job із `links`; прибрати залежність реєстру мока від локального дзеркала.
6. **Read-only перевірка Live** (потрібен лише токен із `service_request:read`): `GET /api/service_requests?requisition=…` і `GET /api/services` — найбезпечніший перший живий крок.

### 4.2. Сценарії для preprod (потрібні доступи)

| № | Сценарій | Потрібне |
|---|---|---|
| S1 | OAuth-вхід LABORANT, перевірка отриманих скоупів проти таблиці | обліковий запис LABORANT (на момент останньої перевірки такої окремої учётки не було; потрібен `employee_request` від OWNER закладу) |
| S2 | Search ЕН за номером: звичайна й «чутлива» послуга | ЕН лабораторної категорії, тестовий пацієнт |
| S3 | Use без програми / з програмою; Complete без програми | ЕН без програми та з програмою (+ `program_service`) |
| S4 | Реєстрація обладнання, перевірка `active/available` | право `equipment:write`, division |
| S5 | Створення Specimen + `process`, пошук за `accession_identifier` | `specimen:*` |
| S6 | Submit DR package (1 observation, потім з компонентами/діапазонами) + job | тестовий сертифікат КЕП (CAdES-X Long) |
| S7 | Повтор того ж пакета (ідемпотентність) і відповідь на дубль id | S6 |
| S8 | Complete ЕН після DR; перевірка статусу ЕН | S6 |
| S9 | Cancel DR package (з observations) і новий виправлений пакет; повторне погашення тієї ж ЕН | S8 |
| S10 | Реакція на відкликану/автоскасовану ЕН; 403 на `GET /api/jobs` без правила | — |

---

## 5. Відкриті питання і розбіжності джерел

1. **Use без програми** — схема вимагає `program`; ТВ 3.17.4.1 допускає Complete без програми. Підтвердити на preprod (S3). До цього — use лише з програмою.
2. **Чи може LABORANT шукати ЕН за номером.** Внутрішній довідник-узагальнення консультацій стверджує «ні» (немає `service_request:read_impersonal`), але **офіційна таблиця скоупів** (версія 38 від 24.09.2026) дає LABORANT саме `service_request:read`, `read_impersonal`, `use`, `complete`, `makeinprogress`, а ТВ 3.17.3.1 прямо називає LABORANT серед тих, хто перевіряє/використовує ЕН. Довідник — вторинне джерело, йому не довіряти; перевіряти фактом (S1-S2). Для чутливих ЕН підтверджено лише шлях через Specimen (Q&A ЕСОЗ: «функціональність розроблена в проекті “облік зразку біоматеріалу”»).
3. **Таблиця скоупів може розходитися з фактом** на preprod (виявлено для інших ролей); тип клієнта МІС дає ≈ 22 скоупи проти повного набору ролі в клієнта закладу. Перевірити, який клієнт використовує лабораторія.
4. **Категорія DR** для лабораторії: значення з `eHealth/diagnostic_report_categories` (імовірно код лабораторної категорії) — взяти з `GET /api/dictionaries`, не припускати.
5. **Виправлення після Complete** — можливість повторного погашення виправленим звітом не описана (кінцевий статус `COMPLETED`).
6. **Подпис Specimen/дій КЕП** — які саме дії зі зразком вимагають підпису (цілком: create/cancel; для process/reject/invalidate — перевірити).
7. **Відомі ризики preprod**: «голі» 403 шлюзу при коректних скоупах; `GET /api/jobs` без правила `job:read`; поведінка job відрізняється від документації (див. 1.9).
8. **Q-21 (віджет підпису)** залишається вирішальним для етапу 4: потрібен вибір ЦСК/віджета, що видає CAdES-X Long без виходу на зовнішні ресурси (ТВ 1.3.4).
9. Проєкт ТВ v1.40 (чернетка) у блоках 3.17, 3.19, 3.21 змін для лабораторії не містить; у 3.32 (Specimen) змінено три пункти (3.32.1.1.1, 3.32.1.2.1, 3.32.3.4) — перевірити після затвердження.

## 6. Джерела (відносні шляхи)

- ТВ МІС v1.39: `EHEALTH/MIS/05_text_extracts/tv39.txt` (1.1-1.3, 2.10-2.14, 3.17.1-3.17.4, 3.19.1, 3.21.1-3.21.3, 3.32.*).
- OpenAPI eHealth (2026-10-27): `EHEALTH/MIS/06_git_sources/api-docs/dist/{prod,preprod}/medical-events.json`, `general-mis.json`.
- Модель статусів ЕН, автоскасування, бізнес-процеси ЕН: `EHEALTH/MIS/04_links/confluence/583403541.md`, `583403665.md`, `583402291.md`.
- Таблиця скоупів ролей: `EHEALTH/MIS/04_links/confluence/19600179308.md` (рядки LABORANT).
- Q&A консультацій ЕСОЗ: `EHEALTH/MIS/04_links/sheets/MIS_consultations_QA.csv` (питання про лабораторії, чутливі ЕН, LABORANT).
- MedLink (SDK МІС evomis, експорт 2026-10-27): `EHEALTH/Medlink/evomis_phrm-prod_2026-10-27/src/App.EhealthSdk/`.
- Референс-бібліотека інтеграції (власна, TS): модулі `ehc` (`job-poller.ts`, `http-client.ts`, `rate-limiter.ts`), `ehm` (`service-request-flow-rules.ts`, `observation-flow-rules.ts`), `ehm-kep` (підпис); ADR-005 (outbox/inbox), ADR-008 (КЕП).
- ЛІС: `LIS/Services/Ehealth/*`, `LIS/Services/ReferralService.cs`, `LIS/Controllers/ReferralsController.cs`, `LIS/Controllers/EhealthSignController.cs`, `docs/ТЗ_ЛІС_MedLink_v4.md` (розд. 10.2, 20.6, 21.13-21.15), `docs/AUDIT_MOCKS.md` (рядки про ЕСОЗ).

### Що перевірено, а що ні

Перевірено читанням: код ЛІС (клієнт, мок-обробник, сервіс підпису, контролери, сервіс направлень, видача замовлення), код SDK МІС evomis, OpenAPI (шляхи, скоупи, обов'язкові поля Use/Complete/Qualify/DR package/Specimen), ТВ v1.39, таблиця скоупів, Q&A. Не запускалося: збірка/тести ЛІС, живі виклики ЕСОЗ (їх у цьому дослідженні не було). Усі твердження з позначкою [?] — припущення; з позначкою [преприд] — з результатів іншої групи робіт (референс-бібліотека), не відтворені тут. У OpenAPI тіло DR package описане через посилання на схему (`Submit_DR_Package_DecodedPayload`) — перелік полів DR/observation у розд. 1.8 зібрано з цієї схеми, ТВ та SDK; точний перелік обов'язкових полів observation за типами значень слід звірити з `observation_configurations` при реалізації етапу 3.

## 7. Стан виконання п. 4.1 (без preprod)

Виконано в коді (09.11.2026), перевірено автотестами `tests/MedLink.LIS.Tests` у режимах Mock і Replay; ТЗ — розд. 21.13.1 (FR-SGN-015…023):

1. **Клієнт за OpenAPI**: шляхи `/api/service_requests…`, `/api/patients/{id}/diagnostic_report_package`, `/api/jobs/{id}`; id job з `links[entity=job]`; 202 без `data.id` не дає NRE; таймаути; повтори лише для GET; відповіді 401/403/404/409/422/5xx → доменні помилки; поопераційні прапорці Live (`Ehealth:Live:Operations:*`).
2. **Пакет**: `diagnostic_report` + `observations[]`, UUID v1, канонічний UTF-8 без BOM, перевірка контракту до підпису; `signed_data` — base64 CAdES-X Long, на сервері лише структурна перевірка.
3. **Outbox `ehe_outbox` + воркер**: ідемпотентні ключі, атомарне захоплення, витримки, відновлення `SENDING` після збою, опитування job; порядок «пакет → job processed → окрема задача `complete`»; виправлено помилку погашення ЕН до реєстрації звіту; помилка → скасування звіту й новий пакет з новими id.
4. **Режими**: Mock — двійник контракту (не дзеркало БД), Replay — фікстури `tests/MedLink.LIS.Tests/Fixtures/ehealth/*.json` (з прикладів OpenAPI), Live — за прапорцями. `EnsureMock` прибрано.
5. **ЕН**: пошук у ЕСОЗ першим, `ehe_incoming_medical_referral` — кеш.
6. **UI** «Відправка в ЕСОЗ»: задачі, фільтри, «Повторити»/«Скасувати».

**Не виконано / не перевірено:** будь-яка операція в режимі Live (preprod); OAuth-вхід і віджет КЕП; криптографічна перевірка CAdES; Specimen, Equipment (`used_references`), `components`, `results_interpreter`; ідентифікатор працівника в ЕСОЗ з реєстру співробітників; довідники LOINC/UCUM; звірка коду категорії звіту, причини скасування та поведінки `use` без програми. П. 6 (read-only перевірка Live) і розд. 4.2 чекають токена й доступів.

## 8. Схема «через сервіс MedLink eHealth» (09.11.2026) — замінює прямий виклик

Вимога замовника: методи ЄСОЗ викликаються лише через проксі MedLink eHealth; ЛІС не звертається до `api.ehealth.gov.ua`, не зберігає токени. Тому розд. 1-7 (шляхи `/api/service_requests…`, Bearer-токен, вхід за логіном/TOTP) описують **вихідне дослідження прямого OpenAPI**; для реалізації чинний контракт проксі — `docs/EHEALTH_MEDLINK_PROXY_CONTRACT.md`.

- Режим `Ehealth:Mode = MedLinkProxy`; типово `Mock`; `Live` (прямий) і `Replay` залишені як застарілі/тестові.
- Заголовки `ClientId` + `UserId`, без `Authorization`; токен користувача на проксі.
- Шляхи без `/api`: `patients/service_requests…`, `patients/{id}/diagnostic_report_package`, `jobs/{id}`, `auth/*`.
- Вхід користувача: посилання → портал ЄСОЗ → `?code` → обмін на проксі (замість імітації 2FA 123456).
- Outbox, ідемпотентність, окремий `complete` після звіту, поопераційні прапорці `Ehealth:Proxy:Operations:*` збережено.
- Контракт виведено з SDK та коду реле, **на живому проксі не перевірено**; Specimen, каталог послуг у проксі відсутні (вимоги — у контракті, розд. 6).
