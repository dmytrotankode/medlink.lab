# -*- coding: utf-8 -*-
"""
Script to create the complete MedLink LIS SQL suite inside C:\__MEDLINK___\LABA\sql
"""

import os
import shutil

BASE_DIR = r"C:\__MEDLINK___\LABA"
SQL_DIR = os.path.join(BASE_DIR, "sql")
DICT_DIR = os.path.join(BASE_DIR, "dictionaries")

os.makedirs(SQL_DIR, exist_ok=True)

# 1. 01_lis_schema_core.sql
schema_core = """-- =============================================================================
-- MedLink LIS 3.0: Core PostgreSQL Database Schema
-- Platform: MedLink (evomis) / .NET 8 / EF Core / PostgreSQL 14+
-- Copyright (c) MedLink LLC. All rights reserved.
-- =============================================================================

START TRANSACTION;

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- -----------------------------------------------------------------------------
-- 1. DICTIONARIES & NOMENCLATURE
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS lab_biomaterial_types (
    id SERIAL PRIMARY KEY,
    code VARCHAR(64) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    config JSONB DEFAULT '{}'::jsonb,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_tube_types (
    id SERIAL PRIMARY KEY,
    code VARCHAR(64) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    color_code VARCHAR(32) NOT NULL, -- Hex or RGB or Int color identifier
    volume_ml VARCHAR(32),
    order_of_draw_index INT DEFAULT 99, -- standard CLSI Order of Draw sequence
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_method_types (
    id SERIAL PRIMARY KEY,
    code VARCHAR(64) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_analyzer_types (
    id SERIAL PRIMARY KEY,
    code VARCHAR(64) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    note TEXT,
    config JSONB DEFAULT '{"exch_type":"ASTM"}'::jsonb,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_analyzers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(64) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    analyzer_type_id INT REFERENCES lab_analyzer_types(id),
    department_id UUID, -- links to org_departments
    connection_mode VARCHAR(32) NOT NULL DEFAULT 'TCP', -- 'TCP', 'COM', 'FILE'
    ip_host VARCHAR(128),
    ip_port INT,
    com_port VARCHAR(32),
    com_baudrate INT DEFAULT 9600,
    com_parity VARCHAR(16) DEFAULT 'None',
    com_databits INT DEFAULT 8,
    com_stopbits VARCHAR(16) DEFAULT 'One',
    file_incoming_path VARCHAR(512),
    file_archive_path VARCHAR(512),
    is_active BOOLEAN NOT NULL DEFAULT true,
    is_online BOOLEAN NOT NULL DEFAULT false,
    last_ping_at TIMESTAMP WITHOUT TIME ZONE,
    last_error_message TEXT,
    config JSONB DEFAULT '{}'::jsonb,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_test_definitions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(64) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    short_name VARCHAR(64),
    loinc_code VARCHAR(64),
    unit VARCHAR(64),
    method_id INT REFERENCES lab_method_types(id),
    method_name VARCHAR(255),
    result_value_type VARCHAR(32) NOT NULL DEFAULT 'NUMERIC', -- 'NUMERIC', 'STRING', 'DROPDOWN', 'CALCULATED'
    dropdown_options JSONB DEFAULT '[]'::jsonb,
    formula TEXT,
    is_calculated BOOLEAN NOT NULL DEFAULT false,
    delta_check_max_pct NUMERIC(6, 2),
    delta_check_hours INT DEFAULT 72,
    is_qc_tracked BOOLEAN NOT NULL DEFAULT true,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_analyzer_parameters (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    analyzer_id UUID NOT NULL REFERENCES lab_analyzers(id) ON DELETE CASCADE,
    analyzer_param_code VARCHAR(64) NOT NULL,
    test_id UUID NOT NULL REFERENCES lab_test_definitions(id) ON DELETE CASCADE,
    multiplier_coefficient NUMERIC(15, 5) NOT NULL DEFAULT 1.0,
    additive_coefficient NUMERIC(15, 5) NOT NULL DEFAULT 0.0,
    unit_override VARCHAR(64),
    is_active BOOLEAN NOT NULL DEFAULT true,
    note TEXT,
    CONSTRAINT uq_analyzer_param UNIQUE (analyzer_id, analyzer_param_code)
);

CREATE TABLE IF NOT EXISTS lab_test_profiles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(64) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    category VARCHAR(128) NOT NULL,
    turnaround_hours INT DEFAULT 24,
    fasting_required BOOLEAN NOT NULL DEFAULT true,
    price NUMERIC(12, 2) DEFAULT 0.0,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_test_profile_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    profile_id UUID NOT NULL REFERENCES lab_test_profiles(id) ON DELETE CASCADE,
    test_id UUID NOT NULL REFERENCES lab_test_definitions(id) ON DELETE CASCADE,
    display_order INT DEFAULT 0,
    is_required BOOLEAN NOT NULL DEFAULT true,
    CONSTRAINT uq_profile_test UNIQUE (profile_id, test_id)
);

CREATE TABLE IF NOT EXISTS lab_reference_ranges (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    test_id UUID NOT NULL REFERENCES lab_test_definitions(id) ON DELETE CASCADE,
    gender VARCHAR(8) NOT NULL DEFAULT 'ANY', -- 'M', 'F', 'ANY'
    age_years_from INT DEFAULT 0,
    age_years_to INT DEFAULT 120,
    age_months_from INT DEFAULT 0,
    age_months_to INT DEFAULT 1440,
    pregnancy_week_from INT,
    pregnancy_week_to INT,
    menstrual_phase VARCHAR(64), -- 'FOLLICULAR', 'OVULATORY', 'LUTEAL', 'POSTMENOPAUSE'
    icd10_code VARCHAR(16),
    analyzer_id UUID REFERENCES lab_analyzers(id),
    method_id INT REFERENCES lab_method_types(id),
    norm_low NUMERIC(18, 6),
    norm_high NUMERIC(18, 6),
    crit_low NUMERIC(18, 6), -- Panic Low
    crit_high NUMERIC(18, 6), -- Panic High
    norm_text TEXT,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_reflex_rules (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    trigger_test_id UUID NOT NULL REFERENCES lab_test_definitions(id) ON DELETE CASCADE,
    condition_op VARCHAR(16) NOT NULL, -- '<', '<=', '>', '>=', '==', 'OUT_OF_RANGE', 'CRITICAL'
    threshold_value NUMERIC(18, 6),
    threshold_text VARCHAR(128),
    target_test_id UUID NOT NULL REFERENCES lab_test_definitions(id) ON DELETE CASCADE,
    auto_approve BOOLEAN NOT NULL DEFAULT false,
    reason_description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true
);

-- -----------------------------------------------------------------------------
-- 2. ORDERS, SAMPLES, LOGISTICS & TESTING WORKFLOW
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS lab_orders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_number VARCHAR(64) UNIQUE NOT NULL,
    patient_id UUID NOT NULL, -- links to mis_patient_card
    ordering_doctor_id UUID, -- links to org_employee
    department_id UUID, -- collection room / branch (org_department)
    order_datetime TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT clock_timestamp(),
    status VARCHAR(32) NOT NULL DEFAULT 'NEW', -- 'NEW', 'COLLECTED', 'IN_TRANSIT', 'RECEIVED', 'PROCESSING', 'VERIFIED', 'COMPLETED', 'CANCELLED'
    is_urgent_cito BOOLEAN NOT NULL DEFAULT false,
    ehealth_referral_id UUID, -- links to ehe_incoming_medical_referral
    clinical_notes TEXT,
    created_by_id UUID,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_order_samples (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id UUID NOT NULL REFERENCES lab_orders(id) ON DELETE CASCADE,
    barcode VARCHAR(64) UNIQUE NOT NULL,
    tube_type_id INT NOT NULL REFERENCES lab_tube_types(id),
    biomaterial_type_id INT NOT NULL REFERENCES lab_biomaterial_types(id),
    collected_at TIMESTAMP WITHOUT TIME ZONE,
    collected_by_id UUID,
    sampling_site VARCHAR(64),
    status VARCHAR(32) NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'COLLECTED', 'IN_TRANSIT', 'RECEIVED', 'PROCESSING', 'STORED', 'DISPOSED', 'REJECTED'
    reject_reason VARCHAR(255),
    is_hemolyzed BOOLEAN NOT NULL DEFAULT false,
    is_lipemic BOOLEAN NOT NULL DEFAULT false,
    is_icteric BOOLEAN NOT NULL DEFAULT false,
    is_insufficient_volume BOOLEAN NOT NULL DEFAULT false,
    volume_collected_ml NUMERIC(6, 2),
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_order_tests (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id UUID NOT NULL REFERENCES lab_orders(id) ON DELETE CASCADE,
    sample_id UUID REFERENCES lab_order_samples(id) ON DELETE SET NULL,
    profile_id UUID REFERENCES lab_test_profiles(id),
    test_id UUID NOT NULL REFERENCES lab_test_definitions(id),
    status VARCHAR(32) NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'IN_ANALYSIS', 'COMPLETED', 'VERIFIED', 'REJECTED'
    assigned_analyzer_id UUID REFERENCES lab_analyzers(id),
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_test_results (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_test_id UUID NOT NULL REFERENCES lab_order_tests(id) ON DELETE CASCADE,
    numeric_value NUMERIC(18, 6),
    string_value TEXT,
    flag VARCHAR(16) NOT NULL DEFAULT 'NORMAL', -- 'NORMAL', 'LOW', 'HIGH', 'CRIT_LOW', 'CRIT_HIGH'
    unit VARCHAR(64),
    reference_interval_display VARCHAR(255),
    is_auto_verified BOOLEAN NOT NULL DEFAULT false,
    verified_by_id UUID,
    verified_at TIMESTAMP WITHOUT TIME ZONE,
    delta_check_alert BOOLEAN NOT NULL DEFAULT false,
    delta_check_diff_pct NUMERIC(6, 2),
    previous_value NUMERIC(18, 6),
    previous_datetime TIMESTAMP WITHOUT TIME ZONE,
    analyzer_id UUID REFERENCES lab_analyzers(id),
    analyzer_raw_message TEXT,
    operator_comment TEXT,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_batches (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    batch_code VARCHAR(64) UNIQUE NOT NULL,
    analyzer_id UUID REFERENCES lab_analyzers(id),
    created_by_id UUID,
    status VARCHAR(32) NOT NULL DEFAULT 'OPEN', -- 'OPEN', 'SENT_TO_ANALYZER', 'COMPLETED'
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

-- -----------------------------------------------------------------------------
-- 3. QUALITY CONTROL (QC / ВЯК)
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS lab_qc_materials (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    analyzer_id UUID NOT NULL REFERENCES lab_analyzers(id) ON DELETE CASCADE,
    material_name VARCHAR(255) NOT NULL,
    level VARCHAR(32) NOT NULL, -- 'LEVEL_1_LOW', 'LEVEL_2_NORMAL', 'LEVEL_3_HIGH'
    lot_number VARCHAR(64) NOT NULL,
    manufacturer VARCHAR(128),
    expiry_date DATE NOT NULL,
    open_stability_days INT DEFAULT 30,
    opened_at DATE,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp(),
    CONSTRAINT uq_qc_lot UNIQUE (analyzer_id, material_name, lot_number)
);

CREATE TABLE IF NOT EXISTS lab_qc_targets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    qc_material_id UUID NOT NULL REFERENCES lab_qc_materials(id) ON DELETE CASCADE,
    test_id UUID NOT NULL REFERENCES lab_test_definitions(id) ON DELETE CASCADE,
    target_mean NUMERIC(18, 6) NOT NULL,
    target_sd NUMERIC(18, 6) NOT NULL,
    target_cv_pct NUMERIC(6, 2),
    total_allowable_error_pct NUMERIC(6, 2),
    unit VARCHAR(64),
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp(),
    CONSTRAINT uq_qc_target UNIQUE (qc_material_id, test_id)
);

CREATE TABLE IF NOT EXISTS lab_qc_results (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    qc_target_id UUID NOT NULL REFERENCES lab_qc_targets(id) ON DELETE CASCADE,
    measured_value NUMERIC(18, 6) NOT NULL,
    z_score NUMERIC(6, 3), -- (measured - target_mean) / target_sd
    is_violation BOOLEAN NOT NULL DEFAULT false,
    violated_rule VARCHAR(32), -- '1_2s', '1_3s', '2_2s', 'R_4s', '4_1s', '10_x'
    is_warning BOOLEAN NOT NULL DEFAULT false,
    lockout_enforced BOOLEAN NOT NULL DEFAULT false, -- Blocks release of patient tests
    resolved_by_id UUID,
    resolution_comment TEXT,
    resolved_at TIMESTAMP WITHOUT TIME ZONE,
    run_datetime TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT clock_timestamp(),
    operator_id UUID,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

-- -----------------------------------------------------------------------------
-- 4. LOGISTICS, COLD-CHAIN & BIOBANK ARCHIVE
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS lab_sample_logistics (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    transfer_act_number VARCHAR(64) UNIQUE NOT NULL,
    origin_point_id UUID NOT NULL, -- Sampling room / филиал
    destination_point_id UUID NOT NULL, -- Central Laboratory
    courier_name VARCHAR(128),
    courier_phone VARCHAR(32),
    dispatched_at TIMESTAMP WITHOUT TIME ZONE,
    dispatched_by_id UUID,
    temperature_dispatch_celsius NUMERIC(4, 1),
    received_at TIMESTAMP WITHOUT TIME ZONE,
    received_by_id UUID,
    temperature_receipt_celsius NUMERIC(4, 1),
    is_cold_chain_violated BOOLEAN NOT NULL DEFAULT false,
    status VARCHAR(32) NOT NULL DEFAULT 'CREATED', -- 'CREATED', 'DISPATCHED', 'IN_TRANSIT', 'RECEIVED', 'REJECTED'
    notes TEXT,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_sample_logistics_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    logistics_id UUID NOT NULL REFERENCES lab_sample_logistics(id) ON DELETE CASCADE,
    sample_id UUID NOT NULL REFERENCES lab_order_samples(id) ON DELETE CASCADE,
    status VARCHAR(32) NOT NULL DEFAULT 'EN_ROUTE',
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp(),
    CONSTRAINT uq_logistics_sample UNIQUE (logistics_id, sample_id)
);

CREATE TABLE IF NOT EXISTS lab_sample_archive_racks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(64) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    room_number VARCHAR(32),
    freezer_name VARCHAR(128),
    shelf_number VARCHAR(32),
    temperature_celsius NUMERIC(4, 1) DEFAULT -20.0,
    rows_count INT NOT NULL DEFAULT 10,
    cols_count INT NOT NULL DEFAULT 10,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_sample_archive_cells (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    rack_id UUID NOT NULL REFERENCES lab_sample_archive_racks(id) ON DELETE CASCADE,
    row_num INT NOT NULL,
    col_num INT NOT NULL,
    sample_id UUID REFERENCES lab_order_samples(id) ON DELETE SET NULL,
    stored_at TIMESTAMP WITHOUT TIME ZONE,
    stored_by_id UUID,
    expiry_at TIMESTAMP WITHOUT TIME ZONE,
    is_disposed BOOLEAN NOT NULL DEFAULT false,
    disposed_at TIMESTAMP WITHOUT TIME ZONE,
    disposal_reason VARCHAR(255),
    CONSTRAINT uq_rack_cell UNIQUE (rack_id, row_num, col_num)
);

-- -----------------------------------------------------------------------------
-- 5. REAGENT LOT MANAGEMENT
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS lab_reagent_lots (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    analyzer_id UUID NOT NULL REFERENCES lab_analyzers(id) ON DELETE CASCADE,
    test_id UUID NOT NULL REFERENCES lab_test_definitions(id) ON DELETE CASCADE,
    reagent_name VARCHAR(255) NOT NULL,
    lot_number VARCHAR(64) NOT NULL,
    tests_initial INT NOT NULL,
    tests_remaining INT NOT NULL,
    expiry_date DATE NOT NULL,
    opened_at TIMESTAMP WITHOUT TIME ZONE,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

-- -----------------------------------------------------------------------------
-- INDEXES FOR MAXIMUM QUERY PERFORMANCE (500+ ROWS VIRTUAL SCROLL)
-- -----------------------------------------------------------------------------

CREATE INDEX IF NOT EXISTS idx_lab_orders_patient_id ON lab_orders(patient_id);
CREATE INDEX IF NOT EXISTS idx_lab_orders_status ON lab_orders(status);
CREATE INDEX IF NOT EXISTS idx_lab_orders_datetime ON lab_orders(order_datetime DESC);
CREATE INDEX IF NOT EXISTS idx_lab_order_samples_barcode ON lab_order_samples(barcode);
CREATE INDEX IF NOT EXISTS idx_lab_order_samples_order_id ON lab_order_samples(order_id);
CREATE INDEX IF NOT EXISTS idx_lab_order_tests_order_id ON lab_order_tests(order_id);
CREATE INDEX IF NOT EXISTS idx_lab_order_tests_status ON lab_order_tests(status);
CREATE INDEX IF NOT EXISTS idx_lab_test_results_order_test ON lab_test_results(order_test_id);
CREATE INDEX IF NOT EXISTS idx_lab_test_results_flag ON lab_test_results(flag);
CREATE INDEX IF NOT EXISTS idx_lab_qc_results_target_date ON lab_qc_results(qc_target_id, run_datetime DESC);
CREATE INDEX IF NOT EXISTS idx_lab_sample_logistics_status ON lab_sample_logistics(status);

COMMIT;
"""

with open(os.path.join(SQL_DIR, "01_lis_schema_core.sql"), "w", encoding="utf-8") as f:
    f.write(schema_core)

# 2. 02_lis_schema_microbiology_eucast.sql
schema_micro = """-- =============================================================================
-- MedLink LIS 3.0: Microbiology, Bacterial Culture & EUCAST Susceptibility Schema
-- Compliant with European Committee on Antimicrobial Susceptibility Testing (EUCAST)
-- =============================================================================

START TRANSACTION;

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- -----------------------------------------------------------------------------
-- 1. MICROBIAL DICTIONARIES (Organisms & Antibiotics)
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS lab_micro_organisms (
    id SERIAL PRIMARY KEY,
    code VARCHAR(64) UNIQUE NOT NULL,
    latin_name VARCHAR(255) NOT NULL,
    common_name VARCHAR(255),
    kingdom VARCHAR(32) NOT NULL DEFAULT 'BACTERIA', -- 'BACTERIA', 'FUNGI', 'VIRUS', 'PARASITE'
    gram_stain VARCHAR(16), -- 'GRAM_POSITIVE', 'GRAM_NEGATIVE', 'ACID_FAST', 'VARIABLE'
    morphology VARCHAR(64), -- 'COCCI', 'BACILLI', 'COCCOBACILLI', 'SPIRILLA', 'YEAST'
    is_pathogen BOOLEAN NOT NULL DEFAULT true,
    is_opportunistic BOOLEAN NOT NULL DEFAULT false,
    alert_critical_organism BOOLEAN NOT NULL DEFAULT false, -- e.g. MRSA, VRE, CRE, KPC
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_antibiotics (
    id SERIAL PRIMARY KEY,
    code VARCHAR(64) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    group_name VARCHAR(128) NOT NULL, -- e.g. 'Penicillins', 'Cephalosporins 3rd gen', 'Carbapenems'
    atc_code VARCHAR(16), -- WHO ATC classification
    eucast_code VARCHAR(32),
    default_disk_content_mcg NUMERIC(8, 2), -- Disk content in mcg (e.g. 10 mcg for Ampicillin)
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_eucast_breakpoints (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organism_id INT NOT NULL REFERENCES lab_micro_organisms(id) ON DELETE CASCADE,
    antibiotic_id INT NOT NULL REFERENCES lab_antibiotics(id) ON DELETE CASCADE,
    eucast_version VARCHAR(32) NOT NULL DEFAULT 'v14.0_2024',
    mic_susceptible_le NUMERIC(8, 3), -- MIC <= S (mg/L)
    mic_resistant_gt NUMERIC(8, 3),   -- MIC > R (mg/L)
    zone_susceptible_ge NUMERIC(6, 1), -- Zone diameter >= S (mm)
    zone_resistant_lt NUMERIC(6, 1),   -- Zone diameter < R (mm)
    intrinsic_resistance BOOLEAN NOT NULL DEFAULT false,
    guidance_notes TEXT,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp(),
    CONSTRAINT uq_eucast_org_atb UNIQUE (organism_id, antibiotic_id, eucast_version)
);

-- -----------------------------------------------------------------------------
-- 2. CULTURE OBSERVATIONS & SUSCEPTIBILITY WORKFLOW
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS lab_culture_orders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_test_id UUID NOT NULL REFERENCES lab_order_tests(id) ON DELETE CASCADE,
    incubation_start_datetime TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT clock_timestamp(),
    incubation_hours_recommended INT DEFAULT 48,
    culture_medium VARCHAR(128) NOT NULL, -- 'Blood Agar', 'MacConkey', 'Sabouraud', 'CLED', etc.
    growth_detected BOOLEAN,
    growth_intensity VARCHAR(32), -- 'NO_GROWTH', 'SCANTY_1+', 'MODERATE_2+', 'HEAVY_3+', 'PROFUSE_4+'
    cfu_per_ml VARCHAR(64), -- e.g. '10^5 КУО/мл'
    preliminary_report TEXT,
    final_microscopy_description TEXT,
    status VARCHAR(32) NOT NULL DEFAULT 'INCUBATING', -- 'INCUBATING', 'PRELIMINARY', 'ISOLATED', 'COMPLETED'
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS lab_isolated_isolates (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    culture_order_id UUID NOT NULL REFERENCES lab_culture_orders(id) ON DELETE CASCADE,
    isolate_number INT NOT NULL DEFAULT 1,
    organism_id INT NOT NULL REFERENCES lab_micro_organisms(id),
    quantitative_count VARCHAR(64), -- e.g. '10^6 CFU/ml'
    is_clinically_significant BOOLEAN NOT NULL DEFAULT true,
    colony_morphology TEXT,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp(),
    CONSTRAINT uq_isolate UNIQUE (culture_order_id, isolate_number)
);

CREATE TABLE IF NOT EXISTS lab_antibiotic_susceptibility_results (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    isolate_id UUID NOT NULL REFERENCES lab_isolated_isolates(id) ON DELETE CASCADE,
    antibiotic_id INT NOT NULL REFERENCES lab_antibiotics(id) ON DELETE CASCADE,
    method VARCHAR(32) NOT NULL DEFAULT 'DISK_DIFFUSION', -- 'DISK_DIFFUSION', 'MIC_E_TEST', 'VITEK_AUTOMATED'
    zone_diameter_mm NUMERIC(5, 1),
    mic_value_mg_l NUMERIC(8, 3),
    interpretation VARCHAR(8) NOT NULL DEFAULT 'S', -- 'S' (Susceptible), 'I' (Susceptible Increased Exposure), 'R' (Resistant)
    is_intrinsic_resistance BOOLEAN NOT NULL DEFAULT false,
    override_reason TEXT,
    overridden_by_id UUID,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT clock_timestamp(),
    CONSTRAINT uq_isolate_atb UNIQUE (isolate_id, antibiotic_id)
);

CREATE INDEX IF NOT EXISTS idx_lab_culture_order_test ON lab_culture_orders(order_test_id);
CREATE INDEX IF NOT EXISTS idx_lab_isolates_culture ON lab_isolated_isolates(culture_order_id);
CREATE INDEX IF NOT EXISTS idx_lab_ast_isolate ON lab_antibiotic_susceptibility_results(isolate_id);

COMMIT;
"""

with open(os.path.join(SQL_DIR, "02_lis_schema_microbiology_eucast.sql"), "w", encoding="utf-8") as f:
    f.write(schema_micro)

# 3. 03_evomis_integration_views_and_fk.sql
integration_sql = """-- =============================================================================
-- MedLink LIS 3.0: Integration Views & Foreign Keys with Core MedLink (evomis)
-- Database: evomis / evomis-test (PostgreSQL 14+)
-- Tables integrated:
--   - public.mis_patient_card
--   - public.mis_specimen
--   - public.mis_diagnostic_report
--   - public.org_employee
--   - public.org_department
--   - public.ehe_incoming_medical_referral
-- =============================================================================

START TRANSACTION;

-- 1. Integration Foreign Key constraints (conditional if evomis tables exist in the same database)
DO $$
BEGIN
    -- Link lab_orders to mis_patient_card
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'mis_patient_card') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'fk_lab_orders_patient_card') THEN
            ALTER TABLE lab_orders
            ADD CONSTRAINT fk_lab_orders_patient_card
            FOREIGN KEY (patient_id) REFERENCES mis_patient_card(id) ON DELETE RESTRICT;
        END IF;
    END IF;

    -- Link lab_orders to org_employee
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'org_employee') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'fk_lab_orders_doctor') THEN
            ALTER TABLE lab_orders
            ADD CONSTRAINT fk_lab_orders_doctor
            FOREIGN KEY (ordering_doctor_id) REFERENCES org_employee(id) ON DELETE SET NULL;
        END IF;
    END IF;

    -- Link lab_orders to org_department
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'org_department') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'fk_lab_orders_department') THEN
            ALTER TABLE lab_orders
            ADD CONSTRAINT fk_lab_orders_department
            FOREIGN KEY (department_id) REFERENCES org_department(id) ON DELETE SET NULL;
        END IF;
    END IF;

    -- Link lab_orders to ehe_incoming_medical_referral
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ehe_incoming_medical_referral') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'fk_lab_orders_ehealth_referral') THEN
            ALTER TABLE lab_orders
            ADD CONSTRAINT fk_lab_orders_ehealth_referral
            FOREIGN KEY (ehealth_referral_id) REFERENCES ehe_incoming_medical_referral(id) ON DELETE SET NULL;
        END IF;
    END IF;
END $$;

-- 2. High-performance Unified Results View for EHR/EMR Patient History
CREATE OR REPLACE VIEW v_patient_laboratory_history AS
SELECT 
    o.id AS order_id,
    o.order_number,
    o.patient_id,
    o.order_datetime,
    o.status AS order_status,
    o.is_urgent_cito,
    s.barcode AS sample_barcode,
    b.name AS biomaterial_name,
    t.name AS tube_type_name,
    t.color_code AS tube_color,
    p.code AS profile_code,
    p.name AS profile_name,
    td.code AS test_code,
    td.name AS test_name,
    td.loinc_code,
    r.numeric_value,
    r.string_value,
    r.unit,
    r.flag,
    r.reference_interval_display,
    r.is_auto_verified,
    r.verified_at,
    a.name AS analyzer_name,
    r.delta_check_alert,
    r.delta_check_diff_pct
FROM lab_orders o
LEFT JOIN lab_order_samples s ON s.order_id = o.id
LEFT JOIN lab_biomaterial_types b ON s.biomaterial_type_id = b.id
LEFT JOIN lab_tube_types t ON s.tube_type_id = t.id
LEFT JOIN lab_order_tests ot ON ot.order_id = o.id AND (ot.sample_id = s.id OR ot.sample_id IS NULL)
LEFT JOIN lab_test_profiles p ON ot.profile_id = p.id
LEFT JOIN lab_test_definitions td ON ot.test_id = td.id
LEFT JOIN lab_test_results r ON r.order_test_id = ot.id
LEFT JOIN lab_analyzers a ON r.analyzer_id = a.id;

-- 3. Turnaround Time (TAT) Analytics View for Quality Audits
CREATE OR REPLACE VIEW v_lab_turnaround_time_analytics AS
SELECT
    o.id AS order_id,
    o.order_number,
    o.is_urgent_cito,
    p.name AS profile_name,
    o.order_datetime,
    s.collected_at,
    l.dispatched_at,
    l.received_at AS lab_received_at,
    r.created_at AS analysis_at,
    r.verified_at,
    ROUND(EXTRACT(EPOCH FROM (s.collected_at - o.order_datetime)) / 60) AS order_to_collection_min,
    ROUND(EXTRACT(EPOCH FROM (l.received_at - s.collected_at)) / 60) AS logistics_duration_min,
    ROUND(EXTRACT(EPOCH FROM (r.created_at - l.received_at)) / 60) AS analysis_duration_min,
    ROUND(EXTRACT(EPOCH FROM (r.verified_at - r.created_at)) / 60) AS verification_duration_min,
    ROUND(EXTRACT(EPOCH FROM (r.verified_at - o.order_datetime)) / 60) AS total_tat_min
FROM lab_orders o
JOIN lab_order_samples s ON s.order_id = o.id
LEFT JOIN lab_sample_logistics_items li ON li.sample_id = s.id
LEFT JOIN lab_sample_logistics l ON li.logistics_id = l.id
JOIN lab_order_tests ot ON ot.order_id = o.id
LEFT JOIN lab_test_profiles p ON ot.profile_id = p.id
JOIN lab_test_results r ON r.order_test_id = ot.id
WHERE r.verified_at IS NOT NULL;

COMMIT;
"""

with open(os.path.join(SQL_DIR, "03_evomis_integration_views_and_fk.sql"), "w", encoding="utf-8") as f:
    f.write(integration_sql)

# Copy dictionaries
shutil.copyfile(os.path.join(DICT_DIR, "01_biomaterials.sql"), os.path.join(SQL_DIR, "04_seed_biomaterials.sql"))
shutil.copyfile(os.path.join(DICT_DIR, "02_tube_types.sql"), os.path.join(SQL_DIR, "05_seed_tube_types.sql"))
shutil.copyfile(os.path.join(DICT_DIR, "03_method_types.sql"), os.path.join(SQL_DIR, "06_seed_method_types.sql"))
shutil.copyfile(os.path.join(DICT_DIR, "04_analyzer_types.sql"), os.path.join(SQL_DIR, "07_seed_analyzer_types.sql"))
shutil.copyfile(os.path.join(DICT_DIR, "05_lab_parameters_and_profiles.sql"), os.path.join(SQL_DIR, "08_seed_parameters_and_profiles.sql"))

# 09_seed_microbiology_eucast.sql
micro_seed = """-- =============================================================================
-- MedLink LIS 3.0: Seed Microbiology Pathogens, Antibiotics & EUCAST Breakpoints
-- =============================================================================

START TRANSACTION;

-- 1. Standard Clinical Microorganisms
INSERT INTO lab_micro_organisms (id, code, latin_name, common_name, kingdom, gram_stain, morphology, alert_critical_organism, is_active) VALUES
  (1, 'SAUR', 'Staphylococcus aureus', 'Золотистий стафілокок', 'BACTERIA', 'GRAM_POSITIVE', 'COCCI', false, true),
  (2, 'MRSA', 'Staphylococcus aureus (MRSA)', 'Метицилін-резистентний стафілокок', 'BACTERIA', 'GRAM_POSITIVE', 'COCCI', true, true),
  (3, 'ECOL', 'Escherichia coli', 'Кишкова паличка', 'BACTERIA', 'GRAM_NEGATIVE', 'BACILLI', false, true),
  (4, 'KPNE', 'Klebsiella pneumoniae', 'Клебсієла пневмонії', 'BACTERIA', 'GRAM_NEGATIVE', 'BACILLI', false, true),
  (5, 'PAER', 'Pseudomonas aeruginosa', 'Синьогнійна паличка', 'BACTERIA', 'GRAM_NEGATIVE', 'BACILLI', true, true),
  (6, 'ABAU', 'Acinetobacter baumannii', 'Ацинетобактер Баумана', 'BACTERIA', 'GRAM_NEGATIVE', 'COCCOBACILLI', true, true),
  (7, 'SPNE', 'Streptococcus pneumoniae', 'Пневмокок', 'BACTERIA', 'GRAM_POSITIVE', 'COCCI', false, true),
  (8, 'EFAL', 'Enterococcus faecalis', 'Ентерокок фекальний', 'BACTERIA', 'GRAM_POSITIVE', 'COCCI', false, true),
  (9, 'CALB', 'Candida albicans', 'Кандида альбіканс', 'FUNGI', NULL, 'YEAST', false, true)
ON CONFLICT (id) DO UPDATE SET latin_name = EXCLUDED.latin_name, alert_critical_organism = EXCLUDED.alert_critical_organism;

-- 2. Standard Antibiotics & Chemotherapeutics
INSERT INTO lab_antibiotics (id, code, name, group_name, atc_code, default_disk_content_mcg, is_active) VALUES
  (1, 'AMX', 'Амоксицилін', 'Penicillins', 'J01CA04', 25.0, true),
  (2, 'AMC', 'Амоксицилін / Клавуланат', 'Penicillins + Inhibitors', 'J01CR02', 30.0, true),
  (3, 'CTX', 'Цефотаксим', 'Cephalosporins 3rd gen', 'J01DD01', 30.0, true),
  (4, 'CRO', 'Цефтріаксон', 'Cephalosporins 3rd gen', 'J01DD04', 30.0, true),
  (5, 'CAZ', 'Цефтазидим', 'Cephalosporins 3rd gen', 'J01DD02', 10.0, true),
  (6, 'MEM', 'Меропенем', 'Carbapenems', 'J01DH02', 10.0, true),
  (7, 'IPM', 'Іміпенем', 'Carbapenems', 'J01DH51', 10.0, true),
  (8, 'CIP', 'Ципрофлоксацин', 'Fluoroquinolones', 'J01MA02', 5.0, true),
  (9, 'LEV', 'Левофлоксацин', 'Fluoroquinolones', 'J01MA12', 5.0, true),
  (10, 'GEN', 'Гентаміцин', 'Aminoglycosides', 'J01GB03', 10.0, true),
  (11, 'AMK', 'Амікацин', 'Aminoglycosides', 'J01GB06', 30.0, true),
  (12, 'VAN', 'Ванкоміцин', 'Glycopeptides', 'J01XA01', 5.0, true),
  (13, 'LZD', 'Лінезолід', 'Oxazolidinones', 'J01XX08', 10.0, true)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, group_name = EXCLUDED.group_name;

-- 3. EUCAST Breakpoints (v14.0)
-- E. coli vs Ciprofloxacin: S >= 25 mm, R < 22 mm; MIC S <= 0.25, R > 0.5
INSERT INTO lab_eucast_breakpoints (organism_id, antibiotic_id, eucast_version, mic_susceptible_le, mic_resistant_gt, zone_susceptible_ge, zone_resistant_lt)
VALUES (3, 8, 'v14.0_2024', 0.25, 0.5, 25.0, 22.0)
ON CONFLICT DO NOTHING;

-- E. coli vs Meropenem: S >= 22 mm, R < 16 mm; MIC S <= 2.0, R > 8.0
INSERT INTO lab_eucast_breakpoints (organism_id, antibiotic_id, eucast_version, mic_susceptible_le, mic_resistant_gt, zone_susceptible_ge, zone_resistant_lt)
VALUES (3, 6, 'v14.0_2024', 2.0, 8.0, 22.0, 16.0)
ON CONFLICT DO NOTHING;

-- S. aureus vs Vancomycin: MIC S <= 2.0, R > 2.0
INSERT INTO lab_eucast_breakpoints (organism_id, antibiotic_id, eucast_version, mic_susceptible_le, mic_resistant_gt, zone_susceptible_ge, zone_resistant_lt)
VALUES (1, 12, 'v14.0_2024', 2.0, 2.0, NULL, NULL)
ON CONFLICT DO NOTHING;

COMMIT;
"""

with open(os.path.join(SQL_DIR, "09_seed_microbiology_eucast.sql"), "w", encoding="utf-8") as f:
    f.write(micro_seed)

# 00_master_deploy_all.sql
master_sql = """-- =============================================================================
-- MedLink LIS 3.0: MASTER DEPLOYMENT SCRIPT
-- Executes all schema creations, views, indexes, and standard seeds
-- in a single resilient PostgreSQL transaction.
-- =============================================================================

\\echo '=== 1/9 Deploying MedLink LIS Core Schema ==='
\\ir 01_lis_schema_core.sql

\\echo '=== 2/9 Deploying Microbiology & EUCAST Schema ==='
\\ir 02_lis_schema_microbiology_eucast.sql

\\echo '=== 3/9 Deploying evomis Integration Views & Constraints ==='
\\ir 03_evomis_integration_views_and_fk.sql

\\echo '=== 4/9 Seeding Biomaterials ==='
\\ir 04_seed_biomaterials.sql

\\echo '=== 5/9 Seeding Vacuum Tube Types ==='
\\ir 05_seed_tube_types.sql

\\echo '=== 6/9 Seeding Analytical Methods ==='
\\ir 06_seed_method_types.sql

\\echo '=== 7/9 Seeding Analyzer Models & Protocols ==='
\\ir 07_seed_analyzer_types.sql

\\echo '=== 8/9 Seeding Parameters, Profiles, LOINC & Reference Ranges ==='
\\ir 08_seed_parameters_and_profiles.sql

\\echo '=== 9/9 Seeding Microbiology Organisms, Antibiotics & EUCAST ==='
\\ir 09_seed_microbiology_eucast.sql

\\echo '=== MEDLINK LIS 3.0 DATABASE DEPLOYMENT COMPLETE! ==='
"""

with open(os.path.join(SQL_DIR, "00_master_deploy_all.sql"), "w", encoding="utf-8") as f:
    f.write(master_sql)

print("SQL suite generated successfully inside:", SQL_DIR)
for fname in sorted(os.listdir(SQL_DIR)):
    path = os.path.join(SQL_DIR, fname)
    print(f" - {fname} ({os.path.getsize(path)} bytes)")
