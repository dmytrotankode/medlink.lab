// Мінімальне керування наборами інструкцій у конструкторі: створити / перейменувати / колір / додати-прибрати інструкції та розділи / порядок.
// Вікно відкривається з конструктора (кнопка «Набори») або з панелі допомоги (для редакторів). Зміни складу набору зберігаються одразу.
import { h } from './util.js';
import { getLayer, toast, askConfirm, askText } from './ui.js';

const PALETTE = '#2f7de1';

export class SetsEditor {
  constructor (ctx) { this.ctx = ctx; this.el = null; this.sets = []; this.folders = []; this.scenarios = []; this.sel = null; this.pos = null; }

  async open (selId) {
    try {
      const tree = await this.ctx.api.tree();
      this.folders = tree.folders || []; this.scenarios = tree.scenarios || [];
      this.sets = (await this.ctx.api.listSets()) || [];
    } catch (e) { toast('Не вдалося завантажити набори: ' + e.message, 5000); return; }
    this.sel = selId || (this.sets[0] && this.sets[0].id) || null;
    this.render();
  }

  close () { if (this.el) { this.el.remove(); this.el = null; } }

  cur () { return this.sets.find(s => s.id === this.sel) || null; }

  name (it) {
    if (it.scenarioId) { const s = this.scenarios.find(x => x.id === it.scenarioId); return s ? s.caption + (s.status !== 'published' ? ' (чернетка)' : '') : '(видалена інструкція)'; }
    const f = this.folders.find(x => x.id === it.folderId); return f ? f.caption : '(видалений розділ)';
  }

  async create () {
    const name = await askText('Назва нового набору', 'Наприклад: Огляд для нових співробітників'); if (!name) return;
    try {
      const code = 'set-' + Date.now().toString(36);
      const s = await this.ctx.api.createSet({ caption: name.trim(), code: code, color: PALETTE, sortOrder: this.sets.length });
      this.sets.push(Object.assign({ items: [] }, s)); this.sel = s.id; this.render();
    } catch (e) { toast('Помилка: ' + e.message, 5000); }
  }

  async saveMeta () {
    const s = this.cur(); if (!s) return;
    if (!String(s.caption || '').trim()) { toast('Вкажіть назву набору'); return; }
    try {
      const r = await this.ctx.api.updateSet(s.id, { caption: s.caption.trim(), code: s.code, description: s.description || '', color: s.color || PALETTE, icon: s.icon || null, sortOrder: s.sortOrder || 0 });
      Object.assign(s, { caption: r.caption, color: r.color }); toast('Набір збережено'); this.render();
    } catch (e) { toast('Помилка: ' + e.message, 5000); }
  }

  async remove () {
    const s = this.cur(); if (!s || !(await askConfirm('Видалити набір «' + s.caption + '»? Самі інструкції залишаться. Дію не можна відмінити.', 'Видалити набір', 'Скасувати'))) return;
    try { await this.ctx.api.deleteSet(s.id); this.sets = this.sets.filter(x => x.id !== s.id); this.sel = this.sets[0] ? this.sets[0].id : null; this.render(); } catch (e) { toast('Помилка: ' + e.message, 5000); }
  }

  async saveItems (items) {
    const s = this.cur();
    try {
      const r = await this.ctx.api.setItems(s.id, items.map(i => (i.scenarioId ? { scenarioId: i.scenarioId } : { folderId: i.folderId })));
      s.items = (r && r.items) || items; this.render();
    } catch (e) { toast('Помилка: ' + e.message, 5000); }
  }

  render () {
    if (this.el) this.el.remove();
    const s = this.cur();
    const body = [];
    const selSet = h('select', { class: 'inp' }, this.sets.map(x => h('option', { value: x.id, text: x.caption, selected: x.id === this.sel })));
    selSet.addEventListener('change', () => { this.sel = selSet.value; this.render(); });
    body.push(h('div', { class: 'row' }, this.sets.length ? selSet : h('span', { text: 'Наборів ще немає', style: { color: '#6b7280' } }), h('button', { class: 'btn pri', text: '＋ Новий набір', onclick: () => this.create() })));

    if (s) {
      const cap = h('input', { class: 'inp', value: s.caption || '', placeholder: 'Назва набору' }); cap.addEventListener('input', () => { s.caption = cap.value; });
      const color = h('input', { type: 'color', value: /^#[0-9a-fA-F]{6}$/.test(s.color || '') ? s.color : PALETTE, title: 'Колір набору', style: { width: '44px', height: '30px', padding: '0', border: '1px solid #d1d5db', borderRadius: '6px', background: '#fff' } });
      color.addEventListener('input', () => { s.color = color.value; });
      const desc = h('textarea', { class: 'inp', placeholder: 'Короткий опис (необов’язково)' }, s.description || ''); desc.addEventListener('input', () => { s.description = desc.value; });
      body.push(h('div', null, h('label', { text: 'Назва та колір' }), h('div', { class: 'row' }, cap, color)));
      body.push(h('div', null, h('label', { text: 'Опис' }), desc));
      body.push(h('div', { class: 'row' }, h('button', { class: 'btn', text: '💾 Зберегти назву/колір', onclick: () => this.saveMeta() }), h('span', { class: 'sp' }), h('button', { class: 'btn dng', text: '🗑 Видалити набір', onclick: () => this.remove() })));

      const items = (s.items || []).slice();
      const list = h('div', { class: 'steps' });
      if (!items.length) list.appendChild(h('div', { class: 'empty', text: 'У наборі ще нічого немає. Додайте розділ або інструкцію.' }));
      items.forEach((it, i) => {
        const mv = d => { const j = i + d; const t = items[i]; items[i] = items[j]; items[j] = t; this.saveItems(items); };
        list.appendChild(h('div', { class: 'st' },
          h('span', { text: it.folderId ? 'Розділ' : '▶' }),
          h('span', { class: 't' }, h('b', { text: this.name(it) }), h('i', { text: it.folderId ? 'розділ — усі його опубліковані інструкції' : 'інструкція' })),
          h('button', { class: 'btn sm', text: '↑', disabled: i === 0, onclick: () => mv(-1) }),
          h('button', { class: 'btn sm', text: '↓', disabled: i === items.length - 1, onclick: () => mv(1) }),
          h('button', { class: 'btn sm dng', text: '✕', title: 'Прибрати з набору', onclick: () => { items.splice(i, 1); this.saveItems(items); } })));
      });
      body.push(h('div', null, h('label', { text: 'Склад набору (порядок = порядок показу)' }), list));

      const used = it => items.some(x => (it.scenarioId && x.scenarioId === it.scenarioId) || (it.folderId && x.folderId === it.folderId));
      const filt = h('input', { class: 'inp', placeholder: 'Пошук інструкції або розділу', value: this.filter || '' });
      const addSel = h('select', { class: 'inp' });
      const fillOptions = () => {
        const q = (this.filter || '').trim().toLowerCase();
        const ok = t => !q || String(t).toLowerCase().indexOf(q) >= 0;
        addSel.innerHTML = '';
        addSel.appendChild(h('option', { value: '', text: '— додати розділ або інструкцію (знайдено: ' + (this.folders.filter(f => !used({ folderId: f.id }) && ok(f.caption)).length + this.scenarios.filter(x => !used({ scenarioId: x.id }) && ok(x.caption)).length) + ') —' }));
        addSel.appendChild(h('optgroup', { label: 'Розділи' }, this.folders.filter(f => !used({ folderId: f.id }) && ok(f.caption)).map(f => h('option', { value: 'f:' + f.id, text: f.caption }))));
        addSel.appendChild(h('optgroup', { label: 'Інструкції' }, this.scenarios.filter(x => !used({ scenarioId: x.id }) && ok(x.caption)).map(x => h('option', { value: 's:' + x.id, text: x.caption + (x.status !== 'published' ? ' (чернетка)' : '') }))));
      };
      filt.addEventListener('input', () => { this.filter = filt.value; fillOptions(); });
      fillOptions();
      body.push(h('div', null, filt));
      body.push(h('div', { class: 'row' }, addSel, h('button', { class: 'btn pri', text: '＋ Додати', onclick: () => {
        const v = addSel.value; if (!v) return;
        items.push(v[0] === 'f' ? { folderId: v.slice(2) } : { scenarioId: v.slice(2) }); this.saveItems(items);
      } })));
      body.push(h('div', { style: { fontSize: '11.5px', color: '#6b7280' }, text: 'У панелі допомоги набір з’явиться, коли в ньому є хоча б одна опублікована інструкція. Розділи й інструкції набору підсвічуються його кольором.' }));
    }

    const head = h('div', { class: 'bh' }, h('b', { text: 'Набори інструкцій' }), h('button', { text: '✕', title: 'Закрити', onclick: () => this.close() }));
    this.el = h('div', { class: 'bld' }, head, h('div', { class: 'bb' }, body));
    this.el.style.left = 'auto'; this.el.style.right = '14px';
    if (this.pos) { this.el.style.left = this.pos.x + 'px'; this.el.style.right = 'auto'; this.el.style.top = this.pos.y + 'px'; this.el.style.bottom = 'auto'; }
    head.addEventListener('mousedown', e => {
      if (e.target.tagName === 'BUTTON') return;
      const r = this.el.getBoundingClientRect(); const dx = e.clientX - r.left; const dy = e.clientY - r.top;
      const mv = ev => { this.pos = { x: Math.max(0, ev.clientX - dx), y: Math.max(0, ev.clientY - dy) }; this.el.style.left = this.pos.x + 'px'; this.el.style.right = 'auto'; this.el.style.top = this.pos.y + 'px'; this.el.style.bottom = 'auto'; };
      const up = () => { document.removeEventListener('mousemove', mv); document.removeEventListener('mouseup', up); };
      document.addEventListener('mousemove', mv); document.addEventListener('mouseup', up);
    });
    getLayer().appendChild(this.el);
  }
}
