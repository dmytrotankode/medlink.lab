def get_part1():
    return """<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>ТЕХНІЧНЕ ЗАВДАННЯ: Модуль «Лабораторна інформаційна система» (MedLink LIS 3.0)</title>
<style>
:root {
  --primary: #2563eb;
  --primary-dark: #1d4ed8;
  --primary-light: #eff6ff;
  --secondary: #0f172a;
  --accent: #0ea5e9;
  --success: #10b981;
  --warning: #f59e0b;
  --danger: #ef4444;
  --purple: #8b5cf6;
  --surface: #ffffff;
  --background: #f8fafc;
  --border: #e2e8f0;
  --text-main: #1e293b;
  --text-muted: #64748b;
  --code-bg: #1e293b;
  --font: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  --shadow-sm: 0 1px 2px 0 rgb(0 0 0 / 0.05);
  --shadow-md: 0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1);
  --shadow-lg: 0 10px 15px -3px rgb(0 0 0 / 0.1), 0 4px 6px -4px rgb(0 0 0 / 0.1);
}

* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: var(--font); background: var(--background); color: var(--text-main); line-height: 1.6; }

/* Layout */
.app-container { display: flex; min-height: 100vh; }
.sidebar { width: 310px; background: #0f172a; color: #f8fafc; position: sticky; top: 0; height: 100vh; overflow-y: auto; padding: 24px 16px; flex-shrink: 0; border-right: 1px solid #1e293b; }
.sidebar-logo { display: flex; align-items: center; gap: 12px; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid #334155; }
.sidebar-logo .badge { background: #3b82f6; color: white; padding: 4px 8px; border-radius: 6px; font-weight: 700; font-size: 12px; }
.sidebar-logo h2 { font-size: 18px; font-weight: 700; color: #fff; letter-spacing: -0.5px; }
.nav-group-title { font-size: 11px; text-transform: uppercase; color: #94a3b8; font-weight: 700; letter-spacing: 1px; margin: 20px 8px 8px; }
.nav-link { display: block; padding: 8px 12px; color: #cbd5e1; text-decoration: none; border-radius: 6px; font-size: 13.5px; margin-bottom: 2px; transition: all 0.2s; }
.nav-link:hover { background: #1e293b; color: #fff; }
.nav-link.active { background: #2563eb; color: #fff; font-weight: 600; }

.main-content { flex: 1; padding: 40px 48px; max-width: 1200px; }

/* Typography & Headers */
h1 { font-size: 32px; font-weight: 800; color: #0f172a; margin-bottom: 8px; letter-spacing: -0.5px; }
h2 { font-size: 24px; font-weight: 700; color: #0f172a; margin: 36px 0 16px; padding-bottom: 8px; border-bottom: 2px solid var(--border); display: flex; align-items: center; gap: 10px; }
h3 { font-size: 18px; font-weight: 600; color: #1e293b; margin: 24px 0 12px; }
h4 { font-size: 15px; font-weight: 600; color: #334155; margin: 16px 0 8px; }
p { margin-bottom: 14px; font-size: 15px; color: #334155; }

/* Hero Card */
.hero-card { background: linear-gradient(135deg, #1e3a8a 0%, #0284c7 100%); color: white; border-radius: 16px; padding: 32px; margin-bottom: 32px; box-shadow: var(--shadow-lg); }
.hero-card h1 { color: white; }
.hero-card p { color: #e0f2fe; font-size: 16px; margin-bottom: 20px; }
.meta-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-top: 20px; background: rgba(255, 255, 255, 0.1); padding: 16px; border-radius: 10px; }
.meta-item { display: flex; flex-direction: column; }
.meta-label { font-size: 12px; text-transform: uppercase; color: #bae6fd; font-weight: 600; }
.meta-value { font-size: 14px; font-weight: 700; color: #ffffff; }

/* Cards & Containers */
.card { background: var(--surface); border: 1px solid var(--border); border-radius: 12px; padding: 24px; margin-bottom: 24px; box-shadow: var(--shadow-sm); }
.alert-box { padding: 16px 20px; border-radius: 8px; margin-bottom: 20px; border-left: 4px solid; display: flex; gap: 12px; align-items: flex-start; }
.alert-info { background: #eff6ff; border-color: #3b82f6; color: #1e40af; }
.alert-success { background: #ecfdf5; border-color: #10b981; color: #065f46; }
.alert-warning { background: #fffbeb; border-color: #f59e0b; color: #92400e; }
.alert-danger { background: #fef2f2; border-color: #ef4444; color: #991b1b; }

/* Badges */
.badge { display: inline-flex; align-items: center; padding: 3px 8px; border-radius: 9999px; font-size: 12px; font-weight: 600; text-transform: uppercase; }
.badge-must { background: #fee2e2; color: #b91c1c; border: 1px solid #f87171; }
.badge-should { background: #fef3c7; color: #b45309; border: 1px solid #fcd34d; }
.badge-could { background: #e0e7ff; color: #4338ca; border: 1px solid #a5b4fc; }
.badge-r1 { background: #dcfce7; color: #15803d; border: 1px solid #86efac; }
.badge-r2 { background: #e0f2fe; color: #0369a1; border: 1px solid #7dd3fc; }
.badge-r3 { background: #f3e8ff; color: #7e22ce; border: 1px solid #d8b4fe; }

/* Tables */
.table-wrapper { overflow-x: auto; margin: 16px 0 24px; border-radius: 8px; border: 1px solid var(--border); box-shadow: var(--shadow-sm); }
table { width: 100%; border-collapse: collapse; background: var(--surface); text-align: left; font-size: 13.5px; }
th { background: #f1f5f9; padding: 12px 14px; font-weight: 600; color: #334155; border-bottom: 2px solid var(--border); }
td { padding: 12px 14px; border-bottom: 1px solid var(--border); color: #334155; vertical-align: top; }
tr:last-child td { border-bottom: none; }
tr:hover td { background: #f8fafc; }

/* Code Blocks */
pre { background: var(--code-bg); color: #e2e8f0; padding: 18px; border-radius: 8px; overflow-x: auto; font-family: "Consolas", "Courier New", monospace; font-size: 13px; line-height: 1.5; margin: 14px 0 20px; }
code { background: #f1f5f9; color: #0f172a; padding: 2px 6px; border-radius: 4px; font-family: monospace; font-size: 13px; }
pre code { background: none; color: inherit; padding: 0; }

/* Interactive Simulator Containers */
.simulator-box { border: 2px solid #3b82f6; border-radius: 12px; background: #ffffff; padding: 20px; margin: 24px 0; box-shadow: var(--shadow-md); }
.sim-header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border); padding-bottom: 12px; margin-bottom: 16px; }
.sim-title { font-weight: 700; color: #1e3a8a; display: flex; align-items: center; gap: 8px; }

/* Buttons & Inputs */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 8px 16px; border-radius: 6px; font-weight: 600; font-size: 13px; cursor: pointer; transition: all 0.2s; border: none; }
.btn-primary { background: #2563eb; color: #fff; }
.btn-primary:hover { background: #1d4ed8; }
.btn-success { background: #10b981; color: #fff; }
.btn-success:hover { background: #059669; }
.btn-outline { background: transparent; border: 1px solid var(--border); color: #334155; }
.btn-outline:hover { background: #f1f5f9; }

/* Grid columns */
.grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
.grid-3 { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
@media (max-width: 900px) { .grid-2, .grid-3 { grid-template-columns: 1fr; } }
</style>
</head>
<body>

<div class="app-container">
  <!-- Sidebar Navigation -->
  <aside class="sidebar">
    <div class="sidebar-logo">
      <span class="badge">LIS 3.0</span>
      <h2>MedLink LIS</h2>
    </div>
    
    <div class="nav-group-title">СТРАТЕГІЯ ТА КОНТЕКСТ</div>
    <a href="#sec-summary" class="nav-link active">1. Паспорт проєкту & Vision</a>
    <a href="#sec-benchmark" class="nav-link">2. Аналіз конкурентів & Delphi</a>
    <a href="#sec-processes-directory" class="nav-link" style="color: #60a5fa; font-weight:bold;">⚡ 2.1. Реєстр 12 процесів & Прототипи</a>
    <a href="#sec-registry" class="nav-link">3. Реєстр 48 модулів (MoSCoW)</a>
    
    <div class="nav-group-title">КЛІНІЧНІ РОБОЧІ МІСЦЯ</div>
    <a href="#sec-phlebotomy" class="nav-link">4. Кабінет забору & Штрихкоди</a>
    <a href="#sec-logistics" class="nav-link">5. Логістика & Бракераж зразків</a>
    <a href="#sec-workstation" class="nav-link">6. Робоче місце «Дослідження»</a>
    <a href="#sec-qc" class="nav-link">7. Контроль якості (ВЯК / Levey-J)</a>
    
    <div class="nav-group-title">ІНТЕГРАЦІЇ ТА ДРАЙВЕРИ</div>
    <a href="#sec-connector" class="nav-link">8. Драйверний коннектор (.NET)</a>
    <a href="#sec-interop" class="nav-link">9. HL7, FHIR, eHealth, СМЕ</a>
    <a href="#sec-patient" class="nav-link">10. Кабінет пацієнта & Трекінг</a>
    
    <div class="nav-group-title">ТЕХНІЧНА АРХІТЕКТУРА</div>
    <a href="#sec-architecture" class="nav-link">11. Архітектура в MedLink (evomis)</a>
    <a href="#sec-db" class="nav-link">12. Схема БД PostgreSQL</a>
    <a href="#sec-gaps" class="nav-link">13. GAP-аналіз & Що додали</a>
    <a href="#sec-roadmap" class="nav-link">14. Дорожня карта релізів</a>
    <a href="#sec-appendix" class="nav-link">15. Додатки: Довідники (JSON/SQL)</a>
  </aside>

  <!-- Main Content -->
  <main class="main-content">
    
    <!-- Hero Banner -->
    <div class="hero-card" id="sec-summary">
      <h1>ТЕХНІЧНЕ ЗАВДАННЯ НА РОЗРОБКУ ЛІС</h1>
      <p>Повнофункціональна лабораторна інформаційна система (MedLink LIS 3.0): вбудований модуль платформи MedLink (evomis) та автономний продукт для мереж лабораторій</p>
      
      <div class="meta-grid">
        <div class="meta-item">
          <span class="meta-label">Правовласник</span>
          <span class="meta-value">ТОВ «МедЛінк» (MedLink LLC)</span>
        </div>
        <div class="meta-item">
          <span class="meta-label">Версія документа</span>
          <span class="meta-value">3.0 Comprehensive (Жовтень 2026)</span>
        </div>
        <div class="meta-item">
          <span class="meta-label">Технічний стек</span>
          <span class="meta-value">.NET 6 / PostgreSQL / Vue Quasar / .NET Core Worker</span>
        </div>
        <div class="meta-item">
          <span class="meta-label">Статус узгодження</span>
          <span class="meta-value">Затверджено до розробки (Sprints 1–6)</span>
        </div>
      </div>
    </div>

    <!-- Section 1: Executive Summary -->
    <section>
      <h2>1. Паспорт проєкту та продуктове позиціонування</h2>
      
      <p>Проєкт <strong>MedLink LIS 3.0</strong> покликаний реалізувати сучасну, високоавтоматизовану, клінічно достовірну лабораторну інформаційну систему з відкритою інтеграційною шиною, що вирішує подвійне бізнес-завдання:</p>
      
      <div class="grid-2">
        <div class="card">
          <h3 style="color: #2563eb;">1. Вбудований лабораторний контур MedLink</h3>
          <p>Безшовна частина діючої медичної інформаційної системи <code>evomis</code>. Закриває повний діагностичний цикл для лікарень та приватних клінік: лікар у картці створює призначення або електронне направлення ЕСОЗ &rarr; лаборант у пункті забору маркує пробірку &rarr; аналізатор видає результат &rarr; лікар-лаборант валідує &rarr; дані автоматично повертаються в ЕМК пацієнта та гасяться в eHealth (DiagnosticReport) з КЕП.</p>
        </div>
        <div class="card">
          <h3 style="color: #0d9488;">2. Standalone LIS (Самостійний продукт)</h3>
          <p>Автономна LIS корпоративного рівня для комерційних лабораторних мереж, пунктів забору та приватних кабінетів, що не використовують МІС MedLink. Включає власний білінг, склад реагентів, логістику зразків, модуль внутрішнього контролю якості (ВЯК) за Вестгардом та інтеграцію із зовнішніми МІС через HL7 v2 / FHIR / REST API.</p>
        </div>
      </div>

      <div class="alert-box alert-info">
        <div>
          <strong>Ключова перевага MedLink LIS:</strong> Повна ліквідація десктопного легасі-коду (Delphi). Перехід на 100% хмарну архітектуру (Web/PWA на Quasar) з кросплатформним системним демоном зв'язку з приладами на .NET Core (Windows Service / Linux systemd daemon), що забезпечує безвідмовний обмін з приладами без прив'язки до робочого місця оператора.
        </div>
      </div>
    </section>

    <!-- Section 2: Competitive Benchmark & Delphi Audit -->
    <section id="sec-benchmark">
      <h2>2. Аналіз конкурентів, матеріалів MCLAB/TerraLab та аудит Delphi</h2>
      
      <p>В ході ретельного аудиту проаналізовано 4 ключові джерела: legacy-код Delphi (<code>C:\\Desktop\\src</code>, база <code>hospital_etalon</code>, плагін <code>AConnect</code>), систему LIMS TerraLab Pro, документацію MCLAB (папка <code>C:\\__MEDLINK___\\Лаборатория\\MCLAB</code>) та функціонал промислових LIS (STARLIS, LISmart):</p>

      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Компонент / Система</th>
              <th>Legacy Delphi (AConnect / hospital_etalon)</th>
              <th>Конкуренти (TerraLab, MCLAB, LISmart)</th>
              <th>Рішення MedLink LIS 3.0 (.NET 6 / Web)</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><strong>Архітектура зв'язку з приладами</strong></td>
              <td>Delphi VCL застосунок (AConnectAstm.exe), прив'язаний до COM/TCP. Монолітний розбір в SQL процедурі <code>parse_lab_message</code> (142 КБ коду з блокуваннями таблиць).</td>
              <td>Окремі десктопні драйвери під кожен прилад, часті падіння при перезавантаженні ПК, відсутність буферизації.</td>
              <td><strong>MedLink.LabConnector:</strong> фоновий worker на .NET Core (Windows Service + Linux systemd). Локальний SQLite offline-буфер. Асинхронний розбір ASTM E1381/E1394 та HL7 MLLP на рівні коду C# з відправкою JSON через HTTPS REST API.</td>
            </tr>
            <tr>
              <td><strong>Контроль якості (QC / ВЯК)</strong></td>
              <td>Повністю відсутній у коді Delphi та базі даних.</td>
              <td>У TerraLab/STARLIS є графіки Леві-Дженнінгса та правила Вестгарда, але жорстко прив'язані до десктопу, без автоматичного блокування видачі пацієнтських результатів.</td>
              <td><strong>Повноцінний веб-модуль ВЯК:</strong> 3 рівні контролів (Low/Normal/High), мультивідхилення Вестгарда (1-2s, 1-3s, 2-2s, R-4s, 4-1s, 10-x), автоматичний <em>Lockout</em> (блокування валідації пацієнтських тестів при порушенні), інтерактивні карти Леві-Дженнінгса.</td>
            </tr>
            <tr>
              <td><strong>Пункт забору & Маркування</strong></td>
              <td>Базовий друк штрихкодів через генератор <code>gen_lab_tube_barcode</code> (EAN-8/128). Немає контролю черговості пробірок (Order of Draw).</td>
              <td>В MCLAB є окремі інструкції («Додавання біоматеріалу та друк етикеток.pdf»). Підтримка термопринтерів.</td>
              <td><strong>Інтелектуальний кабінет забору:</strong> автогрупування тестів у пробірки за типами біоматеріалу, обов'язковий порядок набору за CLSI (культури &rarr; цитрат &rarr; сироватка &rarr; гепарин &rarr; ЕДТА &rarr; фторид), прямий друк на Zebra/TSC через ZPL/TSPL.</td>
            </tr>
            <tr>
              <td><strong>Логістика & Температурний ланцюг</strong></td>
              <td>Відсутня логістика в Delphi.</td>
              <td>У TerraLab є маршрутні листи кур'єра, але без фіксації температури в точках передачі.</td>
              <td><strong>Модуль логістики зразків:</strong> електронні акти прийому-передачі (термосумки), валідація холодового ланцюга (t° відправки та прибуття), мобільний інтерфейс кур'єра зі скануванням штрихкодів сумок.</td>
            </tr>
            <tr>
              <td><strong>Кабінет пацієнта & Моніторинг</strong></td>
              <td>Відсутній.</td>
              <td>Більшість LIS не мають пацієнтського кабінету, віддаючи результати тільки PDF-файлом на email.</td>
              <td><strong>Live-трекер для пацієнта:</strong> інтерактивний таймлайн виконання (Забір &rarr; В дорозі &rarr; Доставлено &rarr; В аналізі &rarr; Готово), графіки персональної динаміки показників, завантаження бланка з захисним QR-кодом автентифікації.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
"""
