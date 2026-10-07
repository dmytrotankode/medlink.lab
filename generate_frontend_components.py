# -*- coding: utf-8 -*-
"""
Generator script for MedLink LIS Vue 2 / Quasar components, router, and store.
Matches evomis App.View architecture and coding standards.
Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
"""

import os
import sys

BASE_DIR = r"C:\__MEDLINK___\LABA\medlink_lab_frontend\src"

def ensure_dir(path):
    if not os.path.exists(path):
        os.makedirs(path, exist_ok=True)

# 1. Router: laboratoryRoutes.js
def create_router():
    content = """/**
 * MedLink LIS - Vue Router Configuration
 * Modular route configuration matching evomis/src/App.View routing pattern.
 * Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
 */

export default [
  {
    path: '/laboratory',
    component: () => import('@/layouts/baseLayout/BaseLayout.vue'),
    meta: {
      title: 'Лабораторія (ЛІС 3.0)'
    },
    children: [
      {
        path: '',
        redirect: 'workstation'
      },
      // 1. Phlebotomy & Pre-analytics
      {
        path: 'phlebotomy',
        name: 'lab-phlebotomy',
        meta: {
          title: 'Пункт забору біоматеріалу',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Пункт забору' }]
        },
        component: () => import('@/pages/laboratory/PhlebotomyStation.vue')
      },
      // 2. Logistics & Thermocontainers
      {
        path: 'logistics',
        name: 'lab-logistics',
        meta: {
          title: 'Логістика зразків та термоконтроль',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Логістика' }]
        },
        component: () => import('@/pages/laboratory/SpecimenLogistics.vue')
      },
      // 3. Lab Workstation & Results
      {
        path: 'workstation',
        name: 'lab-workstation',
        meta: {
          title: 'Робочий стіл лаборанта (Журнал досліджень)',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Робочий стіл' }]
        },
        component: () => import('@/pages/laboratory/LabWorkstation.vue')
      },
      // 4. Validation & Panic Alerts
      {
        path: 'validation',
        name: 'lab-validation',
        meta: {
          title: 'Валідація результатів & Панічні алерти',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Валідація & Паніка' }]
        },
        component: () => import('@/pages/laboratory/ValidationPanic.vue')
      },
      // 5. Quality Control (Westgard / Levey-Jennings)
      {
        path: 'quality-control',
        name: 'lab-qc',
        meta: {
          title: 'Внутрішній контроль якості (ВКЯ)',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Контроль якості' }]
        },
        component: () => import('@/pages/laboratory/QualityControl.vue')
      },
      // 6. Patient Portal
      {
        path: 'patient-portal',
        name: 'lab-patient-portal',
        meta: {
          title: 'Кабінет пацієнта (Результати досліджень)',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Кабінет пацієнта' }]
        },
        component: () => import('@/pages/laboratory/PatientPortal.vue')
      },
      // 7. Biobank Archive
      {
        path: 'biobank',
        name: 'lab-biobank',
        meta: {
          title: 'Біобанк та архів зразків',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Біобанк' }]
        },
        component: () => import('@/pages/laboratory/BiobankArchive.vue')
      },
      // 8. Reagents & Inventory
      {
        path: 'reagents',
        name: 'lab-reagents',
        meta: {
          title: 'Склад реактивів та калібраторів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Реактиви' }]
        },
        component: () => import('@/pages/laboratory/ReagentInventory.vue')
      },
      // 9. Microbiology & Culture
      {
        path: 'microbiology',
        name: 'lab-microbiology',
        meta: {
          title: 'Бактеріологія та антибіотикограма',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Мікробіологія' }]
        },
        component: () => import('@/pages/laboratory/MicrobiologyCulture.vue')
      },
      // 10. Analytics & TAT
      {
        path: 'analytics-tat',
        name: 'lab-analytics-tat',
        meta: {
          title: 'Операційна аналітика та TAT',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Аналітика & TAT' }]
        },
        component: () => import('@/pages/laboratory/LabAnalyticsTat.vue')
      },
      // 11. Analyzer Monitor Gateway
      {
        path: 'analyzer-monitor',
        name: 'lab-analyzer-monitor',
        meta: {
          title: 'Монітор шлюзу аналізаторів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Шлюз аналізаторів' }]
        },
        component: () => import('@/pages/laboratory/AnalyzerMonitor.vue')
      },
      // 12. Norms & Methodologies
      {
        path: 'norms-methodologies',
        name: 'lab-norms',
        meta: {
          title: 'Норми, референси та методики',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Норми і методики' }]
        },
        component: () => import('@/pages/laboratory/NormsMethodologies.vue')
      },
      // Dictionaries Subroutes
      {
        path: 'dictionaries/biomaterials',
        name: 'dict-biomaterials',
        meta: {
          title: 'Довідник біоматеріалів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Довідники', path: '/laboratory/dictionaries' }, { name: 'Біоматеріали' }]
        },
        component: () => import('@/pages/dictionaries/BiomaterialsDict.vue')
      },
      {
        path: 'dictionaries/tube-types',
        name: 'dict-tube-types',
        meta: {
          title: 'Довідник пробірок та контейнерів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Довідники', path: '/laboratory/dictionaries' }, { name: 'Типи пробірок' }]
        },
        component: () => import('@/pages/dictionaries/TubeTypesDict.vue')
      },
      {
        path: 'dictionaries/analyzer-types',
        name: 'dict-analyzer-types',
        meta: {
          title: 'Довідник типів аналізаторів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Довідники', path: '/laboratory/dictionaries' }, { name: 'Моделі аналізаторів' }]
        },
        component: () => import('@/pages/dictionaries/AnalyzerTypesDict.vue')
      },
      {
        path: 'dictionaries/parameters',
        name: 'dict-lab-parameters',
        meta: {
          title: 'Довідник лабораторних показників',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Довідники', path: '/laboratory/dictionaries' }, { name: 'Показники і профілі' }]
        },
        component: () => import('@/pages/dictionaries/LabParametersDict.vue')
      }
    ]
  }
];
"""
    dest = os.path.join(BASE_DIR, "router", "laboratoryRoutes.js")
    with open(dest, "w", encoding="utf-8") as f:
        f.write(content)
    print("Created:", dest)

# 2. Store: laboratory.js
def create_store():
    content = """/**
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
"""
    dest_dir = os.path.join(BASE_DIR, "store", "modules")
    ensure_dir(dest_dir)
    dest = os.path.join(dest_dir, "laboratory.js")
    with open(dest, "w", encoding="utf-8") as f:
        f.write(content)
    print("Created:", dest)

# 3. Menu integration helper for baseElements
def create_menu_drawer():
    content = """<!--
  MedLink Menu Drawer Component with Laboratory Submenu Extension
  Matches evomis/src/App.View/src/components/baseElements/menuDrawer.vue
  Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
-->
<template>
  <q-drawer
    data-testid="labMenuDrawer"
    :value="drawer"
    :width="230"
    show-if-above
    :mini="miniState"
    :breakpoint="980"
    persistent
    content-class="no-scroll text-grey-1 shadow-2 nav-drawer bg-grey-9"
  >
    <div class="drawer-header q-pa-sm text-center bg-grey-10">
      <div v-if="!miniState" class="row items-center no-wrap q-gutter-x-sm">
        <q-icon name="fas fa-flask" color="cyan-4" size="22px" />
        <div class="text-subtitle2 text-weight-bold text-white text-left ellipsis">
          МедЛінк ЛІС 3.0
          <div class="text-caption text-grey-5" style="font-size: 10px;">Клінічна Лабораторія</div>
        </div>
      </div>
      <q-icon v-else name="fas fa-flask" color="cyan-4" size="24px" />
    </div>

    <q-scroll-area class="nav_scroll-area">
      <q-list dense padding>
        <!-- Standard MedLink Items -->
        <q-item clickable v-ripple :to="{ path: '/dashboard' }">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon class="icon ico-home text-white" size="20px" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label class="text-white">Головна</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Головна</q-tooltip>
        </q-item>

        <q-separator class="q-my-xs bg-grey-8" />

        <!-- HEADER: ЛАБОРАТОРІЯ -->
        <q-item-label header class="text-uppercase text-cyan-3 text-weight-bold" style="font-size: 11px; letter-spacing: 0.5px;">
          <span v-if="!miniState">Лабораторія (ЛІС)</span>
          <q-icon v-else name="science" size="18px" />
        </q-item-label>

        <!-- 1. Робочий стіл лаборанта -->
        <q-item clickable v-ripple :to="{ name: 'lab-workstation' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-microscope" size="18px" color="cyan-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Робочий стіл</q-item-label>
          </q-item-section>
          <q-badge v-if="!miniState" color="teal-6" text-color="white" label="15" />
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Робочий стіл лаборанта</q-tooltip>
        </q-item>

        <!-- 2. Валідація та Паніка -->
        <q-item clickable v-ripple :to="{ name: 'lab-validation' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-user-check" size="18px" color="amber-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Валідація & Паніка</q-item-label>
          </q-item-section>
          <q-badge v-if="!miniState" color="negative" text-color="white" label="1 CITO" />
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Валідація & Панічні алерти</q-tooltip>
        </q-item>

        <!-- 3. Внутрішній контроль якості (ВКЯ) -->
        <q-item clickable v-ripple :to="{ name: 'lab-qc' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-chart-line" size="18px" color="purple-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Контроль якості</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">ВКЯ (Вестгард / Леві-Дженнінгс)</q-tooltip>
        </q-item>

        <!-- 4. Бактеріологія -->
        <q-item clickable v-ripple :to="{ name: 'lab-microbiology' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-bacterium" size="18px" color="green-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Мікробіологія</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Бактеріологія & Антибіотикограма</q-tooltip>
        </q-item>

        <!-- 5. Біобанк -->
        <q-item clickable v-ripple :to="{ name: 'lab-biobank' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-snowflake" size="18px" color="light-blue-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Біобанк & Архів</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Біобанк та архів зразків</q-tooltip>
        </q-item>

        <!-- 6. Склад реактивів -->
        <q-item clickable v-ripple :to="{ name: 'lab-reagents' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-boxes" size="18px" color="orange-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Склад реактивів</q-item-label>
          </q-item-section>
          <q-badge v-if="!miniState" color="warning" text-color="dark" label="2 увага" />
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Склад реактивів та калібраторів</q-tooltip>
        </q-item>

        <!-- 7. Шлюз аналізаторів -->
        <q-item clickable v-ripple :to="{ name: 'lab-analyzer-monitor' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-network-wired" size="18px" color="teal-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Шлюз приладів</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Монітор підключення аналізаторів (ASTM/HL7)</q-tooltip>
        </q-item>

        <!-- 8. Аналітика & TAT -->
        <q-item clickable v-ripple :to="{ name: 'lab-analytics-tat' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-chart-pie" size="18px" color="indigo-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Аналітика & TAT</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Операційна аналітика та TAT</q-tooltip>
        </q-item>

        <!-- HEADER: ПУНКТИ ЗАБОРУ ТА ЛОГІСТИКА -->
        <q-separator class="q-my-xs bg-grey-8" />
        <q-item-label header class="text-uppercase text-cyan-3 text-weight-bold" style="font-size: 11px; letter-spacing: 0.5px;">
          <span v-if="!miniState">Пункти забору & Логістика</span>
          <q-icon v-else name="local_shipping" size="18px" />
        </q-item-label>

        <q-item clickable v-ripple :to="{ name: 'lab-phlebotomy' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-syringe" size="18px" color="red-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Пункт забору</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Маніпуляційний кабінет / Штрихкоди</q-tooltip>
        </q-item>

        <q-item clickable v-ripple :to="{ name: 'lab-logistics' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-truck" size="18px" color="blue-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Логістика & Кур'єри</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Термоконтейнери та маршрутні листи</q-tooltip>
        </q-item>

        <!-- HEADER: КАБІНЕТ ПАЦІЄНТА -->
        <q-separator class="q-my-xs bg-grey-8" />
        <q-item-label header class="text-uppercase text-cyan-3 text-weight-bold" style="font-size: 11px; letter-spacing: 0.5px;">
          <span v-if="!miniState">Кабінет пацієнта</span>
          <q-icon v-else name="person" size="18px" />
        </q-item-label>

        <q-item clickable v-ripple :to="{ name: 'lab-patient-portal' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-user-circle" size="18px" color="teal-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Мої результати (Портал)</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Кабінет пацієнта: статус і PDF бланки</q-tooltip>
        </q-item>

        <!-- HEADER: ДОВІДНИКИ ЛАБОРАТОРІЇ -->
        <q-separator class="q-my-xs bg-grey-8" />
        <q-expansion-item
          group="lab-dict-group"
          icon="fas fa-book-medical"
          label="Довідники ЛІС"
          header-class="text-cyan-2"
          expand-icon-class="text-grey-4"
        >
          <q-list class="q-pl-md">
            <q-item clickable v-ripple :to="{ name: 'lab-norms' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-sliders-h" size="14px" />
              </q-item-section>
              <q-item-section>Норми і методики</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'dict-biomaterials' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-vial" size="14px" />
              </q-item-section>
              <q-item-section>Біоматеріали</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'dict-tube-types' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-flask" size="14px" />
              </q-item-section>
              <q-item-section>Типи пробірок</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'dict-analyzer-types' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-server" size="14px" />
              </q-item-section>
              <q-item-section>Аналізатори</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'dict-lab-parameters' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-list-ol" size="14px" />
              </q-item-section>
              <q-item-section>Показники та профілі</q-item-section>
            </q-item>
          </q-list>
        </q-expansion-item>
      </q-list>
    </q-scroll-area>
  </q-drawer>
</template>

<script>
export default {
  name: 'MenuDrawerLaboratory',
  props: {
    drawer: {
      type: Boolean,
      default: true
    },
    miniState: {
      type: Boolean,
      default: false
    }
  }
};
</script>

<style scoped>
.nav-drawer {
  background: #333333 !important;
}
.drawer-header {
  border-bottom: 2px solid #0178BC;
}
.nav_scroll-area {
  height: calc(100% - 50px);
}
</style>
"""
    dest_dir = os.path.join(BASE_DIR, "components", "baseElements")
    ensure_dir(dest_dir)
    dest = os.path.join(dest_dir, "menuDrawer.vue")
    with open(dest, "w", encoding="utf-8") as f:
        f.write(content)
    print("Created:", dest)

if __name__ == "__main__":
    create_router()
    create_store()
    create_menu_drawer()
    print("Core frontend infrastructure generated.")
