<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 560px; max-width: 96vw" data-testid="editOrderDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="edit" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Редагування замовлення {{ order.orderNumber }}</div>
        <q-space />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section class="q-gutter-y-sm">
        <q-toggle v-model="form.isUrgentCito" color="deep-orange-5" keep-color label="CITO (терміново)" />
        <q-select v-model="form.doctorId" outlined dense clearable label="Лікар-замовник" :options="doctorOptions" emit-value map-options />
        <q-select v-model="form.departmentId" outlined dense clearable label="Відділення" :options="departmentOptions" emit-value map-options />
        <q-input v-model="form.ehealthReferralId" outlined dense label="ID е-направлення (eHealth)" />
        <q-input v-model="form.clinicalNotes" outlined dense autogrow label="Клінічні дані / коментар" />
        <div class="text-caption text-grey-6">Склад досліджень після створення змінюється через повторне замовлення або reflex-тести (аудит).</div>
        <div v-if="error" class="text-negative">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="editOrderSave" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
export default {
  name: 'EditOrderDialog',
  props: { value: Boolean, order: { type: Object, default: () => ({}) } },
  data () {
    return { form: {}, saving: false, error: '' };
  },
  computed: {
    doctorOptions () { return this.$store.getters['dictionaries/items']('employees').map(e => ({ value: e.id, label: e.fullName || e.name })); },
    departmentOptions () { return this.$store.getters['dictionaries/options']('departments'); }
  },
  watch: {
    value (v) {
      if (v) {
        this.error = '';
        this.form = {
          isUrgentCito: !!this.order.isUrgentCito,
          doctorId: this.order.doctorId || null,
          departmentId: this.order.departmentId || null,
          ehealthReferralId: this.order.ehealthReferralId || '',
          clinicalNotes: this.order.clinicalNotes || ''
        };
        this.$store.dispatch('dictionaries/loadMany', ['employees', 'departments']);
      }
    }
  },
  methods: {
    async save () {
      this.saving = true;
      this.error = '';
      try {
        const res = await this.$api.updateOrder(this.order.id, { ...this.form, ehealthReferralId: this.form.ehealthReferralId || null, clinicalNotes: this.form.clinicalNotes || null });
        this.$q.notify({ type: 'positive', message: 'Замовлення оновлено' });
        this.$emit('saved', res);
        this.$emit('input', false);
      } catch (e) {
        this.error = e.userMessage || 'Не вдалося зберегти';
      } finally {
        this.saving = false;
      }
    }
  }
};
</script>
