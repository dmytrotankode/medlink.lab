<template>
  <div class="order-matrix-page" data-testid="orderMatrixPage">
    <page-header title="Матриця призначень" icon="fas fa-th" :breadcrumbs="[{ label: 'Матриця призначень' }]">
      <q-toggle v-model="isUrgentCito" color="deep-orange-5" keep-color label="Термінове (CITO)" />
    </page-header>

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-6"><patient-select v-model="patient" :allow-create="false" /></div>
      <div class="col-6 col-md-3"><q-select v-model="doctorId" dense outlined clearable label="Лікар" :options="doctorOptions" emit-value map-options /></div>
      <div class="col-6 col-md-3"><q-select v-model="departmentId" dense outlined clearable label="Відділення" :options="departmentOptions" emit-value map-options /></div>
    </div>

    <order-matrix v-model="selection" height="48vh">
      <template v-slot:actions>
        <q-input v-model="clinicalNotes" dense outlined autogrow label="Клінічні дані / коментар" class="q-mt-sm" />
        <q-btn color="primary" icon="save" label="Створити направлення" class="full-width q-mt-sm" :loading="saving" :disable="!canSubmit" data-testid="matrixSubmit" @click="submit"><q-tooltip v-if="!canSubmit && !saving" anchor="top middle" self="bottom middle">{{ submitHint }}</q-tooltip></q-btn>
        <div v-if="!canSubmit && !saving" class="text-caption text-grey-7 q-mt-xs">{{ submitHint }}</div>
        <div v-if="error" class="text-negative text-caption q-mt-xs">{{ error }}</div>
      </template>
    </order-matrix>
  </div>
</template>

<script>
import OrderMatrix from '../../../components/orders/OrderMatrix.vue';
import PatientSelect from '../../../components/common/PatientSelect.vue';

export default {
  name: 'OrderMatrixPage',
  components: { OrderMatrix, PatientSelect },
  data () { return { patient: null, doctorId: null, departmentId: null, isUrgentCito: false, clinicalNotes: '', selection: { profileIds: [], testIds: [] }, saving: false, error: '' }; },
  computed: {
    doctorOptions () { return this.$store.getters['dictionaries/items']('employees').map(e => ({ value: e.id, label: e.fullName || e.name })); },
    departmentOptions () { return this.$store.getters['dictionaries/options']('departments'); },
    submitHint () {
      const missing = [];
      if (!this.patient) missing.push('пацієнта');
      if (!(this.selection.profileIds.length || this.selection.testIds.length)) missing.push('послуги');
      return missing.length ? `Оберіть ${missing.join(' і ')}` : '';
    },
    canSubmit () { return !!this.patient && (this.selection.profileIds.length || this.selection.testIds.length) && !this.saving; }
  },
  created () { this.$store.dispatch('dictionaries/loadMany', ['employees', 'departments']); },
  methods: {
    async submit () {
      this.saving = true; this.error = '';
      try {
        const order = await this.$api.createOrder({ patientId: this.patient.id, doctorId: this.doctorId, departmentId: this.departmentId, isUrgentCito: this.isUrgentCito, clinicalNotes: this.clinicalNotes || null, profileIds: this.selection.profileIds, testIds: this.selection.testIds });
        this.$q.notify({ type: 'positive', message: `Направлення ${order.orderNumber || ''} створено` });
        this.$router.push({ name: 'lab-order-card', params: { id: order.id }, query: { labels: 1 } });
      } catch (e) { this.error = e.userMessage || 'Не вдалося створити направлення'; } finally { this.saving = false; }
    }
  }
};
</script>
