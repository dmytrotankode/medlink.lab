<!--
  ТЗ 22.3, 22.14 п. 3 (FR-PAT-001…008): «Прийом матеріалу» — черга направлень ПГД з усіх джерел, дані лицьової сторони 014/о
  (лише перегляд), сканування кожного контейнера, контроль фіксації, «Прийняти й присвоїти №», «Відмовити» з причиною, запит направнику.
-->
<template>
  <div data-testid="pathologyIntakePage">
    <page-header title="Прийом матеріалу" icon="fas fa-box-open" subtitle="Направлення на патогістологічне дослідження (форма 014/о) з МІС: сканування контейнерів, контроль маркування й фіксації, присвоєння № справи"
      :breadcrumbs="[{ label: 'Патоморфологія' }, { label: 'Прийом матеріалу' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="row q-col-gutter-md">
      <div class="col-12 col-md-4">
        <div class="medlink-card">
          <q-list separator dense>
            <q-item v-for="r in list" :key="r.id" clickable :active="current && current.id === r.id" active-class="bg-blue-1" :data-testid="`incoming-${r.orderNumber}`" @click="select(r)">
              <q-item-section avatar><q-icon :name="sourceIcon(r.source)" :color="r.cito ? 'negative' : 'primary'" /></q-item-section>
              <q-item-section>
                <q-item-label><b>{{ r.patient }}</b> <q-badge v-if="r.cito" color="negative" label="CITO" /></q-item-label>
                <q-item-label caption>{{ r.kindLabel }} · {{ r.sourceLabel }}</q-item-label>
                <q-item-label caption>{{ r.department }}<span v-if="r.doctor"> · {{ r.doctor }}</span></q-item-label>
              </q-item-section>
              <q-item-section side>
                <div class="text-caption">{{ r.createdAt | datetime }}</div>
                <q-badge :color="r.unlabeled ? 'negative' : 'grey-6'" :label="`контейнерів ${r.containers}`" />
                <div class="text-caption text-grey-7">гр. {{ r.serviceGroup }}</div>
              </q-item-section>
            </q-item>
          </q-list>
          <empty-state v-if="!list.length && !loading" title="Черга прийому порожня" hint="Направлення ПГД з'являються після підпису в МІС" icon="inbox" />
        </div>
      </div>

      <div class="col-12 col-md-8">
        <div v-if="current" class="medlink-card q-pa-md" data-testid="intakePanel">
          <div class="row items-center q-mb-sm">
            <div class="col">
              <div class="text-subtitle1 text-weight-bold">{{ current.patient }} <span class="text-grey-7 text-body2">{{ current.birthday | date }}</span></div>
              <div class="text-caption text-grey-7">{{ current.kindLabel }} · {{ current.sourceLabel }}<span v-if="current.ehealthReferral"> · е-направлення {{ current.ehealthReferral }}</span> · {{ current.orderNumber }}</div>
            </div>
            <q-badge color="blue-grey-6" label="Лицьова сторона 014/о — лише перегляд" />
          </div>

          <q-markup-table dense flat bordered wrap-cells class="q-mb-md form014">
            <tbody>
              <tr v-if="facility"><td>1. Заклад</td><td>{{ facility }}</td></tr>
              <tr><td>Відділення / направник</td><td>{{ current.department }}<span v-if="ref.ward"> · {{ ref.ward }}</span> · {{ ref.referringDoctor }}<span v-if="ref.medicalCardNo"> · карта {{ ref.medicalCardNo }}</span></td></tr>
              <tr><td>4–5. Біопсія</td><td>{{ ref.biopsyKindLabel }}<span v-if="ref.primaryCaseNo"> (первинна справа {{ ref.primaryCaseNo }})</span></td></tr>
              <tr><td>6. Операція / маніпуляція</td><td>{{ ref.operationText || '—' }}</td></tr>
              <tr><td>7. Маркування</td><td>{{ ref.markingText || '—' }}</td></tr>
              <tr><td>8. Клінічні дані</td><td>{{ ref.clinicalData || '—' }}</td></tr>
              <tr><td>9. Клінічний діагноз</td><td>{{ ref.clinicalDiagnosis || '—' }}</td></tr>
              <tr v-if="ref.external"><td>Зовнішні препарати</td><td>{{ ref.external.institution }} № {{ ref.external.number }}: скелець {{ ref.external.slides }}, блоків {{ ref.external.blocks }}</td></tr>
            </tbody>
          </q-markup-table>

          <div class="row items-center q-mb-xs">
            <div class="text-weight-bold">Контейнери ({{ scannedCount }} з {{ current.objects.length }} відскановано)</div>
            <q-space />
            <q-input v-model="scan" dense outlined autofocus placeholder="Скануйте штрихкод контейнера" style="width: 260px" data-testid="containerScan" @keyup.enter="doScan">
              <template v-slot:prepend><q-icon name="qr_code_scanner" /></template>
            </q-input>
          </div>
          <q-markup-table dense flat bordered class="q-mb-md">
            <thead><tr><th class="text-left">№</th><th class="text-left">Матеріал</th><th class="text-left">Фіксатор · фіксація</th><th class="text-left">Штрихкод</th><th class="text-left">Стан</th></tr></thead>
            <tbody>
              <tr v-for="o in current.objects" :key="o.id" :class="!o.labeled ? 'bg-red-1' : scanned[o.barcode] ? 'bg-green-1' : ''">
                <td><b>{{ o.mark }}</b></td>
                <td>{{ o.description }}</td>
                <td class="text-caption">{{ o.fixative || '—' }}<span v-if="o.fixationHours !== null"> · {{ o.fixationHours }} год</span>
                  <div v-if="o.fixationWarning" class="text-orange-9"><q-icon name="warning" /> {{ o.fixationWarning }}</div>
                </td>
                <td><code>{{ o.barcode }}</code></td>
                <td>
                  <q-badge v-if="!o.labeled" color="negative" :label="o.problem" />
                  <q-badge v-else-if="scanned[o.barcode]" color="positive" icon="check" label="Відскановано" />
                  <q-btn v-else flat dense size="sm" color="primary" icon="qr_code_scanner" label="Сканувати" :data-testid="`scan-${o.mark}`" @click="markScanned(o.barcode)" />
                </td>
              </tr>
            </tbody>
          </q-markup-table>

          <q-banner v-if="unlabeled" dense class="bg-red-1 text-negative q-mb-sm"><q-icon name="block" /> Контейнери без маркування — прийом заблоковано (FR-PAT-003). Відмовте з причиною або надішліть запит направнику.</q-banner>
          <div class="row q-gutter-sm justify-end">
            <q-btn flat color="grey-8" icon="help_outline" label="Запит направнику" @click="queryOpen = true" />
            <q-btn outline color="negative" icon="block" label="Відмовити" data-testid="rejectBtn" @click="rejectOpen = true" />
            <q-btn unelevated color="primary" icon="assignment_turned_in" label="Прийняти й присвоїти №" data-testid="acceptBtn" :disable="!canAccept" @click="accept" />
          </div>
        </div>
        <empty-state v-else title="Оберіть направлення" hint="Ліворуч — черга направлень ПГД; праворуч — дані форми 014/о та контейнери" icon="fas fa-box-open" />
      </div>
    </div>

    <q-dialog v-model="rejectOpen">
      <q-card style="min-width: 460px">
        <q-card-section class="text-h6">Відмова в прийомі</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-select v-model="reject.reason" dense outlined emit-value map-options :options="reasons" label="Причина (довідник)" data-testid="rejectReason" />
          <q-input v-model="reject.comment" dense outlined type="textarea" autogrow label="Коментар" />
          <div class="text-caption text-grey-7">МІС отримає подію REJECTED; е-направлення ЕСОЗ не погашається.</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="negative" label="Відмовити" :disable="!reject.reason" @click="doReject" />
        </q-card-actions>
      </q-card>
    </q-dialog>
    <q-dialog v-model="queryOpen">
      <q-card style="min-width: 460px">
        <q-card-section class="text-h6">Запит направнику (QUERY)</q-card-section>
        <q-card-section><q-input v-model="queryText" dense outlined type="textarea" autogrow label="Що уточнити" /></q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Надіслати" :disable="!queryText" @click="doQuery" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';

export default {
  name: 'PathologyIntakePage',
  mixins: [apiMixin],
  data () {
    return { list: [], current: null, scanned: {}, scan: '', reasons: [], rejectOpen: false, reject: { reason: null, comment: '' }, queryOpen: false, queryText: '' };
  },
  computed: {
    ref () { return (this.current && this.current.referral) || {}; },
    facility () { return this.ref.form014 && this.ref.form014.facility; },
    scannedCount () { return this.current ? this.current.objects.filter(o => this.scanned[o.barcode]).length : 0; },
    unlabeled () { return this.current && this.current.unlabeled > 0; },
    canAccept () { return this.current && !this.unlabeled && this.scannedCount === this.current.objects.filter(o => !o.isExternal).length; }
  },
  created () {
    this.load();
    this.$api.pathDictionaries().then(d => { this.reasons = d.rejectReasons.map(r => ({ label: r.name, value: r.code })); }).catch(() => {});
  },
  methods: {
    sourceIcon (s) { return { INPATIENT: 'fas fa-procedures', POLYCLINIC: 'fas fa-clinic-medical', EHEALTH: 'fas fa-globe', PARTNER: 'fas fa-handshake' }[s] || 'description'; },
    async load () {
      const res = await this.callApi(() => this.$api.pathIncoming());
      if (res) {
        this.list = res;
        if (this.current) this.current = res.find(r => r.id === this.current.id) || null;
        if (!this.current && res.length) this.select(res[0]);
      }
    },
    select (r) { this.current = r; this.scanned = {}; this.scan = ''; },
    markScanned (code) { this.$set(this.scanned, code, true); },
    doScan () {
      const code = (this.scan || '').trim();
      const o = this.current && this.current.objects.find(x => x.barcode === code || x.mark === code);
      if (o) this.markScanned(o.barcode); else this.$q.notify({ type: 'warning', message: `Контейнер ${code} не належить цьому направленню` });
      this.scan = '';
    },
    async accept () {
      const c = await this.callApi(() => this.$api.pathAccept(this.current.id, Object.keys(this.scanned)));
      if (c) {
        this.$q.notify({ type: 'positive', message: `Прийнято: справа ${c.caseNo}, строк до ${this.$options.filters.datetime(c.slaDueAt)}` });
        this.current = null;
        await this.load();
        this.$store.dispatch('laboratory/refreshStatus').catch(() => {});
      }
    },
    async doReject () {
      const r = await this.callApi(() => this.$api.pathReject(this.current.id, this.reject), { success: 'Відмову зафіксовано, МІС отримала REJECTED' });
      if (r) { this.rejectOpen = false; this.reject = { reason: null, comment: '' }; this.current = null; await this.load(); }
    },
    async doQuery () {
      const r = await this.callApi(() => this.$api.pathQuery(this.current.id, this.queryText), { success: 'Запит надіслано направнику (QUERY)' });
      if (r) { this.queryOpen = false; this.queryText = ''; }
    }
  }
};
</script>

<style scoped>
.form014 td:first-child { width: 190px; color: #5f6b7a; white-space: nowrap; }
</style>
