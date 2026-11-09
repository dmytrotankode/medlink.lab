<template>
  <div data-testid="referralJournal">
    <page-header title="Журнал направлень" icon="fas fa-clipboard-list" :breadcrumbs="[{ label: 'Журнал направлень' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="row q-col-gutter-sm q-mb-md">
      <div v-for="s in summary" :key="s.type" class="col-6 col-md">
        <div class="medlink-card q-pa-sm cursor-pointer type-tile" :class="filter.type === s.type ? 'type-tile--active' : ''" @click="filter.type = filter.type === s.type ? null : s.type">
          <q-tooltip>{{ filter.type === s.type ? 'Натисніть, щоб зняти фільтр' : 'Натисніть, щоб відфільтрувати' }}</q-tooltip>
          <div class="text-caption text-grey-7"><q-icon v-if="filter.type === s.type" name="filter_alt" color="primary" size="14px" class="q-mr-xs" />{{ s.name }}</div>
          <div class="text-h6">{{ s.count }}</div>
          <div class="text-caption text-grey-6">{{ s.count ? formatMoney(s.amount) : '' }}&nbsp;</div>
        </div>
      </div>
    </div>

    <div class="medlink-card q-pa-sm">
      <div class="row q-col-gutter-sm q-mb-sm">
        <div class="col-6 col-md-2"><q-input v-model="filter.from" outlined dense type="date" stack-label label="Дата від" /></div>
        <div class="col-6 col-md-2"><q-input v-model="filter.to" outlined dense type="date" stack-label label="Дата до" /></div>
        <div class="col-6 col-md-2"><q-select v-model="filter.ehealth" outlined dense clearable label="Статус ЕСОЗ" :options="ehealthOptions" emit-value map-options /></div>
        <div class="col-6 col-md-2"><q-select v-model="filter.status" outlined dense clearable label="Статус направлення" :options="statusOptions" emit-value map-options /></div>
        <div class="col-12 col-md-3"><q-input v-model="filter.search" outlined dense clearable label="Пошук" placeholder="№, пацієнт, направник" debounce="400" /></div>
        <div class="col-12 col-md-1 row items-center"><q-btn flat dense no-caps color="grey-8" icon="filter_alt_off" label="Скинути" :disable="!filtersChanged" @click="resetFilters" /></div>
      </div>
      <q-table :data="filteredItems" :columns="columns" row-key="orderId" dense flat :loading="loading" :pagination="{ rowsPerPage: 30 }" no-data-label="Нічого не знайдено. Змініть пошук або скиньте фільтри." :rows-per-page-label="'Рядків на сторінці:'">
        <template v-slot:body-cell-order="props">
          <q-td :props="props"><router-link :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" class="text-primary">{{ props.row.orderNumber }}</router-link></q-td>
        </template>
        <template v-slot:body-cell-status="props">
          <q-td :props="props"><status-chip :value="props.row.status" type="order" /></q-td>
        </template>
        <template v-slot:body-cell-sum="props">
          <q-td :props="props" class="text-right">{{ props.row.totalPrice | money }}<q-badge v-if="props.row.referralType === 'EHEALTH' && !props.row.totalPrice" color="orange-7" class="q-ml-xs" label="Потрібен вибір показників" /></q-td>
        </template>
        <template v-slot:body-cell-type="props">
          <q-td :props="props"><q-badge :color="typeColor(props.row.referralType)" :label="props.row.referralTypeName" /></q-td>
        </template>
      </q-table>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import { formatDateTime, formatDate, formatMoney, daysAgoIso, todayIso } from '../../../utils/format';
import { ORDER_STATUS, toOptions } from '../../../utils/statuses';

export default {
  name: 'ReferralJournal',
  mixins: [apiMixin],
  data () {
    return {
      filter: { from: daysAgoIso(30), to: todayIso(), type: null, search: '', ehealth: null, status: null },
      statusOptions: toOptions(ORDER_STATUS),
      summary: [],
      items: [],
      columns: [
        { name: 'date', label: 'Створено', field: r => formatDateTime(r.orderDatetime), align: 'left', sortable: true },
        { name: 'order', label: '№ направлення в лабораторії', field: 'orderNumber', align: 'left' },
        { name: 'type', label: 'Тип', field: 'referralTypeName', align: 'left' },
        { name: 'number', label: '№ направлення лікаря', field: r => r.referralNumber || '—', align: 'left' },
        { name: 'refDate', label: 'Дата направлення', field: r => formatDate(r.referralDate), align: 'left' },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'referrer', label: 'Направник', field: r => r.referrer || '—', align: 'left' },
        { name: 'ehealth', label: 'Статус ЕСОЗ', field: r => r.ehealthStatus || '—', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' },
        { name: 'sum', label: 'Сума', field: r => r.totalPrice, align: 'right', format: v => formatMoney(v) }
      ]
    };
  },
  computed: {
    ehealthOptions () { return [...new Set(this.items.map(i => i.ehealthStatus).filter(Boolean))].map(v => ({ value: v, label: v })); },
    filteredItems () {
      const f = this.filter;
      return this.items.filter(i => (!f.ehealth || i.ehealthStatus === f.ehealth) && (!f.status || i.status === f.status));
    },
    filtersChanged () {
      const f = this.filter;
      return !!(f.type || f.search || f.ehealth || f.status || f.from !== daysAgoIso(30) || f.to !== todayIso());
    }
  },
  watch: { filter: { deep: true, handler () { this.load(); } } },
  created () { this.load(); },
  methods: {
    formatMoney,
    resetFilters () { this.filter = { from: daysAgoIso(30), to: todayIso(), type: null, search: '', ehealth: null, status: null }; },
    typeColor (t) { return { EHEALTH: 'teal-6', PAPER: 'brown-5', INTERNAL: 'primary', EXTERNAL_CLINIC: 'purple-5', SELF: 'grey-6' }[t] || 'grey'; },
    async load () {
      const f = this.filter;
      const res = await this.callApi(() => this.$api.referralJournal({ from: f.from || null, to: f.to || null, type: f.type, search: f.search || null }), { silent: false });
      if (res) { this.summary = res.summary; this.items = res.items; }
    }
  }
};
</script>

<style scoped>
.type-tile--active { background: #e3f2fd; box-shadow: inset 0 0 0 2px #1976d2; }
</style>
