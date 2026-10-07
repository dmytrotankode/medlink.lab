/**
 * Міксин для сторінок: стан завантаження/помилки, обгортка викликів API, сповіщення.
 * Використання: await this.callApi(() => this.$api.getOrders(params), { silent: true })
 */
import { describeApiError } from '../services/http';

export default {
  data () {
    return {
      loading: false,
      apiError: null
    };
  },
  computed: {
    apiOffline () {
      return this.$store.state.laboratory.apiOffline;
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
        if (!silent) this.apiError = info.message;
        if (!info.offline && !silent) {
          this.$q.notify({ type: 'negative', message: info.message, timeout: 4000 });
        }
        if (rethrow) throw e;
        return undefined;
      } finally {
        if (loading) this.loading = false;
      }
    },
    notifyError (e) {
      const info = describeApiError(e);
      this.$q.notify({ type: 'negative', message: info.message, timeout: 4000 });
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
