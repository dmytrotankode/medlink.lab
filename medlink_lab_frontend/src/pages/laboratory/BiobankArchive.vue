<template>
  <div class="q-pa-md biobank-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-snowflake" class="q-mr-sm" />
          Біобанк та архів зразків
        </h5>
        <div class="text-caption text-grey-7">
          Кріогенне зберігання та архів сироваток. Адресне розміщення: Морозильник &rarr; Полиця &rarr; Штатив &rarr; Комірка (9х9).
        </div>
      </div>
      <div>
        <q-btn color="primary" icon="search" label="Знайти зразок за штрихкодом" dense flat />
      </div>
    </div>

    <div class="row q-col-gutter-md">
      <!-- Freezer visual rack -->
      <div class="col-12 col-md-5">
        <q-card flat bordered class="q-pa-md">
          <div class="text-subtitle1 text-weight-bold text-primary q-mb-sm">
            Кріосховище №1 (-80°C UltraLow)
          </div>
          <div class="text-caption text-grey-7 q-mb-md">Полиця A &rarr; Штатив #4 (Сироватки контрольні)</div>
          
          <!-- 9x9 Grid Representation -->
          <div class="grid-container q-pa-sm bg-grey-2 rounded-borders">
            <div v-for="r in 9" :key="'r'+r" class="row no-wrap justify-between q-mb-xs">
              <div v-for="c in 9" :key="'c'+c" 
                   class="grid-cell flex flex-center text-caption"
                   :class="(r===2 && c===5) ? 'bg-primary text-white text-weight-bold' : ((r%2===0 && c%3===0) ? 'bg-teal-3' : 'bg-white')">
                {{ String.fromCharCode(64+r) }}{{ c }}
              </div>
            </div>
          </div>
          <div class="row items-center justify-between q-mt-sm text-caption">
            <span><span class="dot bg-primary"></span> Вибраний зразок (B5)</span>
            <span><span class="dot bg-teal-3"></span> Зайнято (34/81)</span>
            <span><span class="dot bg-white border"></span> Вільно</span>
          </div>
        </q-card>
      </div>

      <!-- Sample Info -->
      <div class="col-12 col-md-7">
        <q-card flat bordered>
          <q-card-section class="bg-grey-2 q-py-sm">
            <div class="text-subtitle2 text-weight-bold">Картка архівного зразка у комірці B5</div>
          </q-card-section>
          <q-card-section class="q-pa-md">
            <div class="text-body2 q-mb-xs">Штрихкод: <strong>1026004819</strong></div>
            <div class="text-body2 q-mb-xs">Пацієнт: <strong>Коваленко Олександр Сергійович</strong></div>
            <div class="text-body2 q-mb-xs">Тип біоматеріалу: <strong>Сироватка крові (Аліквота №1, 1.5 мл)</strong></div>
            <div class="text-body2 q-mb-xs">Дата заморозки: <strong>2026-10-06 10:30</strong></div>
            <div class="text-body2 q-mb-xs">Термін зберігання: <strong>до 2027-04-06 (6 місяців)</strong></div>
            <div class="text-body2 q-mb-md">Температурний режим: <strong>-80°C ± 2°C (Морозильник Panasonic VIP)</strong></div>

            <div class="row q-gutter-sm">
              <q-btn color="primary" dense icon="replay" label="Видати для повторного аналізу" />
              <q-btn color="negative" dense outline icon="delete" label="Утилізація після закінчення терміну" />
            </div>
          </q-card-section>
        </q-card>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'BiobankArchive'
};
</script>
<style scoped>
.biobank-page { background: #f7f9fa; min-height: 100%; }
.grid-container { border: 1px solid #ccc; max-width: 320px; margin: 0 auto; }
.grid-cell { width: 28px; height: 28px; border: 1px solid #ddd; font-size: 10px; cursor: pointer; border-radius: 3px; }
.dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 4px; }
.border { border: 1px solid #bbb; }
</style>
