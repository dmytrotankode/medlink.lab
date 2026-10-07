<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 760px; max-width: 96vw" data-testid="layerDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="layers" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Шар норми' : 'Новий шар норми' }} — {{ form.testCode }}</div>
        <q-space /><q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section style="max-height: 70vh; overflow: auto">
        <div class="row q-col-gutter-sm">
          <div class="col-12 col-md-4">
            <q-select v-model="form.layerType" outlined dense label="Тип шару *" :options="layerTypes" emit-value map-options @input="onLayerType" data-testid="layerType">
              <template v-slot:option="scope"><q-item v-bind="scope.itemProps" v-on="scope.itemEvents"><q-item-section><q-item-label>{{ scope.opt.label }}</q-item-label><q-item-label caption>пріоритет {{ scope.opt.priority }}</q-item-label></q-item-section></q-item></template>
            </q-select>
          </div>
          <div class="col-6 col-md-2"><q-input v-model.number="form.priorityOrder" outlined dense type="number" label="Пріоритет" /></div>
          <div class="col-6 col-md-3"><q-select v-model="form.methodCode" outlined dense clearable label="Методика" :options="methodOptions" emit-value map-options /></div>
          <div class="col-12 col-md-3"><q-input v-model="form.normName" outlined dense label="Назва норми" /></div>

          <div class="col-12"><div class="section-title q-mt-sm">Демографія</div></div>
          <div class="col-6 col-md-3"><q-select v-model="form.gender" outlined dense label="Стать" :options="[{ value: 'ANY', label: 'Будь-яка' }, { value: 'M', label: 'Чоловіча' }, { value: 'F', label: 'Жіноча' }]" emit-value map-options @input="form.isGender = form.gender !== 'ANY'" /></div>
          <div class="col-6 col-md-3"><q-select v-model="form.ageUnit" outlined dense label="Одиниця віку" :options="ageUnits" emit-value map-options /></div>
          <div class="col-6 col-md-3"><q-input v-model.number="form.ageFrom" outlined dense type="number" label="Вік від" @input="form.isAge = form.ageFrom !== null || form.ageTo !== null" /></div>
          <div class="col-6 col-md-3"><q-input v-model.number="form.ageTo" outlined dense type="number" label="Вік до" @input="form.isAge = form.ageFrom !== null || form.ageTo !== null" /></div>

          <template v-if="form.layerType === 'PREGNANCY'">
            <div class="col-12"><div class="section-title q-mt-sm">Вагітність</div></div>
            <div class="col-6 col-md-3"><q-input v-model.number="form.pregnancyWeekFrom" outlined dense type="number" label="Тиждень від" /></div>
            <div class="col-6 col-md-3"><q-input v-model.number="form.pregnancyWeekTo" outlined dense type="number" label="Тиждень до" /></div>
          </template>
          <template v-if="form.layerType === 'MENSTRUAL_PHASE'">
            <div class="col-12"><div class="section-title q-mt-sm">Фаза циклу</div></div>
            <div class="col-12 col-md-6"><q-select v-model="form.menstrualPhase" outlined dense label="Фаза" :options="phases" emit-value map-options /></div>
          </template>
          <template v-if="form.layerType === 'CLINICAL_ICD10'">
            <div class="col-12"><div class="section-title q-mt-sm">Клінічний контекст</div></div>
            <div class="col-12 col-md-6"><q-input v-model="form.icd10Code" outlined dense label="Код МКХ-10 (напр. E11, O24)" /></div>
          </template>

          <div class="col-12"><div class="section-title q-mt-sm">Референтні межі</div></div>
          <div class="col-6 col-md-2"><q-input v-model="form.normLow" outlined dense label="Норма від" inputmode="decimal" data-testid="normLow" /></div>
          <div class="col-6 col-md-2"><q-input v-model="form.normHigh" outlined dense label="Норма до" inputmode="decimal" data-testid="normHigh" /></div>
          <div class="col-6 col-md-2"><q-input v-model="form.critLow" outlined dense label="Крит. низько" inputmode="decimal" input-class="text-negative" /></div>
          <div class="col-6 col-md-2"><q-input v-model="form.critHigh" outlined dense label="Крит. високо" inputmode="decimal" input-class="text-negative" /></div>
          <div class="col-6 col-md-2"><q-input v-model="form.unit" outlined dense label="Одиниця" /></div>
          <div class="col-6 col-md-2"><q-input v-model="form.deltaCheckMaxPct" outlined dense label="Delta %, поріг" inputmode="decimal" /></div>
          <div class="col-12 col-md-6"><q-input v-model="form.normText" outlined dense label="Текстова норма (для якісних показників: «негативний», «не виявлено»)" /></div>
          <div class="col-6 col-md-3"><q-input v-model="form.analyzerCode" outlined dense label="Код на приладі (norm_code)" /></div>
          <div class="col-6 col-md-3"><q-input v-model="form.dilution" outlined dense label="Розведення" /></div>
          <div class="col-12"><q-toggle v-model="form.isActive" label="Активний шар" color="positive" /></div>
        </div>
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="primary" icon="save" label="Зберегти шар" :loading="saving" :disable="!form.layerType" data-testid="layerSave" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { LAYER_TYPE, AGE_UNITS, MENSTRUAL_PHASES } from '../../../utils/statuses';
import { parseDecimal } from '../../../utils/format';

export default {
  name: 'NormLayerDialog',
  props: {
    value: Boolean,
    layer: { type: Object, default: null },
    testCode: { type: String, default: '' },
    testId: { type: String, default: null },
    unit: { type: String, default: '' }
  },
  data () {
    return {
      form: this.empty(), saving: false, error: '',
      layerTypes: Object.keys(LAYER_TYPE).map(k => ({ value: k, label: LAYER_TYPE[k].label, priority: LAYER_TYPE[k].priority })),
      ageUnits: AGE_UNITS, phases: MENSTRUAL_PHASES
    };
  },
  computed: {
    methodOptions () { return this.$store.getters['dictionaries/items']('method-types').map(m => ({ value: m.code, label: `${m.name} (${m.code})` })); }
  },
  watch: {
    value (v) {
      if (v) {
        this.error = '';
        const l = this.layer;
        this.form = l ? { ...this.empty(), ...l, normLow: this.str(l.normLow), normHigh: this.str(l.normHigh), critLow: this.str(l.critLow), critHigh: this.str(l.critHigh), deltaCheckMaxPct: this.str(l.deltaCheckMaxPct) } : { ...this.empty(), testCode: this.testCode, testId: this.testId, unit: this.unit };
        this.$store.dispatch('dictionaries/load', 'method-types');
      }
    }
  },
  methods: {
    str (v) { return v === null || v === undefined ? '' : String(v).replace('.', ','); },
    empty () {
      return { id: null, testId: null, testCode: '', methodCode: null, layerType: 'BASELINE', priorityOrder: 10, normName: '', gender: 'ANY', isGender: false, ageUnit: 'YEARS', ageFrom: null, ageTo: null, isAge: false, isMenstrualPhase: false, menstrualPhase: null, isPregnancy: false, pregnancyWeekFrom: null, pregnancyWeekTo: null, icd10Code: '', normLow: '', normHigh: '', critLow: '', critHigh: '', normText: '', unit: '', deltaCheckMaxPct: '', analyzerCode: '', dilution: '', isActive: true };
    },
    onLayerType (t) { this.form.priorityOrder = (LAYER_TYPE[t] || {}).priority || 10; this.form.isPregnancy = t === 'PREGNANCY'; this.form.isMenstrualPhase = t === 'MENSTRUAL_PHASE'; },
    async save () {
      this.saving = true; this.error = '';
      const f = this.form;
      const body = {
        ...f,
        isGender: f.gender !== 'ANY',
        isAge: f.ageFrom !== null && f.ageFrom !== '' || f.ageTo !== null && f.ageTo !== '',
        isPregnancy: f.layerType === 'PREGNANCY',
        isMenstrualPhase: f.layerType === 'MENSTRUAL_PHASE',
        normLow: parseDecimal(f.normLow), normHigh: parseDecimal(f.normHigh), critLow: parseDecimal(f.critLow), critHigh: parseDecimal(f.critHigh),
        deltaCheckMaxPct: parseDecimal(f.deltaCheckMaxPct),
        normText: f.normText || null, icd10Code: f.icd10Code || null, analyzerCode: f.analyzerCode || null, dilution: f.dilution || null, methodCode: f.methodCode || null,
        ageFrom: f.ageFrom === '' ? null : f.ageFrom, ageTo: f.ageTo === '' ? null : f.ageTo
      };
      try {
        const res = await this.$api.saveNormCombination(body);
        this.$q.notify({ type: 'positive', message: 'Шар норми збережено' });
        this.$emit('saved', res); this.$emit('input', false);
      } catch (e) { this.error = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    }
  }
};
</script>
