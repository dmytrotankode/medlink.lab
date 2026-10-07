<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 560px; max-width: 96vw" data-testid="collectDialog">
      <q-card-section class="row items-center bg-teal-6 text-white q-py-sm">
        <q-icon name="colorize" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Забір біоматеріалу</div>
        <q-space />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section v-if="sample">
        <div class="row q-col-gutter-md">
          <div class="col-12 col-sm-6">
            <div class="text-caption text-grey-7">Пробірка</div>
            <div class="row items-center no-wrap q-gutter-sm">
              <div :style="{ background: tubeColor, width: '16px', height: '26px', borderRadius: '3px 3px 8px 8px', border: '1px solid #999' }" />
              <div>
                <div class="text-weight-bold">{{ tubeName }}</div>
                <div class="text-caption">{{ biomaterialName }}</div>
              </div>
            </div>
            <div class="text-caption text-grey-7 q-mt-sm">Штрихкод</div>
            <div class="mono text-h6">{{ sample.barcode }}</div>
            <div v-if="patientName" class="text-caption text-grey-7 q-mt-sm">Пацієнт</div>
            <div v-if="patientName" class="text-weight-bold">{{ patientName }}</div>
            <div v-if="tubeMeta" class="text-caption text-grey-7 q-mt-sm">{{ tubeMeta }}</div>
          </div>
          <div class="col-12 col-sm-6">
            <div class="section-title">Чек-лист</div>
            <q-list dense>
              <q-item tag="label" v-ripple>
                <q-item-section avatar><q-checkbox v-model="checklist.idVerified" color="teal-6" data-testid="chk-id" /></q-item-section>
                <q-item-section><q-item-label>Особу пацієнта перевірено</q-item-label><q-item-label caption>ПІБ + дата народження</q-item-label></q-item-section>
              </q-item>
              <q-item tag="label" v-ripple>
                <q-item-section avatar><q-checkbox v-model="checklist.fasting" color="teal-6" data-testid="chk-fasting" /></q-item-section>
                <q-item-section><q-item-label>Умови підготовки дотримано</q-item-label><q-item-label caption>натще / режим прийому ліків</q-item-label></q-item-section>
              </q-item>
              <q-item tag="label" v-ripple>
                <q-item-section avatar><q-checkbox v-model="checklist.orderOfDraw" color="teal-6" data-testid="chk-order" /></q-item-section>
                <q-item-section><q-item-label>Порядок забору пробірок</q-item-label><q-item-label caption>за індексом order of draw</q-item-label></q-item-section>
              </q-item>
              <q-item tag="label" v-ripple>
                <q-item-section avatar><q-checkbox v-model="checklist.mixing" color="teal-6" data-testid="chk-mixing" /></q-item-section>
                <q-item-section><q-item-label>Перемішано</q-item-label><q-item-label caption>{{ inversions ? `${inversions} інверсій` : 'інверсії за інструкцією' }}</q-item-label></q-item-section>
              </q-item>
            </q-list>
            <q-input v-model="volumeMl" type="number" step="0.1" outlined dense label="Об’єм, мл (необов’язково)" class="q-mt-sm" />
          </div>
        </div>
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="teal-6" icon="check" label="Підтвердити забір" :loading="saving" :disable="!allChecked" data-testid="collectConfirm" @click="confirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { parseDecimal } from '../../utils/format';

export default {
  name: 'CollectSampleDialog',
  props: {
    value: Boolean,
    sample: { type: Object, default: null },
    patientName: { type: String, default: '' }
  },
  data () {
    return {
      checklist: { idVerified: false, fasting: false, orderOfDraw: false, mixing: false },
      volumeMl: null,
      saving: false,
      error: ''
    };
  },
  computed: {
    tube () {
      if (!this.sample) return null;
      if (this.sample.tube && typeof this.sample.tube === 'object') return this.sample.tube;
      return this.$store.getters['dictionaries/byId']('tube-types', this.sample.tubeTypeId);
    },
    biomaterial () {
      if (!this.sample) return null;
      if (this.sample.biomaterial && typeof this.sample.biomaterial === 'object') return this.sample.biomaterial;
      return this.$store.getters['dictionaries/byId']('biomaterials', this.sample.biomaterialTypeId);
    },
    tubeName () { return (this.tube && this.tube.name) || this.sample.tubeName || 'Пробірка'; },
    tubeColor () { return (this.tube && this.tube.colorCode) || '#bbb'; },
    biomaterialName () { return (this.biomaterial && this.biomaterial.name) || this.sample.biomaterialName || ''; },
    inversions () { return this.tube && this.tube.inversionsCount; },
    tubeMeta () {
      if (!this.tube) return '';
      return [this.tube.anticoagulant ? `антикоагулянт: ${this.tube.anticoagulant}` : null, this.tube.volumeMl ? `${this.tube.volumeMl} мл` : null, this.tube.orderOfDrawIndex ? `порядок забору #${this.tube.orderOfDrawIndex}` : null].filter(Boolean).join(' · ');
    },
    allChecked () { return Object.values(this.checklist).every(Boolean); }
  },
  watch: {
    value (v) {
      if (v) {
        this.checklist = { idVerified: false, fasting: false, orderOfDraw: false, mixing: false };
        this.volumeMl = null;
        this.error = '';
        this.$store.dispatch('dictionaries/loadMany', ['tube-types', 'biomaterials']);
      }
    }
  },
  methods: {
    async confirm () {
      this.saving = true;
      this.error = '';
      try {
        const res = await this.$api.collectSample(this.sample.barcode, { checklist: { ...this.checklist }, volumeMl: parseDecimal(this.volumeMl) });
        this.$q.notify({ type: 'positive', message: `Пробу ${this.sample.barcode} забрано` });
        this.$emit('collected', res || this.sample);
        this.$emit('input', false);
      } catch (e) {
        this.error = e.userMessage || 'Не вдалося зафіксувати забір';
      } finally {
        this.saving = false;
      }
    }
  }
};
</script>
