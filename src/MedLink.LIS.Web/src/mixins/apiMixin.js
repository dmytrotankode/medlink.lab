/**
 * Міксин для сторінок: стан завантаження/помилки, обгортка викликів API, сповіщення.
 * Використання: await this.callApi(() => this.$api.getOrders(params), { silent: true })
 */
import { describeApiError } from '../services/http';

export default {
  data () {
    return {
      loading: false,
      apiError: null,
      apiErrorStatus: 0
    };
  },
  computed: {
    apiOffline () {
      return this.$store.state.laboratory.apiOffline;
    },
    /** Повтор того самого запиту має сенс лише для збою сервера/мережі, а не для помилок перевірки чи прав */
    apiErrorRetryable () {
      const s = this.apiErrorStatus;
      return s === 0 || s === 408 || s >= 500;
    }
  },
  methods: {
    async callApi (fn, options = {}) {
      const { silent = false, loading = true, rethrow = false, success = null } = options;
      if (loading) this.loading = true;
      if (!silent) this.apiError = null;
      try {
        const result = await fn();
        if (success) this.$q.notify({ type: 'positive', message: success });
        return result;
      } catch (e) {
        const info = describeApiError(e);
        this.apiErrorStatus = info.status;
        // Помилки перевірки, прав і конфлікти (4xx, крім 404/408) показуємо одним тостом — без дублювального банера
        const actionError = !info.offline && info.status >= 400 && info.status < 500 && info.status !== 404 && info.status !== 408;
        if (!silent) this.apiError = actionError ? null : info.message;
        if (!info.offline && !silent) {
          this.notifyProblem(info);
        }
        if (rethrow) throw e;
        return undefined;
      } finally {
        if (loading) this.loading = false;
      }
    },
    notifyError (e) {
      this.notifyProblem(describeApiError(e));
    },
    /** Тост помилки; для відомих кодів додає кнопку переходу до потрібного екрана (що робити далі) */
    notifyProblem (info) {
      const code = info && info.raw && info.raw.code;
      const target = { PANIC_CALL_REQUIRED: ['lab-validation', 'Журнал викликів'], CALIBRATION_EXPIRED: ['lab-calibration', 'Журнал калібрувань'] }[code];
      if (!target) { this.$q.notify({ type: 'negative', message: info.message, timeout: 4000 }); return; }
      this.$q.notify({
        type: 'negative', message: info.message, timeout: 12000, multiLine: true,
        actions: [{ label: target[1], color: 'white', handler: () => { if (this.$router) this.$router.push({ name: target[0] }); } }, { icon: 'close', color: 'white', round: true, dense: true }]
      });
    },
    notifyOk (message) {
      this.$q.notify({ type: 'positive', message });
    },
    /** Нормалізує відповідь списку (масив або {items,total}) */
    asList (res) {
      if (Array.isArray(res)) return res;
      if (res && Array.isArray(res.items)) return res.items;
      return [];
    },
    asTotal (res, fallback) {
      if (res && typeof res.total === 'number') return res.total;
      return fallback;
    }
  }
};
