# -*- coding: utf-8 -*-
"""
Inject rich interactivity, process guides, demo routines, and action modals
into medlink_lab_frontend/run_prototype.html and index.html
"""

import os
import json
import re
from test_guides_data import guides_dict

BASE_DIR = r"C:\__MEDLINK___\LABA"
PROTOTYPE_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "run_prototype.html")
INDEX_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "index.html")

with open(PROTOTYPE_PATH, "r", encoding="utf-8") as f:
    content = f.read()

# 1. CSS Injection
guide_css = """
    /* Interactive Process Guide & Action Modals */
    .process-guide-card {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      border-left: 4px solid #0178BC;
      border-radius: 6px;
      padding: 12px 16px;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
      margin-bottom: 16px;
    }
    .guide-box {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      padding: 10px 12px;
      height: 100%;
    }
    .guide-box-title {
      font-size: 12px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      margin-bottom: 6px;
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .guide-box-desc {
      font-size: 12.5px;
      color: #334155;
      line-height: 1.5;
    }
    .guide-what { border-top: 3px solid #0178BC; }
    .guide-why { border-top: 3px solid #0d9488; }
    .guide-where { border-top: 3px solid #6366f1; }

    /* Barcode Sticker Preview */
    .barcode-sticker {
      width: 280px;
      border: 2px solid #000000;
      padding: 8px;
      background: #ffffff;
      font-family: 'JetBrains Mono', Consolas, monospace;
      color: #000000;
      margin: 0 auto;
      box-shadow: 0 4px 6px rgba(0,0,0,0.1);
    }
    .barcode-lines {
      height: 40px;
      background: repeating-linear-gradient(90deg, #000 0, #000 2px, #fff 2px, #fff 4px, #000 4px, #000 7px, #fff 7px, #fff 9px);
      margin: 6px 0;
    }

    /* PDF Report Document Sheet */
    .pdf-report-sheet {
      background: #ffffff;
      color: #1e293b;
      padding: 30px;
      max-width: 800px;
      margin: 0 auto;
      border: 1px solid #cbd5e1;
      box-shadow: 0 10px 25px rgba(0,0,0,0.15);
      font-family: 'Source Sans Pro', Arial, sans-serif;
    }
    .pdf-header {
      border-bottom: 2px solid #0178BC;
      padding-bottom: 12px;
      margin-bottom: 16px;
    }
    .pdf-table {
      width: 100%;
      border-collapse: collapse;
      margin: 16px 0;
    }
    .pdf-table th, .pdf-table td {
      border: 1px solid #e2e8f0;
      padding: 6px 10px;
      font-size: 12px;
      text-align: left;
    }
    .pdf-table th {
      background: #f1f5f9;
      font-weight: 700;
    }
    .pdf-stamp {
      border: 2px dashed #0178BC;
      border-radius: 50%;
      width: 110px;
      height: 110px;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      color: #0178BC;
      font-size: 10px;
      text-align: center;
      transform: rotate(-8deg);
      font-weight: 700;
    }
"""

if ".process-guide-card" not in content:
    content = content.replace("</style>", guide_css + "\n  </style>")

# 2. Template Guide component
guide_component_html = """
          <!-- GLOBAL INTERACTIVE PROCESS GUIDE (ЩО, ЧОМУ, ЗВІДКИ) -->
          <div class="process-guide-card q-mb-md">
            <div class="row items-center justify-between no-wrap">
              <div class="row items-center q-gutter-x-sm">
                <q-chip color="primary" text-color="white" icon="help_outline" dense class="text-weight-bold" style="font-size: 11px;">
                  ГІД ПО ПРОЦЕСУ ЛІС
                </q-chip>
                <div class="text-subtitle2 text-weight-bolder text-primary">
                  {{ currentGuide.title }}
                </div>
              </div>
              <div class="row items-center q-gutter-x-xs">
                <q-btn color="secondary" dense unelevated icon="smart_button" label="Інтерактивний сценарій (Клік-тур)" @click="runProcessDemo" size="sm" class="q-px-sm"></q-btn>
                <q-btn flat dense round :icon="showGuide ? 'expand_less' : 'expand_more'" @click="showGuide = !showGuide">
                  <q-tooltip>{{ showGuide ? 'Згорнути пояснення' : 'Розгорнути деталі Що / Чому / Звідки' }}</q-tooltip>
                </q-btn>
              </div>
            </div>

            <div v-show="showGuide" class="q-mt-sm">
              <div class="row q-col-gutter-sm">
                <!-- ЩО РОБИТЬСЯ -->
                <div class="col-12 col-md-4">
                  <div class="guide-box guide-what">
                    <div class="guide-box-title text-primary">
                      <q-icon name="play_circle_filled" size="16px"></q-icon>
                      1. Що робиться (Дія)
                    </div>
                    <div class="guide-box-desc" v-html="currentGuide.what"></div>
                  </div>
                </div>

                <!-- ЧОМУ ЦЕ РОБИТЬСЯ -->
                <div class="col-12 col-md-4">
                  <div class="guide-box guide-why">
                    <div class="guide-box-title text-teal">
                      <q-icon name="verified_user" size="16px"></q-icon>
                      2. Чому це робиться (Мета)
                    </div>
                    <div class="guide-box-desc" v-html="currentGuide.why"></div>
                  </div>
                </div>

                <!-- ЗВІДКИ БЕРЕТЬСЯ ІНФОРМАЦІЯ -->
                <div class="col-12 col-md-4">
                  <div class="guide-box guide-where">
                    <div class="guide-box-title text-indigo">
                      <q-icon name="dns" size="16px"></q-icon>
                      3. Звідки береться інформація
                    </div>
                    <div class="guide-box-desc" v-html="currentGuide.where"></div>
                  </div>
                </div>
              </div>
            </div>
          </div>
"""

if "GLOBAL INTERACTIVE PROCESS GUIDE" not in content:
    content = content.replace('<q-page class="q-pa-md">', '<q-page class="q-pa-md">\n' + guide_component_html)

# 3. Action Modals HTML
modals_html = """
      <!-- MODAL 1: ДРУК ТЕРМОШТРИХКОДУ ПРОБІРКИ -->
      <q-dialog v-model="barcodeDialog">
        <q-card style="min-width: 380px; max-width: 440px;">
          <q-card-section class="bg-primary text-white row items-center justify-between q-py-sm">
            <div class="text-subtitle1 text-weight-bold">
              <q-icon name="qr_code_scanner" class="q-mr-xs"></q-icon>
              Друк маркування пробірки (Code128)
            </div>
            <q-btn flat round dense icon="close" v-close-popup></q-btn>
          </q-card-section>
          <q-card-section class="q-pa-md text-center">
            <div class="barcode-sticker q-mb-md">
              <div class="row justify-between text-caption text-weight-bold">
                <span>КНП "ЦМЛ" / КДЛ</span>
                <span>{{ activeSample.orderDate || '06.10.2026' }}</span>
              </div>
              <div class="text-subtitle2 text-weight-bolder q-mt-xs">{{ activeSample.patientName }}</div>
              <div class="text-caption text-grey-8">ID: {{ activeSample.patientId }} | {{ activeSample.birthYear }} р.н.</div>
              <div class="barcode-lines"></div>
              <div class="text-weight-bolder text-subtitle2" style="letter-spacing: 2px;">*{{ activeSample.barcode }}*</div>
              <div class="row justify-between text-caption text-weight-bold q-mt-xs" style="border-top: 1px dashed #000; padding-top: 2px;">
                <span>{{ activeSample.tubeType }}</span>
                <span>{{ activeSample.testsList }}</span>
              </div>
            </div>
            <div class="text-caption text-grey-7">
              Термодрук Zebra ZD220 / GK420t (Етикетка 50x25 мм).<br>
              CLSI H3-A6: Наклеювати строго вертикально вздовж пробірки.
            </div>
          </q-card-section>
          <q-card-actions align="right" class="bg-grey-1 q-pa-sm">
            <q-btn flat label="Закрити" color="grey-8" v-close-popup></q-btn>
            <q-btn color="primary" icon="print" label="Відправити на принтер" @click="confirmPrintBarcode"></q-btn>
          </q-card-actions>
        </q-card>
      </q-dialog>

      <!-- MODAL 2: ОФІЦІЙНИЙ PDF-БЛАНК РЕЗУЛЬТАТІВ З КЕП ТА QR -->
      <q-dialog v-model="pdfDialog" maximized>
        <q-card class="bg-grey-3">
          <q-bar class="bg-dark text-white">
            <q-icon name="picture_as_pdf" color="red"></q-icon>
            <div class="text-weight-bold">Офіційний бланк результатів лабораторного дослідження — MedLink LIS 3.0</div>
            <q-space></q-space>
            <q-btn flat round dense icon="print" @click="windowPrint" title="Друк бланку"></q-btn>
            <q-btn flat round dense icon="close" v-close-popup></q-btn>
          </q-bar>

          <q-card-section class="q-pa-lg scroll" style="max-height: calc(100vh - 40px);">
            <div class="pdf-report-sheet">
              <!-- Header -->
              <div class="pdf-header row items-center justify-between">
                <div>
                  <div class="text-h6 text-weight-bolder text-primary">КНП "Центральна міська клінічна лікарня"</div>
                  <div class="text-caption text-grey-8">Клініко-діагностична лабораторія (Акредитація ДСТУ EN ISO 15189)</div>
                  <div class="text-caption text-grey-7">м. Київ, вул. Госпітальна, 12 | Тел: (044) 222-33-44 | Код ЄДРПОУ 12345678</div>
                </div>
                <div class="text-right">
                  <div class="text-weight-bold text-subtitle2">РЕЗУЛЬТАТ ДОСЛІДЖЕННЯ</div>
                  <div class="text-caption text-weight-bold text-primary">№ Замовлення: {{ activeReport.orderNumber }}</div>
                  <div class="text-caption text-grey-7">Дата забору: {{ activeReport.collectedAt }}</div>
                  <div class="text-caption text-grey-7">Дата видачі: {{ activeReport.completedAt }}</div>
                </div>
              </div>

              <!-- Patient Bio -->
              <div class="row q-col-gutter-sm q-mb-md bg-grey-1 q-pa-sm border-radius">
                <div class="col-6"><strong>Пацієнт:</strong> {{ activeReport.patientName }}</div>
                <div class="col-3"><strong>Стать:</strong> {{ activeReport.gender }}</div>
                <div class="col-3"><strong>Вік:</strong> {{ activeReport.age }} р.</div>
                <div class="col-6"><strong>Направляючий лікар:</strong> {{ activeReport.doctor }}</div>
                <div class="col-6"><strong>Відділення:</strong> {{ activeReport.department }}</div>
                <div class="col-12" v-if="activeReport.eHealthId">
                  <strong>eHealth Е-направлення:</strong> <code>{{ activeReport.eHealthId }}</code> (Статус: Погашено)
                </div>
              </div>

              <!-- Results Table -->
              <div class="text-subtitle2 text-weight-bold text-primary q-mb-xs">{{ activeReport.profileName }}</div>
              <table class="pdf-table">
                <thead>
                  <tr>
                    <th>Показник (Тест)</th>
                    <th>Результат</th>
                    <th>Одиниці</th>
                    <th>Референсні інтервали (Норма)</th>
                    <th>Флаг</th>
                    <th>Методика / Аналізатор</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(item, idx) in activeReport.items" :key="idx" :style="item.isAbnormal ? 'background: #fff1f2;' : ''">
                    <td class="text-weight-medium">{{ item.testName }}</td>
                    <td class="text-weight-bolder" :class="item.isAbnormal ? 'text-negative' : 'text-dark'">{{ item.value }}</td>
                    <td>{{ item.unit }}</td>
                    <td>{{ item.norm }}</td>
                    <td>
                      <span v-if="item.flag" class="text-weight-bold" :class="item.flag.includes('КРИТИЧНО') ? 'text-negative' : 'text-warning'">{{ item.flag }}</span>
                      <span v-else class="text-positive">Норма</span>
                    </td>
                    <td class="text-caption text-grey-8">{{ item.method }}</td>
                  </tr>
                </tbody>
              </table>

              <!-- Clinical Notes -->
              <div class="q-my-sm text-caption text-grey-8" style="border-left: 3px solid #0178BC; padding-left: 8px;">
                <strong>Клінічний коментар лаборанта:</strong> Преаналітичних зауважень немає. Зразок сироватки без ознак гемолізу та ліпемії. Результати верифіковано згідно з критеріями контролю якості.
              </div>

              <!-- Signatures & Seals -->
              <div class="row justify-between items-center q-mt-lg q-pt-md border-top">
                <div class="row items-center q-gutter-x-md">
                  <div class="pdf-stamp">
                    <span>КДЛ КНП ЦМКЛ</span>
                    <span style="font-size: 8px;">ДЛЯ АНАЛІЗІВ</span>
                    <span style="font-size: 8px;">№ 04</span>
                  </div>
                  <div>
                    <div class="text-caption"><strong>Дослідження виконав:</strong> Лаборант Коваленко О.В.</div>
                    <div class="text-caption"><strong>Верифікував:</strong> Д-р Мельник В.С. (Лікар-лаборант)</div>
                    <div class="text-caption text-primary q-mt-xs">
                      <q-icon name="verified" color="positive"></q-icon> <strong>Кваліфікований електронний підпис (КЕП):</strong><br>
                      Сертифікат: <code>45AF...E120</code> | Час: {{ activeReport.completedAt }}
                    </div>
                  </div>
                </div>

                <!-- QR Validation Code -->
                <div class="text-center">
                  <div style="width: 80px; height: 80px; background: #f1f5f9; border: 1px solid #cbd5e1; margin: 0 auto; display: flex; align-items: center; justify-content: center; font-size: 10px; font-weight: bold; color: #475569;">
                    [ QR-КОД ]
                  </div>
                  <div class="text-caption text-grey-7" style="font-size: 9px; margin-top: 4px;">
                    Скануйте для перевірки<br>автентичності на порталі
                  </div>
                </div>
              </div>
            </div>
          </q-card-section>
        </q-card>
      </q-dialog>

      <!-- MODAL 3: РЕЄСТРАЦІЯ ОПОВІЩЕННЯ ПРО ПАНІЧНЕ ЗНАЧЕННЯ (CITO) -->
      <q-dialog v-model="panicDialog">
        <q-card style="min-width: 480px;">
          <q-card-section class="bg-negative text-white row items-center justify-between q-py-sm">
            <div class="text-subtitle1 text-weight-bold">
              <q-icon name="warning" class="q-mr-xs"></q-icon>
              Оповіщення про критичне (панічне) значення!
            </div>
            <q-btn flat round dense icon="close" v-close-popup></q-btn>
          </q-card-section>
          <q-card-section class="q-pa-md">
            <div class="bg-red-1 text-negative q-pa-sm border-radius q-mb-md text-weight-medium">
              У пацієнта <strong>{{ panicRecord.patient }}</strong> виявлено результат, що загрожує життю:<br>
              <strong>{{ panicRecord.test }} = {{ panicRecord.value }} {{ panicRecord.unit }}</strong> (Норма {{ panicRecord.norm }}).
            </div>
            <div class="text-caption text-grey-8 q-mb-sm">
              Згідно зі стандартом ДСТУ EN ISO 15189 розд. 5.7, лаборант зобов’язаний негайно телефоном проінформувати лікуючого лікаря та зафіксувати це в журналі:
            </div>
            <q-input v-model="panicForm.doctorNotified" label="ПІБ лікаря, який прийняв дзвінок" outlined dense class="q-mb-sm" placeholder="Наприклад: Д-р Сидоренко А.В. (Кардіологія)"></q-input>
            <q-input v-model="panicForm.phone" label="Номер телефону" outlined dense class="q-mb-sm" placeholder="+38 (067) 123-45-67"></q-input>
            <q-input v-model="panicForm.comment" type="textarea" rows="2" label="Примітки (реакція лікаря)" outlined dense placeholder="Прийнято до відома, призначено корекцію терапії..."></q-input>
          </q-card-section>
          <q-card-actions align="right" class="bg-grey-1 q-pa-sm">
            <q-btn flat label="Скасувати" color="grey-8" v-close-popup></q-btn>
            <q-btn color="negative" icon="phone_in_talk" label="Зафіксувати інформування лікаря" @click="submitPanicLog"></q-btn>
          </q-card-actions>
        </q-card>
      </q-dialog>

      <!-- MODAL 4: ВИТРЯСКА СТАТИСТИКИ ВКЯ ТА ЗНЯТТЯ LOCKOUT -->
      <q-dialog v-model="qcLockoutDialog">
        <q-card style="min-width: 480px;">
          <q-card-section class="bg-purple text-white row items-center justify-between q-py-sm">
            <div class="text-subtitle1 text-weight-bold">
              <q-icon name="lock" class="q-mr-xs"></q-icon>
              Зняття блокування видачі результатів (QC Lockout)
            </div>
            <q-btn flat round dense icon="close" v-close-popup></q-btn>
          </q-card-section>
          <q-card-section class="q-pa-md">
            <div class="bg-purple-1 text-purple-9 q-pa-sm border-radius q-mb-md text-weight-medium">
              Прилад <strong>Sysmex XN-1000</strong> заблоковано через порушення правила Вестгарда <strong>1-3s (Зсув > 3.2 SD)</strong> для контролю <em>Cellpack Level 2</em>.
            </div>
            <div class="text-caption text-grey-8 q-mb-sm">
              Для відновлення видачі результатів пацієнтів необхідно зафіксувати виконані коригувальні дії:
            </div>
            <q-select v-model="qcActionType" :options="['Промивка гідравлічної системи та повторний контроль', 'Встановлення нового флакона контрольного матеріалу', 'Калібрування оптичного сенсора', 'Технічне обслуговування інженером']" label="Тип коригувальної дії" outlined dense class="q-mb-sm"></q-select>
            <q-input v-model="qcActionComment" type="textarea" rows="2" label="Детальний опис усунення причини" outlined dense placeholder="Виконано Cycle Clean розчином Cellclean, повторний прогін контролю: значення в межах ±1.1 SD..."></q-input>
          </q-card-section>
          <q-card-actions align="right" class="bg-grey-1 q-pa-sm">
            <q-btn flat label="Скасувати" color="grey-8" v-close-popup></q-btn>
            <q-btn color="purple" icon="lock_open" label="Розблокувати аналізатор" @click="resolveQcLockout"></q-btn>
          </q-card-actions>
        </q-card>
      </q-dialog>
"""

if "MODAL 1: ДРУК ТЕРМОШТРИХКОДУ" not in content:
    content = content.replace('</q-page-container>', modals_html + '\n      </q-page-container>')

# 4. Also add click action buttons in phlebotomy, patient, validation tables!
# In phlebotomy: button "Взяти біоматеріал" should trigger openSampleBarcode
content = content.replace("@click=\"notify('Забір виконано')\"", "@click=\"openSampleBarcode(props.row)\"")
# In validation: button "Сповістити лікаря" should trigger openPanicModal
content = content.replace("@click=\"notify('Лікаря відділення сповіщено телефоном')\"", "@click=\"openPanicModal(props.row)\"")
# In patient: button "PDF" should trigger openPdfReport
content = content.replace("@click=\"notify('Завантаження PDF...')\"", "@click=\"openPdfReport(props.row)\"")

# 5. Inject Guide Data & Methods into Vue instance
# Let's find `data: {` inside `<script>`
vue_data_addons = f"""
        showGuide: true,
        guides: {json.dumps(guides_dict, ensure_ascii=False, indent=8)},
        barcodeDialog: false,
        activeSample: {{
          patientName: 'Мельник Юрій Володимирович',
          patientId: 'PT-10482',
          birthYear: '1982',
          orderDate: '06.10.2026',
          barcode: '1026004812',
          tubeType: 'K2 EDTA (Фіолетова)',
          testsList: 'ЗАК + Лейкоцитарна формула + ШОЕ'
        }},
        pdfDialog: false,
        activeReport: {{
          orderNumber: 'ORD-2026-10-0924',
          patientName: 'Коваленко Олена Сергіївна',
          gender: 'Жіноча',
          age: 41,
          doctor: 'Д-р Іванов П.М.',
          department: 'Терапевтичне відділення №1',
          collectedAt: '06.10.2026 08:30',
          completedAt: '06.10.2026 11:15',
          eHealthId: '01HJ89A5K2P89Z1TR0041',
          profileName: 'Печінкові проби + Біохімічний профіль',
          items: [
            {{ testName: 'Аланінамінотрансфераза (АЛТ)', value: '68.5', unit: 'U/L', norm: '0 - 41', flag: 'ВИСОКИЙ', method: 'Кінетичний IFCC / Mindray BS-240', isAbnormal: true }},
            {{ testName: 'Аспартатамінотрансфераза (АСТ)', value: '42.1', unit: 'U/L', norm: '0 - 38', flag: 'ВИСОКИЙ', method: 'Кінетичний IFCC / Mindray BS-240', isAbnormal: true }},
            {{ testName: 'Білірубін загальний', value: '14.2', unit: 'мкмоль/л', norm: '3.4 - 20.5', flag: null, method: 'Колориметричний / Mindray BS-240', isAbnormal: false }},
            {{ testName: 'Глюкоза сироватки', value: '5.1', unit: 'ммоль/л', norm: '4.1 - 5.9', flag: null, method: 'Гексокіназний / Cobas e411', isAbnormal: false }},
            {{ testName: 'Креатинін сироватки', value: '84.0', unit: 'мкмоль/л', norm: '62 - 115', flag: null, method: 'Яффе кінетичний / Mindray BS-240', isAbnormal: false }}
          ]
        }},
        panicDialog: false,
        panicRecord: {{
          patient: 'Мельник Ю.В.',
          test: 'Глюкоза сироватки',
          value: '26.4',
          unit: 'ммоль/л',
          norm: '4.1 - 5.9'
        }},
        panicForm: {{
          doctorNotified: 'Д-р Сидоренко А.В. (Кардіологія)',
          phone: '+38 (067) 123-45-67',
          comment: 'Прийнято до відома, терміново призначено інсулін короткої дії'
        }},
        qcLockoutDialog: false,
        qcActionType: 'Промивка гідравлічної системи та повторний контроль',
        qcActionComment: 'Виконано Cycle Clean розчином Cellclean, повторний прогін контролю: значення в межах ±1.1 SD.',
"""

if "showGuide: true" not in content:
    content = content.replace("data: {", "data: {\n" + vue_data_addons)

# Add computed: currentGuide
computed_code = """
      currentGuide() {
        return this.guides[this.currentView] || this.guides['workstation'];
      },
"""
if "currentGuide()" not in content:
    content = content.replace("computed: {", "computed: {\n" + computed_code)

# Add interactive methods
methods_code = """
      openSampleBarcode(row) {
        if (row) {
          this.activeSample.patientName = row.patient || row.name || this.activeSample.patientName;
          this.activeSample.barcode = row.barcode || '102600' + Math.floor(1000 + Math.random() * 9000);
          this.activeSample.patientId = row.patientId || 'PT-' + Math.floor(10000 + Math.random() * 90000);
          this.activeSample.tubeType = row.tubeType || 'K2 EDTA (Фіолетова)';
          this.activeSample.testsList = row.tests || 'ЗАК + Лейкоформула';
        }
        this.barcodeDialog = true;
      },
      confirmPrintBarcode() {
        this.barcodeDialog = false;
        this.notify('Штрихкод ' + this.activeSample.barcode + ' успішно відправлено на термопринтер Zebra!');
        // Update status of first sample in phlebotomy if available
        if (this.phlebotomyQueue && this.phlebotomyQueue.length > 0) {
          this.phlebotomyQueue[0].status = 'Забір виконано';
        }
      },
      openPdfReport(row) {
        if (row && row.patient) {
          this.activeReport.patientName = row.patient;
          this.activeReport.orderNumber = row.orderNumber || this.activeReport.orderNumber;
        }
        this.pdfDialog = true;
      },
      windowPrint() {
        window.print();
      },
      openPanicModal(row) {
        if (row) {
          this.panicRecord.patient = row.patient || this.panicRecord.patient;
          this.panicRecord.test = row.test || this.panicRecord.test;
          this.panicRecord.value = row.value || this.panicRecord.value;
          this.panicRecord.unit = row.unit || this.panicRecord.unit;
          this.panicRecord.norm = row.norm || this.panicRecord.norm;
        }
        this.panicDialog = true;
      },
      submitPanicLog() {
        this.panicDialog = false;
        this.notify('Сповіщення лікаря стаціонару зафіксовано в журналі аудиту ISO 15189!');
        // Update flag
        if (this.filteredWorklist && this.filteredWorklist[0]) {
          this.filteredWorklist[0].status = 'Очікує лікаря (Сповіщено)';
        }
      },
      resolveQcLockout() {
        this.qcLockoutDialog = false;
        this.notify('Блокування аналізатора Sysmex XN-1000 знято! Коригувальні дії зафіксовано.');
      },
      runProcessDemo() {
        const v = this.currentView;
        if (v === 'workstation') {
          // Add new incoming test result from Sysmex
          const newId = this.worklist.length + 1;
          this.worklist.unshift({
            id: newId,
            barcode: '1026004835',
            patient: 'Бондаренко І.В.',
            analyzer: 'Sysmex XN-1000',
            test: 'Тромбоцити (PLT)',
            value: '245',
            unit: '10^9/л',
            normMin: 150,
            normMax: 400,
            flag: 'NORMAL',
            deltaPercent: '+4.2%',
            status: 'NEW'
          });
          this.$q.notify({
            type: 'positive',
            message: 'Шлюз .NET 8 прийняв пакет ASTM E1394 від Sysmex XN-1000! Додано новий тест: PLT = 245 10^9/л.',
            position: 'top',
            timeout: 3000
          });
          setTimeout(() => {
            this.runAutoValidation();
          }, 1200);
        } else if (v === 'phlebotomy') {
          this.openSampleBarcode(this.phlebotomyQueue[0]);
        } else if (v === 'logistics') {
          this.$q.notify({
            type: 'info',
            message: 'Сформовано кур’єрський маніфест № TR-2026-10-04. Температура термобокса: +4.2°C. Зразки передано в доставку.',
            position: 'top',
            timeout: 3500
          });
        } else if (v === 'validation') {
          this.openPanicModal(null);
        } else if (v === 'qc') {
          this.qcLockoutDialog = true;
        } else if (v === 'patient') {
          this.openPdfReport(null);
        } else if (v === 'biobank') {
          this.$q.notify({
            type: 'positive',
            message: 'Зразок #1026004819 автоматично розміщено в архівний кріобокс: Секція B / Штатив 02 / Комірка C-05 (-20°C).',
            position: 'top',
            timeout: 3500
          });
        } else if (v === 'microbiology') {
          this.$q.notify({
            type: 'warning',
            message: 'EUCAST v14.0 розрахунок: E. coli проти Ципрофлоксацину (Зона 18 мм) → РЕЗИСТЕНТНИЙ (R). Рекомендовано змінити терапію на Меропенем.',
            position: 'top',
            timeout: 4000
          });
        } else {
          this.notify('Інтерактивний сценарій для ' + this.currentGuide.title + ' успішно виконано!');
        }
      },
"""

if "runProcessDemo()" not in content:
    content = content.replace("methods: {", "methods: {\n" + methods_code)

# Write updated run_prototype.html
with open(PROTOTYPE_PATH, "w", encoding="utf-8") as f:
    f.write(content)

# Also write index.html
with open(INDEX_PATH, "w", encoding="utf-8") as f:
    f.write(content)

print("Enrichment complete! run_prototype.html and index.html updated successfully.")
