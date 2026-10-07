# -*- coding: utf-8 -*-
"""
Inject file viewer modal into ТЗ_ЛІС_MedLink_v3_Повне.html and specs_html/*.html.
Expands Section 15 with full interactive buttons and popups.
"""

import os
import re

BASE_DIR = r"C:\__MEDLINK___\LABA"

# 1. Update ТЗ_ЛІС_MedLink_v3_Повне.html
tz_file = os.path.join(BASE_DIR, "ТЗ_ЛІС_MedLink_v3_Повне.html")
with open(tz_file, "r", encoding="utf-8") as f:
    tz_content = f.read()

# Add link to css in head if not present
if "file_viewer_modal.css" not in tz_content:
    tz_content = tz_content.replace("</head>", '  <link rel="stylesheet" href="file_viewer_modal.css">\n</head>')

# Add link to js before </body> if not present
if "file_viewer_modal.js" not in tz_content:
    tz_content = tz_content.replace("</body>", '  <script src="file_viewer_modal.js"></script>\n</body>')

# Replace Section 15 with enriched interactive version
new_sec15 = """<section id="sec-appendix">
      <h2>15. Додатки: Підготовлені файли довідників, міграцій та DDL для бази MedLink</h2>
      
      <p>Всі DDL-структури таблиць, індекси, зовнішні ключі до <code>evomis</code> та клінічні довідники нормалізовано, структуровано та розміщено в каталозі <code>C:\\__MEDLINK___\\LABA\\sql\\</code>. Ви можете відкрити та переглянути будь-який DDL/SQL-скрипт безпосередньо у великому скрольованому модальному вікні, скопіювати запити в буфер або завантажити:</p>

      <!-- Quick Action Toolbar -->
      <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 14px 18px; margin-bottom: 20px;">
        <div style="font-weight: 700; color: #1e293b; margin-bottom: 8px; font-size: 14px;">
          ⚡ Швидкий перегляд основних DDL скриптів та коннекторів:
        </div>
        <div style="display: flex; flex-wrap: wrap; gap: 8px;">
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/00_master_deploy_all.sql')">
            <span class="btn-icon">🚀</span> 00_master_deploy_all.sql <span class="doc-file-badge">All-in-One</span>
          </button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/01_lis_schema_core.sql')">
            <span class="btn-icon">🏛️</span> 01_lis_schema_core.sql <span class="doc-file-badge">28 Таблиць</span>
          </button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/02_lis_schema_microbiology_eucast.sql')">
            <span class="btn-icon">🧫</span> 02_lis_schema_microbiology_eucast.sql <span class="doc-file-badge">EUCAST</span>
          </button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/03_evomis_integration_views_and_fk.sql')">
            <span class="btn-icon">🔗</span> 03_evomis_integration_views_and_fk.sql <span class="doc-file-badge">evomis FK</span>
          </button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/Core/AstmDriver.cs')">
            <span class="btn-icon">⚙️</span> AstmDriver.cs <span class="doc-file-badge">.NET 8</span>
          </button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/Core/Hl7V2Driver.cs')">
            <span class="btn-icon">⚙️</span> Hl7V2Driver.cs <span class="doc-file-badge">HL7 v2</span>
          </button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('test_examples/01_astm_query_sysmex.txt')">
            <span class="btn-icon">📡</span> ASTM Query Sysmex <span class="doc-file-badge">E1394</span>
          </button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('test_examples/07_fhir_diagnostic_report_bundle.json')">
            <span class="btn-icon">🌐</span> FHIR DiagnosticReport <span class="doc-file-badge">JSON</span>
          </button>
        </div>
      </div>

      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Файл додатку / Скрипт</th>
              <th>Формат</th>
              <th>К-сть записів / Розмір</th>
              <th>Опис вмісту та зв'язків</th>
              <th>Цільовий об'єкт / Таблиця</th>
              <th>Дія</th>
            </tr>
          </thead>
          <tbody>
            <tr style="background: #f0fdf4;">
              <td><strong><code>00_master_deploy_all.sql</code></strong></td>
              <td><span class="doc-file-badge" style="background:#16a34a;">SQL Master</span></td>
              <td>1.3 КБ (9 кроків)</td>
              <td>Мастер-скрипт розгортання всієї ЛІС в єдиній стійкій транзакції psql.</td>
              <td>Вся схема MedLink LIS</td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/00_master_deploy_all.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
            <tr>
              <td><strong><code>01_lis_schema_core.sql</code></strong></td>
              <td><span class="doc-file-badge">PostgreSQL DDL</span></td>
              <td>17.7 КБ / 380 рядків</td>
              <td>Повний DDL: 28 базових таблиць (замовлення, штрихкоди, результати, валідація, ВКЯ Вестгард, біобанк, склад, логістика).</td>
              <td><code>lab_orders</code>, <code>lab_order_samples</code>, <code>lab_test_results</code> та ін.</td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/01_lis_schema_core.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
            <tr>
              <td><strong><code>02_lis_schema_microbiology_eucast.sql</code></strong></td>
              <td><span class="doc-file-badge">PostgreSQL DDL</span></td>
              <td>5.6 КБ / 120 рядків</td>
              <td>DDL мікробіології: посіви, виділені ізоляти, діаметри зон затримки росту, МІК, чутливість EUCAST (S/I/R).</td>
              <td><code>lab_micro_organisms</code>, <code>lab_eucast_breakpoints</code></td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/02_lis_schema_microbiology_eucast.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
            <tr>
              <td><strong><code>03_evomis_integration_views_and_fk.sql</code></strong></td>
              <td><span class="doc-file-badge">Views & FK</span></td>
              <td>5.2 КБ / 110 рядків</td>
              <td>Зовнішні ключі до <code>mis_patient_card</code>, <code>org_employee</code>, <code>org_department</code>, <code>ehe_incoming_medical_referral</code> та аналітичні View.</td>
              <td><code>v_patient_laboratory_history</code>, <code>v_lab_turnaround_time_analytics</code></td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/03_evomis_integration_views_and_fk.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
            <tr>
              <td><code>04_seed_biomaterials.sql</code></td>
              <td><span class="doc-file-badge" style="background:#475569;">Seed SQL</span></td>
              <td>18 записів</td>
              <td>Види біоматеріалів (венозна/капілярна кров, сироватка, сеча, ліквор, біоптати) з міграції Delphi.</td>
              <td><code>lab_biomaterial_types</code></td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/04_seed_biomaterials.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
            <tr>
              <td><code>05_seed_tube_types.sql</code></td>
              <td><span class="doc-file-badge" style="background:#475569;">Seed SQL</span></td>
              <td>12 записів</td>
              <td>Типи вакуумних пробірок, кольори кришок (HEX), об'єми, порядок забору CLSI (Order of Draw).</td>
              <td><code>lab_tube_types</code></td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/05_seed_tube_types.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
            <tr>
              <td><code>06_seed_method_types.sql</code></td>
              <td><span class="doc-file-badge" style="background:#475569;">Seed SQL</span></td>
              <td>46 записів</td>
              <td>Методики досліджень (фотометрія, ІФА, хемілюмінесценція, коагулометрія, ПЦР, проточна цитометрія).</td>
              <td><code>lab_method_types</code></td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/06_seed_method_types.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
            <tr>
              <td><code>07_seed_analyzer_types.sql</code></td>
              <td><span class="doc-file-badge" style="background:#475569;">Seed SQL</span></td>
              <td>64 записи</td>
              <td>Каталог моделей аналізаторів (Sysmex, Cobas, Mindray, Access, Maglumi, Vitros, Radiometer) з протоколами ASTM/HL7.</td>
              <td><code>lab_analyzer_types</code></td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/07_seed_analyzer_types.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
            <tr>
              <td><code>08_seed_parameters_and_profiles.sql</code></td>
              <td><span class="doc-file-badge" style="background:#475569;">Seed SQL</span></td>
              <td>19.2 КБ / Пакети & Тести</td>
              <td>Клінічні панелі (ЗАК, Біохімія, Коагулограма, Ліпідограма) з LOINC-кодами, референсами за віком/статтю та Delta-check.</td>
              <td><code>lab_test_profiles</code>, <code>lab_test_definitions</code>, <code>lab_reference_ranges</code></td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/08_seed_parameters_and_profiles.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
            <tr>
              <td><code>09_seed_microbiology_eucast.sql</code></td>
              <td><span class="doc-file-badge" style="background:#475569;">Seed SQL</span></td>
              <td>9 мікроорганізмів, 13 АБ</td>
              <td>Клінічні збудники (MRSA, E.coli, P.aeruginosa), антибіотики за класами та прикордонні значення EUCAST v14.0.</td>
              <td><code>lab_micro_organisms</code>, <code>lab_antibiotics</code>, <code>lab_eucast_breakpoints</code></td>
              <td>
                <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/09_seed_microbiology_eucast.sql')">👁 Переглянути у попапі</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="alert-box alert-info">
        <div>
          <strong>🚀 Порядок застосування міграцій в базі MedLink (evomis / evomis-test):</strong><br>
          Для виконання розгортання достатньо виконати одну команду в консолі psql або pgAdmin:<br>
          <code style="background:#0f172a; color:#38bdf8; padding: 4px 10px; border-radius: 4px; display:inline-block; margin-top: 6px;">psql -h 192.168.255.1 -U d.tanko -d evomis-test -f C:\\__MEDLINK___\\LABA\\sql\\00_master_deploy_all.sql</code><br>
          <span style="font-size: 12px; color: #64748b; margin-top: 4px; display: block;">Скрипт перевіряє наявність сутностей (<code>CREATE TABLE IF NOT EXISTS</code>), безпечно створює зв'язки та оновлює довідники без ризику втрати даних.</span>
        </div>
      </div>
    </section>"""

tz_content = re.sub(r'<section[^>]*id=["\']sec-appendix["\'][^>]*>.*?</section>', lambda m: new_sec15, tz_content, flags=re.DOTALL)

with open(tz_file, "w", encoding="utf-8") as f:
    f.write(tz_content)

print("Updated ТЗ_ЛІС_MedLink_v3_Повне.html with interactive section 15!")

# 2. Update all files in specs_html
specs_dir = os.path.join(BASE_DIR, "specs_html")
for fname in os.listdir(specs_dir):
    if fname.endswith(".html"):
        fpath = os.path.join(specs_dir, fname)
        with open(fpath, "r", encoding="utf-8") as f:
            c = f.read()
        
        modified = False
        if "file_viewer_modal.css" not in c:
            c = c.replace("</head>", '  <link rel="stylesheet" href="file_viewer_modal.css">\n</head>')
            modified = True
        
        if "file_viewer_modal.js" not in c:
            c = c.replace("</body>", '  <script src="file_viewer_modal.js"></script>\n</body>')
            modified = True
            
        # If it's 02_database_schema_and_relations.html, add quick action buttons
        if fname == "02_database_schema_and_relations.html" and "openMedlinkFileModal" not in c:
            modal_bar = """
      <div style="background: #e0f2fe; border: 1px solid #7dd3fc; border-radius: 8px; padding: 14px 18px; margin: 20px 0;">
        <div style="font-weight: 700; color: #0369a1; margin-bottom: 8px; font-size: 14px;">
          👁 Інтерактивний перегляд DDL-скриптів у скрольованому попап-вікні:
        </div>
        <div style="display: flex; flex-wrap: wrap; gap: 8px;">
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/00_master_deploy_all.sql')">🚀 00_master_deploy_all.sql (Повний реліз)</button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/01_lis_schema_core.sql')">🏛️ 01_lis_schema_core.sql (Базовий DDL 28 таблиць)</button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/02_lis_schema_microbiology_eucast.sql')">🧫 02_lis_schema_microbiology_eucast.sql</button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/03_evomis_integration_views_and_fk.sql')">🔗 03_evomis_integration_views_and_fk.sql</button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('sql/08_seed_parameters_and_profiles.sql')">📊 08_seed_parameters_and_profiles.sql</button>
        </div>
      </div>
"""
            # Insert after first header or section
            c = c.replace("</h1>", "</h1>\n" + modal_bar, 1)
            modified = True

        # If it's 06_analyzer_gateway_and_drivers.html, add connector file buttons
        if fname == "06_analyzer_gateway_and_drivers.html" and "openMedlinkFileModal" not in c:
            connector_bar = """
      <div style="background: #e0f2fe; border: 1px solid #7dd3fc; border-radius: 8px; padding: 14px 18px; margin: 20px 0;">
        <div style="font-weight: 700; color: #0369a1; margin-bottom: 8px; font-size: 14px;">
          👁 Перегляд вихідного коду драйверів та інсталяторів у попап-вікні:
        </div>
        <div style="display: flex; flex-wrap: wrap; gap: 8px;">
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/Core/AstmDriver.cs')">⚙️ AstmDriver.cs (.NET 8)</button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/Core/Hl7V2Driver.cs')">⚙️ Hl7V2Driver.cs (.NET 8)</button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/Core/OfflineBufferQueue.cs')">💾 OfflineBufferQueue.cs (SQLite)</button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/appsettings.json')">⚙️ appsettings.json</button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/Installers/install-windows.cmd')">🪟 install-windows.cmd</button>
          <button type="button" class="doc-file-btn" onclick="openMedlinkFileModal('MedLink.LabConnector/Installers/install-linux.sh')">🐧 install-linux.sh</button>
        </div>
      </div>
"""
            c = c.replace("</h1>", "</h1>\n" + connector_bar, 1)
            modified = True

        if modified:
            with open(fpath, "w", encoding="utf-8") as f:
                f.write(c)
            print(f"Updated {fname} with modal integration.")

print("All documentation pages updated successfully!")
