# Повна технічна та архітектурна документація: MedLink LIS 3.0 (.NET 8 Core + Vue + SQLite)

**Версія системи:** 3.0.0-PROD  
**Цільова платформа:** .NET 8 Core (ASP.NET Core Web API)  
**ORM:** Entity Framework Core 8.0.10 (SQLite Provider)  
**База даних:** `medlink_lab_local.db` (SQLite 3)  
**Фронтенд:** Vue 2.6 + Quasar Framework (стилістика MedLink `evomis`)  
**Адреса запущеного .NET сервісу:** `http://localhost:5055`  
**Swagger UI:** `http://localhost:5055/swagger`  
**Веб-інтерфейс:** `http://localhost:5055/medlink_lab_frontend/run_prototype.html`  
**Генеральне ТЗ:** `http://localhost:5055/TZ_LIS_MedLink_v3_Master_Specification.html`  

---

## 1. Архітектурний огляд та статус працездатності

Додаток **MedLink.LIS.Api** є **повністю робочим і автономним .NET 8 Core Web API сервісом**. Він самостійно запускається на веб-сервері **Kestrel**, підключається до реальної бази даних SQLite `medlink_lab_local.db`, обслуговує всі REST API контролери, інтерактивну документацію **Swagger UI (OpenAPI 3.0)**, а також роздає статичні файли інтерфейсу MedLink LIS на Vue/Quasar.

### Фізичне розташування вихідного коду:
```
C:\__MEDLINK___\LABA\MedLink.LIS.Api\
├── Controllers\
│   ├── LaboratoryNormsController.cs               # Багатошаровий каскад норм Delphi (CRUD та резолвер)
│   ├── LaboratoryWorklistController.cs            # Робочий журнал, валідація лікарем, автовалідація, паніка CITO
│   ├── LaboratoryQcController.cs                  # Внутрішній контроль якості (Levey-Jennings, Westgard, Lockout)
│   └── LaboratoryBiobankAndDictionariesController.cs # Біобанк (комірки), довідники біоматеріалів, пробірок, аналізаторів
├── Data\
│   └── MedLinkLabDbContext.cs                     # Entity Framework Core контекст БД для SQLite
├── Models\
│   └── Entities.cs                                # 10 сутностей доменної моделі зі зв'язками з MedLink MIS
├── Services\
│   └── LabNormsCascadeService.cs                  # Математично точний C# рушій нашарування норм Delphi
├── Properties\
│   └── launchSettings.json                        # Профілі запуску
├── wwwroot\                                       # Каталог статичних ресурсів Swagger
├── MedLink.LIS.Api.csproj                         # Проєктний файл (.NET 8.0, EF Core Sqlite, Swashbuckle)
└── Program.cs                                     # Точка входу, конфігурація Kestrel :5055, Swagger, CORS, StaticFiles
```

---

## 2. Інтеграційні зв'язки з платформою MedLink MIS (`evomis`)

Усі сутності LIS у коді C# містять прямі зовнішні ключі (Foreign Keys) до таблиць ядра медичної інформаційної системи MedLink:

| Таблиця LIS (.NET / SQLite) | Колонка | Зв'язок із сутністю MedLink MIS (`evomis`) | Опис клінічного процесу |
|:----------------------------|:--------|:-------------------------------------------|:------------------------|
| `lab_orders` | `patient_id` | `mis_patient_card.id` (`ehp_patients.id`) | Картка пацієнта в МІС MedLink |
| `lab_orders` | `referral_id` | `ehe_incoming_medical_referral.id` | Електронне направлення e-Health (ServiceRequest) |
| `lab_orders` | `doctor_id` | `org_employee.id` (`ehe_employees.id`) | Лікар, який призначив лабораторні дослідження |
| `lab_orders` | `department_id` | `org_department.id` | Клінічне відділення / пункт забору зразків |
| `lab_order_samples` | `collected_by_id` | `org_employee.id` | Медсестра / процедурний лаборант, що виконав забір |
| `lab_test_results` | `verified_by_id` | `org_employee.id` | Лікар-лаборант, який підписав результат (КЕП) |
| `lab_panic_call_logs` | `notified_by_id` | `org_employee.id` | Співробітник лабораторії, що передав сигнал CITO |
| `lab_test_results` | `order_id` $\to$ `diagnostic_report` | `mis_diagnostic_report.id` | Підсумковий діагностичний звіт пацієнта в МІС |

### План прямого перенесення в монорепозиторій `evomis`:
1. Файли з `MedLink.LIS.Api\Models\` переносяться в папку `evomis\src\App.Domain\Models\Laboratory\`.
2. Контекст `MedLinkLabDbContext` об'єднується з основним контекстом `evomis\src\App.DataAccess\MedLinkDbContext.cs`.
3. Сервіс `LabNormsCascadeService` реєструється через `AddScoped` у `evomis\src\App.Business\Laboratory\`.
4. Контролери з `MedLink.LIS.Api\Controllers\` переносяться в `evomis\src\App.Api\Controllers\Laboratory\`.

---

## 3. Алгоритм багатошарового каскаду норм Delphi (C# Implementation)

У спадковій Delphi-системі (`dct_service_lab_nv`, `dct_service_lab_norm`) норми є **багатошаровим стеком з вагами пріоритетів**:

```
Рівень 3: PREGNANCY (100)      --> Найвищий пріоритет (перекриває все для вагітних: 1, 2, 3 триместри)
Рівень 2: MENSTRUAL_PHASE (80)  --> Перекриває демографію для жінок (Фолікулярна, Овуляторна, Лютеїнова, Менопауза)
Рівень 4: CLINICAL_ICD10 (60)   --> Клініко-діагностичний коридор (компенсація діабету E11, ХХН N18)
Рівень 1: DEMOGRAPHIC (40)      --> Віково-статевий шар (немовлята, діти, дорослі Ч/Ж, літні)
Рівень 0: BASELINE (10)         --> Базовий оптимум методики (універсальний запасний fallback)
```

### Реалізація в сервісі `LabNormsCascadeService.cs`:
```csharp
// Нормалізація віку до днів для медично точного порівняння (немовлята 0-28 днів проти дорослих)
double ToDays(double val, string unit) => (unit?.ToUpperInvariant()) switch
{
    "DAYS" => val,
    "MONTHS" => val * 30.4375,
    _ => val * 365.25
};

double pDays = ToDays(req.Age, req.AgeUnit);
double lFromDays = ToDays(layer.AgeFrom, layer.AgeUnit);
double lToDays = ToDays(layer.AgeTo, layer.AgeUnit);

if (pDays < lFromDays || pDays > lToDays)
{
    match = false;
    reason = $"Вік {req.Age} {req.AgeUnit} поза межами діапазону [{layer.AgeFrom}-{layer.AgeTo} {layer.AgeUnit}]";
}
```

---

## 4. Специфікація REST API контролерів .NET 8 Core

### 4.1. `LaboratoryNormsController` (Шлях: `/api/laboratory/norms`)
* **`GET /api/laboratory/norms/combinations`**:
  Повертає повний список усіх налаштованих правил шарів із бази даних SQLite.
* **`GET /api/laboratory/norms/layers?testCode=GLU&methodCode=HEX_IFCC`**:
  Повертає шари конкретної методики, впорядковані за спаданням пріоритету (`priority_order DESC`).
* **`POST /api/laboratory/norms/combinations`**:
  Зберігає або оновлює шар у базі даних SQLite (Entity Framework Core `INSERT OR UPDATE`).
* **`DELETE /api/laboratory/norms/combinations/{id}`**:
  Видаляє шар за його ідентифікатором.
* **`POST /api/laboratory/norms/resolve-cascade`**:
  Головний каскадний резолвер. Приймає параметри пацієнта та повертає виграшний шар, статус прапорця (`NORMAL`, `LOW`, `HIGH`, `CRIT_LOW`, `CRIT_HIGH`), дельта-чек алерти та повний покроковий протокол аудиту `auditTrace`.

#### Приклад запиту до резолвера:
```json
{
  "testCode": "GLU",
  "methodCode": "HEX_IFCC",
  "gender": "F",
  "age": 28,
  "ageUnit": "YEARS",
  "isPregnant": true,
  "pregnancyWeek": 20,
  "measuredValue": 5.2,
  "previousValue": 4.8
}
```

#### Приклад відповіді .NET Core сервісу:
```json
{
  "testCode": "GLU",
  "methodCode": "HEX_IFCC",
  "winningLayer": {
    "id": "REF-GLU-L3-PREG-T2",
    "priorityOrder": 100,
    "normName": "Вагітні: 2-й триместр (14-27 тиж.)",
    "normLow": 3.5,
    "normHigh": 5.3,
    "critLow": 2.2,
    "critHigh": 18.0,
    "unit": "ммоль/л"
  },
  "normLow": 3.5,
  "normHigh": 5.3,
  "critLow": 2.2,
  "critHigh": 18.0,
  "statusFlag": "NORMAL",
  "isPanicCito": false,
  "isDeltaAlert": false,
  "deltaPercent": 8.3,
  "auditTrace": [
    { "priority": 100, "layerType": "PREGNANCY", "layerName": "Вагітні: 2-й триместр (14-27 тиж.)", "isMatched": true, "reason": "Умови співпали" },
    { "priority": 80, "layerType": "MENSTRUAL_PHASE", "layerName": "Фолікулярна фаза (Жінки)", "isMatched": false, "reason": "Перекрито вищим пріоритетом вагітності" },
    { "priority": 40, "layerType": "DEMOGRAPHIC", "layerName": "Новонароджені (0-28 днів)", "isMatched": false, "reason": "Вік 28 YEARS поза межами діапазону [0-28 DAYS]" }
  ]
}
```

---

### 4.2. `LaboratoryWorklistController`
* **`GET /api/laboratory/worklist`**:
  Завантажує робочий список із поточним станом досліджень, штрихкодами, пацієнтами, результатами, нормами та дельта-чеками.
* **`POST /api/laboratory/results/{id}/update`**:
  Оновлення виміряного лаборантом значення з автоматичним перерахунком прапорців відхилення.
* **`POST /api/laboratory/results/{id}/validate`**:
  Медична валідація результату лікарем (`status = "MANUAL_VERIFIED"`, фіксація `verified_by_id`, `verified_at`).
* **`POST /api/laboratory/worklist/autoverify`**:
  Пакетна автовалідація нормальних результатів за правилами безпеки.
* **`GET /api/laboratory/panic-calls`**:
  Журнал телефонних екстрених викликів лікаря при виявленні критичних панічних значень (CITO).
* **`POST /api/laboratory/panic-calls`**:
  Реєстрація факту телефонного дзвінка лікареві у журналі CITO.
* **`POST /api/laboratory/phlebotomy/collect`**:
  Підтвердження забору біоматеріалу за штрихкодом пробірки (`COLLECTED`).

---

### 4.3. `LaboratoryQcController` (Шлях: `/api/laboratory/qc`)
* **`GET /api/laboratory/qc/measurements?param=WBC`**:
  Точки контрольних карт Леві-Дженнінгса з розрахованими $Z$-score та статусом блокування аналізатора.
* **`POST /api/laboratory/qc/measurements`**:
  Внесення контрольного вимірювання з автоматичною перевіркою правил Вестгарда ($1_{2s}, 1_{3s}, 2_{2s}, R_{4s}, 4_{1s}, 10_x$).
* **`POST /api/laboratory/qc/resolve-lockout`**:
  Зняття блокування аналізатора (Lockout) після коригувальних дій (промивка, нове калібрування).

---

### 4.4. `LaboratoryBiobankController` та `LaboratoryDictionariesController`
* **`GET /api/laboratory/biobank/cells`** — сітка архівних комірок біобанку.
* **`POST /api/laboratory/biobank/cells/place`** — розміщення зразка в архівну комірку.
* **`POST /api/laboratory/biobank/cells/remove`** — вилучення/утилізація зразка.
* **Довідники CRUD** (`biomaterials`, `tubes`, `analyzers`, `parameters`) — повне управління довідковими даними з прямою фіксацією в SQLite.

---

## 5. Інструкція із запуску, збирання та перевірки

### 5.1. Збирання проєкту:
```powershell
cd C:\__MEDLINK___\LABA\MedLink.LIS.Api
dotnet build -c Debug
```
*Результат:* `Build succeeded. 0 Warning(s). 0 Error(s).`

### 5.2. Запуск сервісу:
```powershell
cd C:\__MEDLINK___\LABA
dotnet run --project MedLink.LIS.Api
```
*Вихід у консоль:*
```
================================================================================
MedLink LIS 3.0 .NET 8 Core Web API started successfully!
REST API & Swagger UI: http://localhost:5055/swagger
Vue Quasar Frontend:   http://localhost:5055/medlink_lab_frontend/run_prototype.html
Master Specification:  http://localhost:5055/TZ_LIS_MedLink_v3_Master_Specification.html
Database:              C:\__MEDLINK___\LABA\medlink_lab_local.db
================================================================================
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5055
```

### 5.3. Перевірка працездатності через PowerShell / cURL:

#### 1. Перевірка Swagger UI:
```powershell
curl.exe -s http://localhost:5055/swagger/index.html | Select-String "<title>"
# Виведе: <title>Swagger UI</title>
```

#### 2. Перевірка завантаження 49 шарів норм з SQLite:
```powershell
(Invoke-RestMethod -Uri "http://localhost:5055/api/laboratory/norms/combinations").data.Count
# Виведе: 49
```

#### 3. Перевірка каскадного резолвінгу вагітної пацієнтки:
```powershell
$body = @{ testCode="GLU"; methodCode="HEX_IFCC"; gender="F"; age=28; isPregnant=$true; pregnancyWeek=20; measuredValue=5.2 } | ConvertTo-Json
$res = Invoke-RestMethod -Uri "http://localhost:5055/api/laboratory/norms/resolve-cascade" -Method Post -Body $body -ContentType "application/json"
$res.winningLayer.normName
# Виведе: Вагітні: 2-й триместр (14-27 тиж.)
```

#### 4. Перевірка роздачі веб-інтерфейсу на порту 5055:
```powershell
curl.exe -I http://localhost:5055/medlink_lab_frontend/run_prototype.html
# Виведе: HTTP/1.1 200 OK (Content-Length: 282777, Server: Kestrel)
```
