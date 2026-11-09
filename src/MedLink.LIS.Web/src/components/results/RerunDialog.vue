<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 420px; max-width: 96vw" data-testid="rerunDialog">
      <q-card-section class="row items-center q-pb-none">
        <div class="text-h6">Повтор: {{ row.testCode }}</div>
        <q-space />
        <q-btn flat round dense icon="close" :disable="busy" v-close-popup />
      </q-card-section>
      <q-card-section>
        <div class="text-body2 q-mb-sm">Попереднє значення збережеться в історії.</div>
        <q-input v-model="reason" dense outlined autogrow label="Причина повтору (необов’язково)" data-testid="rerunReason" />
        <q-input v-model="dilution" dense outlined type="number" min="1" class="q-mt-sm" label="Розведення зразка 1:N (необов’язково)" hint="Порожньо — без розведення" :rules="[v => v === '' || v === null || Number(v) >= 1 || 'Коефіцієнт має бути не менший за 1']" data-testid="rerunDilution" />
      </q-card-section>
      <q-card-actions align="right">
        <q-btn flat label="Скасувати" :disable="busy" v-close-popup />
        <q-btn color="primary" icon="replay" label="Повторити" :loading="busy" :disable="busy || !valid" data-testid="rerunConfirm" @click="confirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
/** Діалог повтору дослідження: причина й розведення; однаковий для лаборанта й лікаря. Захист від подвійного кліку — busy. */
export default {
  name: 'RerunDialog',
  props: { value: Boolean, row: { type: Object, default: () => ({}) } },
  data () { return { reason: '', dilution: '', busy: false }; },
  computed: { valid () { return this.dilution === '' || this.dilution === null || Number(this.dilution) >= 1; } },
  watch: { value (v) { if (v) { this.reason = ''; this.dilution = ''; this.busy = false; } } },
  methods: {
    async confirm () {
      if (this.busy || !this.valid || !this.row.orderTestId) return;
      this.busy = true;
      const factor = this.dilution === '' || this.dilution === null ? null : Number(this.dilution);
      try {
        await this.$api.rerunResult(this.row.orderTestId, { reason: (this.reason || '').trim() || null, dilutionFactor: factor && factor > 1 ? factor : null });
        this.$emit('done', { row: this.row, factor: factor && factor > 1 ? factor : null });
        this.$emit('input', false);
      } catch (e) {
        this.$emit('failed', e);
      } finally { this.busy = false; }
    }
  }
};
</script>
