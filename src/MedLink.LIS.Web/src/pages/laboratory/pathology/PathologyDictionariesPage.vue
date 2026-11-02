<!--
  ТЗ 22.10 (FR-PAT-100…106): «Довідники патоморфології» — шаблони описів, МКХ-О, методики забарвлення та ІГХ з цінами,
  reflex-правила ІГХ, типи матеріалу та строки (SLA), фіксатори, причини відмови.
-->
<template>
  <div data-testid="pathologyDictionariesPage">
    <page-header title="Довідники патоморфології" icon="fas fa-book-medical" subtitle="Шаблони, МКХ-О, методики та ІГХ, reflex-правила, типи матеріалу й строки, фіксатори, причини відмови"
      :breadcrumbs="[{ label: 'Довідники' }, { label: 'Патоморфологія' }]" />
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />
    <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8 q-mb-sm">
      <q-tab v-for="t in tabs" :key="t.name" :name="t.name" :label="t.label" />
    </q-tabs>
    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="__k" flat dense :pagination="{ rowsPerPage: 50 }" :loading="loading" wrap-cells />
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';

const COLS = {
  templates: [['name', 'Орган · вид матеріалу'], ['macro', 'Макроопис'], ['micro', 'Мікроопис']],
  topo: [['code', 'Код'], ['name', 'Топографія']],
  morph: [['code', 'Код/поведінка'], ['name', 'Морфологія']],
  stains: [['code', 'Код'], ['name', 'Методика'], ['isIhc', 'ІГХ', v => (v ? 'так' : '')], ['price', 'Ціна, грн']],
  reflex: [['topoFrom', 'Топографія від'], ['topoTo', 'до'], ['morphCodes', 'Морфологія'], ['panel', 'Панель'], ['why', 'Обґрунтування']],
  materialTypes: [['code', 'Код'], ['name', 'Тип матеріалу'], ['slaDays', 'SLA, роб. дні', v => (v === 0 ? '20 хв' : v)], ['serviceGroup', 'Група послуг'], ['basePrice', 'Базова ціна']],
  fixatives: [['name', 'Фіксатор'], ['minHours', 'Мін., год'], ['maxHours', 'Макс., год']],
  rejectReasons: [['code', 'Код'], ['name', 'Причина відмови']]
};

export default {
  name: 'PathologyDictionariesPage',
  mixins: [apiMixin],
  data () {
    return {
      dict: {}, tab: 'templates',
      tabs: [
        { name: 'templates', label: 'Шаблони описів' }, { name: 'topo', label: 'МКХ-О топографія' }, { name: 'morph', label: 'МКХ-О морфологія' },
        { name: 'stains', label: 'Методики та ІГХ' }, { name: 'reflex', label: 'Reflex-правила ІГХ' }, { name: 'materialTypes', label: 'Типи матеріалу та строки' },
        { name: 'fixatives', label: 'Фіксатори' }, { name: 'rejectReasons', label: 'Причини відмови' }
      ]
    };
  },
  computed: {
    columns () { return COLS[this.tab].map(([k, l, f]) => ({ name: k, label: l, field: f ? r => f(r[k]) : k, align: 'left', classes: k === 'macro' || k === 'micro' ? 'text-caption' : '' })); },
    rows () { return (this.dict[this.tab] || []).map((r, i) => ({ ...r, __k: i })); }
  },
  created () { this.load(); },
  methods: {
    async load () { const d = await this.callApi(() => this.$api.pathDictionaries()); if (d) this.dict = d; }
  }
};
</script>
