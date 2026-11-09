/**
 * Boot-файл axios: заголовок X-MedLink-Employee-Id із Vuex (context.employeeId),
 * відстеження доступності API та нормалізація помилок (ProblemDetails → error.userMessage).
 * Режими входу — services/auth.js та docs/AUTH_PLAN.md; у режимі Oidc замість заголовка співробітника додається Bearer-токен.
 */
import { http, describeApiError } from '../services/http';
import labApi from '../services/labApiService';
import { isOidc, authHeader, signIn } from '../services/auth';

export default ({ app, store, Vue }) => {
  http.interceptors.request.use(config => {
    if (isOidc()) {
      // Вхід через Identity: особу визначає токен, заголовок співробітника не діє
      Object.assign(config.headers, authHeader());
      return config;
    }
    const employeeId = store.state.context.employeeId;
    if (employeeId) {
      config.headers['X-MedLink-Employee-Id'] = employeeId;
    }
    return config;
  });

  http.interceptors.response.use(
    response => {
      if (store.state.laboratory.apiOffline) {
        store.commit('laboratory/SET_API_OFFLINE', false);
      }
      return response;
    },
    error => {
      if (isOidc() && error.response && error.response.status === 401) signIn(); // сесія закінчилась — повторний вхід
      const info = describeApiError(error);
      error.userMessage = info.message;
      error.apiStatus = info.status;
      error.fieldErrors = info.fields;
      error.isOffline = info.offline;
      if (info.offline) {
        store.commit('laboratory/SET_API_OFFLINE', true);
      }
      store.commit('laboratory/SET_LAST_ERROR', { message: info.message, at: new Date().toISOString(), status: info.status });
      return Promise.reject(error);
    }
  );

  Vue.prototype.$http = http;
  Vue.prototype.$api = labApi;
  Vue.prototype.$apiError = describeApiError;
};

export { http };
