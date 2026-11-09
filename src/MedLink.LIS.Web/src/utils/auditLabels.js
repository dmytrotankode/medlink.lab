/**
 * Українські назви для журналу аудиту: коди дій (CREATE, RECEIVE…) і сутностей (lab_order, LabOrder…).
 * Невідоме значення показується як є — аудит нічого не ховає.
 */
export const ACTION_LABELS = {
  CREATE: 'Створення', UPDATE: 'Зміна', DELETE: 'Видалення', SOFT_DELETE: 'Деактивація', UPSERT: 'Створення або зміна',
  ACCEPT: 'Прийом', ADD_ISOLATE: 'Додано ізолят', ADD_SAMPLE: 'Додано пробірку', ADD_TESTS: 'Додано показники', ANALYZER_SERVICES: 'Послуги аналізатора',
  ASSIGN_ANALYZER: 'Призначено аналізатор', ATTACH: 'Додано вкладення', AUTOVERIFY_BATCH: 'Автоверифікація пачки', CANCEL: 'Скасування',
  CANCEL_INVOICE: 'Анулювання рахунку', CLIENT_REGISTRY_IMPORT: 'Імпорт реєстру клініки', COLLECT: 'Забір', COMMAND: 'Команда коннектору',
  CONSUME: 'Списання', CYTO_PREPARE: 'Приготування препаратів', CYTO_SCREEN: 'Скринінг препаратів', DISPATCH: 'Відправлення',
  DISTRIBUTION_CONFIRM: 'Підтверджено розподіл', EHEALTH_ANNUL_QUEUED: 'Анулювання звіту в ЕСОЗ (у черзі)', EHEALTH_CONFIRM: 'Підтвердження особи в ЕСОЗ',
  EHEALTH_LOGIN: 'Вхід в ЕСОЗ', EHEALTH_REFERRAL_RELEASE: 'Погашення е-направлення', EHEALTH_REFERRAL_USE: 'Використання е-направлення',
  EHEALTH_SIGN: 'Підпис звіту для ЕСОЗ', EHEALTH_SUBMIT_QUEUED: 'Відправка звіту в ЕСОЗ (у черзі)', IMPORT: 'Імпорт', INTERVENTIONS_SYNC: 'Оновлення довідника інтервенцій',
  INVOICE: 'Виставлення рахунку', INVOICE_PAID: 'Оплата рахунку', ITEM_INDICATORS: 'Вибір показників', JOURNAL_REGISTER: 'Реєстрація в журналі',
  LINK: 'Зв’язування', LOCKOUT: 'Блокування аналізатора', LOCKOUT_MANUAL: 'Ручне блокування аналізатора', LOCKOUT_RESOLVE: 'Розблокування аналізатора',
  LOST: 'Позначено втраченим', MAPPING_TASK: 'Задача співставлення', MIS_REFERRAL: 'Направлення з МІС', PACK: 'Пакування', PATH_ACCEPT: 'Прийом матеріалу ПГД',
  PATH_ADDENDUM: 'Доповнення до висновку', PATH_ADDITIONAL: 'Дозамовлення забарвлень', PATH_BLOCK_ISSUE: 'Видача блоків і скелець', PATH_BLOCK_RETURN: 'Повернення блоків і скелець',
  PATH_BLOCK_STORAGE: 'Зберігання блоків', PATH_CITO_NOTICE: 'Повідомлення CITO', PATH_DISPATCH: 'Відправка висновку', PATH_GROSS: 'Вирізка', PATH_PRELIMINARY: 'Попередній висновок',
  PATH_REFERRAL: 'Направлення на ПГД', PATH_REJECT: 'Відмова в прийомі ПГД', PATH_SIGN: 'Підпис висновку', PATH_SLIDE_DISPOSE: 'Утилізація скелець',
  PATIENT_CHOICE: 'Пацієнт сплачує сам', PAYMENT: 'Оплата', PERIOD_INVOICE: 'Рахунок за період', PLACE: 'Розміщення', RECEIVE: 'Приймання', REFLEX_CREATED: 'Додано повторний тест',
  REJECT: 'Відхилення', RELEASE: 'Видача результату', REMOVE: 'Вилучення', REMOVE_ITEM: 'Видалено позицію', REMOVE_TEST: 'Видалено показник', REOPEN: 'Повторне відкриття',
  REPLACE: 'Заміна', REPORT_ACCESS: 'Перегляд бланка', REROUTE: 'Зміна маршруту', RERUN: 'Повторне виконання', RESULT_DELETED: 'Видалено результат',
  RETURN: 'Повернення', ROTATE_KEY: 'Перевипуск ключа', ROUTE_TEST: 'Маршрут показника', SAVE_FAVORITES: 'Збереження обраного', SEAL: 'Опломбування', SPLIT: 'Розділення проби',
  STAGE: 'Етап обробки', STAIN_PRICE: 'Зміна ціни фарбування', START_ANALYSIS: 'Початок аналізу', SUSCEPTIBILITY: 'Антибіотикограма', TEST_RELEASED: 'Показник видано',
  UNCOLLECT: 'Скасовано забір', UNPACK: 'Розпакування', VERIFY: 'Верифікація'
};

export const ENTITY_LABELS = {
  lab_order: 'Замовлення', LabOrder: 'Замовлення', lab_order_sample: 'Пробірка', LabOrderSample: 'Пробірка', lab_order_test: 'Показник замовлення', LabOrderTest: 'Показник замовлення',
  lab_order_item: 'Позиція замовлення', lab_order_charge: 'Нарахування', lab_order_favorite: 'Обране', lab_order_attachment: 'Вкладення замовлення',
  lab_test_result: 'Результат', LabTestResult: 'Результат', lab_panic_call: 'Дзвінок про критичне значення', LabPanicCall: 'Дзвінок про критичне значення',
  lab_qc_result: 'Результат ВКЯ', LabQcResult: 'Результат ВКЯ', lab_qc_material: 'Контрольний матеріал', LabQcMaterial: 'Контрольний матеріал', lab_qc_target: 'Цільове значення ВКЯ',
  lab_analyzer_lockout: 'Блокування аналізатора', LabAnalyzerLockout: 'Блокування аналізатора', lab_analyzer: 'Аналізатор', LabAnalyzer: 'Аналізатор',
  lab_analyzer_service: 'Послуга аналізатора', lab_connector_installation: 'Коннектор', LabConnectorInstallation: 'Коннектор',
  lab_reference_layer: 'Шар норм', LabReferenceLayer: 'Шар норм', lab_test_profile_items: 'Склад послуги', LabTestProfile: 'Послуга', LabTestDefinition: 'Показник',
  lab_payer: 'Платник', lab_payment: 'Оплата', lab_invoice: 'Рахунок', lab_price_list: 'Прайс-лист', lab_performer: 'Лабораторія-виконавець',
  lab_send_out: 'Реєстр відправки', lab_send_out_item: 'Позиція реєстру відправки', lab_sample_logistics: 'Логістика проби', lab_transport_container: 'Транспортний контейнер',
  lab_transport_container_type: 'Тип транспортної тари', lab_sample_stage_event: 'Етап обробки проби', lab_workflow_template: 'Шаблон робочого процесу',
  lab_archive_cell: 'Комірка біобанку', lab_archive_rack: 'Штатив біобанку', lab_reagent_lot: 'Лот реагенту', lab_culture_order: 'Посів', lab_isolate: 'Ізолят',
  lab_susceptibility_result: 'Антибіотикограма', lab_path_block: 'Блок ПГД', lab_path_slide: 'Скельце ПГД', lab_stain_method: 'Метод фарбування',
  lab_section: 'Підрозділ лабораторії', lab_section_journal_entry: 'Запис журналу відділення', lab_settings: 'Налаштування', lab_numerator: 'Нумератор',
  lab_unmatched_result: 'Незв’язаний результат', lab_worklist_batch: 'Робочий лист', lab_ehealth_session: 'Сесія ЕСОЗ', lab_ehealth_submission: 'Відправка в ЕСОЗ',
  lab_ehealth_intervention: 'Інтервенція ЕСОЗ', lab_intervention_map: 'Співставлення інтервенції', ehe_incoming_medical_referral: 'Е-направлення',
  mis_patient_card: 'Картка пацієнта', mis_patient_insurance: 'Поліс пацієнта', org_department: 'Відділення', org_employee: 'Співробітник'
};

export function actionLabel (a) { return ACTION_LABELS[String(a || '').toUpperCase()] || a || '—'; }
export function entityLabel (e) { return ENTITY_LABELS[e] || e || '—'; }
