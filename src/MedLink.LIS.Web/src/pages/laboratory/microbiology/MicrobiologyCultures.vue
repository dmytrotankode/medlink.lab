<template>
  <div class="micro-page" data-testid="microPage">
    <page-header title="Мікробіологія" icon="fas fa-bacterium" subtitle="Посіви, ідентифікація мікроорганізмів, антибіотикограма S/I/R за EUCAST, фенотипи резистентності (MRSA, ESBL, CRE)" :breadcrumbs="[{ label: 'Мікробіологія' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn unelevated dense color="primary" icon="add" label="Новий посів" data-testid="cultureCreate" @click="openDialog(null)" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-4"><q-input v-model="search" dense outlined clearable placeholder="Пацієнт, штрихкод, локус, мікроорганізм" @keyup.enter="load" @clear="load"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
      <div class="col-6 col-md-3"><q-select v-model="status" dense outlined clearable label="Статус" :options="statusOptions" emit-value map-options @input="load" /></div>
    </div>

    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="id" dense flat :loading="loading" :pagination="{ rowsPerPage: 25 }" no-data-label="Посівів немає" data-testid="culturesTable" @row-click="(e, row) => open(row)" class="cursor-pointer">
        <template v-slot:body-cell-status="props"><q-td :props="props"><status-chip :value="props.row.status" type="culture" /></q-td></template>
        <template v-slot:body-cell-growth="props"><q-td :props="props"><q-badge v-if="props.row.hasGrowth" color="deep-orange-6" label="ріст" /><q-badge v-else-if="props.row.status === 'NO_GROWTH'" color="positive" label="немає" /><span v-else class="text-grey-5">—</span></q-td></template>
        <template v-slot:body-cell-isolates="props"><q-td :props="props"><q-chip v-for="iso in (props.row.isolates || [])" :key="iso.id" dense size="sm" color="purple-1" text-color="purple-9">{{ iso.organismName || iso.organism }}<q-badge v-for="ph in (iso.phenotypes || [])" :key="ph" color="negative" :label="ph" class="q-ml-xs" /></q-chip><span v-if="!(props.row.isolates || []).length" class="text-grey-5">—</span></q-td></template>
        <template v-slot:body-cell-registeredAt="props"><q-td :props="props">{{ props.row.registeredAt || props.row.createdOn | datetime }}</q-td></template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right no-wrap" @click.stop>
            <q-btn flat dense round size="sm" icon="open_in_new" color="primary" @click="open(props.row)" />
            <q-btn flat dense round size="sm" icon="edit" color="primary" @click="openDialog(props.row)" />
            <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDelete(props.row)" />
          </q-td>
        </template>
      </q-table>
    </div>

    <culture-dialog v-model="dialogOpen" :culture="active" @saved="onSaved" />
    <confirm-dialog v-model="deleteOpen" title="Видалити посів" message="Посів разом з ізолятами та антибіотикограмою буде видалено." ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import CultureDialog from './CultureDialog.vue';
import { CULTURE_STATUS, toOptions } from '../../../utils/statuses';

export default {
  name: 'MicrobiologyCultures',
  mixins: [apiMixin],
  components: { CultureDialog },
  data () {
    return {
      rows: [], search: '', status: null, statusOptions: toOptions(CULTURE_STATUS),
      dialogOpen: false, deleteOpen: false, active: null,
      columns: [
        { name: 'registeredAt', label: 'Зареєстровано', align: 'left', sortable: true },
        { name: 'patient', label: 'Пацієнт', field: r => r.patientName || (r.patient && r.patient.fullName) || '—', align: 'left' },
        { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left' },
        { name: 'specimen', label: 'Локус', field: r => r.specimenType || r.specimen, align: 'left' },
        { name: 'growth', label: 'Ріст', align: 'center' },
        { name: 'isolates', label: 'Ізоляти / фенотипи', align: 'left' },
        { name: 'status', label: 'Статус', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  created () { this.load(); },
  methods: {
    async load () { const res = await this.callApi(() => this.$api.cultures({ search: this.search, status: this.status })); if (res !== undefined) this.rows = this.asList(res); },
    open (row) { this.$router.push({ name: 'lab-culture-card', params: { id: row.id } }); },
    openDialog (c) { this.active = c; this.dialogOpen = true; },
    onSaved (res) { this.load(); if (res && res.id && !(this.active && this.active.id)) this.$router.push({ name: 'lab-culture-card', params: { id: res.id } }); },
    askDelete (c) { this.active = c; this.deleteOpen = true; },
    async doDelete () { try { await this.$api.deleteCulture(this.active.id); this.notifyOk('Посів видалено'); this.load(); } catch (e) { this.notifyError(e); } }
  }
};
</script>
