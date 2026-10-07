<template>
  <q-layout view="hHh Lpr fFf" class="portal-layout">
    <q-header class="bg-white text-dark" style="border-bottom: 1px solid #e0e0e0">
      <div class="brand-gradient-line" />
      <q-toolbar style="min-height: 52px">
        <router-link :to="{ name: 'portal-home' }" class="row items-center no-wrap text-dark" style="text-decoration: none">
          <div class="brand-logo-box q-mr-sm"><q-icon name="fas fa-user-circle" size="18px" /></div>
          <div class="column">
            <div class="text-weight-bold" style="font-size: 15px; line-height: 1.1">Кабінет пацієнта</div>
            <div class="text-caption text-grey-7" style="font-size: 11px; line-height: 1.1">MedLink LIS · {{ labName }}</div>
          </div>
        </router-link>
        <q-space />
        <q-btn v-if="patientId" flat dense no-caps icon="list_alt" label="Дослідження" class="gt-xs" :to="{ name: 'portal-orders', params: { patientId } }" />
        <q-btn v-if="patientId" flat dense round icon="notifications" :to="{ name: 'portal-notifications', params: { patientId } }">
          <q-tooltip>Сповіщення</q-tooltip>
        </q-btn>
        <q-btn flat dense no-caps icon="swap_horiz" label="Інший пацієнт" class="gt-xs" :to="{ name: 'portal-home' }" />
        <q-btn flat dense round icon="science" :to="{ name: 'lab-dashboard' }"><q-tooltip>До робочого місця лабораторії</q-tooltip></q-btn>
      </q-toolbar>
    </q-header>

    <q-page-container>
      <q-page class="q-pa-md portal-page">
        <api-error-banner v-if="apiOffline" offline-only @retry="retry" />
        <router-view :key="$route.fullPath" />
      </q-page>
    </q-page-container>

    <q-footer class="bg-transparent text-grey-6 text-center q-pa-sm" style="font-size: 11px">
      ТОВ «МедЛінк» © 2026 · результати відповідають підписаному бланку лабораторії
    </q-footer>
  </q-layout>
</template>

<script>
export default {
  name: 'PortalLayout',
  computed: {
    patientId () { return this.$route.params.patientId || null; },
    apiOffline () { return this.$store.state.laboratory.apiOffline; },
    labName () { return this.$store.getters['context/labName']; }
  },
  created () {
    this.$store.dispatch('context/loadMe').catch(() => {});
  },
  methods: {
    retry () { this.$router.go(0); }
  }
};
</script>

<style lang="stylus">
.portal-page
  max-width 960px
  margin 0 auto
</style>
