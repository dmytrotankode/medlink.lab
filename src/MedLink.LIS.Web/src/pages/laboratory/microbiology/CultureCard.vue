<template>
  <div class="culture-card" data-testid="cultureCard">
    <page-header :title="culture ? `Посів · замовлення ${culture.orderNumber || '—'}` : 'Картка посіву'" icon="fas fa-bacterium" :subtitle="culture ? subtitle : ''" :breadcrumbs="[{ label: 'Мікробіологія', to: { name: 'lab-microbiology' } }, { label: 'Посів' }]">
      <template v-slot:title-after><status-chip v-if="culture" :value="culture.status" type="culture" icon /></template>
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn outline dense color="primary" icon="description" label="Протокол" :disable="!culture" @click="openReport" />
      <span class="inline-block">
        <q-btn outline dense color="primary" icon="edit" label="Редагувати" :disable="!can('EDIT')" @click="editOpen = true" />
        <q-tooltip v-if="!can('EDIT')">{{ actionHint('EDIT') }}</q-tooltip>
      </span>
      <span class="inline-block">
        <q-btn unelevated dense color="purple-6" icon="add" label="Додати ізолят" :disable="!can('ADD_ISOLATE')" data-testid="isolateAdd" @click="openIsolate(null)" />
        <q-tooltip v-if="!can('ADD_ISOLATE')">{{ actionHint('ADD_ISOLATE') }}</q-tooltip>
      </span>
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div v-if="culture" class="row q-col-gutter-md">
      <div class="col-12 col-md-4">
        <div class="medlink-card q-pa-md">
          <div class="section-title">Посів</div>
          <q-markup-table dense flat>
            <tbody>
              <tr><td class="text-grey-7">Пацієнт</td><td><b>{{ culture.patientName || '—' }}</b></td></tr>
              <tr><td class="text-grey-7">Замовлення</td><td>{{ culture.orderNumber || '—' }}</td></tr>
              <tr><td class="text-grey-7">Локалізація / біоматеріал</td><td>{{ culture.specimenLocus || '—' }}</td></tr>
              <tr><td class="text-grey-7">Живильне середовище</td><td>{{ culture.cultureMedium || '—' }}</td></tr>
              <tr><td class="text-grey-7">Інкубація з</td><td>{{ culture.incubationStart | datetime }}</td></tr>
              <tr><td class="text-grey-7">Ріст</td><td><q-badge :color="hasGrowth ? 'deep-orange-6' : 'positive'" :label="hasGrowth ? 'Є ріст' : 'Росту немає'" /> {{ growthText }}</td></tr>
              <tr v-if="culture.preliminaryReport"><td class="text-grey-7">Попередній висновок</td><td>{{ culture.preliminaryReport }}</td></tr>
            </tbody>
          </q-markup-table>
          <div class="section-title q-mt-md">Статус</div>
          <div class="row q-gutter-xs">
            <span v-for="s in statusFlow" :key="s" class="inline-block">
              <q-btn dense size="sm" :unelevated="culture.status === s" :outline="culture.status !== s" :color="statusColor(s)" :label="statusLabel(s)" no-caps :disable="culture.status !== s && !transitionFor(s)" @click="setStatus(s)" />
              <q-tooltip v-if="culture.status !== s && !transitionFor(s)">{{ transitionHint(s) }}</q-tooltip>
            </span>
          </div>
        </div>
      </div>

      <div class="col-12 col-md-8">
        <div v-for="iso in isolates" :key="iso.id" class="medlink-card q-mb-md" :data-testid="`isolate-${iso.id}`">
          <div class="medlink-card__title">
            <span><q-icon name="coronavirus" color="purple-6" class="q-mr-xs" /><b>{{ iso.organismName || iso.organism || organismName(iso.organismId) }}</b>
              <span class="text-grey-7 q-ml-sm">{{ isolateCount(iso) }}</span>
              <q-badge v-for="ph in (iso.phenotypes || iso.resistancePhenotypes || [])" :key="ph" color="negative" :label="ph" class="q-ml-xs"><q-tooltip>{{ phenotypeHint(ph) }}</q-tooltip></q-badge>
            </span>
            <span>
              <q-btn flat dense round size="sm" icon="add" color="primary" aria-label="Додати антибіотик" :disable="!can('ADD_SUSCEPTIBILITY')" @click="openSusc(iso)"><q-tooltip>{{ can('ADD_SUSCEPTIBILITY') ? 'Додати антибіотик' : actionHint('ADD_SUSCEPTIBILITY') }}</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="edit" color="primary" aria-label="Змінити ізолят" :disable="!can('ADD_ISOLATE')" @click="openIsolate(iso)"><q-tooltip>{{ can('ADD_ISOLATE') ? 'Змінити ізолят' : actionHint('ADD_ISOLATE') }}</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Видалити ізолят" :disable="!can('ADD_ISOLATE')" @click="askDeleteIsolate(iso)"><q-tooltip>{{ can('ADD_ISOLATE') ? 'Видалити ізолят' : actionHint('ADD_ISOLATE') }}</q-tooltip></q-btn>
            </span>
          </div>
          <q-table :data="suscList(iso)" :columns="suscColumns" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" row-key="id" no-data-label="Антибіотикограма ще не внесена">
            <template v-slot:body-cell-antibiotic="props"><q-td :props="props">{{ props.row.antibioticName || antibioticName(props.row.antibioticId) }}</q-td></template>
            <template v-slot:body-cell-method="props"><q-td :props="props">{{ isMic(props.row) ? 'МІК' : 'Диск-дифузія' }}</q-td></template>
            <template v-slot:body-cell-value="props"><q-td :props="props" class="text-right">{{ suscValue(props.row) }}</q-td></template>
            <template v-slot:body-cell-sir="props">
              <q-td :props="props" class="text-center">
                <span class="sir-chip" :class="`sir-${sirOf(props.row)}`" :data-testid="`sir-${props.row.antibioticId}`">{{ sirOf(props.row) || '?' }}<q-tooltip>{{ sirHint(sirOf(props.row)) }}</q-tooltip></span>
              </q-td>
            </template>
            <template v-slot:body-cell-actions="props"><q-td :props="props" class="text-right"><q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Видалити запис" :disable="!can('ADD_SUSCEPTIBILITY')" @click="deleteSusc(props.row)"><q-tooltip>{{ can('ADD_SUSCEPTIBILITY') ? 'Видалити запис' : actionHint('ADD_SUSCEPTIBILITY') }}</q-tooltip></q-btn></q-td></template>
          </q-table>
        </div>
        <div v-if="!isolates.length" class="medlink-card"><empty-state title="Ізолятів ще немає" icon="coronavirus" hint="Додайте ідентифікований мікроорганізм та внесіть антибіотикограму" /></div>
      </div>
    </div>
    <div v-else-if="loading" class="text-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <!-- Ізолят -->
    <q-dialog v-model="isolateOpen" no-backdrop-dismiss>
      <q-card style="min-width: 520px" data-testid="isolateDialog">
        <q-card-section class="row items-center bg-purple-6 text-white q-py-sm"><q-icon name="coronavirus" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ isolateForm.id ? 'Ізолят' : 'Новий ізолят' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-select v-model="isolateForm.organismId" outlined dense label="Мікроорганізм *" :options="organismOptions" emit-value map-options use-input input-debounce="0" @filter="filterOrganisms" data-testid="organismSelect" />
          <q-input v-model="isolateForm.quantitativeCount" outlined dense label="Кількість (КУО/мл)" />
          <q-input v-model="isolateForm.colonyMorphology" outlined dense label="Морфологія колоній / коментар" />
          <div v-if="isolateError" class="text-negative">{{ isolateError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="purple-6" icon="save" label="Зберегти" :loading="saving" :disable="!isolateForm.organismId" data-testid="isolateSave" @click="saveIsolate"><q-tooltip v-if="!isolateForm.organismId">Оберіть мікроорганізм</q-tooltip></q-btn></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Чутливість -->
    <q-dialog v-model="suscOpen" no-backdrop-dismiss>
      <q-card style="min-width: 520px" data-testid="suscDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="science" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">Антибіотикограма — {{ activeIsolate && (activeIsolate.organismName || organismName(activeIsolate.organismId)) }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-select v-model="suscForm.antibioticId" outlined dense label="Антибіотик *" :options="antibioticOptions" emit-value map-options use-input input-debounce="0" @filter="filterAntibiotics" data-testid="antibioticSelect" :error="!!suscError && !suscForm.antibioticId" hide-bottom-space />
          <q-btn-toggle v-model="suscForm.method" data-testid="suscMethodToggle" spread no-caps toggle-color="primary" :options="[{ value: 'DISK', label: 'Диск-дифузія (зона, мм)' }, { value: 'MIC', label: 'МІК (мг/л)' }]" />
          <q-input v-if="suscForm.method === 'DISK'" v-model="suscForm.zoneMm" outlined dense label="Зона затримки росту, мм *" inputmode="decimal" autofocus data-testid="zoneInput" @keyup.enter="saveSusc" />
          <q-input v-else v-model="suscForm.mic" outlined dense label="МІК, мг/л *" inputmode="decimal" autofocus data-testid="micInput" @keyup.enter="saveSusc" />
          <template v-if="needManual">
            <div class="text-caption text-orange-9">Для цього антибіотика й мікроорганізму немає порогових значень — оберіть інтерпретацію вручну та вкажіть обґрунтування.</div>
            <q-btn-toggle v-model="suscForm.override" spread no-caps dense toggle-color="orange-8" :options="[{ value: 'S', label: 'S — чутливий' }, { value: 'I', label: 'I — проміжний' }, { value: 'R', label: 'R — стійкий' }]" />
            <q-input v-model="suscForm.overrideReason" outlined dense label="Обґрунтування ручної інтерпретації *" />
          </template>
          <div class="text-caption text-grey-6">Інтерпретацію (чутливий / проміжний / стійкий) визначає система за таблицями EUCAST; фенотипи резистентності (MRSA, ESBL, CRE, VRE) визначаються автоматично.</div>
          <div v-if="suscError" class="text-negative">{{ suscError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Інтерпретувати та зберегти" :loading="saving" :disable="!suscReady" data-testid="suscSave" @click="saveSusc"><q-tooltip v-if="!suscReady">{{ needManual ? 'Оберіть інтерпретацію й вкажіть обґрунтування' : 'Оберіть антибіотик і введіть значення' }}</q-tooltip></q-btn></q-card-actions>
      </q-card>
    </q-dialog>

    <culture-dialog v-model="editOpen" :culture="culture" @saved="load" />
    <html-preview-dialog v-model="reportOpen" :title="`Протокол бактеріологічного дослідження`" :html="reportHtml" :url="culture ? $api.cultureReportUrl(culture.id) : ''" :loading="reportLoading" :error="reportError" @retry="openReport" />
    <confirm-dialog v-model="deleteIsolateOpen" title="Видалити ізолят?" message="Ізолят разом із антибіотикограмою буде видалено." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteIsolate" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';
import CultureDialog from './CultureDialog.vue';
import HtmlPreviewDialog from '../../../components/common/HtmlPreviewDialog.vue';
import { CULTURE_STATUS } from '../../../utils/statuses';
import { formatNumber, parseDecimal } from '../../../utils/format';

const PHENOTYPES = {
  MRSA: 'MRSA — метицилін-резистентний золотистий стафілокок',
  ESBL: 'ESBL — β-лактамази розширеного спектра',
  CRE: 'CRE — карбапенем-резистентні ентеробактерії',
  VRE: 'VRE — ванкоміцин-резистентні ентерококи'
};

export default {
  name: 'CultureCard',
  mixins: [apiMixin, roleMixin],
  components: { CultureDialog, HtmlPreviewDialog },
  props: { id: { type: String, required: true } },
  data () {
    return {
      culture: null,
      isolateOpen: false, suscOpen: false, editOpen: false, reportOpen: false, deleteIsolateOpen: false,
      isolateForm: {}, isolateError: '', suscForm: {}, suscError: '', needManual: false, saving: false, activeIsolate: null,
      organismFilter: '', antibioticFilter: '',
      reportHtml: '', reportLoading: false, reportError: '',
      // Послідовність статусів посіву (відповідає серверній машині станів)
      statusFlow: ['INCUBATING', 'PRELIMINARY', 'ISOLATED', 'COMPLETED'],
      suscColumns: [
        { name: 'antibiotic', label: 'Антибіотик', align: 'left' },
        { name: 'method', label: 'Метод', align: 'left' },
        { name: 'value', label: 'Значення', align: 'right' },
        { name: 'sir', label: 'Чутливість (S / I / R)', align: 'center' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    // Відображення політики сервера: ізоляти, антибіотикограма, редагування — адміністратор, лікар-лаборант, лаборант
    canWork () { return this.canDo('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    workHint () { return this.roleHint('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    isolates () { return (this.culture && this.culture.isolates) || []; },
    hasGrowth () { const c = this.culture; return !!(c && (c.growthDetected === true || c.hasGrowth === true || this.isolates.length)); },
    growthText () { const c = this.culture; if (!c) return ''; const names = { 'NO_GROWTH': 'росту немає', 'SCANTY_1+': 'скудний (1+)', 'MODERATE_2+': 'помірний (2+)', 'HEAVY_3+': 'рясний (3+)', 'PROFUSE_4+': 'дуже рясний (4+)' }; const it = c.growthIntensity ? (names[c.growthIntensity] || String(c.growthIntensity).replace(/_/g, ' ')) : ''; return [it, c.cfuPerMl].filter(Boolean).join(' · '); },
    subtitle () { const c = this.culture; return [c.specimenLocus, c.patientName].filter(Boolean).join(' · '); },
    organisms () { return this.$store.getters['dictionaries/items']('organisms'); },
    antibiotics () { return this.$store.getters['dictionaries/items']('antibiotics'); },
    organismOptions () { const f = this.organismFilter.toLowerCase(); return this.organisms.map(o => ({ value: o.id, label: this.orgLabel(o) })).filter(o => !f || o.label.toLowerCase().includes(f)); },
    antibioticOptions () { const f = this.antibioticFilter.toLowerCase(); return this.antibiotics.filter(a => !f || `${a.name} ${a.code || ''}`.toLowerCase().includes(f)).map(a => ({ value: a.id, label: a.name })); },
    suscReady () { return !!this.suscForm.antibioticId && !!(this.suscForm.method === 'DISK' ? this.suscForm.zoneMm : this.suscForm.mic) && (!this.needManual || (!!this.suscForm.override && !!(this.suscForm.overrideReason || '').trim())); }
  },
  created () { this.$store.dispatch('dictionaries/loadMany', ['organisms', 'antibiotics']); this.load(); },
  watch: {
    // зміна антибіотика скидає ручний режим і застарілі повідомлення: пороги визначаються для кожного антибіотика окремо
    'suscForm.antibioticId' () { this.needManual = false; this.suscError = ''; if (this.suscForm) { this.suscForm.override = null; this.suscForm.overrideReason = ''; } }
  },
  methods: {
    formatNumber,
    statusLabel (s) { return (CULTURE_STATUS[s] || {}).label || s; },
    statusColor (s) { return (CULTURE_STATUS[s] || {}).color || 'grey'; },
    orgLabel (o) { return o.commonName || o.name || o.latinName || o.code; },
    organismName (id) { const o = this.organisms.find(x => String(x.id) === String(id)); return o ? this.orgLabel(o) : '—'; },
    antibioticName (id) { const a = this.antibiotics.find(x => String(x.id) === String(id)); return a ? a.name : '—'; },
    phenotypeHint (ph) { return PHENOTYPES[ph] || ph; },
    isolateCount (iso) { const q = iso.quantitativeCount || iso.cfuPerMl; return q ? (/куо/i.test(String(q)) ? q : `${q} КУО/мл`) : ''; },
    // Антибіотикограма: сервер повертає susceptibilities / zoneDiameterMm / micValueMgL / interpretation
    suscList (iso) { return iso.susceptibilities || iso.susceptibility || iso.susceptibilityResults || []; },
    isMic (r) { return /MIC/i.test(r.method || ''); },
    suscValue (r) {
      if (this.isMic(r)) { const v = r.micValueMgL !== undefined ? r.micValueMgL : r.mic; return v === null || v === undefined ? '—' : `${formatNumber(v)} мг/л`; }
      const z = r.zoneDiameterMm !== undefined ? r.zoneDiameterMm : r.zoneMm; return z === null || z === undefined ? '—' : `${formatNumber(z)} мм`;
    },
    sirOf (r) { return r.interpretation || r.sir || ''; },
    sirHint (s) { return { S: 'Чутливий', I: 'Чутливий при підвищеній експозиції', R: 'Стійкий' }[s] || 'Інтерпретацію не визначено'; },
    /** Дозволений серверною машиною станів перехід у вказаний статус (залежить від поточного статусу й ролі) */
    transitionFor (s) { return ((this.culture && this.culture.allowedActions) || []).find(a => a.toStatus === s) || null; },
    transitionHint (s) {
      if (!this.canWork) return this.workHint;
      if (s === 'COMPLETED' && !this.canDo('LAB_DOCTOR')) return this.roleHint('LAB_DOCTOR');
      return 'Перехід у цей статус недоступний із поточного статусу посіву';
    },
    /** Чи дозволена дія серверною машиною станів (враховує і статус посіву, і роль) */
    can (action) { return !!this.culture && ((this.culture.allowedActions || []).some(a => a.action === action)); },
    actionHint (action) {
      if (!this.canWork) return this.workHint;
      if (action === 'ADD_SUSCEPTIBILITY') return 'Антибіотикограму можна вносити після попереднього результату';
      return 'Посів завершено — зміни недоступні';
    },
    async load () { const res = await this.callApi(() => this.$api.culture(this.id)); if (res) this.culture = res; },
    async setStatus (s) {
      if (this.culture.status === s) return;
      const act = this.transitionFor(s);
      if (!act) { this.$q.notify({ type: 'warning', message: this.transitionHint(s) }); return; }
      try { await this.$api.transitionCulture(this.culture.id, act.action); this.notifyOk(`Статус посіву: ${this.statusLabel(s)}`); this.load(); } catch (e) { this.notifyError(e); }
    },
    filterOrganisms (val, update) { update(() => { this.organismFilter = val || ''; }); },
    filterAntibiotics (val, update) { update(() => { this.antibioticFilter = val || ''; }); },
    openIsolate (iso) {
      this.activeIsolate = iso;
      this.isolateForm = iso ? { id: iso.id, organismId: iso.organismId, quantitativeCount: iso.quantitativeCount || '', colonyMorphology: iso.colonyMorphology || '' } : { id: null, organismId: null, quantitativeCount: '', colonyMorphology: '' };
      this.isolateError = ''; this.isolateOpen = true;
    },
    async saveIsolate () {
      this.saving = true; this.isolateError = '';
      const body = { organismId: this.isolateForm.organismId, quantitativeCount: this.isolateForm.quantitativeCount || null, colonyMorphology: this.isolateForm.colonyMorphology || null };
      try {
        if (this.isolateForm.id) await this.$api.updateIsolate(this.isolateForm.id, body); else await this.$api.addIsolate(this.culture.id, body);
        this.notifyOk('Ізолят збережено'); this.isolateOpen = false; this.load();
      } catch (e) { this.isolateError = e.userMessage || 'Не вдалося зберегти ізолят'; } finally { this.saving = false; }
    },
    askDeleteIsolate (iso) { this.activeIsolate = iso; this.deleteIsolateOpen = true; },
    async doDeleteIsolate () { try { await this.$api.deleteIsolate(this.activeIsolate.id); this.notifyOk('Ізолят видалено'); this.load(); } catch (e) { this.notifyError(e); } },
    openSusc (iso) { this.activeIsolate = iso; this.suscForm = { antibioticId: null, method: 'DISK', zoneMm: '', mic: '', override: null, overrideReason: '' }; this.suscError = ''; this.needManual = false; this.suscOpen = true; },
    async saveSusc () {
      // Enter у полі значення обходить неактивну кнопку, тому перевіряємо заповнення тут
      if (!this.suscForm.antibioticId) { this.suscError = 'Оберіть антибіотик'; return; }
      if (!(this.suscForm.method === 'DISK' ? this.suscForm.zoneMm : this.suscForm.mic)) { this.suscError = this.suscForm.method === 'DISK' ? 'Введіть зону затримки росту, мм' : 'Введіть значення МІК, мг/л'; return; }
      if (this.needManual && !this.suscForm.override) { this.suscError = 'Для цього антибіотика немає порогових значень: оберіть інтерпретацію S, I або R'; return; }
      if (this.needManual && !(this.suscForm.overrideReason || '').trim()) { this.suscError = 'Вкажіть обґрунтування ручної інтерпретації'; return; }
      this.saving = true; this.suscError = '';
      const body = { antibioticId: this.suscForm.antibioticId, method: this.suscForm.method === 'MIC' ? 'MIC' : 'DISK_DIFFUSION', zoneMm: this.suscForm.method === 'DISK' ? parseDecimal(this.suscForm.zoneMm) : null, mic: this.suscForm.method === 'MIC' ? parseDecimal(this.suscForm.mic) : null };
      if (this.needManual) { body.overrideInterpretation = this.suscForm.override; body.overrideReason = this.suscForm.overrideReason; }
      try {
        const res = await this.$api.addSusceptibility(this.activeIsolate.id, body);
        const sir = res && (res.interpretation || res.sir);
        this.$q.notify({ type: sir === 'R' ? 'warning' : 'positive', message: `${this.antibioticName(body.antibioticId)}: ${sir ? this.sirHint(sir).toLowerCase() + ' (' + sir + ')' : 'збережено'}${res && (res.phenotypes || []).length ? ' · фенотип ' + res.phenotypes.join(', ') : ''}` });
        this.suscForm = { ...this.suscForm, antibioticId: null, zoneMm: '', mic: '', override: null, overrideReason: '' }; this.needManual = false;
        this.load();
      } catch (e) { this.suscError = e.userMessage || 'Не вдалося зберегти антибіотикограму'; if (/вручну/.test(this.suscError)) this.needManual = true; } finally { this.saving = false; }
    },
    async deleteSusc (row) { try { await this.$api.deleteSusceptibility(row.id); this.notifyOk('Запис видалено'); this.load(); } catch (e) { this.notifyError(e); } },
    async openReport () {
      this.reportOpen = true; this.reportLoading = true; this.reportError = ''; this.reportHtml = '';
      try { this.reportHtml = await this.$api.cultureReport(this.culture.id); } catch (e) { this.reportError = e.userMessage || 'Не вдалося отримати протокол'; } finally { this.reportLoading = false; }
    }
  }
};
</script>
