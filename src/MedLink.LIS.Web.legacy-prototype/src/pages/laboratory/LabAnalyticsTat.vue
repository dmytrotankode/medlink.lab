<template>
  <div class="q-pa-md analytics-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-chart-pie" class="q-mr-sm" />
          Операційна аналітика лабораторії & Turnaround Time (TAT)
        </h5>
        <div class="text-caption text-grey-7">
          Ключові показники ефективності (KPI). Виконання нормативів часу видачі CITO / планових результатів, відсоток гемолізу та простої приладів.
        </div>
      </div>
      <div>
        <q-btn color="primary" icon="file_download" label="Експорт звіту Excel" dense flat />
      </div>
    </div>

    <!-- KPIs Row -->
    <div class="row q-col-gutter-md q-mb-md">
      <div class="col-12 col-md-3">
        <q-card flat bordered class="q-pa-md bg-white text-center">
          <div class="text-caption text-grey-7">Середній час видачі CITO</div>
          <div class="text-h5 text-weight-bold text-positive">28.4 хв</div>
          <div class="text-caption text-teal">Норматив: &lt; 45 хв (SLA 98.2%)</div>
        </q-card>
      </div>
      <div class="col-12 col-md-3">
        <q-card flat bordered class="q-pa-md bg-white text-center">
          <div class="text-caption text-grey-7">Середній час Routine TAT</div>
          <div class="text-h5 text-weight-bold text-primary">2 год 14 хв</div>
          <div class="text-caption text-grey-7">Норматив: &lt; 4 год</div>
        </q-card>
      </div>
      <div class="col-12 col-md-3">
        <q-card flat bordered class="q-pa-md bg-white text-center">
          <div class="text-caption text-grey-7">Відсоток браку біоматеріалу</div>
          <div class="text-h5 text-weight-bold text-warning">0.74%</div>
          <div class="text-caption text-positive">Ціль: &lt; 1.0% (Відповідає нормі)</div>
        </q-card>
      </div>
      <div class="col-12 col-md-3">
        <q-card flat bordered class="q-pa-md bg-white text-center">
          <div class="text-caption text-grey-7">Всього досліджень за добу</div>
          <div class="text-h5 text-weight-bold text-teal">1,482</div>
          <div class="text-caption text-grey-7">+12% до минулого тижня</div>
        </q-card>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'LabAnalyticsTat'
};
</script>
<style scoped>
.analytics-page { background: #f7f9fa; min-height: 100%; }
</style>
