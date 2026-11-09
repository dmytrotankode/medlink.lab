<!--
  ТЗ 23.1 (FR-SVA-001…009): довідник «Послуга ЕСОЗ × аналізатори» — у клітинці пріоритет приладу або «½» (частина показників),
  «окр.» — окремі показники без послуги; картка послуги з показниками × приладами; ТЗ 21.3–21.4: словник інтервенцій ЕСОЗ і готовність;
  ТЗ 23.3: пакети послуг (дзеркало довідника МІС).
-->
<template>
  <div data-testid="analyzerServicesPage">
    <page-header title="Послуги ЕСОЗ та аналізатори" icon="fas fa-project-diagram" subtitle="Які прилади виконують послуги ЕСОЗ і з яким пріоритетом" :breadcrumbs="[{ label: 'Послуги ЕСОЗ та аналізатори' }]">
      <q-btn flat dense color="primary" icon="download" label="Експорт у CSV" type="a" :href="$api.serviceMatrixCsvUrl()" class="q-mr-sm" />
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8 q-mb-sm">
      <q-tab name="matrix" icon="grid_on" label="Матриця" />
      <q-tab name="interventions" icon="fas fa-book-medical" :label="`Інтервенції ЕСОЗ (${interventions.total || 0})`" />
      <q-tab name="packages" icon="inventory_2" :label="`Пакети (${packages.length})`" />
    </q-tabs>

    <div v-show="tab === 'matrix'" class="medlink-card q-pa-sm">
      <div class="row q-gutter-sm q-mb-sm text-caption text-grey-8">
        <span><q-badge color="primary" label="1" /> пріоритет приладу в послузі</span>
        <span><q-badge color="orange-7" label="½" /> прилад виконує частину показників</span>
        <span><q-badge color="grey-6" label="окр." /> Окремі показники (без послуги на приладі)</span>
        <span><q-badge color="negative" icon="lock" /> Блокування за ВКЯ на показник</span>
      </div>
      <div class="matrix-scroll">
        <table class="svc-matrix" data-testid="serviceMatrix">
          <thead>
            <tr>
              <th class="text-left sticky-col">Послуга ЕСОЗ</th>
              <th>Готовність</th>
              <th v-for="a in matrix.analyzers" :key="a.id" class="an-head">
                <div class="an-name">{{ a.name }}</div>
                <div class="text-caption text-grey-7">{{ a.location }}</div>
                <q-icon :name="a.longOffline ? 'wifi_off' : a.isOnline || a.kind === 'MANUAL' ? 'wifi' : 'portable_wifi_off'" :color="a.longOffline ? 'negative' : a.isOnline || a.kind === 'MANUAL' ? 'positive' : 'grey-6'" size="14px">
                  <q-tooltip>{{ a.longOffline ? 'Без зв’язку — тести переназначаються' : a.isOnline ? 'На зв’язку' : 'Немає даних про зв’язок' }} · черга {{ a.queue }}</q-tooltip>
                </q-icon>
                <q-badge v-if="a.lockouts.length" color="negative" icon="lock" class="q-ml-xs"><q-tooltip>Блокування за ВКЯ: {{ a.lockouts.join(', ') }}</q-tooltip></q-badge>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in matrix.rows" :key="r.mapId" class="cursor-pointer" @click="openCard(r.serviceCode)">
              <td class="sticky-col">
                <div class="text-weight-bold">{{ r.serviceName }}</div>
                <div class="text-caption text-grey-7">{{ r.serviceCode }} · {{ r.profileCode || 'набір показників' }} · {{ r.modeLabel }} · {{ r.indicators }} пок.</div>
              </td>
              <td class="text-center"><q-badge :color="readinessColor(r.readiness)" :label="r.readinessLabel" /></td>
              <td v-for="a in matrix.analyzers" :key="a.id" class="text-center">
                <template v-if="r.cells[a.id]">
                  <q-badge :color="cellColor(r.cells[a.id])" :label="cellLabel(r.cells[a.id])">
                    <q-tooltip>{{ r.cells[a.id].covered }} з {{ r.cells[a.id].total }} показників<span v-if="r.cells[a.id].unavailable.length"> · блокування за ВКЯ: {{ r.cells[a.id].unavailable.join(', ') }}</span><span v-if="!r.cells[a.id].enabled"> · вимкнено</span></q-tooltip>
                  </q-badge>
                </template>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div v-show="tab === 'interventions'" class="medlink-card q-pa-sm">
      <div class="row q-gutter-sm items-center q-mb-sm">
        <q-input v-model="ivSearch" dense outlined clearable label="Пошук (код / назва)" style="min-width: 260px" @keyup.enter="loadInterventions" />
        <q-select v-model="ivStatus" dense outlined clearable emit-value map-options label="Готовність" style="min-width: 200px" :options="readinessOptions" @input="loadInterventions" />
        <q-space />
        <span v-for="(n, k) in interventions.summary || {}" :key="k"><q-badge :color="readinessColor(k)" :label="`${readinessText(k)}: ${n}`" /></span>
        <q-btn outline dense color="primary" icon="sync" label="Оновити довідник інтервенцій з ЕСОЗ" @click="confirmSync"><q-tooltip>Завантажити з ЕСОЗ актуальний перелік послуг (інтервенцій)</q-tooltip></q-btn>
      </div>
      <q-table :data="interventions.items || []" :columns="ivColumns" row-key="id" dense flat :pagination="{ rowsPerPage: 30 }">
        <template v-slot:body-cell-readiness="props">
          <q-td :props="props"><q-badge :color="readinessColor(props.row.readiness)" :label="props.row.readinessLabel" /></q-td>
        </template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn v-if="props.row.mapId && !props.row.isPathology" flat dense size="sm" color="primary" icon="open_in_new" label="Картка" @click="openCard(props.row.code)" />
          </q-td>
        </template>
      </q-table>
    </div>

    <div v-show="tab === 'packages'" class="row q-col-gutter-md">
      <div v-for="p in packages" :key="p.id" class="col-12 col-md-6 col-lg-4">
        <q-card flat bordered>
          <q-card-section>
            <div class="text-weight-bold">{{ p.name }} <q-badge v-if="p.isCito" color="deep-orange-6" label="CITO" /></div>
            <div class="text-caption text-grey-7">{{ p.code }} · {{ p.context === 'INPATIENT' ? 'стаціонар' : p.context === 'AMBULATORY' ? 'амбулаторія' : 'будь-який контур' }}<span v-if="p.purpose"> · {{ p.purpose }}</span></div>
          </q-card-section>
          <q-separator />
          <q-list dense>
            <q-item v-for="(i, n) in p.items" :key="n">
              <q-item-section>
                <q-item-label>{{ i.serviceCode }}</q-item-label>
                <q-item-label caption>{{ (i.indicators || []).join(', ') || 'типовий склад' }}</q-item-label>
              </q-item-section>
            </q-item>
          </q-list>
        </q-card>
      </div>
    </div>

    <!-- Картка послуги -->
    <q-dialog v-model="cardOpen" maximized-on-mobile>
      <q-card v-if="card" style="width: 1100px; max-width: 96vw" data-testid="serviceCard">
        <q-card-section class="row items-center">
          <div class="col">
            <div class="text-subtitle1 text-weight-bold">{{ card.serviceName || card.profileName }}</div>
            <div class="text-caption text-grey-7">{{ card.serviceCode }} · профіль {{ card.profileCode }} · <q-badge :color="readinessColor(card.readiness)" :label="readinessText(card.readiness)" /></div>
          </div>
          <q-select v-if="card.mapId" v-model="cardMode" dense outlined emit-value map-options label="Режим вибору показників" style="min-width: 260px" :options="modeOptions" />
        </q-card-section>
        <q-separator />
        <q-card-section class="q-pa-sm">
          <div class="text-weight-bold q-mb-xs">Прилади послуги</div>
          <q-markup-table dense flat bordered>
            <thead><tr><th class="text-left">Прилад</th><th>У послузі</th><th>Пріоритет</th><th>Час виконання, хв</th><th>Черга</th><th class="text-left">Стан</th></tr></thead>
            <tbody>
              <tr v-for="a in cardAnalyzers" :key="a.id">
                <td><b>{{ a.name }}</b>&nbsp;<span class="text-caption text-grey-7">{{ a.location }}</span></td>
                <td class="text-center"><q-toggle v-model="a.enabled" dense /></td>
                <td class="text-center" style="width: 90px"><q-input v-model.number="a.priority" type="number" dense outlined :disable="!a.enabled" /></td>
                <td class="text-center">{{ a.avgTatMinutes }}</td>
                <td class="text-center">{{ a.queue }}</td>
                <td><q-badge :color="a.longOffline ? 'negative' : a.serviceStatus === 'MAINTENANCE' ? 'orange-7' : 'positive'" :label="a.longOffline ? 'без зв’язку' : a.serviceStatus === 'MAINTENANCE' ? 'ТО' : 'працює'" /></td>
              </tr>
            </tbody>
          </q-markup-table>
          <div class="text-weight-bold q-mt-md q-mb-xs">Показники × прилади</div>
          <div class="matrix-scroll">
            <q-markup-table dense flat bordered>
              <thead><tr><th class="text-left">Показник</th><th>Обов.</th><th>Типовий</th><th v-for="a in card.analyzers" :key="a.id">{{ a.name }}</th></tr></thead>
              <tbody>
                <tr v-for="i in cardIndicators" :key="i.code">
                  <td><b>{{ i.code }}</b>&nbsp;<span class="text-grey-7">{{ i.name }}</span></td>
                  <td class="text-center"><q-checkbox v-model="i.isRequired" dense /></td>
                  <td class="text-center"><q-checkbox v-model="i.isDefault" dense :disable="i.isRequired" /></td>
                  <td v-for="a in card.analyzers" :key="a.id" class="text-center">
                    <q-icon v-if="i.analyzers[a.id].performs" :name="i.analyzers[a.id].locked ? 'lock' : 'check_circle'" :color="i.analyzers[a.id].locked ? 'negative' : 'positive'">
                      <q-tooltip>Код на приладі: {{ i.analyzers[a.id].analyzerCode }}</q-tooltip>
                    </q-icon>
                    <span v-else class="text-grey-5">—</span>
                  </td>
                </tr>
              </tbody>
            </q-markup-table>
          </div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Закрити" v-close-popup />
          <q-btn unelevated color="primary" icon="save" label="Зберегти" data-testid="saveServiceCard" @click="saveCard" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';

const READINESS = { READY: ['Готово', 'positive'], PARTIAL: ['Частково', 'orange-7'], NO_EQUIPMENT: ['Немає обладнання', 'negative'], UNMAPPED: ['Не співставлено', 'grey-6'] };

export default {
  name: 'AnalyzerServicesPage',
  mixins: [apiMixin],
  data () {
    return {
      tab: 'matrix', matrix: { analyzers: [], rows: [] }, interventions: {}, packages: [], ivSearch: '', ivStatus: null,
      cardOpen: false, card: null, cardKey: null, cardMode: null, cardAnalyzers: [], cardIndicators: [],
      readinessOptions: Object.keys(READINESS).map(k => ({ value: k, label: READINESS[k][0] })),
      modeOptions: [{ value: 'FIXED', label: 'Фіксований склад' }, { value: 'SELECTABLE', label: 'За вибором (обов’язкові + на вибір)' }, { value: 'SELECTABLE_ANY', label: 'За вибором (хоча б один)' }],
      ivColumns: [
        { name: 'code', label: 'Код', field: 'code', align: 'left', sortable: true },
        { name: 'name', label: 'Інтервенція ЕСОЗ', field: 'name', align: 'left' },
        { name: 'group', label: 'Група', field: r => r.isPathology ? 'патоморфологія' : r.serviceGroup || '', align: 'left' },
        { name: 'profile', label: 'Послуга ЛІС', field: r => r.profileName || (r.mapId ? 'набір показників' : '—'), align: 'left' },
        { name: 'mode', label: 'Режим', field: r => ({ FIXED: 'Усі обов’язкові', SELECTABLE: 'За вибором', SELECTABLE_ANY: 'За вибором (хоча б один)' }[r.mode] || r.mode || ''), align: 'left' },
        { name: 'readiness', label: 'Готовність', field: 'readiness', align: 'left' },
        { name: 'synced', label: 'Синхронізовано', field: r => this.$options.filters.datetime(r.syncedOn), align: 'left' },
        { name: 'actions', label: '', field: 'id' }
      ]
    };
  },
  created () {
    this.load();
    if (this.$route.query.service) this.openCard(this.$route.query.service);
  },
  methods: {
    readinessText (s) { return (READINESS[s] || [s])[0]; },
    readinessColor (s) { return (READINESS[s] || [s, 'grey-6'])[1]; },
    cellLabel (c) { return c.separate ? 'окр.' : c.partial ? '½' : String(c.priority); },
    cellColor (c) { return !c.enabled ? 'grey-4' : c.unavailable.length ? 'negative' : c.separate ? 'grey-6' : c.partial ? 'orange-7' : 'primary'; },
    async load () {
      await this.callApi(async () => {
        const [m, p] = await Promise.all([this.$api.serviceMatrix(), this.$api.misPackages()]);
        this.matrix = m; this.packages = p;
      });
      this.loadInterventions();
    },
    async loadInterventions () {
      const r = await this.callApi(() => this.$api.interventions({ search: this.ivSearch, status: this.ivStatus }), { silent: true });
      if (r) this.interventions = r;
    },
    confirmSync () {
      this.$q.dialog({ title: 'Оновити довідник інтервенцій?', message: 'З ЕСОЗ буде завантажено актуальний перелік послуг (інтервенцій). Наявні співставлення з послугами ЛІС збережуться.', cancel: 'Скасувати', ok: 'Оновити' })
        .onOk(() => this.sync());
    },
    async sync () {
      const r = await this.callApi(() => this.$api.syncInterventions());
      if (r) { this.$q.notify({ type: 'positive', message: `Довідник оновлено: ${r.synced} записів` }); this.loadInterventions(); }
    },
    async openCard (key) {
      const c = await this.callApi(() => this.$api.serviceCard(key));
      if (!c) return;
      this.cardKey = key; this.card = c; this.cardMode = c.mode;
      this.cardAnalyzers = c.analyzers.map(a => ({ ...a, enabled: a.inService && a.enabled, priority: a.priority || 1 }));
      this.cardIndicators = c.indicators.map(i => ({ ...i }));
      this.cardOpen = true;
    },
    async saveCard () {
      const body = {
        analyzers: this.cardAnalyzers.filter(a => a.inService || a.enabled).map(a => ({ analyzerId: a.id, profileId: this.card.profileId, isEnabled: a.enabled, priority: Number(a.priority) || 1 })),
        indicators: this.cardIndicators.map(i => ({ testCode: i.code, isRequired: i.isRequired, isDefault: i.isDefault || i.isRequired })),
        mode: this.cardMode
      };
      const dry = await this.callApi(() => this.$api.saveServiceCard(this.cardKey, { ...body, dryRun: true }));
      if (!dry) return;
      const apply = async () => {
        const r = await this.callApi(() => this.$api.saveServiceCard(this.cardKey, body), { success: 'Картку послуги збережено' });
        if (r) { this.cardOpen = false; this.load(); }
      };
      if (dry.affected > 0) {
        this.$q.dialog({ title: 'Переназначення тестів', message: `Зміна зачепить тестів у черзі: ${dry.affected}. Їх буде переназначено на інші прилади. Продовжити?`, cancel: true }).onOk(apply);
      } else await apply();
    }
  }
};
</script>

<style scoped>
.matrix-scroll { overflow-x: auto; }
.svc-matrix { border-collapse: collapse; font-size: 12.5px; min-width: 100%; }
.svc-matrix th, .svc-matrix td { border: 1px solid #e0e0e0; padding: 4px 6px; }
.svc-matrix tbody tr:hover { background: #f5f9ff; }
.an-head { min-width: 92px; vertical-align: bottom; }
.an-name { font-weight: 600; white-space: nowrap; }
.sticky-col { position: sticky; left: 0; background: #fff; min-width: 260px; z-index: 1; }
</style>
