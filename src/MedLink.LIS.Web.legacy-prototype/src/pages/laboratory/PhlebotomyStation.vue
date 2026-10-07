<template>
  <div class="q-pa-md phlebotomy-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-syringe" class="q-mr-sm" />
          Пункт забору біоматеріалу (Маніпуляційний кабінет)
        </h5>
        <div class="text-caption text-grey-7">
          Реєстратура та маніпуляційний кабінет. Формування замовлення, Order of Draw, валідація преаналітики та друк штрихкодів ZPL.
        </div>
      </div>
      <div class="row q-gutter-sm">
        <q-btn color="secondary" icon="refresh" label="Оновити" dense flat @click="refreshData" />
        <q-btn color="primary" icon="add" label="Нове замовлення" @click="showNewOrderDialog = true" />
      </div>
    </div>

    <!-- Filters & Search -->
    <q-card flat bordered class="q-mb-md bg-grey-1">
      <q-card-section class="q-pa-sm row q-col-gutter-sm items-center">
        <div class="col-12 col-md-4">
          <q-input v-model="searchQuery" outlined dense placeholder="Пошук за ПІБ, картою чи № замовлення..." bg-color="white">
            <template v-slot:append>
              <q-icon name="search" />
            </template>
          </q-input>
        </div>
        <div class="col-6 col-md-3">
          <q-select v-model="priorityFilter" :options="['Всі пріоритети', 'CITO (Терміново)', 'Планові']" outlined dense bg-color="white" />
        </div>
        <div class="col-6 col-md-3">
          <q-select v-model="statusFilter" :options="['Всі статуси', 'Очікує забору', 'Забір виконано', 'Відправлено в лабораторію']" outlined dense bg-color="white" />
        </div>
        <div class="col-12 col-md-2 text-right">
          <q-chip outline color="negative" text-color="negative" icon="priority_high">CITO: {{ urgentCount }}</q-chip>
        </div>
      </q-card-section>
    </q-card>

    <!-- Orders Table -->
    <q-card flat bordered>
      <q-table
        :data="filteredOrders"
        :columns="orderColumns"
        row-key="id"
        dense
        flat
        :pagination.sync="pagination"
      >
        <template v-slot:body-cell-priority="props">
          <q-td :props="props">
            <q-badge :color="props.row.priority === 'CITO' ? 'negative' : 'grey-7'" text-color="white">
              {{ props.row.priority }}
            </q-badge>
          </q-td>
        </template>

        <template v-slot:body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="getStatusColor(props.row.status)">
              {{ formatStatus(props.row.status) }}
            </q-badge>
          </q-td>
        </template>

        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="q-gutter-xs">
            <q-btn size="sm" color="primary" dense icon="fas fa-barcode" label="Друк ZPL" @click="openBarcodeDialog(props.row)">
              <q-tooltip>Друк етикеток на термопринтер</q-tooltip>
            </q-btn>
            <q-btn size="sm" color="teal" dense icon="check" label="Забір" @click="openCollectionDialog(props.row)">
              <q-tooltip>Підтвердити забір пробірок</q-tooltip>
            </q-btn>
          </q-td>
        </template>
      </q-table>
    </q-card>

    <!-- Barcode Print Dialog -->
    <q-dialog v-model="showBarcodeDialog">
      <q-card style="min-width: 450px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="fas fa-barcode" class="q-mr-sm" /> Друк етикеток (ZPL)
          </div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div v-if="selectedOrder">
            <div class="text-subtitle2 q-mb-sm">Пацієнт: <strong>{{ selectedOrder.patientName }}</strong> ({{ selectedOrder.patientAge }} р.)</div>
            <div class="text-caption text-grey-7 q-mb-md">Замовлення №: {{ selectedOrder.orderNumber }} | {{ selectedOrder.ehealthReferralCode }}</div>
            
            <div class="text-weight-bold q-mb-xs">Необхідні контейнери (Order of Draw):</div>
            <q-list bordered separator dense>
              <q-item v-for="sample in relatedSamples" :key="sample.barcode">
                <q-item-section avatar style="min-width: 24px;">
                  <span class="text-weight-bold">{{ sample.orderOfDraw }}.</span>
                </q-item-section>
                <q-item-section>
                  <q-item-label>
                    <q-badge :style="{ backgroundColor: sample.capColor }" class="q-mr-xs text-white">
                      {{ sample.capName }}
                    </q-badge>
                    {{ sample.tubeType }}
                  </q-item-label>
                  <q-item-label caption>{{ sample.biomaterial }} ({{ sample.volume }})</q-item-label>
                </q-item-section>
                <q-item-section side>
                  <div class="text-caption text-mono">{{ sample.barcode }}</div>
                </q-item-section>
              </q-item>
            </q-list>
          </div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Закрити" v-close-popup />
          <q-btn color="primary" icon="print" label="Відправити на принтер ZDesigner" @click="printZplLabels" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Collection Confirmation Dialog -->
    <q-dialog v-model="showCollectionDialog">
      <q-card style="min-width: 500px;">
        <q-card-section class="bg-teal text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="fas fa-check-circle" class="q-mr-sm" /> Чек-лист забору біоматеріалу
          </div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="text-body2 q-mb-sm">Перевірте обов'язкові фактори преаналітики:</div>
          <q-checkbox v-model="preAnalyticsCheck.fasting" label="Пацієнт натщесерце (мінімум 8 годин)" />
          <q-checkbox v-model="preAnalyticsCheck.idVerified" label="Ідентифікація пацієнта за паспортом / МІС" />
          <q-checkbox v-model="preAnalyticsCheck.orderOfDraw" label="Дотримано послідовність наповнення пробірок (Order of Draw)" />
          <q-checkbox v-model="preAnalyticsCheck.mixing" label="Плавне перевертання пробірок виконано (5-8 разів)" />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="teal" label="Підтвердити забір" @click="confirmCollection" :disable="!isChecklistComplete" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import { mockOrders, mockSamples } from '../../services/mockData';

export default {
  name: 'PhlebotomyStation',
  data() {
    return {
      searchQuery: '',
      priorityFilter: 'Всі пріоритети',
      statusFilter: 'Всі статуси',
      orders: mockOrders,
      samples: mockSamples,
      selectedOrder: null,
      showBarcodeDialog: false,
      showCollectionDialog: false,
      showNewOrderDialog: false,
      pagination: { rowsPerPage: 10 },
      preAnalyticsCheck: {
        fasting: true,
        idVerified: true,
        orderOfDraw: true,
        mixing: true
      },
      orderColumns: [
        { name: 'orderNumber', label: '№ Замовлення', field: 'orderNumber', align: 'left', sortable: true },
        { name: 'createdAt', label: 'Дата / Час', field: 'createdAt', align: 'left', sortable: true },
        { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left', sortable: true },
        { name: 'department', label: 'Відділення', field: 'department', align: 'left' },
        { name: 'priority', label: 'Пріоритет', field: 'priority', align: 'center' },
        { name: 'ehealthReferralCode', label: 'Код е-Направлення', field: 'ehealthReferralCode', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'center' },
        { name: 'actions', label: 'Дії', align: 'center' }
      ]
    };
  },
  computed: {
    urgentCount() {
      return this.orders.filter(o => o.priority === 'CITO').length;
    },
    filteredOrders() {
      return this.orders.filter(o => {
        const matchesQuery = !this.searchQuery ||
          o.patientName.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
          o.orderNumber.includes(this.searchQuery);
        const matchesPriority = this.priorityFilter === 'Всі пріоритети' ||
          (this.priorityFilter.includes('CITO') && o.priority === 'CITO') ||
          (this.priorityFilter === 'Планові' && o.priority === 'ROUTINE');
        return matchesQuery && matchesPriority;
      });
    },
    relatedSamples() {
      if (!this.selectedOrder) return [];
      return this.samples.filter(s => s.orderId === this.selectedOrder.id);
    },
    isChecklistComplete() {
      return this.preAnalyticsCheck.fasting &&
        this.preAnalyticsCheck.idVerified &&
        this.preAnalyticsCheck.orderOfDraw &&
        this.preAnalyticsCheck.mixing;
    }
  },
  methods: {
    getStatusColor(st) {
      switch (st) {
        case 'IN_PROGRESS': return 'blue-7';
        case 'PANIC_ALERT': return 'negative';
        case 'VERIFIED': return 'positive';
        case 'COLLECTED': return 'teal-7';
        default: return 'grey-6';
      }
    },
    formatStatus(st) {
      switch (st) {
        case 'IN_PROGRESS': return 'В роботі';
        case 'PANIC_ALERT': return 'Паніка!';
        case 'VERIFIED': return 'Завершено';
        case 'COLLECTED': return 'Забір виконано';
        default: return st;
      }
    },
    openBarcodeDialog(order) {
      this.selectedOrder = order;
      this.showBarcodeDialog = true;
    },
    openCollectionDialog(order) {
      this.selectedOrder = order;
      this.showCollectionDialog = true;
    },
    printZplLabels() {
      this.$q.notify({
        type: 'positive',
        message: `Друк ZPL успішно відправлено на термопринтер для ${this.relatedSamples.length} пробірок.`
      });
      this.showBarcodeDialog = false;
    },
    confirmCollection() {
      if (this.selectedOrder) {
        this.selectedOrder.status = 'IN_PROGRESS';
      }
      this.$q.notify({
        type: 'positive',
        message: 'Забір біоматеріалу успішно зареєстровано. Статус оновлено.'
      });
      this.showCollectionDialog = false;
    },
    refreshData() {
      this.$q.notify({ type: 'info', message: 'Дані пунктів забору оновлено' });
    }
  }
};
</script>

<style scoped>
.phlebotomy-page {
  background: #f7f9fa;
  min-height: 100%;
}
</style>
