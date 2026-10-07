<template>
  <div class="barcode-svg" :style="{ width: width }">
    <!-- SVG з API (Code128 від MedLink.LIS.Core) має пріоритет -->
    <div v-if="svg" v-html="svg" class="barcode-svg__api" />
    <!-- Запасний варіант: локальний Code128-B рендер (тільки для попереднього перегляду) -->
    <svg v-else :viewBox="`0 0 ${totalWidth} ${height + (showText ? 14 : 0)}`" preserveAspectRatio="xMidYMid meet" class="barcode-svg__local">
      <rect :width="totalWidth" :height="height + (showText ? 14 : 0)" fill="#fff" />
      <rect v-for="(bar, i) in bars" :key="i" :x="bar.x" y="0" :width="bar.w" :height="height" fill="#000" />
      <text v-if="showText" :x="totalWidth / 2" :y="height + 11" font-size="11" text-anchor="middle" font-family="Consolas, monospace">{{ value }}</text>
    </svg>
  </div>
</template>

<script>
// Таблиця Code128 (шаблони ширин 11 модулів), код B
const CODE128 = [
  '212222', '222122', '222221', '121223', '121322', '131222', '122213', '122312', '132212', '221213',
  '221312', '231212', '112232', '122132', '122231', '113222', '123122', '123221', '223211', '221132',
  '221231', '213212', '223112', '312131', '311222', '321122', '321221', '312212', '322112', '322211',
  '212123', '212321', '232121', '111323', '131123', '131321', '112313', '132113', '132311', '211313',
  '231113', '231311', '112133', '112331', '132131', '113123', '113321', '133121', '313121', '211331',
  '231131', '213113', '213311', '213131', '311123', '311321', '331121', '312113', '312311', '332111',
  '314111', '221411', '431111', '111224', '111422', '121124', '121421', '141122', '141221', '112214',
  '112412', '122114', '122411', '142112', '142211', '241211', '221114', '413111', '241112', '134111',
  '111242', '121142', '121241', '114212', '124112', '124211', '411212', '421112', '421211', '212141',
  '214121', '412121', '111143', '111341', '131141', '114113', '114311', '411113', '411311', '113141',
  '114131', '311141', '411131', '211412', '211214', '211232', '2331112'
];
const START_B = 104;
const STOP = 106;

export default {
  name: 'BarcodeSvg',
  props: {
    value: { type: String, default: '' },
    svg: { type: String, default: '' },
    height: { type: Number, default: 48 },
    width: { type: String, default: '100%' },
    showText: { type: Boolean, default: true }
  },
  computed: {
    codes () {
      const text = String(this.value || '');
      const codes = [START_B];
      let checksum = START_B;
      for (let i = 0; i < text.length; i++) {
        const c = text.charCodeAt(i) - 32;
        const code = c >= 0 && c < 95 ? c : 0;
        codes.push(code);
        checksum += code * (i + 1);
      }
      codes.push(checksum % 103);
      codes.push(STOP);
      return codes;
    },
    layout () {
      const bars = [];
      let x = 10; // тиха зона
      this.codes.forEach(code => {
        const pattern = CODE128[code];
        for (let i = 0; i < pattern.length; i++) {
          const w = Number(pattern[i]);
          if (i % 2 === 0) bars.push({ x, w });
          x += w;
        }
      });
      return { bars, total: x + 10 };
    },
    bars () { return this.layout.bars; },
    totalWidth () { return this.layout.total; }
  }
};
</script>

<style scoped>
.barcode-svg { display: block; }
.barcode-svg__api >>> svg { max-width: 100%; height: auto; display: block; margin: 0 auto; }
.barcode-svg__local { width: 100%; height: auto; display: block; }
</style>
