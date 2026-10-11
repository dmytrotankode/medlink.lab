-- =============================================================================
-- MedLink LIS R1.1 · FR-GAP-060 — оплата: прайси, пакети, платники, поліси, нарахування, каса, рахунки (ТЗ 20.7)
-- PostgreSQL (evomis). Ідемпотентний. [ЛІС] — таблиці модуля; [MedLink-new] mis_patient_insurance — нова загальна сутність МІС.
-- Фрагмент повної схеми db/postgres/medlink_lis_schema.sql.
-- =============================================================================
BEGIN;

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

-- [ЛІС] lab_order — платник і суми
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS payer_id uuid NULL;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS patient_insurance_id uuid NULL;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS insurance_policy_number varchar(64) NULL;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS authorization_number varchar(64) NULL;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS payer_amount numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS patient_amount numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE lab_order ADD COLUMN IF NOT EXISTS paid_amount numeric(18,2) NOT NULL DEFAULT 0;

DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_invoice_payer_id') THEN ALTER TABLE lab_invoice ADD CONSTRAINT fk_lab_invoice_payer_id FOREIGN KEY (payer_id) REFERENCES lab_payer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_payer_id') THEN ALTER TABLE lab_order ADD CONSTRAINT fk_lab_order_payer_id FOREIGN KEY (payer_id) REFERENCES lab_payer (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_charge_order_id') THEN ALTER TABLE lab_order_charge ADD CONSTRAINT fk_lab_order_charge_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_order_charge_payer_id') THEN ALTER TABLE lab_order_charge ADD CONSTRAINT fk_lab_order_charge_payer_id FOREIGN KEY (payer_id) REFERENCES lab_payer (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_payer_price_list_id') THEN ALTER TABLE lab_payer ADD CONSTRAINT fk_lab_payer_price_list_id FOREIGN KEY (price_list_id) REFERENCES lab_price_list (id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_payment_order_id') THEN ALTER TABLE lab_payment ADD CONSTRAINT fk_lab_payment_order_id FOREIGN KEY (order_id) REFERENCES lab_order (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_price_list_item_price_list_id') THEN ALTER TABLE lab_price_list_item ADD CONSTRAINT fk_lab_price_list_item_price_list_id FOREIGN KEY (price_list_id) REFERENCES lab_price_list (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lab_price_package_price_list_id') THEN ALTER TABLE lab_price_package ADD CONSTRAINT fk_lab_price_package_price_list_id FOREIGN KEY (price_list_id) REFERENCES lab_price_list (id) ON DELETE CASCADE; END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_mis_patient_insurance_patient_card_id') THEN ALTER TABLE mis_patient_insurance ADD CONSTRAINT fk_mis_patient_insurance_patient_card_id FOREIGN KEY (patient_card_id) REFERENCES mis_patient_card (id) ON DELETE CASCADE; END IF; END $$; -- → [MedLink]
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_mis_patient_insurance_payer_id') THEN ALTER TABLE mis_patient_insurance ADD CONSTRAINT fk_mis_patient_insurance_payer_id FOREIGN KEY (payer_id) REFERENCES lab_payer (id) ON DELETE CASCADE; END IF; END $$;

COMMIT;
