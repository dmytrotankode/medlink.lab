# -*- coding: utf-8 -*-
import sqlite3
import os

DB_PATH = r"C:\__MEDLINK___\LABA\medlink_lab_local.db"
conn = sqlite3.connect(DB_PATH)
cur = conn.cursor()

# 1. Create lab_reference_ranges matching Delphi dct_service_lab_norm and dct_service_lab_nv
cur.executescript("""
CREATE TABLE IF NOT EXISTS lab_reference_ranges (
    id TEXT PRIMARY KEY,
    test_code TEXT NOT NULL REFERENCES lab_test_definitions(code),
    norm_name TEXT NOT NULL,
    gender TEXT DEFAULT 'ANY', -- 'M', 'F', 'ANY'
    is_gender INTEGER DEFAULT 0,
    age_unit TEXT DEFAULT 'YEARS', -- 'YEARS', 'MONTHS', 'DAYS'
    age_from INTEGER DEFAULT 0,
    age_to INTEGER DEFAULT 120,
    is_age INTEGER DEFAULT 1,
    menstrual_phase TEXT, -- 'FOLLICULAR', 'OVULATORY', 'LUTEAL', 'POSTMENOPAUSE'
    is_menstrual_phase INTEGER DEFAULT 0,
    pregnancy_week_from INTEGER,
    pregnancy_week_to INTEGER,
    is_pregnancy INTEGER DEFAULT 0,
    method_name TEXT,
    norm_low REAL,
    norm_high REAL,
    crit_low REAL,
    crit_high REAL,
    norm_text TEXT,
    unit TEXT,
    delta_check_max_pct REAL,
    is_active INTEGER DEFAULT 1,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS lab_reflex_rules (
    id TEXT PRIMARY KEY,
    trigger_test_code TEXT NOT NULL,
    condition_operator TEXT NOT NULL, -- 'GT', 'LT', 'BETWEEN', 'EQ'
    threshold_value REAL,
    reflex_test_code TEXT NOT NULL,
    reflex_test_name TEXT NOT NULL,
    description TEXT NOT NULL
);
""")

# 2. Seed realistic Delphi-level combinations
combinations = [
    # --- GLUCOSE (GLU) ---
    ('REF-GLU-01', 'GLU', 'Дорослі (18-59 років)', 'ANY', 0, 'YEARS', 18, 59, 1, None, 0, None, None, 0, 'Гексокіназний IFCC', 4.10, 5.90, 2.50, 25.00, None, 'ммоль/л', 25.0),
    ('REF-GLU-02', 'GLU', 'Похилий вік (60-120 років)', 'ANY', 0, 'YEARS', 60, 120, 1, None, 0, None, None, 0, 'Гексокіназний IFCC', 4.40, 6.40, 2.50, 25.00, None, 'ммоль/л', 25.0),
    ('REF-GLU-03', 'GLU', 'Діти та підлітки (1-17 років)', 'ANY', 0, 'YEARS', 1, 17, 1, None, 0, None, None, 0, 'Гексокіназний IFCC', 3.30, 5.50, 2.20, 22.00, None, 'ммоль/л', 30.0),
    ('REF-GLU-04', 'GLU', 'Новонароджені (0-28 днів)', 'ANY', 0, 'DAYS', 0, 28, 1, None, 0, None, None, 0, 'Гексокіназний IFCC', 2.80, 4.40, 1.70, 16.00, None, 'ммоль/л', 40.0),
    ('REF-GLU-05', 'GLU', 'Вагітність (1-40 тижнів, критерій ГСД)', 'F', 1, 'YEARS', 15, 50, 0, None, 0, 1, 40, 1, 'Гексокіназний IFCC', 3.50, 5.10, 2.50, 15.00, 'Межа ГСД 5.1', 'ммоль/л', 20.0),

    # --- PROGESTERONE (PROG) ---
    ('REF-PROG-01', 'PROG', 'Жінки: Фолікулярна фаза', 'F', 1, 'YEARS', 16, 55, 1, 'FOLLICULAR', 1, None, None, 0, 'ІХЛА (Roche Cobas)', 0.18, 2.84, None, None, None, 'нмоль/л', 50.0),
    ('REF-PROG-02', 'PROG', 'Жінки: Овуляторний пік', 'F', 1, 'YEARS', 16, 55, 1, 'OVULATORY', 1, None, None, 0, 'ІХЛА (Roche Cobas)', 0.38, 38.10, None, None, None, 'нмоль/л', 50.0),
    ('REF-PROG-03', 'PROG', 'Жінки: Лютеїнова фаза', 'F', 1, 'YEARS', 16, 55, 1, 'LUTEAL', 1, None, None, 0, 'ІХЛА (Roche Cobas)', 5.82, 75.90, None, None, None, 'нмоль/л', 50.0),
    ('REF-PROG-04', 'PROG', 'Жінки: Постменопауза', 'F', 1, 'YEARS', 50, 120, 1, 'POSTMENOPAUSE', 1, None, None, 0, 'ІХЛА (Roche Cobas)', 0.13, 1.26, None, None, None, 'нмоль/л', 40.0),
    ('REF-PROG-05', 'PROG', 'Вагітність: I триместр (1-13 тиж)', 'F', 1, 'YEARS', 16, 50, 0, None, 0, 1, 13, 1, 'ІХЛА (Roche Cobas)', 35.60, 286.20, 20.0, None, None, 'нмоль/л', 40.0),
    ('REF-PROG-06', 'PROG', 'Вагітність: II триместр (14-27 тиж)', 'F', 1, 'YEARS', 16, 50, 0, None, 0, 14, 27, 1, 'ІХЛА (Roche Cobas)', 81.30, 284.30, 40.0, None, None, 'нмоль/л', 40.0),
    ('REF-PROG-07', 'PROG', 'Вагітність: III триместр (28-40 тиж)', 'F', 1, 'YEARS', 16, 50, 0, None, 0, 28, 40, 1, 'ІХЛА (Roche Cobas)', 153.90, 1343.50, 60.0, None, None, 'нмоль/л', 40.0),
    ('REF-PROG-08', 'PROG', 'Чоловіки дорослі', 'M', 1, 'YEARS', 18, 120, 1, None, 0, None, None, 0, 'ІХЛА (Roche Cobas)', 0.28, 0.65, None, None, None, 'нмоль/л', 30.0),

    # --- TSH / ТТГ ---
    ('REF-TSH-01', 'TSH', 'Дорослі (18-60 років)', 'ANY', 0, 'YEARS', 18, 60, 1, None, 0, None, None, 0, 'Хемілюмінесцентний ECLIA', 0.40, 4.00, 0.05, 20.00, None, 'мкМО/мл', 35.0),
    ('REF-TSH-02', 'TSH', 'Вагітність: I триместр (1-13 тиж)', 'F', 1, 'YEARS', 16, 50, 0, None, 0, 1, 13, 1, 'Хемілюмінесцентний ECLIA', 0.10, 2.50, 0.02, 15.00, None, 'мкМО/мл', 35.0),
    ('REF-TSH-03', 'TSH', 'Вагітність: II триместр (14-27 тиж)', 'F', 1, 'YEARS', 16, 50, 0, None, 0, 14, 27, 1, 'Хемілюмінесцентний ECLIA', 0.20, 3.00, 0.05, 15.00, None, 'мкМО/мл', 35.0),
    ('REF-TSH-04', 'TSH', 'Вагітність: III триместр (28-40 тиж)', 'F', 1, 'YEARS', 16, 50, 0, None, 0, 28, 40, 1, 'Хемілюмінесцентний ECLIA', 0.30, 3.00, 0.05, 15.00, None, 'мкМО/мл', 35.0),
    ('REF-TSH-05', 'TSH', 'Новонароджені (0-4 дні)', 'ANY', 0, 'DAYS', 0, 4, 1, None, 0, None, None, 0, 'Хемілюмінесцентний ECLIA', 1.00, 18.00, 0.10, 35.00, None, 'мкМО/мл', 50.0),

    # --- HEMOGLOBIN (HGB) ---
    ('REF-HGB-01', 'HGB', 'Чоловіки дорослі (18-65 років)', 'M', 1, 'YEARS', 18, 65, 1, None, 0, None, None, 0, 'SLS безціанідний (Sysmex)', 130.0, 160.0, 70.0, 200.0, None, 'г/л', 15.0),
    ('REF-HGB-02', 'HGB', 'Жінки дорослі (18-65 років)', 'F', 1, 'YEARS', 18, 65, 1, None, 0, None, None, 0, 'SLS безціанідний (Sysmex)', 120.0, 140.0, 65.0, 190.0, None, 'г/л', 15.0),
    ('REF-HGB-03', 'HGB', 'Вагітні (1-40 тижнів)', 'F', 1, 'YEARS', 15, 50, 0, None, 0, 1, 40, 1, 'SLS безціанідний (Sysmex)', 110.0, 130.0, 60.0, 180.0, None, 'г/л', 15.0),
    ('REF-HGB-04', 'HGB', 'Новонароджені (0-28 днів)', 'ANY', 0, 'DAYS', 0, 28, 1, None, 0, None, None, 0, 'SLS безціанідний (Sysmex)', 145.0, 225.0, 100.0, 240.0, None, 'г/л', 20.0),
    ('REF-HGB-05', 'HGB', 'Діти (1-6 років)', 'ANY', 0, 'YEARS', 1, 6, 1, None, 0, None, None, 0, 'SLS безціанідний (Sysmex)', 110.0, 135.0, 60.0, 175.0, None, 'г/л', 20.0),

    # --- ESR / ШОЕ ---
    ('REF-ESR-01', 'ESR', 'Чоловіки (18-50 років)', 'M', 1, 'YEARS', 18, 50, 1, None, 0, None, None, 0, 'Панченков / Вестергрен', 2.0, 10.0, None, 60.0, None, 'мм/год', 30.0),
    ('REF-ESR-02', 'ESR', 'Чоловіки (>50 років)', 'M', 1, 'YEARS', 51, 120, 1, None, 0, None, None, 0, 'Панченков / Вестергрен', 2.0, 15.0, None, 65.0, None, 'мм/год', 30.0),
    ('REF-ESR-03', 'ESR', 'Жінки (18-50 років)', 'F', 1, 'YEARS', 18, 50, 1, None, 0, None, None, 0, 'Панченков / Вестергрен', 2.0, 15.0, None, 65.0, None, 'мм/год', 30.0),
    ('REF-ESR-04', 'ESR', 'Жінки (>50 років)', 'F', 1, 'YEARS', 51, 120, 1, None, 0, None, None, 0, 'Панченков / Вестергрен', 2.0, 20.0, None, 70.0, None, 'мм/год', 30.0),
    ('REF-ESR-05', 'ESR', 'Вагітність (II-III триместр)', 'F', 1, 'YEARS', 15, 50, 0, None, 0, 14, 40, 1, 'Панченков / Вестергрен', 10.0, 45.0, None, 80.0, None, 'мм/год', 30.0)
]

for comb in combinations:
    cur.execute("""
        INSERT OR REPLACE INTO lab_reference_ranges (
            id, test_code, norm_name, gender, is_gender, age_unit, age_from, age_to, is_age,
            menstrual_phase, is_menstrual_phase, pregnancy_week_from, pregnancy_week_to, is_pregnancy,
            method_name, norm_low, norm_high, crit_low, crit_high, norm_text, unit, delta_check_max_pct
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """, comb)

# Seed Reflex rules
reflex_rules = [
    ('RFLX-01', 'TSH', 'GT', 4.00, 'T4_FREE', 'Вільний тироксин (Т4 вільний)', 'При TSH > 4.00 мкМО/мл автоматично дозамовити T4_FREE для диференціації гіпотиреозу'),
    ('RFLX-02', 'GLU', 'GT', 15.00, 'GLU_URINE', 'Глюкоза сечі та кетонові тіла', 'При глікемії > 15.0 ммоль/л призначити кетони для виключення кетоацидозу'),
    ('RFLX-03', 'WBC', 'GT', 30.00, 'BLOOD_SMEAR', 'Ручна мікроскопія лейкоцитарної формули', 'При гіперлейкоцитозі > 30x10^9/л обов`язковий ручний перегляд мазка лікарем-лаборантом')
]

for rflx in reflex_rules:
    cur.execute("""
        INSERT OR REPLACE INTO lab_reflex_rules (
            id, trigger_test_code, condition_operator, threshold_value, reflex_test_code, reflex_test_name, description
        ) VALUES (?, ?, ?, ?, ?, ?, ?)
    """, rflx)

conn.commit()
conn.close()

print("Successfully created and seeded lab_reference_ranges and lab_reflex_rules in medlink_lab_local.db!")
