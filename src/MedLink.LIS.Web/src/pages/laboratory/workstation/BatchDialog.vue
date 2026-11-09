<template>
  <q-dialog :value="value" @input="$emit('input', $event)" no-backdrop-dismiss>
    <q-card style="min-width: 560px; max-width: 96vw" data-testid="batchDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="playlist_add" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Робочий лист</div>
        <q-space /><q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section class="q-gutter-y-sm">
        <q-tabs v-model="tab" dense align="left" active-color="primary" class="text-grey-8">
          <q-tab name="create" label="Новий" />
          <q-tab name="list" :label="`Створені (${batches.length})`" />
        </q-tabs>
        <q-tab-panels v-model="tab" animated>
          <q-tab-panel name="create" class="q-px-none">
            <q-select v-model="analyzerId" outlined dense clearable use-input input-debounce="0" label="Аналізатор (порожньо — ручна постановка)" :options="analyzerFiltered" emit-value map-options @filter="filterAnalyzers" />
            <q-input v-model="batchCode" outlined dense label="Код (необов’язково)" class="q-mt-sm" />
            <div class="text-caption q-mt-sm">Обрано рядків: <b>{{ items.length }}</b> — {{ items.map(i => i.testCode).slice(0, 12).join(', ') }}<span v-if="items.length > 12">…</span></div>
            <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
          </q-tab-panel>
          <q-tab-panel name="list" class="q-px-none">
            <q-table :data="batches" :columns="columns" dense flat row-key="id" hide-pagination :pagination="{ rowsPerPage: 0 }" no-data-label="Робочих листів ще немає">
              <template v-slot:body-cell-status="props"><q-td :props="props"><status-chip :value="props.row.status" type="batch" /></q-td></template>
              <template v-slot:body-cell-actions="props">
                <q-td :props="props" class="text-right">
                  <q-btn flat dense round size="sm" icon="print" color="primary" aria-label="Друк робочого листа" type="a" :href="$api.batchPrintUrl(props.row.id)" target="_blank"><q-tooltip>Друк робочого листа</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Видалити робочий лист" @click="askDelete(props.row)"><q-tooltip>Видалити</q-tooltip></q-btn>
                </q-td>
              </template>
            </q-table>
          </q-tab-panel>
        </q-tab-panels>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Закрити" v-close-popup />
        <q-btn v-if="tab === 'create'" color="primary" icon="save" label="Створити й надрукувати" :loading="saving" :disable="!items.length || !canDo('LAB_DOCTOR', 'LAB_TECHNICIAN')" @click="create"><q-tooltip v-if="!canDo('LAB_DOCTOR', 'LAB_TECHNICIAN')">{{ roleHint('LAB_DOCTOR', 'LAB_TECHNICIAN') }}</q-tooltip></q-btn>
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import roleMixin from '../../../mixins/roleMixin';

export default {
  name: 'BatchDialog',
  mixins: [roleMixin],
  props: { value: Boolean, items: { type: Array, default: () => [] } },
  data () {
    return {
      tab: 'create', analyzerId: null, batchCode: '', saving: false, error: '', batches: [], analyzerNeedle: '',
      columns: [
        { name: 'batchCode', label: 'Код', field: 'batchCode', align: 'left' },
        { name: 'analyzer', label: 'Аналізатор', field: r => r.analyzerName || 'Вручну', align: 'left' },
        { name: 'count', label: 'Показників', field: r => (r.items || []).length || r.itemsCount || 0, align: 'center' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    // Лише назва прилада; технічний код на екран не виводимо
    analyzerOptions () { return this.$store.state.laboratory.analyzers.map(a => ({ value: a.id, label: a.name })); },
    analyzerFiltered () {
      const n = this.analyzerNeedle.trim().toLowerCase();
      return n ? this.analyzerOptions.filter(o => o.label.toLowerCase().includes(n)) : this.analyzerOptions;
    }
  },
  watch: {
    value (v) { if (v) { this.error = ''; this.tab = this.items.length ? 'create' : 'list'; this.loadBatches(); } }
  },
  methods: {
    filterAnalyzers (val, update) { update(() => { this.analyzerNeedle = val || ''; }); },
    async loadBatches () {
      try { const res = await this.$api.getBatches(); this.batches = Array.isArray(res) ? res : (res && res.items) || []; } catch (e) { this.batches = []; }
    },
    async create () {
      this.saving = true; this.error = '';
      try {
        const res = await this.$api.createBatch({ analyzerId: this.analyzerId, batchCode: this.batchCode || null, orderTestIds: this.items.map(i => i.orderTestId), items: this.items.map(i => i.orderTestId) });
        this.$q.notify({ type: 'positive', message: `Робочий лист ${res && res.batchCode ? res.batchCode : ''} створено` });
        if (res && res.id) window.open(this.$api.batchPrintUrl(res.id), '_blank');
        this.$emit('created', res);
        this.loadBatches();
        this.tab = 'list';
      } catch (e) { this.error = e.userMessage || 'Не вдалося створити робочий лист'; } finally { this.saving = false; }
    },
    askDelete (b) {
      this.$q.dialog({ title: 'Видалити робочий лист?', message: `Робочий лист ${b.batchCode || ''} буде видалено. Результати показників не зміняться.`, cancel: { label: 'Скасувати', flat: true }, ok: { label: 'Видалити', color: 'negative' }, focus: 'cancel' })
        .onOk(() => this.deleteBatch(b));
    },
    async deleteBatch (b) {
      try { await this.$api.deleteBatch(b.id); this.$q.notify({ type: 'positive', message: 'Робочий лист видалено' }); this.loadBatches(); } catch (e) { this.$q.notify({ type: 'negative', message: e.userMessage || 'Не вдалося видалити робочий лист' }); }
    }
  }
};
</script>
