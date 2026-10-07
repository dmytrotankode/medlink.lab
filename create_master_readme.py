# -*- coding: utf-8 -*-
"""
Generate master README.md and README.html in C:\__MEDLINK___\LABA
"""

import os

BASE_DIR = r"C:\__MEDLINK___\LABA"

readme_md = """# MedLink LIS 3.0 — Повнофункціональна лабораторна інформаційна система

> **Правовласник:** ТОВ "МедЛінк" (MedLink LLC) © 2026. Всі права захищено.  
> **Технологічний стек:** .NET 8 Core / PostgreSQL 14+ / Quasar v1 (1.15.3) + Vue 2 / Worker Service (ASTM & HL7)  
> **База медичної системи:** Сумісно з MedLink (`evomis` / `evomis-test`)

---

## 🚀 Швидкий старт (Quick Start)

### 1. Інтерактивний прототип (Quasar + Vue 2)
Працює у стилі MedLink, без блокування IdentityServer (попередньо авторизований контекст лікаря-лаборанта):
* **HTTP:** [http://localhost:8088/medlink_lab_frontend/run_prototype.html](http://localhost:8088/medlink_lab_frontend/run_prototype.html)
* **Файловий доступ:** `C:\\__MEDLINK___\\LABA\\medlink_lab_frontend\\run_prototype.html`

### 2. Технічна документація для розробників
Структурований портал із посиланням на всі ендпоінти, моделі даних та попап-перегляд коду:
* **Портал документації:** [http://localhost:8088/specs_html/index.html](http://localhost:8088/specs_html/index.html)
* **Повне ТЗ ЛІС MedLink v3.0:** [http://localhost:8088/ТЗ_ЛІС_MedLink_v3_Повне.html](http://localhost:8088/ТЗ_ЛІС_MedLink_v3_Повне.html)

### 3. Запуск фонового веб-сервера
Сервер обслуговує директорію `C:\\__MEDLINK___\\LABA` на виділеному порту **8088** (без конфліктів із PMG 8085):
```powershell
python server_lab.py
```

---

## 📁 Структура каталогу проєкту (`C:\\__MEDLINK___\\LABA`)

| Папка / Файл | Призначення | Для кого |
|---|---|---|
| **`sql/`** | **Повний набір PostgreSQL DDL-скриптів та клінічних довідників** (10 файлів). Включає мастер-скрипт `00_master_deploy_all.sql`, базову схему `01_lis_schema_core.sql` (28 таблиць), мікробіологію EUCAST `02_lis_schema_microbiology_eucast.sql` та інтеграційні View/FK до `evomis` `03_evomis_integration_views_and_fk.sql`. | **DBA / Backend-розробник** |
| **`medlink_lab_frontend/`** | **Frontend-модуль на Quasar v1 + Vue 2**, готовий до безшовного переносу в `evomis/src/App.View`. Містить 12 сторінок процесів, 4 довідники, Vuex-стор, маршрутизацію та автономний SPA `run_prototype.html`. | **Frontend-розробник** |
| **`MedLink.LabConnector/`** | **Кросплатформний системний демон (.NET 8 C#)** для зв'язку з медичними аналізаторами. Драйвери ASTM E1381/E1394, HL7 v2.5.1 MLLP, локальний буфер SQLite, інсталятори для Windows Service та Linux Systemd. | **C# Backend / Системний інженер** |
| **`specs_html/`** | **Структурована технічна документація (7 HTML-файлів)** з архітектурою RBAC, специфікацією 45+ REST API контролерів, мапінгом міграції з Delphi/MySQL та інтерактивним попап-переглядом коду. | **Вся команда розробки / QA** |
| **`ТЗ_ЛІС_MedLink_v3_Повне.html`** | **Головне ТЗ версії 3.0 (15 розділів)**: бізнес-вимоги, опис 16 процесів, інтерактивні симулятори пробірок, нормалізація довідників, додатки з попап-вікнами. | **Product Owner / Архітектор / QA** |
| **`test_examples/`** | **Тестові дампи та емулятор приладів**: пакети ASTM (Sysmex, Cobas), HL7 ORU^R01 (Mindray), FHIR R4 Bundle та скрипт емуляції приладу `run_analyzer_simulation.py`. | **QA / Інтегратор** |
| **`file_viewer_modal.js / .css`** | **Універсальний попап-переглядач файлів і DDL** з вбудованою базою 30 файлів, підсвіткою синтаксису, копіюванням у буфер та завантаженням. | **Інтерфейс документації** |

---

## 🗄️ База даних та міграції (`sql/`)

Для розгортання повної схеми ЛІС у PostgreSQL достатньо виконати:
```sql
-- Підключення до бази evomis-test або локальної БД
psql -h 192.168.255.1 -U d.tanko -d evomis-test -f sql/00_master_deploy_all.sql
```

### Склад SQL-пакета:
1. `00_master_deploy_all.sql` — єдиний транзакційний скрипт запуску всіх частин.
2. `01_lis_schema_core.sql` — базові 28 таблиць (замовлення, штрихкоди, результати, валідація, ВКЯ Вестгард, біобанк, склад, логістика).
3. `02_lis_schema_microbiology_eucast.sql` — мікробіологія, посіви, збудники, чутливість EUCAST (S/I/R).
4. `03_evomis_integration_views_and_fk.sql` — зв'язки з `mis_patient_card`, `org_employee`, `ehe_incoming_medical_referral` та аналітичні View TAT.
5. `04_seed_biomaterials.sql` — 18 видів біоматеріалів.
6. `05_seed_tube_types.sql` — 12 типів вакуумних пробірок із порядком забору (Order of Draw).
7. `06_seed_method_types.sql` — 46 стандартизованих методик.
8. `07_seed_analyzer_types.sql` — 64 моделі аналізаторів з конфігурацією протоколів.
9. `08_seed_parameters_and_profiles.sql` — клінічні панелі, тести, LOINC-коди, референсні інтервали за статтю/віком, Delta-check.
10. `09_seed_microbiology_eucast.sql` — клінічні штами (MRSA, E.coli, P.aeruginosa), антибіотики та точки зрізу EUCAST.

---

## 🔌 Інтеграція Frontend у робочий MedLink (`evomis/src/App.View`)

1. Скопіювати `medlink_lab_frontend/src/pages/laboratory/` та `dictionaries/` у `evomis/src/App.View/src/pages/`.
2. Скопіювати `medlink_lab_frontend/src/store/modules/laboratory.js` у `evomis/src/App.View/src/store/modules/`.
3. Зареєструвати `laboratoryRoutes.js` у головному `router/routes.js`.
4. Включити пункт меню лабораторії з `menuDrawer.vue`.
5. Перемкнути `USE_MOCK = false` у `labApiService.js` для підключення до реального .NET 8 Backend API.

---

## 📦 Як заархівувати для передачі розробникам

Для створення компактного архіву виконайте команду в PowerShell:
```powershell
Compress-Archive -Path "C:\\__MEDLINK___\\LABA\\*" -DestinationPath "C:\\__MEDLINK___\\MedLink_LIS_3.0_Full_Project.zip" -CompressionLevel Optimal
```

Arхів буде містити абсолютно всі компоненти: вихідний код, прототипи, документацію з попапами, DDL-скрипти, драйвери та тестові дані.
"""

with open(os.path.join(BASE_DIR, "README.md"), "w", encoding="utf-8") as f:
    f.write(readme_md)

# Generate README.html
readme_html = f"""<!DOCTYPE html>
<html lang="uk">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>MedLink LIS 3.0 — Головний гід по проєкту</title>
  <link rel="stylesheet" href="file_viewer_modal.css">
  <style>
    body {{
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      line-height: 1.6;
      color: #1e293b;
      background: #f8fafc;
      margin: 0;
      padding: 30px 20px;
    }}
    .container {{
      max-width: 1100px;
      margin: 0 auto;
      background: #ffffff;
      padding: 40px;
      border-radius: 12px;
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
      border: 1px solid #e2e8f0;
    }}
    h1 {{ color: #0178BC; margin-top: 0; }}
    h2 {{ color: #0f172a; border-bottom: 2px solid #e2e8f0; padding-bottom: 8px; margin-top: 32px; }}
    table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
    th, td {{ border: 1px solid #cbd5e1; padding: 10px 14px; text-align: left; font-size: 14px; }}
    th {{ background: #f1f5f9; font-weight: 600; color: #334155; }}
    .btn-portal {{
      display: inline-block;
      background: #0178BC;
      color: #fff;
      padding: 10px 20px;
      border-radius: 6px;
      text-decoration: none;
      font-weight: 600;
      margin-right: 12px;
      margin-bottom: 12px;
    }}
    .btn-portal:hover {{ background: #005a8e; }}
    code {{ background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-family: monospace; font-size: 13px; }}
    pre {{ background: #0f172a; color: #e2e8f0; padding: 14px; border-radius: 8px; overflow-x: auto; font-size: 13px; }}
  </style>
</head>
<body>
  <div class="container">
    <h1>MedLink LIS 3.0 — Головний гід по проєкту</h1>
    <p><strong>ТОВ "МедЛінк" © 2026</strong> | Комплексна лабораторна інформаційна система, шлюз аналізаторів та клінічні модулі.</p>
    
    <div style="margin: 24px 0;">
      <a href="http://localhost:8088/medlink_lab_frontend/run_prototype.html" target="_blank" class="btn-portal">🚀 Відкрити інтерактивний прототип (8088)</a>
      <a href="http://localhost:8088/specs_html/index.html" target="_blank" class="btn-portal" style="background: #2c3e50;">📚 Портал документації та API</a>
      <a href="http://localhost:8088/ТЗ_ЛІС_MedLink_v3_Повне.html" target="_blank" class="btn-portal" style="background: #059669;">📋 Повне ТЗ v3.0</a>
    </div>

    <h2>Швидкий перегляд DDL-скриптів та вихідного коду (Попап):</h2>
    <div style="display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 24px;">
      <button class="doc-file-btn" onclick="openMedlinkFileModal('sql/00_master_deploy_all.sql')">🚀 00_master_deploy_all.sql</button>
      <button class="doc-file-btn" onclick="openMedlinkFileModal('sql/01_lis_schema_core.sql')">🏛️ 01_lis_schema_core.sql (Базовий DDL)</button>
      <button class="doc-file-btn" onclick="openMedlinkFileModal('sql/02_lis_schema_microbiology_eucast.sql')">🧫 02_lis_schema_microbiology_eucast.sql</button>
      <button class="doc-file-btn" onclick="openMedlinkFileModal('sql/03_evomis_integration_views_and_fk.sql')">🔗 03_evomis_integration_views_and_fk.sql</button>
      <button class="doc-file-btn" onclick="openMedlinkFileModal('sql/08_seed_parameters_and_profiles.sql')">📊 08_seed_parameters_and_profiles.sql</button>
      <button class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/Core/AstmDriver.cs')">⚙️ AstmDriver.cs (.NET 8)</button>
      <button class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/Core/Hl7V2Driver.cs')">⚙️ Hl7V2Driver.cs (.NET 8)</button>
      <button class="doc-file-btn" onclick="openMedlinkFileModal('test_examples/01_astm_query_sysmex.txt')">📡 ASTM Query (Sysmex)</button>
      <button class="doc-file-btn" onclick="openMedlinkFileModal('test_examples/07_fhir_diagnostic_report_bundle.json')">🌐 FHIR Bundle (JSON)</button>
    </div>

    <h2>Основні каталоги проєкту для команди розробників</h2>
    <table>
      <thead>
        <tr>
          <th>Каталог</th>
          <th>Призначення</th>
          <th>Ключові файли</th>
          <th>Для кого</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td><strong><code>sql/</code></strong></td>
          <td>Повний набір PostgreSQL міграцій та клінічних довідників</td>
          <td><code>00_master_deploy_all.sql</code>, <code>01_lis_schema_core.sql</code>, <code>03_evomis_integration_views_and_fk.sql</code></td>
          <td>DBA / Backend</td>
        </tr>
        <tr>
          <td><strong><code>medlink_lab_frontend/</code></strong></td>
          <td>Готовий SPA на Quasar v1 + Vue 2 для вбудовування в <code>evomis/src/App.View</code></td>
          <td><code>run_prototype.html</code>, <code>src/router/</code>, <code>src/store/</code>, <code>src/pages/laboratory/</code></td>
          <td>Frontend</td>
        </tr>
        <tr>
          <td><strong><code>MedLink.LabConnector/</code></strong></td>
          <td>Фоновий .NET 8 демон зв'язку з приладами (ASTM/HL7/COM/TCP)</td>
          <td><code>AstmDriver.cs</code>, <code>Hl7V2Driver.cs</code>, <code>install-windows.cmd</code>, <code>install-linux.sh</code></td>
          <td>.NET Backend / Інтегратор</td>
        </tr>
        <tr>
          <td><strong><code>specs_html/</code></strong></td>
          <td>Технічна документація для розробників з ендпоінтами</td>
          <td><code>index.html</code>, <code>02_database_schema_and_relations.html</code>, <code>03_api_endpoints_specification.html</code></td>
          <td>Вся команда</td>
        </tr>
        <tr>
          <td><strong><code>test_examples/</code></strong></td>
          <td>Тестові пакети протоколів та емулятор приладів</td>
          <td><code>run_analyzer_simulation.py</code>, дампи ASTM, дампи HL7, FHIR JSON</td>
          <td>QA / Інтегратор</td>
        </tr>
      </tbody>
    </table>

    <h2>Команда створення архіву для передачі:</h2>
    <pre>Compress-Archive -Path "C:\\__MEDLINK___\\LABA\\*" -DestinationPath "C:\\__MEDLINK___\\MedLink_LIS_3.0_Full_Project.zip" -CompressionLevel Optimal</pre>
  </div>

  <script src="file_viewer_modal.js"></script>
</body>
</html>
"""

with open(os.path.join(BASE_DIR, "README.html"), "w", encoding="utf-8") as f:
    f.write(readme_html)

print("Master README.md and README.html generated successfully!")
