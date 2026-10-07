<template>
  <div class="portal-home" data-testid="portalHome">
    <div class="medlink-card q-pa-lg q-mt-md text-center">
      <q-icon name="fas fa-user-circle" color="primary" size="56px" />
      <div class="text-h5 text-weight-bold q-mt-sm">Кабінет пацієнта</div>
      <div class="text-grey-7 q-mb-md">Результати досліджень, статуси замовлень, динаміка показників, PDF-бланки та сповіщення.</div>
      <q-banner dense rounded class="bg-orange-1 text-orange-10 q-mb-md text-left"><q-icon name="info" /> Демо-режим без автентифікації: оберіть пацієнта зі списку. У бойовому evomis пацієнт підставляється із сесії МІС (OTP не використовується).</q-banner>
      <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />
      <q-select
        v-model="patient"
        :options="options"
        option-label="display"
        outlined
        use-input
        input-debounce="300"
        label="Пацієнт (почніть вводити ПІБ або телефон)"
        :loading="loading"
        @filter="filterFn"
        class="text-left"
        data-testid="portalPatientSelect"
      >
        <template v-slot:prepend><q-icon name="person_search" /></template>
        <template v-slot:option="scope">
          <q-item v-bind="scope.itemProps" v-on="scope.itemEvents">
            <q-item-section><q-item-label>{{ scope.opt.display }}</q-item-label><q-item-label caption>{{ scope.opt.birthDate | date }} · {{ genderLabel(scope.opt.gender) }} <span v-if="scope.opt.phone">· {{ scope.opt.phone }}</span></q-item-label></q-item-section>
          </q-item>
        </template>
        <template v-slot:no-option><q-item><q-item-section class="text-grey">Пацієнтів не знайдено</q-item-section></q-item></template>
      </q-select>
      <q-btn color="primary" size="lg" icon="login" label="Увійти до кабінету" class="q-mt-md full-width" :disable="!patient" data-testid="portalEnter" @click="enter" />
    </div>
    <div v-if="recent.length" class="medlink-card q-pa-md q-mt-md">
      <div class="section-title">Пацієнти (демо)</div>
      <q-list dense separator>
        <q-item v-for="p in recent" :key="p.id" clickable @click="patient = p; enter()">
          <q-item-section avatar><q-avatar color="blue-1" text-color="primary" icon="person" /></q-item-section>
          <q-item-section><q-item-label>{{ p.display }}</q-item-label><q-item-label caption>{{ p.birthDate | date }} · {{ genderLabel(p.gender) }}</q-item-label></q-item-section>
          <q-item-section side><q-icon name="chevron_right" /></q-item-section>
        </q-item>
      </q-list>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import { patientDisplay, genderLabel } from '../../utils/format';

export default {
  name: 'PortalHome',
  mixins: [apiMixin],
  data () { return { patient: null, options: [], recent: [] }; },
  created () { this.load(); },
  methods: {
    genderLabel,
    async load () {
      const res = await this.callApi(() => this.$api.searchPatients(''));
      if (res === undefined) return;
      this.recent = this.asList(res).slice(0, 8).map(p => ({ ...p, display: patientDisplay(p) }));
      this.options = this.recent;
    },
    async filterFn (val, update, abort) {
      try {
        const res = await this.$api.searchPatients(val || '');
        update(() => { this.options = this.asList(res).map(p => ({ ...p, display: patientDisplay(p) })); });
      } catch (e) { abort(); }
    },
    enter () { if (this.patient) this.$router.push({ name: 'portal-orders', params: { patientId: this.patient.id } }); }
  }
};
</script>
