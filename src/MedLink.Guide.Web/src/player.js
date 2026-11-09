// Плеєр: проходить опублікований сценарій крок за кроком з будь-якого місця системи.
// Стан зберігається у sessionStorage, тож сценарій переживає навігацію/перезавантаження SPA.
import { sleep, interpolate, resolveVariables, routeMatches, setNativeValue, store, isVisible } from './util.js';
import { resolveAnchor, climb, refreshAnchor } from './anchor.js';
import * as overlay from './overlay.js';
import { toast } from './ui.js';
import * as narration from './narration.js';

const RUN_KEY = 'mlg.run';

export class Player {
  constructor (ctx) {
    this.ctx = ctx; // { api, host:{navigate,currentRoute}, opts }
    this.run = null; this.cleanup = []; this.token = 0; this.reported = {};
    this.chain = null; // SetRunner, якщо інструкції проходяться як набір
  }

  get active () { return !!this.run; }

  /** Чи потрібен перехід: порівнюємо шлях (без query), бо сторінка може сама змінити query */
  needNav (route) { return !!route && !routeMatches(String(route).split('?')[0], String(this.ctx.host.currentRoute()).split('?')[0]); }

  async start (scenarioId, o) {
    o = o || {};
    const sc = await this.ctx.api.play(scenarioId);
    return this.startWith(sc, o);
  }

  /** Запуск зі знімка сценарію (опублікованого або локальної чернетки з конструктора, o.preview=true) */
  async startWith (sc, o) {
    o = o || {};
    const scenarioId = sc.id;
    if (sc.version === undefined) sc.version = sc.versionNo || 0;
    if (!sc.steps || !sc.steps.length) { toast('У сценарії немає кроків'); return; }
    this.stop(true);
    this.chain = o.chain || null;
    this.run = { scenarioId: scenarioId, scenario: sc, i: o.from || 0, mode: o.mode || 'guided', vars: resolveVariables(sc.variables), preview: !!o.preview, auto: !!o.auto };
    if (o.preview) this.previewScenario = sc; else this.previewScenario = null;
    this.persist();
    if (!o.preview) this.ctx.api.progress({ scenarioId: scenarioId, versionNo: sc.version, status: 'started', lastStep: 0, mode: this.run.mode });
    const first = sc.steps[this.run.i];
    // перехід до початкового екрана сценарію незалежно від того, де зараз користувач
    const startRoute = first.route || sc.startRoute;
    if (startRoute && !routeMatches(startRoute, this.ctx.host.currentRoute())) await this.navigate(startRoute);
    this.go(this.run.i);
  }

  async resume () {
    const saved = store('mlg.run');
    if (!saved) return;
    try {
      const sc = saved.preview && saved.scenario ? saved.scenario : await this.ctx.api.play(saved.scenarioId);
      this.run = { scenarioId: saved.scenarioId, scenario: sc, i: saved.i, mode: saved.mode, vars: saved.vars || resolveVariables(sc.variables), preview: saved.preview, auto: !!saved.auto };
      this.go(this.run.i);
    } catch (e) { store(RUN_KEY, null); }
  }

  persist () { if (this.run) store(RUN_KEY, { scenarioId: this.run.scenarioId, i: this.run.i, mode: this.run.mode, auto: this.run.auto, vars: this.run.vars, preview: this.run.preview, scenario: this.run.preview ? this.run.scenario : undefined }); }

  stop (silent) {
    this.token++; this.runCleanup();
    overlay.hide(); narration.hide();
    if (this.run && !silent && !this.run.preview) this.ctx.api.progress({ scenarioId: this.run.scenarioId, versionNo: this.run.scenario.version, status: 'abandoned', lastStep: this.run.i, mode: this.run.mode });
    this.run = null; store(RUN_KEY, null);
    if (!silent && this.chain) { const c = this.chain; this.chain = null; c.onUserStop(); }
    if (this.ctx.onStop) this.ctx.onStop();
  }

  runCleanup () { this.cleanup.forEach(f => { try { f(); } catch (e) { /* ignore */ } }); this.cleanup = []; }

  async navigate (route) {
    const before = this.ctx.host.currentRoute();
    await this.ctx.host.navigate(route);
    const t0 = Date.now();
    while (Date.now() - t0 < 6000) {
      if (!this.needNav(route)) break;
      await sleep(100);
    }
    await sleep(350); // дати сторінці відрендеритись
  }

  vars (t) { return interpolate(t, this.run.vars); }

  next () { if (!this.run) return; const n = this.run.i + 1; if (n >= this.run.scenario.steps.length) return this.finish(); this.go(n); }
  prev () { if (this.run && this.run.i > 0) this.go(this.run.i - 1); }

  finish () {
    const r = this.run; if (!r) return;
    if (!r.preview) this.ctx.api.progress({ scenarioId: r.scenarioId, versionNo: r.scenario.version, status: 'completed', lastStep: r.scenario.steps.length, mode: r.mode });
    this.token++; this.runCleanup(); narration.hide();
    if (this.chain && !r.preview) { // у наборі: без діалогу «Готово» — далі заставка наступної інструкції
      const c = this.chain; this.run = null; store(RUN_KEY, null);
      if (this.ctx.onDone) this.ctx.onDone(r.scenarioId, false);
      overlay.hide(); c.onScenarioDone(r.scenarioId); return;
    }
    overlay.show({
      title: 'Готово!', body: 'Інструкцію «' + r.scenario.caption + '» пройдено.', target: null, blockOutside: true,
      buttons: [{ text: 'Закрити', kind: 'pri', onClick: () => this.stop(true) }, { text: 'Повторити', onClick: () => (r.preview ? this.startWith(r.scenario, { preview: true, mode: r.mode }) : this.start(r.scenarioId, { mode: r.mode })) }]
    });
    this.run = null; store(RUN_KEY, null);
    if (this.ctx.onDone) this.ctx.onDone(r.scenarioId, r.preview);
  }

  async waitFor (fn, timeout) {
    const t0 = Date.now();
    while (Date.now() - t0 < timeout) { const v = fn(); if (v) return v; await sleep(120); }
    return null;
  }

  report (step, res, outcome) {
    if (!step.anchor || !step.anchor.id || this.run.preview || this.ctx.opts.reportAnchors === false) return;
    const key = step.anchor.id + outcome;
    if (this.reported[key]) return; this.reported[key] = 1;
    const rep = { anchorId: step.anchor.id, scenarioId: this.run.scenarioId, stepId: step.id, outcome: outcome, route: this.ctx.host.currentRoute() };
    if (outcome !== 'missing' && res) { rep.how = res.how; rep.confidence = res.confidence; }
    if (outcome === 'healed' && res) {
      const probe = refreshAnchor({}, res.el); rep.newStrategies = probe.strategies; rep.newFingerprint = probe.fingerprint;
    }
    this.ctx.api.report(rep);
  }

  async go (i) {
    try { await this.goInner(i); } catch (e) {
      if (typeof console !== 'undefined') console.error('[guide]', e);
      toast('Не вдалося показати крок — інструкцію зупинено');
      this.stop(true);
      if (this.chain) { const c = this.chain; this.chain = null; c.onUserStop(); }
    }
  }

  async goInner (i) {
    const r = this.run; if (!r) return;
    if (!r.scenario.steps[i]) return this.finish();
    const token = ++this.token; this.runCleanup(); narration.pause();
    r.i = i; this.persist();
    const steps = r.scenario.steps; const step = steps[i];
    const total = steps.length;
    const stale = () => token !== this.token || this.run !== r;

    // 1. потрібна сторінка
    if (this.needNav(step.route)) {
      overlay.show({ title: 'Перехід…', body: 'Відкриваю потрібний екран', target: null, blockOutside: true, buttons: [{ text: 'Закрити', kind: 'dng', onClick: () => this.stop() }] });
      await this.navigate(step.route);
      if (stale()) return;
    }
    const type = step.stepType || 'info';
    const title = this.vars(step.title); const body = this.vars(step.body);
    const value = this.vars(step.inputValue);

    // 2. кроки без елемента
    if (type === 'navigate') {
      const target = step.waitRoute || step.route;
      if (this.needNav(target)) await this.navigate(target);
      if (stale()) return; return this.next();
    }
    if (!step.anchor || type === 'wait') {
      if (type === 'wait') { await sleep(Math.min(step.timeoutMs || 1500, 10000)); if (stale()) return; return this.next(); }
      return this.showStep({ step, title, body, i, total, el: null, value, r });
    }

    // 3. пошук якоря (з очікуванням появи)
    const searchTimer = setTimeout(() => {
      if (stale()) return;
      overlay.show({ title: 'Шукаю елемент на екрані…', body: 'Зачекайте мить — сторінка ще завантажується.', target: null, blockOutside: true, index: i, total: total,
        buttons: [{ text: 'Закрити', kind: 'dng', onClick: () => this.stop() }, { spacer: true }, { text: 'Пропустити крок →', onClick: () => this.next() }] });
    }, 600);
    const tFind = Date.now();
    const found = await this.waitFor(() => resolveAnchor(step.anchor, undefined, Date.now() - tFind < 2500), step.timeoutMs || 8000);
    clearTimeout(searchTimer);
    if (stale()) return;
    if (!found) {
      this.report(step, null, 'missing');
      if (step.optional) return this.next();
      return this.showStep({ step, title, body, i, total, el: null, value, r, warn: 'Елемент не знайдено на екрані (можливо, змінився інтерфейс). Можна пропустити крок.' });
    }
    this.report(step, found, found.healed ? 'healed' : 'ok');
    const el = found.el;
    el.scrollIntoView({ block: 'center', inline: 'nearest', behavior: 'auto' });
    await sleep(120);
    // фіксована шапка системи не повинна перекривати елемент: відсуваємо, якщо він надто близько до верху вікна
    { const rr = el.getBoundingClientRect(); if (rr.top < 80) window.scrollBy(0, rr.top - 110); }
    await sleep(200);
    if (stale()) return;
    this.showStep({ step, title, body, i, total, el, value, r, healed: found.healed });
  }

  // ---------------------------------------------------------------------------------------------
  showStep (c) {
    const { step, title, body, i, total, el, value, r } = c;
    const type = step.stepType || 'info';
    const expect = step.expect || 'next';
    const noHole = step.highlight === 'none';
    let hlEl = el && !noHole ? climb(el, step.highlight === 'parent' ? Math.max(1, step.parentLevels || 1) : (step.parentLevels || 0)) : null;
    // поле вводу всередині обгортки UI-бібліотеки (Quasar q-field тощо) — підсвічуємо всю обгортку, а не нативний input
    if (hlEl === el && step.highlight !== 'none' && /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName) && !/^(checkbox|radio)$/.test(el.type || '')) { const w = el.closest('.q-field, .q-select, .v-input, .el-input, .ant-input-affix-wrapper'); if (w) hlEl = w; }
    // дуже малий/згорнутий елемент (напр. поле в адаптивній шапці) — підсвічуємо найближчого достатньо великого предка
    for (let n = 0; hlEl && n < 3; n++) { const b = hlEl.getBoundingClientRect(); if ((b.width >= 24 && b.height >= 14) || !hlEl.parentElement || hlEl.parentElement === document.body) break; hlEl = hlEl.parentElement; }
    const mode = r.mode;
    const token = this.token;
    let advanced = false;
    const live = fn => (...a) => (token === this.token ? fn(...a) : undefined);
    const advance = (delay) => { if (advanced || token !== this.token) return; advanced = true; setTimeout(() => { if (token === this.token) this.next(); }, delay === undefined ? 450 : delay); };

    const clamp = b => {
      // зона не виходить за межі вікна (великі блоки: меню на всю висоту, широкі таблиці)
      const l = Math.max(b.left, 4); const t = Math.max(b.top, 4);
      const r2 = Math.min(b.left + b.width, innerWidth - 4); const b2 = Math.min(b.top + b.height, innerHeight - 4);
      return { left: l, top: t, width: Math.max(0, r2 - l), height: Math.max(0, b2 - t) };
    };
    const rectFn = () => {
      if (!hlEl) return null;
      const b = hlEl.getBoundingClientRect();
      if (step.highlight === 'rect' && step.rect) {
        return clamp({ left: b.left + step.rect.x * b.width, top: b.top + step.rect.y * b.height, width: step.rect.w * b.width, height: step.rect.h * b.height });
      }
      return clamp(b);
    };

    const perform = async () => {
      if (!el) return;
      if (type === 'input' && value !== undefined && value !== '') {
        el.focus && el.focus();
        if (mode === 'demo') { let cur = ''; for (const ch of value) { cur += ch; setNativeValue(el, cur); await sleep(35); } } else setNativeValue(el, value);
      } else if (type === 'select' && el.tagName === 'SELECT' && value) {
        const opt = Array.prototype.find.call(el.options, o => o.value === value || o.text.trim() === value);
        if (opt) setNativeValue(el, opt.value);
      } else if (type === 'click' || type === 'hover') {
        if (type === 'click') el.click(); else el.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
      }
    };

    // очікування дії користувача
    const listen = (target, ev, fn) => { target.addEventListener(ev, fn, true); this.cleanup.push(() => target.removeEventListener(ev, fn, true)); };
    let hint = ''; let nextEnabled = true; let waits = false; // waits — крок чекає дії користувача (автопроходження його не перемикає)
    // префіл (автозаповнення тестовими даними)
    if (el && type === 'input' && step.inputMode === 'prefill' && value && mode === 'guided') { el.focus && el.focus(); setNativeValue(el, value); }
    const prefilled = el && type === 'input' && step.inputMode === 'prefill' && value;
    if (el && mode === 'guided') {
      if (expect === 'click' || (type === 'click' && expect === 'next')) {
        hint = 'Виконайте дію на підсвіченому елементі — крок завершиться автоматично.'; waits = true;
        listen(el, 'click', () => advance(500));
        nextEnabled = true;
      } else if (!prefilled && (expect === 'input' || (type === 'input' && expect === 'next' && step.inputMode === 'require'))) {
        hint = 'Заповніть поле — перейдемо далі автоматично.'; waits = true;
        let typingTimer = null; this.cleanup.push(() => clearTimeout(typingTimer));
        const chk = () => {
          const v = String(el.value === undefined ? el.textContent : el.value).trim();
          if (!v) return;
          if (value && step.inputMode === 'require' && v.toLowerCase() !== String(value).trim().toLowerCase()) return;
          clearTimeout(typingTimer); typingTimer = setTimeout(() => advance(0), 1400);
        };
        listen(el, 'input', chk); listen(el, 'change', chk);
      } else if (expect === 'change') {
        hint = 'Виберіть значення — перейдемо далі автоматично.'; waits = true;
        listen(el, 'change', () => advance(500)); listen(el, 'click', () => setTimeout(() => { if (!resolveAnchor(step.anchor)) advance(200); }, 600));
      } else if (expect === 'route' && step.waitRoute) {
        hint = 'Виконайте дію — крок завершиться при переході на потрібний екран.'; waits = true;
        const t = setInterval(() => { if (routeMatches(step.waitRoute, this.ctx.host.currentRoute())) { clearInterval(t); advance(300); } }, 200);
        this.cleanup.push(() => clearInterval(t));
      } else if (expect === 'disappear') {
        waits = true;
        const t = setInterval(() => { if (!document.contains(el) || !isVisible(el)) { clearInterval(t); advance(200); } }, 200);
        this.cleanup.push(() => clearInterval(t));
      }
    }

    const canAuto = !!el && (type === 'click' || type === 'input' || (type === 'select' && el.tagName === 'SELECT') || type === 'hover');
    const buttons = [];
    if (i > 0) buttons.push({ text: '← Назад', onClick: live(() => this.prev()) });
    buttons.push({ text: 'Закрити', kind: 'dng', onClick: () => this.stop() });
    buttons.push({ spacer: true });
    if (mode === 'guided' && canAuto) buttons.push({ text: 'Зроби за мене', onClick: live(async () => { await perform(); advance(500); }) });
    buttons.push({ text: i === total - 1 ? 'Завершити' : (c.warn ? 'Пропустити →' : 'Далі →'), kind: 'pri', disabled: !nextEnabled, onClick: live(() => this.next()) });
    if (c.warn) buttons.splice(buttons.length - 1, 0, { text: 'Повторити', onClick: () => this.go(i) });

    const valueHint = (type === 'input' || type === 'select') && value ? ((step.inputMode === 'show' ? 'Введіть: ' : step.inputMode === 'prefill' ? 'Автозаповнено: ' : 'Потрібно ввести: ') + value) : '';
    overlay.show({
      target: hlEl ? rectFn : null, padding: step.padding === undefined ? 6 : step.padding, placement: step.placement,
      blockOutside: step.blockOutside !== false && !(el && (type === 'select' || (el.closest && el.closest('.q-select, .q-menu, .q-btn-dropdown, .q-date, .q-time, [role=listbox]')))),
      title: title || (el ? '' : 'Крок ' + (i + 1)), body: body, valueHint: valueHint, warn: c.warn, hint: hint || (c.healed ? 'Елемент знайдено за схожістю — повідомлено автору інструкції.' : ''),
      index: i, total: total, buttons: buttons,
      crumb: this.chain ? this.chain.label() : '', crumbColor: this.chain ? this.chain.color() : ''
    });

    // титри та озвучення; автопроходження — після кінця фрази (+ пауза 1,2 с)
    const text = narration.stepText(this.vars(step.transcript), body, title);
    const voiceOn = narration.isVoiceOn();
    const autoDemo = mode === 'demo' && voiceOn;
    const autoGuided = mode !== 'demo' && !!r.auto && voiceOn && !waits && !c.warn;
    const afterSpeech = fn => () => { setTimeout(() => { if (token === this.token) fn(); }, 1200); };
    const demoAct = async () => { if (token !== this.token) return; if (el && (type === 'click' || type === 'input' || type === 'select')) { await perform(); } advance(300); };
    narration.present(text, { onSpoken: autoDemo ? afterSpeech(demoAct) : autoGuided ? afterSpeech(() => advance(0)) : null });

    // демо-режим: сам виконує
    if (mode === 'demo' && !voiceOn) {
      const dwell = Math.max(2200, Math.min(7000, (body || '').length * 45));
      (async () => {
        await sleep(dwell); if (token !== this.token) return;
        if (el && (type === 'click' || type === 'input' || type === 'select')) { await perform(); }
        advance(700);
      })();
    }
  }
}
