// Якорі: побудова багатьох стратегій пошуку елемента + відбиток (fingerprint) і самовідновлення.
// Принцип: якір не «один селектор», а набір стратегій за вагою; якщо жодна не спрацювала — нечіткий пошук за відбитком.
import { cssEscape, isVisible, isOurs } from './util.js';

const TESTID_ATTRS = ['data-guide-id', 'data-testid', 'data-test', 'data-cy', 'data-qa', 'data-test-id'];
// Схожі на згенеровані значення (Quasar q-*, uid, GUID, довгі цифри) — нестабільні, не використовуємо
const DYNAMIC = /(\d{3,})|([0-9a-f]{8}-[0-9a-f]{4})|^q-|^f_|^v-|__|uid|^ember|^react|^radix|^:r/i;

export function isDynamic (v) { return !v || DYNAMIC.test(v); }
const norm = s => String(s || '').replace(/\s+/g, ' ').trim();

function unique (sel, root) {
  try { const n = (root || document).querySelectorAll(sel); return n.length === 1; } catch (e) { return false; }
}
function stableClasses (el) {
  return Array.prototype.slice.call(el.classList || []).filter(c => !isDynamic(c) && !/^(active|focus|hover|disabled|selected|q-)/.test(c));
}
export function ownText (el) {
  let t = '';
  Array.prototype.forEach.call(el.childNodes, n => { if (n.nodeType === 3) t += n.nodeValue; });
  t = norm(t);
  return (t || norm(el.textContent)).slice(0, 80);
}
export function labelOf (el) {
  if (el.labels && el.labels.length) return norm(el.labels[0].textContent).slice(0, 80);
  const aria = el.getAttribute && el.getAttribute('aria-label');
  if (aria) return norm(aria);
  const f = el.closest && el.closest('.q-field, .form-group, label, .field');
  if (f) { const l = f.querySelector('label, .q-field__label'); if (l) return norm(l.textContent).slice(0, 80); }
  return '';
}

/** Стабільний css-шлях від найближчого предка з тест-ідентифікатором/ id */
function cssPath (el) {
  const parts = [];
  let cur = el;
  for (let depth = 0; cur && cur.nodeType === 1 && cur !== document.body && depth < 8; depth++) {
    const stableAttr = TESTID_ATTRS.filter(a => cur.getAttribute(a))[0];
    if (stableAttr) { parts.unshift('[' + stableAttr + '="' + cur.getAttribute(stableAttr).replace(/"/g, '\\"') + '"]'); break; }
    if (cur.id && !isDynamic(cur.id)) { parts.unshift('#' + cssEscape(cur.id)); break; }
    let seg = cur.tagName.toLowerCase();
    const cls = stableClasses(cur).slice(0, 2);
    if (cls.length) seg += '.' + cls.map(cssEscape).join('.');
    const parent = cur.parentElement;
    if (parent) {
      const same = Array.prototype.filter.call(parent.children, x => x.tagName === cur.tagName);
      if (same.length > 1) seg += ':nth-of-type(' + (same.indexOf(cur) + 1) + ')';
    }
    parts.unshift(seg);
    cur = cur.parentElement;
  }
  return parts.join(' > ');
}

export function ancestorsOf (el, n) {
  const out = []; let cur = el.parentElement;
  while (cur && cur !== document.body && out.length < (n || 4)) {
    const a = {};
    a.tag = cur.tagName.toLowerCase();
    const tid = TESTID_ATTRS.filter(x => cur.getAttribute(x))[0];
    if (tid) a.testid = cur.getAttribute(tid);
    if (cur.id && !isDynamic(cur.id)) a.id = cur.id;
    const cls = stableClasses(cur).slice(0, 2); if (cls.length) a.classes = cls;
    out.push(a); cur = cur.parentElement;
  }
  return out;
}

export function buildFingerprint (el) {
  const attrs = {};
  ['name', 'type', 'placeholder', 'aria-label', 'role', 'title', 'for', 'alt'].forEach(k => { const v = el.getAttribute(k); if (v) attrs[k] = v; });
  const href = el.getAttribute('href'); if (href) attrs.href = href.split('?')[0];
  const r = el.getBoundingClientRect();
  return {
    tag: el.tagName.toLowerCase(), text: ownText(el), classes: stableClasses(el).slice(0, 6), attrs: attrs,
    label: labelOf(el), ancestors: ancestorsOf(el, 4),
    rectRatio: { x: +(r.left / innerWidth).toFixed(3), y: +(r.top / innerHeight).toFixed(3), w: +(r.width / innerWidth).toFixed(3), h: +(r.height / innerHeight).toFixed(3) }
  };
}

/** Побудова набору стратегій для елемента. Кожна — {type,value,weight}; перевіряється унікальність. */
export function buildStrategies (el) {
  const s = [];
  const tag = el.tagName.toLowerCase();
  TESTID_ATTRS.forEach((a, i) => {
    const v = el.getAttribute(a);
    if (!v) return;
    const sel = '[' + a + '="' + v.replace(/"/g, '\\"') + '"]';
    if (unique(sel)) s.push({ type: a === 'data-guide-id' ? 'guide' : 'testid', value: sel, weight: 100 - i });
    else if (unique(tag + sel)) s.push({ type: 'testid', value: tag + sel, weight: 95 - i });
  });
  if (el.id && !isDynamic(el.id) && unique('#' + cssEscape(el.id))) s.push({ type: 'id', value: '#' + cssEscape(el.id), weight: 90 });
  ['aria-label', 'name', 'placeholder', 'title'].forEach((a, i) => {
    const v = el.getAttribute(a);
    if (v && v.length < 80) {
      const sel = tag + '[' + a + '="' + v.replace(/"/g, '\\"') + '"]';
      if (unique(sel)) s.push({ type: a === 'aria-label' ? 'aria' : 'attr', value: sel, weight: 75 - i * 3 });
    }
  });
  const text = ownText(el);
  if (text && text.length >= 2 && text.length <= 60 && ['button', 'a', 'label', 'span', 'div', 'li', 'td', 'th', 'h1', 'h2', 'h3', 'h4'].indexOf(tag) >= 0) {
    s.push({ type: 'text', value: tag + '|' + text, weight: 60 });
  }
  const path = cssPath(el);
  if (path) s.push({ type: 'css', value: path, weight: 45 });
  s.sort((a, b) => b.weight - a.weight);
  return s;
}

export function buildAnchor (el, caption) {
  return { caption: caption || describe(el), strategies: buildStrategies(el), fingerprint: buildFingerprint(el), parentLevels: 0, healthStatus: 'unknown' };
}

export function describe (el) {
  const t = ownText(el) || labelOf(el) || el.getAttribute('placeholder') || el.getAttribute('aria-label') || el.getAttribute('title') || '';
  return (el.tagName.toLowerCase() + (t ? ' «' + t.slice(0, 40) + '»' : '')).trim();
}

// ---------------------------------------------------------------------------------------------------
const TEXT_TAGS = 'button, a, label, span, div, li, td, th, p, b, strong, h1, h2, h3, h4, h5, h6, summary';
function queryStrategy (st, doc, fp) {
  const out = [];
  try {
    if (st.type === 'text') {
      // формат «тег|текст»; без тегу («текст») — шукаємо серед будь-яких текстових елементів (тег з відбитку, якщо є)
      const idx = st.value.indexOf('|');
      const tag = idx >= 0 ? st.value.slice(0, idx) : ((fp && fp.tag) || TEXT_TAGS);
      const text = (idx >= 0 ? st.value.slice(idx + 1) : st.value).toLowerCase();
      const scan = sel => Array.prototype.forEach.call(doc.querySelectorAll(sel), el => { if (ownText(el).toLowerCase() === text) out.push(el); });
      scan(tag);
      // тег змінився після оновлення інтерфейсу (span → div тощо): той самий текст шукаємо серед будь-яких текстових елементів
      if (!out.length && idx >= 0) scan(TEXT_TAGS);
    } else {
      Array.prototype.forEach.call(doc.querySelectorAll(st.value), el => out.push(el));
    }
  } catch (e) { /* некоректний селектор */ }
  return out.filter(el => !isOurs(el));
}

function jaccard (a, b) {
  if (!a.length && !b.length) return 1;
  const A = {}; a.forEach(x => { A[x] = 1; });
  let inter = 0; const U = Object.assign({}, A);
  b.forEach(x => { if (A[x]) inter++; U[x] = 1; });
  return inter / Object.keys(U).length;
}
const words = s => norm(s).toLowerCase().split(/[^a-zа-яіїєґ0-9]+/i).filter(Boolean);

/** Оцінка схожості елемента на відбиток 0..1 */
export function score (el, fp) {
  if (!fp || !fp.tag) return 0;
  if (el.tagName.toLowerCase() !== fp.tag) return 0;
  let sc = 0.2;
  sc += 0.25 * jaccard(words(ownText(el)), words(fp.text));
  const keys = Object.keys(fp.attrs || {});
  if (keys.length) { let m = 0; keys.forEach(k => { if (el.getAttribute(k) === fp.attrs[k] || (k === 'href' && (el.getAttribute('href') || '').split('?')[0] === fp.attrs[k])) m++; }); sc += 0.2 * m / keys.length; } else sc += 0.1;
  sc += 0.1 * jaccard(stableClasses(el), fp.classes || []);
  sc += 0.05 * (fp.label ? (labelOf(el) === fp.label ? 1 : 0) : 0.5);
  const anc = ancestorsOf(el, 4); const fa = fp.ancestors || [];
  let am = 0; fa.forEach((a, i) => { const b = anc[i]; if (b && b.tag === a.tag && (a.testid ? b.testid === a.testid : a.id ? b.id === a.id : true)) am++; });
  sc += 0.15 * (fa.length ? am / fa.length : 0.5);
  if (fp.rectRatio) {
    const r = el.getBoundingClientRect();
    const d = Math.abs(r.left / innerWidth - fp.rectRatio.x) + Math.abs(r.top / innerHeight - fp.rectRatio.y);
    sc += 0.05 * Math.max(0, 1 - d * 2);
  }
  return sc;
}

/**
 * Знайти елемент якоря. Повертає {el, how, confidence, healed} або null.
 * how: назва спрацювавшої стратегії; healed=true — знайдено нечітким збігом (якір варто оновити).
 */
export function resolveAnchor (anchor, doc, noFuzzy) {
  doc = doc || document;
  if (!anchor) return null;
  const strategies = (anchor.strategies || []).slice().sort((a, b) => b.weight - a.weight);
  for (let i = 0; i < strategies.length; i++) {
    const st = strategies[i];
    let c = queryStrategy(st, doc, anchor.fingerprint).filter(isVisible);
    if (!c.length) continue;
    if (c.length > 1 && anchor.fingerprint) {
      c = c.map(el => ({ el: el, s: score(el, anchor.fingerprint) })).sort((a, b) => b.s - a.s).map(x => x.el);
    }
    if (c.length === 1 || st.weight >= 45) return { el: c[0], how: st.type, confidence: Math.min(1, st.weight / 100 + 0.1), healed: false };
  }
  // нечіткий пошук за відбитком
  const fp = anchor.fingerprint;
  if (fp && fp.tag && !noFuzzy) {
    let best = null; let second = 0;
    Array.prototype.forEach.call(doc.querySelectorAll(fp.tag), el => {
      if (isOurs(el) || !isVisible(el)) return;
      const s = score(el, fp);
      if (!best || s > best.s) { if (best) second = best.s; best = { el: el, s: s }; } else if (s > second) second = s;
    });
    if (best && best.s >= 0.8 && best.s - second >= 0.08) return { el: best.el, how: 'fuzzy', confidence: +best.s.toFixed(3), healed: true };
  }
  return null;
}

/** Предок на N рівнів вище (для підсвічування батьківського блоку) */
export function climb (el, levels) {
  let cur = el;
  for (let i = 0; i < (levels || 0); i++) { if (cur.parentElement && cur.parentElement !== document.body) cur = cur.parentElement; else break; }
  return cur;
}

/** Найближчий інтерактивний елемент (для запису кліків) */
export function interactiveTarget (el) {
  const sel = 'button, a[href], input, select, textarea, [role=button], [role=tab], [role=menuitem], [role=option], [role=checkbox], [role=radio], .q-btn, .q-item, .q-tab, .q-checkbox, .q-toggle, .q-radio, .q-chip, label, summary';
  return (el.closest && el.closest(sel)) || el;
}

/** Чи є мінімум одна стабільна стратегія (інакше крок потребує уваги) */
export function anchorQuality (anchor) {
  const best = (anchor.strategies || []).reduce((m, s) => Math.max(m, s.weight), 0);
  return best >= 90 ? 'high' : best >= 60 ? 'medium' : 'low';
}

/** Оновлення якоря даними знайденого елемента (після самовідновлення) */
export function refreshAnchor (anchor, el) {
  anchor.strategies = buildStrategies(el);
  anchor.fingerprint = buildFingerprint(el);
  return anchor;
}
