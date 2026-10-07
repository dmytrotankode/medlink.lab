<template>
  <div class="portal-orders" data-testid="portalOrders">
    <div class="medlink-card q-pa-md q-mb-md" style="border-left: 4px solid #4274A7">
      <div class="row items-center">
        <q-avatar color="primary" text-color="white" icon="person" class="q-mr-md" />
        <div>
          <div class="text-subtitle1 text-weight-bold">{{ patientName }}</div>
          <div v-if="patientLatin" class="text-caption text-grey-7"><q-icon name="translate" size="12px" /> {{ patientLatin }}</div>
          <div class="text-caption text-grey-7">{{ patient && patient.birthDate | date }} · {{ genderLabel(patient && patient.gender) }} <span v-if="patient && patient.phone">· {{ patient.phone }}</span></div>
        </div>
        <q-space />
        <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      </div>
    </div>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="row items-center q-mb-sm q-gutter-sm">
      <q-input v-model="search" dense outlined clearable placeholder="Пошук за назвою послуги / датою" class="col"><template v-slot:prepend><q-icon name="search" /></template></q-input>
      <q-select v-model="statusFilter" dense outlined clearable label="Статус" :options="statusOptions" emit-value map-options style="min-width: 180px" />
    </div>

    <div v-for="o in filtered" :key="o.id" class="medlink-card q-pa-md q-mb-sm order-item cursor-pointer" :data-testid="`portal-order-${o.orderNumber}`" @click="open(o)">
      <div class="row items-center justify-between q-mb-xs">
        <div><span class="text-weight-bold">№ {{ o.orderNumber }}</span> <span class="text-grey-7 q-ml-sm">{{ o.orderDatetime | datetime }}</span> <q-badge v-if="o.isUrgentCito" color="deep-orange-6" label="CITO" class="q-ml-xs" /></div>
        <q-badge :color="stageMeta(o).color" :label="stageMeta(o).label" class="q-pa-xs" />
      </div>
      <div class="text-caption text-grey-8 q-mb-sm">{{ servicesOf(o) }}</div>
      <div v-if="progress(o)" class="row items-center q-gutter-sm q-mb-sm" :data-testid="`portal-progress-${o.orderNumber}`">
        <q-linear-progress :value="progress(o).ratio" :color="progress(o).ratio >= 1 ? 'positive' : 'primary'" size="10px" rounded class="col" />
        <span class="text-caption text-weight-bold">{{ progress(o).released }} / {{ progress(o).total }} показн. готово</span>
      </div>
      <!-- Степер статусу -->
      <div class="row no-wrap items-center portal-stepper">
        <template v-for="(s, i) in stages">
          <div :key="s.key" class="portal-step" :class="{ 'portal-step--done': stageIndex(o) > i, 'portal-step--active': stageIndex(o) === i, 'portal-step--cancelled': isCancelled(o) }">
            <q-icon :name="stageIndex(o) > i ? 'check_circle' : s.icon" size="18px" />
            <span class="gt-xs">{{ s.label }}</span>
          </div>
          <div v-if="i < stages.length - 1" :key="s.key + '-line'" class="portal-step-line" :class="{ 'portal-step-line--done': stageIndex(o) > i }" />
        </template>
      </div>
      <div class="row justify-end q-mt-sm q-gutter-xs" @click.stop>
        <q-btn v-if="o.status === 'RELEASED'" dense outline color="primary" icon="picture_as_pdf" label="Остаточний бланк" type="a" :href="$api.portalPdfUrl(patientId, o.id)" target="_blank" />
        <q-btn v-else-if="progress(o) && progress(o).released > 0" dense outline color="orange-8" icon="hourglass_top" label="Попередній бланк" type="a" :href="$api.portalPdfUrl(patientId, o.id) + '?variant=preliminary'" target="_blank" />
        <q-btn dense flat color="primary" icon="visibility" label="Переглянути" @click="open(o)" />
      </div>
    </div>
    <div v-if="!filtered.length && !loading" class="medlink-card"><empty-state title="Досліджень немає" icon="inbox" hint="Коли лабораторія зареєструє направлення — воно з’явиться тут" /></div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import { ORDER_STATUS, toOptions } from '../../utils/statuses';
import { patientDisplay, genderLabel } from '../../utils/format';

const STAGES = [
  { key: 'registered', label: 'Зареєстровано', icon: 'assignment', statuses: ['NEW'] },
  { key: 'collected', label: 'Матеріал забрано', icon: 'colorize', statuses: ['COLLECTED', 'IN_TRANSIT'] },
  { key: 'inwork', label: 'У роботі', icon: 'science', statuses: ['RECEIVED', 'IN_PROGRESS', 'PARTIALLY_COMPLETED'] },
  { key: 'ready', label: 'Готово', icon: 'task_alt', statuses: ['COMPLETED'] },
  { key: 'released', label: 'Видано', icon: 'verified', statuses: ['RELEASED'] }
];

export default {
  name: 'PortalOrders',
  mixins: [apiMixin],
  props: { patientId: { type: String, required: true } },
  data () { return { orders: [], patient: null, search: '', statusFilter: null, statusOptions: toOptions(ORDER_STATUS), stages: STAGES }; },
  computed: {
    patientName () { return this.patient ? patientDisplay(this.patient) : 'Пацієнт'; },
    patientLatin () { const p = this.patient || {}; return [p.lastNameLatin, p.firstNameLatin].filter(Boolean).join(' '); },
    filtered () {
      const s = (this.search || '').toLowerCase();
      return this.orders.filter(o => (!this.statusFilter || o.status === this.statusFilter) && (!s || `${o.orderNumber} ${this.servicesOf(o)} ${o.orderDatetime}`.toLowerCase().includes(s)));
    }
  },
  created () { this.load(); },
  methods: {
    genderLabel,
    servicesOf (o) { const names = (o.profiles || []).map(p => p.name || p.profileName); if (names.length) return names.join(', '); const t = (o.tests || []).map(x => x.testName || x.testCode); return t.length > 5 ? `${t.slice(0, 5).join(', ')} +${t.length - 5}` : (t.join(', ') || `${o.testsCount || 0} показн.`); },
    isCancelled (o) { return ['CANCELLED', 'REJECTED'].includes(o.status); },
    progress (o) {
      const total = Number(o.totalTests !== undefined ? o.totalTests : (o.tests || []).length) || 0;
      const released = Number(o.releasedTests !== undefined ? o.releasedTests : (o.tests || []).filter(t => ['VERIFIED', 'AUTO_VERIFIED', 'RELEASED'].includes(t.status)).length) || 0;
      if (!total) return null;
      return { total, released, ratio: released / total };
    },
    stageIndex (o) { if (this.isCancelled(o)) return -1; const i = STAGES.findIndex(s => s.statuses.includes(o.status)); return i < 0 ? 0 : i; },
    stageMeta (o) {
      if (this.isCancelled(o)) return { color: 'grey-6', label: ORDER_STATUS[o.status].label };
      if (o.status === 'PARTIALLY_COMPLETED') return { color: 'lime-8', label: 'Частково готово' };
      const s = STAGES[this.stageIndex(o)]; return { color: s.key === 'released' ? 'positive' : (s.key === 'ready' ? 'light-green-7' : 'primary'), label: s.label };
    },
    async load () {
      const results = await Promise.allSettled([this.$api.portalOrders(this.patientId), this.$api.getPatient(this.patientId)]);
      if (results[0].status === 'fulfilled') this.orders = this.asList(results[0].value).sort((a, b) => new Date(b.orderDatetime) - new Date(a.orderDatetime));
      if (results[1].status === 'fulfilled') this.patient = results[1].value;
      if (results[0].status === 'rejected' && !this.apiOffline) this.apiError = results[0].reason.userMessage;
    },
    open (o) { this.$router.push({ name: 'portal-order', params: { patientId: this.patientId, id: o.id } }); }
  }
};
</script>

<style lang="stylus" scoped>
.order-item:hover
  box-shadow 0 4px 12px rgba(0, 0, 0, 0.1)
.portal-step
  display flex
  align-items center
  gap 4px
  font-size 12px
  color #9ca3af
  white-space nowrap
.portal-step--done
  color #21ba45
.portal-step--active
  color #4274A7
  font-weight 700
.portal-step--cancelled
  color #c0c0c0
.portal-step-line
  flex 1
  height 2px
  background #e5e7eb
  margin 0 6px
  min-width 10px
.portal-step-line--done
  background #21ba45
</style>
