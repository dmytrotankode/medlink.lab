// Конструктор інструкцій для впроваджувача: вибір елемента наведенням (з вибором батьківського рівня),
// запис реальних дій, текст пояснення з диктовкою, тестові дані, зона підсвічування, перевірка якорів, збереження/публікація.
import { h, uid, isOurs, safeJson } from './util.js';
import { getLayer, toast, askConfirm, askText } from './ui.js';
import { buildAnchor, resolveAnchor, climb, interactiveTarget, describe, anchorQuality } from './anchor.js';
import * as overlay from './overlay.js';
import { SetsEditor } from './setseditor.js';

const TYPES = { info: 'Пояснення', click: 'Клік', input: 'Введення', select: 'Вибір зі списку', hover: 'Наведення', navigate: 'Перехід', wait: 'Пауза' };
const EXPECT = { next: 'Кнопка «Далі»', click: 'Клік по елементу', input: 'Заповнення поля', change: 'Зміна значення', route: 'Перехід на екран', appear: 'Поява елемента', disappear: 'Зникнення елемента' };
const VARKINDS = { fixed: 'Фіксоване', random_number: 'Випадкове число', random_text: 'Випадковий текст', today: 'Сьогоднішня дата', now: 'Дата й час', pick: 'Зі списку' };

export class Builder {
  constructor (ctx) {
    this.ctx = ctx; this.el = null; this.mode = 'idle';
    this.sc = null; this.steps = []; this.vars = []; this.sel = -1; this.folders = []; this.dirty = false;
    this.pick = null; this.listeners = []; this.hoverBox = null; this.crumbs = null; this.minimized = false; this.rec = null;
    this.status = { text: '', kind: '' }; this.undoStack = []; this.repickIdx = null; this.checklist = null; this.autoTimer = null; this.onKey = null; this.restorable = null;
  }

  // ------------------------------------------------------------------------------------ статус, скасування, автозбереження
  setStatus (text, kind) {
    this.status = { text: text, kind: kind || '' };
    const el = this.el && this.el.querySelector('[data-status]');
    if (el) { el.textContent = text; el.className = 'status ' + (kind || ''); }
  }

  pushUndo () {
    this.undoStack.push(JSON.stringify({ steps: this.steps, sel: this.sel }));
    if (this.undoStack.length > 30) this.undoStack.shift();
  }

  undo () {
    const last = this.undoStack.pop();
    if (!last) { this.setStatus('Немає що скасовувати'); return; }
    const o = JSON.parse(last);
    this.steps = o.steps; this.sel = Math.min(o.sel, this.steps.length - 1); this.dirty = true;
    this.setStatus('Скасовано останню дію', 'ok'); this.render();
  }

  scheduleAutosave () {
    clearTimeout(this.autoTimer);
    this.autoTimer = setTimeout(() => {
      try { localStorage.setItem('mlg.draft.' + (this.sc && this.sc.id ? this.sc.id : 'new'), JSON.stringify({ sc: this.sc, steps: this.steps, vars: this.vars, ts: Date.now() })); } catch (e) { /* ignore */ }
    }, 1200);
  }

  clearDraftCopy () {
    try { localStorage.removeItem('mlg.draft.' + (this.sc && this.sc.id ? this.sc.id : 'new')); localStorage.removeItem('mlg.draft.new'); } catch (e) { /* ignore */ }
  }

  // ------------------------------------------------------------------------------------ життєвий цикл
  async open (scenarioId) {
    this.close(true);
    try { const t = await this.ctx.api.tree(); this.folders = t.folders || []; } catch (e) { this.folders = []; }
    this.steps = []; this.vars = []; this.sel = -1; this.dirty = false; this.mode = 'idle'; this.undoStack = []; this.restorable = null; this.checklist = null;
    if (scenarioId) {
      const s = await this.ctx.api.getScenario(scenarioId);
      this.sc = { id: s.id, caption: s.caption, description: s.description || '', folderId: s.folderId || '', startRoute: s.startRoute || '', requiredRoles: s.requiredRoles || [], status: s.status, publishedVersion: s.publishedVersion };
      this.steps = (s.steps || []).map(x => this.normStep(x)); this.vars = s.variables || [];
      if (this.steps.length) this.sel = 0;
    } else {
      this.sc = { id: null, caption: '', description: '', folderId: '', startRoute: this.ctx.host.currentRoute(), requiredRoles: [], status: 'draft' };
      try {
        const d = JSON.parse(localStorage.getItem('mlg.draft.new') || 'null');
        if (d && d.steps && d.steps.length) this.restorable = d;
      } catch (e) { /* ignore */ }
    }
    if (!this.onKey) {
      this.onKey = e => {
        if (!this.el || !(e.ctrlKey || e.metaKey)) return;
        const t = (e.composedPath && e.composedPath()[0]) || {};
        const inField = /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName || '');
        if (e.key === 's' || e.key === 'S') { e.preventDefault(); this.save(); }
        else if ((e.key === 'z' || e.key === 'Z') && !inField) { e.preventDefault(); this.undo(); }
      };
      document.addEventListener('keydown', this.onKey, true);
    }
    this.render();
    if (this.restorable) this.setStatus('Знайдено незбережену чернетку («' + (this.restorable.sc.caption || 'без назви') + '», кроків: ' + this.restorable.steps.length + ') — натисніть «Відновити».', 'warn');
  }

  /** Керування наборами інструкцій (окреме вікно, див. setseditor.js). */
  openSets () {
    if (!this.setsEditor) this.setsEditor = new SetsEditor(this.ctx);
    return this.setsEditor.open();
  }

  close (silent) {
    this.stopPick(); this.stopRecord();
    if (this.setsEditor) this.setsEditor.close();
    if (this.onKey) { document.removeEventListener('keydown', this.onKey, true); this.onKey = null; }
    if (this.el) { this.el.remove(); this.el = null; }
    if (!silent) overlay.hide();
  }

  normStep (s) {
    return Object.assign({ stepType: 'info', title: '', body: '', route: '', highlight: 'element', parentLevels: 0, padding: 6, placement: 'auto', inputValue: '', inputMode: 'show', expect: 'next', timeoutMs: 8000, optional: false, blockOutside: true, anchor: null }, s);
  }

  // ------------------------------------------------------------------------------------ вибір елемента
  startPick () {
    this.stopRecord(); this.stopPick(); this.mode = 'pick'; this.pick = { base: null, level: 0 };
    const layer = getLayer();
    this.hoverBox = h('div', { class: 'hover' }); this.crumbs = h('div', { class: 'crumbs' }); layer.appendChild(this.hoverBox); layer.appendChild(this.crumbs);
    this.crumbs.style.display = 'none'; this.hoverBox.style.display = 'none';
    const target = e => { const els = document.elementsFromPoint(e.clientX, e.clientY); return els.filter(x => !isOurs(x))[0] || null; };
    const move = e => {
      if (isOurs(e.composedPath()[0]) || (e.composedPath()[0] && e.composedPath()[0].getRootNode && e.composedPath()[0].getRootNode() !== document)) return;
      const el = target(e); if (!el || el === document.documentElement || el === document.body) return;
      if (el !== this.pick.base) { this.pick.base = el; this.pick.level = 0; }
      this.drawPick();
    };
    const swallow = e => {
      const src = e.composedPath()[0];
      if (src && src.getRootNode && src.getRootNode() !== document) return; // наш UI
      if (e.ctrlKey || e.metaKey) return; // Ctrl+клік — виконати дію як зазвичай
      e.preventDefault(); e.stopPropagation(); e.stopImmediatePropagation();
      if (e.type === 'click' && this.pick && this.pick.base) this.lockPick();
    };
    const key = e => {
      if (!this.pick) return;
      if (e.key === 'Escape') { this.stopPick(); this.render(); return; }
      if (e.key === 'ArrowUp' || e.key === ']') { this.pick.level = Math.min(this.pick.level + 1, this.chain().length - 1); this.drawPick(); e.preventDefault(); }
      if (e.key === 'ArrowDown' || e.key === '[') { this.pick.level = Math.max(0, this.pick.level - 1); this.drawPick(); e.preventDefault(); }
      if (e.key === 'Enter' && this.pick.base) { this.lockPick(); e.preventDefault(); }
    };
    this.on(document, 'mousemove', move); ['click', 'mousedown', 'mouseup', 'pointerdown', 'pointerup', 'dblclick'].forEach(t => this.on(document, t, swallow));
    this.on(document, 'keydown', key);
    this.render();
    toast('Наведіть на елемент · ↑/↓ — батьківський блок · клік — вибрати · Ctrl+клік — виконати дію · Esc — скасувати', 5000);
  }

  chain () { const out = []; let c = this.pick.base; while (c && c !== document.body && out.length < 7) { out.push(c); c = c.parentElement; } return out; }

  drawPick () {
    if (!this.pick || !this.pick.base) return;
    const ch = this.chain(); const target = ch[Math.min(this.pick.level, ch.length - 1)];
    const r = target.getBoundingClientRect();
    this.hoverBox.style.cssText = `display:block;left:${r.left}px;top:${r.top}px;width:${r.width}px;height:${r.height}px`;
    this.crumbs.innerHTML = '';
    this.crumbs.style.display = 'flex';
    ch.forEach((c, i) => {
      if (i) this.crumbs.appendChild(h('em', { text: '‹' }));
      const tid = c.getAttribute('data-testid');
      const label = c.tagName.toLowerCase() + (tid ? '[' + tid.slice(0, 18) + ']' : (c.classList[0] && !/^q-/.test(c.classList[0]) ? '.' + c.classList[0].slice(0, 14) : ''));
      this.crumbs.appendChild(h('span', { class: i === this.pick.level ? 'on' : '', text: i === 0 ? '▣ ' + label : label, onclick: () => { this.pick.level = i; this.drawPick(); }, onmouseenter: () => { this.pick.level = i; this.drawPick(); } }));
    });
    const top = r.top > 40 ? r.top - 34 : r.top + r.height + 6;
    this.crumbs.style.left = Math.max(6, Math.min(innerWidth - 300, r.left)) + 'px'; this.crumbs.style.top = Math.max(6, top) + 'px';
  }

  lockPick () {
    const base = this.pick.base; const level = this.pick.level; const idx = this.repickIdx;
    this.stopPick();
    if (idx !== null && this.steps[idx]) {
      this.pushUndo();
      const a = buildAnchor(base); a.parentLevels = level; const st = this.steps[idx];
      st.anchor = a; st.parentLevels = level; st.highlight = level ? 'parent' : 'element'; this.dirty = true;
      this.setStatus('Елемент кроку ' + (idx + 1) + ' замінено', 'ok'); this.render();
      return;
    }
    this.addStepFromElement(base, { level: level, type: 'info' });
    this.render(true);
  }

  addStepFromElement (el, o) {
    const type = o.type || 'info';
    const anchor = buildAnchor(el); anchor.parentLevels = o.level || 0;
    const step = this.normStep({
      stepType: type, anchor: anchor, route: String(this.ctx.host.currentRoute()).split('?')[0],
      highlight: o.level ? 'parent' : 'element', parentLevels: o.level || 0,
      title: o.title || this.autoTitle(el, type), expect: type === 'click' ? 'click' : (type === 'input' ? 'input' : 'next'), inputValue: o.value || ''
    });
    this.pushUndo();
    const at = this.sel >= 0 ? this.sel + 1 : this.steps.length;
    this.steps.splice(at, 0, step); this.sel = at; this.dirty = true; this.scheduleAutosave();
    this.setStatus('Додано крок ' + (at + 1) + ': ' + step.title, 'ok');
    return step;
  }

  /** Заголовок-підказка з назви елемента, щоб у списку не було «(без заголовка)» */
  autoTitle (el, type) {
    const name = describe(el).replace(/^[a-z0-9-]+\s*/i, '').replace(/[«»]/g, '').trim();
    const t = name ? '«' + name.slice(0, 40) + '»' : '';
    if (type === 'click') return t ? 'Натисніть ' + t : 'Натисніть на елемент';
    if (type === 'input') return t ? 'Заповніть ' + t : 'Заповніть поле';
    if (type === 'select') return t ? 'Виберіть значення ' + t : 'Виберіть значення';
    return t ? 'Зверніть увагу: ' + t : 'Зверніть увагу на цей елемент';
  }

  stopPick () {
    this.removeListeners();
    if (this.hoverBox) this.hoverBox.remove(); if (this.crumbs) this.crumbs.remove();
    this.hoverBox = this.crumbs = null; this.pick = null; this.repickIdx = null; if (this.mode === 'pick') this.mode = 'idle';
  }

  on (t, ev, fn) { t.addEventListener(ev, fn, true); this.listeners.push(() => t.removeEventListener(ev, fn, true)); }
  removeListeners () { this.listeners.forEach(f => f()); this.listeners = []; }

  // ------------------------------------------------------------------------------------ запис дій
  startRecord () {
    this.stopPick(); this.stopRecord(); this.mode = 'record'; this.rec = { last: null, lastAt: 0 };
    const isField = el => /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName) && !/^(checkbox|radio|button|submit)$/.test(el.type || '');
    this.on(document, 'click', e => {
      const src = e.composedPath()[0]; if (!src || (src.getRootNode && src.getRootNode() !== document)) return;
      const el = interactiveTarget(src);
      if (isField(el)) return; // поля — по change
      if (this.rec.last === el && Date.now() - this.rec.lastAt < 400) return;
      this.rec.last = el; this.rec.lastAt = Date.now();
      const txt = describe(el).replace(/^[a-z0-9-]+\s*/i, '').replace(/[«»]/g, '');
      this.addStepFromElement(el, { type: 'click', title: txt ? 'Натисніть «' + txt + '»' : 'Натисніть' });
      this.renderSteps();
    });
    this.on(document, 'change', e => {
      const src = e.composedPath()[0]; if (!src || (src.getRootNode && src.getRootNode() !== document)) return;
      if (!isField(src)) return;
      const secret = src.type === 'password';
      const lbl = (src.labels && src.labels[0] && src.labels[0].textContent.trim()) || src.getAttribute('placeholder') || src.getAttribute('aria-label') || '';
      const isSel = src.tagName === 'SELECT';
      this.addStepFromElement(src, { type: isSel ? 'select' : 'input', value: secret ? '' : (isSel ? (src.options[src.selectedIndex] || {}).text || src.value : src.value), title: (isSel ? 'Виберіть ' : 'Заповніть ') + (lbl ? '«' + lbl + '»' : 'поле') });
      this.renderSteps();
    });
    this.render();
  }

  stopRecord () { if (this.mode === 'record') { this.removeListeners(); this.mode = 'idle'; this.rec = null; } }

  // ------------------------------------------------------------------------------------ зона (rect)
  drawZone () {
    const st = this.steps[this.sel]; if (!st || !st.anchor) return;
    const found = resolveAnchor(st.anchor); if (!found) { toast('Елемент не знайдено на екрані'); return; }
    const base = climb(found.el, st.parentLevels || 0).getBoundingClientRect();
    const layer = getLayer(); const catcher = h('div', { class: 'catch' }); layer.appendChild(catcher);
    toast('Протягніть мишею, щоб виділити зону всередині елемента', 4000);
    let sx = 0; let sy = 0; let box = null;
    catcher.addEventListener('mousedown', e => { sx = e.clientX; sy = e.clientY; box = h('div', { class: 'draw' }); layer.appendChild(box); });
    catcher.addEventListener('mousemove', e => { if (!box) return; box.style.cssText = `left:${Math.min(sx, e.clientX)}px;top:${Math.min(sy, e.clientY)}px;width:${Math.abs(e.clientX - sx)}px;height:${Math.abs(e.clientY - sy)}px`; });
    catcher.addEventListener('mouseup', e => {
      const l = Math.min(sx, e.clientX); const t = Math.min(sy, e.clientY); const w = Math.abs(e.clientX - sx); const hh = Math.abs(e.clientY - sy);
      catcher.remove(); if (box) box.remove();
      if (w < 6 || hh < 6) return;
      st.highlight = 'rect'; st.rect = { x: +((l - base.left) / base.width).toFixed(4), y: +((t - base.top) / base.height).toFixed(4), w: +(w / base.width).toFixed(4), h: +(hh / base.height).toFixed(4) };
      this.dirty = true; this.render(true);
    });
  }

  // ------------------------------------------------------------------------------------ диктовка
  toggleDictation (ta, btn) {
    if (this.dictation) { this.dictation.stop(); return; }
    const lang = (this.el.querySelector('[data-k=lang]') || {}).value || 'uk-UA';
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    const done = () => { this.dictation = null; btn.classList.remove('on'); btn.textContent = '🎤 Надиктувати'; };
    const put = text => { ta.value = (ta.value ? ta.value.replace(/\s*$/, ' ') : '') + text; ta.dispatchEvent(new Event('input')); };
    if (this.ctx.opts.transcribe && navigator.mediaDevices && window.MediaRecorder) {
      navigator.mediaDevices.getUserMedia({ audio: true }).then(stream => {
        const rec = new MediaRecorder(stream); const chunks = [];
        rec.ondataavailable = e => chunks.push(e.data);
        rec.onstop = async () => { stream.getTracks().forEach(t => t.stop()); done(); toast('Розпізнаю…'); try { put(await this.ctx.opts.transcribe(new Blob(chunks, { type: rec.mimeType }), lang)); } catch (err) { toast('Не вдалося розпізнати: ' + err.message); } };
        this.dictation = { stop: () => rec.stop() }; rec.start(); btn.classList.add('on'); btn.textContent = '⏹ Зупинити';
      }).catch(() => toast('Немає доступу до мікрофона'));
      return;
    }
    if (!SR) { toast('Диктовка недоступна у цьому браузері (потрібен Chrome/Edge) — введіть текст вручну'); return; }
    const rec = new SR(); rec.lang = lang; rec.continuous = true; rec.interimResults = false;
    rec.onresult = e => { for (let i = e.resultIndex; i < e.results.length; i++) if (e.results[i].isFinal) put(e.results[i][0].transcript.trim()); };
    rec.onerror = ev => { toast('Диктовка: ' + (ev.error === 'not-allowed' ? 'немає доступу до мікрофона' : ev.error)); done(); };
    rec.onend = done;
    this.dictation = { stop: () => rec.stop() }; rec.start(); btn.classList.add('on'); btn.textContent = '⏹ Зупинити';
  }

  // ------------------------------------------------------------------------------------ збереження
  async save () {
    const sc = this.sc;
    if (!sc.caption.trim()) { toast('Вкажіть назву інструкції'); return false; }
    try {
      const meta = { caption: sc.caption.trim(), description: sc.description, folderId: sc.folderId || null, startRoute: sc.startRoute, requiredRoles: sc.requiredRoles };
      if (!sc.id) { const c = await this.ctx.api.createScenario(meta); sc.id = c.id; } else await this.ctx.api.updateScenario(sc.id, meta);
      await this.ctx.api.saveSteps(sc.id, this.steps.map((s, i) => Object.assign({}, s, { sortOrder: i })));
      await this.ctx.api.saveVariables(sc.id, this.vars.map((v, i) => Object.assign({}, v, { sortOrder: i })));
      const fresh = await this.ctx.api.getScenario(sc.id);
      this.steps = (fresh.steps || []).map(x => this.normStep(x)); this.vars = fresh.variables || this.vars;
      sc.status = fresh.status; sc.publishedVersion = fresh.publishedVersion; this.dirty = false;
      toast('Збережено як чернетку'); this.render(); return true;
    } catch (e) { toast('Помилка збереження: ' + e.message, 5000); return false; }
  }

  async publish () {
    if (!(await this.save())) return;
    const bad = this.steps.filter(s => s.stepType !== 'info' && s.stepType !== 'wait' && s.stepType !== 'navigate' && !s.anchor).length;
    if (bad && !(await askConfirm('У ' + bad + ' кроках немає елемента-якоря. Опублікувати все одно?', 'Опублікувати', 'Не публікувати'))) return;
    try { await this.ctx.api.publish(this.sc.id, ''); this.sc.status = 'published'; toast('Опубліковано — інструкція з’явилась у меню допомоги'); const f = await this.ctx.api.getScenario(this.sc.id); this.sc.publishedVersion = f.publishedVersion; this.render(); } catch (e) { toast('Помилка публікації: ' + e.message, 5000); }
  }

  preview (from) {
    if (!this.steps.length) return;
    const snap = { id: this.sc.id || 'draft', caption: this.sc.caption || 'Чернетка', startRoute: this.sc.startRoute, version: 0, variables: this.vars, steps: this.steps.map((s, i) => Object.assign({}, s, { id: s.id || 'tmp' + i })) };
    this.minimized = true; this.render();
    this.ctx.player.startWith(snap, { from: from || 0, mode: 'guided', preview: true });
  }

  // ------------------------------------------------------------------------------------ відображення
  render (focusTitle) {
    if (this.el) this.el.remove();
    const st = this.steps[this.sel];
    const bind = (el, obj, key, o) => { o = o || {}; el.addEventListener(o.event || 'input', () => { let v = el.type === 'checkbox' ? el.checked : el.value; if (o.num) v = Number(v) || 0; obj[key] = v; this.dirty = true; if (o.rerenderList) this.renderSteps(); if (o.after) o.after(v); }); return el; };
    const field = (label, el) => h('div', null, h('label', { text: label }), el);
    const input = (obj, key, ph, o) => bind(h('input', { class: 'inp', placeholder: ph || '', value: obj[key] === undefined || obj[key] === null ? '' : obj[key], type: o && o.num ? 'number' : 'text' }), obj, key, o);
    const select = (obj, key, opts, o) => { const s = h('select', { class: 'inp' }, Object.keys(opts).map(k => h('option', { value: k, text: opts[k], selected: obj[key] === k }))); return bind(s, obj, key, Object.assign({ event: 'change' }, o)); };
    const sc = this.sc;

    const bodyEls = [];
    const folderOpts = [h('option', { value: '', text: '— без розділу —' })];
    const walk = (pid, d) => this.folders.filter(f => (f.parentId || null) === pid).forEach(f => { folderOpts.push(h('option', { value: f.id, text: '  '.repeat(d) + f.caption, selected: sc.folderId === f.id })); walk(f.id, d + 1); });
    walk(null, 0);
    const folderSel = h('select', { class: 'inp' }, folderOpts); folderSel.addEventListener('change', () => { sc.folderId = folderSel.value; this.dirty = true; });

    bodyEls.push(field('Назва інструкції', input(sc, 'caption', 'Напр.: Як створити замовлення')));
    bodyEls.push(h('div', { class: 'grid2' },
      field('Розділ', h('div', { class: 'row' }, folderSel, h('button', { class: 'btn sm', title: 'Новий розділ', text: '＋', onclick: async () => { const n = await askText('Назва нового розділу' + (sc.folderId ? ' (всередині вибраного)' : ''), 'Наприклад: Реєстрація пацієнтів'); if (!n) return; try { const f = await this.ctx.api.createFolder({ caption: n, parentId: sc.folderId || null }); this.folders.push(f); sc.folderId = f.id; this.render(); } catch (e) { toast('Помилка: ' + e.message); } } }))),
      field('Хто бачить (порожньо — усі ролі)', this.rolesPicker(sc, bind))));
    bodyEls.push(field('Початкова сторінка (куди перейти при запуску)', h('div', { class: 'row' }, input(sc, 'startRoute', '/laboratory/orders'), h('button', { class: 'btn sm', text: 'Поточний', onclick: () => { sc.startRoute = this.ctx.host.currentRoute(); this.dirty = true; this.render(); } }))));

    bodyEls.push(h('div', { class: 'row' },
      h('button', { class: 'btn pri', text: '🎯 Вибрати елемент', onclick: () => this.startPick() }),
      h('button', { class: 'btn' + (this.mode === 'record' ? ' pri' : ''), text: this.mode === 'record' ? '⏹ Зупинити запис' : '⏺ Записати дії', onclick: () => { if (this.mode === 'record') { this.stopRecord(); this.render(); } else this.startRecord(); } }),
      h('button', { class: 'btn', text: '＋ Крок без елемента', onclick: () => { const at = this.sel >= 0 ? this.sel + 1 : this.steps.length; this.steps.splice(at, 0, this.normStep({ route: this.ctx.host.currentRoute() })); this.sel = at; this.dirty = true; this.render(true); } })));
    if (this.mode === 'pick') bodyEls.push(h('div', { class: 'mode', text: '🎯 Режим вибору: наведіть курсор на елемент. ↑/↓ або панель зверху — вибір батьківського блоку; клік — вибрати; Ctrl+клік — виконати дію; Esc — скасувати.' }));
    if (this.mode === 'record') bodyEls.push(h('div', { class: 'mode' }, h('span', { class: 'rec' }), 'Запис: працюйте в системі як зазвичай — кліки та введення стають кроками. Паролі не записуються.'));

    const list = h('div', { class: 'steps', 'data-steps': '1' }); bodyEls.push(list);
    if (st) bodyEls.push(this.stepForm(st, { field, input, select, bind }));
    bodyEls.push(this.varsBlock({ input, select }));

    const statusTxt = sc.id ? (sc.status === 'published' ? 'Опубліковано v' + (sc.publishedVersion || 1) : 'Чернетка') : 'Нова';
    bodyEls.push(h('div', { class: 'row' },
      h('button', { class: 'btn', text: '▶ З вибраного', disabled: !this.steps.length, onclick: () => this.preview(Math.max(0, this.sel)) }),
      h('button', { class: 'btn', text: '🎞 Набори', title: 'Керування наборами інструкцій', onclick: () => this.openSets() }),
      h('span', { class: 'sp' }),
      h('span', { text: statusTxt + (this.dirty ? ' • не збережено' : ''), style: { fontSize: '12px', color: '#6b7280' } }),
      h('button', { class: 'btn', text: '💾 Зберегти', onclick: () => this.save() }),
      h('button', { class: 'btn pri', text: '🚀 Опублікувати', onclick: () => this.publish() })));

    const head = h('div', { class: 'bh' }, h('b', { text: '🛠 Конструктор інструкцій' }),
      h('button', { text: this.minimized ? '▢' : '—', title: 'Згорнути', onclick: () => { this.minimized = !this.minimized; this.render(); } }),
      h('button', { text: '✕', title: 'Закрити', onclick: async () => { if (this.dirty && !(await askConfirm('Є незбережені зміни. Закрити конструктор без збереження?', 'Закрити без збереження', 'Скасувати'))) return; this.close(); } }));
    this.el = h('div', { class: 'bld' }, head, this.minimized ? null : h('div', { class: 'bb' }, bodyEls));
    if (this.pos) { this.el.style.left = this.pos.x + 'px'; this.el.style.top = this.pos.y + 'px'; this.el.style.bottom = 'auto'; }
    head.addEventListener('mousedown', e => {
      if (e.target.tagName === 'BUTTON') return;
      const r = this.el.getBoundingClientRect(); const dx = e.clientX - r.left; const dy = e.clientY - r.top;
      const mv = ev => { this.pos = { x: Math.max(0, ev.clientX - dx), y: Math.max(0, ev.clientY - dy) }; this.el.style.left = this.pos.x + 'px'; this.el.style.top = this.pos.y + 'px'; this.el.style.bottom = 'auto'; };
      const up = () => { document.removeEventListener('mousemove', mv); document.removeEventListener('mouseup', up); };
      document.addEventListener('mousemove', mv); document.addEventListener('mouseup', up);
    });
    getLayer().appendChild(this.el);
    this.renderSteps();
    if (focusTitle) { const t = this.el.querySelector('[data-k=title]'); if (t) t.focus(); }
  }

  renderSteps () {
    const list = this.el && this.el.querySelector('[data-steps]'); if (!list) return;
    list.innerHTML = '';
    if (!this.steps.length) list.appendChild(h('div', { class: 'empty', text: 'Кроків ще немає. Виберіть елемент або запишіть дії.' }));
    this.steps.forEach((s, i) => {
      const q = s.anchor ? anchorQuality(s.anchor) : null;
      list.appendChild(h('div', { class: 'st' + (i === this.sel ? ' sel' : ''), onclick: () => { this.sel = i; this.render(); } },
        h('span', { class: 'n', text: i + 1 }),
        h('span', { class: 't' }, h('b', { text: s.title || '(без заголовка)' }), h('i', { text: TYPES[s.stepType] + (s.anchor ? ' · ' + s.anchor.caption : '') })),
        q ? h('span', { class: 'q ' + q, title: 'Наскільки надійно знайдеться елемент на екрані: ●●● — дуже, ●○○ — може зламатись', text: q === 'high' ? '●●●' : q === 'medium' ? '●●○' : '●○○' }) : null,
        h('button', { class: 'btn sm', text: '↑', disabled: i === 0, onclick: e => { e.stopPropagation(); this.move(i, -1); } }),
        h('button', { class: 'btn sm', text: '↓', disabled: i === this.steps.length - 1, onclick: e => { e.stopPropagation(); this.move(i, 1); } }),
        h('button', { class: 'btn sm dng', text: '🗑', onclick: e => { e.stopPropagation(); this.steps.splice(i, 1); this.sel = Math.min(this.sel, this.steps.length - 1); this.dirty = true; this.render(); } })));
    });
  }

  move (i, d) { const j = i + d; const t = this.steps[i]; this.steps[i] = this.steps[j]; this.steps[j] = t; this.sel = j; this.dirty = true; this.render(); }

  stepForm (st, f) {
    const { field, input, select, bind } = f;
    const hasAnchor = !!st.anchor;
    const ta = bind(h('textarea', { class: 'inp', 'data-k': 'body', placeholder: 'Пояснення для користувача. Можна використовувати {{змінні}}.' }, st.body || ''), st, 'body');
    const mic = h('button', { class: 'btn mic', text: '🎤 Надиктувати', onclick: () => this.toggleDictation(ta, mic) });
    const title = bind(h('input', { class: 'inp', 'data-k': 'title', placeholder: 'Заголовок кроку', value: st.title || '' }), st, 'title', { rerenderList: true });
    const lang = h('select', { class: 'inp', 'data-k': 'lang', style: { width: 'auto' } }, h('option', { value: 'uk-UA', text: 'Диктування: українська' }));
    return h('div', { class: 'row', style: { flexDirection: 'column', alignItems: 'stretch', borderTop: '1px solid #e5e7eb', paddingTop: '8px' } },
      h('b', { text: 'Крок ' + (this.sel + 1) }),
      field('Заголовок', title),
      field('Текст пояснення', h('div', null, ta, h('div', { class: 'row', style: { marginTop: '4px' } }, mic, lang))),
      h('div', { class: 'grid2' }, field('Дія', select(st, 'stepType', TYPES, { rerenderList: true, after: () => this.render() })), field('Крок завершується', select(st, 'expect', EXPECT))),
      (st.stepType === 'input' || st.stepType === 'select') ? h('div', { class: 'grid2' },
        field('Тестові дані {{змінні}}', input(st, 'inputValue', 'напр. {{patient}}')),
        field('Режим', select(st, 'inputMode', { show: 'Лише показати', prefill: 'Автозаповнити', require: 'Вимагати ввести' }))) : null,
      st.expect === 'route' ? field('Очікуваний екран', input(st, 'waitRoute', '/laboratory/...')) : null,
      st.stepType === 'navigate' ? field('Перейти на', input(st, 'route', '/laboratory/...')) : null,
      hasAnchor ? h('div', null,
        h('label', { text: 'Підсвітити' }),
        h('div', { class: 'row' },
          select(st, 'highlight', { element: 'Елемент', parent: 'Батьківський блок', rect: 'Власна зона', none: 'Без підсвітки' }),
          st.highlight === 'parent' ? input(st, 'parentLevels', 'рівнів вгору', { num: true }) : null,
          st.highlight === 'rect' || true ? h('button', { class: 'btn sm', text: '✏ Зона', title: 'Намалювати зону підсвітки', onclick: () => this.drawZone() }) : null)) : null,
      h('div', { class: 'grid2' },
        field('Місце підказки', select(st, 'placement', { auto: 'Авто', bottom: 'Знизу', top: 'Зверху', left: 'Ліворуч', right: 'Праворуч', center: 'По центру' })),
        field('Сторінка кроку', input(st, 'route', '/route'))),
      h('div', { class: 'row' },
        h('label', null, bind(h('input', { type: 'checkbox', checked: st.optional }), st, 'optional', { event: 'change' }), ' пропускати, якщо елемент не знайдено'),
        h('label', null, bind(h('input', { type: 'checkbox', checked: st.blockOutside }), st, 'blockOutside', { event: 'change' }), ' блокувати кліки поза зоною')),
      hasAnchor ? h('div', { class: 'row' },
        h('button', { class: 'btn sm', text: '👁 Показати елемент', onclick: () => { const r = resolveAnchor(st.anchor); if (r) { r.el.scrollIntoView({ block: 'center' }); overlay.flash(climb(r.el, st.parentLevels || 0), 1600); toast('Знайдено: ' + r.how + (r.healed ? ' (за схожістю)' : '')); } else toast('Елемент на цій сторінці не знайдено'); } }),
        h('button', { class: 'btn sm', text: '🎯 Перевибрати', onclick: () => { this.repick = this.sel; this.startRepick(); } }),
        h('span', { style: { fontSize: '11.5px', color: '#6b7280' }, text: 'способів знайти елемент: ' + (st.anchor.strategies || []).length })) : null);
  }

  /** Вибір ролей за назвами (якщо хост передав opts.roles), інакше — текстове поле з кодами. */
  rolesPicker (sc, bind) {
    const roles = this.ctx.opts.roles;
    if (!roles || !roles.length) {
      return bind(h('input', { class: 'inp', value: (sc.requiredRoles || []).join(', ') }), sc, 'requiredRoles', { after: v => { sc.requiredRoles = String(v).split(',').map(x => x.trim()).filter(Boolean); } });
    }
    const cur = () => sc.requiredRoles || (sc.requiredRoles = []);
    return h('div', { class: 'row', style: { flexDirection: 'column', alignItems: 'flex-start', gap: '2px' } }, roles.map(r =>
      h('label', { style: { color: '#1f2937', fontSize: '13px' } },
        h('input', { type: 'checkbox', checked: cur().indexOf(r.code) >= 0, onchange: e => { const l = cur(); const i = l.indexOf(r.code); if (e.target.checked && i < 0) l.push(r.code); if (!e.target.checked && i >= 0) l.splice(i, 1); this.dirty = true; } }),
        ' ' + r.label)));
  }

  startRepick () {
    const idx = this.repick; this.startPick(); this.repickIdx = idx;
  }

  varsBlock (f) {
    const { input, select } = f;
    const self = this;
    function bindList (v) { const i = h('input', { class: 'inp', placeholder: 'Наприклад: Іваненко, Петренко', value: ((v.options && v.options.values) || []).join(', ') }); i.addEventListener('input', () => { v.options.values = i.value.split(',').map(x => x.trim()).filter(Boolean); self.dirty = true; }); return i; }
    const wrap = h('details', { open: this.vars.length > 0 }, h('summary', { text: 'Тестові дані (змінні сценарію): ' + this.vars.length, style: { cursor: 'pointer' } }));
    this.vars.forEach((v, i) => {
      const opts = v.options || (v.options = {});
      wrap.appendChild(h('div', { class: 'row', style: { marginTop: '4px' } },
        input(v, 'name', 'ім’я'), select(v, 'kind', VARKINDS, { after: () => this.render() }),
        v.kind === 'fixed' ? input(v, 'defaultValue', 'значення') : v.kind === 'random_number' ? input(opts, 'prefix', 'префікс') : v.kind === 'pick' ? bindList(v) : h('span'),
        h('button', { class: 'btn sm dng', text: '🗑', onclick: () => { this.vars.splice(i, 1); this.dirty = true; this.render(); } })));
    });
    wrap.appendChild(h('button', { class: 'btn sm', style: { marginTop: '6px' }, text: '＋ Змінна', onclick: () => { this.vars.push({ name: 'var' + (this.vars.length + 1), kind: 'fixed', defaultValue: '', options: {} }); this.dirty = true; this.render(); } }));
    return wrap;
  }
}
export { safeJson, uid };
