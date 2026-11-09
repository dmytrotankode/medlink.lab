/**
 * Стан лабораторного модуля: зведення робочого листа, стан коннекторів/аналізаторів,
 * доступність API (для банера «API недоступне»), опитування кожні 30 с.
 */
import labApi from '../../services/labApiService';

const POLL_MS = 30000;
let pollTimer = null;
let lastRefreshStart = 0;
const MIN_GAP_MS = 5000;

const state = {
  summary: { pending: 0, needsReview: 0, panic: 0, cito: 0, autoVerifiedToday: 0 },
  connectors: [],
  analyzers: [],
  activeLockouts: [],
  apiOffline: false,
  lastError: null,
  lastRefreshAt: null,
  refreshing: false
};

const getters = {
  panicCount: s => Number(s.summary && s.summary.panic) || 0,
  citoCount: s => Number(s.summary && s.summary.cito) || 0,
  needsReviewCount: s => Number(s.summary && s.summary.needsReview) || 0,
  gatewayOnline: s => s.connectors.filter(c => c.status === 'ACTIVE').length,
  gatewayTotal: s => s.connectors.filter(c => c.status !== 'DISABLED').length,
  analyzersOnline: s => s.analyzers.filter(a => a.isOnline).length,
  lockoutCount: s => s.activeLockouts.length
};

const mutations = {
  SET_SUMMARY (s, v) { s.summary = { ...state.summary, ...(v || {}) }; },
  SET_CONNECTORS (s, v) { s.connectors = Array.isArray(v) ? v : (v && v.items) || []; },
  SET_ANALYZERS (s, v) { s.analyzers = Array.isArray(v) ? v : (v && v.items) || []; },
  SET_LOCKOUTS (s, v) { s.activeLockouts = Array.isArray(v) ? v : (v && v.items) || []; },
  SET_API_OFFLINE (s, v) { s.apiOffline = !!v; },
  SET_LAST_ERROR (s, v) { s.lastError = v; },
  SET_REFRESHING (s, v) { s.refreshing = v; },
  SET_REFRESHED (s) { s.lastRefreshAt = new Date().toISOString(); }
};

const actions = {
  // Не частіше разу на 5 с (сторінки викликають оновлення після кожної дії), без паралельних запусків;
  // force — примусово (кнопка «Оновити», перша загрузка)
  async refreshStatus ({ commit, state: st }, force) {
    const now = Date.now();
    if (st.refreshing || (!force && now - lastRefreshStart < MIN_GAP_MS)) return;
    lastRefreshStart = now;
    commit('SET_REFRESHING', true);
    const results = await Promise.allSettled([
      labApi.worklistSummary(),
      labApi.getConnectors(),
      labApi.getAnalyzers(),
      labApi.qcLockouts(true)
    ]);
    if (results[0].status === 'fulfilled') commit('SET_SUMMARY', results[0].value);
    if (results[1].status === 'fulfilled') commit('SET_CONNECTORS', results[1].value);
    if (results[2].status === 'fulfilled') commit('SET_ANALYZERS', results[2].value);
    if (results[3].status === 'fulfilled') commit('SET_LOCKOUTS', results[3].value);
    commit('SET_REFRESHED');
    commit('SET_REFRESHING', false);
  },

  startPolling ({ dispatch }) {
    if (pollTimer) return;
    dispatch('refreshStatus', true);
    // у фоновій вкладці не опитуємо
    pollTimer = setInterval(() => { if (!document.hidden) dispatch('refreshStatus', true); }, POLL_MS);
  },

  stopPolling () {
    if (pollTimer) {
      clearInterval(pollTimer);
      pollTimer = null;
    }
  }
};

export default { namespaced: true, state, getters, mutations, actions };
