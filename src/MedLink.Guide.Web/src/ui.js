// Спільний Shadow DOM-хост (стилі гайда не конфліктують зі стилями системи) + CSS.
export const CSS_TEXT = `
:host{all:initial}
*{box-sizing:border-box;font-family:'Source Sans Pro','Segoe UI',Arial,sans-serif}
.layer{position:fixed;inset:0;pointer-events:none;z-index:1}
.hole{position:fixed;border-radius:8px;pointer-events:none;box-shadow:0 0 0 3px #2f7de1,0 0 0 9999px rgba(15,23,42,.58);transition:all .22s ease;z-index:1}
.hole.nodim{box-shadow:0 0 0 3px #2f7de1}
.hole.pulse::after{content:'';position:absolute;inset:-6px;border-radius:12px;border:2px solid #2f7de1;animation:pulse 1.4s infinite}
@keyframes pulse{0%{opacity:.9;transform:scale(1)}100%{opacity:0;transform:scale(1.08)}}
.blocker{position:fixed;pointer-events:auto;background:transparent;z-index:1}
.blocker.full{inset:0;background:rgba(15,23,42,.58)}
.tip{position:fixed;width:340px;max-width:calc(100vw - 24px);background:#fff;color:#1f2937;border-radius:12px;box-shadow:0 12px 40px rgba(0,0,0,.32);pointer-events:auto;z-index:3;font-size:14px;line-height:1.45;overflow:hidden}
.tip .bar{height:4px;background:#e5e7eb}.tip .bar>i{display:block;height:100%;background:#2f7de1;transition:width .3s}
.tip .hd{padding:12px 16px 0;display:flex;align-items:flex-start;gap:8px}
.tip .ttl{font-weight:700;font-size:16px;flex:1}
.tip .cnt{font-size:12px;color:#6b7280;white-space:nowrap;padding-top:3px}
.tip .crumb{margin:10px 16px 0;padding:2px 8px;border-left:4px solid #2f7de1;background:#f3f4f6;border-radius:4px;font-size:12px;color:#4b5563}
.tip .bd{padding:6px 16px 4px;white-space:pre-wrap}
.tip .val{margin:6px 16px;padding:6px 10px;background:#eff6ff;border:1px dashed #2f7de1;border-radius:8px;font-family:Consolas,monospace;font-size:13px}
.tip .warn{margin:6px 16px;padding:6px 10px;background:#fef3c7;border-radius:8px;color:#92400e;font-size:13px}
.tip .hint{margin:4px 16px;color:#2f7de1;font-size:12.5px}
.tip .ft{display:flex;gap:6px;padding:10px 12px 12px;align-items:center;flex-wrap:wrap}
.btn{border:1px solid #d1d5db;background:#fff;color:#1f2937;border-radius:8px;padding:6px 12px;font-size:13.5px;cursor:pointer}
.btn:hover{background:#f3f4f6}.btn.pri{background:#2f7de1;border-color:#2f7de1;color:#fff}.btn.pri:hover{background:#1f66c4}
.btn.dng{color:#b91c1c}.btn:disabled{opacity:.45;cursor:default}.btn.sm{padding:3px 8px;font-size:12px}
.sp{flex:1}
.fab{position:fixed;right:18px;bottom:18px;width:46px;height:46px;border-radius:50%;background:#2f7de1;color:#fff;border:0;font-size:24px;font-weight:700;cursor:pointer;box-shadow:0 6px 18px rgba(47,125,225,.5);pointer-events:auto;z-index:5}
.fab:hover{background:#1f66c4}
.drawer{position:fixed;top:0;right:0;bottom:0;width:380px;max-width:100vw;background:#fff;box-shadow:-8px 0 30px rgba(0,0,0,.25);pointer-events:auto;z-index:6;display:flex;flex-direction:column;color:#1f2937;font-size:14px}
.drawer .dh{padding:14px 16px;background:#2f7de1;color:#fff;display:flex;align-items:center;gap:8px}
.drawer .dh b{font-size:17px;flex:1}.drawer .dh button{background:rgba(255,255,255,.18);border:0;color:#fff;border-radius:6px;padding:4px 9px;cursor:pointer;font-size:13px}
.drawer .srch{padding:10px 12px;border-bottom:1px solid #e5e7eb}
.inp{width:100%;border:1px solid #d1d5db;border-radius:8px;padding:7px 10px;font-size:14px;outline:none;background:#fff;color:#1f2937}
.inp:focus{border-color:#2f7de1}textarea.inp{resize:vertical;min-height:70px}
.drawer .bodyl{flex:1;overflow:auto;padding:6px 8px 20px}
.sec{font-size:11.5px;text-transform:uppercase;letter-spacing:.06em;color:#6b7280;margin:12px 8px 4px}
.node{display:flex;align-items:center;gap:6px;padding:6px 8px;border-radius:8px;cursor:pointer}
.node:hover{background:#f3f4f6}.node.fold{font-weight:600}
.node .nm{flex:1}.node .ds{font-size:12px;color:#6b7280;display:block;font-weight:400}
.node .ok{color:#16a34a;font-weight:700}.node .tag{font-size:11px;background:#fef3c7;color:#92400e;border-radius:4px;padding:1px 5px}
.kids{margin-left:16px;border-left:1px solid #e5e7eb;padding-left:4px}
.empty{padding:24px;text-align:center;color:#6b7280}
.toast{position:fixed;left:50%;bottom:26px;transform:translateX(-50%);background:#1f2937;color:#fff;padding:9px 16px;border-radius:10px;z-index:9;pointer-events:none;font-size:13.5px}
/* титри та озвучення */
.narr{position:fixed;left:50%;bottom:12px;transform:translateX(-50%);width:min(780px,calc(100vw - 24px));background:rgba(17,24,39,.93);color:#fff;border-radius:12px;padding:10px 16px;pointer-events:auto;z-index:4;box-shadow:0 8px 28px rgba(0,0,0,.4)}
.narr .txt{font-size:16.5px;line-height:1.45;text-align:center;max-height:34vh;overflow:auto;margin-bottom:8px}
.narr .tools{display:flex;gap:6px;align-items:center;justify-content:center;flex-wrap:wrap}
.narr .tg{border:1px solid rgba(255,255,255,.4);background:transparent;color:#fff;border-radius:999px;padding:2px 11px;font-size:12px;cursor:pointer}
.narr .tg:hover{background:rgba(255,255,255,.15)}.narr .tg.on{background:#2f7de1;border-color:#2f7de1}
.narr .nh{font-size:11.5px;opacity:.6}
/* набори */
.setcard{margin:6px 4px 10px;padding:10px 12px 10px 14px;border-radius:10px;background:#f9fafb;border:1px solid #e5e7eb;border-left:6px solid #2f7de1}
.setcard .sh{display:flex;align-items:flex-start;gap:8px;cursor:pointer}.setcard .sh b{flex:1;font-size:14.5px}
.setcard .sm:not(.btn){font-size:12px;color:#6b7280;margin:2px 0 6px}.setcard .sd{font-size:12.5px;color:#4b5563;margin-bottom:6px}
.setcard .pbar{height:5px;border-radius:3px;background:#e5e7eb;margin:4px 0 8px;overflow:hidden}.setcard .pbar>i{display:block;height:100%}
.setcard .sitems{margin:2px 0 8px}.setcard .sitems .node{padding:3px 4px;font-size:13px}
.dot{display:inline-block;width:9px;height:9px;border-radius:50%;flex:none}
.node.inset{border-left:4px solid transparent}
/* конструктор */
.bld{position:fixed;left:14px;bottom:14px;width:400px;max-height:calc(100vh - 28px);background:#fff;color:#1f2937;border-radius:12px;box-shadow:0 12px 40px rgba(0,0,0,.35);pointer-events:auto;z-index:8;display:flex;flex-direction:column;font-size:13.5px;overflow:hidden}
.bld .bh{background:#1f2937;color:#fff;padding:9px 12px;display:flex;align-items:center;gap:8px;cursor:move}
.bld .bh b{flex:1}.bld .bh button{background:rgba(255,255,255,.15);border:0;color:#fff;border-radius:6px;padding:3px 8px;cursor:pointer}
.bld .bb{overflow:auto;padding:10px 12px;display:flex;flex-direction:column;gap:8px}
.bld .row{display:flex;gap:6px;align-items:center;flex-wrap:wrap}.bld label{font-size:12px;color:#6b7280;display:block;margin-bottom:2px}
.bld .grid2{display:grid;grid-template-columns:1fr 1fr;gap:6px}
.steps{border:1px solid #e5e7eb;border-radius:8px;max-height:190px;overflow:auto}
.st{display:flex;gap:6px;align-items:center;padding:5px 8px;border-bottom:1px solid #f3f4f6;cursor:pointer}
.st:last-child{border-bottom:0}.st.sel{background:#eff6ff}.st .n{width:22px;height:22px;border-radius:50%;background:#2f7de1;color:#fff;display:flex;align-items:center;justify-content:center;font-size:12px;flex:none}
.st .t{flex:1;min-width:0}.st .t b,.st .t i{display:block;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.st .t i{font-style:normal;font-size:11.5px;color:#6b7280}
.st .q{font-size:10.5px;border-radius:4px;padding:1px 4px}.q.high{background:#dcfce7;color:#166534}.q.medium{background:#fef9c3;color:#854d0e}.q.low{background:#fee2e2;color:#991b1b}
.mode{padding:6px 9px;border-radius:8px;background:#fef3c7;color:#92400e;font-size:12.5px}
.mic.on{background:#dc2626;color:#fff;border-color:#dc2626}
.mdl-back{position:fixed;inset:0;background:rgba(15,23,42,.5);pointer-events:auto;z-index:20;display:flex;align-items:center;justify-content:center}
.mdl{background:#fff;color:#1f2937;border-radius:12px;padding:16px;width:min(400px,calc(100vw - 24px));box-shadow:0 12px 40px rgba(0,0,0,.4);font-size:14px;line-height:1.45}
.mdl .mt{font-weight:700;font-size:16px;margin-bottom:8px}.mdl .mb{margin-bottom:12px;white-space:pre-wrap}.mdl .mf{display:flex;gap:8px;justify-content:flex-end}
.hover{position:fixed;border:2px solid #f59e0b;background:rgba(245,158,11,.14);pointer-events:none;z-index:2;border-radius:4px}
.hover.sel{border-color:#2f7de1;background:rgba(47,125,225,.14)}
.crumbs{position:fixed;z-index:9;pointer-events:auto;background:#1f2937;color:#fff;border-radius:8px;padding:4px 6px;display:flex;gap:2px;font-size:12px;max-width:90vw;flex-wrap:wrap;box-shadow:0 4px 14px rgba(0,0,0,.4)}
.crumbs span{padding:2px 7px;border-radius:5px;cursor:pointer;background:rgba(255,255,255,.1)}.crumbs span.on{background:#2f7de1}.crumbs em{opacity:.5;font-style:normal;padding:2px 0}
.draw{position:fixed;border:2px dashed #dc2626;background:rgba(220,38,38,.12);z-index:3;pointer-events:none}
.catch{position:fixed;inset:0;z-index:4;pointer-events:auto;cursor:crosshair;background:transparent}
.rec{display:inline-block;width:9px;height:9px;border-radius:50%;background:#dc2626;animation:blink 1s infinite;margin-right:4px}
@keyframes blink{50%{opacity:.2}}
@media (max-width:640px){.bld{width:calc(100vw - 20px);left:10px}.tip{width:calc(100vw - 24px)}}
`;

let host = null; let root = null; let layer = null;

export function getRoot () {
  if (root) return root;
  host = document.createElement('div');
  host.id = 'medlink-guide-host';
  host.style.cssText = 'all:initial;position:fixed;inset:0;z-index:2147483000;pointer-events:none';
  root = host.attachShadow({ mode: 'open' });
  const st = document.createElement('style'); st.textContent = CSS_TEXT; root.appendChild(st);
  layer = document.createElement('div'); layer.className = 'layer'; root.appendChild(layer);
  document.body.appendChild(host);
  return root;
}
export function getLayer () { getRoot(); return layer; }
export function destroyRoot () { if (host && host.parentNode) host.parentNode.removeChild(host); host = root = layer = null; }

let toastTimer = null;
export function toast (text, ms) {
  const r = getRoot();
  const old = r.querySelector('.toast'); if (old) old.remove();
  const t = document.createElement('div'); t.className = 'toast'; t.textContent = text; r.appendChild(t);
  clearTimeout(toastTimer); toastTimer = setTimeout(() => t.remove(), ms || 2600);
}

/** Власне вікно підтвердження замість window.confirm (кнопки українською). Повертає Promise<boolean>. */
export function askConfirm (text, okText, cancelText, title) {
  const r = getRoot();
  return new Promise(resolve => {
    const back = document.createElement('div'); back.className = 'mdl-back';
    const box = document.createElement('div'); box.className = 'mdl';
    const done = v => { back.remove(); resolve(v); };
    if (title) { const t = document.createElement('div'); t.className = 'mt'; t.textContent = title; box.appendChild(t); }
    const b = document.createElement('div'); b.className = 'mb'; b.textContent = text; box.appendChild(b);
    const f = document.createElement('div'); f.className = 'mf';
    const no = document.createElement('button'); no.className = 'btn'; no.textContent = cancelText || 'Скасувати'; no.onclick = () => done(false);
    const yes = document.createElement('button'); yes.className = 'btn pri'; yes.textContent = okText || 'Гаразд'; yes.onclick = () => done(true);
    f.appendChild(no); f.appendChild(yes); box.appendChild(f); back.appendChild(box);
    back.addEventListener('keydown', e => { if (e.key === 'Escape') done(false); });
    r.appendChild(back); yes.focus();
  });
}

/** Власне вікно введення тексту замість window.prompt. Повертає Promise<string|null> (null — скасовано). */
export function askText (title, placeholder, okText) {
  const r = getRoot();
  return new Promise(resolve => {
    const back = document.createElement('div'); back.className = 'mdl-back';
    const box = document.createElement('div'); box.className = 'mdl';
    const done = v => { back.remove(); resolve(v); };
    const t = document.createElement('div'); t.className = 'mt'; t.textContent = title; box.appendChild(t);
    const inp = document.createElement('input'); inp.className = 'inp'; inp.placeholder = placeholder || ''; inp.style.marginBottom = '12px';
    inp.addEventListener('keydown', e => { if (e.key === 'Enter') done(inp.value.trim() || null); if (e.key === 'Escape') done(null); });
    box.appendChild(inp);
    const f = document.createElement('div'); f.className = 'mf';
    const no = document.createElement('button'); no.className = 'btn'; no.textContent = 'Скасувати'; no.onclick = () => done(null);
    const yes = document.createElement('button'); yes.className = 'btn pri'; yes.textContent = okText || 'Створити'; yes.onclick = () => done(inp.value.trim() || null);
    f.appendChild(no); f.appendChild(yes); box.appendChild(f); back.appendChild(box);
    r.appendChild(back); inp.focus();
  });
}
