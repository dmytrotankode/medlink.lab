def get_part2():
    return """
    
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

    <!-- Section 3: Master Registry of 48 Modules -->
    <section id="sec-registry">
      <h2>3. Зведений реєстр 48 модулів MedLink LIS (Матриця MoSCoW & Релізи)</h2>
      
      <p>Реєстр структуровано на 8 функціональних блоків. Він об'єднує всі вимоги початкового ТЗ, розширення зі STARLIS/LISmart, специфікацію MCLAB/TerraLab та нові модулі, розроблені спеціально для екосистеми MedLink:</p>

      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th style="width: 60px;">ID</th>
              <th style="width: 220px;">Модуль / Підсистема</th>
              <th style="width: 140px;">Категорія</th>
              <th>Функціональний обсяг та ключові можливості</th>
              <th style="width: 90px;">Пріоритет</th>
              <th style="width: 70px;">Реліз</th>
            </tr>
          </thead>
          <tbody>
            <!-- Core Block -->
            <tr>
              <td><strong>CORE-01</strong></td>
              <td>Автентифікація & RBAC</td>
              <td>Ядро ЛІС</td>
              <td>Авторизація через JWT/MedLink SSO, матриця ролей (Лаборант, Лікар-верифікатор, Завідувач, Медсестра забору, Кур'єр, Реєстратор), журнал аудиту доступу.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-02</strong></td>
              <td>Довідник номенклатури & Показники</td>
              <td>Ядро ЛІС</td>
              <td>Коди тестів (LOINC/внутрішні), одиниці виміру, групи показників, типи результату (число, текст, dropdown), формули розрахункових індексів (ШКФ, індекс атерогенності).</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-03</strong></td>
              <td>Довідник послуг & Пакети досліджень</td>
              <td>Ядро ЛІС</td>
              <td>Картка послуги: групування окремих тестів у комплексні панелі (ЗАК, Біохімія, Тиреоїдна панель, Ліпідограма), тарифи, тривалість виконання (TAT).</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-04</strong></td>
              <td>Багатовимірні референтні норми</td>
              <td>Ядро ЛІС</td>
              <td>Гнучкі референтні діапазони за статтю, віком (дні/місяці/роки), триместрами вагітності, фазами менструального циклу, діагнозами МКХ-10 та приладами/методиками.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-05</strong></td>
              <td>Реєстрація направлень (Замовлень)</td>
              <td>Ядро ЛІС</td>
              <td>Швидка реєстрація направлення з ЕМК або самостійно, пошук пацієнта, терміновість (CITO), друк зведеного направлення та фінансовий статус.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-06</strong></td>
              <td>Штрихкодування & Маркування пробірок</td>
              <td>Ядро ЛІС</td>
              <td>Генерація унікальних штрихкодів (Code128 / DataMatrix), авторозподіл тестів на мінімальну кількість контейнерів, друк етикеток на термопринтерах.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-07</strong></td>
              <td>Черга досліджень (Робоче місце)</td>
              <td>Ядро ЛІС</td>
              <td>Віртуальний скролінг на 1000+ записів, фільтри за статусом/підрозділом/CITO, inline-введення результатів з клавіатури, автопідсвічування критичності.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-08</strong></td>
              <td>Верифікація результатів (Лікар-лаборант)</td>
              <td>Ядро ЛІС</td>
              <td>Окреме робоче місце валідації лікаря: перевірка на прапорці аналізатора, обов'язковий коментар при патологіях, можливість повернення на повторний аналіз.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-09</strong></td>
              <td>Робочі списки (Батчі / Постановки)</td>
              <td>Ядро ЛІС</td>
              <td>Формування партій зразків під планшети, штативи ІФА або завантажувальні каруселі аналізаторів, масове призначення та масове закриття.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-10</strong></td>
              <td>Друк офіційних PDF-бланків</td>
              <td>Ядро ЛІС</td>
              <td>Генерація брендованих бланків (логотип лабораторії, норми пацієнта, таблиця показників, ПІБ лікаря-лаборанта, унікальний перевірочний QR-код).</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-11</strong></td>
              <td>Автоматична розсилка (Email / Месенджери)</td>
              <td>Ядро ЛІС</td>
              <td>Відправка захищеного PDF-файлу на пошту пацієнта або лікаря-замовника одразу після верифікації, журнал доставок, SMS/Viber сповіщення з посиланням.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>
            <tr>
              <td><strong>CORE-12</strong></td>
              <td>Журнал дій та повний аудит (Audit Trail)</td>
              <td>Ядро ЛІС</td>
              <td>Незмінний лог кожної модифікації результату: хто ввів, хто змінив, старе значення, нове значення, точний час до мілісекунд, IP-адреса.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-13</strong></td>
              <td>Базова оперативна звітність</td>
              <td>Ядро ЛІС</td>
              <td>Звіти за період: кількість виконаних тестів за підрозділами, виконавцями, приладами, відсоток патологічних результатів, експорт в Excel/CSV.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>CORE-14</strong></td>
              <td>Дизайнер шаблонів бланків</td>
              <td>Ядро ЛІС</td>
              <td>Налаштування вигляду PDF-бланка адміністратором без програмування (шапка, підвал, шрифти, додаткові примітки лабораторії).</td>
              <td><span class="badge badge-could">Could</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>
            <tr>
              <td><strong>CORE-15</strong></td>
              <td>Налаштування нумераторів та параметрів ЗОЗ</td>
              <td>Ядро ЛІС</td>
              <td>Конфігурація масок номерів замовлень, префіксів філій, реквізитів ліцензій МОЗ, колірних тем інтерфейсу.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>

            <!-- Quality Control & Safety Block -->
            <tr>
              <td><strong>QC-01</strong></td>
              <td>Автоверифікація результатів (Rule Engine)</td>
              <td>Клінічна якість</td>
              <td>Автоматичне закриття результату, якщо: значення суворо в межах норми, відсутні прапорці приладу, delta-check у нормі, тест не вимагає обов'язкового ручного перегляду.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>QC-02</strong></td>
              <td>Delta-Check (Порівняння з анамнезом)</td>
              <td>Клінічна якість</td>
              <td>Порівняння поточного показника з попереднім результатом пацієнта за вікно 72 год. При стрибку понад заданий % (напр. >25% для креатиніну) — блокування автовидачі.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>QC-03</strong></td>
              <td>Reflex-тестування (Автодопризначення)</td>
              <td>Клінічна якість</td>
              <td>Алгоритмічні ланцюжки: якщо ТТГ патологічний &rarr; автододавання вТ4; якщо позитивний скринінг гепатиту &rarr; автопризначення підтверджуючого блоту.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>
            <tr>
              <td><strong>QC-04</strong></td>
              <td>Маршрутизація критичних значень (Panic Values)</td>
              <td>Клінічна якість</td>
              <td>При виявленні загрозливих значень (глюкоза <2.5 або >25 ммоль/л, калій >6.5): миттєвий екранний алерт, push-сповіщення лікарю з вимогою підтвердити ознайомлення.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>QC-05</strong></td>
              <td>Довідник контрольних матеріалів & Серії</td>
              <td>Клінічна якість</td>
              <td>Облік контрольних сироваток (Level 1, 2, 3), номери партій, терміни придатності, стабільність відкритого флакона, паспортні середні та SD від виробника.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>QC-06</strong></td>
              <td>Контрольні карти Леві-Дженнінгса</td>
              <td>Клінічна якість</td>
              <td>Побудова інтерактивних графіків Levey-Jennings у вебі: лінії цільового значення Mean, ±1SD, ±2SD, ±3SD, розрахунок фактичного CV% та зсуву (Bias%).</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>QC-07</strong></td>
              <td>Мультиправила Вестгарда & Lockout приладу</td>
              <td>Клінічна якість</td>
              <td>Автоматична перевірка правил: 1-2s (попередження), 1-3s, 2-2s, R-4s, 4-1s, 10-x (відхилення). <strong>При порушенні блокується видача пацієнтських результатів</strong> до внесення корегуючих дій.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>QC-08</strong></td>
              <td>Зовнішній контроль якості (EQA / ФСВЯ)</td>
              <td>Клінічна якість</td>
              <td>Облік раундів міжлабораторних порівнянь, реєстрація шифрованих проб, розрахунок Z-індексу щодо консенсусного значення, формування звіту для аудиту ISO 15189.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>

            <!-- Pre-analytical & Phlebotomy Block -->
            <tr>
              <td><strong>PRE-01</strong></td>
              <td>Кабінет забору біоматеріалу (Phlebotomy Station)</td>
              <td>Преаналітика</td>
              <td>Спеціалізоване ергономічне вікно маніпуляційної медсестри: пошук пацієнта за штрихкодом/номером, чек-лист підготовки (натще, ліки), фіксація часу пункції.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>PRE-02</strong></td>
              <td>Стандарт CLSI Order of Draw (Черговість забору)</td>
              <td>Преаналітика</td>
              <td>Підказка медсестрі на екрані правильної послідовності наповнення пробірок для виключення перехресного забруднення антикоагулянтами.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>PRE-03</strong></td>
              <td>Консолідація та мінімізація кількості пробірок</td>
              <td>Преаналітика</td>
              <td>Алгоритм об'єднання тестів: аналізи, що виконуються на одному типі біоматеріалу (напр. сироватка) та потребують однакового контейнера, автоматично призначаються на 1 пробірку.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>PRE-04</strong></td>
              <td>Прямий друк етикеток на термопринтери (ZPL/TSPL)</td>
              <td>Преаналітика</td>
              <td>Генерація нативних команд ZPL II / TSPL для швидкісного друку етикеток (Zebra, TSC, Xprinter) через мережу (RAW TCP порт 9100) або USB без вікна діалогу Windows.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>PRE-05</strong></td>
              <td>Бракераж зразків & Причини відхилення</td>
              <td>Преаналітика</td>
              <td>Реєстрація дефектів проби на приймальному столі: гемоліз (+/++/+++), ліпемія, хілоз, згусток, невідповідний об'єм. Автоповідомлення лікаря на перезабір.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>

            <!-- Logistics & Biobank Block -->
            <tr>
              <td><strong>LOG-01</strong></td>
              <td>Електронні акти передачі зразків (Маніфести)</td>
              <td>Логістика</td>
              <td>Формування партій пробірок у термосумку/контейнер, генерація супровідного акта зі штрихкодом сумки, передача кур'єру з підписом/скануванням.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>LOG-02</strong></td>
              <td>Контроль холодового ланцюга (Температурний журнал)</td>
              <td>Логістика</td>
              <td>Фіксація температури при відправленні з пункту забору та при прийомі в центральній лабораторії (+2...+8°C, -20°C). Алерти при порушенні терморежиму.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>LOG-03</strong></td>
              <td>Сортування та акцепт у центральній лабораторії</td>
              <td>Логістика</td>
              <td>Швидкісне сканування пробірок сканером штрихкоду на сортувальному столі: миттєве підтвердження доставки, зміна статусу на «В лабораторії», розподіл по відділах.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>LOG-04</strong></td>
              <td>Архів біоматеріалів (Біобанк / Штативи)</td>
              <td>Логістика</td>
              <td>Облік фізичного зберігання зразків: холодильники, полиці, матричні штативи (напр. 10x10), пошук пробірки за штрихкодом (Ряд A, Колонка 5), акти утилізації за терміном.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>

            <!-- Analyzer Drivers Block -->
            <tr>
              <td><strong>DEV-01</strong></td>
              <td>Драйверний коннектор MedLink.LabConnector (.NET)</td>
              <td>Драйвери приладів</td>
              <td>Фонова служба на .NET 6/8 для Windows Service та Linux systemd daemon. Багатопотоковий моніторинг десятків приладів, права належать ТОВ «МедЛінк».</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>DEV-02</strong></td>
              <td>Протокол ASTM E1381 / ASTM E1394</td>
              <td>Драйвери приладів</td>
              <td>Низькорівневий фреймінг (ENQ/ACK/STX/ETX/CRC/EOT) та розбір повідомлень H/P/O/R/C/Q/L. Двосторонній запит завдань за штрихкодом (Query mode).</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>DEV-03</strong></td>
              <td>Протокол HL7 v2.x MLLP (ORU^R01, OML^O21)</td>
              <td>Драйвери приладів</td>
              <td>Мінімальний протокол MLLP (0x0B...0x1C 0x0D), сегменти MSH, PID, OBR, OBX, генерація квитанцій ACK, підтримка сучасних аналізаторів Mindray, Beckman.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>DEV-04</strong></td>
              <td>Каталог драйверів (Sysmex, Cobas, Mindray та ін.)</td>
              <td>Драйвери приладів</td>
              <td>Підтримка понад 60 моделей аналізаторів: гематологія (Sysmex XN/XS, Mindray BC), біохімія (Cobas, BS-240/300, BioSystems), імунохімія (Maglumi, Access).</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>DEV-05</strong></td>
              <td>Офлайн-буферизація (SQLite Store-and-Forward)</td>
              <td>Драйвери приладів</td>
              <td>При обриві інтернет-зв'язку з хмарою MedLink результати від аналізаторів накопичуються в локальній SQLite БД коннектора та автоматично синхронізуються при відновленні.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>

            <!-- Patient Experience Block -->
            <tr>
              <td><strong>PT-01</strong></td>
              <td>Автентифікація в кабінеті пацієнта</td>
              <td>Кабінет пацієнта</td>
              <td>Вхід за номером телефону через SMS OTP, автоприв'язка до ЕМК пацієнта в MedLink, опційна верифікація через Дія.Підпис / BankID.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>PT-02</strong></td>
              <td>Live-трекінг етапів дослідження</td>
              <td>Кабінет пацієнта</td>
              <td>Покроковий онлайн-трекер замовлення в реальному часі: [Зареєстровано] &rarr; [Забір біоматеріалу] &rarr; [В дорозі] &rarr; [В аналізі] &rarr; [Верифіковано]. Знімає 80% дзвінків у реєстратуру.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>PT-03</strong></td>
              <td>Графіки динаміки та трендів аналізів</td>
              <td>Кабінет пацієнта</td>
              <td>Інтерактивні графіки зміни показників у часі (глюкоза, холестерин, ТТГ) з виділенням «зеленої коридору» референтних меж.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>
            <tr>
              <td><strong>PT-04</strong></td>
              <td>Офіційний PDF-бланк із захисним QR-кодом</td>
              <td>Кабінет пацієнта</td>
              <td>Завантаження офіційного бланку з цифровим підписом. QR-код верифікації відкриває публічну сторінку підтвердження автентичності документа на порталі MedLink.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>

            <!-- Interoperability Block -->
            <tr>
              <td><strong>INT-01</strong></td>
              <td>Інтеграція з eHealth / ЕСОЗ (НСЗУ)</td>
              <td>Інтероперабельність</td>
              <td>Погашення електронних направлень (Service Request) та відправка підписаного КЕП медичного висновку DiagnosticReport згідно з вимогами МОЗ України.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>INT-02</strong></td>
              <td>Інтеграція з ЕМК лікаря MedLink (evomis)</td>
              <td>Інтероперабельність</td>
              <td>Миттєве відображення лабораторних висновків у картці пацієнта, сигналізація лікуючому лікарю про патологічні та критичні показники.</td>
              <td><span class="badge badge-must">Must</span></td>
              <td><span class="badge badge-r1">R1 (MVP)</span></td>
            </tr>
            <tr>
              <td><strong>INT-03</strong></td>
              <td>Відкритий HL7 FHIR REST API</td>
              <td>Інтероперабельність</td>
              <td>FHIR R4 ендпоінти: <code>/fhir/DiagnosticReport</code>, <code>/fhir/Observation</code>, <code>/fhir/Specimen</code>, <code>/fhir/ServiceRequest</code> для зв'язку зі сторонніми МІС.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>
            <tr>
              <td><strong>INT-04</strong></td>
              <td>Шлюзи до референс-лабораторій (Аутсорс тестів)</td>
              <td>Інтероперабельність</td>
              <td>Send-Out тести: направлення проб до партнерських лабораторій (Сінево, Діла, CSD, TerraLab) з автоматичним імпортом готових результатів.</td>
              <td><span class="badge badge-could">Could</span></td>
              <td><span class="badge badge-r3">R3</span></td>
            </tr>

            <!-- Management, Reagents & Billing Block -->
            <tr>
              <td><strong>MGT-01</strong></td>
              <td>Облік реагентів & Партії (Lot Tracking)</td>
              <td>Управління & Склад</td>
              <td>Облік наборів тестів на борту аналізаторів: лічильник залишкових тестів, дата відкриття, контроль терміну придатності реагентів.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>
            <tr>
              <td><strong>MGT-02</strong></td>
              <td>Тарифікація & Калькуляція собівартості</td>
              <td>Управління & Білінг</td>
              <td>Гнучкі прайс-листи, собівартість досліджень (реагенти + калібратори + амортизація), знижки, пакети платних послуг.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>
            <tr>
              <td><strong>MGT-03</strong></td>
              <td>Фіскалізація платних послуг (ПРРО)</td>
              <td>Управління & Білінг</td>
              <td>Інтеграція з програмним РРО (Checkbox, Вчасно.Каса) для друку електронних фіскальних чеків при розрахунку пацієнтів у пункті забору.</td>
              <td><span class="badge badge-could">Could</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>
            <tr>
              <td><strong>MGT-04</strong></td>
              <td>Аналітичний дашборд лабораторії (TAT & KPI)</td>
              <td>Управління & Аналітика</td>
              <td>Графіки Turn-Around Time (час виконання від забору до видачі), завантаженість приладів, статистика преаналітичного браку за філіями.</td>
              <td><span class="badge badge-should">Should</span></td>
              <td><span class="badge badge-r2">R2</span></td>
            </tr>
            <tr>
              <td><strong>MGT-05</strong></td>
              <td>Мікробіологічний модуль & Антибіотикограми</td>
              <td>Спеціалізовані</td>
              <td>Посіви, ідентифікація збудників, формуляр антибіотикочутливості за стандартами EUCAST (S - чутливий, I - помірний, R - стійкий).</td>
              <td><span class="badge badge-could">Could</span></td>
              <td><span class="badge badge-r3">R3</span></td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
"""
