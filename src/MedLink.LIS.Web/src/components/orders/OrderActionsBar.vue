<template>
  <div class="order-actions-bar row items-center q-gutter-xs" data-testid="orderActions">
    <div v-if="!visibleCodes.length" class="text-caption text-grey-7" data-testid="noActions">На цьому етапі для вашої ролі немає доступних дій.</div>
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
          <span>{{ meta(code).hint }}</span>
        </q-tooltip>
      </q-btn>
    </template>
  </div>
</template>

<script>
import { ORDER_ACTIONS, ACTION_ORDER } from '../../utils/orderActions';
import { ORDER_STATUS, ROLE } from '../../utils/statuses';
import { roleAllows } from '../../mixins/roleMixin';

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
      // показуємо лише дії, доступні для статусу й ролі
      return codes.filter(c => ORDER_ACTIONS[c] && this.isAllowed(c));
    },
    statusLabel () { return (ORDER_STATUS[this.status] || {}).label || this.status; },
    roleLabel () { return ROLE[this.$store.getters['context/currentRole']] || 'не визначено'; }
  },
  methods: {
    meta (code) { return ORDER_ACTIONS[code]; },
    isAllowed (code) {
      if (!this.allowed.includes(code)) return false;
      // локальні правила за статусом не знають ролі — додатково відсіюємо за списком ролей дії
      return this.source !== 'local' || roleAllows(this.$store.getters['context/currentRole'], ORDER_ACTIONS[code].roles);
    }
  }
};
</script>
