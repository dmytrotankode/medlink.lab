<!--
  ТЗ 21.13 (FR-SGN-007…012): «Відправка в ЕСОЗ» — видані звіти, що ще не надсилались, черга повторів, помилки,
  зареєстровані й анульовані звіти; відправка з картки замовлення (підпис КЕП).
-->
<template>
  <div data-testid="ehealthSubmissionsPage">
    <page-header title="Відправка в ЕСОЗ" icon="fas fa-cloud-upload-alt" subtitle="Діагностичні звіти (DiagnosticReport): не надіслані, у черзі повторів, з помилкою, зареєстровані, анульовані" :breadcrumbs="[{ label: 'Відправка в ЕСОЗ' }]">
      <q-select v-model="status" dense outlined clearable emit-value map-options :options="statusOptions" label="Статус" style="min-width: 220px" class="q-mr-sm" />
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="key" dense flat :pagination="{ rowsPerPage: 30 }" no-data-label="Звітів немає">
        <template v-slot:body-cell-order="props">
          <q-td :props="props"><router-link :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" class="text-primary">{{ props.row.orderNumber }}</router-link></q-td>
        </template>
        <template v-slot:body-cell-status="props">
          <q-td :props="props"><q-badge :color="color(props.row.status)" :label="props.row.label" /><div v-if="props.row.lastError" class="text-caption text-negative">{{ props.row.lastError }}</div></q-td>
        </template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn v-if="['ERROR', 'AUTH_REQUIRED'].includes(props.row.status)" flat dense size="sm" color="primary" icon="replay" label="Повторити" @click="retry(props.row)" />
            <q-btn v-else-if="['NOT_SENT', 'READY_TO_SIGN'].includes(props.row.status)" flat dense size="sm" color="primary" icon="draw" label="Підписати в картці" :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" />
          </q-td>
        </template>
      </q-table>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';

const COLORS = { NOT_SENT: 'grey-6', READY_TO_SIGN: 'orange-7', SIGNED: 'orange-7', PROCESSING: 'primary', REGISTERED: 'teal-6', COMPLETED: 'positive', ERROR: 'negative', AUTH_REQUIRED: 'deep-orange-6', ANNULLED: 'grey-7' };

export default {
  name: 'EhealthSubmissionsPage',
  mixins: [apiMixin],
  data () {
    return {
      status: null, rows: [],
      statusOptions: [
        { value: 'NOT_SENT', label: 'Не надіслано' }, { value: 'READY_TO_SIGN', label: 'Готово до підпису' }, { value: 'ERROR', label: 'Помилка (черга повторів)' },
        { value: 'AUTH_REQUIRED', label: 'Потрібен вхід в ЕСОЗ' }, { value: 'REGISTERED', label: 'Зареєстровано' }, { value: 'COMPLETED', label: 'Направлення погашено' }, { value: 'ANNULLED', label: 'Анульовано' }
      ],
      columns: [
        { name: 'order', label: 'Замовлення', field: 'orderNumber', align: 'left', sortable: true },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'kind', label: 'Дія', field: r => r.kind === 'ANNUL' ? 'Анулювання' : 'Відправка', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' },
        { name: 'signed', label: 'Підписав', field: r => r.signedByName ? `${r.signedByName} · ${this.$options.filters.datetime(r.signedOn)}` : '', align: 'left', classes: 'text-caption' },
        { name: 'attempts', label: 'Спроб', field: r => r.attempts || '', align: 'right' },
        { name: 'next', label: 'Наступна спроба', field: r => r.nextAttemptAt ? this.$options.filters.datetime(r.nextAttemptAt) : '', align: 'left' },
        { name: 'actions', label: '', field: 'key' }
      ]
    };
  },
  watch: { status () { this.load(); } },
  created () { this.load(); },
  methods: {
    color (s) { return COLORS[s] || 'grey-6'; },
    async load () {
      const r = await this.callApi(() => this.$api.ehealthSubmissions(this.status));
      if (!r) return;
      const notSent = !this.status || this.status === 'NOT_SENT'
        ? (r.notSent || []).map(o => ({ orderId: o.id, orderNumber: o.orderNumber, patientName: o.patientName, kind: 'SUBMIT', status: 'NOT_SENT', label: 'Не надіслано' }))
        : [];
      this.rows = [...notSent, ...this.asList(r)].map((x, i) => ({ ...x, key: x.id || `${x.orderId}-${i}` }));
    },
    async retry (row) {
      try {
        await this.$api.ehealthRetry(row.orderId);
        this.$q.notify({ type: 'positive', message: 'Повтор відправки виконано' });
      } catch (e) {
        this.notifyError(e);
        if (e.apiStatus === 401 || e.apiStatus === 403) this.$root.$emit('ehealth-login');
      }
      this.load();
    }
  }
};
</script>
