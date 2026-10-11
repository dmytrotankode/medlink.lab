-- =============================================================================
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
