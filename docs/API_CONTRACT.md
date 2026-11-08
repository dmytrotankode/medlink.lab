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

### 2.11. Інтеграція з МІС, маршрутизація, розподіл, підпис ЕСОЗ, звіти (ТЗ розд. 21, 23; R1.1)
Помилки предметної області — ProblemDetails з машинним кодом: `type = https://medlink.ua/problems/{code}`, розширення `code`
(напр. `required-indicator-missing`, `unknown-indicator`, `intervention-unmapped`, `duplicate-referral`, `referral-in-progress`,
`analyzer-code-missing`, `ehealth-auth-required` 401, `ehealth-strong-auth-required` 403, `ehealth-totp-disabled` 409, `ehealth-invalid-code` 422,
`signer-mismatch` 422, `certificate-expired` 422, `report-not-ready` 409). Ідентифікатори МІС (`misReferralId`, `hospitalizationId`) — GUID; людський номер — `referralNumber`.

**Словник інтервенцій ЕСОЗ і співставлення (FR-MIS-001…016)**
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/interventions?search=&status=UNMAPPED\|NO_EQUIPMENT\|PARTIAL\|READY` | словник зі статусом готовності |
| POST | `/interventions/sync` | синхронізація словника (LAB_ADMIN; мок — демо-набір `DEMO-`) |
| GET/POST/PUT/DELETE | `/intervention-map`, `/intervention-map/{id}` | співставлення `{interventionId, profileId?, testIds[], indicatorSelectionMode FIXED\|SELECTABLE\|SELECTABLE_ANY, minSelected, isPathology}` |
| GET | `/intervention-map/suggest?interventionId=`, `/intervention-map/coverage` | підказка профілю; звіт покриття (T-421) |

**Послуги обладнання і довідник «Послуга ЕСОЗ × аналізатори» (FR-EQP-001…008, FR-SVA-001…009)**
| Метод | Шлях | Опис |
|---|---|---|
| GET/PUT | `/analyzers/{id}/services?dryRun=` | послуги приладу `[{profileId, isEnabled, priority, avgTatMinutes}]`; `dryRun=true` → `{affected}` до підтвердження |
| POST | `/analyzers/{id}/services/prefill` | заповнення з показників приладу |
| GET | `/analyzer-services/matrix` | `{analyzers[{id, name, kind, location, isOnline, longOffline, lockouts[], queue}], rows[{serviceCode, profileCode, mode, readiness, cells{analyzerId: {priority, enabled, separate, partial, covered, total, unavailable[]}}}]}` |
| GET/PUT | `/analyzer-services/{key}` | картка послуги (ключ — код інтервенції / id чи код профілю / id співставлення); PUT `{analyzers[], indicators[{testCode, isRequired, isDefault}], params[{analyzerId, testCode, isEnabled, analyzerCode, priority}], mode, dryRun}`; новий показник без коду приладу → 422 `analyzer-code-missing` |
| GET / POST | `/analyzer-services/export` (CSV) / `/analyzer-services/import?dryRun=` | пакетне редагування |

**Каталог і направлення МІС (FR-SVA-010…015, FR-MIS-020…035, FR-PKG-001…010)**
| Метод | Шлях | Опис |
|---|---|---|
| GET | `/mis/catalog` | послуги для МІС: склад, режим вибору, обов'язкові/типові показники, `catalogVersion`; `ETag`, `If-None-Match` → 304 |
| GET | `/mis/catalog/packages` | пакети (дзеркало довідника МІС) |
| POST | `/mis/referrals` | `{misReferralId, referralNumber, context INPATIENT\|AMBULATORY, source?, patientId, hospitalizationId?, department, ward, room, bed, doctorId?, doctorName, priority ROUTINE\|CITO, plannedCollectionAt?, icd10?, ehealthReferralNumber?, partnerName?, package?{code, name, cito, changedByDoctor}, items[{interventionCode, indicators[], comment?}]}` → 201 з позиціями, тестами, приладом і позицією в черзі, `warnings[]` |
| GET | `/mis/referrals`, `/mis/referrals/{id}` | id — id замовлення, `misReferralId`, номер замовлення або `referralNumber`; `events[]` (RECEIVED, RESULT_READY, COMPLETED, CANCELLED) |
| POST | `/mis/referrals/{id}/cancel` | `{reason}`; 409 `referral-in-progress`, якщо робота почалась |
| PUT | `/mis/referrals/{id}/items/{itemId}/indicators` | `{indicators[], selectedBy, reason}` — вибір/дозамовлення показників (FR-MIS-022, FR-SVA-015) |

**Розподіл направлень (FR-DST-001…012)**: `GET /distribution/inbox?state=pending|all&source=`; `GET /distribution/{orderId}` (пропозиція: кандидати з доступністю, чергою, TAT, ознака розщеплення послуги); `PUT /distribution/{orderId}` `[{orderTestId|itemId+testCode, analyzerId, reason}]`; `POST /distribution/{orderId}/auto`; `POST /distribution/{orderId}/confirm {noEquipment[{orderTestId, action SENDOUT|MANUAL|REJECT, performerId?, reason?}]}`. Планові направлення МІС (`RequireDistributionConfirmation=true`) приходять як `PROPOSED`, CITO — одразу `CONFIRMED`; без переданих показників — `NEEDS_SELECTION`.

**Маршрутизація (FR-RTE-001…012)**: `POST /routing/preview {profileId?, testCodes[], cito}`; `GET /routing/log?orderId=&orderTestId=`; `GET /analyzers/{id}/queue` (CITO → планований час → час прийому); `PUT /order-tests/{id}/analyzer {analyzerId, reason}` (лише на доступного кандидата); `POST /analyzers/{id}/reroute {reason}` (прилад без зв'язку → online-кандидати). Правило: кандидати — прилади з увімкненим показником; вимкнена послуга приладу виключає його; без послуги — «окремий показник» (пріоритет 10); недоступні — ТО, lockout ВКЯ, без зв'язку довше `RerouteOnOfflineMinutes`; група показників однієї послуги в пробі — спершу прилад, що виконує всі; стратегія `PRIORITY | LEAST_LOAD | PRIORITY_THEN_LOAD`; CITO — мінімальний TAT. Lockout ВКЯ і вимкнення послуги переназначають тести в черзі.

**ЕСОЗ: сесія, підпис КЕП, відправка (FR-SGN-001…014)**
| Метод | Шлях | Опис |
|---|---|---|
| GET/POST/DELETE | `/ehealth/session` | стан `{level L0\|L1\|L2, label, expiresAt, totpEnabled, testCertificate, demoTotpCode}`; POST `{login, password}` → L1 (пароль не зберігається) |
| POST | `/ehealth/session/confirm` | `{method KEP\|TOTP, code?, signer?{drfo, name, certSerial, validTo}}` → L2 |
| POST | `/ehealth/session/totp` | увімкнення 2FA (мок: код `123456`) |
| POST | `/orders/{id}/ehealth/prepare` | пакет DiagnosticReport → `READY_TO_SIGN` (hash) |
| POST | `/orders/{id}/ehealth/submit` | `{signedData, signer, simulateNetworkError?}` → SIGNED → PROCESSING → REGISTERED → COMPLETED (е-направлення) / ERROR (повтор) / AUTH_REQUIRED |
| POST | `/orders/{id}/ehealth/retry`, `/orders/{id}/ehealth/annul {signedData, signer, reason}` | повтор без повторного підпису; анулювання з КЕП |
| GET | `/orders/{id}/ehealth/status`, `/ehealth/submissions?status=` | статус, журнал відправок |

**Робота за направленнями і звіти (FR-WRK-001…010, FR-RPT-001…014)**: `GET /work/summary|/work/by-analyzer/{id}|/work/by-patient[/{id}]|/work/by-service[/{code}]` з фільтрами `source, packagesOnly, department, cito, status=active|all, from, to, q`; `GET /work/export?view=&key=` (XLSX). `GET /reports` — перелік; `GET /reports/{type}?format=json|xlsx|pdf` для `load, sources, indicators, tat, distribution, unperformed, packages`; `GET /reports/cumulative/{patientId}?from=&to=&group=service|analyzer&abnormalOnly=&format=` (стрілки ↑ ↓ ↑↑ ↓↓, критичні, журнал `lab_report_run` + аудит `REPORT_ACCESS`); `POST /reports/stat-daily/rebuild?from=&to=` (агрегати `lab_stat_daily`).

**Модель даних (нові таблиці [ЛІС])**: `lab_ehealth_intervention`, `lab_intervention_map`, `lab_intervention_map_test`, `lab_analyzer_service`, `lab_order_item`, `lab_order_item_indicator`, `lab_routing_log`, `lab_mis_event_log`, `lab_service_package`, `lab_ehealth_session`, `lab_ehealth_submission`, `lab_stat_daily`, `lab_report_run`; нові колонки `lab_order`, `lab_order_test`, `lab_analyzer`, `lab_analyzer_parameter_map`, `lab_test_profile_item`, `lab_settings` — міграція `db/migrations/R1.1/006_ch21_23_mis_routing_distribution_ehealth_sign.sql`.

### 2.12. Патоморфологія (ТЗ розд. 22; R1.1)
Справа ПГД — замовлення `lab_order` з `lab_department = PATHOLOGY`, `pathology_kind` (BIOPSY | CITO | OPERATIVE | CONSULT), `case_no` (ПГД-РР-NNNN), `sla_due_at`.
Етапи: `RCV → GROSS → PROC → EMB → SECT → STAIN → MICRO → SIGNED`; етап касети — останній виконаний, етап справи — наступний після найменш просунутої касети.
Коди проблем: `container-unlabeled` 422, `containers-not-scanned` 422, `icdo-required` 422, `read-back-required` 422, `report-signed` 409, `block-issued` 409, `duplicate-referral` 409, `signer-mismatch` 422.
Ролі (22.11): прийом/відмова/видача — REGISTRAR, LAB_TECHNICIAN, LAB_ADMIN; вирізка, висновок, ІГХ, підпис, CITO — LAB_DOCTOR, LAB_ADMIN; етапи обробки — LAB_TECHNICIAN, LAB_ADMIN.

| Метод | Шлях | Опис |
|---|---|---|
| POST | `/mis/referrals` з `type=PATHOLOGY` | блок `pathology {kind, operationId?, source, department, doctorName, biopsyKind PRIMARY\|SECONDARY, primaryCaseNo?, clinicalData, clinicalDiagnosis, operationText, markingText, templateName?, form014{…}, objects[{mark, description, localization?, fragments, fixative, takenAt?, fixationStartAt?, labeled}], external?{institution, number, date, slides, blocks}}`; спільні поля (patientId, misReferralId GUID, referralNumber, department, doctorName, icd10, ehealthReferralNumber, partnerName) беруться з направлення; `priority=CITO` → kind CITO. 201 — справа в черзі прийому |
| POST | `/pathology/referrals` | те саме без обгортки МІС |
| GET | `/pathology/incoming` | черга прийому: пацієнт, джерело, тип матеріалу, направник, контейнери (`labeled`, `fixationHours`, `fixationWarning`), знімок п. 1–9 форми |
| POST | `/pathology/incoming/{id}/accept` | `{scannedBarcodes[]}` (штрихкоди або маркування всіх контейнерів) → № справи, SLA (робочі дні; CITO — `PathologyCitoTargetMinutes`), вартість (консультація — скельця × 210, зовнішні блоки/скельця зі штрихкодами), подія `RECEIVED` |
| POST | `/pathology/incoming/{id}/reject` | `{reason (код або назва з довідника), comment}` → `REJECTED`; е-направлення не погашається |
| POST | `/pathology/incoming/{id}/query` | `{text}` → подія `QUERY` направнику |
| PUT | `/pathology/cases/{id}/gross` | + `pathologistId`, `assistantId` (обов'язкові для `finish`), `blocks[].track` (`STANDARD`/`DECALC`), `blocks[].methods[]` (до 5 кодів довідника), `objects[].remainderState` (`NONE`/`KEPT`/`DISCARDED`) + `remainderNote` |
| POST | `/pathology/scan`, `/pathology/cases/{id}/advance` | + `performerId`, `count` (мікротомія); `stage=COVER` — покриття скельця, `stage=DECALC` — лише для блоків `DECALC` |
| POST | `/pathology/slides/{id}/dispose` | `{reason, recreate}` → скельце `DISPOSED`, за потреби нове тієї ж методики |
| PUT | `/pathology/blocks/{id}/storage` | `{roomId, location}` |
| GET/POST | `/pathology/rooms` | приміщення архіву (POST — Admin) |
| GET | `/pathology/staff` | `{pathologists[], assistants[], current}` для вибору виконавців |
| POST | `/pathology/cases/{id}/dispatch`, GET `/pathology/cases/{id}/dispatches` | `{address}` → запис черги розсилки (`QUEUED`); лише для підписаної справи |
| POST | `/pathology/blocks/{id}/issue` | + `scope` (`EXTERNAL`/`INTERNAL`), `recipientUnit` (обов'язково для `INTERNAL`) |
| POST | `/pathology/cases/{id}/report/addendum` | + тип `CONSULT` з `consultantName` |
| GET | `/pathology/cases?stage=RCV\|…\|SIGNED\|work\|lab\|cito\|overdue&src=&q=` | журнал: `tiles{stage: n}`, `total, inWork, overdue, cito, lab, incoming`, `items[]` (CITO і прострочені вгорі) |
| GET | `/pathology/summary` | бейджі меню; також `pathologyIncoming, pathologyCito, pathologyOverdue` у `/worklist/summary` |
| GET | `/pathology/cases/{id}` | id — id замовлення, № справи або `misReferralId`: шапка, `stages[]`, `citoTimer`, направлення, об'єкти → блоки → скельця, `totals`, чернетка висновку, `composedText`, `reflex[]`, `versions[]`, `preliminary`, `citoNotices[]`, `stageLog[]`, `events[]` |
| PUT | `/pathology/cases/{id}/gross` (= POST `…/blocks`) | `{macro, templateName, objects[{objectId, blocks[{pieces}]}], finish}`; finish → скельця Г-Е, вартість 420 + 140 × (блоків − 1) (+900 CITO), етап PROC, `STAGE_CHANGED` |
| POST | `/pathology/scan` | `{barcode, stage?, device?}` — етап касети/скельця; `/pathology/cases/{id}/advance {stage}` — етап для всіх касет |
| POST | `/pathology/cases/{id}/additional` | `{blockId, methods[] (коди/назви з lab_stain_method), source LAB\|MIS\|MDK}` → скельця «ІГХ в роботі», ціна, SLA +3 роб. дні, `ADDITIONAL_STUDY_ORDERED`; виданий блок — 409 |
| POST | `/pathology/slides/{id}/ready` | ІГХ-скельце готове |
| PUT | `/pathology/cases/{id}/report` | `{macro, micro, conclusionText, icd10, icdoTopo, icdoMorph, grade, pt, pn, lnTotal, lnPositive, margin, lvi, pni, ihc[{marker, slideId, resultText, score, unit}]}`; після підпису — 409 |
| POST | `…/report/preliminary` | `{text}` → `RESULT_PRELIMINARY` |
| POST | `…/report/sign` | `{signer{drfo, name, certSerial, validTo}}` (тестовий сертифікат — `GET /ehealth/session`.testCertificate) → `RELEASED`, блоки в архів, `RESULT_FINAL`; неочікувана злоякісність (морфологія /3, /6 при клінічному діагнозі не C/D0) → `CRITICAL_RESULT`; `warnings[]` — ІГХ без результату |
| POST | `…/report/addendum` | `{type IHC\|REVIEW\|CORRECTION, text, reason (обов'язково для CORRECTION), changesDiagnosis, icd10?, icdoTopo?, icdoMorph?, ihc[], signer}` → нова версія vN, `ADDENDUM {payload, notifyMdk}` |
| POST | `/pathology/cases/{id}/cito-notice` | `{text, notifiedPerson, method PHONE\|INTERCOM\|IN_PERSON, readBack}`; без read-back — 422; `CRITICAL_RESULT {operationId, minutesFromReceipt}` і аудит |
| GET | `/pathology/archive?status=ARCHIVED\|ISSUED\|overdue&q=` | блоки підписаних справ, місце зберігання, видача |
| POST | `/pathology/blocks/{id}/issue`, `/pathology/blocks/{id}/return` | `{recipient, document, purpose, dueBack}`; повернення |
| GET | `/pathology/dictionaries` | шаблони, МКХ-О (топографія/морфологія), методики та ІГХ з цінами, reflex-правила, типи матеріалу (SLA, група послуг), фіксатори, причини відмови, тарифи |
| GET | `/pathology/cases/{id}/form014`, `/pathology/cases/{id}/form014.pdf?sides=1,2` | поля обох сторін форми 014/о; до підпису п. 18 і підписи порожні; PDF A4 альбомна |

Payload `RESULT_FINAL` / `ADDENDUM.payload`: `caseNo, version, kind, addendumType, signedAt, signedBy, conclusionText, icd10, icdoTopo, icdoMorph, grade, pT, pN, lnTotal, lnPositive, margin, lvi, pni, ihc[], changesDiagnosis, reason`.

**Модель даних (нові таблиці [ЛІС])**: `lab_path_referral`, `lab_path_object`, `lab_path_block`, `lab_path_slide`, `lab_path_stage_log`, `lab_path_report`, `lab_path_ihc_result`, `lab_path_cito_notice`, `lab_path_issue`; довідники `lab_path_template`, `dct_icdo`, `lab_stain_method`, `lab_ihc_reflex_rule`, `lab_path_material_type`, `lab_fixative`, `lab_path_reject_reason` (сід `db/seed/pathology.json`) — міграція `db/migrations/R1.1/007_ch22_pathology.sql`.

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

## 8. API модуля «Інструкції» `/api/v1/guide/*` (MedLink Guide)
Окрема бібліотека `src/MedLink.Guide`, схема БД `guide` (`db/postgres/guide/001_guide_schema.sql`). Опис і конфігурація — `src/MedLink.Guide/README.md`, `docs/GUIDE_MODULE.md`. Заголовки: `X-MedLink-Employee-Id`, `X-MedLink-Roles`, `X-MedLink-Tenant-Id`. Шляхи відносні до `PathBase` хоста. Помилки — ProblemDetails.

**Плеєр (відкрито):** `GET apps/{app}/catalog?role=&route=` (опубліковані розділи та інструкції), `GET scenarios/{id}/play` (знімок опублікованої версії з якорями), `POST progress`, `GET progress/me`, `POST anchor-reports` (`ok|healed|missing`), `GET media/{id}`.

**Конструктор (роль редактора або `Guide:AllowAnonymousEditing`):** `POST apps`; `GET apps/{app}/tree`; папки: `POST/PUT/DELETE folders`, `POST folders/{id}/move`; інструкції: `POST/GET/PUT/DELETE scenarios`, `POST scenarios/{id}/duplicate|publish|unpublish`, `PUT scenarios/{id}/steps|variables`, `GET scenarios/{id}/export`, `POST apps/{app}/import`; якорі: `GET apps/{app}/anchors`, `PUT anchors/{id}`, `GET apps/{app}/anchors/health`, `POST anchor-reports/{id}/accept`; `POST media`.

Сутність кроку: `stepType` (`info|click|input|select|hover|navigate|wait`), `expect`, `highlight` (`element|parent|rect|none`), `parentLevels`, `inputValue` з `{{змінними}}`, `inputMode`, `anchor {strategies[], fingerprint, parentLevels}`.
