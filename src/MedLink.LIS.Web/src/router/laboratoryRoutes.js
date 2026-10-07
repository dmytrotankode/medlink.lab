/**
 * Маршрути лабораторного модуля (docs/API_CONTRACT.md розділ 5).
 * Для перенесення в evomis: імпортувати цей масив у src/router/routes.js МІС.
 */
const lab = (path, name, title, component, extra = {}) => ({
  path,
  name,
  meta: { title, ...(extra.meta || {}) },
  component,
  ...(extra.props ? { props: true } : {})
});

export default [
  {
    path: '/laboratory',
    component: () => import('layouts/baseLayout/BaseLayout.vue'),
    meta: { title: 'Лабораторія (ЛІС 4.0)' },
    children: [
      { path: '', redirect: 'dashboard' },

      // ----- Процеси -----
      lab('dashboard', 'lab-dashboard', 'Дашборд лабораторії', () => import('pages/laboratory/LabDashboard.vue')),
      lab('orders', 'lab-orders', 'Реєстрація направлень', () => import('pages/laboratory/orders/OrdersQueue.vue')),
      lab('orders/:id', 'lab-order-card', 'Картка замовлення', () => import('pages/laboratory/orders/OrderCard.vue'), { props: true }),
      lab('order-matrix', 'lab-order-matrix', 'Матриця призначень', () => import('pages/laboratory/orders/OrderMatrixPage.vue')),
      lab('phlebotomy', 'lab-phlebotomy', 'Пункт забору біоматеріалу', () => import('pages/laboratory/PhlebotomyStation.vue')),
      lab('samples/:barcode', 'lab-sample-processing', 'Обробка зразка', () => import('pages/laboratory/samples/SampleProcessing.vue'), { props: true }),
      lab('sections', 'lab-sections', 'Підрозділи лабораторії', () => import('pages/laboratory/sections/LabSectionsPage.vue')),
      lab('sections/journal', 'lab-section-journal', 'Журнал відділення', () => import('pages/laboratory/sections/DepartmentJournalPage.vue')),
      lab('logistics', 'lab-logistics', 'Логістика зразків', () => import('pages/laboratory/SpecimenLogistics.vue')),
      lab('workstation', 'lab-workstation', 'Робочий стіл лаборанта', () => import('pages/laboratory/workstation/LabWorkstation.vue')),
      lab('validation', 'lab-validation', 'Валідація та паніка', () => import('pages/laboratory/validation/ValidationPanic.vue')),
      lab('quality-control', 'lab-qc', 'Контроль якості (ВКЯ)', () => import('pages/laboratory/qc/QualityControl.vue')),
      lab('biobank', 'lab-biobank', 'Біобанк та кріо-архів', () => import('pages/laboratory/BiobankArchive.vue')),
      lab('reagents', 'lab-reagents', 'Склад реагентів', () => import('pages/laboratory/ReagentInventory.vue')),
      lab('microbiology', 'lab-microbiology', 'Мікробіологія', () => import('pages/laboratory/microbiology/MicrobiologyCultures.vue')),
      lab('microbiology/:id', 'lab-culture-card', 'Картка посіву', () => import('pages/laboratory/microbiology/CultureCard.vue'), { props: true }),
      lab('analytics-tat', 'lab-analytics-tat', 'Аналітика TAT', () => import('pages/laboratory/LabAnalyticsTat.vue')),

      // ----- Довідники -----
      lab('norms', 'lab-norms', 'Довідник послуг та норми', () => import('pages/laboratory/norms/NormsCatalog.vue')),
      lab('dictionaries', 'lab-dictionaries', 'Довідники', () => import('pages/dictionaries/DictionariesHome.vue')),
      lab('dictionaries/:name', 'lab-dictionary', 'Довідник', () => import('pages/dictionaries/DictionaryPage.vue'), { props: true }),

      // ----- Інтеграції -----
      lab('analyzers', 'lab-analyzers', 'Аналізатори та коннектори', () => import('pages/laboratory/analyzers/AnalyzersPage.vue')),
      lab('analyzers/messages', 'lab-analyzer-messages', 'Журнал обміну з аналізаторами', () => import('pages/laboratory/analyzers/AnalyzerMessages.vue')),
      lab('import', 'lab-import', 'Імпорт результатів', () => import('pages/laboratory/ImportResults.vue')),

      // ----- Адміністрування -----
      lab('admin/settings', 'lab-admin-settings', 'Налаштування лабораторії', () => import('pages/admin/LabSettings.vue')),
      lab('admin/users', 'lab-admin-users', 'Співробітники', () => import('pages/admin/EmployeesAdmin.vue')),
      lab('audit', 'lab-audit', 'Аудит', () => import('pages/admin/AuditLog.vue'))
    ]
  }
];
