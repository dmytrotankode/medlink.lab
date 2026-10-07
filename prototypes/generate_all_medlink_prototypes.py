# -*- coding: utf-8 -*-
"""
Full updater to generate 8 MedLink Quasar-styled interactive prototypes.
Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
"""

import os
from update_prototypes_medlink_style import MODULES, wrap_prototype

PROT_DIR = r"c:\__MEDLINK___\LABA\prototypes"

# -------------------------------------------------------------
# 01. Phlebotomy Station
# -------------------------------------------------------------
p01_body = '''
      <div class="patient-banner">
        <div class="patient-meta">
          <div><strong style="color:var(--primary);">Пацієнт:</strong> Коваленко Олександр Сергійович (12.04.1985, 41 рік)</div>
          <div><strong>Картка ЕМК:</strong> №108291</div>
          <div><strong>Телефон:</strong> +380 (67) 123-45-67</div>
          <div><strong>Е-направлення ЕСОЗ:</strong> <span class="q-badge q-badge-info">5491-8821-9012</span></div>
        </div>
        <div>
          <span class="item-colored active">Ідентифіковано</span>
        </div>
      </div>

      <div class="grid-2">
        <!-- Patient Search & Checklist Card -->
        <div class="q-card">
          <div class="q-card-header">
            <div class="q-card-title">🔍 Пошук пацієнта та преаналітичний чек-лист</div>
            <span class="q-badge q-badge-info">Кабінет забору №1</span>
          </div>
          <div class="q-card-body">
            <div style="display:flex; gap:8px; margin-bottom:14px;">
              <input type="text" class="q-input" value="Коваленко Олександр Сергійович" style="flex:1;">
              <button class="q-btn q-btn-primary">Знайти</button>
            </div>

            <div style="background:#f8fafc; border:1px solid #e2e8f0; border-radius:4px; padding:12px; margin-bottom:14px; font-size:13.5px; line-height:1.6;">
              <div><strong>Призначені лабораторні панелі:</strong></div>
              <ul style="padding-left:18px; margin-top:4px; list-style-type:disc;">
                <li>Загальний аналіз крові розгорнутий (Sysmex XN-1000)</li>
                <li>Біохімічний профіль: Глюкоза, АЛТ, АСТ, Білірубін, Креатинін (Mindray BS-240)</li>
                <li>Коагулограма: МНВ, АЧТЧ, Фібриноген (Sysmex CA-660)</li>
              </ul>
            </div>

            <h4 style="font-size:13.5px; font-weight:700; margin-bottom:8px; color:#374151;">Преаналітичний чек-лист медсестри:</h4>
            <div style="display:flex; flex-direction:column; gap:8px; margin-bottom:16px; font-size:13.5px;">
              <label style="display:flex; align-items:center; gap:8px;">
                <input type="checkbox" checked style="accent-color:var(--primary);">
                Пацієнт натщесерце (останній прийом їжі > 8 год тому)
              </label>
              <label style="display:flex; align-items:center; gap:8px;">
                <input type="checkbox" checked style="accent-color:var(--primary);">
                Особу пацієнта перевірено за паспортом / Дією (2 фактори)
              </label>
              <label style="display:flex; align-items:center; gap:8px;">
                <input type="checkbox" style="accent-color:var(--primary);">
                Прийом антикоагулянтів (Варфарин, Ксарелто) за останні 24 год
              </label>
            </div>

            <div style="display:flex; gap:10px;">
              <button class="q-btn q-btn-positive" style="flex:1;" onclick="alert('Біоматеріал зафіксовано як забраний! Друк 3-х штрихкод-етикеток розпочато на принтері Zebra ZD421.')">
                ✓ Підтвердити забір & Друк усіх етикеток
              </button>
              <button class="q-btn q-btn-outline" onclick="alert('Скасовано забір.')">Скасувати</button>
            </div>
          </div>
        </div>

        <!-- CLSI Order of Draw Card -->
        <div class="q-card">
          <div class="q-card-header">
            <div class="q-card-title">🧪 CLSI Order of Draw (Порядок наповнення пробірок)</div>
            <span class="q-badge q-badge-success">Автоконсолідація: 3 пробірки</span>
          </div>
          <div class="q-card-body">
            <p style="font-size:13px; color:var(--text-muted); margin-bottom:12px;">
              Система об'єднала 18 тестів у 3 пробірки за регламентом CLSI H3-A6 для запобігання перехресної контамінації реагентами:
            </p>

            <div style="display:flex; flex-direction:column; gap:10px;">
              <!-- Tube 1 -->
              <div style="display:flex; align-items:center; gap:12px; padding:10px; border:2px solid #0284c7; border-radius:6px; background:#f0f9ff;">
                <span style="font-size:20px; font-weight:800; color:#0284c7;">1.</span>
                <div style="width:26px; height:26px; border-radius:50%; background:#0284c7; border:2px solid #ffffff; box-shadow:0 0 4px rgba(0,0,0,0.2);"></div>
                <div style="flex:1;">
                  <div style="font-weight:700; font-size:13.5px; color:#0369a1;">Цитрат натрію 3.2% (Блакитна кришка) · 3.0 мл</div>
                  <div style="font-size:12px; color:#475569;">Коагулограма (МНВ, АЧТЧ, Фібриноген). Штрихкод: <code>1026004818</code></div>
                </div>
                <button class="q-btn q-btn-outline" style="height:26px; font-size:11px;" onclick="alert('ZPL етикетку 1026004818 надруковано!')">Друк</button>
              </div>

              <!-- Tube 2 -->
              <div style="display:flex; align-items:center; gap:12px; padding:10px; border:2px solid #ca8a04; border-radius:6px; background:#fefce8;">
                <span style="font-size:20px; font-weight:800; color:#ca8a04;">2.</span>
                <div style="width:26px; height:26px; border-radius:50%; background:#ca8a04; border:2px solid #ffffff; box-shadow:0 0 4px rgba(0,0,0,0.2);"></div>
                <div style="flex:1;">
                  <div style="font-weight:700; font-size:13.5px; color:#a16207;">Активатор згортання / Гель (Жовта кришка) · 5.0 мл</div>
                  <div style="font-size:12px; color:#475569;">Біохімія (Глюкоза, АЛТ, АСТ, Білірубін, Креатинін). Штрихкод: <code>1026004819</code></div>
                </div>
                <button class="q-btn q-btn-outline" style="height:26px; font-size:11px;" onclick="alert('ZPL етикетку 1026004819 надруковано!')">Друк</button>
              </div>

              <!-- Tube 3 -->
              <div style="display:flex; align-items:center; gap:12px; padding:10px; border:2px solid #9333ea; border-radius:6px; background:#faf5ff;">
                <span style="font-size:20px; font-weight:800; color:#9333ea;">3.</span>
                <div style="width:26px; height:26px; border-radius:50%; background:#9333ea; border:2px solid #ffffff; box-shadow:0 0 4px rgba(0,0,0,0.2);"></div>
                <div style="flex:1;">
                  <div style="font-weight:700; font-size:13.5px; color:#7e22ce;">K2/K3 ЕДТА (Фіолетова кришка) · 2.6 мл</div>
                  <div style="font-size:12px; color:#475569;">ЗАК + Лейкоцитарна формула + ШОЕ. Штрихкод: <code>1026004820</code></div>
                </div>
                <button class="q-btn q-btn-outline" style="height:26px; font-size:11px;" onclick="alert('ZPL етикетку 1026004820 надруковано!')">Друк</button>
              </div>
            </div>

            <!-- Preanalytical instruction -->
            <div style="margin-top:14px; padding:10px; background:#f4f6f8; border-radius:4px; font-size:12px; color:#4b5563;">
              💡 <em>Підказка для медсестри:</em> Акуратно переверніть пробірку з EDTA 8-10 разів для змішування антикоагулянту без утворення піни.
            </div>
          </div>
        </div>
      </div>
'''

# -------------------------------------------------------------
# 02. Specimen Logistics
# -------------------------------------------------------------
p02_body = '''
      <div class="grid-2">
        <!-- Electronic Transfer Manifest Card -->
        <div class="q-card">
          <div class="q-card-header">
            <div class="q-card-title">📦 Електронний акт передачі (Маніфест №ACT-2026-1006)</div>
            <span class="q-badge q-badge-success">Холодовий ланцюг збережено</span>
          </div>
          <div class="q-card-body">
            <div style="font-size:13.5px; line-height:1.7; margin-bottom:16px;">
              <div><strong>Маршрут:</strong> Відділення забору №1 (вул. Хрещатик, 15) &rarr; Центральна Лабораторія</div>
              <div><strong>Кур'єр:</strong> Гриценко Петро Олексійович (Renault Kangoo AA1234EE)</div>
              <div><strong>Термоконтейнер:</strong> №BOX-04 (Холодоелементи 2-8°C)</div>
              <div><strong>Кількість зразків:</strong> 24 вакутайнери</div>
              <div><strong>Температура при відправці:</strong> <span style="font-weight:700; color:var(--positive);">+3.8°C</span> (09:15)</div>
              <div><strong>Температура при доставці:</strong> <span style="font-weight:700; color:var(--positive);">+4.5°C</span> (10:10)</div>
              <div><strong>Датчик логера:</strong> Testo 174T (№SN-998124)</div>
            </div>

            <div style="display:flex; gap:10px;">
              <button class="q-btn q-btn-primary" style="flex:1;" onclick="alert('Акт закрито! 24 пробірки переведено в статус [Прийнято лабораторією]. Автоматично сформовано чергу для сортування.')">
                ✓ Підтвердити прийом термосумки
              </button>
              <button class="q-btn q-btn-outline" onclick="alert('Друк акта приймання-передачі...')">Друк акта</button>
            </div>
          </div>
        </div>

        <!-- Sorting & Rejection Desk Card -->
        <div class="q-card">
          <div class="q-card-header">
            <div class="q-card-title">🔬 Стіл сортування та реєстрації бракеражу</div>
            <span class="q-badge q-badge-warning">Преаналітичний контроль</span>
          </div>
          <div class="q-card-body">
            <p style="font-size:13px; color:var(--text-muted); margin-bottom:12px;">
              Швидкісне сканування пробірки для допуску в роботу або реєстрації дефектів:
            </p>

            <div style="display:flex; gap:8px; margin-bottom:14px;">
              <input type="text" class="q-input" placeholder="Сканувати штрихкод пробірки..." value="1026004819" style="flex:1;">
              <button class="q-btn q-btn-primary">Пошук</button>
            </div>

            <div style="background:#fffbeb; border:1px solid #fde68a; padding:12px; border-radius:4px; margin-bottom:14px; font-size:13.5px;">
              <div style="font-weight:700; color:#b45309; margin-bottom:6px;">⚠️ Реєстрація дефекту біоматеріалу (ISO 15189):</div>
              <div style="display:flex; flex-direction:column; gap:6px;">
                <label><input type="radio" name="defect" value="HEM" checked> <strong>Гемоліз (+++)</strong> — руйнування еритроцитів, сироватка червона</label>
                <label><input type="radio" name="defect" value="CHYL"> <strong>Хілоз (ліпемія)</strong> — непрозора молочна сироватка</label>
                <label><input type="radio" name="defect" value="CLOT"> <strong>Згусток (Microclot)</strong> у цитратній або EDTA плазмі</label>
                <label><input type="radio" name="defect" value="VOL"> <strong>Недостатній об'єм</strong> для проведення тестування</label>
              </div>
            </div>

            <div style="display:flex; gap:10px;">
              <button class="q-btn q-btn-negative" style="flex:1;" onclick="alert('Пробірку 1026004819 забраковано! Лікарю та кабінету забору №1 надіслано термінове завдання на повторний безкоштовний забір.')">
                ✖ Забракувати & Направити на перезабір
              </button>
              <button class="q-btn q-btn-positive" onclick="alert('Зразок допущено до аналізу!')">
                Допустити
              </button>
            </div>
          </div>
        </div>
      </div>
'''

# -------------------------------------------------------------
# 03. Lab Workstation
# -------------------------------------------------------------
p03_body = '''
      <!-- Filter Toolbar -->
      <div class="q-card" style="margin-bottom:14px;">
        <div class="q-card-body" style="padding:10px 16px; display:flex; justify-content:space-between; align-items:center;">
          <div style="display:flex; gap:12px; align-items:center;">
            <input type="text" class="q-input" placeholder="Фільтр за штрихкодом або пацієнтом..." style="width:240px;">
            <select class="q-select" style="width:260px;">
              <option>Всі прилади (Sysmex, Cobas, Mindray)</option>
              <option>Sysmex XN-1000 (Гематологія)</option>
              <option>Roche Cobas e411 (Імунохімія)</option>
              <option>Mindray BS-240 (Біохімія)</option>
            </select>
            <button class="q-btn q-btn-primary" style="height:32px;">Застосувати</button>
          </div>
          <div style="font-size:12.5px; color:var(--text-muted); display:flex; gap:16px;">
            <span>В черзі: <strong>148</strong> проб</span>
            <span class="item-colored active">Автоверифіковано: <strong>112</strong></span>
            <span class="item-colored stopped">Потребують уваги: <strong>4</strong></span>
          </div>
        </div>
      </div>

      <!-- Laboratory Worklist Table -->
      <div class="q-card">
        <div class="q-table-container">
          <table class="q-table">
            <thead>
              <tr>
                <th>Штрихкод</th>
                <th>Пацієнт</th>
                <th>Дослідження</th>
                <th>Прилад</th>
                <th>Показник</th>
                <th>Значення</th>
                <th>Одиниці</th>
                <th>Норма</th>
                <th>Прапорці (Flags)</th>
                <th>Дія</th>
              </tr>
            </thead>
            <tbody>
              <!-- Row 1: Panic Value -->
              <tr style="background:#fef2f2;">
                <td><code>1026004812</code></td>
                <td><strong>Мельник Ю.В.</strong></td>
                <td>Глюкоза сироватки</td>
                <td>Cobas e411</td>
                <td><strong>GLU</strong></td>
                <td><strong style="color:var(--negative); font-size:15px;">26.4</strong></td>
                <td>ммоль/л</td>
                <td>4.1 - 5.9</td>
                <td><span class="q-badge q-badge-panic">CRIT HIGH</span></td>
                <td>
                  <button class="q-btn q-btn-negative" style="height:26px; font-size:11px;" onclick="alert('Панічне значення! Запущено виклик чергового лікаря відділення.')">
                    Алерт лікарю
                  </button>
                </td>
              </tr>

              <!-- Row 2: Normal Auto -->
              <tr>
                <td><code>1026004815</code></td>
                <td><strong>Шевченко І.П.</strong></td>
                <td>ЗАК розгорнутий</td>
                <td>Sysmex XN-1000</td>
                <td><strong>HGB</strong></td>
                <td><strong>128.0</strong></td>
                <td>г/л</td>
                <td>120 - 140</td>
                <td><span class="q-badge q-badge-success">NORMAL (Auto)</span></td>
                <td>
                  <button class="q-btn q-btn-positive" style="height:26px; font-size:11px;" onclick="alert('Підтверджено лаборантом.')">
                    Затвердити
                  </button>
                </td>
              </tr>

              <!-- Row 3: Delta Alert -->
              <tr style="background:#fffbeb;">
                <td><code>1026004819</code></td>
                <td><strong>Коваленко О.С.</strong></td>
                <td>Печінкові проби</td>
                <td>Mindray BS-240</td>
                <td><strong>ALT</strong></td>
                <td><strong style="color:#b45309; font-size:15px;">68.5</strong></td>
                <td>U/L</td>
                <td>0 - 41</td>
                <td><span class="q-badge q-badge-warning">DELTA +42%</span></td>
                <td>
                  <button class="q-btn q-btn-primary" style="height:26px; font-size:11px;" onclick="alert('Відправлено на розгляд лікарю-лаборанту з поміткою Delta-Check.')">
                    На розгляд
                  </button>
                </td>
              </tr>

              <!-- Row 4: Normal Auto -->
              <tr>
                <td><code>1026004820</code></td>
                <td><strong>Коваленко О.С.</strong></td>
                <td>ЗАК розгорнутий</td>
                <td>Sysmex XN-1000</td>
                <td><strong>PLT</strong></td>
                <td><strong>235.0</strong></td>
                <td>10*9/л</td>
                <td>180 - 320</td>
                <td><span class="q-badge q-badge-success">NORMAL (Auto)</span></td>
                <td>
                  <button class="q-btn q-btn-positive" style="height:26px; font-size:11px;" onclick="alert('Затверджено.')">
                    Затвердити
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
'''

# -------------------------------------------------------------
# 04. Validation & Panic Values
# -------------------------------------------------------------
p04_body = '''
      <!-- Panic Value Emergency Banner -->
      <div class="q-card" style="border-left: 6px solid var(--negative); background:#fef2f2; margin-bottom:16px;">
        <div class="q-card-body" style="padding:14px 20px; display:flex; justify-content:space-between; align-items:center;">
          <div>
            <h3 style="color:var(--negative); font-size:16px; font-weight:700; margin-bottom:4px;">🚨 НЕВІДКЛАДНЕ ПОВІДОМЛЕННЯ: КРИТИЧНЕ ЗНАЧЕННЯ (PANIC VALUE)</h3>
            <p style="color:#7f1d1d; margin:0; font-size:13.5px;">
              Пацієнт <strong>Мельник Ю.В.</strong>, 48 років. Глюкоза сироватки = <strong style="font-size:15px;">26.4 ммоль/л</strong> (Загроза кетоацидотичної або гіперосмолярної коми!).
            </p>
          </div>
          <div style="display:flex; gap:10px;">
            <button class="q-btn q-btn-negative" onclick="alert('Сповіщення успішно надіслано черговому реаніматологу! Зафіксовано таймштамп 16:22:10 за стандартом ISO 15189.')">
              Надіслати терміновий Alert лікарю
            </button>
          </div>
        </div>
      </div>

      <div class="grid-2">
        <!-- Doctor Verification Card -->
        <div class="q-card">
          <div class="q-card-header">
            <div class="q-card-title">📝 Верифікація замовлення №1026-004819</div>
            <span class="q-badge q-badge-info">Готово до підпису</span>
          </div>
          <div class="q-card-body">
            <div style="font-size:13.5px; line-height:1.7; margin-bottom:14px;">
              <div><strong>Пацієнт:</strong> Коваленко О.С. (41 рік, Чоловік)</div>
              <div><strong>Прилад:</strong> Mindray BS-240 | <strong>Біоматеріал:</strong> Сироватка крові</div>
              <div><strong>Delta-Check:</strong> АЛТ зріс з 48.0 до 68.5 U/L за 48 годин (<span style="color:#b45309; font-weight:700;">+42.7%</span>).</div>
              <div><strong>Попередній діагноз:</strong> Стеатогепатит неуточнений (K75.8)</div>
            </div>

            <div style="margin-bottom:14px;">
              <label style="font-size:12.5px; font-weight:600; color:#374151;">Клінічний коментар лікаря-лаборанта до бланка:</label>
              <textarea class="q-input" style="width:100%; height:60px; padding:8px; margin-top:4px; font-size:13px;">Помірний цитолітичний синдром. Рекомендовано контроль вірусних гепатитів B, C та ліпідограми через 14 днів.</textarea>
            </div>

            <div style="display:flex; gap:10px;">
              <button class="q-btn q-btn-primary" style="flex:1;" onclick="alert('Накладено Кваліфікований електронний підпис (КЕП)! Дослідження закрито, PDF сформовано та опубліковано в ЕСОЗ eHealth.')">
                🔏 Підписати КЕП & Затвердити
              </button>
              <button class="q-btn q-btn-outline" onclick="alert('Направлено на повторне вимірювання (re-run).')">Re-run</button>
            </div>
          </div>
        </div>

        <!-- Reflex Engine Card -->
        <div class="q-card">
          <div class="q-card-header">
            <div class="q-card-title">⚡ Reflex-Engine (Правила автодопризначення)</div>
            <span class="q-badge q-badge-success">Правило активно</span>
          </div>
          <div class="q-card-body">
            <div style="background:#f8fafc; border:1px solid #e2e8f0; padding:12px; border-radius:4px; font-size:13px; line-height:1.7; margin-bottom:14px;">
              <div style="font-weight:700; color:var(--primary);">Правило №REF-04 (Тиреоїдний алгоритм):</div>
              <div>Умова: <code>TSH &lt; 0.4 uIU/mL</code> або <code>TSH &gt; 4.0 uIU/mL</code></div>
              <div>Виміряне значення: <strong>TSH = 0.12 uIU/mL</strong> (Знижений)</div>
              <div style="margin-top:8px; color:var(--positive); font-weight:700;">
                &rarr; Автоматично дозамовлено: Тироксин вільний (FT4) з наявного зразка сироватки без додаткового виклику пацієнта!
              </div>
            </div>

            <div style="background:#f8fafc; border:1px solid #e2e8f0; padding:12px; border-radius:4px; font-size:13px; line-height:1.7;">
              <div style="font-weight:700; color:var(--primary);">Правило №REF-09 (Підтвердження ВІЛ/Сифіліс):</div>
              <div>Умова: <code>Скринінг ІФА &ge; 1.0 (Позитивний)</code></div>
              <div>Дія: Автоматичне блокування видачі бланка та постановка імуноблоту.</div>
            </div>
          </div>
        </div>
      </div>
'''

# -------------------------------------------------------------
# 05. Quality Control
# -------------------------------------------------------------
p05_body = '''
      <!-- QC Parameters KPI Cards -->
      <div class="grid-3" style="margin-bottom:16px;">
        <div class="q-card" style="margin:0;">
          <div class="q-card-body" style="padding:12px 16px;">
            <div style="font-size:11.5px; text-transform:uppercase; color:var(--text-muted); font-weight:700;">Аналізатор</div>
            <div style="font-size:16px; font-weight:700; color:#1f2937;">Sysmex XN-1000 (#SN-41029)</div>
          </div>
        </div>
        <div class="q-card" style="margin:0;">
          <div class="q-card-body" style="padding:12px 16px;">
            <div style="font-size:11.5px; text-transform:uppercase; color:var(--text-muted); font-weight:700;">Контрольний матеріал</div>
            <div style="font-size:16px; font-weight:700; color:#1f2937;">XN-CHECK Level 2 (Normal)</div>
          </div>
        </div>
        <div class="q-card" style="margin:0;">
          <div class="q-card-body" style="padding:12px 16px;">
            <div style="font-size:11.5px; text-transform:uppercase; color:var(--text-muted); font-weight:700;">Паспортні параметри WBC</div>
            <div style="font-size:16px; font-weight:700; color:var(--primary);">Mean = 7.20 | SD = 0.30 (CV = 4.1%)</div>
          </div>
        </div>
      </div>

      <!-- Levey-Jennings Chart Card -->
      <div class="q-card">
        <div class="q-card-header">
          <div class="q-card-title">📈 Карта Леві-Дженнінгса (Останні 15 днів контрольних вимірювань WBC)</div>
          <span class="q-badge q-badge-panic">СТАТУС: LOCKOUT (ЗБІЙ 1-3s)</span>
        </div>
        <div class="q-card-body">
          <p style="font-size:13px; color:var(--text-muted); margin-bottom:12px;">
            Графічне відображення контрольних точок щодо меж $\pm 1\text{SD}, \pm 2\text{SD}, \pm 3\text{SD}$. Виявлено критичне порушення правила Вестгарда 1-3s (Day 15 = 8.28 > +3SD):
          </p>

          <div style="background:#ffffff; border:1px solid #e5e7eb; border-radius:4px; padding:16px; text-align:center;">
            <svg width="100%" height="200" viewBox="0 0 620 200">
              <rect x="40" y="10" width="560" height="170" fill="#f8fafc"/>
              <!-- Safe green band ±2SD -->
              <rect x="40" y="38" width="560" height="114" fill="#ecfdf5" opacity="0.7"/>
              <!-- +3SD line -->
              <line x1="40" y1="10" x2="600" y2="10" stroke="#ef4444" stroke-dasharray="4,4" stroke-width="1.5"/>
              <text x="5" y="14" fill="#ef4444" font-size="11" font-weight="bold">+3SD (8.10)</text>
              <!-- +2SD line -->
              <line x1="40" y1="38" x2="600" y2="38" stroke="#f59e0b" stroke-dasharray="4,4" stroke-width="1"/>
              <text x="5" y="42" fill="#f59e0b" font-size="11">+2SD (7.80)</text>
              <!-- Mean line -->
              <line x1="40" y1="95" x2="600" y2="95" stroke="#4274A7" stroke-width="2"/>
              <text x="5" y="99" fill="#4274A7" font-size="11" font-weight="bold">Mean (7.20)</text>
              <!-- -2SD line -->
              <line x1="40" y1="152" x2="600" y2="152" stroke="#f59e0b" stroke-dasharray="4,4" stroke-width="1"/>
              <text x="5" y="156" fill="#f59e0b" font-size="11">-2SD (6.60)</text>
              <!-- -3SD line -->
              <line x1="40" y1="180" x2="600" y2="180" stroke="#ef4444" stroke-dasharray="4,4" stroke-width="1.5"/>
              <text x="5" y="184" fill="#ef4444" font-size="11" font-weight="bold">-3SD (6.30)</text>
              <!-- Trend polyline -->
              <polyline fill="none" stroke="#4274A7" stroke-width="2.5" points="60,90 100,85 140,105 180,75 220,95 260,90 300,70 340,100 380,110 420,92 460,50 500,95 560,12"/>
              <!-- Data dots -->
              <circle cx="60" cy="90" r="4" fill="#4274A7"/>
              <circle cx="100" cy="85" r="4" fill="#4274A7"/>
              <circle cx="140" cy="105" r="4" fill="#4274A7"/>
              <circle cx="180" cy="75" r="4" fill="#4274A7"/>
              <circle cx="220" cy="95" r="4" fill="#4274A7"/>
              <circle cx="260" cy="90" r="4" fill="#4274A7"/>
              <circle cx="300" cy="70" r="4" fill="#4274A7"/>
              <circle cx="340" cy="100" r="4" fill="#4274A7"/>
              <circle cx="380" cy="110" r="4" fill="#4274A7"/>
              <circle cx="420" cy="92" r="4" fill="#4274A7"/>
              <circle cx="460" cy="50" r="4" fill="#4274A7"/>
              <circle cx="500" cy="95" r="4" fill="#4274A7"/>
              <!-- Outlier dot Day 15 -->
              <circle cx="560" cy="12" r="7" fill="#ef4444" stroke="#ffffff" stroke-width="2"/>
            </svg>
          </div>

          <!-- Lockout Box -->
          <div style="margin-top:14px; background:#fef2f2; border:1px solid #fecaca; border-radius:4px; padding:12px; display:flex; justify-content:space-between; align-items:center;">
            <div style="font-size:13px; color:#991b1b;">
              <strong>🛑 Автоматичний Lockout активовано:</strong> Видача результатів ЗАК з приладу Sysmex XN-1000 тимчасово заблокована до усунення відхилення.
            </div>
            <button class="q-btn q-btn-outline" style="border-color:#f87171; color:#b91c1c; font-size:12px;" onclick="alert('Відкрито протокол CAPA: введіть дані промивки апертури та повторного калібрування.')">
              Протокол усунення збою (CAPA)
            </button>
          </div>
        </div>
      </div>
'''

# -------------------------------------------------------------
# 06. Patient Portal
# -------------------------------------------------------------
p06_body = '''
      <div class="q-card">
        <div class="q-card-header">
          <div class="q-card-title">👤 Замовлення пацієнта №1026-004819 від 06.10.2026</div>
          <span class="q-badge q-badge-success">Готово</span>
        </div>
        <div class="q-card-body">
          <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:20px;">
            <div>
              <h2 style="font-size:17px; font-weight:700; color:#1f2937; margin-bottom:2px;">Комплексний чек-ап здоров'я</h2>
              <span style="font-size:12.5px; color:var(--text-muted);">Пацієнт: Коваленко О.С. | Лікар: Дмитренко В.М.</span>
            </div>
            <button class="q-btn q-btn-positive" onclick="alert('Завантажується офіційний підписаний КЕП PDF-бланк з перевірочним QR-кодом автентичності!')">
              📥 Завантажити PDF-бланк з КЕП
            </button>
          </div>

          <!-- 5-Step Process Stepper -->
          <div style="display:flex; justify-content:space-between; text-align:center; padding:10px 0 24px; position:relative;">
            <div style="position:absolute; top:22px; left:40px; right:40px; height:4px; background:var(--positive); z-index:1;"></div>
            
            <div style="position:relative; z-index:2; width:80px;">
              <div style="width:28px; height:28px; border-radius:50%; background:var(--positive); color:white; margin:0 auto 4px; font-size:13px; line-height:28px; font-weight:bold;">✓</div>
              <div style="font-size:11.5px; font-weight:700;">1. Оформлено</div>
              <div style="font-size:10px; color:var(--text-muted);">08:30</div>
            </div>
            <div style="position:relative; z-index:2; width:80px;">
              <div style="width:28px; height:28px; border-radius:50%; background:var(--positive); color:white; margin:0 auto 4px; font-size:13px; line-height:28px; font-weight:bold;">✓</div>
              <div style="font-size:11.5px; font-weight:700;">2. Забір проби</div>
              <div style="font-size:10px; color:var(--text-muted);">08:45</div>
            </div>
            <div style="position:relative; z-index:2; width:80px;">
              <div style="width:28px; height:28px; border-radius:50%; background:var(--positive); color:white; margin:0 auto 4px; font-size:13px; line-height:28px; font-weight:bold;">✓</div>
              <div style="font-size:11.5px; font-weight:700;">3. В дорозі</div>
              <div style="font-size:10px; color:var(--text-muted);">09:15</div>
            </div>
            <div style="position:relative; z-index:2; width:80px;">
              <div style="width:28px; height:28px; border-radius:50%; background:var(--positive); color:white; margin:0 auto 4px; font-size:13px; line-height:28px; font-weight:bold;">✓</div>
              <div style="font-size:11.5px; font-weight:700;">4. Аналіз</div>
              <div style="font-size:10px; color:var(--text-muted);">11:20</div>
            </div>
            <div style="position:relative; z-index:2; width:80px;">
              <div style="width:28px; height:28px; border-radius:50%; background:var(--positive); color:white; margin:0 auto 4px; font-size:13px; line-height:28px; font-weight:bold;">✓</div>
              <div style="font-size:11.5px; font-weight:700;">5. Готово (КЕП)</div>
              <div style="font-size:10px; color:var(--text-muted);">13:00</div>
            </div>
          </div>

          <!-- Results Table -->
          <div class="q-table-container">
            <table class="q-table">
              <thead>
                <tr>
                  <th>Показник (Тест)</th>
                  <th>Результат</th>
                  <th>Одиниці</th>
                  <th>Референтні значення</th>
                  <th>Динаміка</th>
                  <th>Статус</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td><strong>Глюкоза сироватки</strong></td>
                  <td><strong style="color:#1f2937;">5.2</strong></td>
                  <td>ммоль/л</td>
                  <td>4.1 - 5.9</td>
                  <td><span style="color:var(--text-muted);">&rarr; 0%</span></td>
                  <td><span class="item-colored active">В нормі</span></td>
                </tr>
                <tr>
                  <td><strong>Холестерин загальний</strong></td>
                  <td><strong style="color:#1f2937;">4.8</strong></td>
                  <td>ммоль/л</td>
                  <td>&lt; 5.2</td>
                  <td><span style="color:var(--positive);">&darr; -5%</span></td>
                  <td><span class="item-colored active">В нормі</span></td>
                </tr>
                <tr>
                  <td><strong>ТТГ (Тиреотропний гормон)</strong></td>
                  <td><strong style="color:#1f2937;">1.85</strong></td>
                  <td>мкМО/мл</td>
                  <td>0.4 - 4.0</td>
                  <td><span style="color:var(--text-muted);">&rarr; 0%</span></td>
                  <td><span class="item-colored active">В нормі</span></td>
                </tr>
                <tr>
                  <td><strong>АЛТ (Аланінамінотрансфераза)</strong></td>
                  <td><strong style="color:#b45309;">68.5</strong></td>
                  <td>U/L</td>
                  <td>0 - 41</td>
                  <td><span style="color:#b45309;">&uarr; +42%</span></td>
                  <td><span class="item-colored waiting">Вище норми</span></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
'''

# -------------------------------------------------------------
# 07. Biobank Archive
# -------------------------------------------------------------
cells_html = ""
for i in range(1, 101):
    bg_color = "#f1f5f9"
    text_color = "#64748b"
    status_text = "Вільна комірка"
    if i in [4, 15, 29, 46, 68]:
        bg_color = "var(--positive)"
        text_color = "#ffffff"
        status_text = "Зразок 1026004819 (Активний)"
    elif i == 23:
        bg_color = "var(--negative)"
        text_color = "#ffffff"
        status_text = "Зразок 1026003102 (Термін сплив!)"
    cells_html += f'<div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:3px; background:{bg_color}; font-size:10px; display:flex; align-items:center; justify-content:center; color:{text_color}; font-weight:700; cursor:pointer;" onclick="alert(\'Комірка #{i}: {status_text}\')">{i}</div>\n'

p07_body = f'''
      <div class="grid-2">
        <!-- Coordinate Rack Matrix Card -->
        <div class="q-card">
          <div class="q-card-header">
            <div class="q-card-title">🧊 Координатна матриця штатива №RACK-BIO-01</div>
            <span class="q-badge q-badge-info">Камера 2 (-20°C)</span>
          </div>
          <div class="q-card-body">
            <div style="font-size:12.5px; color:var(--text-muted); margin-bottom:12px;">
              Морозильник №2 · Полиця 3 · Штатив №01 · Матриця 10 &times; 10 комірок (кріопробірки):
            </div>

            <div style="display:grid; grid-template-columns:repeat(10, 1fr); gap:4px; max-width:400px; margin:0 auto 16px;">
              {cells_html}
            </div>

            <div style="display:flex; justify-content:center; gap:20px; font-size:12px;">
              <span class="item-colored active">Активний (до 30 діб)</span>
              <span class="item-colored stopped">Термін сплив (утилізація)</span>
              <span style="display:inline-flex; align-items:center; gap:4px; color:#6b7280;">
                <span style="width:8px; height:8px; background:#e2e8f0; border:1px solid #cbd5e1; border-radius:50%;"></span>
                Вільна комірка
              </span>
            </div>
          </div>
        </div>

        <!-- Biobank Operations Card -->
        <div class="q-card">
          <div class="q-card-header">
            <div class="q-card-title">📋 Операції зі зразками архіву</div>
            <span class="q-badge q-badge-info">Облік біоматеріалу</span>
          </div>
          <div class="q-card-body">
            <div style="margin-bottom:16px;">
              <label style="font-size:12.5px; font-weight:600; color:#374151;">Пошук зразка за штрихкодом:</label>
              <div style="display:flex; gap:8px; margin-top:4px;">
                <input type="text" class="q-input" value="1026004819" style="flex:1;">
                <button class="q-btn q-btn-primary" onclick="alert('Зразок знайдено: Морозильник №2, Полиця 3, Штатив RACK-BIO-01, Комірка #46!')">Знайти</button>
              </div>
            </div>

            <div style="background:#f8fafc; border:1px solid #e2e8f0; padding:12px; border-radius:4px; font-size:13.5px; line-height:1.7; margin-bottom:16px;">
              <div><strong>Штрихкод зразка:</strong> 1026004819 (Сироватка крові)</div>
              <div><strong>Пацієнт:</strong> Коваленко О.С. (ЕМК №108291)</div>
              <div><strong>Дата забору:</strong> 06.10.2026 | <strong>Термін зберігання:</strong> до 06.11.2026</div>
              <div><strong>Локація:</strong> Камера 2 &rarr; Полиця 3 &rarr; Штатив RACK-BIO-01 &rarr; Комірка #46</div>
              <div><strong>Об'єм аліквоти:</strong> 1.5 мл</div>
            </div>

            <div style="display:flex; gap:10px;">
              <button class="q-btn q-btn-outline" style="flex:1;" onclick="alert('Сформовано акт вилучення зразка для повторного або підтверджувального аналізу.')">
                Вилучити на дообстеження
              </button>
              <button class="q-btn q-btn-negative" onclick="alert('Сформовано офіційний Акт знезараження та автоклавування біоматеріалу за ДСТУ.')">
                Утилізувати за актом
              </button>
            </div>
          </div>
        </div>
      </div>
'''

# -------------------------------------------------------------
# 08. Reagent Inventory
# -------------------------------------------------------------
p08_body = '''
      <div class="q-card">
        <div class="q-card-header">
          <div class="q-card-title">🧪 Складський облік реагентів та калібраторів на борту приладів</div>
          <span class="q-badge q-badge-info">On-Board Inventory</span>
        </div>
        <div class="q-card-body" style="padding:0;">
          <div class="q-table-container">
            <table class="q-table">
              <thead>
                <tr>
                  <th>Прилад</th>
                  <th>Тест / Назва реагенту</th>
                  <th>Партія (Lot №)</th>
                  <th>Залишок тестів</th>
                  <th>Відкрито флакон</th>
                  <th>Термін придатності</th>
                  <th>Статус</th>
                  <th>Дія</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td><strong>Sysmex XN-1000</strong></td>
                  <td>Cellpack DCL (Ділюент)</td>
                  <td><code>LOT-2026-XN08</code></td>
                  <td><strong style="color:var(--primary);">1,420</strong> / 2,000</td>
                  <td>01.10.2026</td>
                  <td>31.12.2027</td>
                  <td><span class="item-colored active">Активний</span></td>
                  <td><button class="q-btn q-btn-outline" style="height:26px; font-size:11px;">Деталі</button></td>
                </tr>
                <tr style="background:#fffbeb;">
                  <td><strong>Roche Cobas e411</strong></td>
                  <td>Elecsys TSH (ТТГ)</td>
                  <td><code>LOT-682190-01</code></td>
                  <td><strong style="color:#b45309; font-size:15px;">18</strong> / 200</td>
                  <td>28.09.2026</td>
                  <td>28.10.2026 (Onboard)</td>
                  <td><span class="item-colored waiting">Малий залишок</span></td>
                  <td>
                    <button class="q-btn q-btn-primary" style="height:26px; font-size:11px;" onclick="alert('Створено електронну заявку на списання зі складу нового набору TSH!')">
                      Замовити зі складу
                    </button>
                  </td>
                </tr>
                <tr style="background:#fef2f2;">
                  <td><strong>Mindray BS-240</strong></td>
                  <td>Glucose GOD-POD</td>
                  <td><code>LOT-GLU-9902</code></td>
                  <td><strong>85</strong> / 500</td>
                  <td>05.09.2026</td>
                  <td><strong style="color:var(--negative);">05.10.2026 (ПРОСТРОЧЕНО)</strong></td>
                  <td><span class="item-colored stopped">Придатність спливла</span></td>
                  <td>
                    <button class="q-btn q-btn-negative" style="height:26px; font-size:11px;" onclick="alert('Флакон заблоковано на приладі! Лаборанту заборонено проводити аналіз без заміни касети.')">
                      Заблокувати
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
'''

bodies = [p01_body, p02_body, p03_body, p04_body, p05_body, p06_body, p07_body, p08_body]

for i, mod in enumerate(MODULES):
    target_path = os.path.join(PROT_DIR, mod["file"])
    content = wrap_prototype(mod, bodies[i])
    with open(target_path, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Successfully generated MedLink Quasar-styled: {mod['file']}")

print("All 8 prototypes updated successfully!")
