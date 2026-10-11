-- =============================================================================
-- MedLink LIS R1.1 · FR-PRE-004 — правила окремих/об'єднаних пробірок (план пробірок)
-- Цільова СУБД: PostgreSQL (evomis / MedLink). Ідемпотентний скрипт.
-- Позначки: [MedLink] — сутність evomis (не змінюється), [MedLink+] — сутність evomis з
-- доданими колонками, [ЛІС] — таблиця модуля «Лабораторія».
-- Таблиці MedLink у цій міграції не змінюються.
-- =============================================================================
BEGIN;

-- [ЛІС] lab_test_definition — показник (тест): правила тари
ALTER TABLE lab_test_definition ADD COLUMN IF NOT EXISTS requires_separate_tube boolean NOT NULL DEFAULT false;
ALTER TABLE lab_test_definition ADD COLUMN IF NOT EXISTS max_tests_per_tube integer NULL;
ALTER TABLE lab_test_definition ADD COLUMN IF NOT EXISTS required_volume_ml double precision NULL;
ALTER TABLE lab_test_definition ADD COLUMN IF NOT EXISTS tube_compatibility_group varchar(32) NULL;
COMMENT ON COLUMN lab_test_definition.requires_separate_tube IS 'FR-PRE-004: тест завжди в окремій тарі';
COMMENT ON COLUMN lab_test_definition.max_tests_per_tube IS 'FR-PRE-004: ліміт тестів у пробірці з цим тестом';
COMMENT ON COLUMN lab_test_definition.required_volume_ml IS 'FR-PRE-004: об''єм матеріалу, що споживає тест, мл';
COMMENT ON COLUMN lab_test_definition.tube_compatibility_group IS 'FR-PRE-004: група сумісності тестів в одній тарі';

-- [ЛІС] lab_tube_type — тип тари
ALTER TABLE lab_tube_type ADD COLUMN IF NOT EXISTS usable_volume_ml double precision NULL;
ALTER TABLE lab_tube_type ADD COLUMN IF NOT EXISTS max_tests_per_tube integer NULL;
COMMENT ON COLUMN lab_tube_type.usable_volume_ml IS 'FR-PRE-004: корисний об''єм матеріалу (сироватка/плазма/сеча), мл';
COMMENT ON COLUMN lab_tube_type.max_tests_per_tube IS 'FR-PRE-004: ліміт тестів на одиницю тари';

-- [ЛІС] lab_order_sample — проба замовлення
ALTER TABLE lab_order_sample ADD COLUMN IF NOT EXISTS plan_reasons varchar(128) NULL;
COMMENT ON COLUMN lab_order_sample.plan_reasons IS 'FR-PRE-004: коди причин окремої пробірки (SEPARATE_REQUIRED, VOLUME_SPLIT, TEST_LIMIT_SPLIT, COMPATIBILITY_GROUP, OVER_CAPACITY)';

-- Початкові правила (як у демо-довідниках ЛІС)
UPDATE lab_tube_type SET usable_volume_ml = 1.2 WHERE code IN ('SERUM_GEL', 'CITRATE') AND usable_volume_ml IS NULL;
UPDATE lab_tube_type SET usable_volume_ml = 3.0 WHERE code = 'LI_HEPARIN' AND usable_volume_ml IS NULL;
UPDATE lab_tube_type SET usable_volume_ml = 50 WHERE code = 'URINE_CONTAINER' AND usable_volume_ml IS NULL;
UPDATE lab_tube_type SET max_tests_per_tube = 1 WHERE code IN ('BLOOD_CULTURE', 'SLIDE') AND max_tests_per_tube IS NULL;
UPDATE lab_test_definition SET requires_separate_tube = true WHERE code = 'URINE_CULTURE';

COMMIT;
