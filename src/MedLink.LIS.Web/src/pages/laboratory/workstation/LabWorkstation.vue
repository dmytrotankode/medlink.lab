<template>
  <div class="workstation-page" data-testid="workstationPage" tabindex="0" @keydown.enter.exact="onEnter" @keydown.esc="onEsc">
    <page-header title="Робочий стіл лаборанта" icon="fas fa-microscope" subtitle="Журнал досліджень: прийом результатів з аналізаторів, ручне введення, delta-check, автоверифікація" :breadcrumbs="[{ label: 'Робочий стіл' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
      <q-btn outline dense color="primary" icon="add" label="Нове замовлення" data-testid="wsCreateOrder" @click="createOpen = true" />
      <q-btn outline dense color="primary" icon="playlist_add" :label="selected.length ? `Батч (${selected.length})` : 'Батчі'" @click="batchOpen = true" />
      <q-btn unelevated dense color="teal-7" icon="verified" label="Автоверифікація" data-testid="wsAutoverify" @click="autoverify" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <!-- Зона A: тулбар фільтрів -->
    <div class="medlink-card q-pa-sm q-mb-sm">
      <div class="row q-col-gutter-sm items-center">
        <div class="col-12 col-md-3">
          <q-input v-model="filters.search" dense outlined clearable placeholder="Штрихкод, пацієнт, тест, № замовлення" data-testid="wsSearch" @keyup.enter="load" @clear="load">
            <template v-slot:prepend><q-icon name="search" /></template>
          </q-input>
        </div>
        <div class="col-6 col-md-2"><q-select v-model="filters.sectionId" dense outlined clearable label="Підрозділ" :options="sectionOptions" emit-value map-options data-testid="wsSection" @input="load" /></div>
        <div class="col-6 col-md-1"><q-select v-model="filters.status" dense outlined clearable label="Статус" :options="statusOptions" emit-value map-options @input="load" /></div>
        <div class="col-6 col-md-2"><q-select v-model="filters.analyzerId" dense outlined clearable label="Аналізатор" :options="analyzerOptions" emit-value map-options @input="load" /></div>
        <div class="col-6 col-md-1"><q-select v-model="filters.flag" dense outlined clearable label="Прапорець" :options="flagOptions" emit-value map-options @input="load" /></div>
        <div class="col-6 col-md-1"><q-toggle v-model="filters.cito" label="CITO" color="deep-orange-6" keep-color @input="load" /></div>
        <div class="col-12 col-md-2 row justify-end q-gutter-xs">
          <q-chip dense outline color="primary" icon="assignment">{{ rows.length }} ряд.</q-chip>
          <q-chip dense outline color="negative" icon="warning">Паніка {{ summary.panic || 0 }}</q-chip>
          <q-chip dense outline color="warning" text-color="dark" icon="rate_review">Лікар {{ summary.needsReview || 0 }}</q-chip>
        </div>
      </div>
    </div>

    <!-- Зона B: черга -->
    <div class="medlink-card q-mb-sm">
      <worklist-table
        ref="table"
        :rows="rows"
        :loading="loading"
        :selected.sync="selected"
        :selected-id="selectedRow && selectedRow.orderTestId"
        height="50vh"
        @select="selectRow"
        @saved="onSaved"
        @verify="openVerify"
        @rerun="rerun"
        @reject="askReject"
        @delete="askDelete"
        @open-order="openOrder"
      />
      <div class="row items-center q-px-sm q-py-xs text-caption text-grey-7 bg-grey-1">
        <span>Подвійний клік або Enter — введення; Enter — зберегти; Tab — наступний; Esc — скасувати. Віртуальний скрол: {{ rows.length }} рядків{{ total > rows.length ? ` з ${total}` : '' }}.</span>
        <q-space />
        <q-btn v-if="selected.length" flat dense size="sm" color="green-8" icon="done_all" :label="`Верифікувати обрані (${selected.length})`" @click="verifySelected" />
        <q-btn v-if="selected.length" flat dense size="sm" color="grey-8" icon="clear" label="Зняти вибір" @click="selected = []" />
      </div>
    </div>

    <!-- Зона C: деталі -->
    <worklist-detail
      :row="selectedRow"
      :order="selectedOrder"
      @edit="openEditor"
      @verify="openVerify"
      @autoverify="autoverifyOne"
      @history="showHistory"
      @open-order="openOrder"
      @order-action="onOrderAction"
    />

    <result-editor-dialog v-model="editorOpen" :row="activeRow" @saved="onSaved" />
    <verify-result-dialog v-model="verifyOpen" :row="activeRow" @verified="load" />
    <batch-dialog v-model="batchOpen" :items="selected" @created="selected = []" />
    <create-order-dialog v-model="createOpen" @created="onOrderCreated" />
    <confirm-dialog v-model="rejectOpen" title="Відхилити результат" :message="`Результат ${activeRow.testCode} (${activeRow.barcode}) буде відхилено.`" ok-label="Відхилити" color="negative" icon="block" with-reason reason-required @confirm="doReject" />
    <confirm-dialog v-model="deleteOpen" title="Видалити результат" :message="`Запис результату ${activeRow.testCode} буде видалено; тест повернеться до PENDING.`" ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
    <q-dialog v-model="historyOpen">
      <q-card style="min-width: 640px; max-width: 96vw">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm"><div class="text-subtitle1">Історія результату {{ activeRow.testCode }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section>
          <q-table :data="history" :columns="historyColumns" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" row-key="version" no-data-label="Історії немає" />
        </q-card-section>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import { mapState } from 'vuex';
import apiMixin from '../../../mixins/apiMixin';
import WorklistTable from './WorklistTable.vue';
import WorklistDetail from './WorklistDetail.vue';
import BatchDialog from './BatchDialog.vue';
import ResultEditorDialog from '../../../components/results/ResultEditorDialog.vue';
import VerifyResultDialog from '../../../components/results/VerifyResultDialog.vue';
import CreateOrderDialog from '../orders/CreateOrderDialog.vue';
import { TEST_STATUS, FLAG, toOptions } from '../../../utils/statuses';
import { formatNumber, formatDateTime } from '../../../utils/format';

export default {
  name: 'LabWorkstation',
  mixins: [apiMixin],
  components: { WorklistTable, WorklistDetail, BatchDialog, ResultEditorDialog, VerifyResultDialog, CreateOrderDialog },
  data () {
    return {
      rows: [],
      total: 0,
      selected: [],
      selectedRow: null,
      selectedOrder: null,
      activeRow: {},
      filters: { search: '', status: null, analyzerId: null, flag: null, cito: false, sectionId: null },
      sections: [],
      statusOptions: toOptions(TEST_STATUS),
      flagOptions: toOptions(FLAG),
      editorOpen: false, verifyOpen: false, batchOpen: false, createOpen: false, rejectOpen: false, deleteOpen: false, historyOpen: false,
      history: [],
      historyColumns: [
        { name: 'version', label: 'Версія', field: 'version', align: 'left' },
        { name: 'value', label: 'Значення', field: r => r.numericValue !== null && r.numericValue !== undefined ? formatNumber(r.numericValue) : (r.stringValue || r.value || '—'), align: 'right' },
        { name: 'flag', label: 'Прапорець', field: 'flag', align: 'left' },
        { name: 'at', label: 'Коли', field: r => formatDateTime(r.enteredAt || r.at), align: 'left' },
        { name: 'by', label: 'Хто', field: r => r.enteredByName || r.enteredById || r.userId || '—', align: 'left' },
        { name: 'comment', label: 'Коментар', field: r => r.operatorComment || r.verificationComment || r.comment || '', align: 'left' }
      ]
    };
  },
  computed: {
    ...mapState('laboratory', ['summary', 'analyzers']),
    analyzerOptions () { return this.analyzers.map(a => ({ value: a.id, label: a.name })); },
    sectionOptions () { return this.sections.map(s => ({ value: s.id, label: s.name })); }
  },
  created () {
    const q = this.$route.query;
    if (q.search) this.filters.search = q.search;
    if (q.status) this.filters.status = q.status;
    if (q.flag) this.filters.flag = q.flag;
    if (q.sectionId) this.filters.sectionId = q.sectionId;
    this.$store.dispatch('dictionaries/load', 'tests');
    this.$api.sections().then(r => { this.sections = this.asList(r); }).catch(() => {});
    this.load();
  },
  methods: {
    async load () {
      const res = await this.callApi(() => this.$api.getWorklist({
        status: this.filters.status,
        analyzerId: this.filters.analyzerId,
        flag: this.filters.flag,
        cito: this.filters.cito ? true : null,
        sectionId: this.filters.sectionId,
        search: this.filters.search,
        page: 1,
        pageSize: 2000
      }));
      if (res === undefined) return;
      this.rows = this.asList(res);
      this.total = this.asTotal(res, this.rows.length);
      if (this.selectedRow) {
        const fresh = this.rows.find(r => r.orderTestId === this.selectedRow.orderTestId);
        this.selectedRow = fresh || null;
      }
      this.$store.dispatch('laboratory/refreshStatus');
    },
    async selectRow (row) {
      this.selectedRow = row;
      if (row && row.orderId && (!this.selectedOrder || this.selectedOrder.id !== row.orderId)) {
        try { this.selectedOrder = await this.$api.getOrder(row.orderId); } catch (e) { this.selectedOrder = null; }
      } else if (row && !row.orderId) {
        this.selectedOrder = null;
      }
    },
    onEnter (e) {
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes((e.target.tagName || '').toUpperCase())) return;
      if (this.$refs.table) this.$refs.table.editSelected();
    },
    onEsc () { if (this.$refs.table) this.$refs.table.cancelEdit(); },
    onSaved (payload) {
      const result = payload && payload.result ? payload.result : payload;
      if (result && result.orderTestId) {
        const idx = this.rows.findIndex(r => r.orderTestId === result.orderTestId);
        if (idx >= 0) this.$set(this.rows, idx, { ...this.rows[idx], ...result });
        if (this.selectedRow && this.selectedRow.orderTestId === result.orderTestId) this.selectedRow = this.rows[idx];
        if (result.flag === 'CRIT_LOW' || result.flag === 'CRIT_HIGH') {
          this.$q.notify({ type: 'negative', icon: 'warning', message: `ПАНІКА: ${result.testCode} = ${formatNumber(result.value)} ${result.unit || ''} — потрібен дзвінок лікарю`, timeout: 8000, actions: [{ label: 'Паніка', color: 'white', handler: () => this.$router.push({ name: 'lab-validation', query: { tab: 'panic' } }) }] });
        }
      } else {
        this.load();
      }
      this.$store.dispatch('laboratory/refreshStatus');
    },
    openEditor (row) { this.activeRow = row; this.editorOpen = true; },
    openVerify (row) { this.activeRow = row; this.verifyOpen = true; },
    async rerun (row) {
      try { await this.$api.rerunResult(row.orderTestId); this.notifyOk(`${row.testCode} → повтор`); this.load(); } catch (e) { this.notifyError(e); }
    },
    askReject (row) { this.activeRow = row; this.rejectOpen = true; },
    async doReject (reason) {
      try { await this.$api.rejectResult(this.activeRow.orderTestId, reason); this.notifyOk('Результат відхилено'); this.load(); } catch (e) { this.notifyError(e); }
    },
    askDelete (row) { this.activeRow = row; this.deleteOpen = true; },
    async doDelete () {
      try { await this.$api.deleteResult(this.activeRow.orderTestId); this.notifyOk('Результат видалено'); this.load(); } catch (e) { this.notifyError(e); }
    },
    async autoverify () {
      try {
        const ids = this.selected.length ? this.selected.map(r => r.orderTestId) : undefined;
        const res = await this.$api.autoverify(ids);
        const blocked = (res && res.blocked) || [];
        this.$q.notify({ type: blocked.length ? 'warning' : 'positive', message: `Автоверифіковано: ${(res && res.verified) || 0}; заблоковано: ${blocked.length}${blocked.length ? ' — ' + blocked.slice(0, 3).map(b => b.reason).join('; ') : ''}`, timeout: 6000 });
        this.load();
      } catch (e) { this.notifyError(e); }
    },
    async autoverifyOne (row) {
      try {
        const res = await this.$api.autoverify([row.orderTestId]);
        const blocked = (res && res.blocked) || [];
        this.$q.notify({ type: blocked.length ? 'warning' : 'positive', message: blocked.length ? `Заблоковано: ${blocked[0].reason}` : 'Автоверифіковано' });
        this.load();
      } catch (e) { this.notifyError(e); }
    },
    async verifySelected () {
      try {
        const res = await this.$api.verifyBatch(this.selected.map(r => r.orderTestId));
        const skipped = (res && res.skipped) || [];
        this.$q.notify({ type: skipped.length ? 'warning' : 'positive', message: `Верифіковано: ${(res && res.verified) || 0}; пропущено: ${skipped.length}${skipped.length ? ' — ' + skipped.slice(0, 3).map(s => s.reason).join('; ') : ''}`, timeout: 6000 });
        this.selected = [];
        this.load();
      } catch (e) { this.notifyError(e); }
    },
    async showHistory (row) {
      this.activeRow = row; this.historyOpen = true; this.history = [];
      try { this.history = this.asList(await this.$api.resultHistory(row.orderTestId)); } catch (e) { this.notifyError(e); }
    },
    openOrder (row) { if (row.orderId) this.$router.push({ name: 'lab-order-card', params: { id: row.orderId } }); else this.$q.notify({ type: 'warning', message: 'Рядок не містить orderId' }); },
    onOrderCreated (order) { this.load(); if (order && order.id) this.$router.push({ name: 'lab-order-card', params: { id: order.id }, query: { labels: 1 } }); },
    async onOrderAction ({ row, action }) {
      switch (action) {
        case 'ENTER_RESULT': this.openEditor(row); break;
        case 'VERIFY': this.openVerify(row); break;
        case 'RERUN': this.rerun(row); break;
        case 'REJECT': this.askReject(row); break;
        case 'RELEASE':
          if (!row.orderId) return;
          try { await this.$api.releaseOrder(row.orderId); this.notifyOk('Результати видано'); this.load(); } catch (e) { this.notifyError(e); }
          break;
        case 'REOPEN':
          if (!row.orderId) return;
          try { await this.$api.reopenOrder(row.orderId); this.notifyOk('Повернуто в роботу'); this.load(); } catch (e) { this.notifyError(e); }
          break;
        default: this.openOrder(row);
      }
    }
  }
};
</script>

<style scoped>
.workstation-page:focus { outline: none; }
</style>
