-- =============================================================================
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
