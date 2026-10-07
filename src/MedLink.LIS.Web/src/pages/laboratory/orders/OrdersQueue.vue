<template>
  <div class="orders-queue">
    <page-header title="Реєстрація направлень" icon="fas fa-file-medical" subtitle="Черга замовлень лабораторії: реєстрація, пошук, статуси, CITO" :breadcrumbs="[{ label: 'Замовлення' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
      <q-btn unelevated color="primary" icon="add" label="Нове направлення" @click="createOpen = true" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <!-- Зона A: фільтри -->
    <div class="medlink-card q-pa-sm q-mb-md">
      <div class="row q-col-gutter-sm items-center">
        <div class="col-12 col-md-3">
          <q-input v-model="filters.search" dense outlined clearable placeholder="№ замовлення, ПІБ, штрихкод" @keyup.enter="applyFilters" @clear="applyFilters">
            <template v-slot:prepend><q-icon name="search" /></template>
          </q-input>
        </div>
        <div class="col-6 col-md-2">
          <q-select v-model="filters.status" dense outlined clearable label="Статус" :options="statusOptions" emit-value map-options @input="applyFilters" />
        </div>
        <div class="col-6 col-md-2">
          <q-input v-model="filters.from" dense outlined type="date" label="З" stack-label @input="applyFilters" />
        </div>
        <div class="col-6 col-md-2">
          <q-input v-model="filters.to" dense outlined type="date" label="По" stack-label @input="applyFilters" />
        </div>
        <div class="col-6 col-md-2">
          <q-select v-model="filters.departmentId" dense outlined clearable label="Відділення" :options="departmentOptions" emit-value map-options @input="applyFilters" />
        </div>
        <div class="col-12 col-md-1 row items-center justify-end">
          <q-toggle v-model="filters.cito" label="CITO" color="deep-orange-6" keep-color @input="applyFilters" />
        </div>
      </div>
    </div>

    <!-- Зона B: черга -->
    <div class="medlink-card">
      <q-table
        :data="rows"
        :columns="columns"
        row-key="id"
        dense
        flat
        :loading="loading"
        :pagination.sync="pagination"
        :rows-per-page-options="[20, 50, 100]"
        @request="onRequest"
        binary-state-sort
        no-data-label="Замовлень не знайдено"
        :rows-per-page-label="'Рядків на сторінці'"
      >
        <template v-slot:body="props">
          <q-tr :props="props" :class="rowClass(props.row)" class="cursor-pointer" @click="openOrder(props.row)">
            <q-td key="orderNumber" :props="props">
              <span class="text-weight-bold mono">{{ props.row.orderNumber }}</span>
              <q-badge v-if="props.row.isUrgentCito" color="deep-orange-6" label="CITO" class="q-ml-xs" />
            </q-td>
            <q-td key="orderDatetime" :props="props">{{ props.row.orderDatetime | datetime }}</q-td>
            <q-td key="patient" :props="props">
              <div>{{ patientName(props.row) }}</div>
              <div class="text-caption text-grey-7">{{ patientMeta(props.row) }}</div>
            </q-td>
            <q-td key="tests" :props="props">
              <span class="text-caption">{{ testsSummary(props.row) }}</span>
            </q-td>
            <q-td key="department" :props="props">{{ departmentName(props.row.departmentId) }}</q-td>
            <q-td key="totalPrice" :props="props" class="text-right">{{ props.row.totalPrice | money }}</q-td>
            <q-td key="status" :props="props"><status-chip :value="props.row.status" type="order" icon /></q-td>
            <q-td key="actions" :props="props" @click.stop>
              <q-btn flat dense round size="sm" icon="open_in_new" color="primary" @click="openOrder(props.row)"><q-tooltip>Картка замовлення</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="print" color="grey-8" @click="printLabels(props.row)"><q-tooltip>Етикетки</q-tooltip></q-btn>
            </q-td>
          </q-tr>
        </template>
      </q-table>
    </div>

    <!-- Легенда кольорів -->
    <div class="row q-gutter-md q-mt-sm text-caption text-grey-7 items-center">
      <span><span class="legend-box" style="background:#fff;border:1px solid #ccc" /> На виконання</span>
      <span><span class="legend-box" style="background:#f3f4f6" /> Закрито</span>
      <span><span class="legend-box" style="background:#fff8dc" /> Відмова</span>
      <span><span class="legend-box" style="background:#fde8e8" /> Скасовано</span>
      <span><span class="legend-box" style="background:#ffe0b2" /> Прострочено</span>
      <span><span class="legend-box" style="background:#fff;box-shadow: inset 4px 0 0 #d04f45" /> CITO</span>
    </div>

    <create-order-dialog v-model="createOpen" @created="onCreated" />
    <labels-dialog v-model="labelsOpen" :labels="labels" :loading="labelsLoading" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import CreateOrderDialog from './CreateOrderDialog.vue';
import LabelsDialog from '../../../components/common/LabelsDialog.vue';
import { ORDER_STATUS, orderRowClass, toOptions } from '../../../utils/statuses';
import { patientDisplay, genderLabel, ageFromBirthDate, daysAgoIso, todayIso } from '../../../utils/format';

export default {
  name: 'OrdersQueue',
  mixins: [apiMixin],
  components: { CreateOrderDialog, LabelsDialog },
  data () {
    return {
      rows: [],
      createOpen: false,
      labelsOpen: false,
      labels: [],
      labelsLoading: false,
      filters: {
        search: '',
        status: null,
        from: daysAgoIso(7),
        to: todayIso(),
        departmentId: null,
        cito: false
      },
      pagination: { page: 1, rowsPerPage: 20, rowsNumber: 0, sortBy: 'orderDatetime', descending: true },
      statusOptions: toOptions(ORDER_STATUS),
      columns: [
        { name: 'orderNumber', label: '№ замовлення', field: 'orderNumber', align: 'left', sortable: true },
        { name: 'orderDatetime', label: 'Дата', field: 'orderDatetime', align: 'left', sortable: true },
        { name: 'patient', label: 'Пацієнт', align: 'left' },
        { name: 'tests', label: 'Дослідження', align: 'left' },
        { name: 'department', label: 'Відділення', align: 'left' },
        { name: 'totalPrice', label: 'Сума', field: 'totalPrice', align: 'right', sortable: true },
        { name: 'status', label: 'Статус', field: 'status', align: 'center', sortable: true },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    departmentOptions () { return this.$store.getters['dictionaries/options']('departments'); },
    departments () { return this.$store.getters['dictionaries/items']('departments'); }
  },
  created () {
    if (this.$route.query.cito === 'true') this.filters.cito = true;
    if (this.$route.query.status) this.filters.status = this.$route.query.status;
    if (this.$route.query.create) this.createOpen = true;
    this.$store.dispatch('dictionaries/loadMany', ['departments']);
    this.load();
  },
  methods: {
    rowClass: orderRowClass,
    patientName (row) { return patientDisplay(row.patient) !== '—' ? patientDisplay(row.patient) : (row.patientName || '—'); },
    patientMeta (row) {
      const p = row.patient || {};
      const age = ageFromBirthDate(p.birthDate);
      return [age !== null ? `${age} р.` : null, genderLabel(p.gender) !== '—' ? genderLabel(p.gender) : null, p.phone].filter(Boolean).join(' · ');
    },
    testsSummary (row) {
      const tests = row.tests || [];
      if (!tests.length) return row.testsCount ? `${row.testsCount} показн.` : '—';
      const codes = tests.map(t => t.testCode).filter(Boolean);
      return codes.length > 6 ? `${codes.slice(0, 6).join(', ')} +${codes.length - 6}` : codes.join(', ');
    },
    departmentName (id) {
      const d = this.departments.find(x => String(x.id) === String(id));
      return d ? d.name : (id || '—');
    },
    applyFilters () {
      this.pagination.page = 1;
      this.load();
    },
    onRequest (props) {
      this.pagination = { ...this.pagination, ...props.pagination };
      this.load();
    },
    async load () {
      const p = this.pagination;
      const res = await this.callApi(() => this.$api.getOrders({
        status: this.filters.status,
        from: this.filters.from,
        to: this.filters.to,
        cito: this.filters.cito ? true : null,
        departmentId: this.filters.departmentId,
        search: this.filters.search,
        page: p.page,
        pageSize: p.rowsPerPage,
        sort: p.sortBy,
        dir: p.descending ? 'desc' : 'asc'
      }));
      if (res === undefined) { this.rows = []; return; }
      this.rows = this.asList(res);
      this.pagination.rowsNumber = this.asTotal(res, this.rows.length);
    },
    openOrder (row) {
      this.$router.push({ name: 'lab-order-card', params: { id: row.id } });
    },
    onCreated (order) {
      this.load();
      if (order && order.id) this.$router.push({ name: 'lab-order-card', params: { id: order.id }, query: { labels: 1 } });
    },
    async printLabels (row) {
      this.labelsOpen = true;
      this.labelsLoading = true;
      this.labels = [];
      try {
        const res = await this.$api.orderLabels(row.id);
        this.labels = this.asList(res).map(l => ({ patientName: this.patientName(row), orderNumber: row.orderNumber, ...l }));
      } catch (e) {
        this.notifyError(e);
      } finally {
        this.labelsLoading = false;
      }
    }
  }
};
</script>

<style scoped>
.legend-box { display: inline-block; width: 14px; height: 12px; vertical-align: middle; margin-right: 4px; border-radius: 2px; }
</style>
