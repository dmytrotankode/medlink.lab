/**
 * Схеми довідників /dictionaries/{name}: колонки таблиці та поля форми.
 * Типи полів: text, textarea, number, decimal, toggle, color, select{options}, dict{dict,labelField,valueField},
 * chips (масив рядків), json, profileItems (склад профілю), parameterMap.
 */
import { EXCH_TYPES, ANALYZER_CATEGORIES, ROLE } from '../../utils/statuses';

const active = { name: 'isActive', label: 'Активний', type: 'toggle', default: true };
const codeName = [
  { name: 'code', label: 'Код', type: 'text', required: true, col: 4 },
  { name: 'name', label: 'Назва', type: 'text', required: true, col: 8 }
];

export const DICTIONARIES = {
  biomaterials: {
    group: 'basic',
    title: 'Біоматеріали', icon: 'fas fa-tint', description: 'Типи біоматеріалу, стабільність, температурний режим, контейнер за замовчуванням',
    columns: ['code', 'name', 'defaultContainer', 'stabilityHours', 'temperatureRegime', 'isActive'],
    fields: [...codeName,
      { name: 'defaultContainer', label: 'Контейнер / пробірка за замовчуванням (код)', type: 'dict', dict: 'tube-types', labelField: 'name', valueField: 'code', col: 6 },
      { name: 'stabilityHours', label: 'Стабільність, год', type: 'number', col: 3 },
      { name: 'temperatureRegime', label: 'Температурний режим', type: 'select', options: ['+2…+8 °C', '+18…+25 °C', '−20 °C', '−80 °C'], col: 3 },
      active]
  },
  'tube-types': {
    group: 'basic',
    title: 'Пробірки', icon: 'fas fa-vial', description: 'Типи пробірок ISO 6710: колір кришки, антикоагулянт, об’єм, порядок забору',
    columns: ['colorCode', 'code', 'name', 'anticoagulant', 'volumeMl', 'usableVolumeMl', 'maxTestsPerTube', 'orderOfDrawIndex', 'inversionsCount', 'isActive'],
    fields: [...codeName,
      { name: 'colorCode', label: 'Колір кришки', type: 'color', col: 3 },
      { name: 'anticoagulant', label: 'Антикоагулянт', type: 'select', options: ['—', 'K2-ЕДТА', 'K3-ЕДТА', 'Na-цитрат 3,2%', 'Na-цитрат 3,8%', 'Li-гепарин', 'Na-гепарин', 'NaF / K-оксалат', 'Гель-активатор згортання', 'Без добавок'], col: 3 },
      { name: 'volumeMl', label: 'Об’єм, мл', type: 'decimal', col: 2 },
      { name: 'orderOfDrawIndex', label: 'Порядок забору', type: 'number', col: 2 },
      { name: 'inversionsCount', label: 'Інверсій (разів)', type: 'number', col: 2 },
      { name: 'usableVolumeMl', label: 'Корисний об’єм матеріалу, мл', type: 'decimal', col: 3, hint: 'сироватка/плазма/сеча; порожньо — не контролюється' },
      { name: 'maxTestsPerTube', label: 'Макс. тестів на пробірку', type: 'number', col: 3, hint: 'порожньо — без обмеження' },
      active]
  },
  'method-types': {
    group: 'basic',
    title: 'Методики', icon: 'fas fa-flask', description: 'Методики дослідження: фотометрія, ІФА, ІХЛА, ПЛР…',
    columns: ['code', 'name', 'isActive'],
    fields: [...codeName, active]
  },
  'analyzer-types': {
    group: 'basic',
    title: 'Моделі аналізаторів', icon: 'fas fa-server', description: 'Моделі аналізаторів: протокол обміну, формат повідомлень, шаблон замовлення',
    columns: ['code', 'name', 'manufacturer', 'category', 'exchType', 'controlSum', 'orderTemplate', 'isActive'],
    fields: [...codeName,
      { name: 'manufacturer', label: 'Виробник', type: 'text', col: 4 },
      { name: 'category', label: 'Категорія', type: 'select', options: ANALYZER_CATEGORIES, col: 4 },
      { name: 'exchType', label: 'Протокол обміну', type: 'select', options: EXCH_TYPES, col: 4 },
      { name: 'orderTemplate', label: 'Шаблон замовлення', type: 'text', col: 4 },
      { name: 'controlSum', label: 'Контрольна сума', type: 'toggle', col: 4 },
      { name: 'fullText', label: 'Повнотекстовий режим', type: 'toggle', col: 4 },
      { name: 'sleepMs', label: 'Пауза, мс', type: 'number', col: 4 },
      { name: 'bopBase64', label: 'BOP (base64)', type: 'text', col: 6 },
      { name: 'eopBase64', label: 'EOP (base64)', type: 'text', col: 6 },
      active]
  },
  tests: {
    group: 'basic',
    title: 'Показники', icon: 'fas fa-list-ol', description: 'Лабораторні показники: LOINC, одиниці, тип результату, біоматеріал, методика, контроль змін',
    columns: ['code', 'name', 'unit', 'resultType', 'category', 'biomaterialTypeId', 'tubeTypeId', 'requiredVolumeMl', 'requiresSeparateTube', 'deltaCheckMaxPct', 'requiresManualVerification', 'isQcTracked', 'isActive'],
    fields: [...codeName,
      { name: 'shortName', label: 'Коротка назва', type: 'text', col: 4 },
      { name: 'loincCode', label: 'LOINC', type: 'text', col: 4 },
      { name: 'category', label: 'Категорія', type: 'text', col: 4 },
      { name: 'unit', label: 'Одиниця', type: 'text', col: 3 },
      { name: 'decimalPlaces', label: 'Знаків після коми', type: 'number', col: 3, default: 2 },
      { name: 'resultType', label: 'Тип результату', type: 'select', options: [{ value: 'NUMERIC', label: 'Числовий' }, { value: 'TEXT', label: 'Текстовий' }, { value: 'DROPDOWN', label: 'Вибір зі списку' }, { value: 'CALCULATED', label: 'Розрахунковий' }], col: 3, default: 'NUMERIC' },
      { name: 'price', label: 'Ціна, грн', type: 'decimal', col: 3 },
      { name: 'dropdownOptions', label: 'Варіанти зі списку', type: 'chips', col: 6, showIf: f => f.resultType === 'DROPDOWN' },
      { name: 'formula', label: 'Формула розрахунку', type: 'text', col: 6, showIf: f => f.resultType === 'CALCULATED' },
      { name: 'biomaterialTypeId', label: 'Біоматеріал', type: 'dict', dict: 'biomaterials', col: 6 },
      { name: 'methodId', label: 'Методика', type: 'dict', dict: 'method-types', col: 6 },
      { name: 'tubeTypeId', label: 'Тара', type: 'dict', dict: 'tube-types', col: 6 },
      { name: 'requiredVolumeMl', label: 'Потрібний об’єм, мл', type: 'decimal', col: 3 },
      { name: 'maxTestsPerTube', label: 'Макс. тестів у пробірці', type: 'number', col: 3 },
      { name: 'requiresSeparateTube', label: 'Окрема пробірка', type: 'toggle', col: 3 },
      { name: 'tubeCompatibilityGroup', label: 'Група сумісності тари', type: 'text', col: 3, hint: 'однакова група — можна в одну пробірку' },
      { name: 'deltaCheckMaxPct', label: 'Контроль змін, % (поріг)', type: 'decimal', col: 3 },
      { name: 'deltaCheckHours', label: 'Контроль змін, год', type: 'number', col: 3, default: 72 },
      { name: 'requiresManualVerification', label: 'Потребує ручної верифікації', type: 'toggle', col: 3 },
      { name: 'isQcTracked', label: 'Під ВКЯ', type: 'toggle', col: 3 },
      active]
  },
  profiles: {
    group: 'basic',
    title: 'Послуги (профілі)', icon: 'fas fa-medical-services', description: 'Послуги з прайсу MedLink і лабораторні властивості: набір показників, час виконання, підготовка',
    sourceHint: 'Джерело: прайс послуг MedLink. Назва та ціна послуги редагуються в МІС і тут лише для читання; код ЕСОЗ — з каталогу послуг ЕСОЗ.',
    columns: ['code', 'name', 'category', 'turnaroundHours', 'price', 'itemsCount', 'organizationServiceId', 'isActive'],
    fields: [
      { name: 'organizationServiceId', label: 'Послуга прайсу MedLink', type: 'text', col: 4, readonly: true, hint: 'призначається МІС' },
      { name: 'code', label: 'Код', type: 'text', required: true, col: 3 },
      { name: 'name', label: 'Назва', type: 'text', required: true, col: 5, readonlyIf: f => !!f.organizationServiceId },
      { name: 'category', label: 'Категорія', type: 'select', options: ['Гематологія', 'Біохімія', 'Гормони', 'Імунологія', 'Коагулологія', 'Загальноклінічні', 'Мікробіологія', 'ПЛР', 'Інше'], allowCustom: true, col: 4 },
      { name: 'turnaroundHours', label: 'Час виконання, год', type: 'number', col: 3, default: 24 },
      { name: 'price', label: 'Ціна, грн (з МІС)', type: 'decimal', col: 3, readonlyIf: f => !!f.organizationServiceId },
      { name: 'fastingRequired', label: 'Натще', type: 'toggle', col: 2 },
      { name: 'items', label: 'Показники профілю', type: 'profileItems', col: 12 },
      active]
  },
  'reflex-rules': {
    group: 'basic',
    title: 'Правила повторного тесту', icon: 'fas fa-random', description: 'Автододавання тестів: ТТГ > 4 → вільний Т4, ПСА > 4 → вільний ПСА, глюкоза > 11 → HbA1c',
    columns: ['triggerTestCode', 'conditionOperator', 'thresholdValue', 'reflexTestCode', 'autoApprove', 'requiresSameSample', 'description', 'isActive'],
    fields: [
      { name: 'triggerTestCode', label: 'Тригерний тест', type: 'dict', dict: 'tests', labelField: 'code', valueField: 'code', required: true, col: 4 },
      { name: 'conditionOperator', label: 'Умова', type: 'select', options: [{ value: '<', label: 'менше (<)' }, { value: '<=', label: 'не більше (≤)' }, { value: '>', label: 'більше (>)' }, { value: '>=', label: 'не менше (≥)' }, { value: '==', label: 'дорівнює (=)' }, { value: 'OUT_OF_RANGE', label: 'поза нормою' }, { value: 'CRITICAL', label: 'критичне значення' }], required: true, col: 4, default: '>' },
      { name: 'thresholdValue', label: 'Поріг', type: 'decimal', col: 4 },
      { name: 'reflexTestCode', label: 'Тест, що додається', type: 'dict', dict: 'tests', labelField: 'code', valueField: 'code', required: true, col: 4 },
      { name: 'autoApprove', label: 'Автопідтвердження', type: 'toggle', col: 4 },
      { name: 'requiresSameSample', label: 'Та сама проба', type: 'toggle', col: 4, default: true },
      { name: 'description', label: 'Опис', type: 'textarea', col: 12 },
      active]
  },
  organisms: {
    group: 'micro',
    title: 'Мікроорганізми', icon: 'fas fa-disease', description: 'Довідник мікроорганізмів (латинська назва, царство, грам)',
    columns: ['code', 'latinName', 'commonName', 'kingdom', 'gramStain', 'isPathogen', 'alertCriticalOrganism', 'isActive'],
    fields: [
      { name: 'code', label: 'Код', type: 'text', required: true, col: 4 },
      { name: 'latinName', label: 'Латинська назва', type: 'text', required: true, col: 8 },
      { name: 'commonName', label: 'Назва українською', type: 'text', col: 8 },
      { name: 'kingdom', label: 'Царство', type: 'select', col: 4, default: 'BACTERIA', options: [{ value: 'BACTERIA', label: 'Бактерії' }, { value: 'FUNGI', label: 'Гриби' }, { value: 'VIRUS', label: 'Віруси' }, { value: 'PARASITE', label: 'Паразити' }] },
      { name: 'gramStain', label: 'Грам', type: 'select', options: [{ value: 'GRAM_POSITIVE', label: 'Грам-позитивні' }, { value: 'GRAM_NEGATIVE', label: 'Грам-негативні' }, { value: 'FUNGI', label: 'Гриби' }, { value: 'OTHER', label: 'Інше' }], col: 4 },
      { name: 'morphology', label: 'Морфологія', type: 'text', col: 4 },
      { name: 'isPathogen', label: 'Патоген', type: 'toggle', col: 4, default: true },
      { name: 'isOpportunistic', label: 'Умовно-патогенний', type: 'toggle', col: 4 },
      { name: 'alertCriticalOrganism', label: 'Критичний збудник (сповіщення)', type: 'toggle', col: 4 },
      active]
  },
  antibiotics: {
    group: 'micro',
    title: 'Антибіотики', icon: 'fas fa-pills', description: 'Антибіотики для антибіотикограми (код, група, ATC, диск)',
    columns: ['code', 'name', 'groupName', 'atcCode', 'defaultDiskContentMcg', 'isActive'],
    fields: [...codeName,
      { name: 'groupName', label: 'Група / клас', type: 'text', col: 4 },
      { name: 'atcCode', label: 'Код ATC', type: 'text', col: 4 },
      { name: 'eucastCode', label: 'Код EUCAST', type: 'text', col: 4 },
      { name: 'defaultDiskContentMcg', label: 'Вміст диска, мкг', type: 'decimal', col: 4 },
      active]
  },
  'eucast-breakpoints': {
    group: 'micro',
    title: 'Контрольні значення EUCAST', icon: 'fas fa-ruler-horizontal', description: 'Пороги чутливості S/I/R за зоною (мм) та МІК (мг/л) для пари збудник–антибіотик; застарілі версії — в архів',
    columns: ['organismId', 'antibioticId', 'eucastVersion', 'zoneSusceptibleGe', 'zoneResistantLt', 'micSusceptibleLe', 'micResistantGt', 'intrinsicResistance', 'isActive'],
    fields: [
      { name: 'organismId', label: 'Збудник', type: 'dict', dict: 'organisms', required: true, col: 6 },
      { name: 'antibioticId', label: 'Антибіотик', type: 'dict', dict: 'antibiotics', required: true, col: 6 },
      { name: 'zoneSusceptibleGe', label: 'Зона S ≥, мм', type: 'decimal', col: 3 },
      { name: 'zoneResistantLt', label: 'Зона R <, мм', type: 'decimal', col: 3 },
      { name: 'micSusceptibleLe', label: 'МІК S ≤, мг/л', type: 'decimal', col: 3 },
      { name: 'micResistantGt', label: 'МІК R >, мг/л', type: 'decimal', col: 3 },
      { name: 'eucastVersion', label: 'Версія EUCAST', type: 'text', col: 4, default: 'v14.0_2024' },
      { name: 'intrinsicResistance', label: 'Природна резистентність', type: 'toggle', col: 4 },
      { name: 'guidanceNotes', label: 'Примітки', type: 'textarea', col: 12 },
      active]
  },
  departments: {
    group: 'org',
    title: 'Відділення', icon: 'fas fa-hospital', description: 'Підрозділи MedLink і вид підрозділу в ЛІС: лабораторія, пункт забору, клінічне відділення, філія',
    columns: ['code', 'name', 'labKind', 'departmentTypeName', 'address', 'phone', 'isActive'],
    fields: [...codeName,
      { name: 'labKind', label: 'Вид для ЛІС', type: 'select', options: [{ value: 'LABORATORY', label: 'Лабораторія' }, { value: 'COLLECTION_POINT', label: 'Пункт забору' }, { value: 'CLINICAL', label: 'Клінічне відділення' }, { value: 'BRANCH', label: 'Філія' }], required: true, col: 6, default: 'CLINICAL' },
      { name: 'phone', label: 'Телефон', type: 'text', col: 6 },
      { name: 'address', label: 'Адреса', type: 'text', col: 12 },
      active]
  },
  employees: {
    group: 'org',
    title: 'Співробітники', icon: 'fas fa-users', description: 'Співробітники MedLink і їхня лабораторна роль',
    columns: ['fullName', 'position', 'labRole', 'departmentId', 'phone', 'isActive'],
    fields: [
      { name: 'fullName', label: 'ПІБ', type: 'text', required: true, col: 6 },
      { name: 'position', label: 'Посада', type: 'text', col: 6 },
      { name: 'labRole', label: 'Лабораторна роль', type: 'select', options: Object.keys(ROLE).map(k => ({ value: k, label: ROLE[k] })), required: true, col: 6 },
      { name: 'departmentId', label: 'Відділення', type: 'dict', dict: 'departments', col: 6 },
      { name: 'phone', label: 'Телефон', type: 'text', col: 6 },
      { name: 'email', label: 'E-mail', type: 'text', col: 6 },
      active]
  },

  // ---------- Патоморфологія ----------
  'path-methods': {
    group: 'path',
    title: 'Методики забарвлення та ІГХ', icon: 'fas fa-microscope', description: 'Гістологічні та цитологічні забарвлення, ІГХ-антитіла; ціна скельця (0 — входить у вартість блока)',
    columns: ['code', 'name', 'isIhc', 'price', 'isActive'],
    fields: [...codeName,
      { name: 'isIhc', label: 'ІГХ-антитіло', type: 'toggle', col: 4 },
      { name: 'price', label: 'Ціна, грн', type: 'decimal', col: 4 },
      active]
  },
  'path-material-types': {
    group: 'path',
    title: 'Типи матеріалу та строки', icon: 'fas fa-vial', description: 'Тип матеріалу: строк у робочих днях, група послуг, базова ціна',
    columns: ['code', 'name', 'slaDays', 'serviceGroup', 'basePrice', 'isActive'],
    fields: [...codeName,
      { name: 'slaDays', label: 'Строк, робочих днів', type: 'number', col: 4 },
      { name: 'serviceGroup', label: 'Група послуг', type: 'text', col: 4 },
      { name: 'basePrice', label: 'Базова ціна, грн', type: 'decimal', col: 4 },
      active]
  },
  'path-fixatives': {
    group: 'path',
    title: 'Фіксатори', icon: 'fas fa-flask', description: 'Фіксатори з мінімальною та максимальною тривалістю фіксації',
    columns: ['name', 'minHours', 'maxHours', 'isActive'],
    fields: [
      { name: 'name', label: 'Назва', type: 'text', required: true, col: 6 },
      { name: 'minHours', label: 'Мінімум, год', type: 'number', col: 3 },
      { name: 'maxHours', label: 'Максимум, год', type: 'number', col: 3 },
      active]
  },
  'path-reject-reasons': {
    group: 'path',
    title: 'Причини відмови в прийомі матеріалу', icon: 'fas fa-ban', description: 'Причини, з яких матеріал не приймається на патоморфологічне дослідження',
    columns: ['code', 'name', 'isActive'],
    fields: [...codeName, active]
  },
  'path-templates': {
    group: 'path',
    title: 'Шаблони макро- та мікроопису', icon: 'fas fa-file-medical-alt', description: 'Шаблони опису за органом і видом матеріалу для заключень',
    columns: ['name', 'macro', 'micro', 'isActive'],
    fields: [
      { name: 'name', label: 'Орган · вид матеріалу', type: 'text', required: true, col: 12 },
      { name: 'macro', label: 'Макроопис', type: 'textarea', col: 12 },
      { name: 'micro', label: 'Мікроопис', type: 'textarea', col: 12 },
      active]
  },
  'path-rooms': {
    group: 'path',
    title: 'Приміщення зберігання', icon: 'fas fa-archive', description: 'Приміщення архіву блоків і скелець',
    columns: ['code', 'name', 'isActive'],
    fields: [...codeName, active]
  },
  'path-ihc-rules': {
    group: 'path',
    title: 'Правила автозамовлення ІГХ', icon: 'fas fa-project-diagram', description: 'Діапазон топографії МКХ-О (наприклад, C50–C50.9) і морфологія → панель ІГХ-маркерів',
    columns: ['topoFrom', 'topoTo', 'morphCodes', 'panel', 'why', 'isActive'],
    fields: [
      { name: 'topoFrom', label: 'Топографія: початок діапазону', type: 'text', required: true, col: 3, hint: 'наприклад, C50' },
      { name: 'topoTo', label: 'Топографія: кінець діапазону', type: 'text', required: true, col: 3, hint: 'наприклад, C50.9' },
      { name: 'morphCodes', label: 'Морфологія (коди через кому)', type: 'text', col: 6, hint: 'наприклад, 8500/3, 8520/3' },
      { name: 'panel', label: 'Панель маркерів', type: 'text', required: true, col: 12 },
      { name: 'why', label: 'Обґрунтування', type: 'textarea', col: 12 },
      active]
  },
  'path-cytology': {
    group: 'path',
    title: 'Цитологія: матеріали, адекватність, категорії', icon: 'fas fa-dna', description: 'Види матеріалу, оцінка адекватності, категорії висновку; категорія може вимагати повідомлення направнику',
    columns: ['kind', 'code', 'name', 'isHigh', 'isNormal', 'materials', 'sortOrder', 'isActive'],
    fields: [
      { name: 'kind', label: 'Вид позиції', type: 'select', required: true, col: 4, options: [{ value: 'MATERIAL', label: 'Вид матеріалу' }, { value: 'ADEQUACY', label: 'Адекватність' }, { value: 'CATEGORY', label: 'Категорія висновку' }] },
      { name: 'code', label: 'Код', type: 'text', required: true, col: 4 },
      { name: 'sortOrder', label: 'Порядок', type: 'number', col: 4 },
      { name: 'name', label: 'Назва', type: 'text', required: true, col: 12 },
      { name: 'isHigh', label: 'Потребує повідомлення направнику', type: 'toggle', col: 4, showIf: f => f.kind === 'CATEGORY' },
      { name: 'isNormal', label: 'Нормальна категорія', type: 'toggle', col: 4, showIf: f => f.kind === 'CATEGORY' },
      { name: 'materials', label: 'Коди видів матеріалу через кому (порожньо — для всіх)', type: 'text', col: 12, showIf: f => f.kind === 'CATEGORY' },
      active]
  },
  'path-prices': {
    group: 'path',
    title: 'Тарифи патоморфології', icon: 'fas fa-coins', description: 'Вирізка (перший та наступні блоки), ІГХ, консультація, CITO, цитологія; значення за замовчуванням, прайс розрахунків може їх перевизначити',
    columns: ['name', 'amount', 'isActive'],
    fields: [
      { name: 'code', label: 'Тариф', type: 'select', required: true, col: 6, readonlyIf: f => !!f.id, options: [
        { value: 'FIRST_BLOCK', label: 'Вирізка: перший блок' }, { value: 'NEXT_BLOCK', label: 'Вирізка: кожен наступний блок' }, { value: 'IHC', label: 'ІГХ-маркер (типова ціна)' },
        { value: 'CONSULT_SLIDE', label: 'Консультація: за скельце' }, { value: 'CITO', label: 'Надбавка за CITO' }, { value: 'CYTO_SLIDE', label: 'Цитологія: скельце' }, { value: 'IHC_EXTRA_DAYS', label: 'ІГХ: додаткові робочі дні' }] },
      { name: 'name', label: 'Назва', type: 'text', required: true, col: 6 },
      { name: 'amount', label: 'Значення (грн або дні)', type: 'decimal', col: 6 },
      active]
  },

  // ---------- Показники й пошук ----------
  'test-synonyms': {
    group: 'basic',
    title: 'Синоніми показників', icon: 'fas fa-spell-check', description: 'Альтернативні назви показників для пошуку в замовленнях: «глікований гемоглобін» знаходить HbA1c',
    columns: ['testCode', 'synonym', 'language', 'isActive'],
    fields: [
      { name: 'testCode', label: 'Показник', type: 'dict', dict: 'tests', labelField: 'code', valueField: 'code', required: true, col: 6 },
      { name: 'synonym', label: 'Синонім', type: 'text', required: true, col: 6 },
      { name: 'language', label: 'Мова', type: 'select', col: 4, default: 'uk', options: [{ value: 'uk', label: 'Українська' }, { value: 'en', label: 'English' }, { value: 'ru', label: 'Російська' }] },
      active]
  },
  'work-calendar': {
    group: 'basic',
    title: 'Виробничий календар', icon: 'fas fa-calendar-alt', description: 'Святкові дні в будні та робочі суботи/неділі: впливають на строки виконання (наприклад, патоморфології)',
    columns: ['day', 'kind', 'name', 'isActive'],
    fields: [
      { name: 'day', label: 'Дата', type: 'date', required: true, col: 4 },
      { name: 'kind', label: 'Вид дня', type: 'select', required: true, col: 4, default: 'HOLIDAY', options: [{ value: 'HOLIDAY', label: 'Вихідний (свято в будній день)' }, { value: 'WORKDAY', label: 'Робочий (перенесення на вихідний)' }] },
      { name: 'name', label: 'Назва', type: 'text', col: 4 },
      active]
  },
  'reagent-items': {
    group: 'basic',
    title: 'Номенклатура реагентів', icon: 'fas fa-prescription-bottle', description: 'Довідник реагентів окремо від лотів: назва, виробник, каталожний номер, показник, мінімальний запас',
    columns: ['code', 'name', 'manufacturer', 'catalogNumber', 'testCode', 'minimumTests', 'isActive'],
    fields: [...codeName,
      { name: 'manufacturer', label: 'Виробник', type: 'text', col: 4 },
      { name: 'catalogNumber', label: 'Каталожний номер', type: 'text', col: 4 },
      { name: 'unit', label: 'Одиниця обліку', type: 'text', col: 4, hint: 'тест, мл, набір' },
      { name: 'testCode', label: 'Показник', type: 'dict', dict: 'tests', labelField: 'code', valueField: 'code', col: 6 },
      { name: 'storage', label: 'Зберігання', type: 'text', col: 3, hint: '+2…+8 °C' },
      { name: 'minimumTests', label: 'Мін. запас, тестів', type: 'number', col: 3, default: 50 },
      active]
  },
};

export const DICTIONARY_GROUPS = [
  { key: 'basic', title: 'Основні довідники' },
  { key: 'micro', title: 'Мікробіологія' },
  { key: 'path', title: 'Патоморфологія та цитологія' },
  { key: 'org', title: 'Організаційна структура' }
];

export const DICTIONARY_LIST = Object.keys(DICTIONARIES).map(k => ({ name: k, ...DICTIONARIES[k] }));

export function columnLabel (dict, col) {
  const f = (dict.fields || []).find(x => x.name === col);
  if (f) return f.label;
  return { itemsCount: 'Показників', colorCode: 'Колір', biomaterialTypeId: 'Біоматеріал', departmentId: 'Відділення', antibioticId: 'Антибіотик', organismId: 'Збудник', departmentTypeName: 'Тип підрозділу MedLink' }[col] || col;
}
