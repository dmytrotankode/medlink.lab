<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 520px; max-width: 96vw" data-testid="sampleEditDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="science" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Картка проби <span class="mono">{{ sample.barcode }}</span></div>
        <q-space />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section class="q-gutter-y-sm">
        <div class="row q-col-gutter-sm">
          <div class="col-6"><q-select v-model="form.tubeTypeId" outlined dense label="Тип пробірки" :options="tubeOptions" emit-value map-options /></div>
          <div class="col-6"><q-select v-model="form.biomaterialTypeId" outlined dense label="Біоматеріал" :options="bioOptions" emit-value map-options /></div>
        </div>
        <div class="row q-col-gutter-sm text-caption text-grey-8">
          <div class="col-6">Статус: <status-chip :value="sample.status" type="sample" /></div>
          <div class="col-6">Забрано: {{ sample.collectedAt | datetime }} · Прийнято: {{ sample.receivedAt | datetime }}</div>
        </div>
        <div class="section-title">Якість матеріалу</div>
        <div class="row q-gutter-sm">
          <q-checkbox v-model="form.isHemolyzed" label="Гемоліз" />
          <q-checkbox v-model="form.isLipemic" label="Ліпемія" />
          <q-checkbox v-model="form.isIcteric" label="Іктеричність" />
          <q-checkbox v-model="form.isClotted" label="Згусток" />
          <q-checkbox v-model="form.isInsufficientVolume" label="Недостатній об’єм" />
        </div>
        <q-input v-model="form.rejectReason" outlined dense label="Причина відхилення (якщо є)" />
        <div v-if="error" class="text-negative">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat color="primary" icon="print" label="Друк етикетки" :disable="!sample.barcode" @click="printSampleLabel(sample.barcode)" />
        <q-space />
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="primary" icon="save" label="Зберегти" :loading="saving" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import labelPrintMixin from '../../mixins/labelPrintMixin';

export default {
  name: 'SampleEditDialog',
  mixins: [labelPrintMixin],
  props: { value: Boolean, sample: { type: Object, default: () => ({}) } },
  data () { return { form: {}, saving: false, error: '' }; },
  computed: {
    tubeOptions () { return this.$store.getters['dictionaries/options']('tube-types'); },
    bioOptions () { return this.$store.getters['dictionaries/options']('biomaterials'); }
  },
  watch: {
    value (v) {
      if (v) {
        const s = this.sample;
        this.error = '';
        this.form = {
          tubeTypeId: s.tubeTypeId || null,
          biomaterialTypeId: s.biomaterialTypeId || null,
          isHemolyzed: !!s.isHemolyzed,
          isLipemic: !!s.isLipemic,
          isIcteric: !!s.isIcteric,
          isClotted: !!s.isClotted,
          isInsufficientVolume: !!s.isInsufficientVolume,
          rejectReason: s.rejectReason || ''
        };
        this.$store.dispatch('dictionaries/loadMany', ['tube-types', 'biomaterials']);
      }
    }
  },
  methods: {
    async save () {
      this.saving = true;
      this.error = '';
      try {
        const res = await this.$api.updateSample(this.sample.barcode, { ...this.form, rejectReason: this.form.rejectReason || null });
        this.$q.notify({ type: 'positive', message: 'Пробу оновлено' });
        this.$emit('saved', res);
        this.$emit('input', false);
      } catch (e) {
        this.error = e.userMessage || 'Не вдалося зберегти пробу';
      } finally {
        this.saving = false;
      }
    }
  }
};
</script>
