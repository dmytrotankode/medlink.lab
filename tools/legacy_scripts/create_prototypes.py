import os

proto_dir = "C:/__MEDLINK___/LABA/prototypes"

shared_css = """
:root {
  --primary: #2563eb;
  --primary-dark: #1d4ed8;
  --success: #10b981;
  --warning: #f59e0b;
  --danger: #ef4444;
  --surface: #ffffff;
  --bg: #f8fafc;
  --border: #e2e8f0;
  --text: #1e293b;
  --muted: #64748b;
  --font: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
}
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: var(--font); background: var(--bg); color: var(--text); padding: 24px; line-height: 1.5; }
.header-bar { display: flex; justify-content: space-between; align-items: center; background: #0f172a; color: white; padding: 16px 24px; border-radius: 12px; margin-bottom: 24px; }
.header-bar h1 { font-size: 20px; font-weight: 700; display: flex; align-items: center; gap: 10px; }
.header-bar .badge { background: #3b82f6; padding: 4px 10px; border-radius: 999px; font-size: 11px; font-weight: 700; text-transform: uppercase; }
.card { background: var(--surface); border: 1px solid var(--border); border-radius: 12px; padding: 20px; margin-bottom: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
.grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
.grid-3 { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
.grid-4 { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
@media (max-width: 900px) { .grid-2, .grid-3, .grid-4 { grid-template-columns: 1fr; } }
.btn { display: inline-flex; align-items: center; justify-content: center; gap: 6px; padding: 8px 16px; border-radius: 6px; font-weight: 600; font-size: 13px; cursor: pointer; border: none; transition: 0.2s; }
.btn-primary { background: var(--primary); color: white; }
.btn-primary:hover { background: var(--primary-dark); }
.btn-success { background: var(--success); color: white; }
.btn-danger { background: var(--danger); color: white; }
.btn-outline { background: transparent; border: 1px solid var(--border); color: var(--text); }
.btn-outline:hover { background: #f1f5f9; }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th { background: #f1f5f9; padding: 10px 12px; text-align: left; font-weight: 600; color: #475569; border-bottom: 2px solid var(--border); }
td { padding: 10px 12px; border-bottom: 1px solid var(--border); }
tr:hover td { background: #f8fafc; }
.badge-status { padding: 3px 8px; border-radius: 999px; font-size: 11px; font-weight: 700; text-transform: uppercase; }
"""

# 1. Prototype 01: Phlebotomy Station
p01 = f"""<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8"><title>Прототип 1: Кабінет забору біоматеріалу (Phlebotomy Station)</title>
<style>{shared_css}</style>
</head>
<body>
<div class="header-bar">
  <h1><span>Кабінет забору біоматеріалу (Пункт №1)</span> <span class="badge">Роль: Медсестра забору</span></h1>
  <div><button class="btn btn-outline" style="color:white; border-color:#334155;" onclick="window.history.back()">Назад до ТЗ</button></div>
</div>

<div class="grid-2">
  <div class="card">
    <h3 style="margin-bottom:12px;">Пошук та ідентифікація пацієнта</h3>
    <div style="display:flex; gap:8px; margin-bottom:16px;">
      <input type="text" value="Коваленко Олександр Сергійович" style="flex:1; padding:8px 12px; border:1px solid var(--border); border-radius:6px; font-size:14px;">
      <button class="btn btn-primary">Знайти</button>
    </div>
    
    <div style="background:#f1f5f9; padding:12px; border-radius:8px; font-size:13px; line-height:1.7;">
      <div><strong>Пацієнт:</strong> Коваленко О.С. (12.04.1985, 41 рік, Чоловік)</div>
      <div><strong>Картка ЕМК:</strong> №108291 | <strong>Тел:</strong> +380 (67) 123-45-67</div>
      <div><strong>Е-направлення ЕСОЗ:</strong> <code>5491-8821-9012</code> (Погашення при заборі)</div>
      <div><strong>Призначені панелі:</strong> ЗАК з формулою + Біохімія розширена + Коагулограма</div>
    </div>

    <h4 style="margin:16px 0 8px;">Преаналітичний чек-лист:</h4>
    <label style="display:block; font-size:13px; margin-bottom:6px;"><input type="checkbox" checked> Пацієнт натщесерце (останній прийом їжі > 8 год тому)</label>
    <label style="display:block; font-size:13px; margin-bottom:6px;"><input type="checkbox" checked> Особу пацієнта підтверджено двофакторно (ПІБ + дата нар.)</label>
    <label style="display:block; font-size:13px; margin-bottom:12px;"><input type="checkbox"> Прийом антикоагулянтів (Варфарин, Ксарелто) за останні 24 год</label>
    
    <div style="display:flex; gap:10px;">
      <button class="btn btn-success" onclick="alert('Біоматеріал зафіксовано як забраний! Друк 3-х етикеток розпочато.')">Підтвердити забір & Друк усіх етикеток</button>
    </div>
  </div>

  <div class="card">
    <h3 style="margin-bottom:12px;">CLSI Order of Draw (Черговість наповнення пробірок)</h3>
    <p style="font-size:12.5px; color:var(--muted); margin-bottom:14px;">Система автоматично згрупувала 18 призначених тестів у <strong>3 пробірки</strong> для мінімізації травмування вен:</p>

    <!-- Tube sequence cards -->
    <div style="display:flex; flex-direction:column; gap:10px;">
      <!-- Tube 1 -->
      <div style="display:flex; align-items:center; gap:12px; padding:10px; border:2px solid #0284c7; border-radius:8px; background:#f0f9ff;">
        <span style="font-size:18px; font-weight:800; color:#0284c7;">1.</span>
        <div style="width:24px; height:24px; border-radius:50%; background:#0284c7;"></div>
        <div style="flex:1;">
          <div style="font-weight:700; font-size:13px;">Цитрат натрію 3.2% (Блакитна кришка) · 3.0 мл</div>
          <div style="font-size:11.5px; color:#475569;">Коагулограма (МНВ, АЧТЧ, Фібриноген). Штрихкод: <code>1026004818</code></div>
        </div>
        <button class="btn btn-outline" style="padding:4px 8px; font-size:11px;" onclick="alert('ZPL етикетку 1026004818 надруковано!')">Друк</button>
      </div>

      <!-- Tube 2 -->
      <div style="display:flex; align-items:center; gap:12px; padding:10px; border:2px solid #ca8a04; border-radius:8px; background:#fefce8;">
        <span style="font-size:18px; font-weight:800; color:#ca8a04;">2.</span>
        <div style="width:24px; height:24px; border-radius:50%; background:#ca8a04;"></div>
        <div style="flex:1;">
          <div style="font-weight:700; font-size:13px;">Активатор згортання / Гель (Жовта кришка) · 5.0 мл</div>
          <div style="font-size:11.5px; color:#475569;">Біохімія (Глюкоза, АЛТ, АСТ, Білірубін, Креатинін). Штрихкод: <code>1026004819</code></div>
        </div>
        <button class="btn btn-outline" style="padding:4px 8px; font-size:11px;" onclick="alert('ZPL етикетку 1026004819 надруковано!')">Друк</button>
      </div>

      <!-- Tube 3 -->
      <div style="display:flex; align-items:center; gap:12px; padding:10px; border:2px solid #9333ea; border-radius:8px; background:#faf5ff;">
        <span style="font-size:18px; font-weight:800; color:#9333ea;">3.</span>
        <div style="width:24px; height:24px; border-radius:50%; background:#9333ea;"></div>
        <div style="flex:1;">
          <div style="font-weight:700; font-size:13px;">K2/K3 ЕДТА (Фіолетова кришка) · 2.6 мл</div>
          <div style="font-size:11.5px; color:#475569;">ЗАК + Лейкоцитарна формула + ШОЕ. Штрихкод: <code>1026004820</code></div>
        </div>
        <button class="btn btn-outline" style="padding:4px 8px; font-size:11px;" onclick="alert('ZPL етикетку 1026004820 надруковано!')">Друк</button>
      </div>
    </div>
  </div>
</div>
</body>
</html>
"""

# 2. Prototype 02: Specimen Logistics
p02 = f"""<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8"><title>Прототип 2: Логістика зразків та преаналітичний бракераж</title>
<style>{shared_css}</style>
</head>
<body>
<div class="header-bar">
  <h1><span>Логістика зразків & Стіл прийому і бракеражу</span> <span class="badge">Роль: Кур'єр / Сортувальник</span></h1>
  <div><button class="btn btn-outline" style="color:white; border-color:#334155;" onclick="window.history.back()">Назад до ТЗ</button></div>
</div>

<div class="grid-2">
  <div class="card">
    <h3>Електронний акт передачі (Маніфест №ACT-2026-1006)</h3>
    <div style="margin:12px 0; font-size:13px; line-height:1.7;">
      <div><strong>Маршрут:</strong> Відділення №1 (м. Київ, вул. Хрещатик, 15) &rarr; Центральна Лабораторія</div>
      <div><strong>Кур'єр:</strong> Гриценко Петро Олексійович (Авто Renault Kangoo AA1234EE)</div>
      <div><strong>Термосумка:</strong> №BOX-04 (Охолоджуючі елементи 2-8°C)</div>
      <div><strong>Температура при виїзді:</strong> <span style="font-weight:bold; color:#16a34a;">+3.8°C</span> (09:15)</div>
      <div><strong>Температура при прийомі:</strong> <span style="font-weight:bold; color:#16a34a;">+4.5°C</span> (10:10) &mdash; <span style="color:#16a34a; font-weight:700;">Холодовий ланцюг збережено</span></div>
    </div>
    <button class="btn btn-primary" onclick="alert('Акт закрито! 24 пробірки переведено в статус [Прийнято лабораторією].')">Підтвердити прийом термосумки</button>
  </div>

  <div class="card">
    <h3>Стіл сортування та реєстрації бракеражу</h3>
    <p style="font-size:12.5px; color:var(--muted); margin-bottom:12px;">Швидкісне сканування пробірки для виявлення дефектів преаналітики:</p>
    <div style="display:flex; gap:8px; margin-bottom:14px;">
      <input type="text" placeholder="Сканувати штрихкод пробірки..." value="1026004819" style="flex:1; padding:8px 12px; border:1px solid var(--border); border-radius:6px; font-size:14px;">
      <button class="btn btn-primary">Пошук</button>
    </div>

    <div style="background:#fffbeb; border:1px solid #fde68a; padding:12px; border-radius:8px; margin-bottom:14px; font-size:13px;">
      <strong>Виявлено дефект біоматеріалу:</strong>
      <div style="margin-top:6px;">
        <label><input type="radio" name="defect" value="HEM" checked> Гемоліз (+++) &mdash; руйнування еритроцитів</label><br>
        <label><input type="radio" name="defect" value="CHYL"> Хілоз (ліпемія сироватки)</label><br>
        <label><input type="radio" name="defect" value="CLOT"> Згусток у цитратній плазмі</label><br>
        <label><input type="radio" name="defect" value="VOL"> Недостатній об'єм для дослідження</label>
      </div>
    </div>
    <button class="btn btn-danger" onclick="alert('Пробірку забраковано! Лікарю та медсестрі забору надіслано термінове завдання на повторний забір.')">Забракувати & Направити на перезабір</button>
  </div>
</div>
</body>
</html>
"""

# 3. Prototype 03: Lab Workstation
p03 = f"""<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8"><title>Прототип 3: Робоче місце лаборанта «Дослідження»</title>
<style>{shared_css}</style>
</head>
<body>
<div class="header-bar">
  <h1><span>Робоче місце «Дослідження» (Апаратна черга)</span> <span class="badge">Роль: Лаборант</span></h1>
  <div><button class="btn btn-outline" style="color:white; border-color:#334155;" onclick="window.history.back()">Назад до ТЗ</button></div>
</div>

<div class="card" style="padding:14px 20px; display:flex; justify-content:space-between; align-items:center;">
  <div style="display:flex; gap:12px; align-items:center;">
    <input type="text" placeholder="Фільтр пацієнта або штрихкоду..." style="padding:6px 10px; border:1px solid var(--border); border-radius:6px; font-size:13px;">
    <select style="padding:6px 10px; border:1px solid var(--border); border-radius:6px; font-size:13px;">
      <option>Всі прилади (Sysmex, Cobas, Mindray)</option>
      <option>Sysmex XN-1000 (Гематологія)</option>
      <option>Roche Cobas e411 (Імунохімія)</option>
      <option>Mindray BS-240 (Біохімія)</option>
    </select>
  </div>
  <div style="font-size:12px; color:var(--muted);">
    В черзі: <strong>148</strong> проб | Автоматично верифіковано: <strong>112</strong> | Потребують уваги: <strong style="color:#ef4444;">4</strong>
  </div>
</div>

<div class="card" style="padding:0; overflow:hidden;">
  <table>
    <thead>
      <tr>
        <th>Штрихкод</th>
        <th>Пацієнт</th>
        <th>Дослідження</th>
        <th>Прилад</th>
        <th>Показник</th>
        <th>Значення</th>
        <th>Норма</th>
        <th>Прапорці (Flags)</th>
        <th>Дія</th>
      </tr>
    </thead>
    <tbody>
      <tr style="background:#fef2f2;">
        <td><code>1026004812</code></td>
        <td><strong>Мельник Ю.В.</strong></td>
        <td>Глюкоза</td>
        <td>Cobas e411</td>
        <td>GLU</td>
        <td><strong style="color:#ef4444; font-size:15px;">26.4</strong> ммоль/л</td>
        <td>4.1 - 5.9</td>
        <td><span class="badge-status" style="background:#ef4444; color:white;">PANIC HIGH</span></td>
        <td><button class="btn btn-danger" style="padding:3px 8px; font-size:11px;" onclick="alert('Алерт передано лікуючому лікарю!')">Викликати лікаря</button></td>
      </tr>
      <tr>
        <td><code>1026004815</code></td>
        <td><strong>Шевченко І.П.</strong></td>
        <td>ЗАК розгорнутий</td>
        <td>Sysmex XN-1000</td>
        <td>HGB</td>
        <td><strong>128.0</strong> г/л</td>
        <td>120 - 140</td>
        <td><span class="badge-status" style="background:#ecfdf5; color:#065f46;">NORMAL (Auto)</span></td>
        <td><button class="btn btn-success" style="padding:3px 8px; font-size:11px;" onclick="alert('Збережено!')">ОК</button></td>
      </tr>
      <tr style="background:#fffbeb;">
        <td><code>1026004819</code></td>
        <td><strong>Коваленко О.С.</strong></td>
        <td>Печінкові проби</td>
        <td>Mindray BS-240</td>
        <td>ALT</td>
        <td><strong style="color:#b45309;">68.5</strong> U/L</td>
        <td>0 - 41</td>
        <td><span class="badge-status" style="background:#fef3c7; color:#92400e;">DELTA +42%</span></td>
        <td><button class="btn btn-outline" style="padding:3px 8px; font-size:11px;" onclick="alert('Перенаправлено на перегляд лікаря-лаборанта!')">На перегляд</button></td>
      </tr>
    </tbody>
  </table>
</div>
</body>
</html>
"""

# 4. Prototype 04: Validation & Panic
p04 = f"""<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8"><title>Прототип 4: Верифікація лікаря-лаборанта та критичні алерти</title>
<style>{shared_css}</style>
</head>
<body>
<div class="header-bar">
  <h1><span>Екран лікаря-лаборанта & Критичні сповіщення (Panic Values)</span> <span class="badge">Роль: Лікар-лаборант</span></h1>
  <div><button class="btn btn-outline" style="color:white; border-color:#334155;" onclick="window.history.back()">Назад до ТЗ</button></div>
</div>

<div class="card" style="border-left: 6px solid #ef4444; background:#fef2f2;">
  <div style="display:flex; justify-content:space-between; align-items:center;">
    <div>
      <h3 style="color:#991b1b; margin-bottom:4px;">🚨 НЕВІДКЛАДНЕ ПОВІДОМЛЕННЯ: КРИТИЧНЕ ЗНАЧЕННЯ (PANIC VALUE)</h3>
      <p style="color:#7f1d1d; margin:0; font-size:13.5px;">Пацієнт <strong>Мельник Ю.В.</strong>, 48 років. Глюкоза сироватки = <strong>26.4 ммоль/л</strong> (Загроза гіперосмолярної коми!).</p>
    </div>
    <button class="btn btn-danger" onclick="alert('Сповіщення успішно надіслано у мобільний додаток та МІС чергового лікаря-реаніматолога! Зафіксовано таймштамп доставки.')">Надіслати терміновий Alert лікарю</button>
  </div>
</div>

<div class="grid-2">
  <div class="card">
    <h3>Верифікація замовлення №1026-004819</h3>
    <div style="font-size:13px; line-height:1.7; margin-bottom:14px;">
      <div><strong>Пацієнт:</strong> Коваленко О.С. (41 рік, Чоловік)</div>
      <div><strong>Прилад:</strong> Mindray BS-240 | <strong>Біоматеріал:</strong> Сироватка (жовта кришка)</div>
      <div><strong>Delta-Check:</strong> АЛТ зріс з 48.0 до 68.5 U/L за 48 годин (+42.7%).</div>
    </div>
    <div style="margin-bottom:14px;">
      <label style="font-size:12.5px; font-weight:600;">Клінічний коментар лікаря-лаборанта до бланка:</label>
      <textarea style="width:100%; height:60px; padding:6px; border:1px solid var(--border); border-radius:6px; font-size:13px;">Помірний цитолітичний синдром. Рекомендовано контроль вірусних гепатитів.</textarea>
    </div>
    <button class="btn btn-primary" onclick="alert('Накладено Кваліфікований електронний підпис (КЕП)! Дослідження закрито та опубліковано в ЕСОЗ.')">Підписати КЕП & Затвердити</button>
  </div>

  <div class="card">
    <h3>Reflex-Engine (Правила автодопризначення)</h3>
    <div style="background:#f8fafc; border:1px solid var(--border); padding:12px; border-radius:8px; font-size:13px; line-height:1.7;">
      <div style="font-weight:700; color:#1e3a8a;">Правило №REF-04 (Тиреоїдний алгоритм):</div>
      <div>Умова: <code>TSH &lt; 0.4 uIU/mL</code> або <code>TSH &gt; 4.0 uIU/mL</code></div>
      <div>Поточний TSH = <strong>0.12 uIU/mL</strong> (Знижений)</div>
      <div style="margin-top:8px; color:#15803d; font-weight:600;">&rarr; Автоматично замовлено: Тироксин вільний (вТ4) з наявного зразка сироватки без виклику пацієнта.</div>
    </div>
  </div>
</div>
</body>
</html>
"""

# 5. Prototype 05: Quality Control
p05 = f"""<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8"><title>Прототип 5: Внутрішній контроль якості (ВЯК)</title>
<style>{shared_css}</style>
</head>
<body>
<div class="header-bar">
  <h1><span>Модуль Внутрішнього контролю якості (ВЯК)</span> <span class="badge">Роль: Менеджер якості</span></h1>
  <div><button class="btn btn-outline" style="color:white; border-color:#334155;" onclick="window.history.back()">Назад до ТЗ</button></div>
</div>

<div class="grid-3" style="margin-bottom:16px;">
  <div class="card" style="margin:0;">
    <div style="font-size:12px; color:var(--muted);">Аналізатор</div>
    <div style="font-size:16px; font-weight:700;">Sysmex XN-1000</div>
  </div>
  <div class="card" style="margin:0;">
    <div style="font-size:12px; color:var(--muted);">Контрольний матеріал</div>
    <div style="font-size:16px; font-weight:700;">XN-CHECK Level 2 (Normal)</div>
  </div>
  <div class="card" style="margin:0;">
    <div style="font-size:12px; color:var(--muted);">Паспортні параметри WBC</div>
    <div style="font-size:16px; font-weight:700;">Mean = 7.20 | SD = 0.30</div>
  </div>
</div>

<div class="card">
  <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px;">
    <h3>Карта Леві-Дженнінгса (Останні 15 днів вимірювань)</h3>
    <span class="badge-status" style="background:#fee2e2; color:#b91c1c; border:1px solid #f87171;">СТАТУС: LOCKOUT (ЗБІЙ 1-3s)</span>
  </div>

  <div style="background:#ffffff; border:1px solid var(--border); border-radius:8px; padding:16px; text-align:center;">
    <p style="font-size:13px; color:#475569; margin-bottom:10px;">Діаграма Levey-Jennings з відміткою точки порушення правила Вестгарда 1-3s (Day 15 = 8.28 > +3SD):</p>
    <svg width="100%" height="180" viewBox="0 0 600 180">
      <rect x="40" y="10" width="540" height="150" fill="#f8fafc"/>
      <rect x="40" y="35" width="540" height="100" fill="#ecfdf5" opacity="0.6"/>
      <line x1="40" y1="10" x2="580" y2="10" stroke="#ef4444" stroke-dasharray="3,3"/>
      <line x1="40" y1="85" x2="580" y2="85" stroke="#0284c7" stroke-width="2"/>
      <line x1="40" y1="160" x2="580" y2="160" stroke="#ef4444" stroke-dasharray="3,3"/>
      <polyline fill="none" stroke="#2563eb" stroke-width="2" points="60,80 100,75 140,90 180,65 220,85 260,80 300,60 340,90 380,100 420,82 460,40 500,85 540,12"/>
      <circle cx="540" cy="12" r="6" fill="#ef4444" stroke="#ffffff" stroke-width="2"/>
    </svg>
  </div>

  <div style="margin-top:14px; background:#fef2f2; border:1px solid #fecaca; border-radius:8px; padding:12px; display:flex; justify-content:space-between; align-items:center;">
    <div style="font-size:13px; color:#991b1b;">
      <strong>Автоматичний Lockout активовано:</strong> Видача результатів ЗАК з приладу Sysmex XN-1000 тимчасово заблокована.
    </div>
    <button class="btn btn-outline" style="border-color:#f87171; color:#b91c1c; font-size:12px;" onclick="alert('Форма внесення причини збою: виконано промивку апертури та повторне вимірювання. Lockout знято.')">Протокол усунення збою</button>
  </div>
</div>
</body>
</html>
"""

# 6. Prototype 06: Patient Portal
p06 = f"""<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8"><title>Прототип 6: Кабінет пацієнта та моніторинг замовлення</title>
<style>{shared_css}</style>
</head>
<body>
<div class="header-bar">
  <h1><span>Особистий кабінет пацієнта MedLink</span> <span class="badge">Пацієнт: Коваленко О.С.</span></h1>
  <div><button class="btn btn-outline" style="color:white; border-color:#334155;" onclick="window.history.back()">Назад до ТЗ</button></div>
</div>

<div class="card">
  <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:20px;">
    <div>
      <h2 style="font-size:18px; margin:0;">Замовлення №1026-004819 від 06.10.2026</h2>
      <span style="font-size:12px; color:var(--muted);">Комплексний аналіз стану здоров'я</span>
    </div>
    <button class="btn btn-success" onclick="alert('Завантажується офіційний підписаний КЕП PDF-бланк з перевірочним QR-кодом!')">Завантажити офіційний PDF-бланк</button>
  </div>

  <!-- Stepper -->
  <div style="display:flex; justify-content:space-between; text-align:center; padding:10px 0 24px; position:relative;">
    <div style="position:absolute; top:22px; left:30px; right:30px; height:4px; background:#10b981; z-index:1;"></div>
    
    <div style="position:relative; z-index:2; width:70px;">
      <div style="width:28px; height:28px; border-radius:50%; background:#10b981; color:white; margin:0 auto 4px; font-size:12px; line-height:28px;">&#10003;</div>
      <div style="font-size:11px; font-weight:700;">Оформлено</div>
    </div>
    <div style="position:relative; z-index:2; width:70px;">
      <div style="width:28px; height:28px; border-radius:50%; background:#10b981; color:white; margin:0 auto 4px; font-size:12px; line-height:28px;">&#10003;</div>
      <div style="font-size:11px; font-weight:700;">Забір проби</div>
    </div>
    <div style="position:relative; z-index:2; width:70px;">
      <div style="width:28px; height:28px; border-radius:50%; background:#10b981; color:white; margin:0 auto 4px; font-size:12px; line-height:28px;">&#10003;</div>
      <div style="font-size:11px; font-weight:700;">В дорозі</div>
    </div>
    <div style="position:relative; z-index:2; width:70px;">
      <div style="width:28px; height:28px; border-radius:50%; background:#10b981; color:white; margin:0 auto 4px; font-size:12px; line-height:28px;">&#10003;</div>
      <div style="font-size:11px; font-weight:700;">Аналіз</div>
    </div>
    <div style="position:relative; z-index:2; width:70px;">
      <div style="width:28px; height:28px; border-radius:50%; background:#10b981; color:white; margin:0 auto 4px; font-size:12px; line-height:28px;">&#10003;</div>
      <div style="font-size:11px; font-weight:700;">Готово (КЕП)</div>
    </div>
  </div>

  <table>
    <thead>
      <tr><th>Показник</th><th>Результат</th><th>Норма</th><th>Статус</th></tr>
    </thead>
    <tbody>
      <tr><td>Глюкоза сироватки</td><td><strong>5.2</strong> ммоль/л</td><td>4.1 - 5.9</td><td><span style="color:#16a34a; font-weight:700;">В нормі</span></td></tr>
      <tr><td>Холестерин загальний</td><td><strong>4.8</strong> ммоль/л</td><td>&lt; 5.2</td><td><span style="color:#16a34a; font-weight:700;">В нормі</span></td></tr>
      <tr><td>ТТГ (Тиреотропний гормон)</td><td><strong>1.85</strong> мкМО/мл</td><td>0.4 - 4.0</td><td><span style="color:#16a34a; font-weight:700;">В нормі</span></td></tr>
    </tbody>
  </table>
</div>
</body>
</html>
"""

# 7. Prototype 07: Biobank Archive
p07 = f"""<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8"><title>Прототип 7: Біобанк та архів зразків</title>
<style>{shared_css}</style>
</head>
<body>
<div class="header-bar">
  <h1><span>Фізичний архів біоматеріалів (Біобанк)</span> <span class="badge">Роль: Архіваріус</span></h1>
  <div><button class="btn btn-outline" style="color:white; border-color:#334155;" onclick="window.history.back()">Назад до ТЗ</button></div>
</div>

<div class="grid-2">
  <div class="card">
    <h3>Координатна матриця штатива №RACK-BIO-01</h3>
    <div style="font-size:12.5px; color:var(--muted); margin-bottom:12px;">Морозильна камера №2 (-20°C) · Полиця 3 · Матриця 10 &times; 10 комірок:</div>
    
    <div style="display:grid; grid-template-columns:repeat(10, 1fr); gap:4px; max-width:400px; margin:0 auto 16px;">
      <!-- Matrix Cells -->
      <div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #1: Зразок 1026004819 (Коваленко О.С.)')">1</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #2: Зразок 1026004819 (Коваленко О.С.)')">2</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #3: Зразок 1026004819 (Коваленко О.С.)')">3</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#10b981; font-size:9px; display:flex; align-items:center; justify-content:center; color:white; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #4: Зразок 1026004819 (Коваленко О.С.)')">4</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #5: Зразок 1026004819 (Коваленко О.С.)')">5</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #6: Зразок 1026004819 (Коваленко О.С.)')">6</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #7: Зразок 1026004819 (Коваленко О.С.)')">7</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #8: Зразок 1026004819 (Коваленко О.С.)')">8</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #9: Зразок 1026004819 (Коваленко О.С.)')">9</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #10: Зразок 1026004819 (Коваленко О.С.)')">10</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #11: Зразок 1026004819 (Коваленко О.С.)')">11</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #12: Зразок 1026004819 (Коваленко О.С.)')">12</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #13: Зразок 1026004819 (Коваленко О.С.)')">13</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #14: Зразок 1026004819 (Коваленко О.С.)')">14</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#10b981; font-size:9px; display:flex; align-items:center; justify-content:center; color:white; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #15: Зразок 1026004819 (Коваленко О.С.)')">15</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #16: Зразок 1026004819 (Коваленко О.С.)')">16</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #17: Зразок 1026004819 (Коваленко О.С.)')">17</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #18: Зразок 1026004819 (Коваленко О.С.)')">18</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #19: Зразок 1026004819 (Коваленко О.С.)')">19</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #20: Зразок 1026004819 (Коваленко О.С.)')">20</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #21: Зразок 1026004819 (Коваленко О.С.)')">21</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #22: Зразок 1026004819 (Коваленко О.С.)')">22</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#ef4444; font-size:9px; display:flex; align-items:center; justify-content:center; color:white; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #23: Зразок 1026004819 (Коваленко О.С.)')">23</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #24: Зразок 1026004819 (Коваленко О.С.)')">24</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #25: Зразок 1026004819 (Коваленко О.С.)')">25</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #26: Зразок 1026004819 (Коваленко О.С.)')">26</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #27: Зразок 1026004819 (Коваленко О.С.)')">27</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #28: Зразок 1026004819 (Коваленко О.С.)')">28</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#10b981; font-size:9px; display:flex; align-items:center; justify-content:center; color:white; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #29: Зразок 1026004819 (Коваленко О.С.)')">29</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #30: Зразок 1026004819 (Коваленко О.С.)')">30</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #31: Зразок 1026004819 (Коваленко О.С.)')">31</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #32: Зразок 1026004819 (Коваленко О.С.)')">32</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #33: Зразок 1026004819 (Коваленко О.С.)')">33</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #34: Зразок 1026004819 (Коваленко О.С.)')">34</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #35: Зразок 1026004819 (Коваленко О.С.)')">35</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #36: Зразок 1026004819 (Коваленко О.С.)')">36</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #37: Зразок 1026004819 (Коваленко О.С.)')">37</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #38: Зразок 1026004819 (Коваленко О.С.)')">38</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #39: Зразок 1026004819 (Коваленко О.С.)')">39</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #40: Зразок 1026004819 (Коваленко О.С.)')">40</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #41: Зразок 1026004819 (Коваленко О.С.)')">41</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #42: Зразок 1026004819 (Коваленко О.С.)')">42</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #43: Зразок 1026004819 (Коваленко О.С.)')">43</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #44: Зразок 1026004819 (Коваленко О.С.)')">44</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #45: Зразок 1026004819 (Коваленко О.С.)')">45</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#10b981; font-size:9px; display:flex; align-items:center; justify-content:center; color:white; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #46: Зразок 1026004819 (Коваленко О.С.)')">46</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #47: Зразок 1026004819 (Коваленко О.С.)')">47</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #48: Зразок 1026004819 (Коваленко О.С.)')">48</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #49: Зразок 1026004819 (Коваленко О.С.)')">49</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #50: Зразок 1026004819 (Коваленко О.С.)')">50</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #51: Зразок 1026004819 (Коваленко О.С.)')">51</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #52: Зразок 1026004819 (Коваленко О.С.)')">52</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #53: Зразок 1026004819 (Коваленко О.С.)')">53</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #54: Зразок 1026004819 (Коваленко О.С.)')">54</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #55: Зразок 1026004819 (Коваленко О.С.)')">55</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #56: Зразок 1026004819 (Коваленко О.С.)')">56</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #57: Зразок 1026004819 (Коваленко О.С.)')">57</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #58: Зразок 1026004819 (Коваленко О.С.)')">58</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #59: Зразок 1026004819 (Коваленко О.С.)')">59</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #60: Зразок 1026004819 (Коваленко О.С.)')">60</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #61: Зразок 1026004819 (Коваленко О.С.)')">61</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #62: Зразок 1026004819 (Коваленко О.С.)')">62</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #63: Зразок 1026004819 (Коваленко О.С.)')">63</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #64: Зразок 1026004819 (Коваленко О.С.)')">64</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #65: Зразок 1026004819 (Коваленко О.С.)')">65</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #66: Зразок 1026004819 (Коваленко О.С.)')">66</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #67: Зразок 1026004819 (Коваленко О.С.)')">67</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#10b981; font-size:9px; display:flex; align-items:center; justify-content:center; color:white; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #68: Зразок 1026004819 (Коваленко О.С.)')">68</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #69: Зразок 1026004819 (Коваленко О.С.)')">69</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #70: Зразок 1026004819 (Коваленко О.С.)')">70</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #71: Зразок 1026004819 (Коваленко О.С.)')">71</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #72: Зразок 1026004819 (Коваленко О.С.)')">72</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #73: Зразок 1026004819 (Коваленко О.С.)')">73</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #74: Зразок 1026004819 (Коваленко О.С.)')">74</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #75: Зразок 1026004819 (Коваленко О.С.)')">75</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #76: Зразок 1026004819 (Коваленко О.С.)')">76</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #77: Зразок 1026004819 (Коваленко О.С.)')">77</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #78: Зразок 1026004819 (Коваленко О.С.)')">78</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #79: Зразок 1026004819 (Коваленко О.С.)')">79</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #80: Зразок 1026004819 (Коваленко О.С.)')">80</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #81: Зразок 1026004819 (Коваленко О.С.)')">81</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #82: Зразок 1026004819 (Коваленко О.С.)')">82</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #83: Зразок 1026004819 (Коваленко О.С.)')">83</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #84: Зразок 1026004819 (Коваленко О.С.)')">84</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #85: Зразок 1026004819 (Коваленко О.С.)')">85</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #86: Зразок 1026004819 (Коваленко О.С.)')">86</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #87: Зразок 1026004819 (Коваленко О.С.)')">87</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #88: Зразок 1026004819 (Коваленко О.С.)')">88</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #89: Зразок 1026004819 (Коваленко О.С.)')">89</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #90: Зразок 1026004819 (Коваленко О.С.)')">90</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #91: Зразок 1026004819 (Коваленко О.С.)')">91</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #92: Зразок 1026004819 (Коваленко О.С.)')">92</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #93: Зразок 1026004819 (Коваленко О.С.)')">93</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #94: Зразок 1026004819 (Коваленко О.С.)')">94</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #95: Зразок 1026004819 (Коваленко О.С.)')">95</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #96: Зразок 1026004819 (Коваленко О.С.)')">96</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #97: Зразок 1026004819 (Коваленко О.С.)')">97</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #98: Зразок 1026004819 (Коваленко О.С.)')">98</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #99: Зразок 1026004819 (Коваленко О.С.)')">99</div><div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:#f1f5f9; font-size:9px; display:flex; align-items:center; justify-content:center; color:#64748b; font-weight:bold; cursor:pointer;" onclick="alert('Комірка #100: Зразок 1026004819 (Коваленко О.С.)')">100</div>
    </div>
    
    <div style="display:flex; justify-content:center; gap:16px; font-size:11px;">
      <span><span style="display:inline-block; width:10px; height:10px; background:#10b981;"></span> Зберігається (активний)</span>
      <span><span style="display:inline-block; width:10px; height:10px; background:#ef4444;"></span> Термін сплив</span>
      <span><span style="display:inline-block; width:10px; height:10px; background:#f1f5f9; border:1px solid #ccc;"></span> Вільна комірка</span>
    </div>
  </div>

  <div class="card">
    <h3>Операції зі зразками архіву</h3>
    <div style="margin-bottom:16px;">
      <label style="font-size:13px; font-weight:600;">Пошук пробірки за штрихкодом:</label>
      <div style="display:flex; gap:8px; margin-top:4px;">
        <input type="text" value="1026004819" style="flex:1; padding:8px; border:1px solid var(--border); border-radius:6px; font-size:13px;">
        <button class="btn btn-primary" onclick="alert('Зразок знайдено: Морозильник 2, Штатив RACK-BIO-01, Комірка #45!')">Знайти</button>
      </div>
    </div>

    <div style="background:#f1f5f9; padding:12px; border-radius:8px; font-size:13px; line-height:1.7;">
      <div><strong>Зразок:</strong> 1026004819 (Сироватка крові)</div>
      <div><strong>Дата забору:</strong> 06.10.2026 | <strong>Термін зберігання:</strong> до 06.11.2026</div>
      <div><strong>Локація:</strong> Камера 2 &rarr; Полиця 3 &rarr; Штатив 01 &rarr; Комірка 45</div>
    </div>
    <div style="margin-top:14px; display:flex; gap:8px;">
      <button class="btn btn-outline" onclick="alert('Сформовано акт вилучення зразка для додаткового аналізу.')">Вилучити на дообстеження</button>
      <button class="btn btn-danger" onclick="alert('Сформовано офіційний Акт утилізації біоматеріалу.')">Утилізувати за терміном</button>
    </div>
  </div>
</div>
</body>
</html>
"""

# 8. Prototype 08: Reagent Inventory
p08 = f"""<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8"><title>Прототип 8: Складський облік реагентів та калібраторів</title>
<style>{shared_css}</style>
</head>
<body>
<div class="header-bar">
  <h1><span>Облік реагентів на борту приладів (Lot Tracking)</span> <span class="badge">Роль: Завідувач лабораторії</span></h1>
  <div><button class="btn btn-outline" style="color:white; border-color:#334155;" onclick="window.history.back()">Назад до ТЗ</button></div>
</div>

<div class="card" style="padding:0; overflow:hidden;">
  <table>
    <thead>
      <tr>
        <th>Прилад</th>
        <th>Тест / Реагент</th>
        <th>Партія (Lot №)</th>
        <th>Залишок тестів</th>
        <th>Відкрито флакон</th>
        <th>Придатність</th>
        <th>Статус</th>
        <th>Дія</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td><strong>Sysmex XN-1000</strong></td>
        <td>Cellpack DCL (Ділюент)</td>
        <td><code>LOT-2026-XN08</code></td>
        <td><strong>1,420</strong> / 2,000</td>
        <td>01.10.2026</td>
        <td>31.12.2027</td>
        <td><span class="badge-status" style="background:#ecfdf5; color:#065f46;">АКТИВНИЙ</span></td>
        <td><button class="btn btn-outline" style="padding:2px 8px; font-size:11px;">Деталі</button></td>
      </tr>
      <tr style="background:#fffbeb;">
        <td><strong>Roche Cobas e411</strong></td>
        <td>Elecsys TSH (ТТГ)</td>
        <td><code>LOT-682190-01</code></td>
        <td><strong style="color:#b45309;">18</strong> / 200</td>
        <td>28.09.2026</td>
        <td>28.10.2026 (Onboard)</td>
        <td><span class="badge-status" style="background:#fef3c7; color:#92400e;">МАЛИЙ ЗАЛИШОК</span></td>
        <td><button class="btn btn-primary" style="padding:2px 8px; font-size:11px;" onclick="alert('Створено заявку на списання зі складу нового набору TSH!')">Замовити зі складу</button></td>
      </tr>
      <tr style="background:#fef2f2;">
        <td><strong>Mindray BS-240</strong></td>
        <td>Glucose GOD-POD</td>
        <td><code>LOT-GLU-9902</code></td>
        <td><strong>85</strong> / 500</td>
        <td>05.09.2026</td>
        <td><strong style="color:#ef4444;">05.10.2026 (ПРОСТРОЧЕНО)</strong></td>
        <td><span class="badge-status" style="background:#fee2e2; color:#b91c1c;">ПРИДАТНІСТЬ СПЛИВЛА</span></td>
        <td><button class="btn btn-danger" style="padding:2px 8px; font-size:11px;" onclick="alert('Флакон заблоковано на приладі! Потрібно встановити нову касету.')">Заблокувати</button></td>
      </tr>
    </tbody>
  </table>
</div>
</body>
</html>
"""

# Write all prototypes to files
protos = [
    ("01_phlebotomy_station.html", p01),
    ("02_specimen_logistics.html", p02),
    ("03_lab_workstation.html", p03),
    ("04_validation_and_panic.html", p04),
    ("05_quality_control.html", p05),
    ("06_patient_portal.html", p06),
    ("07_biobank_archive.html", p07),
    ("08_reagent_inventory.html", p08),
]

for fname, content in protos:
    with open(f"{proto_dir}/{fname}", "w", encoding="utf-8") as f:
        f.write(content)

print(f"Generated {len(protos)} prototype HTML files successfully in prototypes/")
