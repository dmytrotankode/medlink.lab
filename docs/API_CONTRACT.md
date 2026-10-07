# MedLink LIS 4.0 — Контракт API, доменна модель та протокол коннектора

Цей документ є єдиним джерелом правди для паралельної розробки бекенду (`src/MedLink.LIS.Api`),
коннектора (`src/MedLink.LabConnector`) та фронтенду (`src/MedLink.LIS.Web`). Усі команди
працюють строго за ним. Зміни контракту вносяться лише сюди.

## 0. Загальні угоди

| Тема | Рішення |
|---|---|
| Базовий URL | `/api/v1/lab` (REST, JSON, camelCase) |
| Автентифікація користувачів | **ВІДСУТНЯ** (за вимогою замовника: модуль вбудовується в evomis, де є власний IdentityServer). Поточний співробітник передається необов'язковим заголовком `X-MedLink-Employee-Id: <org_employee.id>`; якщо відсутній — береться співробітник за замовчуванням із налаштувань (`Lab:DefaultEmployeeId`). У UI — перемикач «Працюю як…» (лікар-лаборант / лаборант / медсестра / адміністратор) без пароля. Жодних `[Authorize]`, JWT, логінів |
| Автентифікація коннектора | Заголовок `X-MedLink-ApiKey: <apiKey>` (технічний ключ інсталяції, видається при реєстрації; це не користувацька авторизація, а ідентифікація пристрою) |
| Ролі | Довідкові (для UI та аудиту, не для обмеження доступу): `LAB_ADMIN`, `LAB_DOCTOR`, `LAB_TECHNICIAN`, `PHLEBOTOMIST`, `LOGISTICS_COURIER`, `REGISTRAR`. Беруться з `org_employee.lab_role`. У бойовому evomis мапляться на claims IdentityServer |
| Ідентифікатори | GUID у вигляді рядка (`"018f4a12-..."`). Довідники з `int` id |
| Дати | ISO 8601 UTC (`2026-10-07T08:15:00Z`) |
| Пагінація | `?page=1&pageSize=50&sort=field&dir=asc` → `{ "items": [...], "total": 1234, "page": 1, "pageSize": 50 }` |
| Помилки | RFC 7807 ProblemDetails: `{ "type", "title", "status", "detail", "errors": {field: [..]} }` |
| Аудит | Кожна мутація пише `lab_audit_log` (userId, action, entity, entityId, before, after, at, ip) |
| Локаль | Усі тексти UI українською. Серверні повідомлення помилок українською |
| БД | EF Core 8 + **SQLite** (файл `App_Data/medlink_lis.db`). Схема створюється `EnsureCreated()` і дооновлюється `SchemaUpgrader`; сід — з `db/seed/*.json`. Іменування та службові колонки — як в evomis: `snake_case`, ключі `uuid` (у SQLite — рядок GUID), `created_on`, `created_by uuid`, `modified_on`, `modified_by uuid`, `record_state` (2 — активний, 4 — видалений). DDL PostgreSQL генерується з моделі: `db/postgres/medlink_lis_schema.sql` (ТЗ, розд. 20.2.7) |
| Сутності MedLink | Мінімальна підмножина таблиць evomis у тій самій структурі ([MedLink]): `cmn_enum_record`, `cmn_person`, `org_organization`, `org_department`, `org_employee`, `mis_patient_card` ([MedLink+]: латинізація ПІБ), `ehe_service_catalog_service`, `org_organization_service`, `ehe_incoming_medical_referral`, `mis_diagnostic_report`. Лабораторні атрибути — у [ЛІС] `lab_employee_settings`, `lab_department_settings`. Відповідність сутностей — ТЗ, розд. 20.2.3 |
| Статика | API віддає зібраний фронтенд із `wwwroot/` та SPA fallback на `index.html` для не-`/api` шляхів |
| Swagger | `/swagger` |
| Health | `GET /health` → `{status:"Healthy", db:"ok", version:"4.0.0"}` |

## 1. Доменна модель (сутності та статуси)

### 1.1. Довідники
- `LabBiomaterialType {id:int, code, name, defaultContainer?, stabilityHours, temperatureRegime, isActive}`
- `LabTubeType {id:int, code, name, colorCode(#hex), anticoagulant?, volumeMl, orderOfDrawIndex, inversionsCount, isActive}`
- `LabMethodType {id:int, code, name, isActive}` — 46 методик із Simplex
- `LabAnalyzerType {id:int, code, name, manufacturer?, category(HEMATOLOGY|BIOCHEM|IMMUNO|COAG|URINE|BLOODGAS|OTHER), exchType(ASTM|ASTM_ASK|ASTM2|HL7|TEXT|HUMA5L|UC1000|CYAN|JUNIOR|IRIS|RAPID|FUJI|TXT), bopBase64?, eopBase64?, controlSum:bool, sleepMs:int, fullText:bool, orderTemplate(code of builder), isActive}` — 65 профілів із `ac_analyzer_type` Simplex
- `LabTestDefinition {id:string, code, name, shortName?, loincCode?, unit, decimalPlaces, resultType(NUMERIC|TEXT|DROPDOWN|CALCULATED), dropdownOptions[]?, formula?, category, biomaterialTypeId, methodId?, deltaCheckMaxPct?, deltaCheckHours (72), requiresManualVerification:bool, isQcTracked:bool, isActive}`
- `LabTestProfile {id, code, name, category, turnaroundHours, fastingRequired, price, isActive, items:[{testId, displayOrder, isRequired}]}` — «Довідник послуг»
- `LabReferenceLayer` — **каскад норм Simplex** (`dct_service_lab_norm` + `dct_service_lab_nv` + `dct_service_lab_attribute`):
  `{id, testId, testCode, methodCode, methodName, layerType(BASELINE|DEMOGRAPHIC|CLINICAL_ICD10|MENSTRUAL_PHASE|PREGNANCY), priorityOrder(10|40|60|80|100), normName, gender(M|F|ANY), isGender, ageUnit(DAYS|MONTHS|YEARS), ageFrom, ageTo, isAge, isMenstrualPhase, menstrualPhase?, isPregnancy, pregnancyWeekFrom?, pregnancyWeekTo?, icd10Code?, normLow?, normHigh?, critLow?, critHigh?, normText?, unit, deltaCheckMaxPct, analyzerCode? (norm_code — код показника на приладі), dilution?, isActive}`
- `LabReflexRule {id, triggerTestCode, conditionOperator(<,<=,>,>=,==,OUT_OF_RANGE,CRITICAL), thresholdValue?, reflexTestCode, autoApprove, requiresSameSample:bool, description, isActive}`
- `LabQcMaterial {id, analyzerId, name, level(LEVEL_1_LOW|LEVEL_2_NORMAL|LEVEL_3_HIGH), lotNumber, manufacturer, expiryDate, openedAt?, isActive, targets:[{testCode, targetMean, targetSd, unit, teaPct?}]}`
- Мікробіологія: `LabMicroOrganism`, `LabAntibiotic`, `LabEucastBreakpoint` (як у `db/postgres/02_*.sql`)

### 1.2. Робочий процес
- `LabOrder {id, orderNumber, patientId, patient:{...}, doctorId?, departmentId?, orderDatetime, status, isUrgentCito, ehealthReferralId?, clinicalNotes?, totalPrice, createdById, samples[], tests[]}`
  Статуси: `NEW → COLLECTED → IN_TRANSIT → RECEIVED → IN_PROGRESS → COMPLETED → RELEASED`; термінальні `CANCELLED`, `REJECTED`.
- `LabOrderSample {id, orderId, barcode, tubeTypeId, biomaterialTypeId, status, collectedAt?, collectedById?, receivedAt?, isHemolyzed, isLipemic, isIcteric, isClotted, isInsufficientVolume, rejectReason?}`
  Статуси: `PENDING, COLLECTED, IN_TRANSIT, RECEIVED, PROCESSING, STORED, DISPOSED, REJECTED`.
  Штрихкод: 8 цифр за алгоритмом Simplex `gen_lab_tube_barcode` (лічильник → 7 цифр, перша `0`→`1`, контрольна EAN-8). Друк Code128.
- `LabOrderTest {id, orderId, sampleId?, profileId?, testId, testCode, testName, status, assignedAnalyzerId?, isReflex, reflexFromTestId?}`
  Статуси: `PENDING, IN_ANALYSIS, RESULTED, NEEDS_REVIEW, AUTO_VERIFIED, VERIFIED, REJECTED, RERUN`.
- `LabTestResult {id, orderTestId, numericValue?, stringValue?, unit, normLow?, normHigh?, critLow?, critHigh?, referenceDisplay, flag(NORMAL|LOW|HIGH|CRIT_LOW|CRIT_HIGH|ABNORMAL|NONE), appliedLayerId?, deltaPercent?, deltaAlert, previousValue?, previousAt?, analyzerId?, analyzerFlags?, rawMessageId?, isAutoVerified, verifiedById?, verifiedAt?, verificationComment?, operatorComment?, enteredById?, enteredAt, version}` — історія змін у `lab_result_history`.
- `LabPanicCall {id, resultId, orderId, patientName, testCode, value, doctorNotifiedName, phone, department?, readbackConfirmed, notifiedById, notifiedAt, comments?}`
- `LabWorklistBatch {id, batchCode, analyzerId?, status(OPEN|SENT|COMPLETED), items[orderTestId]}`
- `LabQcResult {id, qcMaterialId, testCode, measuredValue, zScore, violatedRules[], isWarning, isRejection, lockoutEnforced, resolvedById?, resolvedAt?, resolutionAction?, runAt, operatorId}`
- `LabAnalyzerLockout {id, analyzerId, testCode?, reason, startedAt, resolvedAt?, resolvedById?, action?}`
- `LabSampleLogistics` + items, `LabArchiveRack`, `LabArchiveCell`, `LabReagentLot`, `LabCultureOrder`, `LabIsolate`, `LabSusceptibilityResult` — як у `db/postgres`.
- Інтеграція: `MisPatientCard`, `OrgEmployee`, `OrgDepartment`, `EheReferral`, `MisDiagnosticReport` — локальні копії/заглушки сутностей evomis (у складі МІС замінюються на реальні таблиці).
- Коннектор: `LabConnectorInstallation {id, name, hostName?, version?, installKey (одноразовий), apiKeyHash, status(PENDING|ACTIVE|OFFLINE|DISABLED), lastHeartbeatAt?, bufferedCount, createdAt}`,
  `LabAnalyzer {id, code, name, analyzerTypeId, connectorId?, departmentId?, connectionMode(TCP|COM|FILE), tcpHost?, tcpPort?, isTcpServer, comPort?, baudRate, parity(None|Even|Odd|Mark|Space), dataBits, stopBits(One|OneAndHalf|Two), flowControl(None|XonXoff|Hardware), filePath?, autoQueryOrders, isActive, isOnline, lastMessageAt?, lastError?, parameterMap:[{analyzerCode, testCode, factor, offset, unitOverride?}]}`,
  `LabAnalyzerMessage {id, analyzerId, direction(IN|OUT), protocol, rawText, parsedOk, receivedAt, resultsCount, error?}` — журнал обміну.
- Пацієнтський портал: `PatientNotification {id, patientId, channel(SMS|EMAIL|PUSH|VIBER), payload, sentAt, status}` (відправка — заглушка з журналом).

### 1.3. Правила обчислень (реалізуються у `MedLink.LIS.Core`)
- **Каскад норм** `NormsCascadeResolver.Resolve(layers, ctx{gender, ageDays, isPregnant, pregnancyWeek, menstrualPhase, icd10, methodCode})`: шари сортуються за `priorityOrder desc`; перший, що співпав, перемагає; BASELINE завжди співпадає; повертає `auditTrace`. Вік порівнюється у днях (`DAYS`=1, `MONTHS`=30.4375, `YEARS`=365.25).
- **Прапорець**: `v<=critLow→CRIT_LOW; v>=critHigh→CRIT_HIGH; v<normLow→LOW; v>normHigh→HIGH; інакше NORMAL`. Текстовий результат: збіг із `normText` → NORMAL, інакше ABNORMAL.
- **Delta-check**: попередній результат того ж тесту того ж пацієнта за `deltaCheckHours`; `|Δ%| > deltaCheckMaxPct` → `deltaAlert=true`, статус `NEEDS_REVIEW`.
- **Автоверифікація** (`AutoVerificationEngine`): результат → `AUTO_VERIFIED`, якщо одночасно: `flag==NORMAL`, немає прапорців приладу (`analyzerFlags` порожній або `N`), `deltaAlert==false`, `test.requiresManualVerification==false`, активного lockout на аналізаторі/тесті немає. Критичне значення завжди блокує.
- **Reflex**: після збереження результату перевіряються правила; створюється `LabOrderTest {isReflex=true}` з причиною у аудиті; якщо потрібен інший біоматеріал (`requiresSameSample=false`) — тест створюється зі статусом `PENDING` і позначкою для підтвердження лаборантом.
- **Westgard** (`WestgardEvaluator`, вікно — останні 10 точок одного матеріалу+тесту, z=(x−mean)/sd):
  `1_2s` (warning), `1_3s`, `2_2s` (дві поспіль по один бік >2s), `R_4s` (різниця двох поспіль >4s), `4_1s` (чотири поспіль по один бік >1s), `10_x` (десять поспіль по один бік від mean). Rejection-правила `1_3s, 2_2s, R_4s, 4_1s, 10_x` → `lockoutEnforced=true` та створення `LabAnalyzerLockout` для (analyzerId, testCode). Статистика серії: N, mean, SD, CV%, bias%.
- **Lockout**: поки активний lockout — результати цього аналізатора/тесту не автоверифікуються і не можуть бути `VERIFIED` без явного `override` із коментарем.
- **TAT**: етапи `order→collected→received→resulted→verified→released`, хвилини між ними, медіана/P90 за профілями та CITO.

## 2. REST API (користувацька частина)

### 2.1. Контекст користувача (без автентифікації)
| Метод | Шлях | Тіло/параметри | Відповідь |
|---|---|---|---|
| GET | `/context/me` | заголовок `X-MedLink-Employee-Id?` | `{employee:{id, fullName, position, labRole, departmentId}, lab:{name,...}}` |
| GET | `/context/employees` | — | список співробітників `org_employee` для перемикача «Працюю як…» |

Сід `org_employee`: адміністратор лабораторії, лікар-лаборант, фельдшер-лаборант, медсестра пункту забору, кур'єр, реєстратор. Кабінет пацієнта (`/portal/*`) приймає `patientId` у шляху (`/portal/{patientId}/orders`) — без OTP; у бойовому evomis підставляється пацієнт із сесії.

### 2.2. Довідники `/dictionaries/*` (CRUD; GET — всі ролі; мутації — LAB_ADMIN)
`biomaterials`, `tube-types`, `method-types`, `analyzer-types`, `tests`, `profiles`, `reflex-rules`, `organisms`, `antibiotics`, `eucast-breakpoints`, `departments`, `employees`.
Шаблон: `GET /dictionaries/{name}` (список, `?search=&isActive=`), `GET /{name}/{id}`, `POST /{name}` (створити), `PUT /{name}/{id}`, `DELETE /{name}/{id}` (soft delete → `isActive=false`; 409 якщо є залежності).
`GET /dictionaries/tests/{code}/profiles` — у яких профілях використовується.
`POST /dictionaries/import` — імпорт JSON/CSV довідника з попереднім переглядом `?dryRun=true`.

### 2.3. Норми `/norms`
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/norms/layers?testCode=&methodCode=` | шари (priority desc) |
| GET | `/norms/combinations?testCode=` | усі комбінації |
| POST | `/norms/combinations` | створити/оновити шар |
| DELETE | `/norms/combinations/{id}` | |
| POST | `/norms/resolve-cascade` | `{testCode, methodCode?, gender, age, ageUnit, isPregnant, pregnancyWeek?, menstrualPhase?, icd10Code?, measuredValue?, previousValue?}` → `{winningLayer, normLow, normHigh, critLow, critHigh, unit, statusFlag, isPanicCito, isDeltaAlert, deltaPercent, auditTrace[]}` |
| GET | `/norms/service-card/{profileId}` | «Картка послуги»: вкладки Головна/Показники/Норми/Лабораторія одним об'єктом |

### 2.4. Пацієнти `/patients` (локальна картка МІС)
`GET /patients?search=` (ПІБ/телефон/ІПН), `GET /patients/{id}`, `POST /patients`, `PUT /patients/{id}`, `GET /patients/{id}/history` (усі результати з трендами), `GET /patients/{id}/trend/{testCode}` → `[{at, value, normLow, normHigh, flag}]`.

### 2.5. Замовлення `/orders`
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/orders?status=&from=&to=&cito=&departmentId=&search=&page=` | черга (пагінація) |
| GET | `/orders/{id}` | повна картка (samples, tests, results) |
| POST | `/orders` | `{patientId | newPatient:{...}, doctorId?, departmentId?, isUrgentCito, ehealthReferralId?, clinicalNotes?, profileIds[], testIds[]}` → створює замовлення, автопідбір пробірок за біоматеріалом (одна пробірка на тип біоматеріалу/пробірки, `group_numb`), генерує штрихкоди, `LabOrderTest` для кожного тесту |
| POST | `/orders/batch` | масове створення `[{...}]` |
| POST | `/orders/{id}/cancel` | `{reason}` |
| POST | `/orders/{id}/release` | видача (усі тести VERIFIED/AUTO_VERIFIED), сповіщення пацієнта |
| GET | `/orders/{id}/report` | HTML-бланк для друку (A4, QR верифікації) |
| GET | `/orders/{id}/report.pdf` | PDF бланка |
| GET | `/orders/{id}/labels` | `[{barcode, zpl, svg}]` етикетки всіх пробірок |
| GET | `/orders/by-barcode/{barcode}` | пошук за штрихкодом пробірки |
| GET | `/verify/{token}` | **публічна** перевірка автентичності бланка за QR (без ПІБ): `{orderNumber, releasedAt, lab, valid:true}` |

### 2.6. Пробірки / забір / логістика
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/samples?status=&barcode=` | |
| POST | `/samples/{barcode}/collect` | `{checklist:{idVerified, fasting, orderOfDraw, mixing}, volumeMl?}` → `COLLECTED`, замовлення → `COLLECTED` |
| POST | `/samples/{barcode}/receive` | прийом у лабораторії → `RECEIVED` (+ `isHemolyzed/isLipemic/isIcteric/isClotted`) |
| POST | `/samples/{barcode}/reject` | `{reason, createRepeatOrder:bool}` |
| GET | `/samples/{barcode}/label` | `{barcode, zpl, svg, patientName, tube, collectedAt}` |
| GET | `/logistics/manifests?status=` | |
| POST | `/logistics/manifests` | `{originDepartmentId, destinationDepartmentId, courierName, courierPhone, temperatureDispatch, barcodes[]}` → `DISPATCHED`, проби → `IN_TRANSIT` |
| POST | `/logistics/manifests/{id}/receive` | `{temperatureReceipt, receivedBarcodes[], notes}` → перевірка холодового ланцюга (поза +2..+8 → `isColdChainViolated`) |

### 2.7. Робочий лист і результати
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/worklist?status=&analyzerId=&flag=&cito=&search=&page=` | рядки: `{orderTestId, resultId?, orderNumber, barcode, patientName, patientAgeGender, testCode, testName, value, unit, normLow, normHigh, referenceDisplay, flag, deltaPercent, deltaAlert, status, analyzerName, isCito, isAutoVerified, isLockedOut, enteredAt, verifiedAt}` |
| GET | `/worklist/summary` | лічильники: pending, needsReview, panic, cito, autoVerifiedToday |
| PUT | `/results/{orderTestId}` | `{numericValue? , stringValue?, comment?}` — ручне введення: резолв норми за каскадом, прапорець, delta-check, reflex, автоверифікація → повертає рядок worklist |
| POST | `/results/{orderTestId}/verify` | `{comment?, override?:bool}` (LAB_DOCTOR) → `VERIFIED`; при CRIT_* коментар обов'язковий |
| POST | `/results/verify-batch` | `{orderTestIds[]}` → `{verified, skipped:[{id, reason}]}` |
| POST | `/results/{orderTestId}/reject` | `{reason}` |
| POST | `/results/{orderTestId}/rerun` | → `RERUN`, у чергу аналізатора |
| POST | `/results/{orderTestId}/reopen` | (LAB_DOCTOR/LAB_ADMIN) із аудитом |
| GET | `/results/{orderTestId}/history` | версії |
| POST | `/worklist/autoverify` | `{orderTestIds[]?}` → `{verified, blocked:[{id, reason}]}` |
| GET/POST | `/worklist/batches` | робочі листи (батчі) для аналізатора/ручної постановки; `GET /worklist/batches/{id}/print` |
| GET | `/panic-calls`, POST `/panic-calls` | `{resultId, doctorName, phone, department?, readbackConfirmed, comments}` |
| GET | `/panic/pending` | критичні результати без зареєстрованого дзвінка |

### 2.8. Контроль якості `/qc`
| Метод | Шлях | Опис |
|---|---|---|
| GET/POST/PUT | `/qc/materials` | матеріали та цільові значення |
| GET | `/qc/levey-jennings?analyzerId=&testCode=&materialId=&days=30` | `{targetMean, targetSd, cvPct, n, mean, sd, bias, currentStatus(OK|WARNING|LOCKOUT), points:[{at, value, z, rules[], status}]}` |
| POST | `/qc/results` | `{qcMaterialId, testCode, measuredValue, runAt?}` → оцінка Вестгарда, lockout |
| GET | `/qc/lockouts?active=true` | |
| POST | `/qc/lockouts/{id}/resolve` | `{cause, action, comment}` → протокол розблокування |
| GET | `/qc/report?analyzerId=&from=&to=` | зведений звіт ВКЯ |

### 2.9. Аналізатори та коннектори (адміністрування)
| Метод | Шлях | Опис |
|---|---|---|
| GET/POST/PUT/DELETE | `/analyzers` | CRUD аналізаторів (див. 1.2) |
| GET | `/analyzers/{id}/messages?page=` | журнал обміну |
| POST | `/analyzers/{id}/simulate` | `{rawMessage}` → парсинг тестового повідомлення без збереження: `{parsedResults[], warnings[]}` |
| GET | `/analyzers/{id}/order-preview/{barcode}` | згенерований текст замовлення для приладу (для налагодження) |
| GET/POST | `/connectors` | інсталяції; `POST {name}` → `{id, installKey, setupCommand}` |
| GET | `/connectors/{id}` | статус, аналізатори, heartbeat, буфер |
| POST | `/connectors/{id}/disable`, `/connectors/{id}/rotate-key` | |
| GET | `/connectors/{id}/download` | ZIP із `appsettings.json`, інструкцією та посиланням на інсталятор |
| GET | `/connectors/{id}/logs?page=` | |

### 2.10. Біобанк, реагенти, мікробіологія, аналітика
- `/biobank/racks` CRUD; `GET /biobank/racks/{id}/cells`; `POST /biobank/cells/place {rackId, row, col, barcode, expiryAt}`; `POST /biobank/cells/{id}/remove {reason}`; `GET /biobank/search?barcode=`.
- `/reagents/lots` CRUD; `POST /reagents/lots/{id}/consume {tests}`; `GET /reagents/alerts` (мінімум/термін).
- `/microbiology/cultures` CRUD; `POST /microbiology/cultures/{id}/isolates`; `POST /microbiology/isolates/{id}/susceptibility {antibioticId, method, zoneMm?, mic?}` → інтерпретація S/I/R за EUCAST, детекція MRSA/ESBL/CRE; `GET /microbiology/cultures/{id}/report`.
- `/analytics/tat?from=&to=&profileId=&cito=` → `{medianMin, p90Min, stages:[{name, medianMin}], byProfile[], slaViolations}`; `/analytics/volume?groupBy=profile|analyzer|employee|day`; `/analytics/export.xlsx?report=`.
- `/audit?entity=&entityId=&userId=&from=&to=&page=`.
- `/settings/lab` GET/PUT `{name, edrpou, address, phone, email, licenseNumber, logoBase64, directorName, workingHours, orderNumberMask, barcodePrefix, reportFooter, panicPhone}`; `/settings/users` CRUD (LAB_ADMIN); `/settings/numerators`.
- `/portal/{patientId}/orders`, `/portal/{patientId}/orders/{id}`, `/portal/{patientId}/orders/{id}/report.pdf`, `/portal/{patientId}/trend/{testCode}`, `/portal/{patientId}/notifications`.
- `/import/results` `multipart csv|xlsx|xml` + `?dryRun=true` → попередній перегляд, потім застосування.

## 3. API коннектора `/api/v1/lab/connector/*` (авторизація `X-MedLink-ApiKey`)

| Метод | Шлях | Тіло | Відповідь |
|---|---|---|---|
| POST | `/connector/register` | `{installKey, hostName, osDescription, version}` | `{connectorId, apiKey, serverTimeUtc}` (installKey стає використаним) |
| GET | `/connector/config` | — | `ConnectorConfigDto` (нижче) |
| POST | `/connector/heartbeat` | `{version, uptimeSec, bufferedCount, analyzers:[{analyzerId, isConnected, lastMessageAt?, lastError?}]}` | `{serverTimeUtc, configVersion, commands:[{type:"RELOAD_CONFIG"|"RESTART"|"SEND_TEST_MESSAGE", payload?}]}` |
| GET | `/connector/orders/by-barcode/{barcode}?analyzerId=` | — | `AnalyzerOrderDto` або 404 |
| GET | `/connector/orders/pending?analyzerId=` | — | `[AnalyzerOrderDto]` для приладів без режиму запиту (batch download) |
| POST | `/connector/results` | `AnalyzerResultsBatchDto` | `{accepted, matched, unmatched:[{barcode, analyzerCode, reason}], created:[orderTestId]}` |
| POST | `/connector/messages` | `{analyzerId, direction, protocol, rawText, receivedAt, parsedOk, error?}` | 202 |
| POST | `/connector/logs` | `{entries:[{at, level, message, analyzerId?}]}` | 202 |

```jsonc
// ConnectorConfigDto
{
  "connectorId": "…", "configVersion": 17, "pollIntervalSec": 30, "heartbeatIntervalSec": 30,
  "analyzers": [{
    "analyzerId": "…", "code": "SYSMEX_XN", "name": "Sysmex XN-1000", "typeCode": "SYSMEXXN",
    "protocol": "ASTM",               // ASTM | ASTM_ASK | ASTM2 | HL7 | TEXT | HUMA5L | UC1000 | CYAN | JUNIOR | IRIS | RAPID | FUJI | TXT | FILE
    "connection": { "mode": "TCP", "host": "192.168.1.101", "port": 5100, "isServer": true,
                    "comPort": null, "baudRate": 9600, "parity": "None", "dataBits": 8, "stopBits": "One", "flowControl": "None",
                    "filePath": null, "filePollSec": 10 },
    "framing": { "bopBase64": null, "eopBase64": null, "checksum": true, "sleepMs": 100, "maxFrameLen": 240, "ackAfterRecord": true },
    "autoQueryOrders": true, "orderTemplate": "SYSMEX_XN",
    "parameterMap": [{ "analyzerCode": "WBC", "testCode": "WBC", "factor": 1.0, "offset": 0.0, "unitOverride": null }]
  }]
}
// AnalyzerOrderDto
{ "barcode": "10260048", "orderNumber": "1026-004812", "priority": "R|S", "sampleType": "Serum",
  "patient": { "id": "…", "lastName": "Коваленко", "firstName": "Олена", "birthDate": "1985-04-12", "gender": "F", "lastNameLatin": "Kovalenko", "firstNameLatin": "Olena" },
  "tests": [{ "testCode": "GLU", "analyzerCode": "GLU", "dilution": null }] }
// AnalyzerResultsBatchDto
{ "analyzerId": "…", "receivedAt": "…", "rawMessageId": "…", "isQc": false,
  "results": [{ "barcode": "10260048", "analyzerCode": "WBC", "value": "7.45", "unit": "10*9/L", "flags": "N", "measuredAt": "…", "referenceText": "4.0^9.0", "qcLotNumber": null }] }
```

Логіка сервера при `POST /connector/results`: знайти пробу за штрихкодом → знайти `LabOrderTest` за `parameterMap.testCode` (fallback: `LabReferenceLayer.analyzerCode` / `LabTestDefinition.code`) → застосувати `factor/offset` → записати результат через той самий конвеєр, що й ручне введення (норми, прапорець, delta, reflex, автоверифікація) → `analyzerFlags` із приладу. Для `isQc=true` — результат іде у `/qc/results` за лотом. Невідомі штрихкоди повертаються в `unmatched` і зберігаються у `lab_unmatched_results` для ручного зв'язування (`GET /results/unmatched`, `POST /results/unmatched/{id}/link {orderTestId}`).

## 4. Спільна бібліотека `MedLink.LIS.Core` (використовують API, коннектор, тести)

Проєкт класової бібліотеки .NET 8 (без залежності від ASP.NET). Простір імен `MedLink.LIS.Core.Protocols`:
- `AstmFrameCodec` — кадрування ENQ/ACK/NAK/STX/ETX/ETB/EOT, контрольна сума mod 256 (як `CalcCRC` Delphi), розбиття на кадри ≤240 символів з нумерацією 1..7, збирання багатокадрових повідомлень.
- `AstmMessage {Records: H,P,O,R,Q,C,L}` + `AstmParser.Parse(string)` → `AnalyzerInboundMessage {Kind: Query|Results|Other, Barcode, Patient?, Results[]}`.
- `Hl7Message` + `Hl7Parser` (MSH, PID, OBR, OBX, QRD, SPM), `MllpCodec` (VT…FS CR), `Hl7AckBuilder`, `Hl7OrderBuilder` (Mindray DSR^Q03 / ORM^O01 / OML^O21).
- `TextProtocolFramer(bop, eop)` — універсальний рамкувальник для TEXT/HUMA5L/UC1000/CYAN/FUJI/RAPID/IRIS/TXT з Simplex (`bop`/`eop` base64), парсери `RapidParser`, `FujiParser`, `UrisysParser`, `IntegraParser`, `CyanParser`, `GenericKeyValueParser`.
- `IAnalyzerOrderBuilder` + реалізації за `orderTemplate`: `AstmGenericOrderBuilder`, `CobasOrderBuilder` (c111/c311/e411 TSDWN^REPLY), `SysmexOrderBuilder` (XN/XS/CS-2500/CA-600), `PentraOrderBuilder`, `TosohOrderBuilder`, `BioKselOrderBuilder`, `StagoOrderBuilder`, `MaglumiOrderBuilder`, `HumaStarOrderBuilder`, `BeckmanAccessOrderBuilder`, `MindrayAstmOrderBuilder`, `MindrayHl7OrderBuilder`, `IntegraOrderBuilder`, `PrestigeOrderBuilder`, `RapidOrderBuilder` — перенесення `gen_lab_order` Simplex.
- `AnalyzerProfileCatalog` — 65 профілів із `ac_analyzer_type` (код, назва, exchType, bop/eop, controlSum, sleep, orderTemplate, parserKind) як вбудований JSON-ресурс `analyzer_types.json` (також сід для API).

Простір імен `MedLink.LIS.Core.Clinical`: `NormsCascadeResolver`, `ResultFlagger`, `DeltaCheckEvaluator`, `AutoVerificationEngine`, `ReflexRuleEngine`, `WestgardEvaluator`, `QcStatistics`, `EucastInterpreter`, `TatCalculator`.
Простір імен `MedLink.LIS.Core.Barcodes`: `TubeBarcodeGenerator` (Simplex EAN-8-like), `Code128Svg`, `ZplLabelBuilder` (40×25 мм), `QrSvg` (для бланка).
Простір імен `MedLink.LIS.Core.Contracts`: усі DTO з розділу 3 (спільні для API та коннектора).

## 5. Фронтенд `src/MedLink.LIS.Web`

- Стек **строго як в evomis/src/App.View**: Quasar CLI v1 (`@quasar/app` 1.x, webpack) + Quasar Framework **1.15.3** + Vue **2.6**, Vue Router 3, Vuex 3, axios, `quasar.variables.styl` (stylus) із токенами evomis (`$primary: #4274A7`, градієнт `#0178BC→#318F94→#5EC58C`, темний drawer `#212121`, Source Sans Pro, Material Icons + Font Awesome 5). Структура каталогів повторює evomis: `src/layouts/baseLayout/BaseLayout.vue`, `src/components/baseElements/menuDrawer.vue`, `src/pages/laboratory/*`, `src/pages/dictionaries/*`, `src/router/routes.js` (+ `laboratoryRoutes.js`), `src/store/modules/laboratory.js`, `src/services/labApiService.js`, `src/boot/axios.js`. Node 22 → `NODE_OPTIONS=--openssl-legacy-provider` у npm-скриптах.
- `process.env.API_BASE` (`/api/v1/lab`), заголовок `X-MedLink-Employee-Id` з Vuex `context.employeeId` (перемикач у хедері). Логіну немає.
- Маршрути: `/laboratory/dashboard`, `/laboratory/orders` (реєстрація + черга), `/laboratory/orders/:id`, `/laboratory/phlebotomy`, `/laboratory/logistics`, `/laboratory/workstation`, `/laboratory/validation`, `/laboratory/quality-control`, `/laboratory/biobank`, `/laboratory/reagents`, `/laboratory/microbiology`, `/laboratory/analytics-tat`, `/laboratory/analyzers` (аналізатори + коннектори + журнал обміну + симулятор), `/laboratory/norms` (каталог послуг, картка послуги, резолвер), `/laboratory/dictionaries/*`, `/laboratory/admin/users`, `/laboratory/admin/settings`, `/laboratory/audit`, `/portal` (кабінет пацієнта: OTP, список, результат, тренд, PDF).
- Збірка: `quasar build` → `dist/spa` копіюється в `src/MedLink.LIS.Api/wwwroot` (скрипт `build.sh` / `build.ps1` у корені). Перенесення в evomis: копіювання `src/pages/laboratory`, `src/pages/dictionaries`, `src/store/modules/laboratory.js`, `src/services/labApiService.js`, реєстрація `laboratoryRoutes.js` та пункту меню в `menuDrawer.vue`.

## 6. Коннектор `src/MedLink.LabConnector`

- Команди CLI: `MedLink.LabConnector run` (служба), `setup --server https://lis.example.ua --install-key XXXX [--name "Лаб ПК 1"]` (реєстрація, запис `appsettings.local.json`), `test --analyzer CODE --file sample.txt` (прогнати повідомлення через парсер), `status`, `install-service` / `uninstall-service` (Windows sc / systemd).
- Конфігурація: `appsettings.json` (локальні переоприділення) + конфіг із сервера (`GET /connector/config`, кешується у `data/config.cache.json` для роботи офлайн). Якщо сервер недоступний — працює за кешем, буферизує результати у SQLite `data/offline_buffer.db`.
- Локальна сторінка статусу: `http://localhost:5088/` (стан аналізаторів, останні повідомлення, кнопки «Перечитати конфіг», «Відправити тест») та `GET /status.json`.
- Інсталятори у `installers/labconnector`: `MedLink_LabConnector_Setup.iss` (Inno Setup, Windows Service, майстер введення сервера та ключа), `install-windows.ps1` (без Inno: розпакувати, зареєструвати службу, прописати ключ), `install-linux.sh` + `.service`, `build-deb.sh`, `Dockerfile`, `publish.sh/.ps1` (self-contained win-x64/linux-x64/linux-arm64).

## 7. Нумерація та формати
- Номер замовлення: `{yyMM}-{000000}` за лічильником, маска з налаштувань.
- Штрихкод пробірки: 8 цифр (Simplex), Code128 на етикетці 40×25 мм (ZPL `^BCN,60,Y,N,N`).
- QR бланка: `https://{host}/verify/{token}`; `token` = HMAC-SHA256(orderId+releasedAt, secret) base64url.
