<template>
  <div class="portal-trend" data-testid="portalTrend">
    <div class="row items-center q-mb-sm">
      <q-btn flat dense icon="arrow_back" label="Назад" no-caps @click="$router.back()" />
      <q-space />
      <q-select v-model="code" dense outlined label="Показник" :options="testOptions" emit-value map-options style="min-width: 220px" @input="changeTest" />
    </div>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />
    <div class="medlink-card q-pa-md">
      <div class="text-h6 text-weight-bold">{{ testName }} <span class="text-grey-7 text-subtitle2">({{ code }})</span></div>
      <div class="text-caption text-grey-7 q-mb-sm">Динаміка показника в часі; зелений коридор — референтний інтервал для вас.</div>
      <trend-chart :points="points" :unit="unit" />
      <q-markup-table dense flat class="q-mt-md">
        <thead><tr><th class="text-left">Дата</th><th class="text-right">Значення</th><th class="text-left">Норма</th><th class="text-center">Оцінка</th><th class="text-right">Зміна</th></tr></thead>
        <tbody>
          <tr v-for="(p, i) in sorted" :key="i">
            <td>{{ p.at | datetime }}</td>
            <td class="text-right text-weight-bold">{{ p.value | num }} {{ unit }}</td>
            <td>{{ p.normLow | num }} – {{ p.normHigh | num }}</td>
            <td class="text-center"><flag-marker :flag="p.flag || 'NONE'" show-label /></td>
            <td class="text-right">{{ delta(i) }}</td>
          </tr>
          <tr v-if="!sorted.length"><td colspan="5" class="text-center text-grey-6 q-pa-md">Історії результатів ще немає</td></tr>
        </tbody>
      </q-markup-table>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import TrendChart from '../../components/charts/TrendChart.vue';
import { formatPercent } from '../../utils/format';

export default {
  name: 'PortalTrend',
  mixins: [apiMixin],
  components: { TrendChart },
  props: { patientId: { type: String, required: true }, testCode: { type: String, required: true } },
  data () { return { code: this.testCode, points: [] }; },
  computed: {
    tests () { return this.$store.getters['dictionaries/items']('tests'); },
    testOptions () { return this.tests.map(t => ({ value: t.code, label: `${t.code} — ${t.name}` })); },
    testDef () { return this.tests.find(t => t.code === this.code); },
    testName () { return (this.testDef && this.testDef.name) || (this.points[0] && this.points[0].testName) || this.code; },
    unit () { return (this.points.slice(-1)[0] && this.points.slice(-1)[0].unit) || (this.testDef && this.testDef.unit) || ''; },
    sorted () { return [...this.points].sort((a, b) => new Date(b.at) - new Date(a.at)); }
  },
  created () { this.$store.dispatch('dictionaries/load', 'tests'); this.load(); },
  methods: {
    delta (i) { const cur = this.sorted[i]; const prev = this.sorted[i + 1]; if (!prev || !Number(prev.value)) return '—'; return formatPercent((Number(cur.value) - Number(prev.value)) / Number(prev.value) * 100); },
    async load () {
      const res = await this.callApi(() => this.$api.portalTrend(this.patientId, this.code));
      this.points = res === undefined ? [] : this.asList(res).sort((a, b) => new Date(a.at) - new Date(b.at));
    },
    changeTest () { this.$router.replace({ name: 'portal-trend', params: { patientId: this.patientId, testCode: this.code } }); }
  }
};
</script>
