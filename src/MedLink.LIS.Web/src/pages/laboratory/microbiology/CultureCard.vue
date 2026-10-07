<template>
  <div class="culture-card" data-testid="cultureCard">
    <page-header :title="culture ? `Посів ${culture.cultureNumber || culture.barcode || shortId(culture.id)}` : 'Картка посіву'" icon="fas fa-bacterium" :subtitle="culture ? `${culture.specimenType || culture.specimen || ''} · ${culture.patientName || ''}` : ''" :breadcrumbs="[{ label: 'Мікробіологія', to: { name: 'lab-microbiology' } }, { label: 'Посів' }]">
      <template v-slot:title-after><status-chip v-if="culture" :value="culture.status" type="culture" icon /></template>
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn outline dense color="primary" icon="description" label="Протокол" :disable="!culture" @click="openReport" />
      <q-btn outline dense color="primary" icon="edit" label="Редагувати" :disable="!culture" @click="editOpen = true" />
      <q-btn unelevated dense color="purple-6" icon="add" label="Додати ізолят" :disable="!culture" data-testid="isolateAdd" @click="openIsolate(null)" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div v-if="culture" class="row q-col-gutter-md">
      <div class="col-12 col-md-4">
        <div class="medlink-card q-pa-md">
          <div class="section-title">Посів</div>
          <q-markup-table dense flat>
            <tbody>
              <tr><td class="text-grey-7">Пацієнт</td><td><b>{{ culture.patientName || (culture.patient && culture.patient.fullName) || '—' }}</b></td></tr>
              <tr><td class="text-grey-7">Проба</td><td class="mono">{{ culture.barcode || '—' }}</td></tr>
              <tr><td class="text-grey-7">Локус</td><td>{{ culture.specimenType || culture.specimen }}</td></tr>
              <tr><td class="text-grey-7">Інкубація з</td><td>{{ culture.incubationStartedAt | datetime }}</td></tr>
              <tr><td class="text-grey-7">Ріст</td><td><q-badge :color="culture.hasGrowth ? 'deep-orange-6' : 'positive'" :label="culture.hasGrowth ? 'є ріст' : 'росту немає'" /> {{ culture.growthDescription }}</td></tr>
              <tr v-if="culture.clinicalNotes"><td class="text-grey-7">Клініка</td><td>{{ culture.clinicalNotes }}</td></tr>
            </tbody>
          </q-markup-table>
          <div class="section-title q-mt-md">Статус</div>
          <div class="row q-gutter-xs">
            <q-btn v-for="s in statusFlow" :key="s" dense size="sm" :outline="culture.status !== s" :color="statusColor(s)" :label="statusLabel(s)" no-caps @click="setStatus(s)" />
          </div>
        </div>
      </div>

      <div class="col-12 col-md-8">
        <div v-for="iso in isolates" :key="iso.id" class="medlink-card q-mb-md" :data-testid="`isolate-${iso.id}`">
          <div class="medlink-card__title">
            <span><q-icon name="coronavirus" color="purple-6" class="q-mr-xs" /><b>{{ iso.organismName || iso.organism || organismName(iso.organismId) }}</b>
              <span class="text-grey-7 q-ml-sm">{{ iso.cfuPerMl ? `${iso.cfuPerMl} КУО/мл` : '' }} {{ iso.gramStain ? '· ' + iso.gramStain : '' }}</span>
              <q-badge v-for="ph in (iso.phenotypes || iso.resistancePhenotypes || [])" :key="ph" color="negative" :label="ph" class="q-ml-xs" />
            </span>
            <span>
              <q-btn flat dense round size="sm" icon="add" color="primary" @click="openSusc(iso)"><q-tooltip>Додати антибіотик</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="edit" color="primary" @click="openIsolate(iso)" />
              <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDeleteIsolate(iso)" />
            </span>
          </div>
          <q-table :data="iso.susceptibility || iso.susceptibilityResults || []" :columns="suscColumns" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" row-key="id" no-data-label="Антибіотикограма ще не внесена">
            <template v-slot:body-cell-antibiotic="props"><q-td :props="props">{{ props.row.antibioticName || antibioticName(props.row.antibioticId) }}</q-td></template>
            <template v-slot:body-cell-method="props"><q-td :props="props">{{ props.row.method === 'MIC' ? 'МІК' : 'Диск-дифузія' }}</q-td></template>
            <template v-slot:body-cell-value="props"><q-td :props="props" class="text-right">{{ props.row.method === 'MIC' ? (formatNumber(props.row.mic) + ' мг/л') : (formatNumber(props.row.zoneMm) + ' мм') }}</q-td></template>
            <template v-slot:body-cell-breakpoints="props"><q-td :props="props" class="text-caption text-grey-7">{{ breakpointText(props.row) }}</q-td></template>
            <template v-slot:body-cell-sir="props"><q-td :props="props" class="text-center"><span class="sir-chip" :class="`sir-${props.row.interpretation || props.row.sir}`" :data-testid="`sir-${props.row.antibioticId}`">{{ props.row.interpretation || props.row.sir || '?' }}</span></q-td></template>
            <template v-slot:body-cell-actions="props"><q-td :props="props" class="text-right"><q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="deleteSusc(props.row)" /></q-td></template>
          </q-table>
        </div>
        <div v-if="!isolates.length" class="medlink-card"><empty-state title="Ізолятів ще немає" icon="coronavirus" hint="Додайте ідентифікований мікроорганізм та внесіть антибіотикограму" /></div>
      </div>
    </div>
    <div v-else-if="loading" class="text-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <!-- Ізолят -->
    <q-dialog v-model="isolateOpen" persistent>
      <q-card style="min-width: 520px" data-testid="isolateDialog">
        <q-card-section class="row items-center bg-purple-6 text-white q-py-sm"><q-icon name="coronavirus" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ isolateForm.id ? 'Ізолят' : 'Новий ізолят' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-select v-model="isolateForm.organismId" outlined dense label="Мікроорганізм *" :options="organismOptions" emit-value map-options use-input input-debounce="0" @filter="filterOrganisms" data-testid="organismSelect" />
          <div class="row q-col-gutter-sm">
            <div class="col-6"><q-input v-model="isolateForm.cfuPerMl" outlined dense label="КУО/мл" /></div>
            <div class="col-6"><q-select v-model="isolateForm.gramStain" outlined dense clearable label="Грам" :options="['Грам+', 'Грам−', 'Гриби']" /></div>
          </div>
          <q-input v-model="isolateForm.comment" outlined dense label="Коментар" />
          <div v-if="isolateError" class="text-negative">{{ isolateError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="purple-6" icon="save" label="Зберегти" :loading="saving" :disable="!isolateForm.organismId" data-testid="isolateSave" @click="saveIsolate" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Чутливість -->
    <q-dialog v-model="suscOpen" persistent>
      <q-card style="min-width: 520px" data-testid="suscDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="science" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">Антибіотикограма — {{ activeIsolate && (activeIsolate.organismName || organismName(activeIsolate.organismId)) }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-select v-model="suscForm.antibioticId" outlined dense label="Антибіотик *" :options="antibioticOptions" emit-value map-options use-input input-debounce="0" @filter="filterAntibiotics" data-testid="antibioticSelect" />
          <q-btn-toggle v-model="suscForm.method" spread no-caps toggle-color="primary" :options="[{ value: 'DISK', label: 'Диск-дифузія (зона, мм)' }, { value: 'MIC', label: 'МІК (мг/л)' }]" />
          <q-input v-if="suscForm.method === 'DISK'" v-model="suscForm.zoneMm" outlined dense label="Зона затримки росту, мм *" inputmode="decimal" autofocus data-testid="zoneInput" @keyup.enter="saveSusc" />
          <q-input v-else v-model="suscForm.mic" outlined dense label="МІК, мг/л *" inputmode="decimal" autofocus data-testid="micInput" @keyup.enter="saveSusc" />
          <div class="text-caption text-grey-6">Інтерпретацію S / I / R визначає сервер за таблицями EUCAST; фенотипи (MRSA, ESBL, CRE, VRE) детектуються автоматично.</div>
          <div v-if="suscError" class="text-negative">{{ suscError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Інтерпретувати та зберегти" :loading="saving" :disable="!suscForm.antibioticId || !(suscForm.method === 'DISK' ? suscForm.zoneMm : suscForm.mic)" data-testid="suscSave" @click="saveSusc" /></q-card-actions>
      </q-card>
    </q-dialog>

    <culture-dialog v-model="editOpen" :culture="culture" @saved="load" />
    <html-preview-dialog v-model="reportOpen" :title="`Протокол бактеріологічного дослідження`" :html="reportHtml" :url="culture ? $api.cultureReportUrl(culture.id) : ''" :loading="reportLoading" :error="reportError" @retry="openReport" />
    <confirm-dialog v-model="deleteIsolateOpen" title="Видалити ізолят" message="Ізолят разом із антибіотикограмою буде видалено." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteIsolate" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import CultureDialog from './CultureDialog.vue';
import HtmlPreviewDialog from '../../../components/common/HtmlPreviewDialog.vue';
import { CULTURE_STATUS } from '../../../utils/statuses';
import { formatNumber, parseDecimal } from '../../../utils/format';

export default {
  name: 'CultureCard',
  mixins: [apiMixin],
  components: { CultureDialog, HtmlPreviewDialog },
  props: { id: { type: String, required: true } },
  data () {
    return {
      culture: null,
      isolateOpen: false, suscOpen: false, editOpen: false, reportOpen: false, deleteIsolateOpen: false,
      isolateForm: {}, isolateError: '', suscForm: {}, suscError: '', saving: false, activeIsolate: null,
      organismFilter: '', antibioticFilter: '',
      reportHtml: '', reportLoading: false, reportError: '',
      statusFlow: ['REGISTERED', 'INCUBATING', 'GROWTH', 'NO_GROWTH', 'IDENTIFIED', 'COMPLETED', 'RELEASED'],
      suscColumns: [
        { name: 'antibiotic', label: 'Антибіотик', align: 'left' },
        { name: 'method', label: 'Метод', align: 'left' },
        { name: 'value', label: 'Значення', align: 'right' },
        { name: 'breakpoints', label: 'Breakpoints EUCAST', align: 'left' },
        { name: 'sir', label: 'S/I/R', align: 'center' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    isolates () { return (this.culture && this.culture.isolates) || []; },
    organisms () { return this.$store.getters['dictionaries/items']('organisms'); },
    antibiotics () { return this.$store.getters['dictionaries/items']('antibiotics'); },
    organismOptions () { const f = this.organismFilter.toLowerCase(); return this.organisms.filter(o => !f || `${o.name} ${o.code || ''}`.toLowerCase().includes(f)).map(o => ({ value: o.id, label: o.name })); },
    antibioticOptions () { const f = this.antibioticFilter.toLowerCase(); return this.antibiotics.filter(a => !f || `${a.name} ${a.code || ''}`.toLowerCase().includes(f)).map(a => ({ value: a.id, label: `${a.name}${a.code ? ' (' + a.code + ')' : ''}` })); }
  },
  created () { this.$store.dispatch('dictionaries/loadMany', ['organisms', 'antibiotics']); this.load(); },
  methods: {
    formatNumber,
    shortId (id) { return String(id).slice(0, 8); },
    statusLabel (s) { return (CULTURE_STATUS[s] || {}).label || s; },
    statusColor (s) { return (CULTURE_STATUS[s] || {}).color || 'grey'; },
    organismName (id) { const o = this.organisms.find(x => String(x.id) === String(id)); return o ? o.name : (id || '—'); },
    antibioticName (id) { const a = this.antibiotics.find(x => String(x.id) === String(id)); return a ? a.name : (id || '—'); },
    breakpointText (r) {
      if (r.breakpointS !== undefined || r.breakpointR !== undefined) return r.method === 'MIC' ? `S ≤ ${formatNumber(r.breakpointS)} · R > ${formatNumber(r.breakpointR)}` : `S ≥ ${formatNumber(r.breakpointS)} · R < ${formatNumber(r.breakpointR)}`;
      return r.breakpointText || '';
    },
    async load () { const res = await this.callApi(() => this.$api.culture(this.id)); if (res) this.culture = res; },
    async setStatus (s) { try { await this.$api.updateCulture(this.culture.id, { ...this.culture, status: s, isolates: undefined }); this.notifyOk(`Статус: ${this.statusLabel(s)}`); this.load(); } catch (e) { this.notifyError(e); } },
    filterOrganisms (val, update) { update(() => { this.organismFilter = val || ''; }); },
    filterAntibiotics (val, update) { update(() => { this.antibioticFilter = val || ''; }); },
    openIsolate (iso) { this.activeIsolate = iso; this.isolateForm = iso ? { id: iso.id, organismId: iso.organismId, cfuPerMl: iso.cfuPerMl, gramStain: iso.gramStain, comment: iso.comment } : { id: null, organismId: null, cfuPerMl: '', gramStain: null, comment: '' }; this.isolateError = ''; this.isolateOpen = true; },
    async saveIsolate () {
      this.saving = true; this.isolateError = '';
      const body = { ...this.isolateForm }; delete body.id;
      try {
        if (this.isolateForm.id) await this.$api.updateIsolate(this.isolateForm.id, body); else await this.$api.addIsolate(this.culture.id, body);
        this.notifyOk('Ізолят збережено'); this.isolateOpen = false; this.load();
      } catch (e) { this.isolateError = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    },
    askDeleteIsolate (iso) { this.activeIsolate = iso; this.deleteIsolateOpen = true; },
    async doDeleteIsolate () { try { await this.$api.deleteIsolate(this.activeIsolate.id); this.notifyOk('Ізолят видалено'); this.load(); } catch (e) { this.notifyError(e); } },
    openSusc (iso) { this.activeIsolate = iso; this.suscForm = { antibioticId: null, method: 'DISK', zoneMm: '', mic: '' }; this.suscError = ''; this.suscOpen = true; },
    async saveSusc () {
      this.saving = true; this.suscError = '';
      const body = { antibioticId: this.suscForm.antibioticId, method: this.suscForm.method, zoneMm: this.suscForm.method === 'DISK' ? parseDecimal(this.suscForm.zoneMm) : null, mic: this.suscForm.method === 'MIC' ? parseDecimal(this.suscForm.mic) : null };
      try {
        const res = await this.$api.addSusceptibility(this.activeIsolate.id, body);
        const sir = res && (res.interpretation || res.sir);
        this.$q.notify({ type: sir === 'R' ? 'warning' : 'positive', message: `${this.antibioticName(body.antibioticId)}: ${sir || 'збережено'}${res && (res.phenotypes || []).length ? ' · фенотип ' + res.phenotypes.join(', ') : ''}` });
        this.suscForm = { ...this.suscForm, antibioticId: null, zoneMm: '', mic: '' };
        this.load();
      } catch (e) { this.suscError = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    },
    async deleteSusc (row) { try { await this.$api.deleteSusceptibility(row.id); this.notifyOk('Запис видалено'); this.load(); } catch (e) { this.notifyError(e); } },
    async openReport () {
      this.reportOpen = true; this.reportLoading = true; this.reportError = ''; this.reportHtml = '';
      try { this.reportHtml = await this.$api.cultureReport(this.culture.id); } catch (e) { this.reportError = e.userMessage || 'Не вдалося отримати протокол'; } finally { this.reportLoading = false; }
    }
  }
};
</script>
