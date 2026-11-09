/**
 * HTTP-клієнт MedLink LIS (axios). Базовий URL — process.env.API_BASE (/api/v1/lab).
 * Інтерсептори (заголовок X-MedLink-Employee-Id, обробка помилок) підключаються у boot/axios.js,
 * де є доступ до Vuex store.
 */
import axios from 'axios';

export const API_BASE = process.env.API_BASE || '/api/v1/lab';

export const http = axios.create({
  baseURL: API_BASE,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json'
  }
});

/** Англійські тексти стандартної валідації → зрозумілі українські (запасний варіант; основне робить сервер). */
function humanizeFieldError (text) {
  const t = String(text || '');
  if (/could not be converted/i.test(t)) return 'Невірний формат значення: введіть число';
  if (/is required/i.test(t)) return 'Не заповнено обов’язкове поле';
  if (t && !/[А-Яа-яІіЇїЄєҐґ]/.test(t)) return 'Некоректне значення';
  return t;
}

/**
 * Перетворює помилку axios у зручний для UI вигляд (RFC 7807 ProblemDetails).
 */
export function describeApiError (error) {
  if (!error) return { message: 'Невідома помилка', offline: false, status: 0, fields: {} };
  if (error.response) {
    const data = error.response.data || {};
    const fields = data.errors || {};
    let message = data.detail || data.title || error.response.statusText || `HTTP ${error.response.status}`;
    // технічні імена полів (macro, assistantId, $.amount) користувачу не показуємо; текст поля додаємо, лише якщо його ще немає в повідомленні
    const fieldMessages = Object.keys(fields)
      .map(k => [].concat(fields[k]).map(humanizeFieldError).join(', '))
      .filter((text, i, all) => text && !message.includes(text) && all.indexOf(text) === i);
    if (fieldMessages.length) message += ': ' + fieldMessages.join('; ');
    return { message, offline: false, status: error.response.status, fields, raw: data };
  }
  if (error.request) {
    return {
      message: 'API лабораторії недоступне. Перевірте, що сервіс MedLink.LIS.Api запущено.',
      offline: true,
      status: 0,
      fields: {}
    };
  }
  return { message: error.message || 'Помилка запиту', offline: false, status: 0, fields: {} };
}

export default http;
