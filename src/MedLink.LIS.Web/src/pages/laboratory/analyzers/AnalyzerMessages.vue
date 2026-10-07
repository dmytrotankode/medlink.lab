<template>
  <div class="messages-page" data-testid="messagesPage">
    <page-header title="Журнал обміну з аналізаторами" icon="fas fa-exchange-alt" subtitle="Сирі повідомлення ASTM / HL7 / TEXT, напрямок, статус парсингу, кількість результатів" :breadcrumbs="[{ label: 'Аналізатори', to: { name: 'lab-analyzers' } }, { label: 'Журнал обміну' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-toggle v-model="autoRefresh" label="Автооновлення 15 с" dense />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-4"><q-select v-model="analyzerId" dense outlined label="Аналізатор" :options="analyzerOptions" emit-value map-options @input="applyFilter" /></div>
      <div class="col-6 col-md-2"><q-select v-model="direction" dense outlined clearable label="Напрямок" :options="[{ value: 'IN', label: 'Вхідні (IN)' }, { value: 'OUT', label: 'Вихідні (OUT)' }]" emit-value map-options /></div>
      <div class="col-6 col-md-2"><q-select v-model="parsed" dense outlined clearable label="Парсинг" :options="[{ value: true, label: 'Успішний' }, { value: false, label: 'З помилкою' }]" emit-value map-options /></div>
      <div class="col-12 col-md-4"><q-input v-model="search" dense outlined clearable placeholder="Пошук у тексті повідомлення"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
    </div>

    <div class="row q-col-gutter-md">
      <div class="col-12 col-lg-7">
        <div class="medlink-card">
          <q-table :data="filtered" :columns="columns" row-key="id" dense flat :loading="loading" :pagination.sync="pagination" :rows-per-page-options="[25, 50, 100]" @request="onRequest" no-data-label="Повідомлень немає" data-testid="messagesTable">
            <template v-slot:body="props">
              <q-tr :props="props" :class="{ 'row-selected': active && active.id === props.row.id, 'bg-red-1': props.row.parsedOk === false }" class="cursor-pointer" @click="active = props.row">
                <q-td key="receivedAt" :props="props">{{ props.row.receivedAt | datetime }}</q-td>
                <q-td key="analyzer" :props="props">{{ analyzerName(props.row.analyzerId) }}</q-td>
                <q-td key="direction" :props="props"><q-badge :color="props.row.direction === 'IN' ? 'teal-6' : 'indigo-5'" :label="props.row.direction === 'IN' ? '← IN' : 'OUT →'" /></q-td>
                <q-td key="protocol" :props="props">{{ props.row.protocol }}</q-td>
                <q-td key="parsedOk" :props="props" class="text-center"><q-icon :name="props.row.parsedOk ? 'check_circle' : 'error'" :color="props.row.parsedOk ? 'positive' : 'negative'" /></q-td>
                <q-td key="resultsCount" :props="props" class="text-center">{{ props.row.resultsCount }}</q-td>
                <q-td key="preview" :props="props" class="mono text-caption ellipsis" style="max-width: 260px">{{ (props.row.rawText || '').slice(0, 80) }}</q-td>
              </q-tr>
            </template>
          </q-table>
        </div>
      </div>
      <div class="col-12 col-lg-5">
        <div class="medlink-card" data-testid="rawViewer">
          <div class="medlink-card__title"><span>Сирий текст</span><span v-if="active" class="text-caption text-grey-6">{{ active.receivedAt | datetime }} · {{ active.protocol }}</span></div>
          <div v-if="active" class="q-pa-sm">
            <q-banner v-if="active.error" dense rounded class="bg-red-1 text-negative q-mb-sm">{{ active.error }}</q-banner>
            <div class="row q-gutter-xs q-mb-sm">
              <q-btn dense flat size="sm" icon="content_copy" label="Копіювати" @click="copy(active.rawText)" />
              <q-btn dense flat size="sm" icon="science" label="У симулятор" :to="{ name: 'lab-analyzers' }" />
              <q-toggle v-model="showControl" dense label="Показати керуючі символи" size="sm" />
            </div>
            <pre class="mono bg-grey-2 q-pa-sm rounded-borders" style="white-space: pre-wrap; word-break: break-all; font-size: 12px; max-height: 60vh; overflow: auto">{{ rendered }}</pre>
          </div>
          <empty-state v-else title="Оберіть повідомлення" icon="article" />
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import { copyToClipboard } from '../../../utils/format';

const CONTROL = { '\x05': '<ENQ>', '\x06': '<ACK>', '\x15': '<NAK>', '\x02': '<STX>', '\x03': '<ETX>', '\x17': '<ETB>', '\x04': '<EOT>', '\r': '<CR>\n', '\n': '<LF>', '\x0b': '<VT>', '\x1c': '<FS>' };

export default {
  name: 'AnalyzerMessages',
  mixins: [apiMixin],
  data () {
    return {
      rows: [], analyzerId: null, direction: null, parsed: null, search: '', active: null, showControl: true, autoRefresh: false, timer: null,
      pagination: { page: 1, rowsPerPage: 50, rowsNumber: 0 },
      columns: [
        { name: 'receivedAt', label: 'Час', field: 'receivedAt', align: 'left' },
        { name: 'analyzer', label: 'Аналізатор', align: 'left' },
        { name: 'direction', label: 'Напрямок', align: 'left' },
        { name: 'protocol', label: 'Протокол', field: 'protocol', align: 'left' },
        { name: 'parsedOk', label: 'Парсинг', align: 'center' },
        { name: 'resultsCount', label: 'Результатів', align: 'center' },
        { name: 'preview', label: 'Фрагмент', align: 'left' }
      ]
    };
  },
  computed: {
    analyzers () { return this.$store.state.laboratory.analyzers; },
    analyzerOptions () { return this.analyzers.map(a => ({ value: a.id, label: `${a.name} (${a.code})` })); },
    filtered () {
      const s = (this.search || '').toLowerCase();
      return this.rows.filter(r => (!this.direction || r.direction === this.direction) && (this.parsed === null || this.parsed === undefined || !!r.parsedOk === this.parsed) && (!s || (r.rawText || '').toLowerCase().includes(s)));
    },
    rendered () {
      const t = this.active ? (this.active.rawText || '') : '';
      if (!this.showControl) return t;
      return t.replace(/[\x00-\x1f]/g, ch => CONTROL[ch] || `<0x${ch.charCodeAt(0).toString(16).padStart(2, '0')}>`);
    }
  },
  watch: {
    autoRefresh (v) { if (v) this.timer = setInterval(() => this.load(), 15000); else if (this.timer) { clearInterval(this.timer); this.timer = null; } }
  },
  async created () {
    if (!this.analyzers.length) await this.$store.dispatch('laboratory/refreshStatus');
    this.analyzerId = this.$route.query.analyzerId || (this.analyzers[0] && this.analyzers[0].id) || null;
    this.load();
  },
  beforeDestroy () { if (this.timer) clearInterval(this.timer); },
  methods: {
    analyzerName (id) { const a = this.analyzers.find(x => x.id === id); return a ? a.name : (id ? String(id).slice(0, 8) : '—'); },
    copy (t) { copyToClipboard(t || '').then(() => this.notifyOk('Скопійовано')); },
    applyFilter () { this.pagination.page = 1; this.load(); },
    onRequest (props) { this.pagination = { ...this.pagination, ...props.pagination }; this.load(); },
    async load () {
      if (!this.analyzerId) { this.rows = []; return; }
      const res = await this.callApi(() => this.$api.analyzerMessages(this.analyzerId, { page: this.pagination.page, pageSize: this.pagination.rowsPerPage }), { silent: this.autoRefresh });
      if (res === undefined) return;
      this.rows = this.asList(res);
      this.pagination.rowsNumber = this.asTotal(res, this.rows.length);
    }
  }
};
</script>
