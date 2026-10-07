<template>
  <div class="resolver-playground row q-col-gutter-md" data-testid="resolver">
    <div class="col-12 col-md-5">
      <div class="medlink-card q-pa-md">
        <div class="section-title">Контекст пацієнта</div>
        <div class="row q-col-gutter-sm">
          <div class="col-12"><q-select v-model="ctx.testCode" outlined dense label="Показник *" :options="testOptions" emit-value map-options use-input input-debounce="0" @filter="filterTests" data-testid="rpTest" /></div>
          <div class="col-12"><q-select v-model="ctx.methodCode" outlined dense clearable label="Методика" :options="methodOptions" emit-value map-options /></div>
          <div class="col-4"><q-select v-model="ctx.gender" outlined dense label="Стать" :options="[{ value: 'M', label: 'Чол.' }, { value: 'F', label: 'Жін.' }]" emit-value map-options data-testid="rpGender" /></div>
          <div class="col-4"><q-input v-model.number="ctx.age" outlined dense type="number" label="Вік" data-testid="rpAge" /></div>
          <div class="col-4"><q-select v-model="ctx.ageUnit" outlined dense label="Од." :options="ageUnits" emit-value map-options /></div>
          <template v-if="ctx.gender === 'F'">
            <div class="col-5"><q-toggle v-model="ctx.isPregnant" label="Вагітність" color="deep-orange-6" /></div>
            <div class="col-7"><q-input v-if="ctx.isPregnant" v-model.number="ctx.pregnancyWeek" outlined dense type="number" label="Тиждень вагітності" /></div>
            <div class="col-12"><q-select v-if="!ctx.isPregnant" v-model="ctx.menstrualPhase" outlined dense clearable label="Фаза циклу" :options="phases" emit-value map-options /></div>
          </template>
          <div class="col-12"><q-input v-model="ctx.icd10Code" outlined dense label="МКХ-10 (клінічний контекст)" /></div>
          <div class="col-6"><q-input v-model="ctx.measuredValue" outlined dense label="Виміряне значення" inputmode="decimal" data-testid="rpValue" /></div>
          <div class="col-6"><q-input v-model="ctx.previousValue" outlined dense label="Попереднє значення (delta)" inputmode="decimal" /></div>
        </div>
        <q-btn color="primary" icon="play_arrow" label="Розв’язати каскад" class="full-width q-mt-md" :loading="loading" :disable="!ctx.testCode" data-testid="rpResolve" @click="resolve" />
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </div>
    </div>
    <div class="col-12 col-md-7">
      <div class="medlink-card q-pa-md" v-if="result" data-testid="rpResult">
        <div class="row items-center justify-between q-mb-sm">
          <div class="text-subtitle1 text-weight-bold">Результат резолвера</div>
          <flag-marker v-if="result.statusFlag" :flag="result.statusFlag" show-label />
        </div>
        <div class="row q-col-gutter-sm">
          <div class="col-6 col-md-3"><div class="kpi-tile kpi-tile--positive"><div class="kpi-tile__label">Норма</div><div class="kpi-tile__value" style="font-size: 18px">{{ result.normLow | num }} – {{ result.normHigh | num }}</div><div class="kpi-tile__hint">{{ result.unit }}</div></div></div>
          <div class="col-6 col-md-3"><div class="kpi-tile kpi-tile--negative"><div class="kpi-tile__label">Критичні</div><div class="kpi-tile__value" style="font-size: 18px">{{ result.critLow | num }} / {{ result.critHigh | num }}</div></div></div>
          <div class="col-6 col-md-3"><div class="kpi-tile" :class="result.isPanicCito ? 'kpi-tile--negative' : ''"><div class="kpi-tile__label">Паніка / CITO</div><div class="kpi-tile__value" style="font-size: 18px">{{ result.isPanicCito ? 'ТАК' : 'ні' }}</div></div></div>
          <div class="col-6 col-md-3"><div class="kpi-tile" :class="result.isDeltaAlert ? 'kpi-tile--warning' : ''"><div class="kpi-tile__label">Delta</div><div class="kpi-tile__value" style="font-size: 18px">{{ result.deltaPercent | pct }}</div><div class="kpi-tile__hint">{{ result.isDeltaAlert ? 'перевищено поріг' : 'у межах' }}</div></div></div>
        </div>
        <div v-if="result.winningLayer" class="q-mt-md">
          <div class="section-title">Переможний шар</div>
          <q-banner dense rounded class="bg-blue-1">
            <q-badge :color="layerColor(result.winningLayer.layerType)" :label="layerLabel(result.winningLayer.layerType)" class="q-mr-sm" />
            <b>{{ result.winningLayer.normName || result.winningLayer.id }}</b> · пріоритет {{ result.winningLayer.priorityOrder }}
            <span v-if="result.winningLayer.methodCode"> · {{ result.winningLayer.methodCode }}</span>
            <div class="text-caption text-grey-8">{{ describeLayer(result.winningLayer) }}</div>
          </q-banner>
        </div>
        <div class="section-title q-mt-md">Трасування каскаду (auditTrace)</div>
        <q-markup-table dense flat bordered data-testid="rpTrace">
          <thead><tr><th>#</th><th class="text-left">Шар</th><th>Пріоритет</th><th class="text-left">Умова</th><th>Результат</th></tr></thead>
          <tbody>
            <tr v-for="(t, i) in trace" :key="i" :class="{ 'bg-green-1': t.matched || t.isMatch || t.won, 'text-grey-6': !(t.matched || t.isMatch || t.won) }">
              <td>{{ i + 1 }}</td>
              <td><q-badge :color="layerColor(t.layerType)" :label="layerLabel(t.layerType)" class="q-mr-xs" />{{ t.normName || t.layerName || t.layerId || '' }}</td>
              <td class="text-center">{{ t.priorityOrder || t.priority }}</td>
              <td>{{ t.condition || t.reason || t.message || describeLayer(t) }}</td>
              <td class="text-center"><q-icon :name="(t.matched || t.isMatch || t.won) ? 'check_circle' : 'remove_circle_outline'" :color="(t.matched || t.isMatch || t.won) ? 'positive' : 'grey-5'" /></td>
            </tr>
            <tr v-if="!trace.length"><td colspan="5" class="text-grey-6 text-center">Трасування порожнє</td></tr>
          </tbody>
        </q-markup-table>
      </div>
      <div v-else class="medlink-card"><empty-state title="Симулятор підбору норми" icon="calculate" hint="Задайте контекст пацієнта — каскад Simplex (BASELINE → DEMOGRAPHIC → CLINICAL → MENSTRUAL → PREGNANCY) покаже переможний шар і трасування" /></div>
    </div>
  </div>
</template>

<script>
import { LAYER_TYPE, AGE_UNITS, MENSTRUAL_PHASES } from '../../../utils/statuses';
import { parseDecimal } from '../../../utils/format';

export default {
  name: 'ResolverPlayground',
  props: { initialTestCode: { type: String, default: '' } },
  data () {
    return {
      ctx: { testCode: this.initialTestCode || null, methodCode: null, gender: 'F', age: 35, ageUnit: 'YEARS', isPregnant: false, pregnancyWeek: null, menstrualPhase: null, icd10Code: '', measuredValue: '', previousValue: '' },
      result: null, loading: false, error: '', testFilter: '', ageUnits: AGE_UNITS, phases: MENSTRUAL_PHASES
    };
  },
  computed: {
    testOptions () { const f = this.testFilter.toLowerCase(); return this.$store.getters['dictionaries/items']('tests').filter(t => !f || `${t.code} ${t.name}`.toLowerCase().includes(f)).map(t => ({ value: t.code, label: `${t.code} — ${t.name}` })); },
    methodOptions () { return this.$store.getters['dictionaries/items']('method-types').map(m => ({ value: m.code, label: m.name })); },
    trace () { return (this.result && (this.result.auditTrace || this.result.trace)) || []; }
  },
  watch: { initialTestCode (v) { if (v) this.ctx.testCode = v; } },
  created () { this.$store.dispatch('dictionaries/loadMany', ['tests', 'method-types']); },
  methods: {
    filterTests (val, update) { update(() => { this.testFilter = val || ''; }); },
    layerLabel (t) { return (LAYER_TYPE[t] || {}).label || t || ''; },
    layerColor (t) { return (LAYER_TYPE[t] || {}).color || 'grey-6'; },
    describeLayer (l) {
      if (!l) return '';
      const parts = [];
      if (l.gender && l.gender !== 'ANY') parts.push(l.gender === 'M' ? 'чол.' : 'жін.');
      if (l.ageFrom !== null && l.ageFrom !== undefined || l.ageTo !== null && l.ageTo !== undefined) parts.push(`вік ${l.ageFrom !== null && l.ageFrom !== undefined ? l.ageFrom : '…'}–${l.ageTo !== null && l.ageTo !== undefined ? l.ageTo : '…'} ${(AGE_UNITS.find(u => u.value === l.ageUnit) || {}).label || ''}`);
      if (l.pregnancyWeekFrom || l.pregnancyWeekTo) parts.push(`вагітність ${l.pregnancyWeekFrom || '…'}–${l.pregnancyWeekTo || '…'} тиж.`);
      if (l.menstrualPhase) parts.push(`фаза ${(MENSTRUAL_PHASES.find(p => p.value === l.menstrualPhase) || {}).label || l.menstrualPhase}`);
      if (l.icd10Code) parts.push(`МКХ ${l.icd10Code}`);
      if (l.normLow !== undefined || l.normHigh !== undefined) parts.push(`норма ${l.normLow !== null && l.normLow !== undefined ? l.normLow : '…'}–${l.normHigh !== null && l.normHigh !== undefined ? l.normHigh : '…'}`);
      return parts.join(' · ');
    },
    async resolve () {
      this.loading = true; this.error = '';
      const body = { ...this.ctx, measuredValue: parseDecimal(this.ctx.measuredValue), previousValue: parseDecimal(this.ctx.previousValue), icd10Code: this.ctx.icd10Code || null, methodCode: this.ctx.methodCode || null, pregnancyWeek: this.ctx.isPregnant ? this.ctx.pregnancyWeek : null, menstrualPhase: this.ctx.isPregnant ? null : this.ctx.menstrualPhase };
      try { this.result = await this.$api.resolveCascade(body); } catch (e) { this.error = e.userMessage || 'Помилка резолвера'; } finally { this.loading = false; }
    }
  }
};
</script>
