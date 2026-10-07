<template>
  <div class="q-pa-md microbiology-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-bacterium" class="q-mr-sm" />
          Бактеріологія та антибіотикограма (EUCAST)
        </h5>
        <div class="text-caption text-grey-7">
          Культуральне дослідження, морфологія колоній, виділений збудник та визначення чутливості за міжнародним протоколом EUCAST.
        </div>
      </div>
      <div>
        <q-btn color="primary" icon="print" label="Друк бланка антибіотикограми" dense flat />
      </div>
    </div>

    <!-- Culture Info Card -->
    <q-card flat bordered class="q-mb-md bg-white">
      <q-card-section class="row items-center justify-between bg-grey-2 q-py-sm">
        <div class="text-subtitle2 text-weight-bold">
          Зразок: №{{ mb.sampleNumber }} | Пацієнт: {{ mb.patientName }} | Матеріал: {{ mb.material }}
        </div>
        <q-badge color="teal">Збудник ідентифіковано</q-badge>
      </q-card-section>
      <q-card-section class="row q-col-gutter-md">
        <div class="col-12 col-md-3">
          <div class="text-caption text-grey-7">Виділена мікрофлора:</div>
          <div class="text-subtitle1 text-weight-bold text-negative">{{ mb.pathogen }}</div>
        </div>
        <div class="col-12 col-md-3">
          <div class="text-caption text-grey-7">Титр колоній:</div>
          <div class="text-subtitle1 text-weight-bold">{{ mb.colonyCount }}</div>
        </div>
        <div class="col-12 col-md-3">
          <div class="text-caption text-grey-7">Морфологія:</div>
          <div class="text-subtitle1">{{ mb.gramType }}</div>
        </div>
        <div class="col-12 col-md-3">
          <div class="text-caption text-grey-7">Фенотип резистентності:</div>
          <div class="text-subtitle1 text-teal">{{ mb.phenotype }}</div>
        </div>
      </q-card-section>
    </q-card>

    <!-- Antibiotic Susceptibility Table -->
    <q-card flat bordered class="bg-white">
      <q-card-section class="bg-grey-2 q-py-sm">
        <div class="text-subtitle2 text-weight-bold text-dark">Чутливість до антибактеріальних препаратів (EUCAST 2026)</div>
      </q-card-section>
      <q-table
        :data="mb.antibiotics"
        :columns="columns"
        row-key="name"
        dense
        flat
      >
        <template v-slot:body-cell-eucast="props">
          <q-td :props="props">
            <q-badge :color="props.row.eucast === 'S' ? 'positive' : (props.row.eucast === 'I' ? 'warning' : 'negative')">
              {{ props.row.eucast }} ({{ formatEucast(props.row.eucast) }})
            </q-badge>
          </q-td>
        </template>
      </q-table>
    </q-card>
  </div>
</template>

<script>
import { mockMicrobiologyData } from '../../services/mockData';

export default {
  name: 'MicrobiologyCulture',
  data() {
    return {
      mb: mockMicrobiologyData,
      columns: [
        { name: 'name', label: 'Антибіотик', field: 'name', align: 'left' },
        { name: 'mic', label: 'МІК (мкг/мл)', field: 'mic', align: 'center' },
        { name: 'zone', label: 'Діаметр зони затримки (мм)', field: 'zone', align: 'center' },
        { name: 'eucast', label: 'Категорія EUCAST', field: 'eucast', align: 'center' },
        { name: 'interpretation', label: 'Клінічна інтерпретація', field: 'interpretation', align: 'left' }
      ]
    };
  },
  methods: {
    formatEucast(cat) {
      if (cat === 'S') return 'Чутливий';
      if (cat === 'I') return 'Помірно-чутливий';
      return 'Резистентний';
    }
  }
};
</script>
<style scoped>
.microbiology-page { background: #f7f9fa; min-height: 100%; }
</style>
