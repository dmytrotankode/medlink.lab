<template>
  <div class="lj-chart">
    <svg :viewBox="`0 0 ${w} ${h}`" class="lj-svg" preserveAspectRatio="none" data-testid="ljChart">
      <!-- Смуги SD -->
      <rect :x="pad.l" :y="y(mean + 3 * sd)" :width="plotW" :height="y(mean + 2 * sd) - y(mean + 3 * sd)" fill="rgba(208,79,69,0.10)" />
      <rect :x="pad.l" :y="y(mean - 2 * sd)" :width="plotW" :height="y(mean - 3 * sd) - y(mean - 2 * sd)" fill="rgba(208,79,69,0.10)" />
      <rect :x="pad.l" :y="y(mean + 2 * sd)" :width="plotW" :height="y(mean + 1 * sd) - y(mean + 2 * sd)" fill="rgba(242,192,55,0.16)" />
      <rect :x="pad.l" :y="y(mean - 1 * sd)" :width="plotW" :height="y(mean - 2 * sd) - y(mean - 1 * sd)" fill="rgba(242,192,55,0.16)" />
      <rect :x="pad.l" :y="y(mean + sd)" :width="plotW" :height="y(mean - sd) - y(mean + sd)" fill="rgba(33,186,69,0.12)" />

      <!-- Лінії -->
      <g v-for="k in [-3, -2, -1, 1, 2, 3]" :key="'l' + k">
        <line :x1="pad.l" :x2="pad.l + plotW" :y1="y(mean + k * sd)" :y2="y(mean + k * sd)" :stroke="Math.abs(k) === 3 ? '#d04f45' : (Math.abs(k) === 2 ? '#e6a700' : '#21ba45')" stroke-width="1" stroke-dasharray="4 4" />
        <text :x="pad.l - 6" :y="y(mean + k * sd) + 3" text-anchor="end" font-size="10" :fill="Math.abs(k) === 3 ? '#d04f45' : (Math.abs(k) === 2 ? '#b8860b' : '#1e7e34')">{{ k > 0 ? '+' : '' }}{{ k }}SD ({{ fmt(mean + k * sd) }})</text>
      </g>
      <line :x1="pad.l" :x2="pad.l + plotW" :y1="y(mean)" :y2="y(mean)" stroke="#4274A7" stroke-width="1.6" />
      <text :x="pad.l - 6" :y="y(mean) + 3" text-anchor="end" font-size="10" fill="#4274A7" font-weight="700">Mean {{ fmt(mean) }}</text>

      <!-- Лінія точок -->
      <polyline v-if="pts.length > 1" :points="pts.map(p => `${p.x},${p.y}`).join(' ')" fill="none" stroke="#4274A7" stroke-width="1.4" opacity="0.8" />
      <g v-for="(p, i) in pts" :key="'p' + i" class="lj-point" @click="$emit('point-click', p.raw)">
        <circle :cx="p.x" :cy="p.y" :r="p.status === 'OK' ? 4 : 5.5" :fill="color(p)" stroke="#fff" stroke-width="1.2" />
        <title>{{ tooltip(p) }}</title>
        <text v-if="p.rules.length" :x="p.x" :y="p.y - 9" text-anchor="middle" font-size="9" font-weight="700" :fill="color(p)">{{ p.rules.join(',') }}</text>
      </g>

      <!-- Вісь X -->
      <line :x1="pad.l" :x2="pad.l + plotW" :y1="h - pad.b" :y2="h - pad.b" stroke="#cbd5e1" />
      <text v-for="(p, i) in pts" :key="'x' + i" v-show="showLabel(i)" :x="p.x" :y="h - pad.b + 14" text-anchor="middle" font-size="9" fill="#64748b">{{ p.label }}</text>
      <text v-if="!pts.length" :x="pad.l + plotW / 2" :y="h / 2" text-anchor="middle" font-size="13" fill="#9ca3af">Точок контролю за період немає</text>
    </svg>
    <div class="row q-gutter-md justify-end text-caption q-mt-xs">
      <span><span class="legend-dot" style="background:#21ba45" /> у контролі (±2s)</span>
      <span><span class="legend-dot" style="background:#f2c037" /> попередження 1₂s</span>
      <span><span class="legend-dot" style="background:#d04f45" /> порушення (rejection) / Lockout</span>
    </div>
  </div>
</template>

<script>
import { formatNumber, formatDate } from '../../utils/format';

export default {
  name: 'LeveyJenningsChart',
  props: {
    points: { type: Array, default: () => [] },
    mean: { type: Number, default: 0 },
    sd: { type: Number, default: 1 },
    decimals: { type: Number, default: 2 }
  },
  data () { return { w: 900, h: 300, pad: { l: 92, r: 16, t: 14, b: 26 } }; },
  computed: {
    plotW () { return this.w - this.pad.l - this.pad.r; },
    plotH () { return this.h - this.pad.t - this.pad.b; },
    range () {
      const values = this.points.map(p => Number(p.value)).filter(v => !isNaN(v));
      const sd = this.sd || 1;
      let min = this.mean - 3.6 * sd; let max = this.mean + 3.6 * sd;
      values.forEach(v => { if (v < min) min = v - 0.3 * sd; if (v > max) max = v + 0.3 * sd; });
      return { min, max };
    },
    pts () {
      const n = this.points.length;
      return this.points.map((p, i) => ({
        x: n === 1 ? this.pad.l + this.plotW / 2 : this.pad.l + (i / (n - 1)) * this.plotW,
        y: this.y(Number(p.value)),
        status: p.status || (p.isRejection ? 'LOCKOUT' : (p.isWarning ? 'WARNING' : 'OK')),
        rules: p.rules || p.violatedRules || [],
        label: formatDate(p.at).slice(0, 5),
        raw: p
      }));
    }
  },
  methods: {
    fmt (v) { return formatNumber(v, this.decimals); },
    y (v) { const { min, max } = this.range; return this.pad.t + (1 - (v - min) / (max - min || 1)) * this.plotH; },
    color (p) {
      if (p.status === 'LOCKOUT' || p.status === 'REJECTION' || p.raw.isRejection || p.raw.lockoutEnforced) return '#d04f45';
      if (p.status === 'WARNING' || p.raw.isWarning) return '#f2c037';
      return '#21ba45';
    },
    tooltip (p) {
      return `${formatDate(p.raw.at)} · ${this.fmt(p.raw.value)} · z=${formatNumber(p.raw.z !== undefined ? p.raw.z : p.raw.zScore, 2)}${p.rules.length ? ' · ' + p.rules.join(', ') : ''}`;
    },
    showLabel (i) { const n = this.pts.length; const step = Math.ceil(n / 15); return i % step === 0 || i === n - 1; }
  }
};
</script>

<style scoped>
.legend-dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; vertical-align: middle; margin-right: 4px; }
.lj-point { cursor: pointer; }
</style>
