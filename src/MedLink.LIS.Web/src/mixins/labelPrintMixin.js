/**
 * Міксин «Друк етикетки»: GET /samples/{barcode}/label → ZPL → локальний агент друку (localhost:5088).
 * Якщо агент недоступний — сповіщення українською та fallback на друк SVG.
 */
import { printZpl, printerFromSettings, printLabelsSvg, AGENT_UNREACHABLE_MESSAGE } from '../services/printAgentService';

export default {
  methods: {
    printerSettings () {
      return printerFromSettings(this.$store.state.context.labSettings || this.$store.state.context.lab);
    },
    /** labels: [{barcode, zpl, svg, ...}] */
    async printLabelsViaAgent (labels) {
      const list = (labels || []).filter(l => l && l.zpl);
      if (!list.length) {
        this.$q.notify({ type: 'warning', message: 'API не повернуло ZPL для етикетки — доступний лише друк SVG', actions: [{ label: 'Друк SVG', color: 'white', handler: () => printLabelsSvg(labels) }] });
        return false;
      }
      try {
        for (const l of list) await printZpl(l.zpl, this.printerSettings());
        this.$q.notify({ type: 'positive', message: `Надіслано на принтер етикеток: ${list.length}` });
        return true;
      } catch (e) {
        this.$q.notify({
          type: 'warning',
          icon: 'print_disabled',
          message: e.agentUnreachable ? AGENT_UNREACHABLE_MESSAGE : e.message,
          timeout: 8000,
          actions: [{ label: 'Надрукувати SVG', color: 'white', handler: () => printLabelsSvg(labels) }]
        });
        return false;
      }
    },
    async printSampleLabel (barcode, extra = {}) {
      try {
        const l = await this.$api.sampleLabel(barcode);
        return this.printLabelsViaAgent([{ ...extra, ...l }]);
      } catch (e) {
        this.$q.notify({ type: 'negative', message: e.userMessage || 'Не вдалося отримати етикетку з API' });
        return false;
      }
    },
    async printOrderLabels (orderId, extra = {}) {
      try {
        const res = await this.$api.orderLabels(orderId);
        const list = (Array.isArray(res) ? res : (res && res.items) || []).map(l => ({ ...extra, ...l }));
        return this.printLabelsViaAgent(list);
      } catch (e) {
        this.$q.notify({ type: 'negative', message: e.userMessage || 'Не вдалося отримати етикетки з API' });
        return false;
      }
    }
  }
};
