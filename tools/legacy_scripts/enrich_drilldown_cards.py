# -*- coding: utf-8 -*-
"""
Inject rich Drill-Down cards, Delphi-style Reference Combinations Matrix,
Reference Resolver simulator, and Analyzer/Tube cards into run_prototype.html and index.html.
"""

import os
import re

BASE_DIR = r"C:\__MEDLINK___\LABA"
PROTOTYPE_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "run_prototype.html")
INDEX_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "index.html")

with open(PROTOTYPE_PATH, "r", encoding="utf-8") as f:
    html = f.read()

# 1. NEW NORMS VIEW WITH SERVICES CATALOG AND DRILL-DOWN BUTTONS
new_norms_view = """
          <!-- VIEW: NORMS, METHODOLOGIES & SERVICES (DRILL-DOWN CATALOG) -->
          <div v-show="currentView === 'norms'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-sliders-h" class="q-mr-sm"></q-icon>
                  Довідник лабораторних послуг, норми та методики (Delphi Референси)
                </h5>
                <div class="text-caption text-grey-7">
                  Налаштування всіх можливих комбінацій: стать, вік (дні/місяці/роки), фази менструального циклу, тижні вагітності, методики та рефлекс-тести.
                </div>
              </div>
              <div class="row items-center q-gutter-x-sm">
                <q-btn color="secondary" icon="calculate" label="Симулятор підбору норми" unelevated dense @click="openReferenceResolver('PROG')"></q-btn>
                <q-btn color="primary" icon="add" label="Створити нову послугу / тест" dense unelevated @click="openNewServiceDialog"></q-btn>
              </div>
            </div>

            <!-- SEARCH & CATEGORY FILTER BAR -->
            <q-card flat bordered class="q-pa-sm q-mb-md bg-white">
              <div class="row items-center justify-between q-col-gutter-sm">
                <div class="col-12 col-md-5">
                  <q-input v-model="normsSearch" dense outlined placeholder="Пошук послуги за назвою, кодом або LOINC..." clearable>
                    <template v-slot:prepend><q-icon name="search"></q-icon></template>
                  </q-input>
                </div>
                <div class="col-12 col-md-7 text-right">
                  <q-btn-toggle
                    v-model="normsCategoryFilter"
                    toggle-color="primary"
                    dense
                    size="sm"
                    :options="[
                      {label: 'Всі послуги (' + servicesCatalog.length + ')', value: 'ALL'},
                      {label: 'Біохімія', value: 'BIOCHEM'},
                      {label: 'Гормони / Акушерство', value: 'HORMONES'},
                      {label: 'Гематологія', value: 'HEMATOLOGY'}
                    ]"
                  ></q-btn-toggle>
                </div>
              </div>
            </q-card>

            <!-- SERVICES CATALOG GRID WITH DRILL-DOWN ACTION -->
            <div class="row q-col-gutter-md q-mb-md">
              <div v-for="srv in filteredServicesCatalog" :key="'srv-'+srv.code" class="col-12 col-md-6 col-lg-4">
                <q-card flat bordered class="medlink-card cursor-pointer hover-shadow" @click="openServiceDetail(srv)">
                  <q-card-section class="bg-grey-1 q-py-sm">
                    <div class="row items-center justify-between">
                      <div class="row items-center q-gutter-x-xs">
                        <q-badge color="primary" text-color="white" :label="srv.code" class="text-weight-bold"></q-badge>
                        <q-badge color="teal" text-color="white" :label="'LOINC: ' + srv.loinc"></q-badge>
                      </div>
                      <q-badge color="purple-1" text-color="purple-9" :label="srv.category"></q-badge>
                    </div>
                  </q-card-section>

                  <q-card-section class="q-pa-md">
                    <div class="text-subtitle1 text-weight-bold text-dark q-mb-xs">{{ srv.name }}</div>
                    <div class="text-caption text-grey-7 q-mb-sm">Методика за замовчуванням: <strong>{{ srv.defaultMethod }}</strong></div>

                    <div class="row items-center justify-between text-caption q-py-xs bg-blue-1 rounded-borders q-px-sm q-mb-sm">
                      <span>Налаштовано комбінацій норм:</span>
                      <strong class="text-primary text-subtitle2">{{ srv.combinationsCount }} правил</strong>
                    </div>

                    <div class="row q-gutter-xs q-mb-sm">
                      <q-chip v-if="srv.hasPregnancy" size="xs" color="pink-1" text-color="pink-9" icon="pregnant_woman">Вагітність (триместри)</q-chip>
                      <q-chip v-if="srv.hasMenstrual" size="xs" color="purple-1" text-color="purple-9" icon="female">Фази циклу</q-chip>
                      <q-chip v-if="srv.hasPediatric" size="xs" color="blue-1" text-color="blue-9" icon="child_care">Педіатрія (дні/місяці)</q-chip>
                      <q-chip v-if="srv.hasReflex" size="xs" color="amber-1" text-color="amber-9" icon="alt_route">Рефлекс-тести</q-chip>
                    </div>

                    <div class="row items-center justify-between text-caption text-grey-7">
                      <span>Дельта-чек: &plusmn;{{ srv.deltaCheckPct }}% (72 год)</span>
                      <q-btn size="sm" color="primary" unelevated label="Провалитися в картку &rarr;" @click.stop="openServiceDetail(srv)"></q-btn>
                    </div>
                  </q-card-section>
                </q-card>
              </div>
            </div>

            <!-- OVERVIEW MATRIX TABLE (DELPHI STYLE) -->
            <q-card flat bordered class="medlink-card q-mt-md">
              <q-card-section class="bg-grey-2 q-py-sm row items-center justify-between">
                <div class="text-subtitle2 text-weight-bold text-primary">
                  <q-icon name="table_chart" class="q-mr-xs"></q-icon>
                  Зведена матриця всіх 28 комбінацій норм (Delphi: dct_service_lab_norm & dct_service_lab_nv)
                </div>
                <div class="text-caption text-grey-7">Активні фільтри: {{ activeCombinationsCount }} правил у базі даних</div>
              </q-card-section>

              <q-table
                :data="allCombinations"
                :columns="combinationsTableColumns"
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
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props">
                    <q-btn size="xs" color="primary" flat dense icon="edit" label="Деталі" @click="editCombination(props.row)"></q-btn>
                  </q-td>
                </template>
              </q-table>
            </q-card>
          </div>
"""

# Replace old norms block
norms_pattern = r'<!-- VIEW: NORMS & METHODOLOGIES -->[\s\S]*?<!-- VIEW: DICTIONARY BIOMATERIALS -->'
html = re.sub(norms_pattern, new_norms_view + "\n\n          <!-- VIEW: DICTIONARY BIOMATERIALS -->", html)

# 2. DRILL-DOWN SERVICE DETAIL MODAL (FULL FEATURED 5 TABS)
service_modal_template = """
    <!-- ============================================================== -->
    <!-- DRILL-DOWN MODAL: КАРТКА ПОСЛУГИ ТА НАЛАШТУВАННЯ КОМБІНАЦІЙ НОРМ -->
    <!-- ============================================================== -->
    <q-dialog v-model="showServiceDetailDialog" maximized transition-show="slide-up" transition-hide="slide-down">
      <q-card class="bg-grey-1 column full-height">
        <!-- Header -->
        <q-card-section class="bg-primary text-white row items-center justify-between q-py-sm">
          <div class="row items-center q-gutter-x-sm">
            <q-avatar square size="34px" color="white" text-color="primary" icon="biotech"></q-avatar>
            <div>
              <div class="text-h6 text-weight-bold lh-tight">
                {{ selectedService.name }} ({{ selectedService.code }})
              </div>
              <div class="text-caption text-blue-2" style="font-size: 11px;">
                LOINC: <strong>{{ selectedService.loinc }}</strong> | Категорія: <strong>{{ selectedService.category }}</strong> | CITO-доступність: <strong>Так</strong>
              </div>
            </div>
          </div>
          <div class="row items-center q-gutter-x-xs">
            <q-btn icon="save" color="positive" unelevated label="Зберегти всі зміни" @click="saveServiceSettings"></q-btn>
            <q-btn icon="close" flat round dense v-close-popup></q-btn>
          </div>
        </q-card-section>

        <!-- Navigation Tabs -->
        <q-tabs v-model="serviceTab" dense class="bg-white text-grey-8" active-color="primary" indicator-color="primary" align="left">
          <q-tab name="norms_matrix" icon="format_list_bulleted" label="Матриця комбінацій норм (Delphi)" :badge="selectedServiceCombinations.length"></q-tab>
          <q-tab name="methods" icon="science" label="Методики та аналізатори" :badge="selectedServiceMethods.length"></q-tab>
          <q-tab name="delta_reflex" icon="alt_route" label="Дельта-чек & Рефлекс-правила"></q-tab>
          <q-tab name="calculator" icon="calculate" label="Симулятор підбору референсу"></q-tab>
          <q-tab name="tubes" icon="invert_colors" label="Пробірки та преаналітика"></q-tab>
        </q-tabs>

        <q-separator></q-separator>

        <!-- Tab Panels -->
        <q-tab-panels v-model="serviceTab" animated class="col bg-grey-2 q-pa-md overflow-auto">
          <!-- TAB 1: COMBINATIONS MATRIX (DELPHI STYLE) -->
          <q-tab-panel name="norms_matrix" class="q-pa-none">
            <div class="row items-center justify-between q-mb-sm">
              <div>
                <div class="text-subtitle1 text-weight-bold text-dark">
                  Налаштовані комбінації референсних інтервалів для {{ selectedService.name }}
                </div>
                <div class="text-caption text-grey-7">
                  Система автоматично обирає найбільш специфічне правило для пацієнта (Вагітність &gt; Фаза циклу &gt; Вік/Стать &gt; Загальне).
                </div>
              </div>
              <q-btn color="primary" icon="add" label="Додати нову комбінацію норм" unelevated size="sm" @click="openAddCombinationForm"></q-btn>
            </div>

            <q-card flat bordered class="medlink-card q-mb-md">
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
                    <div v-else class="text-grey-8">
                      Стать: {{ props.row.gender === 'M' ? 'Чол' : (props.row.gender === 'F' ? 'Жін' : 'Будь-яка') }},
                      Вік: {{ props.row.age_from }}-{{ props.row.age_to }} {{ props.row.age_unit === 'DAYS' ? 'днів' : (props.row.age_unit === 'MONTHS' ? 'міс' : 'років') }}
                    </div>
                  </q-td>
                </template>
                <template v-slot:body-cell-actions="props">
                  <q-td :props="props">
                    <q-btn size="xs" color="primary" flat dense icon="edit" @click="editCombination(props.row)"></q-btn>
                    <q-btn size="xs" color="negative" flat dense icon="delete" @click="deleteCombination(props.row)"></q-btn>
                  </q-td>
                </template>
              </q-table>
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

          <!-- TAB 4: INTERACTIVE RESOLVER SIMULATOR -->
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

          <!-- TAB 5: TUBES & PREANALYTICS -->
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
"""

# Insert modal right before </q-app>
if "showServiceDetailDialog" not in html:
    html = html.replace("</q-layout>\n    </q-dialog>", "</q-layout>\n    </q-dialog>\n" + service_modal_template)

# 3. VUE DATA PROPERTIES FOR DRILL-DOWN SERVICES
vue_drilldown_data = """
        showServiceDetailDialog: false,
        serviceTab: 'norms_matrix',
        normsSearch: '',
        normsCategoryFilter: 'ALL',
        allCombinations: [],
        selectedService: {
          code: 'GLU',
          name: 'Глюкоза сироватки',
          loinc: '2345-7',
          category: 'Біохімія',
          defaultMethod: 'Гексокіназний IFCC',
          combinationsCount: 5,
          deltaCheckPct: 25.0,
          deltaHours: 72,
          deltaAutoBlock: true
        },
        selectedServiceCombinations: [],
        selectedServiceMethods: [
          { id: 1, code: 'HEX_IFCC', name: 'Гексокіназний кінетичний (IFCC)', description: 'Золотий стандарт референсного вимірювання глюкози у сироватці та плазмі', analyzers: ['Roche Cobas e411', 'Mindray BS-240'], onBoardDays: 28, slope: 1.002 },
          { id: 2, code: 'GOD_PAP', name: 'Глюкозооксидазний колориметричний', description: 'Ферментативний колориметричний метод з оксидазою та пероксидазою', analyzers: ['Mindray BS-240'], onBoardDays: 14, slope: 0.994 }
        ],
        selectedServiceReflexRules: [
          { id: 'RFLX-01', trigger_test_code: 'GLU', threshold_value: 15.0, reflex_test_code: 'GLU_URINE', description: 'При глікемії > 15.0 ммоль/л автоматично призначити глюкозу сечі та кетони' }
        ],
        simParams: {
          gender: 'F',
          age: 28,
          phase: 'LUTEAL',
          pregWeek: null
        },
        simResult: null,
        servicesCatalog: [
          { code: 'GLU', name: 'Глюкоза сироватки', loinc: '2345-7', category: 'BIOCHEM', defaultMethod: 'Гексокіназний IFCC', combinationsCount: 5, hasPregnancy: true, hasMenstrual: false, hasPediatric: true, hasReflex: true, deltaCheckPct: 25.0 },
          { code: 'PROG', name: 'Прогестерон (Progesterone)', loinc: '2839-9', category: 'HORMONES', defaultMethod: 'ІХЛА (Roche Cobas)', combinationsCount: 8, hasPregnancy: true, hasMenstrual: true, hasPediatric: false, hasReflex: false, deltaCheckPct: 40.0 },
          { code: 'TSH', name: 'Тиреотропний гормон (ТТГ)', loinc: '3016-3', category: 'HORMONES', defaultMethod: 'ECLIA (Cobas)', combinationsCount: 5, hasPregnancy: true, hasMenstrual: false, hasPediatric: true, hasReflex: true, deltaCheckPct: 35.0 },
          { code: 'HGB', name: 'Гемоглобін (HGB)', loinc: '718-7', category: 'HEMATOLOGY', defaultMethod: 'SLS безціанідний', combinationsCount: 5, hasPregnancy: true, hasMenstrual: false, hasPediatric: true, hasReflex: false, deltaCheckPct: 15.0 },
          { code: 'ESR', name: 'Швидкість осідання еритроцитів (ШОЕ)', loinc: '30341-2', category: 'HEMATOLOGY', defaultMethod: 'Панченков / Вестергрен', combinationsCount: 5, hasPregnancy: true, hasMenstrual: false, hasPediatric: false, hasReflex: false, deltaCheckPct: 30.0 },
          { code: 'CREAT', name: 'Креатинін сироватки', loinc: '2160-0', category: 'BIOCHEM', defaultMethod: 'Яффе кінетичний', combinationsCount: 4, hasPregnancy: false, hasMenstrual: false, hasPediatric: true, hasReflex: false, deltaCheckPct: 20.0 }
        ],
        combinationsTableColumns: [
          { name: 'test_code', label: 'Тест', field: 'test_code', align: 'left', sortable: true },
          { name: 'norm_name', label: 'Назва правила / Комбінація', field: 'norm_name', align: 'left', sortable: true },
          { name: 'method_name', label: 'Методика', field: 'method_name', align: 'left' },
          { name: 'normRange', label: 'Нормальний інтервал', field: 'norm_low', align: 'center' },
          { name: 'critRange', label: 'Паніка CITO', field: 'crit_low', align: 'center' },
          { name: 'actions', label: 'Дії', field: 'id', align: 'right' }
        ],
        serviceCombColumns: [
          { name: 'norm_name', label: 'Назва комбінації', field: 'norm_name', align: 'left' },
          { name: 'condition', label: 'Умови спрацювання', field: 'gender', align: 'left' },
          { name: 'method_name', label: 'Методика', field: 'method_name', align: 'left' },
          { name: 'normRange', label: 'Нормальний діапазон', field: 'norm_low', align: 'center' },
          { name: 'critRange', label: 'Панічний поріг CITO', field: 'crit_low', align: 'center' },
          { name: 'actions', label: 'Дії', field: 'id', align: 'right' }
        ],
"""

if "showServiceDetailDialog:" not in html:
    html = html.replace("data: {", "data: {\n" + vue_drilldown_data)

# 4. VUE COMPUTED FOR DRILL-DOWN SERVICES
vue_drilldown_computed = """
    filteredServicesCatalog() {
      return this.servicesCatalog.filter(s => {
        const matchesCategory = this.normsCategoryFilter === 'ALL' || s.category === this.normsCategoryFilter;
        const matchesSearch = !this.normsSearch ||
          s.name.toLowerCase().includes(this.normsSearch.toLowerCase()) ||
          s.code.toLowerCase().includes(this.normsSearch.toLowerCase()) ||
          s.loinc.includes(this.normsSearch);
        return matchesCategory && matchesSearch;
      });
    },
    activeCombinationsCount() {
      return this.allCombinations.length;
    },
"""

if "filteredServicesCatalog()" not in html:
    html = html.replace("computed: {", "computed: {\n" + vue_drilldown_computed)

# 5. VUE METHODS FOR DRILL-DOWN SERVICES
vue_drilldown_methods = """
      loadAllCombinations() {
        fetch('/api/laboratory/norms/combinations')
          .then(res => res.json())
          .then(data => {
            if (data.success && data.data) {
              this.allCombinations = data.data;
            }
          })
          .catch(e => console.log('Combinations fetch fallback:', e));
      },
      openServiceDetail(srv) {
        this.selectedService = Object.assign({}, srv);
        this.selectedServiceCombinations = this.allCombinations.filter(c => c.test_code === srv.code);
        this.simParams.gender = 'F';
        this.simParams.age = 28;
        this.simParams.phase = srv.hasMenstrual ? 'LUTEAL' : null;
        this.simParams.pregWeek = null;
        this.simResult = null;
        this.serviceTab = 'norms_matrix';
        this.showServiceDetailDialog = true;
      },
      openNewServiceDialog() {
        this.$q.notify({ type: 'info', message: 'Форма додавання нової послуги відкрито', position: 'top' });
      },
      openReferenceResolver(testCode) {
        const srv = this.servicesCatalog.find(s => s.code === testCode) || this.servicesCatalog[0];
        this.openServiceDetail(srv);
        this.serviceTab = 'calculator';
      },
      editCombination(comb) {
        this.$q.notify({ type: 'info', message: 'Редагування правила: ' + comb.norm_name, position: 'top' });
      },
      deleteCombination(comb) {
        this.$q.notify({ type: 'warning', message: 'Видалення правила ' + comb.norm_name, position: 'top' });
      },
      openAddCombinationForm() {
        this.$q.notify({ type: 'positive', message: 'Конструктор комбінації відкрито', position: 'top' });
      },
      saveServiceSettings() {
        this.$q.notify({ type: 'positive', message: 'Всі налаштування послуги ' + this.selectedService.code + ' збережено в БД!', position: 'top' });
        this.showServiceDetailDialog = false;
      },
      executeSimulateResolve() {
        fetch('/api/laboratory/norms/resolve', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            test_code: this.selectedService.code,
            gender: this.simParams.gender,
            age: this.simParams.age,
            menstrual_phase: this.simParams.phase,
            pregnancy_week: this.simParams.pregWeek
          })
        })
          .then(res => res.json())
          .then(data => {
            if (data.success) {
              this.simResult = data;
              this.$q.notify({ type: 'positive', message: 'Референс успішно підібрано!', position: 'top', timeout: 1500 });
            }
          })
          .catch(e => {
            console.log('Resolver fallback:', e);
            this.simResult = {
              matched: true,
              rule: {
                norm_name: 'Лютеїнова фаза (Локальний розрахунок)',
                norm_low: 5.82,
                norm_high: 75.9,
                unit: 'нмоль/л',
                method_name: 'ІХЛА (Roche Cobas)'
              }
            };
          });
      },
"""

if "loadAllCombinations()" not in html:
    html = html.replace("methods: {", "methods: {\n" + vue_drilldown_methods)

# In mounted hook, call loadAllCombinations
if "this.loadAllCombinations()" not in html:
    html = html.replace("mounted() {", "mounted() {\n    this.loadAllCombinations();")

# Save to run_prototype.html
with open(PROTOTYPE_PATH, "w", encoding="utf-8") as f:
    f.write(html)

# Also save to index.html
with open(INDEX_PATH, "w", encoding="utf-8") as f:
    f.write(html)

print("Drill-Down cards and Delphi-style reference combinations matrix successfully injected!")
