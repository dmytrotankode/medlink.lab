<template>
  <div data-testid="referralJournal">
    <page-header title="Журнал направлень" icon="fas fa-clipboard-list" subtitle="Облік направлень за типами: е-направлення ЕСОЗ, паперові, внутрішні, клініки-партнери, самозвернення" :breadcrumbs="[{ label: 'Журнал направлень' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="row q-col-gutter-sm q-mb-md">
      <div v-for="s in summary" :key="s.type" class="col-6 col-md">
        <div class="medlink-card q-pa-sm cursor-pointer" :class="filter.type === s.type ? 'bg-blue-1' : ''" @click="filter.type = filter.type === s.type ? null : s.type">
          <div class="text-caption text-grey-7">{{ s.name }}</div>
          <div class="text-h6">{{ s.count }}</div>
          <div class="text-caption text-grey-6">{{ s.amount | money }}</div>
        </div>
      </div>
    </div>

    <div class="medlink-card q-pa-sm">
      <div class="row q-col-gutter-sm q-mb-sm">
        <div class="col-6 col-md-2"><q-input v-model="filter.from" outlined dense type="date" stack-label label="З" /></div>
        <div class="col-6 col-md-2"><q-input v-model="filter.to" outlined dense type="date" stack-label label="По" /></div>
        <div class="col-12 col-md-5"><q-input v-model="filter.search" outlined dense clearable label="Пошук: № направлення, замовлення, пацієнт, направник" debounce="400" /></div>
      </div>
      <q-table :data="items" :columns="columns" row-key="orderId" dense flat :loading="loading" :pagination="{ rowsPerPage: 30 }" no-data-label="Направлень немає">
        <template v-slot:body-cell-order="props">
          <q-td :props="props"><router-link :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" class="text-primary">{{ props.row.orderNumber }}</router-link></q-td>
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

export default {
  name: 'ReferralJournal',
  mixins: [apiMixin],
  data () {
    return {
      filter: { from: daysAgoIso(30), to: todayIso(), type: null, search: '' },
      summary: [],
      items: [],
      columns: [
        { name: 'date', label: 'Дата', field: r => formatDateTime(r.orderDatetime), align: 'left', sortable: true },
        { name: 'order', label: '№ замовлення', field: 'orderNumber', align: 'left' },
        { name: 'type', label: 'Тип', field: 'referralTypeName', align: 'left' },
        { name: 'number', label: '№ направлення', field: r => r.referralNumber || '—', align: 'left' },
        { name: 'refDate', label: 'Дата направлення', field: r => formatDate(r.referralDate), align: 'left' },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'referrer', label: 'Направник', field: r => r.referrer || '—', align: 'left' },
        { name: 'ehealth', label: 'Статус ЕСОЗ', field: r => r.ehealthStatus || '—', align: 'left' },
        { name: 'status', label: 'Замовлення', field: 'status', align: 'left' },
        { name: 'sum', label: 'Сума', field: r => r.totalPrice, align: 'right', format: v => formatMoney(v) }
      ]
    };
  },
  watch: { filter: { deep: true, handler () { this.load(); } } },
  created () { this.load(); },
  methods: {
    typeColor (t) { return { EHEALTH: 'teal-6', PAPER: 'brown-5', INTERNAL: 'primary', EXTERNAL_CLINIC: 'purple-5', SELF: 'grey-6' }[t] || 'grey'; },
    async load () {
      const f = this.filter;
      const res = await this.callApi(() => this.$api.referralJournal({ from: f.from || null, to: f.to ? f.to + 'T23:59:59' : null, type: f.type, search: f.search || null }), { silent: false });
      if (res) { this.summary = res.summary; this.items = res.items; }
    }
  }
};
</script>
