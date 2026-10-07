<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 520px; max-width: 96vw" data-testid="resultEditor">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="edit_note" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Результат: {{ row.testName || row.testCode }}</div>
        <q-space />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section>
        <div class="row q-col-gutter-sm text-caption text-grey-8 q-mb-sm">
          <div class="col-6">Пацієнт: <b>{{ row.patientName || '—' }}</b> {{ row.patientAgeGender ? `(${row.patientAgeGender})` : '' }}</div>
          <div class="col-6">Штрихкод: <b class="mono">{{ row.barcode || '—' }}</b></div>
          <div class="col-6">Норма: <b>{{ reference }}</b> {{ row.unit }}</div>
          <div class="col-6" v-if="row.previousValue !== undefined && row.previousValue !== null">Попередній: <b>{{ row.previousValue | num }}</b> ({{ row.previousAt | datetime }})</div>
          <div class="col-12" v-if="row.isLockedOut"><q-badge color="purple-6" icon="lock">Аналізатор/тест під Lockout ВКЯ — автоверифікація заблокована</q-badge></div>
        </div>

        <!-- Структурований протокол (патогістологія / цитологія) -->
        <div v-if="isReport" data-testid="reportEditor">
          <q-input v-model="report.macroDescription" outlined dense autogrow label="Макроскопічний опис" class="q-mb-sm" autofocus />
          <q-input v-model="report.microDescription" outlined dense autogrow label="Мікроскопічний опис" class="q-mb-sm" />
          <q-input v-model="report.conclusion" outlined dense autogrow label="Висновок *" class="q-mb-sm" :rules="[v => !!(v && v.trim()) || 'Висновок обов’язковий']" />
          <div class="row q-col-gutter-sm">
            <div class="col-6"><q-input v-model="report.icd10Code" outlined dense label="Код МКХ-10 / МКХ-О" /></div>
            <div class="col-6"><q-input v-model="report.recommendation" outlined dense label="Рекомендації" /></div>
          </div>
        </div>
        <q-select
          v-else-if="isDropdown"
          v-model="stringValue"
          outlined dense
          label="Значення (якісний результат)"
          :options="dropdownOptions"
          data-testid="resultDropdown"
          autofocus
        />
        <q-input
          v-else-if="isText"
          v-model="stringValue"
          outlined dense autogrow
          label="Текстовий результат"
          data-testid="resultText"
          autofocus
          @keydown.enter.exact.prevent="save"
          @keydown.esc="$emit('input', false)"
        />
        <q-input
          v-else
          v-model="numericInput"
          outlined dense
          :label="`Числове значення${row.unit ? ', ' + row.unit : ''}`"
          inputmode="decimal"
          :hint="livePreview"
          data-testid="resultNumeric"
          autofocus
          :input-class="liveClass"
          @keydown.enter.exact.prevent="save"
          @keydown.esc="$emit('input', false)"
        >
          <template v-slot:append><flag-marker :flag="liveFlag" /></template>
        </q-input>

        <q-input v-model="comment" outlined dense autogrow label="Коментар оператора" class="q-mt-sm" data-testid="resultComment" />
        <div class="text-caption text-grey-6 q-mt-xs">Enter — зберегти, Esc — скасувати. Норма та прапорець розраховуються сервером за каскадом норм.</div>
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="primary" icon="save" label="Зберегти" :loading="saving" :disable="!canSave" data-testid="resultSave" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { parseDecimal, referenceDisplay, formatNumber } from '../../utils/format';

export default {
  name: 'ResultEditorDialog',
  props: {
    value: Boolean,
    row: { type: Object, default: () => ({}) }
  },
  data () {
    return { numericInput: '', stringValue: null, comment: '', saving: false, error: '', report: { macroDescription: '', microDescription: '', conclusion: '', icd10Code: '', recommendation: '' } };
  },
  computed: {
    isReport () {
      if (this.resultType === 'REPORT') return true;
      const cat = ((this.row.category || this.row.section || (this.testDef && this.testDef.category)) || '').toLowerCase();
      return /патог|гістол|цитол|histo|cytol|patho/.test(cat);
    },
    testDef () {
      const tests = this.$store.getters['dictionaries/items']('tests');
      return tests.find(t => t.code === this.row.testCode || t.id === this.row.testId) || null;
    },
    resultType () { return this.row.resultType || (this.testDef && this.testDef.resultType) || 'NUMERIC'; },
    isDropdown () { return this.resultType === 'DROPDOWN'; },
    isText () { return this.resultType === 'TEXT'; },
    dropdownOptions () { return this.row.dropdownOptions || (this.testDef && this.testDef.dropdownOptions) || ['Позитивний', 'Негативний', 'Сумнівний']; },
    reference () { return referenceDisplay(this.row) || '—'; },
    numericValue () { return parseDecimal(this.numericInput); },
    liveFlag () {
      const v = this.numericValue;
      if (v === null) return 'NONE';
      const r = this.row;
      if (r.critLow !== null && r.critLow !== undefined && v <= r.critLow) return 'CRIT_LOW';
      if (r.critHigh !== null && r.critHigh !== undefined && v >= r.critHigh) return 'CRIT_HIGH';
      if (r.normLow !== null && r.normLow !== undefined && v < r.normLow) return 'LOW';
      if (r.normHigh !== null && r.normHigh !== undefined && v > r.normHigh) return 'HIGH';
      if (r.normLow === undefined && r.normHigh === undefined) return 'NONE';
      return 'NORMAL';
    },
    liveClass () {
      return { CRIT_LOW: 'text-negative text-weight-bold', CRIT_HIGH: 'text-negative text-weight-bold', LOW: 'text-orange-9 text-weight-bold', HIGH: 'text-orange-9 text-weight-bold', NORMAL: 'text-green-8' }[this.liveFlag] || '';
    },
    livePreview () {
      if (this.numericValue === null) return 'Введіть число (кома або крапка)';
      const prev = this.row.previousValue;
      if (prev !== null && prev !== undefined && Number(prev) !== 0) {
        const d = (this.numericValue - Number(prev)) / Number(prev) * 100;
        return `Δ до попереднього: ${d > 0 ? '+' : ''}${formatNumber(d, 1)}%`;
      }
      return '';
    },
    canSave () {
      if (this.isReport) return !!(this.report.conclusion && this.report.conclusion.trim());
      if (this.isDropdown || this.isText) return !!(this.stringValue && String(this.stringValue).trim());
      return this.numericValue !== null;
    }
  },
  watch: {
    value (v) {
      if (v) {
        this.error = '';
        this.comment = this.row.operatorComment || this.row.comment || '';
        this.numericInput = this.row.value !== null && this.row.value !== undefined && !isNaN(Number(this.row.value)) ? String(this.row.value).replace('.', ',') : '';
        this.stringValue = this.row.stringValue || (isNaN(Number(this.row.value)) ? this.row.value : null) || null;
        const rt = this.row.reportText || this.row.report;
        const parsed = typeof rt === 'string' ? (() => { try { return JSON.parse(rt); } catch (e) { return { conclusion: rt }; } })() : (rt || {});
        this.report = { macroDescription: '', microDescription: '', conclusion: '', icd10Code: '', recommendation: '', ...parsed };
        this.$store.dispatch('dictionaries/load', 'tests');
      }
    }
  },
  methods: {
    async save () {
      if (!this.canSave) return;
      this.saving = true;
      this.error = '';
      const body = { comment: this.comment || null };
      if (this.isReport) {
        const r = this.report;
        body.reportText = { ...r };
        body.stringValue = [r.macroDescription && `Макроскопічно: ${r.macroDescription}`, r.microDescription && `Мікроскопічно: ${r.microDescription}`, `Висновок: ${r.conclusion}`, r.icd10Code && `МКХ: ${r.icd10Code}`, r.recommendation && `Рекомендації: ${r.recommendation}`].filter(Boolean).join('\n');
      } else if (this.isDropdown || this.isText) body.stringValue = this.stringValue;
      else body.numericValue = this.numericValue;
      try {
        const res = await this.$api.saveResult(this.row.orderTestId || this.row.id, body);
        this.$q.notify({ type: 'positive', message: 'Результат збережено' });
        this.$emit('saved', res);
        this.$emit('input', false);
      } catch (e) {
        this.error = e.userMessage || 'Не вдалося зберегти результат';
      } finally {
        this.saving = false;
      }
    }
  }
};
</script>
