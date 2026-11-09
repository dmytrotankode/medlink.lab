<template>
  <q-dialog :value="value" @input="$emit('input', $event)" no-backdrop-dismiss>
    <q-card style="min-width: 720px; max-width: 96vw" data-testid="qcMaterialDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="inventory_2" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Контрольний матеріал' : 'Новий контрольний матеріал' }}</div>
        <q-space /><q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section class="q-gutter-y-sm">
        <div class="row q-col-gutter-sm">
          <div class="col-6"><q-select v-model="form.analyzerId" outlined dense use-input input-debounce="0" label="Аналізатор *" :options="analyzerFiltered" emit-value map-options :error="touched && !form.analyzerId" hide-bottom-space @filter="filterAnalyzers" /></div>
          <div class="col-6"><q-input v-model="form.name" outlined dense label="Назва *" data-testid="qcMaterialName" :error="touched && !form.name" hide-bottom-space /></div>
          <div class="col-4"><q-select v-model="form.level" outlined dense label="Рівень *" :options="levels" emit-value map-options :error="touched && !form.level" hide-bottom-space /></div>
          <div class="col-4"><q-input v-model="form.lotNumber" outlined dense label="Лот *" :error="touched && !form.lotNumber" hide-bottom-space /></div>
          <div class="col-4"><q-input v-model="form.manufacturer" outlined dense label="Виробник" /></div>
          <div class="col-4"><q-input v-model="form.expiryDate" outlined dense type="date" stack-label label="Придатний до" /></div>
          <div class="col-4"><q-input v-model="form.openedAt" outlined dense type="date" stack-label label="Відкрито" /></div>
          <div class="col-4 row items-center"><q-toggle v-model="form.isActive" label="Активний" color="positive" /></div>
        </div>
        <div class="section-title row items-center justify-between">
          <span>Цільові значення</span>
          <q-btn dense flat size="sm" color="primary" icon="add" label="Додати показник" @click="form.targets.push({ testCode: '', targetMean: null, targetSd: null, unit: '', teaPct: null })" />
        </div>
        <q-markup-table dense flat bordered>
          <thead><tr><th class="text-left">Показник</th><th>Середнє</th><th>СВ (SD)</th><th>Од. вим.</th><th>Допустима похибка, %</th><th /></tr></thead>
          <tbody>
            <tr v-for="(t, i) in form.targets" :key="i">
              <td style="min-width: 180px"><q-select v-model="t.testCode" dense borderless :options="testCodes" use-input input-debounce="0" @filter="filterTests" @input="fillUnit(t)" /></td>
              <td><q-input v-model="t.targetMean" dense borderless inputmode="decimal" input-class="text-right" /></td>
              <td><q-input v-model="t.targetSd" dense borderless inputmode="decimal" input-class="text-right" /></td>
              <td><q-input v-model="t.unit" dense borderless /></td>
              <td><q-input v-model="t.teaPct" dense borderless inputmode="decimal" input-class="text-right" /></td>
              <td class="text-right"><q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Прибрати показник" @click="form.targets.splice(i, 1)"><q-tooltip>Прибрати показник</q-tooltip></q-btn></td>
            </tr>
            <tr v-if="!form.targets.length"><td colspan="6" class="text-grey-6 text-center">Додайте хоча б один показник із цільовим середнім і СВ (SD)</td></tr>
          </tbody>
        </q-markup-table>
        <div v-if="error" class="text-negative">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="qcMaterialSave" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { QC_LEVELS } from '../../../utils/statuses';
import roleMixin from '../../../mixins/roleMixin';
import { parseDecimal } from '../../../utils/format';

export default {
  name: 'QcMaterialDialog',
  mixins: [roleMixin],
  props: { value: Boolean, material: { type: Object, default: null } },
  data () { return { form: this.empty(), saving: false, error: '', levels: QC_LEVELS, testFilter: '', analyzerNeedle: '', touched: false }; },
  computed: {
    // Лише назва прилада; технічний код на екран не виводимо
    analyzerOptions () { return this.$store.state.laboratory.analyzers.map(a => ({ value: a.id, label: a.name })); },
    analyzerFiltered () {
      const n = this.analyzerNeedle.trim().toLowerCase();
      return n ? this.analyzerOptions.filter(o => o.label.toLowerCase().includes(n)) : this.analyzerOptions;
    },
    validTargets () { return this.form.targets.filter(t => t.testCode && parseDecimal(t.targetMean) !== null && parseDecimal(t.targetSd) !== null && parseDecimal(t.targetSd) > 0); },
    /** Чому «Зберегти» неактивна — підказка замість мовчазної блокування */
    blockReason () {
      if (!this.canDo('LAB_DOCTOR', 'LAB_TECHNICIAN')) return this.roleHint('LAB_DOCTOR', 'LAB_TECHNICIAN');
      if (!this.form.analyzerId) return 'Оберіть аналізатор';
      if (!this.form.name) return 'Вкажіть назву матеріалу';
      if (!this.form.level) return 'Оберіть рівень';
      if (!this.form.lotNumber) return 'Вкажіть номер лота';
      if (!this.validTargets.length) return 'Додайте хоча б один показник із цільовим середнім і СВ (SD)';
      return '';
    },
    tests () { return this.$store.getters['dictionaries/items']('tests'); },
    testCodes () { const f = this.testFilter.toLowerCase(); return this.tests.map(t => t.code).filter(c => !f || c.toLowerCase().includes(f)); }
  },
  watch: {
    value (v) {
      if (v) {
        this.error = ''; this.touched = false;
        const m = this.material;
        this.form = m ? { ...this.empty(), ...m, expiryDate: (m.expiryDate || '').slice(0, 10), openedAt: (m.openedAt || '').slice(0, 10), targets: (m.targets || []).map(t => ({ ...t })) } : this.empty();
        this.$store.dispatch('dictionaries/load', 'tests');
      }
    }
  },
  methods: {
    empty () { return { id: null, analyzerId: null, name: '', level: null, lotNumber: '', manufacturer: '', expiryDate: '', openedAt: '', isActive: true, targets: [] }; },
    filterAnalyzers (val, update) { update(() => { this.analyzerNeedle = val || ''; }); },
    filterTests (val, update) { update(() => { this.testFilter = val || ''; }); },
    fillUnit (t) { const def = this.tests.find(x => x.code === t.testCode); if (def && !t.unit) t.unit = def.unit; },
    async save () {
      this.touched = true;
      if (this.blockReason) { this.error = this.blockReason; return; }
      this.saving = true; this.error = '';
      const body = {
        ...this.form,
        expiryDate: this.form.expiryDate || null,
        openedAt: this.form.openedAt || null,
        targets: this.validTargets.map(t => ({ testCode: t.testCode, targetMean: parseDecimal(t.targetMean), targetSd: parseDecimal(t.targetSd), unit: t.unit || null, teaPct: parseDecimal(t.teaPct) }))
      };
      delete body.id;
      try {
        const res = this.form.id ? await this.$api.updateQcMaterial(this.form.id, body) : await this.$api.createQcMaterial(body);
        this.$q.notify({ type: 'positive', message: 'Матеріал збережено' });
        this.$emit('saved', res);
        this.$emit('input', false);
      } catch (e) { this.error = e.userMessage || 'Помилка збереження'; } finally { this.saving = false; }
    }
  }
};
</script>
