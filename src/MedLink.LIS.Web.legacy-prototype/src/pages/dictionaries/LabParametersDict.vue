<template>
  <div class="q-pa-md dict-page">
    <div class="row items-center justify-between q-mb-md">
      <h5 class="q-my-none text-weight-bold text-primary">
        <q-icon name="fas fa-list-ol" class="q-mr-sm" /> Довідник лабораторних показників та профілів
      </h5>
      <q-btn color="primary" icon="add" label="Додати показник" dense />
    </div>
    <q-card flat bordered>
      <q-table :data="items" :columns="columns" row-key="code" dense flat />
    </q-card>
  </div>
</template>

<script>
export default {
  name: 'LabParametersDict',
  data() {
    return {
      items: [
        { code: 'WBC', name: 'Лейкоцити (White Blood Cells)', category: 'Гематологія', loinc: '6690-2', unit: '10*9/л', sampleType: 'EDTA кров' },
        { code: 'HGB', name: 'Гемоглобін (Hemoglobin)', category: 'Гематологія', loinc: '718-7', unit: 'г/л', sampleType: 'EDTA кров' },
        { code: 'GLU', name: 'Глюкоза сироватки', category: 'Біохімія', loinc: '2345-7', unit: 'ммоль/л', sampleType: 'Сироватка / Фторидна плазма' },
        { code: 'ALT', name: 'Аланінамінотрансфераза (АЛТ)', category: 'Біохімія', loinc: '1742-6', unit: 'U/L', sampleType: 'Сироватка' },
        { code: 'CREAT', name: 'Креатинін сироватки', category: 'Біохімія', loinc: '2160-0', unit: 'мкмоль/л', sampleType: 'Сироватка' },
        { code: 'INR', name: 'МНВ (Міжнародне нормалізоване відношення)', category: 'Коагулограма', loinc: '6301-6', unit: 'ум.од.', sampleType: 'Цитратна плазма' }
      ],
      columns: [
        { name: 'code', label: 'Код (LIS / Analyzer)', field: 'code', align: 'left', sortable: true },
        { name: 'name', label: 'Назва показника', field: 'name', align: 'left', sortable: true },
        { name: 'category', label: 'Категорія', field: 'category', align: 'left' },
        { name: 'loinc', label: 'LOINC код', field: 'loinc', align: 'center' },
        { name: 'unit', label: 'Одиниця виміру', field: 'unit', align: 'center' },
        { name: 'sampleType', label: 'Рекомендований зразок', field: 'sampleType', align: 'left' }
      ]
    };
  }
};
</script>
<style scoped> .dict-page { background: #f7f9fa; min-height: 100%; } </style>
