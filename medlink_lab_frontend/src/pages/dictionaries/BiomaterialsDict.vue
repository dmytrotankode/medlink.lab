<template>
  <div class="q-pa-md dict-page">
    <div class="row items-center justify-between q-mb-md">
      <h5 class="q-my-none text-weight-bold text-primary">
        <q-icon name="fas fa-vial" class="q-mr-sm" /> Довідник біоматеріалів
      </h5>
      <q-btn color="primary" icon="add" label="Додати біоматеріал" dense />
    </div>
    <q-card flat bordered>
      <q-table :data="items" :columns="columns" row-key="code" dense flat />
    </q-card>
  </div>
</template>

<script>
import { mockBiomaterials } from '../../services/mockData';
export default {
  name: 'BiomaterialsDict',
  data() {
    return {
      items: mockBiomaterials,
      columns: [
        { name: 'code', label: 'Код', field: 'code', align: 'left' },
        { name: 'name', label: 'Назва біоматеріалу', field: 'name', align: 'left' },
        { name: 'container', label: 'Рекомендований контейнер', field: 'container', align: 'left' },
        { name: 'snomed', label: 'SNOMED CT код', field: 'snomed', align: 'center' },
        { name: 'storage', label: 'Умови зберігання та стабільність', field: 'storage', align: 'left' }
      ]
    };
  }
};
</script>
<style scoped> .dict-page { background: #f7f9fa; min-height: 100%; } </style>
