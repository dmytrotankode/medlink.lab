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

  // ---------- Матриця призначень (PO-1) ----------
  orderMatrix: () => data(http.get('/dictionaries/order-matrix')),
  orderMatrixFavorites: employeeId => data(http.get('/dictionaries/order-matrix/favorites', { params: clean({ employeeId }) })),
  saveOrderMatrixFavorites: (employeeId, body) => data(http.put('/dictionaries/order-matrix/favorites', body, { params: clean({ employeeId }) })),

  // ---------- Підрозділи лабораторії та журнали (PO-2) ----------
  sections: () => data(http.get('/sections')),
  createSection: body => data(http.post('/sections', body)),
  updateSection: (id, body) => data(http.put(`/sections/${id}`, body)),
  deleteSection: id => data(http.delete(`/sections/${id}`)),
  sectionJournal: (id, date) => data(http.get(`/sections/${id}/journal`, { params: clean({ date }) })),
  sectionRenumberPreview: (id, body) => data(http.post(`/sections/${id}/journal/renumber-preview`, body || {})),
  orderJournalEntries: orderId => data(http.get(`/orders/${orderId}/journal-entries`)),

  // ---------- Обробка зразка / аліквоти (PO-3) ----------
  sampleTree: barcode => data(http.get(`/samples/${encodeURIComponent(barcode)}/tree`)),
  splitSample: (barcode, body) => data(http.post(`/samples/${encodeURIComponent(barcode)}/split`, body)),
  sampleStages: barcode => data(http.get(`/samples/${encodeURIComponent(barcode)}/stages`)),
  setSampleStage: (barcode, body) => data(http.post(`/samples/${encodeURIComponent(barcode)}/stage`, body)),

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
  deletePatient: id => data(http.delete(`/patients/${id}`)),
  patientHistory: id => data(http.get(`/patients/${id}/history`)),
  patientTrend: (id, testCode) => data(http.get(`/patients/${id}/trend/${encodeURIComponent(testCode)}`)),

  // ---------- 2.5 Замовлення ----------
  getOrders: params => data(http.get('/orders', { params: clean(params) })),
  getOrder: id => data(http.get(`/orders/${id}`)),
  createOrder: body => data(http.post('/orders', body)),
  tubePlan: body => data(http.post('/orders/tube-plan', body)),
  updateOrder: (id, body) => data(http.put(`/orders/${id}`, body)),
  deleteOrder: id => data(http.delete(`/orders/${id}`)),
  createOrdersBatch: list => data(http.post('/orders/batch', list)),
  /** Дозволені дії/переходи статусів для поточної ролі: { allowedActions: string[] } або string[] */
  orderTransitions: id => data(http.get(`/orders/${id}/transitions`)),
  /** Універсальний перехід статусу (REST-конвенція, якщо для дії немає окремого ендпоінта) */
  orderTransition: (id, action, body) => data(http.post(`/orders/${id}/transitions`, { action, ...(body || {}) })),
  reopenOrder: id => data(http.post(`/orders/${id}/reopen`)),
  cancelOrder: (id, reason) => data(http.post(`/orders/${id}/cancel`, { reason })),
  releaseOrder: id => data(http.post(`/orders/${id}/release`)),
  /** variant: final | preliminary | cito (Q-10) */
  orderReportHtml: (id, variant) => data(http.get(`/orders/${id}/report`, { params: clean({ variant }), responseType: 'text', headers: { Accept: 'text/html' } })),
  orderReportUrl: (id, variant) => absoluteUrl(`/orders/${id}/report${variant ? '?variant=' + variant : ''}`),
  orderReportPdfUrl: (id, variant) => absoluteUrl(`/orders/${id}/report.pdf${variant ? '?variant=' + variant : ''}`),
  orderLabels: id => data(http.get(`/orders/${id}/labels`)),
  orderByBarcode: barcode => data(http.get(`/orders/by-barcode/${encodeURIComponent(barcode)}`)),
  verifyToken: token => data(http.get(`/verify/${encodeURIComponent(token)}`)),

  // ---------- 2.6 Проби / забір / логістика ----------
  getSamples: params => data(http.get('/samples', { params: clean(params) })),
  collectSample: (barcode, body) => data(http.post(`/samples/${encodeURIComponent(barcode)}/collect`, body)),
  receiveSample: (barcode, body) => data(http.post(`/samples/${encodeURIComponent(barcode)}/receive`, body || {})),
  rejectSample: (barcode, body) => data(http.post(`/samples/${encodeURIComponent(barcode)}/reject`, body)),
  getSample: barcode => data(http.get(`/samples/${encodeURIComponent(barcode)}`)),
  updateSample: (barcode, body) => data(http.put(`/samples/${encodeURIComponent(barcode)}`, body)),
  deleteSample: barcode => data(http.delete(`/samples/${encodeURIComponent(barcode)}`)),
  sampleLabel: barcode => data(http.get(`/samples/${encodeURIComponent(barcode)}/label`)),
  getManifests: status => data(http.get('/logistics/manifests', { params: clean({ status }) })),
  createManifest: body => data(http.post('/logistics/manifests', body)),
  receiveManifest: (id, body) => data(http.post(`/logistics/manifests/${id}/receive`, body)),
  getManifest: id => data(http.get(`/logistics/manifests/${id}`)),
  updateManifest: (id, body) => data(http.put(`/logistics/manifests/${id}`, body)),
  deleteManifest: id => data(http.delete(`/logistics/manifests/${id}`)),

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
  getResult: orderTestId => data(http.get(`/results/${orderTestId}`)),
  deleteResult: orderTestId => data(http.delete(`/results/${orderTestId}`)),
  autoverify: orderTestIds => data(http.post('/worklist/autoverify', orderTestIds ? { orderTestIds } : {})),
  getBatches: () => data(http.get('/worklist/batches')),
  createBatch: body => data(http.post('/worklist/batches', body)),
  batchPrintUrl: id => absoluteUrl(`/worklist/batches/${id}/print`),
  unmatchedResults: () => data(http.get('/results/unmatched')),
  linkUnmatched: (id, orderTestId) => data(http.post(`/results/unmatched/${id}/link`, { orderTestId })),
  getPanicCalls: params => data(http.get('/panic-calls', { params: clean(params) })),
  createPanicCall: body => data(http.post('/panic-calls', body)),
  updatePanicCall: (id, body) => data(http.put(`/panic-calls/${id}`, body)),
  deletePanicCall: id => data(http.delete(`/panic-calls/${id}`)),
  deleteBatch: id => data(http.delete(`/worklist/batches/${id}`)),
  panicPending: () => data(http.get('/panic/pending')),

  // ---------- 2.8 Контроль якості ----------
  qcMaterials: params => data(http.get('/qc/materials', { params: clean(params) })),
  createQcMaterial: body => data(http.post('/qc/materials', body)),
  updateQcMaterial: (id, body) => data(http.put(`/qc/materials/${id}`, body)),
  deleteQcMaterial: id => data(http.delete(`/qc/materials/${id}`)),
  qcResults: params => data(http.get('/qc/results', { params: clean(params) })),
  updateQcResult: (id, body) => data(http.put(`/qc/results/${id}`, body)),
  deleteQcResult: id => data(http.delete(`/qc/results/${id}`)),
  deleteLockout: id => data(http.delete(`/qc/lockouts/${id}`)),
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
  updateConnector: (id, body) => data(http.put(`/connectors/${id}`, body)),
  deleteConnector: id => data(http.delete(`/connectors/${id}`)),
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
  updateCell: (id, body) => data(http.put(`/biobank/cells/${id}`, body)),

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
  deleteCulture: id => data(http.delete(`/microbiology/cultures/${id}`)),
  updateIsolate: (id, body) => data(http.put(`/microbiology/isolates/${id}`, body)),
  deleteIsolate: id => data(http.delete(`/microbiology/isolates/${id}`)),
  deleteSusceptibility: id => data(http.delete(`/microbiology/susceptibility/${id}`)),
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

  // ---------- Оплата (FR-GAP-060) ----------
  payers: () => data(http.get('/payers')),
  savePayer: (id, body) => data(id ? http.put(`/payers/${id}`, body) : http.post('/payers', body)),
  priceLists: () => data(http.get('/price-lists')),
  savePriceList: (id, body) => data(id ? http.put(`/price-lists/${id}`, body) : http.post('/price-lists', body)),
  patientInsurances: patientId => data(http.get(`/patients/${patientId}/insurances`)),
  addPatientInsurance: (patientId, body) => data(http.post(`/patients/${patientId}/insurances`, body)),
  billingQuote: body => data(http.post('/billing/quote', body)),
  orderBilling: id => data(http.get(`/orders/${id}/billing`)),
  setOrderPayer: (id, body) => data(http.put(`/orders/${id}/payer`, body)),
  setChargePatientChoice: (id, chargeId, patientPays) => data(http.put(`/orders/${id}/charges/${chargeId}/patient-choice`, null, { params: { patientPays } })),
  addPayment: (id, body) => data(http.post(`/orders/${id}/payments`, body)),
  issueInvoice: id => data(http.post(`/orders/${id}/invoice`)),
  receiptUrl: id => `${API_BASE}/orders/${id}/receipt`,
  invoicePrintUrl: id => `${API_BASE}/invoices/${id}/print`,

  // ---------- Направлення (FR-REF-001) ----------
  lookupEhealthReferral: (number, patientId) => data(http.get('/referrals/ehealth/lookup', { params: clean({ number, patientId }) })),
  referralJournal: params => data(http.get('/referrals/journal', { params: clean(params || {}) })),

  // ---------- Зовнішні лабораторії (send-out) ----------
  performers: includeInactive => data(http.get('/performers', { params: clean({ includeInactive }) })),
  savePerformer: (id, body) => data(id ? http.put(`/performers/${id}`, body) : http.post('/performers', body)),
  setPerformerTests: (id, items) => data(http.put(`/performers/${id}/tests`, items)),
  routeOrderTest: (orderTestId, performerId, reason) => data(http.put(`/order-tests/${orderTestId}/performer`, { performerId, reason })),
  sendOutQueue: performerId => data(http.get('/send-out/queue', { params: clean({ performerId }) })),
  sendOuts: (status, performerId) => data(http.get('/send-out', { params: clean({ status, performerId }) })),
  sendOut: id => data(http.get(`/send-out/${id}`)),
  createSendOut: body => data(http.post('/send-out', body)),
  dispatchSendOut: (id, body) => data(http.post(`/send-out/${id}/dispatch`, body || {})),
  acceptSendOut: (id, body) => data(http.post(`/send-out/${id}/accept`, body || {})),
  cancelSendOut: (id, reason) => data(http.post(`/send-out/${id}/cancel`, { reason })),
  sendOutResult: (itemId, body) => data(http.post(`/send-out/items/${itemId}/result`, body)),
  sendOutRejectItem: (itemId, reason, returnToQueue) => data(http.post(`/send-out/items/${itemId}/reject`, { reason, returnToQueue })),
  sendOutImport: (id, formData, dryRun) => data(http.post(`/send-out/${id}/import`, formData, {
    params: { dryRun: !!dryRun }, headers: { 'Content-Type': 'multipart/form-data' }
  })),
  orderAttachments: orderId => data(http.get(`/orders/${orderId}/attachments`)),
  uploadAttachment: (orderId, formData, sendOutId) => data(http.post(`/orders/${orderId}/attachments`, formData, {
    params: clean({ sendOutId }), headers: { 'Content-Type': 'multipart/form-data' }
  })),
  attachmentUrl: id => `${API_BASE}/attachments/${id}/content`,

  // ---------- Кабінет пацієнта ----------
  portalOrders: patientId => data(http.get(`/portal/${patientId}/orders`)),
  portalOrder: (patientId, id) => data(http.get(`/portal/${patientId}/orders/${id}`)),
  portalPdfUrl: (patientId, id) => absoluteUrl(`/portal/${patientId}/orders/${id}/report.pdf`),
  portalTrend: (patientId, testCode) => data(http.get(`/portal/${patientId}/trend/${encodeURIComponent(testCode)}`)),
  portalNotifications: patientId => data(http.get(`/portal/${patientId}/notifications`))
};

export default labApi;
