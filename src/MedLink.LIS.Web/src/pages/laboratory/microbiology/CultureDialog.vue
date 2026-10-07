<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 640px; max-width: 96vw" data-testid="cultureDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="fas fa-bacterium" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Посів' : 'Новий посів (бактеріологія)' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
      <q-card-section>
        <div class="row q-col-gutter-sm">
          <div class="col-12" v-if="!form.id"><patient-select v-model="patient" :allow-create="false" /></div>
          <div class="col-6"><q-input v-model="form.barcode" outlined dense label="Штрихкод проби (з замовлення)" data-testid="cultureBarcode" /></div>
          <div class="col-6"><q-select v-model="form.specimenType" outlined dense label="Локус / біоматеріал *" :options="loci" use-input new-value-mode="add-unique" data-testid="cultureSpecimen" /></div>
          <div class="col-6"><q-select v-model="form.status" outlined dense label="Статус" :options="statusOptions" emit-value map-options /></div>
          <div class="col-6"><q-input v-model="form.incubationStartedAt" outlined dense type="datetime-local" stack-label label="Початок інкубації" /></div>
          <div class="col-4"><q-toggle v-model="form.hasGrowth" label="Є ріст" color="deep-orange-6" /></div>
          <div class="col-8"><q-input v-model="form.growthDescription" outlined dense label="Опис росту (КУО/мл, характер колоній)" /></div>
          <div class="col-12"><q-input v-model="form.clinicalNotes" outlined dense autogrow label="Клінічні дані / коментар" /></div>
        </div>
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" :disable="!form.specimenType" data-testid="cultureSave" @click="save" /></q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import PatientSelect from '../../../components/common/PatientSelect.vue';
import { CULTURE_STATUS, toOptions } from '../../../utils/statuses';

export default {
  name: 'CultureDialog',
  components: { PatientSelect },
  props: { value: Boolean, culture: { type: Object, default: null } },
  data () {
    return {
      form: this.empty(), patient: null, saving: false, error: '',
      statusOptions: toOptions(CULTURE_STATUS),
      loci: ['Сеча', 'Кров (гемокультура)', 'Мазок із зіва', 'Мазок із носа', 'Мазок із рани', 'Мокротиння', 'Ліквор', 'Кал', 'Мазок урогенітальний', 'Жовч', 'Виділення з ока', 'Виділення з вуха']
    };
  },
  watch: {
    value (v) {
      if (v) {
        this.error = ''; this.patient = null;
        const c = this.culture;
        this.form = c ? { ...this.empty(), ...c, incubationStartedAt: c.incubationStartedAt ? String(c.incubationStartedAt).slice(0, 16) : '' } : this.empty();
      }
    }
  },
  methods: {
    empty () { return { id: null, barcode: '', specimenType: null, status: 'REGISTERED', incubationStartedAt: '', hasGrowth: false, growthDescription: '', clinicalNotes: '' }; },
    async save () {
      this.saving = true; this.error = '';
      const body = { ...this.form, patientId: this.patient ? this.patient.id : this.form.patientId, incubationStartedAt: this.form.incubationStartedAt ? new Date(this.form.incubationStartedAt).toISOString() : null, barcode: this.form.barcode || null };
      delete body.id; delete body.isolates;
      try {
        const res = this.form.id ? await this.$api.updateCulture(this.form.id, body) : await this.$api.createCulture(body);
        this.$q.notify({ type: 'positive', message: 'Посів збережено' });
        this.$emit('saved', res); this.$emit('input', false);
      } catch (e) { this.error = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    }
  }
};
</script>
