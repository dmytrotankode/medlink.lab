// Оверлей: підсвічування зони (spotlight) + підказка з кнопками. Стежить за елементом (скрол/ресайз/анімації).
import { h } from './util.js';
import { getLayer } from './ui.js';

let hole = null; let blockers = []; let tip = null; let raf = 0; let getRect = null; let cfg = null; let lastKey = '';

function ensure () {
  const layer = getLayer();
  if (!hole) { hole = h('div', { class: 'hole pulse' }); layer.appendChild(hole); }
}

function clear () {
  cancelAnimationFrame(raf); raf = 0; getRect = null; lastKey = '';
  if (hole) { hole.remove(); hole = null; }
  blockers.forEach(b => b.remove()); blockers = [];
  if (tip) { tip.remove(); tip = null; }
}

function setBlockers (r, block, dim) {
  blockers.forEach(b => b.remove()); blockers = [];
  if (!block) return;
  const layer = getLayer();
  const mk = (x, y, w, hh, full) => { const b = h('div', { class: 'blocker' + (full ? ' full' : '') }); b.style.cssText = full ? '' : `left:${x}px;top:${y}px;width:${Math.max(0, w)}px;height:${Math.max(0, hh)}px`; layer.insertBefore(b, layer.firstChild); blockers.push(b); };
  if (!r) { mk(0, 0, 0, 0, true); return; }
  const W = innerWidth; const H = innerHeight;
  mk(0, 0, W, r.top); mk(0, r.top + r.height, W, H - r.top - r.height);
  mk(0, r.top, r.left, r.height); mk(r.left + r.width, r.top, W - r.left - r.width, r.height);
}

function placeTip (r, placement) {
  if (!tip) return;
  const tw = tip.offsetWidth; const th = tip.offsetHeight; const W = innerWidth; const H = innerHeight; const g = 14;
  let x; let y;
  if (!r || placement === 'center') { x = (W - tw) / 2; y = (H - th) / 2; } else {
    const space = { bottom: H - (r.top + r.height), top: r.top, right: W - (r.left + r.width), left: r.left };
    let side = placement;
    if (!side || side === 'auto' || space[side] < (side === 'top' || side === 'bottom' ? th : tw) + g) {
      side = ['bottom', 'top', 'right', 'left'].sort((a, b) => space[b] - space[a])[0];
      if (space.bottom >= th + g) side = 'bottom'; else if (space.top >= th + g) side = 'top';
    }
    if (side === 'bottom') { x = r.left + r.width / 2 - tw / 2; y = r.top + r.height + g; }
    else if (side === 'top') { x = r.left + r.width / 2 - tw / 2; y = r.top - th - g; }
    else if (side === 'right') { x = r.left + r.width + g; y = r.top + r.height / 2 - th / 2; }
    else { x = r.left - tw - g; y = r.top + r.height / 2 - th / 2; }
  }
  tip.style.left = Math.max(8, Math.min(W - tw - 8, x)) + 'px';
  tip.style.top = Math.max(8, Math.min(H - th - 8, y)) + 'px';
}

function loop () {
  raf = requestAnimationFrame(loop);
  if (!cfg) return;
  const r = getRect ? getRect() : null;
  const key = r ? [r.left, r.top, r.width, r.height, innerWidth, innerHeight, tip ? tip.offsetHeight : 0].map(Math.round).join(',') : 'none' + (tip ? tip.offsetHeight : 0);
  if (key === lastKey) return;
  lastKey = key;
  if (r && hole) {
    const p = cfg.padding === undefined ? 6 : cfg.padding;
    const hr = { left: r.left - p, top: r.top - p, width: r.width + p * 2, height: r.height + p * 2 };
    hole.style.cssText = `left:${hr.left}px;top:${hr.top}px;width:${hr.width}px;height:${hr.height}px;display:block`;
    hole.className = 'hole pulse' + (cfg.dim === false ? ' nodim' : '');
    setBlockers(hr, cfg.blockOutside, cfg.dim !== false);
    placeTip(hr, cfg.placement);
  } else if (hole) {
    hole.style.display = 'none';
    setBlockers(null, cfg.blockOutside !== false && cfg.modalCenter !== false, true);
    placeTip(null, 'center');
  }
}

/**
 * show({ target: ()=>DOMRect|null, padding, placement, blockOutside, title, body, valueHint, warn, hint,
 *        index, total, buttons:[{text, kind:'pri'|'dng'|'', onClick, disabled}] })
 */
export function show (o) {
  ensure();
  cfg = o; getRect = o.target || null; lastKey = '';
  if (tip) tip.remove();
  const pct = o.total ? Math.round(((o.index + 1) / o.total) * 100) : 0;
  tip = h('div', { class: 'tip' },
    o.total ? h('div', { class: 'bar' }, h('i', { style: { width: pct + '%' } })) : null,
    h('div', { class: 'hd' }, h('div', { class: 'ttl', text: o.title || '' }), o.total ? h('div', { class: 'cnt', text: (o.index + 1) + ' / ' + o.total }) : null),
    o.body ? h('div', { class: 'bd', text: o.body }) : null,
    o.valueHint ? h('div', { class: 'val', text: o.valueHint }) : null,
    o.warn ? h('div', { class: 'warn', text: o.warn }) : null,
    o.hint ? h('div', { class: 'hint', text: o.hint }) : null,
    h('div', { class: 'ft' }, (o.buttons || []).map(b => b.spacer ? h('span', { class: 'sp' }) : h('button', { class: 'btn ' + (b.kind || ''), text: b.text, disabled: b.disabled, onclick: b.onClick })))
  );
  getLayer().appendChild(tip);
  if (!raf) raf = requestAnimationFrame(loop);
}

export function hide () { cfg = null; clear(); }
export function isShown () { return !!cfg; }
export function flash (el, ms) {
  show({ target: () => el.getBoundingClientRect(), padding: 4, blockOutside: false, dim: false, title: '', buttons: [] });
  if (tip) tip.style.display = 'none';
  setTimeout(hide, ms || 1400);
}
