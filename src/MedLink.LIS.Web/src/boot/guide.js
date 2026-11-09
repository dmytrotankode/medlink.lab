/**
 * Boot-файл MedLink Guide: підключає SDK інтерактивних інструкцій (src/MedLink.Guide.Web) до ЛІС.
 * Кнопка «?» вбудовується у шапку (#guide-help-slot у BaseLayout), навігація — через vue-router,
 * роль та співробітник беруться з Vuex (context). Конструктор доступний, коли Guide:AllowAnonymousEditing=true
 * (типово в автономному ЛІС) або роль має право редактора (див. src/MedLink.Guide/README.md).
 */
import MedLinkGuide from '@guide/index.js';
import { API_BASE } from '../services/http';
import { authHeader } from '../services/auth';
import { ROLE_LABELS } from '../mixins/roleMixin';

export default ({ router, store }) => {
  const apiBase = API_BASE.replace(/\/lab\/?$/, '') + '/guide'; // /api/v1/lab → /api/v1/guide

  const start = () => {
    const slot = document.getElementById('guide-help-slot');
    MedLinkGuide.init({
      appCode: 'lab',
      appCaption: 'MedLink LIS',
      apiBase,
      headers: () => {
        const h = { ...authHeader() };
        const emp = store.state.context.employeeId;
        const role = store.getters['context/currentRole'];
        if (emp) h['X-MedLink-Employee-Id'] = emp;
        if (role) h['X-MedLink-Roles'] = role;
        return h;
      },
      // елемент меню в згорнутій бічній панелі (вузьке вікно): відкриваємо меню «гамбургером»
      revealAnchor: anchor => {
        const hidden = el => { const r = el.getBoundingClientRect(); return r.right <= 0 || getComputedStyle(el).visibility === 'hidden'; };
        const sts = (anchor && anchor.strategies) || [];
        for (let i = 0; i < sts.length; i++) {
          if (sts[i].type === 'text') continue;
          let el = null;
          try { el = document.querySelector(sts[i].value); } catch (e) { el = null; }
          const drawer = el && el.closest('.q-drawer');
          if (drawer && hidden(drawer)) {
            const t = document.querySelector('[data-testid="menuToggle"]');
            if (t) { t.click(); return true; }
          }
        }
        return false;
      },
      navigate: route => router.push(route).catch(() => {}),
      currentRoute: () => router.currentRoute.fullPath,
      getRole: () => store.getters['context/currentRole'] || '',
      // ролі для вибору «Хто бачить» у конструкторі: назви замість кодів
      roles: Object.keys(ROLE_LABELS).map(code => ({ code, label: ROLE_LABELS[code] })),
      // конструктор інструкцій — лише для адміністратора лабораторії (автор інструкцій); решта ролей бачить тільки панель
      canEdit: () => store.getters['context/currentRole'] === 'LAB_ADMIN',
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
