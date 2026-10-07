# -*- coding: utf-8 -*-
"""
Master Generator for all 12 MedLink LIS Interactive Prototypes.
Includes full explanatory informational guides, real MedLink (evomis) Quasar styling,
SVG graphs, tables, interactive buttons, alerts, and navigation links.
Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
"""

import os

PROTOTYPES_DIR = r"c:\__MEDLINK___\LABA\prototypes"

MODULES = [
    {
        "id": "01",
        "file": "01_phlebotomy_station.html",
        "title": "Пункт забору біоматеріалу (Phlebotomy Station)",
        "short_title": "Пункт забору & Маркування",
        "role": "Медсестра забору (Phlebotomist)",
        "icon": "💉",
        "crumb": "ПУНКТ ЗАБОРУ ТА МАРКУВАННЯ"
    },
    {
        "id": "02",
        "file": "02_specimen_logistics.html",
        "title": "Логістика біоматеріалів & Стіл прийому і бракеражу",
        "short_title": "Логістика & Приймальний стіл",
        "role": "Кур'єр / Співробітник зони прийому",
        "icon": "🚚",
        "crumb": "ЛОГІСТИКА ТА ПРИЙОМ"
    },
    {
        "id": "03",
        "file": "03_lab_workstation.html",
        "title": "Робоче місце «Дослідження» (Апаратна черга)",
        "short_title": "Журнал досліджень лаборанта",
        "role": "Фельдшер-лаборант / Оператор аналізатора",
        "icon": "🔬",
        "crumb": "РОБОЧЕ МІСЦЕ ЛАБОРАНТА"
    },
    {
        "id": "04",
        "file": "04_validation_and_panic.html",
        "title": "Верифікація лікаря & Критичні сповіщення (Panic Values)",
        "short_title": "Верифікація & Panic Values",
        "role": "Лікар-лаборант / Завідувач КДЛ",
        "icon": "👨‍⚕️",
        "crumb": "ВЕРИФІКАЦІЯ ТА АЛЕРТИ"
    },
    {
        "id": "05",
        "file": "05_quality_control.html",
        "title": "Внутрішній контроль якості (ВЯК & Графіки Леві-Дженнінгса)",
        "short_title": "Контроль якості (QC & Вестгард)",
        "role": "Менеджер якості лабораторії",
        "icon": "📊",
        "crumb": "КОНТРОЛЬ ЯКОСТІ"
    },
    {
        "id": "06",
        "file": "06_patient_portal.html",
        "title": "Особистий кабінет пацієнта (Live Трекінг аналізів)",
        "short_title": "Кабінет пацієнта & Трекер",
        "role": "Пацієнт / Довірена особа",
        "icon": "📱",
        "crumb": "КАБІНЕТ ПАЦІЄНТА"
    },
    {
        "id": "07",
        "file": "07_biobank_archive.html",
        "title": "Фізичний архів біоматеріалів (Біобанк зразків)",
        "short_title": "Біобанк & Архів штативів",
        "role": "Архіваріус лабораторії",
        "icon": "🧊",
        "crumb": "БІОБАНК ТА АРХІВ"
    },
    {
        "id": "08",
        "file": "08_reagent_inventory.html",
        "title": "Складський облік реагентів на борту приладів",
        "short_title": "Склад реагентів (Lot Tracking)",
        "role": "Матеріально відповідальна особа / Старший лаборант",
        "icon": "🧪",
        "crumb": "ОБЛІК РЕАГЕНТІВ"
    },
    {
        "id": "09",
        "file": "09_microbiology_culture.html",
        "title": "Бактеріологічний посів & Антибіотикограма (EUCAST)",
        "short_title": "Бактеріологія & Антибіотикограма",
        "role": "Лікар-бактеріолог / Мікробіолог",
        "icon": "🧫",
        "crumb": "БАКТЕРІОЛОГІЧНИЙ ПОСІВ"
    },
    {
        "id": "10",
        "file": "10_lab_analytics_tat.html",
        "title": "Аналітичний дашборд лабораторії (TAT & KPI Моніторинг)",
        "short_title": "Дашборд KPI & TAT Менеджмент",
        "role": "Директор лабораторії / Головний лікар",
        "icon": "📈",
        "crumb": "АНАЛІТИКА ТА TAT"
    },
    {
        "id": "11",
        "file": "11_analyzer_connector_monitor.html",
        "title": "Монітор шлюзу аналізаторів (MedLink.LabConnector)",
        "short_title": "Шлюз аналізаторів (Драйвери)",
        "role": "Інженер ЛІС / Системний адміністратор",
        "icon": "🔌",
        "crumb": "ШЛЮЗ АНАЛІЗАТОРІВ"
    },
    {
        "id": "12",
        "file": "12_norms_and_methodologies.html",
        "title": "Конструктор референтних норм, методик та розрахунків",
        "short_title": "Конструктор норм & Методики",
        "role": "Клінічний біохімік / Методолог КДЛ",
        "icon": "📐",
        "crumb": "ДОВІДНИК НОРМ ТА МЕТОДИК"
    }
]

def generate_header_html():
    return '''<header class="medlink-header">
    <button class="drawer-toggle" onclick="toggleSidebar()" title="Згорнути / розгорнути меню">&#9776;</button>
    <a href="../README_ЛАБОРАТОРІЯ_MEDLINK.html" class="medlink-brand">
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
      <a href="../ТЗ_ЛІС_MedLink_v3_Повне.html" class="q-btn q-btn-outline" style="height:28px; font-size:11px; text-decoration:none;">📄 ТЗ Лабораторія</a>
      <a href="../README_ЛАБОРАТОРІЯ_MEDLINK.html" class="q-btn q-btn-outline" style="height:28px; font-size:11px; text-decoration:none;">📋 Зведений Гайд</a>
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
  </header>'''

def generate_drawer_html(active_id):
    items_html = ""
    for mod in MODULES:
        is_active = " active" if mod["id"] == active_id else ""
        items_html += f'''        <li>
          <a href="{mod['file']}" class="drawer-nav-item{is_active}">
            <span class="nav-icon">{mod['icon']}</span>
            <span>{mod['id']}. {mod['short_title']}</span>
          </a>
        </li>\n'''
    
    return f'''    <aside class="medlink-drawer" id="sidebarDrawer">
      <div class="drawer-section-title">Основні модулі МІС</div>
      <ul class="drawer-nav-list">
        <li><a href="#" class="drawer-nav-item"><span class="nav-icon">🏠</span><span>Головна</span></a></li>
        <li><a href="#" class="drawer-nav-item"><span class="nav-icon">📅</span><span>Розклад</span></a></li>
        <li><a href="#" class="drawer-nav-item"><span class="nav-icon">📋</span><span>Направлення ЕСОЗ</span></a></li>
      </ul>

      <div class="drawer-divider"></div>

      <div class="drawer-section-title" style="color:#60a5fa;">Лабораторія MedLink (12 Процесів)</div>
      <ul class="drawer-nav-list">
{items_html}      </ul>
    </aside>'''

def generate_info_banner(mod_info, purpose_text, workflow_steps, standards_text):
    steps_li = "".join([f"<li>{step}</li>" for step in workflow_steps])
    return f'''      <!-- MedLink Explanatory Guide Card -->
      <div class="q-card" style="border-left: 4px solid var(--accent); background: #f8fafc; margin-bottom: 16px;">
        <div class="q-card-header" style="background: transparent; border-bottom: 1px solid #e2e8f0; padding: 10px 16px;">
          <div class="q-card-title" style="color: var(--primary); font-size: 13.5px;">
            💡 <strong>Клінічно-технічне роз'яснення процесу:</strong> {mod_info['title']}
          </div>
          <span class="q-badge q-badge-info">Стандарти: {standards_text}</span>
        </div>
        <div class="q-card-body" style="padding: 12px 16px; font-size: 13px; line-height: 1.6;">
          <p style="margin-bottom: 8px;"><strong>Призначення та мета процесу:</strong> {purpose_text}</p>
          <div style="margin-bottom: 6px;"><strong>Покроковий регламент виконання:</strong></div>
          <ol style="padding-left: 20px; color: #374151; margin-bottom: 6px;">
            {steps_li}
          </ol>
          <div style="font-size: 12px; color: var(--text-muted);">
            🔐 <strong>Відповідальна роль:</strong> <span style="color:var(--primary); font-weight:700;">{mod_info['role']}</span> | 
            🔗 <strong>Інтеграція:</strong> БД MedLink <code>evomis_db</code>, протоколи автоматизації та ЕСОЗ eHealth.
          </div>
        </div>
      </div>
'''

def wrap_prototype(mod_info, body_content, purpose_text, workflow_steps, standards_text):
    header_html = generate_header_html()
    drawer_html = generate_drawer_html(mod_info["id"])
    info_banner_html = generate_info_banner(mod_info, purpose_text, workflow_steps, standards_text)
    
    return f'''<!DOCTYPE html>
<html lang="uk">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>MedLink LIS — {mod_info['title']}</title>
  <link rel="stylesheet" href="medlink-theme.css">
</head>
<body>
  {header_html}

  <div class="medlink-app-layout">
{drawer_html}

    <main class="medlink-main-container">
      <!-- MedLink Breadcrumbs -->
      <nav class="medlink-breadcrumbs">
        <a href="../README_ЛАБОРАТОРІЯ_MEDLINK.html">ГОЛОВНА</a>
        <span class="sep">/</span>
        <a href="../ТЗ_ЛІС_MedLink_v3_Повне.html">ЛАБОРАТОРІЯ</a>
        <span class="sep">/</span>
        <span class="current">{mod_info['crumb']}</span>
      </nav>

      <!-- MedLink Page Header -->
      <div class="medlink-page-header">
        <h1>
          <span>{mod_info['title']}</span>
        </h1>
        <div style="display:flex; align-items:center; gap:10px;">
          <span class="role-badge">Роль: {mod_info['role']}</span>
          <button class="q-btn q-btn-outline" style="height:28px;" onclick="window.history.back()">Назад</button>
        </div>
      </div>

{info_banner_html}

      <!-- Main Body Content -->
{body_content}

    </main>
  </div>

  <script>
    function toggleSidebar() {{
      const drawer = document.getElementById('sidebarDrawer');
      if (drawer.style.display === 'none') {{
        drawer.style.display = 'flex';
      }} else {{
        drawer.style.display = 'none';
      }}
    }}
  </script>
</body>
</html>
'''

print("Generator framework ready.")
