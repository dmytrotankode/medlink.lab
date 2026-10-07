<template>
  <q-table
    ref="table"
    :data="rows"
    :columns="columns"
    row-key="orderTestId"
    dense
    flat
    virtual-scroll
    :virtual-scroll-sticky-size-start="28"
    :virtual-scroll-item-size="50"
    :pagination="{ rowsPerPage: 0 }"
    :rows-per-page-options="[0]"
    hide-pagination
    :loading="loading"
    :selected.sync="selectedProxy"
    selection="multiple"
    :style="{ height: height }"
    class="worklist-table sticky-header"
    no-data-label="Рядків робочого листа немає"
    data-testid="worklistTable"
  >
    <template v-slot:body="props">
      <q-tr
        :props="props"
        :class="rowClass(props.row)"
        class="cursor-pointer"
        :data-testid="`wl-row-${props.row.testCode}`"
        @click="$emit('select', props.row)"
        @dblclick="startEdit(props.row)"
      >
        <q-td auto-width><q-checkbox v-model="props.selected" dense /></q-td>
        <q-td key="barcode" :props="props">
          <span class="mono text-weight-bold">{{ props.row.barcode }}</span>
          <q-badge v-if="props.row.isCito" color="deep-orange-6" label="CITO" class="q-ml-xs" />
        </q-td>
        <q-td key="orderNumber" :props="props">
          <a href="#" class="text-primary" @click.prevent.stop="$emit('open-order', props.row)">{{ props.row.orderNumber }}</a>
        </q-td>
        <q-td key="journalNumber" :props="props">
          <span class="mono">{{ props.row.journalNumber || '—' }}</span>
          <div v-if="props.row.sectionName" class="text-caption text-grey-6">{{ props.row.sectionName }}</div>
        </q-td>
        <q-td key="patient" :props="props">
          <div class="ellipsis" style="max-width: 180px">{{ props.row.patientName }}</div>
          <div class="text-caption text-grey-7">{{ props.row.patientAgeGender }}</div>
        </q-td>
        <q-td key="analyzer" :props="props">
          <span>{{ props.row.analyzerName || 'ручне' }}</span>
          <q-badge v-if="props.row.isLockedOut" color="purple-6" class="q-ml-xs"><q-icon name="lock" size="10px" /> lock</q-badge>
        </q-td>
        <q-td key="test" :props="props">
          <span class="text-weight-bold">{{ props.row.testCode }}</span>
          <span class="text-grey-7 q-ml-xs">{{ props.row.testName }}</span>
        </q-td>
        <q-td key="value" :props="props" class="text-right value-cell" @click.stop>
          <div v-if="editingId === props.row.orderTestId" class="row no-wrap items-center justify-end">
            <q-select
              v-if="isDropdown(props.row)"
              :ref="'edit-' + props.row.orderTestId"
              v-model="editValue"
              dense outlined options-dense
              :options="dropdownOptions(props.row)"
              style="min-width: 150px"
              data-testid="inlineDropdown"
              @keydown.enter.prevent="commitEdit(props.row)"
              @keydown.esc="cancelEdit"
            />
            <q-input
              v-else
              :ref="'edit-' + props.row.orderTestId"
              v-model="editValue"
              dense outlined
              input-class="text-right"
              style="width: 120px"
              data-testid="inlineInput"
              @keydown.enter.prevent="commitEdit(props.row)"
              @keydown.esc="cancelEdit"
              @keydown.tab.prevent="commitEdit(props.row, true)"
            />
            <q-btn flat dense round size="sm" icon="check" color="positive" :loading="saving" @click="commitEdit(props.row)" />
            <q-btn flat dense round size="sm" icon="close" color="grey-7" @click="cancelEdit" />
          </div>
          <div v-else @dblclick.stop="startEdit(props.row)">
            <span :class="valueClass(props.row)">{{ displayValue(props.row) }}</span>
            <span class="text-caption text-grey-7 q-ml-xs">{{ props.row.unit }}</span>
          </div>
        </q-td>
        <q-td key="reference" :props="props">{{ referenceDisplay(props.row) || '—' }}</q-td>
        <q-td key="flag" :props="props" class="text-center"><flag-marker :flag="props.row.flag || 'NONE'" /></q-td>
        <q-td key="delta" :props="props" class="text-right">
          <span :class="props.row.deltaAlert ? 'text-orange-9 text-weight-bold' : 'text-grey-8'">{{ props.row.deltaPercent | pct }}</span>
          <q-icon v-if="props.row.deltaAlert" name="trending_up" color="orange-9" size="14px"><q-tooltip>Delta-check: попереднє {{ props.row.previousValue | num }} ({{ props.row.previousAt | datetime }})</q-tooltip></q-icon>
        </q-td>
        <q-td key="status" :props="props"><status-chip :value="props.row.status" type="test" /></q-td>
        <q-td key="actions" :props="props" class="text-right no-wrap" @click.stop>
          <q-btn flat dense round size="sm" icon="edit_note" color="primary" @click="startEdit(props.row)"><q-tooltip>Ввести результат (Enter — зберегти, Esc — скасувати)</q-tooltip></q-btn>
          <q-btn flat dense round size="sm" icon="how_to_reg" color="green-8" :disable="!['RESULTED', 'NEEDS_REVIEW'].includes(props.row.status)" @click="$emit('verify', props.row)"><q-tooltip>Верифікувати</q-tooltip></q-btn>
          <q-btn flat dense round size="sm" icon="replay" color="purple-6" @click="$emit('rerun', props.row)"><q-tooltip>Повтор</q-tooltip></q-btn>
          <q-btn flat dense round size="sm" icon="block" color="negative" @click="$emit('reject', props.row)"><q-tooltip>Відхилити</q-tooltip></q-btn>
          <q-btn flat dense round size="sm" icon="delete" color="grey-7" :disable="!props.row.resultId && props.row.value === undefined" @click="$emit('delete', props.row)"><q-tooltip>Видалити результат</q-tooltip></q-btn>
          <q-btn flat dense round size="sm" icon="biotech" color="teal-7" :disable="!props.row.barcode" :to="props.row.barcode ? { name: 'lab-sample-processing', params: { barcode: props.row.barcode } } : undefined"><q-tooltip>Обробка зразка</q-tooltip></q-btn>
        </q-td>
      </q-tr>
    </template>
  </q-table>
</template>

<script>
import { flagMeta } from '../../../utils/statuses';
import { referenceDisplay, formatNumber, parseDecimal } from '../../../utils/format';

export default {
  name: 'WorklistTable',
  props: {
    rows: { type: Array, default: () => [] },
    loading: { type: Boolean, default: false },
    selected: { type: Array, default: () => [] },
    selectedId: { type: String, default: null },
    height: { type: String, default: '52vh' }
  },
  data () {
    return {
      editingId: null,
      editValue: '',
      saving: false,
      columns: [
        { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left', sortable: true },
        { name: 'orderNumber', label: '№ замовл.', field: 'orderNumber', align: 'left', sortable: true },
        { name: 'journalNumber', label: '№ журналу', field: 'journalNumber', align: 'left', sortable: true },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left', sortable: true },
        { name: 'analyzer', label: 'Аналізатор', field: 'analyzerName', align: 'left', sortable: true },
        { name: 'test', label: 'Тест', field: 'testCode', align: 'left', sortable: true },
        { name: 'value', label: 'Результат', field: 'value', align: 'right', sortable: true },
        { name: 'reference', label: 'Норма', align: 'left' },
        { name: 'flag', label: 'Прапорець', field: 'flag', align: 'center', sortable: true },
        { name: 'delta', label: 'Δ%', field: 'deltaPercent', align: 'right', sortable: true },
        { name: 'status', label: 'Статус', field: 'status', align: 'left', sortable: true },
        { name: 'actions', label: 'Дії', align: 'right' }
      ]
    };
  },
  computed: {
    selectedProxy: {
      get () { return this.selected; },
      set (v) { this.$emit('update:selected', v); }
    },
    tests () { return this.$store.getters['dictionaries/items']('tests'); }
  },
  methods: {
    referenceDisplay,
    rowClass (row) {
      const cls = [];
      if (row.orderTestId === this.selectedId) cls.push('row-selected');
      if (row.flag === 'CRIT_LOW' || row.flag === 'CRIT_HIGH') cls.push('bg-red-1');
      else if (row.status === 'VERIFIED' || row.status === 'AUTO_VERIFIED') cls.push('row-status--closed');
      else if (row.status === 'REJECTED') cls.push('row-status--rejected');
      if (row.isCito) cls.push('row-status--cito');
      return cls.join(' ');
    },
    testDef (row) { return this.tests.find(t => t.code === row.testCode) || null; },
    isDropdown (row) { return row.resultType === 'DROPDOWN' || (this.testDef(row) && this.testDef(row).resultType === 'DROPDOWN'); },
    dropdownOptions (row) { return row.dropdownOptions || (this.testDef(row) && this.testDef(row).dropdownOptions) || ['Позитивний', 'Негативний', 'Сумнівний']; },
    valueClass (row) { return (row.value === null || row.value === undefined || row.value === '') ? 'val-empty' : flagMeta(row.flag || 'NONE').css + ' text-weight-bold'; },
    displayValue (row) {
      const v = row.value;
      if (v === null || v === undefined || v === '') return '—';
      return isNaN(Number(v)) ? v : formatNumber(v);
    },
    startEdit (row) {
      if (['VERIFIED', 'AUTO_VERIFIED', 'REJECTED'].includes(row.status)) {
        this.$q.notify({ type: 'info', message: 'Результат верифіковано/відхилено — для зміни скористайтесь «Повернути в роботу» (reopen) у картці замовлення' });
        return;
      }
      this.editingId = row.orderTestId;
      this.editValue = row.value === null || row.value === undefined ? '' : (isNaN(Number(row.value)) ? row.value : String(row.value).replace('.', ','));
      this.$emit('select', row);
      this.$nextTick(() => {
        const ref = this.$refs['edit-' + row.orderTestId];
        const el = Array.isArray(ref) ? ref[0] : ref;
        if (el && el.focus) el.focus();
        if (el && el.select) el.select();
      });
    },
    cancelEdit () { this.editingId = null; this.editValue = ''; },
    async commitEdit (row, moveNext) {
      const body = {};
      if (this.isDropdown(row) || (row.resultType === 'TEXT')) {
        if (!this.editValue) return;
        body.stringValue = this.editValue;
      } else {
        const n = parseDecimal(this.editValue);
        if (n === null) {
          if (!this.editValue) return;
          body.stringValue = this.editValue; // текстовий результат
        } else body.numericValue = n;
      }
      this.saving = true;
      try {
        const res = await this.$api.saveResult(row.orderTestId, body);
        this.$emit('saved', { row, result: res });
        this.editingId = null;
        if (moveNext) {
          const idx = this.rows.findIndex(r => r.orderTestId === row.orderTestId);
          const next = this.rows.slice(idx + 1).find(r => !['VERIFIED', 'AUTO_VERIFIED', 'REJECTED'].includes(r.status));
          if (next) this.startEdit(next);
        }
      } catch (e) {
        this.$q.notify({ type: 'negative', message: e.userMessage || 'Не вдалося зберегти результат', timeout: 4000 });
      } finally {
        this.saving = false;
      }
    },
    /** Викликається батьком: Enter на вибраному рядку */
    editSelected () {
      const row = this.rows.find(r => r.orderTestId === this.selectedId);
      if (row) this.startEdit(row);
    }
  }
};
</script>

<style lang="stylus">
.worklist-table
  .q-table__top, .q-table__bottom, thead tr:first-child th
    background-color #f8fafc
  thead tr th
    position sticky
    z-index 1
  thead tr:first-child th
    top 0
  &.q-table--loading thead tr:last-child th
    top 28px
  .value-cell
    min-width 150px
</style>
