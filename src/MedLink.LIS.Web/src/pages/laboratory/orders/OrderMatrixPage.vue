<template>
  <div class="order-matrix-page" data-testid="orderMatrixPage">
    <page-header title="Матриця призначень" icon="fas fa-th" subtitle="Швидке призначення лікарем / медсестрою: оберіть пацієнта, позначте послуги та показники по підрозділах, створіть замовлення" :breadcrumbs="[{ label: 'Матриця призначень' }]">
      <q-toggle v-model="isUrgentCito" color="deep-orange-5" keep-color label="CITO" />
    </page-header>

    <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
      <div class="col-12 col-md-6"><patient-select v-model="patient" :allow-create="false" /></div>
      <div class="col-6 col-md-3"><q-select v-model="doctorId" dense outlined clearable label="Лікар-замовник" :options="doctorOptions" emit-value map-options /></div>
      <div class="col-6 col-md-3"><q-select v-model="departmentId" dense outlined clearable label="Відділення" :options="departmentOptions" emit-value map-options /></div>
    </div>

    <order-matrix v-model="selection" height="48vh">
      <template v-slot:actions>
        <q-input v-model="clinicalNotes" dense outlined autogrow label="Клінічні дані / коментар" class="q-mt-sm" />
        <q-btn color="primary" icon="save" label="Створити замовлення" class="full-width q-mt-sm" :loading="saving" :disable="!canSubmit" data-testid="matrixSubmit" @click="submit" />
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
    canSubmit () { return !!this.patient && (this.selection.profileIds.length || this.selection.testIds.length) && !this.saving; }
  },
  created () { this.$store.dispatch('dictionaries/loadMany', ['employees', 'departments']); },
  methods: {
    async submit () {
      this.saving = true; this.error = '';
      try {
        const order = await this.$api.createOrder({ patientId: this.patient.id, doctorId: this.doctorId, departmentId: this.departmentId, isUrgentCito: this.isUrgentCito, clinicalNotes: this.clinicalNotes || null, profileIds: this.selection.profileIds, testIds: this.selection.testIds });
        this.$q.notify({ type: 'positive', message: `Замовлення ${order.orderNumber || ''} створено` });
        this.$router.push({ name: 'lab-order-card', params: { id: order.id }, query: { labels: 1 } });
      } catch (e) { this.error = e.userMessage || 'Не вдалося створити замовлення'; } finally { this.saving = false; }
    }
  }
};
</script>
