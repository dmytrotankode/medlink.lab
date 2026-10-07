-- =============================================================================
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
