<template>
  <div class="q-pa-md workstation-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-microscope" class="q-mr-sm" />
          Робочий стіл лаборанта (Журнал досліджень)
        </h5>
        <div class="text-caption text-grey-7">
          Аналітичний етап. Прийом результатів з аналізаторів, ручне введення мікроскопії, дельта-чек та автоматична валідація.
        </div>
      </div>
      <div class="row q-gutter-sm">
        <q-btn color="secondary" icon="refresh" label="Оновити" dense flat @click="refreshData" />
        <q-btn color="teal" icon="check_circle" label="Автовалідація норм" @click="runAutoValidation" />
      </div>
    </div>

    <!-- Filters Bar -->
    <q-card flat bordered class="q-mb-md bg-grey-1">
      <q-card-section class="q-pa-sm row q-col-gutter-sm items-center">
        <div class="col-12 col-md-3">
          <q-input v-model="searchQuery" outlined dense placeholder="Штрихкод або показник..." bg-color="white">
            <template v-slot:append><q-icon name="search" /></template>
          </q-input>
        </div>
        <div class="col-6 col-md-3">
          <q-select v-model="analyzerFilter" :options="['Всі прилади', 'Sysmex XN-1000', 'Roche Cobas e411', 'Mindray BS-240']" outlined dense bg-color="white" />
        </div>
        <div class="col-6 col-md-3">
          <q-select v-model="flagFilter" :options="['Всі результати', 'Тільки паніка та відхилення', 'Нормальні']" outlined dense bg-color="white" />
        </div>
        <div class="col-12 col-md-3 text-right">
          <q-chip outline color="negative" icon="warning">Паніка: {{ panicCount }}</q-chip>
          <q-chip outline color="primary" icon="assignment">Очікує: {{ pendingCount }}</q-chip>
        </div>
      </q-card-section>
    </q-card>

    <!-- Worklist Results Table -->
    <q-card flat bordered>
      <q-table
        :data="filteredResults"
        :columns="worklistColumns"
        row-key="id"
        dense
        flat
        :pagination.sync="pagination"
      >
        <template v-slot:body-cell-value="props">
          <q-td :props="props" :class="getValueClass(props.row.flag)">
            {{ props.row.value }} {{ props.row.unit }}
          </q-td>
        </template>

        <template v-slot:body-cell-norm="props">
          <q-td :props="props">
            {{ props.row.normMin }} - {{ props.row.normMax }}
          </q-td>
        </template>

        <template v-slot:body-cell-flag="props">
          <q-td :props="props">
            <q-badge :color="getFlagColor(props.row.flag)">
              {{ formatFlag(props.row.flag) }}
            </q-badge>
          </q-td>
        </template>

        <template v-slot:body-cell-deltaPercent="props">
          <q-td :props="props" :class="props.row.deltaPercent.includes('+185') ? 'text-negative text-weight-bold' : ''">
            {{ props.row.deltaPercent }}
          </q-td>
        </template>

        <template v-slot:body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="props.row.status === 'AUTO_VERIFIED' ? 'positive' : 'warning'" :text-color="props.row.status === 'AUTO_VERIFIED' ? 'white' : 'dark'">
              {{ props.row.status === 'AUTO_VERIFIED' ? 'Авто-валідовано' : 'Очікує лікаря' }}
            </q-badge>
          </q-td>
        </template>

        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="q-gutter-xs">
            <q-btn size="sm" color="primary" dense icon="edit" @click="editResult(props.row)">
              <q-tooltip>Коригувати / Коментар</q-tooltip>
            </q-btn>
            <q-btn size="sm" color="secondary" dense icon="replay" @click="requestRerun(props.row)">
              <q-tooltip>Замовити перезапуск (Rerun)</q-tooltip>
            </q-btn>
          </q-td>
        </template>
      </q-table>
    </q-card>

    <!-- Edit Result Dialog -->
    <q-dialog v-model="showEditDialog">
      <q-card style="min-width: 450px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="edit" class="q-mr-sm" /> Редагування результату
          </div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md" v-if="editingResult">
          <div class="text-subtitle2 q-mb-xs">{{ editingResult.testName }} ({{ editingResult.testCode }})</div>
          <div class="text-caption text-grey-7 q-mb-md">Пацієнт: {{ editingResult.patientName }} | Штрихкод: {{ editingResult.barcode }}</div>
          
          <q-input v-model.number="editingResult.value" type="number" step="0.01" label="Значення показника" outlined dense class="q-mb-md" />
          <q-input v-model="editingResult.comment" label="Коментар лаборанта" outlined dense class="q-mb-md" />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" label="Зберегти" @click="saveEditedResult" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import { mockWorklistResults } from '../../services/mockData';

export default {
  name: 'LabWorkstation',
  data() {
    return {
      searchQuery: '',
      analyzerFilter: 'Всі прилади',
      flagFilter: 'Всі результати',
      results: mockWorklistResults,
      editingResult: null,
      showEditDialog: false,
      pagination: { rowsPerPage: 10 },
      worklistColumns: [
        { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left', sortable: true },
        { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left', sortable: true },
        { name: 'analyzer', label: 'Аналізатор', field: 'analyzer', align: 'left' },
        { name: 'testName', label: 'Тест', field: 'testName', align: 'left' },
        { name: 'value', label: 'Результат', field: 'value', align: 'right', sortable: true },
        { name: 'norm', label: 'Референсна норма', align: 'center' },
        { name: 'flag', label: 'Флаг', field: 'flag', align: 'center' },
        { name: 'deltaPercent', label: 'Delta-Check', field: 'deltaPercent', align: 'right' },
        { name: 'status', label: 'Статус', field: 'status', align: 'center' },
        { name: 'actions', label: 'Дії', align: 'center' }
      ]
    };
  },
  computed: {
    panicCount() {
      return this.results.filter(r => r.flag === 'PANIC_HIGH' || r.flag === 'PANIC_LOW').length;
    },
    pendingCount() {
      return this.results.filter(r => r.status === 'PENDING_VERIFY').length;
    },
    filteredResults() {
      return this.results.filter(r => {
        const matchesQuery = !this.searchQuery ||
          r.testName.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
          r.barcode.includes(this.searchQuery) ||
          r.patientName.toLowerCase().includes(this.searchQuery.toLowerCase());
        const matchesAnalyzer = this.analyzerFilter === 'Всі прилади' || r.analyzer.includes(this.analyzerFilter);
        const matchesFlag = this.flagFilter === 'Всі результати' ||
          (this.flagFilter.includes('паніка') && r.flag !== 'NORMAL') ||
          (this.flagFilter === 'Нормальні' && r.flag === 'NORMAL');
        return matchesQuery && matchesAnalyzer && matchesFlag;
      });
    }
  },
  methods: {
    getValueClass(flag) {
      if (flag === 'PANIC_HIGH' || flag === 'PANIC_LOW') return 'text-negative text-weight-bolder bg-red-1';
      if (flag === 'DELTA_ALERT') return 'text-deep-orange text-weight-bold';
      return 'text-dark';
    },
    getFlagColor(flag) {
      switch (flag) {
        case 'PANIC_HIGH':
        case 'PANIC_LOW': return 'negative';
        case 'DELTA_ALERT': return 'deep-orange';
        case 'NORMAL': return 'positive';
        default: return 'grey';
      }
    },
    formatFlag(flag) {
      switch (flag) {
        case 'PANIC_HIGH': return 'КРИТИЧНО ВИСОКИЙ';
        case 'PANIC_LOW': return 'КРИТИЧНО НИЗЬКИЙ';
        case 'DELTA_ALERT': return 'DELTA УВАГА';
        case 'NORMAL': return 'НОРМА';
        default: return flag;
      }
    },
    editResult(r) {
      this.editingResult = { ...r };
      this.showEditDialog = true;
    },
    saveEditedResult() {
      const idx = this.results.findIndex(item => item.id === this.editingResult.id);
      if (idx !== -1) {
        this.results.splice(idx, 1, this.editingResult);
      }
      this.showEditDialog = false;
      this.$q.notify({ type: 'positive', message: 'Результат збережено' });
    },
    requestRerun(r) {
      this.$q.notify({ type: 'info', message: `Запит на перезапуск для ${r.testCode} відправлено в чергу аналізатора.` });
    },
    runAutoValidation() {
      let count = 0;
      this.results.forEach(r => {
        if (r.flag === 'NORMAL' && r.status === 'PENDING_VERIFY') {
          r.status = 'AUTO_VERIFIED';
          count++;
        }
      });
      this.$q.notify({ type: 'positive', message: `Автоматично валідовано ${count} нормальних результатів за критеріями CLSI.` });
    },
    refreshData() {
      this.$q.notify({ type: 'info', message: 'Журнал досліджень оновлено.' });
    }
  }
};
</script>
<style scoped>
.workstation-page { background: #f7f9fa; min-height: 100%; }
</style>
