-- =============================================================================
-- MedLink LIS — схема модуля «Лабораторія» для PostgreSQL (evomis). Згенеровано з EF-моделі ЛІС.
-- Дата генерації: 2026-11-09. Не редагувати вручну — перегенерувати: --export-pg-ddl.
-- [MedLink]  — таблиця evomis, не створюється (наведено колонки, які використовує ЛІС).
-- [MedLink+] — колонки, які ЛІС додає до таблиці evomis.
-- [ЛІС]      — таблиця модуля, створюється цим скриптом.
-- [MedLink-new] — нова загальна сутність МІС (пропонується до ядра MedLink), створюється цим скриптом.
-- =============================================================================
BEGIN;

-- ----------------------------------------------------------------------------- [MedLink]
-- [MedLink] cmn_enum_record: id, caption, code, created_by, created_on, enum_type, modified_by, modified_on, parent_id, record_state, value_type
-- [MedLink] cmn_person: id, birthday, caption, created_by, created_on, description, email, gender_id, ipn, last_name, location, middle_name, modified_by, modified_on, name, no_ipn, phone, record_state, subscribed_to_notifications
-- [MedLink] ehe_incoming_medical_referral: id, caption, complete_date_in_ehealth, completed_with_item_entity_id, completed_with_item_entity_name, created_by, created_on, ehealth_id, ehealth_patient_id, ehealth_program_id, ehealth_status, expiration_date, last_synced_at, medical_referral_category_id, medical_service_program_id, modified_by, modified_on, organization_id, patient_card_id, patient_instruction, patient_short_name, priority_id, processing_status_in_ehealth_id, record_state, reg_date, reg_number, requester_id, service_catalog_service_id, status_id, take_in_work_date, used_in_ehealth
-- [MedLink] ehe_outbox: id, attempts, created_by, created_on, done_at, entity_id, error_code, idempotency_key, job_id, kind, last_error, locked_by, locked_until, modified_by, modified_on, next_attempt_at, order_id, payload, poll_attempts, record_state, status, updated_at
-- [MedLink] ehe_paper_medical_referral: id, caption, created_by, created_on, description, modified_by, modified_on, organization_id, patient_card_id, processed_date, record_state, reg_date, reg_number, requester_employee_name, requester_legal_entity_edrpou, requester_legal_entity_name, status
-- [MedLink] ehe_service_catalog_service: id, caption, code, created_by, created_on, ehealth_id, inserted_at_in_ehealth, is_active, is_composition, medical_referral_category_id, modified_by, modified_on, name, record_state, request_allowed
-- [MedLink] mis_diagnostic_report: id, caption, comment, created_by, created_on, description, diagnostic_report_category_id, division_id, document_type_id, effective_date_time_end, effective_date_time_start, ehealth_id, ehealth_incoming_medical_referral_id, ehealth_service_catalog_service_id, is_performer_string, is_primary_source, issued_at, legal_entity_id, modified_by, modified_on, organization_id, patient_card_id, performer_id, performer_string, record_state, recorded_by_id, reg_date, reg_number, status
-- [MedLink] mis_patient_card: id, birthday, caption, created_by, created_on, description, document_type_id, gender_id, location, modified_by, modified_on, organization_id, patient_card_type_id, person_id, privacy_request_type_id, record_state, reg_date, reg_number
ALTER TABLE mis_patient_card ADD COLUMN IF NOT EXISTS last_name_latin varchar(100); -- [MedLink+]
ALTER TABLE mis_patient_card ADD COLUMN IF NOT EXISTS first_name_latin varchar(100); -- [MedLink+]
-- [MedLink] org_department: id, caption, category, code, created_by, created_on, department_type_id, description, full_name, location, modified_by, modified_on, organization_id, parent_id, record_state, zip_code
-- [MedLink] org_employee: id, caption, created_by, created_on, department_id, department_section_id, has_schedule_with_partner, modified_by, modified_on, organization_id, person_id, position_type_id, record_state, working_start_date
-- [MedLink] org_organization: id, caption, code, created_by, created_on, description, full_name, modified_by, modified_on, record_state
-- [MedLink] org_organization_service: id, caption, duration, organization_id, price, profile_id

-- [ЛІС] dct_icdo (LabIcdo)
CREATE TABLE IF NOT EXISTS dct_icdo (
    id uuid NOT NULL,
    axis varchar(8) NOT NULL,
    code varchar(16) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    version varchar(16) NOT NULL,
    CONSTRAINT pk_dct_icdo PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_dct_icdo_axis_code ON dct_icdo (axis, code);

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

-- [ЛІС] lab_analyzer (LabAnalyzer)
CREATE TABLE IF NOT EXISTS lab_analyzer (
    id uuid NOT NULL,
    analyzer_type_id integer NOT NULL,
    auto_query_orders boolean NOT NULL,
    avg_tat_minutes integer NOT NULL,
    baud_rate integer NOT NULL,
    code varchar(64) NOT NULL,
    com_port varchar(32),
    connection_mode varchar(8) NOT NULL,
    connector_id uuid,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    data_bits integer NOT NULL,
    department_id uuid,
    file_path varchar(512),
    file_poll_sec integer NOT NULL,
    flow_control varchar(16) NOT NULL,
    is_active boolean NOT NULL,
    is_online boolean NOT NULL,
    is_tcp_server boolean NOT NULL,
    kind varchar(16) NOT NULL,
    last_error varchar(1024),
    last_message_at timestamp without time zone,
    location varchar(256),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    offline_since timestamp without time zone,
    parity varchar(8) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    service_status varchar(16) NOT NULL,
    stop_bits varchar(16) NOT NULL,
    tcp_host varchar(128),
    tcp_port integer,
    CONSTRAINT pk_lab_analyzer PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_analyzer_analyzer_type_id ON lab_analyzer (analyzer_type_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_analyzer_code ON lab_analyzer (code);
CREATE INDEX IF NOT EXISTS ix_lab_analyzer_connector_id ON lab_analyzer (connector_id);
CREATE INDEX IF NOT EXISTS ix_lab_analyzer_department_id ON lab_analyzer (department_id);

-- [ЛІС] lab_analyzer_lockout (LabAnalyzerLockout)
CREATE TABLE IF NOT EXISTS lab_analyzer_lockout (
    id uuid NOT NULL,
    action varchar(512),
    analyzer_id uuid NOT NULL,
    cause varchar(512),
    comment varchar(1024),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    protocol_text text,
    qc_result_id uuid,
    reason varchar(512) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    resolved_at timestamp without time zone,
    resolved_by_id uuid,
    started_at timestamp without time zone NOT NULL,
    test_code varchar(64),
    CONSTRAINT pk_lab_analyzer_lockout PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_analyzer_lockout_analyzer_id_resolved_at ON lab_analyzer_lockout (analyzer_id, resolved_at);

-- [ЛІС] lab_analyzer_message (LabAnalyzerMessage)
CREATE TABLE IF NOT EXISTS lab_analyzer_message (
    id uuid NOT NULL,
    analyzer_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    direction varchar(4) NOT NULL,
    error varchar(1024),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    parsed_ok boolean NOT NULL,
    protocol varchar(16) NOT NULL,
    raw_text text NOT NULL,
    received_at timestamp without time zone NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    results_count integer NOT NULL,
    CONSTRAINT pk_lab_analyzer_message PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_analyzer_message_analyzer_id ON lab_analyzer_message (analyzer_id);

-- [ЛІС] lab_analyzer_parameter_map (LabAnalyzerParameterMap)
CREATE TABLE IF NOT EXISTS lab_analyzer_parameter_map (
    id uuid NOT NULL,
    analyzer_code varchar(64) NOT NULL,
    analyzer_id uuid NOT NULL,
    auto_dilution_factor double precision,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    factor double precision NOT NULL,
    is_enabled boolean NOT NULL,
    linearity_high double precision,
    linearity_low double precision,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    offset double precision NOT NULL,
    priority integer,
    record_state integer NOT NULL DEFAULT 2,
    requires_dilution boolean NOT NULL,
    test_code varchar(64) NOT NULL,
    unit_override varchar(64),
    CONSTRAINT pk_lab_analyzer_parameter_map PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_analyzer_parameter_map_analyzer_id ON lab_analyzer_parameter_map (analyzer_id);

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

-- [ЛІС] lab_analyzer_type (LabAnalyzerType)
CREATE TABLE IF NOT EXISTS lab_analyzer_type (
    id integer NOT NULL,
    bop_base64 varchar(64),
    category varchar(32) NOT NULL,
    code varchar(64) NOT NULL,
    control_sum boolean NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    eop_base64 varchar(64),
    exch_type varchar(16) NOT NULL,
    full_text boolean NOT NULL,
    is_active boolean NOT NULL,
    manufacturer varchar(128),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    order_template varchar(64) NOT NULL,
    parser_kind varchar(64) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    sleep_ms integer NOT NULL,
    CONSTRAINT pk_lab_analyzer_type PRIMARY KEY (id)
);

-- [ЛІС] lab_antibiotic (LabAntibiotic)
CREATE TABLE IF NOT EXISTS lab_antibiotic (
    id integer NOT NULL,
    atc_code varchar(16),
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    default_disk_content_mcg double precision,
    eucast_code varchar(32),
    group_name varchar(128) NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_antibiotic PRIMARY KEY (id)
);

-- [ЛІС] lab_archive_cell (LabArchiveCell)
CREATE TABLE IF NOT EXISTS lab_archive_cell (
    id uuid NOT NULL,
    barcode varchar(32),
    col_num integer NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    disposal_reason varchar(256),
    disposed_at timestamp without time zone,
    expiry_at timestamp without time zone,
    is_disposed boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    rack_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    row_num integer NOT NULL,
    sample_id uuid,
    stored_at timestamp without time zone,
    stored_by_id uuid,
    CONSTRAINT pk_lab_archive_cell PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_archive_cell_sample_id ON lab_archive_cell (sample_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_archive_cell_rack_id_row_num_col_num ON lab_archive_cell (rack_id, row_num, col_num);

-- [ЛІС] lab_archive_rack (LabArchiveRack)
CREATE TABLE IF NOT EXISTS lab_archive_rack (
    id uuid NOT NULL,
    code varchar(64) NOT NULL,
    cols_count integer NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    freezer_name varchar(128),
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    room_number varchar(64),
    rows_count integer NOT NULL,
    shelf_number varchar(32),
    temperature_celsius double precision NOT NULL,
    CONSTRAINT pk_lab_archive_rack PRIMARY KEY (id)
);

-- [ЛІС] lab_audit_log (LabAuditLog)
CREATE TABLE IF NOT EXISTS lab_audit_log (
    id uuid NOT NULL,
    action varchar(64) NOT NULL,
    after_json text,
    at timestamp without time zone NOT NULL,
    before_json text,
    comment varchar(512),
    entity varchar(64) NOT NULL,
    entity_id varchar(64),
    ip varchar(64),
    user_id varchar(64),
    user_name varchar(256),
    CONSTRAINT pk_lab_audit_log PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_audit_log_at ON lab_audit_log (at);
CREATE INDEX IF NOT EXISTS ix_lab_audit_log_entity_entity_id ON lab_audit_log (entity, entity_id);

-- [ЛІС] lab_biomaterial_type (LabBiomaterialType)
CREATE TABLE IF NOT EXISTS lab_biomaterial_type (
    id integer NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    default_container varchar(128),
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    stability_hours integer NOT NULL,
    temperature_regime varchar(32) NOT NULL,
    CONSTRAINT pk_lab_biomaterial_type PRIMARY KEY (id)
);

-- [ЛІС] lab_calibration (LabCalibration)
CREATE TABLE IF NOT EXISTS lab_calibration (
    id uuid NOT NULL,
    accepted boolean NOT NULL,
    analyzer_id uuid NOT NULL,
    calibration_value double precision,
    calibrator_lot varchar(64) NOT NULL,
    calibrator_name varchar(256),
    comment varchar(1024),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    next_due_at timestamp without time zone NOT NULL,
    performed_at timestamp without time zone NOT NULL,
    performed_by_id uuid,
    reagent_lot_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    reject_reason varchar(512),
    test_code varchar(64),
    unit varchar(64),
    valid_until timestamp without time zone NOT NULL,
    CONSTRAINT pk_lab_calibration PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_calibration_reagent_lot_id ON lab_calibration (reagent_lot_id);
CREATE INDEX IF NOT EXISTS ix_lab_calibration_analyzer_id_test_code_performed_at ON lab_calibration (analyzer_id, test_code, performed_at);

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

-- [ЛІС] lab_connector_command (LabConnectorCommand)
CREATE TABLE IF NOT EXISTS lab_connector_command (
    id uuid NOT NULL,
    connector_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    delivered_at timestamp without time zone,
    is_delivered boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    payload text,
    record_state integer NOT NULL DEFAULT 2,
    type varchar(32) NOT NULL,
    CONSTRAINT pk_lab_connector_command PRIMARY KEY (id)
);

-- [ЛІС] lab_connector_installation (LabConnectorInstallation)
CREATE TABLE IF NOT EXISTS lab_connector_installation (
    id uuid NOT NULL,
    api_key_hash varchar(128),
    buffered_count integer NOT NULL,
    config_version integer NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    heartbeat_interval_sec integer NOT NULL,
    host_name varchar(128),
    install_key varchar(64) NOT NULL,
    install_key_used boolean NOT NULL,
    last_heartbeat_at timestamp without time zone,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    os_description varchar(256),
    poll_interval_sec integer NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    registered_at timestamp without time zone,
    status varchar(16) NOT NULL,
    uptime_sec bigint NOT NULL,
    version varchar(64),
    CONSTRAINT pk_lab_connector_installation PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_connector_installation_api_key_hash ON lab_connector_installation (api_key_hash);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_connector_installation_install_key ON lab_connector_installation (install_key);

-- [ЛІС] lab_connector_log (LabConnectorLog)
CREATE TABLE IF NOT EXISTS lab_connector_log (
    id uuid NOT NULL,
    analyzer_id uuid,
    at timestamp without time zone NOT NULL,
    connector_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    level varchar(16) NOT NULL,
    message text NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_connector_log PRIMARY KEY (id)
);

-- [ЛІС] lab_counter (LabCounter)
CREATE TABLE IF NOT EXISTS lab_counter (
    counter_code uuid NOT NULL,
    counter_value bigint NOT NULL,
    CONSTRAINT pk_lab_counter PRIMARY KEY (counter_code)
);

-- [ЛІС] lab_culture_order (LabCultureOrder)
CREATE TABLE IF NOT EXISTS lab_culture_order (
    id uuid NOT NULL,
    cfu_per_ml varchar(64),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    culture_medium varchar(128) NOT NULL,
    final_microscopy_description text,
    growth_detected boolean,
    growth_intensity varchar(16),
    incubation_hours_recommended integer NOT NULL,
    incubation_start timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    order_test_id uuid NOT NULL,
    preliminary_report text,
    record_state integer NOT NULL DEFAULT 2,
    specimen_locus varchar(128),
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_culture_order PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_culture_order_order_id ON lab_culture_order (order_id);
CREATE INDEX IF NOT EXISTS ix_lab_culture_order_order_test_id ON lab_culture_order (order_test_id);

-- [ЛІС] lab_department_settings (LabDepartmentSettings)
CREATE TABLE IF NOT EXISTS lab_department_settings (
    department_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    lab_kind varchar(32) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    phone varchar(32),
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_department_settings PRIMARY KEY (department_id)
);

-- [ЛІС] lab_ehealth_exchange_log (LabEhealthExchangeLog)
CREATE TABLE IF NOT EXISTS lab_ehealth_exchange_log (
    id uuid NOT NULL,
    at timestamp without time zone NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    duration_ms integer NOT NULL,
    method varchar(8) NOT NULL,
    mode varchar(8) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    request_json text,
    response_json text,
    status_code integer,
    url varchar(512) NOT NULL,
    CONSTRAINT pk_lab_ehealth_exchange_log PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_ehealth_exchange_log_at ON lab_ehealth_exchange_log (at);

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

-- [ЛІС] lab_employee_settings (LabEmployeeSettings)
CREATE TABLE IF NOT EXISTS lab_employee_settings (
    employee_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    digital_signature_cert_id varchar(128),
    is_active boolean NOT NULL,
    lab_role varchar(32) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    position_name varchar(256),
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_employee_settings PRIMARY KEY (employee_id)
);

-- [ЛІС] lab_eucast_breakpoint (LabEucastBreakpoint)
CREATE TABLE IF NOT EXISTS lab_eucast_breakpoint (
    id uuid NOT NULL,
    antibiotic_id integer NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    eucast_version varchar(32) NOT NULL,
    guidance_notes text,
    intrinsic_resistance boolean NOT NULL,
    is_active boolean NOT NULL,
    mic_resistant_gt double precision,
    mic_susceptible_le double precision,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    organism_id integer NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    zone_resistant_lt double precision,
    zone_susceptible_ge double precision,
    CONSTRAINT pk_lab_eucast_breakpoint PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_eucast_breakpoint_antibiotic_id ON lab_eucast_breakpoint (antibiotic_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_eucast_breakpoint_organism_id_antibiotic_id_eucast_version ON lab_eucast_breakpoint (organism_id, antibiotic_id, eucast_version);

-- [ЛІС] lab_fixative (LabFixative)
CREATE TABLE IF NOT EXISTS lab_fixative (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    max_hours integer NOT NULL,
    min_hours integer NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(64) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_fixative PRIMARY KEY (id)
);

-- [ЛІС] lab_ihc_reflex_rule (LabIhcReflexRule)
CREATE TABLE IF NOT EXISTS lab_ihc_reflex_rule (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    morph_codes varchar(128) NOT NULL,
    panel varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    topo_from varchar(8) NOT NULL,
    topo_to varchar(8) NOT NULL,
    why varchar(512),
    CONSTRAINT pk_lab_ihc_reflex_rule PRIMARY KEY (id)
);

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

-- [ЛІС] lab_invoice (LabInvoice)
CREATE TABLE IF NOT EXISTS lab_invoice (
    id uuid NOT NULL,
    amount numeric(18,2) NOT NULL,
    charge_ids_json text NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    issued_at timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    number varchar(32) NOT NULL,
    order_id uuid,
    paid_at timestamp without time zone,
    payer_id uuid NOT NULL,
    period_from timestamp without time zone,
    period_to timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_invoice PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_invoice_number ON lab_invoice (number);
CREATE INDEX IF NOT EXISTS ix_lab_invoice_payer_id ON lab_invoice (payer_id);

-- [ЛІС] lab_isolate (LabIsolate)
CREATE TABLE IF NOT EXISTS lab_isolate (
    id uuid NOT NULL,
    colony_morphology text,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    culture_order_id uuid NOT NULL,
    is_clinically_significant boolean NOT NULL,
    isolate_number integer NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    organism_id integer NOT NULL,
    quantitative_count varchar(64),
    record_state integer NOT NULL DEFAULT 2,
    resistance_phenotypes varchar(128),
    CONSTRAINT pk_lab_isolate PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_isolate_culture_order_id ON lab_isolate (culture_order_id);
CREATE INDEX IF NOT EXISTS ix_lab_isolate_organism_id ON lab_isolate (organism_id);

-- [ЛІС] lab_method_type (LabMethodType)
CREATE TABLE IF NOT EXISTS lab_method_type (
    id integer NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_method_type PRIMARY KEY (id)
);

-- [ЛІС] lab_micro_organism (LabMicroOrganism)
CREATE TABLE IF NOT EXISTS lab_micro_organism (
    id integer NOT NULL,
    alert_critical_organism boolean NOT NULL,
    code varchar(64) NOT NULL,
    common_name varchar(256),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    gram_stain varchar(16),
    is_active boolean NOT NULL,
    is_opportunistic boolean NOT NULL,
    is_pathogen boolean NOT NULL,
    kingdom varchar(16) NOT NULL,
    latin_name varchar(256) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    morphology varchar(32),
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_micro_organism PRIMARY KEY (id)
);

-- [ЛІС] lab_mis_event_log (LabMisEventLog)
CREATE TABLE IF NOT EXISTS lab_mis_event_log (
    id uuid NOT NULL,
    at timestamp without time zone NOT NULL,
    attempts integer NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    delivered_at timestamp without time zone,
    event varchar(64) NOT NULL,
    http_status integer,
    last_error varchar(1024),
    locked_by varchar(96),
    locked_until timestamp without time zone,
    mis_referral_id uuid,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    next_attempt_at timestamp without time zone,
    order_id uuid,
    payload text,
    record_state integer NOT NULL DEFAULT 2,
    status varchar(16) NOT NULL,
    updated_at timestamp without time zone,
    CONSTRAINT pk_lab_mis_event_log PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_mis_event_log_order_id_at ON lab_mis_event_log (order_id, at);
CREATE INDEX IF NOT EXISTS ix_lab_mis_event_log_status_next_attempt_at ON lab_mis_event_log (status, next_attempt_at);

-- [ЛІС] lab_notify_channel (LabNotifyChannel)
CREATE TABLE IF NOT EXISTS lab_notify_channel (
    id uuid NOT NULL,
    channel varchar(16) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_enabled boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_notify_channel PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_notify_channel_channel ON lab_notify_channel (channel);

-- [ЛІС] lab_numerator (LabNumerator)
CREATE TABLE IF NOT EXISTS lab_numerator (
    code uuid NOT NULL,
    current_value bigint NOT NULL,
    mask varchar(64) NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    period_key varchar(16),
    reset_by_period boolean NOT NULL,
    CONSTRAINT pk_lab_numerator PRIMARY KEY (code)
);

-- [ЛІС] lab_order (LabOrder)
CREATE TABLE IF NOT EXISTS lab_order (
    id uuid NOT NULL,
    authorization_number varchar(64),
    bed varchar(32),
    cancel_reason varchar(512),
    cancel_reason_code varchar(64),
    case_no varchar(32),
    clinical_notes text,
    completed_at timestamp without time zone,
    created_by uuid NOT NULL,
    created_by_id uuid,
    created_on timestamp without time zone NOT NULL,
    department_id uuid,
    diagnostic_report_id uuid,
    distributed_at timestamp without time zone,
    distributed_by_id uuid,
    distribution_status varchar(16) NOT NULL,
    doctor_id uuid,
    ehealth_referral_id uuid,
    ehealth_status varchar(32) NOT NULL,
    icd10_code varchar(16),
    insurance_policy_number varchar(64),
    is_pregnant boolean NOT NULL,
    is_urgent_cito boolean NOT NULL,
    lab_department varchar(16),
    menstrual_phase varchar(32),
    mis_hospitalization_id uuid,
    mis_operation_id uuid,
    mis_referral_id uuid,
    mis_referral_number varchar(64),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_datetime timestamp without time zone NOT NULL,
    order_number varchar(32) NOT NULL,
    organization_id uuid,
    package_changed_by_doctor boolean NOT NULL,
    package_code varchar(64),
    package_name varchar(256),
    paid_amount numeric(18,2) NOT NULL,
    paper_referral_id uuid,
    pathology_kind varchar(16),
    patient_amount numeric(18,2) NOT NULL,
    patient_id uuid NOT NULL,
    patient_insurance_id uuid,
    payer_amount numeric(18,2) NOT NULL,
    payer_id uuid,
    planned_collection_at timestamp without time zone,
    pregnancy_week integer,
    record_state integer NOT NULL DEFAULT 2,
    referral_context varchar(16),
    referral_type varchar(16) NOT NULL,
    referrer_doctor_name varchar(256),
    referrer_number varchar(64),
    referrer_organization_edrpou varchar(16),
    referrer_organization_name varchar(256),
    released_at timestamp without time zone,
    released_by_id uuid,
    repeat_of_order_id uuid,
    requesting_department_name varchar(256),
    room varchar(32),
    sla_due_at timestamp without time zone,
    status varchar(32) NOT NULL,
    total_price numeric(18,2) NOT NULL,
    verify_token varchar(128),
    ward varchar(32),
    CONSTRAINT pk_lab_order PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_order_department_id ON lab_order (department_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_diagnostic_report_id ON lab_order (diagnostic_report_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_doctor_id ON lab_order (doctor_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_ehealth_referral_id ON lab_order (ehealth_referral_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_lab_department ON lab_order (lab_department);
CREATE INDEX IF NOT EXISTS ix_lab_order_mis_referral_id ON lab_order (mis_referral_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_order_datetime ON lab_order (order_datetime);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_order_order_number ON lab_order (order_number);
CREATE INDEX IF NOT EXISTS ix_lab_order_paper_referral_id ON lab_order (paper_referral_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_patient_id ON lab_order (patient_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_payer_id ON lab_order (payer_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_referral_type ON lab_order (referral_type);
CREATE INDEX IF NOT EXISTS ix_lab_order_status ON lab_order (status);
CREATE INDEX IF NOT EXISTS ix_lab_order_verify_token ON lab_order (verify_token);

-- [ЛІС] lab_order_attachment (LabOrderAttachment)
CREATE TABLE IF NOT EXISTS lab_order_attachment (
    id uuid NOT NULL,
    content bytea NOT NULL,
    content_type varchar(128) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    file_name varchar(256) NOT NULL,
    kind varchar(32) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    performer_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    send_out_id uuid,
    sha256 varchar(64) NOT NULL,
    size_bytes bigint NOT NULL,
    visible_to_patient boolean NOT NULL,
    CONSTRAINT pk_lab_order_attachment PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_order_attachment_order_id ON lab_order_attachment (order_id);

-- [ЛІС] lab_order_charge (LabOrderCharge)
CREATE TABLE IF NOT EXISTS lab_order_charge (
    id uuid NOT NULL,
    amount numeric(18,2) NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_not_covered boolean NOT NULL,
    is_patient_choice boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(512) NOT NULL,
    order_id uuid NOT NULL,
    package_id uuid,
    patient_amount numeric(18,2) NOT NULL,
    payer_amount numeric(18,2) NOT NULL,
    payer_id uuid NOT NULL,
    payer_kind varchar(16) NOT NULL,
    price_list_id uuid,
    profile_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    test_id uuid,
    CONSTRAINT pk_lab_order_charge PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_order_charge_order_id ON lab_order_charge (order_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_charge_payer_id ON lab_order_charge (payer_id);

-- [ЛІС] lab_order_favorite (LabOrderFavorite)
CREATE TABLE IF NOT EXISTS lab_order_favorite (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    display_order integer NOT NULL,
    employee_id uuid NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    profile_ids_json text NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    test_ids_json text NOT NULL,
    CONSTRAINT pk_lab_order_favorite PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_order_favorite_employee_id ON lab_order_favorite (employee_id);

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

-- [ЛІС] lab_order_sample (LabOrderSample)
CREATE TABLE IF NOT EXISTS lab_order_sample (
    id uuid NOT NULL,
    barcode varchar(32) NOT NULL,
    biomaterial_type_id integer NOT NULL,
    collect_checklist_json text,
    collected_at timestamp without time zone,
    collected_by_id uuid,
    container_type varchar(64),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    current_stage varchar(32),
    derivation_index integer NOT NULL,
    derivation_type varchar(16) NOT NULL,
    group_numb integer NOT NULL,
    is_clotted boolean NOT NULL,
    is_hemolyzed boolean NOT NULL,
    is_icteric boolean NOT NULL,
    is_insufficient_volume boolean NOT NULL,
    is_lipemic boolean NOT NULL,
    lab_section_id uuid,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    parent_sample_id uuid,
    plan_reasons varchar(128),
    received_at timestamp without time zone,
    received_by_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    reject_reason varchar(512),
    reject_reason_code varchar(64),
    status varchar(32) NOT NULL,
    tube_type_id integer NOT NULL,
    volume_ml double precision,
    CONSTRAINT pk_lab_order_sample PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_order_sample_barcode ON lab_order_sample (barcode);
CREATE INDEX IF NOT EXISTS ix_lab_order_sample_biomaterial_type_id ON lab_order_sample (biomaterial_type_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_sample_lab_section_id ON lab_order_sample (lab_section_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_sample_order_id ON lab_order_sample (order_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_sample_parent_sample_id ON lab_order_sample (parent_sample_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_sample_tube_type_id ON lab_order_sample (tube_type_id);

-- [ЛІС] lab_order_test (LabOrderTest)
CREATE TABLE IF NOT EXISTS lab_order_test (
    id uuid NOT NULL,
    assigned_analyzer_id uuid,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    dilution_factor double precision,
    display_order integer NOT NULL,
    distributed_at timestamp without time zone,
    distributed_by_id uuid,
    enqueued_on timestamp without time zone,
    is_reflex boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    order_item_id uuid,
    performer_id uuid,
    profile_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    reflex_from_test_id uuid,
    reflex_needs_confirmation boolean NOT NULL,
    reject_reason varchar(512),
    released_at timestamp without time zone,
    rerun_count integer NOT NULL,
    routed_by varchar(8),
    routing_status varchar(16),
    sample_id uuid,
    status varchar(32) NOT NULL,
    test_code varchar(64) NOT NULL,
    test_id uuid NOT NULL,
    test_name varchar(256) NOT NULL,
    CONSTRAINT pk_lab_order_test PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_order_id ON lab_order_test (order_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_performer_id ON lab_order_test (performer_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_profile_id ON lab_order_test (profile_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_released_at ON lab_order_test (released_at);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_sample_id ON lab_order_test (sample_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_status ON lab_order_test (status);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_test_code ON lab_order_test (test_code);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_test_id ON lab_order_test (test_id);
CREATE INDEX IF NOT EXISTS ix_lab_order_test_assigned_analyzer_id_status ON lab_order_test (assigned_analyzer_id, status);

-- [ЛІС] lab_panic_call (LabPanicCall)
CREATE TABLE IF NOT EXISTS lab_panic_call (
    id uuid NOT NULL,
    comments varchar(1024),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    department varchar(256),
    doctor_notified_name varchar(256) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    notified_at timestamp without time zone NOT NULL,
    notified_by_id uuid NOT NULL,
    order_id uuid NOT NULL,
    patient_name varchar(256) NOT NULL,
    phone varchar(32) NOT NULL,
    readback_confirmed boolean NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    result_id uuid NOT NULL,
    test_code varchar(64) NOT NULL,
    value varchar(64) NOT NULL,
    CONSTRAINT pk_lab_panic_call PRIMARY KEY (id)
);

-- [ЛІС] lab_path_block (LabPathBlock)
CREATE TABLE IF NOT EXISTS lab_path_block (
    id uuid NOT NULL,
    barcode varchar(32) NOT NULL,
    code varchar(16) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_external boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    object_id uuid NOT NULL,
    order_id uuid NOT NULL,
    pieces integer NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    room_id uuid,
    stage varchar(16),
    status varchar(16) NOT NULL,
    storage_location varchar(128),
    track varchar(16) NOT NULL,
    CONSTRAINT pk_lab_path_block PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_block_barcode ON lab_path_block (barcode);
CREATE INDEX IF NOT EXISTS ix_lab_path_block_object_id ON lab_path_block (object_id);
CREATE INDEX IF NOT EXISTS ix_lab_path_block_order_id ON lab_path_block (order_id);

-- [ЛІС] lab_path_cito_notice (LabPathCitoNotice)
CREATE TABLE IF NOT EXISTS lab_path_cito_notice (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    method varchar(32) NOT NULL,
    minutes_from_receipt integer NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    notified_by_id uuid,
    notified_person varchar(256) NOT NULL,
    order_id uuid NOT NULL,
    read_back boolean NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    text text NOT NULL,
    CONSTRAINT pk_lab_path_cito_notice PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_cito_notice_order_id ON lab_path_cito_notice (order_id);

-- [ЛІС] lab_path_cyto_item (LabPathCytoItem)
CREATE TABLE IF NOT EXISTS lab_path_cyto_item (
    id uuid NOT NULL,
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    is_high boolean NOT NULL,
    is_normal boolean NOT NULL,
    kind varchar(16) NOT NULL,
    materials varchar(256),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    sort_order integer NOT NULL,
    CONSTRAINT pk_lab_path_cyto_item PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_path_cyto_item_kind_code ON lab_path_cyto_item (kind, code);

-- [ЛІС] lab_path_dispatch (LabPathDispatch)
CREATE TABLE IF NOT EXISTS lab_path_dispatch (
    id uuid NOT NULL,
    address varchar(256) NOT NULL,
    channel varchar(16) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    error varchar(512),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    report_id uuid NOT NULL,
    requested_by_id uuid,
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_path_dispatch PRIMARY KEY (id)
);

-- [ЛІС] lab_path_ihc_result (LabPathIhcResult)
CREATE TABLE IF NOT EXISTS lab_path_ihc_result (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    marker varchar(32) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    report_id uuid NOT NULL,
    result_text varchar(256),
    score varchar(32),
    slide_id uuid,
    unit varchar(16),
    CONSTRAINT pk_lab_path_ihc_result PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_ihc_result_report_id ON lab_path_ihc_result (report_id);

-- [ЛІС] lab_path_issue (LabPathIssue)
CREATE TABLE IF NOT EXISTS lab_path_issue (
    id uuid NOT NULL,
    block_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    document varchar(128),
    due_back timestamp without time zone,
    issued_at timestamp without time zone NOT NULL,
    issued_by_id uuid,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    purpose varchar(128) NOT NULL,
    recipient varchar(256) NOT NULL,
    recipient_unit varchar(256),
    record_state integer NOT NULL DEFAULT 2,
    returned_at timestamp without time zone,
    scope varchar(16) NOT NULL,
    CONSTRAINT pk_lab_path_issue PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_issue_block_id_returned_at ON lab_path_issue (block_id, returned_at);

-- [ЛІС] lab_path_material_type (LabPathMaterialType)
CREATE TABLE IF NOT EXISTS lab_path_material_type (
    id uuid NOT NULL,
    base_price numeric(18,2) NOT NULL,
    code varchar(16) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    service_group varchar(32),
    sla_days integer NOT NULL,
    CONSTRAINT pk_lab_path_material_type PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_path_material_type_code ON lab_path_material_type (code);

-- [ЛІС] lab_path_object (LabPathObject)
CREATE TABLE IF NOT EXISTS lab_path_object (
    id uuid NOT NULL,
    accepted boolean NOT NULL,
    barcode varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    description varchar(512),
    display_order integer NOT NULL,
    fixation_start_at timestamp without time zone,
    fixative varchar(64),
    fragments integer NOT NULL,
    is_external boolean NOT NULL,
    is_labeled boolean NOT NULL,
    letter varchar(4) NOT NULL,
    localization varchar(256),
    mark varchar(32) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    reject_reason varchar(256),
    remainder_note varchar(256),
    remainder_state varchar(16) NOT NULL,
    taken_at timestamp without time zone,
    CONSTRAINT pk_lab_path_object PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_object_barcode ON lab_path_object (barcode);
CREATE INDEX IF NOT EXISTS ix_lab_path_object_order_id ON lab_path_object (order_id);

-- [ЛІС] lab_path_price (LabPathPrice)
CREATE TABLE IF NOT EXISTS lab_path_price (
    id uuid NOT NULL,
    amount numeric(18,2) NOT NULL,
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_path_price PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_path_price_code ON lab_path_price (code);

-- [ЛІС] lab_path_referral (LabPathReferral)
CREATE TABLE IF NOT EXISTS lab_path_referral (
    id uuid NOT NULL,
    accepted_at timestamp without time zone,
    accepted_by_id uuid,
    assistant_id uuid,
    assistant_name varchar(256),
    biopsy_kind varchar(16) NOT NULL,
    cito_notified_at timestamp without time zone,
    clinical_data text,
    clinical_dx_icd10 varchar(256),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    cycle_day integer,
    cyto_material varchar(16),
    external_blocks integer NOT NULL,
    external_date timestamp without time zone,
    external_institution varchar(256),
    external_number varchar(64),
    external_slides integer NOT NULL,
    form014_json text,
    gross_by_id uuid,
    gross_finished_at timestamp without time zone,
    intake_status varchar(16) NOT NULL,
    last_menstrual_date timestamp without time zone,
    macro text,
    marking_text varchar(1024),
    medical_card_no varchar(64),
    menopause_state varchar(16),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    operation_text varchar(1024),
    order_id uuid NOT NULL,
    pathologist_id uuid,
    pathologist_name varchar(256),
    prepared_at timestamp without time zone,
    price numeric(18,2) NOT NULL,
    primary_case_no varchar(64),
    record_state integer NOT NULL DEFAULT 2,
    referring_department varchar(256),
    referring_doctor varchar(256),
    reject_comment varchar(1024),
    reject_reason varchar(256),
    result_email varchar(256),
    screen_assessment varchar(16),
    screen_note varchar(512),
    screened_at timestamp without time zone,
    screener_id uuid,
    screener_name varchar(256),
    stage varchar(16) NOT NULL,
    template_name varchar(128),
    CONSTRAINT pk_lab_path_referral PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_referral_intake_status ON lab_path_referral (intake_status);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_path_referral_order_id ON lab_path_referral (order_id);

-- [ЛІС] lab_path_reject_reason (LabPathRejectReason)
CREATE TABLE IF NOT EXISTS lab_path_reject_reason (
    id uuid NOT NULL,
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_path_reject_reason PRIMARY KEY (id)
);

-- [ЛІС] lab_path_report (LabPathReport)
CREATE TABLE IF NOT EXISTS lab_path_report (
    id uuid NOT NULL,
    addendum_type varchar(16),
    adequacy varchar(16),
    changes_diagnosis boolean NOT NULL,
    conclusion_text text,
    consultant_name varchar(256),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    cyto_category varchar(16),
    grade varchar(8),
    icd10 varchar(16),
    icdo_morph varchar(16),
    icdo_topo varchar(16),
    is_preliminary_sent boolean NOT NULL,
    kind varchar(16) NOT NULL,
    ln_positive integer,
    ln_total integer,
    lvi boolean,
    macro text,
    margin varchar(64),
    micro text,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    pn varchar(16),
    pni boolean,
    pt varchar(16),
    reason varchar(1024),
    recommendation varchar(1024),
    record_state integer NOT NULL DEFAULT 2,
    signature_ref varchar(128),
    signed_at timestamp without time zone,
    signed_by varchar(64),
    signed_by_name varchar(256),
    version integer NOT NULL,
    CONSTRAINT pk_lab_path_report PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_report_order_id_version ON lab_path_report (order_id, version);

-- [ЛІС] lab_path_slide (LabPathSlide)
CREATE TABLE IF NOT EXISTS lab_path_slide (
    id uuid NOT NULL,
    antibody_lot varchar(64),
    barcode varchar(32) NOT NULL,
    block_id uuid NOT NULL,
    code varchar(24) NOT NULL,
    covered_at timestamp without time zone,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    dispose_reason varchar(256),
    disposed_at timestamp without time zone,
    is_external boolean NOT NULL,
    is_ihc boolean NOT NULL,
    marker varchar(32),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    ordered_from varchar(8),
    record_state integer NOT NULL DEFAULT 2,
    replaces_slide_id uuid,
    stain_method varchar(128) NOT NULL,
    status varchar(16) NOT NULL,
    wsi_url varchar(512),
    CONSTRAINT pk_lab_path_slide PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_slide_barcode ON lab_path_slide (barcode);
CREATE INDEX IF NOT EXISTS ix_lab_path_slide_block_id ON lab_path_slide (block_id);
CREATE INDEX IF NOT EXISTS ix_lab_path_slide_order_id ON lab_path_slide (order_id);

-- [ЛІС] lab_path_stage_log (LabPathStageLog)
CREATE TABLE IF NOT EXISTS lab_path_stage_log (
    id uuid NOT NULL,
    block_id uuid,
    comment varchar(512),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    device varchar(128),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    performed_at timestamp without time zone NOT NULL,
    performed_by varchar(64),
    performed_by_name varchar(256),
    record_state integer NOT NULL DEFAULT 2,
    slide_id uuid,
    stage varchar(16) NOT NULL,
    CONSTRAINT pk_lab_path_stage_log PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_stage_log_order_id_performed_at ON lab_path_stage_log (order_id, performed_at);

-- [ЛІС] lab_path_storage_room (LabPathStorageRoom)
CREATE TABLE IF NOT EXISTS lab_path_storage_room (
    id uuid NOT NULL,
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_path_storage_room PRIMARY KEY (id)
);

-- [ЛІС] lab_path_template (LabPathTemplate)
CREATE TABLE IF NOT EXISTS lab_path_template (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    macro text,
    micro text,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_path_template PRIMARY KEY (id)
);

-- [ЛІС] lab_patient_consent (LabPatientConsent)
CREATE TABLE IF NOT EXISTS lab_patient_consent (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    granted_at timestamp without time zone,
    is_granted boolean NOT NULL,
    kind varchar(32) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    note varchar(512),
    patient_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    recorded_by_id uuid,
    revoked_at timestamp without time zone,
    source varchar(16) NOT NULL,
    CONSTRAINT pk_lab_patient_consent PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_patient_consent_patient_id_kind ON lab_patient_consent (patient_id, kind);

-- [ЛІС] lab_patient_notification (LabPatientNotification)
CREATE TABLE IF NOT EXISTS lab_patient_notification (
    id uuid NOT NULL,
    attempts integer NOT NULL,
    channel varchar(16) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    idempotency_key varchar(128),
    last_error varchar(1024),
    locked_by varchar(96),
    locked_until timestamp without time zone,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    next_attempt_at timestamp without time zone,
    order_id uuid,
    patient_id uuid NOT NULL,
    payload text NOT NULL,
    provider_message_id varchar(128),
    reason_code varchar(32),
    recipient_masked varchar(128),
    record_state integer NOT NULL DEFAULT 2,
    sent_at timestamp without time zone,
    status varchar(16) NOT NULL,
    template_code varchar(32),
    updated_at timestamp without time zone,
    CONSTRAINT pk_lab_patient_notification PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_patient_notification_idempotency_key ON lab_patient_notification (idempotency_key);
CREATE INDEX IF NOT EXISTS ix_lab_patient_notification_status_next_attempt_at ON lab_patient_notification (status, next_attempt_at);

-- [ЛІС] lab_payer (LabPayer)
CREATE TABLE IF NOT EXISTS lab_payer (
    id uuid NOT NULL,
    code varchar(32) NOT NULL,
    contact_person varchar(256),
    contract_date timestamp without time zone,
    contract_number varchar(64),
    contract_valid_to timestamp without time zone,
    coverage_pct numeric(18,2) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    edrpou varchar(16),
    email varchar(128),
    franchise_amount numeric(18,2) NOT NULL,
    is_active boolean NOT NULL,
    kind varchar(16) NOT NULL,
    medical_program_id uuid,
    medlink_contract_id uuid,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    organization_name varchar(256),
    phone varchar(64),
    price_list_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    requires_authorization boolean NOT NULL,
    requires_policy boolean NOT NULL,
    CONSTRAINT pk_lab_payer PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_payer_code ON lab_payer (code);
CREATE INDEX IF NOT EXISTS ix_lab_payer_price_list_id ON lab_payer (price_list_id);

-- [ЛІС] lab_payment (LabPayment)
CREATE TABLE IF NOT EXISTS lab_payment (
    id uuid NOT NULL,
    amount numeric(18,2) NOT NULL,
    cashier_id uuid,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    method varchar(16) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    note varchar(512),
    order_id uuid NOT NULL,
    paid_at timestamp without time zone NOT NULL,
    receipt_number varchar(32) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_payment PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_payment_order_id ON lab_payment (order_id);

-- [ЛІС] lab_performer (LabPerformer)
CREATE TABLE IF NOT EXISTS lab_performer (
    id uuid NOT NULL,
    address varchar(512),
    code varchar(32) NOT NULL,
    contract_date timestamp without time zone,
    contract_number varchar(128),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    default_tat_hours integer NOT NULL,
    department_id uuid,
    edrpou varchar(16),
    email varchar(128),
    exchange_mode varchar(16) NOT NULL,
    is_active boolean NOT NULL,
    kind varchar(16) NOT NULL,
    license_number varchar(128),
    medlink_provider varchar(32),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    phone varchar(64),
    record_state integer NOT NULL DEFAULT 2,
    report_note varchar(512),
    CONSTRAINT pk_lab_performer PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_performer_code ON lab_performer (code);

-- [ЛІС] lab_performer_test (LabPerformerTest)
CREATE TABLE IF NOT EXISTS lab_performer_test (
    id uuid NOT NULL,
    cost numeric(18,2),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    external_code varchar(64),
    external_name varchar(256),
    is_active boolean NOT NULL,
    is_default_route boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    performer_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    specimen_requirements varchar(512),
    tat_hours integer,
    test_id uuid NOT NULL,
    CONSTRAINT pk_lab_performer_test PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_performer_test_test_id ON lab_performer_test (test_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_performer_test_performer_id_test_id ON lab_performer_test (performer_id, test_id);

-- [ЛІС] lab_price_list (LabPriceList)
CREATE TABLE IF NOT EXISTS lab_price_list (
    id uuid NOT NULL,
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    currency varchar(3) NOT NULL,
    is_active boolean NOT NULL,
    is_default boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    valid_from timestamp without time zone,
    valid_to timestamp without time zone,
    CONSTRAINT pk_lab_price_list PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_price_list_code ON lab_price_list (code);

-- [ЛІС] lab_price_list_item (LabPriceListItem)
CREATE TABLE IF NOT EXISTS lab_price_list_item (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    price numeric(18,2) NOT NULL,
    price_list_id uuid NOT NULL,
    profile_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    test_id uuid,
    CONSTRAINT pk_lab_price_list_item PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_price_list_item_price_list_id_profile_id_test_id ON lab_price_list_item (price_list_id, profile_id, test_id);

-- [ЛІС] lab_price_package (LabPricePackage)
CREATE TABLE IF NOT EXISTS lab_price_package (
    id uuid NOT NULL,
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    member_ids_json text NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    price numeric(18,2) NOT NULL,
    price_list_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_price_package PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_price_package_price_list_id ON lab_price_package (price_list_id);

-- [ЛІС] lab_qc_material (LabQcMaterial)
CREATE TABLE IF NOT EXISTS lab_qc_material (
    id uuid NOT NULL,
    analyzer_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    expiry_date timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    level varchar(16) NOT NULL,
    lot_number varchar(64) NOT NULL,
    manufacturer varchar(128),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    open_stability_days integer NOT NULL,
    opened_at timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_qc_material PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_qc_material_analyzer_id ON lab_qc_material (analyzer_id);

-- [ЛІС] lab_qc_result (LabQcResult)
CREATE TABLE IF NOT EXISTS lab_qc_result (
    id uuid NOT NULL,
    analyzer_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_rejection boolean NOT NULL,
    is_warning boolean NOT NULL,
    lockout_enforced boolean NOT NULL,
    lockout_id uuid,
    measured_value double precision NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    operator_id varchar(64),
    qc_material_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    resolution_action varchar(512),
    resolved_at timestamp without time zone,
    resolved_by_id uuid,
    run_at timestamp without time zone NOT NULL,
    target_mean double precision NOT NULL,
    target_sd double precision NOT NULL,
    test_code varchar(64) NOT NULL,
    violated_rules_json text NOT NULL,
    z_score double precision NOT NULL,
    CONSTRAINT pk_lab_qc_result PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_qc_result_qc_material_id_test_code_run_at ON lab_qc_result (qc_material_id, test_code, run_at);

-- [ЛІС] lab_qc_target (LabQcTarget)
CREATE TABLE IF NOT EXISTS lab_qc_target (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    qc_material_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    target_mean double precision NOT NULL,
    target_sd double precision NOT NULL,
    tea_pct double precision,
    test_code varchar(64) NOT NULL,
    unit varchar(64),
    CONSTRAINT pk_lab_qc_target PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_qc_target_qc_material_id ON lab_qc_target (qc_material_id);

-- [ЛІС] lab_reagent_consumption (LabReagentConsumption)
CREATE TABLE IF NOT EXISTS lab_reagent_consumption (
    id uuid NOT NULL,
    actor_id varchar(64),
    analyzer_id uuid,
    at timestamp without time zone NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    kind varchar(16) NOT NULL,
    lot_id uuid NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_test_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    result_id uuid,
    sample_id uuid,
    shortfall integer NOT NULL,
    test_code varchar(64),
    units integer NOT NULL,
    warning varchar(512),
    CONSTRAINT pk_lab_reagent_consumption PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_reagent_consumption_lot_id ON lab_reagent_consumption (lot_id);

-- [ЛІС] lab_reagent_item (LabReagentItem)
CREATE TABLE IF NOT EXISTS lab_reagent_item (
    id uuid NOT NULL,
    catalog_number varchar(64),
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    manufacturer varchar(128),
    minimum_tests integer NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    storage varchar(64),
    test_code varchar(64),
    unit varchar(32),
    CONSTRAINT pk_lab_reagent_item PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_reagent_item_code ON lab_reagent_item (code);

-- [ЛІС] lab_reagent_lot (LabReagentLot)
CREATE TABLE IF NOT EXISTS lab_reagent_lot (
    id uuid NOT NULL,
    analyzer_id uuid,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    expiry_date timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    lot_number varchar(64) NOT NULL,
    manufacturer varchar(128),
    minimum_tests integer NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    onboard_stability_days integer,
    opened_at timestamp without time zone,
    reagent_item_id uuid,
    reagent_name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    test_code varchar(64),
    tests_initial integer NOT NULL,
    tests_remaining integer NOT NULL,
    units_per_test integer NOT NULL,
    CONSTRAINT pk_lab_reagent_lot PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_reagent_lot_analyzer_id ON lab_reagent_lot (analyzer_id);

-- [ЛІС] lab_reference_layer (LabReferenceLayer)
CREATE TABLE IF NOT EXISTS lab_reference_layer (
    id uuid NOT NULL,
    age_from double precision NOT NULL,
    age_group_id integer,
    age_to double precision NOT NULL,
    age_unit varchar(8) NOT NULL,
    analyzer_code varchar(64),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    crit_high double precision,
    crit_low double precision,
    delta_check_max_pct double precision,
    dilution double precision,
    gender varchar(8) NOT NULL,
    icd10_code varchar(16),
    is_active boolean NOT NULL,
    is_age boolean NOT NULL,
    is_gender boolean NOT NULL,
    is_menstrual_phase boolean NOT NULL,
    is_pregnancy boolean NOT NULL,
    layer_type varchar(32) NOT NULL,
    menstrual_phase varchar(32),
    method_code varchar(64),
    method_name varchar(256),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    norm_high double precision,
    norm_low double precision,
    norm_name varchar(256) NOT NULL,
    norm_text text,
    pregnancy_week_from integer,
    pregnancy_week_to integer,
    priority_order integer NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    source varchar(256),
    test_code varchar(64) NOT NULL,
    test_id uuid,
    unit varchar(64),
    unit_id integer,
    CONSTRAINT pk_lab_reference_layer PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_reference_layer_test_code_method_code_priority_order ON lab_reference_layer (test_code, method_code, priority_order);

-- [ЛІС] lab_reflex_rule (LabReflexRule)
CREATE TABLE IF NOT EXISTS lab_reflex_rule (
    id uuid NOT NULL,
    auto_approve boolean NOT NULL,
    condition_operator varchar(16) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    description varchar(512),
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    reflex_test_code varchar(64) NOT NULL,
    requires_same_sample boolean NOT NULL,
    threshold_value double precision,
    trigger_test_code varchar(64) NOT NULL,
    CONSTRAINT pk_lab_reflex_rule PRIMARY KEY (id)
);

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

-- [ЛІС] lab_report_archive (LabReportArchive)
CREATE TABLE IF NOT EXISTS lab_report_archive (
    id uuid NOT NULL,
    amend_reason varchar(512),
    amends_version_no integer,
    content_hash varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    duplicates_printed integer NOT NULL,
    html text NOT NULL,
    issued_by_id uuid,
    kind varchar(16) NOT NULL,
    language varchar(8) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    pdf bytea,
    record_state integer NOT NULL DEFAULT 2,
    variant varchar(16) NOT NULL,
    version_no integer NOT NULL,
    CONSTRAINT pk_lab_report_archive PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_report_archive_order_id_version_no ON lab_report_archive (order_id, version_no);

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

-- [ЛІС] lab_report_template (LabReportTemplate)
CREATE TABLE IF NOT EXISTS lab_report_template (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    footer_text text,
    header_note varchar(512),
    is_active boolean NOT NULL,
    languages varchar(32) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    note varchar(512),
    record_state integer NOT NULL DEFAULT 2,
    title varchar(256),
    variant varchar(16) NOT NULL,
    version integer NOT NULL,
    CONSTRAINT pk_lab_report_template PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_report_template_variant_version ON lab_report_template (variant, version);

-- [ЛІС] lab_result_attempt (LabResultAttempt)
CREATE TABLE IF NOT EXISTS lab_result_attempt (
    id uuid NOT NULL,
    accepted_at timestamp without time zone,
    accepted_by_id uuid,
    analyzer_id uuid,
    attempt_no integer NOT NULL,
    comment varchar(1024),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    dilution_factor double precision,
    entered_at timestamp without time zone NOT NULL,
    entered_by_id uuid,
    flag varchar(16) NOT NULL,
    is_accepted boolean NOT NULL,
    kind varchar(16) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    numeric_value double precision,
    order_test_id uuid NOT NULL,
    out_of_linearity boolean NOT NULL,
    raw_value double precision,
    reagent_lot_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    string_value varchar(512),
    superseded_by_rerun boolean NOT NULL,
    unit varchar(64),
    CONSTRAINT pk_lab_result_attempt PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_result_attempt_order_test_id ON lab_result_attempt (order_test_id);

-- [ЛІС] lab_result_history (LabResultHistory)
CREATE TABLE IF NOT EXISTS lab_result_history (
    id uuid NOT NULL,
    action varchar(32) NOT NULL,
    actor_id varchar(64),
    at timestamp without time zone NOT NULL,
    comment varchar(1024),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    flag varchar(16),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    numeric_value double precision,
    order_test_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    result_id uuid NOT NULL,
    snapshot_json text,
    string_value varchar(512),
    version integer NOT NULL,
    CONSTRAINT pk_lab_result_history PRIMARY KEY (id)
);

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

-- [ЛІС] lab_sample_logistics (LabSampleLogistics)
CREATE TABLE IF NOT EXISTS lab_sample_logistics (
    id uuid NOT NULL,
    courier_name varchar(128),
    courier_phone varchar(32),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    destination_department_id uuid NOT NULL,
    dispatched_at timestamp without time zone,
    dispatched_by_id uuid,
    is_cold_chain_violated boolean NOT NULL,
    manifest_number varchar(64) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    notes text,
    origin_department_id uuid NOT NULL,
    received_at timestamp without time zone,
    received_by_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    status varchar(16) NOT NULL,
    temperature_dispatch double precision,
    temperature_receipt double precision,
    CONSTRAINT pk_lab_sample_logistics PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_sample_logistics_destination_department_id ON lab_sample_logistics (destination_department_id);
CREATE INDEX IF NOT EXISTS ix_lab_sample_logistics_origin_department_id ON lab_sample_logistics (origin_department_id);

-- [ЛІС] lab_sample_logistics_item (LabSampleLogisticsItem)
CREATE TABLE IF NOT EXISTS lab_sample_logistics_item (
    id uuid NOT NULL,
    barcode varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    logistics_id uuid NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    sample_id uuid NOT NULL,
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_sample_logistics_item PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_sample_logistics_item_logistics_id ON lab_sample_logistics_item (logistics_id);
CREATE INDEX IF NOT EXISTS ix_lab_sample_logistics_item_sample_id ON lab_sample_logistics_item (sample_id);

-- [ЛІС] lab_sample_stage_event (LabSampleStageEvent)
CREATE TABLE IF NOT EXISTS lab_sample_stage_event (
    id uuid NOT NULL,
    at timestamp without time zone NOT NULL,
    by_id varchar(64),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    data_json text,
    instrument_id varchar(64),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    note varchar(1024),
    record_state integer NOT NULL DEFAULT 2,
    sample_id uuid NOT NULL,
    stage_code varchar(32) NOT NULL,
    temperature double precision,
    CONSTRAINT pk_lab_sample_stage_event PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_sample_stage_event_sample_id_at ON lab_sample_stage_event (sample_id, at);

-- [ЛІС] lab_section (LabSection)
CREATE TABLE IF NOT EXISTS lab_section (
    id uuid NOT NULL,
    auto_release_verified boolean NOT NULL,
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    department_id uuid,
    is_active boolean NOT NULL,
    journal_mask varchar(64) NOT NULL,
    journal_reset_period varchar(8) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    secondary_mask varchar(64),
    section_type varchar(32) NOT NULL,
    workflow_template_code varchar(32) NOT NULL,
    CONSTRAINT pk_lab_section PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_section_code ON lab_section (code);
CREATE INDEX IF NOT EXISTS ix_lab_section_department_id ON lab_section (department_id);

-- [ЛІС] lab_section_journal_entry (LabSectionJournalEntry)
CREATE TABLE IF NOT EXISTS lab_section_journal_entry (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    day_number integer NOT NULL,
    journal_number varchar(64) NOT NULL,
    lab_section_id uuid NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    order_test_ids_json text NOT NULL,
    period_key varchar(16) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    registered_at timestamp without time zone NOT NULL,
    registered_by_id uuid,
    sample_id uuid,
    secondary_number varchar(64),
    sequence_value bigint NOT NULL,
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_section_journal_entry PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_section_journal_entry_sample_id ON lab_section_journal_entry (sample_id);
CREATE INDEX IF NOT EXISTS ix_lab_section_journal_entry_lab_section_id_registered_at ON lab_section_journal_entry (lab_section_id, registered_at);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_section_journal_entry_order_id_lab_section_id_sample_id ON lab_section_journal_entry (order_id, lab_section_id, sample_id);

-- [ЛІС] lab_seed_marker (LabSeedMarker)
CREATE TABLE IF NOT EXISTS lab_seed_marker (
    code uuid NOT NULL,
    applied_on timestamp without time zone NOT NULL,
    note varchar(256),
    CONSTRAINT pk_lab_seed_marker PRIMARY KEY (code)
);

-- [ЛІС] lab_send_out (LabSendOut)
CREATE TABLE IF NOT EXISTS lab_send_out (
    id uuid NOT NULL,
    accepted_at timestamp without time zone,
    completed_at timestamp without time zone,
    courier_name varchar(128),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    dispatched_at timestamp without time zone,
    dispatched_by_id uuid,
    external_batch_number varchar(64),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    notes text,
    number varchar(32) NOT NULL,
    performer_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    status varchar(16) NOT NULL,
    temperature_dispatch double precision,
    CONSTRAINT pk_lab_send_out PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_send_out_number ON lab_send_out (number);
CREATE INDEX IF NOT EXISTS ix_lab_send_out_performer_id ON lab_send_out (performer_id);

-- [ЛІС] lab_send_out_item (LabSendOutItem)
CREATE TABLE IF NOT EXISTS lab_send_out_item (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    due_at timestamp without time zone,
    external_code varchar(64),
    external_order_number varchar(64),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_test_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    reject_reason varchar(512),
    result_received_at timestamp without time zone,
    sample_id uuid,
    send_out_id uuid NOT NULL,
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_send_out_item PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_send_out_item_order_test_id ON lab_send_out_item (order_test_id);
CREATE INDEX IF NOT EXISTS ix_lab_send_out_item_sample_id ON lab_send_out_item (sample_id);
CREATE INDEX IF NOT EXISTS ix_lab_send_out_item_send_out_id ON lab_send_out_item (send_out_id);

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

-- [ЛІС] lab_settings (LabSettings)
CREATE TABLE IF NOT EXISTS lab_settings (
    id uuid NOT NULL,
    address varchar(512),
    auto_reroute_offline boolean NOT NULL,
    barcode_prefix varchar(16),
    block_on_expired_calibration boolean NOT NULL,
    calibration_warn_days integer NOT NULL,
    cold_chain_max_c double precision NOT NULL,
    cold_chain_min_c double precision NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    director_name varchar(256),
    distribution_target_minutes integer NOT NULL,
    edrpou varchar(16),
    ehealth_session_idle_minutes integer NOT NULL,
    ehealth_session_ttl_minutes integer NOT NULL,
    ehealth_submit_inpatient boolean NOT NULL,
    email varchar(128),
    intervention_categories varchar(256) NOT NULL,
    intervention_sync_cron varchar(64) NOT NULL,
    label_printer_host varchar(128),
    label_printer_name varchar(128),
    label_printer_port integer NOT NULL,
    license_number varchar(128),
    logo_base64 text,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    order_number_mask varchar(64) NOT NULL,
    panic_phone varchar(32),
    pathology_cito_target_minutes integer NOT NULL,
    pathology_fixation_max_hours integer NOT NULL,
    pathology_fixation_min_hours integer NOT NULL,
    pathology_mirror_to_simplex_cabinet boolean NOT NULL,
    phone varchar(32),
    record_state integer NOT NULL DEFAULT 2,
    report_footer text,
    report_language varchar(8) NOT NULL,
    report_templates_json text NOT NULL,
    require_distribution_confirmation boolean NOT NULL,
    reroute_on_offline_minutes integer NOT NULL,
    routing_strategy varchar(24) NOT NULL,
    working_hours varchar(128),
    CONSTRAINT pk_lab_settings PRIMARY KEY (id)
);

-- [ЛІС] lab_sop (LabSop)
CREATE TABLE IF NOT EXISTS lab_sop (
    id uuid NOT NULL,
    approved_by_id uuid,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    description varchar(2000),
    document_url varchar(512),
    effective_from timestamp without time zone,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    review_due_at timestamp without time zone,
    status varchar(16) NOT NULL,
    target_code varchar(64),
    target_kind varchar(16),
    title varchar(256) NOT NULL,
    version varchar(32) NOT NULL,
    CONSTRAINT pk_lab_sop PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_sop_code_version ON lab_sop (code, version);

-- [ЛІС] lab_staff_competency (LabStaffCompetency)
CREATE TABLE IF NOT EXISTS lab_staff_competency (
    id uuid NOT NULL,
    assessed_at timestamp without time zone NOT NULL,
    assessor_id uuid,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    employee_id uuid NOT NULL,
    is_active boolean NOT NULL,
    kind varchar(16) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    note varchar(512),
    record_state integer NOT NULL DEFAULT 2,
    target_code varchar(64) NOT NULL,
    valid_until timestamp without time zone,
    CONSTRAINT pk_lab_staff_competency PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_staff_competency_employee_id_kind_target_code ON lab_staff_competency (employee_id, kind, target_code);

-- [ЛІС] lab_stain_method (LabStainMethod)
CREATE TABLE IF NOT EXISTS lab_stain_method (
    id uuid NOT NULL,
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    is_ihc boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    price numeric(18,2) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_stain_method PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_stain_method_code ON lab_stain_method (code);

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

-- [ЛІС] lab_susceptibility_result (LabSusceptibilityResult)
CREATE TABLE IF NOT EXISTS lab_susceptibility_result (
    id uuid NOT NULL,
    antibiotic_id integer NOT NULL,
    breakpoint_id uuid,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    interpretation varchar(4) NOT NULL,
    is_intrinsic_resistance boolean NOT NULL,
    isolate_id uuid NOT NULL,
    method varchar(32) NOT NULL,
    mic_value_mg_l double precision,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    overridden_by_id uuid,
    override_reason varchar(512),
    record_state integer NOT NULL DEFAULT 2,
    zone_diameter_mm double precision,
    CONSTRAINT pk_lab_susceptibility_result PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_susceptibility_result_antibiotic_id ON lab_susceptibility_result (antibiotic_id);
CREATE INDEX IF NOT EXISTS ix_lab_susceptibility_result_isolate_id ON lab_susceptibility_result (isolate_id);

-- [ЛІС] lab_test_definition (LabTestDefinition)
CREATE TABLE IF NOT EXISTS lab_test_definition (
    id uuid NOT NULL,
    biomaterial_type_id integer NOT NULL,
    category varchar(64) NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    decimal_places integer NOT NULL,
    delta_check_hours integer NOT NULL,
    delta_check_max_pct double precision,
    dropdown_options_json text,
    formula text,
    is_active boolean NOT NULL,
    is_qc_tracked boolean NOT NULL,
    lab_section_id uuid,
    loinc_code varchar(32),
    max_tests_per_tube integer,
    method_code varchar(64),
    method_id integer,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    name_en varchar(256),
    price numeric(18,2) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    required_volume_ml double precision,
    requires_manual_verification boolean NOT NULL,
    requires_separate_tube boolean NOT NULL,
    result_type varchar(16) NOT NULL,
    short_name varchar(64),
    tube_compatibility_group varchar(32),
    tube_type_id integer,
    unit varchar(64) NOT NULL,
    unit_id integer,
    CONSTRAINT pk_lab_test_definition PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_test_definition_biomaterial_type_id ON lab_test_definition (biomaterial_type_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_test_definition_code ON lab_test_definition (code);
CREATE INDEX IF NOT EXISTS ix_lab_test_definition_lab_section_id ON lab_test_definition (lab_section_id);
CREATE INDEX IF NOT EXISTS ix_lab_test_definition_method_id ON lab_test_definition (method_id);
CREATE INDEX IF NOT EXISTS ix_lab_test_definition_tube_type_id ON lab_test_definition (tube_type_id);

-- [ЛІС] lab_test_profile (LabTestProfile)
CREATE TABLE IF NOT EXISTS lab_test_profile (
    id uuid NOT NULL,
    category varchar(128) NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    default_biomaterial_type_id integer,
    default_tube_type_id integer,
    ehealth_service_catalog_service_id uuid,
    fasting_required boolean NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(512) NOT NULL,
    name_en varchar(512),
    organization_service_id uuid,
    price numeric(18,2) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    turnaround_hours integer NOT NULL,
    CONSTRAINT pk_lab_test_profile PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_test_profile_code ON lab_test_profile (code);
CREATE INDEX IF NOT EXISTS ix_lab_test_profile_ehealth_service_catalog_service_id ON lab_test_profile (ehealth_service_catalog_service_id);
CREATE INDEX IF NOT EXISTS ix_lab_test_profile_organization_service_id ON lab_test_profile (organization_service_id);

-- [ЛІС] lab_test_profile_item (LabTestProfileItem)
CREATE TABLE IF NOT EXISTS lab_test_profile_item (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    display_order integer NOT NULL,
    is_default_selected boolean NOT NULL,
    is_required boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    profile_id uuid NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    test_id uuid NOT NULL,
    CONSTRAINT pk_lab_test_profile_item PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_test_profile_item_profile_id ON lab_test_profile_item (profile_id);
CREATE INDEX IF NOT EXISTS ix_lab_test_profile_item_test_id ON lab_test_profile_item (test_id);

-- [ЛІС] lab_test_result (LabTestResult)
CREATE TABLE IF NOT EXISTS lab_test_result (
    id uuid NOT NULL,
    analyzer_flags varchar(64),
    analyzer_id uuid,
    applied_layer_id uuid,
    applied_layer_name varchar(256),
    auto_verify_block_reason varchar(512),
    calc_formula varchar(512),
    calc_inputs_json text,
    calibration_id uuid,
    calibration_warning varchar(512),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    crit_high double precision,
    crit_low double precision,
    delta_alert boolean NOT NULL,
    delta_percent double precision,
    dilution_factor double precision,
    entered_at timestamp without time zone NOT NULL,
    entered_by_id uuid,
    external_reference varchar(128),
    flag varchar(16) NOT NULL,
    is_auto_verified boolean NOT NULL,
    is_calculated boolean NOT NULL,
    measured_at timestamp without time zone,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    norm_high double precision,
    norm_low double precision,
    numeric_value double precision,
    operator_comment varchar(1024),
    order_test_id uuid NOT NULL,
    out_of_linearity boolean NOT NULL,
    performer_id uuid,
    previous_at timestamp without time zone,
    previous_value double precision,
    raw_message_id uuid,
    raw_value double precision,
    reagent_lot_id uuid,
    reagent_lot_number varchar(64),
    record_state integer NOT NULL DEFAULT 2,
    reference_display varchar(256) NOT NULL,
    report_text text,
    string_value varchar(512),
    unit varchar(64),
    verification_comment varchar(1024),
    verified_at timestamp without time zone,
    verified_by_id uuid,
    version integer NOT NULL,
    CONSTRAINT pk_lab_test_result PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_test_result_analyzer_id ON lab_test_result (analyzer_id);
CREATE INDEX IF NOT EXISTS ix_lab_test_result_entered_at ON lab_test_result (entered_at);
CREATE INDEX IF NOT EXISTS ix_lab_test_result_flag ON lab_test_result (flag);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_test_result_order_test_id ON lab_test_result (order_test_id);

-- [ЛІС] lab_test_synonym (LabTestSynonym)
CREATE TABLE IF NOT EXISTS lab_test_synonym (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    language varchar(8) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    synonym varchar(256) NOT NULL,
    test_code varchar(64) NOT NULL,
    CONSTRAINT pk_lab_test_synonym PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_test_synonym_test_code_synonym ON lab_test_synonym (test_code, synonym);

-- [ЛІС] lab_transport_container (LabTransportContainer)
CREATE TABLE IF NOT EXISTS lab_transport_container (
    id uuid NOT NULL,
    barcode varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    damaged_count integer NOT NULL,
    destination_department_id uuid NOT NULL,
    dispatched_at timestamp without time zone,
    dispatched_by_id uuid,
    extra_barcodes_json text,
    extra_count integer NOT NULL,
    is_cold_chain_violated boolean NOT NULL,
    manifest_id uuid,
    missing_count integer NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    notes text,
    origin_department_id uuid NOT NULL,
    packed_at timestamp without time zone,
    packed_by_id uuid,
    received_at timestamp without time zone,
    received_by_id uuid,
    record_state integer NOT NULL DEFAULT 2,
    returned_at timestamp without time zone,
    returned_by_id uuid,
    sealed_at timestamp without time zone,
    sealed_by_id uuid,
    status varchar(16) NOT NULL,
    temperature_receipt double precision,
    temperature_seal double precision,
    type_id uuid NOT NULL,
    CONSTRAINT pk_lab_transport_container PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_transport_container_barcode ON lab_transport_container (barcode);
CREATE INDEX IF NOT EXISTS ix_lab_transport_container_destination_department_id ON lab_transport_container (destination_department_id);
CREATE INDEX IF NOT EXISTS ix_lab_transport_container_manifest_id ON lab_transport_container (manifest_id);
CREATE INDEX IF NOT EXISTS ix_lab_transport_container_origin_department_id ON lab_transport_container (origin_department_id);
CREATE INDEX IF NOT EXISTS ix_lab_transport_container_status ON lab_transport_container (status);
CREATE INDEX IF NOT EXISTS ix_lab_transport_container_type_id ON lab_transport_container (type_id);

-- [ЛІС] lab_transport_container_event (LabTransportContainerEvent)
CREATE TABLE IF NOT EXISTS lab_transport_container_event (
    id uuid NOT NULL,
    at timestamp without time zone NOT NULL,
    container_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    details varchar(1024),
    employee_id uuid,
    event_type varchar(16) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    position integer,
    record_state integer NOT NULL DEFAULT 2,
    sample_barcode varchar(32),
    temperature double precision,
    CONSTRAINT pk_lab_transport_container_event PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_transport_container_event_container_id_at ON lab_transport_container_event (container_id, at);

-- [ЛІС] lab_transport_container_item (LabTransportContainerItem)
CREATE TABLE IF NOT EXISTS lab_transport_container_item (
    id uuid NOT NULL,
    barcode varchar(32) NOT NULL,
    container_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    packed_at timestamp without time zone NOT NULL,
    packed_by_id uuid,
    position integer NOT NULL,
    received_at timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    sample_id uuid NOT NULL,
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_transport_container_item PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_transport_container_item_sample_id ON lab_transport_container_item (sample_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_transport_container_item_container_id_position ON lab_transport_container_item (container_id, position);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_transport_container_item_container_id_sample_id ON lab_transport_container_item (container_id, sample_id);

-- [ЛІС] lab_transport_container_type (LabTransportContainerType)
CREATE TABLE IF NOT EXISTS lab_transport_container_type (
    id uuid NOT NULL,
    capacity integer NOT NULL,
    code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    is_reusable boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    temp_max_c double precision,
    temp_min_c double precision,
    CONSTRAINT pk_lab_transport_container_type PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_transport_container_type_code ON lab_transport_container_type (code);

-- [ЛІС] lab_tube_type (LabTubeType)
CREATE TABLE IF NOT EXISTS lab_tube_type (
    id integer NOT NULL,
    anticoagulant varchar(128),
    code varchar(64) NOT NULL,
    color_code varchar(16) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    inversions_count integer NOT NULL,
    is_active boolean NOT NULL,
    max_tests_per_tube integer,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    order_of_draw_index integer NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    usable_volume_ml double precision,
    volume_ml double precision NOT NULL,
    CONSTRAINT pk_lab_tube_type PRIMARY KEY (id)
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

-- [ЛІС] lab_unmatched_result (LabUnmatchedResult)
CREATE TABLE IF NOT EXISTS lab_unmatched_result (
    id uuid NOT NULL,
    analyzer_code varchar(64) NOT NULL,
    analyzer_id uuid,
    barcode varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    flags varchar(64),
    is_linked boolean NOT NULL,
    linked_order_test_id uuid,
    measured_at timestamp without time zone,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    raw_message_id uuid,
    reason varchar(512) NOT NULL,
    received_at timestamp without time zone NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    unit varchar(64),
    value varchar(128) NOT NULL,
    CONSTRAINT pk_lab_unmatched_result PRIMARY KEY (id)
);

-- [ЛІС] lab_work_calendar (LabWorkCalendarDay)
CREATE TABLE IF NOT EXISTS lab_work_calendar (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    day timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    kind varchar(16) NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256),
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_work_calendar PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_work_calendar_day ON lab_work_calendar (day);

-- [ЛІС] lab_workflow_template (LabWorkflowTemplate)
CREATE TABLE IF NOT EXISTS lab_workflow_template (
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(256) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    stages_json text NOT NULL,
    CONSTRAINT pk_lab_workflow_template PRIMARY KEY (code)
);

-- [ЛІС] lab_worklist_batch (LabWorklistBatch)
CREATE TABLE IF NOT EXISTS lab_worklist_batch (
    id uuid NOT NULL,
    analyzer_id uuid,
    batch_code varchar(64) NOT NULL,
    created_by uuid NOT NULL,
    created_by_id uuid,
    created_on timestamp without time zone NOT NULL,
    items_json text NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    status varchar(16) NOT NULL,
    CONSTRAINT pk_lab_worklist_batch PRIMARY KEY (id)
);

-- [MedLink-new] mis_patient_insurance (MisPatientInsurance)
CREATE TABLE IF NOT EXISTS mis_patient_insurance (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_active boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    note varchar(512),
    patient_card_id uuid NOT NULL,
    payer_id uuid NOT NULL,
    policy_number varchar(64) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    valid_from timestamp without time zone,
    valid_to timestamp without time zone,
    CONSTRAINT pk_mis_patient_insurance PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_mis_patient_insurance_patient_card_id ON mis_patient_insurance (patient_card_id);
CREATE INDEX IF NOT EXISTS ix_mis_patient_insurance_payer_id ON mis_patient_insurance (payer_id);

-- ----------------------------------------------------------------------------- зовнішні ключі
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_analyzer_type_id') THEN ALTER TABLE lab_analyzer ADD CONSTRAINT fk_lab_analyzer_analyzer_type_id FOREIGN KEY (analyzer_type_id) REFERENCES lab_analyzer_type (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_connector_id') THEN ALTER TABLE lab_analyzer ADD CONSTRAINT fk_lab_analyzer_connector_id FOREIGN KEY (connector_id) REFERENCES lab_connector_installation (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_department_id') THEN ALTER TABLE lab_analyzer ADD CONSTRAINT fk_lab_analyzer_department_id FOREIGN KEY (department_id) REFERENCES org_department (id); END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_lockout_analyzer_id') THEN ALTER TABLE lab_analyzer_lockout ADD CONSTRAINT fk_lab_analyzer_lockout_analyzer_id FOREIGN KEY (analyzer_id) REFERENCES lab_analyzer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_message_analyzer_id') THEN ALTER TABLE lab_analyzer_message ADD CONSTRAINT fk_lab_analyzer_message_analyzer_id FOREIGN KEY (analyzer_id) REFERENCES lab_analyzer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_parameter_map_analyzer_id') THEN ALTER TABLE lab_analyzer_parameter_map ADD CONSTRAINT fk_lab_analyzer_parameter_map_analyzer_id FOREIGN KEY (analyzer_id) REFERENCES lab_analyzer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_service_analyzer_id') THEN ALTER TABLE lab_analyzer_service ADD CONSTRAINT fk_lab_analyzer_service_analyzer_id FOREIGN KEY (analyzer_id) REFERENCES lab_analyzer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_analyzer_service_profile_id') THEN ALTER TABLE lab_analyzer_service ADD CONSTRAINT fk_lab_analyzer_service_profile_id FOREIGN KEY (profile_id) REFERENCES lab_test_profile (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_archive_cell_rack_id') THEN ALTER TABLE lab_archive_cell ADD CONSTRAINT fk_lab_archive_cell_rack_id FOREIGN KEY (rack_id) REFERENCES lab_archive_rack (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_archive_cell_sample_id') THEN ALTER TABLE lab_archive_cell ADD CONSTRAINT fk_lab_archive_cell_sample_id FOREIGN KEY (sample_id) REFERENCES lab_order_sample (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_calibration_analyzer_id') THEN ALTER TABLE lab_calibration ADD CONSTRAINT fk_lab_calibration_analyzer_id FOREIGN KEY (analyzer_id) REFERENCES lab_analyzer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_calibration_reagent_lot_id') THEN ALTER TABLE lab_calibration ADD CONSTRAINT fk_lab_calibration_reagent_lot_id FOREIGN KEY (reagent_lot_id) REFERENCES lab_reagent_lot (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_culture_order_order_id') THEN ALTER TABLE lab_culture_order ADD CONSTRAINT fk_lab_culture_order_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_culture_order_order_test_id') THEN ALTER TABLE lab_culture_order ADD CONSTRAINT fk_lab_culture_order_order_test_id FOREIGN KEY (order_test_id) REFERENCES lab_order_test (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_department_settings_department_id') THEN ALTER TABLE lab_department_settings ADD CONSTRAINT fk_lab_department_settings_department_id FOREIGN KEY (department_id) REFERENCES org_department (id) ON DELETE CASCADE; END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_ehealth_submission_order_id') THEN ALTER TABLE lab_ehealth_submission ADD CONSTRAINT fk_lab_ehealth_submission_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_employee_settings_employee_id') THEN ALTER TABLE lab_employee_settings ADD CONSTRAINT fk_lab_employee_settings_employee_id FOREIGN KEY (employee_id) REFERENCES org_employee (id) ON DELETE CASCADE; END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_eucast_breakpoint_antibiotic_id') THEN ALTER TABLE lab_eucast_breakpoint ADD CONSTRAINT fk_lab_eucast_breakpoint_antibiotic_id FOREIGN KEY (antibiotic_id) REFERENCES lab_antibiotic (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_eucast_breakpoint_organism_id') THEN ALTER TABLE lab_eucast_breakpoint ADD CONSTRAINT fk_lab_eucast_breakpoint_organism_id FOREIGN KEY (organism_id) REFERENCES lab_micro_organism (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_intervention_map_intervention_id') THEN ALTER TABLE lab_intervention_map ADD CONSTRAINT fk_lab_intervention_map_intervention_id FOREIGN KEY (intervention_id) REFERENCES lab_ehealth_intervention (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_intervention_map_profile_id') THEN ALTER TABLE lab_intervention_map ADD CONSTRAINT fk_lab_intervention_map_profile_id FOREIGN KEY (profile_id) REFERENCES lab_test_profile (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_intervention_map_test_map_id') THEN ALTER TABLE lab_intervention_map_test ADD CONSTRAINT fk_lab_intervention_map_test_map_id FOREIGN KEY (map_id) REFERENCES lab_intervention_map (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_intervention_map_test_test_id') THEN ALTER TABLE lab_intervention_map_test ADD CONSTRAINT fk_lab_intervention_map_test_test_id FOREIGN KEY (test_id) REFERENCES lab_test_definition (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_invoice_payer_id') THEN ALTER TABLE lab_invoice ADD CONSTRAINT fk_lab_invoice_payer_id FOREIGN KEY (payer_id) REFERENCES lab_payer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_isolate_culture_order_id') THEN ALTER TABLE lab_isolate ADD CONSTRAINT fk_lab_isolate_culture_order_id FOREIGN KEY (culture_order_id) REFERENCES lab_culture_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_isolate_organism_id') THEN ALTER TABLE lab_isolate ADD CONSTRAINT fk_lab_isolate_organism_id FOREIGN KEY (organism_id) REFERENCES lab_micro_organism (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_department_id') THEN ALTER TABLE lab_order ADD CONSTRAINT fk_lab_order_department_id FOREIGN KEY (department_id) REFERENCES org_department (id); END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_diagnostic_report_id') THEN ALTER TABLE lab_order ADD CONSTRAINT fk_lab_order_diagnostic_report_id FOREIGN KEY (diagnostic_report_id) REFERENCES mis_diagnostic_report (id); END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_doctor_id') THEN ALTER TABLE lab_order ADD CONSTRAINT fk_lab_order_doctor_id FOREIGN KEY (doctor_id) REFERENCES org_employee (id); END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_ehealth_referral_id') THEN ALTER TABLE lab_order ADD CONSTRAINT fk_lab_order_ehealth_referral_id FOREIGN KEY (ehealth_referral_id) REFERENCES ehe_incoming_medical_referral (id); END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_paper_referral_id') THEN ALTER TABLE lab_order ADD CONSTRAINT fk_lab_order_paper_referral_id FOREIGN KEY (paper_referral_id) REFERENCES ehe_paper_medical_referral (id); END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_patient_id') THEN ALTER TABLE lab_order ADD CONSTRAINT fk_lab_order_patient_id FOREIGN KEY (patient_id) REFERENCES mis_patient_card (id) ON DELETE CASCADE; END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_payer_id') THEN ALTER TABLE lab_order ADD CONSTRAINT fk_lab_order_payer_id FOREIGN KEY (payer_id) REFERENCES lab_payer (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_attachment_order_id') THEN ALTER TABLE lab_order_attachment ADD CONSTRAINT fk_lab_order_attachment_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_charge_order_id') THEN ALTER TABLE lab_order_charge ADD CONSTRAINT fk_lab_order_charge_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_charge_payer_id') THEN ALTER TABLE lab_order_charge ADD CONSTRAINT fk_lab_order_charge_payer_id FOREIGN KEY (payer_id) REFERENCES lab_payer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_item_intervention_id') THEN ALTER TABLE lab_order_item ADD CONSTRAINT fk_lab_order_item_intervention_id FOREIGN KEY (intervention_id) REFERENCES lab_ehealth_intervention (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_item_order_id') THEN ALTER TABLE lab_order_item ADD CONSTRAINT fk_lab_order_item_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_item_profile_id') THEN ALTER TABLE lab_order_item ADD CONSTRAINT fk_lab_order_item_profile_id FOREIGN KEY (profile_id) REFERENCES lab_test_profile (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_item_indicator_item_id') THEN ALTER TABLE lab_order_item_indicator ADD CONSTRAINT fk_lab_order_item_indicator_item_id FOREIGN KEY (item_id) REFERENCES lab_order_item (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_sample_biomaterial_type_id') THEN ALTER TABLE lab_order_sample ADD CONSTRAINT fk_lab_order_sample_biomaterial_type_id FOREIGN KEY (biomaterial_type_id) REFERENCES lab_biomaterial_type (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_sample_lab_section_id') THEN ALTER TABLE lab_order_sample ADD CONSTRAINT fk_lab_order_sample_lab_section_id FOREIGN KEY (lab_section_id) REFERENCES lab_section (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_sample_order_id') THEN ALTER TABLE lab_order_sample ADD CONSTRAINT fk_lab_order_sample_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_sample_parent_sample_id') THEN ALTER TABLE lab_order_sample ADD CONSTRAINT fk_lab_order_sample_parent_sample_id FOREIGN KEY (parent_sample_id) REFERENCES lab_order_sample (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_sample_tube_type_id') THEN ALTER TABLE lab_order_sample ADD CONSTRAINT fk_lab_order_sample_tube_type_id FOREIGN KEY (tube_type_id) REFERENCES lab_tube_type (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_test_assigned_analyzer_id') THEN ALTER TABLE lab_order_test ADD CONSTRAINT fk_lab_order_test_assigned_analyzer_id FOREIGN KEY (assigned_analyzer_id) REFERENCES lab_analyzer (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_test_order_id') THEN ALTER TABLE lab_order_test ADD CONSTRAINT fk_lab_order_test_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_test_performer_id') THEN ALTER TABLE lab_order_test ADD CONSTRAINT fk_lab_order_test_performer_id FOREIGN KEY (performer_id) REFERENCES lab_performer (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_test_profile_id') THEN ALTER TABLE lab_order_test ADD CONSTRAINT fk_lab_order_test_profile_id FOREIGN KEY (profile_id) REFERENCES lab_test_profile (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_test_sample_id') THEN ALTER TABLE lab_order_test ADD CONSTRAINT fk_lab_order_test_sample_id FOREIGN KEY (sample_id) REFERENCES lab_order_sample (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_test_test_id') THEN ALTER TABLE lab_order_test ADD CONSTRAINT fk_lab_order_test_test_id FOREIGN KEY (test_id) REFERENCES lab_test_definition (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_path_block_object_id') THEN ALTER TABLE lab_path_block ADD CONSTRAINT fk_lab_path_block_object_id FOREIGN KEY (object_id) REFERENCES lab_path_object (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_path_ihc_result_report_id') THEN ALTER TABLE lab_path_ihc_result ADD CONSTRAINT fk_lab_path_ihc_result_report_id FOREIGN KEY (report_id) REFERENCES lab_path_report (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_path_referral_order_id') THEN ALTER TABLE lab_path_referral ADD CONSTRAINT fk_lab_path_referral_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_path_slide_block_id') THEN ALTER TABLE lab_path_slide ADD CONSTRAINT fk_lab_path_slide_block_id FOREIGN KEY (block_id) REFERENCES lab_path_block (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_payer_price_list_id') THEN ALTER TABLE lab_payer ADD CONSTRAINT fk_lab_payer_price_list_id FOREIGN KEY (price_list_id) REFERENCES lab_price_list (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_payment_order_id') THEN ALTER TABLE lab_payment ADD CONSTRAINT fk_lab_payment_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_performer_test_performer_id') THEN ALTER TABLE lab_performer_test ADD CONSTRAINT fk_lab_performer_test_performer_id FOREIGN KEY (performer_id) REFERENCES lab_performer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_performer_test_test_id') THEN ALTER TABLE lab_performer_test ADD CONSTRAINT fk_lab_performer_test_test_id FOREIGN KEY (test_id) REFERENCES lab_test_definition (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_price_list_item_price_list_id') THEN ALTER TABLE lab_price_list_item ADD CONSTRAINT fk_lab_price_list_item_price_list_id FOREIGN KEY (price_list_id) REFERENCES lab_price_list (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_price_package_price_list_id') THEN ALTER TABLE lab_price_package ADD CONSTRAINT fk_lab_price_package_price_list_id FOREIGN KEY (price_list_id) REFERENCES lab_price_list (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_qc_material_analyzer_id') THEN ALTER TABLE lab_qc_material ADD CONSTRAINT fk_lab_qc_material_analyzer_id FOREIGN KEY (analyzer_id) REFERENCES lab_analyzer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_qc_result_qc_material_id') THEN ALTER TABLE lab_qc_result ADD CONSTRAINT fk_lab_qc_result_qc_material_id FOREIGN KEY (qc_material_id) REFERENCES lab_qc_material (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_qc_target_qc_material_id') THEN ALTER TABLE lab_qc_target ADD CONSTRAINT fk_lab_qc_target_qc_material_id FOREIGN KEY (qc_material_id) REFERENCES lab_qc_material (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_reagent_consumption_lot_id') THEN ALTER TABLE lab_reagent_consumption ADD CONSTRAINT fk_lab_reagent_consumption_lot_id FOREIGN KEY (lot_id) REFERENCES lab_reagent_lot (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_reagent_lot_analyzer_id') THEN ALTER TABLE lab_reagent_lot ADD CONSTRAINT fk_lab_reagent_lot_analyzer_id FOREIGN KEY (analyzer_id) REFERENCES lab_analyzer (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_report_archive_order_id') THEN ALTER TABLE lab_report_archive ADD CONSTRAINT fk_lab_report_archive_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_result_attempt_order_test_id') THEN ALTER TABLE lab_result_attempt ADD CONSTRAINT fk_lab_result_attempt_order_test_id FOREIGN KEY (order_test_id) REFERENCES lab_order_test (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_sample_logistics_destination_department_id') THEN ALTER TABLE lab_sample_logistics ADD CONSTRAINT fk_lab_sample_logistics_destination_department_id FOREIGN KEY (destination_department_id) REFERENCES org_department (id) ON DELETE CASCADE; END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_sample_logistics_origin_department_id') THEN ALTER TABLE lab_sample_logistics ADD CONSTRAINT fk_lab_sample_logistics_origin_department_id FOREIGN KEY (origin_department_id) REFERENCES org_department (id) ON DELETE CASCADE; END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_sample_logistics_item_logistics_id') THEN ALTER TABLE lab_sample_logistics_item ADD CONSTRAINT fk_lab_sample_logistics_item_logistics_id FOREIGN KEY (logistics_id) REFERENCES lab_sample_logistics (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_sample_logistics_item_sample_id') THEN ALTER TABLE lab_sample_logistics_item ADD CONSTRAINT fk_lab_sample_logistics_item_sample_id FOREIGN KEY (sample_id) REFERENCES lab_order_sample (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_sample_stage_event_sample_id') THEN ALTER TABLE lab_sample_stage_event ADD CONSTRAINT fk_lab_sample_stage_event_sample_id FOREIGN KEY (sample_id) REFERENCES lab_order_sample (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_section_department_id') THEN ALTER TABLE lab_section ADD CONSTRAINT fk_lab_section_department_id FOREIGN KEY (department_id) REFERENCES org_department (id); END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_section_journal_entry_lab_section_id') THEN ALTER TABLE lab_section_journal_entry ADD CONSTRAINT fk_lab_section_journal_entry_lab_section_id FOREIGN KEY (lab_section_id) REFERENCES lab_section (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_section_journal_entry_order_id') THEN ALTER TABLE lab_section_journal_entry ADD CONSTRAINT fk_lab_section_journal_entry_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_section_journal_entry_sample_id') THEN ALTER TABLE lab_section_journal_entry ADD CONSTRAINT fk_lab_section_journal_entry_sample_id FOREIGN KEY (sample_id) REFERENCES lab_order_sample (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_send_out_performer_id') THEN ALTER TABLE lab_send_out ADD CONSTRAINT fk_lab_send_out_performer_id FOREIGN KEY (performer_id) REFERENCES lab_performer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_send_out_item_order_test_id') THEN ALTER TABLE lab_send_out_item ADD CONSTRAINT fk_lab_send_out_item_order_test_id FOREIGN KEY (order_test_id) REFERENCES lab_order_test (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_send_out_item_sample_id') THEN ALTER TABLE lab_send_out_item ADD CONSTRAINT fk_lab_send_out_item_sample_id FOREIGN KEY (sample_id) REFERENCES lab_order_sample (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_send_out_item_send_out_id') THEN ALTER TABLE lab_send_out_item ADD CONSTRAINT fk_lab_send_out_item_send_out_id FOREIGN KEY (send_out_id) REFERENCES lab_send_out (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_susceptibility_result_antibiotic_id') THEN ALTER TABLE lab_susceptibility_result ADD CONSTRAINT fk_lab_susceptibility_result_antibiotic_id FOREIGN KEY (antibiotic_id) REFERENCES lab_antibiotic (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_susceptibility_result_isolate_id') THEN ALTER TABLE lab_susceptibility_result ADD CONSTRAINT fk_lab_susceptibility_result_isolate_id FOREIGN KEY (isolate_id) REFERENCES lab_isolate (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_definition_biomaterial_type_id') THEN ALTER TABLE lab_test_definition ADD CONSTRAINT fk_lab_test_definition_biomaterial_type_id FOREIGN KEY (biomaterial_type_id) REFERENCES lab_biomaterial_type (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_definition_lab_section_id') THEN ALTER TABLE lab_test_definition ADD CONSTRAINT fk_lab_test_definition_lab_section_id FOREIGN KEY (lab_section_id) REFERENCES lab_section (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_definition_method_id') THEN ALTER TABLE lab_test_definition ADD CONSTRAINT fk_lab_test_definition_method_id FOREIGN KEY (method_id) REFERENCES lab_method_type (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_definition_tube_type_id') THEN ALTER TABLE lab_test_definition ADD CONSTRAINT fk_lab_test_definition_tube_type_id FOREIGN KEY (tube_type_id) REFERENCES lab_tube_type (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_profile_ehealth_service_catalog_service_id') THEN ALTER TABLE lab_test_profile ADD CONSTRAINT fk_lab_test_profile_ehealth_service_catalog_service_id FOREIGN KEY (ehealth_service_catalog_service_id) REFERENCES ehe_service_catalog_service (id); END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_profile_organization_service_id') THEN ALTER TABLE lab_test_profile ADD CONSTRAINT fk_lab_test_profile_organization_service_id FOREIGN KEY (organization_service_id) REFERENCES org_organization_service (id); END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_profile_item_profile_id') THEN ALTER TABLE lab_test_profile_item ADD CONSTRAINT fk_lab_test_profile_item_profile_id FOREIGN KEY (profile_id) REFERENCES lab_test_profile (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_profile_item_test_id') THEN ALTER TABLE lab_test_profile_item ADD CONSTRAINT fk_lab_test_profile_item_test_id FOREIGN KEY (test_id) REFERENCES lab_test_definition (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_result_analyzer_id') THEN ALTER TABLE lab_test_result ADD CONSTRAINT fk_lab_test_result_analyzer_id FOREIGN KEY (analyzer_id) REFERENCES lab_analyzer (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_test_result_order_test_id') THEN ALTER TABLE lab_test_result ADD CONSTRAINT fk_lab_test_result_order_test_id FOREIGN KEY (order_test_id) REFERENCES lab_order_test (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_transport_container_destination_department_id') THEN ALTER TABLE lab_transport_container ADD CONSTRAINT fk_lab_transport_container_destination_department_id FOREIGN KEY (destination_department_id) REFERENCES org_department (id) ON DELETE CASCADE; END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_transport_container_manifest_id') THEN ALTER TABLE lab_transport_container ADD CONSTRAINT fk_lab_transport_container_manifest_id FOREIGN KEY (manifest_id) REFERENCES lab_sample_logistics (id) ON DELETE SET NULL; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_transport_container_origin_department_id') THEN ALTER TABLE lab_transport_container ADD CONSTRAINT fk_lab_transport_container_origin_department_id FOREIGN KEY (origin_department_id) REFERENCES org_department (id) ON DELETE CASCADE; END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_transport_container_type_id') THEN ALTER TABLE lab_transport_container ADD CONSTRAINT fk_lab_transport_container_type_id FOREIGN KEY (type_id) REFERENCES lab_transport_container_type (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_transport_container_event_container_id') THEN ALTER TABLE lab_transport_container_event ADD CONSTRAINT fk_lab_transport_container_event_container_id FOREIGN KEY (container_id) REFERENCES lab_transport_container (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_transport_container_item_container_id') THEN ALTER TABLE lab_transport_container_item ADD CONSTRAINT fk_lab_transport_container_item_container_id FOREIGN KEY (container_id) REFERENCES lab_transport_container (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_transport_container_item_sample_id') THEN ALTER TABLE lab_transport_container_item ADD CONSTRAINT fk_lab_transport_container_item_sample_id FOREIGN KEY (sample_id) REFERENCES lab_order_sample (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_unit_conversion_from_unit_id') THEN ALTER TABLE lab_unit_conversion ADD CONSTRAINT fk_lab_unit_conversion_from_unit_id FOREIGN KEY (from_unit_id) REFERENCES lab_unit (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_unit_conversion_to_unit_id') THEN ALTER TABLE lab_unit_conversion ADD CONSTRAINT fk_lab_unit_conversion_to_unit_id FOREIGN KEY (to_unit_id) REFERENCES lab_unit (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_mis_patient_insurance_patient_card_id') THEN ALTER TABLE mis_patient_insurance ADD CONSTRAINT fk_mis_patient_insurance_patient_card_id FOREIGN KEY (patient_card_id) REFERENCES mis_patient_card (id) ON DELETE CASCADE; END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_mis_patient_insurance_payer_id') THEN ALTER TABLE mis_patient_insurance ADD CONSTRAINT fk_mis_patient_insurance_payer_id FOREIGN KEY (payer_id) REFERENCES lab_payer (id) ON DELETE CASCADE; END IF; END $$;

COMMIT;
