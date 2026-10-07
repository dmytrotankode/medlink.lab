/**
 * MedLink LIS - Realistic Clinical Mock Data
 * Matching PostgreSQL evomis_db Schema and Clinical Dictionaries
 * Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
 */

export const mockPatients = [
  {
    id: "pat-108291",
    fullName: "Коваленко Олександр Сергійович",
    birthDate: "1985-04-12",
    age: 41,
    gender: "M",
    cardNumber: "108291",
    phone: "+380 (67) 123-45-67",
    ehealthId: "ehealth-p-55019"
  },
  {
    id: "pat-108292",
    fullName: "Мельник Юрій Володимирович",
    birthDate: "1978-08-23",
    age: 48,
    gender: "M",
    cardNumber: "108292",
    phone: "+380 (50) 987-65-43",
    ehealthId: "ehealth-p-55020"
  },
  {
    id: "pat-108293",
    fullName: "Василенко Олена Петрівна",
    birthDate: "1992-11-05",
    age: 34,
    gender: "F",
    cardNumber: "108293",
    phone: "+380 (63) 445-56-67",
    ehealthId: "ehealth-p-55021"
  },
  {
    id: "pat-108294",
    fullName: "Шевченко Іван Павлович",
    birthDate: "1965-02-18",
    age: 61,
    gender: "M",
    cardNumber: "108294",
    phone: "+380 (97) 112-23-34",
    ehealthId: "ehealth-p-55022"
  }
];

export const mockOrders = [
  {
    id: "ord-2026-004819",
    orderNumber: "1026-004819",
    patientId: "pat-108291",
    patientName: "Коваленко О.С.",
    patientAge: 41,
    patientGender: "M",
    orderingDoctor: "Дмитренко В.М. (Терапевт)",
    department: "Відділення поліклініки №1",
    createdAt: "2026-10-06 08:30",
    priority: "ROUTINE",
    status: "IN_PROGRESS",
    ehealthReferralCode: "5491-8821-9012",
    panels: ["Загальний аналіз крові розгорнутий", "Біохімічний профіль розширений", "Коагулограма"]
  },
  {
    id: "ord-2026-004812",
    orderNumber: "1026-004812",
    patientId: "pat-108292",
    patientName: "Мельник Ю.В.",
    patientAge: 48,
    patientGender: "M",
    orderingDoctor: "Савченко І.О. (Реаніматолог)",
    department: "ВРІТ (Реанімація)",
    createdAt: "2026-10-06 09:10",
    priority: "CITO",
    status: "PANIC_ALERT",
    ehealthReferralCode: "7721-3310-4491",
    panels: ["Глюкоза експрес", "Гази крові та електроліти"]
  },
  {
    id: "ord-2026-004825",
    orderNumber: "1026-004825",
    patientId: "pat-108293",
    patientName: "Василенко О.П.",
    patientAge: 34,
    patientGender: "F",
    orderingDoctor: "Ковальчук Т.С. (Уролог)",
    department: "Жіноча консультація",
    createdAt: "2026-10-04 11:20",
    priority: "ROUTINE",
    status: "VERIFIED",
    ehealthReferralCode: "8821-4401-1192",
    panels: ["Бактеріологічний посів сечі з антибіотикограмою EUCAST"]
  }
];

export const mockSamples = [
  {
    barcode: "1026004818",
    orderId: "ord-2026-004819",
    patientName: "Коваленко О.С.",
    biomaterial: "Венозна кров (плазма цитратна)",
    tubeType: "Цитрат натрію 3.2%",
    capColor: "#0284c7", // Блакитна
    capName: "Блакитна кришка",
    volume: "3.0 мл",
    orderOfDraw: 1,
    status: "COLLECTED",
    collectedAt: "2026-10-06 08:45",
    testsCount: 3
  },
  {
    barcode: "1026004819",
    orderId: "ord-2026-004819",
    patientName: "Коваленко О.С.",
    biomaterial: "Сироватка крові",
    tubeType: "Активатор згортання / Гель",
    capColor: "#ca8a04", // Жовта
    capName: "Жовта кришка",
    volume: "5.0 мл",
    orderOfDraw: 2,
    status: "IN_LAB",
    collectedAt: "2026-10-06 08:46",
    testsCount: 5
  },
  {
    barcode: "1026004820",
    orderId: "ord-2026-004819",
    patientName: "Коваленко О.С.",
    biomaterial: "Цільна венозна кров",
    tubeType: "K2/K3 ЕДТА",
    capColor: "#9333ea", // Фіолетова
    capName: "Фіолетова кришка",
    volume: "2.6 мл",
    orderOfDraw: 3,
    status: "COLLECTED",
    collectedAt: "2026-10-06 08:47",
    testsCount: 10
  }
];

export const mockWorklistResults = [
  {
    id: "res-101",
    barcode: "1026004812",
    patientName: "Мельник Ю.В.",
    analyzer: "Cobas e411",
    testCode: "GLU",
    testName: "Глюкоза сироватки",
    value: 26.4,
    unit: "ммоль/л",
    normMin: 4.1,
    normMax: 5.9,
    flag: "PANIC_HIGH",
    deltaPercent: "+185%",
    status: "PENDING_VERIFY",
    comment: "Критично високий рівень! Ризик гіперосмолярної коми."
  },
  {
    id: "res-102",
    barcode: "1026004819",
    patientName: "Коваленко О.С.",
    analyzer: "Mindray BS-240",
    testCode: "ALT",
    testName: "Аланінамінотрансфераза (АЛТ)",
    value: 68.5,
    unit: "U/L",
    normMin: 0.0,
    normMax: 41.0,
    flag: "DELTA_ALERT",
    deltaPercent: "+42.7%",
    status: "PENDING_VERIFY",
    comment: "Помірний цитоліз. Delta-чек перевищує поріг 25%."
  },
  {
    id: "res-103",
    barcode: "1026004819",
    patientName: "Коваленко О.С.",
    analyzer: "Mindray BS-240",
    testCode: "CREAT",
    testName: "Креатинін сироватки",
    value: 84.0,
    unit: "мкмоль/л",
    normMin: 62.0,
    normMax: 115.0,
    flag: "NORMAL",
    deltaPercent: "-2.1%",
    status: "AUTO_VERIFIED",
    comment: "В межах норми."
  },
  {
    id: "res-104",
    barcode: "1026004820",
    patientName: "Коваленко О.С.",
    analyzer: "Sysmex XN-1000",
    testCode: "WBC",
    testName: "Лейкоцити (WBC)",
    value: 7.45,
    unit: "10*9/л",
    normMin: 4.0,
    normMax: 9.0,
    flag: "NORMAL",
    deltaPercent: "+1.2%",
    status: "AUTO_VERIFIED",
    comment: "В межах норми."
  },
  {
    id: "res-105",
    barcode: "1026004820",
    patientName: "Коваленко О.С.",
    analyzer: "Sysmex XN-1000",
    testCode: "HGB",
    testName: "Гемоглобін (HGB)",
    value: 148.0,
    unit: "г/л",
    normMin: 130.0,
    normMax: 160.0,
    flag: "NORMAL",
    deltaPercent: "0.0%",
    status: "AUTO_VERIFIED",
    comment: "В межах норми."
  }
];

export const mockQcData = {
  analyzer: "Sysmex XN-1000 (#SN-41029)",
  controlMaterial: "XN-CHECK Level 2 (Normal)",
  lotNumber: "LOT-XN-2026-L2",
  parameter: "WBC (Лейкоцити)",
  targetMean: 7.20,
  targetSd: 0.30,
  cvPercent: 4.1,
  currentStatus: "LOCKOUT",
  violatedRule: "Westgard 1-3s (Day 15: Value 8.28 > +3SD)",
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
};

export const mockMicrobiologyData = {
  sampleNumber: "BACT-2026-0914",
  patientName: "Василенко О.П.",
  material: "Сеча (середня порція)",
  colonyCount: "1 x 10^6 КУО/мл",
  pathogen: "Escherichia coli",
  gramType: "Грам-негативні палички",
  cultureMedium: "UriSelect Chromogenic Agar",
  phenotype: "ESBL(-), Carbapenemase(-)",
  antibiotics: [
    { name: "Фосфоміцин", mic: "≤ 1.0", zone: 28, eucast: "S", interpretation: "Чутливий (1-а лінія)" },
    { name: "Нітрофурантоїн", mic: "16.0", zone: 22, eucast: "S", interpretation: "Чутливий" },
    { name: "Ципрофлоксацин", mic: "0.25", zone: 26, eucast: "S", interpretation: "Чутливий" },
    { name: "Ампіцилін", mic: "> 32.0", zone: 11, eucast: "R", interpretation: "Резистентний (Стійкий)" },
    { name: "Амоксицилін / Клаванат", mic: "8.0", zone: 17, eucast: "I", interpretation: "Помірно-чутливий" }
  ]
};

export const mockReagents = [
  {
    id: "reag-01",
    analyzer: "Sysmex XN-1000",
    name: "Cellpack DCL (Ділюент)",
    lotNumber: "LOT-2026-XN08",
    testsRemaining: 1420,
    testsTotal: 2000,
    openedAt: "2026-10-01",
    expiresAt: "2027-12-31",
    status: "ACTIVE"
  },
  {
    id: "reag-02",
    analyzer: "Roche Cobas e411",
    name: "Elecsys TSH (ТТГ)",
    lotNumber: "LOT-682190-01",
    testsRemaining: 18,
    testsTotal: 200,
    openedAt: "2026-09-28",
    expiresAt: "2026-10-28",
    status: "LOW_STOCK"
  },
  {
    id: "reag-03",
    analyzer: "Mindray BS-240",
    name: "Glucose GOD-POD",
    lotNumber: "LOT-GLU-9902",
    testsRemaining: 85,
    testsTotal: 500,
    openedAt: "2026-09-05",
    expiresAt: "2026-10-05",
    status: "EXPIRED"
  }
];

export const mockBiomaterials = [
  { code: "BLDV", name: "Цільна кров венозна", container: "EDTA / Цитрат", snomed: "122555007", storage: "2-8°C до 24 год" },
  { code: "SER", name: "Сироватка крові", container: "Активатор згортання / Гель", snomed: "119364003", storage: "2-8°C до 7 діб, -20°C до 6 міс" },
  { code: "PLAS", name: "Плазма крові (цитратна)", container: "Цитрат натрію 3.2%", snomed: "119361006", storage: "2-8°C до 4 год" },
  { code: "URIN", name: "Сеча (ранкова порція)", container: "Стерильний контейнер", snomed: "122575003", storage: "2-8°C до 4 год" },
  { code: "CSF", name: "Спинномозкова рідина (ліквор)", container: "Стерильна пробірка", snomed: "258450006", storage: "Негайно в роботу (CITO)" }
];

export const mockAnalyzers = [
  { id: "an-01", name: "Sysmex XN-1000", type: "Гематологічний 5-diff", protocol: "ASTM E1381/E1394", connection: "TCP/IP 192.168.1.101:5100", status: "ONLINE" },
  { id: "an-02", name: "Roche Cobas e411", type: "Імунохемілюмінесцентний", protocol: "ASTM E1394", connection: "COM3 (9600 8N1)", status: "ONLINE" },
  { id: "an-03", name: "Mindray BS-240", type: "Біохімічний автоматичний", protocol: "HL7 v2.3.1 MLLP", connection: "TCP/IP 192.168.1.105:5000", status: "ONLINE" },
  { id: "an-04", name: "Sysmex CA-660", type: "Коагулометр оптичний", protocol: "ASTM E1381", connection: "COM4 (9600 8N1)", status: "STANDBY" }
];
