# MedLink LIS 3.0 — Повнофункціональна лабораторна інформаційна система

> **Правовласник:** ТОВ "МедЛінк" (MedLink LLC) © 2026. Всі права захищено.  
> **Технологічний стек:** .NET 8 Core / PostgreSQL 14+ / Quasar v1 (1.15.3) + Vue 2 / Worker Service (ASTM & HL7)  
> **База медичної системи:** Сумісно з MedLink (`evomis` / `evomis-test`)

---

## 🚀 Швидкий старт (Quick Start)

### 1. Інтерактивний прототип (Quasar + Vue 2)
Працює у стилі MedLink, без блокування IdentityServer (попередньо авторизований контекст лікаря-лаборанта):
* **HTTP:** [http://localhost:8088/medlink_lab_frontend/run_prototype.html](http://localhost:8088/medlink_lab_frontend/run_prototype.html)
* **Файловий доступ:** `C:\__MEDLINK___\LABA\medlink_lab_frontend\run_prototype.html`

### 2. Технічна документація для розробників
Структурований портал із посиланням на всі ендпоінти, моделі даних та попап-перегляд коду:
* **📖 Повне ТЗ зі скріншотами (Master Spec):** [http://localhost:8088/ТЗ_ЛІС_MedLink_v3_Master_Specification.html](http://localhost:8088/ТЗ_ЛІС_MedLink_v3_Master_Specification.html)
* **Портал документації:** [http://localhost:8088/specs_html/index.html](http://localhost:8088/specs_html/index.html)
* **Повне ТЗ ЛІС MedLink v3.0:** [http://localhost:8088/ТЗ_ЛІС_MedLink_v3_Повне.html](http://localhost:8088/ТЗ_ЛІС_MedLink_v3_Повне.html)

### 3. Запуск фонового веб-сервера
Сервер обслуговує директорію `C:\__MEDLINK___\LABA` на виділеному порту **8088** (без конфліктів із PMG 8085):
```powershell
python server_lab.py
```

---

## 📁 Структура каталогу проєкту (`C:\__MEDLINK___\LABA`)

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
Compress-Archive -Path "C:\__MEDLINK___\LABA\*" -DestinationPath "C:\__MEDLINK___\MedLink_LIS_3.0_Full_Project.zip" -CompressionLevel Optimal
```

Arхів буде містити абсолютно всі компоненти: вихідний код, прототипи, документацію з попапами, DDL-скрипти, драйвери та тестові дані.
