# Матриця покриття ТЗ MedLink LIS 4.0

Дата аудиту: 2026-11-09. Режим: лише читання коду, документів і тестів (збірка і запуск не виконувались; код не змінювався). Джерела: `docs/ТЗ_ЛІС_MedLink_v4.md`, `docs/РЕЄСТР_ЗАДАЧ.md`, `docs/AUDIT_MOCKS.md`, `docs/_inventory.json`, `docs/MIGRATION_LAB_GAP_ANALYSIS.md`, `tests/*`, `src/MedLink.Guide/Seed/*.json`, контролери та сервіси `src/MedLink.LIS.Api`.

## 1. Підсумок

Позначення: **✔** — реалізовано і є автотест, що перевіряє поведінку (не лише код 200); **◐** — частково реалізовано (C), або реалізовано, але автотесту немає чи він поверхневий (T), або працює через імітацію (M); **✘** — немає реалізації (і тесту).

| Показник | Всього | ✔ | ◐ | ✘ | інше |
|---|---:|---:|---:|---:|---|
| Вимоги ТЗ (FR-ідентифікатори, діапазони розгорнуто) | 477 | 246 | 161 | 69 | 1 виключено рішенням власника (FR-GAP-072) |
| з них ◐: без автотесту / часткова реалізація / імітація | 161 | — | 131 / 26 / 4 | — | |
| Приймальні сценарії S-xx (розд. 12.2, 21.12, 22.15, 22.18–19, 23.11, 22.20) | 71 | 44 | 24 | 3 | |
| Вимоги, що залежать від імітації (колонка «Імітація» заповнена) | 69 | | | | |

Ключові висновки:

1. **Автотестів 368** (xUnit `[Fact]/[Theory]`, ~6,4 тис. рядків): сильно покриті протоколи приладів (ASTM/HL7/текст, 64 профілі, побудова замовлень), правила (флаги, delta, автоверифікація, рефлекс, Вестгард, EUCAST), каскад норм, патоморфологія/цитологія (S-40…S-48, S-110…S-125), контейнери (S-130…S-135), ЕСОЗ-контракт/outbox, МІС-розподіл (S-21…S-36, S-50…S-60), білінг.
2. **Жодного API-тесту** немає для: біобанку, реагентів, мікробіології (API), панічних дзвінків, повтору (`rerun`), `autoverify` через API, імпорту результатів, налаштувань, аудиту, аналітики TAT, друку робочого листа, пакетної реєстрації, коннектора (heartbeat/буфер/команди), порталу (до release, чужий пацієнт, тренд). Усі «заплановані» тести Додатка E з іменами `I-*`/`U-*`/`E-*` як такі не існують; Playwright E2E, перф-тести (NFR-01…03), CI-матриця PostgreSQL — відсутні (є лише перемикач `LIS_TEST_PG`).
3. **Дефекти відносно ТЗ, знайдені аудитом** (код не змінювався): (a) `release` замовлення НЕ блокується незареєстрованим панічним дзвінком (FR-VAL-002, S-06); (b) `ConsumeAsync` реагенту блокує списання при нестачі (суперечить FR-REA-002) і списання лише ручне; (c) CALCULATED-показники не обчислюються (FR-WS-005); (d) немає mutex єдиного екземпляра коннектора та перевірки мінімальної версії (FR-CON-007/008); (e) ліміти «200 замовлень/413» і «5000 рядків імпорту» не реалізовані; (f) бланк тільки українською (FR-PRN-002); (g) коефіцієнт `supersededByRerun` відсутній.
4. Імітації та відсутня автентифікація описані в розд. 6; **docs/AUDIT_MOCKS.md розд. 4 частково застарів**: пакет Npgsql, перемикач `Database:Provider`, `UseNpgsql` для ЛІС і Guide вже є (`Program.cs`), тести можна запускати на PG через `LIS_TEST_PG`; відкритими лишаються SQL-міграції, `PRAGMA`/`SchemaUpgrader`, атомарність нумераторів.

## 2. Покриття за модулями (FR-групи)

| Група | Модуль | Реалізація (API / екран) | Тести | Guide | ✔ | ◐ | ✘ | Імітація |
|---|---|---|---|---|---:|---:|---:|---|
| FR-REG | Реєстрація замовлень | POST /orders, /orders/batch, OrderService; екрани /laboratory/orders, /order-matrix | ApiIntegrationTests.Full_happy_path; MisIntegrationApiTests; TubePlanApiTests | lab.orders.*, lab.matrix.* | 14 | 5 | 0 | сповіщення QUEUED |
| FR-PHL | Пункт забору | /samples/{b}/collect, ZplLabelBuilder; /laboratory/phlebotomy | Full_happy_path; BarcodeTests; TubePlannerTests | lab.phlebotomy.* | 5 | 1 | 0 | — |
| FR-PRE | Преаналітика / тара | TubePlanner, TubePlanService, /orders/tube-plan | TubePlannerTests, TubePlanApiTests | lab.orders.tube-plan | 1 | 3 | 3 | — |
| FR-LOG | Логістика | /logistics/manifests*, /logistics/containers*; /laboratory/logistics | Full_happy_path (маніфест, холод); TransportContainerApiTests S130–S135 | lab.logistics.*, lab.containers.* | 16 | 5 | 0 | — |
| FR-SMP | Стадії та поділ зразка | /samples/{b}/split|stage|tree, SampleProcessingService | Sample_split_creates_children… | lab.samples.* | 7 | 6 | 0 | — |
| FR-SEC | Підрозділи / журнали | /sections*, SectionJournalService; /laboratory/sections | Journal_numbering_preview…; Full_happy_path (journal-entries) | lab.sections.* | 4 | 6 | 0 | — |
| FR-WS | Робочий стіл | GET /worklist, PUT /results, /worklist/autoverify, /worklist/batches; /laboratory/workstation | RulesEngineTests (unit); Full_happy_path | lab.ws.* | 3 | 10 | 1 | — |
| FR-VAL | Валідація / паніка | POST /results/{id}/verify|reject|reopen, /panic-calls, /panic/pending; /laboratory/validation | Full_happy_path (verify); AutoVerification unit | lab.val.* | 1 | 5 | 2 | — |
| FR-QC | ВКЯ | /qc/*, QcService, WestgardEvaluator; /laboratory/quality-control | QcAndEucastTests (unit); Qc_1_3s_creates_lockout… | lab.qc.* | 5 | 4 | 0 | — |
| FR-NRM | Норми / послуги | /norms/*, NormsCascadeResolver, ReflexRuleEngine; /laboratory/norms | NormsCascadeResolverTests; RulesEngineTests | lab.nm.* | 5 | 5 | 0 | — |
| FR-DIC | Довідники | /dictionaries/*; /laboratory/dictionaries | Dictionary_crud (12 довідників) | lab.dict.* | 6 | 6 | 0 | sync інтервенцій — MOCK |
| FR-ADM | Адміністрування | /settings/*, /audit, /health; /laboratory/admin/* | частково (health, numerators) | lab.set.* | 3 | 4 | 1 | — |
| FR-IMP | Імпорт файлом | POST /import/results, ImportService; /laboratory/import | НЕМАЄ | lab.im.* | 0 | 4 | 0 | — |
| FR-PRN | Друк / бланки | /orders/{id}/report[.pdf], /verify/{token}, labels | Full_happy_path (HTML/PDF/QR); BarcodeTests | lab.orders.card-report | 3 | 2 | 1 | VerifySecret-заглушка |
| FR-GW | Шлюз аналізаторів | /analyzers*, /connectors*, /connector/*, ConnectorService | Full_happy_path (register/config/results/unmatched); OrderBuilder/Parser unit | lab.an.* | 4 | 7 | 0 | — |
| FR-CON | Коннектор | src/MedLink.LabConnector (сесії, буфер, heartbeat) | AstmSessionE2ETests; PipelineAndCliTests | lab.an.connect* | 0 | 6 | 2 | — |
| FR-PORT | Кабінет пацієнта | /portal/{patientId}/*, ProgressiveReleaseService; /portal | Full_happy_path (portal progress 100%) | — (сценаріїв порталу немає) | 1 | 12 | 0 | сповіщення без відправника; портал без автентифікації |
| FR-BIO | Біобанк | /biobank/*, BiobankReagentService; /laboratory/biobank | НЕМАЄ | lab.bb.* | 0 | 6 | 0 | — |
| FR-REA | Реагенти | /reagents/*; /laboratory/reagents | НЕМАЄ | lab.rg.* | 0 | 3 | 1 | — |
| FR-MIC | Мікробіологія | EucastInterpreter, /microbiology/*; /laboratory/microbiology | QcAndEucastTests (unit EUCAST/фенотипи); API-потоку немає | lab.mb.* | 5 | 3 | 0 | — |
| FR-TAT | Аналітика TAT | /analytics/*, TatCalculator; /laboratory/analytics-tat | RulesEngineTests.Stages_and_total, Median_and_p90 (unit) | lab.tat.* | 1 | 5 | 0 | — |
| FR-RPT | Звіти лабораторії | /reports/*, WorkReportsService; /laboratory/reports | MisIntegrationApiTests.Work_views_reports_and_cumulative_export | lab.rp.* | 2 | 9 | 8 | — |
| FR-MIS | Інтеграція з МІС | MisIntegrationController, MisReferralService, MisCatalogService; /laboratory/refs | MisIntegrationApiTests (S-21…S-29) | lab.mis.*, lab.incoming.* | 29 | 0 | 0 | події без доставки; словник — демо |
| FR-SVA | Послуга ЕСОЗ × аналізатори | /analyzer-services/*, /mis/catalog; /laboratory/analyzer-services | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | 15 | 0 | 0 | — |
| FR-PKG | Пакети МІС | /mis/catalog/packages, MisReferralService | MisIntegrationApiTests.Demo_inbox… | lab.mis.packages|package-referral | 10 | 0 | 0 | — |
| FR-DST | Розподіл по аналізаторах | /distribution/*; /laboratory/refs | MisIntegrationApiTests.Distribution_change_auto_and_confirm… | lab.incoming.* | 12 | 0 | 0 | — |
| FR-WRK | Робота за направленнями | /work/*; /laboratory/work | MisIntegrationApiTests.Work_views… (поверхнево) | lab.mis.work-*, lab.wr.* | 1 | 9 | 0 | — |
| FR-RTE | Маршрутизація в чергу | RoutingService, /routing/*, /analyzers/{id}/queue | MisIntegrationApiTests (S-24 частково) | lab.wr.analyzer-queue|reroute | 9 | 3 | 0 | — |
| FR-EQP | Послуги обладнання | /analyzers/{id}/services | MisIntegrationApiTests.Service_matrix… | lab.mis.disable-service | 8 | 0 | 0 | — |
| FR-SGN | КЕП / відправка в ЕСОЗ | EhealthSignController, EhealthOutbox; /laboratory/ehealth | MisIntegrationApiTests.Ehealth_session…; Ehealth*Tests | lab.esoz.* | 23 | 0 | 0 | ЕСОЗ Mock/Replay, КЕП-імітація |
| FR-EHE | ЕСОЗ висновок | EhealthSignController + Ehealth/* | Ehealth* тести (Mock/Replay) | lab.esoz.* | 0 | 1 | 0 | ЕСОЗ Mock |
| FR-PAT | Патоморфологія | PathologyController, PathologyService; /laboratory/pathology/* | PathologyApiTests (S40–S48, S110, S114) | lab.path.* | 43 | 10 | 9 | КЕП, події МІС, розсилка |
| FR-CYT | Цитологія | PathologyController (cytology), /laboratory/cytology | PathologyApiTests S120–S122 | lab.cyto.* | 6 | 6 | 0 | — |
| FR-BIL | Біллінг (розд. 20.9) | BillingController, BillingService, PriceCalculator | BillingApiTests, PriceCalculatorTests | lab.bill.* | 0 | 1 | 3 | — |
| FR-REF | Направлення / клієнти | ReferralsController, ClientsController, ReferralService | ReferralApiTests, ClientApiTests | lab.referrals.*, lab.clients.* | 2 | 1 | 2 | ЕСОЗ Mock/Replay |
| FR-SO | Send-out розрахунки | SendOutController (без взаєморозрахунків) | SendOutApiTests (лише потік) | lab.sendout.* | 0 | 0 | 2 | — |
| FR-QUE | Електронна черга | — (немає) | — | — | 0 | 0 | 6 | — |
| FR-TEN | Мультитенантність | — (лише в окремій гілці tenancy, не в main) | — | — | 0 | 0 | 9 | — |
| FR-GAP | Прогалини (розд. 19) | різне, див. рядки | різне | — | 2 | 13 | 20 | — |

Примітка щодо методу: статус ✔ для модулів розд. 21–23 (МІС/СВА/ЕСОЗ) і розд. 22 (ПГД) спирається на розділи «Стан реалізації» ТЗ (21.15, 22.17, 23.13) **та** на наявність тесту, що перевіряє відповідну поведінку; для FR без прямого посилання в тесті оцінка зроблена за назвою/змістом тесту і може бути завищена на 1–2 пункти в межах групи (див. повну таблицю в додатку А).

## 3. Біохімічна лабораторія: процес від замовлення до видачі

Колонки: **Є?** — ✔ є / ◐ частково / ✘ немає; **Тест** — що реально перевіряє; **Імітація** — що не справжнє.

| № | Крок | Є? | Де (API / екран / клас) | Чим покрито тестом | Імітація / прогалина | Guide |
|---|---|---|---|---|---|---|
| 1 | Реєстрація замовлення (пацієнт, лікар, підрозділ, CITO, направлення) | ✔ | `POST /orders`, `OrderService`; `/laboratory/orders` | Full_happy_path (створення, №-маска, NEW); MedLinkAlignmentTests; BillingApiTests | CITO і пакетна реєстрація (ліміт 200/413) не тестуються; пацієнти — демо-сід `org.json`; автентифікації користувача нема (`X-MedLink-Employee-Id`) | lab.orders.register-* |
| 2 | Призначення за матрицею / обране | ✔ | `/dictionaries/order-matrix*`; `/laboratory/order-matrix` | Journal_numbering_preview_follows_mask_and_matrix_has_shape | вбудований режим із картки пацієнта evomis (FR-REG-033) не виявлено | lab.matrix.* |
| 3 | План пробірок (об'єднання/окремі/об'єм) | ✔ | `TubePlanner`, `/orders/tube-plan` | TubePlannerTests (8), TubePlanApiTests (5) | — | lab.orders.tube-plan |
| 4 | Штрихкоди (EAN-8, Code128, ZPL, QR) | ✔ | `TubeBarcodeGenerator`, `ZplLabelBuilder`, `Code128Svg`, `QrSvg` | BarcodeTests (14 фактів) | Попередньо надруковані рулонні коди (FR-PRE-005) ✘; друк — через локальний агент `localhost:5088` (без агента лише повідомлення) | lab.phlebotomy.label, lab.orders.queue-labels |
| 5 | Забір біоматеріалу (ідентифікація, чекліст, Order of Draw) | ✔ | `POST /samples/{b}/collect`; `/laboratory/phlebotomy` | Full_happy_path (collect, повтор → 409); негатив `idVerified=false` не тестується | — | lab.phlebotomy.collect-full |
| 6 | Транспорт: маніфести, контейнери, холодовий ланцюг | ✔ | `/logistics/manifests*`, `/logistics/containers*` | Full_happy_path (12 °C → порушення); TransportContainerApiTests S130–S135 | PWA кур'єра/геомітки (FR-GAP-071) ✘; температура вводиться вручну (датчика немає) | lab.logistics.*, lab.containers.* |
| 7 | Прийом проб, бракераж з повторним замовленням | ◐ | `POST /samples/{b}/receive|reject`, `/logistics/manifests/{id}/receive` | Full_happy_path (прийом); реєстр браку/повтор — без тесту (S-03) | `createRepeatOrder` без автотесту | lab.samples.receive, reject |
| 8 | Сортування по підрозділах, аліквоти, стадії (центрифугування…) | ✔ | `/samples/{b}/split|stage|tree`, `/sections*`, журнали відділень | Sample_split_creates_children…; Journal_numbering_preview… | шаблони стадій і SMP-023/025/027/029 без тесту | lab.samples.split, stages; lab.sections.* |
| 9 | Робочі списки (черга приладу, батчі, друк листа) | ◐ | `GET /worklist`, `/worklist/batches*`, `/analyzers/{id}/queue` | Health_swagger_worklist_orders_respond; Distribution_change… (черга) | батчі (`B-yyMMdd-nnn`) та друк листа — без тесту; пошук за `changedSince` (polling) не реалізований | lab.ws.batch, lab.wr.analyzer-queue |
| 10 | Прийом результатів з приладів ASTM / HL7 / текст | ✔ | `LabConnector` (TCP/COM, 64 профілі), `Core.Protocols`, `POST /connector/results|messages` | AstmFrameCodecTests (10), AstmParserTests (19), Hl7Tests (9), TextParserTests (13), OrderBuilderTests (13), AstmSessionE2ETests (5), Full_happy_path (7 matched, 1 unmatched) | Реального приладу в CI немає (критерій приймання R1 №2 не підтверджено); COM-порт та файловий обмін — без тестів; 5 з 69 типів приладів відсутні в каталозі | lab.an.* |
| 11 | Ручне введення результату | ✔ | `PUT /results/{orderTestId}`, `ResultPipelineService` | Full_happy_path (GLU 5,2 → AUTO_VERIFIED/NORMAL; 403 для кур'єра) | CALCULATED-показники (FR-WS-005) ✘; perf ≤300 мс не вимірюється | lab.ws.enter-result, fast-entry |
| 12 | Правила: флаг, норма (каскад), delta-check, правдоподібність, рефлекс, автоверифікація | ✔ | `ResultFlagger`, `NormsCascadeResolver`, `DeltaCheckEvaluator`, `PlausibilityChecker`, `ReflexRuleEngine`, `AutoVerificationEngine` | RulesEngineTests (≈24), NormsCascadeResolverTests (13) | Лише unit; API-інтеграція (S-05/S-07/S-60) не асертується. Немає авто-повтору/розведення за правилами | lab.ws.autoverify, reflex; lab.nm.* |
| 13 | Паніка: журнал дзвінків, read-back, блок видачі | ◐ | `/panic-calls`, `/panic/pending` | **жодного тесту** | **`release` НЕ блокується відсутнім дзвінком** (FR-VAL-002 ✘); push/SMS лікарю (FR-VAL-008) ✘; ескалація (FR-GAP-020) ◐ | lab.val.critical-call |
| 14 | Верифікація лікарем, reject, reopen, пакетна | ✔ | `/results/{id}/verify|reject|reopen`, `verify-batch` | Full_happy_path (403/400/VERIFIED, batch); reject/reopen/skipped не тестуються | override lockout (коментар ≥10 симв.) реалізований, не тестується | lab.val.* |
| 15 | Повтор (rerun) та розведення | ◐ | `POST /results/{id}/rerun`; `dilution` у замовленні приладу (OrderBuilders) | **немає тесту** rerun; dilution — лише в unit побудови замовлення | Авто-розведення при виході за лінійність, правила авто-повтору, `supersededByRerun` — ✘ | lab.ws.row-actions |
| 16 | ВКЯ: Леві-Дженнінгс, правила Вестгарда, lockout, протокол | ✔ | `/qc/*`, `WestgardEvaluator`, `QcStatistics`; `/laboratory/quality-control` | QcAndEucastTests (Вестгард 1_2s/1_3s/2_2s/R_4s/4_1s/10x, статистика), Qc_1_3s_creates_lockout… | Правила 2of3_2s, 7T, 9x не реалізовані; ВКЯ з приладу (`isQc`, лот) і звіт `/qc/report` — без тесту; EQA ✘ (R3) | lab.qc.* |
| 17 | Реагенти і лоти (облік, терміни, алерти) | ◐ | `/reagents/lots*`, `/reagents/alerts` | **жодного тесту** | Списання лише ручне; автоперемикання лота ✘; 409 при нестачі суперечить ТЗ; зв'язку «результат ↔ лот» немає (FR-GAP-030) | lab.rg.* |
| 18 | Калібрування приладів | ✘ | — (є лише слово «калібрування» як причина розблокування ВКЯ) | — | FR-GAP-031: журналу калібрувань, нагадувань, зв'язку з результатом немає | — |
| 19 | Бланки результатів (HTML/PDF, QR-перевірка) | ◐ | `/orders/{id}/report[.pdf]`, `/verify/{token}`, `ReportService` | Full_happy_path (HTML, PDF, valid=true); QR unit | Лише uk; «ВИПРАВЛЕНО»/дублікат/архів PDF із хешем ✘ (FR-GAP-010/012); PDF-fallback у HTML; `VerifySecret="change-me…"` | lab.orders.card-report |
| 20 | Видача пацієнту, поетапна видача, сповіщення | ✔ | `POST /orders/{id}/release`, `ProgressiveReleaseService`, `/portal/*` | Full_happy_path (release, portal 100 %); NotificationDeliveryTests | Відправник сповіщень (NotificationDispatcher) з провайдерами каналів, повторами, згодою; портал без автентифікації; чужий/до-release випадки не тестуються | — |
| 21 | Звіт в ЕСОЗ (КЕП, outbox) | ◐ | `EhealthSignController`, `EhealthOutbox` | MisIntegrationApiTests.Ehealth_session…, EhealthOutboxApiTests (10), EhealthContractTests (18) | Режим Mock/Replay; Live OAuth ✘; віджета КЕП немає | lab.esoz.* |
| 22 | Архів проб (біобанк, утилізація) | ◐ | `/biobank/*`; `/laboratory/biobank` | **жодного тесту** (S-11 ✘) | Політика зберігання за підрозділом (FR-GAP-050) ◐ | lab.bb.* |
| 23 | Рахунки, ціни, оплата, страхові, НСЗУ | ✔ | `/payers`, `/price-lists`, `/billing/quote`, `/orders/{id}/payments|invoice` | BillingApiTests (5), PriceCalculatorTests (4), ClientApiTests | Фіскалізація/РРО/каса МІС ✘ (FR-BIL-002…004), НСЗУ-звітність ✘ | lab.bill.* |
| 24 | Аналітика TAT, звіти, експорт | ◐ | `/analytics/*`, `/reports/*` | TatCalculator (unit), кумулятивний звіт (MIS) | BI/виручка/навантаження персоналу ✘ (T-145, R2) | lab.tat.*, lab.rp.* |
| 25 | Аудит, ролі, автентифікація | ◐ | `AuditService`, `RolePolicy`, `CurrentEmployeeMiddleware` | Unknown_employee_is_forbidden_for_mutations; 403-перевірки в Full_happy_path | **Автентифікації немає** (заголовок `X-MedLink-Employee-Id`, за замовчуванням адміністратор); журнал переглядів ✘ | lab.set.audit, lab.nav.switch-role |

Підсумок по біохімії: з 25 кроків — ✔ 13 (1–6, 8, 10–12, 14, 16, 23), ◐ 11, ✘ 1 (калібрування). Найслабші місця — паніка/блок видачі (крок 13), реагенти без автосписання і зв'язку з результатом (17), відсутнє калібрування (18), повтор/розведення без тестів і правил (15), автентифікація (25).

## 4. Приймальні сценарії S-xx: тест і перевірка по суті

Усього 71: ✔ 44, ◐ 24, ✘ 3. «✔» означає, що в тесті є асерти по ключових умовах «Then» сценарію; «◐» — тест покриває лише частину (нижче вказано, чого не вистачає); «✘» — тесту немає. Іменованих тестів `S..` у коді лише для ПГД/цитології/контейнерів; решта співвіднесена за змістом.

| S | Сценарій | Статус | Тест (що реально перевіряє) | Чого не вистачає / застереження | Guide |
|---|---|:--:|---|---|---|
| S-01 | Реєстрація, 2 профілі, CITO | ◐ | Full_happy_path (створення, №-маска, штрихкод 8 цифр, NEW) | CITO-бейдж і 2 профілі/2 проби не асертяться | lab.orders.register-full, register-cito |
| S-02 | Забір, друк етикеток, чекліст | ◐ | Full_happy_path (collect, 409 повтор); BarcodeTests.Zpl_*; TubePlannerTests (Order of Draw) | немає наскрізного: ZPL через API /labels не перевіряється | lab.phlebotomy.collect-full, label |
| S-03 | Логістика 4,2→9,5 °C, бракераж з повтором | ◐ | Full_happy_path (маніфест, 12 °C → isColdChainViolated) | бракераж (гемоліз) і замовлення-повтор не тестуються | lab.logistics.*, lab.samples.reject |
| S-04 | Sysmex: запит → замовлення → 5 результатів | ◐ | AstmSessionE2ETests.Query_from_sysmex_is_acked…; Full_happy_path (connector/results, unmatched) | журнал обміну і AUTO_VERIFIED для 5 нормальних на API-рівні не асертяться | lab.an.simulator, lab.an.messages |
| S-05 | Mindray HL7: QRY→QCK+DSR, ORU→ACK, delta 42,7 % | ◐ | AstmSessionE2ETests.Hl7_mllp_session…; RulesEngineTests.Alert_when_delta_exceeds_threshold | ALT delta → NEEDS_REVIEW через API не перевіряється | lab.an.connect |
| S-06 | GLU 26,4: критичне, заборона release, дзвінок, verify, release | ◐ | Full_happy_path (K 6,9 CRIT_HIGH → NEEDS_REVIEW; verify без коментаря 400; release до verify 409; QR valid; PDF) | панічні дзвінки, panic/pending, 409 PANIC_CALL_REQUIRED — НЕ тестуються і НЕ реалізовані (release не перевіряє дзвінок) | lab.val.critical-call, critical-verify |
| S-07 | Reflex TSH → FT4 | ◐ | RulesEngineTests.Tsh_above_threshold_triggers_ft4 (unit) | створення LabOrderTest(isReflex) і аудит у БД не перевіряються | lab.ws.reflex |
| S-08 | ВКЯ 8,28 при 7,2±0,3 → lockout → розблокування | ✔ | Qc_1_3s_creates_lockout… (lockout, L-J=LOCKOUT, 403, протокол); QcAndEucastTests (всі правила) | блок автоверифікації WBC — лише unit (Lockout_blocks) | lab.qc.add-point, lockout-resolve |
| S-09 | Норми: 4 приклади, 400 на невалідні межі | ◐ | NormsCascadeResolverTests (13: вагітність, цикл, МКХ, вік) | 400 на normLow>normHigh та trace не тестуються | lab.nm.resolver, add-layer |
| S-10 | Кабінет пацієнта: статуси без значень, тренд, PDF, 404 | ◐ | Full_happy_path (portal progress 100 % після release) | до release/404 чужого пацієнта/тренд/PDF не тестуються | — |
| S-11 | Біобанк: розміщення, пошук, видача, списання | ✘ | немає жодного автотесту | код є (BiobankReagentService) | lab.bb.* |
| S-12 | Коннектор: UI → ZIP → setup → ACTIVE → RELOAD_CONFIG → офлайн-буфер | ◐ | Full_happy_path (register/config/401); PipelineAndCliTests (конфіг, фабрика) | ZIP, setup, RELOAD_CONFIG, offline-буфер, heartbeat — без тестів; інсталятори не тестуються | lab.an.connector-create, connector-manage |
| S-13 | Мікробіологія E. coli, CRE → паніка | ◐ | QcAndEucastTests.Cre_detected…, Mic_interpretation (unit) | API-потік культури/ізоляту/паніки не тестується | lab.mb.cre-case |
| S-14 | Імпорт CSV dryRun | ✘ | немає жодного автотесту | ImportService існує | lab.im.csv-dryrun |
| S-15 | TAT після S-01…S-06 | ◐ | RulesEngineTests.Stages_and_total, Median_and_p90 (unit) | ендпоінт /analytics/tat і SLA CITO не тестуються | lab.tat.read, sla |
| S-21 | Інтервенція ЗАК → статус «Готово» | ◐ | Service_matrix_card_and_catalog_for_mis (NO_EQUIPMENT для DEMO-LAB-TM) | READY для ЗАК явно не асертиться | lab.mis.interventions |
| S-22 | Амбулаторне е-направлення, показники HGB/WBC/PLT | ✔ | Referral_validates_indicators…; Distribution_change_auto_and_confirm_enqueue_tests | — | lab.mis.ambulatory-ehealth |
| S-23 | Стаціонар: замовлення з відділенням і ліжком | ◐ | Demo_inbox… (сід ICU CONFIRMED) | POST внутрішнього (INPATIENT) направлення в тесті не створюється | lab.mis.inpatient-order |
| S-24 | Резерв при lockout основного приладу | ◐ | Cito_routing_prefers_fastest…skips_long_offline (offline) | переназначення саме за lockout ВКЯ не тестується | lab.wr.reroute |
| S-25 | Знято обов'язковий WBC → 422 | ✔ | Referral_validates_indicators_and_rejects_duplicates (required-indicator-missing) | — | lab.mis.select-indicators |
| S-26 | Е-направлення без переліку показників | ✔ | Needs_selection_requires_indicators_before_confirm | — | lab.mis.select-indicators |
| S-27 | Інтервенція без співставлення → 422 | ✔ | Referral_validates… (intervention-unmapped) | — | lab.mis.interventions |
| S-28 | Показник без обладнання → NO_EQUIPMENT | ✔ | Demo_inbox… (CA125), Distribution_change… (send-out) | — | lab.mis.no-equipment |
| S-29 | Вимкнення послуги на приладі: кількість зачеплених тестів | ◐ | Service_matrix_card… (dryRun) | підтвердження і переназначення тестів PENDING не тестуються | lab.mis.disable-service |
| S-30 | Скасування в МІС при IN_ANALYSIS → 409 | ◐ | Referral_validates… (скасування до початку роботи) | 409 при IN_ANALYSIS не тестується | lab.mis.cancel-referral |
| S-31 | Відправка в ЕСОЗ: потрібен вхід | ✔ | Ehealth_session_two_factor_sign_submit_retry_and_annul (401 ehealth-auth-required) | Mock-режим | lab.esoz.login-password |
| S-32 | Вхід через КЕП, збіг підписувача | ✔ | те саме (signer-mismatch, власний тестовий сертифікат) | сертифікат тестовий | lab.esoz.confirm-kep |
| S-33 | 2FA вимкнена → увімкнути | ✔ | те саме (ehealth-totp-disabled, ehealth-invalid-code) | код 123456 захардкоджено | lab.esoz.confirm-totp |
| S-34 | L2 + КЕП → SIGNED→…→COMPLETED | ✔ | те саме; EhealthOutboxApiTests.Full_cycle_registers_report_then_completes_referral… | Mock | lab.esoz.submit-report |
| S-35 | Мережева помилка → повтор; закінчення сесії → AUTH_REQUIRED | ✔ | те саме (503, backoff, 401 на retry); EhealthOutboxApiTests.Transient_errors_back_off… | Mock | lab.esoz.retry |
| S-36 | Анулювання звіту (КЕП) → новий звіт | ✔ | те саме (entered_in_error, ANNULLED); Failed_completion_is_fixed_by_cancelling… | Mock | lab.esoz.annul |
| S-40 | ПГД: 2 контейнери → справа, МІС повідомлено | ✔ | PathologyApiTests.S40_… | події МІС без доставки | lab.path.intake-accept |
| S-41 | Без маркування: прийом заблоковано; відмова → МІС | ✔ | S41_… | — | lab.path.intake-reject |
| S-42 | Вирізка 5 блоків → 5 скелець Г-Е, ціна | ✔ | S42_… | — | lab.path.gross-blocks |
| S-43 | Reflex ІГХ для C50.4; строк +3 дні | ✔ | S43_… | — | lab.path.reflex-ihc |
| S-44 | Підпис без МКХ-О заблоковано | ✔ | S44_S45_… | — | lab.path.sign |
| S-45 | Доповнення → v2, v1 незмінна | ✔ | S44_S45_… | — | lab.path.addendum |
| S-46 | CITO: повідомлення з read-back | ✔ | S46_… | — | lab.path.cito-notice |
| S-47 | Друк 014/о: п. 18 порожній до підпису | ✔ | S47_… | — | lab.path.print-014 |
| S-48 | Консультація 6 зовнішніх скелець | ✔ | S48_… | — | lab.path.intake-accept |
| S-50 | Матриця «Послуга ЕСОЗ × аналізатори» | ✔ | Service_matrix_card_and_catalog_for_mis | — | lab.mis.matrix |
| S-51 | Лікар зняв NEU, MON → «не замовлено» | ✔ | Demo_inbox… (notOrdered MCV), Referral_validates… (NEUT_ABS) | — | lab.mis.select-indicators |
| S-52 | Пакет «Післяопераційний ВРІТ (CITO)» | ✔ | Demo_inbox_shows_package_cito… | — | lab.mis.package-referral |
| S-53 | Лаборант змінює прилад, routed_by=MANUAL | ◐ | Distribution_change_auto_and_confirm… (зміна/кандидати/відмова) | routed_by=MANUAL явно не асертиться | lab.mis.change-analyzer |
| S-54 | Авторозподіл при offline та lockout | ◐ | Cito_routing… (offline Respons); авторозподіл | lockout FIB і резерв Zonci не тестуються | lab.mis.auto-distribute |
| S-55 | Онкомаркер без обладнання → send-out/ручне/відхилити | ◐ | Distribution_change… (CA125 → send-out) | ручне/відхилення не тестуються | lab.mis.no-equipment |
| S-56 | Перенаправлення на інший прилад з причиною | ✘ | немає тесту (/analyzers/{id}/reroute, /order-tests/{id}/analyzer) | код є | lab.mis.reroute-queue |
| S-57 | Розріз «За пацієнтом» | ◐ | Work_views_reports… (виклик without перевірки вмісту) | вміст не асертиться | lab.mis.work-patients |
| S-58 | Розріз «За дослідженням»: матриця «·» | ◐ | Work_views_reports… (виклик) | вміст не асертиться | lab.mis.work-studies |
| S-59 | Кумулятивний звіт: HGB 62 з BC-20S, стрілки | ✔ | Work_views_reports_and_cumulative_export | — | lab.rp.cumulative |
| S-60 | Неправдоподібне HGB/RBC блокує автоверифікацію | ◐ | PlausibilityChecker unit (в MisIntegrationApiTests) | блок автоверифікації в конвеєрі не тестується | — |
| S-110 | Вирізка: патолог+асистент, ≤5 методик, DECALC | ✔ | S110_… | — | lab.path.gross |
| S-111 | Маршрут DECALC і стандартний блок | ✔ | S110_… (409 для DECALC/PROC) | — | lab.path.cassettes |
| S-112 | 6 методик на блок → відмова | ✔ | S110_… (tooMany) | — | lab.path.gross-blocks |
| S-113 | Відмітка покриття скла | ✔ | S114_… (COVER) | — | lab.path.slide-cover-dispose |
| S-114 | Утилізація скельця з відтворенням | ✔ | S114_… | — | lab.path.slide-cover-dispose |
| S-115 | Доповнення «зовнішній консультант» | ✔ | S114_… (CONSULT) | — | lab.path.addendum-consult |
| S-116 | Архів: приміщення/полиця, внутрішня видача без підрозділу | ✔ | S114_… | — | lab.path.archive-place, archive-issue |
| S-117 | Розсилка висновку на пошту | ◐ | S114_… (запис QUEUED, 400 на хибну адресу) | пошта не відправляється (немає відправника) | lab.path.dispatch |
| S-120 | Цитологія: відмова без анамнезу / справа ЦИТ | ✔ | S120_cytology_full_cycle… | — | lab.cyto.intake |
| S-121 | Приготування препаратів C{yy}-NNNN-n | ✔ | S120_…/S121_… | — | lab.cyto.prepare |
| S-122 | Скринінг після фарбування | ✔ | S120_… (409 до фарбування) | — | lab.cyto.screening |
| S-123 | Підпис без адекватності/категорії | ✔ | S120_… (adequacy-required); S121_… | — | lab.cyto.conclusion |
| S-124 | Високий ступінь → критичний результат у МІС | ✔ | S120_… (critical) | події МІС без доставки | lab.cyto.critical |
| S-125 | Журнал цитології: непридатні, розбіжність, TAT | ✔ | S120_… (analytics) | — | lab.cyto.journal |
| S-130 | Контейнер: укладка → запечатування → прийом з розбіжностями | ✔ | TransportContainerApiTests.S130_… | — | lab.containers.pack, receive |
| S-131 | Помилки укладки (409/404/400) | ✔ | S131_… | — | lab.containers.pack |
| S-132 | Статусні обмеження | ✔ | S132_… | — | lab.containers.* |
| S-133 | Холодовий ланцюг контейнера | ✔ | S133_… | — | lab.logistics.cold-chain |
| S-134 | Ролі контейнерів | ✔ | S134_… | — | lab.containers.types |
| S-135 | Пошук, «втрачено» | ✔ | S135_… | — | lab.containers.journal |

Guide: у `src/MedLink.Guide/Seed/*.json` 296 сценаріїв з кодами `lab.<модуль>.<дія>` (у ТЗ/документації згадується 287). Прямого посилання «S-xx ↔ Guide-код» немає; відповідність у таблиці — за змістом. Тести Guide (`MedLink.Guide.Tests`, 20 фактів) перевіряють механізм (каталог, публікація, якорі, набори, імпорт/експорт), а не відповідність кроків інструкцій реальним екранам; перевірка `AnchorReport` — лише у тесті механізму.

## 5. Вимоги без реалізації та без тесту (✘)

| FR | Вимога (скорочено) | Що саме відсутнє |
|---|---|---|
| FR-ADM-007 | Майстер першого запуску (v2 розд. 8): при порожній БД сторінка `/laboratory/dashboard` показує 3 кроки — рекві | майстра першого запуску немає |
| FR-BIL-002 | Попереднє погодження страхової: запит гарантійного листа → очікування → погоджено або відмовлено з переліком п | немає (погодження страхової, РРО/каса МІС, фінзвіти) |
| FR-BIL-003 | Каса МІС і фіскалізація (РРО/ПРРО), зміни каси, Z-звіт; передача нарахувань у фінмодуль МІС / Квитанція ЛІС не | немає (погодження страхової, РРО/каса МІС, фінзвіти) |
| FR-BIL-004 | Звіти: виручка за платниками, програмами, послугами й пунктами; дебіторка платників (виставлено / сплачено); м | немає (погодження страхової, РРО/каса МІС, фінзвіти) |
| FR-CON-007 | Один запущений екземпляр на ПК (mutex); спроба другого — помилка та вихід | mutex одного екземпляра в коді не знайдено |
| FR-CON-008 | Версія коннектора передається у heartbeat; сервер показує попередження, якщо вона старша за мінімально підтрим | перевірка мінімальної версії коннектора на сервері не знайдена |
| FR-GAP-001 | Пошук дублікатів пацієнта при реєстрації (ПІБ+дата народження+телефон/ІПН), об'єднання карток (merge) із перен | дублікатів/merge пацієнтів немає |
| FR-GAP-003 | Згода пацієнта на обробку даних та на отримання результатів каналами зв'язку (телефон/e-mail/Viber), відкликан | згод немає |
| FR-GAP-004 | Анонімізовані/кодовані дослідження (ВІЛ, генетика): видача лише за кодом, обмежений доступ / Should / План R2 | кодованих досліджень немає |
| FR-GAP-010 | Виправлений результат після видачі → новий бланк із позначкою «ВИПРАВЛЕНО» (amended report), посиланням на поп | «ВИПРАВЛЕНО»/amended-бланка немає |
| FR-GAP-012 | Архів виданих бланків PDF (незмінні копії з хешем), повторний друк копії з позначкою «ДУБЛІКАТ» / Must / План  | архіву незмінних PDF із хешем немає |
| FR-GAP-013 | Коментарі та інтерпретація лікаря до результату, шаблони інтерпретацій (текстові блоки) / Should / План R2 | інтерпретацій лікаря немає (окрім мікробіології) |
| FR-GAP-030 | Зв'язок результату з лотом реагенту, калібратором та контрольним матеріалом, що були активні на приладі в моме | зв'язку результат ↔ лот реагенту/калібратор немає |
| FR-GAP-031 | Журнал калібрувань аналізаторів (дата, лот калібратора, результат, виконавець) та нагадування про планове калі | журналу калібрувань немає (згадка лише у причинах розблокування ВКЯ) |
| FR-GAP-033 | Верифікація/валідація методик: записи про валідацію нової методики або лоту реагентів перед введенням у роботу | валідації методик немає |
| FR-GAP-040 | Компетентність персоналу: допуски співробітника до методик/приладів, дата оцінки компетентності; система не до | допусків персоналу немає |
| FR-GAP-041 | Реєстр СОП (стандартних операційних процедур) із версіями, посиланням із екранів процесів («Довідка» відкриває | реєстру СОП немає |
| FR-GAP-051 | Довідник одиниць вимірювання з коефіцієнтами перерахунку SI ↔ традиційні (ммоль/л ↔ мг/дл); показ результату в | коефіцієнтів SI↔традиційні немає |
| FR-GAP-053 | Виробничий календар лабораторії (вихідні, зміни) для коректного розрахунку TAT та обіцяного терміну готовності | виробничого календаря немає |
| FR-GAP-061 | Звітність НСЗУ: кількість досліджень за пакетами ПМГ, вивантаження у форматі НСЗУ, зв'язок з е-направленнями / | звітності НСЗУ немає |
| FR-GAP-070 | Портал лікаря-замовника (поза МІС): перегляд результатів своїх пацієнтів, сповіщення про критичні, друк / Shou | порталу лікаря-замовника немає |
| FR-GAP-071 | Мобільний застосунок/PWA кур'єра: сканування проб при передачі/прийманні, геомітки, температура / Should / Пла | PWA кур'єра немає |
| FR-GAP-073 | Резервне копіювання SQLite/PostgreSQL за розкладом, перевірка відновлення; експорт усіх даних пацієнта (портат | резервного копіювання за розкладом немає (лише бекап SQLite перед перестворенням схеми) |
| FR-GAP-074 | Моніторинг: health-метрики API та коннекторів (Prometheus-формат), сповіщення адміністратору про офлайн коннек | метрик Prometheus і алертів немає (лише /health) |
| FR-GAP-075 | Багатофіліальність: кілька лабораторій/філій в одній інсталяції з ізоляцією даних і консолідованою звітністю / | лише в гілці tenancy |
| FR-PAT-009 | Перехідний період SMED-8714: направлення на групи 21729/21730 потрапляють у ЛІС. Створення візитів у кабінеті  | заявлено «Не реалізовано» у розд. 22.17 |
| FR-PAT-027 | Антитіла та реагенти списуються зі складу (розд. 4.9) з лотом і строком придатності | заявлено «Не реалізовано» у розд. 22.17 |
| FR-PAT-028 | (Could, R2) Цифрова патологія: прикріплення зображень скелець (WSI) і посилання на переглядач | заявлено «Не реалізовано» у розд. 22.17 |
| FR-PAT-029 | Направлення блоків у зовнішню лабораторію (молекулярні дослідження, референс): через send-out (розд. 20.3) з п | заявлено «Не реалізовано» у розд. 22.17 |
| FR-PAT-046 | Подвійний підпис (Should): для визначених типів (наприклад, перша злоякісна знахідка, консультація) — другий п | подвійний підпис — R2 (T-442) |
| FR-PAT-091 | TAT за етапами (розд. 4.11): надходження → вирізка → скельця → висновок → видача; медіана і P90 за типом матер | аналітика якості 22.9 — R2 (T-444) |
| FR-PAT-092 | Показники якості: частка відмов у прийомі за причинами; розбіжність терміновий/плановий; частка доповнень, що  | аналітика якості 22.9 — R2 (T-444) |
| FR-PAT-093 | Навантаження: справи, блоки, скельця на лікаря й лаборанта за період | аналітика якості 22.9 — R2 (T-444) |
| FR-PAT-115 | **Пристрій.** Сканування касети/скельця може вказувати пристрій (гістопроцесор, мікротом, автомат фарбування); | поля пристрою при скануванні не виявлено |
| FR-PRE-005 | Режим попередньо роздрукованих штрихкодів: сканування рулонного коду → прив'язка до пробірки (`external_barcod | режиму попередньо роздрукованих рулонних штрихкодів (external_barcode) немає |
| FR-PRE-006 | Налаштування схеми ідентифікації: «унікальний на пробірку» (типово) або «штрихкод замовлення + суфікс» / Could | немає |
| FR-PRE-007 | Маршрут кур'єра: контрольні точки сканування, час у дорозі, звіт порушень; статистика відбраковки за причинами | немає |
| FR-PRN-002 | Мова бланка обирається окремо (uk за замовчуванням; en — назви тестів із `shortName`/LOINC) — перемикач у діал | бланк лише uk (html lang="uk"), перемикача en немає |
| FR-QUE-001 | Інфокіоск (планшет/термінал у холі): «Стати в чергу» за послугою (забір крові, здача матеріалу, видача результ | сутностей QueueTicket/QueueService, екранів і API немає |
| FR-QUE-002 | Табло черги на телевізорі: поточні номери й кабінети, голосовий виклик; окремий легкий веб-екран `/queue/board | сутностей QueueTicket/QueueService, екранів і API немає |
| FR-QUE-003 | Малі табло біля кабінетів (`/queue/room/{id}`): номер пацієнта, що викликається / Should | сутностей QueueTicket/QueueService, екранів і API немає |
| FR-QUE-004 | Робоче місце медсестри/реєстратора: «Викликати наступного», «Не з'явився», «Повернути в чергу», пошук пацієнта | сутностей QueueTicket/QueueService, екранів і API немає |
| FR-QUE-005 | Пріоритети: CITO, пільгові категорії, діти, запис на час; SLA очікування та звіт / Could | сутностей QueueTicket/QueueService, екранів і API немає |
| FR-QUE-006 | Технічно: сутності `QueueTicket`, `QueueService`, `QueueRoom`; оновлення табло через SignalR (стек MedLink) /  | сутностей QueueTicket/QueueService, екранів і API немає |
| FR-REA-001 | Для пари (аналізатор, тест) активним є один лот (`isActive`), інші — у резерві; при вичерпанні активного систе | автоперемикання активного лота при вичерпанні відсутнє; лише алерти |
| FR-REF-004 | Портал клініки-клієнта з автентифікацією: створення замовлень, друк етикеток, статуси, результати, акти; спові | порталу клініки з автентифікацією / згод пацієнта на передачу даних немає |
| FR-REF-005 | Згода пацієнта на передачу даних клініці-направнику, страховій, зовнішній лабораторії (FR-GAP-003); фіксація в | порталу клініки з автентифікацією / згод пацієнта на передачу даних немає |
| FR-RPT-009 | Критичні значення / Кількість, час до повідомлення (FR-VAL), ескалації (FR-GAP-020) за відділеннями / R2 | немає |
| FR-RPT-010 | Звіт для керівника (зведений) / Обсяги, TAT, брак, ВКЯ, виручка за місяць із порівнянням з попереднім (18.4) / | немає |
| FR-RPT-014 | (R2) Розсилка звіту за розкладом (e-mail керівнику) та збереження варіантів параметрів | немає |
| FR-RPT-015 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає |
| FR-RPT-016 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає |
| FR-RPT-017 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає |
| FR-RPT-018 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає |
| FR-RPT-019 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає |
| FR-SO-001 | Взаєморозрахунки з лабораторіями-виконавцями: реєстр їхніх рахунків, звірка з реєстрами send-out, оплата / Кон | взаєморозрахунки і оцінка якості партнерів відсутні |
| FR-SO-002 | Контроль якості зовнішніх лабораторій: TAT партнера, частка відмов, розбіжності; щорічна оцінка постачальника  | взаєморозрахунки і оцінка якості партнерів відсутні |
| FR-TEN-001 | Модель ізоляції — **рядкова** (колонка `organization_id` у кожній таблиці `lab_*`), як в evomis (`org_organiza | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) |
| FR-TEN-002 | Поточний tenant визначається `ITenantContext`: режим B — з claims IdentityServer (`organization_id`); режим A  | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) |
| FR-TEN-003 | Глобальні фільтри EF Core (`HasQueryFilter(e => e.OrganizationId == tenant.Id)`) на всіх `lab_*` сутностях; за | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) |
| FR-TEN-004 | Довідники поділяються на **системні** (біоматеріали, типи пробірок, методики, типи аналізаторів, каталог профі | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) |
| FR-TEN-005 | Нумератори (замовлення, штрихкоди, журнали) — у межах організації; штрихкод пробірки має префікс організації ( | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) |
| FR-TEN-006 | Коннектор прив'язується до організації при реєстрації інсталяції (`LabConnectorInstallation.organization_id`); | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) |
| FR-TEN-007 | Кабінет пацієнта показує результати пацієнта з усіх організацій, де є його картка (крос-тенантне читання лише  | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) |
| FR-TEN-008 | Аудит і журнали обміну — з `organization_id`; звіти та BI — лише в межах організації, консолідовані звіти — ок | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) |
| FR-TEN-009 | Міграція пілотних даних SQLite в мультитенантну схему: усі наявні рядки отримують `organization_id` пілотної о | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) |
| FR-VAL-002 | Критичний результат без запису `LabPanicCall` відображається у `GET /panic/pending` і в лічильнику хедера «Пан | GET /panic/pending є, але OrderService.ReleaseAsync НЕ перевіряє незареєстровані панічні дзвінки (409 PANIC_CALL_REQUIRED відсутній) |
| FR-VAL-008 | Push/SMS лікарю-направляючому — заглушка `PatientNotification(channel=PUSH, recipientType=DOCTOR)`; підтвердже | запису PatientNotification(recipientType=DOCTOR) немає навіть як заглушки |
| FR-WS-005 | `CALCULATED` тести обчислюються формулою (`formula`, напр. `MCH = HGB / RBC × 10`) після збереження складових; | поле formula є, обчислення CALCULATED-показників немає (формули читаються лише для складових у MisReferralService/RoutingService) |

Окремо — **131** вимог, які реалізовані в коді, але не мають жодного автотесту або тест лише «викликає» ендпоінт (колонка ◐ з причиною T у додатку А). Найбільші групи: портал (10), патоморфологія (9), звіти RPT (9), робота за направленнями (9), робочий стіл (8), шлюз аналізаторів (7), біобанк (6), коннектор (6), цитологія (6), журнали відділень (6), стадії зразка (6), логістика (5), норми (5), TAT (5), валідація/паніка (5).

## 6. Імітації, що лишилися, та оцінка зусиль зробити їх реальними

Оцінка в людино-днях (інженер .NET/Vue; без очікування сторонніх сторін).

| № | Імітація | Де | Що відбувається зараз | Щоб було реальним | Зусилля |
|---|---|---|---|---|---|
| 1 | **Автентифікація відсутня** | `CurrentEmployee.cs`, `CurrentEmployeeMiddleware`, `PortalController`, `store/context.js` | Співробітник береться з заголовка `X-MedLink-Employee-Id`, за замовчуванням адміністратор; портал пацієнта віддає дані за `patientId` з URL, на головній — список усіх пацієнтів | OIDC/JWT (evomis IdentityServer або власний), claims→роль, портал: OTP/Дія/сесія пацієнта, видалити «Працюю як…» і демо-банер, закрити CORS `AllowAnyOrigin` | 8–15 (залежить від провайдера ідентичності); це P0 перед пілотом |
| 2 | Сповіщення пацієнтів: черга без відправника | `OrderService.cs:~464`, `ProgressiveReleaseService.cs`, `LabPatientNotification` | Записи зі статусом `QUEUED`, ніхто не ставить `SENT`; єдиний BackgroundService — очищення логів | Фонова служба + шлюзи SMS/Viber/E-mail (SMTP), ретраї, статуси, відписка/згода (FR-GAP-003) | 4–7 (SMTP+один SMS-провайдер), +2 на кожен додатковий канал |
| 3 | Події для МІС без доставки | `MisReferralService.cs:266–305`, `lab_mis_event_log` | Події `Received/ResultReady/Completed/CRITICAL_RESULT` лише пишуться в таблицю; «емулятор МІС» у ЛІС | Webhook/клієнт API МІС з підписом, outbox + ретраї (за зразком `EhealthOutbox`), ідемпотентність | 4–6 (+ узгодження контракту з МІС) |
| 4 | ЕСОЗ: режим Mock/Replay | `Ehealth:Mode="Mock"`, `EhealthMockHandler`, `EhealthClient` | Двійник OpenAPI в пам'яті; Live вимкнено прапорцями; токен із конфігурації; Live на preprod не перевірявся | Вхід OAuth/ЦСК (L1/L2), оновлення токена, поопераційне ввімкнення Live, прогін на preprod, моніторинг outbox | 10–15 (+ очікування доступу до preprod) |
| 5 | КЕП: структурна перевірка замість криптографії; віджет відсутній | `EhealthSignService`, `EhealthSignature`, `EhealthSubmitPanel.vue` | Тестовий сертифікат `DEMO-xxxx-KEP`, 2FA-код `123456`, TOTP-секрет `DEMODEMODEMO`, підпис створює сервер (`mock-sign`) | Інтеграція JS-віджета ЦСК/IIT, перевірка ланцюга сертифікатів і ДРФО, справжній TOTP (секрет на користувача) | 6–10 |
| 6 | Словник інтервенцій ЕСОЗ: синхронізація — MOCK | `MisCatalogService.SyncAsync` | Оновлює лише `SyncedOn`; 25 демо-інтервенцій `DEMO-…` | Завантаження з ЕСОЗ/МІС, версіонування, диф-звіт | 3–5 |
| 7 | Локальний резервний парсер повідомлень приладів | `AnalyzerMessageParserFallback.cs`, `Program.cs:133` | Симулятор і `ConnectorService` використовують резервний парсер замість `Core.Protocols` («замінюється однією реєстрацією» — не виконано) | Зареєструвати `Core.Protocols.ParserFactory` у DI, прогнати `tests/protocol_samples`, видалити fallback | 1–2 |
| 8 | Підписувач/секрети за замовчуванням | `appsettings.json`: `Lab:VerifySecret="change-me…"`, `PublicBaseUrl=http://localhost:5055` | QR на бланках підписано заглушкою, посилання на localhost | Секрети через змінні середовища/Key Vault, правильний публічний URL, ротація токенів | 0,5–1 |
| 9 | Демо-дані на старті | `Lab:SeedDemoData=true`, `DemoDataSeeder`, `PathologyDemoSeeder` | 8 замовлень, паніка, маніфест, пацієнти, співробітники `org.json` створюються при порожній БД | Вимкнути на проді, окремий одноразовий імпорт довідників, співробітників/пацієнтів дає ядро МІС | 1–2 |
| 10 | Підписний/друк-агент, PDF-fallback | `printAgentService.js`, `OrdersSamplesController.cs:78` | Друк етикеток — через локальний агент `localhost:5088`; якщо QuestPDF недоступний — HTML замість PDF | Інсталятор агента друку, перевірка PDF-генерації у проді | 2–3 |
| 11 | PostgreSQL: схема й нумератори | `SchemaUpgrader`, `PRAGMA journal_mode`, `NumeratorService.cs:46–67` | Провайдер Npgsql уже є, але `EnsureCreated`, PRAGMA, `UPDATE … +1` без транзакції; міграції зупинилися на 007 | SQL-міграції+журнал, атомарні нумератори (RETURNING/sequence), тести на PG в CI | 5–8 |
| 12 | Мультитенантність | гілка `tenancy`, `FR-TEN-*` | У main відсутня | Рев'ю та злиття гілки, глобальні фільтри EF, тести ізоляції | 10–20 (залежить від стану гілки) |
| 13 | Живий обмін з приладом не підтверджений | `LabConnector` | Усі ASTM/HL7 перевірки — на зразках і емуляторі; Windows/Linux інсталяція і мережеві збої (S-12) не автоматизовані | Стенд із реальним приладом, тест offline-буфера (відключення 2 хв), CI-тест інсталятора | 5–8 |

## 7. Що доробити

### P0 (перед пілотом; безпека пацієнта, відповідність ТЗ, «дірки» у тестах критичних шляхів)

1. **Заборона `release` без зареєстрованого панічного дзвінка** (FR-VAL-002, S-06): перевірка в `OrderService.ReleaseAsync` → 409 `PANIC_CALL_REQUIRED`; тести `panic-calls`, `panic/pending`, release-409, read-back. (1–2 д.)
2. **Автентифікація й ізоляція порталу** (розд. 6 №1): мінімум — прив'язка ролі до токена, перевірка особи пацієнта; тести 404 для чужого `patientId` та «значення лише після RELEASED» (FR-PORT-001/002, S-10). (8–15 д.)
3. **Тести для непокритих критичних шляхів**: `rerun`, `autoverify` через API (S-04/S-05/S-07/S-60 в одному інтеграційному потоці), `reject`/`reopen`/`verify-batch.skipped`, бракераж із повторним замовленням (S-03), біобанк (S-11), імпорт CSV dryRun (S-14), мікробіологічний API-потік (S-13), `normLow>normHigh` 400 (S-09). (6–8 д.)
4. **Реагенти**: автосписання за результатами, не блокувати результат при нестачі (FR-REA-002), авто-перемикання лота (FR-REA-001), зв'язок «результат ↔ лот/контроль» (FR-GAP-030) + тести. (4–6 д.)
5. **Калібрування** (FR-GAP-031) і зв'язок із результатом; **CALCULATED-показники** (FR-WS-005, формули MCH/MCHC та ін.) — клінічно значущо для гематології. (4–6 д.)
6. **Відправник сповіщень** і доставка подій МІС (розд. 6 №2–3) — інакше «видача» не доходить до пацієнта/лікаря. (8–13 д.)
7. **Секрети та конфігурація проду**: `VerifySecret`, `PublicBaseUrl`, вимкнення демо-даних, CORS (розд. 6 №8–9). (1–2 д.)

### P1 (R1.1 за ТЗ 19.9 та якість)

- Згоди та дублікати пацієнтів (FR-GAP-001, 003), виправлені/анульовані бланки та архів PDF із хешем (FR-GAP-010…012), ескалація критичних (FR-GAP-020), backup за розкладом (FR-GAP-073).
- Попередньо надруковані штрихкоди (FR-PRE-005), довідник пунктів забору (FR-PRE-001), ліміти batch 200/413 та імпорту 5000 рядків, mutex і перевірка версії коннектора (FR-CON-007/008), мова бланка en (FR-PRN-002), майстер першого запуску (FR-ADM-007), `supersededByRerun` (FR-WS-007).
- Тести коннектора: offline-буфер, heartbeat/RELOAD_CONFIG (S-12); інтеграційні тести порталу, аналітики TAT, звітів «Робота за направленнями» (вміст, а не лише 200).
- Live-режим ЕСОЗ (розд. 6 №4–6) і PostgreSQL у CI (розд. 6 №11).
- Узгодити ТЗ↔код: кількість сценаріїв Guide (296 у сіді проти 287 у документах), кількість типів приладів (64 проти 65–69), назви тестів Додатка E.

### P2 (R2/R3 за реєстром)

- Електронна черга (FR-QUE-001…006), BI (FR-RPT-009/010/014…019, 18.4), фіскалізація й фін. звіти (FR-BIL-002…004), взаєморозрахунки (FR-SO-001/002), портали лікаря та клініки (FR-GAP-070, FR-REF-004), PWA кур'єра (FR-GAP-071).
- Подвійний підпис, WSI, аналітика якості ПГД (FR-PAT-046, 028, 091…094), списання антитіл (FR-PAT-027), send-out блоків (FR-PAT-029), пристрої при скануванні (FR-PAT-115).
- Мультитенантність (FR-TEN-001…009), одиниці SI (FR-GAP-051), виробничий календар (FR-GAP-053), СОП/допуски (FR-GAP-040/041), валідація методик та CAPA (FR-GAP-033/034), метрики Prometheus (FR-GAP-074), EQA (T-033).

## 8. Старі вимоги (Додаток E): відповідність «планових» тестів

| Рядок Додатка E | Фактичний тест | Статус |
|---|---|:--:|
| CORE-03 довідники | Dictionary_crud | ✔ |
| CORE-05 реєстрація | Full_happy_path; TubePlanApiTests | ◐ |
| CORE-06 штрихкоди | BarcodeTests | ✔ |
| CORE-07 батчі | — | ✘ |
| CORE-08 ASTM | AstmFrameCodec/Parser/OrderBuilder/SessionE2E | ✔ |
| CORE-09 HL7 | Hl7Tests; Hl7_mllp_session… | ✔ |
| CORE-10 введення | Full_happy_path (без perf) | ◐ |
| CORE-11 верифікація | Full_happy_path | ◐ |
| CORE-12 друк, QR | Full_happy_path; QR unit | ✔ |
| CORE-13 розсилка | — | ✘ |
| CORE-14 звітність | MIS Work_views (поверхнево) | ◐ |
| CORE-15 налаштування | — | ✘ |
| QC-01 автоверифікація | RulesEngineTests (unit) | ✔ |
| QC-02 delta | RulesEngineTests | ✔ |
| QC-03 reflex | RulesEngineTests | ✔ |
| QC-04 критичні | — | ✘ |
| QC-05..07 ВКЯ | QcAndEucastTests; Qc_1_3s… | ✔ |
| QC-09 норми | NormsCascadeResolverTests | ✔ |
| PT-02..08 портал | Full_happy_path (частково) | ◐ |
| INT-03 імпорт | — | ✘ |
| INT-04 offline коннектор | — | ✘ |
| EXT-01 мікробіологія | QcAndEucastTests (unit) | ◐ |
| EXT-05 трекінг | Full_happy_path, Sample_split | ◐ |
| COM-03 реагенти | — | ✘ |
| COM-04 біобанк | — | ✘ |
| AN-01 TAT | RulesEngineTests (unit) | ◐ |
| Процес 11 монітор шлюзу | Full_happy_path (register, unmatched) | ◐ |
| NFR-01..03 перф | — | ✘ |
| T-122 аудит | — | ✘ |

Критерії приймання R1 (розд. 12.4): №1 «усі G/W/T на SQLite, і на PG» — частково (PG через `LIS_TEST_PG`, у CI не налаштовано); №2 «12 сценаріїв на стенді з приладом» — не підтверджено; №3 інсталятор Win/Ubuntu — не автоматизовано; №4 ETL довідників з системи-джерела — виключено (розд. 20.0); №5 повнота аудиту S-06 — не перевіряється.

## Додаток А. Повна матриця FR → реалізація → тест → Guide → статус

Статус: ✔ / ◐ (причина: T — без автотесту, C — часткова реалізація, M — імітація) / ✘ / виключено. «Guide» — коди сценаріїв модуля (`lab.<модуль>.*`). Колонка «Імітація» — що не справжнє.

| FR | Вимога (скорочено) | Реалізація | Тест | Guide | Статус | Імітація |
|---|---|---|---|---|:--:|---|
| FR-ADM-001 | `GET/PUT /settings/lab`: назва, ЄДРПОУ, адреса, телефон, e-mail, ліцензія, логотип (base64 | /settings/lab | — | lab.set.* | ◐ (T) | — |
| FR-ADM-002 | Нумератори (`/settings/numerators`): `order` (маска, поточне значення, період скидання), ` | /settings/numerators | Journal_numbering_preview… | lab.set.* | ✔ | — |
| FR-ADM-003 | Користувачі (`/settings/users`) — довідкове редагування `org_employee` (ПІБ, посада, `lab_ | /settings/users | Dictionary_crud (employees) | lab.set.* | ✔ | — |
| FR-ADM-004 | Аудит (`GET /audit`): фільтри сутність/id/користувач/період; запис `{id, at, userId, userR | AuditService, lab_result_history | — (повнота аудиту S-06 не перевіряється) | lab.set.* | ◐ (T) | — |
| FR-ADM-005 | Історія результатів `lab_result_history`: кожна версія значення, прапорця, статусу, автора | AuditService, lab_result_history | — (повнота аудиту S-06 не перевіряється) | lab.set.* | ◐ (T) | — |
| FR-ADM-006 | Кольорові маркери статусів/критичності — константи теми (розд. 9.3); редагування у v4 не п | константи теми | — | lab.set.* | ◐ (T) | — |
| FR-ADM-007 | Майстер першого запуску (v2 розд. 8): при порожній БД сторінка `/laboratory/dashboard` пок | майстра першого запуску немає | — | lab.set.* | ✘ | — |
| FR-ADM-008 | Health: `GET /health` → `{status, db, version}`; Swagger `/swagger` | GET /health | Health_swagger_worklist_orders_respond | lab.set.* | ✔ | — |
| FR-BIL-001 | Знижки: ручна з причиною і правом доступу, соціальні категорії, акції, промокоди; ліміти с | franchise/patient-choice є; ручні знижки, акції, промокоди, ліміти страхової — ні | BillingApiTests.Authorization_franchise_and_patient_choice | lab.bill.* | ◐ (C) | — |
| FR-BIL-002 | Попереднє погодження страхової: запит гарантійного листа → очікування → погоджено або відм | немає (погодження страхової, РРО/каса МІС, фінзвіти) | — | lab.bill.* | ✘ | — |
| FR-BIL-003 | Каса МІС і фіскалізація (РРО/ПРРО), зміни каси, Z-звіт; передача нарахувань у фінмодуль МІ | немає (погодження страхової, РРО/каса МІС, фінзвіти) | — | lab.bill.* | ✘ | — |
| FR-BIL-004 | Звіти: виручка за платниками, програмами, послугами й пунктами; дебіторка платників (виста | немає (погодження страхової, РРО/каса МІС, фінзвіти) | — | lab.bill.* | ✘ | — |
| FR-BIO-001 | Комірка унікальна `(rackId, row, col)`; зайнята комірка → 409 | BiobankReagentService, /biobank/* | — (жодного автотесту; S-11 без тесту) | lab.bb.* | ◐ (T) | — |
| FR-BIO-002 | `expiryAt` типово = `storedAt + biomaterial.stabilityHours` для температури штатива (сиров | BiobankReagentService, /biobank/* | — (жодного автотесту; S-11 без тесту) | lab.bb.* | ◐ (T) | — |
| FR-BIO-003 | Прострочені комірки підсвічуються і потрапляють у список «До утилізації»; акт утилізації — | BiobankReagentService, /biobank/* | — (жодного автотесту; S-11 без тесту) | lab.bb.* | ◐ (T) | — |
| FR-BIO-004 | Колір комірки за типом біоматеріалу (сироватка червона, ЕДТА синя, плазма зелена), помаран | BiobankReagentService, /biobank/* | — (жодного автотесту; S-11 без тесту) | lab.bb.* | ◐ (T) | — |
| FR-BIO-005 | Паспорт комірки: штрихкод, пацієнт, біоматеріал/алікота, об'єм, дата заморозки, придатний  | BiobankReagentService, /biobank/* | — (жодного автотесту; S-11 без тесту) | lab.bb.* | ◐ (T) | — |
| FR-BIO-006 | Розмір сітки конфігурований (`rowsCount`, `colsCount`), типово 8×12; рядки літерами A…, ко | BiobankReagentService, /biobank/* | — (жодного автотесту; S-11 без тесту) | lab.bb.* | ◐ (T) | — |
| FR-CON-001 | Кожне вхідне повідомлення спочатку записується у `offline_buffer.db` (таблиці `results`, ` | OfflineBuffer, BufferRetryService, HeartbeatService, ConfigStore | AstmSessionE2ETests (без буфера/heartbeat); окремого тесту буфера немає | lab.an.connect* | ◐ (T) | — |
| FR-CON-002 | Відправка з буфера — FIFO, батчами ≤ 200, з повтором за експоненційною затримкою (1, 2, 4  | OfflineBuffer, BufferRetryService, HeartbeatService, ConfigStore | AstmSessionE2ETests (без буфера/heartbeat); окремого тесту буфера немає | lab.an.connect* | ◐ (T) | — |
| FR-CON-003 | Heartbeat `POST /connector/heartbeat` кожні `heartbeatIntervalSec` (30): версія, uptime, ` | OfflineBuffer, BufferRetryService, HeartbeatService, ConfigStore | AstmSessionE2ETests (без буфера/heartbeat); окремого тесту буфера немає | lab.an.connect* | ◐ (T) | — |
| FR-CON-004 | Команди: `RELOAD_CONFIG` → `GET /connector/config`, перезапуск змінених сесій без зупинки  | OfflineBuffer, BufferRetryService, HeartbeatService, ConfigStore | AstmSessionE2ETests (без буфера/heartbeat); окремого тесту буфера немає | lab.an.connect* | ◐ (T) | — |
| FR-CON-005 | Якщо сервер недоступний при старті — завантажити `config.cache.json` і працювати офлайн; я | OfflineBuffer, BufferRetryService, HeartbeatService, ConfigStore | AstmSessionE2ETests (без буфера/heartbeat); окремого тесту буфера немає | lab.an.connect* | ◐ (T) | — |
| FR-CON-006 | Усі часи — UTC; локальний час ПК не використовується для `measuredAt`, якщо прилад дав вла | OfflineBuffer, BufferRetryService, HeartbeatService, ConfigStore | AstmSessionE2ETests (без буфера/heartbeat); окремого тесту буфера немає | lab.an.connect* | ◐ (T) | — |
| FR-CON-007 | Один запущений екземпляр на ПК (mutex); спроба другого — помилка та вихід | mutex одного екземпляра в коді не знайдено | — | lab.an.connect* | ✘ | — |
| FR-CON-008 | Версія коннектора передається у heartbeat; сервер показує попередження, якщо вона старша з | перевірка мінімальної версії коннектора на сервері не знайдена | — | lab.an.connect* | ✘ | — |
| FR-CYT-001 | Види матеріалу: гінекологічний мазок, рідинна цитологія, пунктат/аспірат, виділення і змив | цитологія (prepare/screening/sign/analytics) | S120, S121, S122 | lab.cyto.* | ✔ | — |
| FR-CYT-002 | Прийом: сканування кожного скельця/флакона, перевірка маркування (як 22.3); для гінекологі | частково через PAT-механізми | S120 (частково) | lab.cyto.* | ◐ (T) | — |
| FR-CYT-003 | Приготування препарату: мазок або рідинна цитологія → скельце(а) з кодом `C{yy}-NNNNN-n`;  | цитологія (prepare/screening/sign/analytics) | S120, S121, S122 | lab.cyto.* | ✔ | — |
| FR-CYT-004 | Фарбування: за Папаніколау (типово), Г-Е або інша методика з довідника; етапи «приготуванн | частково через PAT-механізми | S120 (частково) | lab.cyto.* | ◐ (T) | — |
| FR-CYT-005 | Двоетапний перегляд: лаборант-скринер дає попередню оцінку (норма / підозра), лікар-цитоло | цитологія (prepare/screening/sign/analytics) | S120, S121, S122 | lab.cyto.* | ✔ | — |
| FR-CYT-006 | Структурований висновок: адекватність матеріалу (задовільний / обмежено придатний / неприд | цитологія (prepare/screening/sign/analytics) | S120, S121, S122 | lab.cyto.* | ✔ | — |
| FR-CYT-007 | Підпис КЕП лікаря-цитолога обов'язковий; після підпису висновок незмінний, зміни — тільки  | частково через PAT-механізми | S120 (частково) | lab.cyto.* | ◐ (T) | — |
| FR-CYT-008 | Високий ступінь підозри або невідповідність клінічному діагнозу — критичний результат з по | цитологія (prepare/screening/sign/analytics) | S120, S121, S122 | lab.cyto.* | ✔ | — |
| FR-CYT-009 | Скельця зберігаються в архіві з приміщенням і місцем (FR-PAT-118); видача зовнішня і внутр | частково через PAT-механізми | S120 (частково) | lab.cyto.* | ◐ (T) | — |
| FR-CYT-010 | Розсилка підписаного висновку електронною поштою за FR-PAT-119 | частково через PAT-механізми | S120 (частково) | lab.cyto.* | ◐ (T) | — |
| FR-CYT-011 | Аналітика: частка непридатних препаратів, розбіжність попередньої оцінки скринера і підсум | цитологія (prepare/screening/sign/analytics) | S120, S121, S122 | lab.cyto.* | ✔ | — |
| FR-CYT-012 | Друк форми висновку з тими ж правилами порожніх полів до підпису, що й 22.8 | частково через PAT-механізми | S120 (частково) | lab.cyto.* | ◐ (T) | — |
| FR-DIC-001 | Шаблон CRUD для всіх довідників розд. 2.2 контракту: список із `search`/`isActive`, картка | DictionaryService | Dictionary_crud | lab.dict.* | ✔ | — |
| FR-DIC-002 | Біоматеріали (18, сід системи-джерела `BM_01…BM_18`): код, назва, контейнер за замовчуванням, стаб | сід db/seed (18/12/46) | Dictionary_crud (CRUD; кількості сіду не перевіряються) | lab.dict.* | ◐ (T) | — |
| FR-DIC-003 | Типи пробірок (12, система-джерело): код, назва, колір кришки `#hex` (конвертований з TColo | сід db/seed (18/12/46) | Dictionary_crud (CRUD; кількості сіду не перевіряються) | lab.dict.* | ◐ (T) | — |
| FR-DIC-004 | Методики (46, системи-джерела `dct_lab_method_type`) — код, назва | сід db/seed (18/12/46) | Dictionary_crud (CRUD; кількості сіду не перевіряються) | lab.dict.* | ◐ (T) | — |
| FR-DIC-005 | Типи аналізаторів (65 позицій системи-джерела `ac_analyzer_type`): код, назва, виробник, категорія | 64 профілі в каталозі замість 65–69 (MIGRATION_LAB_GAP_ANALYSIS) | CatalogAndTransliterationTests.Catalog_loads_64_profiles… | lab.dict.* | ◐ (C) | — |
| FR-DIC-006 | Тести (`LabTestDefinition`) і профілі (`LabTestProfile`) — як у контракті 1.1; `GET /dicti | DictionaryService | Dictionary_crud | lab.dict.* | ✔ | — |
| FR-DIC-007 | Мікроорганізми, антибіотики (ATC, вміст диска), EUCAST breakpoints (версія) | DictionaryService | Dictionary_crud | lab.dict.* | ✔ | — |
| FR-DIC-008 | Підрозділи та співробітники — копії `org_department`/`org_employee` (у evomis — лише читан | DictionaryService | Dictionary_crud | lab.dict.* | ✔ | — |
| FR-DIC-009 | Усі довідники експортуються у JSON/CSV і імпортуються з попереднім переглядом | export/import | — | lab.dict.* | ◐ (T) | — |
| FR-DIC-010 | НОВЕДовідник «Інтервенції ЕСОЗ» (`lab_ehealth_intervention`): лише читання, синхронізація  | MisCatalogService.SyncAsync лише оновлює SyncedOn, source="MOCK" | — | lab.dict.* | ◐ (M) | синхронізація словника інтервенцій — імітація |
| FR-DIC-011 | НОВЕДовідник «Співставлення інтервенцій» (`lab_intervention_map`) — редагує LAB\_ADMIN (ро | /intervention-map, /analyzer-services | MisIntegrationApiTests | lab.dict.* | ✔ | — |
| FR-DIC-012 | НОВЕДовідник «Послуги обладнання» (`lab_analyzer_service`) — вкладка картки аналізатора й  | /intervention-map, /analyzer-services | MisIntegrationApiTests | lab.dict.* | ✔ | — |
| FR-DST-001 | Сторінка «Вхідні направлення»: внутрішні (стаціонар, поліклініка) і зовнішні (е-направленн | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-002 | Стан розподілу: «Потрібен вибір показників» (FR-MIS-022), «Є показники без обладнання», «З | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-003 | Панель розподілу по кожній послузі: показники, обрані лікарем (з позначкою обов’язкових),  | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-004 | Зміна приладу на рівні показника (список лише з приладів, що виконують цей показник; недос | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-005 | «Авторозподіл» перераховує пропозицію за правилом: пріоритет приладу → доступність (зв’язо | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-006 | Якщо показники однієї послуги розподілено між приладами — попередження «потрібна аліквота  | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-007 | «Підтвердити й поставити в черги» створює замовлення (якщо ще не створене), тести отримуют | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-008 | Налаштування `Lab:Routing:RequireConfirmation`: `true` (типово) — планові направлення чека | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-009 | CITO не чекає підтвердження: пропозиція застосовується одразу, лаборант може змінити прила | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-010 | Показник без обладнання: варіанти «send-out» (розд. 20.3), «ручне виконання», «відхилити з | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-011 | Лічильник «очікують розподілу» в меню та на дашборді; час від надходження до підтвердження | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-DST-012 | Кожна дія розподілу (пропозиція, зміна, підтвердження) — запис у `lab_routing_log` з типом | /distribution/* | Distribution_change_auto_and_confirm…, Needs_selection_requires_indicators… | lab.incoming.* | ✔ | — |
| FR-EHE-001 | Реєстрація висновку в ЕСОЗ: `POST patients/{id}/diagnostic_report_package` (КЕП) → job → e | Ehealth/* (пакет, КЕП, outbox) | Ehealth*Tests | lab.esoz.* | ◐ (M) | Mock/Replay; Live не перевірено |
| FR-EQP-001 | Картка аналізатора отримує вкладку «Послуги обладнання»: список послуг ЛІС, які виконує пр | /analyzers/{id}/services | Service_matrix_card_and_catalog_for_mis (dryRun, analyzer-code-missing) | lab.mis.disable-service | ✔ | — |
| FR-EQP-002 | Поля `lab_analyzer_service`: `analyzer_id`, `profile_id`, `is_enabled`, `priority` (1 — ос | /analyzers/{id}/services | Service_matrix_card_and_catalog_for_mis (dryRun, analyzer-code-missing) | lab.mis.disable-service | ✔ | — |
| FR-EQP-003 | Поля показника на приладі: `is_enabled`, `analyzer_test_code` (як сьогодні `analyzerCode`) | /analyzers/{id}/services | Service_matrix_card_and_catalog_for_mis (dryRun, analyzer-code-missing) | lab.mis.disable-service | ✔ | — |
| FR-EQP-004 | Первинне наповнення: кнопка «Заповнити з профілю приладу» пропонує послуги, показники яких | /analyzers/{id}/services | Service_matrix_card_and_catalog_for_mis (dryRun, analyzer-code-missing) | lab.mis.disable-service | ✔ | — |
| FR-EQP-005 | Сторінка-матриця «Послуги × обладнання»: рядки — послуги, стовпці — прилади, у клітинці —  | /analyzers/{id}/services | Service_matrix_card_and_catalog_for_mis (dryRun, analyzer-code-missing) | lab.mis.disable-service | ✔ | — |
| FR-EQP-006 | Показник можна ввімкнути на приладі, лише якщо в нього є `analyzer_test_code`; інакше — по | /analyzers/{id}/services | Service_matrix_card_and_catalog_for_mis (dryRun, analyzer-code-missing) | lab.mis.disable-service | ✔ | — |
| FR-EQP-007 | Вимкнення послуги/показника на приладі або вимкнення самого приладу запускає переназначенн | /analyzers/{id}/services | Service_matrix_card_and_catalog_for_mis (dryRun, analyzer-code-missing) | lab.mis.disable-service | ✔ | — |
| FR-EQP-008 | На картці послуги вкладка «Обладнання» — зворотний вигляд: які прилади, у якому порядку пр | /analyzers/{id}/services | Service_matrix_card_and_catalog_for_mis (dryRun, analyzer-code-missing) | lab.mis.disable-service | ✔ | — |
| FR-GAP-001 | Пошук дублікатів пацієнта при реєстрації (ПІБ+дата народження+телефон/ІПН), об'єднання кар | дублікатів/merge пацієнтів немає | — | — | ✘ | — |
| FR-GAP-002 | Ідентифікація пацієнта при заборі за двома ідентифікаторами (ISO 15189 7.2.6): ПІБ + дата  | чек-лист idVerified є; другий ідентифікатор не вимагається | — | — | ◐ (C) | — |
| FR-GAP-003 | Згода пацієнта на обробку даних та на отримання результатів каналами зв'язку (телефон/e-ma | згод немає | — | — | ✘ | — |
| FR-GAP-004 | Анонімізовані/кодовані дослідження (ВІЛ, генетика): видача лише за кодом, обмежений доступ | кодованих досліджень немає | — | — | ✘ | — |
| FR-GAP-005 | Журнал доступу до результатів (хто і коли переглядав), експорт для перевірок / Should / Ча | аудит мутацій є, журналу переглядів немає | — | — | ◐ (C) | — |
| FR-GAP-010 | Виправлений результат після видачі → новий бланк із позначкою «ВИПРАВЛЕНО» (amended report | «ВИПРАВЛЕНО»/amended-бланка немає | — | — | ✘ | — |
| FR-GAP-011 | Анулювання результату (помилковий зразок) із відкликанням бланка та записом у ЕМК / Must / | reject/reopen/annul ЕСОЗ-звіту; відкликання бланка і запис в ЕМК — ні | — | — | ◐ (C) | — |
| FR-GAP-012 | Архів виданих бланків PDF (незмінні копії з хешем), повторний друк копії з позначкою «ДУБЛ | архіву незмінних PDF із хешем немає | — | — | ✘ | — |
| FR-GAP-013 | Коментарі та інтерпретація лікаря до результату, шаблони інтерпретацій (текстові блоки) /  | інтерпретацій лікаря немає (окрім мікробіології) | — | — | ✘ | — |
| FR-GAP-020 | Ланцюг ескалації: якщо лікар не підтвердив критичний результат за N хв → старша медсестра  | журнал дзвінків є; таймерів і ланцюга ескалації немає | — | — | ◐ (C) | — |
| FR-GAP-021 | Довідник критичних значень окремо від референсів (список панічних значень лабораторії, зат | crit у шарах норм; окремого довідника панічних значень немає | — | — | ◐ (C) | — |
| FR-GAP-030 | Зв'язок результату з лотом реагенту, калібратором та контрольним матеріалом, що були актив | зв'язку результат ↔ лот реагенту/калібратор немає | — | — | ✘ | — |
| FR-GAP-031 | Журнал калібрувань аналізаторів (дата, лот калібратора, результат, виконавець) та нагадува | журналу калібрувань немає (згадка лише у причинах розблокування ВКЯ) | — | — | ✘ | — |
| FR-GAP-032 | Журнал технічного обслуговування та інцидентів обладнання (ТО, ремонти, простої), статус « | статус приладу MAINTENANCE враховує маршрутизація; журналу ТО немає | — | — | ◐ (C) | — |
| FR-GAP-033 | Верифікація/валідація методик: записи про валідацію нової методики або лоту реагентів пере | валідації методик немає | — | — | ✘ | — |
| FR-GAP-034 | Невідповідності та коригувальні дії (CAPA): реєстр інцидентів (помилки преаналітики, поруш | протокол lockout ВКЯ; реєстру CAPA немає | — | — | ◐ (C) | — |
| FR-GAP-040 | Компетентність персоналу: допуски співробітника до методик/приладів, дата оцінки компетент | допусків персоналу немає | — | — | ✘ | — |
| FR-GAP-041 | Реєстр СОП (стандартних операційних процедур) із версіями, посиланням із екранів процесів  | реєстру СОП немає | — | — | ✘ | — |
| FR-GAP-042 | Електронні підписи співробітників у протоколах ВКЯ, журналах відділень (ПІБ, посада, час — | виконавець фіксується (ПІБ/час) у протоколах | — | — | ◐ (C) | — |
| FR-GAP-050 | Політика зберігання зразків за підрозділом (термін, температура) з автоматичним списком «д | біобанк, «до утилізації», стадія DISPOSED; політики за підрозділом немає | — | — | ◐ (C) | — |
| FR-GAP-051 | Довідник одиниць вимірювання з коефіцієнтами перерахунку SI ↔ традиційні (ммоль/л ↔ мг/дл) | коефіцієнтів SI↔традиційні немає | — | — | ✘ | — |
| FR-GAP-052 | Синоніми та коди тестів для пошуку (укр./лат./англ., LOINC, НК 025:2021), пошук у матриці  | LOINC є; синоніми/НК 025 — ні | — | — | ◐ (C) | — |
| FR-GAP-053 | Виробничий календар лабораторії (вихідні, зміни) для коректного розрахунку TAT та обіцяног | виробничого календаря немає | — | — | ✘ | — |
| FR-GAP-054 | Дозамовлення тестів на вже взятий зразок (add-on) із перевіркою стабільності біоматеріалу  | додавання тесту є; перевірки стабільності/об'єму немає | — | — | ◐ (C) | — |
| FR-GAP-055 | Виявлення дублікатів призначень (той самий тест пацієнту за добу) з попередженням лікарю / | попередження 24 год лише для направлень МІС | — | — | ◐ (C) | — |
| FR-GAP-060 | Джерело оплати замовлення: пацієнт / договір (страхова, підприємство) / пакет НСЗУ; прайс- | Billing: прайси, платники, поліси, НСЗУ-змішане замовлення | BillingApiTests, PriceCalculatorTests | — | ✔ | — |
| FR-GAP-061 | Звітність НСЗУ: кількість досліджень за пакетами ПМГ, вивантаження у форматі НСЗУ, зв'язок | звітності НСЗУ немає | — | — | ✘ | — |
| FR-GAP-062 | Взаєморозрахунки з пунктами забору-партнерами та референс-лабораторіями (акти, обсяги) / S | акти/рахунки клінік (ClientService) є; з референс-лабораторіями — ні | — | — | ◐ (C) | — |
| FR-GAP-070 | Портал лікаря-замовника (поза МІС): перегляд результатів своїх пацієнтів, сповіщення про к | порталу лікаря-замовника немає | — | — | ✘ | — |
| FR-GAP-071 | Мобільний застосунок/PWA кур'єра: сканування проб при передачі/прийманні, геомітки, темпер | PWA кур'єра немає | — | — | ✘ | — |
| FR-GAP-072 | Імпорт довідника послуг і норм із системи-джерела (ETL за розд. 5.6) як утиліта з попереднім перег | виключено рішенням власника (розд. 20.0) | — | — | виключено | — |
| FR-GAP-073 | Резервне копіювання SQLite/PostgreSQL за розкладом, перевірка відновлення; експорт усіх да | резервного копіювання за розкладом немає (лише бекап SQLite перед перестворенням схеми) | — | — | ✘ | — |
| FR-GAP-074 | Моніторинг: health-метрики API та коннекторів (Prometheus-формат), сповіщення адміністрато | метрик Prometheus і алертів немає (лише /health) | — | — | ✘ | — |
| FR-GAP-075 | Багатофіліальність: кілька лабораторій/філій в одній інсталяції з ізоляцією даних і консол | лише в гілці tenancy | — | — | ✘ | — |
| FR-GAP-076 | Обробка невідповідних повідомлень приладів: екран «Невідповідні результати» з ручною прив' | unmatched results | Full_happy_path (unmatched) | — | ✔ | — |
| FR-GW-001 | `installKey` — 24 символи base32, діє 72 год, одноразовий; після реєстрації `status=ACTIVE | POST /connector/register, X-MedLink-ApiKey | Full_happy_path (register, 401 без ключа) | lab.an.* | ✔ | — |
| FR-GW-002 | `apiKey` генерується сервером (32 байти, base64url), зберігається лише як SHA-256 хеш; від | POST /connector/register, X-MedLink-ApiKey | Full_happy_path (register, 401 без ключа) | lab.an.* | ✔ | — |
| FR-GW-003 | Статуси коннектора: `PENDING` (створено, не зареєстровано), `ACTIVE` (heartbeat  3×`heartb | ConnectorService/AnalyzerService | — | lab.an.* | ◐ (T) | — |
| FR-GW-004 | Зміна будь-якого аналізатора коннектора інкрементує `configVersion`; коннектор підхоплює ч | ConnectorService/AnalyzerService | — | lab.an.* | ◐ (T) | — |
| FR-GW-005 | Журнал обміну `LabAnalyzerMessage`: напрямок, протокол, сирий текст (керівні символи як `` | ConnectorService/AnalyzerService | — | lab.an.* | ◐ (T) | — |
| FR-GW-006 | Симулятор `POST /analyzers/{id}/simulate {rawMessage}` — розбір тим самим парсером, без зб | ConnectorService/AnalyzerService | — | lab.an.* | ◐ (T) | — |
| FR-GW-007 | `GET /analyzers/{id}/order-preview/{barcode}` показує текст замовлення у форматі приладу з | GET /analyzers/{id}/order-preview | OrderBuilderTests (unit) | lab.an.* | ✔ | — |
| FR-GW-008 | Незв'язані результати (`lab_unmatched_results`): штрихкод не знайдено або код показника не | /results/unmatched | Full_happy_path (unmatched містить 99999999) | lab.an.* | ✔ | — |
| FR-GW-009 | Хедер усіх сторінок: бейдж «Шлюз аналізаторів: Онлайн (N/M)» = аналізатори online / активн | ConnectorService/AnalyzerService | — | lab.an.* | ◐ (T) | — |
| FR-GW-010 | ZIP завантаження містить: `appsettings.json` з `Server.BaseUrl`, `InstallKey`, `ConnectorN | ConnectorService/AnalyzerService | — | lab.an.* | ◐ (T) | — |
| FR-GW-011 | Логи коннектора `POST /connector/logs` зберігаються у `lab_connector_logs` (рівень, повідо | ConnectorService/AnalyzerService | — | lab.an.* | ◐ (T) | — |
| FR-IMP-001 | `POST /import/results` multipart: CSV (роздільник `;` або `,`, UTF-8/Windows-1251 автовизн | ImportService (CSV/XLSX/XML, dryRun) | — (жодного тесту; S-14 без тесту) | lab.im.* | ◐ (T) | — |
| FR-IMP-002 | Обов'язкові колонки: `barcode`, `code` (код тесту або код приладу), `value`; опційні `unit | ImportService (CSV/XLSX/XML, dryRun) | — (жодного тесту; S-14 без тесту) | lab.im.* | ◐ (T) | — |
| FR-IMP-003 | `dryRun=true` → таблиця рядків зі статусом `OK / WARN / ERROR` і причиною; застосування —  | ImportService (CSV/XLSX/XML, dryRun) | — (жодного тесту; S-14 без тесту) | lab.im.* | ◐ (T) | — |
| FR-IMP-004 | Ліміт 5 000 рядків за файл; результат імпорту пишеться в аудит одним записом + по рядках в | ліміт 5000 рядків у коді не виявлено | — | lab.im.* | ◐ (C) | — |
| FR-LOG-001 | Коридор холодового ланцюга +2,0…+8,0 °C; `temperatureDispatch` або `temperatureReceipt` по | LogisticsService (+2…+8 °C) | Full_happy_path (12,0 °C → isColdChainViolated) | lab.logistics.*, lab.containers.* | ✔ | — |
| FR-LOG-002 | Порушення холодового ланцюга не блокує прийом, але додає попередження до кожної проби і до | sampleWarnings | — | lab.logistics.*, lab.containers.* | ◐ (T) | — |
| FR-LOG-003 | Відхилена проба з `createRepeatOrder=true` створює нове замовлення з тими самими тестами л | SampleService.RejectAsync(createRepeatOrder) | — (S-03: повтор не тестується) | lab.logistics.*, lab.containers.* | ◐ (T) | — |
| FR-LOG-004 | Причини браку (довідник): гемоліз, ліпемія, іктеричність, згусток, недостатній об'єм, пору | довідник причин браку | — | lab.logistics.*, lab.containers.* | ◐ (T) | — |
| FR-LOG-005 | Прийом фіксує `receivedAt`, `receivedById`; TAT-етап «логістика» = `receivedAt − collected | receivedAt/receivedById | — | lab.logistics.*, lab.containers.* | ◐ (T) | — |
| FR-LOG-006 | Маніфест із `status=RECEIVED` не редагується | RECEIVED не редагується | — | lab.logistics.*, lab.containers.* | ◐ (T) | — |
| FR-LOG-101 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-102 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-103 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-104 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-105 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-106 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-107 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-108 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-109 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-110 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-111 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-112 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-113 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-114 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-LOG-115 | Транспортні контейнери (термобокси, сумки): типи тари, укладка по місцях, запечатування, п | LogisticsService.Containers, TransportContainersController | TransportContainerApiTests S130–S135 | lab.containers.* | ✔ | — |
| FR-MIC-001 | Інтерпретація за зоною: `zone ≥ zone_susceptible_ge → S`; `zone < zone_resistant_lt → R`;  | EucastInterpreter | QcAndEucastTests: Zone_interpretation, Mic_interpretation, No_breakpoint_data_gives_empty, Intrinsic_resistance… | lab.mb.* | ✔ | — |
| FR-MIC-002 | Інтерпретація за МІК: `mic ≤ mic_susceptible_le → S`; `mic > mic_resistant_gt → R`; інакше | EucastInterpreter | QcAndEucastTests: Zone_interpretation, Mic_interpretation, No_breakpoint_data_gives_empty, Intrinsic_resistance… | lab.mb.* | ✔ | — |
| FR-MIC-003 | Якщо breakpoint для пари (організм, антибіотик, версія) відсутній — інтерпретація `NA` з п | EucastInterpreter | QcAndEucastTests: Zone_interpretation, Mic_interpretation, No_breakpoint_data_gives_empty, Intrinsic_resistance… | lab.mb.* | ✔ | — |
| FR-MIC-004 | Версія EUCAST — атрибут breakpoint (`v14.0_2024` у сіді); при інтерпретації використовуєть | EucastInterpreter | QcAndEucastTests: Zone_interpretation, Mic_interpretation, No_breakpoint_data_gives_empty, Intrinsic_resistance… | lab.mb.* | ✔ | — |
| FR-MIC-005 | Фенотипи (**правила детекції, пропозиція команди на базі v3**): MRSA — *S. aureus* з R до  | EucastInterpreter (MRSA/ESBL/CRE/VRE) | QcAndEucastTests: Mrsa/Esbl/Cre/Vre | lab.mb.* | ✔ | — |
| FR-MIC-006 | Статуси посіву: `INCUBATING → PRELIMINARY → ISOLATED → COMPLETED`; попередній звіт доступн | MicrobiologyService | — (API потоку немає) | lab.mb.* | ◐ (T) | — |
| FR-MIC-007 | Override інтерпретації лікарем — обов'язкова причина, зберігається `overriddenById` | MicrobiologyService | — (API потоку немає) | lab.mb.* | ◐ (T) | — |
| FR-MIC-008 | Бланк мікробіології: зразок, локус, мікрофлора, титр, морфологія, фенотип, таблиця антибіо | MicrobiologyService | — (API потоку немає) | lab.mb.* | ◐ (T) | — |
| FR-MIS-001 | ЛІС тримає дзеркало словника інтервенцій ЕСОЗ у `lab_ehealth_intervention` [MedLink]: `cod | MisCatalogService, словник інтервенцій | MisIntegrationApiTests.Service_matrix… | lab.mis.*, lab.incoming.* | ✔ | словник — демо DEMO-…, синхронізація — MOCK |
| FR-MIS-002 | Джерело: у складі МІС — довідники/каталог послуг evomis (`ehe_service_catalog_service` та  | MisCatalogService, словник інтервенцій | MisIntegrationApiTests.Service_matrix… | lab.mis.*, lab.incoming.* | ✔ | словник — демо DEMO-…, синхронізація — MOCK |
| FR-MIS-003 | Синхронізація за розкладом (`Lab:Intervention:SyncCron`, типово щодоби) і кнопкою «Оновити | MisCatalogService, словник інтервенцій | MisIntegrationApiTests.Service_matrix… | lab.mis.*, lab.incoming.* | ✔ | словник — демо DEMO-…, синхронізація — MOCK |
| FR-MIS-004 | До ЛІС потрапляють лише інтервенції лабораторної категорії; фільтр категорії налаштовуєтьс | MisCatalogService, словник інтервенцій | MisIntegrationApiTests.Service_matrix… | lab.mis.*, lab.incoming.* | ✔ | словник — демо DEMO-…, синхронізація — MOCK |
| FR-MIS-005 | Сторінка «Інтервенції ЕСОЗ»: пошук за кодом/назвою, фільтри «Співставлено / Не співставлен | MisCatalogService, словник інтервенцій | MisIntegrationApiTests.Service_matrix… | lab.mis.*, lab.incoming.* | ✔ | словник — демо DEMO-…, синхронізація — MOCK |
| FR-MIS-006 | Кожна синхронізація пишеться в `lab_ehealth_exchange_log` (розд. 20.6.3) і в аудит | MisCatalogService, словник інтервенцій | MisIntegrationApiTests.Service_matrix… | lab.mis.*, lab.incoming.* | ✔ | словник — демо DEMO-…, синхронізація — MOCK |
| FR-MIS-010 | Довідник `lab_intervention_map` [ЛІС]: інтервенція → послуга ЛІС (профіль) або набір показ | /intervention-map/* | MisIntegrationApiTests (coverage/readiness частково) | lab.mis.*, lab.incoming.* | ✔ | — |
| FR-MIS-011 | Режим вибору показників на співставленні (`indicator_selection_mode`): `FIXED` — виконують | /intervention-map/* | MisIntegrationApiTests (coverage/readiness частково) | lab.mis.*, lab.incoming.* | ✔ | — |
| FR-MIS-012 | Обов'язкові показники задаються ознакою `is_required` у складі послуги (`lab_test_profile_ | /intervention-map/* | MisIntegrationApiTests (coverage/readiness частково) | lab.mis.*, lab.incoming.* | ✔ | — |
| FR-MIS-013 | Автопідказка співставлення: за збігом LOINC, коду НК 025:2021, подібністю назви; результат | /intervention-map/* | MisIntegrationApiTests (coverage/readiness частково) | lab.mis.*, lab.incoming.* | ✔ | — |
| FR-MIS-014 | **Готовність до маршрутизації.** Співставлення має індикатор: «Не співставлено» → «Співста | /intervention-map/* | MisIntegrationApiTests (coverage/readiness частково) | lab.mis.*, lab.incoming.* | ✔ | — |
| FR-MIS-015 | Перевірка при збереженні мапінгу: послуга активна; усі `is_required` показники існують; є  | /intervention-map/* | MisIntegrationApiTests (coverage/readiness частково) | lab.mis.*, lab.incoming.* | ✔ | — |
| FR-MIS-016 | Звіт покриття (T-421): інтервенції без співставлення; співставлені без обладнання; прилади | /intervention-map/* | MisIntegrationApiTests (coverage/readiness частково) | lab.mis.*, lab.incoming.* | ✔ | — |
| FR-MIS-020 | **Зовнішнє.** Пошук е-направлення (`GET /referrals/ehealth/lookup`) додатково повертає: ко | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-021 | Групове е-направлення (кілька `ServiceRequest`) — одне замовлення, по одній позиції `lab_o | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-022 | **Показники не передані** (е-направлення іншого закладу, старий МІС): ЛІС відкриває діалог | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-023 | **Інтервенція не співставлена.** Замовлення не блокується: реєстратор обирає послугу вручн | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-024 | **Внутрішнє.** `POST /mis/referrals` приймає призначення: пацієнт, госпіталізація, відділе | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-025 | Скасування призначення в МІС (`POST /mis/referrals/{id}/cancel`) скасовує тести у статусі  | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-026 | Зворотні події в МІС: `COLLECTED`, `RECEIVED`, `RESULT_READY` (по позиції), `COMPLETED`, ` | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-027 | Критичні значення стаціонарних пацієнтів (розд. 19.3, FR-VAL) показуються в черзі відділен | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-028 | (Could, R2) Планові та повторювані призначення (щодня о 06:00 протягом N діб): одне призна | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-029 | Дублювання: ідентична позиція (пацієнт + інтервенція + показники) у нескасованому замовлен | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-030 | Після вибору інтервенції відкривається вікно «Показники послуги»: перелік показників співс | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-031 | Мінімум один показник; для `FIXED` вікно не показується | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-032 | Залежні показники: розрахункові (напр. MCH) автоматично додають складові (HGB, RBC) із під | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-033 | Живий підсумок у вікні: кількість показників, пробірки (за планувальником розд. 20.1 лише  | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-034 | Вартість: якщо обрано всі показники послуги — ціна послуги (`lab_test_profile.price`); якщ | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-MIS-035 | Зміна складу до приймання проби: додавання — новий тест із маршрутизацією; зняття — тест ` | POST /mis/referrals, MisReferralService | MisIntegrationApiTests (S-22, 25–28) | lab.mis.*, lab.incoming.* | ✔ | події МІС лише в lab_mis_event_log, доставки назовні немає |
| FR-NRM-001 | Шар норми (`LabReferenceLayer`) належить тесту і (опційно) методиці; `layerType ∈ {BASELIN | NormsCascadeResolver | NormsCascadeResolverTests (13) | lab.nm.* | ✔ | — |
| FR-NRM-002 | Умова шару збігається, якщо виконані всі увімкнені фільтри: `isGender → gender==ctx.gender | NormsCascadeResolver | NormsCascadeResolverTests (13) | lab.nm.* | ✔ | — |
| FR-NRM-003 | Резолвер: кандидати = активні шари тесту з `methodCode == ctx.methodCode` або без методики | NormsCascadeResolver | NormsCascadeResolverTests (13) | lab.nm.* | ✔ | — |
| FR-NRM-004 | `auditTrace[]` містить для кожного перевіреного шару: пріоритет, тип, назву, `isMatched`,  | auditTrace | — (trace не асертиться) | lab.nm.* | ◐ (T) | — |
| FR-NRM-005 | Валідація шару: `normLow < normHigh`, `critLow ≤ normLow`, `critHigh ≥ normHigh` (якщо зад | ContextDictionariesController /norms/*, DictionaryService | — (400 на normLow>normHigh не тестується) | lab.nm.* | ◐ (T) | — |
| FR-NRM-006 | Видалення тесту заборонене за наявності шарів, результатів або профілів (409 з переліком з | ContextDictionariesController /norms/*, DictionaryService | — (400 на normLow>normHigh не тестується) | lab.nm.* | ◐ (T) | — |
| FR-NRM-007 | Reflex-правило: `triggerTestCode`, оператор `,>=,==,OUT_OF_RANGE,CRITICAL`, поріг, `reflex | ReflexRuleEngine | RulesEngineTests.Tsh_above_threshold_triggers_ft4… | lab.nm.* | ✔ | — |
| FR-NRM-008 | Код тесту: regex `^[A-Z0-9_\-]{1,32}$`, унікальний; зміна `resultType` тесту, що має резул | ContextDictionariesController /norms/*, DictionaryService | — (400 на normLow>normHigh не тестується) | lab.nm.* | ◐ (T) | — |
| FR-NRM-009 | Імпорт довідників `POST /dictionaries/import?dryRun=true` показує таблицю змін (створити/о | ContextDictionariesController /norms/*, DictionaryService | — (400 на normLow>normHigh не тестується) | lab.nm.* | ◐ (T) | — |
| FR-NRM-010 | НОВЕКартка послуги отримує вкладки «Інтервенції ЕСОЗ» (з якими інтервенціями співставлено  | MisCatalogService.ServiceCard | MisIntegrationApiTests.Service_matrix_card… | lab.nm.* | ✔ | — |
| FR-PAT-001 | Сторінка «Прийом матеріалу»: черга направлень типу PATHOLOGY з усіх джерел (стаціонар, пол | PathologyService.Accept/Reject | PathologyApiTests.S40/S41 | lab.path.* | ✔ | — |
| FR-PAT-002 | Дані лицьової сторони 014/о (п. 1–9) відображаються лише для перегляду. Уточнення — запит  | PathologyService | S40 (частково) | lab.path.* | ◐ (T) | — |
| FR-PAT-003 | Кожен контейнер сканується. Прийом можливий лише коли відскановано всі контейнери, кількіс | PathologyService.Accept/Reject | PathologyApiTests.S40/S41 | lab.path.* | ✔ | — |
| FR-PAT-004 | Контроль фіксації: показ тривалості від `fixStart` (передає МІС). Попередження: &lt; 6 год | PathologyService | S40 (частково) | lab.path.* | ◐ (T) | — |
| FR-PAT-005 | Відмова в прийомі з причиною з довідника (22.10) і коментарем. МІС отримує `REJECTED`. Е-н | PathologyService.Accept/Reject | PathologyApiTests.S40/S41 | lab.path.* | ✔ | — |
| FR-PAT-006 | При прийомі: присвоєння № справи, друк етикеток, тип матеріалу (п. 12–14: біопсія діагност | PathologyService | S40 (частково) | lab.path.* | ◐ (T) | — |
| FR-PAT-007 | Е-направлення ЕСОЗ: пошук і `use` за розд. 20.6.2; інтервенція зіставляється з послугою «П | PathologyService | S40 (частково) | lab.path.* | ◐ (T) | — |
| FR-PAT-008 | Консультація готових препаратів: прийом зовнішніх скелець і блоків з реквізитами закладу-в | консультація зовнішніх скелець | S48 | lab.path.* | ✔ | — |
| FR-PAT-009 | Перехідний період SMED-8714: направлення на групи 21729/21730 потрапляють у ЛІС. Створення | заявлено «Не реалізовано» у розд. 22.17 | — | lab.path.* | ✘ | — |
| FR-PAT-010 | Автозв’язок з попередніми справами пацієнта (п. 4–5 форми): якщо МІС позначила «вторинна», | PathologyService | S40 (частково) | lab.path.* | ◐ (T) | — |
| FR-PAT-020 | Вирізка: макроопис за шаблоном органу (довідник 22.10) з вільним редагуванням, фото макроп | gross/blocks/slides, ціна | S42 | lab.path.* | ✔ | — |
| FR-PAT-021 | Формування блоків для кожного об’єкта: ідентифікатор, кількість шматочків. Підсумок «шмато | gross/blocks/slides, ціна | S42 | lab.path.* | ✔ | — |
| FR-PAT-022 | Завершення вирізки створює для кожного блока скельце Г-Е і перераховує вартість: перший бл | gross/blocks/slides, ціна | S42 | lab.path.* | ✔ | — |
| FR-PAT-023 | Етапи Проводка → Заливка → Мікротомія → Фарбування фіксуються скануванням касет або скелец | scan стадій | S110 | lab.path.* | ✔ | — |
| FR-PAT-024 | Черги етапів: окремий лічильник на кожен етап; CITO і прострочені — вгорі | лічильники черг (UI) | — | lab.path.* | ◐ (T) | — |
| FR-PAT-025 | Додаткові забарвлення та ІГХ на наявних блоках: вибір блока й методик. Створюються скельця | reflex ІГХ, additional | S43 | lab.path.* | ✔ | — |
| FR-PAT-026 | Дозамовлення ІГХ може надійти з МДК (ТЗ МДК, FR-7.6) або з МІС. Воно прив’язується до наяв | reflex ІГХ, additional | S43 | lab.path.* | ✔ | — |
| FR-PAT-027 | Антитіла та реагенти списуються зі складу (розд. 4.9) з лотом і строком придатності | заявлено «Не реалізовано» у розд. 22.17 | — | lab.path.* | ✘ | — |
| FR-PAT-028 | (Could, R2) Цифрова патологія: прикріплення зображень скелець (WSI) і посилання на перегля | заявлено «Не реалізовано» у розд. 22.17 | — | lab.path.* | ✘ | — |
| FR-PAT-029 | Направлення блоків у зовнішню лабораторію (молекулярні дослідження, референс): через send- | заявлено «Не реалізовано» у розд. 22.17 | — | lab.path.* | ✘ | — |
| FR-PAT-040 | Мікроопис із шаблоном органу. Структурований висновок: МКХ-10, МКХ-О топографія, МКХ-О мор | report/sign/addendum | S44_S45, Sign_requires_micro_conclusion… | lab.path.* | ✔ | КЕП — Mock |
| FR-PAT-041 | Текст п. 18 збирається кнопкою зі структурованих полів і редагується. Для підпису обов’язк | report/sign/addendum | S44_S45, Sign_requires_micro_conclusion… | lab.path.* | ✔ | КЕП — Mock |
| FR-PAT-042 | Reflex-правила ІГХ (розширення розд. 4.13): умова на МКХ-О → пропонована панель. Приклад:  | reflex ІГХ, additional | S43 | lab.path.* | ✔ | — |
| FR-PAT-043 | Попередній висновок передається в МІС подією `RESULT_PRELIMINARY` з позначкою «Попередній» | RESULT_PRELIMINARY у lab_mis_event_log | S40 (нотифікація МІС частково) | lab.path.* | ◐ (T) | події МІС без доставки |
| FR-PAT-044 | Підпис КЕП лікарем-патологоанатомом (розд. 21.13, віджет підпису). Справа → `RELEASED`, по | report/sign/addendum | S44_S45, Sign_requires_micro_conclusion… | lab.path.* | ✔ | КЕП — Mock |
| FR-PAT-045 | Після підпису висновок незмінний. Зміни — лише доповнення: тип (ІГХ/молекулярні, перегляд, | report/sign/addendum | S44_S45, Sign_requires_micro_conclusion… | lab.path.* | ✔ | КЕП — Mock |
| FR-PAT-046 | Подвійний підпис (Should): для визначених типів (наприклад, перша злоякісна знахідка, конс | подвійний підпис — R2 (T-442) | — | lab.path.* | ✘ | — |
| FR-PAT-047 | Неочікувана злоякісність у матеріалі без онкологічного клінічного діагнозу — критична знах | report/sign/addendum | S44_S45, Sign_requires_micro_conclusion… | lab.path.* | ✔ | КЕП — Mock |
| FR-PAT-048 | Видача пацієнту: друк 014/о обох сторін (22.8). Через кабінет пацієнта (розд. 4.7) — лише  | друк 014/о є; видача через кабінет пацієнта — ні | S47 | lab.path.* | ◐ (C) | — |
| FR-PAT-060 | Тип CITO приходить із протоколу операції (вид дослідження «Термінове інтраопераційне»). Сп | CITO-таймер, cito-notice (read-back) | S46 | lab.path.* | ✔ | — |
| FR-PAT-061 | Таймер від надходження. Цільовий час — 20 хв (`Lab:Pathology:CitoTargetMinutes`), після нь | CITO-таймер, cito-notice (read-back) | S46 | lab.path.* | ✔ | — |
| FR-PAT-062 | «Повідомити результат в операційну»: текст попереднього висновку, кому повідомлено, спосіб | CITO-таймер, cito-notice (read-back) | S46 | lab.path.* | ✔ | — |
| FR-PAT-063 | Після інтраопераційного висновку матеріал проходить плановий цикл на парафіні в тій самій  | не заявлено окремо в 22.17 | — | lab.path.* | ◐ (T) | — |
| FR-PAT-070 | Місце зберігання блоків і скелець (шафа / полиця / комірка) на базі моделі біобанку (розд. | архів, issue/return | Archive_issue_blocks_additional_studies_until_return; S114 | lab.path.* | ✔ | — |
| FR-PAT-071 | Видача: отримувач (ПІБ / заклад), документ, мета (консультація, друга думка, молекулярне д | архів, issue/return | Archive_issue_blocks_additional_studies_until_return; S114 | lab.path.* | ✔ | — |
| FR-PAT-072 | Прострочене повернення — задача завідувачу. Видані блоки недоступні для дозамовлення ІГХ ( | архів, issue/return | Archive_issue_blocks_additional_studies_until_return; S114 | lab.path.* | ✔ | — |
| FR-PAT-080 | Друк обох сторін форми 014/о (наказ МОЗ № 435 від 29.05.2013), A4, альбомна орієнтація, ст | form014(.pdf) | S47 | lab.path.* | ✔ | — |
| FR-PAT-081 | Сторона 1 (п. 1–9) — дані з МІС без змін. Сторона 2: п. 10 № справи; п. 11 дата й час прий | form014(.pdf) | S47 | lab.path.* | ✔ | — |
| FR-PAT-082 | До підпису п. 18 і підписи друкуються порожніми. Довгий текст переноситься на додаткову ст | form014(.pdf) | S47 | lab.path.* | ✔ | — |
| FR-PAT-083 | Режим «Підсвітити автозаповнення» (для перевірки): поля з даними системи виділяються, при  | UI-підсвічування | — | lab.path.* | ◐ (T) | — |
| FR-PAT-090 | SLA за типом (робочі дні): біопсія 5, операційний матеріал 7, консультація 5, ІГХ +3; CITO | SLA за типом | S43 (SLA +3 дні) | lab.path.* | ✔ | — |
| FR-PAT-091 | TAT за етапами (розд. 4.11): надходження → вирізка → скельця → висновок → видача; медіана  | аналітика якості 22.9 — R2 (T-444) | — | lab.path.* | ✘ | — |
| FR-PAT-092 | Показники якості: частка відмов у прийомі за причинами; розбіжність терміновий/плановий; ч | аналітика якості 22.9 — R2 (T-444) | — | lab.path.* | ✘ | — |
| FR-PAT-093 | Навантаження: справи, блоки, скельця на лікаря й лаборанта за період | аналітика якості 22.9 — R2 (T-444) | — | lab.path.* | ✘ | — |
| FR-PAT-100 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | /pathology/dictionaries | Demo_data_has_six_cases…; S122 | lab.path.* | ✔ | — |
| FR-PAT-101 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | /pathology/dictionaries | Demo_data_has_six_cases…; S122 | lab.path.* | ✔ | — |
| FR-PAT-102 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | /pathology/dictionaries | Demo_data_has_six_cases…; S122 | lab.path.* | ✔ | — |
| FR-PAT-103 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | /pathology/dictionaries | Demo_data_has_six_cases…; S122 | lab.path.* | ✔ | — |
| FR-PAT-104 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | /pathology/dictionaries | Demo_data_has_six_cases…; S122 | lab.path.* | ✔ | — |
| FR-PAT-105 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | /pathology/dictionaries | Demo_data_has_six_cases…; S122 | lab.path.* | ✔ | — |
| FR-PAT-106 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | /pathology/dictionaries | Demo_data_has_six_cases…; S122 | lab.path.* | ✔ | — |
| FR-PAT-110 | **Маршрут блока.** Під час вирізки кожен блок має маршрут `STANDARD` (проводка → заливка → | PathologyService | S110/S114 (PathologyApiTests) | lab.path.* | ✔ | розсилка (119): черга QUEUED без відправника |
| FR-PAT-111 | **Виконавці.** Завершити вирізку без лікаря-патолога та асистента, який провів вирізку, не | PathologyService | S110/S114 (PathologyApiTests) | lab.path.* | ✔ | розсилка (119): черга QUEUED без відправника |
| FR-PAT-112 | **Мікротомія.** Під час етапу «Мікротомія» вказується кількість зрізів/скелець; значення з | PathologyService | S110/S114 (PathologyApiTests) | lab.path.* | ✔ | розсилка (119): черга QUEUED без відправника |
| FR-PAT-113 | **Методики на блок.** Для кожного блока під час вирізки обирається до 5 методик із довідни | PathologyService | S110/S114 (PathologyApiTests) | lab.path.* | ✔ | розсилка (119): черга QUEUED без відправника |
| FR-PAT-114 | **Покриття скла.** Після фарбування скельце може бути позначене як покрите покривним склом | PathologyService | S110/S114 (PathologyApiTests) | lab.path.* | ✔ | розсилка (119): черга QUEUED без відправника |
| FR-PAT-115 | **Пристрій.** Сканування касети/скельця може вказувати пристрій (гістопроцесор, мікротом,  | поля пристрою при скануванні не виявлено | — | lab.path.* | ✘ | — |
| FR-PAT-116 | **Залишок матеріалу.** Для кожного об'єкта після вирізки фіксується стан залишку: немає /  | PathologyService | S110/S114 (PathologyApiTests) | lab.path.* | ✔ | розсилка (119): черга QUEUED без відправника |
| FR-PAT-117 | **Зовнішня консультація.** До підписаного висновку додається доповнення типу «Висновок зов | PathologyService | S110/S114 (PathologyApiTests) | lab.path.* | ✔ | розсилка (119): черга QUEUED без відправника |
| FR-PAT-118 | **Архів і препарати.** (а) Блок має приміщення архіву (довідник) і місце всередині нього ( | PathologyService | S110/S114 (PathologyApiTests) | lab.path.* | ✔ | розсилка (119): черга QUEUED без відправника |
| FR-PAT-119 | **Розсилка висновку.** Для підписаної справи можна поставити висновок у чергу розсилки еле | PathologyService | S110/S114 (PathologyApiTests) | lab.path.* | ✔ | розсилка (119): черга QUEUED без відправника |
| FR-PAT-120 | **Гінекологічний анамнез.** Направлення може містити день менструального циклу (1–60), дат | цитологічний анамнез | S120 | lab.path.* | ✔ | — |
| FR-PHL-001 | Порядок пробірок у списку — за `tubeType.orderOfDrawIndex` (CLSI): гемокультура → цитрат → | TubePlanner (Order of Draw) | TubePlannerTests.Same_biomaterial_and_tube_are_merged_and_sorted_by_order_of_draw | lab.phlebotomy.* | ✔ | — |
| FR-PHL-002 | Чекліст обов'язковий: `idVerified=true` обов'язково; інші пункти — попередження | SampleService.CollectAsync (idVerified) | Full_happy_path (позитив); негатив idVerified=false не тестується | lab.phlebotomy.* | ◐ (T) | — |
| FR-PHL-003 | Етикетка 40×25 мм: Code128 зі штрихкодом (`^BCN,60,Y,N,N`), ПІБ скорочено + вік, тип пробі | ZplLabelBuilder; GET /samples/{b}/label | BarcodeTests.Zpl_label_contains_code128_and_fields (unit) | lab.phlebotomy.* | ✔ | — |
| FR-PHL-004 | `collectedAt` = час сканування (сервер, UTC), `collectedById` = поточний співробітник | collect | Full_happy_path | lab.phlebotomy.* | ✔ | — |
| FR-PHL-010 | Генерація штрихкоду: `n = counter++`; `s = right("00000000"+n, 7) + "0"`; якщо `s[0]=='0'` | TubeBarcodeGenerator | BarcodeTests | lab.phlebotomy.* | ✔ | — |
| FR-PHL-011 | Штрихкод одразу прив'язується до проби; повторний друк не змінює код | TubeBarcodeGenerator | BarcodeTests | lab.phlebotomy.* | ✔ | — |
| FR-PKG-001 | Довідник пакетів ведеться в МІС (код, назва, контур застосування, перелік послуг з типовим | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PKG-002 | Лікар може змінити склад пакета під конкретного пацієнта: прибрати послугу, додати послугу | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PKG-003 | Пакет надходить **одним направленням** (`POST /mis/referrals` з блоком `package{code,name, | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PKG-004 | CITO-пакет ставить CITO на всі позиції; розподіл віддає перевагу приладам з меншим TAT (FR | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PKG-005 | Для зовнішнього контуру (е-направлення) пакет передається в ЕСОЗ як групове направлення —  | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PKG-006 | Якщо частину пакета виконати неможливо (немає обладнання), ЛІС приймає решту й повертає МІ | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PKG-007 | Тарифікація: якщо в прайсі є ціна пакета (`lab_price_package`, розд. 20.7) і склад не змін | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PKG-008 | Повторювані пакети ВРІТ («кожні 12 год») — через планові призначення FR-MIS-028 (R2): кожн | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PKG-009 | Код пакета показується в черзі приладу, картці замовлення, розрізах 23.5 і є виміром звіті | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PKG-010 | Результати пакета повертаються в МІС по кожній послузі (подія `RESULT_READY`), а подія `CO | /mis/catalog/packages | Demo_inbox_shows_package_cito…; Cito_routing… | lab.mis.packages|package-referral | ✔ | — |
| FR-PORT-001 | Пацієнт бачить значення лише після `RELEASED`; до того — лише статус і етапи | PortalController | Full_happy_path перевіряє лише стан ПІСЛЯ release | — (сценаріїв порталу немає) | ◐ (T) | — |
| FR-PORT-002 | Портал повертає лише дані свого `patientId`; запит чужого замовлення → 404 (не 403, щоб не | фільтр PatientId + 404 | — (чужий patientId не тестується) | — (сценаріїв порталу немає) | ◐ (T) | немає автентифікації пацієнта |
| FR-PORT-003 | Тренд будується для ≥ 3 точок; при 2 — текстове порівняння «↑ вище попереднього на X %»; п | trend endpoint, UI | — | — (сценаріїв порталу немає) | ◐ (T) | — |
| FR-PORT-004 | Межі норми на графіку — ті, що були застосовані для кожної точки (`normLow/normHigh` із ре | trend endpoint, UI | — | — (сценаріїв порталу немає) | ◐ (T) | — |
| FR-PORT-005 | Кольори точок повторюють схему ЛІС (зелений/жовтий/червоний) | trend endpoint, UI | — | — (сценаріїв порталу немає) | ◐ (T) | — |
| FR-PORT-006 | PDF-бланк ідентичний бланку лабораторії (той самий шаблон і QR) | GET /portal/.../report.pdf | — | — (сценаріїв порталу немає) | ◐ (T) | — |
| FR-PORT-007 | Сповіщення про готовність створюється при `release` із каналом за даними пацієнта (телефон | LabPatientNotification Status=QUEUED | NotificationDeliveryTests | lab.notify.* | ✔ | Канал не налаштовано → статус «Не налаштовано», не імітація; див. розд. 26 ТЗ |
| FR-PORT-008 | Мобільна верстка: одна колонка до 600 px, степер вертикальний | trend endpoint, UI | — | — (сценаріїв порталу немає) | ◐ (T) | — |
| FR-PORT-020 | Для кожного `LabOrderTest` ведеться `released_at`; заповнюється автоматично при переході р | ProgressiveReleaseService.ReleaseVerifiedTestAsync | Full_happy_path (releasedAt) | — (сценаріїв порталу немає) | ✔ | — |
| FR-PORT-021 | Замовлення, у якого частина тестів видана, має статус `PARTIALLY_COMPLETED`; `COMPLETED` — | статуси/прогрес | Full_happy_path (progress 100%) | — (сценаріїв порталу немає) | ◐ (T) | — |
| FR-PORT-022 | Кабінет пацієнта показує лише видані тести, лічильник «готово N з M», для невиданих — «очі | статуси/прогрес | Full_happy_path (progress 100%) | — (сценаріїв порталу немає) | ◐ (T) | — |
| FR-PORT-023 | Сповіщення пацієнту формуються пакетно: не частіше одного на замовлення за 10 хвилин, із п | NotificationDebounce 10 хв | NotificationDeliveryTests | lab.notify.* | ✔ | Канал не налаштовано → статус «Не налаштовано», не імітація; див. розд. 26 ТЗ |
| FR-PORT-024 | Лікар-замовник бачить у картці замовлення ту саму поетапну картину з позначкою, який підро | статуси/прогрес | Full_happy_path (progress 100%) | — (сценаріїв порталу немає) | ◐ (T) | — |
| FR-PRE-001 | Довідник пунктів забору: код, назва, адреса, графік, відповідальна медсестра, кур'єрський  | пункт забору = org_department; графіка/маршруту/типового набору тари немає | — | lab.orders.tube-plan | ◐ (C) | — |
| FR-PRE-002 | Біоматеріал: локалізація забору, мінімальний об'єм, інструкція підготовки пацієнта (натщес | частково поля біоматеріалу/тари | — | lab.orders.tube-plan | ◐ (C) | — |
| FR-PRE-003 | Тара: виробник/артикул, мінімальний і номінальний об'єм, інверсії, педіатрична альтернатив | частково поля біоматеріалу/тари | — | lab.orders.tube-plan | ◐ (C) | — |
| FR-PRE-004 | Правила об'єднання: `requiresSeparateTube` (напр., посів крові, глюкоза у фториді), `maxTe | TubePlanner | TubePlannerTests (8), TubePlanApiTests (5) | lab.orders.tube-plan | ✔ | — |
| FR-PRE-005 | Режим попередньо роздрукованих штрихкодів: сканування рулонного коду → прив'язка до пробір | режиму попередньо роздрукованих рулонних штрихкодів (external_barcode) немає | — | lab.orders.tube-plan | ✘ | — |
| FR-PRE-006 | Налаштування схеми ідентифікації: «унікальний на пробірку» (типово) або «штрихкод замовлен | немає | — | lab.orders.tube-plan | ✘ | — |
| FR-PRE-007 | Маршрут кур'єра: контрольні точки сканування, час у дорозі, звіт порушень; статистика відб | немає | — | lab.orders.tube-plan | ✘ | — |
| FR-PRN-001 | Бланк результатів A4 (HTML → PDF): шапка з реквізитами й логотипом, № замовлення, дати заб | ReportService, QuestPDF/HTML fallback | Full_happy_path (HTML+PDF) | lab.orders.card-report | ✔ | — |
| FR-PRN-002 | Мова бланка обирається окремо (uk за замовчуванням; en — назви тестів із `shortName`/LOINC | бланк лише uk (html lang="uk"), перемикача en немає | — | lab.orders.card-report | ✘ | — |
| FR-PRN-003 | Етикетка 40×25 мм ZPL: `^XA^FO50,30^BY2^BCN,60,Y,N,N^FD{barcode}^FS^FO50,110^A0N,24,24^FD{ | ZplLabelBuilder | BarcodeTests.Zpl_label_contains_code128_and_fields | lab.orders.card-report | ✔ | — |
| FR-PRN-004 | Робочий лист батчу A4: номер, аналізатор, дата, таблиця (№, штрихкод, пацієнт, тести, поле | worklist batch print, протокол розблокування | Qc_1_3s (текст протоколу) | lab.orders.card-report | ◐ (T) | — |
| FR-PRN-005 | Протокол розблокування ВКЯ, акт утилізації біобанку, маніфест логістики, бланк мікробіолог | worklist batch print, протокол розблокування | Qc_1_3s (текст протоколу) | lab.orders.card-report | ◐ (T) | — |
| FR-PRN-006 | QR-токен: `token = base64url(HMAC-SHA256(orderId + "/" + releasedAt, Lab:VerifySecret))`;  | QR + HMAC verifyToken, GET /verify/{token} | Full_happy_path (valid=true); QR unit | lab.orders.card-report | ✔ | Lab:VerifySecret="change-me…" у конфігурації |
| FR-QC-001 | Статистика серії: `N`, `mean = Σx/N`, `SD = sqrt(Σ(x−mean)²/(N−1))`, `CV% = SD/mean×100`,  | QcStatistics | QcAndEucastTests.Computes_n_mean_sd_cv_bias | lab.qc.* | ✔ | — |
| FR-QC-002 | Правила оцінюються для кожного нового вимірювання; активний набір правил — властивість мат | WestgardEvaluator | Rule_1_2s…Rule_10x, Window_is_limited_to_last_10_points | lab.qc.* | ✔ | — |
| FR-QC-003 | Lockout блокує: автоверифікацію результатів пари (аналізатор, тест) і `verify` без overrid | lockout блокує autoverify/verify | Qc_1_3s_creates_lockout…; Lockout_blocks (unit) | lab.qc.* | ✔ | — |
| FR-QC-004 | Один активний lockout на пару (аналізатор, тест); повторне порушення під час активного loc | один активний lockout/пара | — | lab.qc.* | ◐ (T) | — |
| FR-QC-005 | Протокол розблокування обов'язково містить причину (довідник: реагент, калібрування, обслу | POST /qc/lockouts/{id}/resolve (protocolText) | Qc_1_3s… (текст «ПРОТОКОЛ РОЗБЛОКУВАННЯ», 403 для лаборанта) | lab.qc.* | ✔ | — |
| FR-QC-006 | QC-результат від коннектора (`isQc=true`) зіставляється з матеріалом за `qcLotNumber` та ` | ConnectorService (qcLotNumber) | — | lab.qc.* | ◐ (T) | — |
| FR-QC-007 | Матеріал із простроченим `expiryDate` або відкритий довше `openStabilityDays` — попереджен | expiryDate/openStabilityDays у матеріалі | — | lab.qc.* | ◐ (T) | — |
| FR-QC-008 | Статус карти: `OK` (немає порушень за останні 10 точок), `WARNING` (лише 1_2s), `LOCKOUT`  | GET /qc/levey-jennings (currentStatus) | Qc_1_3s… (LOCKOUT); OK/WARNING не тестуються | lab.qc.* | ✔ | — |
| FR-QC-009 | Зведений звіт ВКЯ за період (`GET /qc/report`): по аналізаторах/тестах/рівнях — N, mean, S | GET /qc/report | — | lab.qc.* | ◐ (T) | — |
| FR-QUE-001 | Інфокіоск (планшет/термінал у холі): «Стати в чергу» за послугою (забір крові, здача матер | сутностей QueueTicket/QueueService, екранів і API немає | — | — | ✘ | — |
| FR-QUE-002 | Табло черги на телевізорі: поточні номери й кабінети, голосовий виклик; окремий легкий веб | сутностей QueueTicket/QueueService, екранів і API немає | — | — | ✘ | — |
| FR-QUE-003 | Малі табло біля кабінетів (`/queue/room/{id}`): номер пацієнта, що викликається / Should | сутностей QueueTicket/QueueService, екранів і API немає | — | — | ✘ | — |
| FR-QUE-004 | Робоче місце медсестри/реєстратора: «Викликати наступного», «Не з'явився», «Повернути в че | сутностей QueueTicket/QueueService, екранів і API немає | — | — | ✘ | — |
| FR-QUE-005 | Пріоритети: CITO, пільгові категорії, діти, запис на час; SLA очікування та звіт / Could | сутностей QueueTicket/QueueService, екранів і API немає | — | — | ✘ | — |
| FR-QUE-006 | Технічно: сутності `QueueTicket`, `QueueService`, `QueueRoom`; оновлення табло через Signa | сутностей QueueTicket/QueueService, екранів і API немає | — | — | ✘ | — |
| FR-REA-001 | Для пари (аналізатор, тест) активним є один лот (`isActive`), інші — у резерві; при вичерп | автоперемикання активного лота при вичерпанні відсутнє; лише алерти | — | lab.rg.* | ✘ | — |
| FR-REA-002 | Списання не блокує результат при `testsRemaining=0` (лише алерт), щоб не спотворювати клін | ConsumeAsync повертає 409 при недостатньому залишку — суперечить вимозі «не блокувати»; автосписання при результаті немає (лише ручне POST …/consume) | — | lab.rg.* | ◐ (C) | — |
| FR-REA-003 | Бейдж у меню «Склад реактивів» показує кількість алертів | GET /reagents/alerts | — | lab.rg.* | ◐ (T) | — |
| FR-REA-004 | Історія списань зберігається в аудиті (`entity=LabReagentLot, action=CONSUME`) | audit CONSUME | — | lab.rg.* | ◐ (T) | — |
| FR-REF-001 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | ReferralService/lookup/journal | ReferralApiTests | lab.referrals.*, lab.clients.* | ✔ | ЕСОЗ Mock/Replay |
| FR-REF-002 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | ClientService (send-in) | ClientApiTests.Clinic_registry_import… | lab.referrals.*, lab.clients.* | ✔ | — |
| FR-REF-003 | **Групове е-направлення** (requisition з кількома ServiceRequest): одне замовлення ЛІС на  | RemainingQuantity перевіряється; групового направлення (кілька ServiceRequest) як процесу немає | ReferralApiTests (частково) | lab.referrals.*, lab.clients.* | ◐ (C) | — |
| FR-REF-004 | Портал клініки-клієнта з автентифікацією: створення замовлень, друк етикеток, статуси, рез | порталу клініки з автентифікацією / згод пацієнта на передачу даних немає | — | lab.referrals.*, lab.clients.* | ✘ | — |
| FR-REF-005 | Згода пацієнта на передачу даних клініці-направнику, страховій, зовнішній лабораторії (FR- | порталу клініки з автентифікацією / згод пацієнта на передачу даних немає | — | lab.referrals.*, lab.clients.* | ✘ | — |
| FR-REG-001 | Номер замовлення формується за маскою з налаштувань (типово `{yyMM}-{000000}`), лічильник  | NumeratorService, маска {yyMM}-{000000} | Full_happy_path (regex №), Journal_numbering_preview | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-002 | Штрихкод проби — 8 цифр за алгоритмом системи-джерела (розд. 4.2, FR-PHL-010); унікальний глобальн | TubeBarcodeGenerator (EAN-8) | BarcodeTests.Counter_*, Check_digit_follows_ean8_weights | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-003 | Одна проба на унікальну комбінацію біоматеріал/пробірка/`groupNumb` (аналог `dct_service_l | TubePlanner/TubePlanService | TubePlannerTests, TubePlanApiTests | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-004 | CITO-замовлення отримує `isUrgentCito=true`, відображається червоним бейджем, потрапляє до | isUrgentCito, фільтр, SLA 45 хв у TAT | — (CITO не асертиться в S-01) | lab.orders.*, lab.matrix.* | ◐ (T) | — |
| FR-REG-005 | `ehealthReferralId` посилається на `ehe_incoming_medical_referral.id`; якщо направлення вж | ReferralService (ЕСОЗ — джерело істини) | ReferralApiTests | lab.orders.*, lab.matrix.* | ✔ | ЕСОЗ Mock/Replay |
| FR-REG-006 | Скасування можливе лише до `RELEASED`; обов'язкова причина; проби переходять у `REJECTED`  | POST /orders/{id}/cancel | BillingApiTests.Cancelled_paid_order… | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-007 | `release` дозволено, коли всі тести `VERIFIED/AUTO_VERIFIED` (відхилені тести не блокують) | OrderService.ReleaseAsync | Full_happy_path | lab.orders.*, lab.matrix.* | ✔ | сповіщення QUEUED без відправника |
| FR-REG-008 | Пакетне створення до 200 замовлень за запит; більші — 413 | POST /orders/batch існує; ліміту 200 / 413 у коді немає | — | lab.orders.*, lab.matrix.* | ◐ (C) | — |
| FR-REG-009 | Пацієнт зберігається у локальній копії `mis_patient_card` (ПІБ, дата народження, стать, те | cmn_person + mis_patient_card | MedLinkAlignmentTests.New_patient_is_stored… | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-010 | Пошук за `search` — за підрядком ПІБ (без урахування регістру), телефоном (цифри), ІПН | GET /patients?search | — | lab.orders.*, lab.matrix.* | ◐ (T) | — |
| FR-REG-011 | НОВЕЗамовлення створюється з направлення МІС у двох контурах: **зовнішнє** (амбулаторія, е | MisReferralService | MisIntegrationApiTests | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-012 | НОВЕКожна позиція замовлення зберігає код інтервенції, послугу ЛІС і **перелік показників, | MisReferralService | MisIntegrationApiTests | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-013 | НОВЕПід час створення замовлення кожен тест отримує прилад і місце в черзі аналізатора (ма | MisReferralService | MisIntegrationApiTests | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-014 | НОВЕЯкщо показники в направленні не передані (е-направлення іншого закладу), реєстратор аб | MisReferralService | MisIntegrationApiTests | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-030 | `GET /dictionaries/order-matrix` повертає тести та профілі, згруповані за підрозділом лабо | OrderMatrixService, order-matrix/favorites | Journal_numbering_preview_follows_mask_and_matrix_has_shape | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-031 | Екран «Матриця призначень» (окрема сторінка та вкладка діалогу створення замовлення): сітк | екран OrderMatrix.vue | — (UI) | lab.orders.*, lab.matrix.* | ◐ (T) | — |
| FR-REG-032 | Обрані набори зберігаються як «обране» співробітника (`lab_order_favorites`) та показуютьс | OrderMatrixService, order-matrix/favorites | Journal_numbering_preview_follows_mask_and_matrix_has_shape | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-REG-033 | Матриця доступна в режимі вбудованого модуля з картки пацієнта evomis (передається `patien | режиму вбудованого модуля з картки evomis у коді не виявлено | — | lab.orders.*, lab.matrix.* | ◐ (C) | — |
| FR-REG-034 | Після підтвердження створюється одне замовлення; тести автоматично розподіляються за підро | OrderMatrixService, order-matrix/favorites | Journal_numbering_preview_follows_mask_and_matrix_has_shape | lab.orders.*, lab.matrix.* | ✔ | — |
| FR-RPT-001 | Кумулятивний звіт пацієнта / Усі верифіковані результати пацієнта за період: групування за | /reports/cumulative/{patientId} | Work_views_reports_and_cumulative_export (стрілки ↓↓, критичні) | lab.rp.* | ✔ | — |
| FR-RPT-002 | Навантаження аналізаторів / По приладу: тести, CITO, стаціонар / поліклініка / ЕСОЗ, повто | /reports/{type} | Work_views… (load) — інші звіти без перевірки | lab.rp.* | ◐ (T) | — |
| FR-RPT-003 | Направлення за джерелами / Стаціонар (у т.ч. пакети), поліклініка, е-направлення ЕСОЗ (у т | /reports/{type} | Work_views… (load) — інші звіти без перевірки | lab.rp.* | ◐ (T) | — |
| FR-RPT-004 | Використання показників у послугах / По послузі ЕСОЗ: скільки разів обрано кожен показник, | /reports/{type} | Work_views… (load) — інші звіти без перевірки | lab.rp.* | ◐ (T) | — |
| FR-RPT-005 | TAT за дослідженнями й аналізаторами / Медіана й P90 (планові, CITO), частка в нормативі;  | /reports/{type} | Work_views… (load) — інші звіти без перевірки | lab.rp.* | ◐ (T) | — |
| FR-RPT-006 | Розподіл і перенаправлення / Час від надходження до підтвердження розподілу, частка ручних | /reports/{type} | Work_views… (load) — інші звіти без перевірки | lab.rp.* | ◐ (T) | — |
| FR-RPT-007 | Невиконані показники / Без обладнання, брак проби, скасування, відхилення — за послугами,  | /reports/{type} | Work_views… (load) — інші звіти без перевірки | lab.rp.* | ◐ (T) | — |
| FR-RPT-008 | Пакети послуг / Кількість за пакетами, відділеннями, частка змінених лікарем, повнота вико | /reports/{type} | Work_views… (load) — інші звіти без перевірки | lab.rp.* | ◐ (T) | — |
| FR-RPT-009 | Критичні значення / Кількість, час до повідомлення (FR-VAL), ескалації (FR-GAP-020) за від | немає | — | lab.rp.* | ✘ | — |
| FR-RPT-010 | Звіт для керівника (зведений) / Обсяги, TAT, брак, ВКЯ, виручка за місяць із порівнянням з | немає | — | lab.rp.* | ✘ | — |
| FR-RPT-011 | Звіти будуються з денних агрегатів `lab_stat_daily` (оновлення щогодини) і з оперативних д | lab_stat_daily | — | lab.rp.* | ◐ (T) | — |
| FR-RPT-012 | Кожне формування звіту пишеться в `lab_report_run` (хто, коли, параметри) — для звітів з п | lab_report_run | Work_views… (ReportRuns.AnyAsync) | lab.rp.* | ✔ | — |
| FR-RPT-013 | Числа — з десятковою комою, дати `dd.MM.yyyy`; шрифт із кирилицею й символами ↑ ↓ вбудован | XLSX/PDF | Work_views… (XLSX+PDF формуються) | lab.rp.* | ◐ (T) | — |
| FR-RPT-014 | (R2) Розсилка звіту за розкладом (e-mail керівнику) та збереження варіантів параметрів | немає | — | lab.rp.* | ✘ | — |
| FR-RPT-015 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає | — | lab.rp.* | ✘ | — |
| FR-RPT-016 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає | — | lab.rp.* | ✘ | — |
| FR-RPT-017 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає | — | lab.rp.* | ✘ | — |
| FR-RPT-018 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає | — | lab.rp.* | ✘ | — |
| FR-RPT-019 | (окремого рядка таблиці немає; вимога згадана діапазоном/в тексті) | немає | — | lab.rp.* | ✘ | — |
| FR-RTE-001 | Кожен тест після створення замовлення має прилад або явну причину його відсутності (`routi | RoutingService | Cito_routing_prefers_fastest_analyzer_and_skips_long_offline; Distribution_change… | lab.wr.analyzer-queue|reroute | ✔ | — |
| FR-RTE-002 | **Черга аналізатора** = тести з `analyzer_id` у статусах `PENDING/RERUN`. Порядок: CITO, п | RoutingService | Cito_routing_prefers_fastest_analyzer_and_skips_long_offline; Distribution_change… | lab.wr.analyzer-queue|reroute | ✔ | — |
| FR-RTE-003 | Коннектор отримує від ЛІС лише замовлення своїх приладів (`GET /connector/orders/pending`, | RoutingService | Cito_routing_prefers_fastest_analyzer_and_skips_long_offline; Distribution_change… | lab.wr.analyzer-queue|reroute | ✔ | — |
| FR-RTE-004 | Лабораторія бачить інформацію про обладнання вже в картці замовлення та журналі направлень | RoutingService | Cito_routing_prefers_fastest_analyzer_and_skips_long_offline; Distribution_change… | lab.wr.analyzer-queue|reroute | ✔ | — |
| FR-RTE-005 | `NO_EQUIPMENT`: жоден прилад не виконує показник. Тест лишається в окремій черзі «Без обла | RoutingService | Cito_routing_prefers_fastest_analyzer_and_skips_long_offline; Distribution_change… | lab.wr.analyzer-queue|reroute | ✔ | — |
| FR-RTE-006 | Ручне перенаправлення (`PUT /order-tests/{id}/analyzer`): лише на прилад із переліку канди | RoutingService | Cito_routing_prefers_fastest_analyzer_and_skips_long_offline; Distribution_change… | lab.wr.analyzer-queue|reroute | ✔ | — |
| FR-RTE-007 | Тести однієї проби за можливості в одну чергу (крок 3). Якщо розділення неминуче — поперед | RoutingService | Cito_routing_prefers_fastest_analyzer_and_skips_long_offline; Distribution_change… | lab.wr.analyzer-queue|reroute | ✔ | — |
| FR-RTE-008 | Локаут ВКЯ на приладі: тести `PENDING` для цього показника автоматично переназначаються на | RoutingService | Cito_routing_prefers_fastest_analyzer_and_skips_long_offline; Distribution_change… | lab.wr.analyzer-queue|reroute | ✔ | — |
| FR-RTE-009 | Вимкнення послуги/показника/приладу (FR-EQP-007): автоматичне переназначення всіх `PENDING | авто-переназначення при lockout/вимкненні послуги | — (lockout-reroute, S-24, не тестується) | lab.wr.analyzer-queue|reroute | ◐ (T) | — |
| FR-RTE-010 | Тривала відсутність зв'язку (heartbeat коннектора, розд. 6.7) понад `Lab:Routing:RerouteOn | авто-переназначення при lockout/вимкненні послуги | — (lockout-reroute, S-24, не тестується) | lab.wr.analyzer-queue|reroute | ◐ (T) | — |
| FR-RTE-011 | Rerun: тест повертається в чергу того самого приладу; зміна приладу — лише ручна (FR-RTE-0 | авто-переназначення при lockout/вимкненні послуги | — (lockout-reroute, S-24, не тестується) | lab.wr.analyzer-queue|reroute | ◐ (T) | — |
| FR-RTE-012 | Зміна складу показників після реєстрації (21.8) перераховує маршрутизацію лише для доданих | RoutingService | Cito_routing_prefers_fastest_analyzer_and_skips_long_offline; Distribution_change… | lab.wr.analyzer-queue|reroute | ✔ | — |
| FR-SEC-001 | Довідник `LabSection {code, name, sectionType, departmentId (org_department), journalMask, | SectionJournalService, /sections* | — | lab.sections.* | ◐ (T) | — |
| FR-SEC-002 | `LabTestDefinition.labSectionId` — обов'язкове поле; профіль може містити тести різних під | SectionJournalService, /sections* | — | lab.sections.* | ◐ (T) | — |
| FR-SEC-003 | При `POST /samples/{barcode}/receive` система створює по одному запису журналу `LabSection | SectionJournalService | Full_happy_path (journal-entries) | lab.sections.* | ✔ | — |
| FR-SEC-004 | Маска журналу підтримує плейсхолдери `{yyyy}`, `{yy}`, `{MM}`, `{dd}`, `{seqN}` (наскрізни | маски журналів, renumber-preview | Journal_numbering_preview… | lab.sections.* | ✔ | — |
| FR-SEC-005 | Лічильники атомарні (`lab_numerators` за ключем підрозділ+період); передбачений попередній | маски журналів, renumber-preview | Journal_numbering_preview… | lab.sections.* | ✔ | — |
| FR-SEC-006 | Сторінка «Журнал відділення»: за датою/періодом — номер журналу, номер за добу, замовлення | SectionJournalService, /sections* | — | lab.sections.* | ◐ (T) | — |
| FR-SEC-007 | Робочий стіл лаборанта фільтрується за підрозділом; у рядку показується номер журналу | SectionJournalService, /sections* | — | lab.sections.* | ◐ (T) | — |
| FR-SEC-008 | Для підрозділу можна вимкнути автовидачу результатів пацієнту (наприклад, патогістологія в | SectionJournalService, /sections* | — | lab.sections.* | ◐ (T) | — |
| FR-SEC-009 | Один зразок може обслуговувати кілька підрозділів (наприклад, сироватка — біохімія та імун | SectionJournalService, /sections* | — | lab.sections.* | ◐ (T) | — |
| FR-SEC-010 | Сід: 8 підрозділів із масками, шаблонами стадій і прив'язкою тестів | маски журналів, renumber-preview | Journal_numbering_preview… | lab.sections.* | ✔ | — |
| FR-SGN-001 | У картці замовлення зі статусом `RELEASED`, створеним `mis_diagnostic_report` і `ehealthSt | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-002 | **Крок 1. Вхід в ЕСОЗ.** Форма «Вхід в ЕСОЗ»: поле «Ел. пошта або логін», поле «Пароль» (з | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-003 | **Крок 2. Підтвердження особи (обов'язковий).** Екран «Двофакторна автентифікація (2FA)»:  | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-004 | Сесія ЕСОЗ має термін дії (`Ehealth:Session:TtlMinutes`, типово 30; `IdleMinutes` — 15). У | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-005 | **Підпис КЕП.** Окремий крок після L2, навіть якщо вхід був через 2FA. Підписується пакет  | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-006 | Перевірки перед підписом: замовлення `RELEASED`; усі показники валідовані, критичні підтве | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-007 | Відправка: `POST patients/{patientId}/diagnostic_report_package` → `GET jobs/{id}` → id зв | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-008 | Черга повторів: підписаний пакет зберігається; при тимчасовій помилці (мережа, 5xx) — авто | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-009 | Виправлення після відправки: анулювання звіту (`entered_in_error`) підписується КЕП так са | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-010 | Аудит у `lab_ehealth_exchange_log`: користувач, час, метод входу (PASSWORD+KEP/TOTP), сері | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-011 | Підпис особистий: делегування, спільні облікові записи й підпис «від імені» заборонені | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-012 | Режим `Ehealth:Mode=Mock` (прототип): імітація форми входу, вибору КЕП/2FA, віджета підпис | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-013 | Для внутрішніх (стаціонар) призначень відправка в ЕСОЗ вимкнена, доки не ввімкнено `Ehealt | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-014 | Безпека: лише TLS, CSP з дозволом для скриптів віджета підпису, захист від clickjacking, б | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-015 | **Порядок викликів.** Видача результату в ЛІС більше **не погашає** е-направлення. Спершу  | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-016 | **Таблиця `ehe_outbox`:** `id, kind (SUBMIT_DR / COMPLETE_SR / CANCEL_DR), entity_id, orde | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-017 | **Воркер** (`BackgroundService`, `Ehealth:Outbox:*`): раз на `PollIntervalSeconds` вибирає | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-018 | **Витримки.** Повтор відправки: `BackoffBaseSeconds · 2^(n−1)` (30 с, 1, 2, 4, 8, 16 хв …) | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-019 | **Класифікація відповідей ЕСОЗ.** `401` → зупинка (`FAILED`, статус звіту `AUTH_REQUIRED`) | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-020 | **Виправлення.** Пакет відхилено (job `failed`) → задача `FAILED`/`JOB_FAILED`, звіт `REJE | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-021 | **Режими `Ehealth:Mode`.** `Mock` — двійник контракту OpenAPI в пам'яті (реєстр направлень | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-022 | **Е-направлення.** Пошук виконується в ЕСОЗ (`GET /api/service_requests?requisition=`), да | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SGN-023 | **Екран «Відправка в ЕСОЗ»** (`/laboratory/ehealth`): таблиця вихідних задач (замовлення,  | EhealthSignService, EhealthOutbox, EhealthClient | MisIntegrationApiTests.Ehealth_session…; EhealthOutboxApiTests (10); EhealthContractTests (18); EhealthReplayApiTests (2) | lab.esoz.* | ✔ | Mock/Replay; Live OAuth не реалізовано; КЕП — структурна перевірка, 2FA «123456»; віджета КЕП немає |
| FR-SMP-020 | `LabOrderSample` розширюється полями `parentSampleId`, `derivationType (PRIMARY, ALIQUOT,  | SampleProcessingService (split/stage/tree) | Sample_split_creates_children_with_derived_barcodes_and_moves_tests | lab.samples.* | ✔ | — |
| FR-SMP-021 | Ідентифікатор дочірнього контейнера = ідентифікатор батьківського + суфікс типу та порядко | SampleProcessingService (split/stage/tree) | Sample_split_creates_children_with_derived_barcodes_and_moves_tests | lab.samples.* | ✔ | — |
| FR-SMP-022 | Дія «Розділити» (`POST /samples/{barcode}/split`): кількість, об'єм, тип похідного, цільов | SampleProcessingService (split/stage/tree) | Sample_split_creates_children_with_derived_barcodes_and_moves_tests | lab.samples.* | ✔ | — |
| FR-SMP-023 | Для кожного підрозділу визначено шаблон стадій (`lab_workflow_templates`) з упорядкованим  | workflow-templates, події стадій | — | lab.samples.* | ◐ (T) | — |
| FR-SMP-024 | Перехід стадії (`POST /samples/{barcode}/stage`) пише подію `LabSampleStageEvent {stageCod | SampleProcessingService (split/stage/tree) | Sample_split_creates_children_with_derived_barcodes_and_moves_tests | lab.samples.* | ✔ | — |
| FR-SMP-025 | Стадії відображаються степпером у картці зразка та у журналі відділення; TAT розраховуєтьс | workflow-templates, події стадій | — | lab.samples.* | ◐ (T) | — |
| FR-SMP-026 | Для патогістології/цитології результат тесту — структурований текстовий висновок (макроопи | структурований висновок / сід | PathologyApiTests | lab.samples.* | ✔ | — |
| FR-SMP-027 | Для мікробіології дочірні об'єкти «чашка/середовище» та «ізолят» зберігають зв'язок із пер | workflow-templates, події стадій | — | lab.samples.* | ◐ (T) | — |
| FR-SMP-028 | Будь-який контейнер дерева можна знайти за штрихкодом; картка показує повний шлях (chain o | SampleProcessingService (split/stage/tree) | Sample_split_creates_children_with_derived_barcodes_and_moves_tests | lab.samples.* | ✔ | — |
| FR-SMP-029 | Утилізація/архівування фіксується як стадія з терміном зберігання за типом підрозділу (біо | workflow-templates, події стадій | — | lab.samples.* | ◐ (T) | — |
| FR-SMP-030 | Усі операції аудуються; поділ і зміна стадії доступні ролям LAB_TECHNICIAN, LAB_DOCTOR, LA | workflow-templates, події стадій | — | lab.samples.* | ◐ (T) | — |
| FR-SMP-031 | Сід демонстраційних даних містить приклад поділу сироватки на дві аліквоти та приклад пато | структурований висновок / сід | PathologyApiTests | lab.samples.* | ✔ | — |
| FR-SMP-032 | Коннектор передає штрихкод дочірнього контейнера як є; сервер знаходить тести за дочірнім  | workflow-templates, події стадій | — | lab.samples.* | ◐ (T) | — |
| FR-SO-001 | Взаєморозрахунки з лабораторіями-виконавцями: реєстр їхніх рахунків, звірка з реєстрами se | взаєморозрахунки і оцінка якості партнерів відсутні | — | lab.sendout.* | ✘ | — |
| FR-SO-002 | Контроль якості зовнішніх лабораторій: TAT партнера, частка відмов, розбіжності; щорічна о | взаєморозрахунки і оцінка якості партнерів відсутні | — | lab.sendout.* | ✘ | — |
| FR-SVA-001 | Сторінка «Послуги ЕСОЗ × аналізатори» (Довідники ЛІС): рядки — послуги ЕСОЗ (код, назва, п | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-002 | Одна послуга може мати будь-яку кількість приладів. Пріоритет задається на рівні послуги ( | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-003 | Картка послуги (клік по рядку): таблиця «показник × прилад» з ознакою «виконує», колонки « | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-004 | Показник може виконуватися приладом, що не входить у перелік приладів послуги (наприклад,  | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-005 | Стан приладу відображається в довіднику й ураховується при розподілі: «немає зв’язку» (hea | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-006 | Ручні методи моделюються як «прилад» типу `MANUAL` (робоче місце з ручною постановкою), що | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-007 | Редагування — LAB\_ADMIN і LAB\_DOCTOR; кожна зміна — в аудит (хто, коли, до/після). Зміна | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-008 | Послуга без жодного приладу має готовність «Немає обладнання»; її показники при надходженн | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-009 | Експорт/імпорт матриці CSV (для первинного наповнення); імпорт — з попереднім переглядом з | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-010 | `GET /mis/catalog` повертає для кожної активної послуги: код інтервенції ЕСОЗ, назву, режи | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-011 | Каталог версіонується (`catalogVersion`, ETag). МІС кешує його; при зміні ЛІС надсилає под | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-012 | У МІС: обов’язкові показники позначені замком і не знімаються; для `FIXED` вибір не показу | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-013 | ЛІС повторно перевіряє склад при прийомі: відсутній обов’язковий показник → 422 `required- | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-014 | Показники, не обрані лікарем, не створюються як тести, не ставляться в чергу й не тарифіку | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-SVA-015 | Лаборант або лікар-лаборант може додати показник до вже прийнятої послуги (дозамовлення, F | /analyzer-services/*, /mis/catalog | MisIntegrationApiTests.Service_matrix_card_and_catalog_for_mis | lab.mis.matrix|service-card | ✔ | — |
| FR-TAT-001 | Етапи: `order→collected` (забір), `collected→received` (логістика), `received→resulted` (а | TatCalculator | RulesEngineTests.Stages_and_total, Missing_stage_gives_null | lab.tat.* | ✔ | — |
| FR-TAT-002 | Агрегати: медіана та P90 за період, за профілем, за CITO/рутина; кількість порушень SLA (C | AnalyticsService | RulesEngineTests.Median_and_p90 (unit) | lab.tat.* | ◐ (T) | — |
| FR-TAT-003 | Обсяги: кількість тестів/замовлень за `groupBy=profile/analyzer/employee/day` | /analytics/volume, export.xlsx | — | lab.tat.* | ◐ (T) | — |
| FR-TAT-004 | Брак біоматеріалу: частка відхилених проб і розподіл за причинами; ціль < 1 % | /analytics/volume, export.xlsx | — | lab.tat.* | ◐ (T) | — |
| FR-TAT-005 | Експорт XLSX (`/analytics/export.xlsx?report=tat/volume/rejections/qc`) з українськими заг | /analytics/volume, export.xlsx | — | lab.tat.* | ◐ (T) | — |
| FR-TAT-006 | Плитки дашборду: CITO TAT (медіана, % у SLA), плановий TAT, брак %, досліджень за зміну; w | /analytics/volume, export.xlsx | — | lab.tat.* | ◐ (T) | — |
| FR-TEN-001 | Модель ізоляції — **рядкова** (колонка `organization_id` у кожній таблиці `lab_*`), як в e | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) | — | — | ✘ | — |
| FR-TEN-002 | Поточний tenant визначається `ITenantContext`: режим B — з claims IdentityServer (`organiz | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) | — | — | ✘ | — |
| FR-TEN-003 | Глобальні фільтри EF Core (`HasQueryFilter(e => e.OrganizationId == tenant.Id)`) на всіх ` | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) | — | — | ✘ | — |
| FR-TEN-004 | Довідники поділяються на **системні** (біоматеріали, типи пробірок, методики, типи аналіза | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) | — | — | ✘ | — |
| FR-TEN-005 | Нумератори (замовлення, штрихкоди, журнали) — у межах організації; штрихкод пробірки має п | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) | — | — | ✘ | — |
| FR-TEN-006 | Коннектор прив'язується до організації при реєстрації інсталяції (`LabConnectorInstallatio | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) | — | — | ✘ | — |
| FR-TEN-007 | Кабінет пацієнта показує результати пацієнта з усіх організацій, де є його картка (крос-те | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) | — | — | ✘ | — |
| FR-TEN-008 | Аудит і журнали обміну — з `organization_id`; звіти та BI — лише в межах організації, конс | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) | — | — | ✘ | — |
| FR-TEN-009 | Міграція пілотних даних SQLite в мультитенантну схему: усі наявні рядки отримують `organiz | organization_id/ITenantContext/фільтри в main відсутні (є лише план у docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md) | — | — | ✘ | — |
| FR-VAL-001 | `CRIT_LOW/CRIT_HIGH` ніколи не автоверифікуються і не можуть бути `VERIFIED` без коментаря | VerifyAsync + AutoVerificationEngine | Full_happy_path (400 без коментаря); Critical_value_always_blocks | lab.val.* | ✔ | — |
| FR-VAL-002 | Критичний результат без запису `LabPanicCall` відображається у `GET /panic/pending` і в лі | GET /panic/pending є, але OrderService.ReleaseAsync НЕ перевіряє незареєстровані панічні дзвінки (409 PANIC_CALL_REQUIRED відсутній) | — | lab.val.* | ✘ | — |
| FR-VAL-003 | Журнал дзвінка містить: `resultId, orderId, patientName, testCode, value, doctorNotifiedNa | LabPanicCall, /panic-calls | — | lab.val.* | ◐ (T) | — |
| FR-VAL-004 | Пакетна верифікація пропускає критичні без коментаря та lockout-рядки, повертає `skipped[{ | VerifyBatchAsync (skipped[]) | Full_happy_path (verified≥1; skipped не асертиться) | lab.val.* | ◐ (T) | — |
| FR-VAL-005 | Верифікований результат — immutable: зміна значення можлива лише після reopen, що створює  | reopen + нова версія | Full_happy_path (лише наявність REOPEN у transitions) | lab.val.* | ◐ (T) | — |
| FR-VAL-006 | Відхилення результату (`reject {reason}`) переводить тест у `REJECTED`; якщо причина — про | RejectAsync | — | lab.val.* | ◐ (T) | — |
| FR-VAL-007 | Типові панічні межі (сід): калій  6,2 ммоль/л; глюкоза  25 ммоль/л; гемоглобін  200 г/л; т | сід шарів норм (crit) | — | lab.val.* | ◐ (T) | — |
| FR-VAL-008 | Push/SMS лікарю-направляючому — заглушка `PatientNotification(channel=PUSH, recipientType= | запису PatientNotification(recipientType=DOCTOR) немає навіть як заглушки | — | lab.val.* | ✘ | — |
| FR-WRK-001 | Спільні фільтри: джерело (стаціонар / поліклініка / ЕСОЗ / клініка-партнер), лише пакети,  | /work/summary | Work_views_reports_and_cumulative_export (noEquipment, analyzers) | lab.mis.work-*, lab.wr.* | ✔ | — |
| FR-WRK-002 | **За аналізатором.** Плитки приладів: категорія, у черзі, в аналізі, CITO, стан (зв’язок,  | /work/by-analyzer/by-patient/by-service/export | Work_views… лише викликає by-service/by-patient без перевірки вмісту | lab.mis.work-*, lab.wr.* | ◐ (T) | — |
| FR-WRK-003 | Дії в черзі: «Перенаправити» (лише на прилади, що виконують усі показники групи й доступні | /work/by-analyzer/by-patient/by-service/export | Work_views… лише викликає by-service/by-patient без перевірки вмісту | lab.mis.work-*, lab.wr.* | ◐ (T) | — |
| FR-WRK-004 | **За пацієнтом.** Список пацієнтів з активними замовленнями (готово N з M). Картка: усі за | /work/by-analyzer/by-patient/by-service/export | Work_views… лише викликає by-service/by-patient без перевірки вмісту | lab.mis.work-*, lab.wr.* | ◐ (T) | — |
| FR-WRK-005 | **За дослідженням.** Рядок на послугу ЕСОЗ: кількість направлень і показників, розподіл по | /work/by-analyzer/by-patient/by-service/export | Work_views… лише викликає by-service/by-patient без перевірки вмісту | lab.mis.work-*, lab.wr.* | ◐ (T) | — |
| FR-WRK-006 | Статуси тесту в розрізах: в черзі, в аналізі, результат (очікує верифікації), верифіковано | /work/by-analyzer/by-patient/by-service/export | Work_views… лише викликає by-service/by-patient без перевірки вмісту | lab.mis.work-*, lab.wr.* | ◐ (T) | — |
| FR-WRK-007 | Оновлення без перезавантаження (polling 15 с або SignalR, як у FR-WS); зміни, зроблені в і | /work/by-analyzer/by-patient/by-service/export | Work_views… лише викликає by-service/by-patient без перевірки вмісту | lab.mis.work-*, lab.wr.* | ◐ (T) | — |
| FR-WRK-008 | Експорт поточного розрізу з фільтрами в XLSX | /work/by-analyzer/by-patient/by-service/export | Work_views… лише викликає by-service/by-patient без перевірки вмісту | lab.mis.work-*, lab.wr.* | ◐ (T) | — |
| FR-WRK-009 | Обраний розріз і фільтри запам’ятовуються для користувача | /work/by-analyzer/by-patient/by-service/export | Work_views… лише викликає by-service/by-patient без перевірки вмісту | lab.mis.work-*, lab.wr.* | ◐ (T) | — |
| FR-WRK-010 | Робочий стіл лаборанта (розд. 4.4) лишається місцем введення й верифікації результатів; з  | /work/by-analyzer/by-patient/by-service/export | Work_views… лише викликає by-service/by-patient без перевірки вмісту | lab.mis.work-*, lab.wr.* | ◐ (T) | — |
| FR-WS-001 | Рядок worklist = один `LabOrderTest`; поля згідно з контрактом 2.7 | WorklistService | Health_swagger_worklist_orders_respond; Full_happy_path | lab.ws.* | ✔ | — |
| FR-WS-002 | Введення числа приймає як «,» так і «.»; зберігається `decimal`, відображається з `decimal | UI (кома/крапка) | — | lab.ws.* | ◐ (T) | — |
| FR-WS-003 | Збереження проходить єдиний конвеєр (розд. 4.0) і повертає оновлений рядок ≤ 300 мс (P95) | ResultPipelineService | — (perf ≤300 мс не вимірюється) | lab.ws.* | ◐ (T) | — |
| FR-WS-004 | Текстові/dropdown результати не беруть участі у delta-check | delta лише для numeric | RulesEngineTests (unit) | lab.ws.* | ◐ (T) | — |
| FR-WS-005 | `CALCULATED` тести обчислюються формулою (`formula`, напр. `MCH = HGB / RBC × 10`) після з | поле formula є, обчислення CALCULATED-показників немає (формули читаються лише для складових у MisReferralService/RoutingService) | — | lab.ws.* | ✘ | — |
| FR-WS-006 | Автоверифікація пакетна: обробляє всі `RESULTED` (або передані id), повертає `verified[]`, | AutoVerificationEngine + POST /worklist/autoverify | RulesEngineTests: Normal_clean_result_is_approved, Critical/Abnormal/Delta/Lockout/Manual/Analyzer_flags_block (unit); API-рівень без тесту | lab.ws.* | ✔ | — |
| FR-WS-007 | Rerun дозволений для `RESULTED/NEEDS_REVIEW`; попередній результат зберігається в історії  | RerunAsync; прапорця supersededByRerun немає | — | lab.ws.* | ◐ (C) | — |
| FR-WS-008 | Батч (`LabWorklistBatch`): код `B-{yyMMdd}-{nnn}`, перелік `orderTestId`, аналізатор; друк | /worklist/batches + print | — | lab.ws.* | ◐ (T) | — |
| FR-WS-009 | Кольори прапорців: NORMAL зелений, LOW/HIGH жовтий (▲/▼), CRIT_* червоний (✖, мерехтіння 3 | UI | — (UI) | lab.ws.* | ◐ (T) | — |
| FR-WS-010 | Статус рядка: `PENDING` білий/синій бейдж «Очікує», `RESULTED` «Введено», `NEEDS_REVIEW` п | UI | — (UI) | lab.ws.* | ◐ (T) | — |
| FR-WS-011 | Прострочення: якщо `now − receivedAt > profile.turnaroundHours` і статус не термінальний — | UI/worklist | — | lab.ws.* | ◐ (T) | — |
| FR-WS-012 | Коментар оператора (`operatorComment`) зберігається разом зі значенням; обов'язковий при з | OperatorComment є; обов'язковість поза фізіол. діапазоном не виявлено | — | lab.ws.* | ◐ (C) | — |
| FR-WS-013 | НОВЕФільтр приладу в робочому столі показує **чергу аналізатора**: тести, яким маршрутизац | GET /analyzers/{id}/queue | MisIntegrationApiTests.Distribution_change_auto_and_confirm… | lab.ws.* | ✔ | — |
| FR-WS-014 | НОВЕДля рядка доступна дія «Перенаправити на інший прилад» (лише приладів, що вміють цей п | POST /analyzers/{id}/reroute | — (reroute не тестується) | lab.ws.* | ◐ (T) | — |

Метод: FR-ідентифікатори витягнуто з таблиць і тексту ТЗ (діапазони `FR-XXX-001…010` розгорнуто); оцінку виставлено за прочитаним кодом (контролери, сервіси, Core, коннектор), переліком тестів, розділами «Стан реалізації» ТЗ та реєстром задач. Тести не запускались, тому «✔» означає наявність тесту з релевантними асертами, а не його проходження. Назви сторонніх систем-джерел у документі не наводяться.


---

## Доповнення: сповіщення, доставка подій у МІС, конфігурація, автентифікація (розд. 26 ТЗ)

| ID | Вимога | Реалізація | Автотест | Guide | Статус | Примітка |
|----|--------|-----------|----------|-------|--------|----------|
| FR-NTF-001 | Фонова служба відправляє сповіщення пацієнтам із черги, статуси QUEUED→SENDING→SENT/FAILED/SKIPPED/NOT_CONFIGURED | NotificationDispatcher, NotificationWorker | NotificationDeliveryTests | lab.notify.queue | ✔ | Реальні шлюзи потребують налаштування замовником |
| FR-NTF-002 | Провайдер каналу: Email (SMTP), SMS/Viber/Telegram (HTTP-шлюз); без налаштувань — «Не налаштовано», а не успіх | SmtpEmailProvider, HttpNotifyProvider | Unconfigured_channel_is_honest…, Default_config_marks_everything_not_configured… | lab.notify.channels | ✔ | Живі SMTP/шлюзи не перевірялись (фейки) |
| FR-NTF-003 | Повтори з експоненційною паузою, межа спроб, ручний повтор | NotifyOptions.RetryDelay, POST /notifications/{id}/retry | Transient_errors_retry…, Permanent_error_fails… | lab.notify.queue | ✔ | |
| FR-NTF-004 | Ідемпотентність постановки та відправки | унікальний IdempotencyKey, атомарне блокування запису | Queueing_is_idempotent… | — | ✔ | |
| FR-NTF-005 | Згода пацієнта й наявність контакту; без них SKIPPED з причиною | CmnPerson.SubscribedToNotifications | No_consent_or_no_contact_is_skipped… | lab.notify.queue | ✔ | |
| FR-NTF-006 | Шаблони без медичних даних (лише посилання) | NotifyTemplates | Mask_and_templates_do_not_leak, Sms_text_has_link_only… | — | ✔ | |
| FR-NTF-007 | Екран «Сповіщення» з фільтрами, повтором і станом каналів без показу секретів | NotificationsPage, NotificationsController | Admin_can_switch_channel_off…, List_filters_and_summary | lab.notify.* | ✔ | Скріншота немає (фронт не збирався) |
| FR-MIS-026a | Доставка подій МІС: outbox + webhook із HMAC, повтори, ідемпотентний ключ, статуси | MisDeliveryEngine, MisDeliveryWorker | Mis_event_is_delivered_with_valid_hmac…, Mis_delivery_retries… | lab.notify.mis | ✔ | Режим Disabled за замовчуванням |
| FR-CFG-001 | Гучне попередження про заглушки Lab:VerifySecret / PublicBaseUrl поза Development (лог + /health) | StartupConfigCheck | StartupConfigCheckTests, Default_config_… | — | ✔ | Система не падає |
| FR-AUTH-001 | Перемикач Auth:Mode (None за замовчуванням, Header) і попередження в /health | AuthModes, CurrentEmployeeMiddleware | Auth_mode_none_ignores_employee_header | — | ✔ | Вхід не реалізовано: docs/AUTH_PLAN.md |
