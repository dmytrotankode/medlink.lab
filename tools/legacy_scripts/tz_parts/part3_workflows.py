def get_part3():
    return """
    <!-- Section 4: Phlebotomy Station & Labeling -->
    <section id="sec-phlebotomy">
      <h2>4. Кабінет забору біоматеріалу (Phlebotomy Station) та маркування</h2>
      <div style="margin-bottom:16px;"><a href="prototypes/01_phlebotomy_station.html" target="_blank" class="btn btn-outline" style="border-color:#3b82f6; color:#2563eb; font-weight:bold;">🖥 Відкрити повноцінний екранний прототип Кабінету забору &rarr;</a></div>
      
      <p>Пункт забору біоматеріалу (як при стаціонарі/поліклініці, так і віддалене відділення) є критичною точкою преаналітичного етапу, де виникає до 70% усіх діагностичних помилок. Модуль MedLink LIS проектується для максимального захисту від людського фактору:</p>

      <div class="grid-2">
        <div class="card">
          <h4>Клінічний чек-лист взяття біоматеріалу</h4>
          <ul style="padding-left: 20px; font-size: 14px; color: #334155; line-height: 1.8;">
            <li><strong>Ідентифікація пацієнта:</strong> двофакторне підтвердження (ПІБ + дата народження або номер карти).</li>
            <li><strong>Верифікація підготовки:</strong> фіксація статусу (натщесерце / не натщесерце, прийом антикоагулянтів/гормонів).</li>
            <li><strong>Анатомічна локалізація:</strong> вибір вени (ліктьова права/ліва, тил кисті, капілярний забір).</li>
            <li><strong>Фіксація часу:</strong> точний системний таймштамп пункції з прив'язкою до ідентифікатора медсестри.</li>
          </ul>
        </div>

        <div class="card">
          <h4>Міжнародний стандарт CLSI H3-A6: Order of Draw</h4>
          <p style="font-size: 13.5px; margin-bottom: 8px;">Суворе дотримання черговості наповнення пробірок запобігає перенесенню добавок з однієї пробірки в іншу через голку:</p>
          <ol style="padding-left: 20px; font-size: 13.5px; color: #334155; line-height: 1.6;">
            <li><strong style="color: #ca8a04;">Флакони для гемокультури</strong> (посів крові на стерильність)</li>
            <li><strong style="color: #0284c7;">Цитрат натрію 3.2%</strong> (Блакитна кришка — коагулограма)</li>
            <li><strong style="color: #dc2626;">Активатор згортання / Гель</strong> (Червона/Жовта — біохімія, імунохімія)</li>
            <li><strong style="color: #16a34a;">Літій-гепарин</strong> (Зелена кришка — експрес-біохімія, електроліти)</li>
            <li><strong style="color: #9333ea;">К2/К3 ЕДТА</strong> (Фіолетова кришка — загальний аналіз крові, HbA1c)</li>
            <li><strong style="color: #475569;">Фторид натрію / Оксалат</strong> (Сіра кришка — глюкоза, лактат)</li>
          </ol>
        </div>
      </div>

      <!-- Interactive Widget: Barcode & Tube Visualizer -->
      <div class="simulator-box">
        <div class="sim-header">
          <span class="sim-title">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M7 7h3v10H7zM14 7h3v10h-3z"/></svg>
            Інтерактивний візуалізатор пробірки та термодрук етикетки
          </span>
          <span style="font-size: 12px; color: #64748b;">MedLink Pre-Analytical Engine</span>
        </div>

        <div class="grid-2">
          <div>
            <div style="margin-bottom: 12px;">
              <label style="font-size: 13px; font-weight: 600; display: block; margin-bottom: 4px;">Тип контейнера / Пробірки:</label>
              <select id="simTubeSelect" onchange="updateTubePreview()" style="width: 100%; padding: 8px; border: 1px solid var(--border); border-radius: 6px; font-size: 13px;">
                <option value="EDTA">K2/K3 ЕДТА (Фіолетова) — Загальний аналіз крові</option>
                <option value="CITRATE">Цитрат натрію 3.2% (Блакитна) — Коагулограма</option>
                <option value="GEL">Гель / Активатор (Жовта) — Біохімія & Імунохімія</option>
                <option value="HEPARIN">Li-Гепарин (Зелена) — Гази & Електроліти</option>
                <option value="URINE">Стерильний контейнер (Біла/Жовта) — Сеча</option>
              </select>
            </div>
            
            <div style="margin-bottom: 12px;">
              <label style="font-size: 13px; font-weight: 600; display: block; margin-bottom: 4px;">ПІБ Пацієнта:</label>
              <input type="text" id="simPatientInput" value="Коваленко Олександр Сергійович" oninput="updateTubePreview()" style="width: 100%; padding: 8px; border: 1px solid var(--border); border-radius: 6px; font-size: 13px;">
            </div>

            <div style="display: flex; gap: 8px;">
              <button class="btn btn-primary" onclick="simulateZplPrint()">Друк ZPL на термопринтер</button>
              <button class="btn btn-outline" onclick="copyZplCode()">Копіювати ZPL код</button>
            </div>
          </div>

          <!-- Live Visual Tube & Label Mockup -->
          <div style="background: #f8fafc; border: 1px dashed #cbd5e1; border-radius: 8px; padding: 16px; display: flex; align-items: center; justify-content: space-around;">
            <!-- Tube graphic -->
            <div style="display: flex; flex-direction: column; align-items: center;">
              <div id="tubeCap" style="width: 34px; height: 38px; background: #9333ea; border-radius: 6px 6px 0 0; border: 2px solid #581c87; box-shadow: inset 0 2px 4px rgba(255,255,255,0.4);"></div>
              <div style="width: 30px; height: 110px; background: rgba(226, 232, 240, 0.6); border: 2px solid #94a3b8; border-top: none; border-radius: 0 0 15px 15px; position: relative; overflow: hidden; display: flex; flex-direction: column; justify-content: flex-end;">
                <div id="tubeLiquid" style="width: 100%; height: 75%; background: #991b1b; opacity: 0.85;"></div>
              </div>
              <span id="tubeLabelVol" style="font-size: 11px; font-weight: 700; color: #475569; margin-top: 4px;">2.6 мл</span>
            </div>

            <!-- Thermal Label (50x30 mm) Preview -->
            <div style="width: 200px; height: 120px; background: #ffffff; border: 1px solid #1e293b; border-radius: 4px; padding: 8px; box-shadow: var(--shadow-sm); font-family: monospace; font-size: 10px; display: flex; flex-direction: column; justify-content: space-between;">
              <div>
                <div style="display: flex; justify-content: space-between; font-weight: bold; border-bottom: 1px solid #000; padding-bottom: 2px; font-size: 9px;">
                  <span>MedLink LIS</span>
                  <span id="lblDate">06.10.2026 15:45</span>
                </div>
                <div id="lblPatient" style="font-weight: 700; font-size: 10px; margin-top: 3px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">Коваленко О.С. 1985 р.н.</div>
                <div id="lblTests" style="font-size: 8.5px; color: #334155;">ЗАК + Лейкоформула + ШОЕ</div>
              </div>
              
              <!-- SVG 1D Barcode -->
              <div style="text-align: center; margin: 4px 0;">
                <svg width="170" height="32" viewBox="0 0 170 32">
                  <rect x="0" y="0" width="3" height="26" fill="#000"/>
                  <rect x="5" y="0" width="2" height="26" fill="#000"/>
                  <rect x="9" y="0" width="5" height="26" fill="#000"/>
                  <rect x="17" y="0" width="2" height="26" fill="#000"/>
                  <rect x="22" y="0" width="4" height="26" fill="#000"/>
                  <rect x="29" y="0" width="3" height="26" fill="#000"/>
                  <rect x="34" y="0" width="6" height="26" fill="#000"/>
                  <rect x="43" y="0" width="2" height="26" fill="#000"/>
                  <rect x="48" y="0" width="4" height="26" fill="#000"/>
                  <rect x="55" y="0" width="5" height="26" fill="#000"/>
                  <rect x="63" y="0" width="2" height="26" fill="#000"/>
                  <rect x="68" y="0" width="6" height="26" fill="#000"/>
                  <rect x="77" y="0" width="3" height="26" fill="#000"/>
                  <rect x="83" y="0" width="2" height="26" fill="#000"/>
                  <rect x="88" y="0" width="5" height="26" fill="#000"/>
                  <rect x="96" y="0" width="4" height="26" fill="#000"/>
                  <rect x="103" y="0" width="2" height="26" fill="#000"/>
                  <rect x="108" y="0" width="6" height="26" fill="#000"/>
                  <rect x="117" y="0" width="3" height="26" fill="#000"/>
                  <rect x="123" y="0" width="5" height="26" fill="#000"/>
                  <rect x="131" y="0" width="2" height="26" fill="#000"/>
                  <rect x="136" y="0" width="4" height="26" fill="#000"/>
                  <rect x="143" y="0" width="6" height="26" fill="#000"/>
                  <rect x="152" y="0" width="3" height="26" fill="#000"/>
                  <rect x="158" y="0" width="4" height="26" fill="#000"/>
                  <rect x="165" y="0" width="3" height="26" fill="#000"/>
                  <text x="85" y="32" text-anchor="middle" font-size="8" fill="#000" font-family="monospace">1026004819</text>
                </svg>
              </div>

              <div style="display: flex; justify-content: space-between; font-size: 8px;">
                <span id="lblTubeType">K2 EDTA 2.6ml</span>
                <span>Відділення №1</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Section 5: Specimen Logistics & Accessioning -->
    <section id="sec-logistics">
      <h2>5. Логістика біоматеріалу, холодовий ланцюг та лабораторний бракераж</h2>
      <div style="margin-bottom:16px;"><a href="prototypes/02_specimen_logistics.html" target="_blank" class="btn btn-outline" style="border-color:#3b82f6; color:#2563eb; font-weight:bold;">🚚 Відкрити прототип Логістики та бракеражу &rarr;</a></div>
      
      <p>Для мереж лабораторій або лікарень із відокремленими корпусами транспортування зразків регламентується модулем логістики:</p>

      <div class="grid-3">
        <div class="card">
          <h4>1. Електронний маніфест передачі</h4>
          <p style="font-size: 13.5px;">Партія пробірок сканується в термосумку. Формується Акт прийому-передачі з QR-кодом контейнера. Кур'єр підтверджує прийом в один клік у веб-інтерфейсі смартфону.</p>
        </div>
        <div class="card">
          <h4>2. Контроль холодового ланцюга</h4>
          <p style="font-size: 13.5px;">Обов'язкова фіксація температури за термодатчиком: при виїзді з пункту забору (+4°C) та при прибутті в лабораторію (+5°C). При відхиленні понад +8°C система маркує рейс як інцидент.</p>
        </div>
        <div class="card">
          <h4>3. Стіл акцепту та бракеражу</h4>
          <p style="font-size: 13.5px;">Оператор приймального відділення сканує пробірку. При виявленні преаналітичного дефекту вказується статус браку з автоматичним сповіщенням на повторний забір.</p>
        </div>
      </div>

      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Тип браку зразка</th>
              <th>Клінічний вплив на аналіз</th>
              <th>Дія системи MedLink LIS</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><strong>Гемоліз (+, ++, +++)</strong></td>
              <td>Штучне завищення Калію (K+), ЛДГ, АСТ, АЛТ, заліза через руйнування еритроцитів.</td>
              <td>Блокування аналізу біохімічних тестів, автоматичне завдання медсестрі на перезабір, повідомлення лікарю.</td>
            </tr>
            <tr>
              <td><strong>Хілоз / Ліпемія</strong></td>
              <td>Каламутність сироватки спотворює оптичну фотометрію та нефелометрію.</td>
              <td>Рекомендація повторного забору після 12-годинного голодування.</td>
            </tr>
            <tr>
              <td><strong>Наявність мікрозгустків</strong></td>
              <td>Забиває капілярну гідравліку гематологічного аналізатора, спотворює тромбоцити.</td>
              <td>Категорична відмова в аналізі ЗАК/коагулограми.</td>
            </tr>
            <tr>
              <td><strong>Недостатній об'єм (Underfilling)</strong></td>
              <td>Порушує співвідношення кров/антикоагулянт (особливо для цитрату 1:9).</td>
              <td>Анулювання направлення на коагулограму (непридатний результат МНВ/АЧТЧ).</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Section 6: Laboratory Workstation (Вікно Дослідження) -->
    <section id="sec-workstation">
      <h2>6. Робоче місце «Дослідження» та обробка результатів</h2>
      <div style="margin-bottom:16px; display:flex; gap:10px;"><a href="prototypes/03_lab_workstation.html" target="_blank" class="btn btn-outline" style="border-color:#3b82f6; color:#2563eb; font-weight:bold;">🔬 Відкрити робоче місце лаборанта &rarr;</a><a href="prototypes/04_validation_and_panic.html" target="_blank" class="btn btn-outline" style="border-color:#ef4444; color:#dc2626; font-weight:bold;">🚨 Прототип верифікації лікаря & Panic Alert &rarr;</a></div>
      
      <p>Головний екран лаборанта та лікаря-верифікатора оптимізовано під високу швидкість обробки (до 500–1000 проб за зміну):</p>

      <!-- Interactive Lab Queue Simulator -->
      <div class="simulator-box">
        <div class="sim-header">
          <span class="sim-title">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 6h16M4 10h16M4 14h16M4 18h16"/></svg>
            Симулятор черги досліджень (Віртуальний скролінг & Inline-введення)
          </span>
          <div>
            <button class="btn btn-outline" style="font-size: 11px; padding: 4px 8px;" onclick="toggleAutoFilter('ALL')">Всі (5)</button>
            <button class="btn btn-outline" style="font-size: 11px; padding: 4px 8px; color: #b91c1c;" onclick="toggleAutoFilter('PANIC')">Критичні (1)</button>
            <button class="btn btn-outline" style="font-size: 11px; padding: 4px 8px; color: #0369a1;" onclick="toggleAutoFilter('IN_PROGRESS')">В роботі (2)</button>
          </div>
        </div>

        <div class="table-wrapper" style="margin: 0;">
          <table id="simQueueTable">
            <thead>
              <tr>
                <th>Штрихкод</th>
                <th>Пацієнт</th>
                <th>Дослідження</th>
                <th>Аналізатор</th>
                <th>Показник</th>
                <th>Результат</th>
                <th>Норма</th>
                <th>Статус / Алерт</th>
                <th>Дія</th>
              </tr>
            </thead>
            <tbody>
              <tr style="background: #fef2f2;">
                <td><code>1026004812</code></td>
                <td><strong>Мельник Ю.В.</strong> (48 р., Ч)</td>
                <td>Глюкоза крові</td>
                <td>Roche Cobas e411</td>
                <td>Глюкоза</td>
                <td><strong style="color: #b91c1c;">26.4</strong> ммоль/л</td>
                <td>4.1 – 5.9</td>
                <td><span class="badge" style="background: #ef4444; color: white;">PANIC HIGH</span></td>
                <td><button class="btn btn-primary" style="padding: 2px 8px; font-size: 11px;" onclick="alert('Відкрито модальне вікно екстреного виклику лікаря! Сповіщення надіслано в МІС лікуючому лікарю.')">Алерт лікарю</button></td>
              </tr>
              <tr>
                <td><code>1026004815</code></td>
                <td><strong>Шевченко І.П.</strong> (32 р., Ж)</td>
                <td>ЗАК розгорнутий</td>
                <td>Sysmex XN-1000</td>
                <td>Гемоглобін (HGB)</td>
                <td><input type="text" value="128.0" style="width: 60px; padding: 2px 4px; border: 1px solid #94a3b8; border-radius: 4px; font-weight: bold;"> г/л</td>
                <td>120 – 140</td>
                <td><span class="badge" style="background: #ecfdf5; color: #065f46;">НОРМА (Авто)</span></td>
                <td><button class="btn btn-success" style="padding: 2px 8px; font-size: 11px;" onclick="alert('Результат верифіковано лікарем!')">Верифікувати</button></td>
              </tr>
              <tr>
                <td><code>1026004819</code></td>
                <td><strong>Коваленко О.С.</strong> (41 р., Ч)</td>
                <td>Печінкові проби</td>
                <td>Mindray BS-240</td>
                <td>АЛТ</td>
                <td><strong style="color: #b45309;">68.5</strong> Од/л</td>
                <td>0 – 41</td>
                <td><span class="badge badge-should">ВИЩЕ НОРМИ</span></td>
                <td><button class="btn btn-success" style="padding: 2px 8px; font-size: 11px;" onclick="alert('Результат верифіковано з приміткою лікаря!')">Верифікувати</button></td>
              </tr>
              <tr>
                <td><code>1026004822</code></td>
                <td><strong>Бондар Т.М.</strong> (64 р., Ж)</td>
                <td>Коагулограма</td>
                <td>Sysmex CA-600</td>
                <td>МНВ (INR)</td>
                <td><input type="text" value="1.05" style="width: 60px; padding: 2px 4px; border: 1px solid #94a3b8; border-radius: 4px; font-weight: bold;"> од</td>
                <td>0.85 – 1.15</td>
                <td><span class="badge" style="background: #ecfdf5; color: #065f46;">НОРМА</span></td>
                <td><button class="btn btn-success" style="padding: 2px 8px; font-size: 11px;" onclick="alert('Результат верифіковано!')">Верифікувати</button></td>
              </tr>
              <tr>
                <td><code>1026004825</code></td>
                <td><strong>Ткач В.А.</strong> (29 р., Ч)</td>
                <td>Тиреоїдна панель</td>
                <td>Snibe Maglumi 800</td>
                <td>ТТГ (TSH)</td>
                <td><strong style="color: #4338ca;">0.12</strong> мкМО/мл</td>
                <td>0.4 – 4.0</td>
                <td><span class="badge" style="background: #fef3c7; color: #92400e;">REFLEX TRIGGER</span></td>
                <td><button class="btn btn-outline" style="padding: 2px 8px; font-size: 11px;" onclick="alert('Reflex-правило активовано: автоматично призначено вТ4 без потреби нового направлення!')">+ Додати вТ4</button></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <!-- Section 7: Quality Control (Levey-Jennings & Westgard) -->
    <section id="sec-qc">
      <h2>7. Внутрішній контроль якості (ВЯК): Карти Леві-Дженнінгса та правила Вестгарда</h2>
      <div style="margin-bottom:16px;"><a href="prototypes/05_quality_control.html" target="_blank" class="btn btn-outline" style="border-color:#10b981; color:#059669; font-weight:bold;">📊 Відкрити прототип ВЯК & Levey-Jennings &rarr;</a></div>
      
      <p>Модуль ВЯК гарантує метрологічну достовірність результатів згідно з міжнародними стандартами ISO 15189. Він повністю відсутній у колишньому Delphi-рішенні та реалізується в MedLink LIS з нуля на базі алгоритмів Westgard Multirule:</p>

      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Правило Вестгарда</th>
              <th>Тип помилки</th>
              <th>Умова спрацювання</th>
              <th>Реакція MedLink LIS (Lockout Engine)</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><strong>1-2s</strong></td>
              <td>Попередження</td>
              <td>1 контрольне вимірювання вийшло за межі Mean &plusmn; 2SD.</td>
              <td>Попереджувальний жовтий маркер. Видача пацієнтських тестів <strong>дозволена</strong>, вимагає пильності оператора.</td>
            </tr>
            <tr>
              <td><strong>1-3s</strong></td>
              <td>Випадкова помилка</td>
              <td>1 вимірювання вийшло за межі Mean &plusmn; 3SD.</td>
              <td><strong>ВІДХИЛЕННЯ СЕРІЇ. Автоматичний Lockout:</strong> блокування видачі результатів пацієнтів цього приладу/тесту!</td>
            </tr>
            <tr>
              <td><strong>2-2s</strong></td>
              <td>Систематична помилка</td>
              <td>2 послідовних вимірювання вийшли за межі &plusmn; 2SD по один бік від середнього.</td>
              <td><strong>ВІДХИЛЕННЯ СЕРІЇ. Lockout.</strong> Потрібне калібрування приладу або заміна лоту реагенту.</td>
            </tr>
            <tr>
              <td><strong>R-4s</strong></td>
              <td>Випадкова помилка</td>
              <td>Різниця між двома контролями в серії перевищує 4SD (один > +2SD, інший < -2SD).</td>
              <td><strong>ВІДХИЛЕННЯ СЕРІЇ. Lockout.</strong> Помилка дозування або дефект оптичного каналу.</td>
            </tr>
            <tr>
              <td><strong>4-1s</strong></td>
              <td>Систематичний зсув</td>
              <td>4 послідовних вимірювання перевищують 1SD по один бік від середнього.</td>
              <td><strong>ВІДХИЛЕННЯ СЕРІЇ.</strong> Систематичний дрейф калібрувальної кривої.</td>
            </tr>
            <tr>
              <td><strong>10-x</strong></td>
              <td>Систематичний зсув</td>
              <td>10 послідовних вимірювань лежать по один бік від Mean.</td>
              <td><strong>ВІДХИЛЕННЯ СЕРІЇ.</strong> Знос лампи фотометра або старіння робочого буферу.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Interactive Levey-Jennings SVG Chart Widget -->
      <div class="simulator-box">
        <div class="sim-header">
          <span class="sim-title">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>
            Інтерактивна контрольна карта Леві-Дженнінгса (Sysmex XN-1000 / WBC)
          </span>
          <div style="font-size: 12px; display: flex; gap: 10px; align-items: center;">
            <span>Цільове: <strong>7.20</strong> &times; 10^9/л</span>
            <span>SD: <strong>0.30</strong></span>
            <span>CV: <strong>4.16%</strong></span>
          </div>
        </div>

        <!-- SVG Chart -->
        <div style="background: #ffffff; border-radius: 8px; padding: 10px; position: relative;">
          <svg id="ljSvgChart" width="100%" height="260" viewBox="0 0 700 260" style="overflow: visible;">
            <!-- Background bands -->
            <rect x="50" y="20" width="630" height="200" fill="#f8fafc"/>
            <rect x="50" y="53" width="630" height="134" fill="#ecfdf5" opacity="0.6"/> <!-- +/- 2SD zone -->
            <rect x="50" y="86" width="630" height="68" fill="#dcfce7" opacity="0.8"/> <!-- +/- 1SD zone -->

            <!-- Grid Lines -->
            <!-- +3SD Line -->
            <line x1="50" y1="20" x2="680" y2="20" stroke="#ef4444" stroke-dasharray="4,4" stroke-width="1.5"/>
            <text x="42" y="24" text-anchor="end" font-size="10" fill="#ef4444" font-weight="bold">+3SD (8.10)</text>

            <!-- +2SD Line -->
            <line x1="50" y1="53" x2="680" y2="53" stroke="#f59e0b" stroke-dasharray="3,3" stroke-width="1"/>
            <text x="42" y="57" text-anchor="end" font-size="10" fill="#f59e0b">+2SD (7.80)</text>

            <!-- +1SD Line -->
            <line x1="50" y1="86" x2="680" y2="86" stroke="#94a3b8" stroke-dasharray="2,2" stroke-width="1"/>
            <text x="42" y="90" text-anchor="end" font-size="10" fill="#64748b">+1SD (7.50)</text>

            <!-- Mean Center Line -->
            <line x1="50" y1="120" x2="680" y2="120" stroke="#0284c7" stroke-width="2"/>
            <text x="42" y="124" text-anchor="end" font-size="10" fill="#0284c7" font-weight="bold">Mean (7.20)</text>

            <!-- -1SD Line -->
            <line x1="50" y1="154" x2="680" y2="154" stroke="#94a3b8" stroke-dasharray="2,2" stroke-width="1"/>
            <text x="42" y="158" text-anchor="end" font-size="10" fill="#64748b">-1SD (6.90)</text>

            <!-- -2SD Line -->
            <line x1="50" y1="187" x2="680" y2="187" stroke="#f59e0b" stroke-dasharray="3,3" stroke-width="1"/>
            <text x="42" y="191" text-anchor="end" font-size="10" fill="#f59e0b">-2SD (6.60)</text>

            <!-- -3SD Line -->
            <line x1="50" y1="220" x2="680" y2="220" stroke="#ef4444" stroke-dasharray="4,4" stroke-width="1.5"/>
            <text x="42" y="224" text-anchor="end" font-size="10" fill="#ef4444" font-weight="bold">-3SD (6.30)</text>

            <!-- Data Polyline -->
            <polyline fill="none" stroke="#2563eb" stroke-width="2" points="
              70,118 110,105 150,130 190,95 230,122 270,110 310,88 350,125 390,140 430,115 470,60 510,120 550,108 590,125 630,12
            "/>

            <!-- Normal Points -->
            <circle cx="70" cy="118" r="4.5" fill="#2563eb"/>
            <circle cx="110" cy="105" r="4.5" fill="#2563eb"/>
            <circle cx="150" cy="130" r="4.5" fill="#2563eb"/>
            <circle cx="190" cy="95" r="4.5" fill="#2563eb"/>
            <circle cx="230" cy="122" r="4.5" fill="#2563eb"/>
            <circle cx="270" cy="110" r="4.5" fill="#2563eb"/>
            <circle cx="310" cy="88" r="4.5" fill="#2563eb"/>
            <circle cx="350" cy="125" r="4.5" fill="#2563eb"/>
            <circle cx="390" cy="140" r="4.5" fill="#2563eb"/>
            <circle cx="430" cy="115" r="4.5" fill="#2563eb"/>

            <!-- Warning Point (1-2s): Day 11, y=60 (~2.1SD) -->
            <circle cx="470" cy="60" r="6" fill="#f59e0b" stroke="#ffffff" stroke-width="2"/>
            
            <circle cx="510" cy="120" r="4.5" fill="#2563eb"/>
            <circle cx="550" cy="108" r="4.5" fill="#2563eb"/>
            <circle cx="590" cy="125" r="4.5" fill="#2563eb"/>

            <!-- Violation Point (1-3s Out of Control): Day 15, y=12 (> +3SD) -->
            <circle cx="630" cy="12" r="7" fill="#ef4444" stroke="#ffffff" stroke-width="2" style="cursor: pointer;" onclick="alert('Порушення правила Вестгарда 1-3s (WBC = 8.28)! Активовано режим Lockout: видача ЗАК на Sysmex XN-1000 заблокована до калібрування.')"/>

            <!-- Day Axis Labels -->
            <text x="70" y="245" text-anchor="middle" font-size="10" fill="#64748b">1</text>
            <text x="150" y="245" text-anchor="middle" font-size="10" fill="#64748b">3</text>
            <text x="230" y="245" text-anchor="middle" font-size="10" fill="#64748b">5</text>
            <text x="310" y="245" text-anchor="middle" font-size="10" fill="#64748b">7</text>
            <text x="390" y="245" text-anchor="middle" font-size="10" fill="#64748b">9</text>
            <text x="470" y="245" text-anchor="middle" font-size="10" fill="#64748b">11</text>
            <text x="550" y="245" text-anchor="middle" font-size="10" fill="#64748b">13</text>
            <text x="630" y="245" text-anchor="middle" font-size="10" fill="#64748b">15</text>
            <text x="360" y="258" text-anchor="middle" font-size="11" font-weight="600" fill="#334155">Дні вимірювання контрольної сироватки (Партія QC-WBC-2026B)</text>
          </svg>
        </div>

        <div style="margin-top: 14px; background: #fef2f2; border: 1px solid #fecaca; border-radius: 6px; padding: 10px 14px; display: flex; justify-content: space-between; align-items: center;">
          <div style="font-size: 13px; color: #991b1b;">
            <strong>Увага! Спрацював автоматичний Lockout:</strong> Точка №15 (8.28 &times; 10^9/л) порушила правило Вестгарда <strong>1-3s</strong> (перевищення +3.6 SD).
          </div>
          <button class="btn btn-outline" style="border-color: #f87171; color: #b91c1c; font-size: 12px; padding: 4px 10px;" onclick="alert('Форма внесення коригуючих дій лікаря-лаборанта: перевірка температури кювети, повторне калібрування.')">Внести протокол усунення</button>
        </div>
      </div>
    </section>
"""
