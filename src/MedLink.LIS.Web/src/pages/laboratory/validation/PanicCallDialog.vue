<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 560px; max-width: 96vw" data-testid="panicCallDialog">
      <q-card-section class="row items-center bg-negative text-white q-py-sm">
        <q-icon name="call" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Редагування запису дзвінка' : 'Реєстрація CITO-дзвінка про критичне значення' }}</div>
        <q-space /><q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section class="q-gutter-y-sm">
        <q-banner v-if="result" dense rounded class="bg-red-1 text-grey-9">
          <div class="row items-center justify-between">
            <div>
              <div class="text-weight-bold">{{ result.patientName }}</div>
              <div class="text-caption">{{ result.testName || result.testCode }} · замовлення {{ result.orderNumber }} · {{ result.barcode }}</div>
            </div>
            <div class="text-h5 val-critical">{{ result.value | num }} <span class="text-caption">{{ result.unit }}</span></div>
          </div>
        </q-banner>
        <div class="row q-col-gutter-sm">
          <div class="col-7"><q-input v-model="form.doctorName" outlined dense label="Лікар, якому повідомлено (ПІБ) *" data-testid="panicDoctor" /></div>
          <div class="col-5"><q-input v-model="form.phone" outlined dense label="Телефон *" data-testid="panicPhone" /></div>
          <div class="col-12"><q-select v-model="form.department" outlined dense clearable label="Відділення" :options="departmentNames" use-input new-value-mode="add-unique" /></div>
          <div class="col-12"><q-input v-model="form.comments" outlined dense autogrow label="Коментар (що саме повідомлено, рекомендації)" /></div>
        </div>
        <q-item tag="label" class="bg-grey-1 rounded-borders" v-ripple>
          <q-item-section avatar><q-checkbox v-model="form.readbackConfirmed" color="negative" data-testid="panicReadback" /></q-item-section>
          <q-item-section>
            <q-item-label class="text-weight-bold">Read-back підтверджено</q-item-label>
            <q-item-label caption>Лікар повторив уголос ПІБ пацієнта, показник та значення (вимога ISO 15189 / CLSI)</q-item-label>
          </q-item-section>
        </q-item>
        <div v-if="error" class="text-negative">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="negative" icon="call" :label="form.id ? 'Зберегти' : 'Зареєструвати дзвінок'" :loading="saving" :disable="!canSave" data-testid="panicSave" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
export default {
  name: 'PanicCallDialog',
  props: {
    value: Boolean,
    result: { type: Object, default: null },
    call: { type: Object, default: null }
  },
  data () {
    return { form: this.empty(), saving: false, error: '' };
  },
  computed: {
    departmentNames () { return this.$store.getters['dictionaries/items']('departments').map(d => d.name); },
    canSave () { return !!(this.form.doctorName && this.form.phone && this.form.readbackConfirmed); }
  },
  watch: {
    value (v) {
      if (v) {
        this.error = '';
        this.form = this.call ? { id: this.call.id, doctorName: this.call.doctorNotifiedName || this.call.doctorName, phone: this.call.phone, department: this.call.department, readbackConfirmed: !!this.call.readbackConfirmed, comments: this.call.comments } : { ...this.empty(), phone: this.panicPhone() };
        this.$store.dispatch('dictionaries/load', 'departments');
      }
    }
  },
  methods: {
    empty () { return { id: null, doctorName: '', phone: '', department: null, readbackConfirmed: false, comments: '' }; },
    panicPhone () { const lab = this.$store.state.context.lab; return (lab && lab.panicPhone) || ''; },
    async save () {
      this.saving = true; this.error = '';
      const body = { doctorName: this.form.doctorName, phone: this.form.phone, department: this.form.department || null, readbackConfirmed: this.form.readbackConfirmed, comments: this.form.comments || null };
      try {
        if (this.form.id) await this.$api.updatePanicCall(this.form.id, body);
        else await this.$api.createPanicCall({ resultId: this.result && (this.result.resultId || this.result.id), ...body });
        this.$q.notify({ type: 'positive', message: 'Дзвінок зафіксовано в журналі' });
        this.$emit('saved');
        this.$emit('input', false);
      } catch (e) { this.error = e.userMessage || 'Не вдалося зберегти'; } finally { this.saving = false; }
    }
  }
};
</script>
