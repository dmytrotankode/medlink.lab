<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 480px; max-width: 96vw" data-testid="rejectSampleDialog">
      <q-card-section class="row items-center bg-negative text-white q-py-sm">
        <q-icon name="block" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Відхилення (бракераж) проби</div>
        <q-space />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section>
        <q-select v-if="samples.length > 1" v-model="barcode" outlined dense label="Проба" :options="samples.map(s => ({ value: s.barcode, label: `${s.barcode} · ${s.tubeName || (s.tube && s.tube.name) || ''}` }))" emit-value map-options class="q-mb-sm" />
        <div v-else-if="samples.length === 1" class="q-mb-sm">Проба <b class="mono">{{ samples[0].barcode }}</b></div>
        <q-select v-model="reason" outlined dense label="Причина відхилення *" :options="reasons" use-input new-value-mode="add-unique" @new-value="(v, done) => done(v, 'add-unique')" />
        <q-toggle v-model="createRepeatOrder" label="Створити повторне замовлення на забір" color="primary" class="q-mt-sm" />
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="negative" icon="block" label="Відхилити" :loading="saving" :disable="!barcode || !reason" data-testid="rejectConfirm" @click="confirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
export default {
  name: 'RejectSampleDialog',
  props: {
    value: Boolean,
    samples: { type: Array, default: () => [] }
  },
  data () {
    return {
      barcode: null,
      reason: null,
      createRepeatOrder: true,
      saving: false,
      error: '',
      reasons: ['Гемоліз', 'Згусток у пробірці', 'Недостатній об’єм', 'Ліпемія', 'Невідповідність пробірки', 'Порушення холодового ланцюга', 'Немає ідентифікації / штрихкоду', 'Пошкоджено при транспортуванні']
    };
  },
  watch: {
    value (v) {
      if (v) {
        this.barcode = this.samples.length ? this.samples[0].barcode : null;
        this.reason = null;
        this.createRepeatOrder = true;
        this.error = '';
      }
    }
  },
  methods: {
    async confirm () {
      this.saving = true;
      this.error = '';
      try {
        await this.$api.rejectSample(this.barcode, { reason: this.reason, createRepeatOrder: this.createRepeatOrder });
        this.$q.notify({ type: 'warning', message: `Пробу ${this.barcode} відхилено` });
        this.$emit('rejected');
        this.$emit('input', false);
      } catch (e) {
        this.error = e.userMessage || 'Не вдалося відхилити пробу';
      } finally {
        this.saving = false;
      }
    }
  }
};
</script>
