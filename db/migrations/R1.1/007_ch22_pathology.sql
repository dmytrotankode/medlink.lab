-- =============================================================================
-- MedLink LIS R1.1 · ТЗ розд. 22 — модуль «Патоморфологія»: справа ПГД (форма 014/о), об'єкти, блоки, скельця, журнал етапів,
-- висновок з версіями й доповненнями, ІГХ, повідомлення CITO, видача з архіву; довідники (шаблони, МКХ-О, методики, reflex ІГХ,
-- типи матеріалу, фіксатори, причини відмови). Колонки lab_order.lab_department/pathology_kind/case_no/sla_due_at — у міграції 006.
-- Цільова СУБД: PostgreSQL (evomis / MedLink). Ідемпотентний. Фрагмент повної схеми db/postgres/medlink_lis_schema.sql.
-- Позначки: [ЛІС] — таблиця модуля «Лабораторія». Таблиці MedLink у цій міграції не змінюються.
-- =============================================================================
BEGIN;

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

-- [ЛІС] lab_fixative (LabFixative)
CREATE TABLE IF NOT EXISTS lab_fixative (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
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
    stage varchar(16),
    status varchar(16) NOT NULL,
    storage_location varchar(128),
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
    record_state integer NOT NULL DEFAULT 2,
    returned_at timestamp without time zone,
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
    taken_at timestamp without time zone,
    CONSTRAINT pk_lab_path_object PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_path_object_barcode ON lab_path_object (barcode);
CREATE INDEX IF NOT EXISTS ix_lab_path_object_order_id ON lab_path_object (order_id);

-- [ЛІС] lab_path_referral (LabPathReferral)
CREATE TABLE IF NOT EXISTS lab_path_referral (
    id uuid NOT NULL,
    accepted_at timestamp without time zone,
    accepted_by_id uuid,
    biopsy_kind varchar(16) NOT NULL,
    cito_notified_at timestamp without time zone,
    clinical_data text,
    clinical_dx_icd10 varchar(256),
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    external_blocks integer NOT NULL,
    external_date timestamp without time zone,
    external_institution varchar(256),
    external_number varchar(64),
    external_slides integer NOT NULL,
    form014_json text,
    gross_by_id uuid,
    gross_finished_at timestamp without time zone,
    intake_status varchar(16) NOT NULL,
    macro text,
    marking_text varchar(1024),
    medical_card_no varchar(64),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    operation_text varchar(1024),
    order_id uuid NOT NULL,
    price numeric(18,2) NOT NULL,
    primary_case_no varchar(64),
    record_state integer NOT NULL DEFAULT 2,
    referring_department varchar(256),
    referring_doctor varchar(256),
    reject_comment varchar(1024),
    reject_reason varchar(256),
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
    changes_diagnosis boolean NOT NULL,
    conclusion_text text,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
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
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_external boolean NOT NULL,
    is_ihc boolean NOT NULL,
    marker varchar(32),
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    order_id uuid NOT NULL,
    ordered_from varchar(8),
    record_state integer NOT NULL DEFAULT 2,
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

-- [ЛІС] lab_path_template (LabPathTemplate)
CREATE TABLE IF NOT EXISTS lab_path_template (
    id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    macro text,
    micro text,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_path_template PRIMARY KEY (id)
);

-- [ЛІС] lab_stain_method (LabStainMethod)
CREATE TABLE IF NOT EXISTS lab_stain_method (
    id uuid NOT NULL,
    code varchar(32) NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    is_ihc boolean NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    name varchar(128) NOT NULL,
    price numeric(18,2) NOT NULL,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_stain_method PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lab_stain_method_code ON lab_stain_method (code);

DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_path_block_object_id') THEN ALTER TABLE lab_path_block ADD CONSTRAINT fk_lab_path_block_object_id FOREIGN KEY (object_id) REFERENCES lab_path_object (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_path_ihc_result_report_id') THEN ALTER TABLE lab_path_ihc_result ADD CONSTRAINT fk_lab_path_ihc_result_report_id FOREIGN KEY (report_id) REFERENCES lab_path_report (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_path_referral_order_id') THEN ALTER TABLE lab_path_referral ADD CONSTRAINT fk_lab_path_referral_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_path_slide_block_id') THEN ALTER TABLE lab_path_slide ADD CONSTRAINT fk_lab_path_slide_block_id FOREIGN KEY (block_id) REFERENCES lab_path_block (id) ON DELETE CASCADE; END IF; END $$;
COMMIT;
