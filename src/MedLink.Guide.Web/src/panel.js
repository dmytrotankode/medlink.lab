// Панель допомоги: кнопка «?» та шухляда з деревом сценаріїв (розділи → інструкції), пошуком і підказкою «для цієї сторінки».
import { h, routeMatches, lstore } from './util.js';
import { getLayer } from './ui.js';

/** Український відмінок за числом: plural(5, ['інструкція', 'інструкції', 'інструкцій']) */
export function plural (n, forms) {
  const a = Math.abs(n) % 100; const b = a % 10;
  if (a > 10 && a < 20) return forms[2];
  if (b > 1 && b < 5) return forms[1];
  if (b === 1) return forms[0];
  return forms[2];
}

export class HelpPanel {
  constructor (ctx) { this.ctx = ctx; this.drawer = null; this.fab = null; this.open = {}; this.q = ''; this.data = null; this.done = {}; this.openSets = {}; }

  mountButton () {
    const o = this.ctx.opts;
    if (o.button === false) return;
    if (o.button && o.button.mount) { // вбудовування у шапку системи (елемент, наданий хостом)
      const b = h('button', { class: 'mlg-help-btn', title: 'Інструкції', 'aria-label': 'Інструкції', onclick: () => this.toggle() }, '?');
      b.style.cssText = 'width:32px;height:32px;border-radius:50%;border:0;background:#2f7de1;color:#fff;font-weight:700;font-size:18px;cursor:pointer';
      o.button.mount.appendChild(b); this.fab = b; return;
    }
    this.fab = h('button', { class: 'fab', title: 'Інструкції', 'aria-label': 'Інструкції', onclick: () => this.toggle() }, '?');
    getLayer().appendChild(this.fab);
  }

  toggle () { if (this.drawer) this.close(); else this.show(); }
  close () { if (this.drawer) { this.drawer.remove(); this.drawer = null; } }

  async load () {
    const role = this.ctx.opts.getRole ? this.ctx.opts.getRole() : '';
    try {
      const editor = this.ctx.canEdit();
      const tree = editor ? await this.ctx.api.tree() : null;
      let cat = null;
      try { cat = await this.ctx.api.catalog(role, this.ctx.host.currentRoute()); } catch (e) { if (!editor) throw e; }
      this.data = editor ? tree : cat;
      this.data.sets = (cat && cat.sets) || [];
      if (editor && cat) { // редактору показуємо повне дерево, але набори й кольори беремо з каталогу
        const byId = arr => { const m = {}; (arr || []).forEach(x => { m[x.id] = x; }); return m; };
        const cs = byId(cat.scenarios); const cf = byId(cat.folders);
        (tree.scenarios || []).forEach(s => {
          const c = cs[s.id];
          if (c) {
            s.sets = c.sets || []; s.setCodes = c.setCodes || [];
            if (!s.startRoute) s.startRoute = c.startRoute; // для блоку «Для цієї сторінки»
            if (!s.description) s.description = c.description;
          }
        });
        (tree.folders || []).forEach(f => { const c = cf[f.id]; if (c) { f.sets = c.sets || []; f.setCodes = c.setCodes || []; } });
      }
      const pr = await this.ctx.api.myProgress();
      this.done = {}; (pr || []).forEach(p => { if (p.status === 'completed') this.done[p.scenarioId] = true; });
      const local = lstore('mlg.done') || {}; Object.keys(local).forEach(k => { this.done[k] = true; });
    } catch (e) { this.data = { folders: [], scenarios: [], error: e.message }; }
  }

  async show () {
    this.close();
    this.drawer = h('div', { class: 'drawer' });
    getLayer().appendChild(this.drawer);
    this.drawer.appendChild(h('div', { class: 'empty', text: 'Завантаження…' }));
    await this.load();
    this.render();
  }

  render () {
    if (!this.drawer) return;
    const editor = this.ctx.canEdit();
    const d = this.data;
    const q = this.q.trim().toLowerCase();
    const list = h('div', { class: 'bodyl' });
    const matches = s => !q || (s.caption + ' ' + (s.description || '') + ' ' + (s.tags || []).join(' ')).toLowerCase().indexOf(q) >= 0;

    // підсвічування приналежності до наборів: кольорова смужка зліва + крапки з підказкою
    const marks = o => (o && o.sets && o.sets.length ? o.sets : []);
    const decorate = (el, o) => { const m = marks(o); if (m.length) { el.classList.add('inset'); el.style.borderLeftColor = m[0].color || '#2f7de1'; el.title = 'Набір: ' + m.map(x => x.caption).join(', '); } return el; };
    const dots = o => marks(o).map(x => h('span', { class: 'dot', title: 'Набір: ' + x.caption, style: { background: x.color || '#2f7de1' } }));

    const item = s => decorate(h('div', { class: 'node', onclick: () => this.startScenario(s, 'guided') },
      h('span', { text: this.done[s.id] ? '✔' : '▶', class: this.done[s.id] ? 'ok' : '' }),
      h('span', { class: 'nm' }, s.caption, s.description ? h('span', { class: 'ds', text: s.description }) : null),
      dots(s),
      s.status && s.status !== 'published' ? h('span', { class: 'tag', text: s.status === 'draft' ? 'чернетка' : s.status }) : null,
      h('button', { class: 'btn sm', title: 'Показати автоматично (демо)', text: '⚡', onclick: e => { e.stopPropagation(); this.startScenario(s, 'demo'); } }),
      editor ? h('button', { class: 'btn sm', title: 'Редагувати у конструкторі', text: '✎', onclick: e => { e.stopPropagation(); this.close(); this.ctx.openBuilder(s.id); } }) : null), s);

    if (d.error) list.appendChild(h('div', { class: 'warn', text: 'Не вдалося завантажити: ' + d.error }));

    const sets = (d.sets || []).filter(st => !q || (st.caption + ' ' + (st.description || '')).toLowerCase().indexOf(q) >= 0);
    if (sets.length) { list.appendChild(h('div', { class: 'sec', text: 'Набори' })); sets.forEach(st => list.appendChild(this.setCard(st))); }

    const here = (d.scenarios || []).filter(s => s.startRoute && routeMatches(s.startRoute, this.ctx.host.currentRoute()) && matches(s));
    if (here.length && !q) { list.appendChild(h('div', { class: 'sec', text: 'Для цієї сторінки' })); here.forEach(s => list.appendChild(item(s))); }

    const folders = d.folders || []; const scenarios = (d.scenarios || []).filter(matches);
    const hasContent = fid => scenarios.some(s => s.folderId === fid) || folders.some(f => f.parentId === fid && hasContent(f.id));
    const build = (parentId, container) => {
      folders.filter(f => (f.parentId || null) === (parentId || null)).sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)).forEach(f => {
        if (q && !hasContent(f.id)) return;
        const isOpen = q ? true : this.open[f.id];
        container.appendChild(decorate(h('div', { class: 'node fold', onclick: () => { this.open[f.id] = !this.open[f.id]; this.render(); } }, h('span', { text: isOpen ? '▾' : '▸' }), h('span', { class: 'nm', text: (f.icon && /[^\u0000-\u007f]/.test(f.icon) ? f.icon : '📁') + ' ' + f.caption }), dots(f)), f));
        if (isOpen) { const kids = h('div', { class: 'kids' }); build(f.id, kids); scenarios.filter(s => s.folderId === f.id).sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)).forEach(s => kids.appendChild(item(s))); container.appendChild(kids); }
      });
    };
    list.appendChild(h('div', { class: 'sec', text: 'Усі інструкції' }));
    build(null, list);
    scenarios.filter(s => !s.folderId).forEach(s => list.appendChild(item(s)));
    if (!scenarios.length) list.appendChild(h('div', { class: 'empty', text: q ? 'Нічого не знайдено' : 'Інструкцій поки немає' }));

    const search = h('input', { class: 'inp', placeholder: 'Пошук інструкції…', value: this.q });
    search.addEventListener('input', () => { this.q = search.value; const pos = search.selectionStart; this.render(); const s2 = this.drawer && this.drawer.querySelector('input'); if (s2) { s2.focus(); s2.setSelectionRange(pos, pos); } });

    this.drawer.innerHTML = '';
    this.drawer.appendChild(h('div', { class: 'dh' }, h('b', { text: 'Інструкції' }),
      editor ? h('button', { text: '🎞 Набори', title: 'Керування наборами інструкцій', onclick: () => { this.close(); this.ctx.openSets(); } }) : null,
      editor ? h('button', { text: '＋ Конструктор', onclick: () => { this.close(); this.ctx.openBuilder(); } }) : null,
      h('button', { text: '✕', onclick: () => this.close() })));
    this.drawer.appendChild(h('div', { class: 'srch' }, search));
    this.drawer.appendChild(list);
  }

  /** Картка набору: колір, кількість інструкцій, сумарний час, прогрес «пройдено», кнопка «Показати все». */
  setCard (st) {
    const color = st.color || '#2f7de1';
    const list = st.scenarios || [];
    const n = list.length; const done = list.filter(s => this.done[s.id]).length;
    const open = !!this.openSets[st.id];
    const word = plural(n, ['інструкція', 'інструкції', 'інструкцій']);
    const time = st.estMinutes ? st.estMinutes + ' хв' : 'час не вказано';
    const unavailable = st.unavailableCount || 0;
    const card = h('div', { class: 'setcard', style: { borderLeftColor: color } },
      h('div', { class: 'sh', onclick: () => { this.openSets[st.id] = !open; this.render(); } },
        h('b', { text: (st.icon && /[^\u0000-\u007f]/.test(st.icon) ? st.icon + ' ' : '') + st.caption }), h('span', { text: open ? '▾' : '▸' })),
      st.description ? h('div', { class: 'sd', text: st.description }) : null,
      h('div', { class: 'sm', title: unavailable ? 'Решта інструкцій набору не показуються вашій ролі' : '', text: (unavailable ? n + ' з ' + (n + unavailable) + ' інструкцій доступні для вашої ролі' : n + ' ' + word) + ' · ' + time + ' · пройдено ' + done + ' з ' + n }),
      h('div', { class: 'pbar' }, h('i', { style: { width: (n ? Math.round(done * 100 / n) : 0) + '%', background: color } })),
      open ? h('div', { class: 'sitems' }, list.map(s => h('div', { class: 'node', onclick: () => this.startScenario(s, 'guided') },
        h('span', { text: this.done[s.id] ? '✔' : '▶', class: this.done[s.id] ? 'ok' : '' }),
        h('span', { class: 'nm', text: s.caption }), s.estMinutes ? h('span', { class: 'ds', text: s.estMinutes + ' хв' }) : null))) : null,
      h('div', { class: 'row' }, h('button', { class: 'btn pri sm', text: '▶ Показати все', title: 'Пройти всі інструкції набору підряд', onclick: () => this.startSet(st) })));
    return card;
  }

  async startSet (st) {
    this.close();
    await this.ctx.setRunner.start(st, { mode: 'guided' });
  }

  async startScenario (s, mode) {
    if (s.status && s.status !== 'published') { this.ctx.toast('Чернетку можна переглянути у конструкторі (✎ → ▶)'); return; }
    this.close();
    await this.ctx.player.start(s.id, { mode: mode });
  }

  markDone (id) { const l = lstore('mlg.done') || {}; l[id] = 1; lstore('mlg.done', l); }
}
