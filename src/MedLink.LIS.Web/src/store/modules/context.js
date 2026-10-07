/**
 * Контекст користувача: поточний співробітник («Працюю як…»), список співробітників, лабораторія.
 * Автентифікації немає — employeeId передається заголовком X-MedLink-Employee-Id (boot/axios.js).
 */
import labApi from '../../services/labApiService';

const STORAGE_KEY = 'medlink.lis.employeeId';

function readStored () {
  try {
    return window.localStorage.getItem(STORAGE_KEY) || null;
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

const state = {
  employeeId: readStored(),
  employees: [],
  employee: null,
  lab: null,
  loaded: false,
  loading: false
};

const getters = {
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

  async init ({ dispatch }) {
    try {
      await dispatch('loadEmployees');
    } catch (e) { /* банер API покаже стан */ }
    try {
      await dispatch('loadMe');
    } catch (e) { /* ignore */ }
  },

  async switchEmployee ({ commit, dispatch }, id) {
    commit('SET_EMPLOYEE_ID', id);
    try {
      await dispatch('loadMe');
    } catch (e) { /* ignore */ }
  }
};

export default { namespaced: true, state, getters, mutations, actions };
