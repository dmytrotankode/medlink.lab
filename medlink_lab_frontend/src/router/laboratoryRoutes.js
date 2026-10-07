/**
 * MedLink LIS - Vue Router Configuration
 * Modular route configuration matching evomis/src/App.View routing pattern.
 * Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC). All rights reserved.
 */

export default [
  {
    path: '/laboratory',
    component: () => import('@/layouts/baseLayout/BaseLayout.vue'),
    meta: {
      title: 'Лабораторія (ЛІС 3.0)'
    },
    children: [
      {
        path: '',
        redirect: 'workstation'
      },
      // 1. Phlebotomy & Pre-analytics
      {
        path: 'phlebotomy',
        name: 'lab-phlebotomy',
        meta: {
          title: 'Пункт забору біоматеріалу',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Пункт забору' }]
        },
        component: () => import('@/pages/laboratory/PhlebotomyStation.vue')
      },
      // 2. Logistics & Thermocontainers
      {
        path: 'logistics',
        name: 'lab-logistics',
        meta: {
          title: 'Логістика зразків та термоконтроль',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Логістика' }]
        },
        component: () => import('@/pages/laboratory/SpecimenLogistics.vue')
      },
      // 3. Lab Workstation & Results
      {
        path: 'workstation',
        name: 'lab-workstation',
        meta: {
          title: 'Робочий стіл лаборанта (Журнал досліджень)',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Робочий стіл' }]
        },
        component: () => import('@/pages/laboratory/LabWorkstation.vue')
      },
      // 4. Validation & Panic Alerts
      {
        path: 'validation',
        name: 'lab-validation',
        meta: {
          title: 'Валідація результатів & Панічні алерти',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Валідація & Паніка' }]
        },
        component: () => import('@/pages/laboratory/ValidationPanic.vue')
      },
      // 5. Quality Control (Westgard / Levey-Jennings)
      {
        path: 'quality-control',
        name: 'lab-qc',
        meta: {
          title: 'Внутрішній контроль якості (ВКЯ)',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Контроль якості' }]
        },
        component: () => import('@/pages/laboratory/QualityControl.vue')
      },
      // 6. Patient Portal
      {
        path: 'patient-portal',
        name: 'lab-patient-portal',
        meta: {
          title: 'Кабінет пацієнта (Результати досліджень)',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Кабінет пацієнта' }]
        },
        component: () => import('@/pages/laboratory/PatientPortal.vue')
      },
      // 7. Biobank Archive
      {
        path: 'biobank',
        name: 'lab-biobank',
        meta: {
          title: 'Біобанк та архів зразків',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Біобанк' }]
        },
        component: () => import('@/pages/laboratory/BiobankArchive.vue')
      },
      // 8. Reagents & Inventory
      {
        path: 'reagents',
        name: 'lab-reagents',
        meta: {
          title: 'Склад реактивів та калібраторів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Реактиви' }]
        },
        component: () => import('@/pages/laboratory/ReagentInventory.vue')
      },
      // 9. Microbiology & Culture
      {
        path: 'microbiology',
        name: 'lab-microbiology',
        meta: {
          title: 'Бактеріологія та антибіотикограма',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Мікробіологія' }]
        },
        component: () => import('@/pages/laboratory/MicrobiologyCulture.vue')
      },
      // 10. Analytics & TAT
      {
        path: 'analytics-tat',
        name: 'lab-analytics-tat',
        meta: {
          title: 'Операційна аналітика та TAT',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Аналітика & TAT' }]
        },
        component: () => import('@/pages/laboratory/LabAnalyticsTat.vue')
      },
      // 11. Analyzer Monitor Gateway
      {
        path: 'analyzer-monitor',
        name: 'lab-analyzer-monitor',
        meta: {
          title: 'Монітор шлюзу аналізаторів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Шлюз аналізаторів' }]
        },
        component: () => import('@/pages/laboratory/AnalyzerMonitor.vue')
      },
      // 12. Norms & Methodologies
      {
        path: 'norms-methodologies',
        name: 'lab-norms',
        meta: {
          title: 'Норми, референси та методики',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Норми і методики' }]
        },
        component: () => import('@/pages/laboratory/NormsMethodologies.vue')
      },
      // Dictionaries Subroutes
      {
        path: 'dictionaries/biomaterials',
        name: 'dict-biomaterials',
        meta: {
          title: 'Довідник біоматеріалів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Довідники', path: '/laboratory/dictionaries' }, { name: 'Біоматеріали' }]
        },
        component: () => import('@/pages/dictionaries/BiomaterialsDict.vue')
      },
      {
        path: 'dictionaries/tube-types',
        name: 'dict-tube-types',
        meta: {
          title: 'Довідник пробірок та контейнерів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Довідники', path: '/laboratory/dictionaries' }, { name: 'Типи пробірок' }]
        },
        component: () => import('@/pages/dictionaries/TubeTypesDict.vue')
      },
      {
        path: 'dictionaries/analyzer-types',
        name: 'dict-analyzer-types',
        meta: {
          title: 'Довідник типів аналізаторів',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Довідники', path: '/laboratory/dictionaries' }, { name: 'Моделі аналізаторів' }]
        },
        component: () => import('@/pages/dictionaries/AnalyzerTypesDict.vue')
      },
      {
        path: 'dictionaries/parameters',
        name: 'dict-lab-parameters',
        meta: {
          title: 'Довідник лабораторних показників',
          breadcrumb: [{ name: 'Лабораторія', path: '/laboratory' }, { name: 'Довідники', path: '/laboratory/dictionaries' }, { name: 'Показники і профілі' }]
        },
        component: () => import('@/pages/dictionaries/LabParametersDict.vue')
      }
    ]
  }
];
