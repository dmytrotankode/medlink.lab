<template>
  <q-table
    ref="table"
    :data="rows"
    :columns="visibleColumns"
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
    <template v-slot:no-data="{ message }">
      <div class="full-width row flex-center text-grey-7 q-gutter-sm q-py-md">
        <q-icon name="inbox" size="22px" />
        <span>{{ message }}</span>
      </div>
    </template>
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
          <div v-if="props.row.journalNumber" class="text-caption text-grey-6 mono">{{ props.row.journalNumber }}</div>
        </q-td>
        <q-td key="patient" :props="props">
          <div class="ellipsis" style="max-width: 160px">{{ props.row.patientName }}</div>
          <div class="text-caption text-grey-7">{{ props.row.patientAgeGender }}</div>
        </q-td>
        <q-td key="analyzer" :props="props">
          <span>{{ props.row.analyzerName || 'Вручну' }}</span>
          <q-badge v-if="props.row.isLockedOut" color="purple-6" class="q-ml-xs"><q-icon name="lock" size="10px" class="q-mr-xs" />Блокування ВКЯ</q-badge>
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
              :error="editError"
              hide-bottom-space
              style="min-width: 150px"
              data-testid="inlineDropdown"
              @keydown.enter.prevent="commitEdit(props.row)"
              @keydown.esc="cancelEdit"
            />
            <q-input
              v-else
              :ref="'edit-' + props.row.orderTestId"
              v-model="editValue"
              dense outlined autofocus
              input-class="text-right"
              :error="editError"
              hide-bottom-space
              style="width: 110px"
              data-testid="inlineInput"
              @input="editError = false"
              @keydown.enter.prevent="commitEdit(props.row)"
              @keydown.esc="cancelEdit"
              @keydown.tab.prevent="commitEdit(props.row, true)"
            />
            <span v-if="props.row.unit" class="text-caption text-grey-7 q-ml-xs">{{ props.row.unit }}</span>
            <q-btn flat dense round size="sm" icon="check" color="positive" aria-label="Зберегти результат" :loading="saving" @click="commitEdit(props.row)"><q-tooltip>Зберегти (Enter)</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="close" color="grey-7" aria-label="Скасувати введення" @click="cancelEdit"><q-tooltip>Скасувати (Esc)</q-tooltip></q-btn>
          </div>
          <div v-else @dblclick.stop="startEdit(props.row)">
            <span :class="valueClass(props.row)">{{ displayValue(props.row) }}</span>
            <span class="text-caption text-grey-7 q-ml-xs">{{ props.row.unit }}</span>
          </div>
        </q-td>
        <q-td key="reference" :props="props">{{ referenceDisplay(props.row) || '—' }}</q-td>
        <q-td key="flag" :props="props" class="text-center"><flag-marker :flag="props.row.flag || 'NONE'" /></q-td>
        <q-td v-if="colVisible('delta')" key="delta" :props="props" class="text-right">
          <span :class="props.row.deltaAlert ? 'text-orange-9 text-weight-bold' : 'text-grey-8'">{{ props.row.deltaPercent | pct }}</span>
          <q-icon v-if="props.row.deltaAlert" name="trending_up" color="orange-9" size="14px"><q-tooltip>Контроль змін: попереднє значення {{ props.row.previousValue | num }} ({{ props.row.previousAt | datetime }})</q-tooltip></q-icon>
        </q-td>
        <q-td key="status" :props="props" class="wl-sticky wl-sticky--status"><status-chip :value="props.row.status" type="test" /></q-td>
        <q-td key="actions" :props="props" class="text-right no-wrap wl-sticky wl-sticky--actions" @click.stop>
          <span v-for="a in rowActions(props.row)" :key="a.key" class="inline-block">
            <q-btn
              flat dense round size="sm"
              :icon="a.icon" :color="a.color"
              :disable="a.disabled"
              :aria-label="a.label"
              :to="a.to"
              @click="a.handler && a.handler()"
            />
            <q-tooltip>{{ a.disabled && a.reason ? a.reason : a.label }}</q-tooltip>
          </span>
          <q-btn flat dense round size="sm" icon="more_vert" color="grey-8" aria-label="Інші дії">
            <q-tooltip>Інші дії</q-tooltip>
            <q-menu auto-close>
              <q-list dense style="min-width: 190px">
                <q-item v-for="a in menuActions(props.row)" :key="a.key" :clickable="!a.disabled" :disable="a.disabled" @click="a.handler()">
                  <q-item-section avatar><q-icon :name="a.icon" :color="a.disabled ? 'grey-5' : a.color" size="18px" /></q-item-section>
                  <q-item-section>
                    <q-item-label>{{ a.label }}</q-item-label>
                    <q-item-label v-if="a.disabled && a.reason" caption>{{ a.reason }}</q-item-label>
                  </q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </q-btn>
        </q-td>
      </q-tr>
    </template>
  </q-table>
</template>

<script>
import roleMixin from '../../../mixins/roleMixin';
import { flagMeta } from '../../../utils/statuses';
import { referenceDisplay, formatNumber, parseDecimal } from '../../../utils/format';
import { enterBlockReason, roleBlockReason, VERIFY_ROLES, REJECT_ROLES, DELETE_ROLES } from '../../../utils/resultRules';

const CLOSED = ['VERIFIED', 'AUTO_VERIFIED', 'REJECTED'];

export default {
  name: 'WorklistTable',
  mixins: [roleMixin],
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
      editError: false,
      saving: false,
      allColumns: [
        { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left', sortable: true },
        { name: 'orderNumber', label: '№ замовлення', field: 'orderNumber', align: 'left', sortable: true },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left', sortable: true },
        { name: 'analyzer', label: 'Аналізатор', field: 'analyzerName', align: 'left', sortable: true },
        { name: 'test', label: 'Показник', field: 'testCode', align: 'left', sortable: true },
        { name: 'value', label: 'Результат', field: 'value', align: 'right', sortable: true },
        { name: 'reference', label: 'Норма', align: 'left' },
        { name: 'flag', label: 'Прапорець', field: 'flag', align: 'center', sortable: true },
        { name: 'delta', label: 'Δ%', field: 'deltaPercent', align: 'right', sortable: true },
        { name: 'status', label: 'Статус', field: 'status', align: 'left', sortable: true, headerClasses: 'wl-sticky wl-sticky--status' },
        { name: 'actions', label: 'Дії', align: 'right', headerClasses: 'wl-sticky wl-sticky--actions' }
      ]
    };
  },
  computed: {
    selectedProxy: {
      get () { return this.selected; },
      set (v) { this.$emit('update:selected', v); }
    },
    narrow () { return this.$q.screen.width < 1400; },
    visibleColumns () {
      // На вужчих екранах прибираємо другорядні колонки (деталі є в панелі нижче)
      return this.allColumns.filter(c => !(this.narrow && c.name === 'delta'));
    },
    tests () { return this.$store.getters['dictionaries/items']('tests'); }
  },
  methods: {
    referenceDisplay,
    colVisible (name) { return this.visibleColumns.some(c => c.name === name); },
    rowClass (row) {
      const cls = [];
      if (row.orderTestId === this.selectedId) cls.push('row-selected');
      if (row.flag === 'CRIT_LOW' || row.flag === 'CRIT_HIGH') cls.push('bg-red-1');
      else if (row.status === 'VERIFIED' || row.status === 'AUTO_VERIFIED') cls.push('row-status--closed');
      else if (row.status === 'REJECTED') cls.push('row-status--rejected');
      if (row.isCito) cls.push('row-status--cito');
      return cls.join(' ');
    },
    /** Дії рядка: недоступна ролі або стану дія блокується заздалегідь з поясненням, а не відмовою сервера після кліку */
    rowActions (row) {
      const enterReason = CLOSED.includes(row.status) ? 'Результат верифіковано або відхилено. Для зміни поверніть його в роботу в картці замовлення' : enterBlockReason(row, this.currentLabRole);
      const verifyRole = roleBlockReason(this.currentLabRole, VERIFY_ROLES) ? 'Верифікує лікар-лаборант або адміністратор лабораторії' : '';
      const verifyState = ['RESULTED', 'NEEDS_REVIEW'].includes(row.status) ? '' : 'Верифікувати можна показник зі статусом «Є результат» або «Очікує лікаря»';
      const rerunReason = roleBlockReason(this.currentLabRole, REJECT_ROLES);
      return [
        { key: 'edit', icon: 'edit_note', color: 'primary', label: 'Ввести результат', disabled: !!enterReason, reason: enterReason, handler: () => this.startEdit(row) },
        { key: 'verify', icon: 'how_to_reg', color: 'green-8', label: 'Верифікувати', disabled: !!(verifyRole || verifyState), reason: verifyRole || verifyState, handler: () => this.$emit('verify', row) },
        { key: 'rerun', icon: 'replay', color: 'purple-6', label: 'Повторити дослідження', disabled: !!rerunReason, reason: rerunReason, handler: () => this.$emit('rerun', row) },
        { key: 'sample', icon: 'biotech', color: 'teal-7', label: 'Обробка зразка', disabled: !row.barcode, reason: 'У показника немає штрихкоду проби', to: row.barcode ? { name: 'lab-sample-processing', params: { barcode: row.barcode } } : undefined }
      ];
    },
    /** Небезпечні дії — у меню «⋮», щоб їх не натиснути випадково */
    menuActions (row) {
      const rejectReason = roleBlockReason(this.currentLabRole, REJECT_ROLES);
      const delRole = roleBlockReason(this.currentLabRole, DELETE_ROLES) ? 'Видаляє результат лікар-лаборант або адміністратор лабораторії' : '';
      const delState = (row.resultId || (row.value !== undefined && row.value !== null)) ? '' : 'Результату ще немає';
      return [
        { key: 'reject', icon: 'block', color: 'negative', label: 'Відхилити', disabled: !!rejectReason, reason: rejectReason, handler: () => this.$emit('reject', row) },
        { key: 'delete', icon: 'delete', color: 'negative', label: 'Видалити результат', disabled: !!(delRole || delState), reason: delRole || delState, handler: () => this.$emit('delete', row) }
      ];
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
      if (CLOSED.includes(row.status)) {
        this.$q.notify({ type: 'info', message: 'Результат верифіковано або відхилено. Для зміни поверніть його в роботу в картці замовлення' });
        return;
      }
      const reason = enterBlockReason(row, this.currentLabRole);
      if (reason) {
        // Не відкриваємо поле, яке сервер все одно відхилить (проба ще не прийнята / немає права)
        this.$emit('select', row);
        this.$q.notify({ type: 'warning', message: reason, timeout: 5000 });
        return;
      }
      this.editingId = row.orderTestId;
      this.editError = false;
      this.editValue = row.value === null || row.value === undefined ? '' : (isNaN(Number(row.value)) ? row.value : String(row.value).replace('.', ','));
      this.$emit('select', row);
      this.focusEditor(row, 0);
    },
    /** Фокус у полі введення: поле з'являється після перемальовки таблиці (віртуальний скрол), тому кілька спроб */
    focusEditor (row, attempt) {
      this.$nextTick(() => {
        if (this.editingId !== row.orderTestId) return;
        const ref = this.$refs['edit-' + row.orderTestId];
        const el = Array.isArray(ref) ? ref[0] : ref;
        const native = document.querySelector('[data-testid="inlineInput"] input, input[data-testid="inlineInput"]');
        const active = document.activeElement;
        const done = native && active === native;
        if (done) { if (native.select) native.select(); return; }
        if (el && el.focus) el.focus();
        else if (native) native.focus();
        if (native && document.activeElement === native) { native.select(); return; }
        if (attempt < 6) setTimeout(() => this.focusEditor(row, attempt + 1), 60);
      });
    },
    cancelEdit () { this.editingId = null; this.editValue = ''; this.editError = false; },
    async commitEdit (row, moveNext) {
      const body = {};
      const empty = this.editValue === null || this.editValue === undefined || String(this.editValue).trim() === '';
      if (empty) {
        this.editError = true;
        this.$q.notify({ type: 'warning', message: 'Введіть значення результату', timeout: 2500 });
        return;
      }
      if (this.isDropdown(row) || (row.resultType === 'TEXT')) {
        body.stringValue = this.editValue;
      } else {
        const n = parseDecimal(this.editValue);
        if (n === null) body.stringValue = this.editValue; // текстовий результат
        else body.numericValue = n;
      }
      this.saving = true;
      try {
        const res = await this.$api.saveResult(row.orderTestId, body);
        this.$emit('saved', { row, result: res });
        this.editingId = null;
        if (moveNext) {
          const idx = this.rows.findIndex(r => r.orderTestId === row.orderTestId);
          const next = this.rows.slice(idx + 1).find(r => !CLOSED.includes(r.status) && !enterBlockReason(r, this.currentLabRole));
          if (next) this.startEdit(next);
        }
      } catch (e) {
        this.editError = true;
        this.$q.notify({ type: 'negative', message: e.userMessage || 'Не вдалося зберегти результат', timeout: 5000 });
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
    min-width 190px
  // Статус і «Дії» закріплені праворуч — доступні без горизонтальної прокрутки
  .wl-sticky
    position sticky
    z-index 2
  td.wl-sticky
    background-color #fff
  thead th.wl-sticky
    background-color #f8fafc
    z-index 3
  .wl-sticky--actions
    right 0
    min-width 170px
    width 170px
  .wl-sticky--status
    right 170px
    min-width 130px
    box-shadow -4px 0 4px -4px rgba(0, 0, 0, .25)
  tr.bg-red-1 td.wl-sticky
    background-color #ffebee
  tr.row-status--closed td.wl-sticky
    background-color #f3f4f6
  tr.row-status--rejected td.wl-sticky
    background-color #fff8dc
  tr.row-selected td.wl-sticky
    background-color #dce7f2
</style>
