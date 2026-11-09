/**
 * Права за роллю в інтерфейсі: кнопка/пункт меню, недоступні ролі, не показуються (або блокуються) заздалегідь,
 * а не після відповіді 403. Джерело істини — серверні політики (_policy.Require у сервісах); тут лише відображення.
 *
 * Використання:  mixins: [roleMixin]
 *   v-if="canDo('LAB_ADMIN', 'LAB_TECHNICIAN')"    — адміністратор має право завжди
 *   :disable="!canDo('LAB_DOCTOR')"
 * Ролі: LAB_ADMIN, LAB_DOCTOR, LAB_TECHNICIAN, PHLEBOTOMIST, REGISTRAR, LOGISTICS_COURIER.
 */
export const ROLE_LABELS = {
  LAB_ADMIN: 'Адміністратор лабораторії',
  LAB_DOCTOR: 'Лікар-лаборант',
  LAB_TECHNICIAN: 'Лаборант',
  PHLEBOTOMIST: 'Медсестра пункту забору',
  REGISTRAR: 'Реєстратор',
  LOGISTICS_COURIER: 'Кур’єр'
};

export function roleAllows (role, allowed) {
  if (!role) return true; // співробітник ще не завантажений — не ховаємо нічого, відмову дасть сервер
  if (role === 'LAB_ADMIN' || role === 'Admin') return true;
  return allowed.includes(role);
}

export default {
  computed: {
    currentLabRole () { return this.$store.getters['context/currentRole']; }
  },
  methods: {
    canDo (...allowed) { return roleAllows(this.currentLabRole, allowed); },
    roleLabel (code) { return ROLE_LABELS[code] || code; },
    /** Підказка для заблокованої кнопки: «Доступно: Лікар-лаборант, Адміністратор лабораторії» (без технічних кодів) */
    roleHint (...allowed) {
      const names = Array.from(new Set(allowed.concat('LAB_ADMIN'))).map(c => ROLE_LABELS[c] || c);
      return `Недоступно для вашої ролі. Доступно: ${names.join(', ')}`;
    }
  }
};
