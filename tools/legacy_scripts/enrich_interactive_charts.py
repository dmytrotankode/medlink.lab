# -*- coding: utf-8 -*-
"""
Inject rich interactive charts and visualizations into run_prototype.html and index.html:
1. Quality Control (QC): Interactive Levey-Jennings SVG Chart with Mean, +/-1s, +/-2s, +/-3s lines,
   points 1-20, Westgard rule flags (1-3s, 2-2s, 1-2s), hover/click point inspector, parameter switcher.
2. Patient Portal: Interactive Trend & Reference Corridor Chart (Historical Glucose over 5 visits with norm corridor).
3. Biobank: Interactive 8x12 Cryo-Rack Grid (96 cells A01..H12) with cell selection and sample inspector.
4. Microbiology: EUCAST 2026 Interactive Zone Diameter & S/I/R Sensitivity visualizer.
5. TAT Analytics: Interactive Stage Waterfall / SLA Breakdown (Pre-analytical, Analytical, Post-analytical).
6. Logistics: Interactive Cold Chain Temperature Track (+2..+8 C corridor and alert spike).
"""

import os
import re

BASE_DIR = r"C:\__MEDLINK___\LABA"
PROTOTYPE_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "run_prototype.html")
INDEX_PATH = os.path.join(BASE_DIR, "medlink_lab_frontend", "index.html")

with open(PROTOTYPE_PATH, "r", encoding="utf-8") as f:
    html = f.read()

# 1. CSS FOR INTERACTIVE CHARTS
charts_css = """
    /* --- INTERACTIVE CHARTS & VISUALIZATIONS --- */
    .levey-jennings-container {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      padding: 16px;
      position: relative;
    }
    .lj-svg {
      width: 100%;
      height: 280px;
      overflow: visible;
    }
    .lj-grid-line {
      stroke-dasharray: 4, 4;
      stroke-width: 1.2;
    }
    .lj-point {
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .lj-point:hover {
      r: 7;
      stroke: #0284c7;
      stroke-width: 3;
    }
    .lj-point-violation {
      animation: pulse-ring 1.5s infinite;
    }
    @keyframes pulse-ring {
      0% { transform: scale(0.95); opacity: 1; }
      50% { transform: scale(1.15); opacity: 0.8; }
      100% { transform: scale(0.95); opacity: 1; }
    }
    .trend-corridor {
      fill: rgba(34, 197, 94, 0.12);
      stroke: rgba(34, 197, 94, 0.4);
      stroke-dasharray: 4, 4;
    }
    .cryo-grid-8x12 {
      display: grid;
      grid-template-columns: repeat(12, 1fr);
      gap: 5px;
      background: #f1f5f9;
      padding: 10px;
      border-radius: 8px;
      border: 1px solid #cbd5e1;
    }
    .cryo-cell {
      aspect-ratio: 1;
      border-radius: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 10px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s;
      user-select: none;
      border: 1px solid transparent;
    }
    .cryo-cell:hover {
      transform: scale(1.12);
      z-index: 2;
      box-shadow: 0 2px 6px rgba(0,0,0,0.25);
    }
    .cryo-cell-empty {
      background: #ffffff;
      color: #94a3b8;
      border-color: #e2e8f0;
    }
    .cryo-cell-serum {
      background: #dc2626;
      color: #ffffff;
    }
    .cryo-cell-plasma {
      background: #9333ea;
      color: #ffffff;
    }
    .cryo-cell-edta {
      background: #2563eb;
      color: #ffffff;
    }
    .cryo-cell-warning {
      background: #f59e0b;
      color: #ffffff;
    }
    .cryo-cell-selected {
      border: 2px solid #000000 !important;
      box-shadow: 0 0 0 3px #38bdf8;
      transform: scale(1.15);
      z-index: 3;
    }
    .eucast-scale {
      height: 18px;
      border-radius: 9px;
      position: relative;
      background: linear-gradient(to right, #ef4444 0%, #ef4444 40%, #f59e0b 40%, #f59e0b 60%, #22c55e 60%, #22c55e 100%);
    }
    .eucast-pointer {
      position: absolute;
      top: -4px;
      width: 6px;
      height: 26px;
      background: #0f172a;
      border-radius: 3px;
      box-shadow: 0 0 4px rgba(0,0,0,0.5);
      transform: translateX(-50%);
    }
    .tat-waterfall-bar {
      height: 24px;
      border-radius: 4px;
      display: flex;
      align-items: center;
      padding: 0 8px;
      font-size: 11px;
      font-weight: 700;
      color: #ffffff;
      transition: width 0.4s ease;
    }
"""

if ".levey-jennings-container" not in html:
    html = html.replace("</style>", charts_css + "\n  </style>")

# 2. ENHANCE QC VIEW WITH LEVEY-JENNINGS CHART
new_qc_template = """
          <!-- VIEW: QUALITY CONTROL (QC) -->
          <div v-show="currentView === 'qc'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-chart-line" class="q-mr-sm"></q-icon>
                  Внутрішній контроль якості (ВКЯ) - Карта Леві-Дженнінгса & Вестгард
                </h5>
                <div class="text-caption text-grey-7">Оцінка відтворюваності, правила 1-3s, 2-2s, R-4s, 4-1s, 10-x, автоматичний Lockout аналізатора.</div>
              </div>
              <div class="row items-center q-gutter-x-sm">
                <q-btn-toggle
                  v-model="selectedQcParam"
                  toggle-color="primary"
                  size="sm"
                  dense
                  :options="[
                    {label: 'WBC (Лейкоцити)', value: 'WBC'},
                    {label: 'GLU (Глюкоза)', value: 'GLU'},
                    {label: 'HGB (Гемоглобін)', value: 'HGB'},
                    {label: 'ALT (АЛТ)', value: 'ALT'}
                  ]"
                  @input="onQcParamChange"
                ></q-btn-toggle>
                <q-btn color="negative" icon="lock_open" label="Скинути блокування аналізатора" dense unelevated @click="showQcUnlockDialog = true"></q-btn>
              </div>
            </div>

            <!-- LOCKOUT BANNER -->
            <q-banner dense rounded class="bg-red-1 text-negative q-mb-md" inline-actions style="border-left: 5px solid #dc2626;">
              <template v-slot:avatar><q-icon name="lock" color="negative" size="30px"></q-icon></template>
              <div class="text-subtitle2 text-weight-bold">
                УВАГА: Аналізатор {{ qcData.analyzer }} заблоковано через порушення правила Вестгарда 1-3s!
              </div>
              <div class="text-caption">
                Матеріал: <strong>{{ qcData.controlMaterial }}</strong> (Лот: {{ qcData.lotNumber }}). Останній вимір (День 18): <strong class="text-negative">8.28 10*9/л</strong> (Ціль: {{ qcData.targetMean }} &plusmn; 3SD [8.10]). Видача результатів заморожена до усунення.
              </div>
            </q-banner>

            <!-- STATS SUMMARY CARDS -->
            <div class="row q-col-gutter-sm q-mb-md">
              <div class="col-6 col-md-2"><q-card flat bordered class="q-pa-xs text-center"><div class="text-caption text-grey-7">Цільове Mean</div><div class="text-subtitle1 text-weight-bold text-primary">{{ qcData.targetMean }}</div></q-card></div>
              <div class="col-6 col-md-2"><q-card flat bordered class="q-pa-xs text-center"><div class="text-caption text-grey-7">Стандартне SD</div><div class="text-subtitle1 text-weight-bold text-indigo">&plusmn;{{ qcData.targetSd }}</div></q-card></div>
              <div class="col-6 col-md-2"><q-card flat bordered class="q-pa-xs text-center"><div class="text-caption text-grey-7">Варіація CV%</div><div class="text-subtitle1 text-weight-bold text-teal">{{ qcData.cvPercent }}%</div></q-card></div>
              <div class="col-6 col-md-2"><q-card flat bordered class="q-pa-xs text-center"><div class="text-caption text-grey-7">+3SD / -3SD</div><div class="text-subtitle2 text-weight-bold text-negative">8.10 / 6.30</div></q-card></div>
              <div class="col-6 col-md-2"><q-card flat bordered class="q-pa-xs text-center"><div class="text-caption text-grey-7">+2SD / -2SD</div><div class="text-subtitle2 text-weight-bold text-amber-9">7.80 / 6.60</div></q-card></div>
              <div class="col-6 col-md-2"><q-card flat bordered class="q-pa-xs text-center"><div class="text-caption text-grey-7">Поточний Z-Score</div><div class="text-subtitle1 text-weight-bold text-negative">+3.60 s</div></q-card></div>
            </div>

            <!-- INTERACTIVE LEVEY-JENNINGS SVG CHART -->
            <q-card flat bordered class="q-mb-md levey-jennings-container">
              <div class="row items-center justify-between q-mb-sm">
                <div class="row items-center q-gutter-x-sm">
                  <span class="text-subtitle2 text-weight-bold">
                    Графік Леві-Дженнінгса за останні 20 серій (Sysmex XN-1000, WBC)
                  </span>
                  <q-badge color="grey-3" text-color="dark" label="Контроль Рівень 2"></q-badge>
                </div>
                <div class="row items-center q-gutter-x-md text-caption">
                  <span class="row items-center"><span style="width:10px; height:10px; background:#22c55e; border-radius:50%; display:inline-block; margin-right:4px;"></span> Норма (&plusmn;2s)</span>
                  <span class="row items-center"><span style="width:10px; height:10px; background:#f59e0b; border-radius:50%; display:inline-block; margin-right:4px;"></span> Попередження 1-2s</span>
                  <span class="row items-center"><span style="width:10px; height:10px; background:#ef4444; border-radius:50%; display:inline-block; margin-right:4px;"></span> Порушення 1-3s (LOCK)</span>
                </div>
              </div>

              <!-- SVG CANVAS -->
              <svg class="lj-svg" viewBox="0 0 900 280">
                <!-- Zone fills -->
                <rect x="60" y="20" width="810" height="40" fill="#fee2e2" opacity="0.6"></rect>
                <rect x="60" y="60" width="810" height="40" fill="#fef3c7" opacity="0.6"></rect>
                <rect x="60" y="100" width="810" height="80" fill="#dcfce7" opacity="0.5"></rect>
                <rect x="60" y="180" width="810" height="40" fill="#fef3c7" opacity="0.6"></rect>
                <rect x="60" y="220" width="810" height="40" fill="#fee2e2" opacity="0.6"></rect>

                <!-- Grid & SD lines -->
                <!-- +3SD (y=20, val 8.10) -->
                <line x1="60" y1="20" x2="870" y2="20" stroke="#ef4444" stroke-width="1.5" class="lj-grid-line"></line>
                <text x="50" y="24" fill="#dc2626" font-size="11" font-weight="700" text-anchor="end">+3SD (8.10)</text>

                <!-- +2SD (y=60, val 7.80) -->
                <line x1="60" y1="60" x2="870" y2="60" stroke="#f59e0b" stroke-width="1.2" class="lj-grid-line"></line>
                <text x="50" y="64" fill="#d97706" font-size="11" font-weight="600" text-anchor="end">+2SD (7.80)</text>

                <!-- +1SD (y=100, val 7.50) -->
                <line x1="60" y1="100" x2="870" y2="100" stroke="#16a34a" stroke-width="1" class="lj-grid-line"></line>
                <text x="50" y="104" fill="#15803d" font-size="11" text-anchor="end">+1SD (7.50)</text>

                <!-- Mean (y=140, val 7.20) -->
                <line x1="60" y1="140" x2="870" y2="140" stroke="#0284c7" stroke-width="2.5"></line>
                <text x="50" y="144" fill="#0369a1" font-size="11" font-weight="700" text-anchor="end">Mean (7.20)</text>

                <!-- -1SD (y=180, val 6.90) -->
                <line x1="60" y1="180" x2="870" y2="180" stroke="#16a34a" stroke-width="1" class="lj-grid-line"></line>
                <text x="50" y="184" fill="#15803d" font-size="11" text-anchor="end">-1SD (6.90)</text>

                <!-- -2SD (y=220, val 6.60) -->
                <line x1="60" y1="220" x2="870" y2="220" stroke="#f59e0b" stroke-width="1.2" class="lj-grid-line"></line>
                <text x="50" y="224" fill="#d97706" font-size="11" font-weight="600" text-anchor="end">-2SD (6.60)</text>

                <!-- -3SD (y=260, val 6.30) -->
                <line x1="60" y1="260" x2="870" y2="260" stroke="#ef4444" stroke-width="1.5" class="lj-grid-line"></line>
                <text x="50" y="264" fill="#dc2626" font-size="11" font-weight="700" text-anchor="end">-3SD (6.30)</text>

                <!-- Connecting Polyline -->
                <polyline
                  :points="ljPolylinePoints"
                  fill="none"
                  stroke="#3b82f6"
                  stroke-width="2.5"
                  stroke-linejoin="round"
                ></polyline>

                <!-- Interactive Data Points -->
                <g v-for="(p, idx) in ljPoints" :key="'ljpt-'+idx">
                  <!-- Vertical tick line -->
                  <line :x1="p.x" y1="20" :x2="p.x" y2="260" stroke="#e2e8f0" stroke-width="1" stroke-dasharray="2, 4"></line>
                  <!-- X-Axis Day label -->
                  <text :x="p.x" y="275" font-size="9.5" fill="#64748b" text-anchor="middle">Д{{ p.day }}</text>

                  <!-- Halo circle for violation -->
                  <circle v-if="p.status === 'VIOLATION_1_3S'" :cx="p.x" :cy="p.y" r="12" fill="#ef4444" opacity="0.35" class="lj-point-violation"></circle>

                  <!-- Main Circle -->
                  <circle
                    :cx="p.x"
                    :cy="p.y"
                    :r="selectedLjPointIndex === idx ? 7 : (p.status === 'VIOLATION_1_3S' ? 6.5 : 5)"
                    :fill="p.status === 'VIOLATION_1_3S' ? '#dc2626' : (p.status === 'WARN_1_2S' ? '#d97706' : '#16a34a')"
                    :stroke="selectedLjPointIndex === idx ? '#0f172a' : '#ffffff'"
                    :stroke-width="selectedLjPointIndex === idx ? 2.5 : 1.5"
                    class="lj-point"
                    @click="selectLjPoint(idx)"
                  ></circle>
                </g>
              </svg>

              <!-- Selected Point Details Banner -->
              <div class="row items-center justify-between q-mt-sm q-pa-sm bg-grey-1 rounded-borders" style="border: 1px solid #e2e8f0; font-size: 12px;">
                <div>
                  <strong>Обрана контрольна точка: День {{ activeLjPoint.day }} ({{ activeLjPoint.date }})</strong> |
                  Виміряно: <strong :class="activeLjPoint.status === 'VIOLATION_1_3S' ? 'text-negative text-h6' : 'text-primary'">{{ activeLjPoint.val }} {{ qcData.unit }}</strong> |
                  Z-Score: <strong>{{ activeLjPoint.zScore > 0 ? '+' : '' }}{{ activeLjPoint.zScore }} s</strong> |
                  Статус: <q-badge :color="activeLjPoint.status === 'VIOLATION_1_3S' ? 'negative' : (activeLjPoint.status === 'WARN_1_2S' ? 'warning' : 'positive')">{{ activeLjPoint.ruleLabel }}</q-badge>
                </div>
                <div class="text-caption text-grey-7">
                  Оператор: {{ activeLjPoint.operator }} | Час: {{ activeLjPoint.time }}
                </div>
              </div>
            </q-card>

            <!-- WESTGARD RULES STATUS CARDS -->
            <div class="row q-col-gutter-sm q-mb-md">
              <div class="col-12 col-md-2">
                <q-card flat bordered class="q-pa-xs text-center bg-red-1" style="border: 2px solid #ef4444;">
                  <div class="text-caption text-weight-bold text-negative">1-3s (LOCKOUT)</div>
                  <div class="text-caption text-grey-8">1 вимір > 3SD</div>
                  <q-badge color="negative" label="ПОРУШЕНО" class="q-mt-xs"></q-badge>
                </q-card>
              </div>
              <div class="col-12 col-md-2">
                <q-card flat bordered class="q-pa-xs text-center bg-green-1">
                  <div class="text-caption text-weight-bold text-positive">2-2s (Системне)</div>
                  <div class="text-caption text-grey-8">2 поспіль > 2SD</div>
                  <q-badge color="positive" label="В нормі" class="q-mt-xs"></q-badge>
                </q-card>
              </div>
              <div class="col-12 col-md-2">
                <q-card flat bordered class="q-pa-xs text-center bg-green-1">
                  <div class="text-caption text-weight-bold text-positive">R-4s (Розмах)</div>
                  <div class="text-caption text-grey-8">Різниця > 4SD</div>
                  <q-badge color="positive" label="В нормі" class="q-mt-xs"></q-badge>
                </q-card>
              </div>
              <div class="col-12 col-md-2">
                <q-card flat bordered class="q-pa-xs text-center bg-amber-1">
                  <div class="text-caption text-weight-bold text-warning">4-1s (Тренд)</div>
                  <div class="text-caption text-grey-8">4 поспіль > 1SD</div>
                  <q-badge color="warning" text-color="dark" label="Увага (дрейф)" class="q-mt-xs"></q-badge>
                </q-card>
              </div>
              <div class="col-12 col-md-2">
                <q-card flat bordered class="q-pa-xs text-center bg-green-1">
                  <div class="text-caption text-weight-bold text-positive">10-x (Зсув)</div>
                  <div class="text-caption text-grey-8">10 по один бік</div>
                  <q-badge color="positive" label="В нормі" class="q-mt-xs"></q-badge>
                </q-card>
              </div>
              <div class="col-12 col-md-2">
                <q-card flat bordered class="q-pa-xs text-center bg-green-1">
                  <div class="text-caption text-weight-bold text-positive">1-2s (Попередження)</div>
                  <div class="text-caption text-grey-8">1 вимір > 2SD</div>
                  <q-badge color="teal" label="Під контролем" class="q-mt-xs"></q-badge>
                </q-card>
              </div>
            </div>

            <!-- DETAILED DATA TABLE -->
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
"""

# Replace old QC view block
qc_pattern = r'<!-- VIEW: QUALITY CONTROL \(QC\) -->[\s\S]*?<!-- VIEW: PHLEBOTOMY -->'
html = re.sub(qc_pattern, new_qc_template + "\n\n          <!-- VIEW: PHLEBOTOMY -->", html)

# 3. ENHANCE PATIENT PORTAL WITH TREND / CORRIDOR CHART
new_patient_section = """
            <!-- HISTORICAL TREND & REFERENCE CORRIDOR CHART -->
            <q-card flat bordered class="q-mb-md q-pa-md bg-white">
              <div class="row items-center justify-between q-mb-sm">
                <div>
                  <div class="text-subtitle1 text-weight-bold text-primary">
                    <q-icon name="show_chart" class="q-mr-xs"></q-icon> Динаміка показника у часі: Глюкоза сироватки (GLU)
                  </div>
                  <div class="text-caption text-grey-7">Референсний діапазон дорослої норми: 4.10 - 5.90 ммоль/л (зелений коридор безпеки)</div>
                </div>
                <div class="row items-center q-gutter-x-sm">
                  <q-badge color="positive" label="Коридор норми"></q-badge>
                  <q-badge color="negative" label="Критичний пік CITO (26.4 ммоль/л)"></q-badge>
                </div>
              </div>

              <!-- SVG TREND CHART -->
              <svg style="width: 100%; height: 210px; overflow: visible;" viewBox="0 0 800 200">
                <!-- Reference Norm Corridor (y=130 to y=155) -->
                <rect x="50" y="125" width="720" height="30" class="trend-corridor"></rect>
                <line x1="50" y1="125" x2="770" y2="125" stroke="#22c55e" stroke-width="1" stroke-dasharray="3, 3"></line>
                <text x="45" y="128" fill="#16a34a" font-size="10" text-anchor="end">Верхня норма (5.9)</text>
                <line x1="50" y1="155" x2="770" y2="155" stroke="#22c55e" stroke-width="1" stroke-dasharray="3, 3"></line>
                <text x="45" y="158" fill="#16a34a" font-size="10" text-anchor="end">Нижня норма (4.1)</text>

                <!-- Critical Threshold Line (y=30) -->
                <line x1="50" y1="35" x2="770" y2="35" stroke="#ef4444" stroke-width="1.2" stroke-dasharray="4, 4"></line>
                <text x="45" y="38" fill="#dc2626" font-size="10" font-weight="700" text-anchor="end">Паніка CITO (25.0)</text>

                <!-- Trend Line -->
                <polyline
                  points="110,145 250,140 390,118 530,95 670,28"
                  fill="none"
                  stroke="#ef4444"
                  stroke-width="3"
                  stroke-linejoin="round"
                ></polyline>

                <!-- Historical Points -->
                <!-- Point 1: 10.04.2025 (5.1) -->
                <circle cx="110" cy="145" r="5" fill="#22c55e" stroke="#fff" stroke-width="2"></circle>
                <text x="110" y="170" font-size="10" fill="#64748b" text-anchor="middle">10.04.2025</text>
                <text x="110" y="135" font-size="11" font-weight="700" fill="#16a34a" text-anchor="middle">5.1</text>

                <!-- Point 2: 15.07.2025 (5.6) -->
                <circle cx="250" cy="140" r="5" fill="#22c55e" stroke="#fff" stroke-width="2"></circle>
                <text x="250" y="170" font-size="10" fill="#64748b" text-anchor="middle">15.07.2025</text>
                <text x="250" y="130" font-size="11" font-weight="700" fill="#16a34a" text-anchor="middle">5.6</text>

                <!-- Point 3: 12.11.2025 (6.8) -->
                <circle cx="390" cy="118" r="5" fill="#f59e0b" stroke="#fff" stroke-width="2"></circle>
                <text x="390" y="170" font-size="10" fill="#64748b" text-anchor="middle">12.11.2025</text>
                <text x="390" y="108" font-size="11" font-weight="700" fill="#d97706" text-anchor="middle">6.8 &uarr;</text>

                <!-- Point 4: 14.02.2026 (8.9) -->
                <circle cx="530" cy="95" r="5.5" fill="#ea580c" stroke="#fff" stroke-width="2"></circle>
                <text x="530" y="170" font-size="10" fill="#64748b" text-anchor="middle">14.02.2026</text>
                <text x="530" y="85" font-size="11" font-weight="700" fill="#c2410c" text-anchor="middle">8.9 &uarr;&uarr;</text>

                <!-- Point 5: СЬОГОДНІ 06.10.2026 (26.4 - CITO) -->
                <circle cx="670" cy="28" r="8" fill="#dc2626" stroke="#fff" stroke-width="2.5" class="lj-point-violation"></circle>
                <text x="670" y="170" font-size="10" font-weight="700" fill="#dc2626" text-anchor="middle">Сьогодні (06.10)</text>
                <text x="670" y="20" font-size="12" font-weight="900" fill="#dc2626" text-anchor="middle">26.4 CITO!</text>
              </svg>

              <!-- Delta check comment -->
              <div class="row items-center justify-between q-mt-xs q-pa-sm bg-red-1 rounded-borders" style="border: 1px solid #fecaca; font-size: 12px;">
                <div class="text-negative">
                  <q-icon name="warning"></q-icon> <strong>Дельта-чек 72 год:</strong> Зростання показника на <strong>+185.0%</strong> порівняно з базовим рівнем. Виявлено декомпенсацію.
                </div>
                <div class="text-caption text-grey-8">
                  Лікарю надіслано push-сповіщення та зафіксовано в журналі CITO.
                </div>
              </div>
            </q-card>
"""

# Inject new_patient_section right before the closing tag of patient view
if "HISTORICAL TREND & REFERENCE CORRIDOR CHART" not in html:
    html = html.replace('</q-card>\n          </div>\n\n          <!-- VIEW: MICROBIOLOGY -->', '</q-card>\n' + new_patient_section + '\n          </div>\n\n          <!-- VIEW: MICROBIOLOGY -->')

# 4. ENHANCE BIOBANK WITH FULL 8x12 CRYO-RACK
new_biobank_template = """
          <!-- VIEW: BIOBANK -->
          <div v-show="currentView === 'biobank'">
            <div class="row items-center justify-between q-mb-md">
              <div>
                <h5 class="q-my-none text-weight-bold text-primary">
                  <q-icon name="fas fa-snowflake" class="q-mr-sm"></q-icon>
                  Біобанк та кріо-архів біоматеріалів (-80°C)
                </h5>
                <div class="text-caption text-grey-7">Адресне розміщення: Морозильник ULT-01 &rarr; Секція B &rarr; Штатив #4 &rarr; Кріо-бокс 8х12 (96 комірок).</div>
              </div>
              <div class="row items-center q-gutter-x-sm">
                <q-btn size="sm" color="teal" icon="qr_code_scanner" label="Сканувати штрихкод комірки" outline></q-btn>
                <q-btn size="sm" color="primary" icon="inventory" label="Акт утилізації зразків" unelevated></q-btn>
              </div>
            </div>

            <div class="row q-col-gutter-md">
              <!-- CRYO RACK 8x12 -->
              <div class="col-12 col-md-7">
                <q-card flat bordered class="q-pa-md bg-white">
                  <div class="row items-center justify-between q-mb-sm">
                    <div>
                      <div class="text-subtitle1 text-weight-bold text-primary">Кріо-бокс #BOX-04 (96 комірок)</div>
                      <div class="text-caption text-grey-7">Температура зберігання: <strong>-82.4°C</strong> | Заповнено: <strong>58/96 (60.4%)</strong></div>
                    </div>
                    <div class="row q-gutter-x-xs text-caption">
                      <span class="row items-center"><span style="width:9px; height:9px; background:#dc2626; border-radius:2px; display:inline-block; margin-right:3px;"></span> Сироватка</span>
                      <span class="row items-center"><span style="width:9px; height:9px; background:#2563eb; border-radius:2px; display:inline-block; margin-right:3px;"></span> ЕДТА</span>
                      <span class="row items-center"><span style="width:9px; height:9px; background:#f59e0b; border-radius:2px; display:inline-block; margin-right:3px;"></span> Термін до 30д</span>
                    </div>
                  </div>

                  <!-- 8x12 MATRIX -->
                  <div class="cryo-grid-8x12">
                    <div
                      v-for="cell in cryoCells"
                      :key="'cell-'+cell.coord"
                      class="cryo-cell"
                      :class="getCryoCellClass(cell)"
                      @click="selectCryoCell(cell)"
                    >
                      {{ cell.coord }}
                    </div>
                  </div>
                </q-card>
              </div>

              <!-- CELL DETAILS INSPECTOR -->
              <div class="col-12 col-md-5">
                <q-card flat bordered class="medlink-card bg-white">
                  <q-card-section class="bg-primary text-white q-py-sm">
                    <div class="row items-center justify-between">
                      <div class="text-subtitle1 text-weight-bold">
                        <q-icon name="view_in_ar"></q-icon> Паспорт комірки {{ selectedBiobankCell.coord }}
                      </div>
                      <q-badge color="white" text-color="primary" :label="selectedBiobankCell.status"></q-badge>
                    </div>
                  </q-card-section>
                  <q-card-section class="q-pa-md">
                    <div class="text-body2 q-mb-xs">Штрихкод зразка: <strong class="text-primary">{{ selectedBiobankCell.barcode || '—' }}</strong></div>
                    <div class="text-body2 q-mb-xs">Пацієнт: <strong>{{ selectedBiobankCell.patient || 'Вільна комірка' }}</strong></div>
                    <div class="text-body2 q-mb-xs">Біоматеріал: <strong>{{ selectedBiobankCell.biomaterial || '—' }}</strong></div>
                    <div class="text-body2 q-mb-xs">Об'єм аліквоти: <strong>{{ selectedBiobankCell.volume || '—' }}</strong></div>
                    <div class="text-body2 q-mb-xs">Дата заморозки: <strong>{{ selectedBiobankCell.frozenAt || '—' }}</strong></div>
                    <div class="text-body2 q-mb-xs">Придатний до: <strong>{{ selectedBiobankCell.expiresAt || '—' }}</strong></div>
                    <div class="text-body2 q-mb-md">Циклів дефростації: <strong>{{ selectedBiobankCell.defrostCount }}</strong></div>

                    <q-separator class="q-my-sm"></q-separator>

                    <div class="row q-gutter-sm">
                      <q-btn color="primary" dense unelevated label="Видати на повторний аналіз" :disabled="!selectedBiobankCell.barcode" @click="notify('Зразок ' + selectedBiobankCell.barcode + ' видано на дослідження')"></q-btn>
                      <q-btn color="negative" dense outline label="Списати в архів" :disabled="!selectedBiobankCell.barcode" @click="notify('Зразок списано за актом')"></q-btn>
                    </div>
                  </q-card-section>
                </q-card>
              </div>
            </div>
          </div>
"""

biobank_pattern = r'<!-- VIEW: BIOBANK -->[\s\S]*?<!-- VIEW: REAGENTS -->'
html = re.sub(biobank_pattern, new_biobank_template + "\n\n          <!-- VIEW: REAGENTS -->", html)

# 5. ENHANCE MICROBIOLOGY WITH EUCAST S/I/R SLIDER VISUALIZER
new_microbiology_section = """
            <!-- EUCAST ZONE DIAMETER VISUALIZER -->
            <q-card flat bordered class="q-mb-md q-pa-md bg-white">
              <div class="text-subtitle1 text-weight-bold text-primary q-mb-xs">
                Графічна оцінка зон затримки росту (EUCAST v14.0, мм)
              </div>
              <div class="text-caption text-grey-7 q-mb-md">
                Червоний: Резистентний (R) | Жовтий: Чутливий при підвищеній експозиції (I) | Зелений: Чутливий стандарт (S)
              </div>

              <div class="q-gutter-y-md">
                <div v-for="ab in eucastAntibiotics" :key="'eucast-'+ab.name">
                  <div class="row items-center justify-between text-body2 q-mb-xs">
                    <div>
                      <strong>{{ ab.name }}</strong> ({{ ab.dose }}) &mdash; Виміряно: <strong>{{ ab.zone }} мм</strong>
                    </div>
                    <q-badge :color="ab.category === 'S' ? 'positive' : (ab.category === 'I' ? 'warning' : 'negative')">
                      {{ ab.category }} ({{ ab.category === 'S' ? 'Чутливий' : (ab.category === 'I' ? 'Підвищена експозиція' : 'Резистентний') }})
                    </q-badge>
                  </div>
                  <div class="eucast-scale">
                    <div class="eucast-pointer" :style="{ left: ab.percent + '%' }"></div>
                  </div>
                  <div class="row justify-between text-caption text-grey-6 q-mt-xs" style="font-size:10px;">
                    <span>0 мм (R)</span>
                    <span>Межа R/I: {{ ab.rCutoff }} мм</span>
                    <span>Межа I/S: {{ ab.sCutoff }} мм</span>
                    <span>40 мм (S)</span>
                  </div>
                </div>
              </div>
            </q-card>
"""

if "Графічна оцінка зон затримки росту" not in html:
    html = html.replace('</q-card>\n          </div>\n\n          <!-- VIEW: BIOBANK -->', '</q-card>\n' + new_microbiology_section + '\n          </div>\n\n          <!-- VIEW: BIOBANK -->')

# 6. ENHANCE TAT ANALYTICS WITH WATERFALL / STAGE TIMELINE
new_tat_section = """
            <!-- TAT WATERFALL & SLA MONITOR -->
            <q-card flat bordered class="q-mb-md q-pa-md bg-white q-mt-md">
              <div class="text-subtitle1 text-weight-bold text-primary q-mb-xs">
                Поетапна розбивка часу виконання замовлення (Turnaround Time, TAT)
              </div>
              <div class="text-caption text-grey-7 q-mb-md">
                Середня тривалість етапів за сьогодні (Плановий норматив: &le; 180 хв, Фактичний середній TAT: 64 хв).
              </div>

              <div class="q-gutter-y-sm">
                <div>
                  <div class="row justify-between text-caption q-mb-xs">
                    <span>1. Забір біоматеріалу та маркування штрихкодом</span>
                    <strong>12 хв (норма: &le; 15 хв)</strong>
                  </div>
                  <div class="tat-waterfall-bar bg-primary" style="width: 20%;">12 хв</div>
                </div>
                <div>
                  <div class="row justify-between text-caption q-mb-xs">
                    <span>2. Транспортування кур'єром у термобоксі (+4°C)</span>
                    <strong>24 хв (норма: &le; 45 хв)</strong>
                  </div>
                  <div class="tat-waterfall-bar bg-indigo" style="width: 40%;">24 хв</div>
                </div>
                <div>
                  <div class="row justify-between text-caption q-mb-xs">
                    <span>3. Вхідний бракераж, центрифугування та сортування</span>
                    <strong>8 хв (норма: &le; 15 хв)</strong>
                  </div>
                  <div class="tat-waterfall-bar bg-teal" style="width: 14%;">8 хв</div>
                </div>
                <div>
                  <div class="row justify-between text-caption q-mb-xs">
                    <span>4. Аналітичний вимір на аналізаторі (Sysmex / Cobas)</span>
                    <strong>14 хв (норма: &le; 30 хв)</strong>
                  </div>
                  <div class="tat-waterfall-bar bg-cyan" style="width: 24%;">14 хв</div>
                </div>
                <div>
                  <div class="row justify-between text-caption q-mb-xs">
                    <span>5. Автовалідація / Лікарська валідація та підпис КЕП</span>
                    <strong>6 хв (норма: &le; 20 хв)</strong>
                  </div>
                  <div class="tat-waterfall-bar bg-positive" style="width: 10%;">6 хв</div>
                </div>
              </div>
            </q-card>
"""

if "Поетапна розбивка часу виконання замовлення" not in html:
    html = html.replace('</div>\n          </div>\n\n          <!-- VIEW: NORMS & METHODOLOGIES -->', '</div>\n' + new_tat_section + '\n          </div>\n\n          <!-- VIEW: NORMS & METHODOLOGIES -->')

# 7. ENHANCE LOGISTICS WITH COLD CHAIN TEMPERATURE CURVE
new_logistics_curve = """
            <!-- COLD CHAIN TEMPERATURE LOG GRAPH -->
            <q-card flat bordered class="q-mb-md q-pa-md bg-white">
              <div class="row items-center justify-between q-mb-sm">
                <div>
                  <div class="text-subtitle1 text-weight-bold text-primary">
                    <q-icon name="device_thermostat" class="q-mr-xs"></q-icon>
                    Холодовий ланцюг: Лог температур термодатчиків у рейсі
                  </div>
                  <div class="text-caption text-grey-7">Допустимий коридор: від +2.0°C до +8.0°C. Опитування щохвилини через Bluetooth/GSM.</div>
                </div>
                <div class="row items-center q-gutter-x-sm">
                  <q-badge color="teal" label="TC-201: +4.2°C (Норма)"></q-badge>
                  <q-badge color="negative" label="TC-309: +9.5°C (ПОРУШЕННЯ!)"></q-badge>
                </div>
              </div>

              <svg style="width: 100%; height: 160px; overflow: visible;" viewBox="0 0 750 150">
                <!-- Permissible Range Corridor (y=50 to y=110) -->
                <rect x="50" y="50" width="670" height="60" fill="rgba(34, 197, 94, 0.12)"></rect>
                <line x1="50" y1="50" x2="720" y2="50" stroke="#ef4444" stroke-width="1" stroke-dasharray="3, 3"></line>
                <text x="45" y="54" fill="#dc2626" font-size="10" text-anchor="end">Макс +8.0°C</text>

                <line x1="50" y1="80" x2="720" y2="80" stroke="#16a34a" stroke-width="1" stroke-dasharray="2, 2"></line>
                <text x="45" y="84" fill="#16a34a" font-size="10" text-anchor="end">Ціль +4.0°C</text>

                <line x1="50" y1="110" x2="720" y2="110" stroke="#0284c7" stroke-width="1" stroke-dasharray="3, 3"></line>
                <text x="45" y="114" fill="#0284c7" font-size="10" text-anchor="end">Мін +2.0°C</text>

                <!-- TC-201 Curve (Normal, blue/teal) -->
                <polyline
                  points="60,82 120,80 180,78 240,79 300,77 360,78 420,77 480,78 540,79 600,77 660,78 710,78"
                  fill="none"
                  stroke="#0284c7"
                  stroke-width="2.5"
                ></polyline>

                <!-- TC-309 Curve (Violation, red spike) -->
                <polyline
                  points="60,75 120,74 180,72 240,68 300,58 360,45 420,32 480,26 540,24 600,28 660,35 710,40"
                  fill="none"
                  stroke="#ef4444"
                  stroke-width="2.5"
                ></polyline>
                <circle cx="540" cy="24" r="6" fill="#dc2626" stroke="#fff" stroke-width="2" class="lj-point-violation"></circle>
                <text x="540" y="16" fill="#dc2626" font-size="10" font-weight="700" text-anchor="middle">+9.5°C ПІК!</text>
              </svg>
            </q-card>
"""

if "Холодовий ланцюг: Лог температур" not in html:
    html = html.replace('</div>\n          </div>\n\n          <!-- VIEW: PATIENT PORTAL -->', '</div>\n' + new_logistics_curve + '\n          </div>\n\n          <!-- VIEW: PATIENT PORTAL -->')

# 8. VUE DATA PROPERTIES FOR CHARTS
vue_charts_data = """
        selectedQcParam: 'WBC',
        selectedLjPointIndex: 17, // default to violation point (Day 18)
        activeLjPoint: {
          day: 18,
          date: '2026-10-06',
          time: '07:45',
          val: 8.28,
          zScore: 3.60,
          status: 'VIOLATION_1_3S',
          ruleLabel: 'ПОРУШЕНО 1-3s (LOCK)',
          operator: 'Коваленко О.В.'
        },
        selectedBiobankCell: {
          coord: 'C-05',
          barcode: '1026004812',
          patient: 'Мельник Юрій Володимирович',
          biomaterial: 'Сироватка венозної крові (Аліквота №1)',
          volume: '1.5 мл',
          frozenAt: '2026-10-06 09:15',
          expiresAt: '2027-04-06',
          defrostCount: 0,
          status: 'АКТИВНИЙ ЗРАЗОК'
        },
        eucastAntibiotics: [
          { name: 'Амоксицилін / Клавуланат', dose: '20/10 мкг', zone: 14, percent: 35, category: 'R', rCutoff: 16, sCutoff: 19 },
          { name: 'Ципрофлоксацин', dose: '5 мкг', zone: 26, percent: 65, category: 'S', rCutoff: 22, sCutoff: 25 },
          { name: 'Меропенем', dose: '10 мкг', zone: 31, percent: 78, category: 'S', rCutoff: 22, sCutoff: 28 },
          { name: 'Цефтріаксон', dose: '30 мкг', zone: 19, percent: 48, category: 'I', rCutoff: 17, sCutoff: 20 },
          { name: 'Гентаміцин', dose: '10 мкг', zone: 21, percent: 53, category: 'S', rCutoff: 15, sCutoff: 18 }
        ],
"""

if "selectedQcParam:" not in html:
    html = html.replace("data: {", "data: {\n" + vue_charts_data)

# 9. VUE COMPUTED & METHODS FOR CHARTS
vue_charts_computed = """
    ljPoints() {
      // 20 data points mapped to SVG coordinates (width 60 to 870, y 20 to 260)
      // Mean = 7.20 (y=140), SD = 0.30 (40px per SD)
      const values = [
        7.15, 7.22, 7.18, 7.25, 7.12, 7.30, 7.19, 7.21, 7.28, 7.14,
        7.26, 7.20, 7.35, 7.84, 7.40, 7.32, 7.45, 8.28, 7.25, 7.22
      ];
      const startX = 80;
      const stepX = (850 - startX) / 19;
      return values.map((val, i) => {
        const z = (val - 7.20) / 0.30;
        const y = 140 - (z * 40);
        let status = 'OK';
        let ruleLabel = 'В межах норми (OK)';
        if (z > 3.0 || z < -3.0) {
          status = 'VIOLATION_1_3S';
          ruleLabel = 'ПОРУШЕНО 1-3s (LOCK)';
        } else if (z > 2.0 || z < -2.0) {
          status = 'WARN_1_2S';
          ruleLabel = 'ПОПЕРЕДЖЕННЯ 1-2s';
        }
        return {
          day: i + 1,
          date: '2026-09-' + String(17 + i > 30 ? i - 13 : 17 + i).padStart(2, '0'),
          time: '08:15',
          val: val.toFixed(2),
          zScore: z.toFixed(2),
          status: status,
          ruleLabel: ruleLabel,
          operator: i % 2 === 0 ? 'Мельник В.С.' : 'Коваленко О.В.',
          x: Math.round(startX + (i * stepX)),
          y: Math.round(y)
        };
      });
    },
    ljPolylinePoints() {
      return this.ljPoints.map(p => `${p.x},${p.y}`).join(' ');
    },
    cryoCells() {
      const rows = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
      const cells = [];
      for (let r = 0; r < 8; r++) {
        for (let c = 1; c <= 12; c++) {
          const coord = `${rows[r]}-${String(c).padStart(2, '0')}`;
          let type = 'empty';
          let barcode = '';
          let patient = '';
          let biomaterial = '';
          if (r === 2 && c === 5) {
            type = 'selected';
            barcode = '1026004812';
            patient = 'Мельник Юрій Володимирович';
            biomaterial = 'Сироватка венозної крові (Аліквота №1)';
          } else if ((r * 12 + c) % 5 === 0) {
            type = 'warning';
            barcode = '102600' + (4800 + r * 12 + c);
            patient = 'Пацієнт ' + (r * 12 + c);
            biomaterial = 'Сироватка крові (Термін спливає)';
          } else if ((r + c) % 3 === 0) {
            type = 'serum';
            barcode = '102600' + (4800 + r * 12 + c);
            patient = 'Пацієнт ' + (r * 12 + c);
            biomaterial = 'Сироватка венозної крові';
          } else if ((r * c) % 4 === 0) {
            type = 'plasma';
            barcode = '102600' + (4800 + r * 12 + c);
            patient = 'Пацієнт ' + (r * 12 + c);
            biomaterial = 'Плазма (Li-гепарин)';
          } else if ((r + c) % 2 === 0) {
            type = 'edta';
            barcode = '102600' + (4800 + r * 12 + c);
            patient = 'Пацієнт ' + (r * 12 + c);
            biomaterial = 'Цільна кров (K2 EDTA)';
          }
          cells.push({ coord, type, barcode, patient, biomaterial });
        }
      }
      return cells;
    },
"""

if "ljPoints()" not in html:
    html = html.replace("computed: {", "computed: {\n" + vue_charts_computed)

vue_charts_methods = """
      selectLjPoint(idx) {
        this.selectedLjPointIndex = idx;
        this.activeLjPoint = this.ljPoints[idx];
      },
      onQcParamChange(val) {
        this.qcData.parameter = val;
        this.$q.notify({ type: 'info', message: 'Завантажено контрольні дані для ' + val, position: 'top', timeout: 1500 });
      },
      getCryoCellClass(cell) {
        if (this.selectedBiobankCell && this.selectedBiobankCell.coord === cell.coord) {
          return 'cryo-cell-selected cryo-cell-' + cell.type;
        }
        return 'cryo-cell-' + cell.type;
      },
      selectCryoCell(cell) {
        this.selectedBiobankCell = {
          coord: cell.coord,
          barcode: cell.barcode,
          patient: cell.patient || (cell.type === 'empty' ? 'Вільна комірка' : 'Анонімізований пацієнт'),
          biomaterial: cell.biomaterial || (cell.type === 'empty' ? '—' : 'Сироватка венозної крові'),
          volume: cell.type === 'empty' ? '—' : '1.5 мл',
          frozenAt: cell.type === 'empty' ? '—' : '2026-10-06 09:15',
          expiresAt: cell.type === 'empty' ? '—' : '2027-04-06',
          defrostCount: 0,
          status: cell.type === 'empty' ? 'ВІЛЬНА КОМІРКА' : (cell.type === 'warning' ? 'ТЕРМІН СПЛИВАЄ' : 'АКТИВНИЙ ЗРАЗОК')
        };
      },
"""

if "selectLjPoint(idx)" not in html:
    html = html.replace("methods: {", "methods: {\n" + vue_charts_methods)

# Write to run_prototype.html
with open(PROTOTYPE_PATH, "w", encoding="utf-8") as f:
    f.write(html)

# Also write to index.html
with open(INDEX_PATH, "w", encoding="utf-8") as f:
    f.write(html)

print("Rich interactive charts successfully injected into run_prototype.html and index.html!")
