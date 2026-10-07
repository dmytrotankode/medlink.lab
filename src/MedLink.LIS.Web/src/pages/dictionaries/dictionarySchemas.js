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
    title: 'Біоматеріали', icon: 'fas fa-tint', description: 'Типи біоматеріалу, стабільність, температурний режим, контейнер за замовчуванням',
    columns: ['code', 'name', 'defaultContainer', 'stabilityHours', 'temperatureRegime', 'isActive'],
    fields: [...codeName,
      { name: 'defaultContainer', label: 'Контейнер / пробірка за замовчуванням (код)', type: 'dict', dict: 'tube-types', labelField: 'name', valueField: 'code', col: 6 },
      { name: 'stabilityHours', label: 'Стабільність, год', type: 'number', col: 3 },
      { name: 'temperatureRegime', label: 'Температурний режим', type: 'select', options: ['+2…+8 °C', '+18…+25 °C', '−20 °C', '−80 °C'], col: 3 },
      active]
  },
  'tube-types': {
    title: 'Пробірки', icon: 'fas fa-vial', description: 'Типи пробірок ISO 6710: колір кришки, антикоагулянт, об’єм, порядок забору',
    columns: ['colorCode', 'code', 'name', 'anticoagulant', 'volumeMl', 'orderOfDrawIndex', 'inversionsCount', 'isActive'],
    fields: [...codeName,
      { name: 'colorCode', label: 'Колір кришки', type: 'color', col: 3 },
      { name: 'anticoagulant', label: 'Антикоагулянт', type: 'select', options: ['—', 'K2-ЕДТА', 'K3-ЕДТА', 'Na-цитрат 3,2%', 'Na-цитрат 3,8%', 'Li-гепарин', 'Na-гепарин', 'NaF / K-оксалат', 'Гель-активатор згортання', 'Без добавок'], col: 3 },
      { name: 'volumeMl', label: 'Об’єм, мл', type: 'decimal', col: 2 },
      { name: 'orderOfDrawIndex', label: 'Порядок забору', type: 'number', col: 2 },
      { name: 'inversionsCount', label: 'Інверсій', type: 'number', col: 2 },
      active]
  },
  'method-types': {
    title: 'Методики', icon: 'fas fa-flask', description: '46 методик дослідження (Simplex): фотометрія, ІФА, ІХЛА, ПЛР…',
    columns: ['code', 'name', 'isActive'],
    fields: [...codeName, active]
  },
  'analyzer-types': {
    title: 'Моделі аналізаторів', icon: 'fas fa-server', description: '65 профілів приладів з ac_analyzer_type Simplex: протокол обміну, рамкування, шаблон замовлення',
    columns: ['code', 'name', 'manufacturer', 'category', 'exchType', 'controlSum', 'orderTemplate', 'isActive'],
    fields: [...codeName,
      { name: 'manufacturer', label: 'Виробник', type: 'text', col: 4 },
      { name: 'category', label: 'Категорія', type: 'select', options: ANALYZER_CATEGORIES, col: 4 },
      { name: 'exchType', label: 'Протокол обміну (exchType)', type: 'select', options: EXCH_TYPES, col: 4 },
      { name: 'orderTemplate', label: 'Шаблон замовлення (builder)', type: 'text', col: 4 },
      { name: 'controlSum', label: 'Контрольна сума', type: 'toggle', col: 4 },
      { name: 'fullText', label: 'Повнотекстовий режим', type: 'toggle', col: 4 },
      { name: 'sleepMs', label: 'Пауза, мс', type: 'number', col: 4 },
      { name: 'bopBase64', label: 'BOP (base64)', type: 'text', col: 6 },
      { name: 'eopBase64', label: 'EOP (base64)', type: 'text', col: 6 },
      active]
  },
  tests: {
    title: 'Показники', icon: 'fas fa-list-ol', description: 'Лабораторні показники: LOINC, одиниці, тип результату, біоматеріал, методика, delta-check',
    columns: ['code', 'name', 'unit', 'resultType', 'category', 'biomaterialTypeId', 'deltaCheckMaxPct', 'requiresManualVerification', 'isQcTracked', 'isActive'],
    fields: [...codeName,
      { name: 'shortName', label: 'Коротка назва', type: 'text', col: 4 },
      { name: 'loincCode', label: 'LOINC', type: 'text', col: 4 },
      { name: 'category', label: 'Категорія', type: 'text', col: 4 },
      { name: 'unit', label: 'Одиниця', type: 'text', col: 3 },
      { name: 'decimalPlaces', label: 'Знаків після коми', type: 'number', col: 3, default: 2 },
      { name: 'resultType', label: 'Тип результату', type: 'select', options: ['NUMERIC', 'TEXT', 'DROPDOWN', 'CALCULATED'], col: 3, default: 'NUMERIC' },
      { name: 'price', label: 'Ціна, грн', type: 'decimal', col: 3 },
      { name: 'dropdownOptions', label: 'Варіанти (DROPDOWN)', type: 'chips', col: 6, showIf: f => f.resultType === 'DROPDOWN' },
      { name: 'formula', label: 'Формула (CALCULATED)', type: 'text', col: 6, showIf: f => f.resultType === 'CALCULATED' },
      { name: 'biomaterialTypeId', label: 'Біоматеріал', type: 'dict', dict: 'biomaterials', col: 6 },
      { name: 'methodId', label: 'Методика', type: 'dict', dict: 'method-types', col: 6 },
      { name: 'deltaCheckMaxPct', label: 'Delta-check, % (поріг)', type: 'decimal', col: 3 },
      { name: 'deltaCheckHours', label: 'Delta-check, год', type: 'number', col: 3, default: 72 },
      { name: 'requiresManualVerification', label: 'Потребує ручної верифікації', type: 'toggle', col: 3 },
      { name: 'isQcTracked', label: 'Під ВКЯ', type: 'toggle', col: 3 },
      active]
  },
  profiles: {
    title: 'Послуги (профілі)', icon: 'fas fa-medical-services', description: 'Довідник послуг МІС (dct_service) + лабораторні атрибути: набір показників, TAT, підготовка',
    sourceHint: 'Джерело: довідник послуг MedLink (dct_service). Код, назва та ціна послуги з misServiceId редагуються в МІС і тут лише для читання.',
    columns: ['code', 'name', 'category', 'turnaroundHours', 'price', 'itemsCount', 'misServiceId', 'isActive'],
    fields: [
      { name: 'misServiceId', label: 'ID послуги МІС (dct_service)', type: 'text', col: 4, readonly: true, hint: 'призначається МІС' },
      { name: 'code', label: 'Код', type: 'text', required: true, col: 3, readonlyIf: f => !!f.misServiceId },
      { name: 'name', label: 'Назва', type: 'text', required: true, col: 5, readonlyIf: f => !!f.misServiceId },
      { name: 'category', label: 'Категорія', type: 'select', options: ['Гематологія', 'Біохімія', 'Гормони', 'Імунологія', 'Коагулологія', 'Загальноклінічні', 'Мікробіологія', 'ПЛР', 'Інше'], allowCustom: true, col: 4 },
      { name: 'turnaroundHours', label: 'TAT, год', type: 'number', col: 3, default: 24 },
      { name: 'price', label: 'Ціна, грн (з МІС)', type: 'decimal', col: 3, readonlyIf: f => !!f.misServiceId },
      { name: 'fastingRequired', label: 'Натще', type: 'toggle', col: 2 },
      { name: 'items', label: 'Показники профілю', type: 'profileItems', col: 12 },
      active]
  },
  'reflex-rules': {
    title: 'Reflex-правила', icon: 'fas fa-random', description: 'Автододавання тестів: TSH > 4 → FT4, PSA > 4 → fPSA, Glucose > 11 → HbA1c',
    columns: ['triggerTestCode', 'conditionOperator', 'thresholdValue', 'reflexTestCode', 'autoApprove', 'requiresSameSample', 'description', 'isActive'],
    fields: [
      { name: 'triggerTestCode', label: 'Тригерний тест', type: 'dict', dict: 'tests', labelField: 'code', valueField: 'code', required: true, col: 4 },
      { name: 'conditionOperator', label: 'Умова', type: 'select', options: ['<', '<=', '>', '>=', '==', 'OUT_OF_RANGE', 'CRITICAL'], required: true, col: 4, default: '>' },
      { name: 'thresholdValue', label: 'Поріг', type: 'decimal', col: 4 },
      { name: 'reflexTestCode', label: 'Reflex-тест', type: 'dict', dict: 'tests', labelField: 'code', valueField: 'code', required: true, col: 4 },
      { name: 'autoApprove', label: 'Автопідтвердження', type: 'toggle', col: 4 },
      { name: 'requiresSameSample', label: 'Та сама проба', type: 'toggle', col: 4, default: true },
      { name: 'description', label: 'Опис', type: 'textarea', col: 12 },
      active]
  },
  organisms: {
    title: 'Мікроорганізми', icon: 'fas fa-disease', description: 'Довідник мікроорганізмів (рід, вид, грам)',
    columns: ['code', 'name', 'genus', 'gramStain', 'eucastGroup', 'isActive'],
    fields: [...codeName,
      { name: 'genus', label: 'Рід', type: 'text', col: 4 },
      { name: 'gramStain', label: 'Грам', type: 'select', options: ['Грам+', 'Грам−', 'Гриби', 'Інше'], col: 4 },
      { name: 'eucastGroup', label: 'Група EUCAST', type: 'text', col: 4 },
      active]
  },
  antibiotics: {
    title: 'Антибіотики', icon: 'fas fa-pills', description: 'Антибіотики для антибіотикограми (код, клас, диск)',
    columns: ['code', 'name', 'antibioticClass', 'diskContent', 'isActive'],
    fields: [...codeName,
      { name: 'antibioticClass', label: 'Клас', type: 'text', col: 6 },
      { name: 'diskContent', label: 'Вміст диска (мкг)', type: 'text', col: 6 },
      active]
  },
  'eucast-breakpoints': {
    title: 'EUCAST breakpoints', icon: 'fas fa-ruler-horizontal', description: 'Пороги S/I/R за зоною (мм) та МІК (мг/л) для пари організм–антибіотик',
    columns: ['organismGroup', 'antibioticId', 'zoneS', 'zoneR', 'micS', 'micR', 'version', 'isActive'],
    fields: [
      { name: 'organismGroup', label: 'Група організмів / організм', type: 'text', required: true, col: 6 },
      { name: 'antibioticId', label: 'Антибіотик', type: 'dict', dict: 'antibiotics', required: true, col: 6 },
      { name: 'zoneS', label: 'Зона S ≥, мм', type: 'decimal', col: 3 },
      { name: 'zoneR', label: 'Зона R <, мм', type: 'decimal', col: 3 },
      { name: 'micS', label: 'МІК S ≤', type: 'decimal', col: 3 },
      { name: 'micR', label: 'МІК R >', type: 'decimal', col: 3 },
      { name: 'version', label: 'Версія EUCAST', type: 'text', col: 4, default: 'v14.0' },
      active]
  },
  departments: {
    title: 'Відділення', icon: 'fas fa-hospital', description: 'Відділення МІС (org_department): замовники, пункти забору, лабораторія',
    columns: ['code', 'name', 'type', 'phone', 'isActive'],
    fields: [...codeName,
      { name: 'type', label: 'Тип', type: 'select', options: ['Лабораторія', 'Пункт забору', 'Стаціонар', 'Поліклініка', 'Інше'], allowCustom: true, col: 6 },
      { name: 'phone', label: 'Телефон', type: 'text', col: 6 },
      active]
  },
  employees: {
    title: 'Співробітники', icon: 'fas fa-users', description: 'org_employee: ПІБ, посада, лабораторна роль (довідкова), відділення',
    columns: ['fullName', 'position', 'labRole', 'departmentId', 'phone', 'isActive'],
    fields: [
      { name: 'fullName', label: 'ПІБ', type: 'text', required: true, col: 6 },
      { name: 'position', label: 'Посада', type: 'text', col: 6 },
      { name: 'labRole', label: 'Лабораторна роль', type: 'select', options: Object.keys(ROLE).map(k => ({ value: k, label: ROLE[k] })), required: true, col: 6 },
      { name: 'departmentId', label: 'Відділення', type: 'dict', dict: 'departments', col: 6 },
      { name: 'phone', label: 'Телефон', type: 'text', col: 6 },
      { name: 'email', label: 'E-mail', type: 'text', col: 6 },
      active]
  }
};

export const DICTIONARY_LIST = Object.keys(DICTIONARIES).map(k => ({ name: k, ...DICTIONARIES[k] }));

export function columnLabel (dict, col) {
  const f = (dict.fields || []).find(x => x.name === col);
  if (f) return f.label;
  return { itemsCount: 'Показників', colorCode: 'Колір', biomaterialTypeId: 'Біоматеріал', departmentId: 'Відділення', antibioticId: 'Антибіотик' }[col] || col;
}
