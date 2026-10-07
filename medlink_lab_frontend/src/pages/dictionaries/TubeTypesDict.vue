<template>
  <div class="q-pa-md dict-page">
    <div class="row items-center justify-between q-mb-md">
      <h5 class="q-my-none text-weight-bold text-primary">
        <q-icon name="fas fa-flask" class="q-mr-sm" /> Довідник пробірок та контейнерів
      </h5>
      <q-btn color="primary" icon="add" label="Додати пробірку" dense />
    </div>
    <q-card flat bordered>
      <q-table :data="items" :columns="columns" row-key="code" dense flat>
        <template v-slot:body-cell-color="props">
          <q-td :props="props">
            <q-badge :style="{ backgroundColor: props.row.colorHex }" text-color="white">
              {{ props.row.colorName }}
            </q-badge>
          </q-td>
        </template>
      </q-table>
    </q-card>
  </div>
</template>

<script>
export default {
  name: 'TubeTypesDict',
  data() {
    return {
      items: [
        { code: 'TUBE-CITRATE', name: 'Цитрат натрію 3.2%', colorName: 'Блакитна', colorHex: '#0284c7', volume: '3.0 мл', orderOfDraw: 1, inversions: '3-4 рази' },
        { code: 'TUBE-SERUM-GEL', name: 'Активатор згортання / Гель', colorName: 'Жовта', colorHex: '#ca8a04', volume: '5.0 мл', orderOfDraw: 2, inversions: '5-6 разів' },
        { code: 'TUBE-EDTA', name: 'K2 / K3 ЕДТА', colorName: 'Фіолетова', colorHex: '#9333ea', volume: '2.6 мл', orderOfDraw: 3, inversions: '8-10 разів' },
        { code: 'TUBE-FLUORIDE', name: 'Натрій фторид / Оксалат', colorName: 'Сіра', colorHex: '#4b5563', volume: '2.0 мл', orderOfDraw: 4, inversions: '5-8 разів' }
      ],
      columns: [
        { name: 'code', label: 'Код', field: 'code', align: 'left' },
        { name: 'name', label: 'Наповнювач', field: 'name', align: 'left' },
        { name: 'color', label: 'Колір кришки', align: 'center' },
        { name: 'volume', label: 'Об'єм', field: 'volume', align: 'center' },
        { name: 'orderOfDraw', label: 'Order of Draw', field: 'orderOfDraw', align: 'center' },
        { name: 'inversions', label: 'Кількість перевертань', field: 'inversions', align: 'left' }
      ]
    };
  }
};
</script>
<style scoped> .dict-page { background: #f7f9fa; min-height: 100%; } </style>
