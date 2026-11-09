/**
 * Єдина таблиця «маршрут → ролі, яким екран потрібен».
 * Джерело: серверні політики (_policy.Require у сервісах API) і ТЗ розд. 3.2 «Екрани за замовчуванням».
 * Адміністратор лабораторії (LAB_ADMIN) бачить усе завжди (див. roleAllows у mixins/roleMixin.js).
 *
 * Правило: екран ховається, лише якщо жодна дія на ньому не доступна ролі за серверною політикою
 * (інакше роль зайшла б на сторінку, де нічого не може). Маршрути, яких тут немає, доступні всім.
 *
 * Використання:
 *   routeAllowed(routeName, role)  — чи показувати пункт меню / пускати на екран
 *   ACCESS_DENIED_TEXT             — тексти екрана «Недостатньо прав»
 */
import { roleAllows } from '../mixins/roleMixin';

const ADMIN = 'LAB_ADMIN';
const DOCTOR = 'LAB_DOCTOR';
const TECH = 'LAB_TECHNICIAN';
const PHLEB = 'PHLEBOTOMIST';
const REG = 'REGISTRAR';
const COURIER = 'LOGISTICS_COURIER';

// Набори ролей — повторюють статичні масиви в сервісах API.
const CREATORS = [ADMIN, REG, DOCTOR, PHLEB, TECH]; // OrderService.CreatorRoles
const LAB_STAFF = [ADMIN, DOCTOR, TECH]; // Roles у Biobank/Microbiology/Qc/SampleProcessing, DistributorRoles, Operators
const CASHIERS = [ADMIN, REG, PHLEB]; // BillingService.Cashiers
const PATH_ALL = [ADMIN, DOCTOR, TECH, REG]; // IntakeRoles + PathologistRoles + HistotechRoles

/** routeName → дозволені ролі (LAB_ADMIN додається завжди) */
export const ROUTE_ROLES = {
  // ----- Процеси -----
  'lab-dashboard': null, // усі
  'lab-orders': CREATORS,
  'lab-order-card': [...CREATORS, COURIER], // кур'єр відкриває замовлення за штрихкодом пробірки
  'lab-referrals': CREATORS,
  'lab-incoming-referrals': [DOCTOR, TECH, REG], // розподіл: Admin/Doctor/Technician; реєстратор приймає й скасовує
  'lab-work': LAB_STAFF,
  'lab-reports': [DOCTOR, TECH, REG],
  'lab-ehealth': [DOCTOR], // SubmitRoles у EhealthSignService
  'lab-order-matrix': CREATORS,
  'lab-phlebotomy': [PHLEB, REG],
  'lab-sample-processing': [...LAB_STAFF, PHLEB],
  'lab-section-journal': LAB_STAFF,
  'lab-logistics': [COURIER, PHLEB, TECH, REG, DOCTOR], // маніфести: Admin/Courier/Phlebotomist; контейнери: ContainerPackers
  'lab-clients': [REG, TECH],
  'lab-send-out': [DOCTOR, TECH, REG, COURIER],
  'lab-workstation': LAB_STAFF,
  'lab-validation': LAB_STAFF,
  'lab-qc': LAB_STAFF,
  'lab-biobank': LAB_STAFF,
  'lab-reagents': LAB_STAFF,
  'lab-microbiology': LAB_STAFF,
  'lab-culture-card': LAB_STAFF,
  'lab-analytics-tat': [DOCTOR], // ТЗ 3.2: аналітика — лікар-лаборант

  // ----- Патоморфологія / цитологія -----
  // реєстратор лише приймає матеріал (IntakeRoles); журнал справ, термінові й архів — для лікаря й лаборанта
  'lab-path-cases': [DOCTOR, TECH],
  'lab-cyto-cases': [DOCTOR, TECH],
  'lab-path-intake': [REG, TECH, DOCTOR],
  'lab-path-cito': [DOCTOR, TECH],
  'lab-path-archive': [DOCTOR, TECH],
  'lab-path-dictionaries': [], // лише адміністратор
  'lab-path-case': PATH_ALL,

  // ----- Довідники -----
  'lab-pricing': [...CASHIERS, DOCTOR],
  'lab-norms': LAB_STAFF,
  'lab-dictionaries': LAB_STAFF,
  'lab-dictionary': LAB_STAFF,
  'lab-sections': LAB_STAFF,

  // ----- Інтеграції -----
  'lab-analyzers': LAB_STAFF,
  'lab-analyzer-services': [DOCTOR], // EditorRoles: Admin/Doctor
  'lab-analyzer-messages': LAB_STAFF,
  'lab-import': LAB_STAFF,

  // ----- Адміністрування (лише адміністратор лабораторії) -----
  'lab-admin-settings': [],
  'lab-admin-users': [],
  'lab-audit': []
};

/** Дії всередині екранів: код → ролі (LAB_ADMIN завжди). Використовується в canDo(...ACTION_ROLES.x). */
export const ACTION_ROLES = {
  cancelInMis: [REG, DOCTOR], // OrderActions.Cancel: Admin/Registrar/Doctor
  misEmulator: [], // тестовий інструмент — лише адміністратор
  ehealthProcessNow: [DOCTOR], // SubmitRoles
  ehealthSign: [DOCTOR], // SubmitRoles
  ehealthRetry: [DOCTOR],
  priceEdit: [], // BillingService: «Прайс-листи» — лише Admin
  payerEdit: [], // «Довідник платників» — лише Admin
  takePayment: [REG, PHLEB], // Cashiers
  changePayer: [REG, PHLEB, DOCTOR],
  issueInvoice: [REG, PHLEB, DOCTOR]
};

export function routeAllowed (routeName, role) {
  if (!routeName || !Object.prototype.hasOwnProperty.call(ROUTE_ROLES, routeName)) return true;
  const allowed = ROUTE_ROLES[routeName];
  if (allowed === null) return true;
  return roleAllows(role, allowed);
}

export const ACCESS_DENIED_TEXT = {
  title: 'Недостатньо прав',
  message: 'Цей розділ недоступний для вашої ролі. Оберіть іншого співробітника у верхньому правому куті або поверніться на головну сторінку.',
  button: 'На головну'
};
