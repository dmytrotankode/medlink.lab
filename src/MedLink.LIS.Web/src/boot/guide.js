/**
 * Boot-файл MedLink Guide: підключає SDK інтерактивних інструкцій (src/MedLink.Guide.Web) до ЛІС.
 * Кнопка «?» вбудовується у шапку (#guide-help-slot у BaseLayout), навігація — через vue-router,
 * роль та співробітник беруться з Vuex (context). Конструктор доступний, коли Guide:AllowAnonymousEditing=true
 * (типово в автономному ЛІС) або роль має право редактора (див. src/MedLink.Guide/README.md).
 */
import MedLinkGuide from '@guide/index.js';
import { API_BASE } from '../services/http';

export default ({ router, store }) => {
  const apiBase = API_BASE.replace(/\/lab\/?$/, '') + '/guide'; // /api/v1/lab → /api/v1/guide

  const start = () => {
    const slot = document.getElementById('guide-help-slot');
    MedLinkGuide.init({
      appCode: 'lab',
      appCaption: 'MedLink LIS',
      apiBase,
      headers: () => {
        const h = {};
        const emp = store.state.context.employeeId;
        const role = store.getters['context/currentRole'];
        if (emp) h['X-MedLink-Employee-Id'] = emp;
        if (role) h['X-MedLink-Roles'] = role;
        return h;
      },
      navigate: route => router.push(route).catch(() => {}),
      currentRoute: () => router.currentRoute.fullPath,
      getRole: () => store.getters['context/currentRole'] || '',
      canEdit: () => true,
      button: slot ? { mount: slot } : true
    });
  };

  // шапка рендериться після монтування App; чекаємо слот (до ~3 с), інакше — плаваюча кнопка
  router.onReady(() => {
    let tries = 0;
    const t = setInterval(() => {
      if (document.getElementById('guide-help-slot') || ++tries > 30) { clearInterval(t); start(); }
    }, 100);
  });
};
