<template>
  <div class="calibration-page" data-testid="calibrationPage">
    <page-header title="Калібрування приладів" icon="fas fa-sliders-h" subtitle="Журнал калібрувань за приладом і показником, терміни дії, наступна дата" :breadcrumbs="[{ label: 'Контроль якості' }, { label: 'Калібрування' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <span class="inline-block">
        <q-btn unelevated dense color="primary" icon="add" label="Внести калібрування" :disable="!canWork" data-testid="calibrationCreate" @click="openForm" />
        <q-tooltip v-if="!canWork">{{ workHint }}</q-tooltip>
      </span>
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div v-if="problems.length" class="q-mb-md" style="max-width: 980px" data-testid="calibrationAlerts">
      <q-banner v-for="p in problems" :key="p.id" dense rounded class="q-mb-xs" :class="p.status === 'EXPIRING' ? 'bg-orange-1 text-orange-9' : 'bg-red-1 text-negative'">
        <template v-slot:avatar><q-icon :name="p.status === 'EXPIRING' ? 'schedule' : 'event_busy'" /></template>
        <b>{{ p.analyzerName }}</b>, {{ p.testCode ? 'показник ' + testLabel(p.testCode) : 'усі показники' }}: {{ problemText(p) }}
      </q-banner>
    </div>

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-3"><q-select v-model="filters.analyzerId" dense outlined clearable label="Аналізатор" :options="analyzerOptions" emit-value map-options @input="load" /></div>
      <div class="col-12 col-md-3"><q-select v-model="filters.status" dense outlined clearable label="Стан" :options="statusOptions" emit-value map-options @input="load" /></div>
      <div class="col-12 col-md-3"><q-toggle v-model="onlyCurrent" label="Лише поточні (останні по кожному показнику)" color="primary" /></div>
    </div>

    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="id" dense flat :loading="loading" :pagination="{ rowsPerPage: 25 }" no-data-label="Калібрувань ще не внесено" data-testid="calibrationTable">
        <template v-slot:body="props">
          <q-tr :props="props" :class="rowClass(props.row)">
            <q-td key="analyzer" :props="props"><b>{{ props.row.analyzerName || '—' }}</b></q-td>
            <q-td key="test" :props="props">{{ props.row.testCode ? testLabel(props.row.testCode) : 'Усі показники' }}</q-td>
            <q-td key="performedAt" :props="props">{{ props.row.performedAt | datetime }}<div class="text-caption text-grey-7">{{ props.row.performedByName || '' }}</div></q-td>
            <q-td key="calibrator" :props="props"><span class="mono">{{ props.row.calibratorLot }}</span><div v-if="props.row.calibratorName" class="text-caption text-grey-7">{{ props.row.calibratorName }}</div></q-td>
            <q-td key="reagent" :props="props"><span v-if="props.row.reagentLotNumber" class="mono">{{ props.row.reagentLotNumber }}</span><span v-else class="text-grey-6">—</span></q-td>
            <q-td key="value" :props="props">{{ props.row.calibrationValue != null ? props.row.calibrationValue : '—' }} <span class="text-caption text-grey-7">{{ props.row.unit || '' }}</span></q-td>
            <q-td key="result" :props="props">
              <q-badge v-if="props.row.accepted" color="positive" label="Прийнято" />
              <q-badge v-else color="negative" label="Не прийнято"><q-tooltip>{{ props.row.rejectReason }}</q-tooltip></q-badge>
            </q-td>
            <q-td key="validUntil" :props="props">{{ props.row.validUntil | date }}<div class="text-caption text-grey-7">наступне: {{ props.row.nextDueAt | date }}</div></q-td>
            <q-td key="status" :props="props"><q-badge :color="statusColor(props.row.status)" :text-color="props.row.status === 'EXPIRING' ? 'dark' : 'white'" :label="props.row.statusText" /></q-td>
            <q-td key="actions" :props="props" class="text-right no-wrap">
              <q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Видалити запис" :disable="!canDelete" @click="askDelete(props.row)"><q-tooltip>{{ canDelete ? 'Видалити запис журналу' : deleteHint }}</q-tooltip></q-btn>
            </q-td>
          </q-tr>
        </template>
      </q-table>
    </div>

    <q-dialog v-model="formOpen" no-backdrop-dismiss>
      <q-card style="min-width: 640px; max-width: 96vw" data-testid="calibrationDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="fas fa-sliders-h" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">Нове калібрування</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section>
          <div class="row q-col-gutter-sm">
            <div class="col-6"><q-select v-model="form.analyzerId" outlined dense label="Аналізатор *" :options="analyzerOptions" emit-value map-options :error="touched && !form.analyzerId" hide-bottom-space data-testid="calAnalyzer" @input="form.reagentLotId = null" /></div>
            <div class="col-6"><q-select v-model="form.testCode" outlined dense clearable label="Показник (порожньо — усі показники приладу)" :options="testOptions" emit-value map-options use-input input-debounce="0" @filter="filterTests" /></div>
            <div class="col-6"><q-input v-model="form.calibratorLot" outlined dense label="Лот калібратора *" :error="touched && !form.calibratorLot" hide-bottom-space data-testid="calCalibratorLot" /></div>
            <div class="col-6"><q-input v-model="form.calibratorName" outlined dense label="Калібратор" /></div>
            <div class="col-12"><q-select v-model="form.reagentLotId" outlined dense clearable label="Лот реагенту" :options="lotOptions" emit-value map-options hint="Якщо лот реагенту прострочено або знято з використання, калібрування вважається недійсним" /></div>
            <div class="col-4"><q-input v-model="form.performedAt" outlined dense type="datetime-local" stack-label label="Дата й час калібрування" /></div>
            <div class="col-4"><q-input v-model="form.calibrationValue" outlined dense label="Значення (коефіцієнт)" /></div>
            <div class="col-4"><q-input v-model="form.unit" outlined dense label="Одиниця" /></div>
            <div class="col-4"><q-input v-model.number="form.validDays" outlined dense type="number" min="1" label="Діє, діб *" :error="touched && !(form.validDays > 0)" hide-bottom-space data-testid="calValidDays" /></div>
            <div class="col-8 row items-center"><q-toggle v-model="form.accepted" color="positive" label="Калібрування прийнято (критерії виконано)" data-testid="calAccepted" /></div>
            <div v-if="!form.accepted" class="col-12"><q-input v-model="form.rejectReason" outlined dense label="Причина відхилення *" :error="touched && !form.rejectReason" hide-bottom-space /></div>
            <div class="col-12"><q-input v-model="form.comment" outlined dense type="textarea" autogrow label="Коментар" /></div>
          </div>
          <div v-if="formError" class="text-negative q-mt-sm">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="calSave" @click="save" /></q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="deleteOpen" no-backdrop-dismiss>
      <q-card style="min-width: 420px">
        <q-card-section class="row items-center bg-negative text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">Видалити запис журналу?</div></q-card-section>
        <q-card-section>
          <div class="q-mb-sm">Запис залишиться в аудиті. Вкажіть причину видалення.</div>
          <q-input v-model="deleteReason" outlined dense label="Причина *" autofocus data-testid="calDeleteReason" />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="negative" icon="delete" label="Видалити" :disable="!deleteReason" @click="doDelete" /></q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';

const STATUS_OPTIONS = [
  { value: 'VALID', label: 'Дійсне' }, { value: 'EXPIRING', label: 'Спливає' }, { value: 'EXPIRED', label: 'Протерміноване' },
  { value: 'REJECTED', label: 'Відхилене' }, { value: 'LOT_INVALID', label: 'Лот реагенту недійсний' }
];

export default {
  name: 'CalibrationJournal',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      items: [], summary: [], lots: [], filters: { analyzerId: null, status: null }, onlyCurrent: true, testFilter: '',
      formOpen: false, form: {}, touched: false, formError: '', saving: false, deleteOpen: false, deleteReason: '', active: null,
      statusOptions: STATUS_OPTIONS,
      columns: [
        { name: 'analyzer', label: 'Аналізатор', align: 'left' },
        { name: 'test', label: 'Показник', align: 'left' },
        { name: 'performedAt', label: 'Виконано', field: 'performedAt', align: 'left', sortable: true },
        { name: 'calibrator', label: 'Лот калібратора', align: 'left' },
        { name: 'reagent', label: 'Лот реагенту', align: 'left' },
        { name: 'value', label: 'Значення', align: 'left' },
        { name: 'result', label: 'Результат', align: 'left' },
        { name: 'validUntil', label: 'Діє до', field: 'validUntil', align: 'left', sortable: true },
        { name: 'status', label: 'Стан', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    analyzers () { return this.$store.state.laboratory.analyzers; },
    analyzerOptions () { return this.analyzers.map(a => ({ value: a.id, label: a.name })); },
    allTests () { return this.$store.getters['dictionaries/items']('tests'); },
    testOptions () { const f = this.testFilter.toLowerCase(); return this.allTests.filter(t => !f || `${t.code} ${t.name || ''}`.toLowerCase().includes(f)).map(t => ({ value: t.code, label: t.name ? `${t.name} (${t.code})` : t.code })); },
    lotOptions () { return this.lots.filter(l => !this.form.analyzerId || !l.analyzerId || l.analyzerId === this.form.analyzerId).map(l => ({ value: l.id, label: `${l.reagentName}, лот ${l.lotNumber}` })); },
    rows () {
      if (!this.onlyCurrent || this.filters.status) return this.items;
      const ids = new Set(this.summary.map(s => s.id));
      return this.items.filter(i => ids.has(i.id));
    },
    problems () { return this.summary.filter(s => s.status !== 'VALID'); },
    // Права збігаються з серверною політикою: вносять адміністратор, лікар-лаборант, лаборант; видаляють адміністратор і лікар-лаборант
    canWork () { return this.canDo('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    workHint () { return this.roleHint('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    canDelete () { return this.canDo('LAB_DOCTOR'); },
    deleteHint () { return this.roleHint('LAB_DOCTOR'); }
  },
  created () { this.$store.dispatch('dictionaries/load', 'tests'); this.load(); },
  methods: {
    testLabel (code) { const t = this.allTests.find(x => x.code === code); return t && t.name ? `${t.name} (${code})` : code; },
    statusColor (s) { return { VALID: 'positive', EXPIRING: 'warning', EXPIRED: 'negative', REJECTED: 'negative', LOT_INVALID: 'deep-orange-7' }[s] || 'grey'; },
    rowClass (r) { return r.status === 'EXPIRED' || r.status === 'REJECTED' || r.status === 'LOT_INVALID' ? 'row-status--cancelled' : (r.status === 'EXPIRING' ? 'row-status--overdue' : ''); },
    problemText (p) {
      if (p.status === 'EXPIRED') return `калібрування протерміноване (діяло до ${this.fmt(p.validUntil)}). Виконайте калібрування й внесіть його в журнал.`;
      if (p.status === 'REJECTED') return 'останнє калібрування не прийнято. Повторіть калібрування.';
      if (p.status === 'LOT_INVALID') return `лот реагенту ${p.reagentLotNumber || ''} прострочений або знятий з використання. Виконайте калібрування під новий лот.`;
      return `калібрування спливає ${this.fmt(p.validUntil)} (залишилось діб: ${p.daysLeft}).`;
    },
    fmt (d) { return d ? new Date(d).toLocaleDateString('uk-UA') : '—'; },
    filterTests (val, update) { update(() => { this.testFilter = val || ''; }); },
    async load () {
      const params = { analyzerId: this.filters.analyzerId, status: this.filters.status };
      const results = await Promise.allSettled([this.$api.calibrations(params), this.$api.calibrationSummary({ analyzerId: this.filters.analyzerId }), this.$api.reagentLots({ isActive: true })]);
      if (results[0].status === 'fulfilled') this.items = this.asList(results[0].value);
      if (results[1].status === 'fulfilled') this.summary = this.asList(results[1].value);
      if (results[2].status === 'fulfilled') this.lots = this.asList(results[2].value);
      const failed = results.slice(0, 2).find(r => r.status === 'rejected');
      this.apiError = failed && !this.apiOffline ? failed.reason.userMessage : null;
    },
    openForm () {
      const d = new Date(); d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
      this.form = { analyzerId: this.filters.analyzerId, testCode: null, calibratorLot: '', calibratorName: '', reagentLotId: null, performedAt: d.toISOString().slice(0, 16), calibrationValue: '', unit: '', validDays: 30, accepted: true, rejectReason: '', comment: '' };
      this.touched = false; this.formError = ''; this.formOpen = true;
    },
    async save () {
      this.touched = true; this.formError = '';
      const f = this.form;
      if (!f.analyzerId || !f.calibratorLot) { this.formError = 'Оберіть аналізатор і вкажіть лот калібратора'; return; }
      if (!(f.validDays > 0)) { this.formError = 'Вкажіть, скільки діб діє калібрування'; return; }
      if (!f.accepted && !f.rejectReason) { this.formError = 'Для неприйнятого калібрування вкажіть причину'; return; }
      const val = String(f.calibrationValue).replace(',', '.').trim();
      const body = {
        analyzerId: f.analyzerId, testCode: f.testCode || null, reagentLotId: f.reagentLotId || null, calibratorLot: f.calibratorLot, calibratorName: f.calibratorName || null,
        performedAt: f.performedAt ? new Date(f.performedAt).toISOString() : null, calibrationValue: val !== '' && !isNaN(Number(val)) ? Number(val) : null, unit: f.unit || null,
        accepted: f.accepted, rejectReason: f.accepted ? null : f.rejectReason, validDays: f.validDays, comment: f.comment || null
      };
      this.saving = true;
      try { await this.$api.createCalibration(body); this.notifyOk('Калібрування внесено'); this.formOpen = false; this.load(); } catch (e) { this.formError = e.userMessage || 'Не вдалося зберегти калібрування'; } finally { this.saving = false; }
    },
    askDelete (r) { this.active = r; this.deleteReason = ''; this.deleteOpen = true; },
    async doDelete () {
      try { await this.$api.deleteCalibration(this.active.id, this.deleteReason); this.notifyOk('Запис видалено'); this.deleteOpen = false; this.load(); } catch (e) { this.notifyError(e); }
    }
  }
};
</script>
