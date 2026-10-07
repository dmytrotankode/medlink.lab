<template>
  <div class="trend-chart">
    <svg :viewBox="`0 0 ${w} ${h}`" style="width: 100%; height: 260px" preserveAspectRatio="none" data-testid="trendChart">
      <!-- зелений коридор норми -->
      <rect v-if="corridor" :x="pad.l" :y="corridor.y" :width="plotW" :height="corridor.h" fill="rgba(33,186,69,0.16)" />
      <line v-if="corridor" :x1="pad.l" :x2="pad.l + plotW" :y1="corridor.y" :y2="corridor.y" stroke="#21ba45" stroke-dasharray="4 4" />
      <line v-if="corridor" :x1="pad.l" :x2="pad.l + plotW" :y1="corridor.y + corridor.h" :y2="corridor.y + corridor.h" stroke="#21ba45" stroke-dasharray="4 4" />
      <text v-if="corridor" :x="pad.l + plotW - 4" :y="corridor.y - 4" text-anchor="end" font-size="10" fill="#1e7e34">норма {{ fmt(low) }} – {{ fmt(high) }} {{ unit }}</text>
      <!-- осі -->
      <line :x1="pad.l" :x2="pad.l" :y1="pad.t" :y2="h - pad.b" stroke="#cbd5e1" />
      <line :x1="pad.l" :x2="pad.l + plotW" :y1="h - pad.b" :y2="h - pad.b" stroke="#cbd5e1" />
      <g v-for="t in yTicks" :key="'y' + t">
        <line :x1="pad.l - 4" :x2="pad.l + plotW" :y1="y(t)" :y2="y(t)" stroke="#eef2f7" />
        <text :x="pad.l - 8" :y="y(t) + 3" text-anchor="end" font-size="10" fill="#64748b">{{ fmt(t) }}</text>
      </g>
      <!-- лінія -->
      <polyline v-if="pts.length > 1" :points="pts.map(p => `${p.x},${p.y}`).join(' ')" fill="none" stroke="#4274A7" stroke-width="2" />
      <g v-for="(p, i) in pts" :key="i">
        <circle :cx="p.x" :cy="p.y" r="5" :fill="color(p)" stroke="#fff" stroke-width="1.5"><title>{{ p.label }}: {{ fmt(p.v) }} {{ unit }}</title></circle>
        <text :x="p.x" :y="p.y - 10" text-anchor="middle" font-size="10" font-weight="700" :fill="color(p)">{{ fmt(p.v) }}</text>
        <text v-if="showLabel(i)" :x="p.x" :y="h - pad.b + 14" text-anchor="middle" font-size="9" fill="#64748b">{{ p.label }}</text>
      </g>
      <text v-if="!pts.length" :x="pad.l + plotW / 2" :y="h / 2" text-anchor="middle" font-size="13" fill="#9ca3af">Недостатньо даних для тренду (потрібно ≥ 2 результатів)</text>
    </svg>
  </div>
</template>

<script>
import { formatNumber, formatDate } from '../../utils/format';

export default {
  name: 'TrendChart',
  props: {
    points: { type: Array, default: () => [] },
    unit: { type: String, default: '' }
  },
  data () { return { w: 760, h: 260, pad: { l: 56, r: 16, t: 20, b: 28 } }; },
  computed: {
    plotW () { return this.w - this.pad.l - this.pad.r; },
    plotH () { return this.h - this.pad.t - this.pad.b; },
    values () { return this.points.map(p => Number(p.value)).filter(v => !isNaN(v)); },
    low () { const p = this.points.slice(-1)[0]; return p && p.normLow !== null && p.normLow !== undefined ? Number(p.normLow) : null; },
    high () { const p = this.points.slice(-1)[0]; return p && p.normHigh !== null && p.normHigh !== undefined ? Number(p.normHigh) : null; },
    range () {
      const all = [...this.values]; if (this.low !== null) all.push(this.low); if (this.high !== null) all.push(this.high);
      if (!all.length) return { min: 0, max: 1 };
      const min = Math.min(...all); const max = Math.max(...all); const pad = (max - min || 1) * 0.2;
      return { min: Math.max(0, min - pad) === 0 && min - pad < 0 ? 0 : min - pad, max: max + pad };
    },
    pts () {
      const n = this.points.length;
      return this.points.map((p, i) => ({ x: n === 1 ? this.pad.l + this.plotW / 2 : this.pad.l + (i / (n - 1)) * this.plotW, y: this.y(Number(p.value)), v: Number(p.value), flag: p.flag, label: formatDate(p.at) }));
    },
    corridor () { if (this.low === null || this.high === null) return null; const y1 = this.y(this.high); const y2 = this.y(this.low); return { y: y1, h: Math.max(1, y2 - y1) }; },
    yTicks () { const { min, max } = this.range; const step = (max - min) / 4; return [0, 1, 2, 3, 4].map(i => min + i * step); }
  },
  methods: {
    fmt (v) { return formatNumber(v, 2).replace(/,00$/, '').replace(/(,\d)0$/, '$1'); },
    y (v) { const { min, max } = this.range; return this.pad.t + (1 - (v - min) / (max - min || 1)) * this.plotH; },
    color (p) { if (p.flag === 'CRIT_LOW' || p.flag === 'CRIT_HIGH') return '#d04f45'; if (p.flag === 'LOW' || p.flag === 'HIGH' || p.flag === 'ABNORMAL') return '#e6a700'; return '#21ba45'; },
    showLabel (i) { const n = this.pts.length; const step = Math.ceil(n / 10); return i % step === 0 || i === n - 1; }
  }
};
</script>
