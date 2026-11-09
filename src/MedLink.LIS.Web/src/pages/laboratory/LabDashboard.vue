<template>
  <div class="lab-dashboard" data-testid="dashboardPage">
    <page-header title="Дашборд лабораторії" icon="fas fa-tachometer-alt">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
      <q-btn unelevated color="primary" icon="add" label="Нове направлення" data-testid="dashCreateOrder" :to="{ name: 'lab-orders', query: { create: 1 } }" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <!-- Лічильники -->
    <div class="row q-col-gutter-md q-mb-md">
      <div v-for="tile in tiles" :key="tile.key" class="col-6 col-md-3">
        <router-link :to="tile.to" style="text-decoration: none">
          <div class="kpi-tile" :class="tile.cls">
            <div class="kpi-tile__label">{{ tile.label }}</div>
            <div class="kpi-tile__value">{{ tile.value }}</div>
            <div class="kpi-tile__hint">{{ tile.hint }}</div>
          </div>
        </router-link>
      </div>
    </div>

    <div class="row q-col-gutter-md">
      <!-- Черга CITO -->
      <div class="col-12 col-lg-6">
        <div class="medlink-card">
          <div class="medlink-card__title">
            <span><q-icon name="bolt" color="deep-orange-6" class="q-mr-xs" />Черга CITO</span>
            <q-btn flat dense size="sm" color="primary" label="Усі термінові" :to="{ name: 'lab-orders', query: { cito: 'true' } }" />
          </div>
          <q-table
            :data="citoOrders"
            :columns="citoColumns"
            row-key="id"
            dense
            flat
            hide-pagination
            :pagination="{ rowsPerPage: 8 }"
            :loading="loading"
            no-data-label="Термінових направлень у черзі немає"
            @row-click="(e, row) => $router.push({ name: 'lab-order-card', params: { id: row.id } })"
            class="cursor-pointer"
          >
            <template v-slot:body-cell-status="props">
              <q-td :props="props"><status-chip :value="props.row.status" type="order" /></q-td>
            </template>
            <template v-slot:body-cell-orderDatetime="props">
              <q-td :props="props">{{ props.row.orderDatetime | datetime }}</q-td>
            </template>
          </q-table>
        </div>
      </div>

      <!-- Критичні значення без повідомлення -->
      <div class="col-12 col-lg-6">
        <div class="medlink-card">
          <div class="medlink-card__title">
            <span><q-icon name="priority_high" color="negative" class="q-mr-xs" />Критичні значення без повідомлення</span>
            <q-btn flat dense size="sm" color="negative" label="Журнал дзвінків" :to="{ name: 'lab-validation', query: { tab: 'panic' } }" />
          </div>
          <q-table
            :data="panicPending"
            :columns="panicColumns"
            row-key="resultId"
            dense
            flat
            hide-pagination
            :pagination="{ rowsPerPage: 8 }"
            :loading="loading"
            no-data-label="Усі критичні результати опрацьовано"
          >
            <template v-slot:body-cell-value="props">
              <q-td :props="props"><span class="val-critical">{{ props.row.value | num }} {{ props.row.unit }}</span></q-td>
            </template>
            <template v-slot:body-cell-flag="props">
              <q-td :props="props"><flag-marker :flag="props.row.flag" show-label /></q-td>
            </template>
            <template v-slot:body-cell-actions="props">
              <q-td :props="props">
                <q-btn size="sm" dense color="negative" icon="call" label="Зафіксувати дзвінок" :to="{ name: 'lab-validation', query: { tab: 'panic', resultId: props.row.resultId } }" />
              </q-td>
            </template>
          </q-table>
        </div>
      </div>

      <!-- Lockout ВКЯ -->
      <div class="col-12 col-md-6 col-lg-4">
        <div class="medlink-card">
          <div class="medlink-card__title">
            <span><q-icon name="lock" color="purple-6" class="q-mr-xs" />Блокування за результатами ВКЯ</span>
            <q-btn flat dense size="sm" color="primary" label="ВКЯ" :to="{ name: 'lab-qc', query: { tab: 'lockouts' } }" />
          </div>
          <q-list v-if="lockouts.length" dense separator>
            <q-item v-for="l in lockouts" :key="l.id">
              <q-item-section avatar><q-icon name="lock" color="negative" /></q-item-section>
              <q-item-section>
                <q-item-label>{{ analyzerName(l.analyzerId) }} <span v-if="l.testName || l.testCode" class="text-grey-7">· {{ l.testName || l.testCode }}</span></q-item-label>
                <q-item-label caption>{{ lockoutReason(l.reason) }} · з {{ l.startedAt | datetime }}</q-item-label>
              </q-item-section>
            </q-item>
          </q-list>
          <empty-state v-else title="Активних блокувань немає" icon="lock_open" hint="Усі прилади в нормі" />
        </div>
      </div>

      <!-- Коннектори / аналізатори -->
      <div class="col-12 col-md-6 col-lg-4">
        <div class="medlink-card">
          <div class="medlink-card__title">
            <span><q-icon name="wifi" color="teal-6" class="q-mr-xs" />Прилади онлайн: {{ devicesOnline }} з {{ devicesTotal }}</span>
            <q-btn flat dense size="sm" color="primary" label="Коннектори" :to="{ name: 'lab-analyzers' }" />
          </div>
          <q-list v-if="connectors.length || analyzers.length" dense separator>
            <q-item v-for="c in connectors" :key="'c' + c.id">
              <q-item-section avatar><q-icon :name="c.status === 'ACTIVE' ? 'wifi' : 'wifi_off'" :color="c.status === 'ACTIVE' ? 'positive' : 'negative'" /></q-item-section>
              <q-item-section>
                <q-item-label>{{ c.name }} <span class="text-grey-6">{{ c.hostName }}</span></q-item-label>
                <q-item-label caption>Останній сигнал: {{ c.lastHeartbeatAt | datetime }} · у черзі на відправку: {{ c.bufferedCount || 0 }}</q-item-label>
              </q-item-section>
              <q-item-section side><status-chip :value="c.status" type="connector" dense /></q-item-section>
            </q-item>
            <q-item v-for="a in shownAnalyzers" :key="'a' + a.id">
              <q-item-section avatar><q-icon name="memory" :color="a.isOnline ? 'positive' : 'grey-5'" /></q-item-section>
              <q-item-section>
                <q-item-label>{{ a.name }}</q-item-label>
                <q-item-label caption>Останнє повідомлення: {{ a.lastMessageAt | datetime }} <span v-if="a.lastError" class="text-negative">· {{ a.lastError }}</span></q-item-label>
              </q-item-section>
              <q-item-section side>
                <q-badge :color="a.isOnline ? 'positive' : 'grey-6'" :label="a.isOnline ? 'онлайн' : 'офлайн'" />
              </q-item-section>
            </q-item>
            <q-item v-if="sortedAnalyzers.length > 6" clickable @click="showAllDevices = !showAllDevices">
              <q-item-section class="text-primary text-caption">{{ showAllDevices ? 'Згорнути' : `Показати всі прилади (${sortedAnalyzers.length})` }}</q-item-section>
            </q-item>
          </q-list>
          <empty-state v-else title="Коннектори не налаштовані" icon="router" hint="Створіть інсталяцію коннектора та додайте аналізатори" />
        </div>
      </div>

      <!-- TAT сьогодні -->
      <div class="col-12 col-lg-4">
        <div class="medlink-card">
          <div class="medlink-card__title">
            <span><q-icon name="timer" color="indigo-5" class="q-mr-xs" />Час виконання сьогодні</span>
            <q-btn flat dense size="sm" color="primary" label="Аналітика" :to="{ name: 'lab-analytics-tat' }" />
          </div>
          <div v-if="hasTat" class="q-pa-md">
            <div class="row q-col-gutter-sm q-mb-sm">
              <div class="col-6">
                <div class="kpi-tile kpi-tile--accent">
                  <div class="kpi-tile__label">Типовий час</div>
                  <div class="kpi-tile__value">{{ tat.medianMin | minutes }}</div>
                </div>
              </div>
              <div class="col-6">
                <div class="kpi-tile" :class="tat.slaViolations ? 'kpi-tile--negative' : 'kpi-tile--positive'">
                  <div class="kpi-tile__label">90% направлень за</div>
                  <div class="kpi-tile__value">{{ tat.p90Min | minutes }}</div>
                  <div class="kpi-tile__hint">порушень нормативу: {{ tat.slaViolations || 0 }}</div>
                </div>
              </div>
            </div>
            <div v-for="s in (tat.stages || [])" :key="s.name" class="row items-center q-mb-xs" style="font-size: 12.5px">
              <div class="col-4 text-grey-8">{{ stageLabel(s.name) }}</div>
              <div class="col-8">
                <div class="tat-bar bg-primary" :style="{ width: stageWidth(s) }">{{ s.medianMin | minutes }}</div>
              </div>
            </div>
          </div>
          <empty-state v-else title="Сьогодні ще немає завершених направлень" icon="timer_off" />
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { mapState } from 'vuex';
import apiMixin from '../../mixins/apiMixin';
import { todayIso } from '../../utils/format';

/** Українська множина: plural(5, ['тест', 'тести', 'тестів']) */
function plural (n, forms) {
  const a = Math.abs(n) % 100; const b = a % 10;
  if (a > 10 && a < 20) return forms[2];
  if (b > 1 && b < 5) return forms[1];
  return b === 1 ? forms[0] : forms[2];
}

const STAGE_LABELS = {
  order: 'Замовлення',
  collected: 'Забір',
  received: 'Прийом',
  resulted: 'Результат',
  verified: 'Верифікація',
  released: 'Видача',
  'order→collected': 'Замовлення → забір',
  'collected→received': 'Забір → прийом',
  'received→resulted': 'Прийом → результат',
  'resulted→verified': 'Результат → верифікація',
  'verified→released': 'Верифікація → видача'
};

export default {
  name: 'LabDashboard',
  mixins: [apiMixin],
  data () {
    return {
      citoOrders: [],
      panicPending: [],
      tat: null, showAllDevices: false,
      citoColumns: [
        { name: 'orderNumber', label: '№ направлення', field: 'orderNumber', align: 'left' },
        { name: 'patient', label: 'Пацієнт', field: r => (r.patient && (r.patient.fullName || `${r.patient.lastName || ''} ${r.patient.firstName || ''}`)) || r.patientName || '—', align: 'left' },
        { name: 'orderDatetime', label: 'Створено', field: 'orderDatetime', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'center' }
      ],
      panicColumns: [
        { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'testCode', label: 'Тест', field: r => r.testName || r.testCode, align: 'left' },
        { name: 'value', label: 'Значення', field: 'value', align: 'right' },
        { name: 'flag', label: 'Позначка', field: 'flag', align: 'center' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    ...mapState('laboratory', ['summary', 'connectors', 'analyzers', 'activeLockouts']),
    lockouts () { return this.activeLockouts; },
    hasTat () {
      const t = this.tat;
      return !!t && (t.medianMin != null || t.p90Min != null || (t.stages || []).some(s => s.medianMin != null));
    },
    devicesTotal () { return this.analyzers.length; },
    devicesOnline () { return this.analyzers.filter(a => a.isOnline).length; },
    // спочатку прилади з проблемами
    sortedAnalyzers () { return [...this.analyzers].sort((a, b) => Number(!!a.isOnline) - Number(!!b.isOnline)); },
    shownAnalyzers () { return this.showAllDevices ? this.sortedAnalyzers : this.sortedAnalyzers.slice(0, 6); },
    tiles () {
      const s = this.summary || {};
      return [
        { key: 'pending', label: 'У роботі', value: s.pending || 0, hint: plural(s.pending || 0, ['показник чекає результату', 'показники чекають результату', 'показників чекають результату']), cls: 'kpi-tile--accent', to: { name: 'lab-workstation' } },
        { key: 'review', label: 'Чекають лікаря', value: s.needsReview || 0, hint: 'на верифікацію', cls: 'kpi-tile--warning', to: { name: 'lab-validation' } },
        { key: 'panic', label: 'Критичні без дзвінка', value: s.panic || 0, hint: plural(s.panic || 0, ['критичний результат', 'критичні результати', 'критичних результатів']), cls: 'kpi-tile--negative', to: { name: 'lab-validation', query: { tab: 'panic' } } },
        { key: 'cito', label: 'Термінові (CITO)', value: s.cito || 0, hint: plural(s.cito || 0, ['термінове направлення', 'термінові направлення', 'термінових направлень']), cls: 'kpi-tile--negative', to: { name: 'lab-orders', query: { cito: 'true' } } },
        { key: 'auto', label: 'Автоверифіковано', value: s.autoVerifiedToday || 0, hint: 'сьогодні', cls: 'kpi-tile--positive', to: { name: 'lab-workstation', query: { status: 'AUTO_VERIFIED' } } },
        { key: 'lock', label: 'Блокування ВКЯ', value: this.lockouts.length, hint: plural(this.lockouts.length, ['активне блокування', 'активні блокування', 'активних блокувань']), cls: 'kpi-tile--warning', to: { name: 'lab-qc', query: { tab: 'lockouts' } } },
        { key: 'dist', label: 'Направлення МІС до розподілу', value: s.distributionPending || 0, hint: plural(s.distributionPending || 0, ['направлення чекає', 'направлення чекають', 'направлень чекають']), cls: 'kpi-tile--warning', to: { name: 'lab-incoming-referrals' } },
        { key: 'noeq', label: 'Показники без приладу', value: s.noEquipment || 0, hint: plural(s.noEquipment || 0, ['показник не закріплено', 'показники не закріплено', 'показників не закріплено']), cls: 'kpi-tile--negative', to: { name: 'lab-work' } }
      ];
    }
  },
  created () {
    this.load();
  },
  methods: {
    async load () {
      this.loading = true;
      this.apiError = null;
      await this.$store.dispatch('laboratory/refreshStatus');
      const results = await Promise.allSettled([
        this.$api.getOrders({ cito: true, pageSize: 8, status: 'NEW,COLLECTED,IN_TRANSIT,RECEIVED,IN_PROGRESS' }),
        this.$api.panicPending(),
        this.$api.tat({ from: todayIso(), to: todayIso() })
      ]);
      if (results[0].status === 'fulfilled') this.citoOrders = this.asList(results[0].value);
      if (results[1].status === 'fulfilled') this.panicPending = this.asList(results[1].value);
      if (results[2].status === 'fulfilled') this.tat = results[2].value;
      const failed = results.find(r => r.status === 'rejected');
      if (failed && !this.apiOffline) this.apiError = failed.reason.userMessage || 'Частину даних не вдалося завантажити';
      this.loading = false;
    },
    analyzerName (id) {
      const a = this.analyzers.find(x => x.id === id);
      return a ? a.name : (id || 'Аналізатор');
    },
    // технічну причину блокування показуємо людською мовою
    lockoutReason (r) {
      let t = String(r || '').replace(/^Lockout ВКЯ:\s*/i, 'Блокування за ВКЯ: ');
      t = t.replace(/порушено\s+([\w_]+);\s*z=([+-]?[\d.]+)/i, (m, rule, z) => `Порушено правило ${rule}: відхилення ${z.replace('.', ',')} SD`);
      return t || 'Причина не вказана';
    },
    stageLabel (name) { return STAGE_LABELS[name] || name; },
    stageWidth (s) {
      const max = Math.max(...(this.tat.stages || []).map(x => Number(x.medianMin) || 0), 1);
      return `${Math.max(8, Math.round((Number(s.medianMin) || 0) / max * 100))}%`;
    }
  }
};
</script>
