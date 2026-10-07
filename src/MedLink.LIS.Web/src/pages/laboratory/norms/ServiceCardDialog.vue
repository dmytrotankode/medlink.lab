<template>
  <q-dialog :value="value" @input="$emit('input', $event)" maximized>
    <q-card class="column" data-testid="serviceCard">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="medical_services" class="q-mr-sm" />
        <div>
          <div class="text-subtitle1 text-weight-bold">{{ profile ? profile.name : 'Картка послуги' }} <q-badge v-if="profile" color="white" text-color="primary" :label="profile.code" /></div>
          <div class="text-caption">{{ profile && profile.category }} · TAT {{ profile && profile.turnaroundHours }} год · {{ profile && profile.price | money }}</div>
        </div>
        <q-space />
        <q-btn flat dense no-caps icon="edit" label="Редагувати" @click="$emit('edit', profile)" />
        <q-btn flat dense no-caps icon="delete" label="Видалити" @click="$emit('delete', profile)" />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-tabs v-model="tab" dense align="left" class="bg-white text-grey-8" active-color="primary" indicator-color="primary">
        <q-tab name="main" label="Головна" data-testid="sc-main" />
        <q-tab name="tests" :label="`Показники (${tests.length})`" data-testid="sc-tests" />
        <q-tab name="norms" :label="`Норми (${layers.length})`" data-testid="sc-norms" />
        <q-tab name="lab" label="Лабораторія" data-testid="sc-lab" />
        <q-tab name="resolver" label="Резолвер" data-testid="sc-resolver" />
      </q-tabs>
      <q-separator />
      <q-card-section class="col scroll bg-grey-2">
        <div v-if="loading" class="text-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>
        <q-tab-panels v-else v-model="tab" animated class="bg-transparent">
          <!-- Головна -->
          <q-tab-panel name="main">
            <div class="row q-col-gutter-md">
              <div class="col-12 col-md-6">
                <div class="medlink-card q-pa-md">
                  <q-banner v-if="profile && profile.misServiceId" dense rounded class="bg-blue-1 text-grey-9 q-mb-sm"><q-icon name="link" color="primary" /> Джерело: довідник послуг MedLink (dct_service, ID {{ profile.misServiceId }}). Код, назва та ціна — лише для читання; лабораторні атрибути редагуються тут.</q-banner>
                  <q-markup-table dense flat>
                    <tbody>
                      <tr><td class="text-grey-7">Код</td><td class="mono">{{ profile && profile.code }}</td></tr>
                      <tr><td class="text-grey-7">Назва</td><td><b>{{ profile && profile.name }}</b></td></tr>
                      <tr><td class="text-grey-7">Категорія</td><td>{{ profile && profile.category }}</td></tr>
                      <tr><td class="text-grey-7">Термін виконання</td><td>{{ profile && profile.turnaroundHours }} год</td></tr>
                      <tr><td class="text-grey-7">Натще</td><td>{{ profile && profile.fastingRequired ? 'так' : 'ні' }}</td></tr>
                      <tr><td class="text-grey-7">Ціна</td><td>{{ profile && profile.price | money }}</td></tr>
                      <tr><td class="text-grey-7">Активна</td><td><q-icon :name="profile && profile.isActive !== false ? 'check_circle' : 'cancel'" :color="profile && profile.isActive !== false ? 'positive' : 'grey-5'" /></td></tr>
                      <tr v-if="card && card.description"><td class="text-grey-7">Опис</td><td>{{ card.description }}</td></tr>
                    </tbody>
                  </q-markup-table>
                </div>
              </div>
              <div class="col-12 col-md-6">
                <div class="medlink-card q-pa-md">
                  <div class="section-title">Біоматеріали та пробірки</div>
                  <q-list dense>
                    <q-item v-for="t in tubePlan" :key="t.key">
                      <q-item-section avatar><div :style="{ background: t.color, width: '14px', height: '24px', borderRadius: '3px 3px 7px 7px', border: '1px solid #999' }" /></q-item-section>
                      <q-item-section><q-item-label>{{ t.tubeName }}</q-item-label><q-item-label caption>{{ t.biomaterialName }} · {{ t.tests.join(', ') }}</q-item-label></q-item-section>
                    </q-item>
                  </q-list>
                  <div class="section-title q-mt-md">Статистика норм</div>
                  <div class="row q-gutter-xs">
                    <q-chip v-for="(n, type) in layerStats" :key="type" dense :color="layerColor(type)" text-color="white">{{ layerLabel(type) }}: {{ n }}</q-chip>
                  </div>
                </div>
              </div>
            </div>
          </q-tab-panel>

          <!-- Показники -->
          <q-tab-panel name="tests">
            <div class="medlink-card">
              <q-table :data="tests" :columns="testColumns" dense flat row-key="id" hide-pagination :pagination="{ rowsPerPage: 0 }" no-data-label="У профілі немає показників">
                <template v-slot:body-cell-resultType="props"><q-td :props="props"><q-badge color="grey-7" :label="props.row.resultType" /></q-td></template>
                <template v-slot:body-cell-flags="props"><q-td :props="props"><q-badge v-if="props.row.requiresManualVerification" color="warning" text-color="dark" label="ручна вериф." class="q-mr-xs" /><q-badge v-if="props.row.isQcTracked" color="purple-6" label="ВКЯ" class="q-mr-xs" /><q-badge v-if="props.row.isRequired === false" color="grey-5" label="необов." /></q-td></template>
                <template v-slot:body-cell-layers="props"><q-td :props="props" class="text-center"><q-btn flat dense size="sm" color="primary" :label="String(layersFor(props.row.code).length)" @click="tab = 'norms'; normsTest = props.row.code" /></q-td></template>
              </q-table>
            </div>
          </q-tab-panel>

          <!-- Норми -->
          <q-tab-panel name="norms">
            <div class="medlink-card q-pa-sm q-mb-sm row items-center q-col-gutter-sm">
              <div class="col-12 col-md-4"><q-select v-model="normsTest" dense outlined clearable label="Показник" :options="tests.map(t => ({ value: t.code, label: `${t.code} — ${t.name}` }))" emit-value map-options /></div>
              <div class="col-12 col-md-5 text-caption text-grey-7">Каскад: шари сортуються за пріоритетом (100 вагітність → 80 фаза → 60 МКХ → 40 демографія → 10 базова); перший збіг перемагає.</div>
              <div class="col-12 col-md-3 text-right"><q-btn color="primary" icon="add" label="Додати шар" :disable="!normsTest && tests.length !== 1" data-testid="layerAdd" @click="openLayer(null)" /></div>
            </div>
            <div v-for="group in groupedLayers" :key="group.key" class="medlink-card q-mb-sm">
              <div class="medlink-card__title"><span><b>{{ group.testCode }}</b> <span class="text-grey-7">{{ group.testName }}</span> · методика: {{ group.methodName || group.methodCode || 'будь-яка' }}</span><span class="text-caption text-grey-6">{{ group.layers.length }} шар.</span></div>
              <q-markup-table dense flat>
                <thead><tr><th class="text-left">Тип</th><th>Пріор.</th><th class="text-left">Назва</th><th>Стать</th><th>Вік</th><th class="text-left">Умова</th><th>Норма</th><th>Критичні</th><th class="text-left">Текст</th><th>Прилад</th><th /></tr></thead>
                <tbody>
                  <tr v-for="l in group.layers" :key="l.id" :class="{ 'text-grey-5': l.isActive === false }" :data-testid="`layer-${l.id}`">
                    <td><q-badge :color="layerColor(l.layerType)" :label="layerLabel(l.layerType)" /></td>
                    <td class="text-center">{{ l.priorityOrder }}</td>
                    <td>{{ l.normName }}</td>
                    <td class="text-center">{{ l.gender === 'ANY' || !l.gender ? '—' : (l.gender === 'M' ? 'Ч' : 'Ж') }}</td>
                    <td class="text-center">{{ ageText(l) }}</td>
                    <td class="text-caption">{{ condText(l) }}</td>
                    <td class="text-center text-weight-bold">{{ l.normLow | num }} – {{ l.normHigh | num }} <span class="text-caption text-grey-7">{{ l.unit }}</span></td>
                    <td class="text-center text-negative">{{ l.critLow | num }} / {{ l.critHigh | num }}</td>
                    <td class="text-caption">{{ l.normText }}</td>
                    <td class="text-center mono text-caption">{{ l.analyzerCode }}</td>
                    <td class="text-right no-wrap"><q-btn flat dense round size="sm" icon="edit" color="primary" @click="openLayer(l)" /><q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDeleteLayer(l)" /></td>
                  </tr>
                </tbody>
              </q-markup-table>
            </div>
            <div v-if="!groupedLayers.length" class="medlink-card"><empty-state title="Шарів норм немає" icon="layers_clear" hint="Додайте базовий шар (BASELINE), а потім демографічні/клінічні" /></div>
          </q-tab-panel>

          <!-- Лабораторія -->
          <q-tab-panel name="lab">
            <div class="row q-col-gutter-md">
              <div class="col-12 col-md-6">
                <div class="medlink-card q-pa-md">
                  <div class="section-title">Методики та аналізатори</div>
                  <q-list dense separator>
                    <q-item v-for="t in tests" :key="t.id">
                      <q-item-section><q-item-label><b>{{ t.code }}</b> {{ t.name }}</q-item-label><q-item-label caption>методика: {{ methodName(t.methodId) }} · delta {{ t.deltaCheckMaxPct || '—' }}% / {{ t.deltaCheckHours || 72 }} год · {{ t.decimalPlaces }} зн.</q-item-label></q-item-section>
                      <q-item-section side><q-badge v-if="t.loincCode" color="blue-grey-6" :label="'LOINC ' + t.loincCode" /></q-item-section>
                    </q-item>
                  </q-list>
                </div>
              </div>
              <div class="col-12 col-md-6">
                <div class="medlink-card q-pa-md">
                  <div class="section-title">Reflex-правила</div>
                  <q-list dense separator>
                    <q-item v-for="r in reflexRules" :key="r.id">
                      <q-item-section><q-item-label>{{ r.triggerTestCode }} {{ r.conditionOperator }} {{ r.thresholdValue }} → <b>{{ r.reflexTestCode }}</b></q-item-label><q-item-label caption>{{ r.description }} {{ r.autoApprove ? '· авто' : '· підтвердження лаборантом' }}</q-item-label></q-item-section>
                    </q-item>
                    <q-item v-if="!reflexRules.length"><q-item-section class="text-grey-6">Reflex-правил для показників профілю немає</q-item-section></q-item>
                  </q-list>
                  <div v-if="card && card.lab" class="q-mt-md">
                    <div class="section-title">Дані лабораторії</div>
                    <pre class="mono text-caption" style="white-space: pre-wrap">{{ JSON.stringify(card.lab, null, 2) }}</pre>
                  </div>
                </div>
              </div>
            </div>
          </q-tab-panel>

          <!-- Резолвер -->
          <q-tab-panel name="resolver">
            <resolver-playground :initial-test-code="normsTest || (tests[0] && tests[0].code) || ''" />
          </q-tab-panel>
        </q-tab-panels>
      </q-card-section>
    </q-card>

    <norm-layer-dialog v-model="layerOpen" :layer="activeLayer" :test-code="normsTest || (tests[0] && tests[0].code) || ''" :test-id="testIdFor(normsTest)" :unit="unitFor(normsTest)" @saved="loadLayers" />
    <confirm-dialog v-model="deleteLayerOpen" title="Видалити шар норми" message="Шар буде видалено з каскаду." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteLayer" />
  </q-dialog>
</template>

<script>
import NormLayerDialog from './NormLayerDialog.vue';
import ResolverPlayground from './ResolverPlayground.vue';
import { LAYER_TYPE, AGE_UNITS, MENSTRUAL_PHASES } from '../../../utils/statuses';

export default {
  name: 'ServiceCardDialog',
  components: { NormLayerDialog, ResolverPlayground },
  props: { value: Boolean, profile: { type: Object, default: null } },
  data () {
    return {
      tab: 'main', card: null, layers: [], loading: false, normsTest: null,
      layerOpen: false, deleteLayerOpen: false, activeLayer: null,
      testColumns: [
        { name: 'code', label: 'Код', field: 'code', align: 'left' },
        { name: 'name', label: 'Показник', field: 'name', align: 'left' },
        { name: 'unit', label: 'Од.', field: 'unit', align: 'left' },
        { name: 'resultType', label: 'Тип', align: 'left' },
        { name: 'biomaterial', label: 'Біоматеріал', field: r => this.bioName(r.biomaterialTypeId), align: 'left' },
        { name: 'method', label: 'Методика', field: r => this.methodName(r.methodId), align: 'left' },
        { name: 'flags', label: 'Ознаки', align: 'left' },
        { name: 'layers', label: 'Шарів норм', align: 'center' }
      ]
    };
  },
  computed: {
    allTests () { return this.$store.getters['dictionaries/items']('tests'); },
    tests () {
      if (this.card && Array.isArray(this.card.tests) && this.card.tests.length) return this.card.tests;
      const items = (this.profile && this.profile.items) || [];
      return items.map(i => ({ ...(this.allTests.find(t => t.id === i.testId) || { id: i.testId, code: i.testCode, name: i.testName }), isRequired: i.isRequired })).filter(t => t.code);
    },
    layersFiltered () { return this.normsTest ? this.layers.filter(l => l.testCode === this.normsTest) : this.layers; },
    groupedLayers () {
      const groups = {};
      this.layersFiltered.forEach(l => {
        const key = `${l.testCode}|${l.methodCode || ''}`;
        if (!groups[key]) groups[key] = { key, testCode: l.testCode, testName: (this.tests.find(t => t.code === l.testCode) || {}).name, methodCode: l.methodCode, methodName: l.methodName, layers: [] };
        groups[key].layers.push(l);
      });
      Object.values(groups).forEach(g => g.layers.sort((a, b) => (b.priorityOrder || 0) - (a.priorityOrder || 0)));
      return Object.values(groups).sort((a, b) => a.testCode.localeCompare(b.testCode));
    },
    layerStats () { const s = {}; this.layers.forEach(l => { s[l.layerType] = (s[l.layerType] || 0) + 1; }); return s; },
    reflexRules () { const codes = new Set(this.tests.map(t => t.code)); return this.$store.getters['dictionaries/items']('reflex-rules').filter(r => codes.has(r.triggerTestCode)); },
    tubePlan () {
      const bios = this.$store.getters['dictionaries/items']('biomaterials'); const tubes = this.$store.getters['dictionaries/items']('tube-types');
      const groups = {};
      this.tests.forEach(t => {
        const bio = bios.find(b => b.id === t.biomaterialTypeId);
        const tube = tubes.find(tt => bio && (tt.code === bio.defaultContainer || tt.name === bio.defaultContainer));
        const key = `${t.biomaterialTypeId}-${tube ? tube.id : 'x'}`;
        if (!groups[key]) groups[key] = { key, biomaterialName: bio ? bio.name : '—', tubeName: tube ? tube.name : (bio && bio.defaultContainer) || 'Пробірка', color: tube ? tube.colorCode : '#bbb', tests: [] };
        groups[key].tests.push(t.code);
      });
      return Object.values(groups);
    }
  },
  watch: {
    value (v) { if (v) { this.tab = 'main'; this.normsTest = null; this.load(); } }
  },
  methods: {
    layerLabel (t) { return (LAYER_TYPE[t] || {}).label || t; },
    layerColor (t) { return (LAYER_TYPE[t] || {}).color || 'grey-6'; },
    bioName (id) { const b = this.$store.getters['dictionaries/byId']('biomaterials', id); return b ? b.name : '—'; },
    methodName (id) { const m = this.$store.getters['dictionaries/byId']('method-types', id); return m ? m.name : '—'; },
    testIdFor (code) { const t = this.tests.find(x => x.code === code) || this.tests[0]; return t ? t.id : null; },
    unitFor (code) { const t = this.tests.find(x => x.code === code) || this.tests[0]; return t ? t.unit : ''; },
    layersFor (code) { return this.layers.filter(l => l.testCode === code); },
    ageText (l) { if (l.ageFrom === null && l.ageTo === null) return '—'; if (l.ageFrom === undefined && l.ageTo === undefined) return '—'; const u = (AGE_UNITS.find(x => x.value === l.ageUnit) || {}).label || ''; return `${l.ageFrom !== null && l.ageFrom !== undefined ? l.ageFrom : '…'}–${l.ageTo !== null && l.ageTo !== undefined ? l.ageTo : '…'} ${u}`; },
    condText (l) {
      const p = [];
      if (l.pregnancyWeekFrom || l.pregnancyWeekTo) p.push(`вагітн. ${l.pregnancyWeekFrom || '…'}–${l.pregnancyWeekTo || '…'} тиж.`);
      if (l.menstrualPhase) p.push((MENSTRUAL_PHASES.find(x => x.value === l.menstrualPhase) || {}).label || l.menstrualPhase);
      if (l.icd10Code) p.push(`МКХ ${l.icd10Code}`);
      if (l.deltaCheckMaxPct) p.push(`Δ ${l.deltaCheckMaxPct}%`);
      return p.join(' · ');
    },
    async load () {
      if (!this.profile) return;
      this.loading = true;
      await this.$store.dispatch('dictionaries/loadMany', ['tests', 'biomaterials', 'tube-types', 'method-types', 'reflex-rules']);
      try { this.card = await this.$api.serviceCard(this.profile.id); } catch (e) { this.card = null; }
      await this.loadLayers();
      this.loading = false;
    },
    async loadLayers () {
      const codes = this.tests.map(t => t.code);
      if (this.card && Array.isArray(this.card.layers)) { this.layers = this.card.layers; }
      const results = await Promise.allSettled(codes.map(c => this.$api.normCombinations(c)));
      const list = [];
      results.forEach((r, i) => { if (r.status === 'fulfilled') (Array.isArray(r.value) ? r.value : (r.value && r.value.items) || []).forEach(l => list.push({ testCode: codes[i], ...l })); });
      if (list.length || !(this.card && Array.isArray(this.card.layers))) this.layers = list;
    },
    openLayer (l) { this.activeLayer = l; if (!this.normsTest && this.tests.length === 1) this.normsTest = this.tests[0].code; this.layerOpen = true; },
    askDeleteLayer (l) { this.activeLayer = l; this.deleteLayerOpen = true; },
    async doDeleteLayer () { try { await this.$api.deleteNormCombination(this.activeLayer.id); this.$q.notify({ type: 'positive', message: 'Шар видалено' }); this.loadLayers(); } catch (e) { this.$q.notify({ type: 'negative', message: e.userMessage || 'Помилка' }); } }
  }
};
</script>
