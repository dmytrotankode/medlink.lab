-- MedLink LIS: Dictionaries - Standard Clinical Laboratory Profiles and Parameters
-- Generated for PostgreSQL (MedLink evomis)

INSERT INTO lab_test_profiles (code, name, category, turnaround_hours, fasting_required, is_active)
VALUES ('PROF_CBC', 'Загальний аналіз крові розгорнутий (ЗАК + формула + ШОЕ)', 'Гематологія', 3, true, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, category = EXCLUDED.category;

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('WBC', 'Лейкоцити (WBC)', '6690-2', '10^9/л', 'Кондуктометричний / Проточна цитометрія', 30.0, 72, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_CBC' AND t.code = 'WBC'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 0, 1, 6.0, 17.5, 3.0, 30.0 FROM lab_test_definitions WHERE code = 'WBC';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 1, 15, 4.5, 12.0, 2.5, 25.0 FROM lab_test_definitions WHERE code = 'WBC';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 16, 120, 4.0, 9.0, 2.0, 30.0 FROM lab_test_definitions WHERE code = 'WBC';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('RBC', 'Еритроцити (RBC)', '789-8', '10^12/л', 'Кондуктометричний аналіз', 15.0, 72, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_CBC' AND t.code = 'RBC'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'M', 16, 120, 4.0, 5.0, 2.0, 6.5 FROM lab_test_definitions WHERE code = 'RBC';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'F', 16, 120, 3.7, 4.7, 1.8, 6.0 FROM lab_test_definitions WHERE code = 'RBC';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 0, 15, 3.8, 4.9, 2.0, 6.0 FROM lab_test_definitions WHERE code = 'RBC';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('HGB', 'Гемоглобін (HGB)', '718-7', 'г/л', 'Безціанідний колориметричний (SLS)', 15.0, 72, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_CBC' AND t.code = 'HGB'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'M', 16, 120, 130.0, 160.0, 70.0, 200.0 FROM lab_test_definitions WHERE code = 'HGB';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'F', 16, 120, 120.0, 140.0, 65.0, 190.0 FROM lab_test_definitions WHERE code = 'HGB';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 0, 1, 110.0, 140.0, 80.0, 180.0 FROM lab_test_definitions WHERE code = 'HGB';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('HCT', 'Гематокрит (HCT)', '4544-3', '%', 'Розрахунковий / Центрифугування', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_CBC' AND t.code = 'HCT'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'M', 16, 120, 40.0, 48.0, 20.0, 60.0 FROM lab_test_definitions WHERE code = 'HCT';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'F', 16, 120, 36.0, 42.0, 18.0, 55.0 FROM lab_test_definitions WHERE code = 'HCT';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('PLT', 'Тромбоцити (PLT)', '777-3', '10^9/л', 'Кондуктометричний / Оптичний', 25.0, 72, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_CBC' AND t.code = 'PLT'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 0, 120, 180.0, 320.0, 50.0, 800.0 FROM lab_test_definitions WHERE code = 'PLT';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('ESR', 'Швидкість осідання еритроцитів (ШОЕ)', '30341-2', 'мм/год', 'Метод Панченкова / Вестергрена', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_CBC' AND t.code = 'ESR'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'M', 16, 120, 2.0, 10.0, NULL, 60.0 FROM lab_test_definitions WHERE code = 'ESR';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'F', 16, 50, 2.0, 15.0, NULL, 65.0 FROM lab_test_definitions WHERE code = 'ESR';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'F', 51, 120, 2.0, 20.0, NULL, 70.0 FROM lab_test_definitions WHERE code = 'ESR';

INSERT INTO lab_test_profiles (code, name, category, turnaround_hours, fasting_required, is_active)
VALUES ('PROF_BIOCHEM_BASE', 'Базовий біохімічний профіль (Ниркові + Печінкові проби + Глюкоза)', 'Клінічна біохімія', 4, true, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, category = EXCLUDED.category;

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('GLU', 'Глюкоза сироватки', '2345-7', 'ммоль/л', 'Гексокіназний', 20.0, 48, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_BIOCHEM_BASE' AND t.code = 'GLU'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 0, 120, 4.1, 5.9, 2.5, 25.0 FROM lab_test_definitions WHERE code = 'GLU';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('CREAT', 'Креатинін сироватки', '2160-0', 'мкмоль/л', 'Кінетичний метод Яффе (компенсований) / Ферментативний', 25.0, 72, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_BIOCHEM_BASE' AND t.code = 'CREAT'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'M', 16, 120, 62.0, 115.0, 30.0, 500.0 FROM lab_test_definitions WHERE code = 'CREAT';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'F', 16, 120, 53.0, 97.0, 25.0, 450.0 FROM lab_test_definitions WHERE code = 'CREAT';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('UREA', 'Сечовина', '3094-0', 'ммоль/л', 'Уреазний кінетичний UV', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_BIOCHEM_BASE' AND t.code = 'UREA'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 16, 120, 2.5, 8.3, 1.0, 35.0 FROM lab_test_definitions WHERE code = 'UREA';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('ALT', 'Аланінамінотрансфераза (АЛТ)', '1742-6', 'Од/л', 'Кінетичний UV без піридоксальфосфату (IFCC)', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_BIOCHEM_BASE' AND t.code = 'ALT'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'M', 16, 120, 0.0, 41.0, NULL, 500.0 FROM lab_test_definitions WHERE code = 'ALT';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'F', 16, 120, 0.0, 31.0, NULL, 400.0 FROM lab_test_definitions WHERE code = 'ALT';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('AST', 'Аспартатамінотрансфераза (АСТ)', '1920-8', 'Од/л', 'Кінетичний UV без піридоксальфосфату (IFCC)', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_BIOCHEM_BASE' AND t.code = 'AST'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'M', 16, 120, 0.0, 37.0, NULL, 500.0 FROM lab_test_definitions WHERE code = 'AST';
INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'F', 16, 120, 0.0, 31.0, NULL, 400.0 FROM lab_test_definitions WHERE code = 'AST';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('BIL_TOT', 'Білірубін загальний', '1975-2', 'мкмоль/л', 'Діазореакція (Ендрашика-Грофа / DPD)', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_BIOCHEM_BASE' AND t.code = 'BIL_TOT'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 1, 120, 3.4, 20.5, NULL, 250.0 FROM lab_test_definitions WHERE code = 'BIL_TOT';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('PROT_TOT', 'Загальний білок', '2885-2', 'г/л', 'Біуретовий колориметричний', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_BIOCHEM_BASE' AND t.code = 'PROT_TOT'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 16, 120, 64.0, 83.0, 40.0, 120.0 FROM lab_test_definitions WHERE code = 'PROT_TOT';

INSERT INTO lab_test_profiles (code, name, category, turnaround_hours, fasting_required, is_active)
VALUES ('PROF_COAG', 'Коагулограма (Скринінг гемостазу)', 'Гемостаз', 3, true, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, category = EXCLUDED.category;

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('INR', 'Міжнародне нормалізоване відношення (МНВ / INR)', '6301-6', 'од', 'Коагулометричний / Нефелометричний', 30.0, 48, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_COAG' AND t.code = 'INR'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 0, 120, 0.85, 1.15, 0.5, 4.5 FROM lab_test_definitions WHERE code = 'INR';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('APTT', 'Активований частковий тромбопластиновий час (АЧТЧ / APTT)', '3173-2', 'сек', 'Коагулометричний', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_COAG' AND t.code = 'APTT'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 0, 120, 25.0, 37.0, 15.0, 70.0 FROM lab_test_definitions WHERE code = 'APTT';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('FIBRINOGEN', 'Фібриноген за Клаусом', '3255-7', 'г/л', 'Коагулометричний (Клаус)', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_COAG' AND t.code = 'FIBRINOGEN'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 0, 120, 2.0, 4.0, 1.0, 10.0 FROM lab_test_definitions WHERE code = 'FIBRINOGEN';

INSERT INTO lab_test_profiles (code, name, category, turnaround_hours, fasting_required, is_active)
VALUES ('PROF_THYROID', 'Тиреоїдна панель (ТТГ + Т4 вільний)', 'Імунохімія та гормони', 6, true, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, category = EXCLUDED.category;

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('TSH', 'Тиреотропний гормон (ТТГ)', '3016-3', 'мкМО/мл', 'Хемілюмінесцентний імуноаналіз (CLIA / ECLIA)', 40.0, 168, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_THYROID' AND t.code = 'TSH'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 18, 120, 0.4, 4.0, 0.01, 30.0 FROM lab_test_definitions WHERE code = 'TSH';

INSERT INTO lab_test_definitions (code, name, loinc_code, unit, method_name, delta_check_max_pct, delta_check_hours, is_active)
VALUES ('FT4', 'Тироксин вільний (вТ4)', '3024-7', 'пмоль/л', 'Хемілюмінесцентний імуноаналіз (CLIA / ECLIA)', NULL, NULL, true)
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, unit = EXCLUDED.unit, loinc_code = EXCLUDED.loinc_code;

INSERT INTO lab_test_profile_items (profile_id, test_id)
SELECT p.id, t.id FROM lab_test_profiles p, lab_test_definitions t WHERE p.code = 'PROF_THYROID' AND t.code = 'FT4'
ON CONFLICT DO NOTHING;

INSERT INTO lab_reference_ranges (test_id, gender, age_years_from, age_years_to, norm_low, norm_high, crit_low, crit_high)
SELECT id, 'ANY', 18, 120, 10.0, 23.2, 4.0, 50.0 FROM lab_test_definitions WHERE code = 'FT4';

