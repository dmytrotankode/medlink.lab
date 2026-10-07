# -*- coding: utf-8 -*-
"""
build_master_specification_v4.py
Generates the Ultimate Master Specification for MedLink LIS 3.0:
- Full literal transcription and analysis of ТЗ_ЛІС_переосмислене_v2.docx
- All 11 tables verbatim from DOCX
- All 70 numbered headings and paragraphs verbatim from DOCX
- Unified 48 module catalog (CORE-01..15, QC-01..08, PT-01..17, INT-01..04, EXT-01..05, COM-01..04, AN-01..04)
- Solutions to all 10 Open Questions (OQ-01..10)
- 15 live system screenshots (1600x1050)
- Complete SQL suite, REST API endpoints, DDL, .NET 8 ASTM/HL7 gateway
- Links to all files, guides, scripts, standards, and prototypes
"""

import os
import sys
import docx

BASE_DIR = r"C:\__MEDLINK___\LABA"
DOCX_PATH = os.path.join(BASE_DIR, "ТЗ_ЛІС_переосмислене_v2.docx")
HTML_OUT = os.path.join(BASE_DIR, "ТЗ_ЛІС_MedLink_v3_Master_Specification.html")
MD_OUT = os.path.join(BASE_DIR, "ТЗ_ЛІС_MedLink_v3_Master_Specification.md")
HTML_ASCII = os.path.join(BASE_DIR, "TZ_LIS_MedLink_v3_Master_Specification.html")
MD_ASCII = os.path.join(BASE_DIR, "TZ_LIS_MedLink_v3_Master_Specification.md")

print(f"Reading {DOCX_PATH}...")
doc = docx.Document(DOCX_PATH)
print(f"Loaded: {len(doc.paragraphs)} paragraphs, {len(doc.tables)} tables.")

# Extract tables from DOCX
docx_tables = []
for t_idx, table in enumerate(doc.tables):
    t_rows = []
    for row in table.rows:
        t_rows.append([cell.text.replace("\n", " ").strip() for cell in row.cells])
    docx_tables.append(t_rows)
print(f"Extracted {len(docx_tables)} tables successfully.")

# Screenshot helper
def get_screen_html(name, title, desc=""):
    rel_path = f"screenshots/{name}.png"
    return f"""
    <div class="screen-card">
      <div class="screen-header">
        <span class="screen-badge"><i class="fas fa-camera"></i> ЕКРАН СИСТЕМИ (ЖИВИЙ ПРОТОТИП)</span>
        <span class="screen-title">{title}</span>
      </div>
      <div class="screen-body">
        <a href="{rel_path}" target="_blank" title="Клацніть для перегляду у високій роздільній здатності 1600x1050">
          <img src="{rel_path}" alt="{title}" class="screen-img" />
        </a>
      </div>
      <div class="screen-caption">
        <i class="fas fa-search-plus"></i> {desc if desc else f"Живий інтерфейс MedLink evomis: {title}. Клацніть для збільшення."}
      </div>
    </div>
    """

# Table to HTML helper
def table_to_html(rows, class_name="data-table"):
    if not rows:
        return ""
    html = f'<table class="{class_name}">\n<thead>\n<tr>\n'
    for c in rows[0]:
        html += f"  <th>{c}</th>\n"
    html += "</tr>\n</thead>\n<tbody>\n"
    for r in rows[1:]:
        html += "<tr>\n"
        for c in r:
            html += f"  <td>{c}</td>\n"
        html += "</tr>\n"
    html += "</tbody>\n</table>\n"
    return html

# Build HTML
print("Building comprehensive Master Specification HTML...")
html = """<!DOCTYPE html>
<html lang="uk">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Технічне Завдання: MedLink ЛІС 3.0 (Майстер-Специфікація)</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Source+Sans+Pro:wght@300;400;600;700;800&family=JetBrains+Mono:wght@400;500;600&display=swap" rel="stylesheet" />
  <style>
    :root {
      --primary: #4274A7;
      --primary-dark: #2a527a;
      --primary-light: #ecf1f6;
      --accent: #0178BC;
      --dark: #212121;
      --dark-card: #2d3238;
      --light-bg: #f5f5f5;
      --border: #e0e0e0;
      --text: #333333;
      --text-muted: #828999;
      --positive: #21ba45;
      --negative: #d04f45;
      --warning: #f2c037;
      --purple: #4274A7;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: 'Source Sans Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
      color: var(--text);
      background-color: var(--light-bg);
      line-height: 1.6;
      font-size: 14.5px;
    }
    .top-navbar {
      position: sticky;
      top: 0;
      z-index: 1000;
      background: #212121;
      color: #ffffff;
      padding: 10px 24px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      box-shadow: 0 2px 6px rgba(0,0,0,0.12);
      border-bottom: 2px solid var(--primary);
    }
    .nav-brand {
      display: flex;
      align-items: center;
      gap: 12px;
      font-size: 16px;
      font-weight: 700;
    }
    .nav-brand span { color: #82b1ff; }
    .nav-links {
      display: flex;
      gap: 12px;
      align-items: center;
    }
    .nav-btn {
      color: #e2e8f0;
      text-decoration: none;
      padding: 6px 14px;
      border-radius: 6px;
      font-size: 13px;
      font-weight: 500;
      background: rgba(255,255,255,0.08);
      transition: background 0.2s;
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }
    .nav-btn:hover { background: rgba(255,255,255,0.18); color: #fff; }
    .nav-btn.primary { background: var(--primary); color: #fff; font-weight: 700; }
    .nav-btn.primary:hover { background: var(--primary-dark); }
    .nav-btn.success { background: var(--positive); color: #fff; font-weight: 700; }

    .layout-container {
      display: flex;
      max-width: 1750px;
      margin: 0 auto;
    }
    .sidebar {
      width: 320px;
      background: #ffffff;
      border-right: 1px solid var(--border);
      height: calc(100vh - 58px);
      position: sticky;
      top: 58px;
      overflow-y: auto;
      padding: 20px 14px;
      font-size: 13px;
      flex-shrink: 0;
    }
    .sidebar h4 {
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--text-muted);
      margin: 18px 0 8px 8px;
      font-weight: 700;
    }
    .sidebar a {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 7px 10px;
      color: #334155;
      text-decoration: none;
      border-radius: 6px;
      transition: background 0.15s, color 0.15s;
      font-size: 12.5px;
      margin-bottom: 2px;
    }
    .sidebar a:hover {
      background: var(--primary-light);
      color: var(--primary-dark);
      font-weight: 600;
    }
    .sidebar a i { width: 16px; text-align: center; color: var(--primary); }

    .content {
      flex: 1;
      padding: 36px 48px;
      background: #ffffff;
      min-width: 0;
      box-shadow: 0 0 20px rgba(0,0,0,0.03);
    }
    section { margin-bottom: 50px; scroll-margin-top: 75px; }
    h1 {
      font-size: 28px;
      font-weight: 900;
      color: #0f172a;
      margin-bottom: 14px;
      line-height: 1.3;
      border-bottom: 3px solid var(--primary);
      padding-bottom: 12px;
    }
    h2 {
      font-size: 21px;
      font-weight: 800;
      color: #0f172a;
      margin: 36px 0 16px 0;
      display: flex;
      align-items: center;
      gap: 10px;
      border-bottom: 1px solid var(--border);
      padding-bottom: 8px;
    }
    h3 {
      font-size: 16.5px;
      font-weight: 700;
      color: #1e293b;
      margin: 22px 0 10px 0;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    h4 {
      font-size: 14.5px;
      font-weight: 700;
      color: #334155;
      margin: 14px 0 8px 0;
    }
    p { margin-bottom: 12px; }
    ul, ol { margin-left: 24px; margin-bottom: 14px; }
    li { margin-bottom: 6px; }

    /* Tables */
    table.data-table {
      width: 100%;
      border-collapse: collapse;
      margin: 18px 0 26px 0;
      font-size: 13.5px;
      background: #ffffff;
      border: 1px solid var(--border);
      border-radius: 8px;
      overflow: hidden;
      box-shadow: 0 2px 6px rgba(0,0,0,0.02);
    }
    table.data-table th {
      background: #f1f5f9;
      color: #0f172a;
      font-weight: 700;
      text-align: left;
      padding: 10px 14px;
      border-bottom: 2px solid var(--border);
      font-size: 12.5px;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    table.data-table td {
      padding: 10px 14px;
      border-bottom: 1px solid #f1f5f9;
      vertical-align: top;
    }
    table.data-table tr:hover { background: #f8fafc; }

    /* Badges */
    .badge {
      display: inline-block;
      padding: 3px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .badge-must { background: #fee2e2; color: #b91c1c; }
    .badge-should { background: #fef3c7; color: #b45309; }
    .badge-could { background: #e0f2fe; color: #0369a1; }
    .badge-done { background: #dcfce7; color: #15803d; }
    .badge-code { background: #f1f5f9; color: #0f766e; font-family: monospace; }

    /* Alerts */
    .alert {
      padding: 14px 18px;
      border-radius: 8px;
      margin: 18px 0;
      font-size: 14px;
      border-left: 4px solid;
    }
    .alert-info { background: #eff6ff; border-color: #3b82f6; color: #1e40af; }
    .alert-success { background: #f0fdf4; border-color: #22c55e; color: #166534; }
    .alert-warning { background: #fffbeb; border-color: #f59e0b; color: #92400e; }
    .alert-danger { background: #fef2f2; border-color: #ef4444; color: #991b1b; }

    /* Screen Card */
    .screen-card {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      border-radius: 10px;
      margin: 20px 0 28px 0;
      box-shadow: 0 6px 16px -4px rgba(0,0,0,0.08);
      overflow: hidden;
    }
    .screen-header {
      background: #0f172a;
      color: #ffffff;
      padding: 10px 16px;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .screen-badge {
      background: var(--primary);
      color: #ffffff;
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 700;
    }
    .screen-title { font-size: 13.5px; font-weight: 600; }
    .screen-body {
      padding: 8px;
      background: #f8fafc;
      text-align: center;
    }
    .screen-img {
      width: 100%;
      height: auto;
      border-radius: 6px;
      border: 1px solid #e2e8f0;
      transition: opacity 0.2s;
    }
    .screen-img:hover { opacity: 0.96; }
    .screen-caption {
      padding: 8px 16px;
      font-size: 12px;
      color: var(--text-muted);
      background: #ffffff;
      border-top: 1px solid #e2e8f0;
    }

    pre, code { font-family: 'JetBrains Mono', monospace; }
    pre {
      background: #0f172a;
      color: #e2e8f0;
      padding: 16px 20px;
      border-radius: 8px;
      overflow-x: auto;
      font-size: 12.5px;
      margin: 14px 0 20px 0;
      line-height: 1.5;
    }
    code {
      background: #f1f5f9;
      color: #0f766e;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 12.5px;
    }
    pre code { background: transparent; color: inherit; padding: 0; }

    .file-chip {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      background: #f1f5f9;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 12.5px;
      color: #0f172a;
      margin: 3px 6px 3px 0;
      text-decoration: none;
      font-family: monospace;
    }
    .file-chip:hover {
      background: #e0f2fe;
      border-color: #38bdf8;
      color: #0369a1;
    }
    .file-chip i { color: var(--primary); }
    .oq-box {
      background: #f8fafc;
      border: 1px solid #e0e0e0;
      border-left: 4px solid var(--primary);
      border-radius: 4px;
      padding: 14px 18px;
      margin: 14px 0;
    }
    .oq-header {
      font-weight: 700;
      color: var(--primary);
      margin-bottom: 6px;
      font-size: 14px;
    }
    .oq-sol {
      background: #ffffff;
      border: 1px solid #e0e0e0;
      padding: 10px 14px;
      border-radius: 4px;
      margin-top: 8px;
      color: #333333;
    }
  </style>
</head>
<body>

  <!-- TOP NAVBAR -->
  <header class="top-navbar">
    <div class="nav-brand">
      <i class="fas fa-microscope" style="color: #38bdf8; font-size: 22px;"></i>
      <span>MedLink (evomis)</span> &mdash; Лабораторна Інформаційна Система (ЛІС 3.0)
    </div>
    <div class="nav-links">
      <a href="http://localhost:8088/medlink_lab_frontend/run_prototype.html" target="_blank" class="nav-btn primary">
        <i class="fas fa-play-circle"></i> Запустити живий прототип
      </a>
      <a href="#docx-original" class="nav-btn"><i class="fas fa-file-word"></i> ТЗ v2 Оригінал</a>
      <a href="#screens" class="nav-btn"><i class="fas fa-images"></i> 15 Скріншотів</a>
      <a href="#oq-answers" class="nav-btn success"><i class="fas fa-check-circle"></i> 10 Відповідей OQ</a>
      <a href="#api" class="nav-btn"><i class="fas fa-code"></i> REST API</a>
      <a href="#database" class="nav-btn"><i class="fas fa-database"></i> DDL & SQL</a>
    </div>
  </header>

  <div class="layout-container">
    <!-- SIDEBAR NAVIGATION -->
    <nav class="sidebar">
      <h4>ТЗ v2: Оригінальний текст</h4>
      <a href="#doc-meta"><i class="fas fa-heading"></i> 1. Мета та контекст</a>
      <a href="#doc-principles"><i class="fas fa-compass"></i> 1.1. Ключові принципи</a>
      <a href="#doc-moscow"><i class="fas fa-sort-amount-down"></i> 1.2. Шкала MoSCoW</a>
      <a href="#doc-vision"><i class="fas fa-eye"></i> 2. Продуктове бачення</a>
      <a href="#doc-business"><i class="fas fa-briefcase"></i> 2.1. Бізнес-контекст</a>
      <a href="#doc-niche"><i class="fas fa-chart-pie"></i> 2.2. Ніша на ринку ЛІС</a>
      <a href="#doc-advantages"><i class="fas fa-trophy"></i> 2.3. Конкурентні переваги</a>
      <a href="#doc-audience"><i class="fas fa-users"></i> 3. Цільова аудиторія & Ролі</a>
      <a href="#doc-architecture"><i class="fas fa-sitemap"></i> 4. Архітектурні принципи</a>
      <a href="#doc-registry"><i class="fas fa-list-ol"></i> 5. Реєстр модулів (44 од.)</a>
      <a href="#doc-modules-detail"><i class="fas fa-cubes"></i> 6. Детальний опис модулів</a>
      <a href="#doc-core"><i class="fas fa-cube"></i> 6.1. Ядро ЛІС (Core)</a>
      <a href="#doc-qc"><i class="fas fa-check-double"></i> 6.2. Якість результатів</a>
      <a href="#doc-pt"><i class="fas fa-user-circle"></i> 6.3. Кабінет пацієнта (17 ф.)</a>
      <a href="#doc-ext"><i class="fas fa-bacterium"></i> 6.4. Розширена клінічна</a>
      <a href="#doc-int"><i class="fas fa-network-wired"></i> 6.5. Інтеграції</a>
      <a href="#doc-com"><i class="fas fa-boxes"></i> 6.6. Комерція та облік</a>
      <a href="#doc-an"><i class="fas fa-chart-line"></i> 6.7. Аналітика</a>
      <a href="#doc-nfr"><i class="fas fa-tachometer-alt"></i> 7. Нефункціональні вимоги</a>
      <a href="#doc-ux"><i class="fas fa-magic"></i> 8. UX/UI принципи</a>
      <a href="#doc-roadmap"><i class="fas fa-road"></i> 9. Дорожня карта релізів</a>
      <a href="#oq-answers"><i class="fas fa-question-circle"></i> 10. Відкриті питання (OQ)</a>

      <h4>Наші розширення & Скріни</h4>
      <a href="#screen-01"><i class="fas fa-laptop-medical"></i> 1. Робочий стіл лаборанта</a>
      <a href="#screen-02"><i class="fas fa-chart-area"></i> 2. Карта Леві-Дженнінгса</a>
      <a href="#screen-03"><i class="fas fa-signature"></i> 3. Валідація лікаря & КЕП</a>
      <a href="#screen-04"><i class="fas fa-user-shield"></i> 4. Кабінет пацієнта: Коридор</a>
      <a href="#screen-05"><i class="fas fa-snowflake"></i> 5. Біобанк 8х12 (-80°C)</a>
      <a href="#screen-06"><i class="fas fa-vial"></i> 6. Бактеріологія EUCAST</a>
      <a href="#screen-07"><i class="fas fa-stopwatch"></i> 7. TAT Waterfall аналітика</a>
      <a href="#screen-08"><i class="fas fa-truck"></i> 8. Холодовий ланцюг</a>
      <a href="#screen-09"><i class="fas fa-barcode"></i> 9. Штрихкоди Code128</a>
      <a href="#screen-10"><i class="fas fa-file-pdf"></i> 10. Офіційний PDF-бланк</a>
      <a href="#screen-11"><i class="fas fa-exclamation-triangle"></i> 11. CITO Panic Alert</a>
      <a href="#screen-12"><i class="fas fa-lock"></i> 12. QC Lockout розблокування</a>
      <a href="#screen-13"><i class="fas fa-th-list"></i> 13. Каталог послуг</a>
      <a href="#screen-14"><i class="fas fa-layer-group"></i> 14. Картка норми (Delphi)</a>
      <a href="#screen-15"><i class="fas fa-calculator"></i> 15. Lab Resolver симулятор</a>

      <h4>Технічні специфікації & Ресурси</h4>
      <a href="#api"><i class="fas fa-server"></i> REST API Ендпоінти</a>
      <a href="#database"><i class="fas fa-database"></i> DDL Схеми PostgreSQL/SQLite</a>
      <a href="#connector"><i class="fas fa-plug"></i> .NET 8 ASTM/HL7 Gateway</a>
      <a href="#files-catalog"><i class="fas fa-folder-open"></i> Каталог файлів проєкту</a>
    </nav>

    <!-- MAIN ARTICLE CONTENT -->
    <main class="content">

      <!-- TITLE -->
      <section id="doc-meta">
        <h1>ТЕХНІЧНЕ ЗАВДАННЯ: Веб-модуль «Лабораторна інформаційна система» (ЛІС MedLink 3.0)</h1>
        <p class="text-muted" style="font-size: 15px;">
          <strong>Продукт:</strong> МІС MedLink (evomis) та окремий standalone-продукт. <strong>Версія:</strong> 3.0 (повна консолідована версія на базі <code>ТЗ_ЛІС_переосмислене_v2.docx</code> + повний стек розроблених рішень ТОВ «МедЛінк» &copy; 2026).
        </p>
        <div class="alert alert-info">
          <strong><i class="fas fa-info-circle"></i> Статус відповідності:</strong>
          Цей документ містить <strong>100% дослівного змісту</strong> з файлу <code>ТЗ_ЛІС_переосмислене_v2.docx</code> (усі 70 розділів і підрозділів, усі 11 таблиць, повний реєстр 44 модулів + 17 функцій кабінету пацієнта) + <strong>вичерпні відповіді на всі 10 відкритих питань (OQ-01..10)</strong> + <strong>15 високоякісних скріншотів працюючого прототипу</strong> + <strong>повні DDL міграції, REST API специфікацію та посилання на всі вихідні файли</strong>.
        </div>
      </section>

      <!-- SECTION 1 -->
      <section id="doc-principles">
        <h2>1. Мета та контекст документа</h2>
        <p>
          Цей документ є переосмисленою та структурованою версією технічного завдання на модуль «Лабораторна інформаційна система» (ЛІС), що консолідує три джерела, накопичені в процесі аналізу ринку:
        </p>
        <ul>
          <li>початкове функціональне ТЗ вікна лаборанта «Дослідження» та картки «Довідник послуг» веб-МІС;</li>
          <li>порівняльний gap-аналіз функціоналу з промисловими LIS-рішеннями STARLIS (фокус: контроль якості, автоверифікація, delta-check, reflex-тестування) та LISmart компанії NeoTechLab (фокус: мікробіологія, апаратні інтеграції, фінансовий та складський облік, HL7);</li>
          <li>огляд демонстраційних матеріалів (відео та скріншоти) ЛІС початкового рівня, що ілюструє мінімально необхідний набір сценаріїв роботи лабораторії (майстер запуску, довідники, введення результатів, базовий QC, друк, розсилка).</li>
        </ul>
        <p>
          На відміну від початкового ТЗ, яке було побудоване як послідовність доповнень («доопрацьоване», з розділами-нашаруваннями по кожному джерелу), цей документ переорганізує весь накопичений матеріал у єдину, несуперечливу структуру: від продуктового бачення — до реєстру модулів з пріоритетами — до детальних вимог. Мета — дати власнику продукту компактний, повний і однозначний інструмент для ухвалення рішень щодо обсягу MVP та черговості релізів.
        </p>

        <h3>1.1. Ключові принципи переосмислення</h3>
        <ul>
          <li><strong>Пріоритет — малі та середні лабораторії.</strong> Функціонал структуровано так, щоб базовий продукт (MVP) закривав 100% потреб лабораторії з 1–5 робочими місцями без надлишкової складності корпоративних LIS-рішень; розширені можливості (мікробіологія, генетика, повноцінний складський облік, фіскалізація) винесені у пізніші релізи або позначені як опційні.</li>
          <li><strong>Веб-first, без десктопної версії.</strong> На відміну від початкового ТЗ ЛІС, яке розглядало паралельну desktop-версію, ця редакція фіксує розробку виключно як веб-застосунок (SaaS/cloud), що відповідає стратегії МІС-платформи та знижує вартість підтримки.</li>
          <li><strong>Кабінет пацієнта як точка диференціації.</strong> Жодне з проаналізованих джерел (базове ТЗ, STARLIS, LISmart, демо-ЛІС) не описує кабінет пацієнта — цей розділ розроблено «з нуля» за практиками провідних лабораторних мереж (Synevo, Діла, Сінево, Invitro) та адаптовано під потреби МІС.</li>
          <li><strong>Максимальне охоплення модулів для рішення власника продукту.</strong> Розділ 5 містить зведений реєстр із 44 модулів/під-модулів із пріоритетом за шкалою MoSCoW та орієнтовним релізом — свідомо ширше за мінімальний MVP, щоб власник продукту бачив повну карту можливостей і міг усвідомлено відсікати зайве.</li>
          <li><strong>Українська специфіка ринку.</strong> Враховано інтеграцію з eHealth/ЕСОЗ (Service Request &rarr; DiagnosticReport, підпис КЕП), протоколи аналізаторів, поширені в українських лабораторіях (ASTM, HL7 v2.x), вимоги до фіскалізації платних послуг (РРО) та мовну/форматну локалізацію.</li>
        </ul>

        <h3 id="doc-moscow">1.2. Шкала пріоритетів (MoSCoW)</h3>
        """ + table_to_html(docx_tables[0]) + """
      </section>

      <!-- SECTION 2 -->
      <section id="doc-vision">
        <h2>2. Продуктове бачення та ринкове позиціонування</h2>
        
        <h3 id="doc-business">2.1. Бізнес-контекст</h3>
        <p>
          Продукт розробляється діючою МІС-компанією як додатковий модуль екосистеми — з подвійною моделлю виходу на ринок:
        </p>
        <ol>
          <li><strong>Вбудований модуль</strong> — для існуючих клієнтів МІС, які вже ведуть амбулаторний/стаціонарний прийом та потребують закриття лабораторного циклу без інтеграції зі сторонньою ЛІС (єдиний обліковий запис, спільна картка пацієнта, наскрізна робота з ЕСОЗ).</li>
          <li><strong>Самостійний продукт (standalone LIS)</strong> — для лабораторій, що не є клієнтами МІС (приватні лабораторні кабінети, невеликі мережі, лабораторії при клініках на інших МІС), із власною реєстрацією, білінгом та інтеграцією через API/HL7 з довільною зовнішньою МІС.</li>
        </ol>
        <p>
          Така модель дозволяє одночасно підвищити utility (і, відповідно, утримання) для поточної бази МІС та відкрити новий канал доходу від лабораторій, які раніше не були охоплені жодним продуктом компанії.
        </p>

        <h3 id="doc-niche">2.2. Ніша на ринку ЛІС в Україні</h3>
        <p>
          Аналіз наявних рішень (STARLIS, LISmart/NeoTechLab, продукти класу «light LIS» на кшталт розглянутої демо-версії) показує поляризацію ринку:
        </p>
        <ul>
          <li>Продукти рівня STARLIS/LISmart орієнтовані на великі лабораторні мережі та лабораторії з акредитацією — глибокий QC, мікробіологія, фінансовий/складський облік, але висока вартість впровадження, складне навчання персоналу та, як правило, desktop-орієнтована архітектура або гібридна модель з обмеженим web-функціоналом.</li>
          <li>Продукти рівня «light LIS» (демо-версія, розд. 12 аналізу) закривають базові потреби (довідники, введення результатів, найпростіший QC), але не мають сучасного веб-інтерфейсу, кабінету пацієнта, eHealth-інтеграції та автоверифікації — тобто не відповідають очікуванням лабораторії 2026 року.</li>
          <li><strong>Помітно порожня ніша:</strong> сучасна, повністю веб-орієнтована ЛІС середнього рівня складності — з клінічно довірчими механізмами (автоверифікація, delta-check, критичні сповіщення) та зручним кабінетом пацієнта «з коробки», але без надлишкової складності enterprise-рішень.</li>
        </ul>
        <p>
          Саме в цю нішу — «light-to-mid LIS» з web-first підходом, вбудованою якістю результатів і пацієнтським досвідом — і позиціонується цей продукт.
        </p>

        <h3 id="doc-advantages">2.3. Конкурентні переваги, закладені в ТЗ</h3>
        """ + table_to_html(docx_tables[1]) + """
      </section>

      <!-- SECTION 3 -->
      <section id="doc-audience">
        <h2>3. Цільова аудиторія та ролі</h2>

        <h3>3.1. Профіль цільової лабораторії</h3>
        <p>
          Основний сегмент — клініко-діагностичні лабораторії малого та середнього розміру: від одного робочого місця лікаря-лаборанта до лабораторії з кількома підрозділами й точками забору матеріалу, без власного IT-відділу, з обмеженим бюджетом на впровадження та навчання. Система повинна залишатися корисною і для великих лабораторних мереж, але глибока функціональність для них (розд. 5, категорії «Розширена клінічна» та «Комерція та облік») є опційною надбудовою, а не обов'язковою умовою запуску.
        </p>

        <h3>3.2. Ролі користувачів</h3>
        """ + table_to_html(docx_tables[2]) + """
      </section>

      <!-- SECTION 4 -->
      <section id="doc-architecture">
        <h2>4. Архітектурні принципи рішення</h2>

        <h3>4.1. Модель розгортання</h3>
        <p>
          Система розробляється як веб-застосунок (cloud/SaaS) без десктопного клієнта. Доступ — через браузер з будь-якого пристрою (десктоп, планшет, смартфон), з адаптивною версткою. Дані зберігаються централізовано, з автоматичним резервним копіюванням на стороні провайдера.
        </p>

        <h3>4.2. Контекст інтеграцій</h3>
        <ul>
          <li><strong>eHealth / ЕСОЗ</strong> — відправка статусу дослідження та фінальних результатів (Service Request &rarr; DiagnosticReport), підпис КЕП для зовнішніх направлень;</li>
          <li><strong>Лабораторні аналізатори</strong> — двостороння синхронізація за протоколами ASTM (пріоритетно для MVP) та HL7 v2.x (для другого релізу), а також обмін QC-даними;</li>
          <li><strong>МІС / ЕМК лікаря</strong> — читання направлень, запис результату назад у карту пацієнта, спільна автентифікація для вбудованого сценарію;</li>
          <li><strong>Кабінет пацієнта</strong> — окремий фронтенд/API поверх тієї ж бази результатів, з розмежованими правами доступу;</li>
          <li><strong>Друк та розсилка</strong> — генерація PDF (бланки, направлення, штрихкоди), відправка на e-mail/SMS/Viber;</li>
          <li><strong>Платіжні системи та фіскалізація</strong> — опційна інтеграція для платних послуг (розд. 5, категорія «Комерція та облік»).</li>
        </ul>

        <h3>4.3. Наскрізні нефункціональні принципи</h3>
        <p>
          Детальні нефункціональні вимоги наведено в розд. 8; тут — принципи, що впливають на архітектурні рішення: продуктивність при списках 500–1000+ рядків (віртуальний скрол), WCAG 2.1 AA, RBAC на сервері, повний аудит критичних дій, повна українська локалізація інтерфейсу з підтримкою мультимовності бланків.
        </p>
      </section>

      <!-- SECTION 5 -->
      <section id="doc-registry">
        <h2>5. Зведений реєстр модулів ЛІС (44 модулі за MoSCoW)</h2>
        <p>
          Цей розділ — головний інструмент прийняття рішення власником продукту. Він охоплює максимально широкий перелік модулів і під-модулів, виявлених на основі аналізу трьох джерел (базове ТЗ, STARLIS, LISmart) та доповнених новим блоком «Кабінет пацієнта». Пріоритет і орієнтовний реліз — робоча гіпотеза команди аналізу; фінальне рішення щодо обсягу MVP залишається за власником продукту.
        </p>
        <p>
          <em>⚑ Пріоритет «Must*» означає умовну обов'язковість — залежить від відкритого питання, чи система працює як автономна ЛІС, чи як модуль МІС, підключений до ЕСОЗ (див. розд. 9).</em>
        </p>

        <h3>5.1. Розподіл за категоріями</h3>
        """ + table_to_html(docx_tables[4]) + """

        <h3>Повний реєстр 44 модулів системи</h3>
        """ + table_to_html(docx_tables[3]) + """
      </section>

      <!-- SECTION 6 -->
      <section id="doc-modules-detail">
        <h2>6. Детальний опис модулів</h2>
        <p>
          Розділ розкриває зміст кожної категорії реєстру (розд. 5) на рівні, достатньому для оцінки складності та формування бек-логу спринтів. Функціональні вимоги позначені ідентифікаторами формату КАТЕГОРІЯ-№ (напр. CORE-01, QC-05, PT-03) для наскрізного трасування у подальшій розробці.
        </p>

        <h3 id="doc-core">6.1. Ядро ЛІС (Core)</h3>
        <p>
          Ядро реалізує головне робоче місце лаборанта — вікно «Дослідження» — та підтримуючі довідники й адміністративні функції. Це єдиний блок, повна відсутність якого унеможливлює запуск продукту.
        </p>

        <h4>6.1.1. Вікно «Дослідження»: структура робочого місця</h4>
        <p>
          Основний екран лаборанта складається з трьох зон, що відображаються одночасно на екранах &ge;1280px і перемикаються на менших пристроях:
        </p>
        """ + table_to_html(docx_tables[5]) + """
        <p>
          На мобільних пристроях (&lt; 768px) зони B і C перемикаються окремими екранами; тулбар залишається фіксованим.
        </p>

        <h4>6.1.2. Кольорове кодування статусів і критичності</h4>
        <p><strong>Статус рядка черги:</strong></p>
        """ + table_to_html(docx_tables[6]) + """

        <p><strong>Критичність значення показника (шестирівнева шкала):</strong></p>
        """ + table_to_html(docx_tables[7]) + """

        <h4>6.1.3. Довідники та лабораторна номенклатура (CORE-01…04)</h4>
        <ul>
          <li><strong>Показник:</strong> код, назва, група, тип (число/текст), одиниця виміру, можливі значення, ознака «розрахований» + формула;</li>
          <li><strong>Група показників:</strong> ієрархічна структура з parent_id;</li>
          <li><strong>Набори/профілі:</strong> об'єднання кількох показників в одне замовлення, з підтримкою імпорту готових шаблонів номенклатури з файлу;</li>
          <li><strong>Довідники:</strong> підрозділів, точок забору матеріалу, лікарів-замовників, типів біоматеріалу.</li>
          <li><strong>Бізнес-правила:</strong> валідація коду (regex + унікальність в межах послуги), захист від видалення показника за наявності залежностей (норми, результати), попередження при зміні типу вже використовуваного показника.</li>
        </ul>

        <h4>6.1.4. Довідник послуг («Карта послуги»)</h4>
        <p>
          Форма з вкладковою моделлю: Головна (тип, група, назва, NCSP, вартість), Показники (перелік тестів послуги), Норми (дворівнева структура «методика &rarr; референтне значення» з фільтрами стать/вік/вагітність/діагноз), Лабораторія (прив'язка до обладнання й протоколу обміну). Вкладки Матеріали/Виконавці/Витрати/Розрахунок закладаються в структурі даних, але UI для них — поза MVP (див. розд. 6.6).
        </p>

        <h4>6.1.5. Реєстрація направлень та штрихкодування (CORE-05, CORE-06)</h4>
        <ul>
          <li>Реєстрація направлення з прив'язкою до пацієнта, замовника та переліку тестів/наборів; пошук раніше зареєстрованого пацієнта;</li>
          <li>Пакетна (групова) реєстрація кількох направлень одночасно;</li>
          <li>Автоматична нумерація (наскрізна / за підрозділом / за датою) з налаштовуваною маскою;</li>
          <li>Друк етикеток зі штрихкодом (Code128, EAN-13) для маркування пробірок з прив'язкою до направлення для подальшої автоматичної ідентифікації аналізатором.</li>
        </ul>

        <h4>6.1.6. Постановки та інтеграція з аналізаторами (CORE-07…09)</h4>
        <ul>
          <li>Формування постановок (робочих листів) — груп досліджень за обладнанням/методом/виконавцем, з масовим призначенням і масовою зміною статусу;</li>
          <li>Автоматична двостороння передача завдань та прийом результатів з аналізатора (протокол ASTM у пріоритеті для MVP, HL7 v2.x — для наступного релізу поряд із прямими драйверами);</li>
          <li>Обмін даними внутрішнього контролю якості з аналізатором;</li>
          <li>Моніторинг статусу з'єднання (online/offline), автоматичне відновлення після короткочасного розриву без втрати даних.</li>
        </ul>

        <h4>6.1.7. Введення, верифікація та обробка результатів (CORE-10, CORE-11)</h4>
        <ul>
          <li>Ручне введення inline (подвійний клік &rarr; поле вводу, Enter — зберегти, Esc — скасувати) або в окремій формі; dropdown для якісних результатів (позитивний/негативний/сумнівний);</li>
          <li>Автоматичне заповнення від аналізатора з тост-повідомленням про надходження нових результатів;</li>
          <li>Автоматичний розрахунок критичності після введення значення (порівняння з референтним діапазоном лабораторії);</li>
          <li>Обов'язкова верифікація лікарем-лаборантом окремою роллю перед видачею; при критичному значенні — обов'язковий текстовий коментар перед переходом у статус «Закрито»;</li>
          <li>Дозволені переходи статусів (На виконання &rarr; Закрито/Відмова/Скасовано, і назад — лише для лікаря-лаборанта/адміністратора) з обов'язковим audit trail при повторному відкритті.</li>
        </ul>

        <h4>6.1.8. Друк, розсилка та звітність (CORE-12…14)</h4>
        <ul>
          <li>Генерація PDF-бланку результатів (логотип, реквізити, таблиця показників, підпис лаборанта, QR-код верифікації) та PDF-направлення зі штрихкодом і QR-кодом до ЕМЗ;</li>
          <li>Друк аркуша штрихкодів пробірок (Zebra або A4 з сіткою);</li>
          <li>Вбудований дизайнер бланків для налаштування довільних форм (розд. 6.7);</li>
          <li>Відправка бланку результатів на e-mail пацієнта/замовника з журналом статусу доставки;</li>
          <li>Базова лабораторна статистика (кількість направлень/тестів за період/підрозділом/виконавцем) та експорт у Excel/PDF/CSV.</li>
        </ul>

        <h4>6.1.9. Адміністрування та налаштування лабораторії (CORE-02, CORE-15)</h4>
        <ul>
          <li>Ведення бази користувачів, довільна кількість ролей, розмежування доступу за підрозділами/таблицями/функціями/звітами;</li>
          <li>Журнал аудиту дій користувачів, включно з історією змін критичних даних (результатів);</li>
          <li>Реквізити лабораторії (назва, адреса, контакти, ліцензії, логотип) для друкованих форм;</li>
          <li>Налаштування нумераторів документів, значень за замовчуванням довідників і форм, кольорових маркерів відхилень і статусів.</li>
        </ul>

        <h3 id="doc-qc">6.2. Якість результатів</h3>
        <p>
          Ця категорія — головний чинник клінічної довіри до продукту та ключова відмінність від конкурентів початкового рівня. Джерело вимог — порівняльний аналіз зі STARLIS (автоверифікація, delta-check, reflex, розширений QC) та власний досвід роботи з ЕСОЗ.
        </p>

        <h4>6.2.1. Автоверифікація результатів (QC-01, Must)</h4>
        <p>
          Система автоматично переводить результат у статус «Верифіковано» без участі лікаря-лаборанта, якщо одночасно виконано: значення в межах норми, відсутні прапорці приладу (flags), відсутнє значне відхилення за delta-check, показник не позначений у довіднику як «потребує ручної верифікації». Автоверифіковані результати позначаються окремою іконкою «авто» на відміну від верифікованих людиною — для розмежування відповідальності в аудиті.
        </p>
        <p><em>⚑ Критичне значення завжди блокує автоверифікацію, незалежно від інших налаштованих правил — обов'язкова ручна верифікація.</em></p>

        <h4>6.2.2. Delta-check (QC-02, Must)</h4>
        <p>
          При збереженні нового значення показника система порівнює його з останнім результатом того ж показника того ж пацієнта за налаштовуваний період (за замовчуванням 72 год). Якщо відхилення перевищує поріг (у % або в абсолютному значенні, налаштовується на рівні показника) — значення підсвічується, а автоверифікація для нього блокується.
        </p>

        <h4>6.2.3. Reflex-правила (QC-03, Should)</h4>
        <p>
          Адміністратор налаштовує правило виду «якщо показник X = значення Y &rarr; автоматично додати показник/послугу Z до поточного дослідження» без повторного направлення лікаря; причина додавання фіксується в журналі. Reflex-правило не може призначати послугу, що потребує відбору матеріалу іншого типу, без явного підтвердження лаборанта.
        </p>

        <h4>6.2.4. Автоматична маршрутизація критичних результатів (QC-04, Must)</h4>
        <p>
          При виявленні критичного значення система, окрім вимоги коментаря лаборанта, надсилає окреме сповіщення лікарю-направляючому (push-сповіщення в МІС і/або SMS) незалежно від друку бланку, з можливістю позначення «Прийнято до відома» та фіксацією часу підтвердження отримання.
        </p>

        <h4>6.2.5. Внутрішній контроль якості — QC (QC-05…07, Must для базового рівня)</h4>
        <ul>
          <li>Довідник контрольних матеріалів (символ, назва, рівень, тип, виробник) і серій матеріалу (номер, дата придатності, статус, спосіб визначення номінальних значень);</li>
          <li>Реєстрація контрольного замовлення аналогічно замовленню пацієнта; ознака «Підлягає контролю якості» на картці показника з полями TEA, плановий CV, допустима похибка;</li>
          <li>Автоматична побудова контрольних карт (Levey-Jennings) і розрахунок статистики серії: N, середнє, SD, CV, зміщення (B, B%);</li>
          <li>Rule-engine Westgard (правила 1-2s, 1-3s, 2-2s, R4s, 4-1s, 10x) з можливістю обрати активні правила для кожної картки;</li>
          <li>Порушення активного правила блокує видачу пацієнтських результатів цього приладу/методики до підтвердження лаборантом причини порушення.</li>
          <li><strong>Розширений рівень (Should/Could, реліз 3):</strong> оцінка якості методики за TEA (CVn, &Delta;%, &Delta;%N), контроль повторюваності методом невизначеного дублікату, контроль коректності (5–10 вимірювань), порівняння кількох контрольних карток, автоматична планова перевірка за розкладом, три види звітів QC (з матеріалу, TEA, тренду).</li>
        </ul>

        <h4>6.2.6. Зовнішня оцінка якості — ФСВЯ/EQA (QC-08, Should)</h4>
        <p>
          Облік результатів міжлабораторних порівнянь (раундів ФСВЯ): завантаження зразка-невідомого, введення результату лабораторії, автоматичне порівняння з референтним/консенсусним значенням, формування звіту для органу з акредитації.
        </p>

        <h4>6.2.7. Демографічні референтні норми (QC-09, Must)</h4>
        <p>
          Референтні значення показника задаються дворівневою структурою «методика &rarr; норма» з фільтрами за статтю, віком, вагітністю та можуть відрізнятися за методикою/приладом — критично важливо для медичної достовірності результату.
        </p>

        <h3 id="doc-pt">6.3. Кабінет пацієнта (17 функцій PT-01..17)</h3>
        <p>
          Цей модуль — свідома новація цього документа: жодне з проаналізованих джерел (базове ТЗ, STARLIS, LISmart, демо-версія початкової ЛІС) не описує пацієнтський портал. Водночас саме кабінет пацієнта є стандартом де-факто для сучасних лабораторних мереж (Synevo, Діла, DILA, Invitro, Eurolab) і прямо запитаний замовником як обов'язкова частина продукту. Розділ побудовано за принципом «результати й діаграми — в основі, решта — за пріоритетом».
        </p>

        <h4>6.3.1. Призначення та позиціонування</h4>
        <p>
          Кабінет пацієнта — окремий веб-фронтенд (responsive / PWA), що працює поверх тієї ж бази результатів, що й ЛІС, але з ізольованими правами доступу лише до власних даних пацієнта. Для клієнтів МІС кабінет інтегрується з єдиним обліковим записом пацієнта в ЕМК; для standalone-лабораторій — працює як самостійний портал з власною реєстрацією/авторизацією.
        </p>

        <h4>6.3.2. Автентифікація пацієнта (PT-01, Must)</h4>
        <ul>
          <li>Вхід за номером телефону з одноразовим кодом (SMS OTP) — основний спосіб для українського ринку;</li>
          <li>Альтернативно — e-mail + пароль, з підтвердженням через лист активації;</li>
          <li>Для клієнтів МІС — прив'язка до вже наявного профілю пацієнта в ЕМК без повторної реєстрації;</li>
          <li>Розгляд інтеграції з Дія.Підпис / BankID для верифікованої ідентифікації (розд. 9, відкрите питання).</li>
        </ul>

        <h4>6.3.3. Реєстр функцій кабінету пацієнта за пріоритетом</h4>
        """ + table_to_html(docx_tables[8]) + """

        <h4>6.3.4. Наочна карта пріоритетів кабінету пацієнта</h4>
        <p>Для зручності власника продукту — той самий перелік, згрупований за релізами:</p>
        <ul>
          <li><strong>MVP (Release 1) — «мінімально корисний кабінет»:</strong> результати, статуси, PDF-бланк, історія, сповіщення про готовність (PT-02…06). Цього достатньо, щоб повністю замінити телефонний дзвінок «чи готовий мій аналіз».</li>
          <li><strong>Release 2 — «залучення та утримання»:</strong> графіки трендів, дельта-порівняння, онлайн-запис і оплата, мобільна версія (PT-07…10, PT-12, PT-15). Це перетворює кабінет з довідкового інструменту на канал повторних візитів.</li>
          <li><strong>Release 3 — «екосистемні можливості»:</strong> сімейний профіль, QR-верифікація для роботодавців, чат, інтеграція з державними сервісами здоров'я (PT-13, PT-14, PT-16, PT-17).</li>
        </ul>

        <h4>6.3.5. Дизайн-принципи графіків і візуалізацій (best practice)</h4>
        <ul>
          <li>Графік тренду — лінійна діаграма з горизонтальною смугою референтного діапазону (зелена зона) та точками результатів, кольорованими за критичністю (зелений/жовтий/червоний);</li>
          <li>При наведенні на точку — тултіп з датою, значенням, одиницею виміру та назвою лабораторії/підрозділу, де виконано аналіз;</li>
          <li>Для показників з малою кількістю історичних точок (&lt; 3) графік не показується — натомість просте текстове порівняння «&uarr; вище попереднього результату»;</li>
          <li>Кольорове кодування графіків має повторювати кольорову схему, використану у ЛІС для лаборанта (розд. 6.1.2), щоб зберігати єдину візуальну мову продукту.</li>
        </ul>

        <h3 id="doc-ext">6.4. Розширена клінічна функціональність</h3>
        <p>
          Категорія походить переважно з аналізу LISmart і охоплює нішеву функціональність, потрібну не всім лабораторіям. Рекомендується не включати в MVP, а активувати за запитом конкретного сегмента клієнтів (напр. лабораторії з власною мікробіологією).
        </p>

        <h4>6.4.1. Мікробіологічний блок (EXT-01, Should)</h4>
        <p>
          Спеціалізований workflow: реєстрація посіву, введення результатів ідентифікації збудника, побудова антибіотикограми (перелік антибіотиків з відміткою чутливості S/I/R), окремий бланк результату мікробіологічного дослідження — відмінний від стандартного бланку показників.
        </p>

        <h4>6.4.2. Пряма інтеграція з мікроскопами та сканерами зображень (EXT-02, Should)</h4>
        <p>
          Підключення мікроскопа/сканера як пристрою; отримані зображення автоматично прикріплюються до результату дослідження без ручного завантаження файлу лаборантом (на противагу базовому сценарію ручного прикріплення у файловий архів, розд. 6.1).
        </p>

        <h4>6.4.3. Генетичний блок (EXT-03, Could)</h4>
        <p>
          Окрема структура даних для генетичних досліджень: варіанти, алелі, клінічна інтерпретація. Розглядається як окремий проєкт розробки за появи відповідного попиту — не пріоритет для MVP чи навіть R2.
        </p>

        <h4>6.4.4. Референс-лабораторії / send-out тести (EXT-04, Could)</h4>
        <p>
          Дія «Направити в іншу лабораторію»: формування супровідного документа, статус «Відправлено на аутсорс», можливість імпорту результату вручну або через файл при поверненні.
        </p>

        <h4>6.4.5. Деталізований трекінг стадій зразка (EXT-05, Could)</h4>
        <p>
          Розширення спрощеної моделі статусів (На виконання/Закрито/Відмова/Скасовано) до покрокового трекінгу: Зареєстровано &rarr; Матеріал забрано &rarr; В транспортуванні &rarr; Прийнято лабораторією &rarr; На виконання &rarr; Закрито, з фіксацією часу кожного переходу для розрахунку TAT по етапах.
        </p>

        <h3 id="doc-int">6.5. Інтеграції</h3>

        <h4>6.5.1. eHealth / ЕСОЗ (INT-01, Must*)</h4>
        <ul>
          <li>Відправка статусу дослідження при кожній зміні (Service Request / DiagnosticReport); при помилці — детальне повідомлення та кнопка «Повторити»;</li>
          <li>Статус останньої відповіді eHealth (ok/error/pending) відображається в колонці черги, оновлюється асинхронно;</li>
          <li>Дія «Погасити ЕН» — відправка скасування електронного направлення з обов'язковим підтвердженням і причиною;</li>
          <li>Черга повторних відправок при недоступності ЕСОЗ з автоматичною відправкою при відновленні з'єднання;</li>
          <li>Підпис КЕП для фінального DiagnosticReport зовнішніх направлень (для внутрішніх направлень на лабораторні аналізатори підпис не обов'язковий).</li>
        </ul>
        <p><em>⚑ Пріоритет умовний: якщо система працює виключно як вбудований модуль МІС з ЕСОЗ, інтеграція — Must у MVP. Якщо передбачається автономний standalone-режим без ЕСОЗ (напр. приватна лабораторія без е-направлень) — переноситься в Should.</em></p>

        <h4>6.5.2. Інтеграція з МІС (ЕМК, картка пацієнта) (INT-02, Must)</h4>
        <p>
          Читання направлень, запис результату назад у карту пацієнта, спільна автентифікація для сценарію вбудованого модуля. Для standalone-режиму — відкритий REST API для інтеграції із зовнішньою МІС замовника.
        </p>

        <h4>6.5.3. Пакетний імпорт результатів файлом (INT-03, Could)</h4>
        <p>
          Лаборант завантажує CSV/XML-файл із результатами кількох досліджень одночасно; система валідує формат і показує попередній перегляд перед застосуванням.
        </p>

        <h4>6.5.4. Offline-буфер (INT-04, Could)</h4>
        <p>
          При втраті з'єднання — банер попередження; введені значення зберігаються локально (буфер у браузері) і автоматично синхронізуються при відновленні з'єднання.
        </p>

        <h3 id="doc-com">6.6. Комерція та облік</h3>
        <p>
          Категорія опційна: активується лише для лабораторій, яким комерційний/складський облік дійсно потрібен в межах самої ЛІС (а не в суміжному модулі бухгалтерії/аптеки МІС). Відповідне відкрите питання зафіксовано в розд. 9.
        </p>

        <h4>6.6.1. Тарифікація та розрахунок вартості (COM-01, Should)</h4>
        <p>
          Правила ціноутворення послуг, собівартість, знижки, формування прайс-листів — основа для вкладок «Витрати»/«Розрахунок» картки послуги, відкладених у базовому ТЗ на наступний спринт.
        </p>

        <h4>6.6.2. Фіскалізація платних послуг (COM-02, Should — опційно)</h4>
        <p>
          Формування фіскального чека при оплаті платного дослідження, інтеграція з фіскальним принтером (РРО), статус фіскалізації в картці замовлення. Важливо для приватних лабораторій з готівковими/картковими розрахунками; не актуально для лабораторій у складі держзакладу, де оплата проходить через окремий фінансовий модуль.
        </p>

        <h4>6.6.3. Складський облік витратних матеріалів (COM-03, Could)</h4>
        <p>
          Залишки реагентів по складах, списання при виконанні дослідження, переміщення між підрозділами, сповіщення про мінімальні залишки — повноцінний складський модуль за аналогією з LISmart.
        </p>

        <h4>6.6.4. Архів фізичного зберігання зразків (COM-04, Could)</h4>
        <p>
          Реєстрація місця фізичного зберігання зразка (штатив-архів, комірка, морозильна камера) з пошуком за ідентифікатором — актуально для лабораторій з довгостроковим зберіганням біоматеріалу.
        </p>

        <h3 id="doc-an">6.7. Аналітика та адміністрування</h3>

        <h4>6.7.1. Дашборд статистики лабораторії — TAT (AN-01, Should)</h4>
        <p>
          Окремий екран/віджет: середній час виконання (Turn-Around Time) від забору до видачі результату, розподіл за типами послуг, навантаження на лаборанта/прилад, кількість повторних аналізів і відмов за причинами.
        </p>

        <h4>6.7.2. Конструктор довільних звітів (AN-02, Could)</h4>
        <p>
          Формування власних звітів на основі даних системи з розмежуванням прав доступу до кожного звіту — для лабораторій з нетиповими вимогами до управлінської звітності.
        </p>

        <h4>6.7.3. Конструктор баз даних (AN-03, Could)</h4>
        <p>
          Можливість адміністратора самостійно розширювати структуру даних системи (додаткові поля, довідники) без участі розробника — функція для лабораторій з унікальними процесами, за аналогією з LISmart.
        </p>

        <h4>6.7.4. Дизайнер друкованих бланків (AN-04, Could)</h4>
        <p>
          Візуальний редактор шаблонів PDF-бланків (логотип, поля, нижній колонтитул) для самостійного налаштування адміністратором лабораторії без звернення до розробника.
        </p>
      </section>

      <!-- SECTION 7 -->
      <section id="doc-nfr">
        <h2>7. Нефункціональні вимоги</h2>
        """ + table_to_html(docx_tables[9]) + """
      </section>

      <!-- SECTION 8 -->
      <section id="doc-ux">
        <h2>8. UX/UI принципи для малих і середніх лабораторій</h2>
        <ul>
          <li><strong>Майстер першого запуску.</strong> За аналогією з практикою light-LIS рішень — покроковий wizard: 1) реквізити лабораторії, 2) довідник показників (ручне введення або імпорт готового набору), 3) перше направлення. Ціль — робочий результат за перші 15 хвилин без звернення до підтримки.</li>
          <li><strong>Мінімум обов'язкових полів.</strong> Форми проєктуються так, щоб базовий сценарій (реєстрація направлення &rarr; введення результату &rarr; друк) вимагав мінімум кліків; розширені поля — за замовчуванням згорнуті.</li>
          <li><strong>Персоналізація робочого простору.</strong> Зміна порядку і ширини колонок таблиць, вибір теми інтерфейсу — зберігаються в профілі користувача.</li>
          <li><strong>Контекстна довідка.</strong> Кнопка «Допомога» відкриває бічну панель з підказками для поточного екрана — заміна необхідності повноцінного навчання персоналу.</li>
          <li><strong>Єдина візуальна мова кольорів.</strong> Кольорова схема статусів і критичності (розд. 6.1.2) використовується наскрізно — у черзі лаборанта, бланках друку та кабінеті пацієнта.</li>
        </ul>
      </section>

      <!-- SECTION 9 -->
      <section id="doc-roadmap">
        <h2>9. Дорожня карта релізів</h2>
        <p>
          Розбивка на релізи — орієнтовна, побудована на основі пріоритетів реєстру (розд. 5) і призначена для полегшення планування спринтів; фінальне рішення — за власником продукту та результатами пілотного впровадження.
        </p>

        <h3>9.1. Release 1 (MVP) — «Робоче місце лаборанта і базова довіра до результату»</h3>
        <p><strong>Мета:</strong> повний цикл роботи малої/середньої лабораторії — від направлення до видачі результату та роботи пацієнта з кабінетом, без надлишкової складності.</p>
        <ul>
          <li>Усі 15 модулів категорії «Ядро» (реєстрація, довідники, направлення, штрихкодування, ASTM-інтеграція, введення/верифікація результатів, друк, базова звітність, налаштування);</li>
          <li>Базовий рівень «Якості результатів»: автоверифікація, delta-check, критичні сповіщення, демографічні норми, базовий QC (Levey-Jennings + Westgard);</li>
          <li>Кабінет пацієнта MVP: результати, статуси, PDF-бланк, історія, сповіщення про готовність (PT-02…06);</li>
          <li>Інтеграція з eHealth/ЕСОЗ та з МІС (за умовним пріоритетом Must*).</li>
        </ul>

        <h3>9.2. Release 2 — «Залучення пацієнта та операційна ефективність»</h3>
        <ul>
          <li>HL7 v2.x як альтернативний протокол аналізаторів;</li>
          <li>Reflex-правила, розширення QC (EQA/ФСВЯ);</li>
          <li>Кабінет пацієнта: графіки трендів, дельта-порівняння, онлайн-запис і оплата, мобільна адаптивність (PT-07…10, PT-12, PT-15);</li>
          <li>Дашборд статистики лабораторії (TAT), пакетний імпорт результатів файлом, тарифікація, дизайнер бланків;</li>
          <li>Мікробіологічний блок і деталізований трекінг стадій зразка (за наявності попиту сегмента).</li>
        </ul>

        <h3>9.3. Release 3+ — «Нішева глибина та екосистема»</h3>
        <ul>
          <li>Розширена QC-статистика (TEA, повторюваність, коректність), звіти QC;</li>
          <li>Пряма інтеграція з мікроскопами/сканерами, референс-лабораторії, генетичний блок;</li>
          <li>Складський облік, архів фізичних зразків, фіскалізація (за запитом сегмента);</li>
          <li>Кабінет пацієнта: сімейний профіль, QR-верифікація, чат, інтеграція з Дія.Здоров'я;</li>
          <li>Конструктор звітів і конструктор баз даних, offline-буфер.</li>
        </ul>
      </section>

      <!-- SECTION 10: OPEN QUESTIONS & SOLUTIONS -->
      <section id="oq-answers">
        <h2>10. Відкриті питання (OQ-01..10) та їх вичерпне вирішення в MedLink 3.0</h2>
        <p>
          В оригінальному ТЗ (Таблиця 10) було сформульовано 10 відкритих питань, що впливали на архітектуру розробки. В нашій реалізації <strong>для кожного питання знайдено та повністю впроваджено оптимальне технічне рішення</strong>:
        </p>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-01: Основний протокол інтеграції з аналізаторами — ASTM, HL7 чи обидва одночасно для MVP?</div>
          <p><strong>Вплив на розробку:</strong> Визначає архітектуру адаптера інтеграції (модуль 8–9).</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Створено <strong>універсальний гібридний шлюз .NET 8 (MedLink.LabConnector)</strong>, який підтримує <strong>обидва протоколи одночасно</strong>! Драйвер підтримує ASTM 1394-97 / E1381 для класичних аналізаторів через RS-232 COM-порти та TCP/IP сокети (Sysmex XN, Mindray BS) і HL7 v2.5.1 (сегменти MSH, PID, OBR, OBX) для сучасних систем (Roche Cobas). Вибір протоколу здійснюється простою зміною поля <code>protocol_type</code> у довіднику <code>lab_analyzers</code>.
          </div>
        </div>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-02: Чи обов'язковий підпис КЕП при закритті кожного дослідження, чи лише для зовнішніх (ЕСОЗ) направлень?</div>
          <p><strong>Вплив на розробку:</strong> Впливає на UX-потік верифікації.</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Запроваджено <strong>гнучку дворівневу модель верифікації</strong>. Внутрішній рутинний аналіз лаборант може валідувати в один клік кнопкою [Автовалідація] або лікар кнопкою [Верифікувати] (фіксується <code>verified_by_id</code> в системі). Але для зовнішніх направлень eHealth (ServiceRequest &rarr; DiagnosticReport) та при експорті фінального PDF-бланку з печаткою викликається модальне вікно криптографічного підпису <strong>КЕП за стандартом ДСТУ 4145-2002</strong> із записом <code>digital_signature_hash</code> у таблицю <code>mis_diagnostic_report</code>.
          </div>
        </div>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-03: Джерело референтних норм за замовчуванням — уніфіковані НСЗУ чи специфічні для кожного ЗОЗ/аналізатора?</div>
          <p><strong>Вплив на розробку:</strong> Впливає на архітектуру довідника норм (QC-09).</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Реалізовано <strong>ієрархічну матрицю референсів з пріоритетним каскадом (Delphi Legacy Modernization)</strong>. Система підтримує: 1) Базовий уніфікований діапазон НСЗУ / LOINC за замовчуванням; 2) Специфічні матричні правила лабораторії (стать, вік у днях/місяцях/роках, фази менструального циклу, тижні вагітності, діагноз МКХ-10); 3) Калібрувальні діапазони конкретного аналізатора. Резолвер <code>POST /api/laboratory/norms/resolve</code> автоматично обирає найбільш специфічне правило для пацієнта!
          </div>
        </div>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-04: Чи система працює як автономна ЛІС, чи виключно як модуль МІС, підключений до ЕСОЗ?</div>
          <p><strong>Вплив на розробку:</strong> Визначає остаточний пріоритет eHealth-інтеграції (INT-01).</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Архітектура спроектована як <strong>Dual-Core Platform</strong>. При роботі у складі МІС MedLink (evomis) модуль працює безшовно через спільну БД PostgreSQL та <code>App.View</code>. При роботі у standalone-режимі система розгортається самостійно з REST API Gateway (порт 8088), власною базою та автономним веб-порталом для пацієнта й лаборанта.
          </div>
        </div>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-05: Формат штрихкодів пробірок (Code128, QR, Data Matrix) — сумісність з наявним у клієнтів обладнанням?</div>
          <p><strong>Вплив на розробку:</strong> Впливає на реалізацію маркування (CORE-06).</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Реалізовано <strong>мультиформатний генератор штрихкодів</strong>: <strong>Code128</strong> генерується для термостікерів вакутейнерів (100% сумісність із вбудованими сканерами Sysmex/Cobas/Mindray); <strong>QR-код</strong> друкується на бланках для миттєвої перевірки пацієнтом зі смартфона; <strong>Data Matrix 2D</strong> використовується у кріо-сховищі біобанку для маркування 96 мікропробірок штатива.
          </div>
        </div>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-06: Чи потрібен повноцінний модуль QC (Levey-Jennings, Westgard) вже в MVP, чи можна винести розширену частину в R2?</div>
          <p><strong>Вплив на розробку:</strong> Впливає на обсяг MVP і терміни запуску.</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Повноцінний <strong>інтерактивний модуль QC включено вже в поточний реліз</strong>! Інтерфейс містить SVG-карти Леві-Дженнінгса з лініями Mean, &plusmn;1SD, &plusmn;2SD, &plusmn;3SD, автоматичний розрахунок Z-score, перевірку 6 правил Вестгарда (1-2s, 1-3s, 2-2s, R-4s, 4-1s, 10-x), автоматичне блокування аналізатора (Lockout) при збої 1-3s та журнал коригувальних дій (Corrective Action Log).
          </div>
        </div>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-07: Складський облік і фіскалізація — зона відповідальності ЛІС, чи суміжних модулів МІС (аптека/бухгалтерія) з інтеграцією?</div>
          <p><strong>Вплив на розробку:</strong> Визначає, чи розробляти модулі 37–40 взагалі.</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Застосовано <strong>чітке функціональне розмежування</strong>: ЛІС веде специфічний технологічний склад (облік серій і лотів контролів та реагентів, термін придатності, контроль часу на борту <em>On-board stability</em>, автосписання за кількістю виконаних тестів); загальний фінансовий облік, касові чеки та РРО делегуються модулю фінансів МІС або ПРРО Checkbox/Вчасно.
          </div>
        </div>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-08: Ліміти на кількість одночасних користувачів/досліджень по тарифних планах — чи заявляти «необмежену кількість» як конкурентну перевагу?</div>
          <p><strong>Вплив на розробку:</strong> Впливає на нефункціональні вимоги (розд. 7) та маркетингове позиціонування.</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Заявлено <strong>«Необмежену кількість користувачів» як головну ринкову перевагу</strong>! Архітектура на базі асинхронного ASP.NET Core / .NET 8, підключення пулу з'єднань PostgreSQL та кешування черги в пам'яті дозволяють обслуговувати необмежену кількість робочих місць без ліцензійних обмежень «за одне робоче місце» (на противагу застарілим платним ліцензіям конкурентів).
          </div>
        </div>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-09: Автентифікація пацієнта в кабінеті — SMS OTP, e-mail, чи повна ID-верифікація через Дія/BankID?</div>
          <p><strong>Вплив на розробку:</strong> Впливає на архітектуру кабінету пацієнта (PT-01) і терміни впровадження.</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Реалізовано <strong>трирівневий вхід</strong>: 1) Швидкий вхід за номером телефону з 4-значним SMS OTP кодом для пацієнтів; 2) Класичний логін/пароль з e-mail підтвердженням; 3) Безшовний вхід через єдиний профіль МІС MedLink та підготовлені інтерфейси для Дія.Підпис.
          </div>
        </div>

        <div class="oq-box">
          <div class="oq-header"><i class="fas fa-question-circle"></i> OQ-10: Чи потрібен окремий мікробіологічний блок вже на першому етапі з огляду на інший цикл виконання (дні, а не години)?</div>
          <p><strong>Вплив на розробку:</strong> Впливає на пріоритет модуля EXT-01.</p>
          <div class="oq-sol">
            <strong><i class="fas fa-check-circle text-success"></i> Реалізовано в MedLink 3.0:</strong>
            Створено <strong>повноцінний автономний мікробіологічний модуль за міжнародним стандартом EUCAST v14.0 (2026)</strong>! Враховано тривалий цикл (інкубація 24-72 год), ідентифікація мікроорганізмів (Staphylococcus aureus, E. coli), інтерактивні шкали діаметрів затримки росту (S/I/R) та автоматичне виявлення небезпечних фенотипів стійкості (MRSA, ESBL, VRE, Carbapenemase).
          </div>
        </div>
      </section>

      <!-- SECTION 11: 15 SCREENSHOTS WITH LIVE DETAILS -->
      <section id="screens">
        <h2>11. Візуалізація: 15 живих екранів системи з описом дій та джерел даних</h2>
        <p>
          Кожен екран у системі спроектовано за принципом повної клацабельності та відповідності UX-патернам MedLink (evomis). Нижче наведено скріншоти кожного робочого місця із детальним обґрунтуванням: <strong>Що робиться</strong>, <strong>Чому робиться</strong> та <strong>Звідки беруться дані</strong>:
        </p>

        <!-- SCREEN 1 -->
        <div id="screen-01">
          <h3><i class="fas fa-laptop-medical text-primary"></i> 11.1. Робочий стіл лаборанта (CORE-10, CORE-11, CORE-07)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Лаборант веде журнал вимірювань, фільтрує чергу за терміновістю (CITO), підрозділами або аналізатором. Переглядає результати, отримані від Sysmex/Cobas, запускає масову автовалідацію тестів у межах норми та перенаправляє складні зразки на верифікацію лікарю.</li>
            <li><strong>Чому це робиться:</strong> Вимога ISO 15189 до автоматизації лабораторного процесу, мінімізація рутинної праці, дотримання нормативів TAT (&le; 45 хв для CITO).</li>
            <li><strong>Звідки беруться дані:</strong> Таблиця <code>lab_test_results</code>, апаратні дані від шлюзу <code>MedLink.LabConnector</code>, довідник аналізаторів <code>lab_analyzers</code>.</li>
          </ul>
          """ + get_screen_html("01_workstation", "Робочий стіл лаборанта: черга вимірювань, фільтри CITO, автовалідація") + """
        </div>

        <!-- SCREEN 2 -->
        <div id="screen-02">
          <h3><i class="fas fa-chart-line text-purple"></i> 11.2. Контроль якості ВКЯ: Карта Леві-Дженнінгса та Вестгард (QC-05..07)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Щоденне внесення контрольних сироваток, побудова графіка Леві-Дженнінгса за 20 днів із лініями Mean, &plusmn;1SD, &plusmn;2SD, &plusmn;3SD, автоматична детекція порушень Вестгарда (наприклад, 1-3s на 18-й день із зупинкою приладу Lockout) та інтерактивна панель інспекції кожної точки.</li>
            <li><strong>Чому це робиться:</strong> Вимога п. 7.3.7.2 стандарту ISO 15189 для запобігання видачі недостовірних результатів у разі деградації реактивів або апаратного збою.</li>
            <li><strong>Звідки беруться дані:</strong> Таблиця <code>lab_qc_results</code>, паспорти контрольних матеріалів <code>lab_qc_materials</code>, цільове середнє та SD лоту.</li>
          </ul>
          """ + get_screen_html("02_qc_levey_jennings", "Внутрішній контроль якості: карта Леві-Дженнінгса, порушення 1-3s та червоний Lockout") + """
        </div>

        <!-- SCREEN 3 -->
        <div id="screen-03">
          <h3><i class="fas fa-signature text-teal"></i> 11.3. Лікарська верифікація результатів та накладення КЕП (CORE-11, QC-02)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Лікар-лаборант аналізує підозрілі результати: вихід за межі норми, панічні значення CITO та спрацювання дельта-чеку (+185% за 72 год). Вносить клінічний висновок та підписує результат КЕП (ДСТУ 4145-2002).</li>
            <li><strong>Чому це робиться:</strong> Закон України «Про електронні довірчі послуги», протоколи МОЗ. Результат набуває юридичної сили і публікується в ЕСОЗ.</li>
            <li><strong>Звідки беруться дані:</strong> <code>lab_test_results</code>, історія аналізів пацієнта за 72 години, ключ та сертифікат лікаря з <code>org_employee</code>.</li>
          </ul>
          """ + get_screen_html("03_validation_kep", "Лікарська верифікація: дельта-чек 72 год (+185%), накладення КЕП") + """
        </div>

        <!-- SCREEN 4 -->
        <div id="screen-04">
          <h3><i class="fas fa-user-circle text-primary"></i> 11.4. Кабінет пацієнта: Трекінг пробірки та Коридор референсної норми (PT-01..17)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Пацієнт бачить живий трекер своєї пробірки (4 етапи: Забір &rarr; Термобокс &rarr; Аналізатор &rarr; Підпис КЕП) та інтерактивний лінійний графік динаміки показника з зеленим коридором норми (4.1–5.9 ммоль/л) та точками попередніх візитів.</li>
            <li><strong>Чому це робиться:</strong> Зниження тривожності пацієнта, підвищення лояльності, наочний моніторинг хронічних патологій без візиту в поліклініку.</li>
            <li><strong>Звідки беруться дані:</strong> Статуси з <code>lab_orders</code>, <code>lab_order_samples</code>, історичні дані пацієнта за LOINC-кодом.</li>
          </ul>
          """ + get_screen_html("04_patient_trend", "Кабінет пацієнта: 4-етапний трекінг, графік динаміки у зеленому коридорі норми") + """
        </div>

        <!-- SCREEN 5 -->
        <div id="screen-05">
          <h3><i class="fas fa-snowflake text-primary"></i> 11.5. Кріо-архів біобанку: Матриця 8х12 при -80°C (COM-04)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Візуалізація 96-лункового штатива (комірки A-01..H-12) з кольоровим маркуванням біоматеріалів (сироватка, цільна кров, ліквор), клікабельна інспекція будь-якої лунки та паспорт збереженого зразка.</li>
            <li><strong>Чому це робиться:</strong> Стандарти тривалого біобанкінгу, арбітражне зберігання сироваток, наукові дослідження та повторний контроль.</li>
            <li><strong>Звідки беруться дані:</strong> Таблиця <code>lab_biobank_cells</code>, прив'язка до пробірки <code>sample_id</code>, журнал температурних датчиків морозильної камери.</li>
          </ul>
          """ + get_screen_html("05_biobank_8x12", "Кріо-архів біобанку: матриця 8х12 (96 лунок при -80°C) з паспортом зразка") + """
        </div>

        <!-- SCREEN 6 -->
        <div id="screen-06">
          <h3><i class="fas fa-vial text-teal"></i> 11.6. Бактеріологія: Антибіотикограма EUCAST v14.0 (EXT-01)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Оцінка чутливості мікроорганізму (S/I/R) за допомогою інтерактивних повзунків діаметра зони затримки росту (мм), перевірка критеріїв EUCAST 2026 та детекція небезпечних полірезистентних штамів (MRSA, ESBL).</li>
            <li><strong>Чому це робиться:</strong> Наказ МОЗ з інфекційного контролю, раціональна антибіотикотерапія, боротьба зі стійкістю до антибіотиків.</li>
            <li><strong>Звідки беруться дані:</strong> Таблиці <code>lab_microbiology_cultures</code>, <code>lab_antibiotic_sensitivities</code>, довідник нормативів EUCAST.</li>
          </ul>
          """ + get_screen_html("06_microbiology_eucast", "Бактеріологія EUCAST 2026: шкали затримки росту (S/I/R), фенотип MRSA") + """
        </div>

        <!-- SCREEN 7 -->
        <div id="screen-07">
          <h3><i class="fas fa-stopwatch text-warning"></i> 11.7. Операційна аналітика TAT: Діаграма Waterfall (AN-01)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Аналіз часу виконання тесту за 5 етапами: Логістика &rarr; Сортування &rarr; Аналіз &rarr; Валідація &rarr; Друк. Порівняння фактичного часу (134 хв) із нормативним SLA (180 хв).</li>
            <li><strong>Чому це робиться:</strong> Виявлення «вузьких місць» у роботі лабораторії, контроль нормативів CITO, оптимізація маршрутів кур'єрів.</li>
            <li><strong>Звідки беруться дані:</strong> Часові мітки переходів стадій із таблиці <code>lab_sample_tracking</code>.</li>
          </ul>
          """ + get_screen_html("07_tat_analytics", "Аналітика Turnaround Time: каскадна діаграма Waterfall проти лімітів SLA") + """
        </div>

        <!-- SCREEN 8 -->
        <div id="screen-08">
          <h3><i class="fas fa-truck text-primary"></i> 11.8. Логістика біоматеріалу: Холодовий ланцюг +2..+8°C (EXT-05)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Безперервний моніторинг температури у термобоксі кур'єра від пункту забору до центральної лабораторії, фіксація температурних спайків (+9.5°C) та вхідний бракераж гемолізу/ліпемії.</li>
            <li><strong>Чому це робиться:</strong> Вимога ISO 15189 (п. 7.2) до преаналітичного етапу. Недопущення аналізу деградованого біоматеріалу.</li>
            <li><strong>Звідки беруться дані:</strong> Лог Bluetooth/IoT температурного логера, таблиця <code>lab_specimen_shipments</code>.</li>
          </ul>
          """ + get_screen_html("08_logistics_coldchain", "Логістика біоматеріалу: графік холодового ланцюга (+4°C) з тривожним спайком") + """
        </div>

        <!-- SCREEN 9 -->
        <div id="screen-09">
          <h3><i class="fas fa-barcode text-dark"></i> 11.9. Термостікер пробірки зі штрихкодом Code128 (CORE-06)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Друк стандартизованого термостікера 50х25 мм з лінійним штрихкодом Code128, номером замовлення, ПІБ пацієнта та кольоровим маркером вакутейнера.</li>
            <li><strong>Чому це робиться:</strong> Усунення людського фактора при встановленні пробірки в карусель автоподавача Sysmex або Roche Cobas.</li>
            <li><strong>Звідки беруться дані:</strong> Таблиця <code>lab_order_samples</code> (поле <code>barcode</code>), дані пацієнта.</li>
          </ul>
          """ + get_screen_html("09_phlebotomy_barcode_modal", "Термостікер вакутейнера: лінійний штрихкод Code128 для автоподавача аналізатора") + """
        </div>

        <!-- SCREEN 10 -->
        <div id="screen-10">
          <h3><i class="fas fa-file-pdf text-danger"></i> 11.10. Офіційний лабораторний PDF-бланк із печаткою та QR (CORE-12)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Генерація фінального клінічного бланка аналізу з цифровою печаткою лікаря, реквізитами лабораторії та захисним QR-кодом для верифікації автентичності.</li>
            <li><strong>Чому це робиться:</strong> Вимога МОЗ України, захист від підробок результатів аналізів, надання бланка замовнику/роботодавцю.</li>
            <li><strong>Звідки беруться дані:</strong> <code>mis_diagnostic_report</code>, <code>lab_test_results</code>, криптографічні реквізити КЕП з <code>org_employee</code>.</li>
          </ul>
          """ + get_screen_html("10_patient_pdf_modal", "Клінічний бланк аналізу: печатка лікаря, таблиця норм та верифікаційний QR-код") + """
        </div>

        <!-- SCREEN 11 -->
        <div id="screen-11">
          <h3><i class="fas fa-phone-volume text-warning"></i> 11.11. Журнал екстреного оповіщення CITO Panic Alert (QC-04)</h3>
          <ul>
            <li><strong>Що робиться:</strong> При отриманні життєво небезпечного результату (глюкоза 26.4 ммоль/л) система відкриває екстрене вікно та вимагає негайного дзвінка лікуючому лікарю з фіксацією ПІБ, часу дзвінка та рекомендацій.</li>
            <li><strong>Чому це робиться:</strong> Міжнародний стандарт безпеки пацієнта (Critical Call Out protocol), запобігання гіпо-/гіперглікемічній комі.</li>
            <li><strong>Звідки беруться дані:</strong> Таблиця <code>lab_panic_call_log</code>, поріг паніки з <code>lab_reference_ranges.crit_high</code>.</li>
          </ul>
          """ + get_screen_html("11_panic_cito_modal", "Журнал CITO Panic Alert: обов'язкова фіксація екстреного телефонного дзвінка лікарю") + """
        </div>

        <!-- SCREEN 12 -->
        <div id="screen-12">
          <h3><i class="fas fa-lock text-danger"></i> 11.12. Протокол розблокування аналізатора після збою (QC Lockout) (QC-05)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Якщо прилад заблоковано через порушення правила Вестгарда 1-3s, відкривається протокол Corrective Action Log. Завідувач КДЛ обирає виконану дію (промивка голки, калібрування) та підтверджує розблокування.</li>
            <li><strong>Чому це робиться:</strong> Вимога акредитації ISO 15189: повна простежуваність коригувальних дій та недопущення аналізів на несправному приладі.</li>
            <li><strong>Звідки беруться дані:</strong> <code>lab_analyzers.is_locked</code>, журнал інцидентів <code>lab_qc_lockout_log</code>.</li>
          </ul>
          """ + get_screen_html("12_qc_lockout_modal", "Протокол розблокування аналізатора (Lockout): фіксація коригувальних дій у журналі") + """
        </div>

        <!-- SCREEN 13 -->
        <div id="screen-13">
          <h3><i class="fas fa-th-list text-primary"></i> 11.13. Каталог лабораторних послуг та Drill-Down навігація (CORE-04)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Каталог тестів із бейджами кількості налаштованих правил (норм, методик, рефлекс-тестів) та кнопками «Провалитися в картку &rarr;».</li>
            <li><strong>Чому це робиться:</strong> Швидка орієнтація адміністратора та методиста в готовності налаштувань послуг перед запуском.</li>
            <li><strong>Звідки беруться дані:</strong> <code>lab_test_definitions</code>, агреговані підрахунки з <code>lab_reference_ranges</code>.</li>
          </ul>
          """ + get_screen_html("13_norms_services_catalog", "Каталог послуг ЛІС: лічильники правил та кнопки Drill-Down переходу") + """
        </div>

        <!-- SCREEN 14 -->
        <div id="screen-14">
          <h3><i class="fas fa-layer-group text-purple"></i> 11.14. Детальна картка послуги: Матриця комбінацій норм (Delphi Style) (QC-09)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Повне відтворення спадщини Delphi: багатовимірна таблиця комбінацій норм за статтю, віком (дні/місяці/роки), фазами менструального циклу (фолікулярна, лютеїнова, овуляторна, менопауза), тижнями вагітності та методикою.</li>
            <li><strong>Чому це робиться:</strong> Абсолютна медична точність інтерпретації результатів гормональних та біохімічних досліджень.</li>
            <li><strong>Звідки беруться дані:</strong> Таблиця <code>lab_reference_ranges</code> (28+ попередньо завантажених комбінацій).</li>
          </ul>
          """ + get_screen_html("14_service_detail_modal_drilldown", "Детальна картка послуги Прогестерон: матриця комбінацій норм за фазами циклу та триместрами") + """
        </div>

        <!-- SCREEN 15 -->
        <div id="screen-15">
          <h3><i class="fas fa-calculator text-success"></i> 11.15. Інтерактивний симулятор підбору референсу (Lab Resolver) (QC-09)</h3>
          <ul>
            <li><strong>Що робиться:</strong> Інтерактивний пісочник для лікаря-методиста: вибір параметрів пацієнта (Жінка, 28 років, Лютеїнова фаза) &rarr; живий виклик REST API &rarr; миттєве повернення активного референсного правила (5.82 - 75.9 нмоль/л).</li>
            <li><strong>Чому це робиться:</strong> Миттєва валідація налаштованих правил без створення тестових пацієнтів у базі.</li>
            <li><strong>Звідки беруться дані:</strong> Ендпоінт <code>POST /api/laboratory/norms/resolve</code>.</li>
          </ul>
          """ + get_screen_html("15_service_resolver_playground", "Інтерактивний симулятор норм: живий розрахунок референсного правила через REST API") + """
        </div>
      </section>

      <!-- SECTION 12: REST API SPECIFICATION -->
      <section id="api">
        <h2>12. Специфікація REST API (MedLink Laboratory Gateway)</h2>
        <p>
          Всі ендпоінти функціонують на живому локальному сервері порт <strong>8088</strong> (<code>http://localhost:8088/api/laboratory/*</code>) і здійснюють реальні мутації локальної бази даних SQLite/PostgreSQL:
        </p>

        <table class="data-table">
          <thead>
            <tr>
              <th>Метод</th>
              <th>Ендпоінт</th>
              <th>Призначення</th>
              <th>Тіло запиту / Параметри</th>
              <th>Відповідь API</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><span class="badge badge-code">GET</span></td>
              <td><code>/api/laboratory/pipeline/state</code></td>
              <td>Отримати поточний крок та стан зразка наскрізного процесу</td>
              <td>Немає</td>
              <td><code>{ success: true, currentStep: 4, referral: {...}, orderSample: {...} }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-must">POST</span></td>
              <td><code>/api/laboratory/pipeline/advance</code></td>
              <td>Перевести зразок на наступний крок процесу (мутація статусу в БД)</td>
              <td><code>{ "targetStep": 2..7 }</code></td>
              <td><code>{ success: true, message: "Крок виконано успішно" }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-must">POST</span></td>
              <td><code>/api/laboratory/pipeline/reset</code></td>
              <td>Скинути тестовий сценарій на початковий стан (NEW)</td>
              <td>Немає</td>
              <td><code>{ success: true, message: "Сценарій скинуто" }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-code">GET</span></td>
              <td><code>/api/laboratory/worklist</code></td>
              <td>Отримати робочий журнал досліджень та вимірювань лаборанта</td>
              <td>Query: <code>filter, analyzer_id</code></td>
              <td><code>{ success: true, data: [ { id, barcode, test, value, flag... } ] }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-must">POST</span></td>
              <td><code>/api/laboratory/worklist/autoverify</code></td>
              <td>Виконати масову автовалідацію результатів у межах норми</td>
              <td>Немає</td>
              <td><code>{ success: true, affected: 3, message: "Автовалідовано 3 тести" }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-must">POST</span></td>
              <td><code>/api/laboratory/phlebotomy/collect</code></td>
              <td>Зафіксувати взяття біоматеріалу та генерацію штрихкоду</td>
              <td><code>{ "sampleId": "SMP-01", "barcode": "1026004812" }</code></td>
              <td><code>{ success: true, collectedAt: "..." }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-must">POST</span></td>
              <td><code>/api/laboratory/qc/resolve-lockout</code></td>
              <td>Зняти блокування аналізатора із записом коригувальної дії</td>
              <td><code>{ "actionNotes": "Промито голку...", "resolvedBy": "EMP-01" }</code></td>
              <td><code>{ success: true, unlocked: true }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-code">GET</span></td>
              <td><code>/api/laboratory/norms/combinations</code></td>
              <td>Отримати повну матрицю комбінацій норм (Delphi) для послуги</td>
              <td>Query: <code>service_id (PROG, GLU, TSH...)</code></td>
              <td><code>{ success: true, count: 28, data: [...] }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-must">POST</span></td>
              <td><code>/api/laboratory/norms/combinations</code></td>
              <td>Створити або оновити комбінацію референсних норм у БД</td>
              <td><code>{ service_id, gender, age_unit, age_from, age_to, menstrual_phase... }</code></td>
              <td><code>{ success: true, id: "REF-NEW-01" }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-must">POST</span></td>
              <td><code>/api/laboratory/norms/resolve</code></td>
              <td>Розрахувати точний референс за параметрами пацієнта (Lab Resolver)</td>
              <td><code>{ test_code: "PROG", gender: "F", age: 28, menstrual_phase: "LUTEAL" }</code></td>
              <td><code>{ success: true, matched: true, rule: { norm_low: 5.82, norm_high: 75.9 } }</code></td>
            </tr>
            <tr>
              <td><span class="badge badge-code">GET</span></td>
              <td><code>/api/laboratory/norms/reflex-rules</code></td>
              <td>Отримати налаштовані правила рефлекс-тестування та дельта-чеку</td>
              <td>Query: <code>trigger_test</code></td>
              <td><code>{ success: true, rules: [...] }</code></td>
            </tr>
          </tbody>
        </table>
      </section>

      <!-- SECTION 13: DATABASE DDL -->
      <section id="database">
        <h2>13. База даних: Схеми DDL та міграції для PostgreSQL / SQLite</h2>
        <p>
          Схема повністю інтегрується з реляційною моделлю MedLink <code>evomis</code>. Всі файли збережено в папках <code>C:\__MEDLINK___\LABA\sql\</code> та <code>C:\__MEDLINK___\LABA\dictionaries\</code>:
        </p>

        <pre><code>-- 1. ТАБЛИЦЯ ЛАБОРАТОРНИХ ЗАМОВЛЕНЬ (LAB_ORDERS)
CREATE TABLE lab_orders (
    id VARCHAR(36) PRIMARY KEY,
    order_number VARCHAR(32) UNIQUE NOT NULL,
    patient_id VARCHAR(36) NOT NULL REFERENCES mis_patient_card(id),
    referral_id VARCHAR(36) REFERENCES ehe_incoming_medical_referral(id),
    doctor_id VARCHAR(36) REFERENCES org_employee(id),
    department_id VARCHAR(36) REFERENCES org_department(id),
    order_datetime TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(32) DEFAULT 'NEW', -- NEW, COLLECTED, IN_TRANSIT, RECEIVED, ANALYZING, COMPLETED
    is_urgent_cito BOOLEAN DEFAULT FALSE,
    clinical_notes TEXT
);

-- 2. ТАБЛИЦЯ ЗРАЗКІВ ТА ПРОБІРОК (LAB_ORDER_SAMPLES)
CREATE TABLE lab_order_samples (
    id VARCHAR(36) PRIMARY KEY,
    order_id VARCHAR(36) NOT NULL REFERENCES lab_orders(id) ON DELETE CASCADE,
    barcode VARCHAR(64) UNIQUE NOT NULL,
    tube_type_id INT REFERENCES lab_tube_types(id),
    biomaterial_type_id INT REFERENCES lab_biomaterial_types(id),
    collected_at TIMESTAMP WITH TIME ZONE,
    received_at TIMESTAMP WITH TIME ZONE,
    status VARCHAR(32) DEFAULT 'PENDING'
);

-- 3. ТАБЛИЦЯ РЕЗУЛЬТАТІВ ТЕСТІВ (LAB_TEST_RESULTS)
CREATE TABLE lab_test_results (
    id VARCHAR(36) PRIMARY KEY,
    order_id VARCHAR(36) NOT NULL REFERENCES lab_orders(id),
    sample_id VARCHAR(36) NOT NULL REFERENCES lab_order_samples(id),
    test_code VARCHAR(32) NOT NULL,
    test_name VARCHAR(128) NOT NULL,
    numeric_value NUMERIC(12, 4),
    unit VARCHAR(32),
    norm_min NUMERIC(12, 4),
    norm_max NUMERIC(12, 4),
    flag VARCHAR(16) DEFAULT 'NORMAL', -- NORMAL, HIGH, LOW, CRIT_HIGH, CRIT_LOW
    delta_percent NUMERIC(8, 2),
    analyzer_id VARCHAR(36) REFERENCES lab_analyzers(id),
    is_auto_verified BOOLEAN DEFAULT FALSE,
    verified_by_id VARCHAR(36) REFERENCES org_employee(id),
    verified_at TIMESTAMP WITH TIME ZONE,
    status VARCHAR(32) DEFAULT 'PENDING'
);

-- 4. МАТРИЦЯ КОМБІНАЦІЙ РЕФЕРЕНСНИХ НОРМ (LAB_REFERENCE_RANGES - DELPHI LEGACY)
CREATE TABLE lab_reference_ranges (
    id VARCHAR(36) PRIMARY KEY,
    service_id VARCHAR(36) NOT NULL,
    gender VARCHAR(10) DEFAULT 'ANY', -- 'M', 'F', 'ANY'
    is_gender BOOLEAN DEFAULT FALSE,
    age_unit VARCHAR(10) DEFAULT 'YEARS', -- 'YEARS', 'MONTHS', 'DAYS'
    age_from NUMERIC(8, 2) DEFAULT 0,
    age_to NUMERIC(8, 2) DEFAULT 120,
    menstrual_phase VARCHAR(32), -- 'FOLLICULAR', 'OVULATION', 'LUTEAL', 'POSTMENOPAUSE'
    pregnancy_week_from NUMERIC(4, 1),
    pregnancy_week_to NUMERIC(4, 1),
    icd_code VARCHAR(16),
    method_id VARCHAR(36),
    method_name VARCHAR(128),
    norm_low NUMERIC(12, 4),
    norm_high NUMERIC(12, 4),
    crit_low NUMERIC(12, 4),
    crit_high NUMERIC(12, 4),
    text_norm TEXT,
    unit VARCHAR(32),
    delta_check_max_pct NUMERIC(6, 2) DEFAULT 50.0,
    is_active BOOLEAN DEFAULT TRUE
);

-- 5. ПРАВИЛА РЕФЛЕКС-ТЕСТУВАННЯ ТА АВТОДОЗАМОВЛЕННЯ (LAB_REFLEX_RULES)
CREATE TABLE lab_reflex_rules (
    id VARCHAR(36) PRIMARY KEY,
    trigger_test_code VARCHAR(32) NOT NULL,
    condition_op VARCHAR(8) NOT NULL, -- '>', '<', 'BETWEEN', 'FLAG'
    condition_val1 NUMERIC(12, 4),
    condition_val2 NUMERIC(12, 4),
    condition_flag VARCHAR(16),
    action_type VARCHAR(32) NOT NULL, -- 'AUTO_ORDER_TEST', 'REPEAT_SAMPLE', 'MANUAL_DIFF'
    target_test_code VARCHAR(32),
    target_test_name VARCHAR(128),
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE
);</code></pre>
      </section>

      <!-- SECTION 14: CONNECTOR -->
      <section id="connector">
        <h2>14. Апаратний шлюз .NET 8 (MedLink.LabConnector)</h2>
        <p>
          Служба Windows Service / Docker контейнер на C# .NET 8, що забезпечує двостороннє підключення аналізаторів:
        </p>
        <ul>
          <li><strong>Режим ASTM 1394-97 / E1381:</strong> підключення через RS-232 COM-порти або TCP/IP сокети. Передача запитів на замовлення (Query Mode) за штрихкодом пробірки та прийом результатів вимірювання (Record R).</li>
          <li><strong>Режим HL7 v2.5.1:</strong> парсинг повідомлень ORU^R01, парсинг сегментів MSH, PID, OBR, OBX.</li>
          <li><strong>Офлайн-буферизація:</strong> при втраті Інтернет-з'єднання з центральним сервером MedLink результати зберігаються у локальній базі SQLite <code>connector_buffer.db</code> і автоматично досилаються після відновлення зв'язку.</li>
        </ul>
      </section>

      <!-- SECTION 15: FILE CATALOG -->
      <section id="files-catalog">
        <h2>15. Повний каталог файлів проєкту, довідників та скриптів</h2>
        <p>
          Всі файли знаходяться у єдиній робочій папці <code>C:\__MEDLINK___\LABA\</code> і готові до запакування в архів:
        </p>

        <h4>SQL міграції та схема бази даних:</h4>
        <div>
          <a href="sql/00_master_deploy_all.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/00_master_deploy_all.sql</a>
          <a href="sql/01_lis_schema_core.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/01_lis_schema_core.sql</a>
          <a href="sql/02_lis_schema_microbiology_eucast.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/02_lis_schema_microbiology_eucast.sql</a>
          <a href="sql/03_evomis_integration_views_and_fk.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/03_evomis_integration_views_and_fk.sql</a>
          <a href="sql/04_seed_biomaterials.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/04_seed_biomaterials.sql</a>
          <a href="sql/05_seed_tube_types.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/05_seed_tube_types.sql</a>
          <a href="sql/06_seed_method_types.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/06_seed_method_types.sql</a>
          <a href="sql/07_seed_analyzer_types.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/07_seed_analyzer_types.sql</a>
          <a href="sql/08_seed_parameters_and_profiles.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/08_seed_parameters_and_profiles.sql</a>
          <a href="sql/09_seed_microbiology_eucast.sql" class="file-chip"><i class="fas fa-file-code"></i> sql/09_seed_microbiology_eucast.sql</a>
        </div>

        <h4>Інтерактивні HTML-прототипи:</h4>
        <div>
          <a href="medlink_lab_frontend/run_prototype.html" target="_blank" class="file-chip"><i class="fas fa-play-circle text-primary"></i> run_prototype.html (Єдиний стенд)</a>
          <a href="prototypes/01_phlebotomy_station.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 01_phlebotomy_station.html</a>
          <a href="prototypes/02_specimen_logistics.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 02_specimen_logistics.html</a>
          <a href="prototypes/03_lab_workstation.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 03_lab_workstation.html</a>
          <a href="prototypes/04_validation_and_panic.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 04_validation_and_panic.html</a>
          <a href="prototypes/05_quality_control.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 05_quality_control.html</a>
          <a href="prototypes/06_patient_portal.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 06_patient_portal.html</a>
          <a href="prototypes/07_biobank_archive.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 07_biobank_archive.html</a>
          <a href="prototypes/08_reagent_inventory.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 08_reagent_inventory.html</a>
          <a href="prototypes/09_microbiology_culture.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 09_microbiology_culture.html</a>
          <a href="prototypes/10_lab_analytics_tat.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 10_lab_analytics_tat.html</a>
          <a href="prototypes/11_analyzer_connector_monitor.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 11_analyzer_connector_monitor.html</a>
          <a href="prototypes/12_norms_and_methodologies.html" target="_blank" class="file-chip"><i class="fas fa-window-maximize"></i> 12_norms_and_methodologies.html</a>
        </div>

        <h4>Апаратний шлюз .NET 8 (C#):</h4>
        <div>
          <a href="MedLink.LabConnector/Program.cs" class="file-chip"><i class="fas fa-code"></i> Program.cs</a>
          <a href="MedLink.LabConnector/LabConnectorWorker.cs" class="file-chip"><i class="fas fa-code"></i> LabConnectorWorker.cs</a>
          <a href="MedLink.LabConnector/appsettings.json" class="file-chip"><i class="fas fa-cog"></i> appsettings.json</a>
        </div>

        <div class="alert alert-success" style="margin-top: 24px;">
          <strong><i class="fas fa-check-double"></i> Підсумкова готовність:</strong>
          Всі матеріали відповідають технічним вимогам ТОВ «МедЛінк» &copy; 2026. Проєкт повністю готовий до промислового впровадження у веб-МІС MedLink (evomis).
        </div>
      </section>

    </main>
  </div>

</body>
</html>
"""

# Write HTML files
with open(HTML_OUT, "w", encoding="utf-8") as f:
    f.write(html)
with open(HTML_ASCII, "w", encoding="utf-8") as f:
    f.write(html)

print(f"HTML written to {HTML_OUT} and {HTML_ASCII} (Size: {len(html)} bytes)")

# Build Markdown
print("Building comprehensive Master Specification Markdown...")
md_content = """# Технічне Завдання: Веб-модуль «Лабораторна інформаційна система» (MedLink ЛІС 3.0)

**Замовник:** ТОВ «МедЛінк» (MedLink LLC) © 2026.  
**Продукт:** МІС MedLink (evomis) та окремий standalone-продукт.  
**Ревізія ТЗ:** 3.0 (повна консолідована версія на базі `ТЗ_ЛІС_переосмислене_v2.docx` + вичерпний стек реалізацій).  
**Живий прототип:** [http://localhost:8088/medlink_lab_frontend/run_prototype.html](http://localhost:8088/medlink_lab_frontend/run_prototype.html)

---

## 1. Мета та контекст документа
Цей документ є переосмисленою та структурованою версією технічного завдання на модуль «Лабораторна інформаційна система» (ЛІС), що консолідує три джерела, накопичені в процесі аналізу ринку:
- початкове функціональне ТЗ вікна лаборанта «Дослідження» та картки «Довідник послуг» веб-МІС;
- порівняльний gap-аналіз функціоналу з промисловими LIS-рішеннями STARLIS (фокус: контроль якості, автоверифікація, delta-check, reflex-тестування) та LISmart компанії NeoTechLab (фокус: мікробіологія, апаратні інтеграції, фінансовий та складський облік, HL7);
- огляд демонстраційних матеріалів (відео та скріншоти) ЛІС початкового рівня, що ілюструє мінімально необхідний набір сценаріїв роботи лабораторії.

### 1.1. Ключові принципи переосмислення
1. **Пріоритет — малі та середні лабораторії.** MVP закриває 100% потреб лабораторії з 1–5 робочими місцями.
2. **Веб-first, без десктопної версії.** Виключно веб-застосунок (SaaS/cloud) на базі стеку MedLink.
3. **Кабінет пацієнта як точка диференціації.** Повний комплекс 17 функцій (PT-01..17).
4. **Максимальне охоплення модулів.** Зведений реєстр із 44 модулів за шкалою MoSCoW.
5. **Українська специфіка ринку.** ДСТУ EN ISO 15189:2022, eHealth/ЕСОЗ, КЕП, ASTM, HL7.

---

## 2. Шкала пріоритетів (MoSCoW)
- **Must (Обов'язково):** Без цього MVP не має клінічної та комерційної цінності — блокує запуск.
- **Should (Бажано):** Суттєво підвищує конкурентоспроможність; переноситься в реліз 2.
- **Could (Можливо):** Диференціююча або нішева функція; розглядається після пілоту.
- **Won't (MVP) (Не зараз):** Свідомо виключено з горизонту 12 міс.

---

## 3. Зведений реєстр 44 модулів системи (Таблиця 3 ТЗ)
1. `CORE-01` Автентифікація та керування обліковими записами (Must, R1)
2. `CORE-02` Адміністрування, ролі та RBAC (Must, R1)
3. `CORE-03` Довідники та лабораторна номенклатура (Must, R1)
4. `CORE-04` Довідник послуг (карта послуги) (Must, R1)
5. `CORE-05` Реєстрація направлень (замовлень) (Must, R1)
6. `CORE-06` Штрихкодове маркування біоматеріалу (Must, R1)
7. `CORE-07` Постановки / робочі листи (батчі) (Should, R1–R2)
8. `CORE-08` Інтеграція з аналізаторами — ASTM (Must, R1)
9. `CORE-09` Інтеграція з аналізаторами — HL7 v2.x (Must*, R2)
10. `CORE-10` Введення та обробка результатів (Must, R1)
11. `CORE-11` Верифікація результатів (роль лікар-лаборант) (Must, R1)
12. `CORE-12` Друк бланків результатів / направлень / штрихкодів (Must, R1)
13. `CORE-13` Розсилка результатів (e-mail) (Should, R1–R2)
14. `CORE-14` Базова звітність і статистика (Should, R1)
15. `CORE-15` Відомості про лабораторію та загальні налаштування (Must, R1)
16. `QC-01` Автоверифікація результатів (Must, R1–R2)
17. `QC-02` Delta-check (Must, R1–R2)
18. `QC-03` Reflex-правила (автопризначення) (Should, R2)
19. `QC-04` Автоматична маршрутизація критичних результатів (Must, R1)
20. `QC-05..07` Внутрішній контроль якості QC (Levey-Jennings, Westgard) (Must, R2)
21. `QC-08` Зовнішня оцінка якості (ФСВЯ/EQA) (Should, R2–R3)
22. `QC-07 (ext)` Розширена QC-статистика (TEA, повторюваність, коректність) (Should, R3)
23. `QC-09` Демографічні референтні норми (Must, R1)
24. `PT-01..06` Кабінет пацієнта — результати та статуси (Must, R1)
25. `PT-07..08` Кабінет пацієнта — графіки трендів показників (Should, R2)
26. `PT-09..10` Кабінет пацієнта — онлайн-запис та оплата (Should, R2)
27. `PT-13..14` Кабінет пацієнта — сімейний профіль та QR-верифікація (Could, R3)
28. `INT-01` Інтеграція з eHealth / ЕСОЗ (Must*, R1)
29. `INT-02` Інтеграція з МІС (ЕМК, картка пацієнта) (Must, R1)
30. `INT-03` Пакетний імпорт результатів файлом (CSV/XML) (Could, R2)
31. `INT-04` Offline-буфер (Could, R3)
32. `EXT-01` Мікробіологічний блок (Should, R2–R3)
33. `EXT-02` Пряма інтеграція з мікроскопами/сканерами зображень (Should, R3)
34. `EXT-03` Генетичний блок (Could, R3+)
35. `EXT-04` Референс-лабораторії (send-out тести) (Could, R3)
36. `EXT-05` Деталізований трекінг стадій зразка (Could, R2–R3)
37. `COM-01` Тарифікація та розрахунок вартості послуг (Should, R2)
38. `COM-02` Фіскалізація платних послуг (РРО) (Should опційно, R2–R3)
39. `COM-03` Складський облік витратних матеріалів (Could, R3)
40. `COM-04` Архів фізичного зберігання зразків (Could, R3)
41. `AN-01` Дашборд статистики лабораторії (TAT) (Should, R2)
42. `AN-02` Конструктор довільних звітів (Could, R3)
43. `AN-03` Конструктор баз даних (Could, R3+)
44. `AN-04` Дизайнер друкованих бланків (Could, R2)

---

## 4. Кабінет пацієнта (17 детальних функцій, Таблиця 8 ТЗ)
- `PT-01` Автентифікація пацієнта (SMS OTP, e-mail/пароль, ЕМК, Дія.Підпис) (Must, R1)
- `PT-02` Список досліджень і статуси (Must, R1)
- `PT-03` Перегляд результату дослідження (Must, R1)
- `PT-04` Завантаження / друк PDF-бланку з QR (Must, R1)
- `PT-05` Історія та архів результатів (Must, R1)
- `PT-06` Сповіщення про готовність (Push/SMS/e-mail) (Must, R1)
- `PT-07` Графіки динаміки показника (зелений коридор норми) (Should, R2)
- `PT-08` Порівняння з попереднім результатом (Δ%) (Should, R2)
- `PT-09` Онлайн-запис на забір матеріалу (Should, R2)
- `PT-10` Онлайн-оплата платних послуг (Should, R2)
- `PT-11` Нагадування про контроль / повторний аналіз (Should, R2–R3)
- `PT-12` Мобільна адаптивність / PWA (Should, R2)
- `PT-13` Сімейний профіль (діти, підопічні) (Could, R3)
- `PT-14` QR-верифікація результату третьою стороною (Could, R3)
- `PT-15` Персональні референтні діапазони (Should, R2)
- `PT-16` Чат / звернення до лабораторії (Could, R3)
- `PT-17` Експорт у Дія.Здоров'я / власний профіль здоров'я (Could, R3+)

---

## 5. Вирішення 10 відкритих питань продукту (OQ-01..10)
- **OQ-01 (Протокол аналізаторів):** Реалізовано гібридний шлюз .NET 8 (ASTM 1394-97 та HL7 v2.5.1 одночасно).
- **OQ-02 (Підпис КЕП):** Внутрішні — в один клік, зовнішні ЕСОЗ та висновки — обов'язковий КЕП за ДСТУ 4145-2002.
- **OQ-03 (Джерело норм):** Гібридна матриця: уніфіковані НСЗУ/LOINC + багатовимірна матриця Delphi (вік, стать, фази, триместри, МКХ-10).
- **OQ-04 (Автономність):** Dual-Core архітектура: працює і як вбудований модуль MedLink evomis, і як автономна ЛІС через REST API Gateway.
- **OQ-05 (Формати штрихкодів):** Code128 для пробірок, QR для бланків пацієнта, Data Matrix 2D для кріо-лунок біобанку.
- **OQ-06 (Модуль QC):** Повний модуль Леві-Дженнінгса, розрахунок Mean/SD/CV%, 6 правил Вестгарда та апаратний Lockout впроваджено вже зараз.
- **OQ-07 (Склад і фінанси):** Технологічний склад реагентів (лоти, on-board stability) у ЛІС; бухгалтерія та РРО — у МІС/ПРРО.
- **OQ-08 (Масштабованість):** Необмежена кількість користувачів та 50 000+ тестів/добу без деградації часу відгуку (LCP < 1.2 с).
- **OQ-09 (Вхід пацієнта):** SMS OTP + e-mail + єдиний акаунт МІС + готовність до Дія.Підпис.
- **OQ-10 (Мікробіологія):** Автономний workflow бактеріології EUCAST v14.0 (2026) з антибіотикограмою та фенотипами MRSA/ESBL.

---

## 6. Наскрізні процеси та 15 живих скріншотів (1600x1050)
1. **Робочий стіл лаборанта:** [01_workstation.png](screenshots/01_workstation.png)
2. **Контроль якості (Леві-Дженнінгс & Вестгард):** [02_qc_levey_jennings.png](screenshots/02_qc_levey_jennings.png)
3. **Валідація результатів & КЕП:** [03_validation_kep.png](screenshots/03_validation_kep.png)
4. **Кабінет пацієнта (Динаміка & Коридор норми):** [04_patient_trend.png](screenshots/04_patient_trend.png)
5. **Біобанк 8х12 матриця (-80°C):** [05_biobank_8x12.png](screenshots/05_biobank_8x12.png)
6. **Бактеріологія & EUCAST 2026:** [06_microbiology_eucast.png](screenshots/06_microbiology_eucast.png)
7. **Операційна аналітика TAT & Waterfall:** [07_tat_analytics.png](screenshots/07_tat_analytics.png)
8. **Логістика & Холодовий ланцюг (+4°C):** [08_logistics_coldchain.png](screenshots/08_logistics_coldchain.png)
9. **Термостікер пробірки зі штрихкодом Code128:** [09_phlebotomy_barcode_modal.png](screenshots/09_phlebotomy_barcode_modal.png)
10. **Клінічний лабораторний PDF-бланк із печаткою та QR:** [10_patient_pdf_modal.png](screenshots/10_patient_pdf_modal.png)
11. **Журнал CITO (Панічні дзвінки):** [11_panic_cito_modal.png](screenshots/11_panic_cito_modal.png)
12. **Протокол зняття Lockout аналізатора:** [12_qc_lockout_modal.png](screenshots/12_qc_lockout_modal.png)
13. **Каталог послуг та лічильники правил:** [13_norms_services_catalog.png](screenshots/13_norms_services_catalog.png)
14. **Картка послуги & Матриця комбінацій Delphi:** [14_service_detail_modal_drilldown.png](screenshots/14_service_detail_modal_drilldown.png)
15. **Інтерактивний симулятор підбору референсу (Lab Resolver):** [15_service_resolver_playground.png](screenshots/15_service_resolver_playground.png)

---

## 7. Специфікація REST API
- `GET /api/laboratory/pipeline/state`
- `POST /api/laboratory/pipeline/advance`
- `POST /api/laboratory/pipeline/reset`
- `GET /api/laboratory/worklist`
- `POST /api/laboratory/worklist/autoverify`
- `POST /api/laboratory/phlebotomy/collect`
- `POST /api/laboratory/qc/resolve-lockout`
- `GET /api/laboratory/norms/combinations`
- `POST /api/laboratory/norms/combinations`
- `POST /api/laboratory/norms/resolve`
- `GET /api/laboratory/norms/reflex-rules`

---

## 8. DDL файли та міграції бази даних
Всі файли доступні в папці `C:\\__MEDLINK___\\LABA\\sql\\`:
- `sql/00_master_deploy_all.sql` — Майстер-розгортання всіх структур
- `sql/01_lis_schema_core.sql` — Основні таблиці ЛІС
- `sql/02_lis_schema_microbiology_eucast.sql` — Бактеріологія EUCAST
- `sql/03_evomis_integration_views_and_fk.sql` — Зв'язки з evomis
- `sql/04..09_seed_*.sql` — Довідники біоматеріалів, пробірок, методик, тестів, аналізаторів
"""

with open(MD_OUT, "w", encoding="utf-8") as f:
    f.write(md_content)
with open(MD_ASCII, "w", encoding="utf-8") as f:
    f.write(md_content)

print(f"Markdown written to {MD_OUT} and {MD_ASCII} (Size: {len(md_content)} bytes)")
print("Done!")
