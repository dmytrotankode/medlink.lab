<template>
  <div class="sample-processing" data-testid="sampleProcessing">
    <page-header :title="`Обробка зразка ${barcode}`" icon="biotech" subtitle="Дерево зразка (батьківський → аліквоти / касети / блоки / скло / чашки), етапи робочого процесу, розділення з друком етикеток" :breadcrumbs="[{ label: 'Пункт забору', to: { name: 'lab-phlebotomy' } }, { label: 'Обробка зразка' }]">
      <template v-slot:title-after><status-chip v-if="root" :value="root.status" type="sample" icon /></template>
      <q-input v-model="scan" dense outlined placeholder="Інший штрихкод" style="width: 200px" @keyup.enter="$router.push({ name: 'lab-sample-processing', params: { barcode: scan } })"><template v-slot:prepend><q-icon name="qr_code_scanner" /></template></q-input>
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn outline dense color="primary" icon="print" label="Друк етикетки" @click="printSampleLabel(barcode, { patientName })" />
      <q-btn unelevated dense color="teal-7" icon="call_split" label="Розділити" :disable="!root" data-testid="splitBtn" @click="openSplit" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="row q-col-gutter-md">
      <!-- Дерево -->
      <div class="col-12 col-md-5">
        <div class="medlink-card">
          <div class="medlink-card__title"><span><q-icon name="account_tree" class="q-mr-xs" />Дерево зразка</span><span class="text-caption text-grey-6">{{ countNodes }} вузл.</span></div>
          <div v-if="root" class="q-pa-sm">
            <div class="text-caption text-grey-7">Пацієнт: <b>{{ patientName }}</b> · Замовлення: <router-link v-if="orderId" :to="{ name: 'lab-order-card', params: { id: orderId } }" class="text-primary">{{ orderNumber }}</router-link></div>
            <q-tree :nodes="treeNodes" node-key="barcode" default-expand-all :selected.sync="selectedBarcode" selected-color="primary" data-testid="sampleTree">
              <template v-slot:default-header="prop">
                <div class="row items-center no-wrap q-gutter-xs">
                  <q-icon :name="prop.node.icon" :color="prop.node.barcode === barcode ? 'primary' : 'grey-7'" size="18px" />
                  <span class="mono text-weight-bold" :class="{ 'text-primary': prop.node.barcode === barcode }">{{ prop.node.barcode }}</span>
                  <q-badge v-if="prop.node.derivationType" color="teal-6" :label="derivationLabel(prop.node.derivationType)" />
                  <status-chip :value="prop.node.status" type="sample" dense />
                  <span v-if="prop.node.volumeMl" class="text-caption text-grey-7">{{ prop.node.volumeMl | num }} мл</span>
                  <span v-if="prop.node.sectionName" class="text-caption text-grey-7">· {{ prop.node.sectionName }}</span>
                </div>
              </template>
            </q-tree>
            <div v-if="selectedBarcode && selectedBarcode !== barcode" class="q-mt-sm row q-gutter-xs">
              <q-btn dense outline color="primary" icon="open_in_new" :label="`Відкрити ${selectedBarcode}`" :to="{ name: 'lab-sample-processing', params: { barcode: selectedBarcode } }" />
              <q-btn dense outline color="primary" icon="print" label="Етикетка" @click="printSampleLabel(selectedBarcode, { patientName })" />
            </div>
          </div>
          <empty-state v-else-if="!loading" title="Зразок не знайдено" icon="search_off" />
        </div>
      </div>

      <!-- Етапи -->
      <div class="col-12 col-md-7">
        <div class="medlink-card">
          <div class="medlink-card__title"><span><q-icon name="route" class="q-mr-xs" />Етапи обробки</span><span class="text-caption text-grey-6">{{ workflowName }}</span></div>
          <div v-if="stages.length" class="q-pa-sm">
            <div class="row no-wrap q-gutter-xs scroll" style="overflow-x: auto">
              <div v-for="(s, i) in stages" :key="s.code || s.name" class="pipeline-step col" style="min-width: 120px" :class="{ 'pipeline-step--done': s.completedAt || s.isCompleted, 'pipeline-step--active': isCurrent(s, i) }">
                <div class="pipeline-num">{{ i + 1 }}</div>
                <div class="column" style="line-height: 1.15"><span>{{ s.name || s.label || s.code }}</span><span class="text-caption" :class="s.completedAt ? 'text-grey-8' : 'text-grey-5'">{{ s.completedAt ? formatDateTime(s.completedAt) : (s.isCompleted ? 'виконано' : '—') }}</span></div>
              </div>
            </div>
            <q-separator class="q-my-sm" />
            <div class="row q-col-gutter-sm items-end">
              <div class="col-12 col-md-4"><q-select v-model="stageForm.stageCode" outlined dense label="Етап" :options="stages.map(s => ({ value: s.code || s.name, label: s.name || s.label || s.code }))" emit-value map-options data-testid="stageSelect" /></div>
              <div class="col-6 col-md-2" v-if="needs('temperature')"><q-input v-model="stageForm.temperature" outlined dense label="t°, °C" inputmode="decimal" /></div>
              <div class="col-6 col-md-3" v-if="needs('instrument')"><q-select v-model="stageForm.instrument" outlined dense label="Прилад" :options="instrumentOptions" use-input new-value-mode="add-unique" /></div>
              <div class="col-12 col-md-3"><q-input v-model="stageForm.note" outlined dense label="Примітка" /></div>
              <div class="col-12 text-right"><q-btn color="primary" icon="check" label="Зафіксувати етап" :loading="saving" :disable="!stageForm.stageCode" data-testid="stageConfirm" @click="setStage" /></div>
            </div>
            <q-list dense separator class="q-mt-sm">
              <q-item v-for="h in history" :key="h.id || h.at + h.stageCode">
                <q-item-section avatar><q-icon name="check_circle" color="positive" /></q-item-section>
                <q-item-section><q-item-label>{{ h.stageName || h.stageCode }}</q-item-label><q-item-label caption>{{ h.at || h.completedAt | datetime }} · {{ h.byName || h.by || '' }} {{ h.temperature !== undefined && h.temperature !== null ? '· ' + h.temperature + ' °C' : '' }} {{ h.instrument ? '· ' + h.instrument : '' }} {{ h.note ? '· ' + h.note : '' }}</q-item-label></q-item-section>
              </q-item>
            </q-list>
          </div>
          <empty-state v-else-if="!loading" title="Етапи не визначено" icon="route" hint="Шаблон робочого процесу підрозділу не містить етапів або зразок ще не прийнято" />
        </div>
      </div>
    </div>

    <!-- Розділити -->
    <q-dialog v-model="splitOpen" persistent>
      <q-card style="min-width: 560px" data-testid="splitDialog">
        <q-card-section class="row items-center bg-teal-7 text-white q-py-sm"><q-icon name="call_split" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">Розділити зразок {{ barcode }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-btn-toggle v-model="split.derivationType" spread no-caps unelevated toggle-color="teal-7" :options="derivationTypes.map(d => ({ value: d.value, label: d.label }))" data-testid="splitType" />
          <div class="row q-col-gutter-sm">
            <div class="col-6"><q-input v-model.number="split.count" outlined dense type="number" min="1" max="24" label="Кількість *" data-testid="splitCount" /></div>
            <div class="col-6"><q-input v-model="split.volumeEachMl" outlined dense inputmode="decimal" label="Об’єм кожної, мл" /></div>
            <div class="col-12"><q-select v-model="split.targetSectionIds" outlined dense multiple use-chips label="Цільові підрозділи" :options="sectionOptions" emit-value map-options /></div>
          </div>
          <q-toggle v-model="split.printLabels" label="Надрукувати етикетки дочірніх зразків" color="teal-7" />
          <div v-if="splitError" class="text-negative">{{ splitError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="teal-7" icon="call_split" label="Розділити" :loading="saving" :disable="!split.count" data-testid="splitConfirm" @click="doSplit" /></q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import labelPrintMixin from '../../../mixins/labelPrintMixin';
import { DERIVATION_TYPES } from '../../../utils/statuses';
import { formatDateTime, parseDecimal, patientDisplay } from '../../../utils/format';

export default {
  name: 'SampleProcessing',
  mixins: [apiMixin, labelPrintMixin],
  props: { barcode: { type: String, required: true } },
  data () {
    return {
      tree: null, stagesData: null, sections: [], scan: '', selectedBarcode: null,
      stageForm: { stageCode: null, temperature: '', instrument: null, note: '' },
      splitOpen: false, split: { derivationType: 'ALIQUOT', count: 2, volumeEachMl: '', targetSectionIds: [], printLabels: true }, splitError: '', saving: false,
      derivationTypes: DERIVATION_TYPES,
      instrumentOptions: ['Центрифуга Eppendorf 5702', 'Мікротом Leica RM2235', 'Гістопроцесор Thermo Excelsior', 'Термостат 37 °C', 'Фарбувальний автомат']
    };
  },
  computed: {
    root () { if (!this.tree) return null; return this.tree.root || this.tree.sample || this.tree; },
    orderId () { return this.root && (this.root.orderId || (this.tree && this.tree.orderId)); },
    orderNumber () { return (this.root && this.root.orderNumber) || (this.tree && this.tree.orderNumber) || ''; },
    patientName () { return (this.root && (this.root.patientName || patientDisplay(this.root.patient))) || (this.tree && this.tree.patientName) || ''; },
    treeNodes () { return this.root ? [this.toNode(this.root)] : []; },
    countNodes () { const walk = n => 1 + (n.children || []).reduce((s, c) => s + walk(c), 0); return this.root ? walk(this.root) : 0; },
    stages () { const d = this.stagesData; if (!d) return []; return Array.isArray(d) ? d : (d.stages || d.items || []); },
    history () { const d = this.stagesData; if (!d) return []; const h = (d.history || d.completed || []); return h.length ? h : this.stages.filter(s => s.completedAt).map(s => ({ stageCode: s.code, stageName: s.name, at: s.completedAt, byName: s.completedByName, temperature: s.temperature, instrument: s.instrument, note: s.note })); },
    workflowName () { const d = this.stagesData; return (d && (d.workflowTemplate || d.workflowName)) || ''; },
    sectionOptions () { return this.sections.map(s => ({ value: s.id, label: s.name })); },
    currentStage () { return this.stages.find(s => (s.code || s.name) === this.stageForm.stageCode); }
  },
  watch: { barcode () { this.load(); } },
  created () { this.load(); this.$api.sections().then(r => { this.sections = this.asList(r); }).catch(() => {}); },
  methods: {
    formatDateTime,
    derivationLabel (t) { const d = DERIVATION_TYPES.find(x => x.value === t); return d ? d.label : t; },
    toNode (n) { const d = DERIVATION_TYPES.find(x => x.value === n.derivationType); return { ...n, icon: d ? d.icon : 'science', label: n.barcode, children: (n.children || []).map(c => this.toNode(c)) }; },
    isCurrent (s, i) { if (s.isCurrent) return true; const firstOpen = this.stages.findIndex(x => !(x.completedAt || x.isCompleted)); return i === firstOpen; },
    needs (field) { const s = this.currentStage; if (!s) return true; const f = s.fields || s.requiredFields; return Array.isArray(f) ? f.includes(field) : true; },
    async load () {
      this.loading = true;
      const results = await Promise.allSettled([this.$api.sampleTree(this.barcode), this.$api.sampleStages(this.barcode)]);
      if (results[0].status === 'fulfilled') this.tree = results[0].value; else this.tree = null;
      if (results[1].status === 'fulfilled') this.stagesData = results[1].value; else this.stagesData = null;
      const failed = results[0].status === 'rejected' ? results[0].reason : null;
      this.apiError = failed && !this.apiOffline && failed.apiStatus !== 404 ? failed.userMessage : null;
      this.loading = false;
      const open = this.stages.find((s, i) => this.isCurrent(s, i));
      this.stageForm.stageCode = open ? (open.code || open.name) : null;
    },
    async setStage () {
      this.saving = true;
      try {
        await this.$api.setSampleStage(this.barcode, { stageCode: this.stageForm.stageCode, temperature: parseDecimal(this.stageForm.temperature), instrument: this.stageForm.instrument || null, note: this.stageForm.note || null });
        this.notifyOk('Етап зафіксовано'); this.stageForm.note = ''; this.load();
      } catch (e) { this.notifyError(e); } finally { this.saving = false; }
    },
    openSplit () { this.splitError = ''; this.splitOpen = true; },
    async doSplit () {
      this.saving = true; this.splitError = '';
      try {
        const res = await this.$api.splitSample(this.barcode, { count: Number(this.split.count), volumeEachMl: parseDecimal(this.split.volumeEachMl), derivationType: this.split.derivationType, targetSectionIds: this.split.targetSectionIds });
        const children = (res && (res.children || res.samples || res.items)) || (Array.isArray(res) ? res : []);
        this.notifyOk(`Створено дочірніх зразків: ${children.length || this.split.count}`);
        this.splitOpen = false;
        if (this.split.printLabels && children.length) {
          const labels = [];
          for (const c of children) { try { labels.push({ patientName: this.patientName, orderNumber: this.orderNumber, ...(await this.$api.sampleLabel(c.barcode)) }); } catch (e) { /* ignore */ } }
          if (labels.length) this.printLabelsViaAgent(labels);
        }
        this.load();
      } catch (e) { this.splitError = e.userMessage || 'Помилка розділення'; } finally { this.saving = false; }
    }
  }
};
</script>
