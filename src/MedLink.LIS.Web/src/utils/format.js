/**
 * Форматування дат (dd.MM.yyyy HH:mm), чисел (десяткова кома) та інших значень.
 */

const pad = n => String(n).padStart(2, '0');

export function parseDate (value) {
  if (!value) return null;
  if (value instanceof Date) return value;
  const d = new Date(value);
  return isNaN(d.getTime()) ? null : d;
}

export function formatDate (value) {
  const d = parseDate(value);
  if (!d) return '—';
  return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()}`;
}

export function formatDateTime (value) {
  const d = parseDate(value);
  if (!d) return '—';
  return `${formatDate(d)} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function formatTime (value) {
  const d = parseDate(value);
  if (!d) return '—';
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** ISO yyyy-MM-dd для фільтрів дат */
export function toIsoDate (d) {
  const date = parseDate(d) || new Date();
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function todayIso () {
  return toIsoDate(new Date());
}

export function daysAgoIso (days) {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return toIsoDate(d);
}

/** Число з десятковою комою; digits — кількість знаків (null = як є) */
export function formatNumber (value, digits = null) {
  if (value === null || value === undefined || value === '') return '—';
  const n = Number(value);
  if (isNaN(n)) return String(value);
  const s = digits === null ? String(n) : n.toFixed(digits);
  return s.replace('.', ',');
}

export function formatMoney (value) {
  if (value === null || value === undefined) return '—';
  const s = formatNumber(value, 2);
  const parts = s.split(',');
  // розділювач розрядів — нерозривний пробіл: «9 120,00 грн»
  parts[0] = parts[0].replace(/\B(?=(\d{3})+$)/g, ' ');
  return `${parts.join(',')} грн`;
}

export function formatPercent (value, digits = 1) {
  if (value === null || value === undefined || value === '') return '—';
  const n = Number(value);
  if (isNaN(n)) return String(value);
  const sign = n > 0 ? '+' : '';
  return `${sign}${formatNumber(n, digits)}%`;
}

/** Парсинг введеного користувачем числа (кома або крапка) */
export function parseDecimal (input) {
  if (input === null || input === undefined || input === '') return null;
  const n = Number(String(input).trim().replace(',', '.'));
  return isNaN(n) ? null : n;
}

export function formatMinutes (min) {
  if (min === null || min === undefined) return '—';
  const m = Math.round(Number(min));
  if (m < 60) return `${m} хв`;
  const h = Math.floor(m / 60);
  const rest = m % 60;
  if (h < 24) return rest ? `${h} год ${rest} хв` : `${h} год`;
  const d = Math.floor(h / 24);
  return `${d} д ${h % 24} год`;
}

export function ageFromBirthDate (birthDate, at = new Date()) {
  const b = parseDate(birthDate);
  if (!b) return null;
  const now = parseDate(at) || new Date();
  let years = now.getFullYear() - b.getFullYear();
  const m = now.getMonth() - b.getMonth();
  if (m < 0 || (m === 0 && now.getDate() < b.getDate())) years--;
  return years;
}

export function patientDisplay (p) {
  if (!p) return '—';
  if (p.fullName) return p.fullName;
  return [p.lastName, p.firstName, p.middleName].filter(Boolean).join(' ') || p.name || '—';
}

export function genderLabel (g) {
  if (g === 'M') return 'Чол.';
  if (g === 'F') return 'Жін.';
  return '—';
}

/**
 * Єдиний вигляд норми: десяткова кома («4,1 – 5,9»), для якісних показників — лише перше значення
 * українською («не виявлено;negative» → «не виявлено»; синоніми в довіднику норм на екран не виводимо).
 */
export function normalizeReference (text) {
  let s = String(text);
  if (s.includes(';') && /[^\d\s.,;–—<>=+-]/.test(s)) s = s.split(';')[0].trim();
  return s.replace(/(\d)\.(\d)/g, '$1,$2');
}

export function referenceDisplay (row) {
  if (!row) return '';
  if (row.referenceDisplay) return normalizeReference(row.referenceDisplay);
  if (row.normText) return normalizeReference(row.normText);
  const lo = row.normLow; const hi = row.normHigh;
  if (lo === null && hi === null) return '';
  if (lo === undefined && hi === undefined) return '';
  if ((lo === null || lo === undefined) && hi !== null) return `< ${formatNumber(hi)}`;
  if ((hi === null || hi === undefined) && lo !== null) return `> ${formatNumber(lo)}`;
  return `${formatNumber(lo)} – ${formatNumber(hi)}`;
}

export function downloadBlob (blob, filename) {
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  setTimeout(() => {
    document.body.removeChild(a);
    window.URL.revokeObjectURL(url);
  }, 100);
}

export function copyToClipboard (text) {
  if (navigator.clipboard && navigator.clipboard.writeText) {
    return navigator.clipboard.writeText(text);
  }
  const ta = document.createElement('textarea');
  ta.value = text;
  document.body.appendChild(ta);
  ta.select();
  document.execCommand('copy');
  document.body.removeChild(ta);
  return Promise.resolve();
}

/** Відмінювання за числом: plural(4, 'пробірка', 'пробірки', 'пробірок') → 'пробірки' */
export function plural (n, one, few, many) {
  const abs = Math.abs(Number(n)) || 0;
  const m10 = abs % 10; const m100 = abs % 100;
  if (m10 === 1 && m100 !== 11) return one;
  if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return few;
  return many;
}

/** «4 пробірки», «1 показник» тощо */
export function pluralN (n, one, few, many) {
  return `${n} ${plural(n, one, few, many)}`;
}

/** Назва показника без латинського коду в кінці: «Лейкоцити (WBC)» → «Лейкоцити» */
export function testTitle (name) {
  const s = String(name == null ? '' : name).trim();
  const t = s.replace(/\s*\([A-Za-z0-9%#_\-./ ]{1,16}\)\s*$/, '').trim();
  return t || s;
}

/** Маскування ІПН у списках і картках: показуємо лише останні 4 цифри */
export function maskTaxId (v) {
  const s = String(v == null ? '' : v).trim();
  return s.length > 4 ? '******' + s.slice(-4) : s;
}

/** Назва пробірки для показу: десяткова кома, єдине написання ЕДТА К2/К3, літій-гепарин */
export function tubeTitle (name) {
  let s = String(name == null ? '' : name);
  s = s.replace(/[КK][23]\s*(?:ЕДТА|EDTA)\s*\/\s*[КK][23]\s*(?:ЕДТА|EDTA)/i, 'ЕДТА К2/К3');
  s = s.replace(/[КK]([23])\s*\/\s*[КK]([23])\s*(?:ЕДТА|EDTA)/i, 'ЕДТА К$1/К$2').replace(/EDTA/gi, 'ЕДТА');
  s = s.replace(/\bLi[-\s]?гепарин/i, 'Літій-гепарин').replace(/\bNa[-\s]?гепарин/i, 'Натрій-гепарин');
  return s.replace(/(\d)\.(\d)/g, '$1,$2');
}
