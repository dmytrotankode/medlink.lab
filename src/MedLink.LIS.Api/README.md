# MedLink LIS 4.0 — Backend (ASP.NET Core 8, EF Core 8, SQLite)

Лабораторна інформаційна система для МІС MedLink (evomis). Контракт: `docs/API_CONTRACT.md`.
Без автентифікації користувачів: поточний співробітник — заголовок `X-MedLink-Employee-Id` (інакше `Lab:DefaultEmployeeId`);
роль (`org_employee.lab_role`) визначає дозволені дії (403 ProblemDetails українською).

## Запуск

```bash
dotnet run --project src/MedLink.LIS.Api            # http://0.0.0.0:5055 (Kestrel з appsettings.json)
dotnet run --project src/MedLink.LIS.Api --urls http://127.0.0.1:5055
```

- Swagger: `/swagger`, health: `/health`, SPA: `wwwroot/index.html` (fallback для не-`/api` шляхів), QR-верифікація бланка: `/verify/{token}`.
- При старті: `EnsureCreated()` + ідемпотентний сід довідників (`db/seed/*.json`, вбудовані ресурси) + демо-дані (`Lab:SeedDemoData`).

## Конфігурація (`appsettings.json`)

| Ключ | Призначення |
|---|---|
| `Database:SqlitePath` | файл SQLite (типово `App_Data/medlink_lis.db`, відносно content root) |
| `Lab:DefaultEmployeeId` | співробітник за замовчуванням (`emp-…0001`, LAB_ADMIN) |
| `Lab:PublicBaseUrl` | база для QR-посилань та ZIP коннектора |
| `Lab:VerifySecret` | HMAC-секрет токена бланка |
| `Lab:SeedDemoData` | створювати демо-замовлення/ВКЯ/біобанк |
| `Lab:MessageRetentionDays`, `Lab:ConnectorLogRetentionDays`, `Lab:RetentionCleanupIntervalMinutes` | фонове очищення журналів (`RetentionCleanupService`) |

Ролі співробітників для тестування: `emp-0000-0000-0000-00000000000{1..6}` = LAB_ADMIN, LAB_DOCTOR, LAB_TECHNICIAN, PHLEBOTOMIST, LOGISTICS_COURIER, REGISTRAR.
Коннектор демо: install key `DEMO-INSTALL-KEY-0001` → `POST /api/v1/lab/connector/register`.

## Де що лежить

- `Domain/LisStateMachine.cs` — **єдине визначення машини станів та рольових правил** (Order, Sample, OrderTest, Manifest, Culture, Connector); `Domain/RolePolicy.cs` — перевірка (409 статус / 403 роль), `allowedActions` у DTO, `GET /orders/{id}/transitions`.
- `Services/ResultPipelineService.cs` — єдиний конвеєр результатів (ручне введення, коннектор, імпорт): каскад норм → прапорець → delta-check → reflex → автоверифікація (lockout блокує) → історія/аудит → прогресивна видача (`ProgressiveReleaseService`).
- `Services/SectionJournalService.cs` — секції лабораторії, журнали відділень (нумерація за масками `{yyyy}-{seq6}`, `{yy}{MM}{dd}/{dayseq3}`, `S{yy}-{seq5}` через `lab_numerator`).
- `Services/SampleProcessingService.cs` — алікотування/касети/блоки/скельця (похідні штрихкоди), етапи обробки за шаблонами `lab_workflow_template`.
- `Services/Parsing/AnalyzerMessageParserFallback.cs` — локальний парсер ASTM/HL7 для `/analyzers/{id}/simulate`; замінюється реалізацією `MedLink.LIS.Core.Protocols` однією реєстрацією `IAnalyzerMessageParser` у `Program.cs`.
- `Data/LisDbContext.cs` — snake_case, GUID-рядки, службові колонки `created_on/created_by/modified_on/modified_by/is_deleted`.
- Чисті правила: `src/MedLink.LIS.Core/Clinical/*` (NormsCascadeResolver, ResultFlagger, DeltaCheckEvaluator, AutoVerificationEngine, ReflexRuleEngine, WestgardEvaluator, QcStatistics, EucastInterpreter, TatCalculator), `Core/Barcodes/*` (TubeBarcodeGenerator — Simplex gen_lab_tube_barcode, Code128Svg, ZplLabelBuilder, QrSvg), `Core/Common/TransliterationKmu2010`.

## Перехід в evomis / PostgreSQL

1. `Microsoft.EntityFrameworkCore.Sqlite` → `Npgsql.EntityFrameworkCore.PostgreSQL`; у `Program.cs` `UseSqlite` → `UseNpgsql`. Типи колонок (TEXT/INTEGER/REAL/NUMERIC, дати UTC) сумісні; DDL — `db/postgres`.
2. Mirror-сутності (`MisPatientCard`, `MisSpecimen`, `MisDiagnosticReport`, `OrgEmployee`, `OrgDepartment`, `EheIncomingMedicalReferral`, `DctService`) вказують на реальні таблиці evomis — лишити `[Table]`, прибрати їх із сідера (`Data/Seed/SeedService.SeedOrgAsync`).
3. `LabTestProfile.MisServiceId` → `dct_service.id`: код/назва/ціна беруться з МІС (`GET /norms/service-card/{id}`), ЛІС зберігає лише лабораторні атрибути.
4. `EnsureCreated()` замінити міграціями; `ICurrentEmployee` заповнювати з claims IdentityServer замість заголовка.
