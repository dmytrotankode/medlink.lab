<template>
  <div class="validation-page" data-testid="validationPage">
    <page-header title="Валідація та паніка" icon="fas fa-user-check" subtitle="Робоче місце лікаря-лаборанта: верифікація результатів, порівняння з попередніми, панічні значення та CITO-дзвінки" :breadcrumbs="[{ label: 'Валідація' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
      <q-btn unelevated dense color="green-8" icon="done_all" :label="`Верифікувати обрані (${selected.length})`" :disable="!selected.length" data-testid="verifySelected" @click="verifySelected" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <q-tabs v-model="tab" dense align="left" class="text-grey-8 medlink-card bg-white" active-color="primary" indicator-color="primary">
      <q-tab name="review" icon="rate_review" :label="`На верифікацію (${rows.length})`" data-testid="tab-review" />
      <q-tab name="pending" icon="priority_high" :label="`Паніка без дзвінка (${pending.length})`" data-testid="tab-panic" />
      <q-tab name="calls" icon="call" :label="`Журнал дзвінків (${calls.length})`" data-testid="tab-calls" />
    </q-tabs>

    <q-tab-panels v-model="tab" animated class="bg-transparent q-mt-sm">
      <!-- На верифікацію -->
      <q-tab-panel name="review" class="q-pa-none">
        <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
          <div class="col-12 col-md-3"><q-input v-model="filters.search" dense outlined clearable placeholder="Пацієнт / тест / штрихкод" @keyup.enter="load" @clear="load"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
          <div class="col-6 col-md-2"><q-select v-model="filters.status" dense outlined label="Статус" :options="[{ value: 'NEEDS_REVIEW,RESULTED', label: 'Очікує лікаря + з результатом' }, { value: 'NEEDS_REVIEW', label: 'Тільки очікує лікаря' }, { value: 'RESULTED', label: 'Тільки з результатом' }]" emit-value map-options @input="load" /></div>
          <div class="col-6 col-md-2"><q-select v-model="filters.flag" dense outlined clearable label="Прапорець" :options="flagOptions" emit-value map-options @input="load" /></div>
          <div class="col-6 col-md-2"><q-toggle v-model="filters.cito" label="CITO" color="deep-orange-6" keep-color @input="load" /></div>
          <div class="col-6 col-md-3 text-right text-caption text-grey-7">Для CRIT_* коментар лікаря обов’язковий. Lockout ВКЯ — лише з override.</div>
        </div>
        <div class="medlink-card">
          <q-table :data="rows" :columns="columns" row-key="orderTestId" dense flat :loading="loading" :selected.sync="selected" selection="multiple" :pagination.sync="pagination" :rows-per-page-options="[25, 50, 100]" no-data-label="Результатів на верифікацію немає" data-testid="reviewTable">
            <template v-slot:body="props">
              <q-tr :props="props" :class="{ 'bg-red-1': isCritical(props.row.flag), 'row-status--cito': props.row.isCito }" class="cursor-pointer" @click="active = props.row" @dblclick="openVerify(props.row)">
                <q-td auto-width><q-checkbox v-model="props.selected" dense /></q-td>
                <q-td key="patient" :props="props"><div>{{ props.row.patientName }}</div><div class="text-caption text-grey-7">{{ props.row.patientAgeGender }} · {{ props.row.orderNumber }}</div></q-td>
                <q-td key="test" :props="props"><b>{{ props.row.testCode }}</b> <span class="text-grey-7">{{ props.row.testName }}</span></q-td>
                <q-td key="value" :props="props" class="text-right"><span :class="flagCss(props.row.flag)" class="text-weight-bold">{{ display(props.row.value) }}</span> <span class="text-caption text-grey-7">{{ props.row.unit }}</span></q-td>
                <q-td key="reference" :props="props">{{ referenceDisplay(props.row) || '—' }}</q-td>
                <q-td key="flag" :props="props" class="text-center"><flag-marker :flag="props.row.flag || 'NONE'" /></q-td>
                <q-td key="previous" :props="props" class="text-right">
                  <span v-if="props.row.previousValue !== null && props.row.previousValue !== undefined">{{ props.row.previousValue | num }} <span class="text-caption text-grey-6">{{ props.row.previousAt | date }}</span></span>
                  <span v-else class="text-grey-5">—</span>
                </q-td>
                <q-td key="delta" :props="props" class="text-right"><span :class="props.row.deltaAlert ? 'text-orange-9 text-weight-bold' : ''">{{ props.row.deltaPercent | pct }}</span></q-td>
                <q-td key="status" :props="props"><status-chip :value="props.row.status" type="test" /><q-badge v-if="props.row.isLockedOut" color="purple-6" class="q-ml-xs"><q-icon name="lock" size="10px" /></q-badge></q-td>
                <q-td key="actions" :props="props" class="text-right no-wrap" @click.stop>
                  <q-btn flat dense round size="sm" icon="how_to_reg" color="green-8" :data-testid="`verify-${props.row.testCode}`" @click="openVerify(props.row)"><q-tooltip>Верифікувати</q-tooltip></q-btn>
                  <q-btn v-if="isCritical(props.row.flag)" flat dense round size="sm" icon="call" color="negative" @click="openCall(props.row)"><q-tooltip>Зареєструвати дзвінок</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="edit_note" color="primary" @click="openEditor(props.row)"><q-tooltip>Коригувати результат</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="replay" color="purple-6" @click="rerun(props.row)"><q-tooltip>Повтор</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="block" color="negative" @click="askReject(props.row)"><q-tooltip>Відхилити</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="open_in_new" color="grey-8" :disable="!props.row.orderId" @click="$router.push({ name: 'lab-order-card', params: { id: props.row.orderId } })"><q-tooltip>Замовлення</q-tooltip></q-btn>
                </q-td>
              </q-tr>
            </template>
          </q-table>
        </div>

        <!-- Порівняння з попереднім -->
        <div v-if="active" class="medlink-card q-mt-sm q-pa-md" data-testid="comparePanel">
          <div class="row q-col-gutter-md items-center">
            <div class="col-12 col-md-3">
              <div class="text-caption text-grey-7">Пацієнт</div>
              <div class="text-weight-bold">{{ active.patientName }}</div>
              <div class="text-caption">{{ active.patientAgeGender }}</div>
            </div>
            <div class="col-6 col-md-2 text-center">
              <div class="text-caption text-grey-7">Попередній</div>
              <div class="text-h6 text-grey-8">{{ active.previousValue !== null && active.previousValue !== undefined ? formatNumber(active.previousValue) : '—' }}</div>
              <div class="text-caption">{{ active.previousAt | datetime }}</div>
            </div>
            <div class="col-6 col-md-1 text-center"><q-icon :name="deltaIcon(active)" size="28px" :color="active.deltaAlert ? 'orange-9' : 'grey-6'" /><div class="text-caption">{{ active.deltaPercent | pct }}</div></div>
            <div class="col-6 col-md-2 text-center">
              <div class="text-caption text-grey-7">Поточний</div>
              <div class="text-h5" :class="flagCss(active.flag)">{{ display(active.value) }}</div>
              <div class="text-caption">{{ active.unit }} · норма {{ referenceDisplay(active) }}</div>
            </div>
            <div class="col-12 col-md-4">
              <trend-mini :points="trend" :low="active.normLow" :high="active.normHigh" />
              <div class="text-caption text-grey-6 text-center">динаміка показника (/patients/{id}/trend)</div>
            </div>
          </div>
        </div>
      </q-tab-panel>

      <!-- Паніка без дзвінка -->
      <q-tab-panel name="pending" class="q-pa-none">
        <div class="medlink-card">
          <q-table :data="pending" :columns="pendingColumns" row-key="resultId" dense flat :loading="loading" :pagination="{ rowsPerPage: 25 }" no-data-label="Усі критичні результати опрацьовано" data-testid="panicTable">
            <template v-slot:body-cell-value="props"><q-td :props="props"><span class="val-critical">{{ props.row.value | num }} {{ props.row.unit }}</span></q-td></template>
            <template v-slot:body-cell-flag="props"><q-td :props="props"><flag-marker :flag="props.row.flag" show-label /></q-td></template>
            <template v-slot:body-cell-enteredAt="props"><q-td :props="props">{{ props.row.enteredAt || props.row.resultedAt | datetime }}</q-td></template>
            <template v-slot:body-cell-actions="props">
              <q-td :props="props" class="text-right no-wrap">
                <q-btn dense color="negative" icon="call" label="Дзвінок" size="sm" :data-testid="`call-${props.row.testCode}`" @click="openCall(props.row)" />
                <q-btn flat dense round size="sm" icon="open_in_new" color="grey-8" :disable="!props.row.orderId" @click="$router.push({ name: 'lab-order-card', params: { id: props.row.orderId } })" />
              </q-td>
            </template>
          </q-table>
        </div>
      </q-tab-panel>

      <!-- Журнал дзвінків -->
      <q-tab-panel name="calls" class="q-pa-none">
        <div class="medlink-card">
          <q-table :data="calls" :columns="callColumns" row-key="id" dense flat :loading="loading" :pagination="{ rowsPerPage: 25, sortBy: 'notifiedAt', descending: true }" no-data-label="Дзвінків ще не зареєстровано" data-testid="callsTable">
            <template v-slot:body-cell-notifiedAt="props"><q-td :props="props">{{ props.row.notifiedAt | datetime }}</q-td></template>
            <template v-slot:body-cell-readback="props"><q-td :props="props"><q-icon :name="props.row.readbackConfirmed ? 'check_circle' : 'cancel'" :color="props.row.readbackConfirmed ? 'positive' : 'negative'" /></q-td></template>
            <template v-slot:body-cell-actions="props">
              <q-td :props="props" class="text-right no-wrap">
                <q-btn flat dense round size="sm" icon="edit" color="primary" @click="editCall(props.row)" />
                <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDeleteCall(props.row)" />
              </q-td>
            </template>
          </q-table>
        </div>
      </q-tab-panel>
    </q-tab-panels>

    <verify-result-dialog v-model="verifyOpen" :row="activeRow" @verified="afterVerify" />
    <result-editor-dialog v-model="editorOpen" :row="activeRow" @saved="load" />
    <panic-call-dialog v-model="callOpen" :result="activeRow" :call="activeCall" @saved="load" />
    <confirm-dialog v-model="rejectOpen" title="Відхилити результат" :message="`Результат ${activeRow.testCode} буде відхилено.`" ok-label="Відхилити" color="negative" icon="block" with-reason reason-required @confirm="doReject" />
    <confirm-dialog v-model="deleteCallOpen" title="Видалити запис дзвінка" message="Запис буде видалено з журналу (з аудитом)." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteCall" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import VerifyResultDialog from '../../../components/results/VerifyResultDialog.vue';
import ResultEditorDialog from '../../../components/results/ResultEditorDialog.vue';
import PanicCallDialog from './PanicCallDialog.vue';
import TrendMini from '../../../components/charts/TrendMini.vue';
import { FLAG, toOptions, isCritical, flagMeta } from '../../../utils/statuses';
import { referenceDisplay, formatNumber, formatDateTime } from '../../../utils/format';

export default {
  name: 'ValidationPanic',
  mixins: [apiMixin],
  components: { VerifyResultDialog, ResultEditorDialog, PanicCallDialog, TrendMini },
  data () {
    return {
      tab: 'review',
      rows: [], pending: [], calls: [], trend: [],
      selected: [],
      active: null, activeRow: {}, activeCall: null,
      filters: { search: '', status: 'NEEDS_REVIEW,RESULTED', flag: null, cito: false },
      flagOptions: toOptions(FLAG),
      pagination: { rowsPerPage: 25 },
      verifyOpen: false, editorOpen: false, callOpen: false, rejectOpen: false, deleteCallOpen: false,
      columns: [
        { name: 'patient', label: 'Пацієнт', align: 'left' },
        { name: 'test', label: 'Тест', align: 'left' },
        { name: 'value', label: 'Результат', align: 'right' },
        { name: 'reference', label: 'Норма', align: 'left' },
        { name: 'flag', label: 'Прапорець', align: 'center' },
        { name: 'previous', label: 'Попередній', align: 'right' },
        { name: 'delta', label: 'Δ%', align: 'right' },
        { name: 'status', label: 'Статус', align: 'left' },
        { name: 'actions', label: 'Дії', align: 'right' }
      ],
      pendingColumns: [
        { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'orderNumber', label: '№ замовл.', field: 'orderNumber', align: 'left' },
        { name: 'test', label: 'Тест', field: r => r.testName || r.testCode, align: 'left' },
        { name: 'value', label: 'Значення', field: 'value', align: 'right' },
        { name: 'flag', label: 'Прапорець', field: 'flag', align: 'left' },
        { name: 'enteredAt', label: 'Коли', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ],
      callColumns: [
        { name: 'notifiedAt', label: 'Коли', field: 'notifiedAt', align: 'left', sortable: true },
        { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'testCode', label: 'Тест', field: 'testCode', align: 'left' },
        { name: 'value', label: 'Значення', field: 'value', align: 'right' },
        { name: 'doctor', label: 'Лікар', field: r => r.doctorNotifiedName || r.doctorName, align: 'left' },
        { name: 'phone', label: 'Телефон', field: 'phone', align: 'left' },
        { name: 'department', label: 'Відділення', field: 'department', align: 'left' },
        { name: 'readback', label: 'Read-back', align: 'center' },
        { name: 'by', label: 'Повідомив', field: r => r.notifiedByName || r.notifiedById, align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  watch: {
    active (row) { this.loadTrend(row); },
    tab (t) { this.$router.replace({ query: { ...this.$route.query, tab: t } }).catch(() => {}); }
  },
  created () {
    if (this.$route.query.tab === 'panic') this.tab = 'pending';
    else if (this.$route.query.tab === 'calls') this.tab = 'calls';
    this.load();
  },
  methods: {
    isCritical, referenceDisplay, formatNumber, formatDateTime,
    flagCss (f) { return flagMeta(f || 'NONE').css; },
    display (v) { return v === null || v === undefined || v === '' ? '—' : (isNaN(Number(v)) ? v : formatNumber(v)); },
    deltaIcon (r) { const d = Number(r.deltaPercent); if (!d) return 'trending_flat'; return d > 0 ? 'trending_up' : 'trending_down'; },
    async load () {
      this.loading = true;
      const results = await Promise.allSettled([
        this.$api.getWorklist({ status: this.filters.status, flag: this.filters.flag, cito: this.filters.cito ? true : null, search: this.filters.search, pageSize: 500 }),
        this.$api.panicPending(),
        this.$api.getPanicCalls({ pageSize: 200 })
      ]);
      if (results[0].status === 'fulfilled') this.rows = this.asList(results[0].value);
      if (results[1].status === 'fulfilled') this.pending = this.asList(results[1].value);
      if (results[2].status === 'fulfilled') this.calls = this.asList(results[2].value);
      const failed = results.find(r => r.status === 'rejected');
      this.apiError = failed && !this.apiOffline ? failed.reason.userMessage : null;
      this.loading = false;
      if (this.$route.query.resultId && this.pending.length) {
        const r = this.pending.find(p => p.resultId === this.$route.query.resultId);
        if (r) this.openCall(r);
      }
      this.$store.dispatch('laboratory/refreshStatus');
    },
    async loadTrend (row) {
      this.trend = [];
      if (!row || !row.patientId) return;
      try { this.trend = this.asList(await this.$api.patientTrend(row.patientId, row.testCode)); } catch (e) { this.trend = []; }
    },
    openVerify (row) { this.activeRow = row; this.verifyOpen = true; },
    openEditor (row) { this.activeRow = row; this.editorOpen = true; },
    openCall (row) { this.activeRow = row; this.activeCall = null; this.callOpen = true; },
    editCall (call) { this.activeCall = call; this.activeRow = { patientName: call.patientName, testCode: call.testCode, value: call.value, orderNumber: '' }; this.callOpen = true; },
    askDeleteCall (call) { this.activeCall = call; this.deleteCallOpen = true; },
    async doDeleteCall () { try { await this.$api.deletePanicCall(this.activeCall.id); this.notifyOk('Запис видалено'); this.load(); } catch (e) { this.notifyError(e); } },
    async afterVerify () {
      await this.load();
      if (isCritical(this.activeRow.flag)) {
        const stillPending = this.pending.find(p => p.resultId === this.activeRow.resultId || p.orderTestId === this.activeRow.orderTestId);
        if (stillPending) { this.$q.notify({ type: 'warning', message: 'Критичне значення верифіковано — зареєструйте CITO-дзвінок лікарю', timeout: 6000 }); this.openCall(stillPending); }
      }
    },
    async verifySelected () {
      const crit = this.selected.filter(r => isCritical(r.flag));
      if (crit.length) { this.$q.notify({ type: 'warning', message: `Критичні результати (${crit.length}) верифікуються лише індивідуально з коментарем` }); }
      const ids = this.selected.filter(r => !isCritical(r.flag)).map(r => r.orderTestId);
      if (!ids.length) return;
      try {
        const res = await this.$api.verifyBatch(ids);
        const skipped = (res && res.skipped) || [];
        this.$q.notify({ type: skipped.length ? 'warning' : 'positive', message: `Верифіковано: ${(res && res.verified) || 0}; пропущено: ${skipped.length}${skipped.length ? ' — ' + skipped.slice(0, 3).map(s => s.reason).join('; ') : ''}`, timeout: 6000 });
        this.selected = [];
        this.load();
      } catch (e) { this.notifyError(e); }
    },
    async rerun (row) { try { await this.$api.rerunResult(row.orderTestId); this.notifyOk(`${row.testCode} → повтор`); this.load(); } catch (e) { this.notifyError(e); } },
    askReject (row) { this.activeRow = row; this.rejectOpen = true; },
    async doReject (reason) { try { await this.$api.rejectResult(this.activeRow.orderTestId, reason); this.notifyOk('Результат відхилено'); this.load(); } catch (e) { this.notifyError(e); } }
  }
};
</script>
