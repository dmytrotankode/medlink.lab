<template>
  <q-badge
    :color="meta.color"
    :text-color="meta.textColor || 'white'"
    :outline="outline"
    class="status-chip"
    :class="{ 'status-chip--dense': dense }"
  >
    <q-icon v-if="icon && meta.icon" :name="meta.icon" size="12px" class="q-mr-xs" />
    {{ meta.label }}
  </q-badge>
</template>

<script>
import {
  ORDER_STATUS, SAMPLE_STATUS, TEST_STATUS, FLAG, MANIFEST_STATUS, CONNECTOR_STATUS,
  BATCH_STATUS, QC_STATUS, CULTURE_STATUS, statusMeta
} from '../../utils/statuses';

const MAPS = {
  order: ORDER_STATUS,
  sample: SAMPLE_STATUS,
  test: TEST_STATUS,
  flag: FLAG,
  manifest: MANIFEST_STATUS,
  connector: CONNECTOR_STATUS,
  batch: BATCH_STATUS,
  qc: QC_STATUS,
  culture: CULTURE_STATUS
};

export default {
  name: 'StatusChip',
  props: {
    value: { type: String, default: '' },
    type: { type: String, default: 'order' },
    outline: { type: Boolean, default: false },
    icon: { type: Boolean, default: false },
    dense: { type: Boolean, default: false }
  },
  computed: {
    meta () {
      return statusMeta(MAPS[this.type] || ORDER_STATUS, this.value);
    }
  }
};
</script>

<style lang="stylus" scoped>
.status-chip
  font-weight 600
  font-size 11px
  text-transform uppercase
  letter-spacing 0.03em
  padding 3px 8px
  border-radius 10px
  white-space nowrap
.status-chip--dense
  padding 1px 6px
  font-size 10px
</style>
