<template>
  <span class="flag-marker" :title="meta.label" :class="`flag-marker--${kind}`">
    <!-- Норма: зелений маркер -->
    <svg v-if="kind === 'normal'" :width="size" :height="size" viewBox="0 0 16 16">
      <circle cx="8" cy="8" r="6" fill="#21ba45" />
    </svg>
    <!-- Нижче/вище норми: жовтий трикутник -->
    <svg v-else-if="kind === 'abnormal'" :width="size" :height="size" viewBox="0 0 16 16">
      <path d="M8 2 L15 14 H1 Z" fill="#f2c037" stroke="#b8860b" stroke-width="0.8" />
      <text x="8" y="12.5" font-size="8" text-anchor="middle" font-weight="700" fill="#333">{{ arrow }}</text>
    </svg>
    <!-- Критично: червоний хрестик -->
    <svg v-else-if="kind === 'critical'" :width="size" :height="size" viewBox="0 0 16 16">
      <circle cx="8" cy="8" r="7" fill="#d04f45" />
      <path d="M4.5 4.5 L11.5 11.5 M11.5 4.5 L4.5 11.5" stroke="#fff" stroke-width="2.2" stroke-linecap="round" />
    </svg>
    <!-- Не введено: сірий кружок -->
    <svg v-else :width="size" :height="size" viewBox="0 0 16 16">
      <circle cx="8" cy="8" r="5.5" fill="none" stroke="#9ca3af" stroke-width="1.6" />
    </svg>
    <span v-if="showLabel" class="flag-marker__label q-ml-xs">{{ meta.label }}</span>
  </span>
</template>

<script>
import { flagMeta } from '../../utils/statuses';

export default {
  name: 'FlagMarker',
  props: {
    flag: { type: String, default: 'NONE' },
    size: { type: [Number, String], default: 16 },
    showLabel: { type: Boolean, default: false }
  },
  computed: {
    meta () { return flagMeta(this.flag); },
    kind () {
      if (this.flag === 'NORMAL') return 'normal';
      if (this.flag === 'CRIT_LOW' || this.flag === 'CRIT_HIGH') return 'critical';
      if (this.flag === 'LOW' || this.flag === 'HIGH' || this.flag === 'ABNORMAL') return 'abnormal';
      return 'none';
    },
    arrow () {
      if (this.flag === 'LOW') return '↓';
      if (this.flag === 'HIGH') return '↑';
      return '!';
    }
  }
};
</script>

<style scoped>
.flag-marker { display: inline-flex; align-items: center; vertical-align: middle; }
.flag-marker__label { font-size: 12px; }
.flag-marker--critical svg { animation: panic-pulse 1.6s infinite; }
</style>
