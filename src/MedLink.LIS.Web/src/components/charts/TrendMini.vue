<template>
  <svg :viewBox="`0 0 ${w} ${h}`" class="trend-mini" preserveAspectRatio="none" style="width: 100%; height: 70px">
    <rect v-if="corridor" :x="0" :y="corridor.y" :width="w" :height="corridor.h" fill="rgba(33,186,69,0.14)" />
    <polyline v-if="pts.length > 1" :points="pts.map(p => `${p.x},${p.y}`).join(' ')" fill="none" stroke="#4274A7" stroke-width="1.5" />
    <circle v-for="(p, i) in pts" :key="i" :cx="p.x" :cy="p.y" r="3" :fill="pointColor(p)" />
    <text v-if="!pts.length" :x="w / 2" :y="h / 2 + 4" text-anchor="middle" font-size="10" fill="#9ca3af">немає історії</text>
  </svg>
</template>

<script>
export default {
  name: 'TrendMini',
  props: {
    points: { type: Array, default: () => [] },
    low: { type: Number, default: null },
    high: { type: Number, default: null }
  },
  data () { return { w: 300, h: 70 }; },
  computed: {
    values () { return this.points.map(p => Number(p.value)).filter(v => !isNaN(v)); },
    range () {
      const all = [...this.values];
      if (this.low !== null && this.low !== undefined) all.push(Number(this.low));
      if (this.high !== null && this.high !== undefined) all.push(Number(this.high));
      if (!all.length) return { min: 0, max: 1 };
      const min = Math.min(...all); const max = Math.max(...all);
      const pad = (max - min || 1) * 0.15;
      return { min: min - pad, max: max + pad };
    },
    pts () {
      const n = this.points.length;
      return this.points.map((p, i) => ({
        x: n === 1 ? this.w / 2 : 8 + (i / (n - 1)) * (this.w - 16),
        y: this.yOf(Number(p.value)),
        flag: p.flag
      }));
    },
    corridor () {
      if (this.low === null || this.low === undefined || this.high === null || this.high === undefined) return null;
      const y1 = this.yOf(Number(this.high)); const y2 = this.yOf(Number(this.low));
      return { y: y1, h: Math.max(1, y2 - y1) };
    }
  },
  methods: {
    yOf (v) { const { min, max } = this.range; return this.h - 6 - ((v - min) / (max - min)) * (this.h - 12); },
    pointColor (p) {
      if (p.flag === 'CRIT_LOW' || p.flag === 'CRIT_HIGH') return '#d04f45';
      if (p.flag === 'LOW' || p.flag === 'HIGH' || p.flag === 'ABNORMAL') return '#f2c037';
      return '#21ba45';
    }
  }
};
</script>
