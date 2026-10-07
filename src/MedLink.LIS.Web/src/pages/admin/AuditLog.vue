<template>
  <div class="audit-page" data-testid="auditPage">
    <page-header title="Аудит" icon="fas fa-history" subtitle="lab_audit_log: хто, що, коли змінив; стан до/після (JSON diff), IP" :breadcrumbs="[{ label: 'Адміністрування' }, { label: 'Аудит' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-6 col-md-2"><q-select v-model="filters.entity" dense outlined clearable label="Сутність" :options="entities" use-input new-value-mode="add-unique" @input="apply" /></div>
      <div class="col-6 col-md-3"><q-input v-model="filters.entityId" dense outlined clearable label="ID сутності" @keyup.enter="apply" @clear="apply" /></div>
      <div class="col-6 col-md-3"><q-select v-model="filters.userId" dense outlined clearable label="Користувач" :options="employeeOptions" emit-value map-options @input="apply" /></div>
      <div class="col-6 col-md-2"><q-input v-model="filters.from" dense outlined type="date" stack-label label="З" @input="apply" /></div>
      <div class="col-6 col-md-2"><q-input v-model="filters.to" dense outlined type="date" stack-label label="По" @input="apply" /></div>
    </div>

    <div class="row q-col-gutter-md">
      <div class="col-12 col-lg-7">
        <div class="medlink-card">
          <q-table :data="rows" :columns="columns" row-key="id" dense flat :loading="loading" :pagination.sync="pagination" :rows-per-page-options="[25, 50, 100]" @request="onRequest" no-data-label="Подій аудиту немає" data-testid="auditTable">
            <template v-slot:body="props">
              <q-tr :props="props" :class="{ 'row-selected': active && active.id === props.row.id }" class="cursor-pointer" @click="active = props.row">
                <q-td key="at" :props="props">{{ props.row.at | datetime }}</q-td>
                <q-td key="user" :props="props">{{ userName(props.row.userId) }}</q-td>
                <q-td key="action" :props="props"><q-badge :color="actionColor(props.row.action)" :label="props.row.action" /></q-td>
                <q-td key="entity" :props="props">{{ props.row.entity }}</q-td>
                <q-td key="entityId" :props="props" class="mono text-caption">{{ shortId(props.row.entityId) }}</q-td>
                <q-td key="ip" :props="props" class="text-caption">{{ props.row.ip }}</q-td>
              </q-tr>
            </template>
          </q-table>
        </div>
      </div>
      <div class="col-12 col-lg-5">
        <div class="medlink-card" data-testid="auditDiff">
          <div class="medlink-card__title"><span>Зміни (до / після)</span><span v-if="active" class="text-caption text-grey-6">{{ active.entity }} · {{ active.action }}</span></div>
          <div v-if="active" class="q-pa-sm">
            <div class="text-caption q-mb-sm">{{ active.at | datetime }} · {{ userName(active.userId) }} · <span class="mono">{{ active.entityId }}</span>
              <q-btn v-if="active.entity === 'LabOrder'" flat dense size="sm" color="primary" label="Відкрити замовлення" :to="{ name: 'lab-order-card', params: { id: active.entityId } }" />
            </div>
            <json-diff-viewer :before="active.before" :after="active.after" />
          </div>
          <empty-state v-else title="Оберіть подію" icon="compare" />
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import JsonDiffViewer from '../../components/common/JsonDiffViewer.vue';
import { daysAgoIso, todayIso } from '../../utils/format';

export default {
  name: 'AuditLog',
  mixins: [apiMixin],
  components: { JsonDiffViewer },
  data () {
    return {
      rows: [], active: null,
      filters: { entity: null, entityId: '', userId: null, from: daysAgoIso(7), to: todayIso() },
      entities: ['LabOrder', 'LabOrderSample', 'LabOrderTest', 'LabTestResult', 'LabPanicCall', 'LabQcResult', 'LabAnalyzerLockout', 'LabQcMaterial', 'LabAnalyzer', 'LabConnectorInstallation', 'LabReferenceLayer', 'LabTestProfile', 'LabTestDefinition', 'LabArchiveCell', 'LabReagentLot', 'LabCultureOrder', 'LabSampleLogistics', 'LabSettings', 'OrgEmployee'],
      pagination: { page: 1, rowsPerPage: 50, rowsNumber: 0 },
      columns: [
        { name: 'at', label: 'Час', field: 'at', align: 'left' },
        { name: 'user', label: 'Користувач', align: 'left' },
        { name: 'action', label: 'Дія', field: 'action', align: 'left' },
        { name: 'entity', label: 'Сутність', field: 'entity', align: 'left' },
        { name: 'entityId', label: 'ID', field: 'entityId', align: 'left' },
        { name: 'ip', label: 'IP', field: 'ip', align: 'left' }
      ]
    };
  },
  computed: {
    employees () { return this.$store.state.context.employees; },
    employeeOptions () { return this.employees.map(e => ({ value: e.id, label: e.fullName || e.name })); }
  },
  created () {
    if (this.$route.query.entity) this.filters.entity = this.$route.query.entity;
    if (this.$route.query.entityId) this.filters.entityId = this.$route.query.entityId;
    this.load();
  },
  methods: {
    shortId (id) { return id ? String(id).slice(0, 8) : ''; },
    userName (id) { const e = this.employees.find(x => x.id === id); return e ? (e.fullName || e.name) : (id ? String(id).slice(0, 8) : 'система'); },
    actionColor (a) { const k = String(a || '').toUpperCase(); if (k.includes('DELETE') || k.includes('REJECT') || k.includes('CANCEL')) return 'negative'; if (k.includes('CREATE')) return 'positive'; if (k.includes('VERIFY') || k.includes('RELEASE')) return 'green-8'; return 'primary'; },
    apply () { this.pagination.page = 1; this.load(); },
    onRequest (props) { this.pagination = { ...this.pagination, ...props.pagination }; this.load(); },
    async load () {
      const res = await this.callApi(() => this.$api.audit({ ...this.filters, entityId: this.filters.entityId || null, page: this.pagination.page, pageSize: this.pagination.rowsPerPage }));
      if (res === undefined) return;
      this.rows = this.asList(res);
      this.pagination.rowsNumber = this.asTotal(res, this.rows.length);
    }
  }
};
</script>
