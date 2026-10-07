<template>
  <div class="worklist-detail medlink-card" data-testid="worklistDetail">
    <div class="medlink-card__title">
      <span><q-icon name="assignment_ind" class="q-mr-xs" />Деталі дослідження</span>
      <span v-if="row" class="text-caption text-grey-6">orderTestId: <span class="mono">{{ shortId(row.orderTestId) }}</span></span>
    </div>
    <empty-state v-if="!row" title="Оберіть рядок у черзі" icon="touch_app" hint="Подвійний клік або Enter — введення результату; Esc — скасувати" />
    <div v-else class="row q-col-gutter-md q-pa-md">
      <!-- Ліва: метадані пацієнта та дослідження -->
      <div class="col-12 col-md-5">
        <div class="text-caption text-grey-7">Пацієнт</div>
        <div class="text-subtitle1 text-weight-bold">{{ row.patientName || '—' }}</div>
        <div class="text-caption">{{ row.patientAgeGender }}</div>
        <q-list dense class="q-mt-sm">
          <q-item class="q-px-none"><q-item-section><q-item-label caption>Замовлення</q-item-label><q-item-label><a href="#" class="text-primary" @click.prevent="$emit('open-order', row)">{{ row.orderNumber }}</a> <q-badge v-if="row.isCito" color="deep-orange-6" label="CITO" /></q-item-label></q-item-section></q-item>
          <q-item class="q-px-none"><q-item-section><q-item-label caption>Проба</q-item-label><q-item-label class="mono">{{ row.barcode }}</q-item-label></q-item-section></q-item>
          <q-item class="q-px-none"><q-item-section><q-item-label caption>Аналізатор</q-item-label><q-item-label>{{ row.analyzerName || 'ручне введення' }} <q-badge v-if="row.isLockedOut" color="purple-6"><q-icon name="lock" size="10px" /> Lockout ВКЯ</q-badge></q-item-label></q-item-section></q-item>
          <q-item class="q-px-none"><q-item-section><q-item-label caption>Введено / верифіковано</q-item-label><q-item-label>{{ row.enteredAt | datetime }} / {{ row.verifiedAt | datetime }}</q-item-label></q-item-section></q-item>
        </q-list>
        <div class="section-title q-mt-sm">Дії процесу</div>
        <order-actions-bar :allowed="actions" :source="actionsSource" :status="orderStatus" dense :only="['ENTER_RESULT', 'VERIFY', 'RERUN', 'REJECT', 'RELEASE', 'REOPEN']" @action="$emit('order-action', { row, action: $event })" />
      </div>
      <!-- Права: результат -->
      <div class="col-12 col-md-7">
        <div class="row items-center justify-between">
          <div>
            <div class="text-caption text-grey-7">Показник</div>
            <div class="text-subtitle1 text-weight-bold">{{ row.testName }} <span class="text-grey-7">({{ row.testCode }})</span></div>
          </div>
          <status-chip :value="row.status" type="test" icon />
        </div>
        <div class="row items-end q-col-gutter-md q-mt-xs">
          <div class="col-5">
            <div class="text-caption text-grey-7">Результат</div>
            <div class="text-h4" :class="valueClass">{{ displayValue }} <span class="text-subtitle2 text-grey-7">{{ row.unit }}</span></div>
            <flag-marker :flag="row.flag || 'NONE'" show-label />
          </div>
          <div class="col-7">
            <div class="text-caption text-grey-7">Референтний інтервал</div>
            <div class="text-weight-bold">{{ referenceDisplay(row) || '—' }}</div>
            <div class="text-caption text-grey-7">крит.: {{ row.critLow | num }} / {{ row.critHigh | num }}</div>
            <div v-if="row.previousValue !== undefined && row.previousValue !== null" class="text-caption q-mt-xs">
              Попереднє: <b>{{ row.previousValue | num }}</b> ({{ row.previousAt | datetime }}) ·
              Δ <span :class="row.deltaAlert ? 'text-orange-9 text-weight-bold' : ''">{{ row.deltaPercent | pct }}</span>
            </div>
          </div>
        </div>
        <q-banner v-if="row.isLockedOut" dense rounded class="bg-purple-1 text-purple-9 q-mt-sm"><q-icon name="lock" /> Активний Lockout ВКЯ для аналізатора/тесту — автоверифікація заблокована, верифікація лише з override.</q-banner>
        <q-banner v-if="row.deltaAlert" dense rounded class="bg-orange-1 text-orange-9 q-mt-sm"><q-icon name="trending_up" /> Delta-check: відхилення від попереднього результату перевищує поріг — потрібна перевірка лікарем.</q-banner>
        <div v-if="row.operatorComment || row.verificationComment" class="q-mt-sm text-caption">
          <div v-if="row.operatorComment"><b>Оператор:</b> {{ row.operatorComment }}</div>
          <div v-if="row.verificationComment"><b>Лікар:</b> {{ row.verificationComment }}</div>
        </div>
        <div class="row q-gutter-sm q-mt-md">
          <q-btn color="primary" icon="edit_note" label="Ввести / змінити" no-caps data-testid="detailEdit" @click="$emit('edit', row)" />
          <q-btn outline color="green-8" icon="how_to_reg" label="Верифікувати" no-caps :disable="!['RESULTED', 'NEEDS_REVIEW'].includes(row.status)" @click="$emit('verify', row)" />
          <q-btn outline color="teal-7" icon="verified" label="Автоверифікація" no-caps :disable="row.status !== 'RESULTED'" @click="$emit('autoverify', row)" />
          <q-btn flat color="grey-8" icon="history" label="Історія" no-caps @click="$emit('history', row)" />
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import OrderActionsBar from '../../../components/orders/OrderActionsBar.vue';
import { flagMeta } from '../../../utils/statuses';
import { referenceDisplay, formatNumber } from '../../../utils/format';
import { resolveActions } from '../../../utils/orderActions';

export default {
  name: 'WorklistDetail',
  components: { OrderActionsBar },
  props: {
    row: { type: Object, default: null },
    order: { type: Object, default: null }
  },
  computed: {
    displayValue () {
      const v = this.row && this.row.value;
      if (v === null || v === undefined || v === '') return '—';
      return isNaN(Number(v)) ? v : formatNumber(v);
    },
    valueClass () { return this.row && this.row.value !== null && this.row.value !== undefined ? flagMeta(this.row.flag || 'NONE').css : 'val-empty'; },
    orderStatus () { return (this.order && this.order.status) || (this.row && this.row.orderStatus) || ''; },
    resolved () {
      if (this.row && Array.isArray(this.row.allowedActions)) return { actions: this.row.allowedActions, source: 'api' };
      return resolveActions(this.order || { status: this.orderStatus || 'IN_PROGRESS' }, null);
    },
    actions () { return this.resolved.actions; },
    actionsSource () { return this.resolved.source; }
  },
  methods: {
    referenceDisplay,
    shortId (id) { return id ? String(id).slice(0, 8) : ''; }
  }
};
</script>
