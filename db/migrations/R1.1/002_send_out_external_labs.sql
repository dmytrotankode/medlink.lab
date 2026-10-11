-- =============================================================================
-- MedLink LIS R1.1 · Направлення в зовнішні лабораторії (send-out), ТЗ розд. 20.3
-- Цільова СУБД: PostgreSQL (evomis / MedLink). Ідемпотентний. Усі таблиці — [ЛІС]; таблиці MedLink не змінюються.
-- Фрагмент повної схеми db/postgres/medlink_lis_schema.sql (генерується з EF-моделі).
-- =============================================================================
BEGIN;

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

-- [ЛІС] lab_order_test, lab_test_result — виконавець дослідження
ALTER TABLE lab_order_test ADD COLUMN IF NOT EXISTS performer_id uuid NULL;
CREATE INDEX IF NOT EXISTS ix_lab_order_test_performer_id ON lab_order_test (performer_id);
ALTER TABLE lab_test_result ADD COLUMN IF NOT EXISTS performer_id uuid NULL;
ALTER TABLE lab_test_result ADD COLUMN IF NOT EXISTS external_reference varchar(128) NULL;

DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_attachment_order_id') THEN ALTER TABLE lab_order_attachment ADD CONSTRAINT fk_lab_order_attachment_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_test_performer_id') THEN ALTER TABLE lab_order_test ADD CONSTRAINT fk_lab_order_test_performer_id FOREIGN KEY (performer_id) REFERENCES lab_performer (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_performer_test_performer_id') THEN ALTER TABLE lab_performer_test ADD CONSTRAINT fk_lab_performer_test_performer_id FOREIGN KEY (performer_id) REFERENCES lab_performer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_performer_test_test_id') THEN ALTER TABLE lab_performer_test ADD CONSTRAINT fk_lab_performer_test_test_id FOREIGN KEY (test_id) REFERENCES lab_test_definition (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_send_out_performer_id') THEN ALTER TABLE lab_send_out ADD CONSTRAINT fk_lab_send_out_performer_id FOREIGN KEY (performer_id) REFERENCES lab_performer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_send_out_item_order_test_id') THEN ALTER TABLE lab_send_out_item ADD CONSTRAINT fk_lab_send_out_item_order_test_id FOREIGN KEY (order_test_id) REFERENCES lab_order_test (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_send_out_item_sample_id') THEN ALTER TABLE lab_send_out_item ADD CONSTRAINT fk_lab_send_out_item_sample_id FOREIGN KEY (sample_id) REFERENCES lab_order_sample (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_send_out_item_send_out_id') THEN ALTER TABLE lab_send_out_item ADD CONSTRAINT fk_lab_send_out_item_send_out_id FOREIGN KEY (send_out_id) REFERENCES lab_send_out (id) ON DELETE CASCADE; END IF; END $$;

COMMIT;
