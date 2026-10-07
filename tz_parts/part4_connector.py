def get_part4():
    return """
    <!-- Section 8: Analyzer Driver Connector -->
    <section id="sec-connector">
      <h2>8. Драйверний коннектор MedLink.LabConnector (.NET Core Worker)</h2>
      
      <p>Драйверний коннектор <code>MedLink.LabConnector</code> — це високопродуктивний кросплатформний фоновий сервіс, розроблений компанією MedLink на платформі <strong>.NET 6 / .NET 8 Worker Service</strong>. Він повністю замінює застарілу 32-бітну програму на Delphi (<code>AConnectAstm.exe</code>) та забезпечує роботу як у середовищі Windows (Windows Service / Tray), так і в Linux (systemd daemon для шлюзів на базі Ubuntu/Debian або Raspberry Pi / компактних контролерів біля приладів).</p>

      <div class="alert-box alert-success">
        <div>
          <strong>Правовий статус та інтелектуальна власність:</strong> Всі майнові та авторські права на вихідний код, протокольні парсери (ASTM, HL7, MLLP), схеми буферизації та інсталятори коннектора належать виключно <strong>ТОВ «МедЛінк» (MedLink LLC)</strong>. Ліцензійна угода (EULA) та підпис цифровим сертифікатом фіксують авторство MedLink.
        </div>
      </div>

      <div class="grid-2">
        <div class="card">
          <h4>Архітектурні особливості коннектора</h4>
          <ul style="padding-left: 18px; font-size: 13.5px; color: #334155; line-height: 1.8;">
            <li><strong>Асинхронний мультиплексинг:</strong> Один екземпляр коннектора одночасно обслуговує до 32 лабораторних приладів без взаємних затримок (Task-based asynchronous I/O).</li>
            <li><strong>Підтримка інтерфейсів:</strong> Serial RS-232 (COM-порти через перехідники USB-to-COM або MOXA), TCP/IP Client/Server (порт 1000–9999), спільні файлові каталоги (Folder Watcher).</li>
            <li><strong>Локальний SQLite буфер:</strong> При зникненні Інтернету дані аналізів надійно зберігаються на диску ПК біля приладу та скидаються в хмару MedLink автоматично після відновлення зв'язку.</li>
            <li><strong>Відкритий REST API обміну:</strong> Коннектор спілкується з хмарним ядром MedLink через захищений HTTPS REST API з авторизацією за токеном клініки <code>X-MedLink-ApiKey</code>.</li>
          </ul>
        </div>

        <div class="card">
          <h4>Підтримувані протоколи обміну</h4>
          <ul style="padding-left: 18px; font-size: 13.5px; color: #334155; line-height: 1.8;">
            <li><strong>ASTM E1381 / E1394:</strong> Стандарт де-факто для аналізаторів. Повний цикл: кадр <code>ENQ &rarr; ACK &rarr; STX [Data] ETX [CRC] &rarr; EOT</code>. Двосторонній запит рознарядки за штрихкодом (Query mode).</li>
            <li><strong>HL7 v2.3.1 / v2.5 MLLP:</strong> Сучасний медичний стандарт: сегменти <code>MSH, PID, OBR, OBX</code>, квитування <code>MSA|AA</code>.</li>
            <li><strong>HL7 FHIR R4:</strong> Передача сутностей <code>Observation</code> та <code>DiagnosticReport</code>.</li>
            <li><strong>File Drop / Polling:</strong> Авторозбір CSV, XML та форматованого тексту з мережевих папок.</li>
          </ul>
        </div>
      </div>

      <h3>Каталог підтримуваного лабораторного обладнання (60+ моделей)</h3>
      <p>Завдяки аналізу збережених процедур etalon-бази (таблиця <code>ac_analyzer_type</code>) та практик ринку, в MedLink LIS включено готові профілі драйверів для всіх поширених приладів в Україні:</p>

      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Діагностичний профіль</th>
              <th>Виробники</th>
              <th>Підтримувані моделі аналізаторів</th>
              <th>Протокол</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><strong>Гематологічні аналізатори</strong></td>
              <td>Sysmex, Mindray, Nihon Kohden, Horiba, Dymind</td>
              <td>Sysmex XN-1000/2000, XS-1000i, XP-300; Mindray BC-5300, BC-6200; Nihon Kohden MEK-7300, 9100; Pentra 60/80; Dymind D5.</td>
              <td>ASTM E1394, HL7 v2.x (COM / TCP)</td>
            </tr>
            <tr>
              <td><strong>Біохімічні аналізатори</strong></td>
              <td>Roche, Mindray, BioSystems, Erba, Vital</td>
              <td>Cobas c111, c311; Mindray BS-240, BS-300, BS-800; BioSystems BA400, A15; Erba XL-200, XL-640; Vital Junior.</td>
              <td>ASTM E1394, HL7 v2.x (COM / TCP / File)</td>
            </tr>
            <tr>
              <td><strong>Імунохімічні аналізатори</strong></td>
              <td>Roche, Snibe, Beckman Coulter, YHLO, Boditech</td>
              <td>Cobas e411; Maglumi 800, 2000 Plus; Beckman Access 2; iFlash 1800; Boditech iChroma II/III.</td>
              <td>ASTM E1394, HL7 v2.x (COM / TCP)</td>
            </tr>
            <tr>
              <td><strong>Коагулометри (Гемостаз)</strong></td>
              <td>Sysmex, Stago, Werfen (IL)</td>
              <td>Sysmex CA-600, CA-1500, CS-2500; Stago Start 4, Compact Max; ACL TOP 300, 500 CTS.</td>
              <td>ASTM E1394 (COM / TCP)</td>
            </tr>
            <tr>
              <td><strong>Аналізатори сечі</strong></td>
              <td>Roche, Dirui, Beckman (Iris), Sysmex</td>
              <td>Urisys 1100, 2400; Dirui H-100, H-500; Iris iQ200; Sysmex UC-1000, UF-1000i.</td>
              <td>ASTM E1394 (COM / TCP)</td>
            </tr>
            <tr>
              <td><strong>Гази крові & Електроліти</strong></td>
              <td>Radiometer, Werfen</td>
              <td>Radiometer ABL80, ABL9, AQT90 FLEX; GEM Premier 3500, 4000.</td>
              <td>ASTM E1394 (TCP / File)</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="grid-2">
        <div class="card">
          <h4>Інсталятор для Windows (Inno Setup / MSI)</h4>
          <p style="font-size: 13.5px;">Майстер встановлення з брендуванням MedLink. Автоматично встановлює службу <code>MedLinkLabConnector</code>, відкриває порти в брандмауері Windows, налаштовує автозапуск при завантаженні ОС та створює утиліту конфігурації в панелі задач.</p>
          <pre><code>; Фрагмент інсталятора Inno Setup
AppPublisher="ТОВ 'МедЛінк' (MedLink LLC)"
AppCopyright="Copyright (c) 2026 MedLink"
DefaultDirName={autopf}\MedLink\LabConnector
sc.exe create MedLinkLabConnector binPath= "{app}\MedLink.LabConnector.exe" start= auto</code></pre>
        </div>

        <div class="card">
          <h4>Інсталятор для Linux (Systemd Service)</h4>
          <p style="font-size: 13.5px;">Для розгортання на Linux-серверах та мікрокомп'ютерах. Забезпечує безпеку (непривілейований користувач <code>medlink</code>), доступ до портів <code>dialout</code> та миттєвий перезапуск у разі збоїв.</p>
          <pre><code># /etc/systemd/system/medlink-labconnector.service
[Unit]
Description=MedLink LIS Analyzer Driver Connector Daemon
After=network.target

[Service]
ExecStart=/opt/medlink/labconnector/MedLink.LabConnector
Restart=always
User=medlink</code></pre>
        </div>
      </div>
    </section>

    <!-- Section 9: Interoperability (HL7, FHIR, eHealth) -->
    <section id="sec-interop">
      <h2>9. Інтероперабельність: eHealth (ЕСОЗ), HL7 v2, FHIR R4 та Аутсорс</h2>
      
      <p>MedLink LIS спроектовано для безшовної роботи в розподіленій медичній екосистемі України та міжнародних стандартів:</p>

      <div class="grid-3">
        <div class="card">
          <h4 style="color: #2563eb;">1. eHealth / ЕСОЗ (НСЗУ)</h4>
          <p style="font-size: 13.5px;">Повна інтеграція з центральною базою даних eHealth. Погашення електронного направлення пацієнта (Service Request). Підпис фінального висновку кваліфікованим електронним підписом (КЕП лікаря-лаборанта) та публікація ресурсу <code>DiagnosticReport</code> з прив'язкою до візиту.</p>
        </div>

        <div class="card">
          <h4 style="color: #0d9488;">2. HL7 FHIR REST API</h4>
          <p style="font-size: 13.5px;">Сучасний RESTful інтерфейс обміну за стандартом FHIR R4. Дозволяє стороннім клінікам та системам направляти замовлення (<code>ServiceRequest</code>) та отримувати структуровані результати (<code>DiagnosticReport</code> & <code>Observation</code> з кодами LOINC).</p>
        </div>

        <div class="card">
          <h4 style="color: #ca8a04;">3. Зовнішні лабораторії (Send-Out)</h4>
          <p style="font-size: 13.5px;">Автоматизоване направлення рідкісних або високовартісних аналізів (генетика, рідкісні онкомаркери) до лабораторій-партнерів (Сінево, Діла, CSD, TerraLab) із відстеженням статусу та автоімпортом результату.</p>
        </div>
      </div>
    </section>

    <!-- Section 10: Patient Portal & Monitoring -->
    <section id="sec-patient">
      <h2>10. Кабінет пацієнта: Live-моніторинг етапів та графіки динаміки</h2>
      <div style="margin-bottom:16px;"><a href="prototypes/06_patient_portal.html" target="_blank" class="btn btn-outline" style="border-color:#3b82f6; color:#2563eb; font-weight:bold;">📱 Відкрити прототип Кабінету пацієнта &rarr;</a></div>
      
      <p>Кабінет пацієнта MedLink LIS перетворює лабораторну службу з «чорної скриньки» на прозорий сервіс, ліквідуючи понад 80% телефонних запитів у реєстратуру:</p>

      <!-- Interactive Patient Portal Tracker Simulator -->
      <div class="simulator-box">
        <div class="sim-header">
          <span class="sim-title">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
            Інтерактивний кабінет пацієнта (Live-трекер замовлення №1026-004812)
          </span>
          <span style="font-size: 12px; background: #dcfce7; color: #166534; padding: 2px 8px; border-radius: 4px; font-weight: bold;">Дослідження завершено</span>
        </div>

        <!-- Stepper Progress Tracker -->
        <div style="display: flex; justify-content: space-between; position: relative; margin: 30px 20px 40px;">
          <!-- Progress Line -->
          <div style="position: absolute; top: 14px; left: 0; right: 0; height: 4px; background: #10b981; z-index: 1;"></div>

          <!-- Step 1 -->
          <div style="position: relative; z-index: 2; text-align: center; width: 80px;">
            <div style="width: 32px; height: 32px; border-radius: 50%; background: #10b981; color: white; display: flex; align-items: center; justify-content: center; margin: 0 auto 6px; font-weight: bold;">&#10003;</div>
            <div style="font-size: 11px; font-weight: 700; color: #1e293b;">Створено</div>
            <div style="font-size: 10px; color: #64748b;">08:15</div>
          </div>

          <!-- Step 2 -->
          <div style="position: relative; z-index: 2; text-align: center; width: 80px;">
            <div style="width: 32px; height: 32px; border-radius: 50%; background: #10b981; color: white; display: flex; align-items: center; justify-content: center; margin: 0 auto 6px; font-weight: bold;">&#10003;</div>
            <div style="font-size: 11px; font-weight: 700; color: #1e293b;">Забір проби</div>
            <div style="font-size: 10px; color: #64748b;">08:30</div>
          </div>

          <!-- Step 3 -->
          <div style="position: relative; z-index: 2; text-align: center; width: 80px;">
            <div style="width: 32px; height: 32px; border-radius: 50%; background: #10b981; color: white; display: flex; align-items: center; justify-content: center; margin: 0 auto 6px; font-weight: bold;">&#10003;</div>
            <div style="font-size: 11px; font-weight: 700; color: #1e293b;">В дорозі</div>
            <div style="font-size: 10px; color: #64748b;">09:10 (+4°C)</div>
          </div>

          <!-- Step 4 -->
          <div style="position: relative; z-index: 2; text-align: center; width: 80px;">
            <div style="width: 32px; height: 32px; border-radius: 50%; background: #10b981; color: white; display: flex; align-items: center; justify-content: center; margin: 0 auto 6px; font-weight: bold;">&#10003;</div>
            <div style="font-size: 11px; font-weight: 700; color: #1e293b;">В лабораторії</div>
            <div style="font-size: 10px; color: #64748b;">09:45</div>
          </div>

          <!-- Step 5 -->
          <div style="position: relative; z-index: 2; text-align: center; width: 80px;">
            <div style="width: 32px; height: 32px; border-radius: 50%; background: #10b981; color: white; display: flex; align-items: center; justify-content: center; margin: 0 auto 6px; font-weight: bold;">&#10003;</div>
            <div style="font-size: 11px; font-weight: 700; color: #1e293b;">Аналізатор</div>
            <div style="font-size: 10px; color: #64748b;">10:20</div>
          </div>

          <!-- Step 6 -->
          <div style="position: relative; z-index: 2; text-align: center; width: 80px;">
            <div style="width: 32px; height: 32px; border-radius: 50%; background: #10b981; color: white; display: flex; align-items: center; justify-content: center; margin: 0 auto 6px; font-weight: bold;">&#10003;</div>
            <div style="font-size: 11px; font-weight: 700; color: #1e293b;">Верифіковано</div>
            <div style="font-size: 10px; color: #64748b;">11:05 (КЕП)</div>
          </div>
        </div>

        <div class="grid-2" style="background: #f8fafc; padding: 16px; border-radius: 8px;">
          <div>
            <h4 style="margin-top: 0;">Результати дослідження:</h4>
            <div style="font-size: 13.5px; line-height: 1.8;">
              <div>• Глюкоза сироватки: <strong>5.2 ммоль/л</strong> (Норма 4.1 – 5.9) — <span style="color: #16a34a; font-weight: 600;">В нормі</span></div>
              <div>• Глікований гемоглобін (HbA1c): <strong>5.4%</strong> (Норма &lt; 5.7%) — <span style="color: #16a34a; font-weight: 600;">В нормі</span></div>
            </div>
            
            <div style="margin-top: 14px;">
              <button class="btn btn-primary" onclick="alert('Завантажується офіційний підписаний КЕП PDF-бланк з QR-кодом верифікації!')">Завантажити офіційний PDF з QR</button>
            </div>
          </div>

          <!-- Longitudinal Trend Mini-Chart -->
          <div>
            <h4 style="margin-top: 0;">Динаміка глюкози в часі (Тренд):</h4>
            <svg width="100%" height="90" viewBox="0 0 300 90">
              <!-- Normal corridor -->
              <rect x="20" y="25" width="260" height="35" fill="#dcfce7" opacity="0.6"/>
              <line x1="20" y1="25" x2="280" y2="25" stroke="#86efac" stroke-dasharray="2,2"/>
              <line x1="20" y1="60" x2="280" y2="60" stroke="#86efac" stroke-dasharray="2,2"/>
              <text x="285" y="28" font-size="8" fill="#16a34a">5.9</text>
              <text x="285" y="63" font-size="8" fill="#16a34a">4.1</text>
              
              <!-- Trend Line -->
              <polyline fill="none" stroke="#2563eb" stroke-width="2" points="30,55 90,48 150,65 210,40 270,38"/>
              <circle cx="30" cy="55" r="3" fill="#2563eb"/>
              <circle cx="90" cy="48" r="3" fill="#2563eb"/>
              <circle cx="150" cy="65" r="3" fill="#ef4444"/> <!-- low point -->
              <circle cx="210" cy="40" r="3" fill="#2563eb"/>
              <circle cx="270" cy="38" r="4" fill="#16a34a"/> <!-- current -->
              
              <text x="30" y="80" text-anchor="middle" font-size="8" fill="#64748b">Січ</text>
              <text x="90" y="80" text-anchor="middle" font-size="8" fill="#64748b">Кві</text>
              <text x="150" y="80" text-anchor="middle" font-size="8" fill="#64748b">Лип</text>
              <text x="210" y="80" text-anchor="middle" font-size="8" fill="#64748b">Вер</text>
              <text x="270" y="80" text-anchor="middle" font-size="8" fill="#16a34a" font-weight="bold">Жов (Сьогодні)</text>
            </svg>
          </div>
        </div>
      </div>
    </section>
"""
