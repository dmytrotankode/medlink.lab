<template>
  <q-dialog :value="value" @input="$emit('input', $event)">
    <q-card style="min-width: 720px; max-width: 95vw" data-testid="labelsDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="print" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Етикетки пробірок 40×25 мм (Code128)</div>
        <q-space />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section>
        <div v-if="loading" class="text-center q-pa-lg"><q-spinner color="primary" size="36px" /></div>
        <empty-state v-else-if="!labels.length" title="Етикеток немає" hint="API не повернуло етикетки для цього замовлення" icon="label_off" />
        <div v-else class="row q-col-gutter-md">
          <div v-for="l in labels" :key="l.barcode" class="col-12 col-md-6">
            <label-sticker :label="l" />
            <div class="row items-center justify-center q-gutter-sm q-mt-xs">
              <q-btn dense flat size="sm" color="primary" icon="print" label="Друк етикетки" :disable="!l.zpl" @click="printLabelsViaAgent([l])" />
              <q-btn dense flat size="sm" color="primary" icon="content_copy" label="ZPL" :disable="!l.zpl" @click="copyZpl(l)" />
              <q-btn dense flat size="sm" color="grey-8" icon="code" :disable="!l.zpl" @click="showZpl = showZpl === l.barcode ? null : l.barcode" />
            </div>
            <pre v-if="showZpl === l.barcode" class="mono q-pa-sm bg-grey-2" style="font-size: 11px; white-space: pre-wrap; border-radius: 4px">{{ l.zpl }}</pre>
          </div>
        </div>
        <div class="text-caption text-grey-6 q-mt-sm"><q-icon name="info" /> «Друк етикетки» надсилає ZPL локальному агенту MedLink LabConnector (localhost:5088); якщо агент не запущено — скористайтесь друком SVG.</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Закрити" v-close-popup />
        <q-btn outline color="primary" icon="picture_as_pdf" label="Друк SVG (браузер)" :disable="!labels.length" @click="printSvg" />
        <q-btn color="primary" icon="print" label="Друк усіх етикеток" :disable="!labels.length" data-testid="printAllLabels" @click="printLabelsViaAgent(labels)" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import LabelSticker from './LabelSticker.vue';
import labelPrintMixin from '../../mixins/labelPrintMixin';
import { copyToClipboard } from '../../utils/format';
import { printLabelsSvg } from '../../services/printAgentService';

export default {
  name: 'LabelsDialog',
  mixins: [labelPrintMixin],
  components: { LabelSticker },
  props: {
    value: Boolean,
    labels: { type: Array, default: () => [] },
    loading: { type: Boolean, default: false }
  },
  data () {
    return { showZpl: null };
  },
  methods: {
    copyZpl (l) {
      copyToClipboard(l.zpl).then(() => this.$q.notify({ type: 'positive', message: `ZPL для ${l.barcode} скопійовано` }));
    },
    printSvg () { printLabelsSvg(this.labels); }
  }
};
</script>
