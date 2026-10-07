# -*- coding: utf-8 -*-
"""
Generator for MedLink LIS Vue page components in src/pages/laboratory and src/pages/dictionaries.
Matches evomis App.View Quasar v1 (1.15.3) / Vue 2 structure.
Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
"""

import os

LAB_PAGES_DIR = r"C:\__MEDLINK___\LABA\medlink_lab_frontend\src\pages\laboratory"
DICT_PAGES_DIR = r"C:\__MEDLINK___\LABA\medlink_lab_frontend\src\pages\dictionaries"

os.makedirs(LAB_PAGES_DIR, exist_ok=True)
os.makedirs(DICT_PAGES_DIR, exist_ok=True)

# 1. PhlebotomyStation.vue
phlebotomy_vue = """<template>
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
"""

with open(os.path.join(LAB_PAGES_DIR, "PhlebotomyStation.vue"), "w", encoding="utf-8") as f:
    f.write(phlebotomy_vue)
print("Created PhlebotomyStation.vue")

# 2. SpecimenLogistics.vue
logistics_vue = """<template>
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
"""

with open(os.path.join(LAB_PAGES_DIR, "SpecimenLogistics.vue"), "w", encoding="utf-8") as f:
    f.write(logistics_vue)
print("Created SpecimenLogistics.vue")

# 3. LabWorkstation.vue
workstation_vue = """<template>
  <div class="q-pa-md workstation-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-microscope" class="q-mr-sm" />
          Робочий стіл лаборанта (Журнал досліджень)
        </h5>
        <div class="text-caption text-grey-7">
          Аналітичний етап. Прийом результатів з аналізаторів, ручне введення мікроскопії, дельта-чек та автоматична валідація.
        </div>
      </div>
      <div class="row q-gutter-sm">
        <q-btn color="secondary" icon="refresh" label="Оновити" dense flat @click="refreshData" />
        <q-btn color="teal" icon="check_circle" label="Автовалідація норм" @click="runAutoValidation" />
      </div>
    </div>

    <!-- Filters Bar -->
    <q-card flat bordered class="q-mb-md bg-grey-1">
      <q-card-section class="q-pa-sm row q-col-gutter-sm items-center">
        <div class="col-12 col-md-3">
          <q-input v-model="searchQuery" outlined dense placeholder="Штрихкод або показник..." bg-color="white">
            <template v-slot:append><q-icon name="search" /></template>
          </q-input>
        </div>
        <div class="col-6 col-md-3">
          <q-select v-model="analyzerFilter" :options="['Всі прилади', 'Sysmex XN-1000', 'Roche Cobas e411', 'Mindray BS-240']" outlined dense bg-color="white" />
        </div>
        <div class="col-6 col-md-3">
          <q-select v-model="flagFilter" :options="['Всі результати', 'Тільки паніка та відхилення', 'Нормальні']" outlined dense bg-color="white" />
        </div>
        <div class="col-12 col-md-3 text-right">
          <q-chip outline color="negative" icon="warning">Паніка: {{ panicCount }}</q-chip>
          <q-chip outline color="primary" icon="assignment">Очікує: {{ pendingCount }}</q-chip>
        </div>
      </q-card-section>
    </q-card>

    <!-- Worklist Results Table -->
    <q-card flat bordered>
      <q-table
        :data="filteredResults"
        :columns="worklistColumns"
        row-key="id"
        dense
        flat
        :pagination.sync="pagination"
      >
        <template v-slot:body-cell-value="props">
          <q-td :props="props" :class="getValueClass(props.row.flag)">
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
            <q-badge :color="getFlagColor(props.row.flag)">
              {{ formatFlag(props.row.flag) }}
            </q-badge>
          </q-td>
        </template>

        <template v-slot:body-cell-deltaPercent="props">
          <q-td :props="props" :class="props.row.deltaPercent.includes('+185') ? 'text-negative text-weight-bold' : ''">
            {{ props.row.deltaPercent }}
          </q-td>
        </template>

        <template v-slot:body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="props.row.status === 'AUTO_VERIFIED' ? 'positive' : 'warning'" :text-color="props.row.status === 'AUTO_VERIFIED' ? 'white' : 'dark'">
              {{ props.row.status === 'AUTO_VERIFIED' ? 'Авто-валідовано' : 'Очікує лікаря' }}
            </q-badge>
          </q-td>
        </template>

        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="q-gutter-xs">
            <q-btn size="sm" color="primary" dense icon="edit" @click="editResult(props.row)">
              <q-tooltip>Коригувати / Коментар</q-tooltip>
            </q-btn>
            <q-btn size="sm" color="secondary" dense icon="replay" @click="requestRerun(props.row)">
              <q-tooltip>Замовити перезапуск (Rerun)</q-tooltip>
            </q-btn>
          </q-td>
        </template>
      </q-table>
    </q-card>

    <!-- Edit Result Dialog -->
    <q-dialog v-model="showEditDialog">
      <q-card style="min-width: 450px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="edit" class="q-mr-sm" /> Редагування результату
          </div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md" v-if="editingResult">
          <div class="text-subtitle2 q-mb-xs">{{ editingResult.testName }} ({{ editingResult.testCode }})</div>
          <div class="text-caption text-grey-7 q-mb-md">Пацієнт: {{ editingResult.patientName }} | Штрихкод: {{ editingResult.barcode }}</div>
          
          <q-input v-model.number="editingResult.value" type="number" step="0.01" label="Значення показника" outlined dense class="q-mb-md" />
          <q-input v-model="editingResult.comment" label="Коментар лаборанта" outlined dense class="q-mb-md" />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" label="Зберегти" @click="saveEditedResult" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import { mockWorklistResults } from '../../services/mockData';

export default {
  name: 'LabWorkstation',
  data() {
    return {
      searchQuery: '',
      analyzerFilter: 'Всі прилади',
      flagFilter: 'Всі результати',
      results: mockWorklistResults,
      editingResult: null,
      showEditDialog: false,
      pagination: { rowsPerPage: 10 },
      worklistColumns: [
        { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left', sortable: true },
        { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left', sortable: true },
        { name: 'analyzer', label: 'Аналізатор', field: 'analyzer', align: 'left' },
        { name: 'testName', label: 'Тест', field: 'testName', align: 'left' },
        { name: 'value', label: 'Результат', field: 'value', align: 'right', sortable: true },
        { name: 'norm', label: 'Референсна норма', align: 'center' },
        { name: 'flag', label: 'Флаг', field: 'flag', align: 'center' },
        { name: 'deltaPercent', label: 'Delta-Check', field: 'deltaPercent', align: 'right' },
        { name: 'status', label: 'Статус', field: 'status', align: 'center' },
        { name: 'actions', label: 'Дії', align: 'center' }
      ]
    };
  },
  computed: {
    panicCount() {
      return this.results.filter(r => r.flag === 'PANIC_HIGH' || r.flag === 'PANIC_LOW').length;
    },
    pendingCount() {
      return this.results.filter(r => r.status === 'PENDING_VERIFY').length;
    },
    filteredResults() {
      return this.results.filter(r => {
        const matchesQuery = !this.searchQuery ||
          r.testName.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
          r.barcode.includes(this.searchQuery) ||
          r.patientName.toLowerCase().includes(this.searchQuery.toLowerCase());
        const matchesAnalyzer = this.analyzerFilter === 'Всі прилади' || r.analyzer.includes(this.analyzerFilter);
        const matchesFlag = this.flagFilter === 'Всі результати' ||
          (this.flagFilter.includes('паніка') && r.flag !== 'NORMAL') ||
          (this.flagFilter === 'Нормальні' && r.flag === 'NORMAL');
        return matchesQuery && matchesAnalyzer && matchesFlag;
      });
    }
  },
  methods: {
    getValueClass(flag) {
      if (flag === 'PANIC_HIGH' || flag === 'PANIC_LOW') return 'text-negative text-weight-bolder bg-red-1';
      if (flag === 'DELTA_ALERT') return 'text-deep-orange text-weight-bold';
      return 'text-dark';
    },
    getFlagColor(flag) {
      switch (flag) {
        case 'PANIC_HIGH':
        case 'PANIC_LOW': return 'negative';
        case 'DELTA_ALERT': return 'deep-orange';
        case 'NORMAL': return 'positive';
        default: return 'grey';
      }
    },
    formatFlag(flag) {
      switch (flag) {
        case 'PANIC_HIGH': return 'КРИТИЧНО ВИСОКИЙ';
        case 'PANIC_LOW': return 'КРИТИЧНО НИЗЬКИЙ';
        case 'DELTA_ALERT': return 'DELTA УВАГА';
        case 'NORMAL': return 'НОРМА';
        default: return flag;
      }
    },
    editResult(r) {
      this.editingResult = { ...r };
      this.showEditDialog = true;
    },
    saveEditedResult() {
      const idx = this.results.findIndex(item => item.id === this.editingResult.id);
      if (idx !== -1) {
        this.results.splice(idx, 1, this.editingResult);
      }
      this.showEditDialog = false;
      this.$q.notify({ type: 'positive', message: 'Результат збережено' });
    },
    requestRerun(r) {
      this.$q.notify({ type: 'info', message: `Запит на перезапуск для ${r.testCode} відправлено в чергу аналізатора.` });
    },
    runAutoValidation() {
      let count = 0;
      this.results.forEach(r => {
        if (r.flag === 'NORMAL' && r.status === 'PENDING_VERIFY') {
          r.status = 'AUTO_VERIFIED';
          count++;
        }
      });
      this.$q.notify({ type: 'positive', message: `Автоматично валідовано ${count} нормальних результатів за критеріями CLSI.` });
    },
    refreshData() {
      this.$q.notify({ type: 'info', message: 'Журнал досліджень оновлено.' });
    }
  }
};
</script>
<style scoped>
.workstation-page { background: #f7f9fa; min-height: 100%; }
</style>
"""

with open(os.path.join(LAB_PAGES_DIR, "LabWorkstation.vue"), "w", encoding="utf-8") as f:
    f.write(workstation_vue)
print("Created LabWorkstation.vue")

# 4. ValidationPanic.vue
validation_vue = """<template>
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
"""

with open(os.path.join(LAB_PAGES_DIR, "ValidationPanic.vue"), "w", encoding="utf-8") as f:
    f.write(validation_vue)
print("Created ValidationPanic.vue")

# 5. QualityControl.vue
qc_vue = """<template>
  <div class="q-pa-md qc-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-chart-line" class="q-mr-sm" />
          Внутрішній контроль якості (ВКЯ) - Леві-Дженнінгс & Вестгард
        </h5>
        <div class="text-caption text-grey-7">
          Моніторинг стабільності аналітичних систем. Розрахунок Mean, SD, CV%, детекція порушень правил Вестгарда та блокування випуску результатів.
        </div>
      </div>
      <div class="row q-gutter-sm">
        <q-btn color="negative" icon="lock" label="Скинути блокування аналізатора" dense flat @click="showUnlockDialog = true" />
      </div>
    </div>

    <!-- Lockout Alert -->
    <q-banner dense rounded class="bg-red-1 text-negative q-mb-md" inline-actions>
      <template v-slot:avatar>
        <q-icon name="lock" color="negative" size="30px" />
      </template>
      <div class="text-subtitle2 text-weight-bold">
        УВАГА: Аналізатор {{ qc.analyzer }} заблоковано через порушення правила Вестгарда 1-3s!
      </div>
      <div class="text-caption">
        Контрольний матеріал: <strong>{{ qc.controlMaterial }}</strong> (Лот: {{ qc.lotNumber }}). Виміряне значення: <strong>8.28 10*9/л</strong> (Ціль: {{ qc.targetMean }} ± 3SD = {{ (qc.targetMean + 3*qc.targetSd).toFixed(2) }}).
      </div>
    </q-banner>

    <!-- Parameter Info Card -->
    <div class="row q-col-gutter-md q-mb-md">
      <div class="col-12 col-md-3">
        <q-card flat bordered class="q-pa-sm text-center">
          <div class="text-caption text-grey-7">Параметр</div>
          <div class="text-subtitle1 text-weight-bold text-primary">{{ qc.parameter }}</div>
        </q-card>
      </div>
      <div class="col-12 col-md-3">
        <q-card flat bordered class="q-pa-sm text-center">
          <div class="text-caption text-grey-7">Цільове середнє (Mean)</div>
          <div class="text-subtitle1 text-weight-bold">{{ qc.targetMean }}</div>
        </q-card>
      </div>
      <div class="col-12 col-md-3">
        <q-card flat bordered class="q-pa-sm text-center">
          <div class="text-caption text-grey-7">Стандартне відхилення (SD)</div>
          <div class="text-subtitle1 text-weight-bold">±{{ qc.targetSd }}</div>
        </q-card>
      </div>
      <div class="col-12 col-md-3">
        <q-card flat bordered class="q-pa-sm text-center">
          <div class="text-caption text-grey-7">Коефіцієнт варіації (CV%)</div>
          <div class="text-subtitle1 text-weight-bold text-teal">{{ qc.cvPercent }}%</div>
        </q-card>
      </div>
    </div>

    <!-- QC Points Table -->
    <q-card flat bordered class="q-mb-md">
      <q-card-section class="bg-grey-2 q-py-sm row items-center justify-between">
        <div class="text-subtitle2 text-weight-bold">Серія вимірювань за поточний місяць</div>
        <q-btn size="sm" color="primary" icon="add" label="Внести точку контролю" dense @click="showAddPointDialog = true" />
      </q-card-section>
      <q-table
        :data="qc.dataPoints"
        :columns="columns"
        row-key="day"
        dense
        flat
      >
        <template v-slot:body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="props.row.status === 'OK' ? 'positive' : (props.row.status.includes('WARN') ? 'warning' : 'negative')">
              {{ props.row.status }}
            </q-badge>
          </q-td>
        </template>
      </q-table>
    </q-card>

    <!-- Unlock Dialog -->
    <q-dialog v-model="showUnlockDialog">
      <q-card style="min-width: 450px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">Зняття аналітичного блокування</div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="text-body2 q-mb-sm">Опишіть проведені коригувальні дії:</div>
          <q-input v-model="correctiveAction" type="textarea" outlined dense rows="3" placeholder="Наприклад: Промито оптичну кювету концентрованим розчином Cellclean, перекалібровано дозатор." />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" label="Підтвердити та розблокувати" @click="confirmUnlock" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import { mockQcData } from '../../services/mockData';

export default {
  name: 'QualityControl',
  data() {
    return {
      qc: mockQcData,
      showUnlockDialog: false,
      showAddPointDialog: false,
      correctiveAction: 'Промито вимірювальну кювету, замінено ділюент, повторний замір контролю L2 дав значення 7.21 (в нормі).',
      columns: [
        { name: 'day', label: 'День місяця', field: 'day', align: 'center', sortable: true },
        { name: 'val', label: 'Виміряне значення', field: 'val', align: 'right', sortable: true },
        { name: 'status', label: 'Оцінка Вестгарда', field: 'status', align: 'center' }
      ]
    };
  },
  methods: {
    confirmUnlock() {
      this.qc.currentStatus = 'NORMAL';
      this.$q.notify({ type: 'positive', message: 'Аналізатор успішно розблоковано. Коригувальну дію внесено в журнал аудиту.' });
      this.showUnlockDialog = false;
    }
  }
};
</script>
<style scoped>
.qc-page { background: #f7f9fa; min-height: 100%; }
</style>
"""

with open(os.path.join(LAB_PAGES_DIR, "QualityControl.vue"), "w", encoding="utf-8") as f:
    f.write(qc_vue)
print("Created QualityControl.vue")

# 6. PatientPortal.vue
patient_vue = """<template>
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
"""

with open(os.path.join(LAB_PAGES_DIR, "PatientPortal.vue"), "w", encoding="utf-8") as f:
    f.write(patient_vue)
print("Created PatientPortal.vue")

# 7. BiobankArchive.vue
biobank_vue = """<template>
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
"""

with open(os.path.join(LAB_PAGES_DIR, "BiobankArchive.vue"), "w", encoding="utf-8") as f:
    f.write(biobank_vue)
print("Created BiobankArchive.vue")

# 8. ReagentInventory.vue
reagents_vue = """<template>
  <div class="q-pa-md reagents-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-boxes" class="q-mr-sm" />
          Склад реактивів, калібраторів та витратних матеріалів
        </h5>
        <div class="text-caption text-grey-7">
          Облік залишків тестів на борту аналізаторів, терміни придатності відкритих флаконів (On-board stability) та списання за фактом аналізів.
        </div>
      </div>
      <div class="row q-gutter-sm">
        <q-btn color="primary" icon="qr_code_scanner" label="Сканувати штрихкод касети" @click="showScanDialog = true" />
      </div>
    </div>

    <!-- Reagents Table -->
    <q-card flat bordered>
      <q-table
        :data="reagents"
        :columns="columns"
        row-key="id"
        dense
        flat
      >
        <template v-slot:body-cell-testsRemaining="props">
          <q-td :props="props">
            <q-linear-progress
              :value="props.row.testsRemaining / props.row.testsTotal"
              :color="props.row.testsRemaining < 50 ? 'negative' : 'teal'"
              class="q-mb-xs"
              style="height: 8px; border-radius: 4px;"
            />
            <div class="text-caption text-weight-bold">
              {{ props.row.testsRemaining }} / {{ props.row.testsTotal }} тестів
            </div>
          </q-td>
        </template>

        <template v-slot:body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="getStatusColor(props.row.status)">
              {{ formatStatus(props.row.status) }}
            </q-badge>
          </q-td>
        </template>
      </q-table>
    </q-card>
  </div>
</template>

<script>
import { mockReagents } from '../../services/mockData';

export default {
  name: 'ReagentInventory',
  data() {
    return {
      showScanDialog: false,
      reagents: mockReagents,
      columns: [
        { name: 'analyzer', label: 'Аналізатор', field: 'analyzer', align: 'left', sortable: true },
        { name: 'name', label: 'Назва реактиву / Касети', field: 'name', align: 'left', sortable: true },
        { name: 'lotNumber', label: 'Номер лоту', field: 'lotNumber', align: 'left' },
        { name: 'testsRemaining', label: 'Залишок тестів', field: 'testsRemaining', align: 'center' },
        { name: 'openedAt', label: 'Дата відкриття', field: 'openedAt', align: 'center' },
        { name: 'expiresAt', label: 'Придатний до', field: 'expiresAt', align: 'center' },
        { name: 'status', label: 'Статус', field: 'status', align: 'center' }
      ]
    };
  },
  methods: {
    getStatusColor(st) {
      switch (st) {
        case 'ACTIVE': return 'positive';
        case 'LOW_STOCK': return 'warning';
        case 'EXPIRED': return 'negative';
        default: return 'grey';
      }
    },
    formatStatus(st) {
      switch (st) {
        case 'ACTIVE': return 'Активний';
        case 'LOW_STOCK': return 'Закінчується';
        case 'EXPIRED': return 'Протерміновано!';
        default: return st;
      }
    }
  }
};
</script>
<style scoped>
.reagents-page { background: #f7f9fa; min-height: 100%; }
</style>
"""

with open(os.path.join(LAB_PAGES_DIR, "ReagentInventory.vue"), "w", encoding="utf-8") as f:
    f.write(reagents_vue)
print("Created ReagentInventory.vue")

# 9. MicrobiologyCulture.vue
microbiology_vue = """<template>
  <div class="q-pa-md microbiology-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-bacterium" class="q-mr-sm" />
          Бактеріологія та антибіотикограма (EUCAST)
        </h5>
        <div class="text-caption text-grey-7">
          Культуральне дослідження, морфологія колоній, виділений збудник та визначення чутливості за міжнародним протоколом EUCAST.
        </div>
      </div>
      <div>
        <q-btn color="primary" icon="print" label="Друк бланка антибіотикограми" dense flat />
      </div>
    </div>

    <!-- Culture Info Card -->
    <q-card flat bordered class="q-mb-md bg-white">
      <q-card-section class="row items-center justify-between bg-grey-2 q-py-sm">
        <div class="text-subtitle2 text-weight-bold">
          Зразок: №{{ mb.sampleNumber }} | Пацієнт: {{ mb.patientName }} | Матеріал: {{ mb.material }}
        </div>
        <q-badge color="teal">Збудник ідентифіковано</q-badge>
      </q-card-section>
      <q-card-section class="row q-col-gutter-md">
        <div class="col-12 col-md-3">
          <div class="text-caption text-grey-7">Виділена мікрофлора:</div>
          <div class="text-subtitle1 text-weight-bold text-negative">{{ mb.pathogen }}</div>
        </div>
        <div class="col-12 col-md-3">
          <div class="text-caption text-grey-7">Титр колоній:</div>
          <div class="text-subtitle1 text-weight-bold">{{ mb.colonyCount }}</div>
        </div>
        <div class="col-12 col-md-3">
          <div class="text-caption text-grey-7">Морфологія:</div>
          <div class="text-subtitle1">{{ mb.gramType }}</div>
        </div>
        <div class="col-12 col-md-3">
          <div class="text-caption text-grey-7">Фенотип резистентності:</div>
          <div class="text-subtitle1 text-teal">{{ mb.phenotype }}</div>
        </div>
      </q-card-section>
    </q-card>

    <!-- Antibiotic Susceptibility Table -->
    <q-card flat bordered class="bg-white">
      <q-card-section class="bg-grey-2 q-py-sm">
        <div class="text-subtitle2 text-weight-bold text-dark">Чутливість до антибактеріальних препаратів (EUCAST 2026)</div>
      </q-card-section>
      <q-table
        :data="mb.antibiotics"
        :columns="columns"
        row-key="name"
        dense
        flat
      >
        <template v-slot:body-cell-eucast="props">
          <q-td :props="props">
            <q-badge :color="props.row.eucast === 'S' ? 'positive' : (props.row.eucast === 'I' ? 'warning' : 'negative')">
              {{ props.row.eucast }} ({{ formatEucast(props.row.eucast) }})
            </q-badge>
          </q-td>
        </template>
      </q-table>
    </q-card>
  </div>
</template>

<script>
import { mockMicrobiologyData } from '../../services/mockData';

export default {
  name: 'MicrobiologyCulture',
  data() {
    return {
      mb: mockMicrobiologyData,
      columns: [
        { name: 'name', label: 'Антибіотик', field: 'name', align: 'left' },
        { name: 'mic', label: 'МІК (мкг/мл)', field: 'mic', align: 'center' },
        { name: 'zone', label: 'Діаметр зони затримки (мм)', field: 'zone', align: 'center' },
        { name: 'eucast', label: 'Категорія EUCAST', field: 'eucast', align: 'center' },
        { name: 'interpretation', label: 'Клінічна інтерпретація', field: 'interpretation', align: 'left' }
      ]
    };
  },
  methods: {
    formatEucast(cat) {
      if (cat === 'S') return 'Чутливий';
      if (cat === 'I') return 'Помірно-чутливий';
      return 'Резистентний';
    }
  }
};
</script>
<style scoped>
.microbiology-page { background: #f7f9fa; min-height: 100%; }
</style>
"""

with open(os.path.join(LAB_PAGES_DIR, "MicrobiologyCulture.vue"), "w", encoding="utf-8") as f:
    f.write(microbiology_vue)
print("Created MicrobiologyCulture.vue")

# 10. LabAnalyticsTat.vue
analytics_vue = """<template>
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
"""

with open(os.path.join(LAB_PAGES_DIR, "LabAnalyticsTat.vue"), "w", encoding="utf-8") as f:
    f.write(analytics_vue)
print("Created LabAnalyticsTat.vue")

# 11. AnalyzerMonitor.vue
analyzer_vue = """<template>
  <div class="q-pa-md analyzer-monitor-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-network-wired" class="q-mr-sm" />
          Монітор підключення аналізаторів (Шлюз MedLink.LabConnector)
        </h5>
        <div class="text-caption text-grey-7">
          Статус драйверів ASTM E1381/E1394, HL7 v2.x MLLP, COM/RS-232 та мережевих TCP/IP з'єднань з приладами.
        </div>
      </div>
      <div class="row q-gutter-sm">
        <q-btn color="secondary" icon="refresh" label="Оновити статуси" dense flat />
      </div>
    </div>

    <!-- Analyzers Table -->
    <q-card flat bordered class="q-mb-md">
      <q-table
        :data="analyzers"
        :columns="columns"
        row-key="id"
        dense
        flat
      >
        <template v-slot:body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="props.row.status === 'ONLINE' ? 'positive' : 'warning'">
              {{ props.row.status }}
            </q-badge>
          </q-td>
        </template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="q-gutter-xs">
            <q-btn size="sm" color="primary" dense icon="terminal" label="Лог пакетів" @click="openLogs(props.row)" />
            <q-btn size="sm" color="teal" dense icon="send" label="Тест зв'язку" @click="testPing(props.row)" />
          </q-td>
        </template>
      </q-table>
    </q-card>

    <!-- Raw Packet Logs Modal -->
    <q-dialog v-model="showLogDialog">
      <q-card style="min-width: 650px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="terminal" class="q-mr-sm" /> ASTM / HL7 Raw Packet Monitor
          </div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md bg-dark text-cyan-2" style="font-family: monospace; font-size: 12px; max-height: 350px; overflow-y: auto;">
          [10:14:02.102] TCP CONNECT from 192.168.1.101:5100 ESTABLISHED<br>
          [10:14:02.105] RECV &lt;ENQ&gt; (0x05)<br>
          [10:14:02.106] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.110] RECV &lt;STX&gt;1H|\\^&|||Sysmex^XN-1000^00-14||||||||E1394-97&lt;CR&gt;&lt;ETX&gt;4A&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.111] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.115] RECV &lt;STX&gt;2P|1||108291||Коваленко^Олександр^Сергійович||19850412|M&lt;CR&gt;&lt;ETX&gt;7B&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.116] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.122] RECV &lt;STX&gt;3O|1|1026004820||^^^WBC\\^^^HGB|R|20261006084700||||||||||||||||||F&lt;CR&gt;&lt;ETX&gt;9D&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.123] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.130] RECV &lt;STX&gt;4R|1|^^^WBC|7.45|10*9/L|4.0-9.0|N||F||||20261006095011&lt;CR&gt;&lt;ETX&gt;21&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.131] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.140] RECV &lt;EOT&gt; (0x04) - Transaction completed successfully.<br>
          [10:14:02.145] MedLink Backend: Processed sample 1026004820, WBC saved.
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Закрити" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import { mockAnalyzers } from '../../services/mockData';

export default {
  name: 'AnalyzerMonitor',
  data() {
    return {
      showLogDialog: false,
      analyzers: mockAnalyzers,
      columns: [
        { name: 'name', label: 'Модель аналізатора', field: 'name', align: 'left', sortable: true },
        { name: 'type', label: 'Тип дослідження', field: 'type', align: 'left' },
        { name: 'protocol', label: 'Протокол зв\'язку', field: 'protocol', align: 'center' },
        { name: 'connection', label: 'Мережевий порт / COM', field: 'connection', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'center' },
        { name: 'actions', label: 'Дії', align: 'center' }
      ]
    };
  },
  methods: {
    openLogs() {
      this.showLogDialog = true;
    },
    testPing(a) {
      this.$q.notify({ type: 'positive', message: `Зв'язок з ${a.name} перевірено: OK (Ping 4ms, ACK отримано).` });
    }
  }
};
</script>
<style scoped>
.analyzer-monitor-page { background: #f7f9fa; min-height: 100%; }
</style>
"""

with open(os.path.join(LAB_PAGES_DIR, "AnalyzerMonitor.vue"), "w", encoding="utf-8") as f:
    f.write(analyzer_vue)
print("Created AnalyzerMonitor.vue")

# 12. NormsMethodologies.vue
norms_vue = """<template>
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
"""

with open(os.path.join(LAB_PAGES_DIR, "NormsMethodologies.vue"), "w", encoding="utf-8") as f:
    f.write(norms_vue)
print("Created NormsMethodologies.vue")

# Dictionaries
# 1. BiomaterialsDict.vue
bio_dict_vue = """<template>
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
"""

with open(os.path.join(DICT_PAGES_DIR, "BiomaterialsDict.vue"), "w", encoding="utf-8") as f:
    f.write(bio_dict_vue)
print("Created BiomaterialsDict.vue")

# 2. TubeTypesDict.vue
tubes_dict_vue = """<template>
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
        { name: 'volume', label: 'Об\'єм', field: 'volume', align: 'center' },
        { name: 'orderOfDraw', label: 'Order of Draw', field: 'orderOfDraw', align: 'center' },
        { name: 'inversions', label: 'Кількість перевертань', field: 'inversions', align: 'left' }
      ]
    };
  }
};
</script>
<style scoped> .dict-page { background: #f7f9fa; min-height: 100%; } </style>
"""

with open(os.path.join(DICT_PAGES_DIR, "TubeTypesDict.vue"), "w", encoding="utf-8") as f:
    f.write(tubes_dict_vue)
print("Created TubeTypesDict.vue")

# 3. AnalyzerTypesDict.vue
analyzers_dict_vue = """<template>
  <div class="q-pa-md dict-page">
    <div class="row items-center justify-between q-mb-md">
      <h5 class="q-my-none text-weight-bold text-primary">
        <q-icon name="fas fa-server" class="q-mr-sm" /> Довідник моделей аналізаторів
      </h5>
      <q-btn color="primary" icon="add" label="Додати модель" dense />
    </div>
    <q-card flat bordered>
      <q-table :data="items" :columns="columns" row-key="code" dense flat />
    </q-card>
  </div>
</template>

<script>
export default {
  name: 'AnalyzerTypesDict',
  data() {
    return {
      items: [
        { code: 'SYSMEX-XN1000', vendor: 'Sysmex Corporation (Japan)', model: 'XN-1000', discipline: 'Гематологія 5-diff', protocol: 'ASTM E1381/E1394', interfaceType: 'TCP/IP Client/Server' },
        { code: 'ROCHE-COBAS-E411', vendor: 'Roche Diagnostics (Switzerland)', model: 'Cobas e411', discipline: 'Імунохімія (ECLIA)', protocol: 'ASTM E1394', interfaceType: 'RS-232 / TCP' },
        { code: 'MINDRAY-BS240', vendor: 'Mindray Medical (China)', model: 'BS-240', discipline: 'Клінічна біохімія', protocol: 'HL7 v2.3.1 MLLP', interfaceType: 'TCP/IP MLLP' },
        { code: 'SYSMEX-CA660', vendor: 'Sysmex Corporation (Japan)', model: 'CA-660', discipline: 'Коагулометрія', protocol: 'ASTM E1381', interfaceType: 'RS-232 Serial' }
      ],
      columns: [
        { name: 'code', label: 'Код моделі', field: 'code', align: 'left' },
        { name: 'vendor', label: 'Виробник', field: 'vendor', align: 'left' },
        { name: 'model', label: 'Модель', field: 'model', align: 'left' },
        { name: 'discipline', label: 'Дисципліна', field: 'discipline', align: 'left' },
        { name: 'protocol', label: 'Протокол', field: 'protocol', align: 'center' },
        { name: 'interfaceType', label: 'Тип інтерфейсу', field: 'interfaceType', align: 'left' }
      ]
    };
  }
};
</script>
<style scoped> .dict-page { background: #f7f9fa; min-height: 100%; } </style>
"""

with open(os.path.join(DICT_PAGES_DIR, "AnalyzerTypesDict.vue"), "w", encoding="utf-8") as f:
    f.write(analyzers_dict_vue)
print("Created AnalyzerTypesDict.vue")

# 4. LabParametersDict.vue
params_dict_vue = """<template>
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
"""

with open(os.path.join(DICT_PAGES_DIR, "LabParametersDict.vue"), "w", encoding="utf-8") as f:
    f.write(params_dict_vue)
print("Created LabParametersDict.vue")

print("All 16 Vue page and dictionary components created successfully.")
