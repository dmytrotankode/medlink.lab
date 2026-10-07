<template>
  <div class="q-pa-md validation-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-negative">
          <q-icon name="fas fa-exclamation-triangle" class="q-mr-sm" />
          Валідація результатів & Панічні алерти
        </h5>
        <div class="text-caption text-grey-7">
          Пост-аналітичний етап лікаря-лаборанта. Обов'язкова телефонна фіксація критичних значень у стаціонар/ВРІТ та підпис ЕМЗ.
        </div>
      </div>
      <div class="row q-gutter-sm">
        <q-btn color="negative" icon="call" label="Журнал викликів ВРІТ" @click="showCallLog = true" />
      </div>
    </div>

    <!-- Panic Banner -->
    <q-banner dense rounded class="bg-red-2 text-negative q-mb-md" inline-actions>
      <template v-slot:avatar>
        <q-icon name="warning" color="negative" size="32px" />
      </template>
      <div class="text-subtitle1 text-weight-bold">Критичне панічне значення потребує негайної реєстрації!</div>
      <div class="text-body2">
        Пацієнт: <strong>Мельник Ю.В. (48 р.)</strong> | Відділення: <strong>ВРІТ</strong> | Глюкоза сироватки: <strong>26.4 ммоль/л</strong> (Норма: 4.1 - 5.9). Ризик гіперосмолярної коми!
      </div>
      <template v-slot:action>
        <q-btn color="negative" label="Зареєструвати телефонний дзвінок" @click="openCallDialog" />
      </template>
    </q-banner>

    <!-- Verification Table -->
    <q-card flat bordered>
      <q-card-section class="bg-grey-2 q-py-sm">
        <div class="text-subtitle2 text-weight-bold text-primary">Черга результатів на медичну валідацію лікарем</div>
      </q-card-section>
      <q-table
        :data="pendingValidation"
        :columns="columns"
        row-key="id"
        dense
        flat
      >
        <template v-slot:body-cell-value="props">
          <q-td :props="props" class="text-weight-bold text-negative">
            {{ props.row.value }} {{ props.row.unit }}
          </q-td>
        </template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="q-gutter-xs">
            <q-btn size="sm" color="positive" dense icon="check" label="Валідувати" @click="validateResult(props.row)" />
            <q-btn size="sm" color="warning" text-color="dark" dense icon="replay" label="Повтор" @click="recheckResult(props.row)" />
          </q-td>
        </template>
      </q-table>
    </q-card>

    <!-- Call Log Dialog -->
    <q-dialog v-model="showCallDialog">
      <q-card style="min-width: 500px;">
        <q-card-section class="bg-negative text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="phone_in_talk" class="q-mr-sm" /> Фіксація дзвінка панічного значення (SLA &lt; 15 хв)
          </div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="text-caption text-grey-8 q-mb-sm">Згідно наказу МОЗ України та стандартів ISO 15189 дзвінок має бути здійснений черговому лікарю стаціонару негайно.</div>
          <q-input v-model="callData.doctor" label="ПІБ лікаря, який прийняв виклик" outlined dense class="q-mb-sm" />
          <q-input v-model="callData.department" label="Відділення" outlined dense class="q-mb-sm" />
          <q-input v-model="callData.phone" label="Номер телефону" outlined dense class="q-mb-sm" />
          <q-input v-model="callData.readback" label="Зворотне підтвердження (Read-back виконано)" outlined dense class="q-mb-sm" />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="negative" label="Зберегти в протокол" @click="saveCallRecord" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import { mockWorklistResults } from '../../services/mockData';

export default {
  name: 'ValidationPanic',
  data() {
    return {
      showCallDialog: false,
      showCallLog: false,
      callData: {
        doctor: 'Савченко І.О. (Черговий реаніматолог)',
        department: 'ВРІТ',
        phone: 'вн. 214',
        readback: 'Значення 26.4 ммоль/л повторено та підтверджено голосом'
      },
      results: mockWorklistResults,
      columns: [
        { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'testName', label: 'Показник', field: 'testName', align: 'left' },
        { name: 'value', label: 'Результат', field: 'value', align: 'right' },
        { name: 'flag', label: 'Флаг', field: 'flag', align: 'center' },
        { name: 'comment', label: 'Клінічний коментар', field: 'comment', align: 'left' },
        { name: 'actions', label: 'Дії лікаря', align: 'center' }
      ]
    };
  },
  computed: {
    pendingValidation() {
      return this.results.filter(r => r.status === 'PENDING_VERIFY');
    }
  },
  methods: {
    openCallDialog() {
      this.showCallDialog = true;
    },
    saveCallRecord() {
      this.$q.notify({ type: 'positive', message: 'Виклик зафіксовано в журналі передачі критичних значень.' });
      this.showCallDialog = false;
    },
    validateResult(row) {
      row.status = 'VERIFIED';
      this.$q.notify({ type: 'positive', message: `Результат ${row.testName} валідовано лікарем КДЛ.` });
    },
    recheckResult(row) {
      this.$q.notify({ type: 'warning', message: `Результат ${row.testName} відправлено на повторний аналіз.` });
    }
  }
};
</script>
<style scoped>
.validation-page { background: #f7f9fa; min-height: 100%; }
</style>
