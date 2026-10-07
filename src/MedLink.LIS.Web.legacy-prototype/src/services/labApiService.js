/**
 * MedLink LIS - Laboratory API Service
 * Can toggle between Mock in-memory mode and real evomis backend REST API.
 * Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
 */

import axios from 'axios';
import {
  mockPatients,
  mockOrders,
  mockSamples,
  mockWorklistResults,
  mockQcData,
  mockMicrobiologyData,
  mockReagents,
  mockBiomaterials,
  mockAnalyzers
} from './mockData';

// Toggle: Set to false when connecting to real .NET Core backend
export const USE_MOCK = true;

const apiClient = axios.create({
  baseURL: process.env.VUE_APP_API_BASE_URL || '/api/v1/lab',
  headers: {
    'Content-Type': 'application/json'
  }
});

// Attach JWT token in real environment (mock bypass)
apiClient.interceptors.request.use(config => {
  const token = localStorage.getItem('access_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const LabApiService = {
  // Orders
  async getOrders(params = {}) {
    if (USE_MOCK) return Promise.resolve({ data: mockOrders });
    return apiClient.get('/orders', { params });
  },

  async getOrderById(id) {
    if (USE_MOCK) {
      const order = mockOrders.find(o => o.id === id || o.orderNumber === id);
      return Promise.resolve({ data: order || mockOrders[0] });
    }
    return apiClient.get(`/orders/${id}`);
  },

  async createOrder(orderDto) {
    if (USE_MOCK) {
      const newOrder = { ...orderDto, id: `ord-${Date.now()}`, status: 'NEW' };
      mockOrders.unshift(newOrder);
      return Promise.resolve({ data: newOrder });
    }
    return apiClient.post('/orders', orderDto);
  },

  // Phlebotomy & Samples
  async getSamples(params = {}) {
    if (USE_MOCK) return Promise.resolve({ data: mockSamples });
    return apiClient.get('/samples', { params });
  },

  async collectSample(barcode, checklist) {
    if (USE_MOCK) {
      const sample = mockSamples.find(s => s.barcode === barcode);
      if (sample) sample.status = 'COLLECTED';
      return Promise.resolve({ data: { success: true, barcode, status: 'COLLECTED' } });
    }
    return apiClient.post(`/samples/${barcode}/collect`, { checklist });
  },

  async printZpl(barcode) {
    if (USE_MOCK) {
      return Promise.resolve({
        data: {
          success: true,
          barcode,
          zpl: `^XA^FO50,30^BY2^BCN,60,Y,N,N^FD${barcode}^FS^FO50,110^A0N,24,24^FDMedLink LIS 3.0^FS^XZ`
        }
      });
    }
    return apiClient.get(`/samples/${barcode}/zpl`);
  },

  // Logistics & Rejection
  async submitLogisticsManifest(manifestDto) {
    if (USE_MOCK) {
      return Promise.resolve({ data: { success: true, manifestId: 'ACT-2026-1006' } });
    }
    return apiClient.post('/logistics/manifest', manifestDto);
  },

  async rejectSample(barcode, defectReason) {
    if (USE_MOCK) {
      const sample = mockSamples.find(s => s.barcode === barcode);
      if (sample) sample.status = 'REJECTED';
      return Promise.resolve({ data: { success: true, barcode, defectReason, repeatOrderCreated: true } });
    }
    return apiClient.post(`/samples/${barcode}/reject`, { defectReason });
  },

  // Worklist & Results
  async getWorklist(params = {}) {
    if (USE_MOCK) return Promise.resolve({ data: mockWorklistResults });
    return apiClient.get('/worklist', { params });
  },

  async saveResult(id, value, comment) {
    if (USE_MOCK) {
      const item = mockWorklistResults.find(r => r.id === id);
      if (item) {
        item.value = value;
        item.comment = comment;
      }
      return Promise.resolve({ data: { success: true, item } });
    }
    return apiClient.put(`/results/${id}`, { value, comment });
  },

  // Medical Validation & Panic Values
  async verifyResult(id, doctorComment, qesSignature) {
    if (USE_MOCK) {
      const item = mockWorklistResults.find(r => r.id === id);
      if (item) item.status = 'VERIFIED';
      return Promise.resolve({ data: { success: true, id, status: 'VERIFIED' } });
    }
    return apiClient.post(`/results/${id}/verify`, { doctorComment, qesSignature });
  },

  async sendPanicAlert(orderId, alertDetails) {
    if (USE_MOCK) {
      return Promise.resolve({ data: { success: true, dispatchedTo: 'Doctor on Duty', timestamp: new Date().toISOString() } });
    }
    return apiClient.post(`/panic-alert`, { orderId, alertDetails });
  },

  // Quality Control
  async getQcChartData(analyzerId, parameter) {
    if (USE_MOCK) return Promise.resolve({ data: mockQcData });
    return apiClient.get(`/qc/chart`, { params: { analyzerId, parameter } });
  },

  async submitCapaAction(actionDto) {
    if (USE_MOCK) {
      mockQcData.currentStatus = 'ACTIVE';
      return Promise.resolve({ data: { success: true, message: 'Lockout removed successfully.' } });
    }
    return apiClient.post('/qc/capa', actionDto);
  },

  // Microbiology
  async getMicrobiologyData(sampleId) {
    if (USE_MOCK) return Promise.resolve({ data: mockMicrobiologyData });
    return apiClient.get(`/microbiology/${sampleId}`);
  },

  // Reagents
  async getReagents(params = {}) {
    if (USE_MOCK) return Promise.resolve({ data: mockReagents });
    return apiClient.get('/reagents', { params });
  },

  // Dictionaries
  async getBiomaterials() {
    if (USE_MOCK) return Promise.resolve({ data: mockBiomaterials });
    return apiClient.get('/dictionaries/biomaterials');
  },

  async getAnalyzers() {
    if (USE_MOCK) return Promise.resolve({ data: mockAnalyzers });
    return apiClient.get('/dictionaries/analyzers');
  }
};
