<!--
  ТЗ 22.10: «Довідники патоморфології» — повне ведення (створення, правка, архів, повернення з архіву, імпорт/експорт):
  методики забарвлення та ІГХ з цінами, типи матеріалу й строки, фіксатори, причини відмови, шаблони описів,
  приміщення зберігання, правила автозамовлення ІГХ (діапазони топографії), цитологічні довідники й тарифи.
  МКХ-О (топографія та морфологія) — класифікатор, лише перегляд.
  Екран доступний адміністратору лабораторії.
-->
<template>
  <div data-testid="pathologyDictionariesPage">
    <page-header title="Довідники патоморфології" icon="fas fa-book-medical" :breadcrumbs="[{ label: 'Довідники' }, { label: 'Патоморфологія' }]">
      <q-input v-if="!current.crud" v-model="search" dense outlined clearable placeholder="Пошук у довіднику" style="min-width: 220px" data-testid="dictSearch">
        <template v-slot:prepend><q-icon name="search" /></template>
      </q-input>
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" :retryable="apiErrorRetryable" @retry="load" @dismiss="apiError = null" />
    <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8 q-mb-sm" outside-arrows mobile-arrows>
      <q-tab v-for="t in tabs" :key="t.name" :name="t.name" :label="t.label" :data-testid="`pathTab-${t.name}`" />
    </q-tabs>
    <dictionary-crud-table v-if="current.crud" :key="current.crud" :name="current.crud" />
    <div v-else class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="__k" flat dense :pagination="{ rowsPerPage: 50 }" :loading="loading" wrap-cells no-data-label="Нічого не знайдено" />
      <div class="text-caption text-grey-7 q-pa-sm">Класифікатор МКХ-О — лише перегляд; його оновлює постачальник класифікатора разом із оновленням системи.</div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';
import DictionaryCrudTable from '../../dictionaries/DictionaryCrudTable.vue';

const COLS = {
  topo: [['code', 'Код'], ['name', 'Топографія']],
  morph: [['code', 'Код (морфологія / поведінка)'], ['name', 'Морфологія']]
};

export default {
  name: 'PathologyDictionariesPage',
  components: { DictionaryCrudTable },
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      dict: {}, tab: 'path-methods', search: '',
      tabs: [
        { name: 'path-methods', label: 'Методики та ІГХ', crud: 'path-methods' },
        { name: 'path-material-types', label: 'Типи матеріалу та строки', crud: 'path-material-types' },
        { name: 'path-fixatives', label: 'Фіксатори', crud: 'path-fixatives' },
        { name: 'path-reject-reasons', label: 'Причини відмови', crud: 'path-reject-reasons' },
        { name: 'path-templates', label: 'Шаблони описів', crud: 'path-templates' },
        { name: 'path-rooms', label: 'Приміщення зберігання', crud: 'path-rooms' },
        { name: 'path-ihc-rules', label: 'Правила автозамовлення ІГХ', crud: 'path-ihc-rules' },
        { name: 'path-cytology', label: 'Цитологія', crud: 'path-cytology' },
        { name: 'path-prices', label: 'Тарифи', crud: 'path-prices' },
        { name: 'topo', label: 'МКХ-О топографія' },
        { name: 'morph', label: 'МКХ-О морфологія' }
      ]
    };
  },
  computed: {
    current () { return this.tabs.find(t => t.name === this.tab) || {}; },
    columns () {
      return (COLS[this.tab] || []).map(([k, l]) => ({ name: k, label: l, field: k, align: 'left' }));
    },
    rows () {
      const q = (this.search || '').trim().toLowerCase();
      let list = (this.dict[this.tab] || []).map(r => ({ ...r }));
      if (q) list = list.filter(r => Object.keys(r).some(k => String(r[k] === null || r[k] === undefined ? '' : r[k]).toLowerCase().includes(q)));
      return list.map((r, i) => ({ ...r, __k: i }));
    }
  },
  created () { this.load(); },
  methods: {
    async load () { const d = await this.callApi(() => this.$api.pathDictionaries()); if (d) this.dict = d; }
  }
};
</script>
