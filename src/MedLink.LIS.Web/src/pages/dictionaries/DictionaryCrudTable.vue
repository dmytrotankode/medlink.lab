<template>
  <div class="dictionary-crud" :data-testid="`dict-${name}`">
    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-4"><q-input v-model="search" dense outlined clearable placeholder="Пошук…" data-testid="dictSearch"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
      <div class="col-6 col-md-3"><q-toggle v-model="onlyActive" label="Тільки активні" color="primary" /></div>
      <div class="col-6 col-md-5 row justify-end q-gutter-xs">
        <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
        <q-btn v-if="allowImport" outline dense color="grey-8" icon="upload_file" label="Імпорт" @click="importOpen = true" />
        <q-btn unelevated dense color="primary" icon="add" label="Створити" data-testid="dictCreate" @click="openForm(null)" />
      </div>
    </div>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card">
      <q-table :data="filtered" :columns="columns" row-key="id" dense flat :loading="loading" :pagination.sync="pagination" :rows-per-page-options="[25, 50, 100, 0]" no-data-label="Записів немає" data-testid="dictTable" @row-click="(e, row) => openView(row)" class="cursor-pointer">
        <template v-slot:body-cell="props">
          <q-td :props="props">
            <template v-if="props.col.name === 'colorCode'"><div :style="{ background: props.value || '#ccc', width: '18px', height: '18px', borderRadius: '50%', border: '1px solid #999' }" :title="props.value" /></template>
            <template v-else-if="props.col.name === 'exchType'"><q-badge color="teal-7" :label="props.value" /></template>
            <template v-else-if="props.col.name === 'labRole'">{{ roleLabel(props.value) }}</template>
            <template v-else-if="props.col.name === 'category' && name === 'analyzer-types'">{{ categoryLabel(props.value) }}</template>
            <template v-else-if="typeof props.value === 'boolean'"><q-icon :name="props.value ? 'check_circle' : 'radio_button_unchecked'" :color="props.value ? 'positive' : 'grey-5'" /></template>
            <template v-else-if="props.col.name.endsWith('Id')">{{ refLabel(props.col.name, props.value) }}</template>
            <template v-else-if="props.col.name === 'itemsCount'">{{ (props.row.items || []).length }}</template>
            <template v-else-if="selectField(props.col.name)">{{ displayValue(selectField(props.col.name), props.value) }}</template>
            <template v-else>{{ props.value === null || props.value === undefined ? '—' : props.value }}</template>
          </q-td>
        </template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right no-wrap" @click.stop>
            <q-btn flat dense round size="sm" icon="visibility" color="grey-8" @click="openView(props.row)"><q-tooltip>Картка</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="edit" color="primary" :data-testid="`edit-${props.row.code || props.row.id}`" @click="openForm(props.row)" />
            <q-btn flat dense round size="sm" icon="delete" color="grey-7" :data-testid="`delete-${props.row.code || props.row.id}`" @click="askDelete(props.row)" />
          </q-td>
        </template>
      </q-table>
    </div>

    <dictionary-form-dialog v-model="formOpen" :name="name" :item="active" @saved="load" />

    <q-dialog v-model="viewOpen">
      <q-card style="min-width: 560px; max-width: 96vw" data-testid="dictView">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm"><q-icon :name="schema.icon" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ active && (active.name || active.fullName || active.code || active.id) }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section v-if="active">
          <q-markup-table dense flat>
            <tbody>
              <tr v-for="f in schema.fields" :key="f.name" v-show="f.type !== 'profileItems'"><td class="text-grey-7" style="width: 40%">{{ f.label }}</td><td>{{ displayValue(f, active[f.name]) }}</td></tr>
              <tr v-if="active.items"><td class="text-grey-7">Показники</td><td>{{ (active.items || []).map(i => refLabel('testId', i.testId)).join(', ') }}</td></tr>
            </tbody>
          </q-markup-table>
          <div v-if="name === 'tests' && usedIn.length" class="q-mt-sm text-caption">Використовується у профілях: <b>{{ usedIn.map(p => p.name || p.code).join(', ') }}</b></div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat color="primary" icon="edit" label="Редагувати" @click="viewOpen = false; openForm(active)" /><q-btn flat label="Закрити" v-close-popup /></q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="importOpen">
      <q-card style="min-width: 520px">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">Імпорт довідника «{{ schema.title }}» (JSON/CSV)</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-file v-model="importFile" outlined dense label="Файл JSON або CSV" accept=".json,.csv" />
          <div class="row q-gutter-sm"><q-btn outline color="primary" label="Попередній перегляд (dryRun)" :disable="!importFile" :loading="importing" @click="runImport(true)" /><q-btn color="primary" label="Імпортувати" :disable="!importFile || !importPreview" :loading="importing" @click="runImport(false)" /></div>
          <pre v-if="importPreview" class="mono bg-grey-2 q-pa-sm" style="max-height: 240px; overflow: auto; font-size: 11px">{{ JSON.stringify(importPreview, null, 2) }}</pre>
        </q-card-section>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="deleteOpen" title="Видалити запис" :message="`«${active && (active.name || active.fullName || active.code)}» буде деактивовано (soft delete). За наявності залежностей API поверне 409.`" ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import DictionaryFormDialog from './DictionaryFormDialog.vue';
import { DICTIONARIES, columnLabel } from './dictionarySchemas';
import { ROLE, ANALYZER_CATEGORIES } from '../../utils/statuses';

export default {
  name: 'DictionaryCrudTable',
  mixins: [apiMixin],
  components: { DictionaryFormDialog },
  props: { name: { type: String, required: true }, allowImport: { type: Boolean, default: true } },
  data () {
    return { items: [], search: '', onlyActive: true, formOpen: false, viewOpen: false, deleteOpen: false, importOpen: false, active: null, usedIn: [], importFile: null, importPreview: null, importing: false, pagination: { rowsPerPage: 25, sortBy: 'code' } };
  },
  computed: {
    schema () { return DICTIONARIES[this.name] || { title: this.name, fields: [], columns: ['code', 'name'], icon: 'list' }; },
    columns () {
      return [...this.schema.columns.map(c => ({ name: c, label: columnLabel(this.schema, c), field: c === 'itemsCount' ? r => (r.items || []).length : c, align: 'left', sortable: true })), { name: 'actions', label: '', align: 'right' }];
    },
    filtered () {
      const s = (this.search || '').toLowerCase();
      return this.items.filter(i => (!this.onlyActive || i.isActive !== false) && (!s || JSON.stringify(i).toLowerCase().includes(s)));
    }
  },
  watch: { name () { this.load(); } },
  created () { this.load(); },
  methods: {
    roleLabel (r) { return ROLE[r] || r || '—'; },
    categoryLabel (c) { const x = ANALYZER_CATEGORIES.find(a => a.value === c); return x ? x.label : c; },
    refLabel (col, id) {
      if (id === null || id === undefined) return '—';
      const map = { biomaterialTypeId: 'biomaterials', departmentId: 'departments', methodId: 'method-types', antibioticId: 'antibiotics', testId: 'tests', analyzerTypeId: 'analyzer-types' };
      const d = map[col]; if (!d) return id;
      const it = this.$store.getters['dictionaries/byId'](d, id);
      return it ? (it.name || it.fullName || it.code) : id;
    },
    selectField (col) { return (this.schema.fields || []).find(f => f.name === col && f.type === 'select' && Array.isArray(f.options) && typeof f.options[0] === 'object'); },
    displayValue (f, v) {
      if (v === null || v === undefined || v === '') return '—';
      if (f.type === 'toggle') return v ? 'так' : 'ні';
      if (f.type === 'dict') return this.refLabel(f.name, v) || v;
      if (f.type === 'select' && f.name === 'labRole') return this.roleLabel(v);
      if (f.type === 'select' && Array.isArray(f.options)) { const o = f.options.find(x => x && x.value === v); if (o) return o.label; }
      if (Array.isArray(v)) return v.join(', ');
      return v;
    },
    async load () {
      const res = await this.callApi(() => this.$api.dictList(this.name));
      if (res !== undefined) { this.items = this.asList(res); this.$emit('loaded', this.items); }
      const refs = { biomaterialTypeId: 'biomaterials', departmentId: 'departments', methodId: 'method-types', antibioticId: 'antibiotics', testId: 'tests' };
      const needed = new Set();
      this.schema.columns.forEach(c => { if (refs[c]) needed.add(refs[c]); });
      if (this.name === 'profiles') needed.add('tests');
      if (needed.size) this.$store.dispatch('dictionaries/loadMany', [...needed]);
    },
    openForm (item) { this.active = item; this.formOpen = true; },
    async openView (item) {
      this.active = item; this.viewOpen = true; this.usedIn = [];
      if (this.name === 'tests' && item.code) { try { this.usedIn = this.asList(await this.$api.testProfiles(item.code)); } catch (e) { /* ignore */ } }
    },
    askDelete (item) { this.active = item; this.deleteOpen = true; },
    async doDelete () {
      try { await this.$api.dictDelete(this.name, this.active.id); this.notifyOk('Запис видалено'); this.$store.dispatch('dictionaries/invalidate', this.name); this.load(); } catch (e) { this.notifyError(e); }
    },
    async runImport (dryRun) {
      this.importing = true;
      const fd = new FormData(); fd.append('file', this.importFile); fd.append('dictionary', this.name);
      try { const res = await this.$api.dictImport(fd, dryRun); if (dryRun) this.importPreview = res; else { this.notifyOk('Імпорт виконано'); this.importOpen = false; this.importPreview = null; this.load(); } } catch (e) { this.notifyError(e); } finally { this.importing = false; }
    }
  }
};
</script>
