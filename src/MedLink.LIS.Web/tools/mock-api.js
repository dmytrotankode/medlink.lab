#!/usr/bin/env node
/*
 * Мінімальний mock API (/api/v1/lab/*) для smoke-тестів та скриншотів фронтенду БЕЗ бекенду.
 * Дані демонстраційні; жодних мутацій не зберігає (POST/PUT/DELETE повертають echo).
 * Використання: node tools/mock-api.js [port=5055]
 */
const http = require('http');
const url = require('url');

const port = Number(process.argv[2] || 5055);
const now = new Date();
const iso = (minsAgo = 0) => new Date(now.getTime() - minsAgo * 60000).toISOString();
const ids = n => Array.from({ length: n }, (_, i) => `00000000-0000-4000-8000-${String(i + 1).padStart(12, '0')}`);

const employees = [
  { id: 'emp-admin', fullName: 'Шевченко Ірина Петрівна', position: 'Адміністратор лабораторії', labRole: 'LAB_ADMIN' },
  { id: 'emp-doctor', fullName: 'Мельник Володимир Сергійович', position: 'Лікар-лаборант, зав. КДЛ', labRole: 'LAB_DOCTOR' },
  { id: 'emp-tech', fullName: 'Коваль Оксана Іванівна', position: 'Фельдшер-лаборант', labRole: 'LAB_TECHNICIAN' },
  { id: 'emp-nurse', fullName: 'Бондар Тетяна Олегівна', position: 'Медсестра пункту забору', labRole: 'PHLEBOTOMIST' },
  { id: 'emp-courier', fullName: 'Петренко Андрій Миколайович', position: 'Кур’єр', labRole: 'LOGISTICS_COURIER' },
  { id: 'emp-reg', fullName: 'Савченко Марія Василівна', position: 'Реєстратор', labRole: 'REGISTRAR' }
];
const lab = { name: 'Клініко-діагностична лабораторія «МедЛінк»', edrpou: '12345678', address: 'м. Київ, вул. Лабораторна, 1', phone: '+380 44 123 45 67', email: 'lab@medlink.ua', panicPhone: '+380 50 000 00 00', labelPrinterHost: '', labelPrinterPort: 9100, reportFinalEnabled: true, reportPreliminaryEnabled: true, reportCitoEnabled: true, messageRetentionDays: 90, connectorLogRetentionDays: 30 };
const analyzers = [
  { id: 'an-1', code: 'SYSMEX_XN', name: 'Sysmex XN-1000', analyzerTypeId: 1, connectorId: 'con-1', connectionMode: 'TCP', tcpHost: '192.168.1.101', tcpPort: 5100, isTcpServer: true, isOnline: true, lastMessageAt: iso(3), parameterMap: [{ analyzerCode: 'WBC', testCode: 'WBC', factor: 1, offset: 0 }, { analyzerCode: 'HGB', testCode: 'HGB', factor: 1, offset: 0 }], isActive: true },
  { id: 'an-2', code: 'COBAS_E411', name: 'Roche Cobas e411', analyzerTypeId: 2, connectorId: 'con-1', connectionMode: 'TCP', tcpHost: '192.168.1.102', tcpPort: 5200, isOnline: true, lastMessageAt: iso(12), parameterMap: [], isActive: true },
  { id: 'an-3', code: 'BS240', name: 'Mindray BS-240', analyzerTypeId: 3, connectorId: 'con-2', connectionMode: 'COM', comPort: 'COM3', baudRate: 9600, parity: 'None', dataBits: 8, stopBits: 'One', isOnline: false, lastMessageAt: iso(240), lastError: 'Таймаут ACK', parameterMap: [], isActive: true }
];
const connectors = [
  { id: 'con-1', name: 'Лаб ПК 1 — гематологія/імунохімія', hostName: 'LAB-PC-01', version: '4.0.0', status: 'ACTIVE', lastHeartbeatAt: iso(1), bufferedCount: 0 },
  { id: 'con-2', name: 'Лаб ПК 2 — біохімія', hostName: 'LAB-PC-02', version: '4.0.0', status: 'OFFLINE', lastHeartbeatAt: iso(180), bufferedCount: 14 }
];
const patients = [
  { id: 'pat-1', lastName: 'Коваленко', firstName: 'Олена', middleName: 'Петрівна', lastNameLatin: 'Kovalenko', firstNameLatin: 'Olena', birthDate: '1985-04-12', gender: 'F', phone: '+380671112233' },
  { id: 'pat-2', lastName: 'Мельник', firstName: 'Юрій', middleName: 'Володимирович', lastNameLatin: 'Melnyk', firstNameLatin: 'Yurii', birthDate: '1962-11-03', gender: 'M', phone: '+380509998877' },
  { id: 'pat-3', lastName: 'Ткаченко', firstName: 'Марія', middleName: 'Ігорівна', lastNameLatin: 'Tkachenko', firstNameLatin: 'Mariia', birthDate: '2019-06-21', gender: 'F' }
];
const tests = [
  { id: 't-glu', code: 'GLU', name: 'Глюкоза сироватки', unit: 'ммоль/л', decimalPlaces: 2, resultType: 'NUMERIC', category: 'Біохімія', biomaterialTypeId: 1, methodId: 1, deltaCheckMaxPct: 40, deltaCheckHours: 72, isQcTracked: true, isActive: true, price: 120 },
  { id: 't-alt', code: 'ALT', name: 'Аланінамінотрансфераза (АЛТ)', unit: 'Од/л', decimalPlaces: 1, resultType: 'NUMERIC', category: 'Біохімія', biomaterialTypeId: 1, methodId: 2, deltaCheckMaxPct: 40, isActive: true, price: 110 },
  { id: 't-crea', code: 'CREA', name: 'Креатинін сироватки', unit: 'мкмоль/л', decimalPlaces: 0, resultType: 'NUMERIC', category: 'Біохімія', biomaterialTypeId: 1, methodId: 2, isActive: true, price: 110 },
  { id: 't-wbc', code: 'WBC', name: 'Лейкоцити', unit: '10*9/л', decimalPlaces: 2, resultType: 'NUMERIC', category: 'Гематологія', biomaterialTypeId: 2, methodId: 3, isQcTracked: true, isActive: true, price: 60 },
  { id: 't-hgb', code: 'HGB', name: 'Гемоглобін', unit: 'г/л', decimalPlaces: 0, resultType: 'NUMERIC', category: 'Гематологія', biomaterialTypeId: 2, methodId: 3, isQcTracked: true, isActive: true, price: 60 },
  { id: 't-tsh', code: 'TSH', name: 'Тиреотропний гормон (ТТГ)', unit: 'мкМО/мл', decimalPlaces: 2, resultType: 'NUMERIC', category: 'Гормони', biomaterialTypeId: 1, methodId: 4, requiresManualVerification: true, isActive: true, price: 210 },
  { id: 't-hbsag', code: 'HBSAG', name: 'HBsAg (якісно)', unit: '', decimalPlaces: 0, resultType: 'DROPDOWN', dropdownOptions: ['Негативний', 'Позитивний', 'Сумнівний'], category: 'Імунологія', biomaterialTypeId: 1, methodId: 4, isActive: true, price: 180 },
  { id: 't-hist', code: 'HISTO', name: 'Патогістологічне дослідження біоптату', unit: '', resultType: 'REPORT', category: 'Патогістологія', biomaterialTypeId: 4, isActive: true, price: 950 }
];
const profiles = [
  { id: 'p-cbc', code: 'CBC', name: 'Загальний аналіз крові (ЗАК)', category: 'Гематологія', turnaroundHours: 4, fastingRequired: false, price: 180, isActive: true, misServiceId: 'MIS-1001', items: [{ testId: 't-wbc', displayOrder: 1, isRequired: true }, { testId: 't-hgb', displayOrder: 2, isRequired: true }] },
  { id: 'p-bio', code: 'BIO', name: 'Біохімія базова', category: 'Біохімія', turnaroundHours: 24, fastingRequired: true, price: 320, isActive: true, misServiceId: 'MIS-1002', items: [{ testId: 't-glu', displayOrder: 1, isRequired: true }, { testId: 't-alt', displayOrder: 2, isRequired: true }, { testId: 't-crea', displayOrder: 3, isRequired: true }] },
  { id: 'p-thy', code: 'THY', name: 'Щитоподібна залоза (ТТГ)', category: 'Гормони', turnaroundHours: 24, fastingRequired: false, price: 210, isActive: true, items: [{ testId: 't-tsh', displayOrder: 1, isRequired: true }] }
];
const biomaterials = [
  { id: 1, code: 'SERUM', name: 'Сироватка венозної крові', defaultContainer: 'SST', stabilityHours: 48, temperatureRegime: '+2…+8 °C', isActive: true },
  { id: 2, code: 'EDTA', name: 'Венозна кров (ЕДТА)', defaultContainer: 'EDTA', stabilityHours: 24, temperatureRegime: '+18…+25 °C', isActive: true },
  { id: 3, code: 'URINE', name: 'Сеча ранкова', defaultContainer: 'URINE', stabilityHours: 4, temperatureRegime: '+2…+8 °C', isActive: true },
  { id: 4, code: 'TISSUE', name: 'Біоптат тканини (формалін)', defaultContainer: 'FORMALIN', stabilityHours: 720, temperatureRegime: '+18…+25 °C', isActive: true }
];
const tubes = [
  { id: 1, code: 'SST', name: 'Сироватка з гелем (SST)', colorCode: '#f2c037', anticoagulant: 'Гель-активатор згортання', volumeMl: 5, orderOfDrawIndex: 3, inversionsCount: 5, isActive: true },
  { id: 2, code: 'EDTA', name: 'K2-ЕДТА', colorCode: '#7c3aed', anticoagulant: 'K2-ЕДТА', volumeMl: 2, orderOfDrawIndex: 5, inversionsCount: 8, isActive: true },
  { id: 3, code: 'CITR', name: 'Na-цитрат 3,2%', colorCode: '#0178BC', anticoagulant: 'Na-цитрат 3,2%', volumeMl: 2.7, orderOfDrawIndex: 2, inversionsCount: 4, isActive: true },
  { id: 4, code: 'URINE', name: 'Контейнер для сечі', colorCode: '#e0e0e0', volumeMl: 50, orderOfDrawIndex: 9, inversionsCount: 0, isActive: true }
];
const methods = [{ id: 1, code: 'HK', name: 'Гексокіназний (IFCC)', isActive: true }, { id: 2, code: 'KIN', name: 'Кінетичний UV', isActive: true }, { id: 3, code: 'SLS', name: 'SLS-гемоглобін / проточна цитометрія', isActive: true }, { id: 4, code: 'ECLIA', name: 'Електрохемілюмінесценція (ECLIA)', isActive: true }];
const analyzerTypes = [{ id: 1, code: 'SYSMEXXN', name: 'Sysmex XN', manufacturer: 'Sysmex', category: 'HEMATOLOGY', exchType: 'ASTM', controlSum: true, sleepMs: 100, orderTemplate: 'SYSMEX_XN', isActive: true }, { id: 2, code: 'COBASE411', name: 'Cobas e411', manufacturer: 'Roche', category: 'IMMUNO', exchType: 'ASTM', controlSum: true, sleepMs: 100, orderTemplate: 'COBAS', isActive: true }, { id: 3, code: 'BS240', name: 'Mindray BS-240', manufacturer: 'Mindray', category: 'BIOCHEM', exchType: 'HL7', controlSum: false, sleepMs: 50, orderTemplate: 'MINDRAY_HL7', isActive: true }];
const departments = [{ id: 1, code: 'KDL', name: 'КДЛ (лабораторія)', type: 'Лабораторія', isActive: true }, { id: 2, code: 'TER', name: 'Терапевтичне відділення', type: 'Стаціонар', isActive: true }, { id: 3, code: 'PZ1', name: 'Пункт забору №1', type: 'Пункт забору', isActive: true }];
const sections = [{ id: 'sec-bio', code: 'BIO', name: 'Біохімія', type: 'Біохімія', departmentId: 1, journalMask: '{yyyy}-{seq6}', resetPeriod: 'YEARLY', autoRelease: true, workflowTemplate: 'ALIQUOT', isActive: true }, { id: 'sec-hem', code: 'HEM', name: 'Гематологія', type: 'Гематологія', departmentId: 1, journalMask: '{yy}{MM}{dd}/{dayseq3}', resetPeriod: 'DAILY', autoRelease: true, workflowTemplate: 'STANDARD', isActive: true }, { id: 'sec-pat', code: 'PAT', name: 'Патогістологія', type: 'Патогістологія', departmentId: 1, journalMask: 'S{yy}-{seq5}', resetPeriod: 'YEARLY', autoRelease: false, workflowTemplate: 'HISTOLOGY', isActive: true }];

const orderIds = ids(6);
const orders = orderIds.map((id, i) => {
  const p = patients[i % patients.length];
  const statuses = ['NEW', 'COLLECTED', 'RECEIVED', 'IN_PROGRESS', 'COMPLETED', 'RELEASED'];
  const status = statuses[i];
  const bc1 = `1026004${8 + i}`;
  return {
    id, orderNumber: `2610-00${4812 + i}`, patientId: p.id, patient: { ...p, fullName: `${p.lastName} ${p.firstName} ${p.middleName}` }, doctorId: 'emp-doctor', departmentId: 2,
    orderDatetime: iso(60 * (i + 1)), status, isUrgentCito: i % 3 === 0, totalPrice: 500 + i * 60, clinicalNotes: i === 0 ? 'Цукровий діабет 2 типу (E11), контроль' : null,
    allowedActions: { NEW: ['COLLECT', 'CANCEL'], COLLECTED: ['RECEIVE', 'REJECT', 'CANCEL'], RECEIVED: ['ENTER_RESULT', 'REJECT', 'RERUN', 'CANCEL'], IN_PROGRESS: ['ENTER_RESULT', 'VERIFY', 'RERUN', 'REJECT', 'CANCEL'], COMPLETED: ['VERIFY', 'RELEASE', 'RERUN', 'REOPEN'], RELEASED: ['REOPEN'] }[status],
    samples: [
      { id: `s-${i}-1`, orderId: id, barcode: bc1, tubeTypeId: 1, biomaterialTypeId: 1, status: status === 'NEW' ? 'PENDING' : (status === 'COLLECTED' ? 'COLLECTED' : 'RECEIVED'), collectedAt: status === 'NEW' ? null : iso(50 * (i + 1)), receivedAt: ['NEW', 'COLLECTED'].includes(status) ? null : iso(40 * (i + 1)) },
      { id: `s-${i}-2`, orderId: id, barcode: `1026005${8 + i}`, tubeTypeId: 2, biomaterialTypeId: 2, status: status === 'NEW' ? 'PENDING' : 'RECEIVED', collectedAt: status === 'NEW' ? null : iso(50 * (i + 1)) }
    ],
    tests: [
      { id: `ot-${i}-glu`, orderId: id, sampleId: `s-${i}-1`, testId: 't-glu', testCode: 'GLU', testName: 'Глюкоза сироватки', status: i >= 3 ? (i === 3 ? 'NEEDS_REVIEW' : 'VERIFIED') : 'PENDING', result: i >= 3 ? { numericValue: i === 3 ? 26.4 : 5.2, unit: 'ммоль/л', normLow: 4.1, normHigh: 5.9, critLow: 2.5, critHigh: 25, referenceDisplay: '4,1 – 5,9', flag: i === 3 ? 'CRIT_HIGH' : 'NORMAL', deltaPercent: i === 3 ? 185 : 2.1, deltaAlert: i === 3, previousValue: 9.3, previousAt: iso(60 * 24 * 10), enteredAt: iso(30) } : null },
      { id: `ot-${i}-wbc`, orderId: id, sampleId: `s-${i}-2`, testId: 't-wbc', testCode: 'WBC', testName: 'Лейкоцити', status: i >= 3 ? 'AUTO_VERIFIED' : 'PENDING', result: i >= 3 ? { numericValue: 7.45, unit: '10*9/л', normLow: 4, normHigh: 9, referenceDisplay: '4 – 9', flag: 'NORMAL', deltaPercent: 1.2, isAutoVerified: true, enteredAt: iso(28) } : null }
    ]
  };
});

const worklist = [];
orders.forEach((o, i) => o.tests.forEach(t => {
  const r = t.result || {};
  worklist.push({ orderTestId: t.id, resultId: t.result ? `r-${t.id}` : null, orderId: o.id, orderNumber: o.orderNumber, barcode: (o.samples.find(s => s.id === t.sampleId) || {}).barcode, patientId: o.patientId, patientName: o.patient.fullName, patientAgeGender: `${o.patient.gender === 'F' ? 'Ж' : 'Ч'}, ${now.getFullYear() - Number(o.patient.birthDate.slice(0, 4))} р.`, testCode: t.testCode, testName: t.testName, value: r.numericValue, unit: r.unit || (t.testCode === 'GLU' ? 'ммоль/л' : '10*9/л'), normLow: r.normLow !== undefined ? r.normLow : (t.testCode === 'GLU' ? 4.1 : 4), normHigh: r.normHigh !== undefined ? r.normHigh : (t.testCode === 'GLU' ? 5.9 : 9), critLow: r.critLow, critHigh: r.critHigh, referenceDisplay: r.referenceDisplay, flag: r.flag || 'NONE', deltaPercent: r.deltaPercent, deltaAlert: !!r.deltaAlert, previousValue: r.previousValue, previousAt: r.previousAt, status: t.status, analyzerName: t.testCode === 'WBC' ? 'Sysmex XN-1000' : 'Roche Cobas e411', analyzerId: t.testCode === 'WBC' ? 'an-1' : 'an-2', isCito: o.isUrgentCito, isAutoVerified: !!r.isAutoVerified, isLockedOut: t.testCode === 'WBC' && i === 4, enteredAt: r.enteredAt, verifiedAt: t.status === 'VERIFIED' ? iso(10) : null, sectionName: t.testCode === 'WBC' ? 'Гематологія' : 'Біохімія', journalNumber: t.testCode === 'WBC' ? `261007/00${i + 1}` : `2026-00012${i}`, allowedActions: o.allowedActions });
}));

const ljPoints = Array.from({ length: 20 }, (_, i) => {
  const z = [0.2, -0.5, 1.1, -0.3, 0.8, 2.3, -1.2, 0.4, 0.1, -0.8, 1.5, 0.6, -0.2, 0.9, 3.6, 0.3, -0.6, 0.2, 1.0, -0.4][i];
  const rules = z > 3 ? ['1_3s'] : (Math.abs(z) > 2 ? ['1_2s'] : []);
  return { id: `qc-${i}`, at: iso(60 * 24 * (20 - i)), value: Number((7.2 + 0.3 * z).toFixed(2)), z, rules, status: z > 3 ? 'LOCKOUT' : (Math.abs(z) > 2 ? 'WARNING' : 'OK'), isWarning: Math.abs(z) > 2 && z <= 3, isRejection: z > 3, lockoutEnforced: z > 3 };
});
const qcMaterials = [{ id: 'qm-1', analyzerId: 'an-1', name: 'XN-CHECK Level 2', level: 'LEVEL_2_NORMAL', lotNumber: 'LOT-XN-2026-L2', manufacturer: 'Sysmex', expiryDate: '2027-03-01', isActive: true, targets: [{ testCode: 'WBC', targetMean: 7.2, targetSd: 0.3, unit: '10*9/л' }, { testCode: 'HGB', targetMean: 142, targetSd: 2.5, unit: 'г/л' }] }, { id: 'qm-2', analyzerId: 'an-2', name: 'PreciControl ClinChem Multi 1', level: 'LEVEL_1_LOW', lotNumber: 'PCCC1-2026', manufacturer: 'Roche', expiryDate: '2026-12-31', isActive: true, targets: [{ testCode: 'GLU', targetMean: 5.1, targetSd: 0.15, unit: 'ммоль/л' }] }];
const lockouts = [{ id: 'lk-1', analyzerId: 'an-1', testCode: 'WBC', reason: 'Порушення правила Вестгарда 1₃s (z = +3,6)', startedAt: iso(120), resolvedAt: null }];
const racks = [{ id: 'rk-1', code: 'BOX-04', name: 'Кріобокс #BOX-04', location: 'ULT-01 / секція B / штатив #4', temperatureRegime: '-80', rows: 8, cols: 12, occupiedCount: 58 }, { id: 'rk-2', code: 'BOX-05', name: 'Кріобокс #BOX-05', location: 'ULT-01 / секція B / штатив #5', temperatureRegime: '-80', rows: 8, cols: 12, occupiedCount: 12 }];
const cells = []; const bioNames = ['Сироватка', 'Плазма', 'ЕДТА кров', 'Сеча'];
for (let r = 1; r <= 8; r++) for (let c = 1; c <= 12; c++) { if ((r * 7 + c * 3) % 5 === 0) continue; cells.push({ id: `cell-${r}-${c}`, rackId: 'rk-1', row: String.fromCharCode(64 + r), col: c, barcode: `1026${String(1000 + r * 12 + c)}`, biomaterialName: bioNames[(r + c) % 4], patientName: patients[(r + c) % 3].lastName, placedAt: iso(60 * 24 * ((r + c) % 20)), expiryAt: new Date(now.getTime() + 86400000 * (((r * c) % 60) - 5)).toISOString() }); }

const routes = [
  ['GET', /^\/health$/, () => ({ status: 'Healthy', db: 'ok', version: '4.0.0-mock' })],
  ['GET', /^\/context\/me$/, (q, h) => ({ employee: employees.find(e => e.id === h['x-medlink-employee-id']) || employees[1], lab })],
  ['GET', /^\/context\/employees$/, () => employees],
  ['GET', /^\/dictionaries\/order-matrix\/favorites$/, () => ['PROFILE:p-cbc', 'TEST:t-glu']],
  ['GET', /^\/dictionaries\/order-matrix$/, () => { const e = []; return e; }, 404],
  ['GET', /^\/dictionaries\/tests\/[^/]+\/profiles$/, () => profiles.slice(0, 1)],
  ['GET', /^\/dictionaries\/(biomaterials|tube-types|method-types|analyzer-types|tests|profiles|reflex-rules|organisms|antibiotics|eucast-breakpoints|departments|employees)$/, (q, h, m) => ({ biomaterials, 'tube-types': tubes, 'method-types': methods, 'analyzer-types': analyzerTypes, tests, profiles, 'reflex-rules': [{ id: 'rf-1', triggerTestCode: 'TSH', conditionOperator: '>', thresholdValue: 4, reflexTestCode: 'FT4', autoApprove: true, requiresSameSample: true, description: 'ТТГ > 4 → вільний Т4', isActive: true }], organisms: [{ id: 1, code: 'ECOLI', name: 'Escherichia coli', genus: 'Escherichia', gramStain: 'Грам−', isActive: true }, { id: 2, code: 'SAUR', name: 'Staphylococcus aureus', genus: 'Staphylococcus', gramStain: 'Грам+', isActive: true }], antibiotics: [{ id: 1, code: 'AMP', name: 'Ампіцилін', antibioticClass: 'Пеніциліни', diskContent: '10', isActive: true }, { id: 2, code: 'CIP', name: 'Ципрофлоксацин', antibioticClass: 'Фторхінолони', diskContent: '5', isActive: true }], 'eucast-breakpoints': [], departments, employees })[m[1]]],
  ['GET', /^\/norms\/combinations$/, q => [{ id: 'nl-1', testCode: q.testCode || 'GLU', layerType: 'BASELINE', priorityOrder: 10, normName: 'Дорослі', gender: 'ANY', ageUnit: 'YEARS', ageFrom: 18, ageTo: 120, normLow: 4.1, normHigh: 5.9, critLow: 2.5, critHigh: 25, unit: 'ммоль/л', isActive: true }, { id: 'nl-2', testCode: q.testCode || 'GLU', layerType: 'PREGNANCY', priorityOrder: 100, normName: 'Вагітність', gender: 'F', ageUnit: 'YEARS', pregnancyWeekFrom: 1, pregnancyWeekTo: 42, normLow: 3.3, normHigh: 5.1, critLow: 2.5, critHigh: 25, unit: 'ммоль/л', isActive: true }]],
  ['GET', /^\/norms\/service-card\/[^/]+$/, () => ({})],
  ['POST', /^\/norms\/resolve-cascade$/, (q, h, m, body) => ({ winningLayer: { id: 'nl-1', layerType: 'BASELINE', priorityOrder: 10, normName: 'Дорослі' }, normLow: 4.1, normHigh: 5.9, critLow: 2.5, critHigh: 25, unit: 'ммоль/л', statusFlag: body.measuredValue > 5.9 ? 'HIGH' : 'NORMAL', isPanicCito: false, isDeltaAlert: false, deltaPercent: 0, auditTrace: [{ layerType: 'PREGNANCY', priorityOrder: 100, normName: 'Вагітність', matched: false, reason: 'isPregnant = false' }, { layerType: 'BASELINE', priorityOrder: 10, normName: 'Дорослі', matched: true, reason: 'базовий шар завжди співпадає' }] })],
  ['GET', /^\/patients\/([^/]+)\/trend\/([^/]+)$/, () => [{ at: iso(60 * 24 * 90), value: 5.1, normLow: 4.1, normHigh: 5.9, flag: 'NORMAL' }, { at: iso(60 * 24 * 60), value: 6.4, normLow: 4.1, normHigh: 5.9, flag: 'HIGH' }, { at: iso(60 * 24 * 30), value: 9.3, normLow: 4.1, normHigh: 5.9, flag: 'HIGH' }, { at: iso(30), value: 5.2, normLow: 4.1, normHigh: 5.9, flag: 'NORMAL' }]],
  ['GET', /^\/patients\/([^/]+)$/, (q, h, m) => patients.find(p => p.id === m[1]) || patients[0]],
  ['GET', /^\/patients$/, q => patients.filter(p => !q.search || `${p.lastName} ${p.firstName} ${p.phone}`.toLowerCase().includes(q.search.toLowerCase()))],
  ['GET', /^\/orders\/by-barcode\/([^/]+)$/, (q, h, m) => orders.find(o => o.samples.some(s => s.barcode === m[1])) || null, 404],
  ['GET', /^\/orders\/([^/]+)\/transitions$/, (q, h, m) => ({ allowedActions: (orders.find(o => o.id === m[1]) || {}).allowedActions || [] })],
  ['GET', /^\/orders\/([^/]+)\/journal-entries$/, (q, h, m) => [{ id: 'je-1', journalNumber: '2026-000123', dayNumber: 7, sectionId: 'sec-bio', sectionName: 'Біохімія', registeredAt: iso(40), barcode: (orders.find(o => o.id === m[1]) || orders[0]).samples[0].barcode, testCodes: ['GLU'], status: 'IN_ANALYSIS' }]],
  ['GET', /^\/orders\/([^/]+)\/labels$/, (q, h, m) => (orders.find(o => o.id === m[1]) || orders[0]).samples.map(s => ({ barcode: s.barcode, zpl: `^XA^FO20,20^BY2^BCN,60,Y,N,N^FD${s.barcode}^FS^XZ`, svg: '' }))],
  ['GET', /^\/orders\/([^/]+)\/report$/, (q, h, m) => `<html><body style="font-family:sans-serif;padding:24px"><h2>Бланк результатів ${(orders.find(o => o.id === m[1]) || orders[0]).orderNumber}${q.variant === 'preliminary' ? ' — ПОПЕРЕДНІЙ' : ''}</h2><p>mock</p></body></html>`, 200, 'text/html'],
  ['GET', /^\/orders\/([^/]+)$/, (q, h, m) => orders.find(o => o.id === m[1]) || null, 404],
  ['GET', /^\/orders$/, q => { let list = orders; if (q.cito === 'true') list = list.filter(o => o.isUrgentCito); if (q.status) list = list.filter(o => q.status.split(',').includes(o.status)); return { items: list, total: list.length, page: 1, pageSize: 50 }; }],
  ['GET', /^\/samples\/([^/]+)\/label$/, (q, h, m) => ({ barcode: m[1], zpl: `^XA^FO20,20^BY2^BCN,60,Y,N,N^FD${m[1]}^FS^XZ`, svg: '', patientName: 'Коваленко Олена Петрівна', tube: 'Сироватка з гелем (SST)', collectedAt: iso(5) })],
  ['GET', /^\/samples\/([^/]+)\/tree$/, (q, h, m) => ({ root: { barcode: m[1], status: 'RECEIVED', orderId: orders[2].id, orderNumber: orders[2].orderNumber, patientName: orders[2].patient.fullName, children: [{ barcode: m[1] + '-A1', status: 'PROCESSING', derivationType: 'ALIQUOT', volumeMl: 0.5, sectionName: 'Біохімія', children: [] }, { barcode: m[1] + '-A2', status: 'STORED', derivationType: 'ALIQUOT', volumeMl: 0.5, sectionName: 'Імунохімія', children: [] }] } })],
  ['GET', /^\/samples\/([^/]+)\/stages$/, () => ({ workflowTemplate: 'ALIQUOT', stages: [{ code: 'RECEIVE', name: 'Прийом', completedAt: iso(40), fields: [] }, { code: 'CENTRIFUGE', name: 'Центрифугування', completedAt: iso(30), temperature: 20, instrument: 'Центрифуга Eppendorf 5702', fields: ['temperature', 'instrument'] }, { code: 'ALIQUOT', name: 'Аліквотування', fields: ['instrument'] }, { code: 'ANALYSIS', name: 'Аналіз', fields: [] }, { code: 'ARCHIVE', name: 'Архів', fields: ['temperature'] }] })],
  ['GET', /^\/samples$/, q => { const list = []; orders.forEach(o => o.samples.forEach(s => list.push({ ...s, orderNumber: o.orderNumber, patientName: o.patient.fullName, tubeName: (tubes.find(t => t.id === s.tubeTypeId) || {}).name }))); return list.filter(s => !q.status || s.status === q.status); }],
  ['GET', /^\/logistics\/manifests$/, () => [{ id: 'mf-1', manifestNumber: 'MF-2026-0012', originDepartmentId: 3, destinationDepartmentId: 1, courierName: 'Петренко А.М.', courierPhone: '+380 67 555 55 55', temperatureDispatch: 4, temperatureReceipt: 6.2, status: 'RECEIVED', dispatchedAt: iso(300), receivedAt: iso(200), barcodes: ['10260048', '10260058'] }, { id: 'mf-2', manifestNumber: 'MF-2026-0013', originDepartmentId: 3, destinationDepartmentId: 1, courierName: 'Петренко А.М.', courierPhone: '+380 67 555 55 55', temperatureDispatch: 4, temperatureReceipt: 11.5, isColdChainViolated: true, status: 'RECEIVED', dispatchedAt: iso(120), receivedAt: iso(30), barcodes: ['10260049'] }, { id: 'mf-3', manifestNumber: 'MF-2026-0014', originDepartmentId: 3, destinationDepartmentId: 1, courierName: 'Іваненко С.', temperatureDispatch: 5, status: 'DISPATCHED', dispatchedAt: iso(20), barcodes: ['10260050', '10260060'] }]],
  ['GET', /^\/worklist\/summary$/, () => ({ pending: worklist.filter(w => w.status === 'PENDING').length, needsReview: worklist.filter(w => w.status === 'NEEDS_REVIEW').length, panic: 1, cito: orders.filter(o => o.isUrgentCito).length, autoVerifiedToday: 37 })],
  ['GET', /^\/worklist\/batches$/, () => [{ id: 'b-1', batchCode: 'B-2026-0007', analyzerName: 'Roche Cobas e411', status: 'OPEN', items: ['ot-2-glu', 'ot-3-glu'] }]],
  ['GET', /^\/worklist$/, q => { let list = worklist; if (q.status) list = list.filter(w => q.status.split(',').includes(w.status)); if (q.flag) list = list.filter(w => w.flag === q.flag); if (q.search) list = list.filter(w => JSON.stringify(w).toLowerCase().includes(q.search.toLowerCase())); return { items: list, total: list.length }; }],
  ['GET', /^\/results\/([^/]+)\/history$/, () => [{ version: 1, numericValue: 26.4, flag: 'CRIT_HIGH', enteredAt: iso(30), enteredByName: 'Коваль О.І.' }]],
  ['GET', /^\/panic\/pending$/, () => worklist.filter(w => w.flag === 'CRIT_HIGH').map(w => ({ ...w, resultId: w.resultId || 'r-1' }))],
  ['GET', /^\/panic-calls$/, () => [{ id: 'pc-1', patientName: 'Мельник Ю.В.', testCode: 'K', value: 6.8, doctorNotifiedName: 'Д-р Романенко', phone: '+380 50 111 22 33', department: 'Терапевтичне відділення', readbackConfirmed: true, notifiedAt: iso(60 * 26), notifiedByName: 'Мельник В.С.' }]],
  ['GET', /^\/qc\/materials$/, () => qcMaterials],
  ['GET', /^\/qc\/levey-jennings$/, q => ({ targetMean: q.testCode === 'GLU' ? 5.1 : 7.2, targetSd: q.testCode === 'GLU' ? 0.15 : 0.3, cvPct: 4.1, n: 20, mean: 7.26, sd: 0.31, bias: 0.8, currentStatus: 'LOCKOUT', points: ljPoints })],
  ['GET', /^\/qc\/lockouts$/, () => lockouts],
  ['GET', /^\/qc\/report$/, () => ({ items: [{ analyzer: 'Sysmex XN-1000', testCode: 'WBC', n: 20, mean: 7.26, sd: 0.31, cvPct: 4.1, bias: 0.8, violations: 2 }] })],
  ['GET', /^\/analyzers\/([^/]+)\/messages$/, () => ({ items: [{ id: 'm-1', analyzerId: 'an-1', direction: 'IN', protocol: 'ASTM', rawText: '\x05\x021H|\\^&|||Sysmex XN-1000|||||||P|1394-97|20261007081500\rP|1\rR|1|^^^^WBC|7.45|10*9/L|4.0^9.0|N||F\rL|1|N\r\x0317\r\n\x04', parsedOk: true, receivedAt: iso(3), resultsCount: 1 }, { id: 'm-2', analyzerId: 'an-1', direction: 'OUT', protocol: 'ASTM', rawText: '\x06', parsedOk: true, receivedAt: iso(3), resultsCount: 0 }], total: 2 })],
  ['POST', /^\/analyzers\/([^/]+)\/simulate$/, () => ({ kind: 'Results', parsedResults: [{ barcode: '10260048', analyzerCode: 'WBC', testCode: 'WBC', value: '7.45', unit: '10*9/L', flags: 'N' }], warnings: [] })],
  ['GET', /^\/analyzers$/, () => analyzers],
  ['GET', /^\/connectors\/([^/]+)\/logs$/, () => ({ items: [{ id: 'l-1', at: iso(2), level: 'INFO', message: 'Heartbeat OK, buffered=0' }] })],
  ['GET', /^\/connectors\/([^/]+)$/, (q, h, m) => connectors.find(c => c.id === m[1])],
  ['GET', /^\/connectors$/, () => connectors],
  ['POST', /^\/connectors$/, (q, h, m, body) => ({ id: 'con-new', name: body.name, installKey: 'MLK-7F3A-9C21-B8E4', setupCommand: `MedLink.LabConnector setup --server http://localhost:5055 --install-key MLK-7F3A-9C21-B8E4 --name "${body.name}"` })],
  ['GET', /^\/biobank\/racks\/([^/]+)\/cells$/, (q, h, m) => cells.filter(c => c.rackId === m[1])],
  ['GET', /^\/biobank\/racks$/, () => racks],
  ['GET', /^\/biobank\/search$/, q => cells.find(c => c.barcode === q.barcode) || null, 404],
  ['GET', /^\/reagents\/lots$/, () => [{ id: 'rl-1', name: 'Glucose HK Gen.3', lotNumber: 'GLU-2026-07', manufacturer: 'Roche', analyzerId: 'an-2', testCodes: ['GLU'], initialTests: 500, remainingTests: 48, minimumTests: 60, expiryDate: '2026-12-31', openedAt: '2026-09-20' }, { id: 'rl-2', name: 'Cellpack DCL', lotNumber: 'CP-2026-11', manufacturer: 'Sysmex', analyzerId: 'an-1', testCodes: ['WBC', 'HGB'], initialTests: 2000, remainingTests: 1540, minimumTests: 300, expiryDate: '2026-10-20' }, { id: 'rl-3', name: 'TSH Elecsys', lotNumber: 'TSH-2025-02', manufacturer: 'Roche', analyzerId: 'an-2', testCodes: ['TSH'], initialTests: 100, remainingTests: 20, minimumTests: 10, expiryDate: '2026-09-01' }]],
  ['GET', /^\/reagents\/alerts$/, () => [{ kind: 'LOW', title: 'Glucose HK Gen.3 (GLU-2026-07)', remainingTests: 48, minimumTests: 60 }, { kind: 'EXPIRED', title: 'TSH Elecsys (TSH-2025-02)', message: 'термін придатності вичерпано 01.09.2026' }]],
  ['GET', /^\/microbiology\/cultures\/([^/]+)$/, () => ({ id: 'cu-1', cultureNumber: 'MB-2026-0031', patientName: 'Ткаченко Марія Ігорівна', barcode: '10260071', specimenType: 'Сеча', status: 'IDENTIFIED', hasGrowth: true, growthDescription: '10^5 КУО/мл', incubationStartedAt: iso(60 * 48), isolates: [{ id: 'iso-1', organismId: 1, organismName: 'Escherichia coli', cfuPerMl: '10^5', gramStain: 'Грам−', phenotypes: ['ESBL'], susceptibility: [{ id: 'su-1', antibioticId: 1, antibioticName: 'Ампіцилін', method: 'DISK', zoneMm: 9, interpretation: 'R', breakpointS: 14, breakpointR: 14 }, { id: 'su-2', antibioticId: 2, antibioticName: 'Ципрофлоксацин', method: 'MIC', mic: 0.25, interpretation: 'S', breakpointS: 0.25, breakpointR: 0.5 }] }] })],
  ['GET', /^\/microbiology\/cultures$/, () => [{ id: 'cu-1', cultureNumber: 'MB-2026-0031', patientName: 'Ткаченко Марія Ігорівна', barcode: '10260071', specimenType: 'Сеча', status: 'IDENTIFIED', hasGrowth: true, registeredAt: iso(60 * 50), isolates: [{ id: 'iso-1', organismName: 'Escherichia coli', phenotypes: ['ESBL'] }] }]],
  ['GET', /^\/analytics\/tat$/, () => ({ medianMin: 184, p90Min: 412, slaViolations: 3, ordersCount: 128, stages: [{ name: 'order→collected', medianMin: 22 }, { name: 'collected→received', medianMin: 48 }, { name: 'received→resulted', medianMin: 76 }, { name: 'resulted→verified', medianMin: 25 }, { name: 'verified→released', medianMin: 13 }], byProfile: [{ profileId: 'p-cbc', profileName: 'ЗАК', count: 62, medianMin: 95, p90Min: 180, slaViolations: 0 }, { profileId: 'p-bio', profileName: 'Біохімія базова', count: 41, medianMin: 260, p90Min: 520, slaViolations: 2 }, { profileId: 'p-thy', profileName: 'ТТГ', count: 25, medianMin: 310, p90Min: 640, slaViolations: 1 }] })],
  ['GET', /^\/analytics\/volume$/, () => Array.from({ length: 14 }, (_, i) => ({ label: new Date(now.getTime() - 86400000 * (13 - i)).toISOString().slice(0, 10), count: 20 + ((i * 37) % 45) }))],
  ['GET', /^\/audit$/, () => ({ items: [{ id: 'a-1', at: iso(30), userId: 'emp-tech', action: 'RESULT', entity: 'LabTestResult', entityId: 'r-1', before: null, after: { numericValue: 26.4, flag: 'CRIT_HIGH' }, ip: '10.0.0.12' }, { id: 'a-2', at: iso(60), userId: 'emp-nurse', action: 'COLLECT', entity: 'LabOrder', entityId: orders[3].id, before: { status: 'NEW' }, after: { status: 'COLLECTED' }, ip: '10.0.0.21' }], total: 2 })],
  ['GET', /^\/settings\/lab$/, () => lab],
  ['GET', /^\/settings\/numerators$/, () => ({ orderNumber: 4818, tubeBarcode: 60, batch: 7 })],
  ['GET', /^\/sections\/([^/]+)\/journal$/, (q, h, m) => ({ entries: orders.slice(0, 4).map((o, i) => ({ id: `je-${i}`, journalNumber: m[1] === 'sec-hem' ? `261007/00${i + 1}` : `2026-00012${i}`, dayNumber: i + 1, registeredAt: iso(50 * (i + 1)), orderId: o.id, orderNumber: o.orderNumber, patientName: o.patient.fullName, barcode: o.samples[0].barcode, testCodes: o.tests.map(t => t.testCode), status: o.status })) })],
  ['GET', /^\/sections$/, () => sections],
  ['POST', /^\/sections\/([^/]+)\/journal\/renumber-preview$/, () => ({ preview: '2026-000124', affected: 0 })],
  ['GET', /^\/portal\/([^/]+)\/orders\/([^/]+)$/, (q, h, m) => { const o = orders.find(x => x.id === m[2]) || orders[4]; return { ...o, totalTests: o.tests.length, releasedTests: o.tests.filter(t => ['VERIFIED', 'AUTO_VERIFIED'].includes(t.status)).length, pendingTests: o.tests.filter(t => !['VERIFIED', 'AUTO_VERIFIED'].includes(t.status)).length }; }],
  ['GET', /^\/portal\/([^/]+)\/orders$/, (q, h, m) => orders.filter(o => o.patientId === m[1]).map(o => ({ ...o, totalTests: o.tests.length, releasedTests: o.tests.filter(t => ['VERIFIED', 'AUTO_VERIFIED'].includes(t.status)).length }))],
  ['GET', /^\/portal\/([^/]+)\/trend\/([^/]+)$/, () => [{ at: iso(60 * 24 * 90), value: 5.1, normLow: 4.1, normHigh: 5.9, flag: 'NORMAL' }, { at: iso(60 * 24 * 60), value: 6.4, normLow: 4.1, normHigh: 5.9, flag: 'HIGH' }, { at: iso(60 * 24 * 30), value: 9.3, normLow: 4.1, normHigh: 5.9, flag: 'HIGH' }, { at: iso(30), value: 5.2, normLow: 4.1, normHigh: 5.9, flag: 'NORMAL' }]],
  ['GET', /^\/portal\/([^/]+)\/notifications$/, () => [{ id: 'n-1', channel: 'SMS', payload: { text: 'Готові результати замовлення 2610-004817 (2 з 2 показників). Переглянути: medlink.ua/portal' }, sentAt: iso(15), status: 'SENT' }, { id: 'n-2', channel: 'EMAIL', payload: { subject: 'Частково готові результати 2610-004816 (1 з 2)' }, sentAt: iso(200), status: 'DELIVERED' }]],
  ['GET', /^\/verify\/([^/]+)$/, () => ({ orderNumber: '2610-004817', releasedAt: iso(120), lab: lab.name, valid: true })]
];

http.createServer((req, res) => {
  const u = url.parse(req.url, true);
  const path = u.pathname.replace(/^\/api\/v1\/lab/, '');
  let body = '';
  req.on('data', d => { body += d; });
  req.on('end', () => {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Headers', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET,POST,PUT,DELETE,OPTIONS');
    if (req.method === 'OPTIONS') { res.writeHead(204); res.end(); return; }
    let parsed = {};
    try { parsed = body ? JSON.parse(body) : {}; } catch (e) { parsed = {}; }
    const route = routes.find(r => r[0] === req.method && r[1].test(path));
    if (route) {
      const m = path.match(route[1]);
      const data = route[2](u.query, req.headers, m, parsed);
      if (data === null || data === undefined) { res.writeHead(route[3] || 404, { 'Content-Type': 'application/problem+json' }); res.end(JSON.stringify({ title: 'Не знайдено', status: 404, detail: `Ресурс ${path} не знайдено` })); return; }
      if (route[3] === 404 && Array.isArray(data) && !data.length) { res.writeHead(404, { 'Content-Type': 'application/problem+json' }); res.end(JSON.stringify({ title: 'Не знайдено', status: 404 })); return; }
      res.writeHead(200, { 'Content-Type': route[4] || 'application/json; charset=utf-8' });
      res.end(typeof data === 'string' ? data : JSON.stringify(data));
      return;
    }
    if (['POST', 'PUT', 'DELETE'].includes(req.method)) {
      // echo-мутація: повертає тіло з id (без збереження)
      res.writeHead(req.method === 'DELETE' ? 204 : 200, { 'Content-Type': 'application/json; charset=utf-8' });
      res.end(req.method === 'DELETE' ? '' : JSON.stringify({ id: `mock-${Date.now()}`, ...parsed, verified: 1, skipped: [], blocked: [] }));
      return;
    }
    res.writeHead(404, { 'Content-Type': 'application/problem+json' });
    res.end(JSON.stringify({ title: 'Ендпоінт відсутній у mock API', status: 404, detail: `${req.method} ${path}` }));
  });
}).listen(port, () => console.log(`MedLink LIS mock API: http://localhost:${port}/api/v1/lab`));
