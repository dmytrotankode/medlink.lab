<template>
  <div class="order-actions-bar row items-center q-gutter-xs" data-testid="orderActions">
    <template v-for="code in visibleCodes">
      <q-btn
        :key="code"
        :color="meta(code).color"
        :icon="meta(code).icon"
        :label="dense ? undefined : meta(code).label"
        :disable="!isAllowed(code) || busy"
        :outline="!isAllowed(code)"
        :round="dense"
        :flat="dense"
        unelevated
        no-caps
        :size="dense ? 'sm' : 'md'"
        :data-testid="`action-${code}`"
        :data-allowed="isAllowed(code) ? '1' : '0'"
        @click="$emit('action', code)"
      >
        <q-tooltip>
          <b>{{ meta(code).label }}</b><br>
          <span v-if="isAllowed(code)">{{ meta(code).hint }}</span>
          <span v-else>Недоступно для статусу «{{ statusLabel }}» або ролі «{{ roleLabel }}»</span>
        </q-tooltip>
      </q-btn>
    </template>
    <q-badge v-if="source === 'local'" outline color="grey-6" class="q-ml-sm" title="API не повернуло allowedActions — використано локальні правила за статусом">правила: локальні</q-badge>
  </div>
</template>

<script>
import { ORDER_ACTIONS, ACTION_ORDER } from '../../utils/orderActions';
import { ORDER_STATUS, ROLE } from '../../utils/statuses';

export default {
  name: 'OrderActionsBar',
  props: {
    allowed: { type: Array, default: () => [] },
    source: { type: String, default: 'api' },
    status: { type: String, default: '' },
    busy: { type: Boolean, default: false },
    dense: { type: Boolean, default: false },
    only: { type: Array, default: null }
  },
  computed: {
    visibleCodes () {
      const codes = this.only || ACTION_ORDER;
      return codes.filter(c => ORDER_ACTIONS[c]);
    },
    statusLabel () { return (ORDER_STATUS[this.status] || {}).label || this.status; },
    roleLabel () { return ROLE[this.$store.getters['context/currentRole']] || 'не визначено'; }
  },
  methods: {
    meta (code) { return ORDER_ACTIONS[code]; },
    isAllowed (code) { return this.allowed.includes(code); }
  }
};
</script>
