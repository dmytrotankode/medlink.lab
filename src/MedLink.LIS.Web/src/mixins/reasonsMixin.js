/**
 * Міксин: довідники причин браку (відбракування) та причин відмови/скасування для випадних списків.
 * Список — назви активних причин; користувач може ввести й власний текст (сервер приймає і код, і сумісний текст).
 * Якщо довідник недоступний, поля залишаються вільним текстом.
 */
export default {
  data () {
    return { rejectionReasonNames: [], cancelReasonNames: [] };
  },
  created () {
    this.loadReasonDictionaries();
  },
  methods: {
    async loadReasonDictionaries () {
      try {
        const [rej, can] = await Promise.all([
          this.$api.dictList('rejection-reasons', { isActive: true, pageSize: 200 }),
          this.$api.dictList('cancel-reasons', { isActive: true, pageSize: 200 })
        ]);
        this.rejectionReasonNames = ((rej && rej.items) || []).map(r => r.name);
        this.cancelReasonNames = ((can && can.items) || []).map(r => r.name);
      } catch (e) {
        // довідник недоступний — залишається вільний текст
      }
    }
  }
};
