<template>
  <div class="reagents-page" data-testid="reagentsPage">
    <page-header title="Склад реагентів" icon="fas fa-boxes" subtitle="Лоти реагентів, терміни придатності, залишки" :breadcrumbs="[{ label: 'Реагенти' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <span class="inline-block">
        <q-btn unelevated dense color="primary" icon="add" label="Новий лот" :disable="!canWork" data-testid="lotCreate" @click="openLot(null)" />
        <q-tooltip v-if="!canWork">{{ workHint }}</q-tooltip>
      </span>
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div v-if="alerts.length" class="q-mb-md" style="max-width: 900px">
      <q-banner v-for="(a, i) in alerts" :key="i" dense rounded class="q-mb-xs" :class="a.severity === 'CRITICAL' ? 'bg-red-1 text-negative' : 'bg-orange-1 text-orange-9'">
        <template v-slot:avatar><q-icon :name="a.type === 'EXPIRED' ? 'event_busy' : 'warning'" /></template>
        <b>{{ a.reagentName }}</b> (лот {{ a.lotNumber }}{{ a.analyzer ? ', ' + a.analyzer : '' }}): {{ alertText(a) }}
      </q-banner>
    </div>

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-4"><q-input v-model="search" dense outlined clearable placeholder="Реагент, лот, виробник, аналізатор"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
      <div class="col-6 col-md-3"><q-toggle v-model="onlyProblems" label="Лише з попередженнями" color="warning" /></div>
    </div>

    <div class="medlink-card">
      <q-table :data="filtered" :columns="columns" row-key="id" dense flat :loading="loading" :pagination="{ rowsPerPage: 25, sortBy: 'expiryDate' }" no-data-label="Лотів немає" data-testid="lotsTable">
        <template v-slot:body="props">
          <q-tr :props="props" :class="rowClass(props.row)" class="cursor-pointer" @click="openCard(props.row)">
            <q-td key="name" :props="props"><b>{{ props.row.reagentName || '—' }}</b><div v-if="props.row.manufacturer" class="text-caption text-grey-7">{{ props.row.manufacturer }}</div></q-td>
            <q-td key="lotNumber" :props="props" class="mono">{{ props.row.lotNumber }}</q-td>
            <q-td key="analyzer" :props="props">{{ analyzerName(props.row.analyzerId) }}</q-td>
            <q-td key="tests" :props="props">{{ testLabel(props.row.testCode) }}</q-td>
            <q-td key="remaining" :props="props" class="text-right">
              <div class="text-weight-bold" :class="low(props.row) ? 'text-negative' : ''">{{ remaining(props.row) | num }} <span class="text-caption text-grey-7">/ {{ initial(props.row) | num }}</span></div>
              <q-linear-progress :value="ratio(props.row)" :color="low(props.row) ? 'negative' : 'positive'" size="4px" rounded />
            </q-td>
            <q-td key="minimum" :props="props" class="text-right">{{ props.row.minimumTests | num }}</q-td>
            <q-td key="expiryDate" :props="props" :class="expired(props.row) ? 'text-negative text-weight-bold' : (soon(props.row) ? 'text-orange-9 text-weight-bold' : '')">{{ props.row.expiryDate | date }}<div v-if="props.row.openedAt" class="text-caption text-grey-7">відкрито {{ props.row.openedAt | date }}</div></q-td>
            <q-td key="status" :props="props">
              <q-badge v-if="expired(props.row)" color="negative" label="Прострочено" />
              <q-badge v-else-if="empty(props.row)" color="negative" label="Вичерпано" />
              <q-badge v-else-if="low(props.row)" color="warning" text-color="dark" label="Залишок мінімальний" />
              <q-badge v-else-if="soon(props.row)" color="orange-7" label="Спливає за ≤ 30 днів" />
              <q-badge v-else color="positive" label="Норма" />
            </q-td>
            <q-td key="actions" :props="props" class="text-right no-wrap" @click.stop>
              <q-btn flat dense round size="sm" icon="remove_circle_outline" color="deep-orange-6" aria-label="Списати тести" :disable="!canWork" :data-testid="`consume-${props.row.lotNumber}`" @click="openConsume(props.row)"><q-tooltip>{{ canWork ? 'Списати тести' : workHint }}</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="edit" color="primary" aria-label="Змінити лот" :disable="!canWork" @click="openLot(props.row)"><q-tooltip>{{ canWork ? 'Змінити лот' : workHint }}</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Видалити лот" :disable="!canDelete" @click="askDelete(props.row)"><q-tooltip>{{ canDelete ? 'Видалити лот' : deleteHint }}</q-tooltip></q-btn>
            </q-td>
          </q-tr>
        </template>
      </q-table>
    </div>

    <!-- Лот -->
    <q-dialog v-model="lotOpen" no-backdrop-dismiss>
      <q-card style="min-width: 640px; max-width: 96vw" data-testid="lotDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="inventory" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Партія реагенту' : 'Новий лот реагенту' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section>
          <div class="row q-col-gutter-sm">
            <div class="col-8"><q-input v-model="form.reagentName" outlined dense label="Назва реагенту *" data-testid="lotName" :error="touched && !form.reagentName" hide-bottom-space /></div>
            <div class="col-4"><q-input v-model="form.lotNumber" outlined dense label="Номер лота *" data-testid="lotNumber" :error="touched && !form.lotNumber" hide-bottom-space /></div>
            <div class="col-12"><q-input v-model="form.manufacturer" outlined dense label="Виробник" /></div>
            <div class="col-6"><q-select v-model="form.analyzerId" outlined dense clearable label="Аналізатор" :options="analyzerOptions" emit-value map-options /></div>
            <div class="col-6"><q-select v-model="form.testCode" outlined dense clearable label="Показник" :options="testOptions" emit-value map-options use-input input-debounce="0" @filter="filterTests" /></div>
            <div class="col-4"><q-input v-model="form.testsInitial" @input="onInitialChange" outlined dense type="number" min="1" label="Тестів у партії *" :error="touched && !(Number(form.testsInitial) > 0)" hide-bottom-space /></div>
            <div class="col-4"><q-input v-model="form.testsRemaining" @input="remainingEdited = true" outlined dense type="number" min="0" label="Залишок тестів" /></div>
            <div class="col-4"><q-input v-model="form.minimumTests" outlined dense type="number" min="0" label="Мінімальний залишок" /></div>
            <div class="col-4"><q-input v-model="form.expiryDate" outlined dense type="date" stack-label label="Придатний до *" data-testid="lotExpiry" :error="touched && !form.expiryDate" hide-bottom-space /></div>
            <div class="col-4"><q-input v-model="form.openedAt" outlined dense type="date" stack-label label="Відкрито" /></div>
            <div class="col-4"><q-input v-model="form.onboardStabilityDays" outlined dense type="number" min="0" label="Стабільність після відкриття, діб" /></div>
          </div>
          <div v-if="formError" class="text-negative q-mt-sm">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="lotSave" @click="saveLot" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Списання -->
    <q-dialog v-model="consumeOpen" no-backdrop-dismiss>
      <q-card style="min-width: 420px" data-testid="consumeDialog">
        <q-card-section class="row items-center bg-deep-orange-6 text-white q-py-sm"><q-icon name="remove_circle_outline" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">Списання тестів</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <div v-if="active" class="text-body2"><b>{{ active.reagentName }}</b> · лот {{ active.lotNumber }} · залишок <b>{{ remaining(active) }}</b> тестів</div>
          <q-input v-model.number="consumeTests" outlined dense type="number" min="1" label="Кількість тестів *" autofocus data-testid="consumeTests" @keyup.enter="doConsume" />
          <div v-if="consumeError" class="text-negative">{{ consumeError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="deep-orange-6" icon="check" label="Списати" :loading="saving" :disable="!consumeTests || consumeTests <= 0" data-testid="consumeConfirm" @click="doConsume" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Картка -->
    <q-dialog v-model="cardOpen">
      <q-card style="min-width: 520px; max-width: 96vw">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">{{ active && active.reagentName }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section v-if="active">
          <q-markup-table dense flat>
            <tbody>
              <tr><td class="text-grey-7">Лот</td><td class="mono">{{ active.lotNumber }}</td><td class="text-grey-7">Виробник</td><td>{{ active.manufacturer || '—' }}</td></tr>
              <tr><td class="text-grey-7">Аналізатор</td><td>{{ analyzerName(active.analyzerId) }}</td><td class="text-grey-7">Показник</td><td>{{ testLabel(active.testCode) }}</td></tr>
              <tr><td class="text-grey-7">Залишок</td><td>{{ remaining(active) }} / {{ initial(active) }}</td><td class="text-grey-7">Мінімальний залишок</td><td>{{ active.minimumTests != null ? active.minimumTests : '—' }}</td></tr>
              <tr><td class="text-grey-7">Придатний до</td><td :class="expired(active) ? 'text-negative' : ''">{{ active.expiryDate | date }}</td><td class="text-grey-7">Відкрито</td><td>{{ active.openedAt | date }}</td></tr>
              <tr v-if="active.onboardStabilityDays"><td class="text-grey-7">Стабільність після відкриття</td><td colspan="3">{{ active.onboardStabilityDays }} діб</td></tr>
            </tbody>
          </q-markup-table>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat color="primary" icon="edit" label="Редагувати" :disable="!canWork" @click="cardOpen = false; openLot(active)"><q-tooltip v-if="!canWork">{{ workHint }}</q-tooltip></q-btn><q-btn flat label="Закрити" v-close-popup /></q-card-actions>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="deleteOpen" title="Видалити лот?" message="Лот буде видалено; історію списань буде збережено." ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import roleMixin from '../../mixins/roleMixin';
import { parseDecimal } from '../../utils/format';

export default {
  name: 'ReagentInventory',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      touched: false, remainingEdited: false, lots: [], alerts: [], search: '', onlyProblems: false, testFilter: '',
      lotOpen: false, consumeOpen: false, cardOpen: false, deleteOpen: false,
      form: {}, formError: '', saving: false, active: null, consumeTests: 1, consumeError: '',
      columns: [
        { name: 'name', label: 'Реагент', field: 'reagentName', align: 'left', sortable: true },
        { name: 'lotNumber', label: 'Лот', field: 'lotNumber', align: 'left' },
        { name: 'analyzer', label: 'Аналізатор', align: 'left' },
        { name: 'tests', label: 'Показник', align: 'left' },
        { name: 'remaining', label: 'Залишок', field: r => r.testsRemaining, align: 'right', sortable: true },
        { name: 'minimum', label: 'Мінімум', align: 'right' },
        { name: 'expiryDate', label: 'Придатний до', field: 'expiryDate', align: 'left', sortable: true },
        { name: 'status', label: 'Стан', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    analyzers () { return this.$store.state.laboratory.analyzers; },
    analyzerOptions () { return this.analyzers.map(a => ({ value: a.id, label: a.name })); },
    // Дозволи збігаються з серверною політикою: лоти ведуть адміністратор, лікар-лаборант, лаборант; видаляють — адміністратор і лікар-лаборант
    canWork () { return this.canDo('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    workHint () { return this.roleHint('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    canDelete () { return this.canDo('LAB_DOCTOR'); },
    deleteHint () { return this.roleHint('LAB_DOCTOR'); },
    allTests () { return this.$store.getters['dictionaries/items']('tests'); },
    testOptions () { const f = this.testFilter.toLowerCase(); return this.allTests.filter(t => !f || `${t.code} ${t.name || ''}`.toLowerCase().includes(f)).map(t => ({ value: t.code, label: t.name ? `${t.name} (${t.code})` : t.code })); },
    filtered () {
      const s = (this.search || '').toLowerCase();
      return this.lots.filter(l => (!s || `${l.reagentName} ${l.lotNumber} ${l.manufacturer || ''} ${this.analyzerName(l.analyzerId)}`.toLowerCase().includes(s)) && (!this.onlyProblems || this.expired(l) || this.low(l) || this.soon(l)));
    }
  },
  created () { this.$store.dispatch('dictionaries/load', 'tests'); this.load(); },
  methods: {
    analyzerName (id) { const a = this.analyzers.find(x => x.id === id); return a ? a.name : '—'; },
    testLabel (code) { if (!code) return '—'; const t = this.allTests.find(x => x.code === code); return t && t.name ? `${t.name} (${code})` : code; },
    onInitialChange (v) { if (!this.form.id && !this.remainingEdited) this.form.testsRemaining = v; }, // у новому лоті залишок = обсяг партії, поки його не змінили вручну
    remaining (l) { return Number(l.testsRemaining) || 0; },
    initial (l) { return Number(l.testsInitial) || 0; },
    empty (l) { return this.remaining(l) <= 0; },
    ratio (l) { return this.initial(l) ? Math.min(1, this.remaining(l) / this.initial(l)) : 0; },
    low (l) { const min = Number(l.minimumTests); return min ? this.remaining(l) <= min : this.remaining(l) <= 0; },
    expired (l) { return l.expiryDate && new Date(l.expiryDate) < new Date(); },
    soon (l) { if (!l.expiryDate) return false; const d = (new Date(l.expiryDate) - new Date()) / 86400000; return d >= 0 && d <= 30; },
    rowClass (l) { if (this.expired(l)) return 'row-status--cancelled'; if (this.low(l)) return 'row-status--rejected'; if (this.soon(l)) return 'row-status--overdue'; return ''; },
    alertText (a) { return a.message || 'потребує уваги'; },
    async load () {
      const results = await Promise.allSettled([this.$api.reagentLots(), this.$api.reagentAlerts()]);
      if (results[0].status === 'fulfilled') this.lots = this.asList(results[0].value);
      if (results[1].status === 'fulfilled') this.alerts = this.asList(results[1].value);
      const failed = results.find(r => r.status === 'rejected');
      this.apiError = failed && !this.apiOffline ? failed.reason.userMessage : null;
      this.$store.dispatch('laboratory/refreshStatus');
    },
    filterTests (val, update) { update(() => { this.testFilter = val || ''; }); },
    openLot (l) {
      // Поля форми збігаються з контрактом API: reagentName, testsInitial, testsRemaining, testCode
      this.form = l ? { id: l.id, reagentName: l.reagentName, lotNumber: l.lotNumber, manufacturer: l.manufacturer || '', analyzerId: l.analyzerId, testCode: l.testCode, testsInitial: this.initial(l), testsRemaining: this.remaining(l), minimumTests: l.minimumTests, expiryDate: (l.expiryDate || '').slice(0, 10), openedAt: (l.openedAt || '').slice(0, 10), onboardStabilityDays: l.onboardStabilityDays, isActive: l.isActive !== false }
        : { id: null, reagentName: '', lotNumber: '', manufacturer: '', analyzerId: null, testCode: null, testsInitial: 100, testsRemaining: 100, minimumTests: 50, expiryDate: '', openedAt: '', onboardStabilityDays: null, isActive: true };
      this.formError = ''; this.touched = false; this.remainingEdited = false; this.lotOpen = true;
    },
    async saveLot () {
      this.touched = true; this.formError = '';
      const f = this.form;
      if (!f.reagentName || !f.lotNumber) { this.formError = 'Вкажіть назву реагенту та номер лота'; return; }
      if (!(Number(f.testsInitial) > 0)) { this.formError = 'Кількість тестів у партії має бути більшою за нуль'; return; }
      if (!f.expiryDate) { this.formError = 'Вкажіть, до якої дати придатний лот'; return; }
      this.saving = true;
      const num = v => { const n = parseDecimal(v); return n === null ? null : Math.round(n); };
      const body = { reagentName: f.reagentName, lotNumber: f.lotNumber, manufacturer: f.manufacturer || null, analyzerId: f.analyzerId || null, testCode: f.testCode || null, testsInitial: num(f.testsInitial) || 0, testsRemaining: num(f.testsRemaining), minimumTests: num(f.minimumTests) || 0, expiryDate: f.expiryDate, openedAt: f.openedAt || null, onboardStabilityDays: num(f.onboardStabilityDays), isActive: f.isActive !== false };
      try {
        if (this.form.id) await this.$api.updateReagentLot(this.form.id, body); else await this.$api.createReagentLot(body);
        this.notifyOk('Лот збережено'); this.lotOpen = false; this.load();
      } catch (e) { this.formError = e.userMessage || 'Не вдалося зберегти лот'; } finally { this.saving = false; }
    },
    openConsume (l) { this.active = l; this.consumeTests = 1; this.consumeError = ''; this.consumeOpen = true; },
    async doConsume () {
      this.saving = true; this.consumeError = '';
      try { await this.$api.consumeReagent(this.active.id, Number(this.consumeTests)); this.notifyOk(`Списано ${this.consumeTests} ${this.testsWord(this.consumeTests)}`); this.consumeOpen = false; this.load(); } catch (e) { this.consumeError = e.userMessage || 'Не вдалося списати тести'; } finally { this.saving = false; }
    },
    testsWord (n) { const m = Math.abs(Number(n)) % 100; const d = m % 10; return m > 10 && m < 20 ? 'тестів' : d === 1 ? 'тест' : d >= 2 && d <= 4 ? 'тести' : 'тестів'; },
    openCard (l) { this.active = l; this.cardOpen = true; },
    askDelete (l) { this.active = l; this.deleteOpen = true; },
    async doDelete () { try { await this.$api.deleteReagentLot(this.active.id); this.notifyOk('Лот видалено'); this.load(); } catch (e) { this.notifyError(e); } }
  }
};
</script>
