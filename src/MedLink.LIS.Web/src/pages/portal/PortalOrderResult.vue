<template>
  <div class="portal-result" data-testid="portalResult">
    <div class="row items-center q-mb-sm">
      <q-btn flat dense icon="arrow_back" label="До списку" no-caps :to="{ name: 'portal-orders', params: { patientId } }" />
      <q-space />
      <q-btn v-if="order && order.status === 'RELEASED'" outline dense color="primary" icon="picture_as_pdf" label="Остаточний бланк (PDF)" type="a" :href="$api.portalPdfUrl(patientId, id)" target="_blank" data-testid="portalPdf" />
      <q-btn v-else-if="order && releasedCount > 0" outline dense color="orange-8" icon="hourglass_top" label="Попередній бланк (PDF)" type="a" :href="$api.portalPdfUrl(patientId, id) + '?variant=preliminary'" target="_blank" data-testid="portalPdfPreliminary" />
    </div>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div v-if="order" class="medlink-card q-pa-md q-mb-md">
      <div class="row items-center justify-between">
        <div>
          <div class="text-h6 text-weight-bold">Замовлення № {{ order.orderNumber }}</div>
          <div class="text-caption text-grey-7">{{ order.orderDatetime | datetime }} · {{ labName }}</div>
        </div>
        <status-chip :value="order.status" type="order" icon />
      </div>
      <div v-if="totalCount" class="row items-center q-gutter-sm q-mt-sm" data-testid="portalResultProgress">
        <q-linear-progress :value="releasedCount / totalCount" :color="releasedCount >= totalCount ? 'positive' : 'primary'" size="10px" rounded class="col" />
        <span class="text-caption text-weight-bold">Готово {{ releasedCount }} з {{ totalCount }}</span>
      </div>
      <q-banner v-if="order.status !== 'RELEASED'" dense rounded class="bg-orange-1 text-orange-10 q-mt-sm"><q-icon name="hourglass_top" /> Результати видаються поступово: показані лише видані показники{{ pendingCount ? `, ще ${pendingCount} очікується` : '' }}. Остаточний бланк буде доступний після повної видачі.</q-banner>
      <div class="row q-gutter-md q-mt-sm text-caption text-grey-8">
        <span><b>Пацієнт:</b> {{ patientName }}</span>
        <span v-if="order.doctorName"><b>Лікар:</b> {{ order.doctorName }}</span>
        <span v-if="order.releasedAt"><b>Видано:</b> {{ order.releasedAt | datetime }}</span>
      </div>
    </div>

    <div v-if="order" class="medlink-card">
      <q-markup-table flat dense class="portal-results-table">
        <thead><tr><th class="text-left">Показник</th><th class="text-right">Результат</th><th class="text-left">Од.</th><th class="text-left">Референтний інтервал</th><th class="text-center">Оцінка</th><th class="text-center">Динаміка</th></tr></thead>
        <tbody>
          <tr v-for="t in tests" :key="t.id" :class="{ 'bg-red-1': isCritical(res(t).flag) }" :data-testid="`portal-test-${t.testCode}`">
            <td><div class="text-weight-bold">{{ t.testName }}</div><div class="text-caption text-grey-6">{{ t.testCode }}<span v-if="res(t).operatorComment || res(t).verificationComment"> · {{ res(t).verificationComment || res(t).operatorComment }}</span></div></td>
            <td class="text-right"><span class="text-weight-bold" :class="flagCss(res(t).flag)">{{ valueOf(t) }}</span>
              <q-icon v-if="deltaIcon(t)" :name="deltaIcon(t)" size="16px" :color="res(t).deltaAlert ? 'orange-9' : 'grey-6'" class="q-ml-xs"><q-tooltip>Відносно попереднього: {{ res(t).deltaPercent | pct }} (було {{ res(t).previousValue | num }})</q-tooltip></q-icon>
            </td>
            <td>{{ res(t).unit }}</td>
            <td>{{ referenceDisplay(res(t)) || '—' }}</td>
            <td class="text-center"><flag-marker :flag="visible(t) ? (res(t).flag || 'NONE') : 'NONE'" show-label /></td>
            <td class="text-center"><q-btn flat dense round size="sm" icon="show_chart" color="primary" :to="{ name: 'portal-trend', params: { patientId, testCode: t.testCode } }"><q-tooltip>Графік динаміки</q-tooltip></q-btn></td>
          </tr>
          <tr v-for="n in pendingPlaceholders" :key="'pending' + n" class="text-grey-5" data-testid="portal-pending">
            <td><div class="text-weight-bold">Показник очікується</div><div class="text-caption">результат ще не видано</div></td>
            <td class="text-right">очікується</td><td /><td /><td class="text-center"><flag-marker flag="NONE" show-label /></td><td />
          </tr>
          <tr v-if="!tests.length && !pendingPlaceholders"><td colspan="6" class="text-center text-grey-6 q-pa-md">Показників немає</td></tr>
        </tbody>
      </q-markup-table>
      <div class="q-pa-sm text-caption text-grey-6"><flag-marker flag="NORMAL" /> норма · <flag-marker flag="HIGH" /> поза нормою · <flag-marker flag="CRIT_HIGH" /> критично · <flag-marker flag="NONE" /> очікується. Референтні інтервали враховують стать, вік та стан пацієнта.</div>
    </div>
    <div v-else-if="loading" class="text-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import { isCritical, flagMeta } from '../../utils/statuses';
import { referenceDisplay, formatNumber, patientDisplay } from '../../utils/format';

export default {
  name: 'PortalOrderResult',
  mixins: [apiMixin],
  props: { patientId: { type: String, required: true }, id: { type: String, required: true } },
  data () { return { order: null }; },
  computed: {
    tests () { return (this.order && (this.order.releasedTestsList || this.order.tests)) || []; },
    totalCount () { const o = this.order || {}; return Number(o.totalTests !== undefined ? o.totalTests : this.tests.length) || 0; },
    releasedCount () { const o = this.order || {}; if (o.releasedTests !== undefined) return Number(o.releasedTests) || 0; return this.tests.filter(t => this.visible(t)).length; },
    pendingCount () { const o = this.order || {}; if (o.pendingTests !== undefined) return Number(o.pendingTests) || 0; return Math.max(0, this.totalCount - this.tests.length); },
    pendingPlaceholders () { return Math.min(this.pendingCount, 20); },
    patientName () { return this.order ? (patientDisplay(this.order.patient) !== '—' ? patientDisplay(this.order.patient) : this.order.patientName) : ''; },
    labName () { return this.$store.getters['context/labName']; }
  },
  created () { this.load(); },
  methods: {
    isCritical, referenceDisplay,
    res (t) { return t.result || t.lastResult || {}; },
    visible (t) { return ['VERIFIED', 'AUTO_VERIFIED'].includes(t.status) || this.order.status === 'RELEASED'; },
    flagCss (f) { return flagMeta(f || 'NONE').css; },
    valueOf (t) {
      if (!this.visible(t)) return 'очікується';
      const r = this.res(t);
      if (r.numericValue !== null && r.numericValue !== undefined) return formatNumber(r.numericValue);
      if (r.stringValue) return r.stringValue;
      if (r.value !== undefined && r.value !== null) return isNaN(Number(r.value)) ? r.value : formatNumber(r.value);
      return 'очікується';
    },
    deltaIcon (t) { const r = this.res(t); if (r.deltaPercent === null || r.deltaPercent === undefined || !this.visible(t)) return null; const d = Number(r.deltaPercent); if (Math.abs(d) < 1) return 'trending_flat'; return d > 0 ? 'trending_up' : 'trending_down'; },
    async load () {
      const res = await this.callApi(() => this.$api.portalOrder(this.patientId, this.id));
      if (res) this.order = res;
    }
  }
};
</script>

<style scoped>
.portal-results-table td { font-size: 14px; }
@media (max-width: 600px) { .portal-results-table th:nth-child(4), .portal-results-table td:nth-child(4) { display: none; } }
</style>
