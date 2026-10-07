<template>
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
