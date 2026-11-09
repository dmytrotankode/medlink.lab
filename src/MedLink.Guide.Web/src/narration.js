// Титри та озвучення кроків. Титри — панель внизу екрана з текстом озвучення кроку.
// Озвучення — Web Speech API (speechSynthesis): голос «Ostap» (Microsoft Ostap Online (Natural), Edge) → будь-який uk-UA → перший доступний.
// Налаштування «Титри» (типово вкл) та «Озвучення» (типово викл) зберігаються у localStorage. Синтаксис ES2017.
import { h, lstore } from './util.js';
import { getLayer } from './ui.js';

const PREFS_KEY = 'mlg.narration';
const HINT_TEXT = 'Озвучення недоступне в цьому браузері. Відкрийте інструкцію в Microsoft Edge (голос Остап)';

let prefs = null;
let voiceAvail = null; // null — ще не перевіряли, true/false — є українській голос чи ні
let bar = null; let cur = null; let utter = null; let speakToken = 0; let fallbackTimer = 0; let hintText = '';

function loadPrefs () {
  if (!prefs) {
    const p = lstore(PREFS_KEY) || {};
    prefs = { subs: p.subs !== false, voice: p.voice === true };
  }
  return prefs;
}
function savePrefs () { lstore(PREFS_KEY, prefs); }

export function getPrefs () { return Object.assign({}, loadPrefs()); }

// ---------------------------------------------------------------------------------------------- голос
function synth () { return typeof window !== 'undefined' && window.speechSynthesis && typeof window.SpeechSynthesisUtterance !== 'undefined' ? window.speechSynthesis : null; }

/** Вибір голосу: «Ostap» → uk-UA. Чужим (напр. англійським) голосом український текст не читаємо: тоді uk=false. */
export function pickVoice (voices) {
  voices = voices || (synth() ? synth().getVoices() : []);
  if (!voices || !voices.length) return null;
  const ostap = voices.filter(v => /ostap/i.test(v.name || ''))[0];
  if (ostap) return { voice: ostap, ostap: true, uk: true };
  const uk = voices.filter(v => /^uk([-_]|$)/i.test(v.lang || ''))[0];
  if (uk) return { voice: uk, ostap: false, uk: true };
  return { voice: null, ostap: false, uk: false };
}

/** Перевіряє, чи є в браузері український голос; якщо ні — озвучення вимикається (voice:false) і не вмикається знову. */
function checkVoiceAvail () {
  if (voiceAvail !== null) return;
  if (!synth()) { voiceAvail = false; loadPrefs(); if (prefs.voice) { prefs.voice = false; savePrefs(); } if (bar) render(); return; }
  ensureVoices().then(v => {
    const pv = pickVoice(v);
    voiceAvail = !!(pv && pv.uk);
    if (!voiceAvail && loadPrefs().voice) { prefs.voice = false; savePrefs(); stopSpeech(); }
    if (bar) render();
  });
}

/** Голоси завантажуються асинхронно — чекаємо voiceschanged (до 1 с). */
function ensureVoices () {
  const s = synth();
  if (!s) return Promise.resolve([]);
  const now = s.getVoices();
  if (now && now.length) return Promise.resolve(now);
  return new Promise(resolve => {
    let done = false;
    const fin = () => { if (done) return; done = true; try { s.removeEventListener('voiceschanged', fin); } catch (e) { /* ignore */ } resolve(s.getVoices() || []); };
    try { s.addEventListener('voiceschanged', fin); } catch (e) { /* ignore */ }
    setTimeout(fin, 1000);
  });
}

export function voiceSupported () { return !!synth(); }

// ---------------------------------------------------------------------------------------------- текст
/** Текст для титрів і озвучення: transcript, якщо порожній — body без розмітки, інакше заголовок. */
export function plainText (text) {
  return String(text || '')
    .replace(/<br\s*\/?>/gi, '\n').replace(/<[^>]+>/g, ' ')
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/^\s{0,3}#{1,6}\s+/gm, '').replace(/^\s*[-*+]\s+/gm, '').replace(/^\s*>\s?/gm, '')
    .replace(/(\*\*|__|\*|`)/g, '')
    .replace(/&nbsp;/g, ' ').replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"')
    .replace(/\s+/g, ' ').trim();
}

export function stepText (transcript, body, title) {
  return plainText(transcript) || plainText(body) || plainText(title);
}

/** Орієнтовна тривалість мовлення (мс) — запасний таймер, коли озвучення недоступне або подія end не прийшла. */
export function estimateMs (text) { return Math.max(2500, String(text || '').length * 75); }

// ---------------------------------------------------------------------------------------------- мовлення
export function stopSpeech () {
  speakToken++;
  clearTimeout(fallbackTimer); fallbackTimer = 0;
  utter = null;
  const s = synth();
  if (s) { try { s.cancel(); } catch (e) { /* ignore */ } }
}

function finishNatural (token, c) {
  if (token !== speakToken || !c) return;
  clearTimeout(fallbackTimer); fallbackTimer = 0; utter = null;
  if (c.onSpoken) { const f = c.onSpoken; c.onSpoken = null; f(); }
}

async function startSpeech () {
  const c = cur; if (!c) return;
  stopSpeech();
  const token = speakToken;
  if (!c.text) { finishNatural(token, c); return; }
  const s = synth();
  // запасний таймер: якщо озвучення не спрацює або подія end не прийде, автопроходження не зависне
  const armFallback = ms => { clearTimeout(fallbackTimer); fallbackTimer = setTimeout(() => finishNatural(token, c), ms); };
  if (!s) { setHint(HINT_TEXT); armFallback(estimateMs(c.text)); return; }
  const voices = await ensureVoices();
  if (token !== speakToken || cur !== c) return;
  const pv = pickVoice(voices);
  setHint(!pv || !pv.uk ? HINT_TEXT : '');
  if (!pv || !pv.uk) { armFallback(estimateMs(c.text)); return; } // українського голосу немає — мовчимо, титри лишаються
  const u = new window.SpeechSynthesisUtterance(c.text);
  u.voice = pv.voice; u.lang = 'uk-UA'; u.rate = 1; u.pitch = 1;
  u.onend = () => finishNatural(token, c);
  u.onerror = ev => {
    if (token !== speakToken) return;
    const err = ev && ev.error;
    if (err === 'interrupted' || err === 'canceled') return; // нашого ж скасування
    if (err === 'not-allowed') setHint('Браузер заблокував озвучення — натисніть «Повторити»');
    armFallback(estimateMs(c.text));
  };
  utter = u;
  armFallback(estimateMs(c.text) * 2 + 8000); // подія end у Chrome інколи не приходить на довгих фразах
  try { s.speak(u); } catch (e) { armFallback(estimateMs(c.text)); }
}

// ---------------------------------------------------------------------------------------------- панель титрів
function setHint (t) {
  hintText = t || '';
  const el = bar && bar.querySelector('[data-hint]');
  if (el) { el.textContent = hintText; el.style.display = hintText && loadPrefs().voice ? '' : 'none'; }
}

/** Висота панелі, яку не повинна перекривати підказка кроку. */
export function bottomReserve () { return bar ? bar.offsetHeight + 24 : 0; }

function render () {
  if (!bar) { bar = h('div', { class: 'narr' }); getLayer().appendChild(bar); }
  const p = loadPrefs();
  bar.innerHTML = '';
  const txt = h('div', { class: 'txt', text: cur && cur.text ? cur.text : '' });
  if (!p.subs || !cur || !cur.text) txt.style.display = 'none';
  // Перемикачі показують стан словами («увімкнено» / «вимкнено»), а не галочкою
  const toggle = (label, on, fn, title, disabled) => {
    const b = h('button', { class: 'tg' + (on ? ' on' : ''), text: label + ': ' + (on ? 'увімкнено' : 'вимкнено'), title: title, onclick: fn, 'aria-pressed': on ? 'true' : 'false' });
    if (disabled) { b.disabled = true; b.style.opacity = '.55'; b.style.cursor = 'default'; }
    return b;
  };
  const noVoice = voiceAvail === false;
  const tools = h('div', { class: 'tools' },
    toggle('Титри', p.subs, () => { prefs.subs = !prefs.subs; savePrefs(); render(); }, 'Показувати текст озвучення внизу екрана'),
    noVoice
      ? h('button', { class: 'tg', text: 'Озвучення недоступне в цьому браузері', title: HINT_TEXT, disabled: true, style: { opacity: '.6', cursor: 'default' } })
      : toggle('Озвучення', p.voice, () => { prefs.voice = !prefs.voice; savePrefs(); if (prefs.voice) startSpeech(); else stopSpeech(); render(); }, 'Читати текст кроку вголос'),
    p.voice && !noVoice ? h('button', { class: 'tg', text: 'Повторити', title: 'Прочитати крок ще раз', onclick: () => repeat() }) : null,
    h('span', { class: 'nh', 'data-hint': '1', text: noVoice ? 'Відкрийте інструкцію в Microsoft Edge, щоб почути голос Остап' : hintText, style: { display: (noVoice || (hintText && p.voice)) ? '' : 'none' } }));
  bar.appendChild(txt); bar.appendChild(tools);
}

/**
 * Показує титри кроку і (якщо озвучення увімкнене) читає текст.
 * opts.onSpoken — викликається, коли фраза прочитана до кінця (або мине розрахунковий час, якщо голос недоступний); при зупинці/переході — ні.
 */
export function present (text, opts) {
  stopSpeech();
  cur = { text: text || '', onSpoken: (opts && opts.onSpoken) || null };
  checkVoiceAvail();
  render();
  if (loadPrefs().voice) startSpeech();
}

export function repeat () {
  if (!cur || voiceAvail === false) return;
  if (!loadPrefs().voice) { prefs.voice = true; savePrefs(); render(); }
  startSpeech();
}

export function isVoiceOn () { return loadPrefs().voice; }

/** Прибирає панель і зупиняє мовлення (кінець сценарію, закриття, зупинка). */
export function hide () {
  stopSpeech(); cur = null;
  if (bar) { bar.remove(); bar = null; }
}

/** Зупиняє мовлення на час переходу між кроками, панель лишається. */
export function pause () { stopSpeech(); if (cur) cur.onSpoken = null; }
