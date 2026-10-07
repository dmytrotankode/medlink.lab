<template>
  <div class="barcode-sticker" :class="{ 'barcode-sticker--print': print }">
    <div class="row items-center justify-between no-wrap" style="font-size: 11px">
      <span class="text-weight-bold ellipsis" style="max-width: 200px">{{ labelName }}</span>
      <span>{{ labelDate }}</span>
    </div>
    <div class="row items-center justify-between no-wrap" style="font-size: 10px">
      <span class="ellipsis">{{ label.patientBirthDate ? ('нар. ' + formatDate(label.patientBirthDate)) : (label.orderNumber ? '№ ' + label.orderNumber : '') }}</span>
      <span class="ellipsis" style="max-width: 130px">{{ tubeLabel }}</span>
    </div>
    <barcode-svg :value="label.barcode" :svg="label.svg" :height="42" :show-text="false" />
    <div class="row items-center justify-between no-wrap">
      <span class="text-weight-bold" style="font-size: 16px; letter-spacing: 0.12em">{{ label.barcode }}</span>
      <span style="font-size: 9px">{{ label.tests || labName }}</span>
    </div>
  </div>
</template>

<script>
import { formatDate, formatDateTime } from '../../utils/format';

export default {
  name: 'LabelSticker',
  props: {
    label: { type: Object, required: true },
    print: { type: Boolean, default: false }
  },
  computed: {
    labelName () {
      return this.label.patientName || this.label.patient || 'Пацієнт';
    },
    labelDate () {
      return this.label.collectedAt ? formatDateTime(this.label.collectedAt) : (this.label.orderDatetime ? formatDate(this.label.orderDatetime) : formatDate(new Date()));
    },
    tubeLabel () {
      const t = this.label.tube;
      if (!t) return this.label.tubeName || '';
      if (typeof t === 'string') return t;
      return t.name || t.code || '';
    },
    labName () {
      return 'MedLink LIS';
    }
  },
  methods: { formatDate }
};
</script>
