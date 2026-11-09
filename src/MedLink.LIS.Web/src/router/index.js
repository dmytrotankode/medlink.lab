import Vue from 'vue';
import VueRouter from 'vue-router';

import routes from './routes';

Vue.use(VueRouter);

export default function (/* { store, ssrContext } */) {
  const Router = new VueRouter({
    scrollBehavior: () => ({ x: 0, y: 0 }),
    routes,
    mode: process.env.VUE_ROUTER_MODE,
    base: process.env.VUE_ROUTER_BASE
  });

  // Після оновлення збірки відкрита вкладка посилається на відсутній фрагмент (ChunkLoadError): одноразово перезавантажуємо сторінку,
  // а якщо це не допомогло — показуємо повідомлення замість порожнього екрана.
  Router.onError(err => {
    const text = String((err && (err.name + ' ' + err.message)) || err);
    if (!/ChunkLoadError|Loading (CSS )?chunk [\w-]+ failed|Failed to fetch dynamically imported/i.test(text)) return;
    const key = 'medlink.lis.chunkReload';
    let already = false;
    try { already = sessionStorage.getItem(key) === '1'; } catch (e) { /* сховище недоступне */ }
    if (!already) {
      try { sessionStorage.setItem(key, '1'); } catch (e) { /* сховище недоступне */ }
      window.location.reload();
      return;
    }
    try { sessionStorage.removeItem(key); } catch (e) { /* сховище недоступне */ }
    if (!document.getElementById('chunk-error-banner')) {
      const b = document.createElement('div');
      b.id = 'chunk-error-banner';
      b.setAttribute('style', 'position:fixed;top:0;left:0;right:0;z-index:99999;padding:12px 16px;background:#fff3cd;color:#664d03;border-bottom:1px solid #ffda6a;text-align:center;font:14px sans-serif');
      b.textContent = 'Вийшла нова версія застосунку. Оновіть сторінку (Ctrl+F5), щоб продовжити роботу.';
      document.body.appendChild(b);
    }
  });

  Router.afterEach(to => {
    try { sessionStorage.removeItem('medlink.lis.chunkReload'); } catch (e) { /* сховище недоступне */ }
    const title = to.meta && to.meta.title;
    document.title = title ? `${title} — MedLink LIS` : 'MedLink LIS';
  });

  return Router;
}
