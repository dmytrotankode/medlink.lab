/**
 * Вхід через сервер Identity MedLink (Auth:Mode=Oidc): код авторизації + PKCE (oidc-client).
 * Параметри (authority, client id, scope) приходять із /api/v1/lab/auth/config — у збірці їх немає.
 * У режимах None/Header нічого не робить: працює перемикач «Працюю як…» (заголовок X-MedLink-Employee-Id).
 *
 * Посилання на файли API (PDF, друк, експорт) відкриваються браузером без заголовків, тож у режимі Oidc їх
 * перехоплює openAuthorized(): запит із токеном → blob → нова вкладка або завантаження.
 */
import Oidc from 'oidc-client';
import { API_BASE } from './http';

const state = { mode: 'None', runAs: true, bridge: false, shellHints: true, ready: false, error: null };
let manager = null;
let user = null;

export const authState = state;
export const isOidc = () => state.mode === 'Oidc';
export const getAccessToken = () => (user && !user.expired ? user.access_token : null);
/** Дані користувача з токена Identity (ім'я, пошта) — лише для відображення в умовній оболонці МедЛінк. */
export const getTokenProfile = () => {
  const p = (user && user.profile) || null;
  if (!p) return null;
  return { name: p.name || p.preferred_username || p.given_name || '', email: p.email || '', sub: p.sub || '' };
};
export const authHeader = () => {
  const t = getAccessToken();
  return t ? { Authorization: `Bearer ${t}` } : {};
};

const appBase = () => {
  const b = process.env.VUE_ROUTER_BASE || '/';
  return b.endsWith('/') ? b : b + '/';
};
const joinBase = path => window.location.origin + appBase() + String(path || '').replace(/^\//, '');
const currentUrl = () => window.location.pathname + window.location.search + window.location.hash;

async function fetchConfig () {
  try {
    const r = await fetch(API_BASE + '/auth/config', { headers: { Accept: 'application/json' } });
    return r.ok ? await r.json() : null;
  } catch (e) {
    return null; // API недоступне: SPA стартує у звичайному режимі, банер покаже стан
  }
}

/** Повторний вхід (закінчення сесії, 401). Повертає проміс, що не завершується, — сторінка йде на Identity. */
export function signIn (returnUrl) {
  if (!manager) return Promise.resolve();
  return manager.signinRedirect({ state: { returnUrl: returnUrl || currentUrl() } }).then(() => new Promise(() => {}));
}

export function signOut () {
  if (!manager) return Promise.resolve();
  return manager.signoutRedirect();
}

/** Boot: читає конфіг, обробляє повернення з Identity, при відсутності сесії перенаправляє на вхід (застосунок не стартує). */
export async function initAuth () {
  const cfg = await fetchConfig();
  if (!cfg) return state;
  state.mode = cfg.mode || 'None';
  state.runAs = cfg.runAs !== false;
  state.bridge = !!(cfg.bridge && cfg.bridge.enabled);
  state.shellHints = cfg.shellHints !== false;
  if (state.mode !== 'Oidc' || !cfg.oidc || !cfg.oidc.authority) {
    state.ready = true;
    return state;
  }

  const o = cfg.oidc;
  manager = new Oidc.UserManager({
    authority: o.authority,
    client_id: o.clientId,
    redirect_uri: joinBase(o.callbackPath || 'callback'),
    post_logout_redirect_uri: joinBase(o.postLogoutPath || ''),
    response_type: 'code', // код авторизації + PKCE (S256)
    scope: o.scope,
    loadUserInfo: false,
    automaticSilentRenew: true, // оновлення за refresh-токеном (scope offline_access)
    monitorSession: false,
    filterProtocolClaims: true,
    userStore: new Oidc.WebStorageStateStore({ store: window.sessionStorage })
  });
  manager.events.addUserLoaded(u => { user = u; });
  manager.events.addAccessTokenExpired(() => { user = null; signIn(); });
  manager.events.addSilentRenewError(() => { /* наступний запит із простроченим токеном поверне 401 і перенаправить на вхід */ });

  const callbackUrl = joinBase(o.callbackPath || 'callback');
  if (window.location.origin + window.location.pathname === callbackUrl) {
    try {
      const u = await manager.signinRedirectCallback();
      user = u;
      const back = (u.state && u.state.returnUrl) || appBase();
      window.history.replaceState({}, '', back);
      window.sessionStorage.removeItem('lab.oidc.retry');
    } catch (e) {
      // Один повтор входу; далі показуємо помилку, щоб не зациклитись
      if (!window.sessionStorage.getItem('lab.oidc.retry')) {
        window.sessionStorage.setItem('lab.oidc.retry', '1');
        await signIn(appBase());
      }
      window.sessionStorage.removeItem('lab.oidc.retry');
      state.error = 'Не вдалося завершити вхід. Закрийте вкладку й увійдіть знову.';
      window.history.replaceState({}, '', appBase());
    }
  } else {
    user = await manager.getUser();
    if (!user || user.expired) await signIn(currentUrl());
  }
  state.ready = true;
  installAuthorizedLinks();
  return state;
}

// ---------------------------------------------------------------- файли API з токеном
const isApiUrl = href => {
  try {
    const u = new URL(href, window.location.href);
    const apiPath = new URL(API_BASE, window.location.href).pathname;
    return u.origin === window.location.origin && (u.pathname.startsWith(apiPath) || /\/api\//.test(u.pathname));
  } catch (e) {
    return false;
  }
};

function fileNameFrom (disposition) {
  const m = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition || '');
  try { return m ? decodeURIComponent(m[1]) : null; } catch (e) { return m && m[1]; }
}

/** Завантажує файл API з токеном і відкриває його (blob). target — вікно, відкрите синхронно в обробнику кліку. */
export async function openAuthorized (url, { download = false, target = null } = {}) {
  try {
    const r = await fetch(url, { headers: authHeader() });
    if (r.status === 401) { if (target) target.close(); await signIn(); return; }
    if (!r.ok) throw new Error(`HTTP ${r.status}`);
    const blob = await r.blob();
    const disposition = r.headers.get('Content-Disposition') || '';
    const objectUrl = URL.createObjectURL(blob);
    if (download || /attachment/i.test(disposition)) {
      if (target) target.close();
      const a = document.createElement('a');
      a.href = objectUrl;
      a.download = fileNameFrom(disposition) || 'file';
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);
    } else if (target) {
      target.location.href = objectUrl;
    } else {
      window.open(objectUrl, '_blank');
    }
  } catch (e) {
    if (target) target.close();
    window.alert('Не вдалося відкрити файл. Перевірте з’єднання та спробуйте ще раз.');
  }
}

let linksInstalled = false;
function installAuthorizedLinks () {
  if (linksInstalled) return;
  linksInstalled = true;
  const nativeOpen = window.open.bind(window);
  window.open = (url, name, features) => {
    if (typeof url === 'string' && url && isApiUrl(url)) {
      const w = nativeOpen('', name || '_blank', features);
      openAuthorized(url, { target: w });
      return w;
    }
    return nativeOpen(url, name, features);
  };
  document.addEventListener('click', ev => {
    const a = ev.target && ev.target.closest ? ev.target.closest('a[href]') : null;
    if (!a || ev.defaultPrevented || ev.button !== 0 || ev.ctrlKey || ev.metaKey || ev.shiftKey) return;
    const href = a.getAttribute('href');
    if (!href || href.startsWith('#') || !isApiUrl(href)) return;
    ev.preventDefault();
    const newTab = a.target === '_blank';
    const w = newTab && !a.hasAttribute('download') ? nativeOpen('', '_blank') : null;
    openAuthorized(a.href, { download: a.hasAttribute('download'), target: w });
  }, true);
}
