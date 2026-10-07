<template>
  <div class="q-pa-md patient-portal-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-user-circle" class="q-mr-sm" />
          Кабінет пацієнта (Моніторинг замовлення)
        </h5>
        <div class="text-caption text-grey-7">
          Відстеження життєвого циклу пробірок у реальному часі, динаміка показників та офіційний PDF-бланк результатів з печаткою лабораторії.
        </div>
      </div>
      <div>
        <q-btn color="primary" icon="picture_as_pdf" label="Завантажити PDF-бланк" @click="downloadPdf" />
      </div>
    </div>

    <!-- Patient Header Card -->
    <q-card flat bordered class="q-mb-md bg-white">
      <q-card-section class="row items-center justify-between">
        <div class="row items-center q-gutter-md">
          <q-avatar size="50px" color="primary" text-color="white" icon="person" />
          <div>
            <div class="text-h6 text-weight-bold text-dark">{{ patient.fullName }}</div>
            <div class="text-caption text-grey-7">
              Дата народження: {{ patient.birthDate }} ({{ patient.age }} р.) | Карта: №{{ patient.cardNumber }} | Тел: {{ patient.phone }}
            </div>
          </div>
        </div>
        <q-chip color="teal" text-color="white" icon="verified">Замовлення готове (100%)</q-chip>
      </q-card-section>
    </q-card>

    <!-- Real-time Progress Stepper -->
    <q-card flat bordered class="q-mb-md q-pa-md bg-white">
      <div class="text-subtitle2 text-weight-bold q-mb-sm text-primary">Трекінг виконання замовлення №{{ order.orderNumber }}</div>
      <q-stepper v-model="step" ref="stepper" color="primary" animated header-nav dense>
        <q-step :name="1" title="Забір зразка" icon="fas fa-syringe" :done="step > 1">
          Здійснено в маніпуляційному кабінеті №3 о 08:45
        </q-step>
        <q-step :name="2" title="Транспортування" icon="fas fa-truck" :done="step > 2">
          Доставлено термоконтейнером TC-201 (+4.2°C) о 09:20
        </q-step>
        <q-step :name="3" title="Аналіз на приладі" icon="fas fa-microscope" :done="step > 3">
          Досліджено на аналізаторі Sysmex XN-1000 о 09:50
        </q-step>
        <q-step :name="4" title="Валідація лікарем" icon="verified" :done="step >= 4">
          Підписано ЕЦП лікаря КДЛ Мельник В.С. о 10:15
        </q-step>
      </q-stepper>
    </q-card>

    <!-- Results Table -->
    <q-card flat bordered class="bg-white">
      <q-card-section class="bg-grey-2 q-py-sm">
        <div class="text-subtitle2 text-weight-bold text-dark">Результати лабораторних досліджень</div>
      </q-card-section>
      <q-table
        :data="patientResults"
        :columns="columns"
        row-key="id"
        dense
        flat
      >
        <template v-slot:body-cell-value="props">
          <q-td :props="props" class="text-weight-bold">
            {{ props.row.value }} {{ props.row.unit }}
          </q-td>
        </template>
        <template v-slot:body-cell-norm="props">
          <q-td :props="props">
            {{ props.row.normMin }} - {{ props.row.normMax }}
          </q-td>
        </template>
        <template v-slot:body-cell-flag="props">
          <q-td :props="props">
            <q-badge color="positive">НОРМА</q-badge>
          </q-td>
        </template>
      </q-table>
    </q-card>
  </div>
</template>

<script>
import { mockPatients, mockOrders, mockWorklistResults } from '../../services/mockData';

export default {
  name: 'PatientPortal',
  data() {
    return {
      step: 4,
      patient: mockPatients[0],
      order: mockOrders[0],
      columns: [
        { name: 'testName', label: 'Показник', field: 'testName', align: 'left' },
        { name: 'value', label: 'Результат', field: 'value', align: 'right' },
        { name: 'norm', label: 'Референсний інтервал', align: 'center' },
        { name: 'flag', label: 'Статус', align: 'center' }
      ]
    };
  },
  computed: {
    patientResults() {
      return mockWorklistResults.filter(r => r.flag === 'NORMAL');
    }
  },
  methods: {
    downloadPdf() {
      this.$q.notify({ type: 'positive', message: 'Офіційний бланк з QR-кодом та ЕЦП сформовано для завантаження.' });
    }
  }
};
</script>
<style scoped>
.patient-portal-page { background: #f7f9fa; min-height: 100%; }
</style>
