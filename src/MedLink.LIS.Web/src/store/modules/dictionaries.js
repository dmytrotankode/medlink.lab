/**
 * Кеш довідників (/dictionaries/{name}) з TTL. Використовується селектами в усіх формах.
 */
import labApi from '../../services/labApiService';

const TTL_MS = 5 * 60 * 1000;

const state = {
  cache: {} // name -> { items, loadedAt }
};

const getters = {
  items: s => name => (s.cache[name] && s.cache[name].items) || [],
  byId: s => (name, id) => ((s.cache[name] && s.cache[name].items) || []).find(x => String(x.id) === String(id)) || null,
  options: s => (name, labelField = 'name', valueField = 'id') =>
    ((s.cache[name] && s.cache[name].items) || []).map(x => ({ value: x[valueField], label: x[labelField], item: x }))
};

const mutations = {
  SET (s, { name, items }) {
    s.cache = { ...s.cache, [name]: { items: Array.isArray(items) ? items : (items && items.items) || [], loadedAt: Date.now() } };
  },
  INVALIDATE (s, name) {
    const next = { ...s.cache };
    delete next[name];
    s.cache = next;
  }
};

const actions = {
  async load ({ commit, state: s }, payload) {
    const name = typeof payload === 'string' ? payload : payload.name;
    const force = typeof payload === 'object' && payload.force;
    const params = (typeof payload === 'object' && payload.params) || {};
    const cached = s.cache[name];
    if (!force && cached && Date.now() - cached.loadedAt < TTL_MS) return cached.items;
    const items = await labApi.dictList(name, params);
    commit('SET', { name, items });
    return s.cache[name].items;
  },
  async loadMany ({ dispatch }, names) {
    const res = await Promise.allSettled(names.map(n => dispatch('load', n)));
    return res;
  },
  invalidate ({ commit }, name) {
    commit('INVALIDATE', name);
  }
};

export default { namespaced: true, state, getters, mutations, actions };
