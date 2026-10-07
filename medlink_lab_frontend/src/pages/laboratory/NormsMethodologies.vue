<template>
  <div class="q-pa-md norms-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-sliders-h" class="q-mr-sm" />
          Налаштування референсних інтервалів та методик
        </h5>
        <div class="text-caption text-grey-7">
          Клінічні норми за статтю (Ч/Ж) та віковими групами, порогові критичні значення (Panic alert limits) та методики вимірювання.
        </div>
      </div>
      <div>
        <q-btn color="primary" icon="add" label="Додати віковий діапазон" dense />
      </div>
    </div>

    <!-- Norms Table -->
    <q-card flat bordered>
      <q-table
        :data="norms"
        :columns="columns"
        row-key="id"
        dense
        flat
      >
        <template v-slot:body-cell-panicRange="props">
          <q-td :props="props" class="text-negative text-weight-bold">
            {{ props.row.panicLow }} / {{ props.row.panicHigh }}
          </q-td>
        </template>
      </q-table>
    </q-card>
  </div>
</template>

<script>
export default {
  name: 'NormsMethodologies',
  data() {
    return {
      norms: [
        { id: 1, param: 'Глюкоза сироватки', gender: 'Обидва', ageGroup: 'Дорослі (18-60 р.)', method: 'Гексокіназний (IFCC)', unit: 'ммоль/л', normMin: 4.10, normMax: 5.90, panicLow: 2.50, panicHigh: 25.00 },
        { id: 2, param: 'Гемоглобін (HGB)', gender: 'Чоловіки', ageGroup: 'Дорослі (>18 р.)', method: 'SLS-метод (безціанідний)', unit: 'г/л', normMin: 130.0, normMax: 160.0, panicLow: 70.0, panicHigh: 200.0 },
        { id: 3, param: 'Гемоглобін (HGB)', gender: 'Жінки', ageGroup: 'Дорослі (>18 р.)', method: 'SLS-метод (безціанідний)', unit: 'г/л', normMin: 120.0, normMax: 150.0, panicLow: 70.0, panicHigh: 200.0 },
        { id: 4, param: 'Креатинін сироватки', gender: 'Чоловіки', ageGroup: 'Дорослі (>18 р.)', method: 'Ензиматичний (IDMS-калібрований)', unit: 'мкмоль/л', normMin: 62.0, normMax: 115.0, panicLow: 30.0, panicHigh: 350.0 },
        { id: 5, param: 'АЛТ (Аланінамінотрансфераза)', gender: 'Обидва', ageGroup: 'Дорослі', method: 'Оптичний кінетичний (IFCC без П-5-Ф)', unit: 'U/L', normMin: 0.0, normMax: 41.0, panicLow: '-', panicHigh: 300.0 }
      ],
      columns: [
        { name: 'param', label: 'Показник', field: 'param', align: 'left', sortable: true },
        { name: 'gender', label: 'Стать', field: 'gender', align: 'center' },
        { name: 'ageGroup', label: 'Вікова категорія', field: 'ageGroup', align: 'left' },
        { name: 'method', label: 'Методика дослідження', field: 'method', align: 'left' },
        { name: 'unit', label: 'Одиниця', field: 'unit', align: 'center' },
        { name: 'normMin', label: 'Норма Min', field: 'normMin', align: 'right' },
        { name: 'normMax', label: 'Норма Max', field: 'normMax', align: 'right' },
        { name: 'panicRange', label: 'Панічний поріг (Low/High)', align: 'center' }
      ]
    };
  }
};
</script>
<style scoped>
.norms-page { background: #f7f9fa; min-height: 100%; }
</style>
