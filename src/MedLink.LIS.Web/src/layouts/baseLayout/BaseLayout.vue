<template>
  <q-layout view="hHh LpR fFf" class="base-layout">
    <q-header id="main-header" reveal-offset="80">
      <div class="brand-gradient-line" />
      <q-toolbar class="q-px-sm" style="min-height: 52px">
        <q-btn flat dense round icon="menu" color="grey-8" aria-label="Меню" @click="toggleDrawer" />

        <router-link :to="{ name: 'lab-dashboard' }" class="row items-center no-wrap q-ml-sm text-dark" style="text-decoration: none">
          <div class="brand-logo-box q-mr-sm">
            <q-icon name="fas fa-flask" size="18px" />
          </div>
          <div class="column">
            <div class="text-weight-bold" style="font-size: 16px; line-height: 1.1">MedLink LIS <span class="text-primary">4.0</span></div>
            <div class="text-caption text-grey-7 ellipsis" style="font-size: 11.5px; line-height: 1.1; max-width: 260px">{{ labName }}</div>
          </div>
        </router-link>

        <q-space />

        <!-- Глобальний пошук за штрихкодом -->
        <q-input
          v-model="barcodeQuery"
          dense
          outlined
          class="header-barcode gt-xs q-mr-sm"
          placeholder="Штрихкод пробірки / № замовлення"
          bg-color="white"
          :loading="barcodeSearching"
          @keyup.enter="searchBarcode"
        >
          <template v-slot:prepend><q-icon name="qr_code_scanner" size="18px" /></template>
          <template v-slot:append>
            <q-btn flat dense round icon="search" size="sm" @click="searchBarcode" />
          </template>
        </q-input>

        <!-- Індикатор шлюзу аналізаторів -->
        <router-link :to="{ name: 'lab-analyzers' }" class="header-chip gt-sm q-mr-sm" :class="gatewayClass" style="text-decoration: none">
          <q-icon :name="gatewayOnline > 0 ? 'wifi' : 'wifi_off'" size="15px" />
          <span>Шлюз: {{ gatewayLabel }}</span>
          <q-tooltip>Коннектори онлайн {{ gatewayOnline }} з {{ gatewayTotal }}; аналізаторів онлайн {{ analyzersOnline }}</q-tooltip>
        </router-link>

        <!-- Паніка -->
        <router-link :to="{ name: 'lab-validation', query: { tab: 'panic' } }" class="header-chip gt-xs q-mr-sm" :class="panicCount > 0 ? 'header-chip--bad' : 'header-chip--muted'" style="text-decoration: none">
          <q-icon name="priority_high" size="15px" />
          <span>Паніка: {{ panicCount }}</span>
          <q-tooltip>Критичні результати без зареєстрованого дзвінка</q-tooltip>
        </router-link>

        <!-- Lockout ВКЯ -->
        <router-link v-if="lockoutCount > 0" :to="{ name: 'lab-qc', query: { tab: 'lockouts' } }" class="header-chip header-chip--warn gt-sm q-mr-sm" style="text-decoration: none">
          <q-icon name="lock" size="15px" />
          <span>ВКЯ Lockout: {{ lockoutCount }}</span>
        </router-link>

        <!-- Працюю як… -->
        <q-select
          v-model="employeeId"
          :options="employeeOptions"
          dense
          outlined
          emit-value
          map-options
          bg-color="white"
          class="header-employee"
          :loading="contextLoading"
          :label="employeeOptions.length ? undefined : 'Працюю як…'"
        >
          <template v-slot:prepend>
            <q-avatar color="primary" text-color="white" size="26px" icon="person" />
          </template>
          <template v-slot:selected-item="scope">
            <div class="column no-wrap" style="line-height: 1.1">
              <div class="text-weight-bold ellipsis" style="font-size: 13px; max-width: 190px">{{ scope.opt.label }}</div>
              <div class="text-caption text-grey-7 ellipsis" style="font-size: 11px; max-width: 190px">{{ roleLabel(scope.opt.labRole) || scope.opt.caption }}</div>
            </div>
          </template>
          <template v-slot:option="scope">
            <q-item v-bind="scope.itemProps" v-on="scope.itemEvents">
              <q-item-section avatar><q-icon name="person" /></q-item-section>
              <q-item-section>
                <q-item-label>{{ scope.opt.label }}</q-item-label>
                <q-item-label caption>{{ roleLabel(scope.opt.labRole) || scope.opt.caption }}</q-item-label>
              </q-item-section>
            </q-item>
          </template>
          <template v-slot:no-option>
            <q-item><q-item-section class="text-grey">Співробітники недоступні (API)</q-item-section></q-item>
          </template>
          <q-tooltip>Працюю як… (без пароля; заголовок X-MedLink-Employee-Id)</q-tooltip>
        </q-select>
      </q-toolbar>
    </q-header>

    <menu-drawer v-model="drawerOpen" />

    <q-page-container>
      <q-page class="q-pa-md" style="min-height: calc(100vh - 56px)">
        <api-error-banner v-if="apiOffline" offline-only @retry="refreshStatus" />
        <router-view :key="$route.fullPath" />
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script>
import { mapState, mapGetters } from 'vuex';
import MenuDrawer from '../../components/baseElements/menuDrawer.vue';
import { ROLE } from '../../utils/statuses';

export default {
  name: 'BaseLayout',
  components: { MenuDrawer },
  data () {
    return {
      drawerOpen: this.$q.screen.gt.sm,
      barcodeQuery: '',
      barcodeSearching: false
    };
  },
  computed: {
    ...mapState('laboratory', ['apiOffline']),
    ...mapGetters('laboratory', ['panicCount', 'gatewayOnline', 'gatewayTotal', 'analyzersOnline', 'lockoutCount']),
    ...mapGetters('context', ['employeeOptions', 'labName']),
    contextLoading () { return this.$store.state.context.loading; },
    employeeId: {
      get () { return this.$store.state.context.employeeId; },
      set (v) {
        if (v && v !== this.$store.state.context.employeeId) {
          this.$store.dispatch('context/switchEmployee', v);
          this.$q.notify({ type: 'info', message: 'Співробітника змінено. Заголовок X-MedLink-Employee-Id оновлено.' });
        }
      }
    },
    gatewayLabel () {
      if (this.apiOffline) return 'немає зв’язку';
      if (!this.gatewayTotal) return 'не налаштовано';
      return `онлайн ${this.gatewayOnline}/${this.gatewayTotal}`;
    },
    gatewayClass () {
      if (this.apiOffline || (!this.gatewayOnline && this.gatewayTotal)) return 'header-chip--bad';
      if (!this.gatewayTotal) return 'header-chip--muted';
      return this.gatewayOnline === this.gatewayTotal ? 'header-chip--ok' : 'header-chip--warn';
    }
  },
  created () {
    this.$store.dispatch('context/init');
    this.$store.dispatch('laboratory/startPolling');
  },
  beforeDestroy () {
    this.$store.dispatch('laboratory/stopPolling');
  },
  methods: {
    toggleDrawer () { this.drawerOpen = !this.drawerOpen; },
    roleLabel (role) { return ROLE[role] || ''; },
    refreshStatus () {
      this.$store.dispatch('laboratory/refreshStatus');
      this.$store.dispatch('context/init');
    },
    async searchBarcode () {
      const q = (this.barcodeQuery || '').trim();
      if (!q) return;
      this.barcodeSearching = true;
      try {
        const order = await this.$api.orderByBarcode(q);
        if (order && order.id) {
          this.$router.push({ name: 'lab-order-card', params: { id: order.id }, query: { barcode: q } });
          this.barcodeQuery = '';
          return;
        }
        this.$q.notify({ type: 'warning', message: `Пробірку ${q} не знайдено` });
      } catch (e) {
        if (e.apiStatus === 404) {
          // Спробувати як номер замовлення
          try {
            const res = await this.$api.getOrders({ search: q, pageSize: 1 });
            const items = Array.isArray(res) ? res : (res && res.items) || [];
            if (items.length) {
              this.$router.push({ name: 'lab-order-card', params: { id: items[0].id } });
              this.barcodeQuery = '';
              return;
            }
          } catch (e2) { /* ignore */ }
          this.$q.notify({ type: 'warning', message: `Пробірку або замовлення «${q}» не знайдено` });
        } else {
          this.$q.notify({ type: 'negative', message: e.userMessage || 'Помилка пошуку' });
        }
      } finally {
        this.barcodeSearching = false;
      }
    }
  }
};
</script>

<style lang="stylus">
.header-barcode
  width 270px
  .q-field__control
    height 36px
  .q-field__marginal
    height 36px
.header-employee
  min-width 230px
  max-width 290px
  .q-field__control
    height 40px
  .q-field__marginal
    height 40px
  .q-field__native
    padding 0
@media (max-width: 1100px)
  .header-barcode
    width 190px
</style>
