# -*- coding: utf-8 -*-
"""
Script to embed the complete directory of all 12 Laboratory Processes with
interactive prototype transition buttons into the Master Specification and README Hub.
"""

import os
import re

# Update part2_modules.py to include the Master Directory of 12 Laboratory Processes
part2_file = r"c:\__MEDLINK___\LABA\tz_parts\part2_modules.py"

with open(part2_file, "r", encoding="utf-8") as f:
    part2_content = f.read()

# Directory table of 12 processes
master_directory_html = """
    <!-- Section 2.1: Master Directory of 12 Laboratory Processes & Interactive Prototypes -->
    <section id="sec-processes-directory">
      <h2>2.1. Зведений реєстр 12 наскрізних процесів лабораторії та перехід до прототипів</h2>
      
      <p>Для кожного клініко-діагностичного та управлінського процесу лабораторії розроблено повнофункціональний інтерактивний прототип сторінки у фірмовому стилі <strong>MedLink (evomis Quasar Design System)</strong>. Кожен прототип містить клінічні інструкції, валідатори, симуляцію даних та інтерактивні елементи:</p>

      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th style="width: 50px;">№</th>
              <th style="width: 220px;">Клінічний процес ЛІС</th>
              <th style="width: 160px;">Відповідальна роль</th>
              <th>Ключовий функціонал, стандарти та бізнес-правила</th>
              <th style="width: 140px; text-align: center;">Прототип інтерфейсу</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><strong>01</strong></td>
              <td><strong>Пункт забору біоматеріалу & Маркування</strong></td>
              <td><span class="badge badge-r1">Медсестра забору</span></td>
              <td>Пошук за е-направленням ЕСОЗ, 2-факторна ідентифікація, преаналітичний чек-лист, CLSI Order of Draw (порядок наповнення пробірок), автоконсолідація у мінімальну кількість контейнерів, прямий ZPL-друк штрихкодів.</td>
              <td style="text-align: center;">
                <a href="prototypes/01_phlebotomy_station.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 01 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>02</strong></td>
              <td><strong>Логістика зразків & Приймальний стіл</strong></td>
              <td><span class="badge badge-r2">Кур'єр / Сортувальник</span></td>
              <td>Електронні акти передачі (маніфести термосумок), моніторинг температурних логерів (+2...+8°C), бракеражний стіл за 12 дефектами (гемоліз, хілоз, згусток, недобір) з автонаправленням на повторний забір.</td>
              <td style="text-align: center;">
                <a href="prototypes/02_specimen_logistics.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 02 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>03</strong></td>
              <td><strong>Робоче місце лаборанта «Дослідження»</strong></td>
              <td><span class="badge badge-r1">Фельдшер-лаборант</span></td>
              <td>Апаратна черга аналізаторів (Sysmex, Cobas, Mindray), inline-введення з клавіатури, розрахункові індекси (лейкоформула, HOMA-IR), світлова сигналізація відхилень і Delta-Check.</td>
              <td style="text-align: center;">
                <a href="prototypes/03_lab_workstation.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 03 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>04</strong></td>
              <td><strong>Верифікація лікаря & Panic Values</strong></td>
              <td><span class="badge badge-must">Лікар-лаборант</span></td>
              <td>Екстрений банер панічних значень (глюкоза 26.4 ммоль/л) з таймером 15 хв передачі алерту, допризначення тестів Reflex-Engine (ТТГ -> вТ4), підписання висновку КЕП/ЕЦП для передачі в ЕСОЗ.</td>
              <td style="text-align: center;">
                <a href="prototypes/04_validation_and_panic.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 04 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>05</strong></td>
              <td><strong>Внутрішній контроль якості (ВЯК)</strong></td>
              <td><span class="badge badge-must">Менеджер якості</span></td>
              <td>Інтерактивні карти Леві-Дженнінгса з межами ±1SD, ±2SD, ±3SD, аналіз 6 правил Вестгарда, автоматичний Lockout приладу при порушенні правила 1-3s до внесення протоколу CAPA.</td>
              <td style="text-align: center;">
                <a href="prototypes/05_quality_control.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 05 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>06</strong></td>
              <td><strong>Особистий кабінет пацієнта</strong></td>
              <td><span class="badge badge-r3">Пацієнт</span></td>
              <td>Live-трекінг етапів замовлення (Оформлено -> Забір -> В дорозі -> Аналіз -> Готово), інтерпретація норм, графік динаміки показників за роками, PDF-бланк з перевірочним QR-кодом.</td>
              <td style="text-align: center;">
                <a href="prototypes/06_patient_portal.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 06 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>07</strong></td>
              <td><strong>Фізичний архів біоматеріалів (Біобанк)</strong></td>
              <td><span class="badge badge-r2">Архіваріус</span></td>
              <td>Координатна матриця 10x10 кріоштатива морозильної камери (-20°C), пошук пробірки за штрихкодом, кольорове маркування активних/прострочених проб, формування актів утилізації.</td>
              <td style="text-align: center;">
                <a href="prototypes/07_biobank_archive.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 07 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>08</strong></td>
              <td><strong>Складський облік реагентів (Lot Tracking)</strong></td>
              <td><span class="badge badge-should">Старший лаборант</span></td>
              <td>On-board облік залишку тестів у касетах аналізаторів, контроль стабільності після розкриття флакона (On-board Stability), журнал верифікації зміни партій (Lot-to-lot).</td>
              <td style="text-align: center;">
                <a href="prototypes/08_reagent_inventory.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 08 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>09</strong></td>
              <td><strong>Бактеріологічний посів & Антибіотикограма</strong></td>
              <td><span class="badge badge-r1">Бактеріолог</span></td>
              <td>Облік первинного посіву, ідентифікація патогенів (E. coli, S. aureus), автоінтерпретація чутливості до антибіотиків за критеріями EUCAST v14.0 (категорії S / I / R, виявлення MRSA/ESBL).</td>
              <td style="text-align: center;">
                <a href="prototypes/09_microbiology_culture.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 09 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>10</strong></td>
              <td><strong>Аналітичний дашборд лабораторії (TAT & KPI)</strong></td>
              <td><span class="badge badge-must">Головний лікар</span></td>
              <td>Моніторинг нормативів Turn-Around Time (CITO &lt; 60 хв, планові &lt; 6 год), графіки обсягів досліджень за підрозділами, аналітика преаналітичного браку за філіями забору.</td>
              <td style="text-align: center;">
                <a href="prototypes/10_lab_analytics_tat.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 10 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>11</strong></td>
              <td><strong>Монітор шлюзу аналізаторів (LabConnector)</strong></td>
              <td><span class="badge badge-r2">Інженер ЛІС</span></td>
              <td>Стан фізичних з'єднань (RS-232, TCP/IP), живий монітор сирих фреймів ASTM E1381/E1394 та HL7 MLLP, перевірка контрольних сум, черга локальної SQLite-буферизації.</td>
              <td style="text-align: center;">
                <a href="prototypes/11_analyzer_connector_monitor.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 11 &rarr;</a>
              </td>
            </tr>
            <tr>
              <td><strong>12</strong></td>
              <td><strong>Конструктор норм, методик та розрахунків</strong></td>
              <td><span class="badge badge-should">Методолог КДЛ</span></td>
              <td>Багатовимірна матриця референтних інтервалів (стать, вік у днях/роках, триместри вагітності), межі панічних порогів, налаштування формул (eGFR CKD-EPI, індекс HOMA).</td>
              <td style="text-align: center;">
                <a href="prototypes/12_norms_and_methodologies.html" target="_blank" class="btn btn-primary" style="padding: 5px 10px; font-size: 11.5px; text-decoration: none;">🖥 Відкрити 12 &rarr;</a>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
"""

# Insert master_directory_html right before "<!-- Section 3: Master Registry of 48 Modules -->"
if "<!-- Section 2.1:" not in part2_content:
    part2_content = part2_content.replace(
        '<!-- Section 3: Master Registry of 48 Modules -->',
        master_directory_html + '\n    <!-- Section 3: Master Registry of 48 Modules -->'
    )
    with open(part2_file, "w", encoding="utf-8") as f:
        f.write(part2_content)
    print("Embedded Master Directory of 12 Processes into part2_modules.py!")
else:
    print("Master Directory already present in part2_modules.py.")

