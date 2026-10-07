<template>
  <q-list dense separator data-testid="tubePlanList">
    <q-item v-for="t in items" :key="t.key" :active="!!activeKey && t.key === activeKey" active-class="bg-blue-1">
      <q-item-section avatar class="items-center" style="min-width: 34px">
        <div class="text-caption text-grey-7">{{ t.index }}</div>
        <div :style="{ background: t.color || '#bbb', width: '16px', height: '26px', borderRadius: '3px 3px 8px 8px', border: '1px solid #999' }" />
      </q-item-section>
      <q-item-section>
        <q-item-label>
          <b>{{ t.tubeName }}</b> <span class="text-grey-7">· {{ t.biomaterialName }}</span>
          <span v-if="t.barcode" class="mono text-primary q-ml-xs">{{ t.barcode }}</span>
          <q-badge v-if="t.existing" color="grey-6" label="наявна пробірка" class="q-ml-xs" />
          <q-badge v-if="t.isSeparate" color="deep-orange-5" label="окремо" class="q-ml-xs" />
        </q-item-label>
        <q-item-label caption>
          <span v-for="(c, i) in t.tests" :key="c.code" :class="c.isNew === false ? 'text-grey-5' : ''">{{ c.code }}{{ i < t.tests.length - 1 ? ', ' : '' }}</span>
        </q-item-label>
        <q-item-label v-if="t.reasonTexts && t.reasonTexts.length" caption class="text-deep-orange-8">
          <q-icon name="call_split" size="14px" /> {{ t.reasonTexts.join('; ') }}
        </q-item-label>
        <q-item-label v-if="t.capacityMl && t.usedVolumeMl" caption>
          <q-linear-progress :value="Math.min(1, (t.usedVolumeMl || 0) / t.capacityMl)" :color="(t.usedVolumeMl || 0) > t.capacityMl ? 'negative' : 'teal-5'" size="6px" rounded class="q-mt-xs" style="max-width: 220px" />
          <span class="text-grey-7">{{ fmt(t.usedVolumeMl) }} / {{ fmt(t.capacityMl) }} мл матеріалу</span>
        </q-item-label>
      </q-item-section>
      <q-item-section side top>
        <q-badge color="grey-7" :label="`${t.tests.length} тест.`" />
        <div v-if="t.inversionsCount" class="text-caption text-grey-6 q-mt-xs">інверсій: {{ t.inversionsCount }}</div>
        <status-chip v-if="t.status" :value="t.status" type="sample" class="q-mt-xs" />
      </q-item-section>
    </q-item>
  </q-list>
</template>

<script>
import StatusChip from '../common/StatusChip.vue';

/**
 * План пробірок (FR-PRE-004) у порядку забору. Елемент:
 * { key, index, color, tubeName, biomaterialName, tests: [{ code, isNew }], usedVolumeMl, capacityMl,
 *   reasonTexts, isSeparate, inversionsCount, barcode, status, existing }
 */
export default {
  name: 'TubePlanList',
  components: { StatusChip },
  props: {
    items: { type: Array, default: () => [] },
    activeKey: { type: String, default: null }
  },
  methods: {
    fmt (v) { return v == null ? '—' : Number(v).toLocaleString('uk-UA', { maximumFractionDigits: 2 }); }
  }
};
</script>
