<!--
  ТЗ 23.4 (FR-DST-001…012): «Вхідні направлення» — розподіл направлень МІС по аналізаторах лаборантом
  (пропозиція → зміна приладу → «Авторозподіл» → «Підтвердити й поставити в черги»), без обладнання — send-out / ручне / відхилення.
  ТЗ 21.6, 23.3: емулятор МІС (направлення з обраними показниками та пакетами) і журнал направлень МІС з подіями.
-->
<template>
  <div data-testid="incomingReferralsPage">
    <page-header title="Вхідні направлення" icon="fas fa-inbox" :breadcrumbs="[{ label: 'Вхідні направлення' }]">
      <q-btn-toggle v-model="state" dense unelevated toggle-color="primary" color="grey-2" text-color="grey-9" class="q-mr-sm"
        :options="[{ label: 'Очікують', value: 'pending' }, { label: 'Усі за добу', value: 'all' }]" />
      <q-select v-model="source" dense outlined clearable emit-value map-options :options="sourceOptions" label="Джерело" style="min-width: 190px" class="q-mr-sm" />
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8 q-mb-sm">
      <q-tab name="inbox" icon="move_to_inbox" label="Розподіл" />
      <q-tab v-if="canEmulate" name="emulator" icon="fas fa-hospital-user" label="Емулятор МІС" data-testid="misEmulatorTab" />
      <q-tab name="journal" icon="list_alt" label="Журнал направлень МІС" />
    </q-tabs>

    <!-- Розподіл -->
    <div v-show="tab === 'inbox'" class="row q-col-gutter-md">
      <div class="col-12 col-md-5">
        <div class="medlink-card">
          <q-list separator dense>
            <q-item v-for="r in inboxItems" :key="r.orderId" clickable :active="current && current.orderId === r.orderId" active-class="bg-blue-1" :data-testid="`inbox-${r.referralNumber}`" @click="open(r.orderId)">
              <q-item-section avatar>
                <q-icon :name="sourceIcon(r.source)" :color="r.isCito ? 'deep-orange-6' : 'primary'" />
              </q-item-section>
              <q-item-section>
                <q-item-label>
                  <b>{{ r.referralNumber }}</b> <q-badge v-if="r.isCito" color="deep-orange-6" label="CITO" />
                  <q-badge v-if="r.packageCode" color="purple-5" class="q-ml-xs" :label="r.packageName || r.packageCode" />
                </q-item-label>
                <q-item-label caption>{{ r.patientName }} · {{ r.sourceLabel }}<span v-if="r.department"> · {{ r.department }}</span><span v-if="r.bed"> · {{ r.bed }}</span></q-item-label>
                <q-item-label caption>{{ r.services.join(', ') }} · <span v-if="r.indicators">показників {{ r.indicators }}</span><span v-else class="text-negative">показники не обрані</span></q-item-label>
              </q-item-section>
              <q-item-section side>
                <q-badge :color="stateColor(r)" :label="r.state" />
                <div class="text-caption text-grey-7">{{ r.receivedAt | datetime }}</div>
                <div v-if="r.overdue" class="text-caption text-negative row items-center no-wrap"><q-icon name="timer_off" class="q-mr-xs" />час очікування вичерпано<q-tooltip>Розподіл триває довше {{ inbox.targetMinutes }} хв</q-tooltip></div>
              </q-item-section>
            </q-item>
          </q-list>
          <empty-state v-if="!inboxItems.length" title="Направлень немає" hint="Направлення МІС з'являються тут після надходження" icon="inbox" />
        </div>
      </div>

      <div class="col-12 col-md-7">
        <div v-if="current" class="medlink-card q-pa-sm" data-testid="distributionPanel">
          <div class="row items-center q-mb-sm">
            <div class="col">
              <div class="text-subtitle1 text-weight-bold">{{ current.referralNumber || current.orderNumber }} · {{ current.patientName }}</div>
              <div class="text-caption text-grey-7">
                {{ current.department || '—' }}<span v-if="current.bed"> · {{ current.bed }}</span>
                <span v-if="current.packageCode"> · пакет {{ current.packageName || current.packageCode }}</span> · направлення в лабораторії
                <router-link :to="{ name: 'lab-order-card', params: { id: current.orderId } }" class="text-primary">{{ current.orderNumber }}</router-link>
              </div>
            </div>
            <q-badge :color="current.distributionStatus === 'CONFIRMED' ? 'positive' : current.distributionStatus === 'NEEDS_SELECTION' ? 'negative' : 'orange-7'" :label="current.state" />
          </div>

          <div v-for="item in current.items" :key="item.itemId" class="q-mb-md">
            <div class="row items-center q-mb-xs">
              <div class="text-weight-bold">{{ item.interventionName }}</div>
              <q-space />
              <q-btn v-if="item.status === 'NEEDS_SELECTION' || item.status === 'ACTIVE'" flat dense size="sm" color="primary" icon="checklist" label="Обрати показники" @click="openIndicators(item)" />
            </div>
            <q-banner v-if="item.status === 'NEEDS_SELECTION'" dense class="bg-red-1 text-negative q-mb-xs">Показники не передано — оберіть показники для цієї послуги</q-banner>
            <q-banner v-if="item.status === 'UNMAPPED'" dense class="bg-orange-1 text-orange-9 q-mb-xs">Інтервенцію не співставлено з послугою ЛІС — задача адміністратору</q-banner>
            <q-banner v-if="item.splitWarning" dense class="bg-amber-1 text-amber-10 q-mb-xs"><q-icon name="call_split" /> {{ item.splitWarning }}</q-banner>
            <q-markup-table v-if="item.tests.length" dense flat bordered>
              <thead><tr><th class="text-left">Показник</th><th class="text-left">Прилад</th><th class="text-left">Черга · час виконання</th><th class="text-left">Стан</th></tr></thead>
              <tbody>
                <tr v-for="t in item.tests" :key="t.orderTestId" :class="t.routingStatus === 'NO_EQUIPMENT' ? 'bg-red-1' : ''">
                  <td><b>{{ t.code }}</b>&nbsp;<span class="text-grey-7">{{ t.name }}</span> <q-badge v-if="t.isRequired" outline color="grey-7" label="обов’язковий" class="q-ml-xs" /></td>
                  <td style="min-width: 220px">
                    <q-select v-if="editable(t)" dense outlined emit-value map-options :value="t.analyzerId" :options="candidateOptions(t)" :data-testid="`analyzer-${t.code}`"
                      @input="v => change(t, v)">
                      <template v-slot:no-option><q-item><q-item-section class="text-grey">Немає приладів</q-item-section></q-item></template>
                    </q-select>
                    <span v-else>{{ t.analyzer || (t.routingStatus === 'SENDOUT' ? 'Зовнішня лабораторія' : '—') }}</span>
                    <div v-if="t.routedBy === 'MANUAL'" class="text-caption text-orange-8">змінено вручну</div>
                  </td>
                  <td class="text-caption">{{ queueInfo(t) }}</td>
                  <td>
                    <q-badge :color="routingColor(t.routingStatus)" :label="routingLabel(t.routingStatus)" />
                    <q-select v-if="t.routingStatus === 'NO_EQUIPMENT' && current.distributionStatus !== 'CONFIRMED'" v-model="noEq[t.orderTestId]" dense outlined emit-value map-options class="q-mt-xs"
                      :options="noEqOptions" label="Рішення" style="min-width: 170px" />
                  </td>
                </tr>
              </tbody>
            </q-markup-table>
            <div v-if="item.notOrdered && item.notOrdered.length" class="text-caption text-grey-7 q-mt-xs">Не обрані лікарем: {{ sortedNames(item.notOrdered).join(', ') }}</div>
          </div>

          <div class="row q-gutter-sm justify-end">
            <q-btn flat color="grey-8" icon="autorenew" label="Авторозподіл" :disable="current.distributionStatus === 'NEEDS_SELECTION'" @click="auto" />
            <q-btn unelevated color="primary" icon="playlist_add_check" label="Підтвердити й поставити в черги" data-testid="confirmDistribution"
              :disable="current.distributionStatus === 'NEEDS_SELECTION' || current.distributionStatus === 'CONFIRMED'" @click="confirm" />
          </div>
        </div>
        <empty-state v-else title="Оберіть направлення" hint="Ліворуч — вхідні направлення; праворуч з'явиться пропозиція розподілу по аналізаторах" icon="call_split" />
      </div>
    </div>

    <!-- Емулятор МІС -->
    <div v-show="tab === 'emulator'" class="medlink-card q-pa-md" data-testid="misEmulator">
      <div class="text-caption text-grey-7 q-mb-sm">Імітація передачі направлення з МІС: оберіть послуги та показники й надішліть направлення в ЛІС. Інструмент для перевірки, доступний адміністратору.</div>
      <div class="row q-col-gutter-sm">
        <div class="col-12 col-md-4"><patient-select v-model="em.patient" :allow-create="false" /></div>
        <div class="col-6 col-md-2"><q-select v-model="em.context" dense outlined emit-value map-options :options="[{ label: 'Стаціонар', value: 'INPATIENT' }, { label: 'Амбулаторія', value: 'AMBULATORY' }]" label="Контур" /></div>
        <div class="col-6 col-md-2"><q-select v-model="em.source" dense outlined emit-value map-options clearable :options="sourceOptions" label="Джерело" /></div>
        <div class="col-6 col-md-2"><q-input v-model="em.department" dense outlined label="Відділення" /></div>
        <div class="col-3 col-md-1"><q-input v-model="em.ward" dense outlined label="Палата" /></div>
        <div class="col-3 col-md-1"><q-input v-model="em.bed" dense outlined label="Ліжко" /></div>
        <div class="col-6 col-md-3"><q-input v-model="em.doctorName" dense outlined label="Лікар" /></div>
        <div class="col-6 col-md-3"><q-input v-model="em.ehealthReferralNumber" dense outlined label="№ е-направлення (зовнішній контур)" hint="Заповнюйте, лише якщо направлення створено в ЕСОЗ" /></div>
        <div class="col-6 col-md-3"><q-select v-model="em.package" dense outlined clearable :options="packages" option-label="name" label="Пакет послуг" @input="applyPackage" /></div>
        <div class="col-6 col-md-3 row items-center"><q-toggle v-model="em.cito" label="CITO" color="deep-orange-6" /></div>
      </div>
      <q-separator class="q-my-sm" />
      <div class="row q-col-gutter-sm">
        <div v-for="svc in catalog" :key="svc.interventionCode" class="col-12 col-md-6 col-lg-4">
          <q-card flat bordered :class="em.selected[svc.interventionCode] ? 'bg-blue-1' : ''">
            <q-card-section class="q-pa-sm">
              <q-checkbox :value="!!em.selected[svc.interventionCode]" dense @input="toggleService(svc, $event)">
                <b>{{ svc.name }}</b>&nbsp;<span class="text-caption text-grey-7">{{ svc.interventionCode }}</span>
              </q-checkbox>
              <q-badge class="q-ml-xs" :color="readinessColor(svc.readiness)" :label="readinessLabel(svc.readiness)" />
              <div v-if="em.selected[svc.interventionCode] && svc.mode !== 'FIXED'" class="q-mt-xs">
                <q-checkbox v-for="i in svc.indicators" :key="i.code" v-model="em.selected[svc.interventionCode]" :val="i.code" dense class="q-mr-sm" :disable="i.isRequired && svc.mode === 'SELECTABLE'">
                  <span :class="i.performedInHouse ? '' : 'text-negative'">{{ i.name || i.code }}<q-tooltip>{{ i.code }}{{ i.performedInHouse ? '' : ' — немає приладу' }}</q-tooltip></span>
                </q-checkbox>
              </div>
              <div v-else-if="svc.mode === 'FIXED'" class="text-caption text-grey-7">Фіксований склад: {{ svc.indicators.map(i => i.name || i.code).join(', ') }}</div>
            </q-card-section>
          </q-card>
        </div>
      </div>
      <div class="row justify-end q-mt-sm">
        <div v-if="!em.patient || !Object.keys(em.selected).length" class="text-caption text-grey-7 self-center q-mr-sm">Оберіть пацієнта та хоча б одну послугу</div>
        <q-btn unelevated color="primary" icon="send" label="Надіслати направлення в ЛІС" :disable="!em.patient || !Object.keys(em.selected).length" data-testid="sendMisReferral" @click="sendReferral" />
      </div>
    </div>

    <!-- Журнал МІС -->
    <div v-show="tab === 'journal'" class="medlink-card">
      <q-table :data="journal" :columns="journalColumns" row-key="orderId" dense flat :pagination="{ rowsPerPage: 25 }" :rows-per-page-options="[25, 50, 100]" no-data-label="Направлень МІС немає">
        <template v-slot:body-cell-order="props">
          <q-td :props="props"><router-link :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" class="text-primary">{{ props.row.orderNumber }}</router-link></q-td>
        </template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn v-if="props.row.misStatus === 'В роботі' && canCancelInMis" flat dense size="sm" color="negative" icon="cancel" label="Скасувати в МІС" @click="cancelReferral(props.row)" />
          </q-td>
        </template>
      </q-table>
    </div>

    <!-- Скасування направлення в МІС: причина обов'язкова, дію не можна відмінити -->
    <q-dialog v-model="cancelOpen" persistent>
      <q-card style="width: 440px" data-testid="cancelMisDialog">
        <q-card-section class="text-subtitle1 text-weight-bold">{{ cancelTitle }}</q-card-section>
        <q-card-section class="q-pt-none">
          <div class="text-caption text-negative q-mb-sm">Дію не можна відмінити: подію про скасування буде надіслано в МІС.</div>
          <q-input v-model="cancelReason" dense outlined autofocus type="textarea" rows="2" label="Причина скасування *" :error="cancelTouched && !cancelReason.trim()" error-message="Вкажіть причину" @input="cancelTouched = true" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Не скасовувати" v-close-popup />
          <q-btn unelevated color="negative" label="Скасувати направлення" data-testid="cancelMisConfirm" @click="confirmCancel" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Вибір показників -->
    <q-dialog v-model="indOpen">
      <q-card style="width: 520px; max-width: 96vw">
        <q-card-section class="row items-center bg-primary text-white q-py-sm">
          <div class="text-subtitle1 text-weight-bold">Показники: {{ indItem && indItem.interventionName }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section>
          <div class="row items-center q-mb-xs">
            <span class="text-caption text-grey-7">Обрано {{ indSelected.length }} з {{ indOptions.length }}</span><q-space />
            <q-btn flat dense no-caps size="sm" color="primary" label="Вибрати всі" @click="indSelected = indOptions.map(o => o.value)" />
            <q-btn flat dense no-caps size="sm" color="grey-8" label="Зняти всі" @click="indSelected = []" />
          </div>
          <q-option-group v-model="indSelected" type="checkbox" :options="indOptions" dense style="max-height: 46vh; overflow: auto" />
          <q-select v-model="indBy" dense outlined emit-value map-options class="q-mt-sm" label="Хто обрав" :options="[{ label: 'Реєстратор', value: 'REGISTRAR' }, { label: 'Лікар-лаборант', value: 'LAB_DOCTOR' }, { label: 'Лікар, що направив', value: 'REFERRING_DOCTOR' }]" />
          <q-input v-model="indReason" dense outlined class="q-mt-sm" label="Причина (для дозамовлення)" hint="Потрібна, лише якщо дозамовляєте показники" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Зберегти вибір" :disable="!indSelected.length" @click="saveIndicators" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';
import { ACTION_ROLES } from '../../../utils/menuAccess';
import PatientSelect from '../../../components/common/PatientSelect.vue';

const ROUTING = { PROPOSED: ['Пропозиція', 'orange-7'], ROUTED: ['У черзі', 'positive'], NO_EQUIPMENT: ['Без обладнання', 'negative'], SENDOUT: ['Зовнішня лабораторія', 'indigo-6'], MANUAL: ['Ручне', 'teal-6'] };
const READINESS = { READY: ['Прилад є', 'positive'], PARTIAL: ['Прилад є частково', 'orange-7'], NO_EQUIPMENT: ['Немає приладу', 'negative'], UNMAPPED: ['Не співставлено', 'grey-6'] };
const SOURCE_LABELS = { INPATIENT: 'Стаціонар', POLYCLINIC: 'Поліклініка', EHEALTH: 'Е-направлення ЕСОЗ', PARTNER: 'Партнер' };
const DIST_LABELS = { NONE: 'Немає', PROPOSED: 'Запропоновано', NEEDS_SELECTION: 'Потрібен вибір показників', CONFIRMED: 'Підтверджено' };
const dash = v => (v === null || v === undefined || v === '' ? '—' : v);

export default {
  name: 'IncomingReferralsPage',
  components: { PatientSelect },
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      tab: 'inbox', state: 'pending', source: null, inbox: {}, current: null, noEq: {},
      catalog: [], packages: [], journal: [],
      em: { patient: null, context: 'INPATIENT', source: null, department: 'Хірургічне відділення', ward: '', bed: '', doctorName: 'Лікар МІС', ehealthReferralNumber: '', package: null, cito: false, selected: {} },
      cancelOpen: false, cancelRow: null, cancelReason: '', cancelTouched: false,
      indOpen: false, indItem: null, indSelected: [], indBy: 'REGISTRAR', indReason: '',
      sourceOptions: [{ label: 'Стаціонар', value: 'INPATIENT' }, { label: 'Поліклініка', value: 'POLYCLINIC' }, { label: 'Е-направлення ЕСОЗ', value: 'EHEALTH' }, { label: 'Партнер', value: 'PARTNER' }],
      noEqOptions: [{ label: 'Зовнішня лабораторія', value: 'SENDOUT' }, { label: 'Ручне виконання', value: 'MANUAL' }, { label: 'Відхилити', value: 'REJECT' }],
      journalColumns: [
        { name: 'number', label: '№ направлення МІС', field: r => r.referralNumber || 'Без номера', align: 'left' },
        { name: 'order', label: 'Направлення в лабораторії', field: 'orderNumber', align: 'left' },
        { name: 'at', label: 'Надійшло', field: r => this.$options.filters.datetime(r.createdOn), align: 'left' },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'source', label: 'Джерело', field: r => dash(SOURCE_LABELS[r.source] || r.source), align: 'left' },
        { name: 'package', label: 'Пакет', field: r => dash(r.packageCode), align: 'left' },
        { name: 'items', label: 'Послуги', field: r => dash((r.items || []).join(', ')), align: 'left', classes: 'text-caption' },
        { name: 'dist', label: 'Розподіл', field: r => dash(DIST_LABELS[r.distributionStatus] || r.distributionStatus), align: 'left' },
        { name: 'mis', label: 'Статус для МІС', field: 'misStatus', align: 'left' },
        { name: 'actions', label: '', field: 'orderId' }
      ]
    };
  },
  computed: {
    canEmulate () { return this.canDo(...ACTION_ROLES.misEmulator); },
    canCancelInMis () { return this.canDo(...ACTION_ROLES.cancelInMis); },
    tests () { return this.$store.getters['dictionaries/items']('tests'); },
    /** У режимі «Очікують» — лише направлення, що потребують дії (те саме правило, що й лічильник у вкладці) */
    inboxItems () {
      const items = (this.inbox && this.inbox.items) || [];
      return this.state === 'pending' ? items.filter(r => r.pending !== false && r.distributionStatus !== 'CONFIRMED') : items;
    },
    cancelTitle () {
      const r = this.cancelRow;
      if (!r) return 'Скасувати направлення?';
      return r.referralNumber ? `Скасувати направлення ${r.referralNumber}?` : `Скасувати направлення до замовлення ${r.orderNumber}?`;
    },
    indOptions () {
      if (!this.indItem) return [];
      const all = [...this.indItem.tests.map(t => t.code), ...(this.indItem.notOrdered || [])];
      return [...new Set(all)].map(c => ({ label: this.testName(c), value: c })).sort((a, b) => a.label.localeCompare(b.label, 'uk'));
    }
  },
  watch: {
    state () { this.load(); },
    source () { this.load(); },
    tab (v) { if (v === 'emulator' && !this.catalog.length) this.loadCatalog(); if (v === 'journal') this.loadJournal(); }
  },
  created () {
    this.$store.dispatch('dictionaries/loadMany', ['tests']);
    this.load();
    if (this.$route.query.order) this.open(this.$route.query.order);
  },
  methods: {
    /** Назва показника за кодом (код — після назви); якщо довідник ще не завантажено — сам код */
    testName (code) {
      const t = this.tests.find(x => x.code === code);
      return t ? `${t.name} (${code})` : code;
    },
    /** Назви показників за кодами, за абеткою (без кодів) */
    sortedNames (codes) {
      return (codes || []).map(c => { const t = this.tests.find(x => x.code === c); return t ? t.name : c; }).sort((a, b) => a.localeCompare(b, 'uk'));
    },
    sourceIcon (s) { return { INPATIENT: 'fas fa-procedures', POLYCLINIC: 'fas fa-clinic-medical', EHEALTH: 'fas fa-globe', PARTNER: 'fas fa-handshake' }[s] || 'description'; },
    stateColor (r) { return r.distributionStatus === 'NEEDS_SELECTION' ? 'negative' : r.pending ? 'orange-7' : 'positive'; },
    routingLabel (s) { return (ROUTING[s] || [s || '—'])[0]; },
    routingColor (s) { return (ROUTING[s] || [s, 'grey-6'])[1]; },
    readinessLabel (s) { return (READINESS[s] || [s])[0]; },
    readinessColor (s) { return (READINESS[s] || [s, 'grey-6'])[1]; },
    editable (t) { return ['PROPOSED', 'NO_EQUIPMENT', 'ROUTED'].includes(t.routingStatus) && t.status !== 'REJECTED' && (t.candidates || []).length; },
    candidateOptions (t) {
      return (t.candidates || []).map(c => ({
        value: c.analyzerId, disable: !c.available,
        label: `${c.name} · пріоритет ${c.priority} · черга ${c.queue}${c.available ? '' : ' — ' + c.reason}`
      }));
    },
    queueInfo (t) {
      const c = (t.candidates || []).find(x => x.analyzerId === t.analyzerId);
      return c ? `черга ${c.queue} · ~${c.avgTatMinutes} хв` : '—';
    },
    async load () {
      await this.callApi(async () => {
        this.inbox = await this.$api.distributionInbox({ state: this.state, source: this.source });
        if (this.current) await this.open(this.current.orderId);
      });
    },
    async open (orderId) {
      const p = await this.callApi(() => this.$api.distribution(orderId), { silent: true });
      if (p) { this.current = p; this.noEq = {}; }
    },
    async change (t, analyzerId) {
      const p = await this.callApi(() => this.$api.changeDistribution(this.current.orderId, [{ orderTestId: t.orderTestId, analyzerId, reason: 'Зміна приладу лаборантом' }]));
      if (p) this.current = p;
    },
    async auto () {
      const p = await this.callApi(() => this.$api.autoDistribution(this.current.orderId), { success: 'Розподіл перераховано за правилом' });
      if (p) this.current = p;
    },
    async confirm () {
      const noEquipment = Object.keys(this.noEq).filter(k => this.noEq[k]).map(k => ({ orderTestId: k, action: this.noEq[k], reason: this.noEq[k] === 'REJECT' ? 'Немає обладнання' : null }));
      const r = await this.callApi(() => this.$api.confirmDistribution(this.current.orderId, { noEquipment }), { success: 'Розподіл підтверджено — тести в чергах приладів' });
      if (r) {
        (r.warnings || []).forEach(w => this.$q.notify({ type: 'warning', message: w }));
        this.current = r.proposal;
        this.load();
      }
    },
    openIndicators (item) {
      this.indItem = item; this.indSelected = item.tests.map(t => t.code); this.indReason = ''; this.indOpen = true;
    },
    async saveIndicators () {
      const r = await this.callApi(() => this.$api.setItemIndicators(this.current.orderId, this.indItem.itemId, { indicators: this.indSelected, selectedBy: this.indBy, reason: this.indReason || null }), { success: 'Показники збережено' });
      if (r) { this.indOpen = false; this.open(this.current.orderId); this.load(); }
    },
    async loadCatalog () {
      await this.callApi(async () => {
        const [c, p] = await Promise.all([this.$api.misCatalog(), this.$api.misPackages()]);
        this.catalog = c.services.filter(s => !s.isPathology); this.packages = p;
      });
    },
    async loadJournal () { this.journal = (await this.callApi(() => this.$api.misReferrals(200))) || []; },
    toggleService (svc, on) {
      const sel = { ...this.em.selected };
      if (on) sel[svc.interventionCode] = svc.indicators.filter(i => i.isDefault || i.isRequired).map(i => i.code);
      else delete sel[svc.interventionCode];
      this.em.selected = sel;
    },
    applyPackage (p) {
      if (!p) return;
      const sel = {};
      (p.items || []).forEach(i => { sel[i.serviceCode] = i.indicators || []; });
      this.em.selected = sel; this.em.cito = !!p.isCito;
      if (p.context && p.context !== 'ANY') this.em.context = p.context;
    },
    async sendReferral () {
      const e = this.em;
      const body = {
        misReferralId: this.uuid(), referralNumber: `НП-${Date.now().toString().slice(-6)}`, context: e.context, source: e.source, patientId: e.patient.id,
        department: e.department, ward: e.ward || null, bed: e.bed || null, doctorName: e.doctorName, doctorId: this.$store.state.context.employeeId || null,
        priority: e.cito ? 'CITO' : 'ROUTINE', ehealthReferralNumber: e.ehealthReferralNumber || null,
        package: e.package ? { code: e.package.code, name: e.package.name, cito: e.package.isCito } : null,
        items: Object.keys(e.selected).map(code => ({ interventionCode: code, indicators: e.selected[code] }))
      };
      const r = await this.callApi(() => this.$api.createMisReferral(body), { success: 'Направлення прийнято ЛІС' });
      if (r) {
        (r.warnings || []).forEach(w => this.$q.notify({ type: 'warning', message: w, timeout: 6000 }));
        this.em.selected = {}; this.em.package = null;
        this.tab = 'inbox'; this.state = 'all';
        await this.load(); this.open(r.orderId);
      }
    },
    uuid () {
      if (window.crypto && window.crypto.randomUUID) return window.crypto.randomUUID();
      return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => { const r = Math.random() * 16 | 0; return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16); });
    },
    cancelReferral (row) {
      this.cancelRow = row; this.cancelReason = ''; this.cancelTouched = false; this.cancelOpen = true;
    },
    async confirmCancel () {
      const reason = this.cancelReason.trim();
      if (!reason) { this.cancelTouched = true; return; }
      const row = this.cancelRow;
      this.cancelOpen = false;
      await this.callApi(() => this.$api.cancelMisReferral(row.orderId, reason), { success: 'Скасовано, подію надіслано в МІС' });
      this.loadJournal(); this.load();
    }
  }
};
</script>
