# -*- coding: utf-8 -*-
import sqlite3

conn = sqlite3.connect('medlink_lab_local.db')
c = conn.cursor()

# 1. Add columns to lab_reference_ranges if missing
c.execute("PRAGMA table_info(lab_reference_ranges)")
existing_cols = [col[1] for col in c.fetchall()]

new_cols = [
    ("layer_type", "TEXT DEFAULT 'DEMOGRAPHIC'"),
    ("priority_order", "INTEGER DEFAULT 40"),
    ("icd10_code", "TEXT"),
    ("method_code", "TEXT DEFAULT 'HEX_IFCC'")
]

for col_name, col_def in new_cols:
    if col_name not in existing_cols:
        c.execute(f"ALTER TABLE lab_reference_ranges ADD COLUMN {col_name} {col_def}")
        print(f"Added column {col_name} to lab_reference_ranges")

# 2. Update existing rows with proper layer types and priorities
c.execute("""
    UPDATE lab_reference_ranges
    SET layer_type = CASE
        WHEN is_pregnancy = 1 THEN 'PREGNANCY'
        WHEN is_menstrual_phase = 1 THEN 'MENSTRUAL_PHASE'
        WHEN is_gender = 0 AND is_age = 0 THEN 'BASELINE'
        ELSE 'DEMOGRAPHIC'
    END,
    priority_order = CASE
        WHEN is_pregnancy = 1 THEN 100
        WHEN is_menstrual_phase = 1 THEN 80
        WHEN is_gender = 0 AND is_age = 0 THEN 10
        ELSE 40
    END,
    method_code = 'HEX_IFCC'
    WHERE method_code IS NULL OR method_code = ''
""")

# 3. Add explicit rich layered cascades for GLU (Glucose)
glu_layers = [
    # Layer 0: Baseline
    ("REF-GLU-L0-BASE", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "BASELINE", 10,
     "Базова норма IFCC (Клінічний оптимум)", "ANY", 0, "YEARS", 0, 120, 0,
     0, None, 0, None, None, None, 4.10, 5.90, 2.50, 25.00, "Базовий референтний інтервал IFCC", "ммоль/л", 25.0),

    # Layer 1: Demographic age slices
    ("REF-GLU-L1-CHILD", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "DEMOGRAPHIC", 40,
     "Діти від народження до 14 років", "ANY", 0, "YEARS", 0, 14, 1,
     0, None, 0, None, None, None, 3.30, 5.60, 2.20, 22.00, "Фізіологічна норма дитячого віку", "ммоль/л", 20.0),

    ("REF-GLU-L1-ADULT-M", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "DEMOGRAPHIC", 40,
     "Чоловіки дорослі (15-64 роки)", "M", 1, "YEARS", 15, 64, 1,
     0, None, 0, None, None, None, 4.10, 5.90, 2.50, 25.00, "Референс дорослих чоловіків", "ммоль/л", 25.0),

    ("REF-GLU-L1-ADULT-F", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "DEMOGRAPHIC", 40,
     "Жінки дорослі (15-64 роки)", "F", 1, "YEARS", 15, 64, 1,
     0, None, 0, None, None, None, 4.10, 5.90, 2.50, 25.00, "Референс дорослих жінок", "ммоль/л", 25.0),

    ("REF-GLU-L1-ELDERLY", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "DEMOGRAPHIC", 40,
     "Особи похилого віку (65+ років)", "ANY", 0, "YEARS", 65, 120, 1,
     0, None, 0, None, None, None, 4.40, 6.40, 2.80, 25.00, "Вікове зниження толерантності до глюкози", "ммоль/л", 25.0),

    # Layer 2: Menstrual Phase Overrides
    ("REF-GLU-L2-FOLLICULAR", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "MENSTRUAL_PHASE", 80,
     "Фолікулярна фаза (Жінки)", "F", 1, "YEARS", 15, 55, 1,
     1, "FOLLICULAR", 0, None, None, None, 3.90, 5.80, 2.50, 25.00, "Фолікулярна фаза циклу", "ммоль/л", 25.0),

    ("REF-GLU-L2-LUTEAL", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "MENSTRUAL_PHASE", 80,
     "Лютеїнова фаза (Жінки)", "F", 1, "YEARS", 15, 55, 1,
     1, "LUTEAL", 0, None, None, None, 3.80, 5.70, 2.50, 25.00, "Лютеїнова фаза циклу (вплив прогестерону)", "ммоль/л", 25.0),

    # Layer 3: Pregnancy Trimester Overrides (Highest priority 100)
    ("REF-GLU-L3-PREG-T1", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "PREGNANCY", 100,
     "Вагітні: 1-й триместр (1-13 тиж.)", "F", 1, "YEARS", 15, 50, 1,
     0, None, 1, 1, 13, None, 3.50, 5.10, 2.20, 18.00, "1 триместр вагітності (гестаційна норма)", "ммоль/л", 15.0),

    ("REF-GLU-L3-PREG-T2", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "PREGNANCY", 100,
     "Вагітні: 2-й триместр (14-27 тиж.)", "F", 1, "YEARS", 15, 50, 1,
     0, None, 1, 14, 27, None, 3.50, 5.30, 2.20, 18.00, "2 триместр вагітності (тест толерантності)", "ммоль/л", 15.0),

    ("REF-GLU-L3-PREG-T3", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "PREGNANCY", 100,
     "Вагітні: 3-й триместр (28-42 тиж.)", "F", 1, "YEARS", 15, 50, 1,
     0, None, 1, 28, 42, None, 3.80, 5.50, 2.40, 20.00, "3 триместр вагітності (інсулінорезистентність плаценти)", "ммоль/л", 15.0),

    # Layer 4: Clinical ICD-10 Cohort Target
    ("REF-GLU-L4-DIABETES", "GLU", "HEX_IFCC", "Гексокіназний IFCC", "CLINICAL_ICD10", 60,
     "Когорта: ЦД 2 типу (E11) — Цільовий коридор", "ANY", 0, "YEARS", 18, 120, 1,
     0, None, 0, None, None, "E11", 4.00, 7.20, 2.80, 25.00, "Цільовий компенсаторний діапазон для хворих на діабет", "ммоль/л", 30.0),

    # Alternate Methodology: GOD-PAP (Colorimetric)
    ("REF-GLU-GOD-BASE", "GLU", "GOD_PAP", "Глюкозооксидазний GOD-PAP", "BASELINE", 10,
     "Базова норма GOD-PAP", "ANY", 0, "YEARS", 0, 120, 0,
     0, None, 0, None, None, None, 3.89, 5.83, 2.50, 25.00, "Колориметричний метод GOD-PAP", "ммоль/л", 25.0),

    ("REF-GLU-GOD-PREG", "GLU", "GOD_PAP", "Глюкозооксидазний GOD-PAP", "PREGNANCY", 100,
     "Вагітність: GOD-PAP", "F", 1, "YEARS", 15, 50, 1,
     0, None, 1, 1, 42, None, 3.33, 5.00, 2.20, 18.00, "Вагітні (метод GOD-PAP)", "ммоль/л", 15.0),
]

for row in glu_layers:
    c.execute("""
        INSERT OR REPLACE INTO lab_reference_ranges (
            id, test_code, method_code, method_name, layer_type, priority_order,
            norm_name, gender, is_gender, age_unit, age_from, age_to, is_age,
            is_menstrual_phase, menstrual_phase, is_pregnancy, pregnancy_week_from, pregnancy_week_to, icd10_code,
            norm_low, norm_high, crit_low, crit_high, norm_text, unit, delta_check_max_pct, is_active
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1)
    """, row)

conn.commit()
print(f"Successfully configured layered reference norms! Total ranges: {c.execute('SELECT COUNT(*) FROM lab_reference_ranges').fetchone()[0]}")
conn.close()
