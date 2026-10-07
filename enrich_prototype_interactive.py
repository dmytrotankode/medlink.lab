# -*- coding: utf-8 -*-
"""
Script to enrich medlink_lab_frontend/run_prototype.html and index.html
with full interactive guided walkthroughs (Що, Чому, Звідки) for every process,
action modals (Barcode print, PDF report, CITO confirmation, ASTM query, QC lockout),
and live state-changing demo routines.
"""

import os
import re

BASE_DIR = r"C:\__MEDLINK___\LABA"
PROTOTYPE_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "run_prototype.html")
INDEX_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "index.html")

# Read current prototype
with open(PROTOTYPE_PATH, "r", encoding="utf-8") as f:
    html = f.read()

# Let's inspect CSS and add guide box styles
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

if ".process-guide-card" not in html:
    html = html.replace("</style>", guide_css + "\n  </style>")

# Guide component markup to put inside page-container right above view contents
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

# Check where to insert guide_component_html: right after <q-page class="q-pa-md">
if "GLOBAL INTERACTIVE PROCESS GUIDE" not in html:
    html = html.replace('<q-page class="q-pa-md">', '<q-page class="q-pa-md">\n' + guide_component_html)

# Now let's check action modals to add before closing </q-page> or </q-page-container>
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
              Термодрук Zebra ZD220 / GK420t (Етикетка 50x25 мм або 40x25 мм).<br>
              CLSI H3-A6: Наклеювати строго вертикально штрихами вздовж пробірки.
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
              Згідно зі стандартом ДСТУ EN ISO 15189 розд. 5.7, лаборант зобов'язаний негайно телефоном проінформувати лікуючого лікаря та зафіксувати це в журналі:
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

# Check where to insert modals_html: right before </q-page-container>
if "MODAL 1: ДРУК ТЕРМОШТРИХКОДУ" not in html:
    html = html.replace('</q-page-container>', modals_html + '\n      </q-page-container>')

print("HTML template elements prepared.")

# Now update the Vue script data and methods
# Let's inspect where Vue instance starts
with open("update_vue_instance.py", "w", encoding="utf-8") as f:
    f.write("""# -*- coding: utf-8 -*-
with open(r"C:\\__MEDLINK___\\LABA\\medlink_lab_frontend\\run_prototype.html", "r", encoding="utf-8") as f:
    content = f.read()

# Let's write the updated script section with complete guides dictionary and interactive methods
""")

print("Ready to process Vue script.")
