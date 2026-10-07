def get_part5():
    return """
    <!-- Section 11: Technical Architecture in MedLink (evomis) -->
    <section id="sec-architecture">
      <h2>11. Технічна архітектура модуля в MedLink (evomis)</h2>
      
      <p>Лабораторний модуль розробляється як повноцінна підсистема у складі рішення <strong>MIS.Api.sln</strong> (каталог <code>C:\\__MEDLINK___\\__MEDLINK\\evomis</code>) з дотриманням існуючих патернів Clean Architecture та модульності платформи:</p>

      <div class="grid-2">
        <div class="card">
          <h4>Структура Backend (.NET 6 / C#)</h4>
          <pre><code>evomis/src/
├── App.Contracts/Lab/            # DTOs, Enums, інтерфейси
│   ├── Dtos/                     # OrderDto, ResultDto, QcDto
│   └── IAnalyzerDataService.cs
├── App.Domain/Models/lab/        # EF Core сутності (PostgreSQL)
│   ├── LabOrder.cs
│   ├── LabOrderSample.cs
│   ├── LabTestResult.cs
│   ├── LabQcMaterial.cs
│   └── LabSampleLogistics.cs
├── App.Data/Configurations/Lab/  # Fluent API мапінг таблиць
├── App.Lab.Module/               # Бізнес-логіка модуля
│   ├── Services/                 # LabOrderService, QcService
│   ├── RuleEngines/              # WestgardEngine, DeltaCheck
│   └── DependencyInjection.cs
└── App.Api/
    ├── Controllers/Lab/          # REST API контролери
    │   ├── LabOrdersController.cs
    │   ├── LabResultsController.cs
    │   ├── LabAnalyzersController.cs
    │   └── LabQcController.cs
    └── Hubs/LabHub.cs            # SignalR для оновлень у реальному часі</code></pre>
        </div>

        <div class="card">
          <h4>Структура Frontend (Vue.js / Quasar PWA)</h4>
          <pre><code>evomis/src/App.View/src/
├── pages/lab/
│   ├── PhlebotomyStation.vue    # Кабінет забору & друк ZPL
│   ├── LabWorkstation.vue       # Вікно «Дослідження» (500+ рядків)
│   ├── ValidationDesk.vue       # Робоче місце лікаря-лаборанта
│   ├── QualityControl.vue       # Контрольні карти Леві-Дженнінгса
│   ├── LogisticsManifest.vue    # Акти прийому-передачі та t°
│   ├── ArchiveRacks.vue         # Біобанк та архівні штативи
│   └── ReagentStock.vue         # Лічильники залишків тестів
├── components/lab/
│   ├── TubeCapBadge.vue         # Колірний бейдж кришки пробірки
│   ├── LeveyJenningsChart.vue   # SVG-графік Леві-Дженнінгса
│   ├── ZplBarcodeLabel.vue      # Прев'ю термостікера
│   └── CriticalAlertBanner.vue  # Спливаючий алерт паніки
└── stores/
    └── labStore.js              # Pinia/Vuex сховище черги</code></pre>
        </div>
      </div>

      <div class="card">
        <h4>Реальний час: SignalR Hub (LabHub.cs)</h4>
        <p style="font-size: 13.5px;">Для запобігання постійному опитуванню сервера (polling), результати від аналізаторів, що надходять через <code>MedLink.LabConnector</code>, миттєво публікуються через SignalR у відповідну вкладку лаборанта:</p>
        <pre><code>// App.Api/Hubs/LabHub.cs
public class LabHub : Hub
{
    public async Task SendAnalyzerResult(string analyzerCode, LabResultDto result)
    {
        await Clients.Group($"Analyzer_{analyzerCode}").SendAsync("ResultReceived", result);
        if (result.IsPanicValue)
        {
            await Clients.Group("Physicians").SendAsync("PanicAlert", result);
        }
    }
}</code></pre>
      </div>
    </section>

    <!-- Section 12: PostgreSQL Database Schema -->
    <section id="sec-db">
      <h2>12. Схема бази даних PostgreSQL (MedLink LIS)</h2>
      
      <p>Всі сутності лабораторного контуру зберігаються в реляційній БД PostgreSQL платформи <code>evomis</code> з префіксом <code>lab_</code>. Повна міграція міститься у підготовленому файлі <code>C:\\__MEDLINK___\\LABA\\dictionaries\\06_medlink_lab_schema_postgres.sql</code>:</p>

      <div class="grid-3">
        <div class="card">
          <h4 style="color: #2563eb;">1. Довідники & Номенклатура</h4>
          <ul style="font-size: 13px; line-height: 1.8; color: #334155;">
            <li><code>lab_biomaterial_types</code></li>
            <li><code>lab_tube_types</code></li>
            <li><code>lab_method_types</code></li>
            <li><code>lab_analyzer_types</code></li>
            <li><code>lab_analyzers</code></li>
            <li><code>lab_test_definitions</code></li>
            <li><code>lab_test_profiles</code></li>
            <li><code>lab_reference_ranges</code></li>
            <li><code>lab_reflex_rules</code></li>
          </ul>
        </div>

        <div class="card">
          <h4 style="color: #0d9488;">2. Замовлення, Зразки & Рутина</h4>
          <ul style="font-size: 13px; line-height: 1.8; color: #334155;">
            <li><code>lab_orders</code></li>
            <li><code>lab_order_samples</code></li>
            <li><code>lab_order_tests</code></li>
            <li><code>lab_test_results</code></li>
            <li><code>lab_batches</code> (робочі листи)</li>
            <li><code>lab_audit_logs</code></li>
          </ul>
        </div>

        <div class="card">
          <h4 style="color: #8b5cf6;">3. Контроль якості, Склад & Біобанк</h4>
          <ul style="font-size: 13px; line-height: 1.8; color: #334155;">
            <li><code>lab_qc_materials</code></li>
            <li><code>lab_qc_targets</code></li>
            <li><code>lab_qc_results</code></li>
            <li><code>lab_sample_logistics</code></li>
            <li><code>lab_sample_archive_racks</code></li>
            <li><code>lab_reagent_lots</code></li>
          </ul>
        </div>
      </div>
    </section>

    <!-- Section 13: GAP Analysis & Missed Features -->
    <section id="sec-gaps">
      <h2>13. GAP-аналіз: Що було упущено в старих ТЗ та як це вирішено</h2>
      
      <p>На основі детального аналізу предметної області та зіставлення legacy-коду Delphi, LISmart, STARLIS і MCLAB виявлено та усунуто 10 критичних прогалин:</p>

      <div class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th style="width: 40px;">№</th>
              <th style="width: 220px;">Упущений аспект у старих версіях</th>
              <th>Клінічний або операційний ризик</th>
              <th>Рішення, впроваджене в MedLink LIS 3.0</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>1</td>
              <td><strong>Порядок забору (Order of Draw)</strong></td>
              <td>Перенесення ЕДТА в цитратну пробірку спотворює час згортання та руйнує аналіз гемостазу.</td>
              <td>Вбудовано візуальну діаграму та обов'язкову черговість забору за стандартом CLSI H3-A6 у Кабінеті забору.</td>
            </tr>
            <tr>
              <td>2</td>
              <td><strong>Холодовий ланцюг у логістиці</strong></td>
              <td>Перегрів зразків у дорозі призводить до лізису клітин та деградації глюкози і ферментів.</td>
              <td>Обов'язкова валідація температури термосумки (+2...+8°C) при відправці та прийомі в електронному акті.</td>
            </tr>
            <tr>
              <td>3</td>
              <td><strong>Pre-analytical Бракераж</strong></td>
              <td>Виконання аналізу на гемолізованій або згущеній крові видає хибні результати пацієнту.</td>
              <td>Спеціалізований інтерфейс приймального столу з фіксацією гемолізу, ліпемії та автонаправленням на перезабір.</td>
            </tr>
            <tr>
              <td>4</td>
              <td><strong>Блокування приладу (QC Lockout)</strong></td>
              <td>При виході контролю якості з ладу лаборант міг випадково валідувати пацієнтські проби.</td>
              <td>Автоматичний Lockout: порушення правил Вестгарда 1-3s, 2-2s блокує вихід пацієнтських тестів цього приладу.</td>
            </tr>
            <tr>
              <td>5</td>
              <td><strong>Миттєвий алерт панічних значень</strong></td>
              <td>Лікар дізнається про смертельно небезпечну глюкозу (26 ммоль/л) лише після планового друку.</td>
              <td>Push/SMS сповіщення в МІС з вимогою обов'язкового клінічного підтвердження протягом 15 хвилин.</td>
            </tr>
            <tr>
              <td>6</td>
              <td><strong>Delta-Check за вікном 72 години</strong></td>
              <td>Пропуск підміни пробірки або раптового гострого погіршення стану нирок (креатинін).</td>
              <td>Автоматичний розрахунок дельти у % та мг/дл щодо попереднього дослідження пацієнта за останні 72 години.</td>
            </tr>
            <tr>
              <td>7</td>
              <td><strong>Reflex-правила без зайвих візитів</strong></td>
              <td>Пацієнт змушений чекати дні та повторно стояти в черзі, якщо виявлено патологію ТТГ.</td>
              <td>Автоматичне допризначення зв'язаних тестів (вТ4, антитіла) з тієї ж пробірки за затвердженим протоколом.</td>
            </tr>
            <tr>
              <td>8</td>
              <td><strong>Офлайн-стійкість коннектора</strong></td>
              <td>При збої Інтернету прилади видавали помилку переповнення буфера (Buffer Overflow).</td>
              <td>Локальна база SQLite на рівні служби <code>MedLink.LabConnector</code> зі збереженням та фоновим ретраєм.</td>
            </tr>
            <tr>
              <td>9</td>
              <td><strong>Live-моніторинг для пацієнта</strong></td>
              <td>Сотні дзвінків у реєстратуру з питанням «чи готові мої аналізи?».</td>
              <td>Публічний віджет-трекер у кабінеті пацієнта зі статусами «В дорозі», «В аналізі», «Готово».</td>
            </tr>
            <tr>
              <td>10</td>
              <td><strong>Фізичний архів (Біобанк)</strong></td>
              <td>Неможливість знайти пробірку пацієнта через 2 дні для додаткового тестування або перепровірки.</td>
              <td>Координатний облік пробірок у віртуальних штативах (Холодильник &rarr; Полиця &rarr; Штатив &rarr; Комірка [X, Y]).</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
"""
