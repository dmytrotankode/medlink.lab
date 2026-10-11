<template>
  <div class="norms-page" data-testid="normsPage">
    <page-header title="Довідник послуг, норми та методики" icon="fas fa-sliders-h" subtitle="Багатовимірні референтні інтервали (каскад Simplex): стать, вік (дні/місяці/роки), вагітність, фаза циклу, МКХ-10, методики" :breadcrumbs="[{ label: 'Послуги та норми' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn outline dense color="teal-7" icon="calculate" label="Симулятор підбору норми" data-testid="openResolver" @click="tab = 'resolver'" />
      <q-btn unelevated dense color="primary" icon="add" label="Створити послугу" data-testid="profileCreate" @click="openProfile(null)" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <q-tabs v-model="tab" dense align="left" class="text-grey-8 medlink-card bg-white" active-color="primary" indicator-color="primary">
      <q-tab name="catalog" icon="grid_view" :label="`Каталог послуг (${filtered.length})`" />
      <q-tab name="resolver" icon="calculate" label="Резолвер норм" />
    </q-tabs>

    <q-tab-panels v-model="tab" animated class="bg-transparent q-mt-sm">
      <q-tab-panel name="catalog" class="q-pa-none">
        <div class="medlink-card q-pa-sm q-mb-md row q-col-gutter-sm items-center">
          <div class="col-12 col-md-5"><q-input v-model="search" dense outlined clearable placeholder="Пошук послуги за назвою, кодом або показником…" data-testid="profileSearch"><template v-slot:prepend><q-icon name="search" /></template></q-input></div>
          <div class="col-12 col-md-7 row justify-end q-gutter-xs">
            <q-btn-toggle v-model="category" dense no-caps unelevated toggle-color="primary" color="white" text-color="grey-8" :options="[{ value: null, label: `Всі (${profiles.length})` }, ...categories.map(c => ({ value: c, label: c }))]" />
          </div>
        </div>
        <div class="row q-col-gutter-md">
          <div v-for="p in filtered" :key="p.id" class="col-12 col-sm-6 col-lg-4">
            <div class="medlink-card service-card cursor-pointer" :data-testid="`profile-${p.code}`" @click="openCard(p)">
              <div class="q-pa-md">
                <div class="row items-center justify-between q-mb-xs">
                  <div class="row q-gutter-xs"><q-badge color="primary" :label="p.code" /><q-badge v-if="p.isActive === false" color="grey-5" label="неактивна" /></div>
                  <q-badge outline color="purple-6" :label="p.category" />
                </div>
                <div class="text-subtitle1 text-weight-bold" style="line-height: 1.2">{{ p.name }}</div>
                <div v-if="p.organizationServiceId" class="text-caption text-primary"><q-icon name="link" size="12px" /> Джерело: прайс послуг MedLink (ID {{ p.organizationServiceId }})</div>
                <div class="text-caption text-grey-7 q-mt-xs">Показників: <b>{{ (p.items || []).length }}</b> · TAT <b>{{ p.turnaroundHours }}</b> год · {{ p.fastingRequired ? 'натще' : 'без підготовки' }}</div>
                <div class="row items-center justify-between q-mt-sm bg-grey-1 q-pa-xs rounded-borders">
                  <span class="text-caption">Шарів норм:</span>
                  <b class="text-primary">{{ layerCount(p) }}</b>
                </div>
                <div class="row q-gutter-xs q-mt-xs">
                  <q-chip v-for="t in (p.items || []).slice(0, 6)" :key="t.testId" dense size="sm" color="blue-1" text-color="primary">{{ testCode(t.testId) }}</q-chip>
                  <q-chip v-if="(p.items || []).length > 6" dense size="sm" color="grey-3">+{{ (p.items || []).length - 6 }}</q-chip>
                </div>
              </div>
              <div class="row items-center justify-between q-px-md q-py-xs bg-grey-1" style="border-top: 1px solid #eee" @click.stop>
                <span class="text-weight-bold">{{ p.price | money }}</span>
                <span>
                  <q-btn flat dense round size="sm" icon="open_in_new" color="primary" @click="openCard(p)"><q-tooltip>Картка послуги</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="edit" color="primary" @click="openProfile(p)" />
                  <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDelete(p)" />
                </span>
              </div>
            </div>
          </div>
          <div v-if="!filtered.length && !loading" class="col-12"><div class="medlink-card"><empty-state title="Послуг не знайдено" icon="medical_services" /></div></div>
        </div>
      </q-tab-panel>
      <q-tab-panel name="resolver" class="q-pa-none">
        <resolver-playground />
      </q-tab-panel>
    </q-tab-panels>

    <service-card-dialog v-model="cardOpen" :profile="active" @edit="p => { cardOpen = false; openProfile(p); }" @delete="p => { cardOpen = false; askDelete(p); }" />
    <dictionary-form-dialog v-model="profileOpen" name="profiles" :item="active" @saved="load" />
    <confirm-dialog v-model="deleteOpen" title="Видалити послугу" :message="`Послугу «${active && active.name}» буде деактивовано (soft delete). За наявності залежностей API поверне 409.`" ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import ServiceCardDialog from './ServiceCardDialog.vue';
import ResolverPlayground from './ResolverPlayground.vue';
import DictionaryFormDialog from '../../dictionaries/DictionaryFormDialog.vue';

export default {
  name: 'NormsCatalog',
  mixins: [apiMixin],
  components: { ServiceCardDialog, ResolverPlayground, DictionaryFormDialog },
  data () {
    return { tab: 'catalog', search: '', category: null, cardOpen: false, profileOpen: false, deleteOpen: false, active: null, layerCounts: {} };
  },
  computed: {
    profiles () { return this.$store.getters['dictionaries/items']('profiles'); },
    tests () { return this.$store.getters['dictionaries/items']('tests'); },
    categories () { return [...new Set(this.profiles.map(p => p.category).filter(Boolean))].sort(); },
    filtered () {
      const s = (this.search || '').toLowerCase();
      return this.profiles.filter(p => (!this.category || p.category === this.category) && (!s || `${p.name} ${p.code} ${(p.items || []).map(i => this.testCode(i.testId)).join(' ')}`.toLowerCase().includes(s)));
    }
  },
  created () { if (this.$route.query.tab === 'resolver') this.tab = 'resolver'; this.load(); },
  methods: {
    testCode (id) { const t = this.tests.find(x => x.id === id); return t ? t.code : (id ? String(id).slice(0, 6) : ''); },
    layerCount (p) { return (p.items || []).reduce((s, i) => s + (this.layerCounts[this.testCode(i.testId)] || 0), 0) || p.layersCount || '—'; },
    async load () {
      this.loading = true;
      try {
        await this.$store.dispatch('dictionaries/load', { name: 'profiles', force: true });
        await this.$store.dispatch('dictionaries/load', 'tests');
        this.apiError = null;
        // лічильники шарів (легкий запит на всі комбінації)
        try {
          const all = await this.$api.normCombinations();
          const counts = {};
          (Array.isArray(all) ? all : (all && all.items) || []).forEach(l => { counts[l.testCode] = (counts[l.testCode] || 0) + 1; });
          this.layerCounts = counts;
        } catch (e) { /* ignore */ }
      } catch (e) { if (!this.apiOffline) this.apiError = e.userMessage; } finally { this.loading = false; }
    },
    openCard (p) { this.active = p; this.cardOpen = true; },
    openProfile (p) { this.active = p; this.profileOpen = true; },
    askDelete (p) { this.active = p; this.deleteOpen = true; },
    async doDelete () { try { await this.$api.dictDelete('profiles', this.active.id); this.notifyOk('Послугу видалено'); this.load(); } catch (e) { this.notifyError(e); } }
  }
};
</script>

<style scoped>
.service-card { transition: box-shadow .15s, transform .15s; }
.service-card:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.10); transform: translateY(-1px); }
</style>
