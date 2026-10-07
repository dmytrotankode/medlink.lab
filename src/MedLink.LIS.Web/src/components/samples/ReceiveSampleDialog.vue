<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 520px; max-width: 96vw" data-testid="receiveDialog">
      <q-card-section class="row items-center bg-cyan-7 text-white q-py-sm">
        <q-icon name="inbox" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Прийом проб у лабораторії (бракераж)</div>
        <q-space />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section>
        <div class="text-caption text-grey-7 q-mb-xs">Проби до прийому</div>
        <q-list dense bordered separator class="q-mb-sm">
          <q-item v-for="s in samples" :key="s.barcode" tag="label">
            <q-item-section avatar><q-checkbox v-model="selected" :val="s.barcode" color="cyan-7" /></q-item-section>
            <q-item-section>
              <q-item-label class="mono text-weight-bold">{{ s.barcode }}</q-item-label>
              <q-item-label caption>{{ s.tubeName || (s.tube && s.tube.name) || '' }} · <status-chip :value="s.status" type="sample" dense /></q-item-label>
            </q-item-section>
          </q-item>
        </q-list>
        <div class="section-title">Візуальна оцінка якості</div>
        <div class="row q-gutter-sm">
          <q-checkbox v-model="flags.isHemolyzed" label="Гемоліз" color="negative" />
          <q-checkbox v-model="flags.isLipemic" label="Ліпемія" color="warning" />
          <q-checkbox v-model="flags.isIcteric" label="Іктеричність" color="warning" />
          <q-checkbox v-model="flags.isClotted" label="Згусток" color="negative" />
          <q-checkbox v-model="flags.isInsufficientVolume" label="Недостатній об’єм" color="negative" />
        </div>
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="cyan-7" icon="check" label="Прийняти" :loading="saving" :disable="!selected.length" data-testid="receiveConfirm" @click="confirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
export default {
  name: 'ReceiveSampleDialog',
  props: {
    value: Boolean,
    samples: { type: Array, default: () => [] }
  },
  data () {
    return {
      selected: [],
      flags: { isHemolyzed: false, isLipemic: false, isIcteric: false, isClotted: false, isInsufficientVolume: false },
      saving: false,
      error: ''
    };
  },
  watch: {
    value (v) {
      if (v) {
        this.selected = this.samples.filter(s => ['COLLECTED', 'IN_TRANSIT', 'PENDING'].includes(s.status)).map(s => s.barcode);
        this.flags = { isHemolyzed: false, isLipemic: false, isIcteric: false, isClotted: false, isInsufficientVolume: false };
        this.error = '';
      }
    }
  },
  methods: {
    async confirm () {
      this.saving = true;
      this.error = '';
      const failed = [];
      for (const barcode of this.selected) {
        try {
          await this.$api.receiveSample(barcode, { ...this.flags });
        } catch (e) {
          failed.push(`${barcode}: ${e.userMessage || 'помилка'}`);
        }
      }
      this.saving = false;
      if (failed.length) {
        this.error = failed.join('; ');
        if (failed.length < this.selected.length) this.$emit('received');
        return;
      }
      this.$q.notify({ type: 'positive', message: `Прийнято проб: ${this.selected.length}` });
      this.$emit('received');
      this.$emit('input', false);
    }
  }
};
</script>
