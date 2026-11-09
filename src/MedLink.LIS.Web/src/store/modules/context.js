/**
 * Контекст користувача: поточний співробітник («Працюю як…»), список співробітників, лабораторія.
 * Автентифікації немає — employeeId передається заголовком X-MedLink-Employee-Id (boot/axios.js).
 */
import labApi from '../../services/labApiService';

const STORAGE_KEY = 'medlink.lis.employeeId';

function readStored () {
  try {
    const v = window.localStorage.getItem(STORAGE_KEY) || null;
    // До вирівнювання з MedLink демо-ключі мали вигляд emp-0000-…; тепер — uuid 0e000000-…
    return v && v.startsWith('emp-0000-') ? v.replace('emp-0000-', '0e000000-') : v;
  } catch (e) {
    return null;
  }
}

function writeStored (value) {
  try {
    if (value) window.localStorage.setItem(STORAGE_KEY, value);
    else window.localStorage.removeItem(STORAGE_KEY);
  } catch (e) { /* private mode */ }
}

const HINTS_KEY = 'medlink.lis.shellHints';
function readHints () {
  try { return window.localStorage.getItem(HINTS_KEY) !== '0'; } catch (e) { return true; }
}

const state = {
  employeeId: readStored(),
  employees: [],
  employee: null,
  lab: null,
  labSettings: null, // GET /settings/lab (принтер етикеток, бланки, retention)
  loaded: false,
  loading: false,
  // Режим входу з /api/v1/lab/auth/config: Oidc — особу визначає токен Identity, «Працюю як…» вимкнено
  auth: { mode: 'None', runAs: true, bridge: false, shellHints: true, error: null },
  // Особистий перемикач підписів «Ця частина належить МедЛінк» (діє, лише якщо підписи не вимкнено конфігом)
  hintsUser: readHints()
};

const getters = {
  runAsEnabled: s => s.auth.runAs !== false,
  shellEnabled: s => s.auth.shellHints !== false,
  shellHints: s => s.auth.shellHints !== false && s.hintsUser,
  oidcMode: s => s.auth.mode === 'Oidc',
  currentEmployee: s => s.employee || s.employees.find(e => e.id === s.employeeId) || null,
  currentRole: (s, g) => (g.currentEmployee && (g.currentEmployee.labRole || g.currentEmployee.role)) || null,
  labName: s => (s.lab && s.lab.name) || 'Клініко-діагностична лабораторія',
  employeeOptions: s => s.employees.map(e => ({
    value: e.id,
    label: e.fullName || e.name || e.id,
    caption: e.position || e.labRole || '',
    labRole: e.labRole
  }))
};

const mutations = {
  SET_EMPLOYEE_ID (s, id) {
    s.employeeId = id;
    writeStored(id);
  },
  SET_EMPLOYEES (s, list) { s.employees = Array.isArray(list) ? list : (list && list.items) || []; },
  SET_ME (s, me) {
    s.employee = (me && me.employee) || null;
    s.lab = (me && me.lab) || null;
    s.loaded = true;
  },
  SET_LAB_SETTINGS (s, v) { s.labSettings = v || null; },
  SET_AUTH (s, v) { s.auth = Object.assign({ mode: 'None', runAs: true, bridge: false, shellHints: true, error: null }, v || {}); },
  SET_HINTS_USER (s, v) {
    s.hintsUser = !!v;
    try { window.localStorage.setItem(HINTS_KEY, v ? '1' : '0'); } catch (e) { /* private mode */ }
  },
  SET_LOADING (s, v) { s.loading = v; }
};

const actions = {
  async loadEmployees ({ commit, state: s, dispatch }) {
    const list = await labApi.getEmployees();
    commit('SET_EMPLOYEES', list);
    const items = Array.isArray(list) ? list : (list && list.items) || [];
    if (!s.employeeId && items.length) {
      // За замовчуванням — перший співробітник (адміністратор лабораторії у сіді)
      commit('SET_EMPLOYEE_ID', items[0].id);
    } else if (s.employeeId && items.length && !items.find(e => e.id === s.employeeId)) {
      commit('SET_EMPLOYEE_ID', items[0].id);
    }
    return items;
  },

  async loadMe ({ commit }) {
    commit('SET_LOADING', true);
    try {
      const me = await labApi.getMe();
      commit('SET_ME', me);
      return me;
    } finally {
      commit('SET_LOADING', false);
    }
  },

  async loadLabSettings ({ commit }) {
    try {
      const s = await labApi.labSettings();
      commit('SET_LAB_SETTINGS', s);
      return s;
    } catch (e) {
      return null;
    }
  },

  async init ({ dispatch, getters: g }) {
    try {
      if (g.runAsEnabled) await dispatch('loadEmployees'); // у режимі Oidc список для перемикача не потрібен
    } catch (e) { /* банер API покаже стан */ }
    try {
      await dispatch('loadMe');
    } catch (e) { /* ignore */ }
    dispatch('loadLabSettings');
  },

  async switchEmployee ({ commit, dispatch }, id) {
    commit('SET_EMPLOYEE_ID', id);
    try {
      await dispatch('loadMe');
    } catch (e) { /* ignore */ }
  }
};

export default { namespaced: true, state, getters, mutations, actions };
