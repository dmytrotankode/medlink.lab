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
        <template v-for="group in visibleGroups">
          <q-item-label :key="group.title" header>{{ group.title }}</q-item-label>
          <q-item
            v-for="item in group.items"
            :key="item.name"
            clickable
            v-ripple
            :to="item.to"
            :data-testid="'nav-' + item.name"
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
              <q-badge :color="badge(item).color" :text-color="badge(item).textColor || 'white'" :label="badge(item).label">
                <q-tooltip v-if="badge(item).hint">{{ badge(item).hint }}</q-tooltip>
              </q-badge>
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
import roleMixin from '../../mixins/roleMixin';
import { routeAllowed } from '../../utils/menuAccess';

export default {
  name: 'MenuDrawer',
  mixins: [roleMixin],
  props: {
    value: { type: Boolean, default: true }
  },
  computed: {
    ...mapGetters('laboratory', ['panicCount', 'citoCount', 'needsReviewCount', 'lockoutCount']),
    ...mapState('laboratory', ['lastRefreshAt', 'summary']),
    /** Пункти, недоступні ролі поточного співробітника, ховаються (таблиця — utils/menuAccess.js) */
    visibleGroups () {
      const role = this.currentLabRole;
      return this.groups
        .map(g => ({ ...g, items: g.items.filter(i => routeAllowed(i.to && i.to.name, role)) }))
        .filter(g => g.items.length);
    },
    groups () {
      return [
        {
          title: 'Процеси',
          items: [
            { name: 'dashboard', label: 'Дашборд', icon: 'fas fa-tachometer-alt', to: { name: 'lab-dashboard' } },
            { name: 'orders', label: 'Реєстрація направлень', icon: 'fas fa-file-medical', to: { name: 'lab-orders' }, badgeKey: 'cito' },
            { name: 'referrals', label: 'Журнал направлень', icon: 'fas fa-clipboard-list', to: { name: 'lab-referrals' } },
            { name: 'refs', label: 'Вхідні направлення', icon: 'fas fa-inbox', to: { name: 'lab-incoming-referrals' }, badgeKey: 'distribution' },
            { name: 'work', label: 'Робота за направленнями', icon: 'fas fa-tasks', to: { name: 'lab-work' } },
            { name: 'matrix', label: 'Матриця призначень', icon: 'fas fa-th', to: { name: 'lab-order-matrix' } },
            { name: 'phlebotomy', label: 'Пункт забору', icon: 'fas fa-syringe', to: { name: 'lab-phlebotomy' } },
            { name: 'journal', label: 'Журнал відділення', icon: 'fas fa-book', to: { name: 'lab-section-journal' } },
            { name: 'logistics', label: 'Логістика', icon: 'fas fa-truck', to: { name: 'lab-logistics' } },
            { name: 'clients', label: 'Клієнти (клініки)', icon: 'fas fa-handshake', to: { name: 'lab-clients' } },
            { name: 'send-out', label: 'Зовнішні лабораторії', icon: 'fas fa-share-square', to: { name: 'lab-send-out' } },
            { name: 'workstation', label: 'Робочий стіл лаборанта', icon: 'fas fa-microscope', to: { name: 'lab-workstation' }, badgeKey: 'pending' },
            { name: 'validation', label: 'Валідація та критичні значення', icon: 'fas fa-user-check', to: { name: 'lab-validation' }, badgeKey: 'panic' },
            { name: 'qc', label: 'Контроль якості', icon: 'fas fa-chart-line', to: { name: 'lab-qc' }, badgeKey: 'lockout' },
            { name: 'biobank', label: 'Біобанк', icon: 'fas fa-snowflake', to: { name: 'lab-biobank' } },
            { name: 'reagents', label: 'Реагенти', icon: 'fas fa-boxes', to: { name: 'lab-reagents' } },
            { name: 'microbiology', label: 'Мікробіологія', icon: 'fas fa-bacterium', to: { name: 'lab-microbiology' } },
            { name: 'tat', label: 'Аналітика термінів виконання', icon: 'fas fa-chart-pie', to: { name: 'lab-analytics-tat' } },
            { name: 'reports', label: 'Звіти лабораторії', icon: 'fas fa-file-alt', to: { name: 'lab-reports' } },
            { name: 'ehealth', label: 'Відправка в ЕСОЗ', icon: 'fas fa-cloud-upload-alt', to: { name: 'lab-ehealth' } }
          ]
        },
        {
          title: 'Патоморфологія',
          items: [
            { name: 'path-cases', label: 'Журнал справ', icon: 'fas fa-folder-open', to: { name: 'lab-path-cases' }, exact: true, badgeKey: 'pathOverdue' },
            { name: 'path-intake', label: 'Прийом матеріалу', icon: 'fas fa-box-open', to: { name: 'lab-path-intake' }, badgeKey: 'pathIncoming' },
            { name: 'path-cito', label: 'Термінові інтраопераційні (CITO)', icon: 'fas fa-stopwatch', to: { name: 'lab-path-cito' }, badgeKey: 'pathCito' },
            { name: 'path-archive', label: 'Архів блоків і скелець', icon: 'fas fa-archive', to: { name: 'lab-path-archive' } }
          ]
        },
        {
          title: 'Цитологія',
          items: [
            { name: 'cyto-cases', label: 'Журнал цитології', icon: 'fas fa-microscope', to: { name: 'lab-cyto-cases' }, exact: true },
            { name: 'cyto-intake', label: 'Прийом цитології', icon: 'fas fa-box-open', to: { name: 'lab-path-intake', query: { kind: 'cyto' } } }
          ]
        },
        {
          title: 'Довідники',
          items: [
            { name: 'pricing', label: 'Прайси та платники', icon: 'fas fa-file-invoice-dollar', to: { name: 'lab-pricing' } },
            { name: 'report-templates', label: 'Шаблони бланків', icon: 'fas fa-file-alt', to: { name: 'lab-report-templates' } },
            { name: 'norms', label: 'Послуги та норми', icon: 'fas fa-sliders-h', to: { name: 'lab-norms' } },
            { name: 'sections', label: 'Підрозділи лабораторії', icon: 'fas fa-sitemap', to: { name: 'lab-sections' }, exact: true },
            { name: 'd-tests', label: 'Показники', icon: 'fas fa-list-ol', to: { name: 'lab-dictionary', params: { name: 'tests' } } },
            { name: 'd-bio', label: 'Біоматеріали', icon: 'fas fa-tint', to: { name: 'lab-dictionary', params: { name: 'biomaterials' } } },
            { name: 'd-tubes', label: 'Пробірки', icon: 'fas fa-vial', to: { name: 'lab-dictionary', params: { name: 'tube-types' } } },
            { name: 'd-methods', label: 'Методики', icon: 'fas fa-flask', to: { name: 'lab-dictionary', params: { name: 'method-types' } } },
            { name: 'd-atypes', label: 'Моделі аналізаторів', icon: 'fas fa-server', to: { name: 'lab-dictionary', params: { name: 'analyzer-types' } } },
            { name: 'd-reflex', label: 'Правила повторного тесту', icon: 'fas fa-random', to: { name: 'lab-dictionary', params: { name: 'reflex-rules' } } },
            { name: 'path-dict', label: 'Довідники патоморфології', icon: 'fas fa-book-medical', to: { name: 'lab-path-dictionaries' } },
            { name: 'd-micro', label: 'Мікроорганізми та антибіотики', icon: 'fas fa-disease', to: { name: 'lab-dictionary', params: { name: 'organisms' } } }
          ]
        },
        {
          title: 'Інтеграції',
          items: [
            { name: 'analyzers', label: 'Аналізатори та коннектори', icon: 'fas fa-network-wired', to: { name: 'lab-analyzers' }, exact: true },
            { name: 'analyzer-services', label: 'Послуги ЕСОЗ та аналізатори', icon: 'fas fa-project-diagram', to: { name: 'lab-analyzer-services' } },
            { name: 'messages', label: 'Журнал обміну з аналізаторами', icon: 'fas fa-exchange-alt', to: { name: 'lab-analyzer-messages' } },
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
          return this.panicCount > 0 ? { label: String(this.panicCount), color: 'negative', hint: 'Критичні результати без зареєстрованого дзвінка' } : null;
        case 'cito':
          return this.citoCount > 0 ? { label: String(this.citoCount), color: 'deep-orange-6', hint: 'Термінові замовлення (CITO)' } : null;
        case 'pending': {
          const n = Number(this.summary && this.summary.pending) || 0;
          return n > 0 ? { label: String(n), color: 'teal-6' } : null;
        }
        case 'distribution': {
          const n = Number(this.summary && this.summary.distributionPending) || 0;
          return n > 0 ? { label: String(n), color: 'orange-7' } : null;
        }
        case 'pathIncoming':
        case 'pathCito':
        case 'pathOverdue': {
          const key = { pathIncoming: 'pathologyIncoming', pathCito: 'pathologyCito', pathOverdue: 'pathologyOverdue' }[item.badgeKey];
          const n = Number(this.summary && this.summary[key]) || 0;
          const color = { pathIncoming: 'orange-7', pathCito: 'negative', pathOverdue: 'red-5' }[item.badgeKey];
          const hint = { pathIncoming: 'Направлень у черзі прийому', pathCito: 'Термінових справ у роботі', pathOverdue: 'Прострочених справ' }[item.badgeKey];
          return n > 0 ? { label: String(n), color, hint } : null;
        }
        case 'lockout':
          return this.lockoutCount > 0 ? { label: String(this.lockoutCount), color: 'purple-6', hint: 'Активні блокування за ВКЯ' } : null;
        default:
          return null;
      }
    }
  }
};
</script>
