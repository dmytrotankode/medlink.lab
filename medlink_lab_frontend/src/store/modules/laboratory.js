/**
 * MedLink LIS - Vuex Store Module
 * Manages reactive state for all Laboratory Information System modules.
 * Matching evomis Vuex architecture.
 * Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
 */

import { LabApiService } from '../../services/labApiService';
import {
  mockOrders,
  mockSamples,
  mockWorklistResults,
  mockQcData,
  mockMicrobiologyData,
  mockReagents,
  mockBiomaterials,
  mockAnalyzers
} from '../../services/mockData';

const state = {
  orders: [],
  selectedOrder: null,
  samples: [],
  worklist: [],
  qcData: null,
  microbiology: null,
  reagents: [],
  biomaterials: [],
  analyzers: [],
  activeFilter: 'ALL',
  isLoading: false,
  error: null
};

const getters = {
  allOrders: state => state.orders,
  urgentOrders: state => state.orders.filter(o => o.priority === 'CITO'),
  panicResults: state => state.worklist.filter(r => r.flag === 'PANIC_HIGH' || r.flag === 'PANIC_LOW'),
  pendingVerificationCount: state => state.worklist.filter(r => r.status === 'PENDING_VERIFY').length,
  qcStatus: state => state.qcData ? state.qcData.currentStatus : 'NORMAL',
  reagentsLowStock: state => state.reagents.filter(r => r.status === 'LOW_STOCK' || r.status === 'EXPIRED'),
  onlineAnalyzers: state => state.analyzers.filter(a => a.status === 'ONLINE')
};

const mutations = {
  SET_LOADING(state, val) {
    state.isLoading = val;
  },
  SET_ORDERS(state, orders) {
    state.orders = orders;
  },
  SET_SELECTED_ORDER(state, order) {
    state.selectedOrder = order;
  },
  SET_SAMPLES(state, samples) {
    state.samples = samples;
  },
  UPDATE_SAMPLE_STATUS(state, { barcode, status }) {
    const s = state.samples.find(item => item.barcode === barcode);
    if (s) s.status = status;
  },
  SET_WORKLIST(state, results) {
    state.worklist = results;
  },
  UPDATE_RESULT_STATUS(state, { id, status, comment }) {
    const r = state.worklist.find(item => item.id === id);
    if (r) {
      r.status = status;
      if (comment) r.comment = comment;
    }
  },
  SET_QC_DATA(state, qc) {
    state.qcData = qc;
  },
  SET_MICROBIOLOGY(state, mb) {
    state.microbiology = mb;
  },
  SET_REAGENTS(state, reagents) {
    state.reagents = reagents;
  },
  SET_BIOMATERIALS(state, biomaterials) {
    state.biomaterials = biomaterials;
  },
  SET_ANALYZERS(state, analyzers) {
    state.analyzers = analyzers;
  },
  SET_ERROR(state, error) {
    state.error = error;
  }
};

const actions = {
  async fetchOrders({ commit }, params = {}) {
    commit('SET_LOADING', true);
    try {
      const resp = await LabApiService.getOrders(params);
      commit('SET_ORDERS', resp.data);
    } catch (e) {
      commit('SET_ERROR', e.message);
    } finally {
      commit('SET_LOADING', false);
    }
  },

  async fetchSamples({ commit }, params = {}) {
    commit('SET_LOADING', true);
    try {
      const resp = await LabApiService.getSamples(params);
      commit('SET_SAMPLES', resp.data);
    } catch (e) {
      commit('SET_ERROR', e.message);
    } finally {
      commit('SET_LOADING', false);
    }
  },

  async collectSample({ commit }, { barcode, checklist }) {
    try {
      const resp = await LabApiService.collectSample(barcode, checklist);
      commit('UPDATE_SAMPLE_STATUS', { barcode, status: 'COLLECTED' });
      return resp.data;
    } catch (e) {
      commit('SET_ERROR', e.message);
      throw e;
    }
  },

  async fetchWorklist({ commit }, params = {}) {
    commit('SET_LOADING', true);
    try {
      const resp = await LabApiService.getWorklist(params);
      commit('SET_WORKLIST', resp.data);
    } catch (e) {
      commit('SET_ERROR', e.message);
    } finally {
      commit('SET_LOADING', false);
    }
  },

  async verifyResult({ commit }, { id, comment }) {
    try {
      await LabApiService.verifyResult(id, comment);
      commit('UPDATE_RESULT_STATUS', { id, status: 'VERIFIED', comment });
    } catch (e) {
      commit('SET_ERROR', e.message);
      throw e;
    }
  },

  async fetchQcData({ commit }, analyzerId) {
    try {
      const resp = await LabApiService.getQcLeveyJennings(analyzerId);
      commit('SET_QC_DATA', resp.data);
    } catch (e) {
      commit('SET_ERROR', e.message);
    }
  },

  async initMockData({ commit }) {
    commit('SET_ORDERS', mockOrders);
    commit('SET_SAMPLES', mockSamples);
    commit('SET_WORKLIST', mockWorklistResults);
    commit('SET_QC_DATA', mockQcData);
    commit('SET_MICROBIOLOGY', mockMicrobiologyData);
    commit('SET_REAGENTS', mockReagents);
    commit('SET_BIOMATERIALS', mockBiomaterials);
    commit('SET_ANALYZERS', mockAnalyzers);
  }
};

export default {
  namespaced: true,
  state,
  getters,
  mutations,
  actions
};
