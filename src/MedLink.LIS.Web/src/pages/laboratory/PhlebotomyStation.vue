<template>
  <div class="phlebotomy-page" data-testid="phlebotomyPage">
    <page-header title="Пункт забору біоматеріалу" icon="fas fa-syringe" :breadcrumbs="[{ label: 'Пункт забору' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="loadLists" />
      <q-btn unelevated color="primary" icon="add" label="Нове направлення" @click="createOpen = true" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="loadLists" />

    <div class="row q-col-gutter-md">
      <!-- Сканер + картка проби -->
      <div class="col-12 col-lg-5">
        <div class="medlink-card q-pa-md q-mb-md">
          <q-input
            ref="scan"
            v-model="barcode"
            outlined
            autofocus
            label="Відскануйте або введіть штрихкод пробірки"
            :loading="scanning"
            data-testid="scanInput"
            @keyup.enter="scan"
          >
            <template v-slot:prepend><q-icon name="qr_code_scanner" /></template>
            <template v-slot:append><q-btn flat dense icon="search" @click="scan" /></template>
          </q-input>
          <div class="text-caption text-grey-6 q-mt-xs">Відскануйте штрихкод або введіть 8 цифр і натисніть Enter.</div>
        </div>

        <div v-if="order && sample" class="medlink-card q-mb-md" data-testid="sampleCard">
          <div class="medlink-card__title">
            <span><q-icon name="science" class="q-mr-xs" />Проба <span class="mono">{{ sample.barcode }}</span></span>
            <status-chip :value="sample.status" type="sample" icon />
          </div>
          <div class="q-pa-md">
            <div class="row q-col-gutter-sm">
              <div class="col-7">
                <div class="text-caption text-grey-7">Пацієнт</div>
                <div class="text-weight-bold">{{ patientName }}</div>
                <div class="text-caption">{{ patientMeta }}</div>
              </div>
              <div class="col-5">
                <div class="text-caption text-grey-7">Направлення</div>
                <router-link :to="{ name: 'lab-order-card', params: { id: order.id } }" class="text-primary text-weight-bold">{{ order.orderNumber }}</router-link>
                <q-badge v-if="order.isUrgentCito" color="deep-orange-6" label="CITO" class="q-ml-xs" />
              </div>
              <div class="col-12">
                <div class="text-caption text-grey-7">Пробірка</div>
                <div class="row items-center no-wrap q-gutter-sm">
                  <div :style="{ background: tubeColor, width: '16px', height: '26px', borderRadius: '3px 3px 8px 8px', border: '1px solid #999' }" />
                  <div><b>{{ tubeName }}</b> <span class="text-grey-7">· {{ biomaterialName }}</span></div>
                </div>
              </div>
              <div class="col-12">
                <div class="text-caption text-grey-7">Показники в цій пробі</div>
                <div class="row q-gutter-xs">
                  <q-chip v-for="t in sampleTests" :key="t.id" dense size="sm" color="blue-1" text-color="primary">{{ t.testName || t.testCode }}</q-chip>
                  <span v-if="!sampleTests.length" class="text-grey-6 text-caption">—</span>
                </div>
              </div>
            </div>
            <div v-if="orderTubePlan.length > 1" class="q-mt-md" data-testid="orderTubePlan">
              <div class="text-caption text-grey-7">Усі пробірки замовлення в порядку забору</div>
              <tube-plan-list :items="orderTubePlan" :active-key="sample.id" />
            </div>
            <div class="row q-gutter-sm q-mt-md">
              <q-btn v-if="sample.status === 'PENDING'" color="teal-6" icon="colorize" label="Забрати" data-testid="collectBtn" @click="collectOpen = true" />
              <q-btn color="primary" icon="print" label="Друк етикетки" data-testid="printLabelAgent" @click="printSampleLabel(sample.barcode, { patientName, orderNumber: order.orderNumber })" />
              <q-btn-dropdown flat color="grey-8" icon="more_horiz" label="Ще" no-caps>
                <q-list dense>
                  <q-item v-close-popup clickable @click="showLabel(sample.barcode)"><q-item-section avatar><q-icon name="label" /></q-item-section><q-item-section>Переглянути етикетку</q-item-section></q-item>
                  <q-item v-close-popup clickable @click="editSample(sample)"><q-item-section avatar><q-icon name="edit" /></q-item-section><q-item-section>Картка проби</q-item-section></q-item>
                  <q-item v-close-popup clickable :to="{ name: 'lab-sample-processing', params: { barcode: sample.barcode } }"><q-item-section avatar><q-icon name="biotech" /></q-item-section><q-item-section>Обробка зразка</q-item-section></q-item>
                </q-list>
              </q-btn-dropdown>
              <q-btn v-if="sample.status === 'PENDING'" flat color="negative" icon="block" label="Відхилити" @click="rejectOpen = true" />
            </div>
            <div v-if="sample.status !== 'PENDING'" class="text-caption text-grey-7 q-mt-sm"><q-icon name="info" /> {{ sampleStatusHint }}</div>
          </div>
        </div>
        <div v-else-if="scanned && !scanning" class="medlink-card q-pa-md q-mb-md">
          <empty-state title="Пробу не знайдено" :hint="`Штрихкод «${scanned}» відсутній у системі`" icon="search_off" />
        </div>

        <!-- Етикетка -->
        <div v-if="label" class="medlink-card q-pa-md" data-testid="labelPreview">
          <div class="section-title">Етикетка</div>
          <label-sticker :label="label" />
          <div class="row justify-center q-gutter-sm q-mt-sm">
            <q-btn dense color="primary" icon="print" label="Друк етикетки" :disable="!label.zpl" @click="printLabelsViaAgent([label])" />
            <q-btn dense outline color="primary" icon="content_copy" label="Копіювати код" :disable="!label.zpl" @click="copyZpl" />
            <q-btn dense outline color="grey-8" icon="open_in_full" label="Збільшити" @click="labelsOpen = true" />
          </div>
        </div>
      </div>

      <!-- Списки -->
      <div class="col-12 col-lg-7">
        <div class="medlink-card">
          <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8">
            <q-tab name="pending" :label="`Очікують забору (${pending.length})`" icon="hourglass_empty" />
            <q-tab name="collected" :label="`Забрано сьогодні (${collectedToday.length})`" icon="colorize" />
          </q-tabs>
          <q-separator />
          <q-table
            :data="tab === 'pending' ? pending : collectedToday"
            :columns="tableColumns"
            row-key="id"
            dense flat
            :loading="loading"
            :pagination.sync="pagination"
            :rows-per-page-options="[15, 30, 100]"
            no-data-label="Проб немає"
            data-testid="samplesTable"
          >
            <template v-slot:body-cell-barcode="props">
              <q-td :props="props"><a href="#" class="mono text-weight-bold text-primary" @click.prevent="scanBarcode(props.row.barcode)">{{ props.row.barcode }}</a><q-badge v-if="props.row.isUrgentCito" color="deep-orange-6" label="CITO" class="q-ml-xs" /></q-td>
            </template>
            <template v-slot:body-cell-actions="props">
              <q-td :props="props" class="text-right no-wrap">
                <q-btn v-if="props.row.status === 'PENDING'" flat dense round size="sm" icon="colorize" color="teal-6" @click="quickCollect(props.row)"><q-tooltip>Забрати пробу</q-tooltip></q-btn>
                <q-btn flat dense round size="sm" icon="print" color="primary" @click="printSampleLabel(props.row.barcode, { patientName: props.row.patientName, orderNumber: props.row.orderNumber })"><q-tooltip>Друк етикетки</q-tooltip></q-btn>
                <q-btn flat dense round size="sm" icon="open_in_new" color="primary" :disable="!props.row.orderId" @click="$router.push({ name: 'lab-order-card', params: { id: props.row.orderId } })"><q-tooltip>Картка направлення</q-tooltip></q-btn>
              </q-td>
            </template>
          </q-table>
        </div>
      </div>
    </div>

    <collect-sample-dialog v-model="collectOpen" :sample="sample || quickSample" :patient-name="patientName" @collected="onCollected" />
    <reject-sample-dialog v-model="rejectOpen" :samples="sample ? [sample] : []" @rejected="afterChange" />
    <sample-edit-dialog v-model="sampleEditOpen" :sample="editing || {}" @saved="afterChange" />
    <labels-dialog v-model="labelsOpen" :labels="label ? [label] : []" />
    <create-order-dialog v-model="createOpen" @created="onCreated" />
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import LabelSticker from '../../components/common/LabelSticker.vue';
import LabelsDialog from '../../components/common/LabelsDialog.vue';
import CollectSampleDialog from '../../components/samples/CollectSampleDialog.vue';
import RejectSampleDialog from '../../components/samples/RejectSampleDialog.vue';
import SampleEditDialog from '../../components/samples/SampleEditDialog.vue';
import CreateOrderDialog from './orders/CreateOrderDialog.vue';
import TubePlanList from '../../components/samples/TubePlanList.vue';
import { TUBE_PLAN_REASONS, SAMPLE_STATUS } from '../../utils/statuses';
import labelPrintMixin from '../../mixins/labelPrintMixin';
import { patientDisplay, genderLabel, ageFromBirthDate, formatDateTime, todayIso, toIsoDate, copyToClipboard } from '../../utils/format';

export default {
  name: 'PhlebotomyStation',
  mixins: [apiMixin, labelPrintMixin],
  components: { TubePlanList, LabelSticker, LabelsDialog, CollectSampleDialog, RejectSampleDialog, SampleEditDialog, CreateOrderDialog },
  data () {
    return {
      barcode: '',
      scanned: '',
      scanning: false,
      order: null,
      sample: null,
      quickSample: null,
      label: null,
      pending: [],
      collected: [],
      tab: 'pending',
      collectOpen: false, rejectOpen: false, sampleEditOpen: false, labelsOpen: false, createOpen: false,
      editing: null,
      pagination: { rowsPerPage: 15 },
      columns: [
        { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left' },
        { name: 'patient', label: 'Пацієнт', field: r => r.patientName || patientDisplay(r.patient), align: 'left' },
        { name: 'order', label: '№ направлення', field: r => r.orderNumber || '—', align: 'left' },
        { name: 'tube', label: 'Пробірка', field: r => r.tubeName || (r.tube && r.tube.name) || this.tubeNameById(r.tubeTypeId), align: 'left' },
        { name: 'collectedAt', label: 'Забрано', field: r => formatDateTime(r.collectedAt), align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    collectedToday () {
      const today = todayIso();
      return this.collected.filter(s => !s.collectedAt || toIsoDate(s.collectedAt) === today);
    },
    patientName () { return this.order ? (patientDisplay(this.order.patient) !== '—' ? patientDisplay(this.order.patient) : (this.order.patientName || '')) : (this.quickSample && this.quickSample.patientName) || ''; },
    patientMeta () {
      const p = (this.order && this.order.patient) || {};
      const age = ageFromBirthDate(p.birthDate);
      return [age !== null ? `${age} р.` : null, genderLabel(p.gender) !== '—' ? genderLabel(p.gender) : null, p.phone].filter(Boolean).join(' · ');
    },
    tube () { return this.sample ? ((this.sample.tube && typeof this.sample.tube === 'object') ? this.sample.tube : this.$store.getters['dictionaries/byId']('tube-types', this.sample.tubeTypeId)) : null; },
    tubeName () { return (this.tube && this.tube.name) || (this.sample && this.sample.tubeName) || 'Пробірка'; },
    tubeColor () { return (this.tube && this.tube.colorCode) || '#bbb'; },
    biomaterialName () {
      const b = this.sample ? this.$store.getters['dictionaries/byId']('biomaterials', this.sample.biomaterialTypeId) : null;
      return (b && b.name) || (this.sample && this.sample.biomaterialName) || '';
    },
    orderTubePlan () {
      if (!this.order) return [];
      return (this.order.samples || []).filter(s => !s.parentSampleId)
        .slice().sort((a, b) => a.groupNumb - b.groupNumb)
        .map((s, i) => {
          const tube = this.$store.getters['dictionaries/byId']('tube-types', s.tubeTypeId) || {};
          return {
            key: s.id, index: i + 1, color: s.tubeColor || tube.colorCode, tubeName: s.tubeTypeName || tube.name, biomaterialName: s.biomaterialName,
            tests: (s.testCodes || []).map(code => ({ code })), usedVolumeMl: s.plannedVolumeMl, capacityMl: s.capacityMl,
            reasonTexts: (s.planReasons || []).map(r => TUBE_PLAN_REASONS[r] || r), isSeparate: (s.planReasons || []).includes('SEPARATE_REQUIRED'),
            inversionsCount: tube.inversionsCount, barcode: s.barcode, status: s.status
          };
        });
    },
    tableColumns () { return this.tab === 'pending' ? this.columns.filter(c => c.name !== 'collectedAt') : this.columns; },
    sampleStatusHint () {
      if (!this.sample) return '';
      if (this.sample.status === 'COLLECTED') return 'Пробу вже забрано — повторний забір недоступний.';
      const meta = SAMPLE_STATUS[this.sample.status];
      return `Проба має статус «${meta ? meta.label : 'інший'}» — забір недоступний.`;
    },
    sampleTests () { return this.order && this.sample ? (this.order.tests || []).filter(t => t.sampleId === this.sample.id) : []; }
  },
  created () {
    this.$store.dispatch('dictionaries/loadMany', ['tube-types', 'biomaterials']);
    this.loadLists();
    if (this.$route.query.barcode) { this.barcode = this.$route.query.barcode; this.scan(); }
  },
  methods: {
    tubeNameById (id) { const t = this.$store.getters['dictionaries/byId']('tube-types', id); return t ? t.name : '—'; },
    async loadLists () {
      const results = await Promise.allSettled([
        this.$api.getSamples({ status: 'PENDING' }),
        this.$api.getSamples({ status: 'COLLECTED' })
      ]);
      if (results[0].status === 'fulfilled') this.pending = this.asList(results[0].value);
      if (results[1].status === 'fulfilled') this.collected = this.asList(results[1].value);
      const failed = results.find(r => r.status === 'rejected');
      if (failed && !this.apiOffline) this.apiError = failed.reason.userMessage;
      else this.apiError = null;
    },
    scanBarcode (code) { this.barcode = code; this.scan(); },
    async scan () {
      const code = (this.barcode || '').trim();
      if (!code) return;
      this.scanning = true;
      this.scanned = code;
      this.order = null; this.sample = null; this.label = null;
      try {
        const order = await this.$api.orderByBarcode(code);
        this.order = order;
        this.sample = (order.samples || []).find(s => s.barcode === code) || null;
        if (this.sample) this.showLabel(code, true);
      } catch (e) {
        if (e.apiStatus !== 404) this.notifyError(e);
      } finally {
        this.scanning = false;
        this.$nextTick(() => { if (this.$refs.scan) this.$refs.scan.select(); });
      }
    },
    async showLabel (code, silent) {
      try {
        const l = await this.$api.sampleLabel(code);
        this.label = { patientName: this.patientName, orderNumber: this.order && this.order.orderNumber, ...l };
      } catch (e) {
        this.label = null;
        if (!silent) this.notifyError(e);
      }
    },
    copyZpl () { copyToClipboard(this.label.zpl).then(() => this.notifyOk('Код етикетки скопійовано')); },
    quickCollect (row) {
      this.quickSample = row;
      this.sample = null; this.order = null;
      this.collectOpen = true;
    },
    async onCollected () {
      await this.loadLists();
      const code = (this.sample && this.sample.barcode) || (this.quickSample && this.quickSample.barcode);
      this.tab = 'collected';
      if (!code) return;
      // наступна ще не забрана пробірка цього ж направлення — відкриваємо одразу, без повторного сканування
      let next = null;
      try {
        const order = await this.$api.orderByBarcode(code);
        next = (order.samples || []).filter(s => !s.parentSampleId && s.barcode !== code && s.status === 'PENDING').sort((a, b) => a.groupNumb - b.groupNumb)[0] || null;
      } catch (e) { /* без підказки про наступну пробірку */ }
      if (next) {
        this.notifyOk(`Далі: пробірка ${next.barcode}`);
        this.barcode = next.barcode; this.tab = 'pending';
        await this.scan();
      } else {
        this.barcode = code; await this.scan(); this.labelsOpen = !!this.label;
        this.barcode = '';
        this.$nextTick(() => { if (this.$refs.scan) this.$refs.scan.focus(); });
      }
    },
    editSample (s) { this.editing = s; this.sampleEditOpen = true; },
    afterChange () {
      this.loadLists();
      if (this.scanned) this.scan();
    },
    onCreated (order) {
      this.loadLists();
      const first = order && order.samples && order.samples[0];
      if (first) { this.barcode = first.barcode; this.scan(); }
    }
  }
};
</script>
