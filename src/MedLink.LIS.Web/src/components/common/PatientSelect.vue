<template>
  <q-select
    :value="value"
    @input="$emit('input', $event)"
    :options="options"
    use-input
    fill-input
    hide-selected
    input-debounce="300"
    outlined
    dense
    clearable
    :label="label"
    :loading="loading"
    option-label="display"
    @filter="filterFn"
    :rules="rules"
  >
    <template v-slot:prepend><q-icon name="person_search" /></template>
    <template v-slot:option="scope">
      <q-item v-bind="scope.itemProps" v-on="scope.itemEvents">
        <q-item-section>
          <q-item-label>{{ scope.opt.display }}</q-item-label>
          <q-item-label caption>
            {{ scope.opt.birthDate | date }} · {{ genderLabel(scope.opt.gender) }}
            <span v-if="scope.opt.phone"> · {{ scope.opt.phone }}</span>
            <span v-if="scope.opt.taxId"> · ІПН {{ scope.opt.taxId }}</span>
          </q-item-label>
        </q-item-section>
      </q-item>
    </template>
    <template v-slot:no-option>
      <q-item>
        <q-item-section class="text-grey">
          {{ lastQuery.length < 2 ? 'Введіть щонайменше 2 символи (ПІБ, телефон, ІПН)' : 'Пацієнтів не знайдено' }}
        </q-item-section>
        <q-item-section v-if="lastQuery.length >= 2 && allowCreate" side>
          <q-btn dense flat color="primary" icon="person_add" label="Створити" @click="$emit('create', lastQuery)" />
        </q-item-section>
      </q-item>
    </template>
  </q-select>
</template>

<script>
import { patientDisplay, genderLabel } from '../../utils/format';

export default {
  name: 'PatientSelect',
  props: {
    value: { type: Object, default: null },
    label: { type: String, default: 'Пацієнт (пошук за ПІБ / телефоном / ІПН)' },
    allowCreate: { type: Boolean, default: true },
    rules: { type: Array, default: () => [] }
  },
  data () {
    return { options: [], loading: false, lastQuery: '' };
  },
  methods: {
    genderLabel,
    async filterFn (val, update, abort) {
      this.lastQuery = (val || '').trim();
      if (this.lastQuery.length < 2) {
        update(() => { this.options = []; });
        return;
      }
      this.loading = true;
      try {
        const res = await this.$api.searchPatients(this.lastQuery);
        const list = Array.isArray(res) ? res : (res && res.items) || [];
        update(() => {
          this.options = list.map(p => ({ ...p, display: patientDisplay(p) }));
        });
      } catch (e) {
        abort();
      } finally {
        this.loading = false;
      }
    }
  }
};
</script>
