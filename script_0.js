
    Quasar.lang.set(Quasar.lang.uk);

    new Vue({
      el: '#q-app',
      data: {
        leftDrawerOpen: true,
        currentView: 'workstation',
        currentViewTitle: 'Робочий стіл лаборанта',
        defaultPagination: { rowsPerPage: 10 },
        
        // Dialog visibility
        showBarcodeDialog: false,
        showCollectDialog: false,
        showCallDialog: false,
        showEditResultDialog: false,
        showLogDialog: false,
        showQcUnlockDialog: false,
        showRejectionDialog: false,

        activeOrder: null,
        editingRow: null,
        patientStep: 4,
        qcCorrectiveAction: 'Промито оптичну кювету Cellclean, виконано заміну ділюента, контрольний замір L2 = 7.21 (OK).',

        collectCheck: { fasting: true, idVerified: true, orderOfDraw: true, mixing: true },
        callLog: {
          doctor: 'Савченко І.О. (Черговий реаніматолог)',
          department: 'ВРІТ',
          phone: 'вн. 214',
          readback: 'Значення 26.4 ммоль/л підтверджено голосом'
        },

        // Filters
        worklistSearch: '',
        worklistAnalyzerFilter: 'Всі прилади',
        worklistFlagFilter: 'Всі результати',

        // Mock Datasets
        ordersList: [
          {
            id: "ord-2026-004819",
            orderNumber: "1026-004819",
            patientName: "Коваленко О.С.",
            patientAge: 41,
            patientGender: "M",
            department: "Поліклініка №1",
            createdAt: "2026-10-06 08:30",
            priority: "ROUTINE",
            status: "IN_PROGRESS",
            ehealthReferralCode: "5491-8821-9012"
          },
          {
            id: "ord-2026-004812",
            orderNumber: "1026-004812",
            patientName: "Мельник Ю.В.",
            patientAge: 48,
            patientGender: "M",
            department: "ВРІТ",
            createdAt: "2026-10-06 09:10",
            priority: "CITO",
            status: "PANIC_ALERT",
            ehealthReferralCode: "7721-3310-4491"
          },
          {
            id: "ord-2026-004825",
            orderNumber: "1026-004825",
            patientName: "Василенко О.П.",
            patientAge: 34,
            patientGender: "F",
            department: "Жіноча конс.",
            createdAt: "2026-10-04 11:20",
            priority: "ROUTINE",
            status: "VERIFIED",
            ehealthReferralCode: "8821-4401-1192"
          }
        ],

        samplesList: [
          { barcode: "1026004818", orderId: "ord-2026-004819", biomaterial: "Венозна кров (плазма)", tubeType: "Цитрат натрію 3.2%", capColor: "#0284c7", capName: "Блакитна", volume: "3.0 мл", orderOfDraw: 1, status: "COLLECTED" },
          { barcode: "1026004819", orderId: "ord-2026-004819", biomaterial: "Сироватка крові", tubeType: "Активатор згортання / Гель", capColor: "#ca8a04", capName: "Жовта", volume: "5.0 мл", orderOfDraw: 2, status: "IN_LAB" },
          { barcode: "1026004820", orderId: "ord-2026-004819", biomaterial: "Цільна венозна кров", tubeType: "K2/K3 ЕДТА", capColor: "#9333ea", capName: "Фіолетова", volume: "2.6 мл", orderOfDraw: 3, status: "COLLECTED" }
        ],

        worklist: [
          { id: "res-101", barcode: "1026004812", patientName: "Мельник Ю.В.", analyzer: "Cobas e411", testCode: "GLU", testName: "Глюкоза сироватки", value: 26.4, unit: "ммоль/л", normMin: 4.1, normMax: 5.9, flag: "PANIC_HIGH", deltaPercent: "+185%", status: "PENDING_VERIFY", comment: "Критично високий рівень! Ризик гіперосмолярної коми." },
          { id: "res-102", barcode: "1026004819", patientName: "Коваленко О.С.", analyzer: "Mindray BS-240", testCode: "ALT", testName: "Аланінамінотрансфераза (АЛТ)", value: 68.5, unit: "U/L", normMin: 0.0, normMax: 41.0, flag: "DELTA_ALERT", deltaPercent: "+42.7%", status: "PENDING_VERIFY", comment: "Помірний цитоліз. Delta-чек перевищує поріг 25%." },
          { id: "res-103", barcode: "1026004819", patientName: "Коваленко О.С.", analyzer: "Mindray BS-240", testCode: "CREAT", testName: "Креатинін сироватки", value: 84.0, unit: "мкмоль/л", normMin: 62.0, normMax: 115.0, flag: "NORMAL", deltaPercent: "-2.1%", status: "AUTO_VERIFIED", comment: "В межах норми." },
          { id: "res-104", barcode: "1026004820", patientName: "Коваленко О.С.", analyzer: "Sysmex XN-1000", testCode: "WBC", testName: "Лейкоцити (WBC)", value: 7.45, unit: "10*9/л", normMin: 4.0, normMax: 9.0, flag: "NORMAL", deltaPercent: "+1.2%", status: "AUTO_VERIFIED", comment: "В межах норми." },
          { id: "res-105", barcode: "1026004820", patientName: "Коваленко О.С.", analyzer: "Sysmex XN-1000", testCode: "HGB", testName: "Гемоглобін (HGB)", value: 148.0, unit: "г/л", normMin: 130.0, normMax: 160.0, flag: "NORMAL", deltaPercent: "0.0%", status: "AUTO_VERIFIED", comment: "В межах норми." }
        ],

        qcData: {
          analyzer: "Sysmex XN-1000 (#SN-41029)",
          controlMaterial: "XN-CHECK Level 2 (Normal)",
          lotNumber: "LOT-XN-2026-L2",
          parameter: "WBC (Лейкоцити)",
          targetMean: 7.20,
          targetSd: 0.30,
          cvPercent: 4.1,
          dataPoints: [
            { day: 1, val: 7.15, status: "OK" },
            { day: 2, val: 7.22, status: "OK" },
            { day: 3, val: 7.05, status: "OK" },
            { day: 4, val: 7.35, status: "OK" },
            { day: 5, val: 7.18, status: "OK" },
            { day: 6, val: 7.25, status: "OK" },
            { day: 7, val: 7.42, status: "WARN_1_2S" },
            { day: 8, val: 7.10, status: "OK" },
            { day: 9, val: 7.02, status: "OK" },
            { day: 10, val: 7.21, status: "OK" },
            { day: 11, val: 7.55, status: "WARN_1_2S" },
            { day: 12, val: 7.19, status: "OK" },
            { day: 13, val: 7.28, status: "OK" },
            { day: 14, val: 7.32, status: "OK" },
            { day: 15, val: 8.28, status: "FAIL_1_3S" }
          ]
        },

        microbiologyData: {
          antibiotics: [
            { name: "Фосфоміцин", mic: "≤ 1.0", zone: 28, eucast: "S", interpretation: "Чутливий (1-а лінія)" },
            { name: "Нітрофурантоїн", mic: "16.0", zone: 22, eucast: "S", interpretation: "Чутливий" },
            { name: "Ципрофлоксацин", mic: "0.25", zone: 26, eucast: "S", interpretation: "Чутливий" },
            { name: "Ампіцилін", mic: "> 32.0", zone: 11, eucast: "R", interpretation: "Резистентний (Стійкий)" },
            { name: "Амоксицилін / Клаванат", mic: "8.0", zone: 17, eucast: "I", interpretation: "Помірно-чутливий" }
          ]
        },

        reagentsList: [
          { id: "reag-01", analyzer: "Sysmex XN-1000", name: "Cellpack DCL (Ділюент)", lotNumber: "LOT-2026-XN08", testsRemaining: 1420, testsTotal: 2000, openedAt: "2026-10-01", expiresAt: "2027-12-31", status: "ACTIVE" },
          { id: "reag-02", analyzer: "Roche Cobas e411", name: "Elecsys TSH (ТТГ)", lotNumber: "LOT-682190-01", testsRemaining: 18, testsTotal: 200, openedAt: "2026-09-28", expiresAt: "2026-10-28", status: "LOW_STOCK" },
          { id: "reag-03", analyzer: "Mindray BS-240", name: "Glucose GOD-POD", lotNumber: "LOT-GLU-9902", testsRemaining: 85, testsTotal: 500, openedAt: "2026-09-05", expiresAt: "2026-10-05", status: "EXPIRED" }
        ],

        analyzersList: [
          { id: "an-01", name: "Sysmex XN-1000", type: "Гематологічний 5-diff", protocol: "ASTM E1381/E1394", connection: "TCP/IP 192.168.1.101:5100", status: "ONLINE" },
          { id: "an-02", name: "Roche Cobas e411", type: "Імунохімічний", protocol: "ASTM E1394", connection: "COM3 (9600 8N1)", status: "ONLINE" },
          { id: "an-03", name: "Mindray BS-240", type: "Біохімічний", protocol: "HL7 v2.3.1 MLLP", connection: "TCP/IP 192.168.1.105:5000", status: "ONLINE" },
          { id: "an-04", name: "Sysmex CA-660", type: "Коагулометр", protocol: "ASTM E1381", connection: "COM4 (9600 8N1)", status: "STANDBY" }
        ],

        normsList: [
          { id: 1, param: 'Глюкоза сироватки', gender: 'Обидва', ageGroup: 'Дорослі (18-60 р.)', method: 'Гексокіназний (IFCC)', unit: 'ммоль/л', normMin: 4.10, normMax: 5.90, panicLow: 2.50, panicHigh: 25.00 },
          { id: 2, param: 'Гемоглобін (HGB)', gender: 'Чоловіки', ageGroup: 'Дорослі (>18 р.)', method: 'SLS-метод (безціанідний)', unit: 'г/л', normMin: 130.0, normMax: 160.0, panicLow: 70.0, panicHigh: 200.0 },
          { id: 3, param: 'Гемоглобін (HGB)', gender: 'Жінки', ageGroup: 'Дорослі (>18 р.)', method: 'SLS-метод (безціанідний)', unit: 'г/л', normMin: 120.0, normMax: 150.0, panicLow: 70.0, panicHigh: 200.0 },
          { id: 4, param: 'Креатинін сироватки', gender: 'Чоловіки', ageGroup: 'Дорослі (>18 р.)', method: 'Ензиматичний (IDMS)', unit: 'мкмоль/л', normMin: 62.0, normMax: 115.0, panicLow: 30.0, panicHigh: 350.0 }
        ],

        biomaterialsList: [
          { code: "BLDV", name: "Цільна кров венозна", container: "EDTA / Цитрат", snomed: "122555007", storage: "2-8°C до 24 год" },
          { code: "SER", name: "Сироватка крові", container: "Активатор згортання / Гель", snomed: "119364003", storage: "2-8°C до 7 діб, -20°C до 6 міс" },
          { code: "PLAS", name: "Плазма крові (цитратна)", container: "Цитрат натрію 3.2%", snomed: "119361006", storage: "2-8°C до 4 год" },
          { code: "URIN", name: "Сеча (ранкова порція)", container: "Стерильний контейнер", snomed: "122575003", storage: "2-8°C до 4 год" }
        ],

        tubesList: [
          { code: 'TUBE-CITRATE', name: 'Цитрат натрію 3.2%', colorName: 'Блакитна', colorHex: '#0284c7', volume: '3.0 мл', orderOfDraw: 1, inversions: '3-4 рази' },
          { code: 'TUBE-SERUM-GEL', name: 'Активатор згортання / Гель', colorName: 'Жовта', colorHex: '#ca8a04', volume: '5.0 мл', orderOfDraw: 2, inversions: '5-6 разів' },
          { code: 'TUBE-EDTA', name: 'K2 / K3 ЕДТА', colorName: 'Фіолетова', colorHex: '#9333ea', volume: '2.6 мл', orderOfDraw: 3, inversions: '8-10 разів' }
        ],

        analyzerModelsList: [
          { code: 'SYSMEX-XN1000', vendor: 'Sysmex Corporation', model: 'XN-1000', discipline: 'Гематологія 5-diff', protocol: 'ASTM E1381/E1394', interfaceType: 'TCP/IP Client/Server' },
          { code: 'ROCHE-COBAS-E411', vendor: 'Roche Diagnostics', model: 'Cobas e411', discipline: 'Імунохімія', protocol: 'ASTM E1394', interfaceType: 'RS-232 / TCP' },
          { code: 'MINDRAY-BS240', vendor: 'Mindray Medical', model: 'BS-240', discipline: 'Біохімія', protocol: 'HL7 v2.3.1 MLLP', interfaceType: 'TCP/IP MLLP' }
        ],

        parametersList: [
          { code: 'WBC', name: 'Лейкоцити (White Blood Cells)', category: 'Гематологія', loinc: '6690-2', unit: '10*9/л', sampleType: 'EDTA кров' },
          { code: 'HGB', name: 'Гемоглобін (Hemoglobin)', category: 'Гематологія', loinc: '718-7', unit: 'г/л', sampleType: 'EDTA кров' },
          { code: 'GLU', name: 'Глюкоза сироватки', category: 'Біохімія', loinc: '2345-7', unit: 'ммоль/л', sampleType: 'Сироватка' },
          { code: 'ALT', name: 'Аланінамінотрансфераза (АЛТ)', category: 'Біохімія', loinc: '1742-6', unit: 'U/L', sampleType: 'Сироватка' }
        ],

        // Table Columns
        worklistColumns: [
          { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left', sortable: true },
          { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left', sortable: true },
          { name: 'analyzer', label: 'Аналізатор', field: 'analyzer', align: 'left' },
          { name: 'testName', label: 'Тест', field: 'testName', align: 'left' },
          { name: 'value', label: 'Результат', field: 'value', align: 'right', sortable: true },
          { name: 'norm', label: 'Норма', align: 'center' },
          { name: 'flag', label: 'Флаг', field: 'flag', align: 'center' },
          { name: 'deltaPercent', label: 'Delta %', field: 'deltaPercent', align: 'right' },
          { name: 'status', label: 'Статус', field: 'status', align: 'center' },
          { name: 'actions', label: 'Дії', align: 'center' }
        ],

        orderColumns: [
          { name: 'orderNumber', label: '№ Замовлення', field: 'orderNumber', align: 'left', sortable: true },
          { name: 'createdAt', label: 'Дата / Час', field: 'createdAt', align: 'left' },
          { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left', sortable: true },
          { name: 'department', label: 'Відділення', field: 'department', align: 'left' },
          { name: 'priority', label: 'Пріоритет', field: 'priority', align: 'center' },
          { name: 'ehealthReferralCode', label: 'Код е-Направлення', field: 'ehealthReferralCode', align: 'left' },
          { name: 'status', label: 'Статус', field: 'status', align: 'center' },
          { name: 'actions', label: 'Дії', align: 'center' }
        ],

        validationColumns: [
          { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left' },
          { name: 'testName', label: 'Показник', field: 'testName', align: 'left' },
          { name: 'value', label: 'Результат', field: 'value', align: 'right' },
          { name: 'flag', label: 'Флаг', field: 'flag', align: 'center' },
          { name: 'comment', label: 'Клінічний коментар', field: 'comment', align: 'left' },
          { name: 'actions', label: 'Дії лікаря', align: 'center' }
        ],

        qcColumns: [
          { name: 'day', label: 'День місяця', field: 'day', align: 'center' },
          { name: 'val', label: 'Значення', field: 'val', align: 'right' },
          { name: 'status', label: 'Вестгард', field: 'status', align: 'center' }
        ],

        mbColumns: [
          { name: 'name', label: 'Антибіотик', field: 'name', align: 'left' },
          { name: 'mic', label: 'МІК (мкг/мл)', field: 'mic', align: 'center' },
          { name: 'zone', label: 'Зона (мм)', field: 'zone', align: 'center' },
          { name: 'eucast', label: 'EUCAST', field: 'eucast', align: 'center' },
          { name: 'interpretation', label: 'Клінічна інтерпретація', field: 'interpretation', align: 'left' }
        ],

        reagentColumns: [
          { name: 'analyzer', label: 'Аналізатор', field: 'analyzer', align: 'left' },
          { name: 'name', label: 'Реактив / Касета', field: 'name', align: 'left' },
          { name: 'lotNumber', label: 'Номер лоту', field: 'lotNumber', align: 'left' },
          { name: 'testsRemaining', label: 'Залишок тестів', field: 'testsRemaining', align: 'center' },
          { name: 'expiresAt', label: 'Придатний до', field: 'expiresAt', align: 'center' },
          { name: 'status', label: 'Статус', field: 'status', align: 'center' }
        ],

        analyzerColumns: [
          { name: 'name', label: 'Модель', field: 'name', align: 'left' },
          { name: 'type', label: 'Тип', field: 'type', align: 'left' },
          { name: 'protocol', label: 'Протокол', field: 'protocol', align: 'center' },
          { name: 'connection', label: 'З'єднання', field: 'connection', align: 'left' },
          { name: 'status', label: 'Статус', field: 'status', align: 'center' },
          { name: 'actions', label: 'Дії', align: 'center' }
        ],

        normsColumns: [
          { name: 'param', label: 'Показник', field: 'param', align: 'left' },
          { name: 'gender', label: 'Стать', field: 'gender', align: 'center' },
          { name: 'ageGroup', label: 'Вік', field: 'ageGroup', align: 'left' },
          { name: 'method', label: 'Методика', field: 'method', align: 'left' },
          { name: 'unit', label: 'Одиниця', field: 'unit', align: 'center' },
          { name: 'normMin', label: 'Норма Min', field: 'normMin', align: 'right' },
          { name: 'normMax', label: 'Норма Max', field: 'normMax', align: 'right' },
          { name: 'panicRange', label: 'Панічний поріг (L/H)', align: 'center' }
        ],

        dictBioColumns: [
          { name: 'code', label: 'Код', field: 'code', align: 'left' },
          { name: 'name', label: 'Назва', field: 'name', align: 'left' },
          { name: 'container', label: 'Контейнер', field: 'container', align: 'left' },
          { name: 'snomed', label: 'SNOMED CT', field: 'snomed', align: 'center' },
          { name: 'storage', label: 'Умови зберігання', field: 'storage', align: 'left' }
        ],

        dictTubeColumns: [
          { name: 'code', label: 'Код', field: 'code', align: 'left' },
          { name: 'name', label: 'Наповнювач', field: 'name', align: 'left' },
          { name: 'color', label: 'Колір кришки', align: 'center' },
          { name: 'volume', label: 'Об'єм', field: 'volume', align: 'center' },
          { name: 'orderOfDraw', label: 'Order of Draw', field: 'orderOfDraw', align: 'center' },
          { name: 'inversions', label: 'Перевертання', field: 'inversions', align: 'left' }
        ],

        dictAnColumns: [
          { name: 'code', label: 'Код', field: 'code', align: 'left' },
          { name: 'vendor', label: 'Виробник', field: 'vendor', align: 'left' },
          { name: 'model', label: 'Модель', field: 'model', align: 'left' },
          { name: 'discipline', label: 'Дисципліна', field: 'discipline', align: 'left' },
          { name: 'protocol', label: 'Протокол', field: 'protocol', align: 'center' },
          { name: 'interfaceType', label: 'Інтерфейс', field: 'interfaceType', align: 'left' }
        ],

        dictParamColumns: [
          { name: 'code', label: 'Код', field: 'code', align: 'left' },
          { name: 'name', label: 'Назва', field: 'name', align: 'left' },
          { name: 'category', label: 'Категорія', field: 'category', align: 'left' },
          { name: 'loinc', label: 'LOINC', field: 'loinc', align: 'center' },
          { name: 'unit', label: 'Одиниця', field: 'unit', align: 'center' },
          { name: 'sampleType', label: 'Рекомендований зразок', field: 'sampleType', align: 'left' }
        ]
      },

      computed: {
        filteredWorklist() {
          return this.worklist.filter(r => {
            const matchesQuery = !this.worklistSearch ||
              r.testName.toLowerCase().includes(this.worklistSearch.toLowerCase()) ||
              r.barcode.includes(this.worklistSearch) ||
              r.patientName.toLowerCase().includes(this.worklistSearch.toLowerCase());
            const matchesAnalyzer = this.worklistAnalyzerFilter === 'Всі прилади' || r.analyzer.includes(this.worklistAnalyzerFilter);
            const matchesFlag = this.worklistFlagFilter === 'Всі результати' ||
              (this.worklistFlagFilter.includes('паніка') && r.flag !== 'NORMAL') ||
              (this.worklistFlagFilter === 'Нормальні' && r.flag === 'NORMAL');
            return matchesQuery && matchesAnalyzer && matchesFlag;
          });
        },
        pendingValidationList() {
          return this.worklist.filter(r => r.status === 'PENDING_VERIFY');
        },
        activeOrderSamples() {
          if (!this.activeOrder) return [];
          return this.samplesList.filter(s => s.orderId === this.activeOrder.id);
        }
      },

      methods: {
        setView(viewName, title) {
          this.currentView = viewName;
          this.currentViewTitle = title;
        },
        notify(msg, color = 'positive') {
          this.$q.notify({ message: msg, color: color, position: 'top-right', timeout: 2500 });
        },
        getValueClass(flag) {
          if (flag === 'PANIC_HIGH' || flag === 'PANIC_LOW') return 'text-negative text-weight-bolder bg-red-1';
          if (flag === 'DELTA_ALERT') return 'text-deep-orange text-weight-bold';
          return 'text-dark';
        },
        getFlagColor(flag) {
          switch (flag) {
            case 'PANIC_HIGH':
            case 'PANIC_LOW': return 'negative';
            case 'DELTA_ALERT': return 'deep-orange';
            case 'NORMAL': return 'positive';
            default: return 'grey';
          }
        },
        formatFlag(flag) {
          switch (flag) {
            case 'PANIC_HIGH': return 'КРИТИЧНО ВИСОКИЙ';
            case 'PANIC_LOW': return 'КРИТИЧНО НИЗЬКИЙ';
            case 'DELTA_ALERT': return 'DELTA УВАГА';
            case 'NORMAL': return 'НОРМА';
            default: return flag;
          }
        },
        getOrderStatusColor(st) {
          switch (st) {
            case 'IN_PROGRESS': return 'blue-7';
            case 'PANIC_ALERT': return 'negative';
            case 'VERIFIED': return 'positive';
            default: return 'grey-6';
          }
        },
        formatOrderStatus(st) {
          switch (st) {
            case 'IN_PROGRESS': return 'В роботі';
            case 'PANIC_ALERT': return 'Паніка!';
            case 'VERIFIED': return 'Завершено';
            default: return st;
          }
        },
        runAutoValidation() {
          let count = 0;
          this.worklist.forEach(r => {
            if (r.flag === 'NORMAL' && r.status === 'PENDING_VERIFY') {
              r.status = 'AUTO_VERIFIED';
              count++;
            }
          });
          this.notify(`Автоматично валідовано ${count} нормальних результатів за критеріями CLSI.`);
        },
        openBarcodeDialog(order) {
          this.activeOrder = order;
          this.showBarcodeDialog = true;
        },
        openCollectDialog(order) {
          this.activeOrder = order;
          this.showCollectDialog = true;
        },
        confirmPrintZpl() {
          this.notify(`Друк етикеток ZPL успішно відправлено на принтер для ${this.activeOrderSamples.length} пробірок.`);
          this.showBarcodeDialog = false;
        },
        confirmCollect() {
          if (this.activeOrder) this.activeOrder.status = 'IN_PROGRESS';
          this.notify('Забір біоматеріалу успішно зареєстровано.');
          this.showCollectDialog = false;
        },
        saveCall() {
          this.notify('Дзвінок зафіксовано в журналі передачі критичних значень.');
          this.showCallDialog = false;
        },
        validateRow(row) {
          row.status = 'VERIFIED';
          this.notify(`Результат ${row.testName} валідовано лікарем.`);
        },
        openEditResult(row) {
          this.editingRow = { ...row };
          this.showEditResultDialog = true;
        },
        saveEditedRow() {
          const idx = this.worklist.findIndex(r => r.id === this.editingRow.id);
          if (idx !== -1) {
            this.worklist.splice(idx, 1, this.editingRow);
          }
          this.notify('Результат збережено');
          this.showEditResultDialog = false;
        },
        confirmQcUnlock() {
          this.notify('Аналізатор успішно розблоковано. Коригувальну дію записано.');
          this.showQcUnlockDialog = false;
        }
      }
    });
  