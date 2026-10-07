# -*- coding: utf-8 -*-
"""
Updater for README_ЛАБОРАТОРІЯ_MEDLINK.html in MedLink Quasar style.
"""

new_readme = '''<!DOCTYPE html>
<html lang="uk">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>MedLink LIS 3.0 — Повний путівник по лабораторному модулю, процесах та довідниках</title>
  <link rel="stylesheet" href="prototypes/medlink-theme.css">
  <style>
    .hero-banner {
      background: linear-gradient(135deg, #1e293b 0%, #325a84 50%, #4274A7 100%);
      color: white;
      padding: 32px;
      border-radius: 6px;
      margin-bottom: 24px;
      box-shadow: 0 4px 6px rgba(0,0,0,0.1);
      position: relative;
      overflow: hidden;
    }
    .hero-banner::after {
      content: '';
      position: absolute;
      bottom: 0;
      left: 0;
      right: 0;
      height: 4px;
      background: var(--medlink-gradient);
    }
    .hero-banner h1 {
      font-size: 26px;
      font-weight: 700;
      margin-bottom: 8px;
    }
    .hero-banner p {
      color: #e2e8f0;
      font-size: 15px;
      max-width: 900px;
    }
    .step-box {
      border-left: 4px solid var(--primary);
      background: #fafbfc;
      border-radius: 0 4px 4px 0;
      padding: 14px 18px;
      margin-bottom: 16px;
      border-top: 1px solid #e5e7eb;
      border-right: 1px solid #e5e7eb;
      border-bottom: 1px solid #e5e7eb;
    }
    .step-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
    }
    .step-title {
      font-size: 16px;
      font-weight: 700;
      color: #1f2937;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    pre.code-block {
      background: #1e293b;
      color: #f8fafc;
      padding: 12px 16px;
      border-radius: 4px;
      font-family: Consolas, monospace;
      font-size: 13px;
      overflow-x: auto;
      margin: 8px 0;
    }
  </style>
</head>
<body>
  <!-- Header Bar -->
  <header class="medlink-header">
    <a href="README_ЛАБОРАТОРІЯ_MEDLINK.html" class="medlink-brand">
      <svg id="medlink-logo" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 68 19.14" style="height:26px;">
        <defs><style>.cls-1{fill:none;}.cls-2{clip-path:url(#cp);}.cls-3{fill:#4274A7;}.cls-4{fill:#333333;}</style>
        <clipPath id="cp" transform="translate(-1.5 -1.93)"><rect class="cls-1" x="1.5" y="1.93" width="19.4" height="19.14"/></clipPath></defs>
        <g class="cls-2"><path class="cls-3" d="M8.72,17.19V14.33A.32.32,0,0,0,8.4,14H4.92V11.69H9a2.41,2.41,0,0,0,2.4-2.4V3.2a.66.66,0,0,0,.49-.62.65.65,0,0,0-1.3,0,.66.66,0,0,0,.49.62V4.87H8.75a.32.32,0,0,0-.32.32V8.67H5.54a.65.65,0,0,0-.62-.48.64.64,0,0,0-.64.64.65.65,0,0,0,.64.65A.66.66,0,0,0,5.54,9H8.4a.32.32,0,0,0,.32-.32V5.19H11V9.26A2.08,2.08,0,0,1,9,11.34H2.77a.67.67,0,0,0-.62-.49.65.65,0,1,0,0,1.3.67.67,0,0,0,.62-.49H4.6V14a.32.32,0,0,0,.32.32H8.4v2.86a.65.65,0,1,0,.81.62A.6.6,0,0,0,8.72,17.19ZM21,11.5a.66.66,0,0,1-.65.65.67.67,0,0,1-.62-.49h-6.3a2.08,2.08,0,0,0-2.08,2.08v4.07h2.32V14.33A.32.32,0,0,1,14,14h2.89a.66.66,0,0,1,.62-.49.65.65,0,0,1,.64.65.64.64,0,0,1-.64.64.65.65,0,0,1-.62-.48H14.06v3.48a.32.32,0,0,1-.32.32H11.42V19.8a.65.65,0,0,1-.16,1.27.65.65,0,0,1-.17-1.27V13.74a2.41,2.41,0,0,1,2.4-2.4h4.07V9H14.09a.33.33,0,0,1-.33-.32V5.81a.65.65,0,0,1-.48-.62.64.64,0,0,1,.64-.64.64.64,0,0,1,.17,1.26V8.67h3.47a.33.33,0,0,1,.33.32v2.32H19.8a.65.65,0,0,1,.62-.48.69.69,0,0,1,.62.67" transform="translate(-1.5 -1.93)"/></g>
        <path class="cls-4" d="M31.2,14.33V10.69l-1.78,3H28.8L27,10.74V14.3H25.7V8.23h1.16L29.13,12l2.23-3.78h1.16V14.3H31.2Z" transform="translate(-1.5 -1.93)"/>
        <path class="cls-4" d="M38.73,13.22v1.13H34V8.29h4.58V9.42H35.44v1.32h2.83v1.08H35.44v1.4Z" transform="translate(-1.5 -1.93)"/>
        <path class="cls-4" d="M46.13,13.19v2.4h-1.3V14.33H40.55v1.26h-1.3v-2.4h.25c.35,0,.62-.27.78-.81a9,9,0,0,0,.27-2.23l.05-1.89h4.69v4.93Zm-4.34-2.91a11.22,11.22,0,0,1-.19,1.84,2.32,2.32,0,0,1-.43,1.07h2.72V9.42H41.81Z" transform="translate(-1.5 -1.93)"/>
        <path class="cls-4" d="M52.47,8.26v6.07H51.1V9.39H49L49,10.66a13,13,0,0,1-.22,2.1A2.59,2.59,0,0,1,48.21,14a1.31,1.31,0,0,1-1.07.43,2.45,2.45,0,0,1-.7-.1l.08-1.19a.66.66,0,0,0,.24,0,.73.73,0,0,0,.7-.6,8.06,8.06,0,0,0,.27-1.86l.08-2.45Z" transform="translate(-1.5 -1.93)"/>
        <path class="cls-4" d="M54,8.26h1.41v6.07H54Z" transform="translate(-1.5 -1.93)"/>
        <path class="cls-4" d="M62.46,8.26v6.07h-1.4V11.85H58.31v2.48h-1.4V8.26h1.4v2.4h2.75V8.26Z" transform="translate(-1.5 -1.93)"/>
        <path class="cls-4" d="M66.35,11.9h-1v2.45H64V8.29h1.4v2.45h1L68,8.29h1.49l-2,2.94,2,3.12H67.91Z" transform="translate(-1.5 -1.93)"/>
      </svg>
      <span class="logo-tag">ЛІС 3.0</span>
    </a>

    <div style="margin-left: 20px; display: flex; gap: 8px;">
      <a href="ТЗ_ЛІС_MedLink_v3_Повне.html" class="q-btn q-btn-outline" style="height:28px; font-size:11px; text-decoration:none;">📄 Технічне Завдання (HTML)</a>
    </div>

    <div class="medlink-header-right">
      <div class="header-phone-badge">
        <span>Служба техпідтримки:</span>
        <span class="phone-num">+380 (44) 334-55-66</span>
      </div>
      <div class="header-ehealth-btn">
        <span>eHealth</span>
        <span class="status-dot"></span>
      </div>
      <div class="header-user-badge">
        <span>👤 Коваль О.П. (Лабораторія)</span>
      </div>
    </div>
  </header>

  <div style="max-width:1200px; margin:20px auto; padding:0 20px;">
    <!-- Hero Banner -->
    <div class="hero-banner">
      <span class="q-badge" style="background:rgba(255,255,255,0.2); color:white; margin-bottom:10px;">MedLink LIS v3.0 Master Hub</span>
      <h1>Лабораторна інформаційна система MedLink (ЛІС 3.0)</h1>
      <p>Єдиний інтерактивний путівник по клінічних процесах, прототипах інтерфейсів у фірмовому стилі MedLink (evomis), підготовлених довідниках для PostgreSQL та службі шлюзу приладів .NET 8.</p>
    </div>

    <!-- Section 1: Navigation & Quick Links -->
    <div class="q-card">
      <div class="q-card-header">
        <div class="q-card-title">📌 Ключові посилання та створені артефакти проєкту</div>
      </div>
      <div class="q-card-body">
        <div style="display:flex; flex-wrap:wrap; gap:10px; margin-bottom:12px;">
          <a href="ТЗ_ЛІС_MedLink_v3_Повне.html" class="q-btn q-btn-primary" target="_blank" style="text-decoration:none;">
            📄 Відкрити повне ТЗ (HTML версія, 15 розділів)
          </a>
          <a href="prototypes/01_phlebotomy_station.html" class="q-btn q-btn-outline" target="_blank" style="text-decoration:none;">
            💉 Перейти до 1-го прототипу
          </a>
          <a href="dictionaries/06_medlink_lab_schema_postgres.sql" class="q-btn q-btn-outline" target="_blank" style="text-decoration:none;">
            🗄 Схема PostgreSQL
          </a>
        </div>
        <p style="font-size:13px; color:var(--text-muted);">
          Усі права на розроблені рішення захищені ліцензією ТОВ «МедЛінк» (MedLink LLC, 2026).
        </p>
      </div>
    </div>

    <!-- Section 2: 8 Laboratory Processes & Prototypes -->
    <div class="q-card">
      <div class="q-card-header">
        <div class="q-card-title">🧪 8 Клінічних процесів та інтерактивні прототипи у стилі MedLink</div>
      </div>
      <div class="q-card-body">
        <!-- P1 -->
        <div class="step-box">
          <div class="step-header">
            <div class="step-title">💉 01. Пункт забору біоматеріалу (Phlebotomy Station) & Маркування</div>
            <a href="prototypes/01_phlebotomy_station.html" class="q-btn q-btn-primary" target="_blank" style="height:26px; font-size:11px; text-decoration:none;">👁 Відкрити екран</a>
          </div>
          <div style="font-size:13px; color:var(--text-muted); margin-bottom:6px;">
            <strong>Виконавець:</strong> Медсестра забору | <strong>Довідники:</strong> <code>01_biomaterials</code>, <code>02_tube_types</code>
          </div>
          <p style="font-size:13.5px;">
            Двоетапна ідентифікація пацієнта, автоматична консолідація 18 призначень у 3 пробірки за стандартом CLSI H3-A6 Order of Draw, швидкий друк штрихкодів ZPL на Zebra принтери.
          </p>
        </div>

        <!-- P2 -->
        <div class="step-box">
          <div class="step-header">
            <div class="step-title">🚚 02. Логістика зразків, холодовий ланцюг та бракераж</div>
            <a href="prototypes/02_specimen_logistics.html" class="q-btn q-btn-primary" target="_blank" style="height:26px; font-size:11px; text-decoration:none;">👁 Відкрити екран</a>
          </div>
          <div style="font-size:13px; color:var(--text-muted); margin-bottom:6px;">
            <strong>Виконавець:</strong> Кур'єр / Співробітник зони прийому | <strong>Таблиці:</strong> <code>lab_sample_logistics</code>
          </div>
          <p style="font-size:13.5px;">
            Електронний маніфест передачі, логування температурних датчиків термосумки (+2...+8°C), бракеражний стіл за 12 дефектами (гемоліз, хілоз, згусток) з терміновим направленням на повторний забір.
          </p>
        </div>

        <!-- P3 -->
        <div class="step-box">
          <div class="step-header">
            <div class="step-title">🔬 03. Робоче місце лаборанта «Дослідження» (Апаратна черга)</div>
            <a href="prototypes/03_lab_workstation.html" class="q-btn q-btn-primary" target="_blank" style="height:26px; font-size:11px; text-decoration:none;">👁 Відкрити екран</a>
          </div>
          <div style="font-size:13px; color:var(--text-muted); margin-bottom:6px;">
            <strong>Виконавець:</strong> Фельдшер-лаборант / Оператор | <strong>Таблиці:</strong> <code>lab_orders</code>, <code>lab_test_results</code>
          </div>
          <p style="font-size:13.5px;">
            Фільтрація за приладами (Sysmex, Cobas, Mindray), підсвітка патологій, колірні маркери, Delta-Check з попереднім візитом, клавіатурне введення для ручних методик.
          </p>
        </div>

        <!-- P4 -->
        <div class="step-box">
          <div class="step-header">
            <div class="step-title">👨‍⚕️ 04. Верифікація лікаря-лаборанта & Критичні алерти (Panic Values)</div>
            <a href="prototypes/04_validation_and_panic.html" class="q-btn q-btn-primary" target="_blank" style="height:26px; font-size:11px; text-decoration:none;">👁 Відкрити екран</a>
          </div>
          <div style="font-size:13px; color:var(--text-muted); margin-bottom:6px;">
            <strong>Виконавець:</strong> Лікар-лаборант / Завідувач КДЛ | <strong>Таблиці:</strong> <code>lab_reflex_rules</code>
          </div>
          <p style="font-size:13.5px;">
            Екстрений Panic Value баннер (глюкоза 26.4 ммоль/л) з відліком 15 хв передачі алерту реаніматологу, підписання КЕП/ЕЦП для публікації в ЕСОЗ eHealth, правила Reflex-Engine.
          </p>
        </div>

        <!-- P5 -->
        <div class="step-box">
          <div class="step-header">
            <div class="step-title">📊 05. Внутрішній контроль якості (ВЯК) & Карти Леві-Дженнінгса</div>
            <a href="prototypes/05_quality_control.html" class="q-btn q-btn-primary" target="_blank" style="height:26px; font-size:11px; text-decoration:none;">👁 Відкрити екран</a>
          </div>
          <div style="font-size:13px; color:var(--text-muted); margin-bottom:6px;">
            <strong>Виконавець:</strong> Менеджер з якості КДЛ | <strong>Таблиці:</strong> <code>lab_qc_materials</code>, <code>lab_qc_results</code>
          </div>
          <p style="font-size:13.5px;">
            Побудова карт Леві-Дженнінгса з межами ±1SD, ±2SD, ±3SD, контроль правил Вестгарда та автоматичний Lockout приладу при порушенні правила 1-3s до введення протоколу CAPA.
          </p>
        </div>

        <!-- P6 -->
        <div class="step-box">
          <div class="step-header">
            <div class="step-title">📱 06. Особистий кабінет пацієнта (Live Трекінг аналізів)</div>
            <a href="prototypes/06_patient_portal.html" class="q-btn q-btn-primary" target="_blank" style="height:26px; font-size:11px; text-decoration:none;">👁 Відкрити екран</a>
          </div>
          <div style="font-size:13px; color:var(--text-muted); margin-bottom:6px;">
            <strong>Користувач:</strong> Пацієнт
          </div>
          <p style="font-size:13.5px;">
            Живий 5-етапний прогрес-бар (Оформлено &rarr; Забір &rarr; В дорозі &rarr; Аналіз &rarr; Готово), таблиця з коридорами норм та завантаження офіційного PDF з QR-кодом автентичності.
          </p>
        </div>

        <!-- P7 -->
        <div class="step-box">
          <div class="step-header">
            <div class="step-title">🧊 07. Фізичний архів біоматеріалів (Біобанк зразків)</div>
            <a href="prototypes/07_biobank_archive.html" class="q-btn q-btn-primary" target="_blank" style="height:26px; font-size:11px; text-decoration:none;">👁 Відкрити екран</a>
          </div>
          <div style="font-size:13px; color:var(--text-muted); margin-bottom:6px;">
            <strong>Виконавець:</strong> Архіваріус лабораторії | <strong>Таблиці:</strong> <code>lab_sample_archive_racks</code>
          </div>
          <p style="font-size:13.5px;">
            Інтерактивна координатна матриця 10 &times; 10 осередків кріоштатива морозильника (-20°C), пошук пробірки за штрихкодом, акти вилучення на дообстеження та акти утилізації за регламентом.
          </p>
        </div>

        <!-- P8 -->
        <div class="step-box">
          <div class="step-header">
            <div class="step-title">🧪 08. Складський облік реагентів на борту приладів</div>
            <a href="prototypes/08_reagent_inventory.html" class="q-btn q-btn-primary" target="_blank" style="height:26px; font-size:11px; text-decoration:none;">👁 Відкрити екран</a>
          </div>
          <div style="font-size:13px; color:var(--text-muted); margin-bottom:6px;">
            <strong>Виконавець:</strong> Завідувач лабораторії / Старший лаборант | <strong>Таблиці:</strong> <code>lab_reagent_lots</code>
          </div>
          <p style="font-size:13.5px;">
            Зворотний відлік кількості залишку тестів у реагентних касетах приладів, контроль терміну придатності відкритого флакона (Onboard Stability) та автозамовлення зі складу.
          </p>
        </div>
      </div>
    </div>

    <!-- Section 3: Prepared Dictionaries -->
    <div class="q-card">
      <div class="q-card-header">
        <div class="q-card-title">🗄 Підготовлені клінічні довідники для PostgreSQL (evomis)</div>
      </div>
      <div class="q-card-body" style="padding:0;">
        <div class="q-table-container">
          <table class="q-table">
            <thead>
              <tr>
                <th>Файл довідника</th>
                <th>Формат</th>
                <th>Кількість</th>
                <th>Призначення та вміст</th>
                <th>Таблиця БД</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><code>01_biomaterials.json / .sql</code></td>
                <td>JSON + SQL</td>
                <td>18</td>
                <td>Типи біоматеріалу (венозна/капілярна кров, сеча, ліквор, кал тощо) з кодами SNOMED CT.</td>
                <td><code>lab_biomaterial_types</code></td>
              </tr>
              <tr>
                <td><code>02_tube_types.json / .sql</code></td>
                <td>JSON + SQL</td>
                <td>12</td>
                <td>Вакутайнери за ISO 6710: колір кришки, тип наповнювача, об'єм, порядок CLSI.</td>
                <td><code>lab_tube_types</code></td>
              </tr>
              <tr>
                <td><code>03_method_types.json / .sql</code></td>
                <td>JSON + SQL</td>
                <td>46</td>
                <td>Методики аналізу: кінцева точка, кінетика IFCC, ІФА, хемілюмінесценція, ПЛР Real-Time.</td>
                <td><code>lab_method_types</code></td>
              </tr>
              <tr>
                <td><code>04_analyzer_types.json / .sql</code></td>
                <td>JSON + SQL</td>
                <td>64</td>
                <td>Моделі приладів (Sysmex, Roche Cobas, Mindray, Abbott) з протоколами обміну.</td>
                <td><code>lab_analyzer_types</code></td>
              </tr>
              <tr>
                <td><code>05_lab_parameters_and_profiles.json / .sql</code></td>
                <td>JSON + SQL</td>
                <td>Пакети</td>
                <td>Профілі (ЗАК, Біохімія, Коагулограма, ТТГ) з кодами LOINC, статево-віковими нормами і панічними межами.</td>
                <td><code>lab_test_definitions</code></td>
              </tr>
              <tr>
                <td><code>06_medlink_lab_schema_postgres.sql</code></td>
                <td>SQL DDL</td>
                <td>18 таблиць</td>
                <td>Повна схема PostgreSQL evomis_db (первинні ключі uuid, JSONB, партиціювання, аудити).</td>
                <td>PostgreSQL <code>evomis_db</code></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Section 4: Testing & Deployment -->
    <div class="q-card">
      <div class="q-card-header">
        <div class="q-card-title">⚙️ Тестування симулятора протоколів та запуск коннектора</div>
      </div>
      <div class="q-card-body">
        <p style="font-size:13.5px; margin-bottom:8px;">
          <strong>1. Запуск симулятора протоколів ASTM E1394, HL7 MLLP та перевірка FHIR Bundle:</strong>
        </p>
        <pre class="code-block">cd C:\\__MEDLINK___\\LABA\\test_examples
python run_analyzer_simulation.py</pre>

        <p style="font-size:13.5px; margin:14px 0 8px;">
          <strong>2. Збірка та запуск драйверного сервісу MedLink.LabConnector (.NET 8):</strong>
        </p>
        <pre class="code-block">cd C:\\__MEDLINK___\\LABA\\MedLink.LabConnector
dotnet build -c Release
dotnet run</pre>

        <p style="font-size:13.5px; margin:14px 0 8px;">
          <strong>3. Завантаження структури та довідників у базу PostgreSQL evomis:</strong>
        </p>
        <pre class="code-block">psql -U evomis_user -d evomis_db -f C:\\__MEDLINK___\\LABA\\dictionaries\\06_medlink_lab_schema_postgres.sql
psql -U evomis_user -d evomis_db -f C:\\__MEDLINK___\\LABA\\dictionaries\\01_biomaterials.sql
psql -U evomis_user -d evomis_db -f C:\\__MEDLINK___\\LABA\\dictionaries\\02_tube_types.sql
psql -U evomis_user -d evomis_db -f C:\\__MEDLINK___\\LABA\\dictionaries\\03_method_types.sql
psql -U evomis_user -d evomis_db -f C:\\__MEDLINK___\\LABA\\dictionaries\\04_analyzer_types.sql
psql -U evomis_user -d evomis_db -f C:\\__MEDLINK___\\LABA\\dictionaries\\05_lab_parameters_and_profiles.sql</pre>
      </div>
    </div>

  </div>
</body>
</html>
'''

with open(r"c:\__MEDLINK___\LABA\README_ЛАБОРАТОРІЯ_MEDLINK.html", "w", encoding="utf-8") as f:
    f.write(new_readme)

print("Updated README_ЛАБОРАТОРІЯ_MEDLINK.html with MedLink Quasar styling!")
