<template>
  <div data-testid="clientsPage">
    <page-header title="Клієнти (клініки)" icon="fas fa-handshake" :breadcrumbs="[{ label: 'Клієнти' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="row q-col-gutter-md">
      <div class="col-12 col-lg-5">
        <div class="medlink-card">
          <div class="medlink-card__title"><span><q-icon name="business" class="q-mr-xs" />Клініки</span></div>
          <q-list separator>
            <q-item v-for="c in clients" :key="c.id" clickable :active="current && current.id === c.id" active-class="bg-blue-1" @click="select(c)">
              <q-item-section>
                <q-item-label><b>{{ c.name }}</b></q-item-label>
                <q-item-label caption>{{ c.organizationName }} · ЄДРПОУ {{ c.edrpou }} · договір {{ c.contractNumber || '—' }} · {{ c.priceListName }}</q-item-label>
              </q-item-section>
              <q-item-section side class="text-right">
                <div class="text-caption">направлень {{ c.orders }} (в роботі {{ c.openOrders }})</div>
                <div class="text-caption">не виставлено <b :class="c.notInvoiced > 0 ? 'text-deep-orange-8' : ''">{{ c.notInvoiced | money }}</b></div>
              </q-item-section>
            </q-item>
          </q-list>
          <empty-state v-if="!clients.length" title="Клієнтів ще немає" hint="Додайте платника з типом «Клініка-партнер» на сторінці «Прайси та платники»" icon="inbox" />
        </div>
      </div>

      <div v-if="!current && clients.length" class="col-12 col-lg-7">
        <div class="medlink-card"><empty-state title="Оберіть клініку зліва" hint="Тут з’являться її направлення, імпорт реєстру та акти за період" icon="inbox" /></div>
      </div>
      <div v-if="current" class="col-12 col-lg-7">
        <div class="medlink-card q-mb-md q-pa-sm">
          <div class="row items-center q-gutter-sm">
            <div class="text-subtitle1 text-weight-bold">{{ current.name }}</div>
            <q-space />
            <q-btn dense unelevated color="primary" icon="upload_file" label="Імпорт реєстру клініки" @click="$refs.file.click()" />
            <q-btn dense outline color="purple-6" icon="request_quote" label="Акт / рахунок за період" :disable="!orders.length" @click="act"><q-tooltip v-if="!orders.length">За вибраний період немає направлень — змініть дати</q-tooltip></q-btn>
          </div>
          <div class="text-caption text-grey-7 q-mt-xs">Файл CSV або XLSX з колонками: Прізвище; Ім’я; По батькові; Дата народження; Стать; Телефон; Номер у клініки; Лікар; Дослідження (коди через кому); Терміново. <a class="text-primary cursor-pointer" data-testid="registrySample" @click="downloadSample">Завантажити зразок файлу</a></div>
          <div class="row q-col-gutter-sm q-mt-xs">
            <div class="col-6 col-md-3"><q-input v-model="from" dense outlined type="date" stack-label label="Дата від" /></div>
            <div class="col-6 col-md-3"><q-input v-model="to" dense outlined type="date" stack-label label="Дата до" /></div>
          </div>
        </div>

        <div v-if="importResult" class="medlink-card q-mb-md q-pa-sm">
          <div class="text-subtitle2">{{ importResult.dryRun ? 'Перевірка реєстру' : 'Реєстр імпортовано' }}: рядків {{ importResult.total }}, коректних {{ importResult.ok }}, помилок {{ importResult.errors }}</div>
          <q-markup-table dense flat bordered class="q-mt-xs">
            <thead><tr><th>Рядок</th><th class="text-left">Пацієнт</th><th class="text-left">Номер направлення клініки</th><th class="text-left">Дослідження</th><th class="text-left">Статус</th></tr></thead>
            <tbody>
              <tr v-for="r in importResult.rows" :key="r.line">
                <td>{{ r.line }}</td>
                <td>{{ r.lastName }} {{ r.firstName }} <q-badge v-if="r.newPatient" color="teal-5" label="новий" /></td>
                <td>{{ r.clinicNumber }}</td>
                <td>{{ r.codes.join(', ') }}</td>
                <td>
                  <span v-if="r.status === 'ERROR'" class="text-negative">{{ r.message }}</span>
                  <router-link v-else-if="r.orderId" :to="{ name: 'lab-order-card', params: { id: r.orderId } }" class="text-primary">{{ r.orderNumber }}</router-link>
                  <span v-else class="text-positive">готово до створення</span>
                </td>
              </tr>
            </tbody>
          </q-markup-table>
          <q-btn v-if="importResult.dryRun && importResult.ok" class="q-mt-sm" unelevated color="primary" icon="check" :label="`Створити направлення: ${importResult.ok}`" @click="importFile(false)" />
        </div>

        <div class="medlink-card q-pa-sm">
          <q-table :data="orders" :columns="columns" row-key="id" dense flat :pagination="{ rowsPerPage: 20 }" no-data-label="За вибраний період направлень немає. Змініть дати." title="Направлення за період" rows-per-page-label="Рядків на сторінці:">
            <template v-slot:body-cell-order="props"><q-td :props="props"><router-link :to="{ name: 'lab-order-card', params: { id: props.row.id } }" class="text-primary">{{ props.row.orderNumber }}</router-link></q-td></template>
            <template v-slot:body-cell-status="props"><q-td :props="props"><status-chip :value="props.row.status" type="order" /></q-td></template>
            <template v-slot:body-cell-ready="props"><q-td :props="props">{{ props.row.testsReady }}/{{ props.row.testsTotal }} <q-icon v-if="props.row.reportUrl" name="picture_as_pdf" color="red-6" /></q-td></template>
          </q-table>
          <div v-if="invoices.length" class="q-mt-sm text-caption">Рахунки/акти:
            <a v-for="i in invoices" :key="i.id" :href="i.printUrl" target="_blank" class="q-mr-md text-primary">{{ i.number }} — {{ i.amount | money }}</a>
          </div>
        </div>
      </div>
    </div>
    <input ref="file" type="file" accept=".csv,.xlsx" style="display:none" @change="onFile">
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import { formatDateTime, formatMoney, daysAgoIso, todayIso } from '../../utils/format';
import { http } from '../../services/http';

export default {
  name: 'ClientsPage',
  mixins: [apiMixin],
  data () {
    return {
      clients: [], current: null, orders: [], invoices: [], importResult: null, file: null, from: daysAgoIso(30), to: todayIso(),
      columns: [
        { name: 'date', label: 'Дата', field: r => formatDateTime(r.orderDatetime), align: 'left' },
        { name: 'order', label: '№ направлення', field: 'orderNumber', align: 'left' },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'clinic', label: 'Номер направлення клініки', field: r => r.clinicNumber || '—', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' },
        { name: 'ready', label: 'Готово', field: 'testsReady', align: 'left' },
        { name: 'sum', label: 'До сплати клінікою', field: 'payerAmount', align: 'right', format: v => formatMoney(v) }
      ]
    };
  },
  watch: { from () { this.loadOrders(); }, to () { this.loadOrders(); } },
  created () { this.load(); },
  methods: {
    async load () {
      const res = await this.callApi(() => http.get('/clients').then(r => r.data));
      if (res) { this.clients = res; if (!this.current && res.length) this.select(res[0]); }
    },
    async select (c) { this.current = c; this.importResult = null; await this.loadOrders(); },
    async loadOrders () {
      if (!this.current) return;
      const [o, i] = await Promise.all([
        http.get(`/clients/${this.current.id}/orders`, { params: { from: this.from, to: this.to } }).then(r => r.data),
        http.get(`/payers/${this.current.id}/invoices`).then(r => r.data)
      ]);
      this.orders = o.items; this.invoices = i;
    },
    downloadSample () {
      const head = '﻿Прізвище;Ім’я;По батькові;Дата народження;Стать;Телефон;Номер у клініки;Лікар;Дослідження;Терміново';
      const row = 'Шевченко;Олена;Петрівна;15.03.1985;Ж;+380501234567;К-0001;Коваль І.М.;GLU,HGB;ні';
      const csv = head + '\r\n' + row + '\r\n';
      const a = document.createElement('a');
      a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
      a.download = 'зразок_реєстру_клініки.csv';
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 2000);
    },
    onFile (e) { this.file = e.target.files[0]; e.target.value = ''; if (this.file) this.importFile(true); },
    async importFile (dryRun) {
      const fd = new FormData(); fd.append('file', this.file);
      const res = await this.callApi(() => http.post(`/clients/${this.current.id}/registry`, fd, { params: { dryRun }, headers: { 'Content-Type': 'multipart/form-data' } }).then(r => r.data));
      if (res) { this.importResult = res; if (!dryRun) { this.load(); this.loadOrders(); } }
    },
    async act () {
      const res = await this.callApi(() => http.post(`/payers/${this.current.id}/invoices/period`, { from: this.from, to: this.to }).then(r => r.data), { success: 'Акт сформовано' });
      if (res) { window.open(res.printUrl, '_blank'); this.load(); this.loadOrders(); }
    }
  }
};
</script>
