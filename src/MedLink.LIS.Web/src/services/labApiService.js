/**
 * MedLink LIS 4.0 — сервісний шар REST API (docs/API_CONTRACT.md, розділ 2).
 * Одна функція на кожен ендпоінт контракту. Усі функції повертають response.data.
 * Базовий URL: /api/v1/lab. Поточний співробітник — заголовок X-MedLink-Employee-Id (boot/axios.js).
 */
import { http, API_BASE } from './http';

const data = p => p.then(r => r.data);
const clean = params => {
  const out = {};
  Object.keys(params || {}).forEach(k => {
    const v = params[k];
    if (v !== null && v !== undefined && v !== '') out[k] = v;
  });
  return out;
};

export function absoluteUrl (path) {
  return `${API_BASE}${path}`;
}

const labApi = {
  API_BASE,
  absoluteUrl,

  // ---------- 2.1 Контекст ----------
  getMe: () => data(http.get('/context/me')),
  getEmployees: () => data(http.get('/context/employees')),
  health: () => data(http.get('/health', { baseURL: '' })),

  // ---------- 2.2 Довідники ----------
  dictList: (name, params = {}) => data(http.get(`/dictionaries/${name}`, { params: clean(params) })),
  dictGet: (name, id) => data(http.get(`/dictionaries/${name}/${id}`)),
  dictCreate: (name, body) => data(http.post(`/dictionaries/${name}`, body)),
  dictUpdate: (name, id, body) => data(http.put(`/dictionaries/${name}/${id}`, body)),
  dictDelete: (name, id) => data(http.delete(`/dictionaries/${name}/${id}`)),
  testProfiles: code => data(http.get(`/dictionaries/tests/${encodeURIComponent(code)}/profiles`)),
  dictImport: (formData, dryRun) => data(http.post('/dictionaries/import', formData, {
    params: { dryRun: !!dryRun }, headers: { 'Content-Type': 'multipart/form-data' }
  })),

  // ---------- 2.3 Норми ----------
  normLayers: (testCode, methodCode) => data(http.get('/norms/layers', { params: clean({ testCode, methodCode }) })),
  normCombinations: testCode => data(http.get('/norms/combinations', { params: clean({ testCode }) })),
  saveNormCombination: body => data(http.post('/norms/combinations', body)),
  deleteNormCombination: id => data(http.delete(`/norms/combinations/${id}`)),
  resolveCascade: body => data(http.post('/norms/resolve-cascade', body)),
  serviceCard: profileId => data(http.get(`/norms/service-card/${profileId}`)),

  // ---------- 2.4 Пацієнти ----------
  searchPatients: search => data(http.get('/patients', { params: clean({ search }) })),
  getPatient: id => data(http.get(`/patients/${id}`)),
  createPatient: body => data(http.post('/patients', body)),
  updatePatient: (id, body) => data(http.put(`/patients/${id}`, body)),
  patientHistory: id => data(http.get(`/patients/${id}/history`)),
  patientTrend: (id, testCode) => data(http.get(`/patients/${id}/trend/${encodeURIComponent(testCode)}`)),

  // ---------- 2.5 Замовлення ----------
  getOrders: params => data(http.get('/orders', { params: clean(params) })),
  getOrder: id => data(http.get(`/orders/${id}`)),
  createOrder: body => data(http.post('/orders', body)),
  createOrdersBatch: list => data(http.post('/orders/batch', list)),
  cancelOrder: (id, reason) => data(http.post(`/orders/${id}/cancel`, { reason })),
  releaseOrder: id => data(http.post(`/orders/${id}/release`)),
  orderReportHtml: id => data(http.get(`/orders/${id}/report`, { responseType: 'text', headers: { Accept: 'text/html' } })),
  orderReportUrl: id => absoluteUrl(`/orders/${id}/report`),
  orderReportPdfUrl: id => absoluteUrl(`/orders/${id}/report.pdf`),
  orderLabels: id => data(http.get(`/orders/${id}/labels`)),
  orderByBarcode: barcode => data(http.get(`/orders/by-barcode/${encodeURIComponent(barcode)}`)),
  verifyToken: token => data(http.get(`/verify/${encodeURIComponent(token)}`)),

  // ---------- 2.6 Проби / забір / логістика ----------
  getSamples: params => data(http.get('/samples', { params: clean(params) })),
  collectSample: (barcode, body) => data(http.post(`/samples/${encodeURIComponent(barcode)}/collect`, body)),
  receiveSample: (barcode, body) => data(http.post(`/samples/${encodeURIComponent(barcode)}/receive`, body || {})),
  rejectSample: (barcode, body) => data(http.post(`/samples/${encodeURIComponent(barcode)}/reject`, body)),
  sampleLabel: barcode => data(http.get(`/samples/${encodeURIComponent(barcode)}/label`)),
  getManifests: status => data(http.get('/logistics/manifests', { params: clean({ status }) })),
  createManifest: body => data(http.post('/logistics/manifests', body)),
  receiveManifest: (id, body) => data(http.post(`/logistics/manifests/${id}/receive`, body)),

  // ---------- 2.7 Робочий лист і результати ----------
  getWorklist: params => data(http.get('/worklist', { params: clean(params) })),
  worklistSummary: () => data(http.get('/worklist/summary')),
  saveResult: (orderTestId, body) => data(http.put(`/results/${orderTestId}`, body)),
  verifyResult: (orderTestId, body) => data(http.post(`/results/${orderTestId}/verify`, body || {})),
  verifyBatch: orderTestIds => data(http.post('/results/verify-batch', { orderTestIds })),
  rejectResult: (orderTestId, reason) => data(http.post(`/results/${orderTestId}/reject`, { reason })),
  rerunResult: orderTestId => data(http.post(`/results/${orderTestId}/rerun`)),
  reopenResult: orderTestId => data(http.post(`/results/${orderTestId}/reopen`)),
  resultHistory: orderTestId => data(http.get(`/results/${orderTestId}/history`)),
  autoverify: orderTestIds => data(http.post('/worklist/autoverify', orderTestIds ? { orderTestIds } : {})),
  getBatches: () => data(http.get('/worklist/batches')),
  createBatch: body => data(http.post('/worklist/batches', body)),
  batchPrintUrl: id => absoluteUrl(`/worklist/batches/${id}/print`),
  unmatchedResults: () => data(http.get('/results/unmatched')),
  linkUnmatched: (id, orderTestId) => data(http.post(`/results/unmatched/${id}/link`, { orderTestId })),
  getPanicCalls: params => data(http.get('/panic-calls', { params: clean(params) })),
  createPanicCall: body => data(http.post('/panic-calls', body)),
  panicPending: () => data(http.get('/panic/pending')),

  // ---------- 2.8 Контроль якості ----------
  qcMaterials: params => data(http.get('/qc/materials', { params: clean(params) })),
  createQcMaterial: body => data(http.post('/qc/materials', body)),
  updateQcMaterial: (id, body) => data(http.put(`/qc/materials/${id}`, body)),
  leveyJennings: params => data(http.get('/qc/levey-jennings', { params: clean(params) })),
  addQcResult: body => data(http.post('/qc/results', body)),
  qcLockouts: active => data(http.get('/qc/lockouts', { params: clean({ active }) })),
  resolveLockout: (id, body) => data(http.post(`/qc/lockouts/${id}/resolve`, body)),
  qcReport: params => data(http.get('/qc/report', { params: clean(params) })),

  // ---------- 2.9 Аналізатори та коннектори ----------
  getAnalyzers: () => data(http.get('/analyzers')),
  getAnalyzer: id => data(http.get(`/analyzers/${id}`)),
  createAnalyzer: body => data(http.post('/analyzers', body)),
  updateAnalyzer: (id, body) => data(http.put(`/analyzers/${id}`, body)),
  deleteAnalyzer: id => data(http.delete(`/analyzers/${id}`)),
  analyzerMessages: (id, params) => data(http.get(`/analyzers/${id}/messages`, { params: clean(params) })),
  simulateAnalyzer: (id, rawMessage) => data(http.post(`/analyzers/${id}/simulate`, { rawMessage })),
  analyzerOrderPreview: (id, barcode) => data(http.get(`/analyzers/${id}/order-preview/${encodeURIComponent(barcode)}`, { responseType: 'text' })),
  getConnectors: () => data(http.get('/connectors')),
  getConnector: id => data(http.get(`/connectors/${id}`)),
  createConnector: name => data(http.post('/connectors', { name })),
  disableConnector: id => data(http.post(`/connectors/${id}/disable`)),
  rotateConnectorKey: id => data(http.post(`/connectors/${id}/rotate-key`)),
  connectorDownloadUrl: id => absoluteUrl(`/connectors/${id}/download`),
  connectorLogs: (id, params) => data(http.get(`/connectors/${id}/logs`, { params: clean(params) })),

  // ---------- 2.10 Біобанк ----------
  racks: () => data(http.get('/biobank/racks')),
  createRack: body => data(http.post('/biobank/racks', body)),
  updateRack: (id, body) => data(http.put(`/biobank/racks/${id}`, body)),
  deleteRack: id => data(http.delete(`/biobank/racks/${id}`)),
  rackCells: id => data(http.get(`/biobank/racks/${id}/cells`)),
  placeCell: body => data(http.post('/biobank/cells/place', body)),
  removeCell: (id, reason) => data(http.post(`/biobank/cells/${id}/remove`, { reason })),
  biobankSearch: barcode => data(http.get('/biobank/search', { params: { barcode } })),

  // ---------- Реагенти ----------
  reagentLots: params => data(http.get('/reagents/lots', { params: clean(params) })),
  createReagentLot: body => data(http.post('/reagents/lots', body)),
  updateReagentLot: (id, body) => data(http.put(`/reagents/lots/${id}`, body)),
  deleteReagentLot: id => data(http.delete(`/reagents/lots/${id}`)),
  consumeReagent: (id, tests) => data(http.post(`/reagents/lots/${id}/consume`, { tests })),
  reagentAlerts: () => data(http.get('/reagents/alerts')),

  // ---------- Мікробіологія ----------
  cultures: params => data(http.get('/microbiology/cultures', { params: clean(params) })),
  culture: id => data(http.get(`/microbiology/cultures/${id}`)),
  createCulture: body => data(http.post('/microbiology/cultures', body)),
  updateCulture: (id, body) => data(http.put(`/microbiology/cultures/${id}`, body)),
  addIsolate: (cultureId, body) => data(http.post(`/microbiology/cultures/${cultureId}/isolates`, body)),
  addSusceptibility: (isolateId, body) => data(http.post(`/microbiology/isolates/${isolateId}/susceptibility`, body)),
  cultureReport: id => data(http.get(`/microbiology/cultures/${id}/report`, { responseType: 'text', headers: { Accept: 'text/html' } })),
  cultureReportUrl: id => absoluteUrl(`/microbiology/cultures/${id}/report`),

  // ---------- Аналітика ----------
  tat: params => data(http.get('/analytics/tat', { params: clean(params) })),
  volume: params => data(http.get('/analytics/volume', { params: clean(params) })),
  exportXlsxUrl: (report, params = {}) => {
    const q = new URLSearchParams(clean({ report, ...params })).toString();
    return absoluteUrl(`/analytics/export.xlsx?${q}`);
  },

  // ---------- Аудит, налаштування ----------
  audit: params => data(http.get('/audit', { params: clean(params) })),
  labSettings: () => data(http.get('/settings/lab')),
  saveLabSettings: body => data(http.put('/settings/lab', body)),
  settingsUsers: () => data(http.get('/settings/users')),
  createSettingsUser: body => data(http.post('/settings/users', body)),
  updateSettingsUser: (id, body) => data(http.put(`/settings/users/${id}`, body)),
  deleteSettingsUser: id => data(http.delete(`/settings/users/${id}`)),
  numerators: () => data(http.get('/settings/numerators')),
  saveNumerators: body => data(http.put('/settings/numerators', body)),

  // ---------- Імпорт ----------
  importResults: (formData, dryRun) => data(http.post('/import/results', formData, {
    params: { dryRun: !!dryRun }, headers: { 'Content-Type': 'multipart/form-data' }
  })),

  // ---------- Кабінет пацієнта ----------
  portalOrders: patientId => data(http.get(`/portal/${patientId}/orders`)),
  portalOrder: (patientId, id) => data(http.get(`/portal/${patientId}/orders/${id}`)),
  portalPdfUrl: (patientId, id) => absoluteUrl(`/portal/${patientId}/orders/${id}/report.pdf`),
  portalTrend: (patientId, testCode) => data(http.get(`/portal/${patientId}/trend/${encodeURIComponent(testCode)}`)),
  portalNotifications: patientId => data(http.get(`/portal/${patientId}/notifications`))
};

export default labApi;
