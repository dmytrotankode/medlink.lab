<template>
  <div class="sections-page" data-testid="sectionsPage">
    <page-header title="Підрозділи лабораторії" icon="fas fa-sitemap" subtitle="Секції (біохімія, гематологія, патогістологія…): маска журнального номера, період скидання, автовидача, шаблон робочого процесу" :breadcrumbs="[{ label: 'Підрозділи лабораторії' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn outline dense color="primary" icon="book" label="Журнал відділення" :to="{ name: 'lab-section-journal' }" />
      <q-btn unelevated dense color="primary" icon="add" label="Новий підрозділ" data-testid="sectionCreate" @click="openForm(null)" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="id" dense flat :loading="loading" :pagination="{ rowsPerPage: 25 }" no-data-label="Підрозділів ще немає" data-testid="sectionsTable">
        <template v-slot:body-cell-journalMask="props"><q-td :props="props"><code class="mono">{{ props.row.journalMask }}</code></q-td></template>
        <template v-slot:body-cell-resetPeriod="props"><q-td :props="props">{{ resetLabel(props.row.resetPeriod) }}</q-td></template>
        <template v-slot:body-cell-autoRelease="props"><q-td :props="props"><q-icon :name="props.row.autoRelease ? 'check_circle' : 'radio_button_unchecked'" :color="props.row.autoRelease ? 'positive' : 'grey-5'" /></q-td></template>
        <template v-slot:body-cell-department="props"><q-td :props="props">{{ depName(props.row.departmentId) }}</q-td></template>
        <template v-slot:body-cell-isActive="props"><q-td :props="props"><q-icon :name="props.row.isActive === false ? 'cancel' : 'check_circle'" :color="props.row.isActive === false ? 'grey-5' : 'positive'" /></q-td></template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right no-wrap">
            <q-btn flat dense round size="sm" icon="book" color="primary" :to="{ name: 'lab-section-journal', query: { sectionId: props.row.id } }"><q-tooltip>Журнал</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="edit" color="primary" :data-testid="`sectionEdit-${props.row.code}`" @click="openForm(props.row)" />
            <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDelete(props.row)" />
          </q-td>
        </template>
      </q-table>
    </div>

    <q-dialog v-model="formOpen" persistent>
      <q-card style="min-width: 720px; max-width: 96vw" data-testid="sectionDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="fas fa-sitemap" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Підрозділ' : 'Новий підрозділ' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section>
          <div class="row q-col-gutter-sm">
            <div class="col-3"><q-input v-model="form.code" outlined dense label="Код *" data-testid="sectionCode" /></div>
            <div class="col-5"><q-input v-model="form.name" outlined dense label="Назва *" data-testid="sectionName" /></div>
            <div class="col-4"><q-select v-model="form.type" outlined dense label="Тип" :options="types" use-input new-value-mode="add-unique" /></div>
            <div class="col-6"><q-select v-model="form.departmentId" outlined dense clearable label="Відділення (org_department)" :options="depOptions" emit-value map-options /></div>
            <div class="col-6"><q-select v-model="form.workflowTemplate" outlined dense label="Шаблон робочого процесу" :options="workflowOptions" emit-value map-options /></div>
            <div class="col-12"><div class="section-title q-mt-sm">Журнальна нумерація</div></div>
            <div class="col-6">
              <q-select v-model="form.journalMask" outlined dense label="Маска журнального номера *" :options="maskPresets" use-input new-value-mode="add-unique" data-testid="sectionMask" @input="previewMask" />
              <div class="text-caption text-grey-6 q-mt-xs">Токени: {yyyy} {yy} {MM} {dd} {seq6} {seq5} {dayseq3} — послідовність і денний лічильник</div>
            </div>
            <div class="col-3"><q-select v-model="form.resetPeriod" outlined dense label="Скидання лічильника" :options="resetOptions" emit-value map-options @input="previewMask" /></div>
            <div class="col-3 row items-center"><q-toggle v-model="form.autoRelease" label="Автовидача після верифікації" color="positive" /></div>
            <div class="col-12">
              <q-banner dense rounded class="bg-grey-2">
                <div class="row items-center">
                  <div class="col"><div class="text-caption text-grey-7">Попередній перегляд номера</div><div class="mono text-h6" data-testid="maskPreview">{{ preview || localPreview }}</div><div v-if="previewNote" class="text-caption text-grey-6">{{ previewNote }}</div></div>
                  <q-btn flat dense color="primary" icon="refresh" label="Оновити з сервера" :disable="!form.id" :loading="previewing" @click="previewMask" />
                </div>
              </q-banner>
            </div>
          </div>
          <div v-if="formError" class="text-negative q-mt-sm">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" :disable="!form.code || !form.name || !form.journalMask" data-testid="sectionSave" @click="save" /></q-card-actions>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="deleteOpen" title="Видалити підрозділ" message="Підрозділ буде деактивовано; журнальні записи збережуться." ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import { LAB_SECTIONS } from '../../../utils/statuses';

export default {
  name: 'LabSectionsPage',
  mixins: [apiMixin],
  data () {
    return {
      rows: [], formOpen: false, deleteOpen: false, form: {}, formError: '', saving: false, previewing: false, preview: '', previewNote: '', active: null,
      types: LAB_SECTIONS,
      maskPresets: ['{yyyy}-{seq6}', '{yy}{MM}{dd}/{dayseq3}', 'S{yy}-{seq5}', '{yy}{MM}-{seq5}'],
      resetOptions: [{ value: 'NEVER', label: 'Ніколи' }, { value: 'DAILY', label: 'Щодня' }, { value: 'MONTHLY', label: 'Щомісяця' }, { value: 'YEARLY', label: 'Щороку' }],
      workflowOptions: [{ value: 'STANDARD', label: 'Стандарт (забір → прийом → аналіз → верифікація)' }, { value: 'ALIQUOT', label: 'З аліквотуванням (центрифугування → аліквоти)' }, { value: 'HISTOLOGY', label: 'Патогістологія (фіксація → вирізка → проводка → заливка → мікротомія → фарбування → мікроскопія)' }, { value: 'CYTOLOGY', label: 'Цитологія (фіксація → фарбування → мікроскопія)' }, { value: 'MICROBIOLOGY', label: 'Мікробіологія (посів → інкубація → ідентифікація → антибіотикограма)' }],
      columns: [
        { name: 'code', label: 'Код', field: 'code', align: 'left', sortable: true },
        { name: 'name', label: 'Назва', field: 'name', align: 'left', sortable: true },
        { name: 'type', label: 'Тип', field: 'type', align: 'left' },
        { name: 'department', label: 'Відділення', align: 'left' },
        { name: 'journalMask', label: 'Маска журналу', field: 'journalMask', align: 'left' },
        { name: 'resetPeriod', label: 'Скидання', field: 'resetPeriod', align: 'left' },
        { name: 'autoRelease', label: 'Автовидача', align: 'center' },
        { name: 'workflowTemplate', label: 'Процес', field: 'workflowTemplate', align: 'left' },
        { name: 'isActive', label: 'Акт.', align: 'center' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    depOptions () { return this.$store.getters['dictionaries/options']('departments'); },
    localPreview () {
      const d = new Date(); const pad = n => String(n).padStart(2, '0');
      return (this.form.journalMask || '').replace('{yyyy}', d.getFullYear()).replace('{yy}', String(d.getFullYear()).slice(2)).replace('{MM}', pad(d.getMonth() + 1)).replace('{dd}', pad(d.getDate())).replace(/\{seq(\d)\}/, (m, n) => '1'.padStart(Number(n), '0')).replace(/\{dayseq(\d)\}/, (m, n) => '1'.padStart(Number(n), '0'));
    }
  },
  created () { this.$store.dispatch('dictionaries/load', 'departments'); this.load(); },
  methods: {
    resetLabel (v) { const o = this.resetOptions.find(x => x.value === v); return o ? o.label : (v || '—'); },
    depName (id) { const d = this.$store.getters['dictionaries/byId']('departments', id); return d ? d.name : (id || '—'); },
    async load () { const res = await this.callApi(() => this.$api.sections()); if (res !== undefined) this.rows = this.asList(res); },
    openForm (s) {
      this.form = s ? { ...s } : { id: null, code: '', name: '', type: null, departmentId: null, journalMask: '{yyyy}-{seq6}', resetPeriod: 'YEARLY', autoRelease: false, workflowTemplate: 'STANDARD', isActive: true };
      this.preview = ''; this.previewNote = ''; this.formError = ''; this.formOpen = true;
      if (s) this.previewMask();
    },
    async previewMask () {
      if (!this.form.id) { this.preview = ''; return; }
      this.previewing = true;
      try { const res = await this.$api.sectionRenumberPreview(this.form.id, { journalMask: this.form.journalMask, resetPeriod: this.form.resetPeriod }); this.preview = (res && (res.preview || res.nextNumber || res.sample)) || ''; this.previewNote = res && res.note ? res.note : (res && res.affected !== undefined ? `Записів до перенумерації: ${res.affected}` : ''); } catch (e) { this.preview = ''; this.previewNote = e.userMessage || ''; } finally { this.previewing = false; }
    },
    async save () {
      this.saving = true; this.formError = '';
      const body = { ...this.form }; delete body.id;
      try { if (this.form.id) await this.$api.updateSection(this.form.id, body); else await this.$api.createSection(body); this.notifyOk('Підрозділ збережено'); this.formOpen = false; this.load(); } catch (e) { this.formError = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    },
    askDelete (s) { this.active = s; this.deleteOpen = true; },
    async doDelete () { try { await this.$api.deleteSection(this.active.id); this.notifyOk('Підрозділ видалено'); this.load(); } catch (e) { this.notifyError(e); } }
  }
};
</script>
