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
  return `${formatNumber(value, 2)} грн`;
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

export function referenceDisplay (row) {
  if (!row) return '';
  if (row.referenceDisplay) return row.referenceDisplay;
  if (row.normText) return row.normText;
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
