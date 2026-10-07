<!--
  MedLink LIS — бокове меню (темний drawer #212121, активний пункт із лівою смугою $primary),
  структура як у evomis/src/App.View/src/components/baseElements/menuDrawer.vue.
  Для перенесення в evomis: групи «Процеси / Довідники / Інтеграції / Адміністрування» додаються як
  секція «Лабораторія (ЛІС)» в існуючий drawer.
-->
<template>
  <q-drawer
    :value="value"
    @input="$emit('input', $event)"
    show-if-above
    :width="236"
    :breakpoint="1024"
    content-class="nav-drawer"
    data-testid="labMenuDrawer"
  >
    <div class="drawer-header row items-center no-wrap">
      <q-icon name="fas fa-flask" color="light-blue-4" size="20px" class="q-mr-sm" />
      <div>
        <div class="text-white text-weight-bold" style="font-size: 13.5px; line-height: 1.1">Модулі лабораторії</div>
        <div class="drawer-header-title" style="font-size: 10px">Навігація процесів ЛІС</div>
      </div>
    </div>

    <q-scroll-area style="height: calc(100% - 52px)">
      <q-list dense padding>
        <template v-for="group in groups">
          <q-item-label :key="group.title" header>{{ group.title }}</q-item-label>
          <q-item
            v-for="item in group.items"
            :key="item.name"
            clickable
            v-ripple
            :to="item.to"
            active-class="nav-active-item"
            :exact="item.exact"
          >
            <q-item-section avatar style="min-width: 32px">
              <q-icon :name="item.icon" size="17px" />
            </q-item-section>
            <q-item-section>
              <q-item-label>{{ item.label }}</q-item-label>
            </q-item-section>
            <q-item-section v-if="badge(item)" side>
              <q-badge :color="badge(item).color" :text-color="badge(item).textColor || 'white'" :label="badge(item).label" />
            </q-item-section>
          </q-item>
          <q-separator :key="group.title + '-sep'" class="q-my-xs" />
        </template>

        <q-item-label header>Кабінет пацієнта</q-item-label>
        <q-item clickable v-ripple :to="{ name: 'portal-home' }" active-class="nav-active-item">
          <q-item-section avatar style="min-width: 32px"><q-icon name="fas fa-user-circle" size="17px" /></q-item-section>
          <q-item-section><q-item-label>Кабінет пацієнта</q-item-label></q-item-section>
          <q-item-section side><q-icon name="open_in_new" size="14px" /></q-item-section>
        </q-item>

        <div class="q-pa-md text-caption" style="color: #6b7280; font-size: 11px">
          ТОВ «МедЛінк» © 2026 · ЛІС 4.0<br>
          <span v-if="lastRefreshAt">Оновлено {{ lastRefreshAt | datetime }}</span>
        </div>
      </q-list>
    </q-scroll-area>
  </q-drawer>
</template>

<script>
import { mapGetters, mapState } from 'vuex';

export default {
  name: 'MenuDrawer',
  props: {
    value: { type: Boolean, default: true }
  },
  computed: {
    ...mapGetters('laboratory', ['panicCount', 'citoCount', 'needsReviewCount', 'lockoutCount']),
    ...mapState('laboratory', ['lastRefreshAt', 'summary']),
    groups () {
      return [
        {
          title: 'Процеси',
          items: [
            { name: 'dashboard', label: 'Дашборд', icon: 'fas fa-tachometer-alt', to: { name: 'lab-dashboard' } },
            { name: 'orders', label: 'Реєстрація направлень', icon: 'fas fa-file-medical', to: { name: 'lab-orders' }, badgeKey: 'cito' },
            { name: 'phlebotomy', label: 'Пункт забору', icon: 'fas fa-syringe', to: { name: 'lab-phlebotomy' } },
            { name: 'logistics', label: 'Логістика', icon: 'fas fa-truck', to: { name: 'lab-logistics' } },
            { name: 'workstation', label: 'Робочий стіл лаборанта', icon: 'fas fa-microscope', to: { name: 'lab-workstation' }, badgeKey: 'pending' },
            { name: 'validation', label: 'Валідація та паніка', icon: 'fas fa-user-check', to: { name: 'lab-validation' }, badgeKey: 'panic' },
            { name: 'qc', label: 'Контроль якості', icon: 'fas fa-chart-line', to: { name: 'lab-qc' }, badgeKey: 'lockout' },
            { name: 'biobank', label: 'Біобанк', icon: 'fas fa-snowflake', to: { name: 'lab-biobank' } },
            { name: 'reagents', label: 'Реагенти', icon: 'fas fa-boxes', to: { name: 'lab-reagents' } },
            { name: 'microbiology', label: 'Мікробіологія', icon: 'fas fa-bacterium', to: { name: 'lab-microbiology' } },
            { name: 'tat', label: 'Аналітика TAT', icon: 'fas fa-chart-pie', to: { name: 'lab-analytics-tat' } }
          ]
        },
        {
          title: 'Довідники',
          items: [
            { name: 'norms', label: 'Послуги та норми', icon: 'fas fa-sliders-h', to: { name: 'lab-norms' } },
            { name: 'd-tests', label: 'Показники', icon: 'fas fa-list-ol', to: { name: 'lab-dictionary', params: { name: 'tests' } } },
            { name: 'd-bio', label: 'Біоматеріали', icon: 'fas fa-tint', to: { name: 'lab-dictionary', params: { name: 'biomaterials' } } },
            { name: 'd-tubes', label: 'Пробірки', icon: 'fas fa-vial', to: { name: 'lab-dictionary', params: { name: 'tube-types' } } },
            { name: 'd-methods', label: 'Методики', icon: 'fas fa-flask', to: { name: 'lab-dictionary', params: { name: 'method-types' } } },
            { name: 'd-atypes', label: 'Моделі аналізаторів', icon: 'fas fa-server', to: { name: 'lab-dictionary', params: { name: 'analyzer-types' } } },
            { name: 'd-reflex', label: 'Reflex-правила', icon: 'fas fa-random', to: { name: 'lab-dictionary', params: { name: 'reflex-rules' } } },
            { name: 'd-micro', label: 'Мікроорганізми / антибіотики', icon: 'fas fa-disease', to: { name: 'lab-dictionary', params: { name: 'organisms' } } }
          ]
        },
        {
          title: 'Інтеграції',
          items: [
            { name: 'analyzers', label: 'Аналізатори та коннектори', icon: 'fas fa-network-wired', to: { name: 'lab-analyzers' }, exact: true },
            { name: 'messages', label: 'Журнал обміну', icon: 'fas fa-exchange-alt', to: { name: 'lab-analyzer-messages' } },
            { name: 'import', label: 'Імпорт результатів', icon: 'fas fa-file-import', to: { name: 'lab-import' } }
          ]
        },
        {
          title: 'Адміністрування',
          items: [
            { name: 'settings', label: 'Налаштування лабораторії', icon: 'fas fa-cog', to: { name: 'lab-admin-settings' } },
            { name: 'users', label: 'Співробітники', icon: 'fas fa-users', to: { name: 'lab-admin-users' } },
            { name: 'audit', label: 'Аудит', icon: 'fas fa-history', to: { name: 'lab-audit' } }
          ]
        }
      ];
    }
  },
  methods: {
    badge (item) {
      switch (item.badgeKey) {
        case 'panic':
          return this.panicCount > 0 ? { label: `${this.panicCount} CITO`, color: 'negative' } : null;
        case 'cito':
          return this.citoCount > 0 ? { label: String(this.citoCount), color: 'deep-orange-6' } : null;
        case 'pending': {
          const n = Number(this.summary && this.summary.pending) || 0;
          return n > 0 ? { label: String(n), color: 'teal-6' } : null;
        }
        case 'lockout':
          return this.lockoutCount > 0 ? { label: 'Lock', color: 'purple-6' } : null;
        default:
          return null;
      }
    }
  }
};
</script>
