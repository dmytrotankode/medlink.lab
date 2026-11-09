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

  // Транспортні контейнери (термобокси, сумки): типи тари, укладка, запечатування, прийом цілком, повернення тари
  getContainerTypes: includeInactive => data(http.get('/logistics/container-types', { params: clean({ includeInactive: includeInactive ? true : null }) })),
  createContainerType: body => data(http.post('/logistics/container-types', body)),
  updateContainerType: (id, body) => data(http.put(`/logistics/container-types/${id}`, body)),
  getContainers: params => data(http.get('/logistics/containers', { params: clean(params) })),
  getContainer: id => data(http.get(`/logistics/containers/${id}`)),
  getContainerByBarcode: barcode => data(http.get(`/logistics/containers/by-barcode/${encodeURIComponent(barcode)}`)),
  getContainerEvents: id => data(http.get(`/logistics/containers/${id}/events`)),
  createContainer: body => data(http.post('/logistics/containers', body)),
  deleteContainer: id => data(http.delete(`/logistics/containers/${id}`)),
  packContainer: (id, body) => data(http.post(`/logistics/containers/${id}/pack`, body)),
  unpackContainer: (id, barcode) => data(http.delete(`/logistics/containers/${id}/items/${encodeURIComponent(barcode)}`)),
  sealContainer: (id, body) => data(http.post(`/logistics/containers/${id}/seal`, body)),
  dispatchContainer: (id, body) => data(http.post(`/logistics/containers/${id}/dispatch`, body || {})),
  receiveContainer: (id, body) => data(http.post(`/logistics/containers/${id}/receive`, body)),
  returnContainer: (id, body) => data(http.post(`/logistics/containers/${id}/return`, body || {})),

  // ---------- 2.7 Робочий лист і результати ----------
  getWorklist: params => data(http.get('/worklist', { params: clean(params) })),
  worklistSummary: () => data(http.get('/worklist/summary')),
  saveResult: (orderTestId, body) => data(http.put(`/results/${orderTestId}`, body)),
  verifyResult: (orderTestId, body) => data(http.post(`/results/${orderTestId}/verify`, body || {})),
  verifyBatch: orderTestIds => data(http.post('/results/verify-batch', { orderTestIds })),
  rejectResult: (orderTestId, reason) => data(http.post(`/results/${orderTestId}/reject`, { reason })),
  rerunResult: (orderTestId, body) => data(http.post(`/results/${orderTestId}/rerun`, body || {})),
  resultAttempts: orderTestId => data(http.get(`/results/${orderTestId}/attempts`)),
  acceptResultAttempt: (orderTestId, attemptId, comment) => data(http.post(`/results/${orderTestId}/accept-attempt`, { attemptId, comment })),
  resultCalculation: orderTestId => data(http.get(`/results/${orderTestId}/calculation`)),
  validateTestFormula: (code, formula) => data(http.post('/dictionaries/tests/validate-formula', { code, formula })),
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
  reagentConsumption: params => data(http.get('/reagents/consumption', { params: clean(params) })),
  reagentsByResult: orderTestId => data(http.get(`/reagents/by-result/${orderTestId}`)),

  // ---------- Мікробіологія ----------
  cultures: params => data(http.get('/microbiology/cultures', { params: clean(params) })),
  culture: id => data(http.get(`/microbiology/cultures/${id}`)),
  createCulture: body => data(http.post('/microbiology/cultures', body)),
  updateCulture: (id, body) => data(http.put(`/microbiology/cultures/${id}`, body)),
  deleteCulture: id => data(http.delete(`/microbiology/cultures/${id}`)),
  // Сервер приймає ідентифікатор дії (PRELIMINARY / ISOLATE / COMPLETE) у полі status
  transitionCulture: (id, action, comment) => data(http.post(`/microbiology/cultures/${id}/transition`, { status: action, comment })),
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

  // ---------- 2.11 Інтеграція з МІС, маршрутизація, розподіл, ЕСОЗ, звіти (ТЗ 21, 23) ----------
  interventions: params => data(http.get('/interventions', { params: clean(params) })),
  syncInterventions: () => data(http.post('/interventions/sync')),
  interventionCoverage: () => data(http.get('/intervention-map/coverage')),
  saveInterventionMap: (id, body) => data(id ? http.put(`/intervention-map/${id}`, body) : http.post('/intervention-map', body)),
  analyzerServices: id => data(http.get(`/analyzers/${id}/services`)),
  saveAnalyzerServices: (id, items, dryRun) => data(http.put(`/analyzers/${id}/services`, items, { params: { dryRun: !!dryRun } })),
  serviceMatrix: () => data(http.get('/analyzer-services/matrix')),
  serviceCard: key => data(http.get(`/analyzer-services/${encodeURIComponent(key)}`)),
  saveServiceCard: (key, body) => data(http.put(`/analyzer-services/${encodeURIComponent(key)}`, body)),
  serviceMatrixCsvUrl: () => absoluteUrl('/analyzer-services/export'),
  misCatalog: () => data(http.get('/mis/catalog')),
  misPackages: () => data(http.get('/mis/catalog/packages')),
  misReferrals: take => data(http.get('/mis/referrals', { params: clean({ take }) })),
  misReferral: id => data(http.get(`/mis/referrals/${encodeURIComponent(id)}`)),
  createMisReferral: body => data(http.post('/mis/referrals', body)),
  cancelMisReferral: (id, reason) => data(http.post(`/mis/referrals/${id}/cancel`, { reason })),
  setItemIndicators: (id, itemId, body) => data(http.put(`/mis/referrals/${id}/items/${itemId}/indicators`, body)),
  distributionInbox: params => data(http.get('/distribution/inbox', { params: clean(params) })),
  distribution: orderId => data(http.get(`/distribution/${orderId}`)),
  changeDistribution: (orderId, changes) => data(http.put(`/distribution/${orderId}`, changes)),
  autoDistribution: orderId => data(http.post(`/distribution/${orderId}/auto`)),
  confirmDistribution: (orderId, body) => data(http.post(`/distribution/${orderId}/confirm`, body || {})),
  routingPreview: body => data(http.post('/routing/preview', body)),
  routingLog: params => data(http.get('/routing/log', { params: clean(params) })),
  analyzerQueue: id => data(http.get(`/analyzers/${id}/queue`)),
  reassignOrderTest: (orderTestId, analyzerId, reason) => data(http.put(`/order-tests/${orderTestId}/analyzer`, { analyzerId, reason })),
  rerouteAnalyzer: (id, reason) => data(http.post(`/analyzers/${id}/reroute`, { reason })),
  ehealthSession: () => data(http.get('/ehealth/session')),
  ehealthLogin: body => data(http.post('/ehealth/session', body)),
  ehealthConfirm: body => data(http.post('/ehealth/session/confirm', body)),
  ehealthEnableTotp: () => data(http.post('/ehealth/session/totp')),
  ehealthLogout: () => data(http.delete('/ehealth/session')),
  ehealthPrepare: orderId => data(http.post(`/orders/${orderId}/ehealth/prepare`)),
  ehealthSubmit: (orderId, body) => data(http.post(`/orders/${orderId}/ehealth/submit`, body)),
  ehealthRetry: orderId => data(http.post(`/orders/${orderId}/ehealth/retry`)),
  ehealthAnnul: (orderId, body) => data(http.post(`/orders/${orderId}/ehealth/annul`, body)),
  ehealthOrderStatus: orderId => data(http.get(`/orders/${orderId}/ehealth/status`)),
  ehealthMockSign: (orderId, body) => data(http.post(`/orders/${orderId}/ehealth/mock-sign`, body || { kind: 'submit' })),
  ehealthAnnulPrepare: (orderId, body) => data(http.post(`/orders/${orderId}/ehealth/annul/prepare`, body)),
  ehealthSubmissions: params => data(http.get('/ehealth/submissions', { params: clean(params) })),
  ehealthRunOutbox: () => data(http.post('/ehealth/outbox/run')),
  ehealthRetryTask: id => data(http.post(`/ehealth/outbox/${id}/retry`)),
  ehealthCancelTask: id => data(http.post(`/ehealth/outbox/${id}/cancel`)),
  workSummary: params => data(http.get('/work/summary', { params: clean(params) })),
  workByAnalyzer: (id, params) => data(http.get(`/work/by-analyzer/${id}`, { params: clean(params) })),
  workPatients: params => data(http.get('/work/by-patient', { params: clean(params) })),
  workByPatient: (id, params) => data(http.get(`/work/by-patient/${id}`, { params: clean(params) })),
  workServices: params => data(http.get('/work/by-service', { params: clean(params) })),
  workByService: (code, params) => data(http.get(`/work/by-service/${encodeURIComponent(code)}`, { params: clean(params) })),
  workExportUrl: params => absoluteUrl(`/work/export?${new URLSearchParams(clean(params)).toString()}`),
  reportsList: () => data(http.get('/reports')),
  report: (type, params) => data(http.get(`/reports/${type}`, { params: clean(params) })),
  reportUrl: (type, params) => absoluteUrl(`/reports/${type}?${new URLSearchParams(clean(params)).toString()}`),
  cumulativeReport: (patientId, params) => data(http.get(`/reports/cumulative/${patientId}`, { params: clean(params) })),
  cumulativeReportUrl: (patientId, params) => absoluteUrl(`/reports/cumulative/${patientId}?${new URLSearchParams(clean(params)).toString()}`),

  // ---------- Патоморфологія (ТЗ розд. 22) ----------
  pathSummary: () => data(http.get('/pathology/summary')),
  pathIncoming: () => data(http.get('/pathology/incoming')),
  pathCreateReferral: body => data(http.post('/mis/referrals', { type: 'PATHOLOGY', patientId: body.patientId, pathology: body })),
  pathAccept: (id, scannedBarcodes) => data(http.post(`/pathology/incoming/${id}/accept`, { scannedBarcodes })),
  pathReject: (id, body) => data(http.post(`/pathology/incoming/${id}/reject`, body)),
  pathQuery: (id, text) => data(http.post(`/pathology/incoming/${id}/query`, { text })),
  pathCases: params => data(http.get('/pathology/cases', { params: clean(params) })),
  pathPrepare: (id, body) => data(http.post(`/pathology/cases/${id}/prepare`, body)),
  pathPrepareRepeat: (id, body) => data(http.post(`/pathology/cases/${id}/prepare-repeat`, body)),
  pathScreening: (id, body) => data(http.post(`/pathology/cases/${id}/screening`, body)),
  pathCytoAnalytics: () => data(http.get('/pathology/cytology/analytics')),
  pathCase: id => data(http.get(`/pathology/cases/${encodeURIComponent(id)}`)),
  pathGross: (id, body) => data(http.put(`/pathology/cases/${id}/gross`, body)),
  pathScan: body => data(http.post('/pathology/scan', body)),
  pathAdvance: (id, body) => data(http.post(`/pathology/cases/${id}/advance`, body)),
  pathAdditional: (id, body) => data(http.post(`/pathology/cases/${id}/additional`, body)),
  pathSlideReady: slideId => data(http.post(`/pathology/slides/${slideId}/ready`)),
  pathSaveReport: (id, body) => data(http.put(`/pathology/cases/${id}/report`, body)),
  pathPreliminary: (id, text) => data(http.post(`/pathology/cases/${id}/report/preliminary`, { text })),
  pathSign: (id, signer, extra) => data(http.post(`/pathology/cases/${id}/report/sign`, { signer, ...(extra || {}) })),
  pathAddendum: (id, body) => data(http.post(`/pathology/cases/${id}/report/addendum`, body)),
  pathCitoNotice: (id, body) => data(http.post(`/pathology/cases/${id}/cito-notice`, body)),
  pathArchive: params => data(http.get('/pathology/archive', { params: clean(params) })),
  pathIssue: (blockId, body) => data(http.post(`/pathology/blocks/${blockId}/issue`, body)),
  pathReturn: blockId => data(http.post(`/pathology/blocks/${blockId}/return`)),
  pathDictionaries: () => data(http.get('/pathology/dictionaries')),
  pathStaff: () => data(http.get('/pathology/staff')),
  pathMethodPrice: (code, price) => data(http.put(`/pathology/methods/${encodeURIComponent(code)}/price`, { price })),
  pathRooms: () => data(http.get('/pathology/rooms')),
  pathDisposeSlide: (slideId, body) => data(http.post(`/pathology/slides/${slideId}/dispose`, body)),
  pathSetStorage: (blockId, body) => data(http.put(`/pathology/blocks/${blockId}/storage`, body)),
  pathDispatch: (id, body) => data(http.post(`/pathology/cases/${id}/dispatch`, body)),
  pathDispatches: id => data(http.get(`/pathology/cases/${id}/dispatches`)),
  pathForm014: id => data(http.get(`/pathology/cases/${id}/form014`)),
  pathForm014PdfUrl: (id, sides = '1,2') => absoluteUrl(`/pathology/cases/${id}/form014.pdf?sides=${sides}`),
  pathIssueActUrl: issueId => absoluteUrl(`/pathology/issues/${issueId}/act.pdf`),

  // ---------- Кабінет пацієнта ----------
  portalOrders: patientId => data(http.get(`/portal/${patientId}/orders`)),
  portalOrder: (patientId, id) => data(http.get(`/portal/${patientId}/orders/${id}`)),
  portalPdfUrl: (patientId, id) => absoluteUrl(`/portal/${patientId}/orders/${id}/report.pdf`),
  portalTrend: (patientId, testCode) => data(http.get(`/portal/${patientId}/trend/${encodeURIComponent(testCode)}`)),
  portalNotifications: patientId => data(http.get(`/portal/${patientId}/notifications`))
};

export default labApi;
