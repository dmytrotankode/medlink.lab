import sqlite3
import datetime

conn = sqlite3.connect('medlink_lab_local.db')
c = conn.cursor()

# 1. Seed QC Results
c.execute("DELETE FROM lab_qc_results")

# WBC 20 points
wbc_points = [
    (1, 7.18, 0, None, 0),
    (2, 7.22, 0, None, 0),
    (3, 7.15, 0, None, 0),
    (4, 7.28, 0, None, 0),
    (5, 7.20, 0, None, 0),
    (6, 7.32, 0, None, 0),
    (7, 7.19, 0, None, 0),
    (8, 7.25, 0, None, 0),
    (9, 7.11, 0, None, 0),
    (10, 7.29, 0, None, 0),
    (11, 7.21, 0, None, 0),
    (12, 7.35, 0, None, 0),
    (13, 7.18, 0, None, 0),
    (14, 7.24, 0, None, 0),
    (15, 7.30, 0, None, 0),
    (16, 7.26, 0, None, 0),
    (17, 7.42, 0, None, 0),
    (18, 8.28, 1, '1-3s (LOCK)', 1), # Lockout violation
    (19, 7.25, 0, None, 0),
    (20, 7.20, 0, None, 0),
]

for day, val, is_viol, rule, is_lock in wbc_points:
    mean = 7.20
    sd = 0.30
    z = round((val - mean) / sd, 2)
    dt = f"2026-10-{day:02d} 07:45:00"
    c.execute("""
        INSERT INTO lab_qc_results (
            id, analyzer_id, control_material, lot_number, test_code,
            measured_value, target_mean, target_sd, z_score,
            is_violation, violated_rule, is_lockout, created_at
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """, (
        f"QC-WBC-{day:02d}", "AN-01", "Bio-Rad Lyphochek Level 2", "LOT-88412", "WBC",
        val, mean, sd, z, is_viol, rule, is_lock, dt
    ))

# 2. Seed Archive Cells
c.execute("DELETE FROM lab_sample_archive_cells")
cells_data = [
    ("CELL-01", "RACK-A1", "BOX-01", "A-01", "1026004810", "2026-10-01 10:00:00", "2027-04-01 10:00:00"),
    ("CELL-02", "RACK-A1", "BOX-01", "A-05", "1026004811", "2026-10-02 11:30:00", "2027-04-02 11:30:00"),
    ("CELL-03", "RACK-A1", "BOX-01", "B-03", "1026004815", "2026-10-03 09:15:00", "2027-04-03 09:15:00"),
    ("CELL-04", "RACK-A1", "BOX-01", "C-05", "1026004812", "2026-10-06 09:15:00", "2027-04-06 09:15:00"),
    ("CELL-05", "RACK-A1", "BOX-01", "D-08", "1026004818", "2026-10-04 14:20:00", "2027-04-04 14:20:00"),
    ("CELL-06", "RACK-A1", "BOX-01", "F-02", "1026004822", "2026-10-05 08:45:00", "2027-04-05 08:45:00"),
    ("CELL-07", "RACK-A1", "BOX-01", "G-11", "1026004825", "2026-10-05 16:10:00", "2027-04-05 16:10:00"),
    ("CELL-08", "RACK-A1", "BOX-01", "H-12", "1026004830", "2026-10-06 12:00:00", "2027-04-06 12:00:00"),
]
for cid, rack, box, coord, smp, stored, exp in cells_data:
    c.execute("""
        INSERT INTO lab_sample_archive_cells (id, rack_code, box_number, cell_coordinate, sample_id, stored_at, expiry_at)
        VALUES (?, ?, ?, ?, ?, ?, ?)
    """, (cid, rack, box, coord, smp, stored, exp))

# 3. Seed Panic Call Log
c.execute("DELETE FROM lab_panic_call_log")
c.execute("""
    INSERT INTO lab_panic_call_log (id, result_id, doctor_notified_name, phone_called, notified_at, notified_by_id, comments)
    VALUES ('CALL-01', 'RES-01', 'Іванчук М.П. (Лікар стаціонару)', '+380501234567', '2026-10-06 10:15:00', 'EMP-01', 'Повідомлено про критичну гіперглікемію 26.4 ммоль/л. Прийнято в роботу.')
""")

# 4. Additional Orders & Results for real worklist
c.execute("""
    INSERT OR IGNORE INTO mis_patient_card (id, last_name, first_name, second_name, birth_date, gender, tax_id, phone)
    VALUES
    ('PT-1002', 'Шевченко', 'Оксана', 'Петрівна', '1985-04-12', 'F', '3112233445', '+380671112233'),
    ('PT-1003', 'Бондаренко', 'Михайло', 'Ігорович', '1970-11-28', 'M', '2566778899', '+380934445566'),
    ('PT-1004', 'Коваль', 'Ганна', 'Василівна', '1998-09-03', 'F', '3600112233', '+380507778899')
""")

c.execute("""
    INSERT OR IGNORE INTO lab_orders (id, order_number, patient_id, referral_id, doctor_id, department_id, order_datetime, status, is_urgent_cito, clinical_notes)
    VALUES
    ('ORD-02', 'ORD-2026-104813', 'PT-1002', 'REF-2026-002', 'EMP-01', 'DEPT-01', '2026-10-06 08:30:00', 'ANALYZING', 0, 'Плановий огляд'),
    ('ORD-03', 'ORD-2026-104814', 'PT-1003', 'REF-2026-003', 'EMP-02', 'DEPT-01', '2026-10-06 09:00:00', 'COLLECTED', 1, 'CITO! Підозра на інфаркт міокарда'),
    ('ORD-04', 'ORD-2026-104815', 'PT-1004', 'REF-2026-004', 'EMP-01', 'DEPT-01', '2026-10-06 09:20:00', 'COMPLETED', 0, 'Контроль після терапії')
""")

c.execute("""
    INSERT OR IGNORE INTO lab_order_samples (id, order_id, barcode, tube_type_id, biomaterial_type_id, collected_at, status)
    VALUES
    ('SMP-02', 'ORD-02', '1026004813', 1, 1, '2026-10-06 08:45:00', 'RECEIVED'),
    ('SMP-03', 'ORD-03', '1026004814', 2, 2, '2026-10-06 09:10:00', 'COLLECTED'),
    ('SMP-04', 'ORD-04', '1026004815', 3, 1, '2026-10-06 09:30:00', 'COMPLETED')
""")

c.execute("""
    INSERT OR IGNORE INTO lab_test_results (id, order_id, sample_id, test_code, test_name, numeric_value, unit, norm_min, norm_max, flag, delta_percent, analyzer_id, is_auto_verified, status)
    VALUES
    ('RES-02', 'ORD-02', 'SMP-02', 'WBC', 'Лейкоцити (WBC)', 6.8, '10^9/л', 4.0, 9.0, 'NORMAL', 4.5, 'AN-01', 1, 'AUTO_VERIFIED'),
    ('RES-03', 'ORD-02', 'SMP-02', 'HGB', 'Гемоглобін (HGB)', 138.0, 'г/л', 120.0, 160.0, 'NORMAL', -2.1, 'AN-01', 1, 'AUTO_VERIFIED'),
    ('RES-04', 'ORD-03', 'SMP-03', 'TROP_I', 'Тропонін I', 1.85, 'нг/мл', 0.0, 0.04, 'CRIT_HIGH', 320.0, 'AN-02', 0, 'NEEDS_DOCTOR'),
    ('RES-05', 'ORD-04', 'SMP-04', 'ALT', 'АЛТ (Аланінамінотрансфераза)', 28.0, 'Од/л', 0.0, 33.0, 'NORMAL', 1.2, 'AN-02', 1, 'COMPLETED')
""")

conn.commit()
print("Successfully seeded QC results, archive cells, panic logs, and orders!")
conn.close()
