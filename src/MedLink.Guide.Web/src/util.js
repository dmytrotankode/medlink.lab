// Дрібні утиліти SDK. Без залежностей. Синтаксис ES2017 (без ?. та ??) — щоб проходив webpack 4 / Quasar v1.

export function uid () {
  const c = typeof crypto !== 'undefined' && crypto.getRandomValues ? crypto : null;
  const b = new Uint8Array(16);
  if (c) c.getRandomValues(b); else for (let i = 0; i < 16; i++) b[i] = Math.floor(Math.random() * 256);
  b[6] = (b[6] & 0x0f) | 0x40; b[8] = (b[8] & 0x3f) | 0x80;
  const h = Array.prototype.map.call(b, x => ('0' + x.toString(16)).slice(-2)).join('');
  return h.slice(0, 8) + '-' + h.slice(8, 12) + '-' + h.slice(12, 16) + '-' + h.slice(16, 20) + '-' + h.slice(20);
}

export const sleep = ms => new Promise(r => setTimeout(r, ms));

/** Створення DOM-елемента: h('div', {class:'x', onclick:fn}, child, 'text') */
export function h (tag, attrs) {
  const el = document.createElement(tag);
  if (attrs) {
    Object.keys(attrs).forEach(k => {
      const v = attrs[k];
      if (v === undefined || v === null || v === false) return;
      if (k.slice(0, 2) === 'on' && typeof v === 'function') el.addEventListener(k.slice(2), v);
      else if (k === 'class') el.className = v;
      else if (k === 'html') el.innerHTML = v;
      else if (k === 'text') el.textContent = v;
      else if (k === 'style' && typeof v === 'object') Object.assign(el.style, v);
      else el.setAttribute(k, v === true ? '' : v);
    });
  }
  for (let i = 2; i < arguments.length; i++) append(el, arguments[i]);
  return el;
}
function append (el, c) {
  if (c === undefined || c === null || c === false) return;
  if (Array.isArray(c)) c.forEach(x => append(el, x));
  else el.appendChild(c.nodeType ? c : document.createTextNode(String(c)));
}

export function esc (s) {
  return String(s === undefined || s === null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
}

export function cssEscape (s) {
  if (typeof CSS !== 'undefined' && CSS.escape) return CSS.escape(s);
  return String(s).replace(/[^a-zA-Z0-9_ -￿-]/g, c => '\\' + c);
}

/** Встановлення значення у керований Vue/React input: нативний setter + події */
export function setNativeValue (el, value) {
  const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype
    : el.tagName === 'SELECT' ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
  const desc = Object.getOwnPropertyDescriptor(proto, 'value');
  if (desc && desc.set) desc.set.call(el, value); else el.value = value;
  el.dispatchEvent(new Event('input', { bubbles: true }));
  el.dispatchEvent(new Event('change', { bubbles: true }));
}

/** Збіг маршруту з шаблоном ('/laboratory/orders/:id'); query ігнорується, якщо його немає у шаблоні */
export function routeMatches (pattern, current) {
  if (!pattern) return true;
  const strip = s => String(s || '').split('#')[0];
  let p = strip(pattern); let c = strip(current);
  const pq = p.indexOf('?'); const cq = c.indexOf('?');
  const pPath = pq >= 0 ? p.slice(0, pq) : p;
  const cPath = cq >= 0 ? c.slice(0, cq) : c;
  const re = new RegExp('^' + pPath.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replace(/\\?:[A-Za-z_]+/g, '[^/]+').replace(/\/+$/, '') + '/?$');
  if (!re.test(cPath)) return false;
  if (pq >= 0) {
    const need = new URLSearchParams(p.slice(pq + 1)); const have = new URLSearchParams(cq >= 0 ? c.slice(cq + 1) : '');
    let ok = true; need.forEach((v, k) => { if (have.get(k) !== v) ok = false; });
    return ok;
  }
  return true;
}

export function isOurs (el) {
  while (el) { if (el.id === 'medlink-guide-host') return true; el = el.parentNode || el.host; }
  return false;
}

export function isVisible (el) {
  if (!el || !el.getBoundingClientRect) return false;
  const r = el.getBoundingClientRect();
  if (r.width === 0 && r.height === 0) return false;
  const cs = getComputedStyle(el);
  return cs.visibility !== 'hidden' && cs.display !== 'none' && cs.opacity !== '0';
}

/** Підстановка {{змінних}} */
export function interpolate (text, vars) {
  if (!text) return text || '';
  return String(text).replace(/\{\{\s*([^\s{}]+)\s*\}\}/g, (m, k) => (vars && vars[k] !== undefined ? vars[k] : m));
}

/** Визначення змінних сценарію → конкретні значення для запуску */
export function resolveVariables (defs) {
  const out = {};
  (defs || []).forEach(d => {
    const o = d.options || {};
    const rnd = n => Array.from({ length: n }, () => Math.floor(Math.random() * 10)).join('');
    switch (d.kind) {
      case 'random_number': out[d.name] = (o.prefix || '') + rnd(o.length || 6); break;
      case 'random_text': out[d.name] = (o.prefix || 'Тест') + ' ' + rnd(o.length || 4); break;
      case 'today': out[d.name] = localIso(new Date()).slice(0, 10); break;
      case 'now': out[d.name] = localIso(new Date()).slice(0, 16).replace('T', ' '); break;
      case 'pick': { const v = o.values || []; out[d.name] = v.length ? v[Math.floor(Math.random() * v.length)] : (d.defaultValue || ''); break; }
      default: out[d.name] = d.defaultValue || '';
    }
  });
  return out;
}

export function safeJson (s, fallback) { try { return JSON.parse(s); } catch (e) { return fallback; } }
export function store (key, value) {
  try {
    if (value === undefined) { const v = sessionStorage.getItem(key); return v ? JSON.parse(v) : null; }
    if (value === null) sessionStorage.removeItem(key); else sessionStorage.setItem(key, JSON.stringify(value));
  } catch (e) { /* приватний режим */ }
  return null;
}
export function lstore (key, value) {
  try {
    if (value === undefined) { const v = localStorage.getItem(key); return v ? JSON.parse(v) : null; }
    localStorage.setItem(key, JSON.stringify(value));
  } catch (e) { /* ignore */ }
  return null;
}

/** ISO-формат за локальним часом (а не UTC) */
function localIso (d) {
  const p = n => (n < 10 ? '0' : '') + n;
  return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) + 'T' + p(d.getHours()) + ':' + p(d.getMinutes());
}
