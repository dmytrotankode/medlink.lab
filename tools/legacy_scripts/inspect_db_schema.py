import sqlite3

conn = sqlite3.connect('medlink_lab_local.db')
cur = conn.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table';")
tables = [r[0] for r in cur.fetchall()]
print("Tables in medlink_lab_local.db:", tables)

for t in ['lab_test_definitions', 'lab_reference_ranges', 'lab_analyzers']:
    if t in tables:
        cur.execute(f"PRAGMA table_info({t});")
        cols = [c[1] for c in cur.fetchall()]
        print(f"Columns in {t}:", cols)
