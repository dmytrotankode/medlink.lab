// Клієнти API: HttpApi (реальний бекенд /api/v1/guide) та MemoryApi (демо/тести без сервера, зберігає у localStorage).
import { uid, lstore, safeJson } from './util.js';

export class HttpApi {
  constructor (o) { this.base = (o.apiBase || '/api/v1/guide').replace(/\/$/, ''); this.headers = o.headers || (() => ({})); this.appCode = o.appCode; this.appCaption = o.appCaption || o.appCode; }

  async req (method, path, body) {
    const res = await fetch(this.base + path, {
      method: method, headers: Object.assign({ 'Content-Type': 'application/json', Accept: 'application/json' }, this.headers()),
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    if (!res.ok) {
      let msg = 'HTTP ' + res.status;
      try { const j = await res.json(); msg = j.detail || j.title || msg; } catch (e) { /* ignore */ }
      const err = new Error(msg); err.status = res.status; throw err;
    }
    if (res.status === 204) return null;
    const t = await res.text(); return t ? JSON.parse(t) : null;
  }

  ensureApp () { return this.req('POST', '/apps', { code: this.appCode, caption: this.appCaption }); }
  catalog (role, route) {
    return this.req('GET', `/apps/${this.appCode}/catalog?role=${encodeURIComponent(role || '')}&route=${encodeURIComponent(route || '')}`)
      .catch(e => { if (e.status === 404) return { folders: [], scenarios: [], sets: [] }; throw e; }); // система ще не зареєстрована — інструкцій немає
  }
  async tree () { await this.ensureApp(); return this.req('GET', `/apps/${this.appCode}/tree`); }
  play (id) { return this.req('GET', `/scenarios/${id}/play`); }
  getScenario (id) { return this.req('GET', `/scenarios/${id}`); }
  async createScenario (s) { await this.ensureApp(); return this.req('POST', '/scenarios', Object.assign({ appCode: this.appCode }, s)); }
  updateScenario (id, s) { return this.req('PUT', `/scenarios/${id}`, s); }
  saveSteps (id, steps) { return this.req('PUT', `/scenarios/${id}/steps`, steps); }
  saveVariables (id, v) { return this.req('PUT', `/scenarios/${id}/variables`, v); }
  publish (id, note) { return this.req('POST', `/scenarios/${id}/publish`, { note: note || '' }); }
  unpublish (id) { return this.req('POST', `/scenarios/${id}/unpublish`, {}); }
  deleteScenario (id) { return this.req('DELETE', `/scenarios/${id}`); }
  async createFolder (f) { await this.ensureApp(); return this.req('POST', '/folders', Object.assign({ appCode: this.appCode }, f)); }
  report (r) { return this.req('POST', '/anchor-reports', r).catch(() => null); }
  progress (p) { return this.req('POST', '/progress', p).catch(() => null); }
  myProgress () { return this.req('GET', '/progress/me').catch(() => []); }
  async uploadMedia (m) { return this.req('POST', '/media', m); }
  // набори інструкцій (презентації)
  listSets () { return this.req('GET', `/apps/${this.appCode}/sets`); }
  async createSet (s) { await this.ensureApp(); return this.req('POST', '/sets', Object.assign({ appCode: this.appCode }, s)); }
  updateSet (id, s) { return this.req('PUT', `/sets/${id}`, s); }
  deleteSet (id) { return this.req('DELETE', `/sets/${id}`); }
  setItems (id, items) { return this.req('PUT', `/sets/${id}/items`, items); }
}

/** Розкриття наборів (як на бекенді): розділ → усі його інструкції з підрозділами; повертає набори та позначки для розділів/інструкцій. */
function expandSets (sets, folders, scenarios) {
  const out = []; const scMarks = {}; const fMarks = {};
  const mark = (m, id, r) => { const l = m[id] || (m[id] = []); if (!l.some(x => x.code === r.code)) l.push(r); };
  const sub = fid => { const l = [fid]; for (let i = 0; i < l.length; i++) folders.forEach(f => { if (f.parentId === l[i] && l.indexOf(f.id) < 0) l.push(f.id); }); return l; };
  const fname = id => { const f = folders.find(x => x.id === id); return f ? f.caption : null; };
  sets.slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)).forEach(set => {
    const r = { code: set.code || set.id, caption: set.caption, color: set.color || null };
    const list = [];
    const add = s => { if (!list.some(x => x.id === s.id)) list.push(s); mark(scMarks, s.id, r); };
    (set.items || []).forEach(it => {
      if (it.scenarioId) { const s = scenarios.find(x => x.id === it.scenarioId); if (s) add(s); } else if (it.folderId) {
        const ids = sub(it.folderId); ids.forEach(f => mark(fMarks, f, r));
        scenarios.filter(s => ids.indexOf(s.folderId) >= 0).forEach(add);
      }
    });
    if (list.length) {
      out.push({ id: set.id, code: r.code, caption: set.caption, description: set.description, color: set.color, icon: set.icon, sortOrder: set.sortOrder || 0,
        estMinutes: list.reduce((n, s) => n + (s.estMinutes || 0), 0), scenarioCount: list.length,
        scenarios: list.map(s => ({ id: s.id, code: s.code, caption: s.caption, estMinutes: s.estMinutes, folder: s.folderId ? fname(s.folderId) : null })) });
    }
  });
  return { sets: out, scMarks: scMarks, fMarks: fMarks };
}

const KEY = 'mlg.mem.';
export class MemoryApi {
  constructor (o) {
    this.appCode = o.appCode || 'demo';
    this.db = lstore(KEY + this.appCode) || { folders: [], scenarios: [], progress: [], sets: [] };
    if (o.seed && !this.db.scenarios.length && !this.db.folders.length) this.db = JSON.parse(JSON.stringify(o.seed));
    if (!this.db.sets) this.db.sets = [];
  }
  save () { lstore(KEY + this.appCode, this.db); }
  async ensureApp () { return { code: this.appCode }; }
  _pub (s) { return s.status === 'published'; }
  async catalog (role) {
    const sc = this.db.scenarios.filter(s => this._pub(s) && (!(s.requiredRoles || []).length || !role || s.requiredRoles.indexOf(role) >= 0));
    const ex = expandSets(this.db.sets, this.db.folders, sc);
    const tag = (o, marks) => Object.assign({}, o, { sets: marks[o.id] || [], setCodes: (marks[o.id] || []).map(x => x.code) });
    return { sets: ex.sets, folders: this.db.folders.map(f => tag(f, ex.fMarks)), scenarios: sc.map(s => tag(this._head(s), ex.scMarks)) };
  }
  async tree () { return { folders: this.db.folders, scenarios: this.db.scenarios.map(s => this._head(s)) }; }
  _head (s) { return { id: s.id, folderId: s.folderId, caption: s.caption, description: s.description, icon: s.icon, status: s.status, startRoute: s.startRoute, requiredRoles: s.requiredRoles || [], estMinutes: s.estMinutes, publishedVersion: s.publishedVersion, tags: s.tags || [], sortOrder: s.sortOrder || 0 }; }
  async play (id) {
    const s = this.db.scenarios.find(x => x.id === id);
    if (!s || !s.snapshot) throw new Error('Сценарій не опублікований');
    return JSON.parse(JSON.stringify(s.snapshot));
  }
  async getScenario (id) { const s = this.db.scenarios.find(x => x.id === id); if (!s) throw new Error('Не знайдено'); return JSON.parse(JSON.stringify(Object.assign({ steps: [], variables: [] }, s))); }
  async createScenario (o) {
    const s = Object.assign({ id: uid(), status: 'draft', requiredRoles: [], tags: [], steps: [], variables: [], draftVersion: 1, sortOrder: this.db.scenarios.length }, o);
    this.db.scenarios.push(s); this.save(); return s;
  }
  async updateScenario (id, p) { const s = this.db.scenarios.find(x => x.id === id); Object.assign(s, p); this.save(); return s; }
  async saveSteps (id, steps) {
    const s = this.db.scenarios.find(x => x.id === id);
    s.steps = steps.map((st, i) => { const c = JSON.parse(JSON.stringify(st)); c.id = c.id || uid(); c.sortOrder = i; if (c.anchor && !c.anchor.id) c.anchor.id = uid(); return c; });
    this.save(); return s;
  }
  async saveVariables (id, v) { const s = this.db.scenarios.find(x => x.id === id); s.variables = v; this.save(); return v; }
  async publish (id, note) {
    const s = this.db.scenarios.find(x => x.id === id);
    s.publishedVersion = (s.publishedVersion || 0) + 1; s.status = 'published';
    s.snapshot = { id: s.id, caption: s.caption, description: s.description, startRoute: s.startRoute, version: s.publishedVersion, note: note, variables: s.variables || [], steps: JSON.parse(JSON.stringify(s.steps)) };
    this.save(); return this._head(s);
  }
  async unpublish (id) { const s = this.db.scenarios.find(x => x.id === id); s.status = 'draft'; this.save(); return this._head(s); }
  async deleteScenario (id) { this.db.scenarios = this.db.scenarios.filter(x => x.id !== id); this.save(); }
  async createFolder (f) { const x = Object.assign({ id: uid(), sortOrder: this.db.folders.length }, f); this.db.folders.push(x); this.save(); return x; }
  async report () { return null; }
  async progress (p) {
    const ex = this.db.progress.find(x => x.scenarioId === p.scenarioId);
    if (ex) Object.assign(ex, p); else this.db.progress.push(Object.assign({}, p));
    this.save(); return p;
  }
  async myProgress () { return this.db.progress; }
  async uploadMedia () { return null; }
  async listSets () { return JSON.parse(JSON.stringify(this.db.sets)); }
  async createSet (s) { const x = Object.assign({ id: uid(), sortOrder: this.db.sets.length, items: [] }, s); this.db.sets.push(x); this.save(); return x; }
  async updateSet (id, p) { const x = this.db.sets.find(y => y.id === id); Object.assign(x, p); this.save(); return x; }
  async deleteSet (id) { this.db.sets = this.db.sets.filter(x => x.id !== id); this.save(); }
  async setItems (id, items) { const x = this.db.sets.find(y => y.id === id); x.items = items; this.save(); return x; }
}
export { safeJson };
