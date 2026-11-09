<template>
  <div class="tat-page" data-testid="tatPage">
    <page-header title="Аналітика термінів виконання" icon="fas fa-chart-pie" subtitle="Час від замовлення до видачі результату за етапами: медіана, 90-й процентиль, порушення норм часу" :breadcrumbs="[{ label: 'Аналітика термінів виконання' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn outline dense color="positive" icon="table_view" label="Завантажити XLSX" :disable="!hasData" type="a" :href="$api.exportXlsxUrl('tat', { from: filters.from, to: filters.to, profileId: filters.profileId, cito: filters.cito || null })" target="_blank" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-md row q-col-gutter-sm items-center">
      <div class="col-6 col-md-2"><q-input v-model="filters.from" dense outlined type="date" stack-label label="З" @change="load" /></div>
      <div class="col-6 col-md-2"><q-input v-model="filters.to" dense outlined type="date" stack-label label="По" @change="load" /></div>
      <div class="col-12 col-md-3"><q-select v-model="filters.profileId" dense outlined clearable label="Послуга" :options="profileOptions" emit-value map-options @input="load" /></div>
      <div class="col-6 col-md-2"><q-toggle v-model="filters.cito" label="Лише CITO" color="deep-orange-6" keep-color @input="load" /></div>
      <div class="col-6 col-md-3"><q-select v-model="groupBy" dense outlined label="Групувати обсяг" :options="[{ value: 'day', label: 'за днями' }, { value: 'profile', label: 'за послугами' }, { value: 'analyzer', label: 'за аналізаторами' }, { value: 'employee', label: 'за співробітниками' }]" emit-value map-options @input="loadVolume" /></div>
    </div>

    <div v-if="tat" class="row q-col-gutter-sm q-mb-md">
      <div class="col-6 col-md-3"><div class="kpi-tile kpi-tile--accent" data-testid="tatMedian"><div class="kpi-tile__label">Медіана терміну виконання</div><div class="kpi-tile__value">{{ tat.medianMin | minutes }}</div><div class="kpi-tile__hint">від замовлення до видачі</div></div></div>
      <div class="col-6 col-md-3"><div class="kpi-tile" :class="Number(tat.slaViolations) ? 'kpi-tile--warning' : 'kpi-tile--positive'"><div class="kpi-tile__label">90-й процентиль</div><div class="kpi-tile__value">{{ tat.p90Min | minutes }}</div><div class="kpi-tile__hint">90 % замовлень виконано швидше</div></div></div>
      <div class="col-6 col-md-3"><div class="kpi-tile" :class="Number(tat.slaViolations) ? 'kpi-tile--negative' : 'kpi-tile--positive'"><div class="kpi-tile__label">Порушень норм часу</div><div class="kpi-tile__value">{{ tat.slaViolations || 0 }}</div><div class="kpi-tile__hint">понад встановлений строк послуги</div></div></div>
      <div class="col-6 col-md-3"><div class="kpi-tile"><div class="kpi-tile__label">Замовлень</div><div class="kpi-tile__value">{{ tat.orders || 0 }}</div><div class="kpi-tile__hint">за період, виконано повністю: {{ tat.completed || 0 }}</div></div></div>
    </div>

    <div class="row q-col-gutter-md">
      <div class="col-12 col-lg-6">
        <div class="medlink-card q-pa-md">
          <div class="text-subtitle2 text-weight-bold q-mb-sm">Етапи виконання (медіана)</div>
          <div v-if="stages.length">
            <div v-for="(s, i) in stages" :key="s.name" class="row items-center q-mb-xs">
              <div class="col-4 text-caption text-grey-8">{{ stageLabel(s.name) }}</div>
              <div class="col-8 row no-wrap items-center">
                <div :style="{ width: offsetPct(i) + '%' }" />
                <div class="tat-bar" :style="{ width: widthPct(s) + '%', minWidth: '64px', whiteSpace: 'nowrap', background: stageColor(i) }">{{ s.medianMin | minutes }}</div>
              </div>
            </div>
            <div class="row items-center q-mt-sm"><div class="col-4 text-caption text-weight-bold">Разом</div><div class="col-8 text-caption">{{ totalStages | minutes }} <span class="text-grey-6">(сума медіан етапів)</span></div></div>
          </div>
          <empty-state v-else title="Даних про етапи немає" icon="timer_off" hint="Потрібні замовлення з повним циклом за період" />
        </div>
      </div>

      <div class="col-12 col-lg-6">
        <div class="medlink-card q-pa-md">
          <div class="text-subtitle2 text-weight-bold q-mb-sm">Обсяг досліджень ({{ groupLabel }})</div>
          <svg v-if="totalVolume > 0" :viewBox="`0 0 ${vw} ${vh}`" style="width: 100%; height: 220px" data-testid="volumeChart">
            <g v-for="(v, i) in volumeRows" :key="i">
              <rect :x="barX(i)" :y="barY(v)" :width="barW" :height="vh - 24 - barY(v)" fill="#4274A7" rx="2"><title>{{ v.label }}: {{ v.count }}</title></rect>
              <text :x="barX(i) + barW / 2" :y="barY(v) - 3" text-anchor="middle" font-size="11" fill="#374151">{{ v.count }}</text>
              <text v-if="volumeRows.length <= 16 || i % Math.ceil(volumeRows.length / 16) === 0" :x="barX(i) + barW / 2" :y="vh - 10" text-anchor="middle" font-size="11" fill="#6b7280">{{ shortLabel(v.label) }}</text>
            </g>
          </svg>
          <empty-state v-else title="За період досліджень немає" icon="bar_chart" />
        </div>
      </div>

      <div class="col-12">
        <div class="medlink-card">
          <div class="medlink-card__title"><span>За послугами</span><span class="text-caption text-grey-6">медіана, 90-й процентиль, порушення норм часу</span></div>
          <q-table :data="byProfile" :columns="profileColumns" dense flat row-key="profileId" :pagination="{ rowsPerPage: 25, sortBy: 'p90Min', descending: true }" no-data-label="Даних за профілями немає">
            <template v-slot:body-cell-medianMin="props"><q-td :props="props" class="text-right">{{ props.row.medianMin | minutes }}</q-td></template>
            <template v-slot:body-cell-p90Min="props"><q-td :props="props" class="text-right">{{ props.row.p90Min | minutes }}</q-td></template>
            <template v-slot:body-cell-sla="props"><q-td :props="props" class="text-right"><q-badge :color="Number(props.row.slaViolations) ? 'negative' : 'positive'" :label="String(props.row.slaViolations || 0)" /></q-td></template>
            <template v-slot:body-cell-bar="props"><q-td :props="props"><q-linear-progress :value="Math.min(1, Number(props.row.medianMin) / (maxProfileMin || 1))" color="primary" size="8px" rounded /></q-td></template>
          </q-table>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import { daysAgoIso, todayIso } from '../../utils/format';

const STAGE_LABELS = {
  'order→collected': 'Замовлення → забір', 'collected→received': 'Забір → прийом', 'received→resulted': 'Прийом → результат',
  'resulted→verified': 'Результат → верифікація', 'verified→released': 'Верифікація → видача',
  order: 'Замовлення → забір', collected: 'Забір → прийом', received: 'Прийом → результат', resulted: 'Результат → верифікація', verified: 'Верифікація → видача', released: 'Видача'
};
const COLORS = ['#0178BC', '#318F94', '#5EC58C', '#4274A7', '#7c3aed', '#f2c037'];

export default {
  name: 'LabAnalyticsTat',
  mixins: [apiMixin],
  data () {
    return {
      tat: null, volume: [], groupBy: 'day',
      filters: { from: daysAgoIso(30), to: todayIso(), profileId: null, cito: false },
      vw: 600, vh: 220,
      profileColumns: [
        { name: 'name', label: 'Послуга', field: r => r.profileName || r.name || r.profileCode, align: 'left', sortable: true },
        { name: 'count', label: 'Замовлень', field: r => r.n || 0, align: 'right', sortable: true },
        { name: 'medianMin', label: 'Медіана', field: 'medianMin', align: 'right', sortable: true },
        { name: 'p90Min', label: '90-й процентиль', field: 'p90Min', align: 'right', sortable: true },
        { name: 'sla', label: 'Порушень норм часу', field: 'slaViolations', align: 'right', sortable: true },
        { name: 'bar', label: '', align: 'left', style: 'width: 25%' }
      ]
    };
  },
  computed: {
    profileOptions () { return this.$store.getters['dictionaries/options']('profiles'); },
    stages () { return (this.tat && this.tat.stages) || []; },
    totalStages () { return this.stages.reduce((s, x) => s + (Number(x.medianMin) || 0), 0); },
    byProfile () { return ((this.tat && this.tat.byProfile) || []).map((r, i) => ({ profileId: r.profileId || i, ...r })); },
    maxProfileMin () { return Math.max(...this.byProfile.map(r => Number(r.medianMin) || 0), 0); },
    volumeRows () {
      const list = Array.isArray(this.volume) ? this.volume : (this.volume && (this.volume.items || this.volume.rows)) || [];
      // Сервер повертає { rows: [{ key, tests, orders, … }] }; рахуємо кількість досліджень
      const rows = list.map(v => ({ label: v.key || v.label || v.name || '—', count: Number(v.tests !== undefined ? v.tests : (v.count || v.total || v.value)) || 0 }));
      // Дати — за зростанням, решта — за кількістю
      return this.groupBy === 'day' ? rows.sort((a, b) => String(a.label).localeCompare(String(b.label))) : rows;
    },
    totalVolume () { return this.volumeRows.reduce((s, v) => s + v.count, 0); },
    maxVolume () { return Math.max(...this.volumeRows.map(v => v.count), 1); },
    barW () { return Math.max(4, (this.vw - 20) / Math.max(this.volumeRows.length, 1) - 4); },
    groupLabel () { return { day: 'за днями', profile: 'за послугами', analyzer: 'за аналізаторами', employee: 'за співробітниками' }[this.groupBy]; },
    hasData () { return !!(this.tat && Number(this.tat.orders) > 0); }
  },
  created () { this.$store.dispatch('dictionaries/load', 'profiles'); this.load(); },
  methods: {
    stageLabel (n) { return STAGE_LABELS[n] || n; },
    stageColor (i) { return COLORS[i % COLORS.length]; },
    widthPct (s) { return this.totalStages ? Math.max(4, (Number(s.medianMin) || 0) / this.totalStages * 100) : 0; },
    offsetPct (i) { const before = this.stages.slice(0, i).reduce((s, x) => s + (Number(x.medianMin) || 0), 0); return this.totalStages ? before / this.totalStages * 100 : 0; },
    barX (i) { return 10 + i * ((this.vw - 20) / Math.max(this.volumeRows.length, 1)); },
    barY (v) { return 14 + (1 - v.count / this.maxVolume) * (this.vh - 38); },
    shortLabel (l) { const s = String(l); const m = s.match(/^\d{4}-(\d{2})-(\d{2})$/); if (m) return `${m[2]}.${m[1]}`; return s.length > 14 ? s.slice(0, 13) + '…' : s; },
    async load () {
      const res = await this.callApi(() => this.$api.tat({ from: this.filters.from, to: this.filters.to, profileId: this.filters.profileId, cito: this.filters.cito ? true : null }));
      this.tat = res || null;
      this.loadVolume();
    },
    async loadVolume () {
      try { this.volume = await this.$api.volume({ groupBy: this.groupBy, from: this.filters.from, to: this.filters.to }); } catch (e) { this.volume = []; }
    }
  }
};
</script>
