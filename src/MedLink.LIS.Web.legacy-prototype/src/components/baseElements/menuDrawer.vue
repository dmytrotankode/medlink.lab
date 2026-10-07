<!--
  MedLink Menu Drawer Component with Laboratory Submenu Extension
  Matches evomis/src/App.View/src/components/baseElements/menuDrawer.vue
  Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
-->
<template>
  <q-drawer
    data-testid="labMenuDrawer"
    :value="drawer"
    :width="230"
    show-if-above
    :mini="miniState"
    :breakpoint="980"
    persistent
    content-class="no-scroll text-grey-1 shadow-2 nav-drawer bg-grey-9"
  >
    <div class="drawer-header q-pa-sm text-center bg-grey-10">
      <div v-if="!miniState" class="row items-center no-wrap q-gutter-x-sm">
        <q-icon name="fas fa-flask" color="cyan-4" size="22px" />
        <div class="text-subtitle2 text-weight-bold text-white text-left ellipsis">
          МедЛінк ЛІС 3.0
          <div class="text-caption text-grey-5" style="font-size: 10px;">Клінічна Лабораторія</div>
        </div>
      </div>
      <q-icon v-else name="fas fa-flask" color="cyan-4" size="24px" />
    </div>

    <q-scroll-area class="nav_scroll-area">
      <q-list dense padding>
        <!-- Standard MedLink Items -->
        <q-item clickable v-ripple :to="{ path: '/dashboard' }">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon class="icon ico-home text-white" size="20px" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label class="text-white">Головна</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Головна</q-tooltip>
        </q-item>

        <q-separator class="q-my-xs bg-grey-8" />

        <!-- HEADER: ЛАБОРАТОРІЯ -->
        <q-item-label header class="text-uppercase text-cyan-3 text-weight-bold" style="font-size: 11px; letter-spacing: 0.5px;">
          <span v-if="!miniState">Лабораторія (ЛІС)</span>
          <q-icon v-else name="science" size="18px" />
        </q-item-label>

        <!-- 1. Робочий стіл лаборанта -->
        <q-item clickable v-ripple :to="{ name: 'lab-workstation' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-microscope" size="18px" color="cyan-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Робочий стіл</q-item-label>
          </q-item-section>
          <q-badge v-if="!miniState" color="teal-6" text-color="white" label="15" />
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Робочий стіл лаборанта</q-tooltip>
        </q-item>

        <!-- 2. Валідація та Паніка -->
        <q-item clickable v-ripple :to="{ name: 'lab-validation' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-user-check" size="18px" color="amber-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Валідація & Паніка</q-item-label>
          </q-item-section>
          <q-badge v-if="!miniState" color="negative" text-color="white" label="1 CITO" />
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Валідація & Панічні алерти</q-tooltip>
        </q-item>

        <!-- 3. Внутрішній контроль якості (ВКЯ) -->
        <q-item clickable v-ripple :to="{ name: 'lab-qc' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-chart-line" size="18px" color="purple-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Контроль якості</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">ВКЯ (Вестгард / Леві-Дженнінгс)</q-tooltip>
        </q-item>

        <!-- 4. Бактеріологія -->
        <q-item clickable v-ripple :to="{ name: 'lab-microbiology' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-bacterium" size="18px" color="green-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Мікробіологія</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Бактеріологія & Антибіотикограма</q-tooltip>
        </q-item>

        <!-- 5. Біобанк -->
        <q-item clickable v-ripple :to="{ name: 'lab-biobank' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-snowflake" size="18px" color="light-blue-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Біобанк & Архів</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Біобанк та архів зразків</q-tooltip>
        </q-item>

        <!-- 6. Склад реактивів -->
        <q-item clickable v-ripple :to="{ name: 'lab-reagents' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-boxes" size="18px" color="orange-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Склад реактивів</q-item-label>
          </q-item-section>
          <q-badge v-if="!miniState" color="warning" text-color="dark" label="2 увага" />
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Склад реактивів та калібраторів</q-tooltip>
        </q-item>

        <!-- 7. Шлюз аналізаторів -->
        <q-item clickable v-ripple :to="{ name: 'lab-analyzer-monitor' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-network-wired" size="18px" color="teal-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Шлюз приладів</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Монітор підключення аналізаторів (ASTM/HL7)</q-tooltip>
        </q-item>

        <!-- 8. Аналітика & TAT -->
        <q-item clickable v-ripple :to="{ name: 'lab-analytics-tat' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-chart-pie" size="18px" color="indigo-3" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Аналітика & TAT</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Операційна аналітика та TAT</q-tooltip>
        </q-item>

        <!-- HEADER: ПУНКТИ ЗАБОРУ ТА ЛОГІСТИКА -->
        <q-separator class="q-my-xs bg-grey-8" />
        <q-item-label header class="text-uppercase text-cyan-3 text-weight-bold" style="font-size: 11px; letter-spacing: 0.5px;">
          <span v-if="!miniState">Пункти забору & Логістика</span>
          <q-icon v-else name="local_shipping" size="18px" />
        </q-item-label>

        <q-item clickable v-ripple :to="{ name: 'lab-phlebotomy' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-syringe" size="18px" color="red-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Пункт забору</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Маніпуляційний кабінет / Штрихкоди</q-tooltip>
        </q-item>

        <q-item clickable v-ripple :to="{ name: 'lab-logistics' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-truck" size="18px" color="blue-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Логістика & Кур'єри</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Термоконтейнери та маршрутні листи</q-tooltip>
        </q-item>

        <!-- HEADER: КАБІНЕТ ПАЦІЄНТА -->
        <q-separator class="q-my-xs bg-grey-8" />
        <q-item-label header class="text-uppercase text-cyan-3 text-weight-bold" style="font-size: 11px; letter-spacing: 0.5px;">
          <span v-if="!miniState">Кабінет пацієнта</span>
          <q-icon v-else name="person" size="18px" />
        </q-item-label>

        <q-item clickable v-ripple :to="{ name: 'lab-patient-portal' }" active-class="bg-cyan-9 text-white">
          <q-item-section avatar :style="!miniState ? 'min-width: 36px' : 'min-width: initial'">
            <q-icon name="fas fa-user-circle" size="18px" color="teal-4" />
          </q-item-section>
          <q-item-section v-if="!miniState">
            <q-item-label>Мої результати (Портал)</q-item-label>
          </q-item-section>
          <q-tooltip v-if="miniState" anchor="center right" self="center left">Кабінет пацієнта: статус і PDF бланки</q-tooltip>
        </q-item>

        <!-- HEADER: ДОВІДНИКИ ЛАБОРАТОРІЇ -->
        <q-separator class="q-my-xs bg-grey-8" />
        <q-expansion-item
          group="lab-dict-group"
          icon="fas fa-book-medical"
          label="Довідники ЛІС"
          header-class="text-cyan-2"
          expand-icon-class="text-grey-4"
        >
          <q-list class="q-pl-md">
            <q-item clickable v-ripple :to="{ name: 'lab-norms' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-sliders-h" size="14px" />
              </q-item-section>
              <q-item-section>Норми і методики</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'dict-biomaterials' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-vial" size="14px" />
              </q-item-section>
              <q-item-section>Біоматеріали</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'dict-tube-types' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-flask" size="14px" />
              </q-item-section>
              <q-item-section>Типи пробірок</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'dict-analyzer-types' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-server" size="14px" />
              </q-item-section>
              <q-item-section>Аналізатори</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'dict-lab-parameters' }" active-class="bg-grey-8 text-cyan-3">
              <q-item-section avatar style="min-width: 28px;">
                <q-icon name="fas fa-list-ol" size="14px" />
              </q-item-section>
              <q-item-section>Показники та профілі</q-item-section>
            </q-item>
          </q-list>
        </q-expansion-item>
      </q-list>
    </q-scroll-area>
  </q-drawer>
</template>

<script>
export default {
  name: 'MenuDrawerLaboratory',
  props: {
    drawer: {
      type: Boolean,
      default: true
    },
    miniState: {
      type: Boolean,
      default: false
    }
  }
};
</script>

<style scoped>
.nav-drawer {
  background: #333333 !important;
}
.drawer-header {
  border-bottom: 2px solid #0178BC;
}
.nav_scroll-area {
  height: calc(100% - 50px);
}
</style>
