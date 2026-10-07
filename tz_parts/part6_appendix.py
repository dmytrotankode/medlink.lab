def get_part6():
    return """
    <!-- Section 14: Roadmap -->
    <section id="sec-roadmap">
      <h2>14. Дорожня карта релізів (Спринти 1–6)</h2>
      
      <div class="grid-3">
        <div class="card" style="border-top: 4px solid #10b981;">
          <span class="badge badge-r1" style="margin-bottom: 8px;">Реліз 1 (MVP) · Спринти 1–2</span>
          <h4>Базовий повноцінний цикл</h4>
          <ul style="font-size: 13px; line-height: 1.7; color: #334155; padding-left: 16px;">
            <li>Ядро ЛІС: реєстрація, черга, inline-введення.</li>
            <li>Кабінет забору та прямий друк ZPL-етикеток.</li>
            <li>Служба <code>MedLink.LabConnector</code> (ASTM + HL7).</li>
            <li>Базовий ВЯК: графіки Леві-Дженнінгса.</li>
            <li>Верифікація лікарем & PDF-бланк з QR-кодом.</li>
            <li>Інтеграція з eHealth (погашення ЕН, КЕП).</li>
          </ul>
        </div>

        <div class="card" style="border-top: 4px solid #0284c7;">
          <span class="badge badge-r2" style="margin-bottom: 8px;">Реліз 2 · Спринти 3–4</span>
          <h4>Клінічна довіра & Логістика</h4>
          <ul style="font-size: 13px; line-height: 1.7; color: #334155; padding-left: 16px;">
            <li>Мультиправила Вестгарда & Авто-Lockout приладу.</li>
            <li>Delta-Check (72 год) & Reflex-правила.</li>
            <li>Модуль логістики зразків та холодовий ланцюг.</li>
            <li>Стіл сортування та преаналітичний бракераж.</li>
            <li>Кабінет пацієнта: динаміка показників у часі.</li>
            <li>Складський облік реагентів (Lot Tracking).</li>
          </ul>
        </div>

        <div class="card" style="border-top: 4px solid #8b5cf6;">
          <span class="badge badge-r3" style="margin-bottom: 8px;">Реліз 3 · Спринти 5–6</span>
          <h4>Корпоративна глибина & Біобанк</h4>
          <ul style="font-size: 13px; line-height: 1.7; color: #334155; padding-left: 16px;">
            <li>Архів фізичного зберігання зразків (Біобанк).</li>
            <li>Мікробіологічний блок & антибіотикограми EUCAST.</li>
            <li>Зовнішній контроль якості (EQA / ФСВЯ).</li>
            <li>FHIR REST API для зовнішніх партнерів.</li>
            <li>Інтеграція з референс-лабораторіями (Send-Out).</li>
            <li>Конструктор управлінських звітів та TAT аналітика.</li>
          </ul>
        </div>
      </div>
    </section>

    <!-- Section 15: Appendix Dictionaries -->
    <section id="sec-appendix">
      <h2>15. Додатки: Підготовлені файли довідників та міграцій для бази MedLink</h2>
      
      <p>Всі довідники та схеми вивантажено, нормалізовано та збережено в каталозі <code>C:\\__MEDLINK___\\LABA\\dictionaries\\</code> у двох форматах: JSON (для API/frontend) та SQL (для прямого завантаження в PostgreSQL):</p>

      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Файл додатку</th>
              <th>Формат</th>
              <th>К-сть записів</th>
              <th>Опис вмісту</th>
              <th>Цільова таблиця в PostgreSQL</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><code>01_biomaterials.json / .sql</code></td>
              <td>JSON / SQL</td>
              <td>18 записів</td>
              <td>Види біоматеріалів (венозна/капілярна кров, сироватка, сеча, ліквор, біоптати).</td>
              <td><code>lab_biomaterial_types</code></td>
            </tr>
            <tr>
              <td><code>02_tube_types.json / .sql</code></td>
              <td>JSON / SQL</td>
              <td>12 записів</td>
              <td>Типи пробірок та контейнерів (ЕДТА, цитрат, Li-гепарин, гель, об'єми, колірне кодування).</td>
              <td><code>lab_tube_types</code></td>
            </tr>
            <tr>
              <td><code>03_method_types.json / .sql</code></td>
              <td>JSON / SQL</td>
              <td>46 записів</td>
              <td>Методики досліджень (фотометрія, ІФА, хемілюмінесценція, коагулометрія, ПЛР).</td>
              <td><code>lab_method_types</code></td>
            </tr>
            <tr>
              <td><code>04_analyzer_types.json / .sql</code></td>
              <td>JSON / SQL</td>
              <td>64 записи</td>
              <td>Каталог типів аналізаторів (Sysmex, Cobas, Mindray, Access, Maglumi, Vitros та ін.).</td>
              <td><code>lab_analyzer_types</code></td>
            </tr>
            <tr>
              <td><code>05_lab_parameters_and_profiles.json / .sql</code></td>
              <td>JSON / SQL</td>
              <td>Пакети & Тести</td>
              <td>Стандартні профілі (ЗАК, Біохімія, Коагулограма, Тиреоїдна панель) з LOINC та нормами.</td>
              <td><code>lab_test_profiles</code> & <code>lab_test_definitions</code></td>
            </tr>
            <tr>
              <td><code>06_medlink_lab_schema_postgres.sql</code></td>
              <td>SQL DDL</td>
              <td>Повна схема</td>
              <td>Комплексний скрипт створення 18 таблиць, індексів, зовнішніх ключів та прав доступу.</td>
              <td>База даних MedLink (PostgreSQL)</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="alert-box alert-info">
        <div>
          <strong>Інструкція із застосування міграції в MedLink:</strong><br>
          Для ініціалізації лабораторного модуля в робочій або тестовій базі PostgreSQL виконайте команду:<br>
          <code>psql -h localhost -U postgres -d evomis_db -f C:\\__MEDLINK___\\LABA\\dictionaries\\06_medlink_lab_schema_postgres.sql</code><br>
          Після чого завантажте стартові довідники <code>01_*</code> ... <code>05_*</code>.
        </div>
      </div>
    </section>

  </main>
</div>

<!-- Embedded Interactive Scripts for Simulator Widgets -->
<script>
// Interactive Tube & Label Visualizer
function updateTubePreview() {
  const type = document.getElementById("simTubeSelect").value;
  const patient = document.getElementById("simPatientInput").value;
  
  const cap = document.getElementById("tubeCap");
  const liquid = document.getElementById("tubeLiquid");
  const vol = document.getElementById("tubeLabelVol");
  const lblTube = document.getElementById("lblTubeType");
  const lblTests = document.getElementById("lblTests");
  const lblPatient = document.getElementById("lblPatient");

  lblPatient.innerText = patient + " 1985 р.н.";

  if (type === "EDTA") {
    cap.style.background = "#9333ea"; // Purple
    cap.style.borderColor = "#581c87";
    liquid.style.background = "#991b1b"; // Blood
    vol.innerText = "2.6 мл";
    lblTube.innerText = "K2 EDTA 2.6ml";
    lblTests.innerText = "ЗАК + Лейкоформула + ШОЕ";
  } else if (type === "CITRATE") {
    cap.style.background = "#0284c7"; // Light Blue
    cap.style.borderColor = "#0369a1";
    liquid.style.background = "#991b1b";
    vol.innerText = "3.0 мл";
    lblTube.innerText = "Na-Citrate 3.2% 3.0ml";
    lblTests.innerText = "Коагулограма (МНВ, АЧТЧ, Фібриноген)";
  } else if (type === "GEL") {
    cap.style.background = "#eab308"; // Gold/Yellow
    cap.style.borderColor = "#ca8a04";
    liquid.style.background = "#991b1b";
    vol.innerText = "5.0 мл";
    lblTube.innerText = "Serum Gel 5.0ml";
    lblTests.innerText = "Біохімія розширена + Тиреоїдна панель";
  } else if (type === "HEPARIN") {
    cap.style.background = "#16a34a"; // Green
    cap.style.borderColor = "#15803d";
    liquid.style.background = "#991b1b";
    vol.innerText = "4.0 мл";
    lblTube.innerText = "Li-Heparin 4.0ml";
    lblTests.innerText = "Гази крові & Електроліти";
  } else if (type === "URINE") {
    cap.style.background = "#f59e0b";
    cap.style.borderColor = "#d97706";
    liquid.style.background = "#fef08a"; // Yellow urine
    vol.innerText = "60 мл";
    lblTube.innerText = "Контейнер сечі 60ml";
    lblTests.innerText = "Загальний аналіз сечі (ЗАС)";
  }
}

function simulateZplPrint() {
  alert("Команда ZPL успішно надіслана на термопринтер Zebra ZD420 (RAW TCP 9100)!\nЕтикетка надрукована за 0.4 сек.");
}

function copyZplCode() {
  const zpl = `^XA
^PW400
^LL240
^PON
^FO20,15^A0N,20,20^FDMedLink LIS^FS
^FO200,15^A0N,18,18^FD06.10.2026 15:45^FS
^FO20,38^GB360,1,2^FS
^FO20,45^A0N,22,22^FDКоваленко О.С. 1985^FS
^FO20,70^A0N,18,18^FDЗАК + Лейкоформула^FS
^FO40,95^BY2,2.5,45^BCN,45,Y,N,N^FD1026004819^FS
^FO20,195^A0N,18,18^FDK2 EDTA 2.6ml^FS
^FO260,195^A0N,18,18^FDПункт N1^FS
^XZ`;
  navigator.clipboard.writeText(zpl).then(() => {
    alert("Код ZPL скопійовано в буфер обміну!");
  });
}

function toggleAutoFilter(status) {
  const rows = document.querySelectorAll("#simQueueTable tbody tr");
  rows.forEach(r => {
    if (status === "ALL") {
      r.style.display = "";
    } else if (status === "PANIC") {
      r.style.display = r.innerText.includes("PANIC") ? "" : "none";
    } else if (status === "IN_PROGRESS") {
      r.style.display = r.innerText.includes("В роботі") || r.innerText.includes("ВИЩЕ") ? "" : "none";
    }
  });
}

// Smooth scroll for nav links
document.querySelectorAll(".nav-link").forEach(link => {
  link.addEventListener("click", e => {
    document.querySelectorAll(".nav-link").forEach(l => l.classList.remove("active"));
    link.classList.add("active");
  });
});
</script>

</body>
</html>
"""
