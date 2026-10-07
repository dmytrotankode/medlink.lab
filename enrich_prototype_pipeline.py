# -*- coding: utf-8 -*-
"""
Inject End-to-End Pipeline Tracker, Real API integration, and MedLink Integration Blueprint
into run_prototype.html and index.html
"""

import os
import re

BASE_DIR = r"C:\__MEDLINK___\LABA"
PROTOTYPE_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "run_prototype.html")
INDEX_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "index.html")

with open(PROTOTYPE_PATH, "r", encoding="utf-8") as f:
    html = f.read()

# CSS for Pipeline Bar
pipeline_css = """
    /* End-to-End Real API Pipeline Stepper */
    .pipeline-stepper-card {
      background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
      color: #ffffff;
      border-radius: 8px;
      padding: 12px 18px;
      margin-bottom: 16px;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.2);
      border: 1px solid #334155;
    }
    .pipeline-step-item {
      display: flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
      padding: 6px 10px;
      border-radius: 6px;
      transition: all 0.2s;
      background: rgba(255, 255, 255, 0.05);
      border: 1px solid transparent;
      font-size: 12px;
    }
    .pipeline-step-item:hover {
      background: rgba(255, 255, 255, 0.15);
    }
    .pipeline-step-active {
      background: rgba(1, 120, 188, 0.35) !important;
      border-color: #38bdf8 !important;
      color: #ffffff;
      font-weight: 700;
    }
    .pipeline-step-done {
      border-color: #22c55e !important;
      color: #86efac;
    }
    .pipeline-num-badge {
      width: 22px;
      height: 22px;
      border-radius: 50%;
      background: #475569;
      color: #ffffff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 11px;
      font-weight: 700;
    }
    .pipeline-step-active .pipeline-num-badge {
      background: #0284c7;
    }
    .pipeline-step-done .pipeline-num-badge {
      background: #16a34a;
    }

    /* MedLink Integration Context Panel */
    .medlink-extend-banner {
      background: #eff6ff;
      border: 1px solid #bfdbfe;
      border-left: 4px solid #2563eb;
      border-radius: 6px;
      padding: 10px 14px;
      margin-bottom: 14px;
      font-size: 12.5px;
      color: #1e3a8a;
    }
"""

if ".pipeline-stepper-card" not in html:
    html = html.replace("</style>", pipeline_css + "\n  </style>")

# HTML Stepper Bar Markup
stepper_html = """
          <!-- END-TO-END PIPELINE TRACKER (REAL LOCAL SQLITE API) -->
          <div class="pipeline-stepper-card q-mb-md">
            <div class="row items-center justify-between q-mb-xs">
              <div class="row items-center q-gutter-x-sm">
                <q-badge color="teal" text-color="white" label="REAL API + LOCAL SQL" class="text-weight-bold"></q-badge>
                <span class="text-subtitle2 text-weight-bold text-white">
                  Наскрізний покроковий процес: від е-Направлення до Кабінету пацієнта
                </span>
              </div>
              <div class="row items-center q-gutter-x-xs">
                <q-btn size="sm" dense color="primary" unelevated icon="fast_forward" :label="'Крок ' + apiPipelineStep + ': Виконати в API'" @click="advancePipelineStep"></q-btn>
                <q-btn size="sm" dense flat color="grey-4" icon="replay" label="Скинути на початок" @click="resetPipeline"></q-btn>
              </div>
            </div>

            <!-- Steps Bar -->
            <div class="row q-col-gutter-xs items-center justify-between q-mt-xs text-caption">
              <div class="col-12 col-md pipeline-step-item" :class="{ 'pipeline-step-active': apiPipelineStep === 1, 'pipeline-step-done': apiPipelineStep > 1 }" @click="goToPipelineStep(1)">
                <div class="pipeline-num-badge">1</div>
                <div class="ellipsis">1. Е-направлення<br><span style="font-size:10px; color:#94a3b8;">incomingReferral</span></div>
              </div>
              <div class="col-12 col-md pipeline-step-item" :class="{ 'pipeline-step-active': apiPipelineStep === 2, 'pipeline-step-done': apiPipelineStep > 2 }" @click="goToPipelineStep(2)">
                <div class="pipeline-num-badge">2</div>
                <div class="ellipsis">2. Забір & Штрихкод<br><span style="font-size:10px; color:#94a3b8;">phlebotomy</span></div>
              </div>
              <div class="col-12 col-md pipeline-step-item" :class="{ 'pipeline-step-active': apiPipelineStep === 3, 'pipeline-step-done': apiPipelineStep > 3 }" @click="goToPipelineStep(3)">
                <div class="pipeline-num-badge">3</div>
                <div class="ellipsis">3. Кур'єр & Бокс<br><span style="font-size:10px; color:#94a3b8;">logistics (+4°C)</span></div>
              </div>
              <div class="col-12 col-md pipeline-step-item" :class="{ 'pipeline-step-active': apiPipelineStep === 4, 'pipeline-step-done': apiPipelineStep > 4 }" @click="goToPipelineStep(4)">
                <div class="pipeline-num-badge">4</div>
                <div class="ellipsis">4. Прийом у КДЛ<br><span style="font-size:10px; color:#94a3b8;">бракераж зразка</span></div>
              </div>
              <div class="col-12 col-md pipeline-step-item" :class="{ 'pipeline-step-active': apiPipelineStep === 5, 'pipeline-step-done': apiPipelineStep > 5 }" @click="goToPipelineStep(5)">
                <div class="pipeline-num-badge">5</div>
                <div class="ellipsis">5. Аналізатор ASTM<br><span style="font-size:10px; color:#94a3b8;">workstation</span></div>
              </div>
              <div class="col-12 col-md pipeline-step-item" :class="{ 'pipeline-step-active': apiPipelineStep === 6, 'pipeline-step-done': apiPipelineStep > 6 }" @click="goToPipelineStep(6)">
                <div class="pipeline-num-badge">6</div>
                <div class="ellipsis">6. Валідація & КЕП<br><span style="font-size:10px; color:#94a3b8;">diagnosticReport</span></div>
              </div>
              <div class="col-12 col-md pipeline-step-item" :class="{ 'pipeline-step-active': apiPipelineStep === 7, 'pipeline-step-done': apiPipelineStep >= 7 }" @click="goToPipelineStep(7)">
                <div class="pipeline-num-badge">7</div>
                <div class="ellipsis">7. Кабінет пацієнта<br><span style="font-size:10px; color:#94a3b8;">трекінг & PDF</span></div>
              </div>
            </div>

            <!-- Live Status Text & DB Feedback -->
            <div class="row items-center justify-between q-mt-sm q-pt-xs" style="border-top: 1px solid #334155; font-size: 11px;">
              <div>
                <q-icon name="storage" color="cyan-3"></q-icon> <strong>Локальна SQL БД:</strong>
                Зразок <code>{{ pipelineDbState.barcode || '1026004812' }}</code> |
                Пацієнт: <strong>{{ pipelineDbState.patient || 'Мельник Ю.В.' }}</strong> |
                Статус в базі: <q-badge :color="getPipelineStatusColor(pipelineDbState.status)" :label="pipelineDbState.status || 'NEW'" dense></q-badge>
              </div>
              <div class="text-grey-4">
                Ендпоінт: <code>{{ pipelineDbState.lastEndpoint || 'GET /api/laboratory/pipeline/state' }}</code>
              </div>
            </div>
          </div>

          <!-- MEDLINK INTEGRATION ARCHITECTURE BANNER -->
          <div class="medlink-extend-banner q-mb-md">
            <div class="row items-center justify-between">
              <div class="row items-center q-gutter-x-sm">
                <q-icon name="integration_instructions" color="primary" size="20px"></q-icon>
                <strong>Як це доповнює існуючий MedLink (evomis), не ламаючи поточні форми:</strong>
              </div>
              <q-btn flat dense size="xs" color="primary" :label="showIntegrationNotes ? 'Приховати інструкцію' : 'Показати деталі інтеграції'" @click="showIntegrationNotes = !showIntegrationNotes"></q-btn>
            </div>
            <div v-show="showIntegrationNotes" class="q-mt-xs" style="line-height: 1.5;">
              <div class="row q-col-gutter-sm q-mt-xs">
                <div class="col-12 col-md-3">
                  <strong>1. Направлення:</strong> В існуючому вікні <code>incomingMedicalReferral</code> додається кнопка <em>[Відправити в лабораторію]</em>, що автоматично створює запис у <code>lab_orders</code>.
                </div>
                <div class="col-12 col-md-3">
                  <strong>2. Меню навігації:</strong> В існуючий <code>menuDrawer.vue</code> та <code>baseElements.js</code> додається секція <em>«Лабораторія (ЛІС)»</em> без змін інших пунктів.
                </div>
                <div class="col-12 col-md-3">
                  <strong>3. Діагностичні звіти:</strong> Після валідації в ЛІС автоматично створюється запис у <code>mis_diagnostic_report</code> з LOINC-кодами для пакетного підпису КЕП.
                </div>
                <div class="col-12 col-md-3">
                  <strong>4. Картка пацієнта:</strong> В існуючу форму <code>patient.vue</code> додається вкладка <em>«Лабораторні дослідження»</em> з переглядом графіків динаміки.
                </div>
              </div>
            </div>
          </div>
"""

# Insert stepper_html right above the GLOBAL INTERACTIVE PROCESS GUIDE
if "END-TO-END PIPELINE TRACKER" not in html:
    html = html.replace('<div class="process-guide-card q-mb-md">', stepper_html + '\n          <div class="process-guide-card q-mb-md">', 1)

# Vue data additions for Pipeline
pipeline_data_props = """
        apiPipelineStep: 4,
        showIntegrationNotes: true,
        pipelineDbState: {
          barcode: '1026004812',
          patient: 'Мельник Юрій Володимирович',
          status: 'ANALYZING',
          lastEndpoint: 'GET /api/laboratory/pipeline/state'
        },
"""

if "apiPipelineStep:" not in html:
    html = html.replace("data: {", "data: {\n" + pipeline_data_props)

# Vue methods for Pipeline
pipeline_methods = """
      fetchPipelineState() {
        fetch('/api/laboratory/pipeline/state')
          .then(res => res.json())
          .then(data => {
            if (data.success) {
              this.apiPipelineStep = data.currentStep || 1;
              if (data.orderSample) {
                this.pipelineDbState.barcode = data.orderSample.barcode;
                this.pipelineDbState.status = data.orderSample.order_status || data.orderSample.sample_status;
              }
              if (data.referral && data.referral.patient_name) {
                this.pipelineDbState.patient = data.referral.patient_name;
              }
            }
          })
          .catch(e => console.log('API state fetch fallback:', e));
      },
      advancePipelineStep() {
        const nextStep = this.apiPipelineStep >= 7 ? 1 : this.apiPipelineStep + 1;
        fetch('/api/laboratory/pipeline/advance', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ targetStep: nextStep })
        })
          .then(res => res.json())
          .then(data => {
            if (data.success) {
              this.apiPipelineStep = nextStep;
              this.pipelineDbState.lastEndpoint = 'POST /api/laboratory/pipeline/advance';
              this.$q.notify({
                type: 'positive',
                message: data.message,
                position: 'top',
                timeout: 3500
              });
              this.goToPipelineStep(nextStep);
              this.fetchWorklistFromApi();
            }
          })
          .catch(e => {
            this.apiPipelineStep = nextStep;
            this.goToPipelineStep(nextStep);
          });
      },
      resetPipeline() {
        fetch('/api/laboratory/pipeline/reset', { method: 'POST' })
          .then(res => res.json())
          .then(data => {
            this.apiPipelineStep = 1;
            this.pipelineDbState.status = 'NEW';
            this.pipelineDbState.lastEndpoint = 'POST /api/laboratory/pipeline/reset';
            this.$q.notify({
              type: 'info',
              message: data.message,
              position: 'top',
              timeout: 3000
            });
            this.goToPipelineStep(1);
          })
          .catch(e => {
            this.apiPipelineStep = 1;
            this.goToPipelineStep(1);
          });
      },
      goToPipelineStep(step) {
        this.apiPipelineStep = step;
        if (step === 1) {
          this.setView('phlebotomy', 'Вхідні е-направлення (incomingReferral)');
        } else if (step === 2) {
          this.setView('phlebotomy', 'Пункт забору біоматеріалу (Забір & Друк)');
          if (this.phlebotomyQueue && this.phlebotomyQueue[0]) {
            this.openSampleBarcode(this.phlebotomyQueue[0]);
          }
        } else if (step === 3 || step === 4) {
          this.setView('logistics', 'Логістика & Кур’єри (+4°C)');
        } else if (step === 5) {
          this.setView('workstation', 'Робочий стіл лаборанта (Прийом ASTM)');
        } else if (step === 6) {
          this.setView('validation', 'Валідація результатів & КЕП');
        } else if (step === 7) {
          this.setView('patient', 'Кабінет пацієнта (Результати & PDF)');
          this.openPdfReport(null);
        }
      },
      fetchWorklistFromApi() {
        fetch('/api/laboratory/worklist')
          .then(res => res.json())
          .then(data => {
            if (data.success && data.data && data.data.length > 0) {
              this.worklist = data.data;
            }
          })
          .catch(e => console.log('Worklist fallback:', e));
      },
      getPipelineStatusColor(status) {
        if (!status) return 'grey';
        if (status.includes('COMPLETED') || status.includes('VERIFIED')) return 'positive';
        if (status.includes('ANALYZING') || status.includes('TRANSIT')) return 'primary';
        if (status.includes('COLLECTED') || status.includes('RECEIVED')) return 'teal';
        return 'warning';
      },
"""

if "fetchPipelineState()" not in html:
    html = html.replace("methods: {", "methods: {\n" + pipeline_methods)

# In mounted hook, call fetchPipelineState and fetchWorklistFromApi
if "this.fetchPipelineState()" not in html:
    html = html.replace("mounted() {", "mounted() {\n    this.fetchPipelineState();\n    this.fetchWorklistFromApi();")

# Save to run_prototype.html
with open(PROTOTYPE_PATH, "w", encoding="utf-8") as f:
    f.write(html)

# Also save to index.html
with open(INDEX_PATH, "w", encoding="utf-8") as f:
    f.write(html)

print("Pipeline stepper and MedLink integration banner successfully injected!")
