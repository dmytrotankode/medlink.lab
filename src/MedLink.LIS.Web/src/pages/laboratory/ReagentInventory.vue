<template>
  <div class="reagents-page" data-testid="reagentsPage">
    <page-header title="Склад реагентів" icon="fas fa-boxes" subtitle="Партії (лоти), терміни придатності, залишки тест-доз, автосписання" :breadcrumbs="[{ label: 'Реагенти' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn unelevated dense color="primary" icon="add" label="Новий лот" data-testid="lotCreate" @click="openLot(null)" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div v-if="alerts.length" class="q-mb-md">
      <q-banner v-for="(a, i) in alerts" :key="i" dense rounded class="q-mb-xs" :class="a.kind === 'EXPIRED' || a.severity === 'CRITICAL' ? 'bg-red-1 text-negative' : 'bg-orange-1 text-orange-9'">
        <template v-slot:avatar><q-icon :name="a.kind === 'EXPIRED' ? 'event_busy' : 'warning'" /></template>
        <b>{{ a.title || a.lotName || a.name }}</b> — {{ a.message || a.description || alertText(a) }}
      </q-banner>
    </div>

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-4"><q-input v-model="search" dense outlined clearable placeholder="Назва, лот, виробник, аналізатор"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
      <div class="col-6 col-md-3"><q-toggle v-model="onlyProblems" label="Лише з попередженнями" color="warning" /></div>
    </div>

    <div class="medlink-card">
      <q-table :data="filtered" :columns="columns" row-key="id" dense flat :loading="loading" :pagination="{ rowsPerPage: 25, sortBy: 'expiryDate' }" no-data-label="Лотів немає" data-testid="lotsTable">
        <template v-slot:body="props">
          <q-tr :props="props" :class="rowClass(props.row)" class="cursor-pointer" @click="openCard(props.row)">
            <q-td key="name" :props="props"><b>{{ props.row.name }}</b><div class="text-caption text-grey-7">{{ props.row.manufacturer }} · {{ props.row.catalogNumber || '' }}</div></q-td>
            <q-td key="lotNumber" :props="props" class="mono">{{ props.row.lotNumber }}</q-td>
            <q-td key="analyzer" :props="props">{{ analyzerName(props.row.analyzerId) }}</q-td>
            <q-td key="tests" :props="props">{{ (props.row.testCodes || []).join(', ') || props.row.testCode || '—' }}</q-td>
            <q-td key="remaining" :props="props" class="text-right">
              <div class="text-weight-bold" :class="low(props.row) ? 'text-negative' : ''">{{ props.row.remainingTests !== undefined ? props.row.remainingTests : props.row.quantity | num }} <span class="text-caption text-grey-7">/ {{ props.row.initialTests || props.row.quantityInitial | num }}</span></div>
              <q-linear-progress :value="ratio(props.row)" :color="low(props.row) ? 'negative' : 'positive'" size="4px" rounded />
            </q-td>
            <q-td key="minimum" :props="props" class="text-right">{{ props.row.minimumTests || props.row.minimum | num }}</q-td>
            <q-td key="expiryDate" :props="props" :class="expired(props.row) ? 'text-negative text-weight-bold' : (soon(props.row) ? 'text-orange-9 text-weight-bold' : '')">{{ props.row.expiryDate | date }}<div v-if="props.row.openedAt" class="text-caption text-grey-7">відкрито {{ props.row.openedAt | date }}</div></q-td>
            <q-td key="status" :props="props">
              <q-badge v-if="expired(props.row)" color="negative" label="Прострочено" />
              <q-badge v-else-if="low(props.row)" color="warning" text-color="dark" label="Мінімум" />
              <q-badge v-else-if="soon(props.row)" color="orange-7" label="Термін ≤ 30 дн" />
              <q-badge v-else color="positive" label="OK" />
            </q-td>
            <q-td key="actions" :props="props" class="text-right no-wrap" @click.stop>
              <q-btn flat dense round size="sm" icon="remove_circle_outline" color="deep-orange-6" :data-testid="`consume-${props.row.lotNumber}`" @click="openConsume(props.row)"><q-tooltip>Списати тест-дози</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="edit" color="primary" @click="openLot(props.row)" />
              <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDelete(props.row)" />
            </q-td>
          </q-tr>
        </template>
      </q-table>
    </div>

    <!-- Лот -->
    <q-dialog v-model="lotOpen" persistent>
      <q-card style="min-width: 640px; max-width: 96vw" data-testid="lotDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="inventory" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Партія реагенту' : 'Новий лот реагенту' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section>
          <div class="row q-col-gutter-sm">
            <div class="col-8"><q-input v-model="form.name" outlined dense label="Назва реагенту *" data-testid="lotName" /></div>
            <div class="col-4"><q-input v-model="form.lotNumber" outlined dense label="Лот *" data-testid="lotNumber" /></div>
            <div class="col-6"><q-input v-model="form.manufacturer" outlined dense label="Виробник" /></div>
            <div class="col-6"><q-input v-model="form.catalogNumber" outlined dense label="Каталожний №" /></div>
            <div class="col-6"><q-select v-model="form.analyzerId" outlined dense clearable label="Аналізатор" :options="analyzerOptions" emit-value map-options /></div>
            <div class="col-6"><q-select v-model="form.testCodes" outlined dense multiple use-chips label="Показники" :options="testCodes" use-input input-debounce="0" @filter="filterTests" /></div>
            <div class="col-4"><q-input v-model="form.initialTests" outlined dense type="number" label="Тест-доз у партії *" /></div>
            <div class="col-4"><q-input v-model="form.remainingTests" outlined dense type="number" label="Залишок" /></div>
            <div class="col-4"><q-input v-model="form.minimumTests" outlined dense type="number" label="Мінімальний залишок" /></div>
            <div class="col-4"><q-input v-model="form.expiryDate" outlined dense type="date" stack-label label="Придатний до *" data-testid="lotExpiry" /></div>
            <div class="col-4"><q-input v-model="form.openedAt" outlined dense type="date" stack-label label="Відкрито" /></div>
            <div class="col-4"><q-input v-model="form.onboardStabilityDays" outlined dense type="number" label="On-board стабільність, дн" /></div>
            <div class="col-12"><q-input v-model="form.storageConditions" outlined dense label="Умови зберігання" /></div>
          </div>
          <div v-if="formError" class="text-negative q-mt-sm">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" :disable="!form.name || !form.lotNumber || !form.expiryDate" data-testid="lotSave" @click="saveLot" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Списання -->
    <q-dialog v-model="consumeOpen" persistent>
      <q-card style="min-width: 420px" data-testid="consumeDialog">
        <q-card-section class="row items-center bg-deep-orange-6 text-white q-py-sm"><q-icon name="remove_circle_outline" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">Списання тест-доз</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <div v-if="active" class="text-body2"><b>{{ active.name }}</b> · лот {{ active.lotNumber }} · залишок <b>{{ active.remainingTests !== undefined ? active.remainingTests : active.quantity }}</b></div>
          <q-input v-model.number="consumeTests" outlined dense type="number" min="1" label="Кількість тестів *" autofocus data-testid="consumeTests" @keyup.enter="doConsume" />
          <div v-if="consumeError" class="text-negative">{{ consumeError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="deep-orange-6" icon="check" label="Списати" :loading="saving" :disable="!consumeTests || consumeTests <= 0" data-testid="consumeConfirm" @click="doConsume" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Картка -->
    <q-dialog v-model="cardOpen">
      <q-card style="min-width: 520px; max-width: 96vw">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">{{ active && active.name }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section v-if="active">
          <q-markup-table dense flat>
            <tbody>
              <tr><td class="text-grey-7">Лот</td><td class="mono">{{ active.lotNumber }}</td><td class="text-grey-7">Виробник</td><td>{{ active.manufacturer || '—' }}</td></tr>
              <tr><td class="text-grey-7">Аналізатор</td><td>{{ analyzerName(active.analyzerId) }}</td><td class="text-grey-7">Показники</td><td>{{ (active.testCodes || []).join(', ') || '—' }}</td></tr>
              <tr><td class="text-grey-7">Залишок</td><td>{{ active.remainingTests !== undefined ? active.remainingTests : active.quantity }} / {{ active.initialTests || active.quantityInitial }}</td><td class="text-grey-7">Мінімум</td><td>{{ active.minimumTests || active.minimum || '—' }}</td></tr>
              <tr><td class="text-grey-7">Придатний до</td><td :class="expired(active) ? 'text-negative' : ''">{{ active.expiryDate | date }}</td><td class="text-grey-7">Відкрито</td><td>{{ active.openedAt | date }}</td></tr>
              <tr><td class="text-grey-7">Зберігання</td><td colspan="3">{{ active.storageConditions || '—' }}</td></tr>
            </tbody>
          </q-markup-table>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat color="primary" icon="edit" label="Редагувати" @click="cardOpen = false; openLot(active)" /><q-btn flat label="Закрити" v-close-popup /></q-card-actions>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="deleteOpen" title="Видалити лот" message="Партію реагенту буде видалено (soft delete з аудитом)." ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';

export default {
  name: 'ReagentInventory',
  mixins: [apiMixin],
  data () {
    return {
      lots: [], alerts: [], search: '', onlyProblems: false, testFilter: '',
      lotOpen: false, consumeOpen: false, cardOpen: false, deleteOpen: false,
      form: {}, formError: '', saving: false, active: null, consumeTests: 1, consumeError: '',
      columns: [
        { name: 'name', label: 'Реагент', field: 'name', align: 'left', sortable: true },
        { name: 'lotNumber', label: 'Лот', field: 'lotNumber', align: 'left' },
        { name: 'analyzer', label: 'Аналізатор', align: 'left' },
        { name: 'tests', label: 'Показники', align: 'left' },
        { name: 'remaining', label: 'Залишок', field: r => r.remainingTests !== undefined ? r.remainingTests : r.quantity, align: 'right', sortable: true },
        { name: 'minimum', label: 'Мін.', align: 'right' },
        { name: 'expiryDate', label: 'Придатний до', field: 'expiryDate', align: 'left', sortable: true },
        { name: 'status', label: 'Стан', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    analyzers () { return this.$store.state.laboratory.analyzers; },
    analyzerOptions () { return this.analyzers.map(a => ({ value: a.id, label: a.name })); },
    testCodes () { const f = this.testFilter.toLowerCase(); return this.$store.getters['dictionaries/items']('tests').map(t => t.code).filter(c => !f || c.toLowerCase().includes(f)); },
    filtered () {
      const s = (this.search || '').toLowerCase();
      return this.lots.filter(l => (!s || `${l.name} ${l.lotNumber} ${l.manufacturer} ${this.analyzerName(l.analyzerId)}`.toLowerCase().includes(s)) && (!this.onlyProblems || this.expired(l) || this.low(l) || this.soon(l)));
    }
  },
  created () { this.$store.dispatch('dictionaries/load', 'tests'); this.load(); },
  methods: {
    analyzerName (id) { const a = this.analyzers.find(x => x.id === id); return a ? a.name : (id ? String(id).slice(0, 8) : '—'); },
    remaining (l) { return Number(l.remainingTests !== undefined ? l.remainingTests : l.quantity) || 0; },
    initial (l) { return Number(l.initialTests || l.quantityInitial) || 0; },
    ratio (l) { return this.initial(l) ? Math.min(1, this.remaining(l) / this.initial(l)) : 0; },
    low (l) { const min = Number(l.minimumTests || l.minimum); return min ? this.remaining(l) <= min : this.remaining(l) <= 0; },
    expired (l) { return l.expiryDate && new Date(l.expiryDate) < new Date(); },
    soon (l) { if (!l.expiryDate) return false; const d = (new Date(l.expiryDate) - new Date()) / 86400000; return d >= 0 && d <= 30; },
    rowClass (l) { if (this.expired(l)) return 'row-status--cancelled'; if (this.low(l)) return 'row-status--rejected'; if (this.soon(l)) return 'row-status--overdue'; return ''; },
    alertText (a) { return a.kind === 'EXPIRED' ? 'термін придатності вичерпано' : (a.kind === 'LOW' || a.kind === 'MINIMUM' ? `залишок ${a.remainingTests} нижче мінімуму ${a.minimumTests}` : JSON.stringify(a)); },
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
      this.form = l ? { ...l, expiryDate: (l.expiryDate || '').slice(0, 10), openedAt: (l.openedAt || '').slice(0, 10), testCodes: l.testCodes || (l.testCode ? [l.testCode] : []), remainingTests: this.remaining(l), initialTests: this.initial(l), minimumTests: l.minimumTests || l.minimum || null }
        : { id: null, name: '', lotNumber: '', manufacturer: '', catalogNumber: '', analyzerId: null, testCodes: [], initialTests: 100, remainingTests: 100, minimumTests: 20, expiryDate: '', openedAt: '', onboardStabilityDays: null, storageConditions: '+2…+8 °C' };
      this.formError = ''; this.lotOpen = true;
    },
    async saveLot () {
      this.saving = true; this.formError = '';
      const body = { ...this.form, initialTests: Number(this.form.initialTests) || 0, remainingTests: Number(this.form.remainingTests) || 0, minimumTests: Number(this.form.minimumTests) || 0, onboardStabilityDays: this.form.onboardStabilityDays ? Number(this.form.onboardStabilityDays) : null, openedAt: this.form.openedAt || null };
      delete body.id;
      try {
        if (this.form.id) await this.$api.updateReagentLot(this.form.id, body); else await this.$api.createReagentLot(body);
        this.notifyOk('Лот збережено'); this.lotOpen = false; this.load();
      } catch (e) { this.formError = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    },
    openConsume (l) { this.active = l; this.consumeTests = 1; this.consumeError = ''; this.consumeOpen = true; },
    async doConsume () {
      this.saving = true; this.consumeError = '';
      try { await this.$api.consumeReagent(this.active.id, Number(this.consumeTests)); this.notifyOk(`Списано ${this.consumeTests} тест.`); this.consumeOpen = false; this.load(); } catch (e) { this.consumeError = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    },
    openCard (l) { this.active = l; this.cardOpen = true; },
    askDelete (l) { this.active = l; this.deleteOpen = true; },
    async doDelete () { try { await this.$api.deleteReagentLot(this.active.id); this.notifyOk('Лот видалено'); this.load(); } catch (e) { this.notifyError(e); } }
  }
};
</script>
