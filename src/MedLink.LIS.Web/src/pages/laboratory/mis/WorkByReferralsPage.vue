<!--
  ТЗ 23.5 (FR-WRK-001…010): робота за направленнями — ті самі тести в трьох розрізах: «Аналізатори» (черга приладу),
  «Пацієнти» (направлення пацієнта з послугами), «Дослідження» (матриця замовлення × показник; «·» — не обрано лікарем).
-->
<template>
  <div data-testid="workByReferralsPage">
    <page-header title="Робота за направленнями" icon="fas fa-tasks" subtitle="Черги аналізаторів, пацієнти й дослідження: що в черзі, в аналізі, з результатом, без обладнання" :breadcrumbs="[{ label: 'Робота за направленнями' }]">
      <q-btn flat dense color="primary" icon="download" label="XLSX" type="a" :href="exportUrl" class="q-mr-sm" />
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-sm row q-gutter-sm items-center">
      <q-select v-model="f.source" dense outlined clearable emit-value map-options :options="sourceOptions" label="Джерело" style="min-width: 180px" />
      <q-input v-model="f.department" dense outlined clearable label="Відділення" style="min-width: 180px" />
      <q-input v-model="f.q" dense outlined clearable label="Пошук: № замовлення, штрихкод, пацієнт, код" style="min-width: 280px" @keyup.enter="load" />
      <q-toggle v-model="f.packagesOnly" label="Лише пакети" dense />
      <q-toggle v-model="citoOnly" label="Лише CITO" dense color="deep-orange-6" />
      <q-btn-toggle v-model="f.status" dense unelevated toggle-color="primary" color="grey-2" text-color="grey-9" :options="[{ label: 'Активні', value: 'active' }, { label: 'Усі', value: 'all' }]" />
    </div>

    <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8 q-mb-sm">
      <q-tab name="analyzers" icon="fas fa-server" label="Аналізатори" />
      <q-tab name="patients" icon="people" label="Пацієнти" />
      <q-tab name="services" icon="fas fa-vials" label="Дослідження" />
    </q-tabs>

    <!-- Аналізатори -->
    <div v-show="tab === 'analyzers'">
      <div class="row q-col-gutter-sm q-mb-sm">
        <div v-for="a in summary.analyzers || []" :key="a.id" class="col-6 col-sm-4 col-md-3 col-lg-2">
          <q-card flat bordered class="cursor-pointer an-tile" :class="{ 'an-tile--active': analyzerId === a.id, 'an-tile--bad': a.longOffline }" @click="openAnalyzer(a.id)">
            <q-card-section class="q-pa-sm">
              <div class="text-weight-bold ellipsis">{{ a.name }}</div>
              <div class="text-caption text-grey-7 ellipsis">{{ a.location }}</div>
              <div class="row q-gutter-xs q-mt-xs">
                <q-badge color="grey-7" :label="`черга ${a.queue}`" />
                <q-badge v-if="a.inAnalysis" color="primary" :label="`аналіз ${a.inAnalysis}`" />
                <q-badge v-if="a.results" color="teal-6" :label="`рез. ${a.results}`" />
                <q-badge v-if="a.cito" color="deep-orange-6" :label="`CITO ${a.cito}`" />
              </div>
              <div v-if="a.longOffline" class="text-caption text-negative q-mt-xs"><q-icon name="wifi_off" /> без зв’язку</div>
              <div v-if="a.lockouts.length" class="text-caption text-negative"><q-icon name="lock" /> {{ a.lockouts.join(', ') }}</div>
            </q-card-section>
          </q-card>
        </div>
        <div class="col-6 col-sm-4 col-md-3 col-lg-2">
          <q-card flat bordered class="cursor-pointer an-tile an-tile--none" :class="{ 'an-tile--active': analyzerId === 'none' }" @click="openAnalyzer('none')">
            <q-card-section class="q-pa-sm">
              <div class="text-weight-bold">Без обладнання</div>
              <div class="text-caption text-grey-7">send-out / ручне / відхилення</div>
              <q-badge class="q-mt-xs" :color="summary.noEquipment ? 'negative' : 'grey-5'" :label="String(summary.noEquipment || 0)" />
            </q-card-section>
          </q-card>
        </div>
      </div>
      <div v-if="analyzerView" class="medlink-card">
        <div class="q-pa-sm row items-center">
          <div class="text-subtitle1 text-weight-bold">{{ analyzerView.analyzer }}</div>
          <span class="text-caption text-grey-7 q-ml-sm">{{ analyzerView.location }} · позицій {{ analyzerView.total }}</span>
          <q-space />
          <q-btn v-if="currentTile && currentTile.longOffline" outline dense color="negative" icon="alt_route" label="Переназначити чергу" @click="reroute" />
        </div>
        <q-table :data="analyzerView.items" :columns="rowColumns" row-key="orderTestId" dense flat :pagination="{ rowsPerPage: 50 }" no-data-label="Черга порожня">
          <template v-slot:body-cell-order="props">
            <q-td :props="props"><router-link :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" class="text-primary">{{ props.row.orderNumber }}</router-link> <q-badge v-if="props.row.isCito" color="deep-orange-6" label="CITO" /></q-td>
          </template>
          <template v-slot:body-cell-view="props">
            <q-td :props="props"><q-badge :color="viewColor(props.row.view)" :label="props.row.viewLabel" /> <q-icon v-if="props.row.manual" name="pan_tool" color="orange-8" size="14px"><q-tooltip>Змінено вручну</q-tooltip></q-icon></q-td>
          </template>
        </q-table>
      </div>
    </div>

    <!-- Пацієнти -->
    <div v-show="tab === 'patients'" class="row q-col-gutter-md">
      <div class="col-12 col-md-4">
        <div class="medlink-card">
          <q-list separator dense>
            <q-item v-for="p in patients" :key="p.patientId" clickable :active="patientView && patientView.patientId === p.patientId" active-class="bg-blue-1" @click="openPatient(p.patientId)">
              <q-item-section>
                <q-item-label><b>{{ p.patientName }}</b> <q-badge v-if="p.cito" color="deep-orange-6" label="CITO" /></q-item-label>
                <q-item-label caption>{{ p.department || '—' }} · направлень {{ p.orders }}</q-item-label>
              </q-item-section>
              <q-item-section side><span class="text-caption">{{ p.done }}/{{ p.total }}</span><q-linear-progress :value="p.total ? p.done / p.total : 0" color="positive" style="width: 60px" /></q-item-section>
            </q-item>
          </q-list>
          <empty-state v-if="!patients.length" title="Немає пацієнтів" icon="people" />
        </div>
      </div>
      <div class="col-12 col-md-8">
        <div v-if="patientView" class="medlink-card q-pa-sm">
          <div class="row items-center q-mb-sm">
            <div class="text-subtitle1 text-weight-bold">{{ patientView.patientName }}</div>
            <span class="text-caption text-grey-7 q-ml-sm">{{ patientView.ageGender }}</span>
            <q-space />
            <q-btn flat dense color="primary" icon="timeline" label="Кумулятивний звіт" :to="{ name: 'lab-reports', query: { patient: patientView.patientId } }" />
          </div>
          <div v-for="o in patientView.orders" :key="o.orderId" class="q-mb-md">
            <div class="text-weight-bold">
              <router-link :to="{ name: 'lab-order-card', params: { id: o.orderId } }" class="text-primary">{{ o.orderNumber }}</router-link>
              <span class="text-caption text-grey-7"> · {{ o.orderDatetime | datetime }} · {{ o.sourceLabel }}<span v-if="o.department"> · {{ o.department }}</span><span v-if="o.bed"> · {{ o.bed }}</span></span>
              <q-badge v-if="o.packageCode" color="purple-5" class="q-ml-xs" :label="o.packageName || o.packageCode" />
              <q-badge v-if="o.isCito" color="deep-orange-6" class="q-ml-xs" label="CITO" />
            </div>
            <div v-for="s in o.services" :key="s.service" class="q-ml-md q-mt-xs">
              <div class="text-caption text-weight-bold">{{ s.service }} · {{ s.done }}/{{ s.total }}</div>
              <span v-for="t in s.tests" :key="t.orderTestId" class="test-chip" :class="`test-chip--${t.view}`">
                {{ t.testCode }}<span v-if="t.value"> {{ t.value }}</span><q-tooltip>{{ t.viewLabel }}<span v-if="t.analyzer"> · {{ t.analyzer }}</span></q-tooltip>
              </span>
            </div>
          </div>
        </div>
        <empty-state v-else title="Оберіть пацієнта" icon="person" />
      </div>
    </div>

    <!-- Дослідження -->
    <div v-show="tab === 'services'" class="row q-col-gutter-md">
      <div class="col-12 col-md-4">
        <div class="medlink-card">
          <q-list separator dense>
            <q-item v-for="s in services" :key="s.serviceCode" clickable :active="serviceView && serviceView.serviceCode === s.serviceCode" active-class="bg-blue-1" @click="openService(s.serviceCode)">
              <q-item-section>
                <q-item-label><b>{{ s.service }}</b></q-item-label>
                <q-item-label caption>{{ s.serviceCode }} · направлень {{ s.referrals }} · показників {{ s.indicators }}</q-item-label>
                <q-item-label caption>{{ s.byAnalyzer.map(a => `${a.analyzer}: ${a.count}`).join(' · ') }}</q-item-label>
              </q-item-section>
              <q-item-section side>
                <q-badge v-if="s.noEquipment" color="negative" :label="`без обл. ${s.noEquipment}`" />
              </q-item-section>
            </q-item>
          </q-list>
        </div>
      </div>
      <div class="col-12 col-md-8">
        <div v-if="serviceView" class="medlink-card q-pa-sm">
          <div class="text-subtitle1 text-weight-bold q-mb-sm">{{ serviceView.service }}</div>
          <div class="matrix-scroll">
            <q-markup-table dense flat bordered>
              <thead><tr><th class="text-left">Замовлення</th><th class="text-left">Пацієнт</th><th v-for="c in serviceView.columns" :key="c">{{ c }}</th></tr></thead>
              <tbody>
                <tr v-for="r in serviceView.rows" :key="r.orderId">
                  <td><router-link :to="{ name: 'lab-order-card', params: { id: r.orderId } }" class="text-primary">{{ r.orderNumber }}</router-link> <q-badge v-if="r.isCito" color="deep-orange-6" label="CITO" /></td>
                  <td>{{ r.patientName }}</td>
                  <td v-for="c in serviceView.columns" :key="c" class="text-center">
                    <span v-if="r.cells[c].notOrdered" class="text-grey-5">·</span>
                    <span v-else class="test-chip" :class="`test-chip--${r.cells[c].view}`">{{ shortAnalyzer(r.cells[c].analyzer) }}</span>
                  </td>
                </tr>
              </tbody>
            </q-markup-table>
          </div>
          <div class="text-caption text-grey-7 q-mt-xs">«·» — показник не обрано лікарем; колір — стан (черга / аналіз / результат / верифіковано / без обладнання)</div>
        </div>
        <empty-state v-else title="Оберіть дослідження" icon="science" />
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';

export default {
  name: 'WorkByReferralsPage',
  mixins: [apiMixin],
  data () {
    return {
      tab: 'analyzers', citoOnly: false,
      f: { source: null, department: '', q: '', packagesOnly: false, status: 'active' },
      summary: {}, analyzerId: null, analyzerView: null, patients: [], patientView: null, services: [], serviceView: null,
      sourceOptions: [{ label: 'Стаціонар', value: 'INPATIENT' }, { label: 'Поліклініка', value: 'POLYCLINIC' }, { label: 'Е-направлення ЕСОЗ', value: 'EHEALTH' }, { label: 'Партнер', value: 'PARTNER' }, { label: 'Інше', value: 'OTHER' }],
      rowColumns: [
        { name: 'pos', label: '№', field: 'position', align: 'right' },
        { name: 'order', label: 'Замовлення', field: 'orderNumber', align: 'left' },
        { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left', classes: 'mono' },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'dept', label: 'Відділення · ліжко', field: r => [r.department, r.bed].filter(Boolean).join(' · '), align: 'left', classes: 'text-caption' },
        { name: 'service', label: 'Дослідження', field: 'service', align: 'left' },
        { name: 'test', label: 'Показник', field: 'testCode', align: 'left' },
        { name: 'source', label: 'Джерело', field: 'sourceLabel', align: 'left', classes: 'text-caption' },
        { name: 'view', label: 'Стан', field: 'view', align: 'left' },
        { name: 'value', label: 'Результат', field: r => r.value || '', align: 'left' }
      ]
    };
  },
  computed: {
    params () { return { ...this.f, cito: this.citoOnly ? true : null }; },
    currentTile () { return (this.summary.analyzers || []).find(a => a.id === this.analyzerId); },
    exportUrl () {
      const key = this.tab === 'analyzers' ? this.analyzerId : this.tab === 'patients' ? (this.patientView && this.patientView.patientId) : (this.serviceView && this.serviceView.serviceCode);
      return this.$api.workExportUrl({ ...this.params, view: { analyzers: 'analyzer', patients: 'patient', services: 'service' }[this.tab], key });
    }
  },
  watch: {
    params: { deep: true, handler () { this.load(); } },
    tab () { this.load(); }
  },
  created () { this.load(); },
  methods: {
    viewColor (v) { return { queue: 'grey-7', run: 'primary', res: 'teal-6', ver: 'positive', none: 'negative', rej: 'grey-5' }[v] || 'grey'; },
    shortAnalyzer (a) { return a ? a.replace(/^(Mindray|MedicalSystem|Sysmex|Roche)\s+/, '') : '—'; },
    async load () {
      await this.callApi(async () => {
        if (this.tab === 'analyzers') {
          this.summary = await this.$api.workSummary(this.params);
          if (this.analyzerId) this.analyzerView = await this.$api.workByAnalyzer(this.analyzerId, this.params);
        } else if (this.tab === 'patients') {
          this.patients = await this.$api.workPatients(this.params);
          if (this.patientView) this.patientView = await this.$api.workByPatient(this.patientView.patientId, this.params);
        } else {
          this.services = await this.$api.workServices(this.params);
          if (this.serviceView) this.serviceView = await this.$api.workByService(this.serviceView.serviceCode, this.params);
        }
      });
    },
    async openAnalyzer (id) { this.analyzerId = id; this.analyzerView = await this.callApi(() => this.$api.workByAnalyzer(id, this.params)); },
    async openPatient (id) { this.patientView = await this.callApi(() => this.$api.workByPatient(id, this.params)); },
    async openService (code) { this.serviceView = await this.callApi(() => this.$api.workByService(code, this.params)); },
    reroute () {
      this.$q.dialog({ title: 'Переназначити чергу приладу?', message: 'Тести в черзі буде перенесено на доступні прилади на зв’язку', prompt: { model: 'Прилад без зв’язку', type: 'text', label: 'Причина' }, cancel: true })
        .onOk(async reason => { await this.callApi(() => this.$api.rerouteAnalyzer(this.analyzerId, reason), { success: 'Черга переназначена' }); this.load(); });
    }
  }
};
</script>

<style scoped>
.an-tile { border-left: 4px solid #1976d2; }
.an-tile--active { background: #e3f2fd; }
.an-tile--bad { border-left-color: #c10015; }
.an-tile--none { border-left-color: #9e9e9e; }
.matrix-scroll { overflow-x: auto; }
.test-chip { display: inline-block; padding: 1px 6px; margin: 2px; border-radius: 10px; font-size: 11.5px; background: #eeeeee; }
.test-chip--queue { background: #eeeeee; }
.test-chip--run { background: #bbdefb; }
.test-chip--res { background: #b2dfdb; }
.test-chip--ver { background: #c8e6c9; }
.test-chip--none { background: #ffcdd2; }
.test-chip--rej { background: #e0e0e0; text-decoration: line-through; }
</style>
