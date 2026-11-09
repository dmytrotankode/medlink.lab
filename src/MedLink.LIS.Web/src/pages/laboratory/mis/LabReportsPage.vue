<!--
  ТЗ 23.6 (FR-RPT-001…014): звіти лабораторії — кумулятивний звіт пацієнта (стрілки ↑ ↓ ↑↑ ↓↓, критичні, групування за
  дослідженням або аналізатором), навантаження, джерела, використання показників, TAT, розподіл, невиконані, пакети; XLSX/PDF.
-->
<template>
  <div data-testid="labReportsPage">
    <page-header title="Звіти лабораторії" icon="fas fa-file-alt" subtitle="Кумулятивний звіт пацієнта і звіти роботи лабораторії за період; завантаження в XLSX і PDF" :breadcrumbs="[{ label: 'Звіти' }]" />
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="run" />

    <div class="row q-col-gutter-md">
      <div class="col-12 col-md-3">
        <div class="medlink-card">
          <q-list dense separator>
            <q-item v-for="r in list" :key="r.type" clickable :active="type === r.type" active-class="bg-blue-1" :data-testid="`report-${r.type}`" @click="select(r.type)">
              <q-item-section avatar><q-icon :name="r.type === 'cumulative' ? 'timeline' : 'table_chart'" color="primary" /></q-item-section>
              <q-item-section><q-item-label>{{ r.title }}</q-item-label><q-item-label v-if="r.description" caption>{{ r.description }}</q-item-label></q-item-section>
            </q-item>
          </q-list>
        </div>
      </div>

      <div class="col-12 col-md-9">
        <div class="medlink-card q-pa-sm q-mb-sm row q-gutter-sm items-center">
          <patient-select v-if="type === 'cumulative'" v-model="patient" :allow-create="false" style="min-width: 320px" />
          <q-input v-model="from" dense outlined type="date" label="З" stack-label />
          <q-input v-model="to" dense outlined type="date" label="По" stack-label />
          <template v-if="type === 'cumulative'">
            <q-btn-toggle v-model="group" dense unelevated toggle-color="primary" color="grey-2" text-color="grey-9" :options="[{ label: 'За дослідженням', value: 'service' }, { label: 'За аналізатором', value: 'analyzer' }]" />
            <q-toggle v-model="abnormalOnly" dense label="Лише відхилення" />
          </template>
          <q-select v-else v-model="source" dense outlined clearable emit-value map-options :options="sourceOptions" label="Джерело" style="min-width: 170px" />
          <span class="inline-block">
            <q-btn unelevated color="primary" icon="play_arrow" label="Сформувати" :loading="loading" :disable="type === 'cumulative' && !patientId" data-testid="runReport" @click="run" />
            <q-tooltip v-if="type === 'cumulative' && !patientId">Оберіть пацієнта</q-tooltip>
          </span>
          <q-space />
          <span class="inline-block">
            <q-btn flat dense color="primary" icon="grid_on" label="Завантажити XLSX" type="a" :href="fileUrl('xlsx')" :disable="!canExport" />
            <q-tooltip v-if="!canExport">{{ exportHint }}</q-tooltip>
          </span>
          <span class="inline-block">
            <q-btn flat dense color="primary" icon="picture_as_pdf" label="Завантажити PDF" type="a" :href="fileUrl('pdf')" :disable="!canExport" />
            <q-tooltip v-if="!canExport">{{ exportHint }}</q-tooltip>
          </span>
        </div>

        <!-- Кумулятивний -->
        <div v-if="type === 'cumulative' && cumulative" class="medlink-card q-pa-sm" data-testid="cumulativeReport">
          <div class="text-subtitle1 text-weight-bold">{{ cumulative.patientName }} <span class="text-caption text-grey-7">{{ cumulative.ageGender }}</span></div>
          <q-banner v-if="cumulative.criticals.length" dense class="bg-red-1 text-negative q-my-sm">
            <b>Критичні значення:</b>
            <span v-for="(c, i) in cumulative.criticals" :key="i" class="q-ml-sm">{{ c.at | datetime }} {{ c.testCode }} {{ c.value }} {{ c.arrow }}<span v-if="c.analyzer"> ({{ c.analyzer }})</span>;</span>
          </q-banner>
          <div v-for="(g, gi) in cumulative.groups" :key="gi" class="q-mb-md matrix-scroll">
            <div class="text-weight-bold q-mb-xs">{{ g.service }}<span v-if="g.analyzer" class="text-grey-7"> · {{ g.analyzer }}</span></div>
            <q-markup-table dense flat bordered>
              <thead>
                <tr><th class="text-left">Показник</th><th class="text-left">Од. вим.</th><th class="text-left">Норма</th><th v-for="(p, pi) in g.points" :key="pi">{{ p.at | datetime }}<div class="text-caption text-grey-7">{{ p.analyzer }}</div></th></tr>
              </thead>
              <tbody>
                <tr v-for="r in g.rows" :key="r.code">
                  <td><b>{{ r.code }}</b>&nbsp;<span class="text-grey-7">{{ r.name }}</span></td>
                  <td>{{ r.unit }}</td>
                  <td class="text-caption">{{ r.reference }}</td>
                  <td v-for="(v, vi) in r.values" :key="vi" class="text-center" :class="v && v.critical ? 'bg-red-2 text-weight-bold' : v && v.arrow ? 'bg-orange-1' : ''">
                    <span v-if="v">{{ v.display }} <span class="text-weight-bold">{{ v.arrow }}</span></span>
                  </td>
                </tr>
              </tbody>
            </q-markup-table>
          </div>
          <empty-state v-if="!cumulative.groups.length" title="Результатів за період немає" icon="timeline" />
        </div>

        <!-- Табличні -->
        <div v-if="type !== 'cumulative' && table" class="medlink-card q-pa-sm">
          <div class="text-subtitle1 text-weight-bold">{{ table.title }}</div>
          <div v-if="table.note" class="text-caption text-grey-7 q-mb-sm">{{ table.note }}</div>
          <q-table :data="table.rows" :columns="tableColumns" row-key="__i" dense flat :pagination="{ rowsPerPage: 50 }" no-data-label="Даних за період немає" />
        </div>
        <empty-state v-if="!type" title="Оберіть звіт" icon="description" />
        <div v-else-if="(type === 'cumulative' && !cumulative) || (type !== 'cumulative' && !table && !loading)" class="medlink-card">
          <empty-state title="Звіт ще не сформовано" icon="description" :hint="type === 'cumulative' ? 'Оберіть пацієнта й період та натисніть «Сформувати»' : 'Оберіть параметри й натисніть «Сформувати»'" />
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import PatientSelect from '../../../components/common/PatientSelect.vue';
import { patientDisplay } from '../../../utils/format';

const iso = d => d.toISOString().slice(0, 10);

export default {
  name: 'LabReportsPage',
  components: { PatientSelect },
  mixins: [apiMixin],
  data () {
    const now = new Date();
    return {
      list: [], type: 'cumulative', patient: null, from: iso(new Date(now.getTime() - 30 * 864e5)), to: iso(now), group: 'service', abnormalOnly: false, source: null,
      cumulative: null, table: null,
      sourceOptions: [{ label: 'Стаціонар', value: 'INPATIENT' }, { label: 'Поліклініка', value: 'POLYCLINIC' }, { label: 'Е-направлення ЕСОЗ', value: 'EHEALTH' }, { label: 'Партнер', value: 'PARTNER' }]
    };
  },
  computed: {
    patientId () { return this.patient && this.patient.id; },
    params () {
      const p = { from: this.from, to: `${this.to}T23:59:59` };
      return this.type === 'cumulative' ? { ...p, group: this.group, abnormalOnly: this.abnormalOnly } : { ...p, source: this.source };
    },
    canExport () { return this.type === 'cumulative' ? !!this.patientId : !!this.type; },
    exportHint () { return this.type === 'cumulative' ? 'Оберіть пацієнта' : 'Спершу сформуйте звіт'; },
    tableColumns () {
      return this.table ? this.table.columns.map(c => ({ name: c.key, label: c.label, field: r => this.cell(r[c.key]), align: c.numeric ? 'right' : 'left', sortable: true })) : [];
    }
  },
  async created () {
    this.list = (await this.callApi(() => this.$api.reportsList())) || [];
    const q = this.$route.query.patient;
    if (q) {
      const p = await this.callApi(() => this.$api.getPatient(q), { silent: true });
      if (p) { this.patient = { ...p, display: patientDisplay(p) }; this.run(); }
    }
  },
  methods: {
    cell (v) { return typeof v === 'number' ? v.toLocaleString('uk-UA') : v; },
    select (t) { this.type = t; this.table = null; this.cumulative = null; if (t !== 'cumulative') this.run(); },
    fileUrl (format) {
      if (this.type === 'cumulative') return this.patientId ? this.$api.cumulativeReportUrl(this.patientId, { ...this.params, format }) : null;
      return this.$api.reportUrl(this.type, { ...this.params, format });
    },
    async run () {
      if (this.type === 'cumulative') {
        if (!this.patientId) return;
        this.cumulative = await this.callApi(() => this.$api.cumulativeReport(this.patientId, this.params));
      } else {
        const t = await this.callApi(() => this.$api.report(this.type, this.params));
        if (t) { t.rows = t.rows.map((r, i) => ({ ...r, __i: i })); this.table = t; }
      }
    }
  }
};
</script>

<style scoped>
.matrix-scroll { overflow-x: auto; }
</style>
