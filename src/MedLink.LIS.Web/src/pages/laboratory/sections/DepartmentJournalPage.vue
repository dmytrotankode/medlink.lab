<template>
  <div class="journal-page" data-testid="journalPage">
    <page-header title="Журнал відділення" icon="fas fa-book" subtitle="Журнальні номери по підрозділу за дату: номер, денний №, замовлення, пацієнт, проба, тести, статус" :breadcrumbs="[{ label: 'Підрозділи лабораторії', to: { name: 'lab-sections' } }, { label: 'Журнал' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn outline dense color="primary" icon="print" label="Друк журналу" :disable="!rows.length" @click="print" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-4"><q-select v-model="sectionId" dense outlined label="Підрозділ" :options="sectionOptions" emit-value map-options data-testid="journalSection" @input="load" /></div>
      <div class="col-6 col-md-2"><q-input v-model="date" dense outlined type="date" stack-label label="Дата" data-testid="journalDate" @input="load" /></div>
      <div class="col-6 col-md-2 row q-gutter-xs"><q-btn dense flat icon="chevron_left" @click="shift(-1)" /><q-btn dense flat label="Сьогодні" @click="date = today(); load()" /><q-btn dense flat icon="chevron_right" @click="shift(1)" /></div>
      <div class="col-12 col-md-4"><q-input v-model="search" dense outlined clearable placeholder="Пошук: номер, пацієнт, штрихкод"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
    </div>

    <div class="medlink-card" id="journalPrintArea">
      <div class="medlink-card__title"><span>{{ sectionName }} — {{ date | date }}</span><span class="text-caption text-grey-6">записів: {{ filtered.length }}</span></div>
      <q-table :data="filtered" :columns="columns" row-key="id" dense flat :loading="loading" :pagination="{ rowsPerPage: 0 }" hide-pagination no-data-label="Записів у журналі за цю дату немає" data-testid="journalTable">
        <template v-slot:body-cell-journalNumber="props"><q-td :props="props"><b class="mono">{{ props.row.journalNumber }}</b></q-td></template>
        <template v-slot:body-cell-order="props"><q-td :props="props"><router-link v-if="props.row.orderId" :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" class="text-primary">{{ props.row.orderNumber }}</router-link><span v-else>{{ props.row.orderNumber }}</span></q-td></template>
        <template v-slot:body-cell-barcode="props"><q-td :props="props"><router-link v-if="props.row.barcode" :to="{ name: 'lab-sample-processing', params: { barcode: props.row.barcode } }" class="mono text-primary">{{ props.row.barcode }}</router-link></q-td></template>
        <template v-slot:body-cell-tests="props"><q-td :props="props" class="text-caption">{{ (props.row.tests || props.row.testCodes || []).map(t => t.testCode || t).join(', ') }}</q-td></template>
        <template v-slot:body-cell-status="props"><q-td :props="props"><status-chip :value="props.row.status || props.row.orderStatus" :type="props.row.testStatus ? 'test' : 'order'" /></q-td></template>
        <template v-slot:body-cell-registeredAt="props"><q-td :props="props">{{ props.row.registeredAt || props.row.createdOn || props.row.at | datetime }}</q-td></template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right no-wrap">
            <q-btn flat dense round size="sm" icon="open_in_new" color="primary" :disable="!props.row.orderId" :to="props.row.orderId ? { name: 'lab-order-card', params: { id: props.row.orderId } } : undefined" />
            <q-btn flat dense round size="sm" icon="fas fa-microscope" color="grey-8" :to="{ name: 'lab-workstation', query: { search: props.row.orderNumber || props.row.barcode } }"><q-tooltip>На робочий стіл</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="biotech" color="teal-7" :disable="!props.row.barcode" :to="props.row.barcode ? { name: 'lab-sample-processing', params: { barcode: props.row.barcode } } : undefined"><q-tooltip>Обробка зразка</q-tooltip></q-btn>
          </q-td>
        </template>
      </q-table>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import { todayIso, toIsoDate } from '../../../utils/format';

export default {
  name: 'DepartmentJournalPage',
  mixins: [apiMixin],
  data () {
    return {
      sections: [], rows: [], sectionId: null, date: todayIso(), search: '',
      columns: [
        { name: 'journalNumber', label: '№ журналу', field: 'journalNumber', align: 'left', sortable: true },
        { name: 'dayNumber', label: '№ за день', field: r => r.dayNumber || r.daySeq, align: 'center', sortable: true },
        { name: 'registeredAt', label: 'Час', align: 'left' },
        { name: 'order', label: 'Замовлення', align: 'left' },
        { name: 'patient', label: 'Пацієнт', field: r => r.patientName || (r.patient && r.patient.fullName) || '—', align: 'left' },
        { name: 'barcode', label: 'Проба', align: 'left' },
        { name: 'tests', label: 'Дослідження', align: 'left' },
        { name: 'status', label: 'Статус', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    sectionOptions () { return this.sections.map(s => ({ value: s.id, label: `${s.name} (${s.code})` })); },
    sectionName () { const s = this.sections.find(x => x.id === this.sectionId); return s ? s.name : 'Підрозділ'; },
    filtered () { const q = (this.search || '').toLowerCase(); return this.rows.filter(r => !q || JSON.stringify(r).toLowerCase().includes(q)); }
  },
  async created () {
    try { this.sections = this.asList(await this.$api.sections()); } catch (e) { if (!this.apiOffline) this.apiError = e.userMessage; }
    this.sectionId = this.$route.query.sectionId || (this.sections[0] && this.sections[0].id) || null;
    if (this.$route.query.date) this.date = this.$route.query.date;
    this.load();
  },
  methods: {
    today: todayIso,
    shift (d) { const x = new Date(this.date); x.setDate(x.getDate() + d); this.date = toIsoDate(x); this.load(); },
    async load () {
      if (!this.sectionId) { this.rows = []; return; }
      const res = await this.callApi(() => this.$api.sectionJournal(this.sectionId, this.date));
      if (res !== undefined) this.rows = this.asList(res.entries ? res.entries : res);
      this.$router.replace({ query: { sectionId: this.sectionId, date: this.date } }).catch(() => {});
    },
    print () { window.print(); }
  }
};
</script>

<style>
@media print {
  .q-header, .q-drawer, .page-header, .q-page > .medlink-card:first-of-type { display: none !important; }
  #journalPrintArea { box-shadow: none; border: 0; }
}
</style>
