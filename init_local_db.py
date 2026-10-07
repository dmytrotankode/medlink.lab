# -*- coding: utf-8 -*-
"""
Initialize local SQLite database for MedLink LIS: medlink_lab_local.db
Includes all core tables, evomis mock tables (patients, referrals, diagnostic reports),
and seed dictionaries.
"""

import sqlite3
import os
import json

BASE_DIR = r"C:\__MEDLINK___\LABA"
DB_PATH = os.path.join(BASE_DIR, "medlink_lab_local.db")

if os.path.exists(DB_PATH):
    try:
        os.remove(DB_PATH)
    except Exception as e:
        print("Note: couldn't delete existing db, will overwrite tables:", e)

conn = sqlite3.connect(DB_PATH)
cur = conn.cursor()

# Enable foreign keys
cur.execute("PRAGMA foreign_keys = ON;")

# 1. EVOMIS INTEGRATION TABLES (matching evomis PostgreSQL schema)
cur.executescript("""
CREATE TABLE IF NOT EXISTS mis_patient_card (
    id TEXT PRIMARY KEY,
    last_name TEXT NOT NULL,
    first_name TEXT NOT NULL,
    second_name TEXT,
    birth_date TEXT NOT NULL,
    gender TEXT NOT NULL, -- 'MALE', 'FEMALE'
    phone TEXT,
    tax_id TEXT, -- РНОКПП
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS org_department (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    code TEXT,
    department_type TEXT DEFAULT 'CLINIC'
);

CREATE TABLE IF NOT EXISTS org_employee (
    id TEXT PRIMARY KEY,
    full_name TEXT NOT NULL,
    position_name TEXT NOT NULL,
    speciality_name TEXT,
    digital_signature_cert_id TEXT
);

CREATE TABLE IF NOT EXISTS ehe_incoming_medical_referral (
    id TEXT PRIMARY KEY,
    referral_code TEXT UNIQUE NOT NULL, -- e.g. '01HJ-89A5-K2P8'
    patient_id TEXT NOT NULL REFERENCES mis_patient_card(id),
    service_code TEXT NOT NULL, -- e.g. '59300-00'
    service_name TEXT NOT NULL,
    category TEXT DEFAULT 'LABORATORY',
    requester_doctor_id TEXT REFERENCES org_employee(id),
    requester_organization TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'NEW', -- 'NEW', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED'
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS mis_diagnostic_report (
    id TEXT PRIMARY KEY,
    report_number TEXT UNIQUE NOT NULL,
    patient_id TEXT NOT NULL REFERENCES mis_patient_card(id),
    referral_id TEXT REFERENCES ehe_incoming_medical_referral(id),
    service_name TEXT NOT NULL,
    performer_doctor_id TEXT REFERENCES org_employee(id),
    status TEXT NOT NULL DEFAULT 'FINAL', -- 'PRELIMINARY', 'FINAL', 'AMENDED'
    ehealth_synced BOOLEAN DEFAULT 0,
    conclusions TEXT,
    signed_at TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);
""")

# 2. MEDLINK LIS CORE TABLES
cur.executescript("""
CREATE TABLE IF NOT EXISTS lab_biomaterial_types (
    id INTEGER PRIMARY KEY,
    code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    is_active INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS lab_tube_types (
    id INTEGER PRIMARY KEY,
    code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    color_code TEXT NOT NULL,
    volume_ml TEXT,
    order_of_draw_index INTEGER DEFAULT 99,
    is_active INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS lab_method_types (
    id INTEGER PRIMARY KEY,
    code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    is_active INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS lab_analyzer_types (
    id INTEGER PRIMARY KEY,
    code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    exch_type TEXT DEFAULT 'ASTM',
    is_active INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS lab_analyzers (
    id TEXT PRIMARY KEY,
    code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    analyzer_type_id INTEGER REFERENCES lab_analyzer_types(id),
    connection_mode TEXT DEFAULT 'TCP',
    ip_host TEXT,
    ip_port INTEGER,
    is_online INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS lab_test_definitions (
    id TEXT PRIMARY KEY,
    code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    loinc_code TEXT,
    unit TEXT,
    method_name TEXT,
    delta_check_max_pct REAL,
    is_active INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS lab_orders (
    id TEXT PRIMARY KEY,
    order_number TEXT UNIQUE NOT NULL,
    patient_id TEXT NOT NULL REFERENCES mis_patient_card(id),
    referral_id TEXT REFERENCES ehe_incoming_medical_referral(id),
    doctor_id TEXT REFERENCES org_employee(id),
    department_id TEXT REFERENCES org_department(id),
    order_datetime TEXT DEFAULT CURRENT_TIMESTAMP,
    status TEXT NOT NULL DEFAULT 'NEW', -- 'NEW', 'COLLECTED', 'IN_TRANSIT', 'RECEIVED', 'ANALYZING', 'VERIFIED', 'COMPLETED'
    is_urgent_cito INTEGER DEFAULT 0,
    clinical_notes TEXT
);

CREATE TABLE IF NOT EXISTS lab_order_samples (
    id TEXT PRIMARY KEY,
    order_id TEXT NOT NULL REFERENCES lab_orders(id) ON DELETE CASCADE,
    barcode TEXT UNIQUE NOT NULL,
    tube_type_id INTEGER REFERENCES lab_tube_types(id),
    biomaterial_type_id INTEGER REFERENCES lab_biomaterial_types(id),
    collected_at TEXT,
    collected_by_id TEXT REFERENCES org_employee(id),
    status TEXT NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'COLLECTED', 'IN_TRANSIT', 'RECEIVED', 'REJECTED'
    is_hemolyzed INTEGER DEFAULT 0,
    is_lipemic INTEGER DEFAULT 0,
    is_clotted INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS lab_test_results (
    id TEXT PRIMARY KEY,
    order_id TEXT NOT NULL REFERENCES lab_orders(id) ON DELETE CASCADE,
    sample_id TEXT REFERENCES lab_order_samples(id),
    test_code TEXT NOT NULL,
    test_name TEXT NOT NULL,
    numeric_value REAL,
    string_value TEXT,
    unit TEXT,
    norm_min REAL,
    norm_max REAL,
    flag TEXT DEFAULT 'NORMAL', -- 'NORMAL', 'HIGH', 'LOW', 'CRIT_HIGH', 'CRIT_LOW', 'DELTA_ALERT'
    delta_percent REAL,
    analyzer_id TEXT REFERENCES lab_analyzers(id),
    is_auto_verified INTEGER DEFAULT 0,
    verified_by_id TEXT REFERENCES org_employee(id),
    verified_at TEXT,
    status TEXT NOT NULL DEFAULT 'PENDING' -- 'PENDING', 'AUTO_VERIFIED', 'MANUAL_VERIFIED', 'NEEDS_DOCTOR'
);

CREATE TABLE IF NOT EXISTS lab_panic_call_log (
    id TEXT PRIMARY KEY,
    result_id TEXT NOT NULL REFERENCES lab_test_results(id),
    doctor_notified_name TEXT NOT NULL,
    phone_called TEXT NOT NULL,
    notified_at TEXT DEFAULT CURRENT_TIMESTAMP,
    notified_by_id TEXT REFERENCES org_employee(id),
    comments TEXT
);

CREATE TABLE IF NOT EXISTS lab_sample_logistics (
    id TEXT PRIMARY KEY,
    manifest_number TEXT UNIQUE NOT NULL,
    origin_dept_id TEXT REFERENCES org_department(id),
    dest_dept_id TEXT REFERENCES org_department(id),
    courier_name TEXT,
    temperature_dispatch REAL,
    dispatched_at TEXT,
    temperature_receipt REAL,
    received_at TEXT,
    status TEXT DEFAULT 'DISPATCHED' -- 'DISPATCHED', 'IN_TRANSIT', 'RECEIVED'
);

CREATE TABLE IF NOT EXISTS lab_sample_logistics_items (
    id TEXT PRIMARY KEY,
    logistics_id TEXT NOT NULL REFERENCES lab_sample_logistics(id),
    sample_id TEXT NOT NULL REFERENCES lab_order_samples(id)
);

CREATE TABLE IF NOT EXISTS lab_qc_results (
    id TEXT PRIMARY KEY,
    analyzer_id TEXT NOT NULL REFERENCES lab_analyzers(id),
    control_material TEXT NOT NULL,
    lot_number TEXT NOT NULL,
    test_code TEXT NOT NULL,
    measured_value REAL NOT NULL,
    target_mean REAL NOT NULL,
    target_sd REAL NOT NULL,
    z_score REAL NOT NULL,
    is_violation INTEGER DEFAULT 0,
    violated_rule TEXT, -- '1-3s', '2-2s', 'R-4s'
    is_lockout INTEGER DEFAULT 0,
    lockout_resolved_at TEXT,
    resolution_action TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS lab_sample_archive_cells (
    id TEXT PRIMARY KEY,
    rack_code TEXT NOT NULL,
    box_number TEXT NOT NULL,
    cell_coordinate TEXT NOT NULL, -- e.g. 'C-05'
    sample_id TEXT REFERENCES lab_order_samples(id),
    stored_at TEXT DEFAULT CURRENT_TIMESTAMP,
    expiry_at TEXT
);
""")

# 3. SEED TEST PATIENTS & EMPLOYEES & REFERRALS
cur.executescript("""
INSERT OR REPLACE INTO mis_patient_card (id, last_name, first_name, second_name, birth_date, gender, phone, tax_id) VALUES
  ('PT-1001', 'Мельник', 'Юрій', 'Володимирович', '1982-04-12', 'MALE', '+380671112233', '2987102918'),
  ('PT-1002', 'Коваленко', 'Олена', 'Сергіївна', '1985-09-24', 'FEMALE', '+380504445566', '3128409124'),
  ('PT-1003', 'Бондаренко', 'Ігор', 'Васильович', '1976-11-03', 'MALE', '+380637778899', '2812401923');

INSERT OR REPLACE INTO org_department (id, name, code, department_type) VALUES
  ('DEPT-01', 'Пункт забору №1 (Поліклініка, каб. 104)', 'PZ-01', 'COLLECTION_ROOM'),
  ('DEPT-02', 'Центральна клініко-діагностична лабораторія (КДЛ)', 'KDL-CENTRAL', 'LABORATORY'),
  ('DEPT-03', 'Терапевтичне відділення №1 (Стаціонар)', 'TER-01', 'INPATIENT');

INSERT OR REPLACE INTO org_employee (id, full_name, position_name, speciality_name, digital_signature_cert_id) VALUES
  ('EMP-01', 'Мельник В.С.', 'Завідувач КДЛ (Лікар-лаборант)', 'Клінічна лабораторна діагностика', 'CERT-KEP-45AF12'),
  ('EMP-02', 'Коваленко О.В.', 'Лаборант КДЛ', 'Сестринська справа / Лабораторія', NULL),
  ('EMP-03', 'Іванов П.М.', 'Лікар-терапевт', 'Терапія', 'CERT-KEP-88BC99'),
  ('EMP-04', 'Петренко О.І.', 'Лікар-кардіолог', 'Кардіологія', 'CERT-KEP-99CD11');

INSERT OR REPLACE INTO ehe_incoming_medical_referral (id, referral_code, patient_id, service_code, service_name, requester_doctor_id, requester_organization, status) VALUES
  ('REF-2026-001', '01HJ-89A5-K2P8', 'PT-1001', '59300-00', 'Комплексне біохімічне дослідження крові (Глюкоза, Ниркові, Печінкові)', 'EMP-03', 'КНП "ЦМЛ" Поліклініка', 'NEW'),
  ('REF-2026-002', '01HJ-90B1-M3Q9', 'PT-1002', '59100-00', 'Розгорнутий загальний аналіз крові (ЗАК + формула + ШОЕ)', 'EMP-03', 'КНП "ЦМЛ" Терапія', 'NEW'),
  ('REF-2026-003', '01HJ-91C4-L5X2', 'PT-1003', '59500-00', 'Коагулограма (МНВ, ПЧ, Фібриноген)', 'EMP-04', 'КНП "ЦМЛ" Кардіологія', 'NEW');

INSERT OR REPLACE INTO lab_biomaterial_types (id, code, name) VALUES
  (1, 'BM_01', 'Цільна кров (капілярна)'),
  (2, 'BM_04', 'Сироватка венозної крові'),
  (3, 'BM_05', 'Плазма (Li-гепарин / Цитрат)'),
  (4, 'BM_07', 'Сеча');

INSERT OR REPLACE INTO lab_tube_types (id, code, name, color_code, volume_ml, order_of_draw_index) VALUES
  (1, 'TUBE_EDTA', 'K2 EDTA (Гематологія ЗАК)', '#9333ea', '3.0 мл', 4),
  (2, 'TUBE_GEL', 'Активатор згортання + Гель (Біохімія)', '#dc2626', '5.0 мл', 3),
  (3, 'TUBE_CITRATE', 'Цитрат натрію 3.2% (Коагулограма)', '#0284c7', '3.0 мл', 2),
  (4, 'TUBE_HEPARIN', 'Літій-гепарин (Електроліти)', '#16a34a', '4.0 мл', 4);

INSERT OR REPLACE INTO lab_analyzer_types (id, code, name, exch_type) VALUES
  (1, 'SYSMEX', 'Sysmex XN Series', 'ASTM'),
  (2, 'COBAS', 'Roche Cobas e411', 'ASTM'),
  (3, 'MINDRAY', 'Mindray BS Series', 'ASTM');

INSERT OR REPLACE INTO lab_analyzers (id, code, name, analyzer_type_id, connection_mode, ip_host, ip_port, is_online) VALUES
  ('ANZ-01', 'SYSMEX_XN1000', 'Sysmex XN-1000 (Гематологія)', 1, 'TCP', '192.168.1.50', 5100, 1),
  ('ANZ-02', 'COBAS_E411', 'Roche Cobas e411 (Імунохімія)', 2, 'TCP', '192.168.1.51', 5101, 1),
  ('ANZ-03', 'MINDRAY_BS240', 'Mindray BS-240 (Біохімія)', 3, 'COM', 'COM3', NULL, 1);

INSERT OR REPLACE INTO lab_test_definitions (id, code, name, loinc_code, unit, method_name, delta_check_max_pct) VALUES
  ('TEST-GLU', 'GLU', 'Глюкоза сироватки', '2345-7', 'ммоль/л', 'Гексокіназний', 30.0),
  ('TEST-ALT', 'ALT', 'Аланінамінотрансфераза (АЛТ)', '1742-6', 'U/L', 'Кінетичний IFCC', 40.0),
  ('TEST-CREAT', 'CREAT', 'Креатинін сироватки', '2160-0', 'мкмоль/л', 'Яффе кінетичний', 25.0),
  ('TEST-WBC', 'WBC', 'Лейкоцити (WBC)', '6690-2', '10^9/л', 'Проточна цитометрія', 30.0),
  ('TEST-HGB', 'HGB', 'Гемоглобін (HGB)', '718-7', 'г/л', 'SLS колориметричний', 15.0),
  ('TEST-PLT', 'PLT', 'Тромбоцити (PLT)', '777-3', '10^9/л', 'Кондуктометричний', 25.0);

INSERT OR REPLACE INTO lab_orders (id, order_number, patient_id, referral_id, doctor_id, department_id, order_datetime, status, is_urgent_cito, clinical_notes) VALUES
  ('ORD-01', '1026004812', 'PT-1001', 'REF-2026-001', 'EMP-03', 'DEPT-01', '2026-10-06 08:15:00', 'ANALYZING', 1, 'Цукровий діабет 2 типу, декомпенсація CITO!'),
  ('ORD-02', '1026004819', 'PT-1002', 'REF-2026-002', 'EMP-03', 'DEPT-01', '2026-10-06 08:30:00', 'ANALYZING', 0, 'Плановий профілактичний огляд'),
  ('ORD-03', '1026004820', 'PT-1003', 'REF-2026-003', 'EMP-04', 'DEPT-01', '2026-10-06 08:45:00', 'ANALYZING', 0, 'Контроль антикоагулянтної терапії');

INSERT OR REPLACE INTO lab_order_samples (id, order_id, barcode, tube_type_id, biomaterial_type_id, collected_at, status) VALUES
  ('SMP-01', 'ORD-01', '1026004812', 2, 2, '2026-10-06 08:20:00', 'RECEIVED'),
  ('SMP-02', 'ORD-02', '1026004819', 1, 1, '2026-10-06 08:35:00', 'RECEIVED'),
  ('SMP-03', 'ORD-03', '1026004820', 3, 3, '2026-10-06 08:50:00', 'RECEIVED');

INSERT OR REPLACE INTO lab_test_results (id, order_id, sample_id, test_code, test_name, numeric_value, unit, norm_min, norm_max, flag, delta_percent, analyzer_id, is_auto_verified, status) VALUES
  ('RES-01', 'ORD-01', 'SMP-01', 'GLU', 'Глюкоза сироватки', 26.4, 'ммоль/л', 4.1, 5.9, 'CRIT_HIGH', 185.0, 'ANZ-02', 0, 'NEEDS_DOCTOR'),
  ('RES-02', 'ORD-02', 'SMP-02', 'ALT', 'Аланінамінотрансфераза (АЛТ)', 68.5, 'U/L', 0, 41, 'HIGH', 42.7, 'ANZ-03', 0, 'NEEDS_DOCTOR'),
  ('RES-03', 'ORD-02', 'SMP-02', 'CREAT', 'Креатинін сироватки', 84.0, 'мкмоль/л', 62, 115, 'NORMAL', -2.1, 'ANZ-03', 1, 'AUTO_VERIFIED'),
  ('RES-04', 'ORD-02', 'SMP-02', 'WBC', 'Лейкоцити (WBC)', 7.45, '10^9/л', 4.0, 9.0, 'NORMAL', 1.2, 'ANZ-01', 1, 'AUTO_VERIFIED'),
  ('RES-05', 'ORD-02', 'SMP-02', 'HGB', 'Гемоглобін (HGB)', 148.0, 'г/л', 130, 160, 'NORMAL', 0.0, 'ANZ-01', 1, 'AUTO_VERIFIED');
""")

conn.commit()
conn.close()

print(f"Local SQLite database successfully initialized at: {DB_PATH}")
print(f"File size: {os.path.getsize(DB_PATH)} bytes.")
