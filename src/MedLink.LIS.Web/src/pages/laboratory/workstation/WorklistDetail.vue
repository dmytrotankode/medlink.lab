<template>
  <div class="worklist-detail medlink-card" data-testid="worklistDetail">
    <div class="medlink-card__title">
      <span><q-icon name="assignment_ind" class="q-mr-xs" />Деталі дослідження</span>
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
          <q-item class="q-px-none"><q-item-section><q-item-label caption>Аналізатор</q-item-label><q-item-label>{{ row.analyzerName || 'Вручну' }} <q-badge v-if="row.isLockedOut" color="purple-6"><q-icon name="lock" size="10px" class="q-mr-xs" />Блокування ВКЯ</q-badge></q-item-label></q-item-section></q-item>
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
            <div class="text-subtitle1 text-weight-bold">{{ testTitle }}</div>
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
        <q-banner v-if="row.isLockedOut" dense rounded class="bg-purple-1 text-purple-9 q-mt-sm"><q-icon name="lock" /> Аналізатор заблоковано контролем якості: автоверифікація недоступна, верифікація можлива лише з підтвердженням винятку.</q-banner>
        <q-banner v-if="row.autoVerifyBlockReason && ['NEEDS_REVIEW', 'RESULTED'].includes(row.status)" dense rounded class="bg-grey-2 text-grey-9 q-mt-sm" data-testid="autoVerifyReasonBanner"><q-icon name="help_outline" /> Чому не автоверифіковано: {{ row.autoVerifyBlockReason }}. Автоверифікація недоступна, доки причина не усунена; результат верифікує лікар.</q-banner>
        <q-banner v-if="row.deltaAlert && row.previousValue != null" dense rounded class="bg-orange-1 text-orange-9 q-mt-sm"><q-icon name="trending_up" /> Різка зміна відносно попереднього результату — потрібна перевірка лікарем.</q-banner>
        <div v-if="row.operatorComment || row.verificationComment" class="q-mt-sm text-caption">
          <div v-if="row.operatorComment"><b>Оператор:</b> {{ row.operatorComment }}</div>
          <div v-if="row.verificationComment"><b>Лікар:</b> {{ row.verificationComment }}</div>
        </div>
        <div class="row q-gutter-sm q-mt-md">
          <span class="inline-block">
            <q-btn color="primary" icon="edit_note" label="Ввести / змінити" no-caps data-testid="detailEdit" :disable="!!enterReason" @click="$emit('edit', row)" />
            <q-tooltip v-if="enterReason">{{ enterReason }}</q-tooltip>
          </span>
          <span class="inline-block">
            <q-btn outline color="green-8" icon="how_to_reg" label="Верифікувати" no-caps :disable="!!verifyReason" @click="$emit('verify', row)" />
            <q-tooltip v-if="verifyReason">{{ verifyReason }}</q-tooltip>
          </span>
          <span class="inline-block">
            <q-btn outline color="teal-7" icon="verified" label="Автоверифікація" no-caps :disable="!!autoReason" @click="$emit('autoverify', row)" />
            <q-tooltip v-if="autoReason">{{ autoReason }}</q-tooltip>
          </span>
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
import roleMixin from '../../../mixins/roleMixin';
import { enterBlockReason, roleBlockReason, VERIFY_ROLES, ENTER_ROLES } from '../../../utils/resultRules';

export default {
  name: 'WorklistDetail',
  mixins: [roleMixin],
  components: { OrderActionsBar },
  props: {
    row: { type: Object, default: null },
    order: { type: Object, default: null }
  },
  computed: {
    /** «Лейкоцити (WBC)»: код дописуємо лише якщо його ще немає в назві */
    testTitle () {
      const r = this.row;
      if (!r) return '';
      const name = r.testName || r.testCode || '';
      return r.testCode && !name.includes(`(${r.testCode})`) && name !== r.testCode ? `${name} (${r.testCode})` : name;
    },
    enterReason () { return this.row ? enterBlockReason(this.row, this.currentLabRole) : ''; },
    verifyReason () {
      if (!this.row) return '';
      if (roleBlockReason(this.currentLabRole, VERIFY_ROLES)) return 'Верифікує лише лікар-лаборант (або адміністратор лабораторії)';
      return ['RESULTED', 'NEEDS_REVIEW'].includes(this.row.status) ? '' : 'Верифікувати можна показник зі статусом «Є результат» або «Очікує лікаря»';
    },
    autoReason () {
      if (!this.row) return '';
      if (roleBlockReason(this.currentLabRole, ENTER_ROLES)) return this.roleHint(...ENTER_ROLES);
      return this.row.status === 'RESULTED' ? '' : 'Автоверифікація доступна для показника зі статусом «Є результат»';
    },
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
    referenceDisplay
  }
};
</script>
