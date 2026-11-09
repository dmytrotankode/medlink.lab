<template>
  <div class="dictionary-crud" :data-testid="`dict-${name}`">
    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-4"><q-input v-model="search" dense outlined clearable placeholder="Пошук…" data-testid="dictSearch"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
      <div class="col-6 col-md-3"><q-toggle v-model="onlyActive" label="Тільки активні" color="primary"><q-tooltip>Лише активні записи; вимкніть, щоб побачити й неактивні</q-tooltip></q-toggle></div>
      <div class="col-6 col-md-5 row justify-end q-gutter-xs">
        <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
        <q-btn-dropdown outline dense color="grey-8" icon="download" label="Експорт" no-caps data-testid="dictExport">
          <q-list dense>
            <q-item clickable v-close-popup data-testid="dictExportCsv" @click="doExport('csv')"><q-item-section>Таблиця CSV (Excel)</q-item-section></q-item>
            <q-item clickable v-close-popup data-testid="dictExportJson" @click="doExport('json')"><q-item-section>Файл JSON</q-item-section></q-item>
          </q-list>
        </q-btn-dropdown>
        <span v-if="allowImport">
          <q-btn outline dense color="grey-8" icon="upload_file" label="Імпорт" :disable="!canImport" data-testid="dictImport" @click="openImport"><q-tooltip v-if="!canImport">{{ roleHint() }}</q-tooltip></q-btn>
        </span>
        <span>
          <q-btn unelevated dense color="primary" icon="add" label="Створити" :disable="!canWrite" data-testid="dictCreate" @click="openForm(null)"><q-tooltip v-if="!canWrite">{{ readOnlyHint }}</q-tooltip></q-btn>
        </span>
      </div>
    </div>

    <q-banner v-if="!canWrite" dense rounded class="bg-blue-1 text-grey-9 q-mb-sm" data-testid="dictReadOnly">
      <template v-slot:avatar><q-icon name="visibility" color="primary" /></template>
      Режим перегляду: роль «{{ roleLabel(currentLabRole) }}» може переглядати та експортувати довідники, але не змінювати їх. Зміни вносять лікар-лаборант або адміністратор лабораторії.
    </q-banner>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <q-banner v-if="loaded && onlyActive && !filtered.length && inactiveCount" dense rounded class="bg-orange-1 text-grey-9 q-mb-sm" data-testid="dictInactiveHint">
      <template v-slot:avatar><q-icon name="info" color="orange-8" /></template>
      Активних записів немає, але є неактивні ({{ inactiveCount }}).
      <template v-slot:action><q-btn flat dense no-caps color="primary" label="Показати неактивні" data-testid="dictShowInactive" @click="onlyActive = false" /></template>
    </q-banner>

    <div class="medlink-card">
      <q-table :data="filtered" :columns="columns" row-key="id" dense flat :loading="loading" :pagination.sync="pagination" :rows-per-page-options="[25, 50, 100, 0]" :no-data-label="loaded ? 'Записів немає' : 'Завантаження…'" data-testid="dictTable" @row-click="(e, row) => openView(row)" class="cursor-pointer">
        <template v-slot:body-cell="props">
          <q-td :props="props">
            <template v-if="props.col.name === 'colorCode' && !props.value">—</template>
            <template v-else-if="props.col.name === 'colorCode'"><div :style="{ background: props.value || '#ccc', width: '18px', height: '18px', borderRadius: '50%', border: '1px solid #999' }" :title="props.value" /></template>
            <template v-else-if="props.col.name === 'exchType'"><q-badge color="teal-7" :label="props.value" /></template>
            <template v-else-if="props.col.name === 'labRole'">{{ roleLabel(props.value) }}</template>
            <template v-else-if="props.col.name === 'category' && name === 'analyzer-types'">{{ categoryLabel(props.value) }}</template>
            <template v-else-if="props.col.name === 'category' && name === 'tests'">{{ testCategoryLabel(props.value) }}</template>
            <template v-else-if="typeof props.value === 'boolean'"><q-icon :name="props.value ? 'check_circle' : 'radio_button_unchecked'" :color="props.value ? 'positive' : 'grey-5'" /></template>
            <template v-else-if="props.col.name === 'testCode' && name === 'unit-conversions'">{{ testWithName(props.value) }}</template>
            <template v-else-if="name === 'unit-conversions' && (props.col.name === 'factor' || props.col.name === 'offset')">{{ props.value === null || props.value === undefined ? '—' : String(props.value).replace('.', ',') }}</template>
            <template v-else-if="props.col.name.endsWith('Id')">{{ refLabel(props.col.name, props.value) }}</template>
            <template v-else-if="props.col.name === 'itemsCount'">{{ (props.row.items || []).length }}</template>
            <template v-else-if="props.col.name === 'day'">{{ dayLabel(props.value) }}</template>
            <template v-else-if="selectField(props.col.name)">{{ displayValue(selectField(props.col.name), props.value) }}</template>
            <template v-else>{{ props.value === null || props.value === undefined ? '—' : props.value }}</template>
          </q-td>
        </template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right no-wrap" @click.stop>
            <q-btn flat dense round size="sm" icon="visibility" color="grey-8" @click="openView(props.row)"><q-tooltip>Картка</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="edit" color="primary" :disable="!canWrite" :data-testid="`edit-${props.row.code || props.row.id}`" @click="openForm(props.row)"><q-tooltip>{{ canWrite ? 'Редагувати' : readOnlyHint }}</q-tooltip></q-btn>
            <q-btn v-if="props.row.isActive === false" flat dense round size="sm" icon="restore_from_trash" color="positive" :disable="!canWrite" :data-testid="`restore-${props.row.code || props.row.id}`" @click="restore(props.row)"><q-tooltip>{{ canWrite ? 'Повернути з архіву' : readOnlyHint }}</q-tooltip></q-btn>
            <q-btn v-else flat dense round size="sm" icon="delete" color="grey-7" :disable="!canDelete" :data-testid="`delete-${props.row.code || props.row.id}`" @click="askDelete(props.row)"><q-tooltip>{{ canDelete ? 'В архів (зробити неактивним)' : roleHint() }}</q-tooltip></q-btn>
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
      <q-card style="min-width: 640px; max-width: 96vw" data-testid="dictImportDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">Імпорт довідника «{{ schema.title }}»</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <div class="text-caption text-grey-8">
            Файл CSV (роздільник «;» або «,», перший рядок — назви колонок) або JSON-масив, до {{ importLimit }} рядків.
            Записи, що вже є в довіднику, оновлюються; порожні клітинки не змінюють значення. Зразок файлу — кнопка «Експорт».
          </div>
          <q-file v-model="importFile" outlined dense label="Файл CSV або JSON" accept=".json,.csv,.txt" data-testid="dictImportFile" @input="importPreview = null"><template v-slot:prepend><q-icon name="attach_file" /></template></q-file>
          <div class="row q-gutter-sm">
            <q-btn outline color="primary" label="Попередній перегляд" :disable="!importFile" :loading="importing" data-testid="dictImportPreview" @click="runImport(true)" />
            <q-btn color="primary" label="Імпортувати" :disable="!importFile || !importPreview || !importPreview.dryRun || (importPreview.create + importPreview.update) === 0" :loading="importing" data-testid="dictImportRun" @click="runImport(false)" />
          </div>
          <div v-if="importPreview" data-testid="dictImportReport">
            <div class="row q-gutter-xs q-mb-xs">
              <q-badge color="positive" :label="`Буде створено: ${importPreview.create}`" />
              <q-badge color="primary" :label="`Буде оновлено: ${importPreview.update}`" />
              <q-badge color="grey-7" :label="`Пропущено: ${importPreview.skip}`" />
              <q-badge :color="importPreview.error ? 'negative' : 'grey-5'" :label="`З помилками: ${importPreview.error}`" />
            </div>
            <div v-if="importPreview.error" class="text-caption text-negative q-mb-xs">Рядки з помилками не будуть імпортовані; решту можна завантажити.</div>
            <q-markup-table dense flat bordered style="max-height: 280px; overflow: auto">
              <thead><tr><th class="text-left">Рядок</th><th class="text-left">Запис</th><th class="text-left">Дія</th><th class="text-left">Пояснення</th></tr></thead>
              <tbody>
                <tr v-for="(r, i) in importPreview.rows" :key="i" v-show="importShowAll || r.action !== 'SKIP' || i < 50">
                  <td>{{ r.line }}</td><td>{{ r.key || '—' }}</td>
                  <td><q-badge :color="actionColor(r.action)" :label="actionLabel(r.action)" /></td>
                  <td class="text-caption">{{ r.reason || '' }}</td>
                </tr>
              </tbody>
            </q-markup-table>
          </div>
        </q-card-section>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="deleteOpen" title="Видалити запис?" :message="`Видалити запис «${active && (active.name || active.fullName || active.code)}»? Якщо він уже використовується, його буде переведено в архів (зникне зі списків вибору, історія збережеться), інакше — видалено остаточно.`" ok-label="Видалити" color="negative" icon="visibility_off" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import roleMixin from '../../mixins/roleMixin';
import DictionaryFormDialog from './DictionaryFormDialog.vue';
import { DICTIONARIES, columnLabel } from './dictionarySchemas';
import { tubeTitle } from '../../utils/format';
import { ROLE, ANALYZER_CATEGORIES } from '../../utils/statuses';

export default {
  name: 'DictionaryCrudTable',
  mixins: [apiMixin, roleMixin],
  components: { DictionaryFormDialog },
  props: { name: { type: String, required: true }, allowImport: { type: Boolean, default: true } },
  data () {
    return { loaded: false, items: [], search: '', onlyActive: true, formOpen: false, viewOpen: false, deleteOpen: false, importOpen: false, active: null, usedIn: [], importFile: null, importPreview: null, importing: false, importShowAll: false, importLimit: 5000, pagination: { rowsPerPage: 25, sortBy: 'code' } };
  },
  computed: {
    /** Створення/правка: лікар-лаборант і адміністратор (серверна політика). Лаборант та інші ролі — лише перегляд. */
    canWrite () { return this.canDo('LAB_DOCTOR'); },
    /** Архів/видалення й імпорт: лише адміністратор лабораторії. */
    canDelete () { return this.canDo(); },
    canImport () { return this.canDo(); },
    readOnlyHint () { return this.roleHint('LAB_DOCTOR'); },
    schema () { return DICTIONARIES[this.name] || { title: this.name, fields: [], columns: ['code', 'name'], icon: 'list' }; },
    columns () {
      return [...this.schema.columns.map(c => ({ name: c, label: columnLabel(this.schema, c), field: c === 'itemsCount' ? r => (r.items || []).length : c, align: 'left', sortable: true })), { name: 'actions', label: '', align: 'right' }];
    },
    inactiveCount () { return this.items.filter(i => i.isActive === false).length; },
    filtered () {
      const s = (this.search || '').toLowerCase();
      return this.items.filter(i => (!this.onlyActive || i.isActive !== false) && (!s || JSON.stringify(i).toLowerCase().includes(s)));
    }
  },
  watch: { name () { this.loaded = false; this.items = []; this.load(); } },
  created () { this.load(); },
  methods: {
    roleLabel (r) { return ROLE[r] || r || '—'; },
    /** Категорія показника: службові коди з каталогу → українські назви; невідому показуємо як є (вільний текст) */
    testCategoryLabel (c) {
      if (c === null || c === undefined || c === '') return '—';
      return { BIOCHEM: 'Біохімія', URINE: 'Сеча', HEMATOLOGY: 'Гематологія', COAG: 'Коагулологія', HORMONES: 'Гормони', IMMUNO: 'Імунологія', MICRO: 'Мікробіологія', GENERAL: 'Загальноклінічні', PCR: 'ПЛР' }[c] || c;
    },
    /** Показник у таблицях з кодом-ключем: «Альбумін (ALB)»; якщо назви немає в довіднику — лише код */
    testWithName (code) {
      if (!code) return '—';
      const t = this.$store.getters['dictionaries/items']('tests').find(x => x.code === code);
      return t ? `${t.name} (${code})` : code;
    },
    categoryLabel (c) { const x = ANALYZER_CATEGORIES.find(a => a.value === c); return x ? x.label : c; },
    refLabel (col, id) {
      if (id === null || id === undefined) return '—';
      const map = { biomaterialTypeId: 'biomaterials', departmentId: 'departments', methodId: 'method-types', tubeTypeId: 'tube-types', antibioticId: 'antibiotics', organismId: 'organisms', testId: 'tests', analyzerTypeId: 'analyzer-types', fromUnitId: 'units', toUnitId: 'units' };
      const d = map[col]; if (!d) return id;
      const it = this.$store.getters['dictionaries/byId'](d, id);
      const label = it ? (it.name || it.fullName || it.latinName || it.code) : id;
      return d === 'tube-types' && it ? tubeTitle(label) : label;
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
      this.loaded = true;
      const refs = { biomaterialTypeId: 'biomaterials', departmentId: 'departments', methodId: 'method-types', tubeTypeId: 'tube-types', antibioticId: 'antibiotics', organismId: 'organisms', testId: 'tests', fromUnitId: 'units', toUnitId: 'units' };
      const needed = new Set();
      this.schema.columns.forEach(c => { if (refs[c]) needed.add(refs[c]); });
      if (this.name === 'profiles' || this.name === 'unit-conversions') needed.add('tests');
      if (needed.size) this.$store.dispatch('dictionaries/loadMany', [...needed]);
    },
    openForm (item) { this.active = item; this.formOpen = true; },
    async openView (item) {
      this.active = item; this.viewOpen = true; this.usedIn = [];
      if (this.name === 'tests' && item.code) { try { this.usedIn = this.asList(await this.$api.testProfiles(item.code)); } catch (e) { /* ignore */ } }
    },
    askDelete (item) { this.active = item; this.deleteOpen = true; },
    async doDelete () {
      try {
        const res = await this.$api.dictDelete(this.name, this.active.id);
        this.notifyOk((res && res.message) || (res && res.deleted ? 'Запис видалено' : 'Запис зроблено неактивним'));
        this.$store.dispatch('dictionaries/invalidate', this.name); this.load();
      } catch (e) {
        if (e && e.apiStatus === 409) this.$q.notify({ type: 'negative', message: (e.userMessage || 'Запис використовується й не може бути видалений'), timeout: 4000 });
        else this.notifyError(e);
      }
    },
    async restore (item) {
      try { await this.$api.dictUpdate(this.name, item.id, { isActive: true }); this.notifyOk('Запис повернуто з архіву'); this.$store.dispatch('dictionaries/invalidate', this.name); this.load(); } catch (e) { this.notifyError(e); }
    },
    dayLabel (v) { if (!v) return '—'; const d = String(v).slice(0, 10).split('-'); return d.length === 3 ? `${d[2]}.${d[1]}.${d[0]}` : v; },
    actionLabel (a) { return { CREATE: 'Створити', UPDATE: 'Оновити', SKIP: 'Пропустити', ERROR: 'Помилка' }[a] || a; },
    actionColor (a) { return { CREATE: 'positive', UPDATE: 'primary', SKIP: 'grey-6', ERROR: 'negative' }[a] || 'grey'; },
    openImport () { this.importFile = null; this.importPreview = null; this.importOpen = true; },
    async doExport (format) {
      try {
        const blob = await this.$api.dictExport(this.name, format);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url; a.download = `${this.name}.${format}`; document.body.appendChild(a); a.click(); document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      } catch (e) { this.notifyError(e); }
    },
    async runImport (dryRun) {
      this.importing = true;
      try {
        const text = await this.readFile(this.importFile);
        const res = await this.$api.dictImportText(this.name, text, dryRun);
        if (dryRun) this.importPreview = res;
        else {
          this.notifyOk(`Імпорт виконано: створено ${res.created}, оновлено ${res.updated}, пропущено ${res.skipped}, з помилками ${res.errors}`);
          this.importOpen = false; this.importPreview = null; this.$store.dispatch('dictionaries/invalidate', this.name); this.load();
        }
      } catch (e) { this.notifyError(e); } finally { this.importing = false; }
    },
    readFile (file) {
      return new Promise((resolve, reject) => {
        const r = new FileReader();
        r.onload = () => resolve(String(r.result || ''));
        r.onerror = () => reject(new Error('Не вдалося прочитати файл'));
        r.readAsText(file, 'utf-8');
      });
    }
  }
};
</script>
