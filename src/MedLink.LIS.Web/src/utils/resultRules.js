/**
 * Правила доступності дій над результатом (відображення серверних політик ResultPipelineService / LisStateMachine).
 * Повертають порожній рядок, якщо дія доступна, інакше — зрозуміле українське пояснення (без технічних кодів).
 * Адміністратор лабораторії має право на все; невідома роль (ще не завантажена) — не блокуємо, відмову дасть сервер.
 */
import { ROLE_LABELS, roleAllows } from '../mixins/roleMixin';

export const ENTER_ROLES = ['LAB_DOCTOR', 'LAB_TECHNICIAN'];
export const VERIFY_ROLES = ['LAB_DOCTOR'];
export const REJECT_ROLES = ['LAB_DOCTOR', 'LAB_TECHNICIAN'];
export const DELETE_ROLES = ['LAB_DOCTOR'];
export const CALL_ROLES = ['LAB_DOCTOR', 'LAB_TECHNICIAN'];

/** Статуси замовлення, у яких сервер дозволяє введення результатів */
const ENTER_ORDER_STATUSES = ['RECEIVED', 'IN_PROGRESS', 'PARTIALLY_COMPLETED'];
const NOT_RECEIVED = ['NEW', 'COLLECTED', 'IN_TRANSIT'];

export function roleNames (allowed) {
  return Array.from(new Set(allowed.concat('LAB_ADMIN'))).map(c => ROLE_LABELS[c] || c).join(', ');
}

export function roleBlockReason (role, allowed) {
  return roleAllows(role, allowed) ? '' : `Недоступно для вашої ролі. Доступно: ${roleNames(allowed)}`;
}

/** Чому не можна ввести результат у рядок черги (роль або стан замовлення) */
export function enterBlockReason (row, role) {
  const byRole = roleBlockReason(role, ENTER_ROLES);
  if (byRole) return byRole;
  if (row && row.isCalculated) return 'Розрахунковий показник обчислюється автоматично за формулою — введіть вхідні показники';
  const st = row && row.orderStatus;
  if (!st || ENTER_ORDER_STATUSES.includes(st)) return '';
  if (NOT_RECEIVED.includes(st)) return `Замовлення ${row.orderNumber || ''} ще не прийнято в лабораторії. Спершу відмітьте забір і прийом проби, потім вводьте результат`.replace('  ', ' ');
  if (st === 'COMPLETED' || st === 'RELEASED') return 'Замовлення вже виконано. Для зміни поверніть його в роботу в картці замовлення';
  return 'Для цього замовлення введення результатів неможливе';
}

export function verifyBlockReason (role) {
  return roleBlockReason(role, VERIFY_ROLES) ? 'Верифікує лише лікар-лаборант (або адміністратор лабораторії)' : '';
}
