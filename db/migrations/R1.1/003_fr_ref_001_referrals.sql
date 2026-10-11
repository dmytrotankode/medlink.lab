-- =============================================================================
-- MedLink LIS R1.1 · FR-REF-001 — типи направлень, паперові направлення, обробка е-направлення, журнал обміну з ЕСОЗ (ТЗ 20.6)
-- PostgreSQL (evomis). Ідемпотентний.
-- [MedLink] ehe_paper_medical_referral, ehe_incoming_medical_referral — таблиці evomis, колонки вже існують (не змінюються).
-- =============================================================================
BEGIN;

-- [ЛІС] lab_order — тип і реквізити направлення
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS referral_type varchar(16) NOT NULL DEFAULT 'SELF';
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS paper_referral_id uuid NULL;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS referrer_organization_name varchar(256) NULL;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS referrer_organization_edrpou varchar(16) NULL;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS referrer_doctor_name varchar(256) NULL;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS referrer_number varchar(64) NULL;
CREATE INDEX IF NOT EXISTS ix_lab_order_referral_type ON lab_order (referral_type);
CREATE INDEX IF NOT EXISTS ix_lab_order_ehealth_referral_id ON lab_order (ehealth_referral_id);
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_paper_referral_id') THEN
  ALTER TABLE lab_order ADD CONSTRAINT fk_lab_order_paper_referral_id FOREIGN KEY (paper_referral_id) REFERENCES ehe_paper_medical_referral (id); END IF; END $$; -- → [MedLink]
UPDATE lab_order SET referral_type = 'EHEALTH' WHERE ehealth_referral_id IS NOT NULL AND referral_type = 'SELF';
UPDATE lab_order SET referral_type = 'INTERNAL' WHERE ehealth_referral_id IS NULL AND doctor_id IS NOT NULL AND referral_type = 'SELF';

-- [ЛІС] журнал обміну з ЕСОЗ
CREATE TABLE IF NOT EXISTS lab_ehealth_exchange_log (
    id uuid NOT NULL,
    at timestamp without time zone NOT NULL,
    mode varchar(8) NOT NULL,
    method varchar(8) NOT NULL,
    url varchar(512) NOT NULL,
    request_json text,
    status_code integer,
    response_json text,
    duration_ms integer NOT NULL,
    created_by uuid NOT NULL,
    created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL,
    modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_lab_ehealth_exchange_log PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_lab_ehealth_exchange_log_at ON lab_ehealth_exchange_log (at);

COMMIT;
