<template>
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
