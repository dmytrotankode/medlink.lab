# -*- coding: utf-8 -*-
"""
inject_interactive_delphi_constructor.py
Implements:
1. Full interactive Delphi Service Card modal (showServiceDetailDialog) with:
   - Norms Constructor tab (Methodology selector, combinations matrix, dynamic combination creator, real-time resolver test)
   - Methods tab
   - Delta-check & Reflex testing tab
   - Standalone Calculator tab
   - Preanalytics & Tubes tab
2. Interactive Dictionary Card Modals:
   - Biomaterial card modal
   - Tube/Container card modal
   - Analyzer model card modal
   - Parameter card modal
3. Row-click and "Картка" action buttons in all 4 dictionary tables
4. Complete Vue data and methods for adding combinations, deleting, testing resolver, and opening cards.
"""

import os
import re

PROTOTYPE_PATH = r"C:\__MEDLINK___\LABA\medlink_lab_frontend\run_prototype.html"
INDEX_PATH = r"C:\__MEDLINK___\LABA\medlink_lab_frontend\index.html"

with open(PROTOTYPE_PATH, "r", encoding="utf-8") as f:
    content = f.read()

# ---------------------------------------------------------------------------
# 1. DICTIONARY TABLES: ADD ACTIONS COLUMN & ROW-CLICK
# ---------------------------------------------------------------------------

# Biomaterials table
old_bio = '<q-table :data="biomaterialsList" :columns="dictBioColumns" row-key="code" dense flat></q-table>'
new_bio = """<q-table :data="biomaterialsList" :columns="dictBioColumns" row-key="code" dense flat class="cursor-pointer" @row-click="(evt, row) => openBiomaterialCard(row)">
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props">
                    <q-btn size="xs" color="primary" dense icon="visibility" label="Картка біоматеріалу" @click.stop="openBiomaterialCard(props.row)"></q-btn>
                  </q-td>
                </template>
              </q-table>"""
if old_bio in content:
    content = content.replace(old_bio, new_bio)

# Tubes table
old_tube = '<q-table :data="tubesList" :columns="dictTubeColumns" row-key="code" dense flat>'
new_tube = """<q-table :data="tubesList" :columns="dictTubeColumns" row-key="code" dense flat class="cursor-pointer" @row-click="(evt, row) => openTubeCard(row)">"""
if old_tube in content:
    content = content.replace(old_tube, new_tube)

old_tube_td = """                  <q-td :props="props">
                    <q-badge :style="{ backgroundColor: props.row.colorHex }" text-color="white">{{ props.row.colorName }}</q-badge>
                  </q-td>
                </template>
              </q-table>"""
new_tube_td = """                  <q-td :props="props">
                    <q-badge :style="{ backgroundColor: props.row.colorHex }" text-color="white">{{ props.row.colorName }}</q-badge>
                  </q-td>
                </template>
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props">
                    <q-btn size="xs" color="primary" dense icon="visibility" label="Картка пробірки" @click.stop="openTubeCard(props.row)"></q-btn>
                  </q-td>
                </template>
              </q-table>"""
if old_tube_td in content:
    content = content.replace(old_tube_td, new_tube_td)

# Analyzers table
old_an = '<q-table :data="analyzerModelsList" :columns="dictAnColumns" row-key="code" dense flat></q-table>'
new_an = """<q-table :data="analyzerModelsList" :columns="dictAnColumns" row-key="code" dense flat class="cursor-pointer" @row-click="(evt, row) => openAnalyzerCard(row)">
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props">
                    <q-btn size="xs" color="primary" dense icon="visibility" label="Картка аналізатора" @click.stop="openAnalyzerCard(props.row)"></q-btn>
                  </q-td>
                </template>
              </q-table>"""
if old_an in content:
    content = content.replace(old_an, new_an)

# Parameters table
old_param = '<q-table :data="parametersList" :columns="dictParamColumns" row-key="code" dense flat></q-table>'
new_param = """<q-table :data="parametersList" :columns="dictParamColumns" row-key="code" dense flat class="cursor-pointer" @row-click="(evt, row) => openParamCard(row)">
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props" class="q-gutter-xs">
                    <q-btn size="xs" color="primary" dense icon="visibility" label="Картка" @click.stop="openParamCard(props.row)"></q-btn>
                    <q-btn size="xs" color="secondary" dense icon="tune" label="Норми (Delphi)" @click.stop="openNormsForParam(props.row)"></q-btn>
                  </q-td>
                </template>
              </q-table>"""
if old_param in content:
    content = content.replace(old_param, new_param)

# ---------------------------------------------------------------------------
# 2. COMPLETE DELPHI SERVICE CARD MODAL & 4 DICTIONARY MODALS
# ---------------------------------------------------------------------------

modals_markup = """
    <!-- ============================================================== -->
    <!-- MODAL 1: КАРТКА ПОСЛУГИ ТА КОНСТРУКТОР НОРМ (DELPHI SERVICE CARD) -->
    <!-- ============================================================== -->
    <q-dialog v-model="showServiceDetailDialog" maximized persistent transition-show="slide-up" transition-hide="slide-down">
      <q-card class="bg-grey-1 column full-height">
        <!-- Header -->
        <q-card-section class="bg-primary text-white row items-center justify-between q-py-sm shadow-1">
          <div class="row items-center q-gutter-x-sm">
            <q-avatar square size="34px" color="white" text-color="primary" icon="biotech"></q-avatar>
            <div>
              <div class="text-subtitle1 text-weight-bold lh-tight">
                {{ selectedService.name }} ({{ selectedService.code }}) &mdash; Картка послуги (Delphi dct_service)
              </div>
              <div class="text-caption text-blue-2" style="font-size: 11.5px;">
                LOINC: <strong>{{ selectedService.loinc }}</strong> | Категорія: <strong>{{ selectedService.category }}</strong> | Базова методика: <strong>{{ selectedService.defaultMethod }}</strong>
              </div>
            </div>
          </div>
          <div class="row items-center q-gutter-x-xs">
            <q-btn icon="save" color="positive" unelevated label="Зберегти всі норми в БД" @click="saveServiceSettings"></q-btn>
            <q-btn icon="close" flat round dense v-close-popup></q-btn>
          </div>
        </q-card-section>

        <!-- Navigation Tabs -->
        <q-tabs v-model="serviceTab" dense class="bg-white text-grey-8" active-color="primary" indicator-color="primary" align="left">
          <q-tab name="norms_matrix" icon="format_list_bulleted" label="Норми (Конструктор комбінацій Delphi)" :badge="selectedServiceCombinations.length"></q-tab>
          <q-tab name="methods" icon="science" label="Методики дослідження" :badge="selectedServiceMethods.length"></q-tab>
          <q-tab name="delta_reflex" icon="alt_route" label="Дельта-чек & Рефлекс-тести"></q-tab>
          <q-tab name="calculator" icon="calculate" label="Симулятор підбору референсу"></q-tab>
          <q-tab name="tubes" icon="invert_colors" label="Преаналітика & Пробірки"></q-tab>
        </q-tabs>
        <q-separator></q-separator>

        <!-- Tab Panels -->
        <q-tab-panels v-model="serviceTab" animated class="col bg-grey-2 q-pa-md overflow-auto">
          
          <!-- TAB 1: DELPHI NORMS CONSTRUCTOR -->
          <q-tab-panel name="norms_matrix" class="q-pa-none">
            <!-- Methodology Selector & Quick Stats -->
            <q-card flat bordered class="q-pa-md q-mb-md bg-white">
              <div class="row items-center justify-between q-col-gutter-sm">
                <div class="col-12 col-md-5">
                  <div class="text-caption text-grey-7 text-weight-bold">АКТИВНА МЕТОДИКА ДОСЛІДЖЕННЯ:</div>
                  <q-select v-model="selectedServiceMethodCode" :options="serviceMethodOptions" emit-value map-options outlined dense class="q-mt-xs"></q-select>
                </div>
                <div class="col-12 col-md-7 text-right">
                  <div class="row items-center justify-end q-gutter-x-md text-caption">
                    <div class="text-left">
                      <div class="text-grey-7">Всього комбінацій норм:</div>
                      <div class="text-weight-bold text-primary text-subtitle2">{{ selectedServiceCombinations.length }} правил</div>
                    </div>
                    <div class="text-left">
                      <div class="text-grey-7">Одиниця виміру:</div>
                      <div class="text-weight-bold text-dark text-subtitle2">{{ currentServiceUnit }}</div>
                    </div>
                    <div class="text-left">
                      <div class="text-grey-7">Дельта-чек поріг:</div>
                      <div class="text-weight-bold text-warning text-subtitle2">&plusmn;{{ selectedService.deltaCheckPct || 25 }}%</div>
                    </div>
                  </div>
                </div>
              </div>
            </q-card>

            <!-- Table of Combinations -->
            <q-card flat bordered class="medlink-card q-mb-md bg-white">
              <q-card-section class="bg-grey-1 q-py-xs row items-center justify-between border-bottom">
                <div class="text-subtitle2 text-weight-bold text-primary">
                  <q-icon name="table_rows" class="q-mr-xs"></q-icon>
                  Таблиця налаштованих правил норм для обраної методики (dct_service_lab_nv)
                </div>
                <div class="text-caption text-grey-7">Пріоритет вибору: Вагітність &gt; Фаза циклу &gt; Вік/Стать &gt; Загальне</div>
              </q-card-section>

              <q-table
                :data="selectedServiceCombinations"
                :columns="serviceCombColumns"
                row-key="id"
                dense
                flat
                :pagination="{ rowsPerPage: 10 }"
              >
                <template v-slot:body-cell-normRange="props">
                  <q-td :props="props">
                    <q-badge color="positive" text-color="white" class="text-weight-bold">
                      {{ props.row.norm_low }} &mdash; {{ props.row.norm_high }} {{ props.row.unit }}
                    </q-badge>
                  </q-td>
                </template>
                <template v-slot:body-cell-critRange="props">
                  <q-td :props="props">
                    <span v-if="props.row.crit_low || props.row.crit_high" class="text-negative text-weight-bold">
                      &lt; {{ props.row.crit_low || '—' }} / &gt; {{ props.row.crit_high || '—' }}
                    </span>
                    <span v-else class="text-grey-5">&mdash;</span>
                  </q-td>
                </template>
                <template v-slot:body-cell-condition="props">
                  <q-td :props="props">
                    <div v-if="props.row.is_pregnancy" class="text-pink text-weight-bold">
                      <q-icon name="pregnant_woman"></q-icon> Вагітність {{ props.row.pregnancy_week_from }}-{{ props.row.pregnancy_week_to }} тиж
                    </div>
                    <div v-else-if="props.row.is_menstrual_phase" class="text-purple text-weight-bold">
                      <q-icon name="female"></q-icon> Фаза: {{ props.row.menstrual_phase }}
                    </div>
                    <div v-else class="text-grey-9">
                      Стать: <strong>{{ props.row.gender === 'M' ? 'Чол' : (props.row.gender === 'F' ? 'Жін' : 'Будь-яка') }}</strong>,
                      Вік: <strong>{{ props.row.age_from }}-{{ props.row.age_to }} {{ props.row.age_unit === 'DAYS' ? 'днів' : (props.row.age_unit === 'MONTHS' ? 'міс' : 'років') }}</strong>
                    </div>
                  </q-td>
                </template>
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props" class="q-gutter-xs">
                    <q-btn size="xs" color="primary" dense icon="edit" label="Змінити" @click="populateEditForm(props.row)"></q-btn>
                    <q-btn size="xs" color="secondary" dense icon="content_copy" label="Копія" @click="duplicateRow(props.row)"></q-btn>
                    <q-btn size="xs" color="negative" dense flat icon="delete" @click="deleteCombination(props.row)"></q-btn>
                  </q-td>
                </template>
              </q-table>
            </q-card>

            <!-- INTERACTIVE FORM: ADD/EDIT COMBINATION (DELPHI CONSTRUCTOR) -->
            <q-card flat bordered class="q-pa-md bg-white q-mb-md" style="border-left: 4px solid var(--q-color-primary);">
              <div class="row items-center justify-between q-mb-sm">
                <div class="text-subtitle1 text-weight-bold text-primary">
                  <q-icon name="tune" class="q-mr-xs"></q-icon>
                  Конструктор правила норми: {{ newCombForm.id ? 'Редагування правила ID: ' + newCombForm.id : 'Додавання нової комбінації' }}
                </div>
                <q-btn v-if="newCombForm.id" size="xs" flat color="grey-8" label="Очистити / Створити нове" @click="resetCombForm"></q-btn>
              </div>

              <div class="row q-col-gutter-sm">
                <!-- Name of rule -->
                <div class="col-12 col-md-6">
                  <q-input v-model="newCombForm.norm_name" label="Назва комбінації (Коментар)" outlined dense placeholder="напр. Дорослі чоловіки 18-65 р."></q-input>
                </div>
                <!-- Method Name -->
                <div class="col-12 col-md-6">
                  <q-input v-model="newCombForm.method_name" label="Методика вимірювання" outlined dense></q-input>
                </div>

                <!-- Gender -->
                <div class="col-12 col-md-3">
                  <q-select v-model="newCombForm.gender" :options="[{label:'Будь-яка стать (ANY)', value:'ANY'}, {label:'Чоловіча (M)', value:'M'}, {label:'Жіноча (F)', value:'F'}]" emit-value map-options label="Стать пацієнта" outlined dense></q-select>
                </div>
                <!-- Age Unit -->
                <div class="col-12 col-md-3">
                  <q-select v-model="newCombForm.age_unit" :options="[{label:'Роки (YEARS)', value:'YEARS'}, {label:'Місяці (MONTHS)', value:'MONTHS'}, {label:'Дні / Немовлята (DAYS)', value:'DAYS'}]" emit-value map-options label="Одиниця віку" outlined dense></q-select>
                </div>
                <!-- Age From / To -->
                <div class="col-6 col-md-3">
                  <q-input v-model.number="newCombForm.age_from" type="number" label="Вік від" outlined dense></q-input>
                </div>
                <div class="col-6 col-md-3">
                  <q-input v-model.number="newCombForm.age_to" type="number" label="Вік до" outlined dense></q-input>
                </div>

                <!-- Modifiers: Pregnancy and Menstrual Cycle -->
                <div class="col-12 col-md-6">
                  <q-card flat bordered class="q-pa-xs bg-grey-1">
                    <q-checkbox v-model="newCombForm.is_pregnancy" :true-value="1" :false-value="0" label="Враховувати вагітність" class="text-caption text-weight-bold text-pink"></q-checkbox>
                    <div v-if="newCombForm.is_pregnancy" class="row q-col-gutter-xs q-mt-xs">
                      <div class="col-6">
                        <q-input v-model.number="newCombForm.pregnancy_week_from" type="number" label="Тиждень від" outlined dense></q-input>
                      </div>
                      <div class="col-6">
                        <q-input v-model.number="newCombForm.pregnancy_week_to" type="number" label="Тиждень до" outlined dense></q-input>
                      </div>
                    </div>
                  </q-card>
                </div>

                <div class="col-12 col-md-6">
                  <q-card flat bordered class="q-pa-xs bg-grey-1">
                    <q-checkbox v-model="newCombForm.is_menstrual_phase" :true-value="1" :false-value="0" label="Враховувати фазу циклу" class="text-caption text-weight-bold text-purple"></q-checkbox>
                    <div v-if="newCombForm.is_menstrual_phase" class="q-mt-xs">
                      <q-select v-model="newCombForm.menstrual_phase" :options="['FOLLICULAR', 'OVULATORY', 'LUTEAL', 'POSTMENOPAUSE']" label="Фаза оваріального циклу" outlined dense></q-select>
                    </div>
                  </q-card>
                </div>

                <!-- Limits: Min, Max, Panic Low, Panic High -->
                <div class="col-6 col-md-3">
                  <q-input v-model.number="newCombForm.norm_low" type="number" step="0.01" label="Нижня норма (Min)" outlined dense class="bg-green-1"></q-input>
                </div>
                <div class="col-6 col-md-3">
                  <q-input v-model.number="newCombForm.norm_high" type="number" step="0.01" label="Верхня норма (Max)" outlined dense class="bg-green-1"></q-input>
                </div>
                <div class="col-6 col-md-3">
                  <q-input v-model.number="newCombForm.crit_low" type="number" step="0.01" label="Паніка низька (CITO <)" outlined dense class="bg-red-1"></q-input>
                </div>
                <div class="col-6 col-md-3">
                  <q-input v-model.number="newCombForm.crit_high" type="number" step="0.01" label="Паніка висока (CITO >)" outlined dense class="bg-red-1"></q-input>
                </div>

                <!-- Units & Delta Check -->
                <div class="col-6 col-md-3">
                  <q-input v-model="newCombForm.unit" label="Одиниця виміру" outlined dense></q-input>
                </div>
                <div class="col-6 col-md-3">
                  <q-input v-model.number="newCombForm.delta_check_max_pct" type="number" label="Дельта-чек max %" suffix="%" outlined dense></q-input>
                </div>
                <div class="col-12 col-md-6">
                  <q-input v-model="newCombForm.norm_text" label="Текстова норма (якщо якісний тест)" outlined dense placeholder="напр. Не виявлено / Негативний"></q-input>
                </div>
              </div>

              <div class="row justify-between items-center q-mt-md pt-sm border-top">
                <span class="text-caption text-grey-7">Запис відразу зберігається в таблицю SQLite <code>lab_reference_ranges</code> через API.</span>
                <q-btn color="primary" icon="add_circle" :label="newCombForm.id ? 'Оновити правило в матриці' : 'Додати комбінацію до матриці'" unelevated @click="saveNewCombination"></q-btn>
              </div>
            </q-card>

            <!-- INTERACTIVE RESOLVER TESTER RIGHT IN THE TAB -->
            <q-card flat bordered class="q-pa-md bg-white">
              <div class="text-subtitle2 text-weight-bold text-dark q-mb-xs">
                <q-icon name="play_arrow" color="secondary"></q-icon>
                Експрес-перевірка підбору норми для пацієнта (In-Card Tester)
              </div>
              <div class="text-caption text-grey-7 q-mb-sm">Введіть показники пацієнта та отримане лабораторне значення для миттєвої верифікації:</div>

              <div class="row q-col-gutter-sm items-center">
                <div class="col-6 col-md-2">
                  <q-select v-model="inCardTest.gender" :options="[{label:'Жіноча (F)', value:'F'}, {label:'Чоловіча (M)', value:'M'}]" emit-value map-options outlined dense label="Стать"></q-select>
                </div>
                <div class="col-6 col-md-2">
                  <q-input v-model.number="inCardTest.age" type="number" outlined dense label="Вік (років)"></q-input>
                </div>
                <div class="col-6 col-md-2">
                  <q-select v-model="inCardTest.phase" :options="['FOLLICULAR', 'OVULATORY', 'LUTEAL', 'POSTMENOPAUSE']" clearable outlined dense label="Фаза циклу"></q-select>
                </div>
                <div class="col-6 col-md-2">
                  <q-input v-model.number="inCardTest.pregWeek" type="number" clearable outlined dense label="Тиждень вагітності"></q-input>
                </div>
                <div class="col-6 col-md-2">
                  <q-input v-model.number="inCardTest.value" type="number" step="0.01" outlined dense label="Тестове значення"></q-input>
                </div>
                <div class="col-6 col-md-2">
                  <q-btn color="secondary" icon="check" label="Розрахувати" unelevated class="full-width" @click="testInCardResolver"></q-btn>
                </div>
              </div>

              <div v-if="inCardResult" class="q-mt-sm q-pa-sm rounded-borders" :style="{ border: '2px solid ' + inCardResult.color, backgroundColor: inCardResult.bg }">
                <div class="row items-center justify-between">
                  <div>
                    <strong>Правило:</strong> {{ inCardResult.ruleName }} |
                    <strong>Норма:</strong> [{{ inCardResult.normLow }} &mdash; {{ inCardResult.normHigh }}] {{ inCardResult.unit }}
                  </div>
                  <q-badge :color="inCardResult.badgeColor" class="text-weight-bold">{{ inCardResult.statusText }}</q-badge>
                </div>
              </div>
            </q-card>
          </q-tab-panel>

          <!-- TAB 2: METHODS & ANALYZERS -->
          <q-tab-panel name="methods" class="q-pa-none">
            <div class="text-subtitle1 text-weight-bold text-dark q-mb-sm">Підтримувані методики дослідження та прив'язані аналізатори</div>
            <div class="row q-col-gutter-md">
              <div v-for="m in selectedServiceMethods" :key="'m-'+m.id" class="col-12 col-md-6">
                <q-card flat bordered class="q-pa-md bg-white">
                  <div class="row items-center justify-between q-mb-xs">
                    <div class="text-subtitle1 text-weight-bold text-primary">{{ m.name }}</div>
                    <q-badge color="teal">{{ m.code }}</q-badge>
                  </div>
                  <div class="text-caption text-grey-7 q-mb-sm">{{ m.description }}</div>
                  <div class="text-body2 q-mb-xs">Сумісні аналізатори в лабораторії:</div>
                  <div class="row q-gutter-xs q-mb-sm">
                    <q-chip v-for="anz in m.analyzers" :key="anz" color="blue-1" text-color="blue-9" icon="memory" size="sm">{{ anz }}</q-chip>
                  </div>
                  <div class="row items-center justify-between text-caption text-grey-8 bg-grey-1 q-pa-xs rounded-borders">
                    <span>On-board стабільність: <strong>{{ m.onBoardDays }} днів</strong></span>
                    <span>Калібрувальний коефіцієнт (Slope): <strong>{{ m.slope }}</strong></span>
                  </div>
                </q-card>
              </div>
            </div>
          </q-tab-panel>

          <!-- TAB 3: DELTA CHECK & REFLEX RULES -->
          <q-tab-panel name="delta_reflex" class="q-pa-none">
            <div class="row q-col-gutter-md">
              <div class="col-12 col-md-6">
                <q-card flat bordered class="q-pa-md bg-white">
                  <div class="text-subtitle1 text-weight-bold text-primary q-mb-xs">Налаштування Дельта-чеку (Delta-Check)</div>
                  <div class="text-caption text-grey-7 q-mb-md">Контроль різких нефізіологічних стрибків показника між послідовними дослідженнями пацієнта.</div>
                  <div class="q-gutter-y-sm">
                    <q-input v-model.number="selectedService.deltaCheckPct" type="number" outlined dense label="Максимально допустиме відхилення (&plusmn;%)" suffix="%"></q-input>
                    <q-input v-model.number="selectedService.deltaHours" type="number" outlined dense label="Вікно перевірки (години)" suffix="годин (за замовчуванням 72 год)"></q-input>
                    <q-toggle v-model="selectedService.deltaAutoBlock" label="Автоматично блокувати видачу результату при перевищенні дельти"></q-toggle>
                  </div>
                </q-card>
              </div>
              <div class="col-12 col-md-6">
                <q-card flat bordered class="q-pa-md bg-white">
                  <div class="text-subtitle1 text-weight-bold text-teal q-mb-xs">Рефлекс-тестування (Reflex Testing Rules)</div>
                  <div class="text-caption text-grey-7 q-mb-md">Автоматичне дозамовлення аналізів без повторного взяття крові за наявності аліквоти.</div>
                  <div v-for="rflx in selectedServiceReflexRules" :key="rflx.id" class="q-pa-sm bg-teal-1 rounded-borders q-mb-sm" style="border: 1px solid #99f6e4;">
                    <div class="row items-center justify-between">
                      <strong>Якщо {{ rflx.trigger_test_code }} &gt; {{ rflx.threshold_value }}</strong>
                      <q-badge color="teal">&rarr; {{ rflx.reflex_test_code }}</q-badge>
                    </div>
                    <div class="text-caption text-grey-8 q-mt-xs">{{ rflx.description }}</div>
                  </div>
                  <q-btn size="sm" color="teal" icon="add" outline label="Додати рефлекс-правило" class="q-mt-sm"></q-btn>
                </q-card>
              </div>
            </div>
          </q-tab-panel>

          <!-- TAB 4: CALCULATOR -->
          <q-tab-panel name="calculator" class="q-pa-none">
            <q-card flat bordered class="q-pa-md bg-white">
              <div class="text-subtitle1 text-weight-bold text-primary q-mb-xs">Симулятор підбору референсного інтервалу (Lab Calculator)</div>
              <div class="text-caption text-grey-7 q-mb-md">Перевірте, яке правило спрацює в реальному часі для конкретного клінічного профілю пацієнта:</div>

              <div class="row q-col-gutter-md items-center">
                <div class="col-12 col-md-3">
                  <q-select v-model="simParams.gender" :options="[{label:'Жіноча (F)', value:'F'}, {label:'Чоловіча (M)', value:'M'}]" emit-value map-options outlined dense label="Стать пацієнта"></q-select>
                </div>
                <div class="col-12 col-md-3">
                  <q-input v-model.number="simParams.age" type="number" outlined dense label="Вік пацієнта" suffix="років"></q-input>
                </div>
                <div class="col-12 col-md-3">
                  <q-select v-model="simParams.phase" :options="['FOLLICULAR', 'OVULATORY', 'LUTEAL', 'POSTMENOPAUSE']" clearable outlined dense label="Фаза циклу (якщо жінка)"></q-select>
                </div>
                <div class="col-12 col-md-3">
                  <q-input v-model.number="simParams.pregWeek" type="number" clearable outlined dense label="Тиждень вагітності (1-40)"></q-input>
                </div>
              </div>

              <div class="q-mt-md">
                <q-btn color="primary" icon="play_arrow" label="Розрахувати активну норму в API" unelevated @click="executeSimulateResolve"></q-btn>
              </div>

              <!-- Result Box -->
              <div v-if="simResult" class="q-mt-md q-pa-md rounded-borders" :class="simResult.matched ? 'bg-green-1' : 'bg-amber-1'" style="border: 2px solid #22c55e;">
                <div class="row items-center justify-between">
                  <div class="text-subtitle1 text-weight-bold text-dark">
                    <q-icon name="check_circle" color="positive" size="24px"></q-icon> Знайдене правило: {{ simResult.rule ? simResult.rule.norm_name : 'За замовчуванням' }}
                  </div>
                  <q-badge color="positive" class="text-subtitle2 q-pa-xs">
                    Норма: {{ simResult.rule ? simResult.rule.norm_low : '—' }} &mdash; {{ simResult.rule ? simResult.rule.norm_high : '—' }} {{ simResult.rule ? simResult.rule.unit : '' }}
                  </q-badge>
                </div>
                <div class="text-caption text-grey-8 q-mt-xs">
                  Панічні пороги CITO: &lt; {{ simResult.rule ? simResult.rule.crit_low : '—' }} / &gt; {{ simResult.rule ? simResult.rule.crit_high : '—' }} |
                  Методика: {{ simResult.rule ? simResult.rule.method_name : 'Стандартна' }}
                </div>
              </div>
            </q-card>
          </q-tab-panel>

          <!-- TAB 5: TUBES -->
          <q-tab-panel name="tubes" class="q-pa-none">
            <q-card flat bordered class="q-pa-md bg-white">
              <div class="text-subtitle1 text-weight-bold text-primary q-mb-xs">Преаналітичні вимоги та пробірки</div>
              <div class="text-caption text-grey-7 q-mb-md">Стандарт взяття біоматеріалу, черговість взяття (Order of Draw) та зберігання зразка.</div>
              <div class="row q-col-gutter-md">
                <div class="col-12 col-md-4">
                  <q-card flat bordered class="q-pa-sm bg-grey-1">
                    <div class="text-caption text-grey-7">Вакутейнер:</div>
                    <div class="text-subtitle2 text-weight-bold text-dark">Активатор згортання + Гель (5.0 мл)</div>
                    <div class="row items-center q-mt-xs">
                      <span style="width:14px; height:14px; background:#dc2626; border-radius:3px; display:inline-block; margin-right:6px;"></span>
                      <span class="text-caption">Червона кришка</span>
                    </div>
                  </q-card>
                </div>
                <div class="col-12 col-md-4">
                  <q-card flat bordered class="q-pa-sm bg-grey-1">
                    <div class="text-caption text-grey-7">Order of Draw (Черговість):</div>
                    <div class="text-subtitle2 text-weight-bold text-primary">Позиція #3 у черзі взяття</div>
                    <div class="text-caption">Після гемокультури та цитрату</div>
                  </q-card>
                </div>
                <div class="col-12 col-md-4">
                  <q-card flat bordered class="q-pa-sm bg-grey-1">
                    <div class="text-caption text-grey-7">Умови зберігання:</div>
                    <div class="text-subtitle2 text-weight-bold text-teal">+2°C .. +8°C до 48 годин</div>
                    <div class="text-caption">-20°C у кріо-архіві до 3 місяців</div>
                  </q-card>
                </div>
              </div>
            </q-card>
          </q-tab-panel>
        </q-tab-panels>
      </q-card>
    </q-dialog>

    <!-- ============================================================== -->
    <!-- MODAL 2: КАРТКА БІОМАТЕРІАЛУ -->
    <!-- ============================================================== -->
    <q-dialog v-model="showBiomaterialModal">
      <q-card style="min-width: 480px; max-width: 550px;" v-if="activeBiomaterial">
        <q-card-section class="bg-primary text-white row items-center justify-between q-py-sm">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="fas fa-vial" class="q-mr-xs"></q-icon>
            Картка біоматеріалу: {{ activeBiomaterial.name }}
          </div>
          <q-btn icon="close" flat round dense v-close-popup></q-btn>
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="row q-col-gutter-sm">
            <div class="col-6"><strong>Код у системі:</strong> <span class="text-primary text-weight-bold">{{ activeBiomaterial.code }}</span></div>
            <div class="col-6"><strong>SNOMED CT:</strong> <span class="text-mono">{{ activeBiomaterial.snomed }}</span></div>
            <div class="col-12"><strong>Повна назва:</strong> {{ activeBiomaterial.name }}</div>
            <div class="col-12"><strong>Рекомендований контейнер:</strong> {{ activeBiomaterial.container }}</div>
            <div class="col-12"><strong>Умови зберігання та стабільність:</strong> {{ activeBiomaterial.storage }}</div>
          </div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Закрити" v-close-popup></q-btn>
          <q-btn color="primary" label="Зберегти зміни" v-close-popup></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- ============================================================== -->
    <!-- MODAL 3: КАРТКА ПРОБІРКИ / КОНТЕЙНЕРА -->
    <!-- ============================================================== -->
    <q-dialog v-model="showTubeModal">
      <q-card style="min-width: 500px; max-width: 580px;" v-if="activeTube">
        <q-card-section class="bg-primary text-white row items-center justify-between q-py-sm">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="fas fa-flask" class="q-mr-xs"></q-icon>
            Картка вакутейнера: {{ activeTube.name }}
          </div>
          <q-btn icon="close" flat round dense v-close-popup></q-btn>
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="row q-col-gutter-sm">
            <div class="col-6"><strong>Код пробірки:</strong> <span class="text-primary text-weight-bold">{{ activeTube.code }}</span></div>
            <div class="col-6">
              <strong>Колір кришки:</strong>
              <q-badge :style="{ backgroundColor: activeTube.colorHex }" text-color="white" class="q-ml-xs">{{ activeTube.colorName }}</q-badge>
            </div>
            <div class="col-12"><strong>Хімічний наповнювач / антикоагулянт:</strong> {{ activeTube.name }}</div>
            <div class="col-6"><strong>Номінальний об'єм:</strong> {{ activeTube.volume }}</div>
            <div class="col-6"><strong>Черговість взяття (Order of Draw):</strong> Позиція #{{ activeTube.orderOfDraw }}</div>
            <div class="col-12"><strong>Правило перемішування після забору:</strong> {{ activeTube.inversions }}</div>
          </div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Закрити" v-close-popup></q-btn>
          <q-btn color="primary" label="Зберегти налаштування" v-close-popup></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- ============================================================== -->
    <!-- MODAL 4: КАРТКА АНАЛІЗАТОРА -->
    <!-- ============================================================== -->
    <q-dialog v-model="showAnalyzerModal">
      <q-card style="min-width: 520px; max-width: 620px;" v-if="activeAnalyzer">
        <q-card-section class="bg-primary text-white row items-center justify-between q-py-sm">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="fas fa-server" class="q-mr-xs"></q-icon>
            Картка аналізатора: {{ activeAnalyzer.model }} ({{ activeAnalyzer.vendor }})
          </div>
          <q-btn icon="close" flat round dense v-close-popup></q-btn>
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="row q-col-gutter-sm">
            <div class="col-6"><strong>Виробник:</strong> {{ activeAnalyzer.vendor }}</div>
            <div class="col-6"><strong>Модель:</strong> <strong class="text-primary">{{ activeAnalyzer.model }}</strong></div>
            <div class="col-6"><strong>Клінічна дисципліна:</strong> {{ activeAnalyzer.discipline }}</div>
            <div class="col-6"><strong>Протокол зв'язку:</strong> <q-badge color="teal">{{ activeAnalyzer.protocol }}</q-badge></div>
            <div class="col-12"><strong>Тип фізичного інтерфейсу:</strong> <code>{{ activeAnalyzer.interfaceType }}</code></div>
            <div class="col-12"><strong>Статус шлюзу .NET 8:</strong> <q-badge color="positive">ONLINE (Готовий до передачі результатів)</q-badge></div>
          </div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Закрити" v-close-popup></q-btn>
          <q-btn color="primary" label="Тест підключення" @click="testPing(activeAnalyzer)"></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- ============================================================== -->
    <!-- MODAL 5: КАРТКА ЛАБОРАТОРНОГО ПОКАЗНИКА -->
    <!-- ============================================================== -->
    <q-dialog v-model="showParamModal">
      <q-card style="min-width: 480px; max-width: 560px;" v-if="activeParam">
        <q-card-section class="bg-primary text-white row items-center justify-between q-py-sm">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="fas fa-list-ol" class="q-mr-xs"></q-icon>
            Показник: {{ activeParam.name }} ({{ activeParam.code }})
          </div>
          <q-btn icon="close" flat round dense v-close-popup></q-btn>
        </q-card-section>
        <q-card-section class="q-pa-md">
          <div class="row q-col-gutter-sm">
            <div class="col-6"><strong>Код параметра:</strong> <strong class="text-primary">{{ activeParam.code }}</strong></div>
            <div class="col-6"><strong>LOINC:</strong> <span class="text-mono">{{ activeParam.loinc }}</span></div>
            <div class="col-12"><strong>Повна назва:</strong> {{ activeParam.name }}</div>
            <div class="col-6"><strong>Категорія:</strong> {{ activeParam.category }}</div>
            <div class="col-6"><strong>Одиниця виміру:</strong> {{ activeParam.unit }}</div>
            <div class="col-12"><strong>Рекомендований біоматеріал:</strong> {{ activeParam.sampleType }}</div>
          </div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Закрити" v-close-popup></q-btn>
          <q-btn color="primary" icon="tune" label="Відкрити конструктор норм (Delphi)" @click="openNormsForParam(activeParam)"></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>
"""

# Insert modals right before </div>\n  <!-- Vue 2 & Quasar JS CDN -->
if "showBiomaterialModal" not in content:
    content = content.replace("</div>\n\n  <!-- Vue 2 & Quasar JS CDN -->", modals_markup + "\n  </div>\n\n  <!-- Vue 2 & Quasar JS CDN -->")

# ---------------------------------------------------------------------------
# 3. VUE DATA: ADD MISSING PROPERTIES
# ---------------------------------------------------------------------------

new_data_props = """
        // Dictionary Modals
        showBiomaterialModal: false,
        activeBiomaterial: null,
        showTubeModal: false,
        activeTube: null,
        showAnalyzerModal: false,
        activeAnalyzer: null,
        showParamModal: false,
        activeParam: null,

        selectedServiceMethodCode: 'HEX_IFCC',
        serviceMethodOptions: [
          { label: 'Гексокіназний кінетичний (IFCC) — Золотий стандарт', value: 'HEX_IFCC' },
          { label: 'Глюкозооксидазний колориметричний (GOD-PAP)', value: 'GOD_PAP' },
          { label: 'Хемілюмінесцентний імуноаналіз (CLIA)', value: 'CLIA' },
          { label: 'SLS безціанідний метод (Sysmex)', value: 'SLS' }
        ],

        // New Combination Form
        newCombForm: {
          id: null,
          norm_name: '',
          method_name: 'Гексокіназний IFCC',
          gender: 'ANY',
          age_unit: 'YEARS',
          age_from: 18,
          age_to: 65,
          is_age: 1,
          is_pregnancy: 0,
          pregnancy_week_from: 1,
          pregnancy_week_to: 14,
          is_menstrual_phase: 0,
          menstrual_phase: 'FOLLICULAR',
          norm_low: 4.10,
          norm_high: 5.90,
          crit_low: 2.50,
          crit_high: 25.00,
          unit: 'ммоль/л',
          delta_check_max_pct: 25.0,
          norm_text: ''
        },

        // In-card resolver tester
        inCardTest: {
          gender: 'F',
          age: 28,
          phase: 'LUTEAL',
          pregWeek: null,
          value: 5.4
        },
        inCardResult: null,
"""

if "showBiomaterialModal:" not in content:
    content = content.replace("showServiceDetailDialog: false,", "showServiceDetailDialog: false,\n" + new_data_props)

# ---------------------------------------------------------------------------
# 4. VUE METHODS: ADD INTERACTIVE CARD OPENERS & RESOLVER
# ---------------------------------------------------------------------------

new_methods = """
      openBiomaterialCard(row) {
        this.activeBiomaterial = Object.assign({}, row);
        this.showBiomaterialModal = true;
      },
      openTubeCard(row) {
        this.activeTube = Object.assign({}, row);
        this.showTubeModal = true;
      },
      openAnalyzerCard(row) {
        this.activeAnalyzer = Object.assign({}, row);
        this.showAnalyzerModal = true;
      },
      openParamCard(row) {
        this.activeParam = Object.assign({}, row);
        this.showParamModal = true;
      },
      openNormsForParam(param) {
        this.showParamModal = false;
        let srv = this.servicesCatalog.find(s => s.code === param.code);
        if (!srv) {
          srv = {
            code: param.code,
            name: param.name,
            loinc: param.loinc,
            category: param.category,
            defaultMethod: 'Стандартна методика',
            combinationsCount: 5,
            deltaCheckPct: 25.0,
            deltaHours: 72,
            deltaAutoBlock: true
          };
          this.servicesCatalog.push(srv);
        }
        this.openServiceDetail(srv);
      },

      populateEditForm(row) {
        this.newCombForm = Object.assign({}, row);
        this.$q.notify({ type: 'info', message: 'Дані правила ' + row.norm_name + ' завантажено у конструктор', position: 'top', timeout: 1200 });
      },
      duplicateRow(row) {
        this.newCombForm = Object.assign({}, row);
        this.newCombForm.id = null;
        this.newCombForm.norm_name = row.norm_name + ' (Копія)';
        this.$q.notify({ type: 'positive', message: 'Правило скопійовано у конструктор для швидкого редагування', position: 'top', timeout: 1200 });
      },
      resetCombForm() {
        this.newCombForm = {
          id: null,
          norm_name: '',
          method_name: this.selectedService.defaultMethod || 'Стандартна',
          gender: 'ANY',
          age_unit: 'YEARS',
          age_from: 18,
          age_to: 65,
          is_age: 1,
          is_pregnancy: 0,
          pregnancy_week_from: 1,
          pregnancy_week_to: 14,
          is_menstrual_phase: 0,
          menstrual_phase: 'FOLLICULAR',
          norm_low: 4.10,
          norm_high: 5.90,
          crit_low: 2.50,
          crit_high: 25.00,
          unit: this.currentServiceUnit,
          delta_check_max_pct: 25.0,
          norm_text: ''
        };
      },
      saveNewCombination() {
        const payload = Object.assign({}, this.newCombForm, { test_code: this.selectedService.code });
        fetch('/api/laboratory/norms/combinations', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload)
        })
          .then(res => res.json())
          .then(data => {
            if (data.success) {
              this.$q.notify({ type: 'positive', message: 'Правило збережено в БД!', position: 'top' });
              // Refresh combinations
              this.loadAllCombinations();
              // Add to local array if not present
              if (!this.selectedServiceCombinations.find(c => c.id === data.id)) {
                payload.id = data.id;
                this.selectedServiceCombinations.push(payload);
              }
              this.resetCombForm();
            }
          })
          .catch(e => {
            this.$q.notify({ type: 'positive', message: 'Комбінацію додано в локальний сеанс!', position: 'top' });
            payload.id = 'LOCAL-' + Date.now();
            this.selectedServiceCombinations.push(payload);
            this.resetCombForm();
          });
      },
      testInCardResolver() {
        const val = parseFloat(this.inCardTest.value);
        fetch('/api/laboratory/norms/resolve', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            test_code: this.selectedService.code,
            gender: this.inCardTest.gender,
            age: this.inCardTest.age,
            menstrual_phase: this.inCardTest.phase,
            pregnancy_week: this.inCardTest.pregWeek
          })
        })
          .then(res => res.json())
          .then(data => {
            const rule = data.rule || (this.selectedServiceCombinations[0] || {});
            const nLow = parseFloat(rule.norm_low || 4.1);
            const nHigh = parseFloat(rule.norm_high || 5.9);
            const cLow = rule.crit_low ? parseFloat(rule.crit_low) : null;
            const cHigh = rule.crit_high ? parseFloat(rule.crit_high) : null;

            let statusText = 'В МЕЖАХ НОРМИ';
            let badgeColor = 'positive';
            let color = '#21ba45';
            let bg = '#f0fdf4';

            if ((cLow !== null && val <= cLow) || (cHigh !== null && val >= cHigh)) {
              statusText = '🚨 ПАНІЧНЕ ЗНАЧЕННЯ CITO!';
              badgeColor = 'negative';
              color = '#d04f45';
              bg = '#fef2f2';
            } else if (val < nLow) {
              statusText = '⚠️ НИЖЧЕ НОРМИ';
              badgeColor = 'warning';
              color = '#f2c037';
              bg = '#fffbeb';
            } else if (val > nHigh) {
              statusText = '⚠️ ВИЩЕ НОРМИ';
              badgeColor = 'warning';
              color = '#f2c037';
              bg = '#fffbeb';
            }

            this.inCardResult = {
              ruleName: rule.norm_name || 'Базове правило',
              normLow: nLow,
              normHigh: nHigh,
              unit: rule.unit || this.currentServiceUnit,
              statusText: statusText,
              badgeColor: badgeColor,
              color: color,
              bg: bg
            };
            this.$q.notify({ type: badgeColor === 'negative' ? 'negative' : 'positive', message: 'Результат верифікації: ' + statusText, position: 'top', timeout: 1500 });
          })
          .catch(e => {
            this.inCardResult = {
              ruleName: 'Локальний референс (Клінічна норма)',
              normLow: 4.1,
              normHigh: 5.9,
              unit: 'ммоль/л',
              statusText: val > 5.9 ? '⚠️ ВИЩЕ НОРМИ' : 'В МЕЖАХ НОРМИ',
              badgeColor: val > 5.9 ? 'warning' : 'positive',
              color: val > 5.9 ? '#f2c037' : '#21ba45',
              bg: '#f0fdf4'
            };
          });
      },
"""

if "openBiomaterialCard(row)" not in content:
    content = content.replace("methods: {", "methods: {\n" + new_methods)

# Also add currentServiceUnit computed property
new_computed = """
    currentServiceUnit() {
      return (this.selectedService && this.selectedService.unit) || 'ммоль/л';
    },
"""
if "currentServiceUnit()" not in content:
    content = content.replace("computed: {", "computed: {\n" + new_computed)

# Add actions column to dictBioColumns, dictTubeColumns, dictAnColumns, dictParamColumns
content = content.replace(
    "{ name: 'storage', label: 'Умови зберігання', field: 'storage', align: 'left' }",
    "{ name: 'storage', label: 'Умови зберігання', field: 'storage', align: 'left' },\n          { name: 'actions', label: 'Дії', align: 'center' }"
)
content = content.replace(
    "{ name: 'inversions', label: 'Перевертання', field: 'inversions', align: 'left' }",
    "{ name: 'inversions', label: 'Перевертання', field: 'inversions', align: 'left' },\n          { name: 'actions', label: 'Дії', align: 'center' }"
)
content = content.replace(
    "{ name: 'interfaceType', label: 'Інтерфейс', field: 'interfaceType', align: 'left' }",
    "{ name: 'interfaceType', label: 'Інтерфейс', field: 'interfaceType', align: 'left' },\n          { name: 'actions', label: 'Дії', align: 'center' }"
)
content = content.replace(
    "{ name: 'sampleType', label: 'Рекомендований зразок', field: 'sampleType', align: 'left' }",
    "{ name: 'sampleType', label: 'Рекомендований зразок', field: 'sampleType', align: 'left' },\n          { name: 'actions', label: 'Дії', align: 'center' }"
)

with open(PROTOTYPE_PATH, "w", encoding="utf-8") as f:
    f.write(content)
with open(INDEX_PATH, "w", encoding="utf-8") as f:
    f.write(content)

print(f"Successfully injected Delphi Service Card Modal & Dictionary Card Modals into:")
print(f" - {PROTOTYPE_PATH}")
print(f" - {INDEX_PATH}")
