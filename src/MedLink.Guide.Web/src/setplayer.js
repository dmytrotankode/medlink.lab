// Відтворення набору інструкцій: сценарії набору проходяться підряд; між ними — коротка заставка «Далі: <назва>»
// з кнопками «Пропустити» / «Зупинити». Стан зберігається у sessionStorage (переживає навігацію/перезавантаження).
import { store } from './util.js';
import * as overlay from './overlay.js';
import * as narration from './narration.js';
import { toast } from './ui.js';

const KEY = 'mlg.setrun';
const GAP_MS = 3000; // скільки показується заставка, якщо користувач нічого не натиснув

export class SetRunner {
  constructor (ctx) { this.ctx = ctx; this.st = null; this.timer = 0; }

  get active () { return !!this.st; }

  label () { return this.st ? 'Набір «' + this.st.caption + '» · ' + (this.st.idx + 1) + ' з ' + this.st.items.length : ''; }
  color () { return this.st && this.st.color ? this.st.color : ''; }

  persist () { if (this.st) store(KEY, this.st); else store(KEY, null); }

  /** set — елемент catalog.sets: { id, code, caption, color, scenarios:[{id, caption, estMinutes}] } */
  async start (set, o) {
    o = o || {};
    const items = (set.scenarios || []).map(s => ({ id: s.id, caption: s.caption }));
    if (!items.length) { toast('У наборі немає опублікованих інструкцій'); return; }
    this.ctx.player.stop(true);
    this.clearTimer();
    this.st = { id: set.id, caption: set.caption, color: set.color || '', items: items, idx: Math.max(0, o.from || 0), mode: o.mode || 'guided' };
    this.persist();
    await this.playCurrent();
  }

  async playCurrent () {
    const st = this.st; if (!st) return;
    const it = st.items[st.idx];
    this.persist();
    try {
      await this.ctx.player.start(it.id, { mode: st.mode, auto: true, chain: this });
    } catch (e) {
      toast('Не вдалося запустити «' + it.caption + '»: ' + e.message);
      this.onScenarioDone(it.id); // пропускаємо недоступну інструкцію
    }
  }

  /** Після перезавантаження сторінки: плеєр уже відновив поточну інструкцію — підключаємо набір назад. */
  resume () {
    const saved = store(KEY);
    if (!saved || !saved.items || !saved.items.length) { store(KEY, null); return; }
    const run = this.ctx.player.run;
    if (run && saved.items[saved.idx] && saved.items[saved.idx].id === run.scenarioId) { this.st = saved; this.ctx.player.chain = this; } else store(KEY, null);
  }

  clearTimer () { clearTimeout(this.timer); this.timer = 0; }

  end () { this.clearTimer(); this.st = null; store(KEY, null); if (this.ctx.player.chain === this) this.ctx.player.chain = null; }

  /** Користувач закрив інструкцію всередині набору — набір зупиняється. */
  onUserStop () { this.end(); }

  onScenarioDone () {
    const st = this.st; if (!st) return;
    st.idx++;
    this.next();
  }

  next () {
    const st = this.st; if (!st) return;
    if (st.idx >= st.items.length) { this.finish(); return; }
    this.persist();
    this.interstitial();
  }

  interstitial () {
    const st = this.st; const it = st.items[st.idx];
    this.clearTimer();
    const text = 'Далі: ' + it.caption;
    let waited = false; let spoken = !narration.isVoiceOn(); let gone = false;
    const go = () => { if (gone || !this.st) return; gone = true; this.clearTimer(); narration.hide(); overlay.hide(); this.playCurrent(); };
    const tryGo = () => { if (waited && spoken) go(); };
    overlay.show({
      title: text, body: 'Інструкція ' + (st.idx + 1) + ' з ' + st.items.length, target: null, blockOutside: true,
      crumb: this.label(), crumbColor: this.color(), index: st.idx, total: st.items.length,
      buttons: [
        { text: 'Зупинити', kind: 'dng', onClick: () => { gone = true; narration.hide(); overlay.hide(); this.end(); this.ctx.player.stop(true); } },
        { spacer: true },
        { text: 'Пропустити', onClick: () => { gone = true; this.clearTimer(); narration.hide(); st.idx++; this.next(); } },
        { text: 'Почати', kind: 'pri', onClick: go }
      ]
    });
    narration.present(text, { onSpoken: () => { spoken = true; tryGo(); } });
    this.timer = setTimeout(() => { waited = true; tryGo(); }, GAP_MS);
  }

  finish () {
    const st = this.st; if (!st) return;
    this.end();
    narration.hide();
    overlay.show({
      title: 'Набір пройдено', body: 'Набір «' + st.caption + '»: усі інструкції (' + st.items.length + ') переглянуто.', target: null, blockOutside: true,
      crumb: 'Набір «' + st.caption + '»', crumbColor: st.color,
      buttons: [{ text: 'Закрити', kind: 'pri', onClick: () => { overlay.hide(); if (this.ctx.onStop) this.ctx.onStop(); } }]
    });
  }
}
