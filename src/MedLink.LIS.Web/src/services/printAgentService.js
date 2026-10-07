/**
 * Локальний агент друку етикеток (вбудований у MedLink LabConnector на ПК лабораторії).
 * POST http://localhost:5088/print/zpl { zpl, printer: null | {host, port} | {name} }
 * POST http://localhost:5088/print/test
 * Налаштування принтера — з GET /settings/lab (labelPrinterHost / labelPrinterPort / labelPrinterName).
 */
import axios from 'axios';

export const PRINT_AGENT_URL = process.env.PRINT_AGENT_URL || 'http://localhost:5088';
export const AGENT_UNREACHABLE_MESSAGE = 'Агент друку не запущено на цьому ПК — встановіть MedLink LabConnector або надрукуйте SVG';

const agent = axios.create({ baseURL: PRINT_AGENT_URL, timeout: 4000, headers: { 'Content-Type': 'application/json' } });

export function printerFromSettings (lab) {
  if (!lab) return null;
  if (lab.labelPrinterHost) return { host: lab.labelPrinterHost, port: Number(lab.labelPrinterPort) || 9100 };
  if (lab.labelPrinterName) return { name: lab.labelPrinterName };
  return null;
}

export async function printZpl (zpl, printer = null) {
  try {
    const res = await agent.post('/print/zpl', { zpl, printer: printer || null });
    return res.data || { ok: true };
  } catch (e) {
    const err = new Error(e.response ? (e.response.data && (e.response.data.detail || e.response.data.title)) || `Агент друку: HTTP ${e.response.status}` : AGENT_UNREACHABLE_MESSAGE);
    err.agentUnreachable = !e.response;
    throw err;
  }
}

export async function printTest (printer = null) {
  try {
    const res = await agent.post('/print/test', { printer: printer || null });
    return res.data || { ok: true };
  } catch (e) {
    const err = new Error(e.response ? `Агент друку: HTTP ${e.response.status}` : AGENT_UNREACHABLE_MESSAGE);
    err.agentUnreachable = !e.response;
    throw err;
  }
}

export async function agentStatus () {
  try {
    const res = await agent.get('/status.json', { timeout: 2500 });
    return { online: true, data: res.data };
  } catch (e) {
    return { online: false };
  }
}

/** Друк SVG-етикетки через вікно браузера (fallback) */
export function printLabelsSvg (labels) {
  const w = window.open('', '_blank', 'width=420,height=600');
  if (!w) return false;
  const items = labels.map(l => `
    <div class="sticker">
      <div class="row"><b>${l.patientName || ''}</b><span>${l.collectedAt ? new Date(l.collectedAt).toLocaleString('uk-UA') : ''}</span></div>
      <div class="row small"><span>${l.orderNumber ? '№ ' + l.orderNumber : ''}</span><span>${(l.tube && (l.tube.name || l.tube)) || l.tubeName || ''}</span></div>
      <div class="bc">${l.svg || ''}</div>
      <div class="row"><span class="code">${l.barcode}</span><span class="small">MedLink LIS</span></div>
    </div>`).join('');
  w.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>Етикетки</title>
    <style>
      @page { size: 40mm 25mm; margin: 0; }
      body { margin: 0; font-family: Consolas, monospace; }
      .sticker { width: 40mm; height: 25mm; padding: 1.5mm 2mm; box-sizing: border-box; page-break-after: always; display: flex; flex-direction: column; justify-content: space-between; }
      .row { display: flex; justify-content: space-between; font-size: 8pt; }
      .small { font-size: 7pt; }
      .code { font-size: 12pt; font-weight: 700; letter-spacing: .1em; }
      .bc svg { width: 100%; height: 9mm; }
    </style></head><body>${items}<script>window.onload=function(){window.print();}<\/script></body></html>`);
  w.document.close();
  return true;
}
