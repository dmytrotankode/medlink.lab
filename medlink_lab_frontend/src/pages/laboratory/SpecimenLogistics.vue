<template>
  <div class="q-pa-md logistics-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-truck" class="q-mr-sm" />
          Логістика зразків та термоконтроль
        </h5>
        <div class="text-caption text-grey-7">
          Трекінг термоконтейнерів віддалених пунктів забору, фіксація логістичних маніфестів та відбраковка біоматеріалу.
        </div>
      </div>
      <div class="row q-gutter-sm">
        <q-btn color="secondary" icon="refresh" label="Оновити" dense flat />
        <q-btn color="primary" icon="post_add" label="Створити маніфест" @click="showCreateManifest = true" />
      </div>
    </div>

    <!-- Active Shipments Grid -->
    <div class="row q-col-gutter-md q-mb-md">
      <div class="col-12 col-md-4">
        <q-card flat bordered class="bg-blue-1">
          <q-card-section>
            <div class="row items-center justify-between">
              <div class="text-subtitle1 text-weight-bold text-primary">
                <q-icon name="fas fa-box" class="q-mr-xs" /> Контейнер #TC-201
              </div>
              <q-badge color="positive">В дорозі (+4.2°C)</q-badge>
            </div>
            <div class="text-caption q-mt-sm">
              Маршрут: <strong>Пункт №3 (Лівий Берег) &rarr; Центральна КДЛ</strong><br>
              Кур'єр: <strong>Шевчук Д. (Авто №KA4412BC)</strong><br>
              Кількість пробірок: <strong>24 шт.</strong>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="between">
            <span class="text-caption text-grey-7">Виїзд: 08:30 (Очікується: 09:20)</span>
            <q-btn size="sm" color="primary" dense label="Прийняти" @click="acceptManifest('TC-201')" />
          </q-card-actions>
        </q-card>
      </div>

      <div class="col-12 col-md-4">
        <q-card flat bordered class="bg-green-1">
          <q-card-section>
            <div class="row items-center justify-between">
              <div class="text-subtitle1 text-weight-bold text-positive">
                <q-icon name="fas fa-check-circle" class="q-mr-xs" /> Контейнер #TC-104
              </div>
              <q-badge color="teal">Доставлено (+3.8°C)</q-badge>
            </div>
            <div class="text-caption q-mt-sm">
              Маршрут: <strong>Поліклініка №1 &rarr; Центральна КДЛ</strong><br>
              Кур'єр: <strong>Коваль М. (Піший)</strong><br>
              Кількість пробірок: <strong>12 шт.</strong>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="between">
            <span class="text-caption text-grey-7">Прийнято о 08:55</span>
            <q-btn size="sm" color="teal" flat dense label="Переглянути акт" />
          </q-card-actions>
        </q-card>
      </div>

      <div class="col-12 col-md-4">
        <q-card flat bordered class="bg-amber-1">
          <q-card-section>
            <div class="row items-center justify-between">
              <div class="text-subtitle1 text-weight-bold text-warning">
                <q-icon name="fas fa-temperature-high" class="q-mr-xs" /> Контейнер #TC-309
              </div>
              <q-badge color="warning" text-color="dark">Увага (+9.5°C)</q-badge>
            </div>
            <div class="text-caption q-mt-sm">
              Маршрут: <strong>Денний стаціонар &rarr; Центральна КДЛ</strong><br>
              Попередження: <strong>Перевищено температурний поріг 8°C!</strong><br>
              Кількість пробірок: <strong>6 шт.</strong>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="between">
            <span class="text-caption text-negative">Термологер #TL-441</span>
            <q-btn size="sm" color="negative" dense label="Брак зразків" @click="showRejectionDialog = true" />
          </q-card-actions>
        </q-card>
      </div>
    </div>

    <!-- Rejection Dialog -->
    <q-dialog v-model="showRejectionDialog">
      <q-card style="min-width: 480px;">
        <q-card-section class="bg-negative text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="warning" class="q-mr-sm" /> Відбраковка біоматеріалу
          </div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="text-body2 q-mb-sm">Оберіть причину відмови та зафіксуйте акт дефектури:</div>
          <q-select v-model="rejectionReason" :options="rejectionReasons" label="Причина дефектури" outlined dense class="q-mb-md" />
          <q-input v-model="rejectionComment" type="textarea" label="Коментар експерта КДЛ" outlined dense rows="3" />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="negative" label="Зафіксувати брак та запитати перезабір" @click="confirmRejection" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
export default {
  name: 'SpecimenLogistics',
  data() {
    return {
      showCreateManifest: false,
      showRejectionDialog: false,
      rejectionReason: 'Порушення температурного режиму доставки (> 8°C)',
      rejectionComment: 'Зафіксовано за даними термологера TL-441. Зразки непридатні для аналізу коагулограми.',
      rejectionReasons: [
        'Порушення температурного режиму доставки (> 8°C)',
        'Виражений гемоліз (візуально / індекс H > 300)',
        'Згусток у пробірці з ЕДТА (мікрозгусток)',
        'Хілус / виражена ліпемія (L > 500)',
        'Недостатній обʼєм біоматеріалу (Underfilling)',
        'Пошкодження або протікання контейнера'
      ]
    };
  },
  methods: {
    acceptManifest(id) {
      this.$q.notify({ type: 'positive', message: `Маніфест для ${id} успішно прийнято у лабораторії.` });
    },
    confirmRejection() {
      this.$q.notify({ type: 'negative', message: 'Зразки позначено як БРАК. Лікаря сповіщено для повторного забору.' });
      this.showRejectionDialog = false;
    }
  }
};
</script>
<style scoped>
.logistics-page { background: #f7f9fa; min-height: 100%; }
</style>
