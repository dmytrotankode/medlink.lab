<!--
  ТЗ 22.14 п. 2, 7 (FR-PAT-024, 060, 061): «Журнал справ» ПГД — плитки-фільтри за етапами, пошук за № / ПІБ / штрихкодом,
  фільтр джерела, етап з міні-прогресом і строк; CITO і прострочені вгорі. Режим «Інтраопераційні (CITO)» — таймер від надходження.
-->
<template>
  <div data-testid="pathologyCasesPage">
    <page-header :title="cito ? 'Термінові інтраопераційні (CITO)' : cyto ? 'Журнал цитології' : 'Журнал справ ПГД'" :icon="cito ? 'fas fa-stopwatch' : cyto ? 'fas fa-microscope' : 'fas fa-folder-open'"
      :subtitle="cito ? `Таймер від надходження, ціль ${target} хв; результат повідомляється в операційну з підтвердженням отримувача` : ''"
      :breadcrumbs="[{ label: cyto ? 'Цитологія' : 'Патоморфологія' }, { label: cito ? 'CITO' : cyto ? 'Журнал цитології' : 'Журнал справ' }]">
      <q-input v-if="!cito" v-model="q" dense outlined clearable debounce="300" placeholder="№ справи, ПІБ, штрихкод" style="min-width: 240px" class="q-mr-sm" data-testid="pathSearch">
        <template v-slot:prepend><q-icon name="search" /></template>
      </q-input>
      <q-select v-if="!cito" v-model="src" dense outlined clearable emit-value map-options :options="sourceOptions" label="Джерело" style="min-width: 170px" class="q-mr-sm" />
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" :retryable="apiErrorRetryable" @retry="load" @dismiss="apiError = null" />

    <div v-if="!cito" class="row q-col-gutter-sm q-mb-xs">
      <div v-for="t in tiles" :key="t.value" class="col-6 col-sm-4 col-md">
        <div class="medlink-card q-pa-sm cursor-pointer tile" :class="{ 'tile-active': stage === t.value }" :data-testid="`tile-${t.value}`" @click="stage = stage === t.value ? 'all' : t.value">
          <div class="text-caption text-grey-7">{{ t.label }}</div>
          <div class="text-h6" :class="t.cls">{{ t.count }}</div>
        </div>
      </div>
    </div>
    <div v-if="!cito" class="text-caption text-grey-7 q-mb-md">Лічильники плиток — за весь період, пошук і джерело на них не впливають.</div>

    <div v-if="cyto && an" class="row q-col-gutter-sm q-mb-md" data-testid="cytoAnalytics">
      <div v-for="a in analyticsTiles" :key="a.label" class="col-6 col-sm-4 col-md">
        <div class="medlink-card q-pa-sm"><div class="text-caption text-grey-7">{{ a.label }}</div><div class="text-subtitle1 text-weight-bold">{{ a.value }}</div></div>
      </div>
    </div>

    <div class="medlink-card">
      <q-table :data="items" :columns="columns" row-key="id" flat dense :pagination="{ rowsPerPage: 50 }" :loading="loading" no-data-label="Справ немає"
        @row-click="(e, r) => openCase(r)">
        <template v-slot:no-data>
          <div class="full-width row flex-center q-pa-md text-grey-7 q-gutter-sm" data-testid="noCases">
            <q-icon name="search_off" size="sm" />
            <span>{{ filtered ? 'За вибраними умовами справ немає' : 'Справ немає' }}</span>
            <q-btn v-if="filtered" flat dense color="primary" no-caps label="Скинути фільтри" data-testid="resetFilters" @click="resetFilters" />
          </div>
        </template>
        <template v-slot:body-cell-caseNo="p">
          <q-td :props="p">
            <b :class="p.row.overdue ? 'text-negative' : 'text-primary'">{{ p.row.caseNo }}</b>
            <q-badge v-if="p.row.cito" color="negative" label="CITO" class="q-ml-xs" />
            <q-badge v-if="p.row.ihcInWork" color="purple-5" :label="`ІГХ у роботі: ${p.row.ihcInWork}`" class="q-ml-xs" />
          </q-td>
        </template>
        <template v-slot:body-cell-source="p">
          <q-td :props="p">{{ p.row.sourceLabel }}<div v-if="deptShort(p.row)" class="text-caption text-grey-7 ellipsis" style="max-width: 260px">{{ deptShort(p.row) }}</div></q-td>
        </template>
        <template v-slot:body-cell-stage="p">
          <q-td :props="p">
            <div class="row items-center no-wrap">
              <div class="mini-progress q-mr-sm">
                <span v-for="i in (cyto ? 4 : 8)" :key="i" :class="stepClass(i - 1, p.row)"><q-tooltip>{{ segmentLabel(i - 1) }}</q-tooltip></span>
              </div>
              <span class="text-caption">{{ p.row.stageLabel }}</span>
            </div>
          </q-td>
        </template>
        <template v-slot:body-cell-sla="p">
          <q-td :props="p">
            <template v-if="p.row.stage === 'SIGNED'">
              <span class="text-positive">підписано {{ p.row.signedAt | datetime }}</span>
            </template>
            <template v-else-if="p.row.cito && p.row.citoMinutes !== null">
              <q-badge :color="p.row.citoMinutes > target ? 'negative' : 'orange-7'" :label="`${p.row.citoMinutes} хв від надходження`" data-testid="citoTimer" />
              <div v-if="p.row.citoNotifiedAt" class="text-caption text-positive" style="white-space: normal; min-width: 150px">повідомлено в операційну {{ p.row.citoNotifiedAt | datetime }}</div>
            </template>
            <span v-else :class="p.row.overdue ? 'text-negative text-weight-bold' : ''">{{ p.row.slaDueAt | datetime }}</span>
            <q-icon v-if="p.row.overdue && p.row.stage !== 'SIGNED'" name="timer_off" color="negative" class="q-ml-xs"><q-tooltip>{{ p.row.cito ? (p.row.citoNotifiedAt ? `Повідомлено пізніше за ціль (${target} хв)` : `Ціль ${target} хв минула, результат ще не повідомлено`) : 'Строк виконання минув' }}</q-tooltip></q-icon>
            <div class="text-caption text-grey-7">прийнято {{ p.row.receivedAt | datetime }}</div>
          </q-td>
        </template>
      </q-table>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';

const KIND_SHORT = { CYTO: 'Цитологія', BIOPSY: 'Біопсія діагностична', CITO: 'Термінова інтраопераційна (CITO)', OPERATIVE: 'Операційний матеріал', CONSULT: 'Консультація препаратів' };
const HISTO_SEGMENTS = ['Прийнято', 'Вирізка', 'Декальцинація', 'Проводка', 'Заливка', 'Мікротомія', 'Фарбування', 'Мікроскопія й опис'];
const CYTO_SEGMENTS = ['Приготування', 'Фарбування', 'Скринінг і висновок', 'Підписано'];
/** Години → «6 хв» або «1,5 год» (десяткова кома, менше години — у хвилинах) */
const hoursText = h => (h < 1 ? `${Math.max(1, Math.round(h * 60))} хв` : `${String(h).replace('.', ',')} год`);

export default {
  name: 'PathologyCasesPage',
  mixins: [apiMixin],
  data () {
    return {
      data: {}, an: null, stage: 'all', src: null, q: '', target: 20, timer: null,
      sourceOptions: [{ label: 'Стаціонар', value: 'INPATIENT' }, { label: 'Поліклініка', value: 'POLYCLINIC' }, { label: 'Е-направлення ЕСОЗ', value: 'EHEALTH' }, { label: 'Клініка-партнер', value: 'PARTNER' }]
    };
  },
  computed: {
    columns () {
      const cyto = this.cyto;
      return [
        { name: 'caseNo', label: '№ справи', field: 'caseNo', align: 'left', sortable: true },
        { name: 'patient', label: 'Пацієнт', field: 'patient', align: 'left', sortable: true },
        { name: 'kind', label: cyto ? 'Вид матеріалу' : 'Тип матеріалу', field: r => (r.kind === 'CYTO' ? r.materialLabel || KIND_SHORT.CYTO : KIND_SHORT[r.kind] || r.kindLabel), align: 'left' },
        { name: 'source', label: 'Джерело', field: 'sourceLabel', align: 'left', sortable: true },
        cyto ? { name: 'bs', label: 'Скельця', field: 'slides', align: 'center' } : { name: 'bs', label: 'Блоки / скельця', field: r => `${r.blocks} / ${r.slides}`, align: 'center' },
        { name: 'stage', label: 'Етап', field: 'stageIndex', align: 'left', sortable: true, format: (v, r) => r.stageLabel },
        { name: 'sla', label: this.cito ? 'Від надходження' : 'Строк', field: r => r.slaDueAt, align: 'left', sortable: true }
      ];
    },
    filtered () { return this.stage !== 'all' || !!this.src || !!(this.q && this.q.trim()); },
    cito () { return !!this.$route.meta.cito; },
    cyto () { return this.$route.meta.group === 'cyto'; },
    analyticsTiles () {
      const a = this.an || {};
      return [
        { label: 'Непридатний матеріал', value: `${a.unsatisfactory || 0} (${a.unsatisfactoryShare || 0}%)` },
        { label: 'Розбіжність скринінгу й лікаря', value: `${a.mismatches || 0} з ${a.screened || 0} (${String(a.mismatchShare || 0).replace('.', ',')}%)` },
        { label: 'Час виконання, медіана', value: a.tatMedianHours == null ? '—' : hoursText(a.tatMedianHours) }
      ];
    },
    items () { return this.data.items || []; },
    tiles () {
      const t = this.data.tiles || {};
      if (this.cyto) {
        return [
          { value: 'all', label: 'Усі справи', count: this.data.total || 0 },
          { value: 'work', label: 'У роботі', count: this.data.inWork || 0 },
          { value: 'overdue', label: 'Прострочені', count: this.data.overdue || 0, cls: 'text-negative' },
          { value: 'GROSS', label: 'Приготування', count: (t.RCV || 0) + (t.GROSS || 0) },
          { value: 'STAIN', label: 'Фарбування', count: t.STAIN || 0 },
          { value: 'MICRO', label: 'Скринінг і висновок', count: t.MICRO || 0 },
          { value: 'SIGNED', label: 'Підписано', count: t.SIGNED || 0, cls: 'text-positive' }
        ];
      }
      return [
        { value: 'all', label: 'Усі справи', count: this.data.total || 0 },
        { value: 'work', label: 'У роботі', count: this.data.inWork || 0 },
        { value: 'cito', label: 'CITO', count: this.data.cito || 0, cls: 'text-negative' },
        { value: 'overdue', label: 'Прострочені', count: this.data.overdue || 0, cls: 'text-negative' },
        { value: 'GROSS', label: 'Вирізка', count: (t.RCV || 0) + (t.GROSS || 0) },
        { value: 'lab', label: 'Лаб. обробка', count: this.data.lab || 0 },
        { value: 'MICRO', label: 'Мікроскопія й опис', count: t.MICRO || 0 },
        { value: 'SIGNED', label: 'Підписано', count: t.SIGNED || 0, cls: 'text-positive' }
      ];
    }
  },
  watch: {
    stage () { this.load(); },
    src () { this.load(); },
    q () { this.load(); },
    '$route.name' () { this.load(); }
  },
  created () {
    this.load();
    this.timer = setInterval(() => { if (this.cito) this.load(true); }, 30000);
  },
  beforeDestroy () { clearInterval(this.timer); },
  methods: {
    async load (silent) {
      const stage = this.cito ? 'cito' : this.stage === 'GROSS' ? 'all' : this.stage;
      const group = this.cito ? null : this.$route.meta.group || null;
      if (this.cyto && silent !== true) this.an = await this.$api.pathCytoAnalytics().catch(() => null);
      const res = await this.callApi(() => this.$api.pathCases({ stage, src: this.src, q: this.q, group }), { silent: silent === true, loading: silent !== true });
      if (res) {
        if (this.stage === 'GROSS' && !this.cito) res.items = res.items.filter(i => i.stage === 'RCV' || i.stage === 'GROSS');
        this.data = res;
      }
    },
    stepClass (i, row) {
      const idx = this.cyto ? ({ RCV: 0, GROSS: 0, STAIN: 1, MICRO: 2, SIGNED: 3 }[row.stage] || 0) : row.stageIndex;
      return i < idx || row.stage === 'SIGNED' ? 'done' : i === idx ? 'cur' : '';
    },
    segmentLabel (i) { return (this.cyto ? CYTO_SEGMENTS : HISTO_SEGMENTS)[i] || ''; },
    /** Відділення без повтору назви джерела: «Поліклініка · гінеколог» під підписом «Поліклініка» → «гінеколог» */
    deptShort (row) {
      return String(row.department || '').split('·').map(s => s.trim()).filter(s => s && s !== row.sourceLabel).join(' · ');
    },
    resetFilters () { this.stage = 'all'; this.src = null; this.q = ''; },
    openCase (r) { this.$router.push({ name: 'lab-path-case', params: { id: r.id } }); }
  }
};
</script>

<style scoped>
.tile { border: 1px solid transparent; transition: border-color .15s; }
.tile-active { border-color: var(--q-color-primary, #4274a7); border-width: 2px; background: #eef4fb; }
.mini-progress { display: inline-flex; gap: 2px; }
.mini-progress span { width: 9px; height: 6px; border-radius: 2px; background: #dde3ea; }
.mini-progress span.done { background: #4caf50; }
.mini-progress span.cur { background: #ff9800; }
</style>
