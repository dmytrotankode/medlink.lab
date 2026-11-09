-- =============================================================================
-- MedLink LIS R1.1 · Біохімічний каталог і довідники-сутності: одиниці виміру та правила перерахунку (SI ↔ традиційні),
-- вікові групи, причини браку та відмови, журнал застосованих стартових наборів даних; нові колонки у показниках, нормах,
-- пробірках і замовленнях. Дані (одиниці, групи, причини, показники, норми) заповнює сід db/seed/biochem_catalog.json
-- (SeedService/BiochemCatalogSeeder) — лише відсутнє, без перезапису ручних змін. Ідемпотентний. Цільова СУБД: PostgreSQL.
-- Позначки: [ЛІС] — таблиця модуля «Лабораторія».
-- =============================================================================
BEGIN;

-- [ЛІС] lab_age_group (LabAgeGroup)
CREATE TABLE IF NOT EXISTS lab_age_group (
    id integer NOT NULL,
    age_from double precision NOT NULL,
    age_to double precision NOT NULL,
    age_unit varchar(8) NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    sort_order integer NOT NULL,
    CONSTRAINT pk_lab_age_group PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_age_group_code ON lab_age_group (code);

-- [ЛІС] lab_cancel_reason (LabCancelReason)
CREATE TABLE IF NOT EXISTS lab_cancel_reason (
    id integer NOT NULL,
    category varchar(32) NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    sort_order integer NOT NULL,
    CONSTRAINT pk_lab_cancel_reason PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_cancel_reason_code ON lab_cancel_reason (code);

-- [ЛІС] lab_rejection_reason (LabRejectionReason)
CREATE TABLE IF NOT EXISTS lab_rejection_reason (
    id integer NOT NULL,
    category varchar(32) NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    requires_recollection boolean NOT NULL,
    sort_order integer NOT NULL,
    CONSTRAINT pk_lab_rejection_reason PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_rejection_reason_code ON lab_rejection_reason (code);

-- [ЛІС] lab_seed_marker (LabSeedMarker)
CREATE TABLE IF NOT EXISTS lab_seed_marker (
    code uuid NOT NULL,
    applied_on timestamp without time zone NOT NULL,
    note varchar(256),
    CONSTRAINT pk_lab_seed_marker PRIMARY KEY (code)
);

-- [ЛІС] lab_unit (LabUnit)
CREATE TABLE IF NOT EXISTS lab_unit (
    id integer NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    dimension varchar(32) NOT NULL,
    full_name varchar(256),
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(64) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    sort_order integer NOT NULL,
    system_kind varchar(16) NOT NULL,
    CONSTRAINT pk_lab_unit PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_unit_code ON lab_unit (code);

-- [ЛІС] lab_unit_conversion (LabUnitConversion)
CREATE TABLE IF NOT EXISTS lab_unit_conversion (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    factor double precision NOT NULL,
    from_unit_id integer NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    note varchar(256),
    offset double precision NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    test_code varchar(64) NOT NULL,
    to_unit_id integer NOT NULL,
    CONSTRAINT pk_lab_unit_conversion PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_unit_conversion_from_unit_id ON lab_unit_conversion (from_unit_id);
CREATE INDEX IF NOT EXISTS ix_lab_unit_conversion_to_unit_id ON lab_unit_conversion (to_unit_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_unit_conversion_test_code_from_unit_id_to_unit_id ON lab_unit_conversion (test_code, from_unit_id, to_unit_id);

-- Нові колонки наявних таблиць
ALTER TABLE lab_test_definition ADD COLUMN IF NOT EXISTS unit_id integer;
ALTER TABLE lab_reference_layer ADD COLUMN IF NOT EXISTS unit_id integer;
ALTER TABLE lab_reference_layer ADD COLUMN IF NOT EXISTS age_group_id integer;
ALTER TABLE lab_reference_layer ADD COLUMN IF NOT EXISTS source varchar(256);
ALTER TABLE lab_order_sample ADD COLUMN IF NOT EXISTS reject_reason_code varchar(64);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS cancel_reason_code varchar(64);

DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_unit_conversion_from_unit_id') THEN ALTER TABLE lab_unit_conversion ADD CONSTRAINT fk_lab_unit_conversion_from_unit_id FOREIGN KEY (from_unit_id) REFERENCES lab_unit (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_unit_conversion_to_unit_id') THEN ALTER TABLE lab_unit_conversion ADD CONSTRAINT fk_lab_unit_conversion_to_unit_id FOREIGN KEY (to_unit_id) REFERENCES lab_unit (id); END IF; END $$;

COMMIT;
