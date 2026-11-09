<template>
  <div class="sections-page" data-testid="sectionsPage">
    <page-header title="Підрозділи лабораторії" icon="fas fa-sitemap" :breadcrumbs="[{ label: 'Підрозділи лабораторії' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load"><q-tooltip>Оновити</q-tooltip></q-btn>
      <q-btn outline dense color="primary" icon="book" label="Журнал відділення" :to="{ name: 'lab-section-journal' }" />
      <q-btn v-if="isAdmin" unelevated dense color="primary" icon="add" label="Новий підрозділ" data-testid="sectionCreate" @click="openForm(null)" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="id" dense flat :loading="loading" :pagination="{ rowsPerPage: 25 }" rows-per-page-label="Рядків на сторінці:" no-data-label="Підрозділів ще немає" data-testid="sectionsTable">
        <template v-slot:body-cell-sectionType="props"><q-td :props="props">{{ typeLabel(props.row.sectionType) }}</q-td></template>
        <template v-slot:body-cell-journalMask="props"><q-td :props="props"><code class="mono">{{ props.row.journalMask }}</code></q-td></template>
        <template v-slot:body-cell-journalResetPeriod="props"><q-td :props="props">{{ resetLabel(props.row.journalResetPeriod) }}</q-td></template>
        <template v-slot:body-cell-autoReleaseVerified="props"><q-td :props="props"><q-badge :color="props.row.autoReleaseVerified ? 'positive' : 'grey-5'" :label="props.row.autoReleaseVerified ? 'Так' : 'Ні'" /></q-td></template>
        <template v-slot:body-cell-workflowTemplateCode="props"><q-td :props="props">{{ workflowLabel(props.row.workflowTemplateCode) }}</q-td></template>
        <template v-slot:body-cell-department="props"><q-td :props="props">{{ props.row.departmentName || depName(props.row.departmentId) }}</q-td></template>
        <template v-slot:body-cell-isActive="props"><q-td :props="props"><q-badge :color="props.row.isActive === false ? 'grey-5' : 'positive'" :label="props.row.isActive === false ? 'Вимкнено' : 'Працює'" /></q-td></template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right no-wrap">
            <q-btn flat dense round size="sm" icon="book" color="primary" :to="{ name: 'lab-section-journal', query: { sectionId: props.row.id } }"><q-tooltip>Журнал підрозділу</q-tooltip></q-btn>
            <template v-if="isAdmin">
              <q-btn flat dense round size="sm" icon="edit" color="primary" :data-testid="`sectionEdit-${props.row.code}`" @click="openForm(props.row)"><q-tooltip>Редагувати</q-tooltip></q-btn>
              <q-btn v-if="props.row.isActive !== false" flat dense round size="sm" icon="power_settings_new" color="grey-7" @click="askDelete(props.row)"><q-tooltip>Вимкнути</q-tooltip></q-btn>
              <q-btn v-else flat dense round size="sm" icon="power_settings_new" color="positive" @click="enable(props.row)"><q-tooltip>Увімкнути</q-tooltip></q-btn>
            </template>
          </q-td>
        </template>
      </q-table>
    </div>

    <q-dialog v-model="formOpen" persistent @shake="onShake">
      <q-card style="min-width: 720px; max-width: 96vw" data-testid="sectionDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="fas fa-sitemap" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Підрозділ' : 'Новий підрозділ' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section>
          <div class="row q-col-gutter-sm">
            <div class="col-3"><q-input v-model="form.code" outlined dense label="Код *" data-testid="sectionCode" lazy-rules :rules="[codeRule]" /></div>
            <div class="col-5"><q-input v-model="form.name" outlined dense label="Назва *" data-testid="sectionName" /></div>
            <div class="col-4"><q-select v-model="form.sectionType" outlined dense label="Тип" :options="typeOptions" emit-value map-options /></div>
            <div class="col-6"><q-select v-model="form.departmentId" outlined dense clearable label="Відділення" :options="depOptions" emit-value map-options /></div>
            <div class="col-6"><q-select v-model="form.workflowTemplateCode" outlined dense label="Шаблон робочого процесу" :options="workflowOptions" emit-value map-options /></div>
            <div class="col-12"><div class="section-title q-mt-sm">Журнальна нумерація</div></div>
            <div class="col-6">
              <q-select v-model="form.journalMask" outlined dense label="Маска журнального номера *" :options="maskPresets" use-input new-value-mode="add-unique" data-testid="sectionMask" @input="previewMask" />
              <div class="text-caption text-grey-6 q-mt-xs">Приклад: {yy}{MM}{dd}/{dayseq3} дає 261109/001. {yyyy} — рік повністю, {yy} — рік двома цифрами, {MM} — місяць, {dd} — день, {seq6} — наскрізний номер із 6 цифр, {dayseq3} — номер за день із 3 цифр.</div>
            </div>
            <div class="col-3"><q-select v-model="form.journalResetPeriod" outlined dense label="Скидання лічильника" :options="resetOptions" emit-value map-options @input="previewMask" /></div>
            <div class="col-3 row items-center"><q-toggle v-model="form.autoReleaseVerified" label="Автовидача після верифікації" color="positive" /></div>
            <div class="col-12">
              <q-banner dense rounded class="bg-grey-2">
                <div class="row items-center">
                  <div class="col"><div class="text-caption text-grey-7">{{ previewText ? 'Наступні номери' : 'Приклад номера' }}</div><div class="mono text-h6" data-testid="maskPreview">{{ previewText || localPreview }}</div><div v-if="previewNote" class="text-caption text-grey-6">{{ previewNote }}</div></div>
                  <q-btn flat dense color="primary" icon="refresh" label="Оновити" :disable="!form.id" :loading="previewing" @click="previewMask"><q-tooltip v-if="!form.id">Доступно після збереження підрозділу</q-tooltip></q-btn>
                </div>
              </q-banner>
            </div>
          </div>
          <div v-if="formError" class="text-negative q-mt-sm">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <span v-if="!canSave && !saving" class="text-caption text-grey-7 q-mr-sm">Не заповнено: {{ missingFields.join(', ') }}</span>
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" icon="save" label="Зберегти" :loading="saving" :disable="!canSave" data-testid="sectionSave" @click="save" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="deleteOpen" title="Вимкнути підрозділ?" message="Якщо підрозділ уже використовується (є журнальні записи або показники), його лише вимкнуть — усі дані збережуться. Якщо підрозділ ніде не використовувався, його буде видалено без можливості відновлення." ok-label="Підтвердити" color="negative" icon="power_settings_new" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';

const TYPES = [
  { value: 'CLINICAL_BIOCHEM', label: 'Клінічна біохімія' }, { value: 'HEMATOLOGY', label: 'Гематологія' }, { value: 'IMMUNO', label: 'Імунохімія' },
  { value: 'COAG', label: 'Коагулологія' }, { value: 'URINALYSIS', label: 'Аналіз сечі' }, { value: 'MICROBIOLOGY', label: 'Мікробіологія' },
  { value: 'PATHOLOGY', label: 'Патогістологія' }, { value: 'CYTOLOGY', label: 'Цитологія' }, { value: 'GENETICS', label: 'Генетика' }, { value: 'OTHER', label: 'Інше' }
];
const RESET = [{ value: 'NEVER', label: 'Ніколи' }, { value: 'DAY', label: 'Щодня' }, { value: 'MONTH', label: 'Щомісяця' }, { value: 'YEAR', label: 'Щороку' }];
const WORKFLOWS = [
  { value: 'CLINICAL', label: 'Стандартний (забір → прийом → аналіз → верифікація)' }, { value: 'HEMATOLOGY', label: 'Гематологія' }, { value: 'URINALYSIS', label: 'Аналіз сечі' },
  { value: 'MICROBIOLOGY', label: 'Мікробіологія (посів → інкубація → ідентифікація → антибіотикограма)' },
  { value: 'PATHOLOGY', label: 'Патогістологія (фіксація → вирізка → проводка → заливка → мікротомія → фарбування → мікроскопія)' },
  { value: 'CYTOLOGY', label: 'Цитологія (фіксація → фарбування → мікроскопія)' }
];
const emptyForm = () => ({ id: null, code: '', name: '', sectionType: 'OTHER', departmentId: null, journalMask: '{yyyy}-{seq6}', journalResetPeriod: 'YEAR', secondaryMask: null, autoReleaseVerified: true, workflowTemplateCode: 'CLINICAL', isActive: true });

export default {
  name: 'LabSectionsPage',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      rows: [], formOpen: false, deleteOpen: false, form: {}, formSnapshot: '', formError: '', saving: false, previewing: false, previewText: '', previewNote: '', active: null,
      typeOptions: TYPES,
      maskPresets: ['{yyyy}-{seq6}', '{yy}{MM}{dd}/{dayseq3}', 'S{yy}-{seq5}', '{yy}{MM}-{seq5}'],
      resetOptions: RESET,
      workflowOptions: WORKFLOWS,
      columns: [
        { name: 'code', label: 'Код', field: 'code', align: 'left', sortable: true },
        { name: 'name', label: 'Назва', field: 'name', align: 'left', sortable: true },
        { name: 'sectionType', label: 'Тип', field: 'sectionType', align: 'left' },
        { name: 'department', label: 'Відділення', align: 'left' },
        { name: 'journalMask', label: 'Маска журналу', field: 'journalMask', align: 'left' },
        { name: 'journalResetPeriod', label: 'Скидання', field: 'journalResetPeriod', align: 'left' },
        { name: 'autoReleaseVerified', label: 'Автовидача', align: 'center' },
        { name: 'workflowTemplateCode', label: 'Процес', field: 'workflowTemplateCode', align: 'left' },
        { name: 'isActive', label: 'Стан', align: 'center' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    // керування підрозділами — лише адміністратор (серверна політика)
    isAdmin () { return this.canDo(); },
    depOptions () { return this.$store.getters['dictionaries/options']('departments'); },
    missingFields () {
      const m = [];
      if (!this.form.code) m.push('код');
      if (!this.form.name) m.push('назва');
      if (!this.form.journalMask) m.push('маска журналу');
      if (this.form.code && this.codeRule(this.form.code) !== true) m.push('унікальний код');
      return m;
    },
    canSave () { return !this.missingFields.length; },
    localPreview () {
      const d = new Date(); const pad = n => String(n).padStart(2, '0');
      return (this.form.journalMask || '').replace('{yyyy}', d.getFullYear()).replace('{yy}', String(d.getFullYear()).slice(2)).replace('{MM}', pad(d.getMonth() + 1)).replace('{dd}', pad(d.getDate())).replace(/\{seq(\d*)\}/, (m, n) => '1'.padStart(Number(n || 1), '0')).replace(/\{dayseq(\d*)\}/, (m, n) => '1'.padStart(Number(n || 1), '0'));
    }
  },
  created () { this.$store.dispatch('dictionaries/load', 'departments'); this.load(); },
  methods: {
    typeLabel (v) { const o = TYPES.find(x => x.value === v); return o ? o.label : (v || '—'); },
    resetLabel (v) { const o = RESET.find(x => x.value === v); return o ? o.label : (v || '—'); },
    workflowLabel (v) { const o = WORKFLOWS.find(x => x.value === v); return o ? o.label.replace(/ \(.*\)$/, '') : (v || '—'); },
    depName (id) { const d = this.$store.getters['dictionaries/byId']('departments', id); return d ? d.name : (id ? '—' : '—'); },
    codeRule (v) {
      const code = String(v || '').trim().toUpperCase();
      if (!code) return true;
      return !this.rows.some(r => String(r.code).toUpperCase() === code && r.id !== this.form.id) || 'Підрозділ із таким кодом уже є';
    },
    async load () { const res = await this.callApi(() => this.$api.sections()); if (res !== undefined) this.rows = this.asList(res); },
    openForm (s) {
      const base = emptyForm();
      if (s) Object.keys(base).forEach(k => { if (s[k] !== undefined && s[k] !== null) base[k] = s[k]; });
      this.form = base;
      this.formSnapshot = JSON.stringify(this.form);
      this.previewText = ''; this.previewNote = ''; this.formError = ''; this.formOpen = true;
      if (s) this.previewMask();
    },
    onShake () {
      // Esc / клік поза вікном: форму без змін закриваємо одразу
      if (JSON.stringify(this.form) === this.formSnapshot) this.formOpen = false;
    },
    async previewMask () {
      if (!this.form.id) { this.previewText = ''; return; }
      this.previewing = true;
      try {
        const res = await this.$api.sectionRenumberPreview(this.form.id, { mask: this.form.journalMask, resetPeriod: this.form.journalResetPeriod });
        const list = (res && Array.isArray(res.preview)) ? res.preview : [];
        this.previewText = list.length ? list.slice(0, 3).map(p => p.journalNumber).join(', ') + ' …' : '';
        this.previewNote = '';
      } catch (e) { this.previewText = ''; this.previewNote = e.userMessage || ''; } finally { this.previewing = false; }
    },
    bodyOf (s, isActive) {
      return { code: s.code, name: s.name, sectionType: s.sectionType, departmentId: s.departmentId || null, journalMask: s.journalMask, journalResetPeriod: s.journalResetPeriod, secondaryMask: s.secondaryMask || null, autoReleaseVerified: !!s.autoReleaseVerified, workflowTemplateCode: s.workflowTemplateCode, isActive };
    },
    async save () {
      this.saving = true; this.formError = '';
      const f = this.form;
      try {
        const body = this.bodyOf(f, f.isActive !== false);
        if (f.id) await this.$api.updateSection(f.id, body); else await this.$api.createSection(body);
        this.notifyOk('Підрозділ збережено'); this.formOpen = false; this.load();
      } catch (e) { this.formError = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    },
    askDelete (s) { this.active = s; this.deleteOpen = true; },
    async doDelete () {
      try {
        const res = await this.$api.deleteSection(this.active.id);
        this.notifyOk(res && res.deactivated ? 'Підрозділ вимкнено' : 'Підрозділ видалено');
        this.load();
      } catch (e) { this.notifyError(e); }
    },
    async enable (s) {
      try { await this.$api.updateSection(s.id, this.bodyOf(s, true)); this.notifyOk('Підрозділ увімкнено'); this.load(); } catch (e) { this.notifyError(e); }
    }
  }
};
</script>
