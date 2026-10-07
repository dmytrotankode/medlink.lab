# -*- coding: utf-8 -*-
"""
Generator for MedLink LIS Runnable Quasar Frontend Prototype.
Outputs run_prototype.html and index.html in C:\__MEDLINK___\LABA\medlink_lab_frontend\
Ensures clean syntax, no unescaped apostrophes, and robust Vue/Quasar mounting.
Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
"""

import os

RUN_HTML_PATH = r"C:\__MEDLINK___\LABA\medlink_lab_frontend\run_prototype.html"
INDEX_HTML_PATH = r"C:\__MEDLINK___\LABA\medlink_lab_frontend\index.html"

prototype_html = """<!DOCTYPE html>
<html lang="uk">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>MedLink LIS 3.0 - Прототип Лабораторної Інформаційної Системи</title>
  
  <!-- Quasar v1.15.3 & Vue 2 CDN (Matching MedLink evomis) -->
  <link href="https://fonts.googleapis.com/css?family=Roboto:100,300,400,500,700,900|Material+Icons|Material+Icons+Outlined" rel="stylesheet" type="text/css">
  <link rel="stylesheet" href="https://use.fontawesome.com/releases/v5.15.4/css/all.css">
  <link href="https://cdn.jsdelivr.net/npm/quasar@1.15.3/dist/quasar.min.css" rel="stylesheet" type="text/css">
  
  <style>
    [v-cloak] { display: none !important; }
    :root {
      --q-color-primary: #4274A7;
      --q-color-secondary: #318F94;
      --q-color-accent: #0178BC;
      --q-color-positive: #23B947;
      --q-color-negative: #d04f45;
      --q-color-info: #0284c7;
      --q-color-warning: #f59e0b;
      --q-color-dark: #2d3238;
    }
    body {
      font-family: 'Roboto', -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      background-color: #f5f6f8;
      color: #333333;
      margin: 0;
      padding: 0;
    }
    #main-header {
      background-color: #f1f3f5;
      border-bottom: 1px solid #d9d9d9;
      color: #2c3e50;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
    }
    .brand-gradient-line {
      height: 3px;
      width: 100%;
      background: linear-gradient(90deg, #0178BC 0%, #318F94 51%, #5EC58C 100%);
    }
    .nav-drawer {
      background-color: #2d3238 !important;
      color: #e0e0e0;
    }
    .drawer-header {
      border-bottom: 2px solid #0178BC;
      background-color: #212529;
    }
    .nav-active-item {
      background: linear-gradient(90deg, rgba(1, 120, 188, 0.4) 0%, rgba(49, 143, 148, 0.2) 100%) !important;
      color: #ffffff !important;
      border-left: 4px solid #5EC58C;
      font-weight: 500;
    }
    .nav-active-item .q-icon {
      color: #5EC58C !important;
    }
    .medlink-card {
      border-radius: 6px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      background: #ffffff;
    }
    .grid-cell {
      width: 26px;
      height: 26px;
      border: 1px solid #cbd5e1;
      font-size: 10px;
      cursor: pointer;
      border-radius: 3px;
      transition: all 0.15s;
    }
    .grid-cell:hover {
      transform: scale(1.15);
      z-index: 2;
      box-shadow: 0 2px 5px rgba(0,0,0,0.2);
    }
    .dot {
      display: inline-block;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      margin-right: 4px;
    }
  </style>
</head>
<body>
  <div id="q-app" v-cloak>
    <q-layout view="lHh Lpr lff">
      <!-- Top Application Header -->
      <q-header id="main-header">
        <div class="brand-gradient-line"></div>
        <q-toolbar class="q-px-md" style="min-height: 52px;">
          <q-btn flat dense round icon="menu" @click="leftDrawerOpen = !leftDrawerOpen" aria-label="Меню" class="q-mr-sm text-grey-8" />
          
          <!-- Logo & System Title -->
          <div class="row items-center no-wrap">
            <q-avatar square size="32px" class="q-mr-sm">
              <svg viewBox="0 0 40 40" width="32" height="32">
                <rect width="40" height="40" rx="8" fill="#0178BC"/>
                <path d="M12 20 L28 20 M20 12 L20 28" stroke="white" stroke-width="4" stroke-linecap="round"/>
                <circle cx="28" cy="12" r="3" fill="#5EC58C"/>
              </svg>
            </q-avatar>
            <div>
              <div class="text-weight-bold text-subtitle1 text-primary lh-tight">
                MedLink <span class="text-secondary">LIS 3.0</span>
              </div>
              <div class="text-caption text-grey-7" style="font-size: 11px;">Клініко-діагностична лабораторія</div>
            </div>
          </div>

          <q-space></q-space>

          <!-- Status Indicators -->
          <div class="row items-center q-gutter-x-sm gt-xs">
            <q-chip dense outline color="positive" icon="wifi" class="text-weight-medium">
              Шлюз аналізаторів: Онлайн (3/4)
            </q-chip>
            <q-chip dense outline color="negative" icon="priority_high" class="text-weight-bold cursor-pointer" @click="setView('validation', 'Валідація результатів & Паніка')">
              Паніка: 1 CITO
            </q-chip>
            <q-chip dense outline color="purple" icon="analytics" class="cursor-pointer" @click="setView('qc', 'Контроль якості (ВКЯ)')">
              ВКЯ: Sysmex Lockout
            </q-chip>
          </div>

          <q-separator vertical inset class="q-mx-md" />

          <!-- Mock User Profile (Bypass Auth) -->
          <div class="row items-center q-gutter-x-sm cursor-pointer">
            <q-avatar size="34px" color="primary" text-color="white" icon="fas fa-user-md" />
            <div class="gt-sm text-left">
              <div class="text-caption text-weight-bold text-dark">Д-р Мельник В.С.</div>
              <div class="text-caption text-grey-7" style="font-size: 10px;">Завідувач КДЛ (Лікар-лаборант)</div>
            </div>
            <q-btn flat round dense icon="arrow_drop_down" size="sm" class="text-grey-7" />
          </div>
        </q-toolbar>

        <!-- Breadcrumbs Toolbar -->
        <div class="bg-grey-2 q-px-md q-py-xs row items-center justify-between border-bottom" style="font-size: 13px;">
          <q-breadcrumbs active-color="primary" separator=">">
            <q-breadcrumbs-el label="Головна" icon="home" />
            <q-breadcrumbs-el label="Лабораторія" />
            <q-breadcrumbs-el :label="currentViewTitle" class="text-weight-bold" />
          </q-breadcrumbs>
          <div class="text-caption text-grey-6 gt-xs">
            ТОВ "МедЛінк" &copy; 2026 | Прототип без авторизації (Режим розробника)
          </div>
        </div>
      </q-header>

      <!-- Sidebar Navigation Drawer -->
      <q-drawer
        v-model="leftDrawerOpen"
        show-if-above
        :width="250"
        behavior="desktop"
        content-class="nav-drawer"
      >
        <div class="drawer-header q-pa-sm text-center">
          <div class="row items-center no-wrap q-gutter-x-sm">
            <q-icon name="fas fa-vial" color="cyan-3" size="20px" />
            <div class="text-subtitle2 text-weight-bold text-white text-left ellipsis">
              Модулі Лабораторії
              <div class="text-caption text-grey-4" style="font-size: 10px;">Навігація процесів ЛІС</div>
            </div>
          </div>
        </div>

        <q-scroll-area style="height: calc(100% - 50px);">
          <q-list dense padding>
            <!-- SECTION 1: АНАЛІТИЧНИЙ БЛОК -->
            <q-item-label header class="text-uppercase text-cyan-3 text-weight-bold q-pt-sm q-pb-xs" style="font-size: 11px;">
              Лабораторія (Аналітика)
            </q-item-label>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'workstation' }" @click="setView('workstation', 'Робочий стіл лаборанта')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-microscope" size="16px" color="cyan-2" /></q-item-section>
              <q-item-section>Робочий стіл лаборанта</q-item-section>
              <q-badge color="teal" label="15" />
            </q-item>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'validation' }" @click="setView('validation', 'Валідація результатів & Паніка')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-user-check" size="16px" color="amber-3" /></q-item-section>
              <q-item-section>Валідація & Паніка</q-item-section>
              <q-badge color="negative" label="1 CITO" />
            </q-item>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'qc' }" @click="setView('qc', 'Контроль якості (ВКЯ)')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-chart-line" size="16px" color="purple-3" /></q-item-section>
              <q-item-section>Контроль якості (ВКЯ)</q-item-section>
              <q-badge color="purple" label="Lock" />
            </q-item>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'microbiology' }" @click="setView('microbiology', 'Бактеріологія & EUCAST')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-bacterium" size="16px" color="green-3" /></q-item-section>
              <q-item-section>Мікробіологія</q-item-section>
            </q-item>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'biobank' }" @click="setView('biobank', 'Біобанк та архів зразків')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-snowflake" size="16px" color="light-blue-3" /></q-item-section>
              <q-item-section>Біобанк & Архів</q-item-section>
            </q-item>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'reagents' }" @click="setView('reagents', 'Склад реактивів та калібраторів')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-boxes" size="16px" color="orange-3" /></q-item-section>
              <q-item-section>Склад реактивів</q-item-section>
              <q-badge color="warning" text-color="dark" label="2!" />
            </q-item>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'analyzers' }" @click="setView('analyzers', 'Монітор шлюзу аналізаторів')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-network-wired" size="16px" color="teal-3" /></q-item-section>
              <q-item-section>Шлюз приладів</q-item-section>
            </q-item>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'tat' }" @click="setView('tat', 'Операційна аналітика та TAT')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-chart-pie" size="16px" color="indigo-3" /></q-item-section>
              <q-item-section>Аналітика & TAT</q-item-section>
            </q-item>

            <q-separator class="q-my-xs bg-grey-8" />

            <!-- SECTION 2: ПУНКТИ ЗАБОРУ & ЛОГІСТИКА -->
            <q-item-label header class="text-uppercase text-cyan-3 text-weight-bold q-pt-sm q-pb-xs" style="font-size: 11px;">
              Пункти забору & Логістика
            </q-item-label>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'phlebotomy' }" @click="setView('phlebotomy', 'Пункт забору біоматеріалу')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-syringe" size="16px" color="red-3" /></q-item-section>
              <q-item-section>Пункт забору (Забір)</q-item-section>
            </q-item>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'logistics' }" @click="setView('logistics', 'Логістика та термоконтроль')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-truck" size="16px" color="blue-3" /></q-item-section>
              <q-item-section>Логістика & Кур’єри</q-item-section>
            </q-item>

            <q-separator class="q-my-xs bg-grey-8" />

            <!-- SECTION 3: КАБІНЕТ ПАЦІЄНТА -->
            <q-item-label header class="text-uppercase text-cyan-3 text-weight-bold q-pt-sm q-pb-xs" style="font-size: 11px;">
              Кабінет пацієнта
            </q-item-label>

            <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'patient' }" @click="setView('patient', 'Кабінет пацієнта (Результати)')">
              <q-item-section avatar style="min-width: 32px;"><q-icon name="fas fa-user-circle" size="16px" color="teal-3" /></q-item-section>
              <q-item-section>Мої результати (Портал)</q-item-section>
            </q-item>

            <q-separator class="q-my-xs bg-grey-8" />

            <!-- SECTION 4: ДОВІДНИКИ ЛАБОРАТОРІЇ -->
            <q-expansion-item
              icon="fas fa-book-medical"
              label="Довідники ЛІС"
              header-class="text-cyan-2"
              default-opened
            >
              <q-list class="q-pl-sm">
                <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'norms' }" @click="setView('norms', 'Норми, методики та референси')">
                  <q-item-section avatar style="min-width: 28px;"><q-icon name="fas fa-sliders-h" size="14px" /></q-item-section>
                  <q-item-section>Норми і методики</q-item-section>
                </q-item>
                <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'dict_biomaterials' }" @click="setView('dict_biomaterials', 'Довідник біоматеріалів')">
                  <q-item-section avatar style="min-width: 28px;"><q-icon name="fas fa-vial" size="14px" /></q-item-section>
                  <q-item-section>Біоматеріали</q-item-section>
                </q-item>
                <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'dict_tubes' }" @click="setView('dict_tubes', 'Довідник пробірок та контейнерів')">
                  <q-item-section avatar style="min-width: 28px;"><q-icon name="fas fa-flask" size="14px" /></q-item-section>
                  <q-item-section>Типи пробірок</q-item-section>
                </q-item>
                <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'dict_analyzers' }" @click="setView('dict_analyzers', 'Довідник типів аналізаторів')">
                  <q-item-section avatar style="min-width: 28px;"><q-icon name="fas fa-server" size="14px" /></q-item-section>
                  <q-item-section>Моделі аналізаторів</q-item-section>
                </q-item>
                <q-item clickable v-ripple :class="{ 'nav-active-item': currentView === 'dict_parameters' }" @click="setView('dict_parameters', 'Довідник лабораторних показників')">
                  <q-item-section avatar style="min-width: 28px;"><q-icon name="fas fa-list-ol" size="14px" /></q-item-section>
                  <q-item-section>Показники та профілі</q-item-section>
                </q-item>
              </q-list>
            </q-expansion-item>
          </q-list>
        </q-scroll-area>
      </q-drawer>

      <!-- Main Application Container -->
      <q-page-container>
        <q-page class="q-pa-md">

          <!-- VIEW: WORKSTATION -->
          <div v-show="currentView === 'workstation'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-microscope" class="q-mr-sm" />
                  Робочий стіл лаборанта (Журнал досліджень)
                </h5>
                <div class="text-caption text-grey-7">Аналітичний етап: автоматичний прийом з аналізаторів, перевірка референсів, Delta-check.</div>
              </div>
              <div class="row q-gutter-sm">
                <q-btn color="secondary" icon="refresh" label="Оновити" dense flat @click="notify('Дані оновлено')" />
                <q-btn color="teal" icon="check_circle" label="Автовалідація норм" @click="runAutoValidation" />
              </div>
            </div>

            <q-card flat bordered class="q-mb-md bg-white">
              <q-card-section class="q-pa-sm row q-col-gutter-sm items-center">
                <div class="col-12 col-md-4">
                  <q-input v-model="worklistSearch" outlined dense placeholder="Пошук за штрихкодом, пацієнтом або тестом..." bg-color="white">
                    <template v-slot:append><q-icon name="search" /></template>
                  </q-input>
                </div>
                <div class="col-6 col-md-3">
                  <q-select v-model="worklistAnalyzerFilter" :options="['Всі прилади', 'Sysmex XN-1000', 'Roche Cobas e411', 'Mindray BS-240']" outlined dense bg-color="white" />
                </div>
                <div class="col-6 col-md-3">
                  <q-select v-model="worklistFlagFilter" :options="['Всі результати', 'Тільки паніка та відхилення', 'Нормальні']" outlined dense bg-color="white" />
                </div>
                <div class="col-12 col-md-2 text-right">
                  <q-chip outline color="negative" icon="warning">Паніка: 1</q-chip>
                </div>
              </q-card-section>
            </q-card>

            <q-card flat bordered class="medlink-card">
              <q-table :data="filteredWorklist" :columns="worklistColumns" row-key="id" dense flat :pagination.sync="defaultPagination">
                <template v-slot:body-cell-value="props">
                  <q-td :props="props" :class="getValueClass(props.row.flag)">
                    {{ props.row.value }} {{ props.row.unit }}
                  </q-td>
                </template>
                <template v-slot:body-cell-norm="props">
                  <q-td :props="props">{{ props.row.normMin }} - {{ props.row.normMax }}</q-td>
                </template>
                <template v-slot:body-cell-flag="props">
                  <q-td :props="props">
                    <q-badge :color="getFlagColor(props.row.flag)">{{ formatFlag(props.row.flag) }}</q-badge>
                  </q-td>
                </template>
                <template v-slot:body-cell-deltaPercent="props">
                  <q-td :props="props" :class="props.row.deltaPercent.includes('+185') ? 'text-negative text-weight-bold' : ''">
                    {{ props.row.deltaPercent }}
                  </q-td>
                </template>
                <template v-slot:body-cell-status="props">
                  <q-td :props="props">
                    <q-badge :color="props.row.status === 'AUTO_VERIFIED' ? 'positive' : 'warning'" :text-color="props.row.status === 'AUTO_VERIFIED' ? 'white' : 'dark'">
                      {{ props.row.status === 'AUTO_VERIFIED' ? 'Авто-валідовано' : 'Очікує лікаря' }}
                    </q-badge>
                  </q-td>
                </template>
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props" class="q-gutter-xs">
                    <q-btn size="sm" color="primary" dense icon="edit" @click="openEditResult(props.row)"><q-tooltip>Коригувати</q-tooltip></q-btn>
                    <q-btn size="sm" color="secondary" dense icon="replay" @click="notify('Запит на перезапуск відправлено в чергу')"><q-tooltip>Перезапуск</q-tooltip></q-btn>
                  </q-td>
                </template>
              </q-table>
            </q-card>
          </div>

          <!-- VIEW: VALIDATION & PANIC -->
          <div v-show="currentView === 'validation'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-negative">
                  <q-icon name="fas fa-exclamation-triangle" class="q-mr-sm" />
                  Валідація результатів & Панічні алерти (Пост-аналітика)
                </h5>
                <div class="text-caption text-grey-7">Дворівнева валідація лікарем, контроль критичних значень, дзвінки у відділення стаціонару.</div>
              </div>
              <div>
                <q-btn color="negative" icon="call" label="Журнал викликів ВРІТ" @click="showCallDialog = true" />
              </div>
            </div>

            <q-banner dense rounded class="bg-red-2 text-negative q-mb-md" inline-actions>
              <template v-slot:avatar><q-icon name="warning" color="negative" size="32px" /></template>
              <div class="text-subtitle1 text-weight-bold">Критичне панічне значення потребує негайної реєстрації!</div>
              <div class="text-body2">
                Пацієнт: <strong>Мельник Ю.В. (48 р.)</strong> | Відділення: <strong>ВРІТ</strong> | Глюкоза: <strong>26.4 ммоль/л</strong> (Норма: 4.1 - 5.9). Ризик гіперосмолярної коми!
              </div>
              <template v-slot:action>
                <q-btn color="negative" label="Зареєструвати телефонний дзвінок" @click="showCallDialog = true" />
              </template>
            </q-banner>

            <q-card flat bordered class="medlink-card">
              <q-table :data="pendingValidationList" :columns="validationColumns" row-key="id" dense flat>
                <template v-slot:body-cell-value="props">
                  <q-td :props="props" class="text-weight-bold text-negative">{{ props.row.value }} {{ props.row.unit }}</q-td>
                </template>
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props" class="q-gutter-xs">
                    <q-btn size="sm" color="positive" dense icon="check" label="Валідувати" @click="validateRow(props.row)" />
                    <q-btn size="sm" color="warning" text-color="dark" dense icon="replay" label="Повтор" @click="notify('Направлено на перевірку')" />
                  </q-td>
                </template>
              </q-table>
            </q-card>
          </div>

          <!-- VIEW: QUALITY CONTROL (QC) -->
          <div v-show="currentView === 'qc'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-chart-line" class="q-mr-sm" />
                  Внутрішній контроль якості (ВКЯ) - Вестгард & Леві-Дженнінгс
                </h5>
                <div class="text-caption text-grey-7">Оцінка відтворюваності та правильності, правила 1-3s, 2-2s, R-4s, автоматичне блокування аналізатора.</div>
              </div>
              <div>
                <q-btn color="negative" icon="lock_open" label="Скинути блокування аналізатора" dense flat @click="showQcUnlockDialog = true" />
              </div>
            </div>

            <q-banner dense rounded class="bg-red-1 text-negative q-mb-md" inline-actions>
              <template v-slot:avatar><q-icon name="lock" color="negative" size="30px" /></template>
              <div class="text-subtitle2 text-weight-bold">
                УВАГА: Аналізатор {{ qcData.analyzer }} заблоковано через порушення правила Вестгарда 1-3s!
              </div>
              <div class="text-caption">
                Матеріал: <strong>{{ qcData.controlMaterial }}</strong> (Лот: {{ qcData.lotNumber }}). Виміряно: <strong>8.28 10*9/л</strong> (Ціль: {{ qcData.targetMean }} &plusmn; 3SD = 8.10).
              </div>
            </q-banner>

            <div class="row q-col-gutter-md q-mb-md">
              <div class="col-12 col-md-3"><q-card flat bordered class="q-pa-sm text-center"><div class="text-caption text-grey-7">Параметр</div><div class="text-subtitle1 text-weight-bold text-primary">{{ qcData.parameter }}</div></q-card></div>
              <div class="col-12 col-md-3"><q-card flat bordered class="q-pa-sm text-center"><div class="text-caption text-grey-7">Цільове середнє (Mean)</div><div class="text-subtitle1 text-weight-bold">{{ qcData.targetMean }}</div></q-card></div>
              <div class="col-12 col-md-3"><q-card flat bordered class="q-pa-sm text-center"><div class="text-caption text-grey-7">Стандартне відхилення (SD)</div><div class="text-subtitle1 text-weight-bold">&plusmn;{{ qcData.targetSd }}</div></q-card></div>
              <div class="col-12 col-md-3"><q-card flat bordered class="q-pa-sm text-center"><div class="text-caption text-grey-7">Коефіцієнт варіації (CV%)</div><div class="text-subtitle1 text-weight-bold text-teal">{{ qcData.cvPercent }}%</div></q-card></div>
            </div>

            <q-card flat bordered class="medlink-card">
              <q-table :data="qcData.dataPoints" :columns="qcColumns" row-key="day" dense flat>
                <template v-slot:body-cell-status="props">
                  <q-td :props="props">
                    <q-badge :color="props.row.status === 'OK' ? 'positive' : (props.row.status.includes('WARN') ? 'warning' : 'negative')">
                      {{ props.row.status }}
                    </q-badge>
                  </q-td>
                </template>
              </q-table>
            </q-card>
          </div>

          <!-- VIEW: PHLEBOTOMY -->
          <div v-show="currentView === 'phlebotomy'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-syringe" class="q-mr-sm" />
                  Пункт забору біоматеріалу (Маніпуляційний кабінет)
                </h5>
                <div class="text-caption text-grey-7">Преаналітичний етап: формування замовлення, Order of Draw, валідація умов забору та друк ZPL штрихкодів.</div>
              </div>
              <div class="row q-gutter-sm">
                <q-btn color="primary" icon="add" label="Нове замовлення" @click="notify('Створення замовлення')" />
              </div>
            </div>

            <q-card flat bordered class="medlink-card">
              <q-table :data="ordersList" :columns="orderColumns" row-key="id" dense flat>
                <template v-slot:body-cell-priority="props">
                  <q-td :props="props"><q-badge :color="props.row.priority === 'CITO' ? 'negative' : 'grey-7'">{{ props.row.priority }}</q-badge></q-td>
                </template>
                <template v-slot:body-cell-status="props">
                  <q-td :props="props"><q-badge :color="getOrderStatusColor(props.row.status)">{{ formatOrderStatus(props.row.status) }}</q-badge></q-td>
                </template>
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props" class="q-gutter-xs">
                    <q-btn size="sm" color="primary" dense icon="fas fa-barcode" label="Друк ZPL" @click="openBarcodeDialog(props.row)" />
                    <q-btn size="sm" color="teal" dense icon="check" label="Забір" @click="openCollectDialog(props.row)" />
                  </q-td>
                </template>
              </q-table>
            </q-card>
          </div>

          <!-- VIEW: LOGISTICS -->
          <div v-show="currentView === 'logistics'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-truck" class="q-mr-sm" />
                  Логістика зразків та термоконтроль
                </h5>
                <div class="text-caption text-grey-7">Маршрутні листи, термоконтейнери, термологери та вхідна відбраковка біоматеріалу.</div>
              </div>
              <div><q-btn color="primary" icon="post_add" label="Створити маніфест" @click="notify('Формування маніфесту')" /></div>
            </div>

            <div class="row q-col-gutter-md q-mb-md">
              <div class="col-12 col-md-4">
                <q-card flat bordered class="bg-blue-1">
                  <q-card-section>
                    <div class="row items-center justify-between">
                      <div class="text-subtitle1 text-weight-bold text-primary">Контейнер #TC-201</div>
                      <q-badge color="positive">В дорозі (+4.2°C)</q-badge>
                    </div>
                    <div class="text-caption q-mt-sm">Пункт №3 &rarr; Центральна КДЛ<br>Кур’єр: Шевчук Д. (24 пробірки)</div>
                  </q-card-section>
                  <q-card-actions align="between">
                    <span class="text-caption text-grey-7">Виїзд: 08:30</span>
                    <q-btn size="sm" color="primary" label="Прийняти" @click="notify('Контейнер TC-201 прийнято')" />
                  </q-card-actions>
                </q-card>
              </div>
              <div class="col-12 col-md-4">
                <q-card flat bordered class="bg-green-1">
                  <q-card-section>
                    <div class="row items-center justify-between">
                      <div class="text-subtitle1 text-weight-bold text-positive">Контейнер #TC-104</div>
                      <q-badge color="teal">Доставлено (+3.8°C)</q-badge>
                    </div>
                    <div class="text-caption q-mt-sm">Поліклініка №1 &rarr; Центральна КДЛ<br>Кур’єр: Коваль М. (12 пробірок)</div>
                  </q-card-section>
                  <q-card-actions align="between">
                    <span class="text-caption text-grey-7">Прийнято 08:55</span>
                    <q-btn size="sm" color="teal" flat label="Акт прийому" />
                  </q-card-actions>
                </q-card>
              </div>
              <div class="col-12 col-md-4">
                <q-card flat bordered class="bg-amber-1">
                  <q-card-section>
                    <div class="row items-center justify-between">
                      <div class="text-subtitle1 text-weight-bold text-warning">Контейнер #TC-309</div>
                      <q-badge color="warning" text-color="dark">Увага (+9.5°C)</q-badge>
                    </div>
                    <div class="text-caption q-mt-sm">Денний стаціонар &rarr; Центральна КДЛ<br>Перевищено поріг 8°C! (6 пробірок)</div>
                  </q-card-section>
                  <q-card-actions align="between">
                    <span class="text-caption text-negative">Термологер TL-441</span>
                    <q-btn size="sm" color="negative" label="Брак зразків" @click="showRejectionDialog = true" />
                  </q-card-actions>
                </q-card>
              </div>
            </div>
          </div>

          <!-- VIEW: PATIENT PORTAL -->
          <div v-show="currentView === 'patient'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-user-circle" class="q-mr-sm" />
                  Кабінет пацієнта (Моніторинг замовлення)
                </h5>
                <div class="text-caption text-grey-7">Трекінг етапів дослідження, динаміка показників та офіційний бланк з печаткою.</div>
              </div>
              <div><q-btn color="primary" icon="picture_as_pdf" label="Завантажити PDF-бланк" @click="notify('Бланк з QR-кодом успішно згенеровано')" /></div>
            </div>

            <q-card flat bordered class="q-mb-md bg-white">
              <q-card-section class="row items-center justify-between">
                <div class="row items-center q-gutter-md">
                  <q-avatar size="48px" color="primary" text-color="white" icon="person" />
                  <div>
                    <div class="text-h6 text-weight-bold">Коваленко Олександр Сергійович</div>
                    <div class="text-caption text-grey-7">12.04.1985 (41 р.) | Карта №108291 | +380 (67) 123-45-67</div>
                  </div>
                </div>
                <q-chip color="teal" text-color="white" icon="verified">Замовлення готове (100%)</q-chip>
              </q-card-section>
            </q-card>

            <q-card flat bordered class="q-mb-md q-pa-md bg-white">
              <div class="text-subtitle2 text-weight-bold text-primary q-mb-sm">Життєвий цикл пробірок у лабораторії</div>
              <q-stepper v-model="patientStep" color="primary" animated header-nav dense>
                <q-step :name="1" title="Забір зразка" icon="fas fa-syringe" done>Виконано о 08:45</q-step>
                <q-step :name="2" title="Транспортування" icon="fas fa-truck" done>Доставлено термоконтейнером о 09:20 (+4.2°C)</q-step>
                <q-step :name="3" title="Аналіз" icon="fas fa-microscope" done>Sysmex XN-1000 о 09:50</q-step>
                <q-step :name="4" title="Валідація" icon="verified" done>Підписано ЕЦП лікаря о 10:15</q-step>
              </q-stepper>
            </q-card>
          </div>

          <!-- VIEW: MICROBIOLOGY -->
          <div v-show="currentView === 'microbiology'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-bacterium" class="q-mr-sm" />
                  Бактеріологія та антибіотикограма (EUCAST 2026)
                </h5>
                <div class="text-caption text-grey-7">Культуральний аналіз, ідентифікація мікроорганізмів, тестування чутливості (S/I/R).</div>
              </div>
              <div><q-btn color="primary" icon="print" label="Друк антибіотикограми" dense flat /></div>
            </div>

            <q-card flat bordered class="q-mb-md bg-white">
              <q-card-section class="row items-center justify-between bg-grey-2 q-py-sm">
                <div class="text-subtitle2 text-weight-bold">Зразок: №BACT-2026-0914 | Пацієнт: Василенко О.П. | Сеча</div>
                <q-badge color="teal">Збудник ідентифіковано</q-badge>
              </q-card-section>
              <q-card-section class="row q-col-gutter-md">
                <div class="col-12 col-md-3"><div class="text-caption text-grey-7">Мікрофлора:</div><div class="text-subtitle1 text-weight-bold text-negative">Escherichia coli</div></div>
                <div class="col-12 col-md-3"><div class="text-caption text-grey-7">Титр колоній:</div><div class="text-subtitle1 text-weight-bold">1 x 10^6 КУО/мл</div></div>
                <div class="col-12 col-md-3"><div class="text-caption text-grey-7">Морфологія:</div><div class="text-subtitle1">Грам-негативні палички</div></div>
                <div class="col-12 col-md-3"><div class="text-caption text-grey-7">Фенотип:</div><div class="text-subtitle1 text-teal">ESBL(-), Carbapenemase(-)</div></div>
              </q-card-section>
            </q-card>

            <q-card flat bordered class="medlink-card">
              <q-table :data="microbiologyData.antibiotics" :columns="mbColumns" row-key="name" dense flat>
                <template v-slot:body-cell-eucast="props">
                  <q-td :props="props">
                    <q-badge :color="props.row.eucast === 'S' ? 'positive' : (props.row.eucast === 'I' ? 'warning' : 'negative')">
                      {{ props.row.eucast }} ({{ props.row.eucast === 'S' ? 'Чутливий' : (props.row.eucast === 'I' ? 'Помірно' : 'Стійкий') }})
                    </q-badge>
                  </q-td>
                </template>
              </q-table>
            </q-card>
          </div>

          <!-- VIEW: BIOBANK -->
          <div v-show="currentView === 'biobank'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-snowflake" class="q-mr-sm" />
                  Біобанк та архів зразків
                </h5>
                <div class="text-caption text-grey-7">Адресне розміщення: Морозильник -80°C &rarr; Полиця A &rarr; Штатив 4 &rarr; Матриця 9х9.</div>
              </div>
            </div>

            <div class="row q-col-gutter-md">
              <div class="col-12 col-md-5">
                <q-card flat bordered class="q-pa-md">
                  <div class="text-subtitle1 text-weight-bold text-primary q-mb-xs">Кріосховище №1 (-80°C)</div>
                  <div class="text-caption text-grey-7 q-mb-md">Штатив #4 (Сироватки контрольні)</div>
                  <div style="border: 1px solid #ccc; max-width: 290px; margin: 0 auto;" class="q-pa-xs bg-grey-2 rounded-borders">
                    <div v-for="r in 9" :key="'br'+r" class="row no-wrap justify-between q-mb-xs">
                      <div v-for="c in 9" :key="'bc'+c"
                           class="grid-cell flex flex-center"
                           :class="(r===2 && c===5) ? 'bg-primary text-white text-weight-bold' : ((r%2===0 && c%3===0) ? 'bg-teal-3' : 'bg-white')">
                        {{ String.fromCharCode(64+r) }}{{ c }}
                      </div>
                    </div>
                  </div>
                </q-card>
              </div>
              <div class="col-12 col-md-7">
                <q-card flat bordered class="medlink-card">
                  <q-card-section class="bg-grey-2 q-py-sm"><div class="text-subtitle2 text-weight-bold">Картка зразка B5</div></q-card-section>
                  <q-card-section class="q-pa-md">
                    <div class="text-body2 q-mb-xs">Штрихкод: <strong>1026004819</strong></div>
                    <div class="text-body2 q-mb-xs">Пацієнт: <strong>Коваленко Олександр Сергійович</strong></div>
                    <div class="text-body2 q-mb-xs">Тип зразка: <strong>Сироватка крові (Аліквота №1, 1.5 мл)</strong></div>
                    <div class="text-body2 q-mb-xs">Дата заморозки: <strong>2026-10-06 10:30</strong></div>
                    <div class="text-body2 q-mb-md">Термін зберігання: <strong>до 2027-04-06 (6 місяців)</strong></div>
                    <div class="row q-gutter-sm">
                      <q-btn color="primary" dense label="Видати на аналіз" @click="notify('Зразок видано з архіву')" />
                      <q-btn color="negative" dense outline label="Списати" />
                    </div>
                  </q-card-section>
                </q-card>
              </div>
            </div>
          </div>

          <!-- VIEW: REAGENTS -->
          <div v-show="currentView === 'reagents'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-boxes" class="q-mr-sm" />
                  Склад реактивів, калібраторів та витратних матеріалів
                </h5>
                <div class="text-caption text-grey-7">Залишок тестів на борту, On-board stability та автоматичне списання за фактом тестів.</div>
              </div>
              <div><q-btn color="primary" icon="qr_code_scanner" label="Сканувати касету" @click="notify('Камеру сканера активовано')" /></div>
            </div>

            <q-card flat bordered class="medlink-card">
              <q-table :data="reagentsList" :columns="reagentColumns" row-key="id" dense flat>
                <template v-slot:body-cell-testsRemaining="props">
                  <q-td :props="props">
                    <q-linear-progress :value="props.row.testsRemaining / props.row.testsTotal" :color="props.row.testsRemaining < 50 ? 'negative' : 'teal'" style="height: 6px;" />
                    <div class="text-caption">{{ props.row.testsRemaining }} / {{ props.row.testsTotal }}</div>
                  </q-td>
                </template>
                <template v-slot:body-cell-status="props">
                  <q-td :props="props">
                    <q-badge :color="props.row.status === 'ACTIVE' ? 'positive' : (props.row.status === 'LOW_STOCK' ? 'warning' : 'negative')">
                      {{ props.row.status === 'ACTIVE' ? 'Активний' : (props.row.status === 'LOW_STOCK' ? 'Закінчується' : 'Протерміновано') }}
                    </q-badge>
                  </q-td>
                </template>
              </q-table>
            </q-card>
          </div>

          <!-- VIEW: ANALYZERS -->
          <div v-show="currentView === 'analyzers'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-network-wired" class="q-mr-sm" />
                  Монітор підключення аналізаторів (Шлюз MedLink.LabConnector)
                </h5>
                <div class="text-caption text-grey-7">Драйвери ASTM E1381/E1394, HL7 v2.x MLLP, локальний буфер SQLite, COM/RS-232 та TCP/IP.</div>
              </div>
              <div><q-btn color="secondary" icon="refresh" label="Оновити шлюз" dense flat @click="notify('Шлюз опитує аналізатори...')" /></div>
            </div>

            <q-card flat bordered class="medlink-card">
              <q-table :data="analyzersList" :columns="analyzerColumns" row-key="id" dense flat>
                <template v-slot:body-cell-status="props">
                  <q-td :props="props"><q-badge :color="props.row.status === 'ONLINE' ? 'positive' : 'warning'">{{ props.row.status }}</q-badge></q-td>
                </template>
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props" class="q-gutter-xs">
                    <q-btn size="sm" color="primary" dense icon="terminal" label="Лог пакетів" @click="showLogDialog = true" />
                    <q-btn size="sm" color="teal" dense icon="send" label="Тест Ping" @click="testPing(props.row)" />
                  </q-td>
                </template>
              </q-table>
            </q-card>
          </div>

          <!-- VIEW: TAT & ANALYTICS -->
          <div v-show="currentView === 'tat'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-chart-pie" class="q-mr-sm" />
                  Операційна аналітика лабораторії & Turnaround Time (TAT)
                </h5>
                <div class="text-caption text-grey-7">Контроль термінів виконання CITO/Routine, відсоток відбраковки біоматеріалу та навантаження приладів.</div>
              </div>
              <div><q-btn color="primary" icon="file_download" label="Експорт Excel" dense flat /></div>
            </div>

            <div class="row q-col-gutter-md">
              <div class="col-12 col-md-3"><q-card flat bordered class="q-pa-md text-center"><div class="text-caption text-grey-7">Час CITO TAT</div><div class="text-h5 text-weight-bold text-positive">28.4 хв</div><div class="text-caption text-teal">SLA 98.2% (&lt; 45 хв)</div></q-card></div>
              <div class="col-12 col-md-3"><q-card flat bordered class="q-pa-md text-center"><div class="text-caption text-grey-7">Плановий TAT</div><div class="text-h5 text-weight-bold text-primary">2 год 14 хв</div><div class="text-caption text-grey-7">Норматив: &lt; 4 год</div></q-card></div>
              <div class="col-12 col-md-3"><q-card flat bordered class="q-pa-md text-center"><div class="text-caption text-grey-7">Брак біоматеріалу</div><div class="text-h5 text-weight-bold text-warning">0.74%</div><div class="text-caption text-positive">Ціль: &lt; 1.0%</div></q-card></div>
              <div class="col-12 col-md-3"><q-card flat bordered class="q-pa-md text-center"><div class="text-caption text-grey-7">Досліджень за зміну</div><div class="text-h5 text-weight-bold text-teal">1,482</div><div class="text-caption text-grey-7">+12% до середнього</div></q-card></div>
            </div>
          </div>

          <!-- VIEW: NORMS & METHODOLOGIES -->
          <div v-show="currentView === 'norms'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-sliders-h" class="q-mr-sm" />
                  Норми, референси, панічні пороги та методики
                </h5>
                <div class="text-caption text-grey-7">Налаштування референсних інтервалів за статтю та віком, методики вимірювання (IFCC / DGKC).</div>
              </div>
              <div><q-btn color="primary" icon="add" label="Додати норму" dense /></div>
            </div>

            <q-card flat bordered class="medlink-card">
              <q-table :data="normsList" :columns="normsColumns" row-key="id" dense flat>
                <template v-slot:body-cell-panicRange="props">
                  <q-td :props="props" class="text-negative text-weight-bold">{{ props.row.panicLow }} / {{ props.row.panicHigh }}</q-td>
                </template>
              </q-table>
            </q-card>
          </div>

          <!-- VIEW: DICTIONARY BIOMATERIALS -->
          <div v-show="currentView === 'dict_biomaterials'">
            <div class="row items-center justify-between q-mb-md">
              <h5 class="q-my-none text-weight-bold text-primary"><q-icon name="fas fa-vial" class="q-mr-sm" /> Довідник біоматеріалів</h5>
              <q-btn color="primary" icon="add" label="Додати" dense />
            </div>
            <q-card flat bordered class="medlink-card">
              <q-table :data="biomaterialsList" :columns="dictBioColumns" row-key="code" dense flat />
            </q-card>
          </div>

          <!-- VIEW: DICTIONARY TUBES -->
          <div v-show="currentView === 'dict_tubes'">
            <div class="row items-center justify-between q-mb-md">
              <h5 class="q-my-none text-weight-bold text-primary"><q-icon name="fas fa-flask" class="q-mr-sm" /> Довідник пробірок та контейнерів</h5>
              <q-btn color="primary" icon="add" label="Додати" dense />
            </div>
            <q-card flat bordered class="medlink-card">
              <q-table :data="tubesList" :columns="dictTubeColumns" row-key="code" dense flat>
                <template v-slot:body-cell-color="props">
                  <q-td :props="props">
                    <q-badge :style="{ backgroundColor: props.row.colorHex }" text-color="white">{{ props.row.colorName }}</q-badge>
                  </q-td>
                </template>
              </q-table>
            </q-card>
          </div>

          <!-- VIEW: DICTIONARY ANALYZERS -->
          <div v-show="currentView === 'dict_analyzers'">
            <div class="row items-center justify-between q-mb-md">
              <h5 class="q-my-none text-weight-bold text-primary"><q-icon name="fas fa-server" class="q-mr-sm" /> Довідник типів та моделей аналізаторів</h5>
              <q-btn color="primary" icon="add" label="Додати" dense />
            </div>
            <q-card flat bordered class="medlink-card">
              <q-table :data="analyzerModelsList" :columns="dictAnColumns" row-key="code" dense flat />
            </q-card>
          </div>

          <!-- VIEW: DICTIONARY PARAMETERS -->
          <div v-show="currentView === 'dict_parameters'">
            <div class="row items-center justify-between q-mb-md">
              <h5 class="q-my-none text-weight-bold text-primary"><q-icon name="fas fa-list-ol" class="q-mr-sm" /> Довідник лабораторних показників</h5>
              <q-btn color="primary" icon="add" label="Додати" dense />
            </div>
            <q-card flat bordered class="medlink-card">
              <q-table :data="parametersList" :columns="dictParamColumns" row-key="code" dense flat />
            </q-card>
          </div>

        </q-page>
      </q-page-container>
    </q-layout>

    <!-- Barcode Print Dialog -->
    <q-dialog v-model="showBarcodeDialog">
      <q-card style="min-width: 480px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold"><q-icon name="fas fa-barcode" class="q-mr-sm" /> Друк ZPL етикеток на термопринтер</div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md" v-if="activeOrder">
          <div class="text-subtitle2">Пацієнт: <strong>{{ activeOrder.patientName }}</strong> ({{ activeOrder.patientAge }} р.)</div>
          <div class="text-caption text-grey-7 q-mb-md">Замовлення №: {{ activeOrder.orderNumber }}</div>
          <div class="text-weight-bold q-mb-xs">Пробірки для друку (Order of Draw):</div>
          <q-list bordered separator dense>
            <q-item v-for="s in activeOrderSamples" :key="s.barcode">
              <q-item-section avatar style="min-width: 24px;"><strong>{{ s.orderOfDraw }}.</strong></q-item-section>
              <q-item-section>
                <q-item-label>
                  <q-badge :style="{ backgroundColor: s.capColor }" class="q-mr-xs text-white">{{ s.capName }}</q-badge>
                  {{ s.tubeType }}
                </q-item-label>
                <q-item-label caption>{{ s.biomaterial }} ({{ s.volume }})</q-item-label>
              </q-item-section>
              <q-item-section side><span class="text-caption text-mono">{{ s.barcode }}</span></q-item-section>
            </q-item>
          </q-list>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Закрити" v-close-popup />
          <q-btn color="primary" icon="print" label="Відправити на Zebra ZPL" @click="confirmPrintZpl" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Collection Dialog -->
    <q-dialog v-model="showCollectDialog">
      <q-card style="min-width: 480px;">
        <q-card-section class="bg-teal text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold"><q-icon name="fas fa-check-circle" class="q-mr-sm" /> Чек-лист преаналітики</div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md">
          <q-checkbox v-model="collectCheck.fasting" label="Пацієнт натщесерце (8+ годин)" />
          <q-checkbox v-model="collectCheck.idVerified" label="Ідентифікація пацієнта перевірена" />
          <q-checkbox v-model="collectCheck.orderOfDraw" label="Порядок наповнення пробірок дотримано" />
          <q-checkbox v-model="collectCheck.mixing" label="Перевертання пробірок виконано (5-8 разів)" />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="teal" label="Підтвердити забір" @click="confirmCollect" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Call Dialog -->
    <q-dialog v-model="showCallDialog">
      <q-card style="min-width: 500px;">
        <q-card-section class="bg-negative text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold"><q-icon name="phone_in_talk" class="q-mr-sm" /> Реєстрація дзвінка панічного значення</div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="text-caption text-grey-8 q-mb-sm">Обов’язкова передача критичних результатів у ВРІТ згідно ISO 15189.</div>
          <q-input v-model="callLog.doctor" label="ПІБ лікаря стаціонару" outlined dense class="q-mb-sm" />
          <q-input v-model="callLog.department" label="Відділення" outlined dense class="q-mb-sm" />
          <q-input v-model="callLog.phone" label="Внутрішній телефон" outlined dense class="q-mb-sm" />
          <q-input v-model="callLog.readback" label="Зворотне підтвердження (Read-back)" outlined dense />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Закрити" v-close-popup />
          <q-btn color="negative" label="Зафіксувати в журналі аудиту" @click="saveCall" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Edit Result Dialog -->
    <q-dialog v-model="showEditResultDialog">
      <q-card style="min-width: 440px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">Редагування результату</div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md" v-if="editingRow">
          <div class="text-subtitle2 q-mb-xs">{{ editingRow.testName }} ({{ editingRow.testCode }})</div>
          <div class="text-caption text-grey-7 q-mb-md">Пацієнт: {{ editingRow.patientName }} | Штрихкод: {{ editingRow.barcode }}</div>
          <q-input v-model.number="editingRow.value" type="number" step="0.01" label="Значення" outlined dense class="q-mb-sm" />
          <q-input v-model="editingRow.comment" label="Коментар" outlined dense />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" label="Зберегти" @click="saveEditedRow" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Raw Packet Logs Modal -->
    <q-dialog v-model="showLogDialog">
      <q-card style="min-width: 650px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold"><q-icon name="terminal" class="q-mr-sm" /> ASTM / HL7 Raw Packet Monitor</div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md bg-dark text-cyan-2" style="font-family: monospace; font-size: 12px; max-height: 300px; overflow-y: auto;">
          [10:14:02.102] TCP CONNECT from 192.168.1.101:5100 ESTABLISHED<br>
          [10:14:02.105] RECV &lt;ENQ&gt; (0x05)<br>
          [10:14:02.106] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.110] RECV &lt;STX&gt;1H|\\^&amp;|||Sysmex^XN-1000^00-14||||||||E1394-97&lt;CR&gt;&lt;ETX&gt;4A&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.111] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.115] RECV &lt;STX&gt;2P|1||108291||Коваленко^Олександр^Сергійович||19850412|M&lt;CR&gt;&lt;ETX&gt;7B&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.122] RECV &lt;STX&gt;3O|1|1026004820||^^^WBC\\^^^HGB|R|20261006084700||||||||||||||||||F&lt;CR&gt;&lt;ETX&gt;9D&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.130] RECV &lt;STX&gt;4R|1|^^^WBC|7.45|10*9/L|4.0-9.0|N||F||||20261006095011&lt;CR&gt;&lt;ETX&gt;21&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.140] RECV &lt;EOT&gt; (0x04) - Transaction completed successfully.<br>
          [10:14:02.145] MedLink Backend: Processed sample 1026004820, WBC saved.
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Закрити" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- QC Unlock Dialog -->
    <q-dialog v-model="showQcUnlockDialog">
      <q-card style="min-width: 450px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">Зняття блокування аналізатора</div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="text-caption q-mb-sm">Опишіть проведені коригувальні дії:</div>
          <q-input v-model="qcCorrectiveAction" type="textarea" outlined dense rows="3" />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" label="Розблокувати" @click="confirmQcUnlock" />
        </q-card-actions>
      </q-card>
    </q-dialog>

  </div>

  <!-- Vue 2 & Quasar JS CDN -->
  <script src="https://cdn.jsdelivr.net/npm/vue@2.6.14/dist/vue.min.js"></script>
  <script src="https://cdn.jsdelivr.net/npm/quasar@1.15.3/dist/quasar.umd.min.js"></script>
  <script src="https://cdn.jsdelivr.net/npm/quasar@1.15.3/dist/lang/uk.umd.min.js"></script>

  <script>
    if (window.Quasar && window.Quasar.lang && window.Quasar.lang.uk) {
      Quasar.lang.set(Quasar.lang.uk);
    }

    new Vue({
      el: '#q-app',
      data: {
        leftDrawerOpen: true,
        currentView: 'workstation',
        currentViewTitle: 'Робочий стіл лаборанта',
        defaultPagination: { rowsPerPage: 10 },
        
        // Dialog visibility
        showBarcodeDialog: false,
        showCollectDialog: false,
        showCallDialog: false,
        showEditResultDialog: false,
        showLogDialog: false,
        showQcUnlockDialog: false,
        showRejectionDialog: false,

        activeOrder: null,
        editingRow: null,
        patientStep: 4,
        qcCorrectiveAction: 'Промито оптичну кювету Cellclean, виконано заміну ділюента, контрольний замір L2 = 7.21 (OK).',

        collectCheck: { fasting: true, idVerified: true, orderOfDraw: true, mixing: true },
        callLog: {
          doctor: 'Савченко І.О. (Черговий реаніматолог)',
          department: 'ВРІТ',
          phone: 'вн. 214',
          readback: 'Значення 26.4 ммоль/л підтверджено голосом'
        },

        // Filters
        worklistSearch: '',
        worklistAnalyzerFilter: 'Всі прилади',
        worklistFlagFilter: 'Всі результати',

        // Mock Datasets
        ordersList: [
          {
            id: "ord-2026-004819",
            orderNumber: "1026-004819",
            patientName: "Коваленко О.С.",
            patientAge: 41,
            patientGender: "M",
            department: "Поліклініка №1",
            createdAt: "2026-10-06 08:30",
            priority: "ROUTINE",
            status: "IN_PROGRESS",
            ehealthReferralCode: "5491-8821-9012"
          },
          {
            id: "ord-2026-004812",
            orderNumber: "1026-004812",
            patientName: "Мельник Ю.В.",
            patientAge: 48,
            patientGender: "M",
            department: "ВРІТ",
            createdAt: "2026-10-06 09:10",
            priority: "CITO",
            status: "PANIC_ALERT",
            ehealthReferralCode: "7721-3310-4491"
          },
          {
            id: "ord-2026-004825",
            orderNumber: "1026-004825",
            patientName: "Василенко О.П.",
            patientAge: 34,
            patientGender: "F",
            department: "Жіноча конс.",
            createdAt: "2026-10-04 11:20",
            priority: "ROUTINE",
            status: "VERIFIED",
            ehealthReferralCode: "8821-4401-1192"
          }
        ],

        samplesList: [
          { barcode: "1026004818", orderId: "ord-2026-004819", biomaterial: "Венозна кров (плазма)", tubeType: "Цитрат натрію 3.2%", capColor: "#0284c7", capName: "Блакитна", volume: "3.0 мл", orderOfDraw: 1, status: "COLLECTED" },
          { barcode: "1026004819", orderId: "ord-2026-004819", biomaterial: "Сироватка крові", tubeType: "Активатор згортання / Гель", capColor: "#ca8a04", capName: "Жовта", volume: "5.0 мл", orderOfDraw: 2, status: "IN_LAB" },
          { barcode: "1026004820", orderId: "ord-2026-004819", biomaterial: "Цільна венозна кров", tubeType: "K2/K3 ЕДТА", capColor: "#9333ea", capName: "Фіолетова", volume: "2.6 мл", orderOfDraw: 3, status: "COLLECTED" }
        ],

        worklist: [
          { id: "res-101", barcode: "1026004812", patientName: "Мельник Ю.В.", analyzer: "Cobas e411", testCode: "GLU", testName: "Глюкоза сироватки", value: 26.4, unit: "ммоль/л", normMin: 4.1, normMax: 5.9, flag: "PANIC_HIGH", deltaPercent: "+185%", status: "PENDING_VERIFY", comment: "Критично високий рівень! Ризик гіперосмолярної коми." },
          { id: "res-102", barcode: "1026004819", patientName: "Коваленко О.С.", analyzer: "Mindray BS-240", testCode: "ALT", testName: "Аланінамінотрансфераза (АЛТ)", value: 68.5, unit: "U/L", normMin: 0.0, normMax: 41.0, flag: "DELTA_ALERT", deltaPercent: "+42.7%", status: "PENDING_VERIFY", comment: "Помірний цитоліз. Delta-чек перевищує поріг 25%." },
          { id: "res-103", barcode: "1026004819", patientName: "Коваленко О.С.", analyzer: "Mindray BS-240", testCode: "CREAT", testName: "Креатинін сироватки", value: 84.0, unit: "мкмоль/л", normMin: 62.0, normMax: 115.0, flag: "NORMAL", deltaPercent: "-2.1%", status: "AUTO_VERIFIED", comment: "В межах норми." },
          { id: "res-104", barcode: "1026004820", patientName: "Коваленко О.С.", analyzer: "Sysmex XN-1000", testCode: "WBC", testName: "Лейкоцити (WBC)", value: 7.45, unit: "10*9/л", normMin: 4.0, normMax: 9.0, flag: "NORMAL", deltaPercent: "+1.2%", status: "AUTO_VERIFIED", comment: "В межах норми." },
          { id: "res-105", barcode: "1026004820", patientName: "Коваленко О.С.", analyzer: "Sysmex XN-1000", testCode: "HGB", testName: "Гемоглобін (HGB)", value: 148.0, unit: "г/л", normMin: 130.0, normMax: 160.0, flag: "NORMAL", deltaPercent: "0.0%", status: "AUTO_VERIFIED", comment: "В межах норми." }
        ],

        qcData: {
          analyzer: "Sysmex XN-1000 (#SN-41029)",
          controlMaterial: "XN-CHECK Level 2 (Normal)",
          lotNumber: "LOT-XN-2026-L2",
          parameter: "WBC (Лейкоцити)",
          targetMean: 7.20,
          targetSd: 0.30,
          cvPercent: 4.1,
          dataPoints: [
            { day: 1, val: 7.15, status: "OK" },
            { day: 2, val: 7.22, status: "OK" },
            { day: 3, val: 7.05, status: "OK" },
            { day: 4, val: 7.35, status: "OK" },
            { day: 5, val: 7.18, status: "OK" },
            { day: 6, val: 7.25, status: "OK" },
            { day: 7, val: 7.42, status: "WARN_1_2S" },
            { day: 8, val: 7.10, status: "OK" },
            { day: 9, val: 7.02, status: "OK" },
            { day: 10, val: 7.21, status: "OK" },
            { day: 11, val: 7.55, status: "WARN_1_2S" },
            { day: 12, val: 7.19, status: "OK" },
            { day: 13, val: 7.28, status: "OK" },
            { day: 14, val: 7.32, status: "OK" },
            { day: 15, val: 8.28, status: "FAIL_1_3S" }
          ]
        },

        microbiologyData: {
          antibiotics: [
            { name: "Фосфоміцин", mic: "≤ 1.0", zone: 28, eucast: "S", interpretation: "Чутливий (1-а лінія)" },
            { name: "Нітрофурантоїн", mic: "16.0", zone: 22, eucast: "S", interpretation: "Чутливий" },
            { name: "Ципрофлоксацин", mic: "0.25", zone: 26, eucast: "S", interpretation: "Чутливий" },
            { name: "Ампіцилін", mic: "> 32.0", zone: 11, eucast: "R", interpretation: "Резистентний (Стійкий)" },
            { name: "Амоксицилін / Клаванат", mic: "8.0", zone: 17, eucast: "I", interpretation: "Помірно-чутливий" }
          ]
        },

        reagentsList: [
          { id: "reag-01", analyzer: "Sysmex XN-1000", name: "Cellpack DCL (Ділюент)", lotNumber: "LOT-2026-XN08", testsRemaining: 1420, testsTotal: 2000, openedAt: "2026-10-01", expiresAt: "2027-12-31", status: "ACTIVE" },
          { id: "reag-02", analyzer: "Roche Cobas e411", name: "Elecsys TSH (ТТГ)", lotNumber: "LOT-682190-01", testsRemaining: 18, testsTotal: 200, openedAt: "2026-09-28", expiresAt: "2026-10-28", status: "LOW_STOCK" },
          { id: "reag-03", analyzer: "Mindray BS-240", name: "Glucose GOD-POD", lotNumber: "LOT-GLU-9902", testsRemaining: 85, testsTotal: 500, openedAt: "2026-09-05", expiresAt: "2026-10-05", status: "EXPIRED" }
        ],

        analyzersList: [
          { id: "an-01", name: "Sysmex XN-1000", type: "Гематологічний 5-diff", protocol: "ASTM E1381/E1394", connection: "TCP/IP 192.168.1.101:5100", status: "ONLINE" },
          { id: "an-02", name: "Roche Cobas e411", type: "Імунохімічний", protocol: "ASTM E1394", connection: "COM3 (9600 8N1)", status: "ONLINE" },
          { id: "an-03", name: "Mindray BS-240", type: "Біохімічний", protocol: "HL7 v2.3.1 MLLP", connection: "TCP/IP 192.168.1.105:5000", status: "ONLINE" },
          { id: "an-04", name: "Sysmex CA-660", type: "Коагулометр", protocol: "ASTM E1381", connection: "COM4 (9600 8N1)", status: "STANDBY" }
        ],

        normsList: [
          { id: 1, param: 'Глюкоза сироватки', gender: 'Обидва', ageGroup: 'Дорослі (18-60 р.)', method: 'Гексокіназний (IFCC)', unit: 'ммоль/л', normMin: 4.10, normMax: 5.90, panicLow: 2.50, panicHigh: 25.00 },
          { id: 2, param: 'Гемоглобін (HGB)', gender: 'Чоловіки', ageGroup: 'Дорослі (>18 р.)', method: 'SLS-метод (безціанідний)', unit: 'г/л', normMin: 130.0, normMax: 160.0, panicLow: 70.0, panicHigh: 200.0 },
          { id: 3, param: 'Гемоглобін (HGB)', gender: 'Жінки', ageGroup: 'Дорослі (>18 р.)', method: 'SLS-метод (безціанідний)', unit: 'г/л', normMin: 120.0, normMax: 150.0, panicLow: 70.0, panicHigh: 200.0 },
          { id: 4, param: 'Креатинін сироватки', gender: 'Чоловіки', ageGroup: 'Дорослі (>18 р.)', method: 'Ензиматичний (IDMS)', unit: 'мкмоль/л', normMin: 62.0, normMax: 115.0, panicLow: 30.0, panicHigh: 350.0 }
        ],

        biomaterialsList: [
          { code: "BLDV", name: "Цільна кров венозна", container: "EDTA / Цитрат", snomed: "122555007", storage: "2-8°C до 24 год" },
          { code: "SER", name: "Сироватка крові", container: "Активатор згортання / Гель", snomed: "119364003", storage: "2-8°C до 7 діб, -20°C до 6 міс" },
          { code: "PLAS", name: "Плазма крові (цитратна)", container: "Цитрат натрію 3.2%", snomed: "119361006", storage: "2-8°C до 4 год" },
          { code: "URIN", name: "Сеча (ранкова порція)", container: "Стерильний контейнер", snomed: "122575003", storage: "2-8°C до 4 год" }
        ],

        tubesList: [
          { code: 'TUBE-CITRATE', name: 'Цитрат натрію 3.2%', colorName: 'Блакитна', colorHex: '#0284c7', volume: '3.0 мл', orderOfDraw: 1, inversions: '3-4 рази' },
          { code: 'TUBE-SERUM-GEL', name: 'Активатор згортання / Гель', colorName: 'Жовта', colorHex: '#ca8a04', volume: '5.0 мл', orderOfDraw: 2, inversions: '5-6 разів' },
          { code: 'TUBE-EDTA', name: 'K2 / K3 ЕДТА', colorName: 'Фіолетова', colorHex: '#9333ea', volume: '2.6 мл', orderOfDraw: 3, inversions: '8-10 разів' }
        ],

        analyzerModelsList: [
          { code: 'SYSMEX-XN1000', vendor: 'Sysmex Corporation', model: 'XN-1000', discipline: 'Гематологія 5-diff', protocol: 'ASTM E1381/E1394', interfaceType: 'TCP/IP Client/Server' },
          { code: 'ROCHE-COBAS-E411', vendor: 'Roche Diagnostics', model: 'Cobas e411', discipline: 'Імунохімія', protocol: 'ASTM E1394', interfaceType: 'RS-232 / TCP' },
          { code: 'MINDRAY-BS240', vendor: 'Mindray Medical', model: 'BS-240', discipline: 'Біохімія', protocol: 'HL7 v2.3.1 MLLP', interfaceType: 'TCP/IP MLLP' }
        ],

        parametersList: [
          { code: 'WBC', name: 'Лейкоцити (White Blood Cells)', category: 'Гематологія', loinc: '6690-2', unit: '10*9/л', sampleType: 'EDTA кров' },
          { code: 'HGB', name: 'Гемоглобін (Hemoglobin)', category: 'Гематологія', loinc: '718-7', unit: 'г/л', sampleType: 'EDTA кров' },
          { code: 'GLU', name: 'Глюкоза сироватки', category: 'Біохімія', loinc: '2345-7', unit: 'ммоль/л', sampleType: 'Сироватка' },
          { code: 'ALT', name: 'Аланінамінотрансфераза (АЛТ)', category: 'Біохімія', loinc: '1742-6', unit: 'U/L', sampleType: 'Сироватка' }
        ],

        // Table Columns
        worklistColumns: [
          { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left', sortable: true },
          { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left', sortable: true },
          { name: 'analyzer', label: 'Аналізатор', field: 'analyzer', align: 'left' },
          { name: 'testName', label: 'Тест', field: 'testName', align: 'left' },
          { name: 'value', label: 'Результат', field: 'value', align: 'right', sortable: true },
          { name: 'norm', label: 'Норма', align: 'center' },
          { name: 'flag', label: 'Флаг', field: 'flag', align: 'center' },
          { name: 'deltaPercent', label: 'Delta %', field: 'deltaPercent', align: 'right' },
          { name: 'status', label: 'Статус', field: 'status', align: 'center' },
          { name: 'actions', label: 'Дії', align: 'center' }
        ],

        orderColumns: [
          { name: 'orderNumber', label: '№ Замовлення', field: 'orderNumber', align: 'left', sortable: true },
          { name: 'createdAt', label: 'Дата / Час', field: 'createdAt', align: 'left' },
          { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left', sortable: true },
          { name: 'department', label: 'Відділення', field: 'department', align: 'left' },
          { name: 'priority', label: 'Пріоритет', field: 'priority', align: 'center' },
          { name: 'ehealthReferralCode', label: 'Код е-Направлення', field: 'ehealthReferralCode', align: 'left' },
          { name: 'status', label: 'Статус', field: 'status', align: 'center' },
          { name: 'actions', label: 'Дії', align: 'center' }
        ],

        validationColumns: [
          { name: 'patientName', label: 'Пацієнт', field: 'patientName', align: 'left' },
          { name: 'testName', label: 'Показник', field: 'testName', align: 'left' },
          { name: 'value', label: 'Результат', field: 'value', align: 'right' },
          { name: 'flag', label: 'Флаг', field: 'flag', align: 'center' },
          { name: 'comment', label: 'Клінічний коментар', field: 'comment', align: 'left' },
          { name: 'actions', label: 'Дії лікаря', align: 'center' }
        ],

        qcColumns: [
          { name: 'day', label: 'День місяця', field: 'day', align: 'center' },
          { name: 'val', label: 'Значення', field: 'val', align: 'right' },
          { name: 'status', label: 'Вестгард', field: 'status', align: 'center' }
        ],

        mbColumns: [
          { name: 'name', label: 'Антибіотик', field: 'name', align: 'left' },
          { name: 'mic', label: 'МІК (мкг/мл)', field: 'mic', align: 'center' },
          { name: 'zone', label: 'Зона (мм)', field: 'zone', align: 'center' },
          { name: 'eucast', label: 'EUCAST', field: 'eucast', align: 'center' },
          { name: 'interpretation', label: 'Клінічна інтерпретація', field: 'interpretation', align: 'left' }
        ],

        reagentColumns: [
          { name: 'analyzer', label: 'Аналізатор', field: 'analyzer', align: 'left' },
          { name: 'name', label: 'Реактив / Касета', field: 'name', align: 'left' },
          { name: 'lotNumber', label: 'Номер лоту', field: 'lotNumber', align: 'left' },
          { name: 'testsRemaining', label: 'Залишок тестів', field: 'testsRemaining', align: 'center' },
          { name: 'expiresAt', label: 'Придатний до', field: 'expiresAt', align: 'center' },
          { name: 'status', label: 'Статус', field: 'status', align: 'center' }
        ],

        analyzerColumns: [
          { name: 'name', label: 'Модель', field: 'name', align: 'left' },
          { name: 'type', label: 'Тип', field: 'type', align: 'left' },
          { name: 'protocol', label: 'Протокол', field: 'protocol', align: 'center' },
          { name: 'connection', label: 'З’єднання', field: 'connection', align: 'left' },
          { name: 'status', label: 'Статус', field: 'status', align: 'center' },
          { name: 'actions', label: 'Дії', align: 'center' }
        ],

        normsColumns: [
          { name: 'param', label: 'Показник', field: 'param', align: 'left' },
          { name: 'gender', label: 'Стать', field: 'gender', align: 'center' },
          { name: 'ageGroup', label: 'Вік', field: 'ageGroup', align: 'left' },
          { name: 'method', label: 'Методика', field: 'method', align: 'left' },
          { name: 'unit', label: 'Одиниця', field: 'unit', align: 'center' },
          { name: 'normMin', label: 'Норма Min', field: 'normMin', align: 'right' },
          { name: 'normMax', label: 'Норма Max', field: 'normMax', align: 'right' },
          { name: 'panicRange', label: 'Панічний поріг (L/H)', align: 'center' }
        ],

        dictBioColumns: [
          { name: 'code', label: 'Код', field: 'code', align: 'left' },
          { name: 'name', label: 'Назва', field: 'name', align: 'left' },
          { name: 'container', label: 'Контейнер', field: 'container', align: 'left' },
          { name: 'snomed', label: 'SNOMED CT', field: 'snomed', align: 'center' },
          { name: 'storage', label: 'Умови зберігання', field: 'storage', align: 'left' }
        ],

        dictTubeColumns: [
          { name: 'code', label: 'Код', field: 'code', align: 'left' },
          { name: 'name', label: 'Наповнювач', field: 'name', align: 'left' },
          { name: 'color', label: 'Колір кришки', align: 'center' },
          { name: 'volume', label: 'Об’єм', field: 'volume', align: 'center' },
          { name: 'orderOfDraw', label: 'Order of Draw', field: 'orderOfDraw', align: 'center' },
          { name: 'inversions', label: 'Перевертання', field: 'inversions', align: 'left' }
        ],

        dictAnColumns: [
          { name: 'code', label: 'Код', field: 'code', align: 'left' },
          { name: 'vendor', label: 'Виробник', field: 'vendor', align: 'left' },
          { name: 'model', label: 'Модель', field: 'model', align: 'left' },
          { name: 'discipline', label: 'Дисципліна', field: 'discipline', align: 'left' },
          { name: 'protocol', label: 'Протокол', field: 'protocol', align: 'center' },
          { name: 'interfaceType', label: 'Інтерфейс', field: 'interfaceType', align: 'left' }
        ],

        dictParamColumns: [
          { name: 'code', label: 'Код', field: 'code', align: 'left' },
          { name: 'name', label: 'Назва', field: 'name', align: 'left' },
          { name: 'category', label: 'Категорія', field: 'category', align: 'left' },
          { name: 'loinc', label: 'LOINC', field: 'loinc', align: 'center' },
          { name: 'unit', label: 'Одиниця', field: 'unit', align: 'center' },
          { name: 'sampleType', label: 'Рекомендований зразок', field: 'sampleType', align: 'left' }
        ]
      },

      computed: {
        filteredWorklist() {
          return this.worklist.filter(r => {
            const matchesQuery = !this.worklistSearch ||
              r.testName.toLowerCase().includes(this.worklistSearch.toLowerCase()) ||
              r.barcode.includes(this.worklistSearch) ||
              r.patientName.toLowerCase().includes(this.worklistSearch.toLowerCase());
            const matchesAnalyzer = this.worklistAnalyzerFilter === 'Всі прилади' || r.analyzer.includes(this.worklistAnalyzerFilter);
            const matchesFlag = this.worklistFlagFilter === 'Всі результати' ||
              (this.worklistFlagFilter.includes('паніка') && r.flag !== 'NORMAL') ||
              (this.worklistFlagFilter === 'Нормальні' && r.flag === 'NORMAL');
            return matchesQuery && matchesAnalyzer && matchesFlag;
          });
        },
        pendingValidationList() {
          return this.worklist.filter(r => r.status === 'PENDING_VERIFY');
        },
        activeOrderSamples() {
          if (!this.activeOrder) return [];
          return this.samplesList.filter(s => s.orderId === this.activeOrder.id);
        }
      },

      methods: {
        setView(viewName, title) {
          this.currentView = viewName;
          this.currentViewTitle = title;
        },
        notify(msg, color = 'positive') {
          if (this.$q && this.$q.notify) {
            this.$q.notify({ message: msg, color: color, position: 'top-right', timeout: 2500 });
          } else if (window.Quasar && window.Quasar.Notify) {
            window.Quasar.Notify.create({ message: msg, color: color, position: 'top-right', timeout: 2500 });
          }
        },
        testPing(row) {
          this.notify(`Зв’язок з ${row.name}: OK (Ping 4ms, ACK отримано)`);
        },
        getValueClass(flag) {
          if (flag === 'PANIC_HIGH' || flag === 'PANIC_LOW') return 'text-negative text-weight-bolder bg-red-1';
          if (flag === 'DELTA_ALERT') return 'text-deep-orange text-weight-bold';
          return 'text-dark';
        },
        getFlagColor(flag) {
          switch (flag) {
            case 'PANIC_HIGH':
            case 'PANIC_LOW': return 'negative';
            case 'DELTA_ALERT': return 'deep-orange';
            case 'NORMAL': return 'positive';
            default: return 'grey';
          }
        },
        formatFlag(flag) {
          switch (flag) {
            case 'PANIC_HIGH': return 'КРИТИЧНО ВИСОКИЙ';
            case 'PANIC_LOW': return 'КРИТИЧНО НИЗЬКИЙ';
            case 'DELTA_ALERT': return 'DELTA УВАГА';
            case 'NORMAL': return 'НОРМА';
            default: return flag;
          }
        },
        getOrderStatusColor(st) {
          switch (st) {
            case 'IN_PROGRESS': return 'blue-7';
            case 'PANIC_ALERT': return 'negative';
            case 'VERIFIED': return 'positive';
            default: return 'grey-6';
          }
        },
        formatOrderStatus(st) {
          switch (st) {
            case 'IN_PROGRESS': return 'В роботі';
            case 'PANIC_ALERT': return 'Паніка!';
            case 'VERIFIED': return 'Завершено';
            default: return st;
          }
        },
        runAutoValidation() {
          let count = 0;
          this.worklist.forEach(r => {
            if (r.flag === 'NORMAL' && r.status === 'PENDING_VERIFY') {
              r.status = 'AUTO_VERIFIED';
              count++;
            }
          });
          this.notify(`Автоматично валідовано ${count} нормальних результатів за критеріями CLSI.`);
        },
        openBarcodeDialog(order) {
          this.activeOrder = order;
          this.showBarcodeDialog = true;
        },
        openCollectDialog(order) {
          this.activeOrder = order;
          this.showCollectDialog = true;
        },
        confirmPrintZpl() {
          this.notify(`Друк етикеток ZPL успішно відправлено на принтер для ${this.activeOrderSamples.length} пробірок.`);
          this.showBarcodeDialog = false;
        },
        confirmCollect() {
          if (this.activeOrder) this.activeOrder.status = 'IN_PROGRESS';
          this.notify('Забір біоматеріалу успішно зареєстровано.');
          this.showCollectDialog = false;
        },
        saveCall() {
          this.notify('Дзвінок зафіксовано в журналі передачі критичних значень.');
          this.showCallDialog = false;
        },
        validateRow(row) {
          row.status = 'VERIFIED';
          this.notify(`Результат ${row.testName} валідовано лікарем.`);
        },
        openEditResult(row) {
          this.editingRow = { ...row };
          this.showEditResultDialog = true;
        },
        saveEditedRow() {
          const idx = this.worklist.findIndex(r => r.id === this.editingRow.id);
          if (idx !== -1) {
            this.worklist.splice(idx, 1, this.editingRow);
          }
          this.notify('Результат збережено');
          this.showEditResultDialog = false;
        },
        confirmQcUnlock() {
          this.notify('Аналізатор успішно розблоковано. Коригувальну дію записано.');
          this.showQcUnlockDialog = false;
        }
      }
    });
  </script>
</body>
</html>
"""

with open(RUN_HTML_PATH, "w", encoding="utf-8") as f:
    f.write(prototype_html)
print("Updated:", RUN_HTML_PATH)

with open(INDEX_HTML_PATH, "w", encoding="utf-8") as f:
    f.write(prototype_html)
print("Updated:", INDEX_HTML_PATH)
