-- =============================================================================
-- MedLink LIS R1.1 · ТЗ розд. 21 і 23 — інтервенції ЕСОЗ і співставлення, послуги обладнання, маршрутизація по приладах,
-- направлення МІС з обраними показниками і пакетами, розподіл, підпис і відправка звітів в ЕСОЗ (КЕП/2FA), звіти
-- Цільова СУБД: PostgreSQL (evomis / MedLink). Ідемпотентний. Фрагмент повної схеми db/postgres/medlink_lis_schema.sql.
-- Позначки: [ЛІС] — таблиця модуля «Лабораторія». Таблиці MedLink у цій міграції не змінюються.
-- =============================================================================
BEGIN;

-- [ЛІС] нові колонки наявних таблиць (lab_order, lab_order_test, lab_analyzer, lab_analyzer_parameter_map, lab_test_profile_item, lab_settings)
ALTER TABLE lab_analyzer ADD COLUMN IF NOT EXISTS avg_tat_minutes integer NOT NULL DEFAULT 30;
ALTER TABLE lab_analyzer ADD COLUMN IF NOT EXISTS kind varchar(16) NOT NULL DEFAULT 'ANALYZER';
ALTER TABLE lab_analyzer ADD COLUMN IF NOT EXISTS location varchar(256);
ALTER TABLE lab_analyzer ADD COLUMN IF NOT EXISTS offline_since timestamp without time zone;
ALTER TABLE lab_analyzer ADD COLUMN IF NOT EXISTS service_status varchar(16) NOT NULL DEFAULT 'ACTIVE';
ALTER TABLE lab_analyzer_parameter_map ADD COLUMN IF NOT EXISTS is_enabled boolean NOT NULL DEFAULT true;
ALTER TABLE lab_analyzer_parameter_map ADD COLUMN IF NOT EXISTS priority integer;
ALTER TABLE lab_analyzer_parameter_map ADD COLUMN IF NOT EXISTS requires_dilution boolean NOT NULL DEFAULT false;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS bed varchar(32);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS case_no varchar(32);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS distributed_at timestamp without time zone;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS distributed_by_id uuid;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS distribution_status varchar(16) NOT NULL DEFAULT 'NONE';
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS ehealth_status varchar(32) NOT NULL DEFAULT 'NOT_SENT';
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS lab_department varchar(16);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS mis_hospitalization_id uuid;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS mis_operation_id uuid;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS mis_referral_id uuid;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS mis_referral_number varchar(64);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS package_changed_by_doctor boolean NOT NULL DEFAULT false;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS package_code varchar(64);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS package_name varchar(256);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS pathology_kind varchar(16);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS planned_collection_at timestamp without time zone;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS referral_context varchar(16);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS requesting_department_name varchar(256);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS room varchar(32);
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS sla_due_at timestamp without time zone;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS ward varchar(32);
CREATE INDEX IF NOT EXISTS ix_lab_order_lab_department ON lab_order (lab_department);
CREATE INDEX IF NOT EXISTS ix_lab_order_mis_referral_id ON lab_order (mis_referral_id);
ALTER TABLE lab_order_test ADD COLUMN IF NOT EXISTS distributed_at timestamp without time zone;
ALTER TABLE lab_order_test ADD COLUMN IF NOT EXISTS distributed_by_id uuid;
ALTER TABLE lab_order_test ADD COLUMN IF NOT EXISTS enqueued_on timestamp without time zone;
ALTER TABLE lab_order_test ADD COLUMN IF NOT EXISTS order_item_id uuid;
ALTER TABLE lab_order_test ADD COLUMN IF NOT EXISTS routed_by varchar(8);
ALTER TABLE lab_order_test ADD COLUMN IF NOT EXISTS routing_status varchar(16);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_assigned_analyzer_id_status ON lab_order_test (assigned_analyzer_id, status);
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS auto_reroute_offline boolean NOT NULL DEFAULT true;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS distribution_target_minutes integer NOT NULL DEFAULT 15;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS ehealth_session_idle_minutes integer NOT NULL DEFAULT 15;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS ehealth_session_ttl_minutes integer NOT NULL DEFAULT 30;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS ehealth_submit_inpatient boolean NOT NULL DEFAULT false;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS intervention_categories varchar(256) NOT NULL DEFAULT 'laboratory_procedure';
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS intervention_sync_cron varchar(64) NOT NULL DEFAULT '0 3 * * *';
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS pathology_cito_target_minutes integer NOT NULL DEFAULT 20;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS pathology_fixation_max_hours integer NOT NULL DEFAULT 72;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS pathology_fixation_min_hours integer NOT NULL DEFAULT 6;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS pathology_mirror_to_simplex_cabinet boolean NOT NULL DEFAULT false;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS require_distribution_confirmation boolean NOT NULL DEFAULT true;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS reroute_on_offline_minutes integer NOT NULL DEFAULT 30;
ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS routing_strategy varchar(24) NOT NULL DEFAULT 'PRIORITY_THEN_LOAD';
ALTER TABLE lab_test_profile_item ADD COLUMN IF NOT EXISTS is_default_selected boolean NOT NULL DEFAULT true;

-- [ЛІС] lab_analyzer_service (LabAnalyzerService)
CREATE TABLE IF NOT EXISTS lab_analyzer_service (
    id uuid NOT NULL,
    analyzer_id uuid NOT NULL,
    avg_tat_minutes integer NOT NULL,
    biomaterial_type_id integer,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_enabled boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    note varchar(512),
    priority integer NOT NULL,
    profile_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_analyzer_service PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_analyzer_service_profile_id ON lab_analyzer_service (profile_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_analyzer_service_analyzer_id_profile_id ON lab_analyzer_service (analyzer_id, profile_id);

-- [ЛІС] lab_ehealth_intervention (LabEhealthIntervention)
CREATE TABLE IF NOT EXISTS lab_ehealth_intervention (
    id uuid NOT NULL,
    category varchar(64) NOT NULL,
    code varchar(64) NOT NULL,
    code_system varchar(128) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(512) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    service_group varchar(32),
    source_version varchar(32),
    synced_on timestamp without time zone,
    CONSTRAINT pk_lab_ehealth_intervention PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_ehealth_intervention_code ON lab_ehealth_intervention (code);

-- [ЛІС] lab_ehealth_session (LabEhealthSession)
CREATE TABLE IF NOT EXISTS lab_ehealth_session (
    id uuid NOT NULL,
    auth_level varchar(4) NOT NULL,
    auth_method varchar(32) NOT NULL,
    cert_serial varchar(64),
    closed_at timestamp without time zone,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    employee_id uuid NOT NULL,
    expires_at timestamp without time zone NOT NULL,
    last_activity_on timestamp without time zone NOT NULL,
    login varchar(128) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    signer_drfo varchar(16),
    signer_name varchar(256),
    token_enc text,
    totp_enabled boolean NOT NULL,
    CONSTRAINT pk_lab_ehealth_session PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_ehealth_session_employee_id_closed_at ON lab_ehealth_session (employee_id, closed_at);

-- [ЛІС] lab_ehealth_submission (LabEhealthSubmission)
CREATE TABLE IF NOT EXISTS lab_ehealth_submission (
    id uuid NOT NULL,
    attempts integer NOT NULL,
    cert_serial varchar(64),
    completed_at timestamp without time zone,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    diagnostic_report_id uuid,
    ehealth_report_id uuid,
    job_id uuid,
    kind varchar(16) NOT NULL,
    last_error varchar(1024),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    next_attempt_at timestamp without time zone,
    order_id uuid NOT NULL,
    package_hash varchar(128),
    package_json text,
    record_state integer NOT NULL DEFAULT 2,
    signed_by varchar(64),
    signed_by_name varchar(256),
    signed_data text,
    signed_on timestamp without time zone,
    status varchar(32) NOT NULL,
    CONSTRAINT pk_lab_ehealth_submission PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_ehealth_submission_order_id_status ON lab_ehealth_submission (order_id, status);

-- [ЛІС] lab_intervention_map (LabInterventionMap)
CREATE TABLE IF NOT EXISTS lab_intervention_map (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    indicator_selection_mode varchar(16) NOT NULL,
    intervention_id uuid NOT NULL,
    is_active boolean NOT NULL,
    is_pathology boolean NOT NULL,
    min_selected integer NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    note varchar(512),
    profile_id uuid,
    readiness varchar(16) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_intervention_map PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_intervention_map_intervention_id ON lab_intervention_map (intervention_id);
CREATE INDEX IF NOT EXISTS ix_lab_intervention_map_profile_id ON lab_intervention_map (profile_id);

-- [ЛІС] lab_intervention_map_test (LabInterventionMapTest)
CREATE TABLE IF NOT EXISTS lab_intervention_map_test (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    display_order integer NOT NULL,
    is_default boolean NOT NULL,
    is_required boolean NOT NULL,
    map_id uuid NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    test_id uuid NOT NULL,
    CONSTRAINT pk_lab_intervention_map_test PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_intervention_map_test_test_id ON lab_intervention_map_test (test_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_intervention_map_test_map_id_test_id ON lab_intervention_map_test (map_id, test_id);

-- [ЛІС] lab_mis_event_log (LabMisEventLog)
CREATE TABLE IF NOT EXISTS lab_mis_event_log (
    id uuid NOT NULL,
    at timestamp without time zone NOT NULL,
    attempts integer NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    event varchar(64) NOT NULL,
    last_error varchar(1024),
    mis_referral_id uuid,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid,
    payload text,
    record_state integer NOT NULL DEFAULT 2,
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_mis_event_log PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_mis_event_log_order_id_at ON lab_mis_event_log (order_id, at);

-- [ЛІС] lab_order_item (LabOrderItem)
CREATE TABLE IF NOT EXISTS lab_order_item (
    id uuid NOT NULL,
    comment varchar(1024),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    display_order integer NOT NULL,
    ehealth_service_request_id uuid,
    intervention_code varchar(64),
    intervention_id uuid,
    intervention_name varchar(512),
    is_cito boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    package_item_no integer,
    profile_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    selection_mode varchar(16) NOT NULL,
    selection_source varchar(32) NOT NULL,
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_order_item PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_order_item_intervention_id ON lab_order_item (intervention_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_item_order_id ON lab_order_item (order_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_item_profile_id ON lab_order_item (profile_id);

-- [ЛІС] lab_order_item_indicator (LabOrderItemIndicator)
CREATE TABLE IF NOT EXISTS lab_order_item_indicator (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_auto_added boolean NOT NULL,
    is_required boolean NOT NULL,
    is_selected boolean NOT NULL,
    item_id uuid NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_test_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    selected_by varchar(32),
    selected_on timestamp without time zone,
    test_code varchar(64) NOT NULL,
    test_id uuid NOT NULL,
    CONSTRAINT pk_lab_order_item_indicator PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_order_item_indicator_item_id ON lab_order_item_indicator (item_id);

-- [ЛІС] lab_report_run (LabReportRun)
CREATE TABLE IF NOT EXISTS lab_report_run (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    format varchar(8) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    params_json text,
    patient_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    report_type varchar(32) NOT NULL,
    rows integer NOT NULL,
    run_at timestamp without time zone NOT NULL,
    run_by varchar(64),
    CONSTRAINT pk_lab_report_run PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_report_run_run_at ON lab_report_run (run_at);

-- [ЛІС] lab_routing_log (LabRoutingLog)
CREATE TABLE IF NOT EXISTS lab_routing_log (
    id uuid NOT NULL,
    actor_id varchar(64),
    analyzer_id uuid,
    at timestamp without time zone NOT NULL,
    candidates_json text,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    event varchar(16) NOT NULL,
    from_analyzer_id uuid,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid,
    order_test_id uuid NOT NULL,
    reason varchar(512),
    record_state integer NOT NULL DEFAULT 2,
    routed_by varchar(16) NOT NULL,
    rule varchar(64),
    test_code varchar(64),
    CONSTRAINT pk_lab_routing_log PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_routing_log_at ON lab_routing_log (at);
CREATE INDEX IF NOT EXISTS ix_lab_routing_log_order_test_id ON lab_routing_log (order_test_id);

-- [ЛІС] lab_service_package (LabServicePackage)
CREATE TABLE IF NOT EXISTS lab_service_package (
    id uuid NOT NULL,
    code varchar(64) NOT NULL,
    context varchar(16) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    is_cito boolean NOT NULL,
    items_json text NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    purpose varchar(256),
    record_state integer NOT NULL DEFAULT 2,
    source_version varchar(32),
    synced_on timestamp without time zone,
    CONSTRAINT pk_lab_service_package PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_service_package_code ON lab_service_package (code);

-- [ЛІС] lab_stat_daily (LabStatDaily)
CREATE TABLE IF NOT EXISTS lab_stat_daily (
    id uuid NOT NULL,
    analyzer_id uuid,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    day timestamp without time zone NOT NULL,
    department_id uuid,
    is_cito boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    package_code varchar(64),
    profile_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    rejected integer NOT NULL,
    reruns integer NOT NULL,
    source varchar(16) NOT NULL,
    tat_p50 double precision,
    tat_p90 double precision,
    test_code varchar(64),
    tests integer NOT NULL,
    CONSTRAINT pk_lab_stat_daily PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_stat_daily_day ON lab_stat_daily (day);

DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_service_analyzer_id') THEN ALTER TABLE lab_analyzer_service ADD CONSTRAINT fk_lab_analyzer_service_analyzer_id FOREIGN KEY (analyzer_id) REFERENCES lab_analyzer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_service_profile_id') THEN ALTER TABLE lab_analyzer_service ADD CONSTRAINT fk_lab_analyzer_service_profile_id FOREIGN KEY (profile_id) REFERENCES lab_test_profile (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_ehealth_submission_order_id') THEN ALTER TABLE lab_ehealth_submission ADD CONSTRAINT fk_lab_ehealth_submission_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_intervention_map_intervention_id') THEN ALTER TABLE lab_intervention_map ADD CONSTRAINT fk_lab_intervention_map_intervention_id FOREIGN KEY (intervention_id) REFERENCES lab_ehealth_intervention (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_intervention_map_profile_id') THEN ALTER TABLE lab_intervention_map ADD CONSTRAINT fk_lab_intervention_map_profile_id FOREIGN KEY (profile_id) REFERENCES lab_test_profile (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_intervention_map_test_map_id') THEN ALTER TABLE lab_intervention_map_test ADD CONSTRAINT fk_lab_intervention_map_test_map_id FOREIGN KEY (map_id) REFERENCES lab_intervention_map (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_intervention_map_test_test_id') THEN ALTER TABLE lab_intervention_map_test ADD CONSTRAINT fk_lab_intervention_map_test_test_id FOREIGN KEY (test_id) REFERENCES lab_test_definition (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_item_intervention_id') THEN ALTER TABLE lab_order_item ADD CONSTRAINT fk_lab_order_item_intervention_id FOREIGN KEY (intervention_id) REFERENCES lab_ehealth_intervention (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_item_order_id') THEN ALTER TABLE lab_order_item ADD CONSTRAINT fk_lab_order_item_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_item_profile_id') THEN ALTER TABLE lab_order_item ADD CONSTRAINT fk_lab_order_item_profile_id FOREIGN KEY (profile_id) REFERENCES lab_test_profile (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_item_indicator_item_id') THEN ALTER TABLE lab_order_item_indicator ADD CONSTRAINT fk_lab_order_item_indicator_item_id FOREIGN KEY (item_id) REFERENCES lab_order_item (id) ON DELETE CASCADE; END IF; END $$;
COMMIT;
