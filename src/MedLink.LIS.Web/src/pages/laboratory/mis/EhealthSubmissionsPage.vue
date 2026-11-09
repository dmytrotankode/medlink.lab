<!--
  ТЗ 21.13 (FR-SGN-007…012): «Відправка в ЕСОЗ» — вихідні задачі (outbox): відправка звіту, погашення направлення, скасування звіту.
  Стан, спроби, наступна спроба, job, остання помилка; «Повторити» і «Скасувати» — лише там, де сервер дозволяє (canRetry / canCancel).
-->
<template>
  <div data-testid="ehealthSubmissionsPage">
    <page-header title="Відправка в ЕСОЗ" icon="fas fa-cloud-upload-alt" :breadcrumbs="[{ label: 'Відправка в ЕСОЗ' }]">
      <q-badge v-if="mode" :color="mode === 'LIVE' ? 'deep-orange-6' : 'grey-7'" :label="mode === 'LIVE' ? 'Бойовий режим' : 'Тестовий режим'" class="q-mr-sm" data-testid="ehealthMode">
        <q-tooltip>{{ mode === 'LIVE' ? 'Звіти передаються в реальний ЕСОЗ' : 'Звіти не передаються в реальний ЕСОЗ' }}</q-tooltip>
      </q-badge>
      <q-btn v-if="mode && mode !== 'LIVE' && canRunNow" flat dense color="primary" icon="play_arrow" label="Обробити зараз" :loading="running" class="q-mr-sm" data-testid="ehealthRunNow" @click="runNow">
        <q-tooltip>Запустити обробку черги відправки зараз</q-tooltip>
      </q-btn>
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" data-testid="ehealthRefresh" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center" data-testid="ehealthFilters">
      <div class="col-12 col-md-3">
        <q-select v-model="filters.status" dense outlined clearable emit-value map-options :options="statusOptions" label="Статус" data-testid="ehealthStatusFilter" />
      </div>
      <div class="col-6 col-md-2"><q-input v-model="filters.from" dense outlined type="date" stack-label label="Дата з" data-testid="ehealthDateFrom" /></div>
      <div class="col-6 col-md-2"><q-input v-model="filters.to" dense outlined type="date" stack-label label="Дата по" data-testid="ehealthDateTo" /></div>
      <div class="col-12 col-md-5">
        <q-input v-model="filters.search" dense outlined clearable debounce="400" label="Номер направлення, замовлення або пацієнт" data-testid="ehealthSearch">
          <template v-slot:prepend><q-icon name="search" /></template>
        </q-input>
      </div>
    </div>

    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="key" dense flat :loading="loading" :pagination.sync="pagination" :rows-per-page-options="[25, 50, 100]" no-data-label="Задач немає" data-testid="ehealthTable">
        <template v-slot:body-cell-order="props">
          <q-td :props="props">
            <router-link :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" class="text-primary" :data-testid="`ehealthOrderLink-${props.row.key}`">{{ props.row.orderNumber }}</router-link>
          </q-td>
        </template>
        <template v-slot:body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="color(props.row.status)" :label="props.row.statusLabel" :data-testid="`ehealthStatus-${props.row.key}`" />
          </q-td>
        </template>
        <template v-slot:body-cell-error="props">
          <q-td :props="props" class="text-negative text-caption" style="max-width: 360px; white-space: normal" :data-testid="`ehealthError-${props.row.key}`">{{ props.row.lastError }}</q-td>
        </template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn v-if="props.row.canRetry && canSubmit" flat dense size="sm" color="primary" icon="replay" label="Повторити" :data-testid="`ehealthRetry-${props.row.key}`" @click="retry(props.row)" />
            <q-btn v-if="props.row.canCancel && canSubmit" flat dense size="sm" color="negative" icon="close" label="Скасувати" :data-testid="`ehealthCancel-${props.row.key}`" @click="cancel(props.row)" />
            <q-btn v-if="props.row.notSent && canSubmit" flat dense size="sm" color="primary" icon="draw" label="Підписати в картці" :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" :data-testid="`ehealthOpen-${props.row.key}`">
              <q-tooltip>Відкрити замовлення {{ props.row.orderNumber }} для підпису й відправки</q-tooltip>
            </q-btn>
            <span v-else-if="props.row.notSent" class="text-caption text-grey-6">Підпис — лікар-лаборант</span>
          </q-td>
        </template>
      </q-table>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';
import { ACTION_ROLES } from '../../../utils/menuAccess';

const COLORS = { NEW: 'primary', SENDING: 'primary', WAITING_JOB: 'primary', DONE: 'positive', FAILED: 'negative', DEAD: 'grey-8', NOT_SENT: 'grey-6' };

export default {
  name: 'EhealthSubmissionsPage',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      rows: [], mode: '', running: false, pagination: { rowsPerPage: 25 },
      filters: { status: null, from: '', to: '', search: '' },
      statusOptions: [
        { value: 'NOT_SENT', label: 'Не надіслано' }, { value: 'NEW', label: 'У черзі' }, { value: 'SENDING', label: 'Відправляється' }, { value: 'WAITING_JOB', label: 'Очікує обробки в ЕСОЗ' },
        { value: 'DONE', label: 'Виконано' }, { value: 'FAILED', label: 'Помилка' }, { value: 'DEAD', label: 'Зупинено' }
      ],
      columns: [
        { name: 'order', label: 'Замовлення', field: 'orderNumber', align: 'left', sortable: true },
        { name: 'referral', label: 'Направлення', field: 'referralNumber', align: 'left' },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'kind', label: 'Дія', field: 'kindLabel', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' },
        { name: 'attempts', label: 'Спроби', field: r => r.attempts || '', align: 'right' },
        { name: 'next', label: 'Наступна спроба', field: r => (r.nextAttemptAt ? this.$options.filters.datetime(r.nextAttemptAt) : ''), align: 'left' },
        { name: 'job', label: 'Номер завдання', field: r => (r.jobId ? r.jobId.slice(0, 10) : ''), align: 'left', classes: 'mono text-caption' },
        { name: 'error', label: 'Остання помилка', field: 'lastError', align: 'left' },
        { name: 'actions', label: '', field: 'key' }
      ]
    };
  },
  computed: {
    canSubmit () { return this.canDo(...ACTION_ROLES.ehealthSign); },
    canRunNow () { return this.canDo(...ACTION_ROLES.ehealthProcessNow); }
  },
  watch: { filters: { deep: true, handler () { this.load(); } } },
  created () { this.load(); },
  methods: {
    color (s) { return COLORS[s] || 'grey-6'; },
    async load () {
      const f = this.filters;
      const r = await this.callApi(() => this.$api.ehealthSubmissions({ status: f.status, from: f.from, to: f.to, search: f.search }));
      if (!r) return;
      this.mode = r.mode || '';
      const notSent = (r.notSent || []).map(o => ({
        key: `ns-${o.id}`, orderId: o.id, orderNumber: o.orderNumber, patientName: o.patientName, kindLabel: 'Відправка звіту', status: 'NOT_SENT', statusLabel: 'Не надіслано', notSent: true
      }));
      this.rows = [...notSent, ...this.asList(r).map(x => ({ ...x, key: x.id }))];
    },
    async retry (row) {
      try {
        await this.$api.ehealthRetryTask(row.id);
        this.notifyOk('Задачу повернуто в чергу');
      } catch (e) {
        this.notifyError(e);
        if (e.apiStatus === 401 || e.apiStatus === 403) this.$root.$emit('ehealth-login');
      }
      this.load();
    },
    cancel (row) {
      this.$q.dialog({ title: 'Скасувати відправку?', message: 'Підписаний звіт збережеться, відправити його можна повторно кнопкою «Повторити».', cancel: 'Не скасовувати', ok: 'Скасувати відправку', persistent: true })
        .onOk(async () => {
          try {
            await this.$api.ehealthCancelTask(row.id);
            this.notifyOk('Задачу скасовано');
          } catch (e) {
            this.notifyError(e);
          }
          this.load();
        });
    },
    async runNow () {
      this.running = true;
      try {
        await this.$api.ehealthRunOutbox();
      } catch (e) {
        this.notifyError(e);
      } finally {
        this.running = false;
        this.load();
      }
    }
  }
};
</script>
