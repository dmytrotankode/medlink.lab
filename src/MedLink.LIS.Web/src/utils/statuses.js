/**
 * Словники статусів, прапорців та ролей (docs/API_CONTRACT.md розділ 1, ТЗ розділ 8).
 * Кожен запис: { label, color, icon?, textColor? } — color у термінах Quasar.
 */

export const ORDER_STATUS = {
  NEW: { label: 'Нове', color: 'blue-7', icon: 'fiber_new' },
  COLLECTED: { label: 'Забрано', color: 'teal-6', icon: 'colorize' },
  IN_TRANSIT: { label: 'У дорозі', color: 'indigo-5', icon: 'local_shipping' },
  RECEIVED: { label: 'Прийнято', color: 'cyan-7', icon: 'inbox' },
  IN_PROGRESS: { label: 'У роботі', color: 'orange-7', icon: 'science' },
  PARTIALLY_COMPLETED: { label: 'Частково видано', color: 'lime-8', icon: 'donut_large' },
  COMPLETED: { label: 'Виконано', color: 'light-green-7', icon: 'task_alt' },
  RELEASED: { label: 'Видано', color: 'positive', icon: 'verified' },
  CANCELLED: { label: 'Скасовано', color: 'grey-6', icon: 'cancel' },
  REJECTED: { label: 'Відхилено', color: 'negative', icon: 'block' }
};

export const ORDER_STATUS_FLOW = ['NEW', 'COLLECTED', 'IN_TRANSIT', 'RECEIVED', 'IN_PROGRESS', 'COMPLETED', 'RELEASED'];

export const LAB_SECTIONS = ['Біохімія', 'Гематологія', 'Імунохімія', 'Коагулологія', 'Сеча', 'Мікробіологія', 'Патогістологія', 'Цитологія'];

export const DERIVATION_TYPES = [
  { value: 'ALIQUOT', label: 'Аліквота', icon: 'water_drop' },
  { value: 'CASSETTE', label: 'Касета', icon: 'inventory_2' },
  { value: 'BLOCK', label: 'Блок', icon: 'view_in_ar' },
  { value: 'SLIDE', label: 'Скло', icon: 'crop_7_5' },
  { value: 'PLATE', label: 'Чашка', icon: 'album' }
];

export const SAMPLE_STATUS = {
  PENDING: { label: 'Очікує забору', color: 'grey-6', icon: 'hourglass_empty' },
  COLLECTED: { label: 'Забрано', color: 'teal-6', icon: 'colorize' },
  IN_TRANSIT: { label: 'У дорозі', color: 'indigo-5', icon: 'local_shipping' },
  RECEIVED: { label: 'Прийнято', color: 'cyan-7', icon: 'inbox' },
  PROCESSING: { label: 'В обробці', color: 'orange-7', icon: 'science' },
  STORED: { label: 'В архіві', color: 'blue-grey-6', icon: 'ac_unit' },
  DISPOSED: { label: 'Утилізовано', color: 'grey-8', icon: 'delete' },
  REJECTED: { label: 'Відбраковано', color: 'negative', icon: 'block' }
};

export const TEST_STATUS = {
  PENDING: { label: 'Очікує', color: 'grey-6', icon: 'hourglass_empty' },
  IN_ANALYSIS: { label: 'В аналізі', color: 'orange-7', icon: 'science' },
  RESULTED: { label: 'Є результат', color: 'blue-7', icon: 'edit_note' },
  NEEDS_REVIEW: { label: 'Очікує лікаря', color: 'warning', textColor: 'dark', icon: 'rate_review' },
  AUTO_VERIFIED: { label: 'Автоверифіковано', color: 'positive', icon: 'verified' },
  VERIFIED: { label: 'Верифіковано', color: 'green-9', icon: 'how_to_reg' },
  REJECTED: { label: 'Відхилено', color: 'negative', icon: 'block' },
  RERUN: { label: 'Повтор', color: 'purple-6', icon: 'replay' }
};

export const FLAG = {
  NORMAL: { label: 'Норма', color: 'positive', short: 'N', css: 'val-normal' },
  LOW: { label: 'Нижче норми', color: 'warning', textColor: 'dark', short: 'L', css: 'val-abnormal' },
  HIGH: { label: 'Вище норми', color: 'warning', textColor: 'dark', short: 'H', css: 'val-abnormal' },
  CRIT_LOW: { label: 'Критично низько', color: 'negative', short: 'LL', css: 'val-critical' },
  CRIT_HIGH: { label: 'Критично високо', color: 'negative', short: 'HH', css: 'val-critical' },
  ABNORMAL: { label: 'Патологія', color: 'warning', textColor: 'dark', short: 'A', css: 'val-abnormal' },
  NONE: { label: 'Не введено', color: 'grey-5', short: '—', css: 'val-empty' }
};

export const MANIFEST_STATUS = {
  DISPATCHED: { label: 'Відправлено', color: 'indigo-5', icon: 'local_shipping' },
  RECEIVED: { label: 'Прийнято', color: 'positive', icon: 'inbox' },
  PARTIAL: { label: 'Частково', color: 'warning', textColor: 'dark', icon: 'rule' },
  CANCELLED: { label: 'Скасовано', color: 'grey-6', icon: 'cancel' }
};

export const CONNECTOR_STATUS = {
  PENDING: { label: 'Очікує реєстрації', color: 'grey-6', icon: 'hourglass_empty' },
  ACTIVE: { label: 'Активний', color: 'positive', icon: 'wifi' },
  OFFLINE: { label: 'Офлайн', color: 'negative', icon: 'wifi_off' },
  DISABLED: { label: 'Вимкнено', color: 'grey-8', icon: 'power_off' }
};

export const BATCH_STATUS = {
  OPEN: { label: 'Відкритий', color: 'blue-7' },
  SENT: { label: 'Надіслано', color: 'orange-7' },
  COMPLETED: { label: 'Завершено', color: 'positive' }
};

export const QC_STATUS = {
  OK: { label: 'У контролі', color: 'positive' },
  WARNING: { label: 'Попередження', color: 'warning', textColor: 'dark' },
  LOCKOUT: { label: 'LOCKOUT', color: 'negative' }
};

export const CULTURE_STATUS = {
  REGISTERED: { label: 'Зареєстровано', color: 'blue-7' },
  INCUBATING: { label: 'Інкубація', color: 'orange-7' },
  GROWTH: { label: 'Є ріст', color: 'deep-orange-6' },
  NO_GROWTH: { label: 'Росту немає', color: 'positive' },
  IDENTIFIED: { label: 'Ідентифіковано', color: 'purple-6' },
  COMPLETED: { label: 'Завершено', color: 'green-9' },
  RELEASED: { label: 'Видано', color: 'positive' }
};

export const ROLE = {
  LAB_ADMIN: 'Адміністратор лабораторії',
  LAB_DOCTOR: 'Лікар-лаборант',
  LAB_TECHNICIAN: 'Лаборант',
  PHLEBOTOMIST: 'Медсестра пункту забору',
  LOGISTICS_COURIER: "Кур'єр",
  REGISTRAR: 'Реєстратор'
};

export const LAYER_TYPE = {
  BASELINE: { label: 'Базова', color: 'grey-7', priority: 10 },
  DEMOGRAPHIC: { label: 'Демографічна (стать/вік)', color: 'blue-7', priority: 40 },
  CLINICAL_ICD10: { label: 'Клінічна (МКХ-10)', color: 'purple-6', priority: 60 },
  MENSTRUAL_PHASE: { label: 'Фаза циклу', color: 'pink-5', priority: 80 },
  PREGNANCY: { label: 'Вагітність', color: 'deep-orange-6', priority: 100 }
};

export const MENSTRUAL_PHASES = [
  { value: 'FOLLICULAR', label: 'Фолікулярна' },
  { value: 'OVULATION', label: 'Овуляторна' },
  { value: 'LUTEAL', label: 'Лютеїнова' },
  { value: 'MENOPAUSE', label: 'Менопауза' }
];

export const AGE_UNITS = [
  { value: 'DAYS', label: 'днів' },
  { value: 'MONTHS', label: 'місяців' },
  { value: 'YEARS', label: 'років' }
];

export const EXCH_TYPES = ['ASTM', 'ASTM_ASK', 'ASTM2', 'HL7', 'TEXT', 'HUMA5L', 'UC1000', 'CYAN', 'JUNIOR', 'IRIS', 'RAPID', 'FUJI', 'TXT'];
export const ANALYZER_CATEGORIES = [
  { value: 'HEMATOLOGY', label: 'Гематологія' },
  { value: 'BIOCHEM', label: 'Біохімія' },
  { value: 'IMMUNO', label: 'Імунологія' },
  { value: 'COAG', label: 'Коагулологія' },
  { value: 'URINE', label: 'Аналіз сечі' },
  { value: 'BLOODGAS', label: 'Гази крові' },
  { value: 'OTHER', label: 'Інше' }
];

export const WESTGARD_RULES = [
  { code: '1_2s', label: '1₂s', kind: 'warning', description: 'Одна точка за межами ±2SD — попередження' },
  { code: '1_3s', label: '1₃s', kind: 'rejection', description: 'Одна точка за межами ±3SD — випадкова помилка' },
  { code: '2_2s', label: '2₂s', kind: 'rejection', description: 'Дві поспіль за межами 2SD по один бік — систематична помилка' },
  { code: 'R_4s', label: 'R₄s', kind: 'rejection', description: 'Різниця двох поспіль понад 4SD — випадкова помилка' },
  { code: '4_1s', label: '4₁s', kind: 'rejection', description: 'Чотири поспіль за межами 1SD по один бік — зсув тренду' },
  { code: '10_x', label: '10ₓ', kind: 'rejection', description: 'Десять поспіль по один бік від середнього — зсув середнього' }
];

export const QC_LEVELS = [
  { value: 'LEVEL_1_LOW', label: 'Рівень 1 (Low)' },
  { value: 'LEVEL_2_NORMAL', label: 'Рівень 2 (Normal)' },
  { value: 'LEVEL_3_HIGH', label: 'Рівень 3 (High)' }
];

export function statusMeta (map, value) {
  return (map && map[value]) || { label: value || '—', color: 'grey-6' };
}

/** Клас рядка черги за ТЗ 8.1 */
export function orderRowClass (order) {
  if (!order) return '';
  const cls = [];
  if (order.isUrgentCito) cls.push('row-status--cito');
  if (order.status === 'RELEASED' || order.status === 'COMPLETED') cls.push('row-status--closed');
  else if (order.status === 'REJECTED') cls.push('row-status--rejected');
  else if (order.status === 'CANCELLED') cls.push('row-status--cancelled');
  else if (order.dueAt && new Date(order.dueAt) < new Date()) cls.push('row-status--overdue');
  return cls.join(' ');
}

export function flagMeta (flag) {
  return FLAG[flag] || FLAG.NONE;
}

export function isCritical (flag) {
  return flag === 'CRIT_LOW' || flag === 'CRIT_HIGH';
}

export function toOptions (map) {
  return Object.keys(map).map(k => ({ value: k, label: map[k].label }));
}
