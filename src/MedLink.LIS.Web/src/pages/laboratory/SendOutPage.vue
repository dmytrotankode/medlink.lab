<template>
  <div data-testid="sendOutPage">
    <page-header title="Зовнішні лабораторії" icon="fas fa-share-square" subtitle="Тести, які виконують партнерські лабораторії: черга → реєстр → відправка → приймання → результати (ручний ввід, файл, PDF-бланк) → верифікація" :breadcrumbs="[{ label: 'Зовнішні лабораторії' }]">
      <q-select v-model="performerId" dense outlined clearable emit-value map-options :options="performerOptions" label="Лабораторія" style="min-width: 260px" class="q-mr-sm" />
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="loadAll" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="loadAll" />

    <div class="medlink-card">
      <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8">
        <q-tab name="queue" icon="playlist_add" :label="`До відправки (${queue.length})`" />
        <q-tab name="batches" icon="local_shipping" :label="`Реєстри (${openBatches})`" />
        <q-tab name="performers" icon="apartment" :label="`Лабораторії (${performers.length})`" />
      </q-tabs>
      <q-separator />

      <!-- Черга -->
      <div v-show="tab === 'queue'" class="q-pa-sm">
        <div class="row items-center q-gutter-sm q-mb-sm">
          <q-btn unelevated color="primary" icon="post_add" label="Сформувати реєстр" :disable="!canCreateBatch" data-testid="createBatch" @click="createBatch" />
          <span v-if="selected.length && !sameLab" class="text-negative text-caption">Оберіть тести однієї лабораторії</span>
          <span v-else-if="selected.some(r => !r.sampleReady)" class="text-negative text-caption">Є проби, ще не прийняті в лабораторії</span>
          <span v-else class="text-grey-7 text-caption">Тести потрапляють у чергу за маршрутом довідника або після ручного перенаправлення в картці замовлення</span>
        </div>
        <q-table :data="queue" :columns="queueColumns" row-key="orderTestId" dense flat selection="multiple" :selected.sync="selected" :pagination="{ rowsPerPage: 30 }" no-data-label="Черга порожня">
          <template v-slot:body-cell-order="props">
            <q-td :props="props"><router-link :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" class="text-primary">{{ props.row.orderNumber }}</router-link> <q-badge v-if="props.row.isCito" color="deep-orange-6" label="CITO" /></q-td>
          </template>
          <template v-slot:body-cell-sample="props">
            <q-td :props="props"><span class="mono">{{ props.row.barcode || '—' }}</span> <q-icon :name="props.row.sampleReady ? 'check_circle' : 'hourglass_empty'" :color="props.row.sampleReady ? 'positive' : 'grey-6'"><q-tooltip>{{ props.row.sampleReady ? 'Проба в лабораторії' : 'Проба ще не прийнята' }}</q-tooltip></q-icon></q-td>
          </template>
        </q-table>
      </div>

      <!-- Реєстри -->
      <div v-show="tab === 'batches'" class="q-pa-sm">
        <q-list separator>
          <q-expansion-item v-for="b in batches" :key="b.id" group="batches" :data-testid="`batch-${b.number}`" @show="current = b.id">
            <template v-slot:header>
              <q-item-section avatar><q-icon name="local_shipping" :color="b.itemsOverdue ? 'negative' : 'primary'" /></q-item-section>
              <q-item-section>
                <q-item-label><b class="mono">{{ b.number }}</b> · {{ b.performerName }} <span v-if="b.externalBatchNumber" class="text-grey-7">· № {{ b.externalBatchNumber }}</span></q-item-label>
                <q-item-label caption>{{ fmt(b.createdAt) }} · позицій {{ b.itemsTotal }}, отримано {{ b.itemsResulted }}<span v-if="b.itemsOverdue" class="text-negative"> · прострочено {{ b.itemsOverdue }}</span></q-item-label>
              </q-item-section>
              <q-item-section side><q-badge :color="statusColor(b.status)" :label="statusLabel(b.status)" /></q-item-section>
            </template>
            <div class="q-pa-sm bg-grey-1">
              <div class="row q-gutter-sm q-mb-sm">
                <q-btn v-if="b.status === 'CREATED'" dense unelevated color="primary" icon="send" label="Відправити" @click="dispatch(b)" />
                <q-btn v-if="b.status === 'CREATED'" dense flat color="negative" icon="cancel" label="Скасувати" @click="cancelBatch(b)" />
                <q-btn v-if="b.status === 'DISPATCHED'" dense unelevated color="teal-6" icon="how_to_reg" label="Прийнято лабораторією" @click="accept(b)" />
                <q-btn v-if="['DISPATCHED', 'ACCEPTED', 'PARTIAL'].includes(b.status)" dense outline color="primary" icon="upload_file" label="Імпорт файлу результатів" @click="pickImport(b)" />
                <q-btn dense flat color="grey-8" icon="print" label="Друк реєстру" @click="printBatch(b)" />
              </div>
              <q-markup-table dense flat bordered>
                <thead><tr><th class="text-left">Замовлення</th><th class="text-left">Пацієнт</th><th class="text-left">Проба</th><th class="text-left">Тест</th><th class="text-left">Код у лабораторії</th><th class="text-left">№ у лабораторії</th><th class="text-left">Термін</th><th class="text-left">Результат</th><th class="text-left">Статус</th><th></th></tr></thead>
                <tbody>
                  <tr v-for="i in b.items" :key="i.id" :class="i.isOverdue ? 'bg-red-1' : ''">
                    <td><router-link :to="{ name: 'lab-order-card', params: { id: i.orderId } }" class="text-primary">{{ i.orderNumber }}</router-link></td>
                    <td>{{ i.patientName }}</td>
                    <td class="mono">{{ i.barcode }}</td>
                    <td><b>{{ i.testCode }}</b> <span class="text-grey-7">{{ i.testName }}</span></td>
                    <td>{{ i.externalCode || '—' }}</td>
                    <td>{{ i.externalOrderNumber || '—' }}</td>
                    <td>{{ fmt(i.dueAt) }}</td>
                    <td>{{ resultText(i) }}</td>
                    <td><q-badge :color="itemColor(i.status)" :label="itemLabel(i.status)" /><div v-if="i.rejectReason" class="text-caption text-negative">{{ i.rejectReason }}</div></td>
                    <td class="text-right no-wrap">
                      <q-btn v-if="['SENT', 'ACCEPTED'].includes(i.status)" flat dense round size="sm" icon="edit_note" color="primary" @click="openResult(b, i)"><q-tooltip>Внести результат</q-tooltip></q-btn>
                      <q-btn v-if="['SENT', 'ACCEPTED'].includes(i.status)" flat dense round size="sm" icon="report" color="negative" @click="rejectItem(i)"><q-tooltip>Відмова лабораторії</q-tooltip></q-btn>
                      <q-btn flat dense round size="sm" icon="attach_file" color="grey-8" @click="pickPdf(b, i)"><q-tooltip>Завантажити PDF-бланк</q-tooltip></q-btn>
                    </td>
                  </tr>
                </tbody>
              </q-markup-table>
            </div>
          </q-expansion-item>
        </q-list>
        <empty-state v-if="!batches.length" title="Реєстрів немає" hint="Сформуйте реєстр на вкладці «До відправки»" icon="local_shipping" />
      </div>

      <!-- Лабораторії -->
      <div v-show="tab === 'performers'" class="q-pa-sm">
        <q-list separator>
          <q-expansion-item v-for="p in performers" :key="p.id" group="perf">
            <template v-slot:header>
              <q-item-section avatar><q-icon :name="p.kind === 'INTERNAL' ? 'home' : 'apartment'" :color="p.kind === 'INTERNAL' ? 'positive' : 'primary'" /></q-item-section>
              <q-item-section>
                <q-item-label><b>{{ p.name }}</b> <span class="text-grey-7">({{ p.code }})</span></q-item-label>
                <q-item-label caption>{{ p.kind === 'INTERNAL' ? 'Власна лабораторія' : 'Зовнішня лабораторія' }} · обмін: {{ modeLabel(p.exchangeMode) }} · TAT {{ p.defaultTatHours }} год<span v-if="p.contractNumber"> · договір {{ p.contractNumber }}</span></q-item-label>
              </q-item-section>
              <q-item-section side><q-badge v-if="p.medlinkProvider" color="purple-6" :label="`інтеграція MedLink: ${p.medlinkProvider}`" /></q-item-section>
            </template>
            <div class="q-pa-sm bg-grey-1">
              <div v-if="p.reportNote" class="text-caption q-mb-sm"><q-icon name="info" /> На бланку: {{ p.reportNote }}</div>
              <q-markup-table v-if="p.tests.length" dense flat bordered>
                <thead><tr><th class="text-left">Показник</th><th class="text-left">Код / назва в прайсі</th><th class="text-right">Вартість</th><th class="text-right">TAT, год</th><th class="text-left">Маршрут</th><th class="text-left">Вимоги до зразка</th></tr></thead>
                <tbody>
                  <tr v-for="t in p.tests" :key="t.id">
                    <td><b>{{ t.testCode }}</b> {{ t.testName }}</td>
                    <td>{{ t.externalCode }} {{ t.externalName }}</td>
                    <td class="text-right">{{ t.cost | money }}</td>
                    <td class="text-right">{{ t.tatHours || p.defaultTatHours }}</td>
                    <td><q-badge :color="t.isDefaultRoute ? 'primary' : 'grey-5'" :label="t.isDefaultRoute ? 'за замовчуванням' : 'вручну'" /></td>
                    <td class="text-caption">{{ t.specimenRequirements }}</td>
                  </tr>
                </tbody>
              </q-markup-table>
              <div v-else class="text-caption text-grey-7">{{ p.medlinkProvider ? 'Направлення оформлюються в МІС MedLink; ЛІС лише фіксує виконавця.' : 'Прайс не заповнено' }}</div>
            </div>
          </q-expansion-item>
        </q-list>
      </div>
    </div>

    <!-- Результат зовнішньої лабораторії -->
    <q-dialog v-model="resultOpen">
      <q-card style="width: 460px">
        <q-card-section class="text-subtitle1 text-weight-bold">Результат: {{ resultItem && resultItem.testCode }} · {{ resultItem && resultItem.patientName }}</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input v-model="resultForm.value" outlined dense label="Значення *" autofocus data-testid="extValue" />
          <q-input v-model="resultForm.unit" outlined dense label="Одиниці (як у бланку)" />
          <q-input v-model="resultForm.referenceText" outlined dense label="Референс зовнішньої лабораторії" hint="Використовується, якщо в ЛІС немає норм для показника" />
          <q-input v-model="resultForm.externalReference" outlined dense label="№ бланка / замовлення в лабораторії" />
          <q-input v-model="resultForm.comment" outlined dense autogrow label="Коментар" />
          <div class="text-caption text-grey-7"><q-icon name="info" /> Результат зовнішньої лабораторії не автоверифікується — його переглядає й верифікує лікар-лаборант.</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Зберегти" :disable="!resultForm.value" @click="saveResult" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <input ref="importFile" type="file" accept=".csv,.xlsx,.xml" style="display:none" @change="doImport">
    <input ref="pdfFile" type="file" accept=".pdf,image/*" style="display:none" @change="doPdf">
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import { formatDateTime, parseDecimal } from '../../utils/format';

const SO_STATUS = { CREATED: ['Сформовано', 'grey-7'], DISPATCHED: ['Відправлено', 'primary'], ACCEPTED: ['Прийнято лабораторією', 'teal-6'], PARTIAL: ['Частково отримано', 'orange-7'], COMPLETED: ['Завершено', 'positive'], CANCELLED: ['Скасовано', 'grey-5'] };
const ITEM_STATUS = { QUEUED: ['У реєстрі', 'grey-7'], SENT: ['Відправлено', 'primary'], ACCEPTED: ['Прийнято', 'teal-6'], RESULTED: ['Результат', 'positive'], REJECTED: ['Відмова', 'negative'], RECALLED: ['Відкликано', 'grey-5'] };

export default {
  name: 'SendOutPage',
  mixins: [apiMixin],
  data () {
    return {
      tab: 'queue', performerId: null, performers: [], queue: [], batches: [], selected: [], current: null,
      resultOpen: false, resultItem: null, resultForm: {}, importBatch: null, pdfTarget: null,
      queueColumns: [
        { name: 'order', label: '№ замовлення', field: 'orderNumber', align: 'left', sortable: true },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'test', label: 'Тест', field: r => `${r.testCode} — ${r.testName}`, align: 'left' },
        { name: 'performer', label: 'Лабораторія', field: 'performerName', align: 'left' },
        { name: 'ext', label: 'Код у прайсі', field: r => r.externalCode || '—', align: 'left' },
        { name: 'sample', label: 'Проба', field: 'barcode', align: 'left' },
        { name: 'req', label: 'Вимоги до зразка', field: r => r.specimenRequirements || '', align: 'left', classes: 'text-caption' },
        { name: 'tat', label: 'TAT, год', field: 'tatHours', align: 'right' }
      ]
    };
  },
  computed: {
    performerOptions () { return this.performers.filter(p => p.kind === 'EXTERNAL').map(p => ({ value: p.id, label: p.name })); },
    sameLab () { return new Set(this.selected.map(r => r.performerId)).size === 1; },
    canCreateBatch () { return this.selected.length > 0 && this.sameLab && this.selected.every(r => r.sampleReady); },
    openBatches () { return this.batches.filter(b => !['COMPLETED', 'CANCELLED'].includes(b.status)).length; }
  },
  watch: { performerId () { this.loadAll(); } },
  created () { this.loadAll(); },
  methods: {
    fmt: formatDateTime,
    statusLabel (s) { return (SO_STATUS[s] || [s])[0]; },
    statusColor (s) { return (SO_STATUS[s] || [s, 'grey'])[1]; },
    itemLabel (s) { return (ITEM_STATUS[s] || [s])[0]; },
    itemColor (s) { return (ITEM_STATUS[s] || [s, 'grey'])[1]; },
    modeLabel (m) { return { MANUAL: 'ручний', FILE: 'файл результатів', API: 'електронний' }[m] || m; },
    resultText (i) { if (!i.result) return '—'; const v = i.result.numericValue != null ? i.result.numericValue : i.result.stringValue; return `${v} ${i.result.unit || ''}`.trim(); },
    async loadAll () {
      await this.callApi(async () => {
        const [p, q, b] = await Promise.all([this.$api.performers(), this.$api.sendOutQueue(this.performerId), this.$api.sendOuts(null, this.performerId)]);
        this.performers = p; this.queue = q; this.batches = b; this.selected = [];
      }, { silent: false });
    },
    async createBatch () {
      const so = await this.callApi(() => this.$api.createSendOut({ performerId: this.selected[0].performerId, orderTestIds: this.selected.map(r => r.orderTestId) }), { success: 'Реєстр сформовано' });
      if (so) { this.tab = 'batches'; await this.loadAll(); }
    },
    async dispatch (b) {
      this.$q.dialog({ title: `Відправка ${b.number}`, message: 'Температура при відправці, °C (необов’язково)', prompt: { model: '', type: 'text' }, cancel: true })
        .onOk(async t => { await this.callApi(() => this.$api.dispatchSendOut(b.id, { temperature: parseDecimal(t) }), { success: 'Відправлено' }); this.loadAll(); });
    },
    accept (b) {
      this.$q.dialog({ title: `Приймання ${b.number}`, message: 'Номер замовлення / накладної в зовнішній лабораторії', prompt: { model: b.externalBatchNumber || '', type: 'text' }, cancel: true })
        .onOk(async n => { await this.callApi(() => this.$api.acceptSendOut(b.id, { externalBatchNumber: n || null }), { success: 'Приймання підтверджено' }); this.loadAll(); });
    },
    cancelBatch (b) {
      this.$q.dialog({ title: `Скасувати ${b.number}?`, prompt: { model: '', type: 'text', label: 'Причина' }, cancel: true })
        .onOk(async r => { await this.callApi(() => this.$api.cancelSendOut(b.id, r), { success: 'Реєстр скасовано' }); this.loadAll(); });
    },
    rejectItem (i) {
      this.$q.dialog({ title: `Відмова лабораторії: ${i.testCode}`, prompt: { model: '', type: 'text', label: 'Причина (гемоліз, недостатній об’єм…)' }, cancel: true })
        .onOk(async r => { if (!r) return; await this.callApi(() => this.$api.sendOutRejectItem(i.id, r, true), { success: 'Тест повернуто в чергу' }); this.loadAll(); });
    },
    openResult (b, i) { this.resultItem = i; this.resultForm = { value: '', unit: '', referenceText: '', externalReference: i.externalOrderNumber || '', comment: '' }; this.resultOpen = true; },
    async saveResult () {
      const f = this.resultForm; const num = parseDecimal(f.value);
      const body = { numericValue: num, stringValue: num == null ? f.value : null, unit: f.unit || null, referenceText: f.referenceText || null, externalReference: f.externalReference || null, comment: f.comment || null };
      const r = await this.callApi(() => this.$api.sendOutResult(this.resultItem.id, body), { success: 'Результат збережено — очікує верифікації' });
      if (r) { this.resultOpen = false; this.loadAll(); }
    },
    pickImport (b) { this.importBatch = b; this.$refs.importFile.value = ''; this.$refs.importFile.click(); },
    async doImport (e) {
      const file = e.target.files[0]; if (!file) return;
      const fd = () => { const f = new FormData(); f.append('file', file); return f; };
      const dry = await this.callApi(() => this.$api.sendOutImport(this.importBatch.id, fd(), true));
      if (!dry) return;
      this.$q.dialog({ title: 'Імпорт результатів', message: `Рядків: ${dry.total}, зіставлено: ${dry.matched}, помилок: ${dry.errors}. Застосувати?`, cancel: true })
        .onOk(async () => { const res = await this.callApi(() => this.$api.sendOutImport(this.importBatch.id, fd(), false)); if (res) { this.$q.notify({ type: 'positive', message: `Застосовано: ${res.applied}` }); this.loadAll(); } });
    },
    pickPdf (b, i) { this.pdfTarget = { batch: b, item: i }; this.$refs.pdfFile.value = ''; this.$refs.pdfFile.click(); },
    async doPdf (e) {
      const file = e.target.files[0]; if (!file) return;
      const f = new FormData(); f.append('file', file);
      await this.callApi(() => this.$api.uploadAttachment(this.pdfTarget.item.orderId, f, this.pdfTarget.batch.id), { success: 'Бланк завантажено до замовлення' });
    },
    printBatch (b) {
      const rows = b.items.map((i, n) => `<tr><td>${n + 1}</td><td>${i.orderNumber}</td><td>${i.patientName || ''}</td><td>${i.barcode || ''}</td><td>${i.externalCode || ''}</td><td>${i.testCode} ${i.testName}</td></tr>`).join('');
      const w = window.open('', '_blank');
      if (!w) return;
      w.document.write(`<html><head><meta charset="utf-8"><title>${b.number}</title><style>body{font-family:sans-serif;font-size:12px}table{border-collapse:collapse;width:100%}td,th{border:1px solid #999;padding:4px}</style></head><body><h3>Реєстр відправки ${b.number}</h3><p>Виконавець: <b>${b.performerName}</b> · сформовано ${this.fmt(b.createdAt)}${b.dispatchedAt ? ' · відправлено ' + this.fmt(b.dispatchedAt) : ''}</p><table><tr><th>№</th><th>Замовлення</th><th>Пацієнт</th><th>Штрихкод</th><th>Код</th><th>Дослідження</th></tr>${rows}</table><p>Передав: __________________ &nbsp;&nbsp; Прийняв: __________________</p></body></html>`);
      w.document.close(); w.print();
    }
  }
};
</script>
