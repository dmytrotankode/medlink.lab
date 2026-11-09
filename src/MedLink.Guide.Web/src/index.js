// MedLink Guide SDK — інтерактивні покрокові інструкції як плагін до будь-якої веб-системи.
//
//   import MedLinkGuide from '.../MedLink.Guide.Web/src';
//   const guide = MedLinkGuide.init({
//     appCode: 'lab', appCaption: 'MedLink LIS',
//     apiBase: '/api/v1/guide',
//     headers: () => ({ 'X-MedLink-Employee-Id': employeeId }),
//     navigate: route => router.push(route),          // як перейти на екран у вашій системі
//     currentRoute: () => router.currentRoute.fullPath,
//     getRole: () => role,                            // для фільтра інструкцій за ролями
//     canEdit: () => isImplementer                    // показувати конструктор
//   });
//   guide.open();  guide.start(scenarioId);  guide.openBuilder();
import { HttpApi, MemoryApi } from './api.js';
import { Player } from './player.js';
import { HelpPanel } from './panel.js';
import { Builder } from './builder.js';
import { SetRunner } from './setplayer.js';
import { getRoot, destroyRoot, toast } from './ui.js';

let instance = null;

function init (opts) {
  opts = opts || {};
  if (instance) instance.destroy();
  getRoot();
  const host = {
    currentRoute: opts.currentRoute || (() => location.pathname + location.search),
    navigate: opts.navigate || (route => { location.assign(route); return new Promise(() => {}); })
  };
  const api = opts.api || (opts.memory ? new MemoryApi(opts) : new HttpApi(opts));
  const ctx = {
    opts: opts, api: api, host: host, toast: toast,
    canEdit: () => typeof opts.canEdit === 'function' ? !!opts.canEdit() : !!opts.canEdit,
    openBuilder: id => builder.open(id),
    openSets: () => builder.openSets()
  };
  const player = new Player(ctx); ctx.player = player;
  const builder = new Builder(ctx); ctx.builder = builder;
  const panel = new HelpPanel(ctx); ctx.panel = panel;
  const setRunner = new SetRunner(ctx); ctx.setRunner = setRunner;
  // після попереднього перегляду чернетки повертаємось у конструктор
  const backToBuilder = () => { if (builder.el) { builder.minimized = false; builder.render(); } };
  ctx.onStop = backToBuilder; ctx.onDone = (id, preview) => { if (preview) backToBuilder(); else panel.markDone(id); };

  panel.mountButton();
  setTimeout(() => { player.resume().then(() => setRunner.resume()); }, 400); // продовжити сценарій (і набір) після навігації/перезавантаження

  instance = {
    open: () => panel.show(), close: () => panel.close(),
    start: (id, o) => player.start(id, o), stop: () => player.stop(),
    startSet: (set, o) => setRunner.start(set, o), openSets: () => builder.openSets(),
    openBuilder: id => builder.open(id), closeBuilder: () => builder.close(),
    player: player, builder: builder, setRunner: setRunner, api: api,
    destroy: () => { player.stop(true); builder.close(true); destroyRoot(); instance = null; }
  };
  return instance;
}

const MedLinkGuide = { init: init, MemoryApi: MemoryApi, HttpApi: HttpApi, get instance () { return instance; } };
if (typeof window !== 'undefined') window.MedLinkGuide = MedLinkGuide;
export { init, MemoryApi, HttpApi };
export default MedLinkGuide;
