<template>
  <q-dialog :value="value" @input="$emit('input', $event)">
    <q-card style="min-width: 720px; max-width: 95vw" data-testid="labelsDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="print" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Етикетки пробірок</div>
        <q-space />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section>
        <div v-if="loading" class="text-center q-pa-lg"><q-spinner color="primary" size="36px" /></div>
        <empty-state v-else-if="!labels.length" title="Етикеток немає" hint="Для цього направлення етикеток немає" icon="label_off" />
        <div v-else class="row q-col-gutter-md">
          <div v-for="l in labels" :key="l.barcode" class="col-12 col-md-6">
            <label-sticker :label="l" />
            <div class="row items-center justify-center q-gutter-sm q-mt-xs">
              <q-btn dense flat size="sm" color="primary" icon="print" label="Друк етикетки" :disable="!l.zpl" @click="printLabelsViaAgent([l])" />
              <q-btn v-if="isAdmin" dense flat size="sm" color="grey-8" icon="content_copy" label="Копіювати код" :disable="!l.zpl" @click="copyZpl(l)" />
              <q-btn v-if="isAdmin" dense flat size="sm" color="grey-8" icon="code" label="Показати код" :disable="!l.zpl" @click="showZpl = showZpl === l.barcode ? null : l.barcode" />
            </div>
            <pre v-if="showZpl === l.barcode" class="mono q-pa-sm bg-grey-2" style="font-size: 11px; white-space: pre-wrap; border-radius: 4px">{{ l.zpl }}</pre>
          </div>
        </div>
        <div class="text-caption text-grey-6 q-mt-sm"><q-icon name="info" /> Друк виконується на принтері етикеток. Якщо принтер не відповідає — скористайтесь кнопкою «Друк через браузер».</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Закрити" v-close-popup />
        <q-btn outline color="primary" icon="print" label="Друк через браузер" :disable="!labels.length" @click="printSvg" />
        <q-btn color="primary" icon="print" label="Друк усіх етикеток" :disable="!labels.length" data-testid="printAllLabels" @click="printLabelsViaAgent(labels)" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import LabelSticker from './LabelSticker.vue';
import labelPrintMixin from '../../mixins/labelPrintMixin';
import roleMixin from '../../mixins/roleMixin';
import { copyToClipboard } from '../../utils/format';
import { printLabelsSvg } from '../../services/printAgentService';

export default {
  name: 'LabelsDialog',
  mixins: [labelPrintMixin, roleMixin],
  components: { LabelSticker },
  props: {
    value: Boolean,
    labels: { type: Array, default: () => [] },
    loading: { type: Boolean, default: false }
  },
  data () {
    return { showZpl: null };
  },
  computed: {
    // службові дії з кодом етикетки — лише для адміністратора
    isAdmin () { return this.canDo(); }
  },
  methods: {
    copyZpl (l) {
      copyToClipboard(l.zpl).then(() => this.$q.notify({ type: 'positive', message: `Код етикетки ${l.barcode} скопійовано` }));
    },
    printSvg () { printLabelsSvg(this.labels); }
  }
};
</script>
