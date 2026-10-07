/**
 * Дії (переходи статусів) замовлення. Бекенд повертає `allowedActions: string[]` у DTO замовлення /
 * рядках worklist та `GET /orders/{id}/transitions`. Кнопки рендеряться для всіх відомих дій;
 * недозволені — вимкнені з підказкою. 403 ProblemDetails (українською) показується у сповіщенні.
 */
export const ORDER_ACTIONS = {
  COLLECT: { label: 'Забрати матеріал', icon: 'colorize', color: 'teal-6', roles: ['PHLEBOTOMIST', 'LAB_TECHNICIAN', 'LAB_ADMIN'], hint: 'Забір проб із чек-листом (медсестра пункту забору)' },
  RECEIVE: { label: 'Прийняти в лабораторії', icon: 'inbox', color: 'cyan-7', roles: ['LAB_TECHNICIAN', 'LAB_DOCTOR', 'LAB_ADMIN'], hint: 'Прийом проб у лабораторії (бракераж)' },
  ENTER_RESULT: { label: 'Ввести результати', icon: 'edit_note', color: 'primary', roles: ['LAB_TECHNICIAN', 'LAB_DOCTOR', 'LAB_ADMIN'], hint: 'Ручне введення результатів на робочому столі' },
  VERIFY: { label: 'Верифікувати', icon: 'how_to_reg', color: 'green-8', roles: ['LAB_DOCTOR', 'LAB_ADMIN'], hint: 'Медична верифікація результатів (лікар-лаборант)' },
  REJECT: { label: 'Відхилити пробу', icon: 'block', color: 'negative', roles: ['LAB_TECHNICIAN', 'LAB_DOCTOR', 'LAB_ADMIN'], hint: 'Бракераж проби з причиною та повторним замовленням' },
  RERUN: { label: 'Повторити тест', icon: 'replay', color: 'purple-6', roles: ['LAB_TECHNICIAN', 'LAB_DOCTOR', 'LAB_ADMIN'], hint: 'Поставити тест у чергу повтору' },
  RELEASE: { label: 'Видати результати', icon: 'verified', color: 'positive', roles: ['LAB_DOCTOR', 'LAB_ADMIN', 'REGISTRAR'], hint: 'Видача бланка та сповіщення пацієнта' },
  CANCEL: { label: 'Скасувати замовлення', icon: 'cancel', color: 'grey-7', roles: ['REGISTRAR', 'LAB_ADMIN', 'LAB_DOCTOR'], hint: 'Скасування з причиною' },
  REOPEN: { label: 'Повернути в роботу', icon: 'undo', color: 'orange-8', roles: ['LAB_DOCTOR', 'LAB_ADMIN'], hint: 'Повторне відкриття з аудитом' }
};

export const ACTION_ORDER = ['COLLECT', 'RECEIVE', 'ENTER_RESULT', 'VERIFY', 'RERUN', 'REJECT', 'RELEASE', 'REOPEN', 'CANCEL'];

/** Витягує список дозволених дій із DTO або відповіді /transitions */
export function allowedActionsOf (dto) {
  if (!dto) return [];
  if (Array.isArray(dto)) return dto.map(a => (typeof a === 'string' ? a : a.action || a.code)).filter(Boolean);
  if (Array.isArray(dto.allowedActions)) return dto.allowedActions;
  if (Array.isArray(dto.transitions)) return dto.transitions.map(t => (typeof t === 'string' ? t : t.action || t.code)).filter(Boolean);
  if (Array.isArray(dto.actions)) return dto.actions.map(a => (typeof a === 'string' ? a : a.action || a.code)).filter(Boolean);
  return null; // бекенд не повернув — визначаємо локально за статусом
}

/** Локальний fallback, якщо API не надає allowedActions (за статусами з контракту) */
export function fallbackActions (order) {
  if (!order) return [];
  const s = order.status;
  const map = {
    NEW: ['COLLECT', 'CANCEL'],
    COLLECTED: ['RECEIVE', 'REJECT', 'CANCEL'],
    IN_TRANSIT: ['RECEIVE', 'REJECT', 'CANCEL'],
    RECEIVED: ['ENTER_RESULT', 'REJECT', 'RERUN', 'CANCEL'],
    IN_PROGRESS: ['ENTER_RESULT', 'VERIFY', 'RERUN', 'REJECT', 'CANCEL'],
    COMPLETED: ['VERIFY', 'RELEASE', 'RERUN', 'REOPEN'],
    RELEASED: ['REOPEN'],
    CANCELLED: ['REOPEN'],
    REJECTED: ['REOPEN']
  };
  return map[s] || [];
}

export function resolveActions (order, transitions) {
  const fromTransitions = allowedActionsOf(transitions);
  if (fromTransitions && fromTransitions.length) return { actions: fromTransitions, source: 'api' };
  const fromDto = allowedActionsOf(order);
  if (fromDto) return { actions: fromDto, source: 'api' };
  return { actions: fallbackActions(order), source: 'local' };
}
