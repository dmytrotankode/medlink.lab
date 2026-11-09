<template>
  <q-layout view="hHh LpR fFf" class="base-layout">
    <q-header id="main-header" reveal-offset="80">
      <div class="brand-gradient-line" />
      <q-toolbar class="q-px-sm" style="min-height: 52px">
        <q-btn flat dense round icon="menu" color="grey-8" aria-label="Меню" data-testid="menuToggle" @click="toggleDrawer" />

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
          data-testid="headerBarcode"
          dense
          outlined
          class="header-barcode gt-xs q-mr-sm"
          placeholder="Штрихкод пробірки / № замовлення"
          bg-color="white"
          :loading="barcodeSearching"
          :error="barcodeNotFound"
          error-message="Не знайдено. Перевірте цифри."
          hide-bottom-space
          @input="barcodeNotFound = false"
          @keyup.enter="searchBarcode"
        >
          <template v-slot:prepend><q-icon name="qr_code_scanner" size="18px" /></template>
          <template v-slot:append>
            <q-btn flat dense round icon="search" size="sm" @click="searchBarcode" />
          </template>
        </q-input>

        <!-- На вузькому екрані пошук штрихкоду — кнопкою-лупою -->
        <q-btn flat dense round icon="search" color="grey-8" class="xs q-mr-xs" aria-label="Пошук за штрихкодом" data-testid="headerBarcodeBtn" @click="askBarcode">
          <q-tooltip>Пошук за штрихкодом пробірки або номером замовлення</q-tooltip>
        </q-btn>

        <!-- Індикатор шлюзу аналізаторів: на вузькому екрані — лише значок і число -->
        <router-link v-if="canSeeGateway" :to="{ name: 'lab-analyzers' }" class="header-chip q-mr-sm" :class="gatewayClass" style="text-decoration: none" data-testid="headerGateway">
          <q-icon :name="gatewayOnline > 0 ? 'wifi' : 'wifi_off'" size="15px" />
          <span class="gt-sm">Шлюз аналізаторів: {{ gatewayLabel }}</span>
          <span class="lt-md">{{ gatewayShort }}</span>
          <q-tooltip>Шлюз аналізаторів: коннекторів онлайн {{ gatewayOnline }} з {{ gatewayTotal }}; аналізаторів онлайн {{ analyzersOnline }}</q-tooltip>
        </router-link>

        <!-- Сесія ЕСОЗ (ТЗ 21.13) -->
        <ehealth-session-chip class="q-mr-sm" />

        <!-- Інструкції (MedLink Guide): сюди вбудовується кнопка «?» (boot/guide.js) -->
        <span id="guide-help-slot" class="q-mr-sm row items-center" />

        <!-- Критичні -->
        <router-link v-if="canSeeValidation" :to="{ name: 'lab-validation', query: { tab: 'panic' } }" class="header-chip q-mr-sm" :class="panicCount > 0 ? 'header-chip--bad' : 'header-chip--muted'" style="text-decoration: none" data-testid="headerPanic">
          <q-icon name="priority_high" size="15px" />
          <span class="gt-sm">Критичні: </span><span>{{ panicCount }}</span>
          <q-tooltip>Критичні результати без зареєстрованого дзвінка</q-tooltip>
        </router-link>

        <!-- Блокування за ВКЯ -->
        <router-link v-if="lockoutCount > 0 && canSeeQc" :to="{ name: 'lab-qc', query: { tab: 'lockouts' } }" class="header-chip header-chip--warn q-mr-sm" style="text-decoration: none" data-testid="headerLockout">
          <q-icon name="lock" size="15px" />
          <span class="gt-sm">ВКЯ: блокувань </span><span>{{ lockoutCount }}</span>
          <q-tooltip>Аналізатори, заблоковані за результатами внутрішнього контролю якості (ВКЯ)</q-tooltip>
        </router-link>

        <!-- Працюю як… -->
        <q-select
          v-model="employeeId"
          data-testid="employeeSelect"
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
          <q-tooltip>Працюю як… Роль співробітника визначає, які розділи й дії доступні (розділи, недоступні ролі, приховані).</q-tooltip>
        </q-select>
      </q-toolbar>
    </q-header>

    <menu-drawer v-model="drawerOpen" />

    <q-page-container>
      <q-page class="q-pa-md" style="min-height: calc(100vh - 56px)">
        <api-error-banner v-if="apiOffline" offline-only @retry="refreshStatus" />
        <access-denied v-if="!routeIsAllowed" />
        <router-view v-else :key="$route.fullPath" />
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script>
import { mapState, mapGetters } from 'vuex';
import MenuDrawer from '../../components/baseElements/menuDrawer.vue';
import EhealthSessionChip from '../../components/common/EhealthSessionChip.vue';
import AccessDenied from '../../components/common/AccessDenied.vue';
import roleMixin from '../../mixins/roleMixin';
import { ROLE } from '../../utils/statuses';
import { routeAllowed } from '../../utils/menuAccess';

export default {
  name: 'BaseLayout',
  components: { MenuDrawer, EhealthSessionChip, AccessDenied },
  mixins: [roleMixin],
  data () {
    return {
      drawerOpen: this.$q.screen.gt.sm,
      barcodeQuery: '',
      barcodeSearching: false,
      barcodeNotFound: false
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
          this.$q.notify({ type: 'info', message: 'Співробітника змінено. Меню й доступні дії оновлено за його роллю.' });
        }
      }
    },
    gatewayLabel () {
      if (this.apiOffline) return 'немає зв’язку';
      if (!this.gatewayTotal) return 'не налаштовано';
      return `онлайн ${this.gatewayOnline}/${this.gatewayTotal}`;
    },
    gatewayShort () {
      if (this.apiOffline || !this.gatewayTotal) return '—';
      return `${this.gatewayOnline}/${this.gatewayTotal}`;
    },
    /** Чи доступний поточний екран ролі (прямий перехід за URL → «Недостатньо прав») */
    routeIsAllowed () { return routeAllowed(this.$route && this.$route.name, this.currentLabRole); },
    canSeeGateway () { return routeAllowed('lab-analyzers', this.currentLabRole); },
    canSeeValidation () { return routeAllowed('lab-validation', this.currentLabRole); },
    canSeeQc () { return routeAllowed('lab-qc', this.currentLabRole); },
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
    askBarcode () {
      this.$q.dialog({ title: 'Пошук за штрихкодом', message: 'Штрихкод пробірки або номер замовлення', prompt: { model: '', type: 'text' }, cancel: 'Скасувати', ok: 'Знайти' })
        .onOk(v => { this.barcodeQuery = String(v || ''); this.searchBarcode(); });
    },
    async searchBarcode () {
      const q = (this.barcodeQuery || '').trim();
      if (!q) return;
      this.barcodeNotFound = false;
      this.barcodeSearching = true;
      try {
        const order = await this.$api.orderByBarcode(q);
        if (order && order.id) {
          this.$router.push({ name: 'lab-order-card', params: { id: order.id }, query: { barcode: q } });
          this.barcodeQuery = '';
          return;
        }
        this.barcodeNotFound = true;
        this.$q.notify({ type: 'warning', message: `Пробірку чи замовлення «${q}» не знайдено. Перевірте цифри.`, timeout: 5000 });
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
          this.barcodeNotFound = true;
          this.$q.notify({ type: 'warning', message: `Пробірку чи замовлення «${q}» не знайдено. Перевірте цифри.`, timeout: 5000 });
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
