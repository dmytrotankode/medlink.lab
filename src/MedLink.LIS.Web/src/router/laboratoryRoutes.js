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
      lab('referrals', 'lab-referrals', 'Журнал направлень', () => import('pages/laboratory/orders/ReferralJournal.vue')),
      lab('refs', 'lab-incoming-referrals', 'Вхідні направлення', () => import('pages/laboratory/mis/IncomingReferralsPage.vue')),
      lab('work', 'lab-work', 'Робота за направленнями', () => import('pages/laboratory/mis/WorkByReferralsPage.vue')),
      lab('reports', 'lab-reports', 'Звіти лабораторії', () => import('pages/laboratory/mis/LabReportsPage.vue')),
      lab('ehealth', 'lab-ehealth', 'Відправка в ЕСОЗ', () => import('pages/laboratory/mis/EhealthSubmissionsPage.vue')),
      lab('orders/:id', 'lab-order-card', 'Направлення', () => import('pages/laboratory/orders/OrderCard.vue'), { props: true }),
      lab('order-matrix', 'lab-order-matrix', 'Матриця призначень', () => import('pages/laboratory/orders/OrderMatrixPage.vue')),
      lab('phlebotomy', 'lab-phlebotomy', 'Пункт забору біоматеріалу', () => import('pages/laboratory/PhlebotomyStation.vue')),
      lab('samples/:barcode', 'lab-sample-processing', 'Обробка зразка', () => import('pages/laboratory/samples/SampleProcessing.vue'), { props: true }),
      lab('sections', 'lab-sections', 'Підрозділи лабораторії', () => import('pages/laboratory/sections/LabSectionsPage.vue')),
      lab('sections/journal', 'lab-section-journal', 'Журнал відділення', () => import('pages/laboratory/sections/DepartmentJournalPage.vue')),
      lab('logistics', 'lab-logistics', 'Логістика зразків', () => import('pages/laboratory/SpecimenLogistics.vue')),
      lab('clients', 'lab-clients', 'Клієнти (клініки)', () => import('pages/laboratory/ClientsPage.vue')),
      lab('send-out', 'lab-send-out', 'Зовнішні лабораторії', () => import('pages/laboratory/SendOutPage.vue')),
      lab('workstation', 'lab-workstation', 'Робочий стіл лаборанта', () => import('pages/laboratory/workstation/LabWorkstation.vue')),
      lab('validation', 'lab-validation', 'Валідація та критичні значення', () => import('pages/laboratory/validation/ValidationPanic.vue')),
      lab('quality-control', 'lab-qc', 'Контроль якості (ВКЯ)', () => import('pages/laboratory/qc/QualityControl.vue')),
      lab('biobank', 'lab-biobank', 'Біобанк та кріо-архів', () => import('pages/laboratory/BiobankArchive.vue')),
      lab('reagents', 'lab-reagents', 'Склад реагентів', () => import('pages/laboratory/ReagentInventory.vue')),
      lab('microbiology', 'lab-microbiology', 'Мікробіологія', () => import('pages/laboratory/microbiology/MicrobiologyCultures.vue')),
      lab('microbiology/:id', 'lab-culture-card', 'Картка посіву', () => import('pages/laboratory/microbiology/CultureCard.vue'), { props: true }),
      // ----- Патоморфологія (ТЗ розд. 22) -----
      lab('pathology', 'lab-path-cases', 'Журнал справ ПГД', () => import('pages/laboratory/pathology/PathologyCasesPage.vue'), { meta: { group: 'hist' } }),
      lab('cytology', 'lab-cyto-cases', 'Журнал цитології', () => import('pages/laboratory/pathology/PathologyCasesPage.vue'), { meta: { group: 'cyto' } }),
      lab('pathology/intake', 'lab-path-intake', 'Прийом матеріалу', () => import('pages/laboratory/pathology/PathologyIntakePage.vue')),
      lab('pathology/cito', 'lab-path-cito', 'Термінові інтраопераційні (CITO)', () => import('pages/laboratory/pathology/PathologyCasesPage.vue'), { meta: { cito: true } }),
      lab('pathology/archive', 'lab-path-archive', 'Архів блоків і скелець', () => import('pages/laboratory/pathology/PathologyArchivePage.vue')),
      lab('pathology/dictionaries', 'lab-path-dictionaries', 'Довідники патоморфології', () => import('pages/laboratory/pathology/PathologyDictionariesPage.vue')),
      lab('pathology/cases/:id', 'lab-path-case', 'Справа ПГД', () => import('pages/laboratory/pathology/PathologyCasePage.vue'), { props: true }),
      lab('analytics-tat', 'lab-analytics-tat', 'Аналітика термінів виконання', () => import('pages/laboratory/LabAnalyticsTat.vue')),

      // ----- Довідники -----
      lab('report-templates', 'lab-report-templates', 'Шаблони бланків результатів', () => import('pages/laboratory/ReportTemplatesPage.vue')),
      lab('pricing', 'lab-pricing', 'Прайси та платники', () => import('pages/laboratory/PricingPage.vue')),
      lab('norms', 'lab-norms', 'Послуги та норми', () => import('pages/laboratory/norms/NormsCatalog.vue')),
      lab('dictionaries', 'lab-dictionaries', 'Довідники', () => import('pages/dictionaries/DictionariesHome.vue')),
      lab('dictionaries/:name', 'lab-dictionary', 'Довідник', () => import('pages/dictionaries/DictionaryPage.vue'), { props: true }),

      // ----- Інтеграції -----
      lab('analyzers', 'lab-analyzers', 'Аналізатори та коннектори', () => import('pages/laboratory/analyzers/AnalyzersPage.vue')),
      lab('analyzer-services', 'lab-analyzer-services', 'Послуги ЕСОЗ та аналізатори', () => import('pages/laboratory/mis/AnalyzerServicesPage.vue')),
      lab('analyzers/messages', 'lab-analyzer-messages', 'Журнал обміну з аналізаторами', () => import('pages/laboratory/analyzers/AnalyzerMessages.vue')),
      lab('import', 'lab-import', 'Імпорт результатів', () => import('pages/laboratory/ImportResults.vue')),

      // ----- Адміністрування -----
      lab('admin/settings', 'lab-admin-settings', 'Налаштування лабораторії', () => import('pages/admin/LabSettings.vue')),
      lab('admin/users', 'lab-admin-users', 'Співробітники', () => import('pages/admin/EmployeesAdmin.vue')),
      lab('audit', 'lab-audit', 'Аудит', () => import('pages/admin/AuditLog.vue'))
    ]
  }
];
