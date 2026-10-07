<template>
  <div class="order-stepper">
    <div class="row no-wrap q-gutter-xs scroll" style="overflow-x: auto">
      <div
        v-for="(step, i) in steps"
        :key="step.code"
        class="pipeline-step col"
        :class="{ 'pipeline-step--done': step.done, 'pipeline-step--active': step.active }"
        style="min-width: 128px"
      >
        <div class="pipeline-num">{{ i + 1 }}</div>
        <div class="column" style="line-height: 1.15">
          <span>{{ step.label }}</span>
          <span class="text-caption" :class="step.at ? 'text-grey-8' : 'text-grey-5'">{{ step.at ? formatDateTime(step.at) : '—' }}</span>
        </div>
      </div>
    </div>
    <div v-if="terminal" class="q-mt-xs">
      <q-badge :color="terminal === 'CANCELLED' ? 'grey-7' : 'negative'" class="q-pa-xs">
        <q-icon :name="terminal === 'CANCELLED' ? 'cancel' : 'block'" class="q-mr-xs" />
        {{ terminal === 'CANCELLED' ? 'Замовлення скасовано' : 'Замовлення відхилено' }}
        <span v-if="terminalAt"> · {{ formatDateTime(terminalAt) }}</span>
      </q-badge>
    </div>
  </div>
</template>

<script>
import { ORDER_STATUS_FLOW, ORDER_STATUS } from '../../utils/statuses';
import { formatDateTime } from '../../utils/format';

/**
 * Відмітки часу беруться з DTO (statusHistory / timestamps) або з проб/результатів, якщо DTO їх не містить.
 */
export default {
  name: 'OrderStepper',
  props: {
    order: { type: Object, required: true },
    audit: { type: Array, default: () => [] }
  },
  computed: {
    terminal () {
      return ['CANCELLED', 'REJECTED'].includes(this.order.status) ? this.order.status : null;
    },
    terminalAt () {
      return this.order.cancelledAt || this.order.rejectedAt || this.auditAtFor(this.order.status) || this.order.modifiedOn || null;
    },
    currentIndex () {
      const idx = ORDER_STATUS_FLOW.indexOf(this.order.status);
      if (idx >= 0) return idx;
      // термінальні — показуємо прогрес до останнього досягнутого етапу
      return this.lastReachedIndex;
    },
    lastReachedIndex () {
      let last = 0;
      ORDER_STATUS_FLOW.forEach((code, i) => { if (this.timestampFor(code)) last = i; });
      return last;
    },
    steps () {
      return ORDER_STATUS_FLOW.map((code, i) => ({
        code,
        label: ORDER_STATUS[code].label,
        at: this.timestampFor(code),
        done: i < this.currentIndex || (i === this.currentIndex && !this.terminal && code === 'RELEASED'),
        active: i === this.currentIndex && !this.terminal
      }));
    }
  },
  methods: {
    formatDateTime,
    auditAtFor (status) {
      const hit = (this.audit || []).find(a => {
        const after = typeof a.after === 'string' ? a.after : JSON.stringify(a.after || {});
        return after && after.includes(`"${status}"`);
      });
      return hit ? hit.at : null;
    },
    timestampFor (code) {
      const o = this.order;
      const hist = o.statusHistory || o.history || [];
      const h = hist.find(x => x.status === code);
      if (h) return h.at || h.changedAt;
      const samples = o.samples || [];
      const tests = o.tests || [];
      const minOf = arr => arr.filter(Boolean).sort()[0] || null;
      switch (code) {
        case 'NEW': return o.orderDatetime || o.createdOn;
        case 'COLLECTED': return o.collectedAt || minOf(samples.map(s => s.collectedAt));
        case 'IN_TRANSIT': return o.dispatchedAt || this.auditAtFor('IN_TRANSIT');
        case 'RECEIVED': return o.receivedAt || minOf(samples.map(s => s.receivedAt));
        case 'IN_PROGRESS': return o.inProgressAt || minOf(tests.map(t => t.result && t.result.enteredAt));
        case 'COMPLETED': return o.completedAt || this.auditAtFor('COMPLETED');
        case 'RELEASED': return o.releasedAt || this.auditAtFor('RELEASED');
        default: return null;
      }
    }
  }
};
</script>
