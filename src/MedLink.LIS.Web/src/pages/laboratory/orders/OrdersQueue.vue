<template>
  <div class="orders-queue" data-testid="ordersPage">
    <page-header title="Реєстрація направлень" icon="fas fa-file-medical" :breadcrumbs="[{ label: 'Реєстрація направлень' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
      <q-btn unelevated color="primary" icon="add" label="Нове направлення" data-testid="ordersCreate" @click="createOpen = true" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <!-- Зона A: фільтри -->
    <div class="medlink-card q-pa-sm q-mb-md">
      <div class="row q-col-gutter-sm items-center">
        <div class="col-12 col-md-3">
          <q-input v-model="filters.search" dense outlined clearable label="Пошук" placeholder="№ направлення, ПІБ, штрихкод" @input="onSearchInput" @keyup.enter="applyFilters" @clear="applyFilters">
            <template v-slot:prepend><q-icon name="search" /></template>
          </q-input>
        </div>
        <div class="col-6 col-md-2">
          <q-select v-model="filters.status" dense outlined clearable label="Статус" :options="statusOptions" emit-value map-options @input="applyFilters" />
        </div>
        <div class="col-6 col-md-2">
          <q-input v-model="filters.from" dense outlined type="date" label="Дата від" stack-label @input="applyFilters" />
        </div>
        <div class="col-6 col-md-2">
          <q-input v-model="filters.to" dense outlined type="date" label="Дата до" stack-label @input="applyFilters" />
        </div>
        <div class="col-6 col-md-2">
          <q-select v-model="filters.departmentId" dense outlined clearable label="Відділення" :options="departmentOptions" emit-value map-options @input="applyFilters" />
        </div>
        <div class="col-12 col-md-1 row items-center justify-end">
          <q-toggle v-model="filters.cito" label="Лише термінові (CITO)" color="deep-orange-6" keep-color @input="applyFilters" />
        </div>
      </div>
      <div class="row items-center q-gutter-xs q-mt-xs">
        <span class="text-caption text-grey-7">Період:</span>
        <q-btn v-for="p in periods" :key="p.key" flat dense no-caps size="sm" color="primary" :label="p.label" :class="{ 'text-weight-bold': periodKey === p.key }" @click="setPeriod(p)" />
        <q-space />
        <q-btn flat dense no-caps size="sm" color="grey-8" icon="filter_alt_off" label="Скинути фільтри" :disable="!filtersChanged" @click="resetFilters" />
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
        no-data-label="Нічого не знайдено. Змініть пошук або скиньте фільтри."
        :rows-per-page-label="'Рядків на сторінці:'"
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
            <q-td key="department" :props="props">{{ props.row.departmentName || departmentName(props.row.departmentId) }}</q-td>
            <q-td key="totalPrice" :props="props" class="text-right">{{ props.row.totalPrice | money }}</q-td>
            <q-td key="status" :props="props"><status-chip :value="props.row.status" type="order" icon /></q-td>
            <q-td key="actions" :props="props" @click.stop>
              <q-btn flat dense round size="sm" icon="open_in_new" color="primary" @click="openOrder(props.row)"><q-tooltip>Картка направлення</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="print" color="grey-8" @click="printLabels(props.row)"><q-tooltip>Етикетки</q-tooltip></q-btn>
            </q-td>
          </q-tr>
        </template>
      </q-table>
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
import { patientDisplay, genderLabel, ageFromBirthDate, daysAgoIso, todayIso, pluralN } from '../../../utils/format';

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
      searchTimer: null,
      periods: [
        { key: 'today', label: 'Сьогодні', days: 0 },
        { key: '7', label: '7 днів', days: 7 },
        { key: '30', label: 'Місяць', days: 30 }
      ],
      pagination: { page: 1, rowsPerPage: 20, rowsNumber: 0, sortBy: 'orderDatetime', descending: true },
      statusOptions: toOptions(ORDER_STATUS),
      columns: [
        { name: 'orderNumber', label: '№ направлення', field: 'orderNumber', align: 'left', sortable: true },
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
    departments () { return this.$store.getters['dictionaries/items']('departments'); },
    periodKey () {
      const p = this.periods.find(x => this.filters.from === daysAgoIso(x.days) && this.filters.to === todayIso());
      return p ? p.key : null;
    },
    filtersChanged () {
      const f = this.filters;
      return !!(f.search || f.status || f.departmentId || f.cito || this.periodKey !== '7');
    }
  },
  beforeDestroy () { clearTimeout(this.searchTimer); },
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
      const total = row.testsTotal != null ? row.testsTotal : row.testsCount;
      if (!tests.length) return total ? `${pluralN(total, 'показник', 'показники', 'показників')}${row.testsVerified != null ? `, верифіковано ${row.testsVerified}` : ''}` : 'Без послуг';
      const codes = tests.map(t => t.testCode).filter(Boolean);
      return codes.length > 6 ? `${codes.slice(0, 6).join(', ')} +${codes.length - 6}` : codes.join(', ');
    },
    departmentName (id) {
      const d = this.departments.find(x => String(x.id) === String(id));
      return d ? d.name : (id || '—');
    },
    onSearchInput () {
      clearTimeout(this.searchTimer);
      this.searchTimer = setTimeout(() => this.applyFilters(), 400);
    },
    setPeriod (p) {
      this.filters.from = daysAgoIso(p.days);
      this.filters.to = todayIso();
      this.applyFilters();
    },
    resetFilters () {
      this.filters = { search: '', status: null, from: daysAgoIso(7), to: todayIso(), departmentId: null, cito: false };
      this.applyFilters();
    },
    applyFilters () {
      clearTimeout(this.searchTimer);
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
