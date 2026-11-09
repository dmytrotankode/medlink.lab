<template>
  <q-dialog :value="value" @input="$emit('input', $event)" no-backdrop-dismiss>
    <q-card style="min-width: 640px; max-width: 96vw" data-testid="cultureDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="fas fa-bacterium" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Редагування посіву' : 'Новий посів (бактеріологія)' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
      <q-card-section>
        <div class="row q-col-gutter-sm">
          <div class="col-12" v-if="!form.id">
            <q-select v-model="form.orderTestId" outlined dense use-input hide-selected fill-input input-debounce="300" label="Дослідження замовлення (посів) *" :options="testOptions" emit-value map-options :loading="searching" :error="touched && !form.orderTestId" hide-bottom-space data-testid="cultureOrderTest" @filter="searchTests">
              <template v-slot:no-option><q-item><q-item-section class="text-grey">Введіть номер замовлення, штрихкод або прізвище пацієнта</q-item-section></q-item></template>
            </q-select>
          </div>
          <div class="col-12 col-sm-6"><q-select v-model="form.specimenLocus" outlined dense label="Локалізація / біоматеріал *" :options="loci" use-input new-value-mode="add-unique" :error="touched && !form.specimenLocus" hide-bottom-space data-testid="cultureSpecimen" /></div>
          <div class="col-12 col-sm-6"><q-input v-model="form.cultureMedium" outlined dense label="Живильне середовище" placeholder="Кров'яний агар" /></div>
          <div class="col-12 col-sm-6"><q-input v-model="form.incubationStart" outlined dense type="datetime-local" stack-label label="Початок інкубації" /></div>
          <div class="col-12 col-sm-6"><q-input v-model="form.incubationHoursRecommended" outlined dense inputmode="numeric" label="Рекомендована тривалість інкубації, год" /></div>
          <template v-if="form.id">
            <div class="col-12 col-sm-4"><q-toggle v-model="form.growthDetected" label="Є ріст" color="deep-orange-6" /></div>
            <div class="col-12 col-sm-4"><q-select v-model="form.growthIntensity" outlined dense clearable emit-value map-options label="Інтенсивність росту" :options="[{ value: 'NO_GROWTH', label: 'Росту немає' }, { value: 'SCANTY_1+', label: 'Скудний ріст (1+)' }, { value: 'MODERATE_2+', label: 'Помірний ріст (2+)' }, { value: 'HEAVY_3+', label: 'Рясний ріст (3+)' }, { value: 'PROFUSE_4+', label: 'Дуже рясний ріст (4+)' }]" /></div>
            <div class="col-12 col-sm-4"><q-input v-model="form.cfuPerMl" outlined dense label="КУО/мл" /></div>
            <div class="col-12"><q-input v-model="form.preliminaryReport" outlined dense autogrow label="Попередній висновок" /></div>
            <div class="col-12"><q-input v-model="form.finalMicroscopyDescription" outlined dense autogrow label="Опис мікроскопії" /></div>
          </template>
        </div>
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="cultureSave" @click="save" /></q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { parseDecimal } from '../../../utils/format';

export default {
  name: 'CultureDialog',
  props: { value: Boolean, culture: { type: Object, default: null } },
  data () {
    return {
      form: this.empty(), saving: false, error: '', touched: false, searching: false, testOptions: [],
      loci: ['Сеча', 'Кров (гемокультура)', 'Мазок із зіва', 'Мазок із носа', 'Мазок із рани', 'Мокротиння', 'Ліквор', 'Кал', 'Мазок урогенітальний', 'Жовч', 'Виділення з ока', 'Виділення з вуха']
    };
  },
  watch: {
    value (v) {
      if (v) {
        this.error = ''; this.touched = false; this.testOptions = [];
        const c = this.culture;
        // Поля відповідають відповіді сервера (specimenLocus, growthDetected, incubationStart …)
        this.form = c ? { ...this.empty(), id: c.id, specimenLocus: c.specimenLocus, cultureMedium: c.cultureMedium || '', incubationStart: c.incubationStart ? this.toLocal(c.incubationStart) : '', incubationHoursRecommended: c.incubationHoursRecommended != null ? String(c.incubationHoursRecommended) : '', growthDetected: !!c.growthDetected, growthIntensity: c.growthIntensity || '', cfuPerMl: c.cfuPerMl || '', preliminaryReport: c.preliminaryReport || '', finalMicroscopyDescription: c.finalMicroscopyDescription || '' } : this.empty();
      }
    }
  },
  methods: {
    empty () { return { id: null, orderTestId: null, specimenLocus: null, cultureMedium: '', incubationStart: '', incubationHoursRecommended: '', growthDetected: false, growthIntensity: '', cfuPerMl: '', preliminaryReport: '', finalMicroscopyDescription: '' }; },
    toLocal (iso) { const d = new Date(iso); const p = n => String(n).padStart(2, '0'); return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`; },
    /** Пошук дослідження, для якого заводиться посів: за номером замовлення, штрихкодом або пацієнтом */
    async searchTests (val, update, abort) {
      if (!val || val.length < 2) { update(() => { this.testOptions = []; }); return; }
      this.searching = true;
      try {
        const res = await this.$api.getWorklist({ search: val, pageSize: 20 });
        const rows = Array.isArray(res) ? res : (res && (res.items || res.rows)) || [];
        update(() => { this.testOptions = rows.map(r => ({ value: r.orderTestId, label: `№ ${r.orderNumber} · ${r.patientName} · ${r.testName || r.testCode}` })); });
      } catch (e) { abort(); } finally { this.searching = false; }
    },
    async save () {
      this.touched = true; this.error = '';
      if (!this.form.id && !this.form.orderTestId) { this.error = 'Оберіть дослідження замовлення, для якого заводиться посів'; return; }
      if (!this.form.specimenLocus) { this.error = 'Вкажіть локалізацію або біоматеріал'; return; }
      this.saving = true;
      const f = this.form;
      const hours = parseDecimal(f.incubationHoursRecommended);
      const body = { specimenLocus: f.specimenLocus, cultureMedium: f.cultureMedium || null, incubationHoursRecommended: hours !== null ? Math.round(hours) : null, incubationStart: f.incubationStart ? new Date(f.incubationStart).toISOString() : null };
      if (f.id) Object.assign(body, { growthDetected: !!f.growthDetected, growthIntensity: f.growthIntensity || null, cfuPerMl: f.cfuPerMl || null, preliminaryReport: f.preliminaryReport || null, finalMicroscopyDescription: f.finalMicroscopyDescription || null });
      else body.orderTestId = f.orderTestId;
      try {
        const res = f.id ? await this.$api.updateCulture(f.id, body) : await this.$api.createCulture(body);
        this.$q.notify({ type: 'positive', message: 'Посів збережено' });
        this.$emit('saved', res); this.$emit('input', false);
      } catch (e) { this.error = e.userMessage || 'Не вдалося зберегти посів'; } finally { this.saving = false; }
    }
  }
};
</script>
