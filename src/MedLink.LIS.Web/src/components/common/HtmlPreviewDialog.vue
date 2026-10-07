<template>
  <q-dialog :value="value" @input="$emit('input', $event)" maximized>
    <q-card class="column">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="description" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">{{ title }}</div>
        <q-space />
        <q-btn v-if="url" flat dense no-caps icon="open_in_new" label="У новій вкладці" type="a" :href="url" target="_blank" class="q-mr-sm" />
        <q-btn v-if="pdfUrl" flat dense no-caps icon="picture_as_pdf" label="PDF" type="a" :href="pdfUrl" target="_blank" class="q-mr-sm" />
        <q-btn flat dense no-caps icon="print" label="Друк" class="q-mr-sm" @click="print" />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section class="col q-pa-none bg-grey-3">
        <div v-if="loading" class="text-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>
        <div v-else-if="error" class="q-pa-lg"><api-error-banner :message="error" @retry="$emit('retry')" /></div>
        <iframe v-else ref="frame" :srcdoc="html" class="full-width full-height" style="border: 0; min-height: calc(100vh - 56px); background: #fff" title="Попередній перегляд" />
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script>
export default {
  name: 'HtmlPreviewDialog',
  props: {
    value: Boolean,
    title: { type: String, default: 'Попередній перегляд' },
    html: { type: String, default: '' },
    url: { type: String, default: '' },
    pdfUrl: { type: String, default: '' },
    loading: { type: Boolean, default: false },
    error: { type: String, default: '' }
  },
  methods: {
    print () {
      const f = this.$refs.frame;
      if (f && f.contentWindow) f.contentWindow.print();
    }
  }
};
</script>
