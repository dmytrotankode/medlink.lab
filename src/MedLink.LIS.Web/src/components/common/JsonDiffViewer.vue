<template>
  <div class="json-diff-viewer">
    <div v-if="!hasAny" class="text-grey-6 text-caption">Немає даних «до/після»</div>
    <div v-else class="row q-col-gutter-sm">
      <div class="col-12 col-md-6">
        <div class="text-caption text-weight-bold text-grey-7 q-mb-xs">До</div>
        <div class="json-diff">
          <div v-for="line in beforeLines" :key="'b' + line.key" :class="{ 'diff-removed': line.changed }">{{ line.text }}</div>
        </div>
      </div>
      <div class="col-12 col-md-6">
        <div class="text-caption text-weight-bold text-grey-7 q-mb-xs">Після</div>
        <div class="json-diff">
          <div v-for="line in afterLines" :key="'a' + line.key" :class="{ 'diff-added': line.changed }">{{ line.text }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
function parse (v) {
  if (v === null || v === undefined || v === '') return null;
  if (typeof v === 'string') {
    try { return JSON.parse(v); } catch (e) { return { value: v }; }
  }
  return v;
}

function flatten (obj, prefix = '', out = {}) {
  if (obj === null || typeof obj !== 'object') {
    out[prefix || 'value'] = obj;
    return out;
  }
  Object.keys(obj).forEach(k => {
    const key = prefix ? `${prefix}.${k}` : k;
    const v = obj[k];
    if (v !== null && typeof v === 'object' && !Array.isArray(v)) flatten(v, key, out);
    else out[key] = v;
  });
  return out;
}

export default {
  name: 'JsonDiffViewer',
  props: {
    before: { type: [Object, String], default: null },
    after: { type: [Object, String], default: null }
  },
  computed: {
    b () { return parse(this.before); },
    a () { return parse(this.after); },
    hasAny () { return this.b !== null || this.a !== null; },
    fb () { return this.b ? flatten(this.b) : {}; },
    fa () { return this.a ? flatten(this.a) : {}; },
    beforeLines () {
      return Object.keys(this.fb).map(k => ({
        key: k,
        text: `${k}: ${JSON.stringify(this.fb[k])}`,
        changed: JSON.stringify(this.fb[k]) !== JSON.stringify(this.fa[k])
      }));
    },
    afterLines () {
      return Object.keys(this.fa).map(k => ({
        key: k,
        text: `${k}: ${JSON.stringify(this.fa[k])}`,
        changed: JSON.stringify(this.fb[k]) !== JSON.stringify(this.fa[k])
      }));
    }
  }
};
</script>
