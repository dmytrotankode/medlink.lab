<template>
  <div class="micro-page" data-testid="microPage">
    <page-header title="Мікробіологія" icon="fas fa-bacterium" subtitle="Посіви, виділені мікроорганізми та їх чутливість до антибіотиків" :breadcrumbs="[{ label: 'Мікробіологія' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <span class="inline-block">
        <q-btn unelevated dense color="primary" icon="add" label="Новий посів" :disable="!canWork" data-testid="cultureCreate" @click="openDialog(null)" />
        <q-tooltip v-if="!canWork">{{ workHint }}</q-tooltip>
      </span>
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-4"><q-input v-model="search" dense outlined clearable placeholder="Пацієнт, замовлення, локалізація, мікроорганізм"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
      <div class="col-6 col-md-3"><q-select v-model="status" dense outlined clearable label="Статус" :options="statusOptions" emit-value map-options @input="load" /></div>
    </div>

    <div class="medlink-card">
      <q-table :data="filtered" :columns="columns" row-key="id" dense flat :loading="loading" :pagination="{ rowsPerPage: 25 }" no-data-label="Посівів немає" data-testid="culturesTable" @row-click="(e, row) => open(row)" class="cursor-pointer">
        <template v-slot:body-cell-status="props"><q-td :props="props"><status-chip :value="props.row.status" type="culture" /></q-td></template>
        <template v-slot:body-cell-growth="props"><q-td :props="props"><q-badge v-if="hasGrowth(props.row)" color="deep-orange-6" label="є ріст" /><q-badge v-else-if="props.row.growthDetected === false" color="positive" label="росту немає" /><span v-else class="text-grey-5">—</span></q-td></template>
        <template v-slot:body-cell-isolates="props"><q-td :props="props"><q-chip v-for="iso in (props.row.isolates || [])" :key="iso.id" dense size="sm" color="purple-1" text-color="purple-9">{{ iso.organismName || iso.organismLatin }}<q-badge v-for="ph in (iso.phenotypes || [])" :key="ph" color="negative" :label="ph" class="q-ml-xs" /></q-chip><span v-if="!(props.row.isolates || []).length" class="text-grey-5">—</span></q-td></template>
        <template v-slot:body-cell-createdOn="props"><q-td :props="props">{{ props.row.createdOn | datetime }}</q-td></template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right no-wrap" @click.stop>
            <q-btn flat dense round size="sm" icon="open_in_new" color="primary" aria-label="Відкрити посів" @click="open(props.row)"><q-tooltip>Відкрити посів</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="edit" color="primary" aria-label="Редагувати посів" :disable="!can(props.row, 'EDIT')" @click="openDialog(props.row)"><q-tooltip>{{ can(props.row, 'EDIT') ? 'Редагувати посів' : actionHint('EDIT') }}</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Видалити посів" :disable="!can(props.row, 'DELETE')" @click="askDelete(props.row)"><q-tooltip>{{ can(props.row, 'DELETE') ? 'Видалити посів' : actionHint('DELETE') }}</q-tooltip></q-btn>
          </q-td>
        </template>
      </q-table>
    </div>

    <culture-dialog v-model="dialogOpen" :culture="active" @saved="onSaved" />
    <confirm-dialog v-model="deleteOpen" title="Видалити посів?" message="Посів разом з ізолятами та антибіотикограмою буде видалено." ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';
import CultureDialog from './CultureDialog.vue';
import { CULTURE_STATUS } from '../../../utils/statuses';

// Статуси, які реально існують у життєвому циклі посіву
const STATUSES = ['INCUBATING', 'PRELIMINARY', 'ISOLATED', 'COMPLETED'];

export default {
  name: 'MicrobiologyCultures',
  mixins: [apiMixin, roleMixin],
  components: { CultureDialog },
  data () {
    return {
      rows: [], search: '', status: null, statusOptions: STATUSES.map(s => ({ value: s, label: CULTURE_STATUS[s].label })),
      dialogOpen: false, deleteOpen: false, active: null,
      columns: [
        { name: 'createdOn', label: 'Зареєстровано', align: 'left', field: 'createdOn', sortable: true },
        { name: 'patient', label: 'Пацієнт', field: r => r.patientName || '—', align: 'left', sortable: true },
        { name: 'order', label: 'Замовлення', field: r => r.orderNumber || '—', align: 'left' },
        { name: 'specimen', label: 'Локалізація / біоматеріал', field: r => r.specimenLocus || '—', align: 'left' },
        { name: 'growth', label: 'Ріст', align: 'center' },
        { name: 'isolates', label: 'Мікроорганізми / фенотипи', align: 'left' },
        { name: 'status', label: 'Статус', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    canWork () { return this.canDo('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    workHint () { return this.roleHint('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    // Сервер фільтрує лише за статусом; текстовий пошук виконуємо тут
    filtered () {
      const n = (this.search || '').trim().toLowerCase();
      if (!n) return this.rows;
      return this.rows.filter(r => [r.patientName, r.orderNumber, r.specimenLocus, ...(r.isolates || []).map(i => `${i.organismName || ''} ${i.organismLatin || ''}`)].join(' ').toLowerCase().includes(n));
    }
  },
  created () { this.load(); },
  methods: {
    hasGrowth (r) { return r.growthDetected === true || (r.isolates || []).length > 0; },
    can (row, action) { return (row.allowedActions || []).some(a => a.action === action); },
    actionHint (action) {
      if (!this.canWork) return this.workHint;
      return action === 'DELETE' ? 'Видалити можна лише посів, що перебуває на інкубації' : 'Посів завершено — зміни недоступні';
    },
    async load () { const res = await this.callApi(() => this.$api.cultures({ status: this.status })); if (res !== undefined) this.rows = this.asList(res); },
    open (row) { this.$router.push({ name: 'lab-culture-card', params: { id: row.id } }); },
    openDialog (c) { this.active = c; this.dialogOpen = true; },
    onSaved (res) { this.load(); if (res && res.id && !(this.active && this.active.id)) this.$router.push({ name: 'lab-culture-card', params: { id: res.id } }); },
    askDelete (c) { this.active = c; this.deleteOpen = true; },
    async doDelete () { try { await this.$api.deleteCulture(this.active.id); this.notifyOk('Посів видалено'); this.load(); } catch (e) { this.notifyError(e); } }
  }
};
</script>
