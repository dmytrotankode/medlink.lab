import sqlite3

conn = sqlite3.connect('medlink_lab_local.db')
c = conn.cursor()
for t in ['lab_qc_results', 'lab_sample_archive_cells', 'lab_panic_call_log', 'lab_orders', 'lab_order_samples', 'lab_test_results', 'lab_reference_ranges', 'lab_analyzers']:
    c.execute(f"PRAGMA table_info({t})")
    print(f"=== {t} ===")
    for col in c.fetchall():
        print(f"  {col[1]} ({col[2]})")
conn.close()
