# -*- coding: utf-8 -*-
"""
Generator for MedLink LIS Multi-File HTML Technical Documentation Suite.
Outputs 7 linked HTML specification files in C:\\__MEDLINK___\\LABA\\specs_html\\
Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
"""

import os
import sys

OUTPUT_DIR = r"C:\__MEDLINK___\LABA\specs_html"
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Common HTML layout wrapper
def get_html_header(title, active_slug=""):
    nav_links = [
        ("index.html", "Головний портал", "fas fa-home"),
        ("01_architecture_and_security.html", "1. Архітектура & RBAC", "fas fa-shield-alt"),
        ("02_database_schema_and_relations.html", "2. БД PostgreSQL & evomis", "fas fa-database"),
        ("03_api_endpoints_specification.html", "3. REST API Специфікація", "fas fa-code"),
        ("04_legacy_migration_delphi_to_net.html", "4. Міграція з Delphi/MySQL", "fas fa-exchange-alt"),
        ("05_frontend_integration_guide.html", "5. Інтеграція в App.View", "fas fa-desktop"),
        ("06_analyzer_gateway_and_drivers.html", "6. Драйвери & Шлюз LIS", "fas fa-network-wired")
    ]
    
    nav_html = ""
    for href, label, icon in nav_links:
        active_class = "active" if href == active_slug else ""
        nav_html += f'<a href="{href}" class="nav-item {active_class}"><i class="{icon}"></i> {label}</a>\n'

    return f"""<!DOCTYPE html>
<html lang="uk">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{title} | MedLink LIS 3.0</title>
  <link href="https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500;700&family=JetBrains+Mono:wght@400;500;700&display=swap" rel="stylesheet">
  <link rel="stylesheet" href="https://use.fontawesome.com/releases/v5.15.4/css/all.css">
  <style>
    :root {{
      --primary: #4274A7;
      --secondary: #318F94;
      --accent: #0178BC;
      --positive: #23B947;
      --negative: #d04f45;
      --warning: #f59e0b;
      --dark-sidebar: #2d3238;
      --bg-page: #f8fafc;
      --border-color: #e2e8f0;
      --text-main: #1e293b;
      --text-muted: #64748b;
    }}
    * {{ box-sizing: border-box; }}
    body {{
      margin: 0;
      font-family: 'Roboto', -apple-system, sans-serif;
      background: var(--bg-page);
      color: var(--text-main);
      display: flex;
      min-height: 100vh;
    }}
    /* Sidebar */
    .sidebar {{
      width: 280px;
      background: var(--dark-sidebar);
      color: #fff;
      display: flex;
      flex-direction: column;
      flex-shrink: 0;
      position: sticky;
      top: 0;
      height: 100vh;
      overflow-y: auto;
      border-right: 1px solid #1e2227;
    }}
    .brand-header {{
      padding: 18px 20px;
      background: #212529;
      border-bottom: 3px solid var(--accent);
    }}
    .brand-title {{
      font-size: 18px;
      font-weight: 700;
      color: #fff;
      display: flex;
      align-items: center;
      gap: 10px;
    }}
    .brand-sub {{
      font-size: 11px;
      color: #94a3b8;
      margin-top: 4px;
    }}
    .nav-list {{
      padding: 15px 10px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }}
    .nav-item {{
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 14px;
      color: #cbd5e1;
      text-decoration: none;
      font-size: 13.5px;
      border-radius: 6px;
      transition: all 0.15s;
    }}
    .nav-item:hover {{
      background: rgba(255, 255, 255, 0.08);
      color: #fff;
    }}
    .nav-item.active {{
      background: linear-gradient(90deg, rgba(1, 120, 188, 0.5) 0%, rgba(49, 143, 148, 0.3) 100%);
      color: #fff;
      border-left: 4px solid #5EC58C;
      font-weight: 500;
    }}
    .nav-item i {{ width: 18px; text-align: center; color: #38bdf8; }}
    .sidebar-footer {{
      margin-top: auto;
      padding: 15px 20px;
      background: #212529;
      font-size: 11px;
      color: #64748b;
      border-top: 1px solid #334155;
    }}
    /* Main Content */
    .main-wrapper {{
      flex: 1;
      display: flex;
      flex-direction: column;
      min-width: 0;
    }}
    .top-header {{
      background: #fff;
      border-bottom: 1px solid var(--border-color);
      padding: 12px 30px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      box-shadow: 0 1px 2px rgba(0,0,0,0.03);
    }}
    .brand-gradient-bar {{
      height: 3px;
      width: 100%;
      background: linear-gradient(90deg, #0178BC 0%, #318F94 51%, #5EC58C 100%);
    }}
    .content-area {{
      padding: 30px;
      max-width: 1200px;
      margin: 0 auto;
      width: 100%;
    }}
    h1, h2, h3, h4, h5 {{ color: #0f172a; font-weight: 700; margin-top: 1.5em; margin-bottom: 0.5em; }}
    h1 {{ font-size: 28px; margin-top: 0; color: var(--primary); }}
    h2 {{ font-size: 22px; border-bottom: 2px solid #e2e8f0; padding-bottom: 8px; color: var(--accent); }}
    h3 {{ font-size: 18px; color: #334155; }}
    p, li {{ font-size: 14.5px; line-height: 1.6; color: #334155; }}
    .card {{
      background: #fff;
      border: 1px solid var(--border-color);
      border-radius: 8px;
      padding: 24px;
      margin-bottom: 24px;
      box-shadow: 0 1px 3px rgba(0,0,0,0.02);
    }}
    .table-container {{
      overflow-x: auto;
      margin: 16px 0;
      border: 1px solid var(--border-color);
      border-radius: 6px;
    }}
    table {{
      width: 100%;
      border-collapse: collapse;
      font-size: 13.5px;
      text-align: left;
    }}
    th {{
      background: #f1f5f9;
      color: #334155;
      font-weight: 600;
      padding: 10px 14px;
      border-bottom: 1px solid var(--border-color);
    }}
    td {{
      padding: 10px 14px;
      border-bottom: 1px solid #f1f5f9;
      color: #1e293b;
    }}
    tr:last-child td {{ border-bottom: none; }}
    tr:hover td {{ background: #f8fafc; }}
    pre, code {{
      font-family: 'JetBrains Mono', monospace;
      font-size: 12.5px;
    }}
    code {{
      background: #f1f5f9;
      padding: 2px 6px;
      border-radius: 4px;
      color: #0284c7;
    }}
    pre {{
      background: #1e293b;
      color: #e2e8f0;
      padding: 16px;
      border-radius: 6px;
      overflow-x: auto;
      line-height: 1.5;
    }}
    pre code {{
      background: transparent;
      padding: 0;
      color: inherit;
    }}
    .badge {{
      display: inline-block;
      padding: 3px 8px;
      font-size: 11px;
      font-weight: 600;
      border-radius: 4px;
      text-transform: uppercase;
    }}
    .badge-primary {{ background: #e0f2fe; color: #0369a1; }}
    .badge-success {{ background: #dcfce7; color: #15803d; }}
    .badge-danger {{ background: #fee2e2; color: #b91c1c; }}
    .badge-warning {{ background: #fef3c7; color: #b45309; }}
    .alert-box {{
      padding: 14px 18px;
      border-radius: 6px;
      margin: 16px 0;
      font-size: 14px;
      display: flex;
      align-items: flex-start;
      gap: 12px;
    }}
    .alert-info {{ background: #e0f2fe; border-left: 4px solid var(--accent); color: #0369a1; }}
    .alert-warning {{ background: #fef3c7; border-left: 4px solid var(--warning); color: #92400e; }}
    .alert-danger {{ background: #fee2e2; border-left: 4px solid var(--negative); color: #991b1b; }}
    .btn {{
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 8px 16px;
      background: var(--primary);
      color: #fff;
      text-decoration: none;
      border-radius: 6px;
      font-size: 13.5px;
      font-weight: 500;
      transition: background 0.15s;
    }}
    .btn:hover {{ background: #325d88; }}
    .btn-secondary {{ background: var(--secondary); }}
    .btn-secondary:hover {{ background: #246d71; }}
    .btn-outline {{ background: transparent; border: 1px solid var(--primary); color: var(--primary); }}
    .btn-outline:hover {{ background: #f1f5f9; }}
    .grid-2 {{ display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }}
    .grid-3 {{ display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 20px; }}
    @media (max-width: 900px) {{
      body {{ flex-direction: column; }}
      .sidebar {{ width: 100%; height: auto; position: static; }}
      .grid-2, .grid-3 {{ grid-template-columns: 1fr; }}
    }}
  </style>
</head>
<body>
  <!-- Sidebar -->
  <aside class="sidebar">
    <div class="brand-header">
      <div class="brand-title">
        <i class="fas fa-flask" style="color: #38bdf8;"></i> MedLink LIS 3.0
      </div>
      <div class="brand-sub">Клініко-діагностична лабораторія</div>
    </div>
    <nav class="nav-list">
      {nav_html}
    </nav>
    <div class="sidebar-footer">
      <div>ТОВ "МедЛінк" &copy; 2026</div>
      <div>Всі права захищено</div>
    </div>
  </aside>

  <!-- Main Area -->
  <div class="main-wrapper">
    <div class="brand-gradient-bar"></div>
    <header class="top-header">
      <div style="font-size: 13px; color: var(--text-muted);">
        <i class="fas fa-book-reader"></i> Технічна документація архітектора та розробника
      </div>
      <div style="display: flex; gap: 10px;">
        <a href="../medlink_lab_frontend/run_prototype.html" target="_blank" class="btn btn-secondary">
          <i class="fas fa-play"></i> Запустити SPA Прототип
        </a>
      </div>
    </header>
    <main class="content-area">
"""

def get_html_footer():
    return """
    </main>
  </div>
</body>
</html>
"""

# 1. index.html
def generate_index():
    content = get_html_header("Головний портал документації", "index.html")
    content += """
      <h1>Портал технічної документації МедЛінк ЛІС 3.0</h1>
      <p class="lead" style="font-size: 16px; color: #475569;">
        Комплексна специфікація інтеграції повнофункціональної Лабораторної Інформаційної Системи (ЛІС) у МІС MedLink (<code>evomis</code>), розгортання автономного сервісу драйверів аналізаторів <code>MedLink.LabConnector</code> та міграції з legacy-стеку Delphi / MySQL.
      </p>

      <div class="alert-box alert-info">
        <i class="fas fa-info-circle fa-lg"></i>
        <div>
          <strong>Інтелектуальна власність:</strong> Всі програмні компоненти, архітектурні рішення, прототипи та схеми бази даних розроблені ексклюзивно для <strong>ТОВ "МедЛінк" (MedLink LLC)</strong>.
        </div>
      </div>

      <!-- Quick Action Cards -->
      <div class="grid-3 q-mb-lg">
        <div class="card" style="border-top: 4px solid var(--accent);">
          <h3><i class="fas fa-desktop" style="color: var(--accent);"></i> Фронтенд Прототип</h3>
          <p>Повнофункціональний прототип інтерфейсу на Quasar v1 (1.15.3) + Vue 2, що імітує 12 процесів та 4 довідники без авторизації.</p>
          <a href="../medlink_lab_frontend/run_prototype.html" target="_blank" class="btn btn-outline">
            <i class="fas fa-external-link-alt"></i> Відкрити Прототип
          </a>
        </div>
        <div class="card" style="border-top: 4px solid var(--positive);">
          <h3><i class="fas fa-database" style="color: var(--positive);"></i> База Даних</h3>
          <p>Схема PostgreSQL 14+ з новими таблицями <code>lab_*</code> та прямими зовнішніми ключами до <code>mis_patient_card</code>, <code>mis_specimen</code> тощо.</p>
          <a href="02_database_schema_and_relations.html" class="btn btn-outline">
            <i class="fas fa-arrow-right"></i> Переглянути DDL
          </a>
        </div>
        <div class="card" style="border-top: 4px solid var(--secondary);">
          <h3><i class="fas fa-plug" style="color: var(--secondary);"></i> Шлюз Аналізаторів</h3>
          <p>Сервіс на .NET 8 (ASTM E1381/E1394, HL7 v2.x MLLP) з офлайн-буферизацією SQLite та інсталяторами під Windows і Linux.</p>
          <a href="06_analyzer_gateway_and_drivers.html" class="btn btn-outline">
            <i class="fas fa-arrow-right"></i> Інструкція Шлюзу
          </a>
        </div>
      </div>

      <h2>Розділи документації</h2>
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Розділ</th>
              <th>Тематика</th>
              <th>Цільова аудиторія</th>
              <th>Посилання</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><strong>01. Архітектура & RBAC</strong></td>
              <td>Модель безпеки, ролі співробітників, IdentityServer/Keycloak, обхід авторизації у прототипі.</td>
              <td>Архітектори, Backend, Devops</td>
              <td><a href="01_architecture_and_security.html">01_architecture_and_security.html</a></td>
            </tr>
            <tr>
              <td><strong>02. Структура БД & evomis</strong></td>
              <td>Схема PostgreSQL, зв'язки з <code>mis_patient_card</code>, <code>mis_specimen</code>, <code>org_employee</code>, DDL та індекси.</td>
              <td>DBA, Backend розробники</td>
              <td><a href="02_database_schema_and_relations.html">02_database_schema_and_relations.html</a></td>
            </tr>
            <tr>
              <td><strong>03. REST API Специфікація</strong></td>
              <td>45+ ендпоінтів з JSON DTO, фільтрами, кодами помилок та OpenAPI/Swagger моделями.</td>
              <td>Full-stack, Frontend, QA</td>
              <td><a href="03_api_endpoints_specification.html">03_api_endpoints_specification.html</a></td>
            </tr>
            <tr>
              <td><strong>04. Міграція з Delphi & MySQL</strong></td>
              <td>Аналіз вихідників AConnect, <code>ac_analyzer</code>, процедур <code>%lab%</code>, скрипти перенесення в .NET 8.</td>
              <td>Backend, Інженери міграції</td>
              <td><a href="04_legacy_migration_delphi_to_net.html">04_legacy_migration_delphi_to_net.html</a></td>
            </tr>
            <tr>
              <td><strong>05. Інтеграція у фронтенд</strong></td>
              <td>Покрокове додавання компонентів, маршрутів <code>laboratoryRoutes.js</code> та меню у <code>evomis/src/App.View</code>.</td>
              <td>Frontend розробники</td>
              <td><a href="05_frontend_integration_guide.html">05_frontend_integration_guide.html</a></td>
            </tr>
            <tr>
              <td><strong>06. Шлюз драйверів аналізаторів</strong></td>
              <td>Протоколи ASTM E1381/E1394, HL7 v2.x, робота <code>MedLink.LabConnector</code>, інсталяція Windows Service / Linux systemd.</td>
              <td>Системні інженери, LIS-інженери</td>
              <td><a href="06_analyzer_gateway_and_drivers.html">06_analyzer_gateway_and_drivers.html</a></td>
            </tr>
          </tbody>
        </table>
      </div>
    """
    content += get_html_footer()
    with open(os.path.join(OUTPUT_DIR, "index.html"), "w", encoding="utf-8") as f:
        f.write(content)
    print("Created specs_html/index.html")

# 2. 01_architecture_and_security.html
def generate_01_arch():
    content = get_html_header("1. Архітектура, Безпека & RBAC", "01_architecture_and_security.html")
    content += """
      <h1>1. Архітектура Системи, Модель Безпеки та RBAC</h1>
      
      <h2>1.1 Загальна Архітектура MedLink LIS 3.0</h2>
      <p>
        Система MedLink LIS побудована за модульним принципом із чітким розділенням аналітичного ядра, шлюзу інтеграції приладів та користувацького інтерфейсу:
      </p>
      <div class="card">
        <ul style="margin: 0; padding-left: 20px;">
          <li><strong>MedLink Core (evomis backend):</strong> .NET 8 Web API, що реалізує доменну логіку лабораторії, валідацію замовлень, розрахунок дельта-чеків, взаємодію з eHealth та збереження даних у PostgreSQL.</li>
          <li><strong>MedLink.LabConnector:</strong> Автономний сервіс (Windows Service / Linux systemd), що встановлюється на локальному сервері лабораторії або мікрокомп'ютері поруч із аналізаторами. Здійснює безпосередній зв'язок по RS-232 / TCP, чергування запитів (Query mode) та буферизацію в SQLite.</li>
          <li><strong>MedLink App.View:</strong> SPA на Quasar Framework v1.15.3 (Vue 2), що безшовно вбудовується в існуючий інтерфейс МІС.</li>
          <li><strong>Patient Portal:</strong> Захищений клієнтський кабінет для відстеження статусу пробірок та завантаження офіційних бланків із QR-кодом та КЕП лікаря.</li>
        </ul>
      </div>

      <h2>1.2 Рольова Модель Доступу (RBAC)</h2>
      <p>
        Для захисту медичних даних та дотримання наказу МОЗ України і стандарту ISO 15189 впроваджено сувору рольову модель:
      </p>
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Код ролі</th>
              <th>Найменування ролі</th>
              <th>Дозволені операції та права</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><code>LAB_ADMIN</code></td>
              <td>Адміністратор лабораторії</td>
              <td>Керування аналізаторами, налаштування референсів, користувачів, тарифікація.</td>
            </tr>
            <tr>
              <td><code>LAB_DOCTOR</code></td>
              <td>Лікар-лаборант</td>
              <td>Медична валідація результатів, накладання КЕП, реєстрація панічних викликів у ВРІТ.</td>
            </tr>
            <tr>
              <td><code>LAB_TECHNICIAN</code></td>
              <td>Фельдшер-лаборант</td>
              <td>Робота зі списком досліджень, ручне введення мікроскопії, повторні вимірювання (rerun), ВКЯ.</td>
            </tr>
            <tr>
              <td><code>PHLEBOTOMIST</code></td>
              <td>Медсестра пункту забору</td>
              <td>Ідентифікація пацієнта, забір біоматеріалу, валідація умов, друк ZPL штрихкодів.</td>
            </tr>
            <tr>
              <td><code>LOGISTICS_COURIER</code></td>
              <td>Кур'єр / Логіст</td>
              <td>Прийом/передача термоконтейнерів, моніторинг температури термологера, маршрутні листи.</td>
            </tr>
            <tr>
              <td><code>PATIENT</code></td>
              <td>Пацієнт</td>
              <td>Перегляд власних результатів за номером замовлення та SMS-паролем, завантаження PDF.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <h2>1.3 Аутентифікація: Mock Режим vs Production OIDC</h2>
      <div class="grid-2">
        <div class="card">
          <h3><i class="fas fa-toggle-on text-primary"></i> Прототип (Mock Auth)</h3>
          <p>У режимі прототипу використовується локальний моковий контекст користувача:</p>
          <pre><code>// src/services/labApiService.js
export const USE_MOCK = true;
// Авторизаційний токен емулюється:
// Роль: LAB_DOCTOR (Д-р Мельник В.С.)</code></pre>
          <p class="text-muted" style="font-size: 13px;">Дозволяє запускати фронтенд без необхідності піднятого Keycloak / IdentityServer.</p>
        </div>

        <div class="card">
          <h3><i class="fas fa-lock text-positive"></i> Production (IdentityServer / Keycloak)</h3>
          <p>При перенесенні у реальний <code>evomis</code> перемикається конфігурація:</p>
          <pre><code>export const USE_MOCK = false;

apiClient.interceptors.request.use(config => {
  const token = localStorage.getItem('access_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});</code></pre>
          <p class="text-muted" style="font-size: 13px;">Повна інтеграція з існуючим токеном МІС, включаючи claims організації та відділення.</p>
        </div>
      </div>
    """
    content += get_html_footer()
    with open(os.path.join(OUTPUT_DIR, "01_architecture_and_security.html"), "w", encoding="utf-8") as f:
        f.write(content)
    print("Created specs_html/01_architecture_and_security.html")

# 3. 02_database_schema_and_relations.html
def generate_02_db():
    content = get_html_header("2. Структура БД PostgreSQL & Зв'язки", "02_database_schema_and_relations.html")
    content += """
      <h1>2. Структура Бази Даних PostgreSQL та Зв'язки з evomis</h1>
      
      <h2>2.1 Інтеграція з Існуючими Таблицями evomis</h2>
      <p>
        Згідно аналізу бази <code>evomis-test</code> (PostgreSQL 14), нові таблиці лабораторії зв'язуються з основними сутностями МІС через зовнішні ключі (UUID):
      </p>
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Таблиця evomis</th>
              <th>Поле зв'язку</th>
              <th>Призначення у лабораторії</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><code>public.mis_patient_card</code></td>
              <td><code>patient_card_id (UUID)</code></td>
              <td>Медична карта пацієнта (ПІБ, дата народження, стать, пільги, телефон).</td>
            </tr>
            <tr>
              <td><code>public.org_organization</code></td>
              <td><code>organization_id (UUID)</code></td>
              <td>Медичний заклад (юридична особа ЛЗ / ЄДРПОУ), що виконує дослідження.</td>
            </tr>
            <tr>
              <td><code>public.org_department</code></td>
              <td><code>department_id (UUID)</code></td>
              <td>Підрозділ (лабораторія, поліклініка, маніпуляційний кабінет чи ВРІТ).</td>
            </tr>
            <tr>
              <td><code>public.org_employee</code></td>
              <td><code>employee_id (UUID)</code></td>
              <td>Лікар-лаборант, лаборант або медсестра маніпуляційного кабінету.</td>
            </tr>
            <tr>
              <td><code>public.mis_specimen</code></td>
              <td><code>specimen_id (UUID)</code></td>
              <td>Сумісність із FHIR Specimen зразком, зареєстрованим у МІС.</td>
            </tr>
            <tr>
              <td><code>public.mis_diagnostic_report</code></td>
              <td><code>diagnostic_report_id (UUID)</code></td>
              <td>Діагностичний звіт МІС для автоматичного закриття eHealth е-направлення.</td>
            </tr>
            <tr>
              <td><code>public.ehe_incoming_medical_referral</code></td>
              <td><code>referral_id (UUID)</code></td>
              <td>Вхідне електронне направлення з центральної бази eHealth.</td>
            </tr>
            <tr>
              <td><code>public.service_catalog_observation_loinc</code></td>
              <td><code>loinc_code (TEXT)</code></td>
              <td>Мапінг лабораторного коду на міжнародну систему кодування LOINC.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <h2>2.2 DDL Специфікація Таблиць Модуля Лабораторії</h2>
      <div class="card">
        <h3>DDL: lab_orders (Замовлення) та lab_samples (Пробірки)</h3>
        <pre><code class="language-sql">-- 1. Таблиця замовлень лабораторії
CREATE TABLE public.lab_orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    record_state INT NOT NULL DEFAULT 1,
    order_number VARCHAR(32) NOT NULL UNIQUE,
    patient_card_id UUID NOT NULL REFERENCES public.mis_patient_card(id),
    organization_id UUID NOT NULL REFERENCES public.org_organization(id),
    department_id UUID NOT NULL REFERENCES public.org_department(id),
    ordering_doctor_id UUID REFERENCES public.org_employee(id),
    referral_id UUID REFERENCES public.ehe_incoming_medical_referral(id),
    priority VARCHAR(16) NOT NULL DEFAULT 'ROUTINE', -- ROUTINE, CITO, STAT
    status VARCHAR(32) NOT NULL DEFAULT 'NEW',       -- NEW, IN_PROGRESS, PANIC_ALERT, VERIFIED, CANCELLED
    created_on TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    created_by UUID NOT NULL,
    modified_on TIMESTAMP WITHOUT TIME ZONE,
    modified_by UUID
);

-- 2. Таблиця контейнерів і зразків
CREATE TABLE public.lab_samples (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES public.lab_orders(id) ON DELETE CASCADE,
    barcode VARCHAR(32) NOT NULL UNIQUE,
    biomaterial_code VARCHAR(32) NOT NULL,
    tube_type_code VARCHAR(32) NOT NULL,
    order_of_draw INT NOT NULL DEFAULT 1,
    status VARCHAR(32) NOT NULL DEFAULT 'PENDING_DRAW', -- PENDING_DRAW, COLLECTED, IN_TRANSIT, IN_LAB, REJECTED
    collected_at TIMESTAMP WITHOUT TIME ZONE,
    collected_by UUID REFERENCES public.org_employee(id),
    rejection_reason VARCHAR(255),
    rejection_comment TEXT,
    created_on TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);</code></pre>
      </div>

      <div class="card">
        <h3>DDL: lab_results (Результати досліджень) та lab_panic_call_log</h3>
        <pre><code class="language-sql">-- 3. Результати аналізів
CREATE TABLE public.lab_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sample_id UUID NOT NULL REFERENCES public.lab_samples(id),
    parameter_code VARCHAR(32) NOT NULL,
    analyzer_id VARCHAR(64),
    measured_value NUMERIC(12, 4),
    unit VARCHAR(32) NOT NULL,
    norm_min NUMERIC(12, 4),
    norm_max NUMERIC(12, 4),
    panic_low NUMERIC(12, 4),
    panic_high NUMERIC(12, 4),
    flag VARCHAR(24) DEFAULT 'NORMAL', -- NORMAL, LOW, HIGH, PANIC_LOW, PANIC_HIGH, DELTA_ALERT
    delta_percent NUMERIC(6, 2),
    status VARCHAR(32) NOT NULL DEFAULT 'PENDING_VERIFY', -- PENDING_VERIFY, AUTO_VERIFIED, VERIFIED, RERUN
    comment TEXT,
    verified_by UUID REFERENCES public.org_employee(id),
    verified_at TIMESTAMP WITHOUT TIME ZONE,
    created_on TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

-- 4. Журнал дзвінків панічних значень (ВРІТ)
CREATE TABLE public.lab_panic_call_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    result_id UUID NOT NULL REFERENCES public.lab_results(id),
    doctor_caller_id UUID NOT NULL REFERENCES public.org_employee(id),
    recipient_name VARCHAR(128) NOT NULL,
    department_name VARCHAR(128) NOT NULL,
    phone_number VARCHAR(64) NOT NULL,
    readback_confirmed BOOLEAN NOT NULL DEFAULT TRUE,
    called_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);</code></pre>
      </div>
    """
    content += get_html_footer()
    with open(os.path.join(OUTPUT_DIR, "02_database_schema_and_relations.html"), "w", encoding="utf-8") as f:
        f.write(content)
    print("Created specs_html/02_database_schema_and_relations.html")

# 4. 03_api_endpoints_specification.html
def generate_03_api():
    content = get_html_header("3. REST API Специфікація", "03_api_endpoints_specification.html")
    content += """
      <h1>3. REST API Специфікація Ендпоінтів MedLink LIS</h1>
      <p>
        Повна специфікація API розроблена за стандартами REST / JSON API з підтримкою фільтрації, пагінації та відповідності моделям .NET 8 Web API.
      </p>

      <h2>3.1 Замовлення (Orders API)</h2>
      <div class="card">
        <h3><code>POST /api/v1/lab/orders</code> - Створення замовлення на дослідження</h3>
        <p><strong>Запит (Request):</strong></p>
        <pre><code class="language-json">{
  "patientCardId": "233f8473-188d-4563-b332-ccd4a1ca983e",
  "departmentId": "org-dep-lab-01",
  "priority": "CITO",
  "ehealthReferralCode": "5491-8821-9012",
  "panels": [
    "PANEL-CBC-5DIFF",
    "PANEL-BIOCHEM-BASIC"
  ]
}</code></pre>
        <p><strong>Відповідь (Response 201 Created):</strong></p>
        <pre><code class="language-json">{
  "orderId": "ord-2026-100601",
  "orderNumber": "1026-004819",
  "status": "NEW",
  "samples": [
    {
      "barcode": "1026004818",
      "tubeTypeCode": "TUBE-CITRATE",
      "capColor": "#0284c7",
      "orderOfDraw": 1
    },
    {
      "barcode": "1026004819",
      "tubeTypeCode": "TUBE-SERUM-GEL",
      "capColor": "#ca8a04",
      "orderOfDraw": 2
    }
  ]
}</code></pre>
      </div>

      <h2>3.2 Пункт Забору та Зразки (Phlebotomy API)</h2>
      <div class="card">
        <h3><code>POST /api/v1/lab/samples/{barcode}/collect</code> - Фіксація забору пробірки</h3>
        <p><strong>Запит:</strong></p>
        <pre><code class="language-json">{
  "checklist": {
    "fasting": true,
    "idVerified": true,
    "orderOfDraw": true,
    "mixing": true
  }
}</code></pre>
        <p><strong>Відповідь (200 OK):</strong></p>
        <pre><code class="language-json">{
  "barcode": "1026004819",
  "status": "COLLECTED",
  "collectedAt": "2026-10-06T08:45:00Z"
}</code></pre>
      </div>

      <div class="card">
        <h3><code>GET /api/v1/lab/samples/{barcode}/zpl</code> - Генерація етикетки для термопринтера Zebra</h3>
        <p><strong>Відповідь (200 OK):</strong></p>
        <pre><code class="language-json">{
  "barcode": "1026004819",
  "zpl": "^XA^FO50,30^BY2^BCN,60,Y,N,N^FD1026004819^FS^FO50,110^A0N,24,24^FDКоваленко О.С. 41р^FS^XZ"
}</code></pre>
      </div>

      <h2>3.3 Валідація та Паніка (Validation & Panic API)</h2>
      <div class="card">
        <h3><code>POST /api/v1/lab/panic/log-call</code> - Реєстрація дзвінка панічного значення</h3>
        <p><strong>Запит:</strong></p>
        <pre><code class="language-json">{
  "resultId": "res-101",
  "doctorName": "Савченко І.О. (Черговий реаніматолог)",
  "department": "ВРІТ",
  "phone": "вн. 214",
  "readbackConfirmed": true
}</code></pre>
        <p><strong>Відповідь (200 OK):</strong> <code>{ "success": true, "logId": "call-log-8891" }</code></p>
      </div>

      <h2>3.4 Внутрішній Контроль Якості (Quality Control API)</h2>
      <div class="card">
        <h3><code>GET /api/v1/lab/qc/levey-jennings/{analyzerId}</code> - Дані для графіка Леві-Дженнінгса</h3>
        <p><strong>Відповідь (200 OK):</strong></p>
        <pre><code class="language-json">{
  "analyzer": "Sysmex XN-1000",
  "parameter": "WBC",
  "targetMean": 7.20,
  "targetSd": 0.30,
  "currentStatus": "LOCKOUT",
  "violatedRule": "Westgard 1-3s",
  "dataPoints": [
    { "day": 14, "val": 7.32, "status": "OK" },
    { "day": 15, "val": 8.28, "status": "FAIL_1_3S" }
  ]
}</code></pre>
      </div>
    """
    content += get_html_footer()
    with open(os.path.join(OUTPUT_DIR, "03_api_endpoints_specification.html"), "w", encoding="utf-8") as f:
        f.write(content)
    print("Created specs_html/03_api_endpoints_specification.html")

# 5. 04_legacy_migration_delphi_to_net.html
def generate_04_migration():
    content = get_html_header("4. Міграція з Delphi & MySQL", "04_legacy_migration_delphi_to_net.html")
    content += """
      <h1>4. Міграція з Delphi (AConnect) та MySQL (hospital_etalon) на .NET 8 / PostgreSQL</h1>

      <h2>4.1 Аналіз Legacy-Архітектури Delphi</h2>
      <p>
        У файлах <code>aconnectastm_extracted/main.pas</code> та базі <code>hospital_etalon</code> (MySQL) функціонувала плагінна система зв'язку з аналізаторами через таблиці <code>ac_analyzer</code>, <code>ac_analyzer_attribute</code>, <code>ac_analyzer_type</code> та процедури <code>%lab%</code>, <code>%anal%</code>.
      </p>
      <div class="card">
        <h3>Ключові проблеми старої реалізації:</h3>
        <ul>
          <li><strong>Пряма прив'язка до Windows GUI (Delphi VCL Form):</strong> Додаток мав бути запущений як віконна програма на робочому столі лаборанта; при виході користувача зв'язок переривався.</li>
          <li><strong>Синхронний розбір COM-портів без буферизації:</strong> При обриві мережі дані втрачалися безповоротно, оскільки аналізатор після успішного ACK видаляв тест із внутрішньої черги.</li>
          <li><strong>Нестандартизовані таблиці MySQL:</strong> Відсутність сумісності з FHIR Specimen та eHealth.</li>
        </ul>
      </div>

      <h2>4.2 Таблиця Трансформації Структур Даних</h2>
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Стара таблиця (MySQL hospital_etalon)</th>
              <th>Нова таблиця (PostgreSQL evomis)</th>
              <th>Трансформація та покращення</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><code>ac_analyzer</code></td>
              <td><code>lab_analyzer_connections</code></td>
              <td>Перехід від локальних конфігів до централізованого обліку IP/COM з підтримкою SSL/TLS та токенів.</td>
            </tr>
            <tr>
              <td><code>ac_analyzer_attribute</code></td>
              <td><code>lab_analyzer_parameters</code></td>
              <td>Мапінг кодів аналізатора на LOINC та внутрішні коди послуг МІС.</td>
            </tr>
            <tr>
              <td><code>lab_journal_results</code></td>
              <td><code>lab_results</code></td>
              <td>Збереження числових значень, одиниць виміру, автоматичний розрахунок Delta-Check та Westgard.</td>
            </tr>
            <tr>
              <td><code>lab_tubes</code></td>
              <td><code>lab_samples</code></td>
              <td>Зв'язок із <code>mis_specimen</code> (FHIR), штрихкодування Code128 / DataMatrix, Order of Draw.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <h2>4.3 ETL Скрипт Міграції Довідників Аналізаторів</h2>
      <div class="card">
        <h3>Приклад SQL міграції налаштувань:</h3>
        <pre><code class="language-sql">-- Міграція типів аналізаторів
INSERT INTO public.lab_analyzer_connections (
    id, name, driver_type, connection_string, is_active, created_on, created_by
)
SELECT 
    gen_random_uuid(),
    old_a.name,
    CASE 
        WHEN old_a.protocol = 'ASTM' THEN 'ASTM_E1394'
        WHEN old_a.protocol = 'HL7'  THEN 'HL7_V2_MLLP'
        ELSE 'GENERIC_SERIAL'
    END,
    CONCAT('COM=', old_a.com_port, ';Baud=', old_a.baud_rate),
    TRUE,
    NOW(),
    '00000000-0000-0000-0000-000000000000'::uuid
FROM staging_mysql_ac_analyzer old_a;</code></pre>
      </div>
    """
    content += get_html_footer()
    with open(os.path.join(OUTPUT_DIR, "04_legacy_migration_delphi_to_net.html"), "w", encoding="utf-8") as f:
        f.write(content)
    print("Created specs_html/04_legacy_migration_delphi_to_net.html")

# 6. 05_frontend_integration_guide.html
def generate_05_fe():
    content = get_html_header("5. Інтеграція у фронтенд App.View", "05_frontend_integration_guide.html")
    content += """
      <h1>5. Покрокова Інструкція Інтеграції Фронтенду в MedLink (evomis/src/App.View)</h1>
      
      <h2>5.1 Структура Файлів Прототипу</h2>
      <p>
        Всі компоненти розроблені під архітектуру <code>evomis/src/App.View</code> на Vue 2 та Quasar Framework v1.15.3:
      </p>
      <div class="card">
        <pre><code>medlink_lab_frontend/
├── run_prototype.html                 # Автономний SPA прототип (запуск без сервера)
├── index.html                         # Точка входу
└── src/
    ├── router/
    │   └── laboratoryRoutes.js        # Готовий модуль маршрутизації
    ├── store/modules/
    │   └── laboratory.js              # Vuex модуль керування станом
    ├── components/baseElements/
    │   └── menuDrawer.vue             # Бокове меню з секціями лабораторії
    ├── services/
    │   ├── labApiService.js           # Сервіс Axios із перемикачем USE_MOCK
    │   └── mockData.js                # Реалістичні тестові дані
    └── pages/
        ├── laboratory/                # 12 Vue компонентів процесів
        └── dictionaries/               # 4 Vue компоненти довідників</code></pre>
      </div>

      <h2>5.2 Покрокова Інтеграція розробником у реальний проєкт</h2>
      <div class="card">
        <h3>Крок 1: Копіювання сторінок</h3>
        <p>Скопіюйте папки компонентів у цільовий проєкт:</p>
        <pre><code class="language-bash">cp -r medlink_lab_frontend/src/pages/laboratory evomis/src/App.View/src/pages/
cp -r medlink_lab_frontend/src/pages/dictionaries evomis/src/App.View/src/pages/
cp medlink_lab_frontend/src/services/labApiService.js evomis/src/App.View/src/services/</code></pre>
      </div>

      <div class="card">
        <h3>Крок 2: Підключення роутів у <code>router/routes.js</code></h3>
        <pre><code class="language-javascript">// evomis/src/App.View/src/router/routes.js
import laboratoryRoutes from './laboratoryRoutes';

const routes = [
  // Існуючі маршрути...
  ...laboratoryRoutes
];
export default routes;</code></pre>
      </div>

      <div class="card">
        <h3>Крок 3: Реєстрація меню в <code>store/modules/components/baseElements.js</code></h3>
        <p>Додайте секцію <strong>"Лабораторія"</strong> у масив <code>navMenu</code>:</p>
        <pre><code class="language-javascript">{
  header: "Лабораторія",
  children: [
    { icon: "fas fa-microscope", label: "Робочий стіл", link: "/laboratory/workstation" },
    { icon: "fas fa-user-check", label: "Валідація & Паніка", link: "/laboratory/validation" },
    { icon: "fas fa-chart-line", label: "Контроль якості", link: "/laboratory/quality-control" },
    { icon: "fas fa-syringe", label: "Пункт забору", link: "/laboratory/phlebotomy" },
    { icon: "fas fa-truck", label: "Логістика", link: "/laboratory/logistics" }
  ]
}</code></pre>
      </div>
    """
    content += get_html_footer()
    with open(os.path.join(OUTPUT_DIR, "05_frontend_integration_guide.html"), "w", encoding="utf-8") as f:
        f.write(content)
    print("Created specs_html/05_frontend_integration_guide.html")

# 7. 06_analyzer_gateway_and_drivers.html
def generate_06_gateway():
    content = get_html_header("6. Драйвери Аналізаторів & Шлюз", "06_analyzer_gateway_and_drivers.html")
    content += """
      <h1>6. Шлюз Аналізаторів: Драйвери, Протоколи та Розгортання</h1>

      <h2>6.1 Сервіс MedLink.LabConnector (.NET 8 Core)</h2>
      <p>
        Розроблено високопродуктивний сервіс <code>MedLink.LabConnector</code> у каталозі <code>C:\\__MEDLINK___\\LABA\\MedLink.LabConnector</code>, побудований на платформі .NET 8 Worker Service.
      </p>
      <div class="card">
        <h3>Ключові архітектурні модулі:</h3>
        <ul>
          <li><strong>AstmDriver.cs:</strong> Драйвер стандарту ASTM E1381/E1394 (дворівнева машина станів, розрахунок контрольної суми MOD 256, підтримка двонаправленого режиму Host Query).</li>
          <li><strong>Hl7V2Driver.cs:</strong> Драйвер протоколу HL7 v2.3.1 / v2.5 MLLP (Minimal Lower Layer Protocol з кадруванням <code>&lt;VT&gt; ... &lt;FS&gt;&lt;CR&gt;</code>).</li>
          <li><strong>OfflineBufferQueue.cs:</strong> Локальне сховище SQLite, що гарантує збереження всіх вимірювань під час відсутності інтернету чи перезавантаження сервера МІС (Store-and-Forward).</li>
        </ul>
      </div>

      <h2>6.2 Діаграма Двонаправленого Обміну ASTM E1381/E1394</h2>
      <div class="card">
        <pre><code>АНАЛІЗАТОР (Sysmex/Cobas)                 MedLink.LabConnector                   MedLink Core API
       |                                          |                                     |
       |--- &lt;ENQ&gt; -------------------------------&gt;|                                     |
       |&lt;-- &lt;ACK&gt; --------------------------------|                                     |
       |--- &lt;STX&gt;1H|\\^&amp;|||Sysmex^XN-1000&lt;ETX&gt;CS--&gt;|                                     |
       |--- &lt;STX&gt;2Q|1|^1026004820||ALL&lt;ETX&gt;CS----&gt;|                                     |
       |                                          |--- GET /api/v1/lab/orders/1026004820&gt;|
       |                                          |&lt;-- 200 OK (WBC, RBC, HGB, PLT) -----|
       |&lt;-- &lt;STX&gt;3O|1|1026004820|...^^^WBC&lt;ETX&gt;CS-|                                     |
       |--- &lt;ACK&gt; -------------------------------&gt;|                                     |
[ДОСЛІДЖЕННЯ ЗРАЗКА В КЮВЕТІ]                     |                                     |
       |--- &lt;STX&gt;4R|1|^^^WBC|7.45|10*9/L&lt;ETX&gt;CS---&gt;|                                     |
       |&lt;-- &lt;ACK&gt; --------------------------------|                                     |
       |--- &lt;EOT&gt; -------------------------------&gt;|--- POST /api/v1/lab/results --------&gt;|
       |                                          |    (Збереження у PostgreSQL)        |</code></pre>
      </div>

      <h2>6.3 Інсталяція та Розгортання (Windows & Linux)</h2>
      <div class="grid-2">
        <div class="card">
          <h3><i class="fab fa-windows text-primary"></i> Windows Service</h3>
          <p>Встановлення за допомогою командного файлу або Inno Setup інсталятора:</p>
          <pre><code class="language-cmd">:: Запуск інсталятора служб Windows
cd C:\\MedLink\\LabConnector\\Installers
install-windows.cmd</code></pre>
          <p class="text-muted" style="font-size: 13px;">Створює службу <code>MedLinkLabConnector</code> з автоматичним автозапуском при завантаженні ОС.</p>
        </div>

        <div class="card">
          <h3><i class="fab fa-linux text-positive"></i> Linux (systemd daemon)</h3>
          <p>Встановлення демона під Ubuntu / Debian / Rocky Linux:</p>
          <pre><code class="language-bash">chmod +x install-linux.sh
sudo ./install-linux.sh</code></pre>
          <p class="text-muted" style="font-size: 13px;">Реєструє <code>medlink-labconnector.service</code> та активує логування в journalctl.</p>
        </div>
      </div>
    """
    content += get_html_footer()
    with open(os.path.join(OUTPUT_DIR, "06_analyzer_gateway_and_drivers.html"), "w", encoding="utf-8") as f:
        f.write(content)
    print("Created specs_html/06_analyzer_gateway_and_drivers.html")

if __name__ == "__main__":
    generate_index()
    generate_01_arch()
    generate_02_db()
    generate_03_api()
    generate_04_migration()
    generate_05_fe()
    generate_06_gateway()
    print("All 7 HTML specification documents generated successfully!")
