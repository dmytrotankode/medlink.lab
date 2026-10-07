readme_path = "C:/__MEDLINK___/LABA/README_ЛАБОРАТОРІЯ_MEDLINK.html"

html_content = """<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>MedLink LIS 3.0 — Повний путівник по лабораторному модулю, процесах та довідниках</title>
<style>
:root {
  --primary: #2563eb;
  --primary-dark: #1d4ed8;
  --surface: #ffffff;
  --bg: #f8fafc;
  --border: #e2e8f0;
  --text: #0f172a;
  --muted: #475569;
  --font: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
}
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: var(--font); background: var(--bg); color: var(--text); padding: 32px; line-height: 1.6; }
.container { max-width: 1100px; margin: 0 auto; }
.hero { background: linear-gradient(135deg, #0f172a 0%, #1e3a8a 100%); color: white; padding: 36px; border-radius: 16px; margin-bottom: 32px; }
.hero h1 { font-size: 28px; margin-bottom: 8px; }
.hero p { color: #93c5fd; font-size: 16px; }
.badge-header { background: #3b82f6; color: white; padding: 4px 10px; border-radius: 999px; font-size: 11px; font-weight: 700; text-transform: uppercase; margin-bottom: 12px; display: inline-block; }
.card { background: var(--surface); border: 1px solid var(--border); border-radius: 12px; padding: 24px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
h2 { font-size: 22px; margin-bottom: 16px; color: #0f172a; border-bottom: 2px solid var(--border); padding-bottom: 8px; }
h3 { font-size: 17px; margin: 20px 0 10px; color: #1e3a8a; }
table { width: 100%; border-collapse: collapse; margin: 12px 0 20px; font-size: 13.5px; }
th { background: #f1f5f9; padding: 10px 12px; text-align: left; font-weight: 600; color: #334155; border-bottom: 2px solid var(--border); }
td { padding: 10px 12px; border-bottom: 1px solid var(--border); vertical-align: top; }
tr:hover td { background: #f8fafc; }
.badge { display: inline-block; padding: 2px 8px; border-radius: 999px; font-size: 11px; font-weight: 700; }
.badge-role { background: #e0f2fe; color: #0369a1; }
.badge-file { background: #f1f5f9; color: #334155; font-family: monospace; }
.btn { display: inline-flex; align-items: center; gap: 6px; padding: 8px 16px; border-radius: 6px; font-weight: 600; font-size: 13px; text-decoration: none; cursor: pointer; transition: 0.2s; }
.btn-primary { background: #2563eb; color: white; }
.btn-primary:hover { background: #1d4ed8; }
.btn-outline { background: white; border: 1px solid #cbd5e1; color: #1e293b; }
.btn-outline:hover { background: #f1f5f9; }
.step-box { border-left: 4px solid #3b82f6; padding-left: 16px; margin: 16px 0; }
pre { background: #0f172a; color: #e2e8f0; padding: 14px; border-radius: 8px; overflow-x: auto; font-size: 12.5px; font-family: monospace; margin: 10px 0; }
</style>
</head>
<body>
<div class="container">

  <div class="hero">
    <span class="badge-header">MedLink LIS v3.0 Master Guide</span>
    <h1>Лабораторна інформаційна система MedLink</h1>
    <p>Повний путівник по розроблених модулях, покрокових процесах, інтерактивних прототипах, тестових прикладах та підготовлених довідниках.</p>
  </div>

  <!-- Section 1: Navigation Overview -->
  <div class="card">
    <h2>1. Ключові посилання та створені артефакти</h2>
    <div style="display:flex; flex-wrap:wrap; gap:10px; margin-bottom:16px;">
      <a href="ТЗ_ЛІС_MedLink_v3_Повне.html" class="btn btn-primary" target="_blank">📄 Відкрити повне ТЗ (HTML версія)</a>
      <a href="test_examples/run_analyzer_simulation.py" class="btn btn-outline" target="_blank">▶ Запуск тесту аналізаторів</a>
      <a href="MedLink.LabConnector/MedLink.LabConnector.csproj" class="btn btn-outline" target="_blank">💻 Проект .NET Коннектора</a>
    </div>
    <p style="font-size:13.5px; color:var(--muted);">Всі компоненти розроблені компанією MedLink. Права захищені згідно з ліцензією ТОВ «МедЛінк» (2026).</p>
  </div>

  <!-- Section 2: Step-by-Step Laboratory Processes with Prototypes -->
  <div class="card">
    <h2>2. Покроковий опис 8 клінічних процесів лабораторії та прототипи</h2>
    <p>Для кожного етапу підготовлено робочий інтерактивний прототип сторінки (HTML/CSS/JS) з живими діями:</p>

    <!-- Process 1 -->
    <div class="step-box">
      <div style="display:flex; justify-content:space-between; align-items:center;">
        <h3>Процес 1: Кабінет забору біоматеріалу (Phlebotomy Station) & Маркування</h3>
        <a href="prototypes/01_phlebotomy_station.html" class="btn btn-outline" target="_blank" style="font-size:12px; padding:4px 10px;">👁 Відкрити прототип</a>
      </div>
      <p><strong>Виконавець:</strong> <span class="badge badge-role">Медсестра пункту забору</span> | <strong>Використовувані довідники:</strong> <code>01_biomaterials</code>, <code>02_tube_types</code></p>
      <ol style="padding-left:20px; font-size:13.5px; line-height:1.7; margin-top:6px;">
        <li>Пошук пацієнта за ПІБ, телефоном або кодом електронного направлення ЕСОЗ.</li>
        <li>Заповнення преаналітичного чек-листа: перевірка умов підготовки (натщесерце, прийом ліків).</li>
        <li><strong>Консолідація та Order of Draw:</strong> система об'єднує 18 призначених тестів у 3 пробірки (Цитрат &rarr; Гель &rarr; ЕДТА) за стандартом CLSI H3-A6.</li>
        <li><strong>Друк ZPL:</strong> миттєвий прямий друк етикеток на термопринтер (Code128, ПІБ, відділення) без вікон діалогу.</li>
      </ol>
    </div>

    <!-- Process 2 -->
    <div class="step-box">
      <div style="display:flex; justify-content:space-between; align-items:center;">
        <h3>Процес 2: Логістика зразків, холодовий ланцюг та бракераж</h3>
        <a href="prototypes/02_specimen_logistics.html" class="btn btn-outline" target="_blank" style="font-size:12px; padding:4px 10px;">👁 Відкрити прототип</a>
      </div>
      <p><strong>Виконавець:</strong> <span class="badge badge-role">Кур'єр / Сортувальник приймального столу</span> | <strong>Таблиці БД:</strong> <code>lab_sample_logistics</code></p>
      <ol style="padding-left:20px; font-size:13.5px; line-height:1.7; margin-top:6px;">
        <li>Формування електронного маніфесту (акта передачі) та завантаження пробірок у термосумку.</li>
        <li>Валідація температури: виїзд з філії (+3.8°C) &rarr; прибуття в лабораторію (+4.5°C). Контроль діапазону +2...+8°C.</li>
        <li>Сортування та реєстрація браку: при виявленні гемолізу (+++), хілозу чи згустку проба бракується з автосповіщенням на повторний забір.</li>
      </ol>
    </div>

    <!-- Process 3 -->
    <div class="step-box">
      <div style="display:flex; justify-content:space-between; align-items:center;">
        <h3>Процес 3: Робоче місце лаборанта «Дослідження» (Апаратна черга)</h3>
        <a href="prototypes/03_lab_workstation.html" class="btn btn-outline" target="_blank" style="font-size:12px; padding:4px 10px;">👁 Відкрити прототип</a>
      </div>
      <p><strong>Виконавець:</strong> <span class="badge badge-role">Лаборант / Фельдшер</span> | <strong>Таблиці БД:</strong> <code>lab_orders</code>, <code>lab_test_results</code></p>
      <ol style="padding-left:20px; font-size:13.5px; line-height:1.7; margin-top:6px;">
        <li>Віртуальний скролінг на 500+ зразків з фільтрами за приладами (Sysmex, Cobas, Mindray).</li>
        <li>Прийом результатів через службу <code>MedLink.LabConnector</code> по ASTM E1394 та HL7 MLLP.</li>
        <li>Швидке клавіатурне введення для ручних методик (стрілки, Enter, Esc), підсвічування патологій.</li>
      </ol>
    </div>

    <!-- Process 4 -->
    <div class="step-box">
      <div style="display:flex; justify-content:space-between; align-items:center;">
        <h3>Процес 4: Верифікація лікаря-лаборанта & Критичні алерти (Panic Values)</h3>
        <a href="prototypes/04_validation_and_panic.html" class="btn btn-outline" target="_blank" style="font-size:12px; padding:4px 10px;">👁 Відкрити прототип</a>
      </div>
      <p><strong>Виконавець:</strong> <span class="badge badge-role">Лікар-лаборант</span> | <strong>Таблиці БД:</strong> <code>lab_test_results</code>, <code>lab_reflex_rules</code></p>
      <ol style="padding-left:20px; font-size:13.5px; line-height:1.7; margin-top:6px;">
        <li><strong>Panic Alert:</strong> при виявленні загрозливих життю значень (глюкоза 26.4 ммоль/л) спливає екстрене вікно виклику лікаря.</li>
        <li><strong>Reflex-Engine:</strong> автоматичне дозамовлення пов'язаних тестів (вТ4 при патології ТТГ).</li>
        <li><strong>Накладення КЕП:</strong> лікар перевіряє дельта-чек (+42%), додає клінічний висновок та підписує КЕП для вивантаження в ЕСОЗ.</li>
      </ol>
    </div>

    <!-- Process 5 -->
    <div class="step-box">
      <div style="display:flex; justify-content:space-between; align-items:center;">
        <h3>Процес 5: Внутрішній контроль якості (ВЯК) та карти Леві-Дженнінгса</h3>
        <a href="prototypes/05_quality_control.html" class="btn btn-outline" target="_blank" style="font-size:12px; padding:4px 10px;">👁 Відкрити прототип</a>
      </div>
      <p><strong>Виконавець:</strong> <span class="badge badge-role">Менеджер з якості / Завідувач лабораторії</span> | <strong>Таблиці БД:</strong> <code>lab_qc_materials</code>, <code>lab_qc_targets</code>, <code>lab_qc_results</code></p>
      <ol style="padding-left:20px; font-size:13.5px; line-height:1.7; margin-top:6px;">
        <li>Щоденне вимірювання контрольних зразків 3-х рівнів (Level 1, Level 2, Level 3).</li>
        <li>Побудова інтерактивних карт Леві-Дженнінгса: розрахунок середнього, SD, CV%.</li>
        <li><strong>Автоматичний Lockout:</strong> порушення правила Вестгарда 1-3s (Day 15 = 8.28 > +3SD) блокує вихід пацієнтських результатів до калібрування.</li>
      </ol>
    </div>

    <!-- Process 6 -->
    <div class="step-box">
      <div style="display:flex; justify-content:space-between; align-items:center;">
        <h3>Процес 6: Кабінет пацієнта & Live-трекер етапів замовлення</h3>
        <a href="prototypes/06_patient_portal.html" class="btn btn-outline" target="_blank" style="font-size:12px; padding:4px 10px;">👁 Відкрити прототип</a>
      </div>
      <p><strong>Користувач:</strong> <span class="badge badge-role">Пацієнт</span></p>
      <ol style="padding-left:20px; font-size:13.5px; line-height:1.7; margin-top:6px;">
        <li>Вхід за номером телефону через SMS OTP або MedLink ID.</li>
        <li>Покроковий візуальний трекер виконання замовлення (*Оформлено &rarr; Забір &rarr; В дорозі &rarr; Аналіз &rarr; Готово*).</li>
        <li>Завантаження офіційного підписаного PDF-бланка з захисним QR-кодом автентифікації.</li>
      </ol>
    </div>

    <!-- Process 7 -->
    <div class="step-box">
      <div style="display:flex; justify-content:space-between; align-items:center;">
        <h3>Процес 7: Біобанк та координатна матриця архівних штативів</h3>
        <a href="prototypes/07_biobank_archive.html" class="btn btn-outline" target="_blank" style="font-size:12px; padding:4px 10px;">👁 Відкрити прототип</a>
      </div>
      <p><strong>Виконавець:</strong> <span class="badge badge-role">Архіваріус біоматеріалів</span> | <strong>Таблиці БД:</strong> <code>lab_sample_archive_racks</code>, <code>lab_sample_archive_cells</code></p>
      <ol style="padding-left:20px; font-size:13.5px; line-height:1.7; margin-top:6px;">
        <li>Облік зразків у сітці 10 &times; 10 комірок штатива з прив'язкою до морозильної камери (-20°C).</li>
        <li>Миттєвий пошук пробірки за штрихкодом: підсвічування точної координати (Ряд D, Комірка 45).</li>
        <li>Формування актів утилізації біоматеріалу після завершення регламентного терміну зберігання (30 днів).</li>
      </ol>
    </div>

    <!-- Process 8 -->
    <div class="step-box">
      <div style="display:flex; justify-content:space-between; align-items:center;">
        <h3>Процес 8: Облік реагентів на борту приладів (Lot Tracking)</h3>
        <a href="prototypes/08_reagent_inventory.html" class="btn btn-outline" target="_blank" style="font-size:12px; padding:4px 10px;">👁 Відкрити прототип</a>
      </div>
      <p><strong>Виконавець:</strong> <span class="badge badge-role">Завідувач лабораторії / Старший лаборант</span> | <strong>Таблиці БД:</strong> <code>lab_reagent_lots</code></p>
      <ol style="padding-left:20px; font-size:13.5px; line-height:1.7; margin-top:6px;">
        <li>Облік реагентних касет на борту аналізаторів: зворотний відлік кількості виконаних тестів.</li>
        <li>Контроль терміну стабільності відкритого флакона (Onboard Stability).</li>
        <li>Блокування тестів при простроченні партії реагенту та автоформування замовлення на склад.</li>
      </ol>
    </div>
  </div>

  <!-- Section 3: Prepared Dictionaries -->
  <div class="card">
    <h2>3. Підготовлені довідники для бази MedLink (evomis)</h2>
    <p>Усі довідники нормалізовано, збагачено та збережено в каталозі <code>C:\\__MEDLINK___\\LABA\\dictionaries\\</code>:</p>

    <table>
      <thead>
        <tr>
          <th>Файл</th>
          <th>Формат</th>
          <th>Записів</th>
          <th>Вміст та призначення</th>
          <th>Таблиця PostgreSQL</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td><span class="badge-file">01_biomaterials.json / .sql</span></td>
          <td>JSON / SQL</td>
          <td>18</td>
          <td>Цільна кров венозна/капілярна, сироватка, сеча, ліквор, кал, мокротиння, плевральна рідина.</td>
          <td><code>lab_biomaterial_types</code></td>
        </tr>
        <tr>
          <td><span class="badge-file">02_tube_types.json / .sql</span></td>
          <td>JSON / SQL</td>
          <td>12</td>
          <td>Типи пробірок (К2/К3 ЕДТА, Na-цитрат, Li-гепарин, активатор/гель, об'єми 1.5–7 мл, кольори кришок).</td>
          <td><code>lab_tube_types</code></td>
        </tr>
        <tr>
          <td><span class="badge-file">03_method_types.json / .sql</span></td>
          <td>JSON / SQL</td>
          <td>46</td>
          <td>Аналітичні методики: кінцева точка, кінетика IFCC, ІФА, хемілюмінесценція, коагулометрія, ПЛР.</td>
          <td><code>lab_method_types</code></td>
        </tr>
        <tr>
          <td><span class="badge-file">04_analyzer_types.json / .sql</span></td>
          <td>JSON / SQL</td>
          <td>64</td>
          <td>Каталог моделей аналізаторів (Sysmex XN/XS, Cobas, Mindray, Access, Maglumi, Vitros, Erba).</td>
          <td><code>lab_analyzer_types</code></td>
        </tr>
        <tr>
          <td><span class="badge-file">05_lab_parameters_and_profiles.json / .sql</span></td>
          <td>JSON / SQL</td>
          <td>Пакети</td>
          <td>Стандартні профілі (ЗАК, Біохімія, Коагулограма, ТТГ) з кодами LOINC, нормами та критичними порогами.</td>
          <td><code>lab_test_profiles</code> / <code>lab_test_definitions</code></td>
        </tr>
        <tr>
          <td><span class="badge-file">06_medlink_lab_schema_postgres.sql</span></td>
          <td>SQL DDL</td>
          <td>Схема</td>
          <td>Повна схема PostgreSQL (18 таблиць, індекси, зовнішні ключі, JSONB конфігурації, партиціювання).</td>
          <td>PostgreSQL <code>evomis_db</code></td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- Section 4: Test Examples & Simulator -->
  <div class="card">
    <h2>4. Тестові приклади та інструкція запуску симулятора</h2>
    <p>У каталозі <code>C:\\__MEDLINK___\\LABA\\test_examples\\</code> підготовлено набір еталонних клінічних повідомлень:</p>
    
    <ul style="font-size:13.5px; line-height:1.8; padding-left:20px; margin-bottom:14px;">
      <li><code>01_astm_query_sysmex.txt</code> — Запит рознарядки за штрихкодом від Sysmex XN-1000 (Query Mode).</li>
      <li><code>02_astm_order_response.txt</code> — Відповідь MedLink LIS з призначенням тестів (Order Message).</li>
      <li><code>03_astm_results_cobas.txt</code> — Результати гормонів (TSH, FT4) від Roche Cobas e411.</li>
      <li><code>04_astm_results_sysmex_xn.txt</code> — Повний загальний аналіз крові (WBC, RBC, HGB, HCT, PLT) від Sysmex.</li>
      <li><code>05_hl7_oru_r01_mindray.hl7</code> — Біохімічний аналіз (Глюкоза, АЛТ, АСТ, Сечовина) від Mindray BS-240.</li>
      <li><code>06_hl7_ack_response.hl7</code> — Квитанція підтвердження HL7 ACK.</li>
      <li><code>07_fhir_diagnostic_report_bundle.json</code> — Ресурс FHIR R4 Bundle з DiagnosticReport та Observation.</li>
    </ul>

    <h3>Як запустити верифікацію парсерів протоколів:</h3>
    <pre><code>cd C:\__MEDLINK___\LABA\test_examples
python run_analyzer_simulation.py</code></pre>

    <h3>Як скомпілювати та протестувати драйверний коннектор (.NET 8):</h3>
    <pre><code>cd C:\__MEDLINK___\LABA\MedLink.LabConnector
dotnet build -c Release
# Запуск у консольному тестовому режимі:
dotnet run</code></pre>
  </div>

</div>
</body>
</html>
"""

with open(readme_path, "w", encoding="utf-8") as f:
    f.write(html_content)

print(f"Generated {readme_path} successfully!")
