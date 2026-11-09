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
                <q-item-section><q-item-label>Порядок забору пробірок</q-item-label><q-item-label caption>за встановленою послідовністю пробірок</q-item-label></q-item-section>
              </q-item>
              <q-item tag="label" v-ripple>
                <q-item-section avatar><q-checkbox v-model="checklist.mixing" color="teal-6" data-testid="chk-mixing" /></q-item-section>
                <q-item-section><q-item-label>Перемішано</q-item-label><q-item-label caption>{{ inversions ? `перевернути ${pluralN(inversions, 'раз', 'рази', 'разів')}` : 'перевернути за інструкцією' }}</q-item-label></q-item-section>
              </q-item>
            </q-list>
            <q-input v-model="volumeMl" type="number" step="0.1" outlined dense label="Об’єм, мл (необов’язково)" class="q-mt-sm" />
          </div>
        </div>
        <q-banner v-if="earlierPending.length" dense rounded class="bg-orange-1 text-deep-orange-9 q-mt-sm" data-testid="orderOfDrawWarning">
          <template v-slot:avatar><q-icon name="warning" color="deep-orange" /></template>
          Спочатку заберіть пробірки, які йдуть раніше за порядком забору (№ {{ earlierPending.join(', ') }}). Порушення порядку може вплинути на результати.
          <q-checkbox v-model="orderOverride" dense color="deep-orange" label="Все одно забрати цю пробірку" class="q-mt-xs" />
        </q-banner>
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <span v-if="!canConfirm && !saving" class="text-caption text-grey-7 q-mr-sm">{{ confirmHint }}</span>
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="teal-6" icon="check" label="Підтвердити забір" :loading="saving" :disable="!canConfirm" data-testid="collectConfirm" @click="confirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { parseDecimal, pluralN, tubeTitle } from '../../utils/format';

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
      error: '',
      earlierPending: [],
      orderOverride: false,
      pendingCheck: null
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
    tubeName () { return tubeTitle((this.tube && this.tube.name) || this.sample.tubeName || 'Пробірка'); },
    tubeColor () { return (this.tube && this.tube.colorCode) || '#bbb'; },
    biomaterialName () { return (this.biomaterial && this.biomaterial.name) || this.sample.biomaterialName || ''; },
    inversions () { return this.tube && this.tube.inversionsCount; },
    tubeMeta () {
      if (!this.tube) return '';
      const anticoagulant = String(this.tube.anticoagulant || '').replace(/K(\d)\/K(\d)\s*EDTA/i, 'ЕДТА К$1/К$2').replace(/EDTA/gi, 'ЕДТА');
      const volume = this.tube.volumeMl ? String(this.tube.volumeMl).replace('.', ',') : '';
      return [anticoagulant ? `антикоагулянт: ${anticoagulant}` : null, volume ? `${volume} мл` : null, this.tube.orderOfDrawIndex ? `порядок забору: ${this.tube.orderOfDrawIndex}` : null].filter(Boolean).join(' · ');
    },
    allChecked () { return Object.values(this.checklist).every(Boolean); },
    canConfirm () { return this.allChecked && (!this.earlierPending.length || this.orderOverride); },
    confirmHint () {
      if (!this.allChecked) return 'Позначте всі пункти чек-листа';
      return 'Підтвердіть забір поза порядком';
    }
  },
  watch: {
    value (v) {
      if (v) {
        this.checklist = { idVerified: false, fasting: false, orderOfDraw: false, mixing: false };
        this.volumeMl = null;
        this.error = '';
        this.earlierPending = [];
        this.orderOverride = false;
        this.pendingCheck = this.loadEarlierPending();
        this.$store.dispatch('dictionaries/loadMany', ['tube-types', 'biomaterials']);
      }
    }
  },
  methods: {
    pluralN,
    /** Пробірки цього ж направлення, які ще не забрані, але за порядком забору йдуть раніше за поточну */
    async loadEarlierPending () {
      const s = this.sample;
      if (!s || !s.barcode) return;
      try {
        const order = await this.$api.orderByBarcode(s.barcode);
        const me = (order.samples || []).find(x => x.barcode === s.barcode);
        const main = (order.samples || []).filter(x => !x.parentSampleId).slice().sort((a, b) => a.groupNumb - b.groupNumb);
        const myIndex = me ? (me.orderOfDrawIndex || 99) : 99;
        this.earlierPending = (order.samples || [])
          .filter(x => !x.parentSampleId && x.barcode !== s.barcode && x.status === 'PENDING' && (x.orderOfDrawIndex || 99) < myIndex)
          .sort((a, b) => (a.orderOfDrawIndex || 99) - (b.orderOfDrawIndex || 99))
          .map(x => { const n = main.findIndex(m => m.barcode === x.barcode) + 1; return n || x.barcode; });
      } catch (e) { this.earlierPending = []; }
    },
    async confirm () {
      this.saving = true;
      this.error = '';
      try {
        // порядок забору перевіряємо ще раз до запису, навіть якщо відповідь сервера прийшла із запізненням
        if (this.pendingCheck) await this.pendingCheck;
        if (this.earlierPending.length && !this.orderOverride) { this.saving = false; return; }
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
