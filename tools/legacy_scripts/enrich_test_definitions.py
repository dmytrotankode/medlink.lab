# -*- coding: utf-8 -*-
import sqlite3

conn = sqlite3.connect('medlink_lab_local.db')
cur = conn.cursor()

tests = [
    ('TEST-PROG', 'PROG', 'Прогестерон (Progesterone)', '2839-9', 'нмоль/л', 'ІХЛА (Roche Cobas)', 40.0, 1),
    ('TEST-TSH', 'TSH', 'Тиреотропний гормон (ТТГ / TSH)', '3016-3', 'мкМО/мл', 'Хемілюмінесцентний ECLIA', 35.0, 1),
    ('TEST-ESR', 'ESR', 'Швидкість осідання еритроцитів (ШОЕ)', '30341-2', 'мм/год', 'Панченков / Вестергрен', 30.0, 1),
    ('TEST-BHCG', 'BETA_HCG', 'Хоріонічний гонадотропін людини (Бета-ХГЛ)', '21198-7', 'мМО/мл', 'ІХЛА (Roche Cobas)', 50.0, 1),
    ('TEST-T4F', 'T4_FREE', 'Вільний тироксин (Т4 вільний)', '3024-7', 'пмоль/л', 'ІХЛА (Roche Cobas)', 25.0, 1)
]

for t in tests:
    cur.execute("""
        INSERT OR REPLACE INTO lab_test_definitions (id, code, name, loinc_code, unit, method_name, delta_check_max_pct, is_active)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
    """, t)

conn.commit()
conn.close()
print("Successfully enriched lab_test_definitions!")
