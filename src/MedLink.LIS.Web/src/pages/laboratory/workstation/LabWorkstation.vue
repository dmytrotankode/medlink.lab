<template>
  <div class="workstation-page" data-testid="workstationPage" tabindex="0" @keydown.enter.exact="onEnter" @keydown.esc="onEsc">
    <page-header title="Робочий стіл лаборанта" icon="fas fa-microscope" subtitle="Результати з аналізаторів і ручне введення" :breadcrumbs="[{ label: 'Робочий стіл' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
      <q-btn outline dense color="primary" icon="add" label="Нове замовлення" data-testid="wsCreateOrder" @click="createOpen = true" />
      <q-btn outline dense color="primary" icon="playlist_add" :label="selected.length ? `Робочий лист (${selected.length})` : 'Робочі листи'" data-testid="wsBatch" @click="batchOpen = true" />
      <span class="inline-block">
        <q-btn unelevated dense color="teal-7" icon="verified" label="Автоверифікація" data-testid="wsAutoverify" :disable="!canDo(...ENTER_ROLES)" @click="autoverify" />
        <q-tooltip>{{ canDo(...ENTER_ROLES) ? 'Автоматично верифікує показники зі статусом «Є результат» (виділені або всі) за правилами; решту залишає лікарю' : roleHint(...ENTER_ROLES) }}</q-tooltip>
      </span>
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <!-- Зона A: тулбар фільтрів -->
    <div class="medlink-card q-pa-sm q-mb-sm">
      <div class="row q-col-gutter-sm items-center">
        <div class="col-12 col-md-3">
          <q-input v-model="filters.search" dense outlined clearable placeholder="Штрихкод, пацієнт, показник, № замовлення" data-testid="wsSearch" @keyup.enter="load" @clear="load">
            <template v-slot:prepend><q-icon name="search" /></template>
          </q-input>
        </div>
        <div class="col-6 col-md-2"><q-select v-model="filters.sectionId" dense outlined clearable label="Підрозділ" :options="sectionOptions" emit-value map-options data-testid="wsSection" @input="load" /></div>
        <div class="col-6 col-md-2"><q-select v-model="filters.status" dense outlined clearable label="Статус" :options="statusOptions" emit-value map-options @input="load" /></div>
        <div class="col-6 col-md-2"><q-select v-model="filters.analyzerId" dense outlined clearable label="Аналізатор" :options="analyzerOptions" emit-value map-options @input="load" /></div>
        <div class="col-6 col-md-2"><q-select v-model="filters.flag" dense outlined clearable label="Прапорець" :options="flagOptions" emit-value map-options @input="load" /></div>
        <div class="col-6 col-md-1"><q-toggle v-model="filters.cito" label="CITO" color="deep-orange-6" keep-color @input="load" /></div>
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
        <span>Подвійний клік або Enter — введення; Enter — зберегти; Tab — наступний; Esc — скасувати.</span>
        <q-space />
        <q-chip dense outline color="primary" icon="assignment" class="q-ma-none q-mr-xs">Рядків: {{ rows.length }}{{ total > rows.length ? ` з ${total}` : '' }}</q-chip>
        <q-chip dense outline color="negative" icon="warning" class="q-ma-none q-mr-xs">Критичні: {{ summary.panic || 0 }}</q-chip>
        <q-chip dense outline color="warning" text-color="dark" icon="rate_review" class="q-ma-none q-mr-sm">Очікують лікаря: {{ summary.needsReview || 0 }}</q-chip>
        <span v-if="selected.length" class="inline-block">
          <q-btn flat dense size="sm" color="green-8" icon="done_all" :label="`Верифікувати обрані (${selected.length})`" :disable="!canDo(...VERIFY_ROLES)" @click="verifySelected" />
          <q-tooltip v-if="!canDo(...VERIFY_ROLES)">{{ roleHint(...VERIFY_ROLES) }}</q-tooltip>
        </span>
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
    <confirm-dialog v-model="rejectOpen" title="Відхилити результат" :message="`Результат ${activeRow.testCode} (${activeRow.barcode}) буде відхилено.`" ok-label="Відхилити" color="negative" icon="block" with-reason reason-required :reason-options="rejectionReasonNames" @confirm="doReject" />
    <confirm-dialog v-model="deleteOpen" title="Видалити результат" :message="`Результат ${activeRow.testCode} буде видалено, показник знову очікуватиме результат.`" ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
    <q-dialog v-model="historyOpen">
      <q-card style="min-width: 640px; max-width: 96vw">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">Історія результату {{ activeRow.testCode }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
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
import roleMixin from '../../../mixins/roleMixin';
import reasonsMixin from '../../../mixins/reasonsMixin';
import { TEST_STATUS, ORDER_STATUS, FLAG, toOptions, flagMeta } from '../../../utils/statuses';
import { formatNumber, formatDateTime } from '../../../utils/format';
import { enterBlockReason, roleBlockReason, ENTER_ROLES, VERIFY_ROLES } from '../../../utils/resultRules';

// Події журналу версій результату (серверні коди → підписи)
const HISTORY_ACTIONS = {
  ENTERED: 'Введено', CORRECTED: 'Виправлено', SUPERSEDED: 'Замінено новою версією', VERIFIED: 'Верифіковано', AUTO_VERIFIED: 'Автоверифіковано',
  REJECTED: 'Відхилено', REOPENED: 'Повернуто в роботу', RERUN: 'Відправлено на повтор', DELETED: 'Видалено'
};

export default {
  name: 'LabWorkstation',
  mixins: [apiMixin, roleMixin, reasonsMixin],
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
      ENTER_ROLES,
      VERIFY_ROLES
    };
  },
  computed: {
    ...mapState('laboratory', ['summary', 'analyzers']),
    analyzerOptions () { return this.analyzers.map(a => ({ value: a.id, label: a.name })); },
    sectionOptions () { return this.sections.map(s => ({ value: s.id, label: s.name })); },
    historyColumns () {
      return [
        { name: 'version', label: 'Версія', field: 'version', align: 'left' },
        { name: 'action', label: 'Подія', field: r => HISTORY_ACTIONS[r.action] || '—', align: 'left' },
        { name: 'value', label: 'Значення', field: r => r.numericValue !== null && r.numericValue !== undefined ? formatNumber(r.numericValue) : (r.stringValue || r.value || '—'), align: 'right' },
        { name: 'flag', label: 'Прапорець', field: r => (r.flag ? flagMeta(r.flag).label : '—'), align: 'left' },
        { name: 'at', label: 'Коли', field: r => formatDateTime(r.at || r.enteredAt), align: 'left' },
        { name: 'by', label: 'Хто', field: r => this.actorName(r.actorId || r.enteredById), align: 'left' },
        { name: 'comment', label: 'Коментар', field: r => r.comment || r.operatorComment || r.verificationComment || '', align: 'left' }
      ];
    }
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
          this.$q.notify({ type: 'negative', icon: 'warning', message: `КРИТИЧНЕ ЗНАЧЕННЯ: ${result.testCode} = ${formatNumber(result.value)} ${result.unit || ''} — потрібен дзвінок лікарю`, timeout: 8000, actions: [{ label: 'Критичні', color: 'white', handler: () => this.$router.push({ name: 'lab-validation', query: { tab: 'panic' } }) }] });
        }
      } else {
        this.load();
      }
      this.$store.dispatch('laboratory/refreshStatus');
    },
    /** Хто виконав дію: співробітник за ідентифікатором або аналізатор (SYSTEM:<id>) */
    actorName (id) {
      if (!id) return '—';
      if (String(id).startsWith('SYSTEM:')) {
        const a = this.analyzers.find(x => x.id === String(id).slice(7));
        return a ? `Аналізатор ${a.name}` : 'Аналізатор';
      }
      const e = this.$store.state.context.employees.find(x => x.id === id);
      return e ? (e.fullName || e.name) : 'Співробітник';
    },
    openEditor (row) {
      // Не відкриваємо вікно, яке сервер відхилить: проба не прийнята або немає права вводити результат
      const reason = enterBlockReason(row, this.currentLabRole);
      if (reason) { this.$q.notify({ type: 'warning', message: reason, timeout: 5000 }); return; }
      this.activeRow = row; this.editorOpen = true;
    },
    openVerify (row) {
      const reason = roleBlockReason(this.currentLabRole, VERIFY_ROLES) ? 'Верифікує лише лікар-лаборант (або адміністратор лабораторії)' : '';
      if (reason) { this.$q.notify({ type: 'warning', message: reason, timeout: 5000 }); return; }
      this.activeRow = row; this.verifyOpen = true;
    },
    async rerun (row) {
      try { await this.$api.rerunResult(row.orderTestId); this.notifyOk(`Показник ${row.testCode} відправлено на повтор`); this.load(); } catch (e) { this.notifyError(e); }
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
        this.$q.notify({ type: blocked.length ? 'warning' : 'positive', message: `Автоверифіковано: ${(res && res.verified) || 0}; заблоковано: ${blocked.length}${blocked.length ? ' — ' + this.reasonSummary(blocked) : ''}`, timeout: 6000 });
        this.load();
      } catch (e) { this.notifyError(e); }
    },
    async autoverifyOne (row) {
      try {
        const res = await this.$api.autoverify([row.orderTestId]);
        const blocked = (res && res.blocked) || [];
        this.$q.notify({ type: blocked.length ? 'warning' : 'positive', message: blocked.length ? `Заблоковано: ${this.reasonText(blocked[0].reason)}` : 'Автоверифіковано' });
        this.load();
      } catch (e) { this.notifyError(e); }
    },
    async verifySelected () {
      try {
        const res = await this.$api.verifyBatch(this.selected.map(r => r.orderTestId));
        const skipped = (res && res.skipped) || [];
        this.$q.notify({ type: skipped.length ? 'warning' : 'positive', message: `Верифіковано: ${(res && res.verified) || 0}; пропущено: ${skipped.length}${skipped.length ? ' — ' + skipped.slice(0, 3).map(s => this.reasonText(s.reason)).join('; ') : ''}`, timeout: 6000 });
        this.selected = [];
        this.load();
      } catch (e) { this.notifyError(e); }
    },
    /** Згортає однакові причини: «Результат поза нормою — 5; Прапорці аналізатора — 2» */
    reasonSummary (items) {
      const counts = {};
      items.forEach(b => { const t = this.reasonText(b.reason).replace(/\s*\((ABNORMAL|LOW|HIGH|NORMAL)\)/g, ''); counts[t] = (counts[t] || 0) + 1; });
      return Object.keys(counts).map(t => counts[t] > 1 ? `${t} — ${counts[t]}` : t).join('; ');
    },
    /** Причина пропуску/блокування від сервера: не показуємо технічні коди статусів */
    reasonText (reason) {
      if (!reason) return 'причину не вказано';
      return String(reason).replace(/\b(NEW|RECEIVED|IN_PROGRESS|RESULTED|NEEDS_REVIEW|VERIFIED|AUTO_VERIFIED|PENDING|REJECTED|RERUN|IN_ANALYSIS)\b/g, m => (TEST_STATUS[m] || ORDER_STATUS[m] || { label: m }).label);
    },
    async showHistory (row) {
      this.activeRow = row; this.historyOpen = true; this.history = [];
      try { this.history = this.asList(await this.$api.resultHistory(row.orderTestId)); } catch (e) { this.notifyError(e); }
    },
    openOrder (row) { if (row.orderId) this.$router.push({ name: 'lab-order-card', params: { id: row.orderId } }); else this.$q.notify({ type: 'warning', message: 'Не вдалося відкрити замовлення: рядок не пов’язано із замовленням' }); },
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
