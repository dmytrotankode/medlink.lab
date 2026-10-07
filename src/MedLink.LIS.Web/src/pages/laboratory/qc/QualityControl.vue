<template>
  <div class="qc-page" data-testid="qcPage">
    <page-header title="Внутрішній контроль якості (ВКЯ)" icon="fas fa-chart-line" subtitle="Карти Леві-Дженнінгса, правила Вестгарда (1₂s, 1₃s, 2₂s, R₄s, 4₁s, 10ₓ), автоматичний Lockout аналізатора" :breadcrumbs="[{ label: 'Контроль якості' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="loadAll" />
      <q-btn outline dense color="primary" icon="inventory_2" label="Матеріал" data-testid="qcNewMaterial" @click="openMaterial(null)" />
      <q-btn unelevated dense color="primary" icon="add_chart" label="Внести точку контролю" :disable="!materialId" data-testid="qcAddPoint" @click="pointOpen = true" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="loadAll" />

    <q-banner v-for="l in lockouts" :key="l.id" dense rounded class="bg-red-1 text-negative q-mb-sm" style="border-left: 5px solid #d04f45">
      <template v-slot:avatar><q-icon name="lock" color="negative" size="28px" /></template>
      <div class="text-weight-bold">УВАГА: {{ analyzerName(l.analyzerId) }} заблоковано{{ l.testCode ? ` (тест ${l.testCode})` : '' }} — {{ l.reason }}</div>
      <div class="text-caption text-grey-8">з {{ l.startedAt | datetime }}. Видача результатів цього аналізатора/тесту заморожена до усунення причини.</div>
      <template v-slot:action><q-btn dense color="negative" icon="lock_open" label="Розблокувати" @click="openResolve(l)" /></template>
    </q-banner>

    <q-tabs v-model="tab" dense align="left" class="text-grey-8 medlink-card bg-white" active-color="primary" indicator-color="primary">
      <q-tab name="chart" icon="show_chart" label="Карта Леві-Дженнінгса" />
      <q-tab name="materials" icon="inventory_2" :label="`Матеріали (${materials.length})`" />
      <q-tab name="lockouts" icon="lock" :label="`Lockout (${lockouts.length})`" data-testid="tab-lockouts" />
      <q-tab name="report" icon="summarize" label="Звіт ВКЯ" />
    </q-tabs>

    <q-tab-panels v-model="tab" animated class="bg-transparent q-mt-sm">
      <!-- Карта -->
      <q-tab-panel name="chart" class="q-pa-none">
        <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
          <div class="col-12 col-md-3"><q-select v-model="analyzerId" dense outlined clearable label="Аналізатор" :options="analyzerOptions" emit-value map-options @input="onAnalyzer" /></div>
          <div class="col-12 col-md-3"><q-select v-model="materialId" dense outlined label="Контрольний матеріал / лот" :options="materialOptions" emit-value map-options data-testid="qcMaterialSelect" @input="onMaterial" /></div>
          <div class="col-6 col-md-2"><q-select v-model="testCode" dense outlined label="Показник" :options="testOptions" emit-value map-options data-testid="qcTestSelect" @input="loadChart" /></div>
          <div class="col-6 col-md-2"><q-select v-model="days" dense outlined label="Період" :options="[{ value: 7, label: '7 днів' }, { value: 30, label: '30 днів' }, { value: 90, label: '90 днів' }]" emit-value map-options @input="loadChart" /></div>
          <div class="col-12 col-md-2 text-right"><status-chip v-if="chart" :value="chart.currentStatus || 'OK'" type="qc" icon /></div>
        </div>

        <div v-if="chart" class="row q-col-gutter-sm q-mb-sm">
          <div v-for="k in kpis" :key="k.label" class="col-6 col-sm-4 col-md-2">
            <div class="kpi-tile" :class="k.cls">
              <div class="kpi-tile__label">{{ k.label }}</div>
              <div class="kpi-tile__value" :class="k.valueCls">{{ k.value }}</div>
              <div class="kpi-tile__hint">{{ k.hint }}</div>
            </div>
          </div>
        </div>

        <div class="medlink-card q-pa-md q-mb-sm">
          <div class="row items-center justify-between q-mb-xs">
            <div class="text-subtitle2 text-weight-bold">Карта Леві-Дженнінгса <span v-if="chart" class="text-grey-7">— {{ materialLabel }}, {{ testCode }}</span></div>
            <div class="text-caption text-grey-7">N = {{ chart ? chart.n : 0 }} · вікно Вестгарда 10 точок</div>
          </div>
          <levey-jennings-chart v-if="chart" :points="chart.points || []" :mean="Number(chart.targetMean) || 0" :sd="Number(chart.targetSd) || 1" @point-click="editPoint" />
          <empty-state v-else title="Оберіть матеріал і показник" icon="show_chart" hint="Карта будується з /qc/levey-jennings" />
        </div>

        <div class="row q-col-gutter-sm">
          <div class="col-12 col-md-7">
            <div class="medlink-card">
              <div class="medlink-card__title"><span>Серія вимірювань</span><span class="text-caption text-grey-6">клік по точці на графіку — редагування</span></div>
              <q-table :data="chart ? (chart.points || []) : []" :columns="pointColumns" dense flat row-key="id" :pagination="{ rowsPerPage: 10, sortBy: 'at', descending: true }" no-data-label="Точок немає" data-testid="qcPointsTable">
                <template v-slot:body-cell-at="props"><q-td :props="props">{{ props.row.at | datetime }}</q-td></template>
                <template v-slot:body-cell-value="props"><q-td :props="props" class="text-right text-weight-bold">{{ props.row.value | num }}</q-td></template>
                <template v-slot:body-cell-z="props"><q-td :props="props" class="text-right" :class="Math.abs(Number(props.row.z !== undefined ? props.row.z : props.row.zScore)) > 2 ? 'text-negative text-weight-bold' : ''">{{ (props.row.z !== undefined ? props.row.z : props.row.zScore) | num(2) }}</q-td></template>
                <template v-slot:body-cell-rules="props"><q-td :props="props"><q-badge v-for="r in (props.row.rules || props.row.violatedRules || [])" :key="r" :color="ruleColor(r)" :label="ruleLabel(r)" class="q-mr-xs" /></q-td></template>
                <template v-slot:body-cell-status="props"><q-td :props="props"><status-chip :value="pointStatus(props.row)" type="qc" /></q-td></template>
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props" class="text-right no-wrap">
                    <q-btn flat dense round size="sm" icon="edit" color="primary" @click="editPoint(props.row)" />
                    <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDeletePoint(props.row)" />
                  </q-td>
                </template>
              </q-table>
            </div>
          </div>
          <div class="col-12 col-md-5">
            <div class="medlink-card">
              <div class="medlink-card__title"><span>Правила Вестгарда</span></div>
              <q-list dense separator>
                <q-item v-for="r in rules" :key="r.code">
                  <q-item-section avatar><q-badge :color="r.kind === 'warning' ? 'warning' : 'negative'" :text-color="r.kind === 'warning' ? 'dark' : 'white'" :label="r.label" /></q-item-section>
                  <q-item-section><q-item-label>{{ r.description }}</q-item-label><q-item-label caption>{{ r.kind === 'warning' ? 'попередження' : 'rejection → Lockout аналізатора/тесту' }}</q-item-label></q-item-section>
                </q-item>
              </q-list>
            </div>
          </div>
        </div>
      </q-tab-panel>

      <!-- Матеріали -->
      <q-tab-panel name="materials" class="q-pa-none">
        <div class="medlink-card">
          <q-table :data="materials" :columns="materialColumns" dense flat row-key="id" :loading="loading" :pagination="{ rowsPerPage: 25 }" no-data-label="Контрольних матеріалів немає" data-testid="qcMaterialsTable">
            <template v-slot:body-cell-level="props"><q-td :props="props">{{ levelLabel(props.row.level) }}</q-td></template>
            <template v-slot:body-cell-expiryDate="props"><q-td :props="props" :class="expired(props.row) ? 'text-negative text-weight-bold' : ''">{{ props.row.expiryDate | date }}</q-td></template>
            <template v-slot:body-cell-targets="props"><q-td :props="props"><q-chip v-for="t in (props.row.targets || [])" :key="t.testCode" dense size="sm" color="blue-1" text-color="primary">{{ t.testCode }}: {{ t.targetMean | num }}±{{ t.targetSd | num }}</q-chip></q-td></template>
            <template v-slot:body-cell-isActive="props"><q-td :props="props"><q-icon :name="props.row.isActive === false ? 'cancel' : 'check_circle'" :color="props.row.isActive === false ? 'grey-5' : 'positive'" /></q-td></template>
            <template v-slot:body-cell-actions="props">
              <q-td :props="props" class="text-right no-wrap">
                <q-btn flat dense round size="sm" icon="show_chart" color="primary" @click="selectMaterial(props.row)"><q-tooltip>Карта</q-tooltip></q-btn>
                <q-btn flat dense round size="sm" icon="edit" color="primary" @click="openMaterial(props.row)" />
                <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDeleteMaterial(props.row)" />
              </q-td>
            </template>
          </q-table>
        </div>
      </q-tab-panel>

      <!-- Lockouts -->
      <q-tab-panel name="lockouts" class="q-pa-none">
        <div class="medlink-card q-pa-sm q-mb-sm row items-center">
          <q-toggle v-model="onlyActive" label="Тільки активні" color="primary" @input="loadLockouts" />
        </div>
        <div class="medlink-card">
          <q-table :data="lockoutsAll" :columns="lockoutColumns" dense flat row-key="id" :loading="loading" :pagination="{ rowsPerPage: 25 }" no-data-label="Блокувань немає" data-testid="lockoutsTable">
            <template v-slot:body-cell-analyzer="props"><q-td :props="props">{{ analyzerName(props.row.analyzerId) }}</q-td></template>
            <template v-slot:body-cell-startedAt="props"><q-td :props="props">{{ props.row.startedAt | datetime }}</q-td></template>
            <template v-slot:body-cell-resolvedAt="props"><q-td :props="props">{{ props.row.resolvedAt | datetime }}</q-td></template>
            <template v-slot:body-cell-state="props"><q-td :props="props"><q-badge :color="props.row.resolvedAt ? 'positive' : 'negative'" :label="props.row.resolvedAt ? 'знято' : 'активне'" /></q-td></template>
            <template v-slot:body-cell-actions="props">
              <q-td :props="props" class="text-right no-wrap">
                <q-btn v-if="!props.row.resolvedAt" dense color="negative" size="sm" icon="lock_open" label="Розблокувати" :data-testid="`resolve-${props.row.id}`" @click="openResolve(props.row)" />
                <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDeleteLockout(props.row)" />
              </q-td>
            </template>
          </q-table>
        </div>
      </q-tab-panel>

      <!-- Звіт -->
      <q-tab-panel name="report" class="q-pa-none">
        <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
          <div class="col-12 col-md-3"><q-select v-model="reportAnalyzerId" dense outlined clearable label="Аналізатор" :options="analyzerOptions" emit-value map-options /></div>
          <div class="col-6 col-md-2"><q-input v-model="reportFrom" dense outlined type="date" stack-label label="З" /></div>
          <div class="col-6 col-md-2"><q-input v-model="reportTo" dense outlined type="date" stack-label label="По" /></div>
          <div class="col-12 col-md-2"><q-btn color="primary" icon="summarize" label="Сформувати" @click="loadReport" /></div>
        </div>
        <div class="medlink-card q-pa-md">
          <empty-state v-if="!report" title="Зведений звіт ВКЯ" hint="Оберіть період та натисніть «Сформувати»" icon="summarize" />
          <div v-else>
            <q-table v-if="Array.isArray(reportRows)" :data="reportRows" :columns="reportColumns" dense flat row-key="key" :pagination="{ rowsPerPage: 50 }" />
            <pre v-else class="mono" style="font-size: 12px; white-space: pre-wrap">{{ JSON.stringify(report, null, 2) }}</pre>
          </div>
        </div>
      </q-tab-panel>
    </q-tab-panels>

    <!-- Діалоги -->
    <qc-material-dialog v-model="materialOpen" :material="activeMaterial" @saved="afterMaterial" />

    <q-dialog v-model="pointOpen" persistent>
      <q-card style="min-width: 460px" data-testid="qcPointDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="add_chart" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ point.id ? 'Редагування точки' : 'Точка контролю' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <div class="text-caption">Матеріал: <b>{{ materialLabel }}</b></div>
          <q-select v-model="point.testCode" outlined dense label="Показник" :options="testOptions" emit-value map-options />
          <q-input v-model="point.measuredValue" outlined dense label="Виміряне значення *" inputmode="decimal" autofocus data-testid="qcPointValue" @keyup.enter="savePoint" :hint="target ? `ціль ${formatNumber(target.targetMean)} ± ${formatNumber(target.targetSd)} ${target.unit || ''}` : ''" />
          <q-input v-model="point.runAt" outlined dense type="datetime-local" stack-label label="Час вимірювання (порожньо — зараз)" />
          <div v-if="pointError" class="text-negative">{{ pointError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" icon="save" label="Зберегти та оцінити" :loading="saving" :disable="!point.testCode || !point.measuredValue" data-testid="qcPointSave" @click="savePoint" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="resolveOpen" persistent>
      <q-card style="min-width: 520px" data-testid="resolveDialog">
        <q-card-section class="row items-center bg-negative text-white q-py-sm"><q-icon name="lock_open" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">Протокол зняття блокування</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <div v-if="activeLockout" class="text-body2">{{ analyzerName(activeLockout.analyzerId) }} <span v-if="activeLockout.testCode">· {{ activeLockout.testCode }}</span> — <b>{{ activeLockout.reason }}</b></div>
          <q-select v-model="resolve.cause" outlined dense label="Встановлена причина *" :options="causes" use-input new-value-mode="add-unique" data-testid="resolveCause" />
          <q-select v-model="resolve.action" outlined dense label="Коригувальна дія *" :options="actions" use-input new-value-mode="add-unique" data-testid="resolveAction" />
          <q-input v-model="resolve.comment" outlined dense autogrow label="Коментар / результат повторного контролю *" data-testid="resolveComment" />
          <div v-if="resolveError" class="text-negative">{{ resolveError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="negative" icon="lock_open" label="Підтвердити та розблокувати" :loading="saving" :disable="!resolve.cause || !resolve.action || !resolve.comment" data-testid="resolveConfirm" @click="doResolve" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="deleteMaterialOpen" title="Видалити матеріал" message="Контрольний матеріал буде деактивовано/видалено разом із цільовими значеннями." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteMaterial" />
    <confirm-dialog v-model="deletePointOpen" title="Видалити точку контролю" message="Точку буде видалено; правила Вестгарда переоцінюються сервером." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeletePoint" />
    <confirm-dialog v-model="deleteLockoutOpen" title="Видалити запис Lockout" message="Запис блокування буде видалено з журналу." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteLockout" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import LeveyJenningsChart from '../../../components/charts/LeveyJenningsChart.vue';
import QcMaterialDialog from './QcMaterialDialog.vue';
import { WESTGARD_RULES, QC_LEVELS } from '../../../utils/statuses';
import { formatNumber, parseDecimal, daysAgoIso, todayIso, formatDateTime } from '../../../utils/format';

export default {
  name: 'QualityControl',
  mixins: [apiMixin],
  components: { LeveyJenningsChart, QcMaterialDialog },
  data () {
    return {
      tab: 'chart',
      materials: [], lockouts: [], lockoutsAll: [], chart: null, report: null,
      analyzerId: null, materialId: null, testCode: null, days: 30,
      onlyActive: true,
      reportAnalyzerId: null, reportFrom: daysAgoIso(30), reportTo: todayIso(),
      materialOpen: false, pointOpen: false, resolveOpen: false, deleteMaterialOpen: false, deletePointOpen: false, deleteLockoutOpen: false,
      activeMaterial: null, activeLockout: null, activePoint: null,
      point: { id: null, testCode: null, measuredValue: '', runAt: '' }, pointError: '',
      resolve: { cause: null, action: null, comment: '' }, resolveError: '',
      saving: false,
      rules: WESTGARD_RULES,
      causes: ['Реагент: новий лот / закінчення терміну', 'Калібрування: дрейф', 'Контрольний матеріал: неправильне зберігання / відкриття', 'Технічна несправність аналізатора', 'Помилка оператора', 'Температурний режим'],
      actions: ['Перекалібровано', 'Замінено реагент', 'Відкрито новий флакон контролю', 'Проведено технічне обслуговування', 'Повторено контроль — у межах ±2SD', 'Викликано сервісного інженера'],
      pointColumns: [
        { name: 'at', label: 'Час', field: 'at', align: 'left', sortable: true },
        { name: 'value', label: 'Значення', field: 'value', align: 'right', sortable: true },
        { name: 'z', label: 'z', align: 'right' },
        { name: 'rules', label: 'Правила', align: 'left' },
        { name: 'status', label: 'Оцінка', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ],
      materialColumns: [
        { name: 'name', label: 'Назва', field: 'name', align: 'left', sortable: true },
        { name: 'analyzer', label: 'Аналізатор', field: r => this.analyzerName(r.analyzerId), align: 'left' },
        { name: 'level', label: 'Рівень', field: 'level', align: 'left' },
        { name: 'lotNumber', label: 'Лот', field: 'lotNumber', align: 'left' },
        { name: 'manufacturer', label: 'Виробник', field: 'manufacturer', align: 'left' },
        { name: 'expiryDate', label: 'Придатний до', field: 'expiryDate', align: 'left', sortable: true },
        { name: 'targets', label: 'Цілі', align: 'left' },
        { name: 'isActive', label: 'Акт.', align: 'center' },
        { name: 'actions', label: '', align: 'right' }
      ],
      lockoutColumns: [
        { name: 'analyzer', label: 'Аналізатор', align: 'left' },
        { name: 'testCode', label: 'Тест', field: r => r.testCode || 'усі', align: 'left' },
        { name: 'reason', label: 'Причина', field: 'reason', align: 'left' },
        { name: 'startedAt', label: 'Початок', field: 'startedAt', align: 'left', sortable: true },
        { name: 'resolvedAt', label: 'Знято', field: 'resolvedAt', align: 'left' },
        { name: 'action', label: 'Дія', field: r => r.action || r.resolutionAction || '', align: 'left' },
        { name: 'state', label: 'Стан', align: 'center' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    analyzers () { return this.$store.state.laboratory.analyzers; },
    analyzerOptions () { return this.analyzers.map(a => ({ value: a.id, label: a.name })); },
    materialsFiltered () { return this.analyzerId ? this.materials.filter(m => m.analyzerId === this.analyzerId) : this.materials; },
    materialOptions () { return this.materialsFiltered.map(m => ({ value: m.id, label: `${m.name} · ${this.levelLabel(m.level)} · лот ${m.lotNumber}` })); },
    material () { return this.materials.find(m => m.id === this.materialId) || null; },
    materialLabel () { return this.material ? `${this.material.name} (лот ${this.material.lotNumber})` : '—'; },
    testOptions () { return ((this.material && this.material.targets) || []).map(t => ({ value: t.testCode, label: `${t.testCode}${t.unit ? ' · ' + t.unit : ''}` })); },
    target () { return ((this.material && this.material.targets) || []).find(t => t.testCode === (this.point.testCode || this.testCode)) || null; },
    kpis () {
      const c = this.chart; if (!c) return [];
      const sd = Number(c.targetSd) || 0; const mean = Number(c.targetMean) || 0;
      const last = (c.points || []).slice(-1)[0];
      const z = last ? Number(last.z !== undefined ? last.z : last.zScore) : null;
      return [
        { label: 'Цільове Mean', value: formatNumber(mean), hint: this.target && this.target.unit, cls: 'kpi-tile--accent' },
        { label: 'Цільове SD', value: `±${formatNumber(sd, 2)}`, hint: `±2SD: ${formatNumber(mean - 2 * sd, 2)}–${formatNumber(mean + 2 * sd, 2)}`, cls: '' },
        { label: 'N / Mean факт.', value: `${c.n || 0} / ${formatNumber(c.mean, 2)}`, hint: `SD факт. ${formatNumber(c.sd, 2)}`, cls: '' },
        { label: 'CV %', value: formatNumber(c.cvPct, 1), hint: 'коефіцієнт варіації', cls: Number(c.cvPct) > 5 ? 'kpi-tile--warning' : 'kpi-tile--positive' },
        { label: 'Bias %', value: formatNumber(c.bias, 2), hint: 'зміщення від цілі', cls: Math.abs(Number(c.bias)) > 3 ? 'kpi-tile--warning' : '' },
        { label: 'Поточний z', value: z === null ? '—' : formatNumber(z, 2), hint: last ? formatDateTime(last.at) : '', cls: z !== null && Math.abs(z) > 2 ? 'kpi-tile--negative' : 'kpi-tile--positive', valueCls: z !== null && Math.abs(z) > 3 ? 'text-negative' : '' }
      ];
    },
    reportRows () { if (!this.report) return null; if (Array.isArray(this.report)) return this.report.map((r, i) => ({ key: i, ...r })); if (Array.isArray(this.report.items)) return this.report.items.map((r, i) => ({ key: i, ...r })); if (Array.isArray(this.report.rows)) return this.report.rows.map((r, i) => ({ key: i, ...r })); return null; },
    reportColumns () { const first = this.reportRows && this.reportRows[0]; if (!first) return []; return Object.keys(first).filter(k => k !== 'key').map(k => ({ name: k, label: k, field: k, align: 'left', sortable: true })); }
  },
  watch: {
    tab (t) { this.$router.replace({ query: { ...this.$route.query, tab: t } }).catch(() => {}); }
  },
  created () {
    if (this.$route.query.tab) this.tab = this.$route.query.tab;
    this.loadAll();
  },
  methods: {
    formatNumber,
    levelLabel (l) { const x = QC_LEVELS.find(q => q.value === l); return x ? x.label : l; },
    analyzerName (id) { const a = this.analyzers.find(x => x.id === id); return a ? a.name : (id || '—'); },
    expired (m) { return m.expiryDate && new Date(m.expiryDate) < new Date(); },
    ruleLabel (code) { const r = WESTGARD_RULES.find(x => x.code === code); return r ? r.label : code; },
    ruleColor (code) { const r = WESTGARD_RULES.find(x => x.code === code); return r && r.kind === 'warning' ? 'warning' : 'negative'; },
    pointStatus (p) { if (p.status) return p.status; if (p.isRejection || p.lockoutEnforced) return 'LOCKOUT'; if (p.isWarning) return 'WARNING'; return 'OK'; },
    async loadAll () {
      this.loading = true;
      await this.$store.dispatch('laboratory/refreshStatus');
      const results = await Promise.allSettled([this.$api.qcMaterials(), this.$api.qcLockouts(true)]);
      if (results[0].status === 'fulfilled') this.materials = this.asList(results[0].value);
      if (results[1].status === 'fulfilled') this.lockouts = this.asList(results[1].value);
      const failed = results.find(r => r.status === 'rejected');
      this.apiError = failed && !this.apiOffline ? failed.reason.userMessage : null;
      this.loading = false;
      if (!this.materialId && this.materials.length) {
        this.materialId = this.materials[0].id;
        this.analyzerId = this.materials[0].analyzerId;
      }
      if (this.materialId && !this.testCode) this.onMaterial();
      else if (this.materialId) this.loadChart();
      this.loadLockouts();
    },
    onAnalyzer () {
      const first = this.materialsFiltered[0];
      this.materialId = first ? first.id : null;
      this.onMaterial();
    },
    onMaterial () {
      const t = this.testOptions[0];
      this.testCode = t ? t.value : null;
      if (this.material) this.analyzerId = this.material.analyzerId;
      this.loadChart();
    },
    selectMaterial (m) { this.materialId = m.id; this.tab = 'chart'; this.onMaterial(); },
    async loadChart () {
      if (!this.materialId || !this.testCode) { this.chart = null; return; }
      const res = await this.callApi(() => this.$api.leveyJennings({ analyzerId: this.analyzerId, materialId: this.materialId, testCode: this.testCode, days: this.days }), { silent: true });
      this.chart = res || null;
    },
    async loadLockouts () {
      try { this.lockoutsAll = this.asList(await this.$api.qcLockouts(this.onlyActive ? true : null)); } catch (e) { this.lockoutsAll = this.lockouts; }
    },
    openMaterial (m) { this.activeMaterial = m; this.materialOpen = true; },
    afterMaterial (res) { this.loadAll(); if (res && res.id) { this.materialId = res.id; this.onMaterial(); } },
    askDeleteMaterial (m) { this.activeMaterial = m; this.deleteMaterialOpen = true; },
    async doDeleteMaterial () { try { await this.$api.deleteQcMaterial(this.activeMaterial.id); this.notifyOk('Матеріал видалено'); if (this.materialId === this.activeMaterial.id) { this.materialId = null; this.testCode = null; this.chart = null; } this.loadAll(); } catch (e) { this.notifyError(e); } },
    editPoint (p) {
      this.point = { id: p.id, testCode: p.testCode || this.testCode, measuredValue: String(p.value !== undefined ? p.value : p.measuredValue).replace('.', ','), runAt: p.at ? String(p.at).slice(0, 16) : '' };
      this.pointError = ''; this.pointOpen = true;
    },
    async savePoint () {
      this.saving = true; this.pointError = '';
      const body = { qcMaterialId: this.materialId, testCode: this.point.testCode, measuredValue: parseDecimal(this.point.measuredValue), runAt: this.point.runAt ? new Date(this.point.runAt).toISOString() : undefined };
      try {
        const res = this.point.id ? await this.$api.updateQcResult(this.point.id, body) : await this.$api.addQcResult(body);
        const rules = (res && (res.violatedRules || res.rules)) || [];
        if (res && (res.lockoutEnforced || res.isRejection)) this.$q.notify({ type: 'negative', icon: 'lock', message: `Порушення ${rules.map(this.ruleLabel).join(', ')} — аналізатор заблоковано (Lockout)`, timeout: 8000 });
        else if (res && res.isWarning) this.$q.notify({ type: 'warning', message: `Попередження ${rules.map(this.ruleLabel).join(', ')} (z=${formatNumber(res.zScore, 2)})` });
        else this.notifyOk(`Точку збережено${res && res.zScore !== undefined ? ` (z=${formatNumber(res.zScore, 2)})` : ''}`);
        this.pointOpen = false;
        this.point = { id: null, testCode: this.testCode, measuredValue: '', runAt: '' };
        this.loadChart(); this.loadAll();
      } catch (e) { this.pointError = e.userMessage || 'Помилка збереження'; } finally { this.saving = false; }
    },
    askDeletePoint (p) { this.activePoint = p; this.deletePointOpen = true; },
    async doDeletePoint () { try { await this.$api.deleteQcResult(this.activePoint.id); this.notifyOk('Точку видалено'); this.loadChart(); } catch (e) { this.notifyError(e); } },
    openResolve (l) { this.activeLockout = l; this.resolve = { cause: null, action: null, comment: '' }; this.resolveError = ''; this.resolveOpen = true; },
    async doResolve () {
      this.saving = true; this.resolveError = '';
      try {
        await this.$api.resolveLockout(this.activeLockout.id, { ...this.resolve });
        this.notifyOk('Блокування знято, протокол збережено');
        this.resolveOpen = false;
        this.loadAll();
      } catch (e) { this.resolveError = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    },
    askDeleteLockout (l) { this.activeLockout = l; this.deleteLockoutOpen = true; },
    async doDeleteLockout () { try { await this.$api.deleteLockout(this.activeLockout.id); this.notifyOk('Запис видалено'); this.loadAll(); } catch (e) { this.notifyError(e); } },
    async loadReport () {
      const res = await this.callApi(() => this.$api.qcReport({ analyzerId: this.reportAnalyzerId, from: this.reportFrom, to: this.reportTo }));
      this.report = res || null;
    }
  }
};
</script>
