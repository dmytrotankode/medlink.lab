<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 520px; max-width: 96vw" data-testid="verifyDialog">
      <q-card-section class="row items-center text-white q-py-sm" :class="critical ? 'bg-negative' : 'bg-green-8'">
        <q-icon :name="critical ? 'warning' : 'how_to_reg'" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">{{ critical ? 'Верифікація критичного результату' : 'Медична верифікація' }}</div>
        <q-space />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section>
        <div class="row q-col-gutter-sm q-mb-sm">
          <div class="col-7">
            <div class="text-caption text-grey-7">Показник</div>
            <div class="text-weight-bold">{{ row.testName || row.testCode }}</div>
            <div class="text-caption text-grey-7 q-mt-xs">Пацієнт</div>
            <div>{{ row.patientName || '—' }} <span class="text-grey-7">{{ row.patientAgeGender }}</span></div>
          </div>
          <div class="col-5 text-right">
            <div class="text-caption text-grey-7">Значення</div>
            <div class="text-h5" :class="flagCss"><span>{{ displayValue }}</span> <span class="text-caption">{{ row.unit }}</span></div>
            <div class="text-caption">норма {{ reference }}</div>
            <flag-marker :flag="row.flag" show-label class="q-mt-xs" />
          </div>
        </div>
        <q-banner v-if="row.deltaAlert" dense rounded class="bg-orange-1 text-orange-9 q-mb-sm">
          <q-icon name="trending_up" /> Delta-check: зміна {{ row.deltaPercent | pct }} відносно попереднього {{ row.previousValue | num }} ({{ row.previousAt | datetime }})
        </q-banner>
        <q-banner v-if="row.isLockedOut" dense rounded class="bg-purple-1 text-purple-9 q-mb-sm">
          <q-icon name="lock" /> Активний Lockout ВКЯ: верифікація можлива лише з явним override та коментарем.
        </q-banner>
        <q-input
          v-model="comment"
          outlined dense autogrow
          :label="critical ? 'Коментар лікаря (обов’язково для критичних значень) *' : 'Коментар лікаря'"
          :rules="critical ? [v => !!(v && v.trim()) || 'Для CRIT_* коментар обов’язковий'] : []"
          data-testid="verifyComment"
          autofocus
        />
        <q-toggle v-if="row.isLockedOut" v-model="override" color="purple-6" label="Override Lockout (підтверджую відповідальність)" />
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn :color="critical ? 'negative' : 'green-8'" icon="how_to_reg" label="Верифікувати" :loading="saving" :disable="!canSave" data-testid="verifyConfirm" @click="confirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { isCritical, flagMeta } from '../../utils/statuses';
import { referenceDisplay, formatNumber } from '../../utils/format';

export default {
  name: 'VerifyResultDialog',
  props: {
    value: Boolean,
    row: { type: Object, default: () => ({}) }
  },
  data () {
    return { comment: '', override: false, saving: false, error: '' };
  },
  computed: {
    critical () { return isCritical(this.row.flag); },
    flagCss () { return flagMeta(this.row.flag).css; },
    reference () { return referenceDisplay(this.row) || '—'; },
    displayValue () {
      const v = this.row.value !== undefined ? this.row.value : (this.row.numericValue !== undefined && this.row.numericValue !== null ? this.row.numericValue : this.row.stringValue);
      return isNaN(Number(v)) || v === null || v === '' ? (v || '—') : formatNumber(v);
    },
    canSave () {
      if (this.critical && !(this.comment && this.comment.trim())) return false;
      if (this.row.isLockedOut && !this.override) return false;
      return true;
    }
  },
  watch: {
    value (v) { if (v) { this.comment = ''; this.override = false; this.error = ''; } }
  },
  methods: {
    async confirm () {
      this.saving = true;
      this.error = '';
      try {
        const res = await this.$api.verifyResult(this.row.orderTestId || this.row.id, { comment: this.comment || null, override: this.override || undefined });
        this.$q.notify({ type: 'positive', message: 'Результат верифіковано' });
        this.$emit('verified', res);
        this.$emit('input', false);
      } catch (e) {
        this.error = e.userMessage || 'Не вдалося верифікувати';
      } finally {
        this.saving = false;
      }
    }
  }
};
</script>
