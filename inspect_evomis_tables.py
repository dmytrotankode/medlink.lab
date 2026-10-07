# -*- coding: utf-8 -*-
"""
Deep inspection of evomis-test database tables (Read-Only).
Inspect schema, columns, primary keys, foreign keys, and existing patterns.
"""

import psycopg2

conn_params = {
    'host': '192.168.255.1',
    'port': 5432,
    'dbname': 'evomis-test',
    'user': 'd.tanko',
    'password': r'u37[kDm4f=.*{49j=S\!.O',
    'connect_timeout': 10
}

target_tables = [
    'mis_specimen',
    'mis_diagnostic_report',
    'service_catalog_observation_loinc',
    'mis_simplex_exec_lab_service',
    'mis_simplex_exec_lab_service_attribute',
    'mis_simplex_lab_referral_service_transfer',
    'mis_service',
    'ter_referrals',
    'ehe_incoming_medical_referral',
    'Patients',
    'mis_patient_card'
]

conn = psycopg2.connect(**conn_params)
conn.set_session(readonly=True)
cur = conn.cursor()

print("="*80)
print("EVOMIS-TEST DATABASE INSPECTION (READ-ONLY)")
print("="*80)

for tbl in target_tables:
    # Check if table exists (case-sensitive check)
    cur.execute("""
        SELECT table_name 
        FROM information_schema.tables 
        WHERE table_schema = 'public' AND table_name ILIKE %s
    """, (tbl,))
    found = cur.fetchone()
    if not found:
        print(f"\n[TABLE NOT FOUND: {tbl}]")
        continue
    
    actual_table_name = found[0]
    print(f"\nTABLE: public.\"{actual_table_name}\"")
    print("-" * 60)
    
    # Columns
    cur.execute("""
        SELECT column_name, data_type, is_nullable, character_maximum_length, column_default
        FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = %s
        ORDER BY ordinal_position;
    """, (actual_table_name,))
    cols = cur.fetchall()
    for col in cols:
        name, dtype, nullable, max_len, default = col
        len_str = f"({max_len})" if max_len else ""
        null_str = "NULL" if nullable == 'YES' else "NOT NULL"
        def_str = f" DEFAULT {default[:30]}" if default else ""
        print(f"  {name:35} {dtype}{len_str:10} {null_str:10}{def_str}")

    # Check row count
    try:
        cur.execute(f'SELECT COUNT(*) FROM public."{actual_table_name}";')
        cnt = cur.fetchone()[0]
        print(f"  --> Row count: {cnt}")
        
        # If rows exist, fetch sample row (1 row) to see actual values
        if cnt > 0:
            cur.execute(f'SELECT * FROM public."{actual_table_name}" LIMIT 1;')
            sample = cur.fetchone()
            col_names = [desc[0] for desc in cur.description]
            print("  --> Sample record:")
            for cn, val in zip(col_names[:8], sample[:8]):
                val_str = str(val)[:50] if val is not None else "NULL"
                print(f"      {cn}: {val_str}")
    except Exception as e:
        print(f"  --> Could not count rows: {e}")

cur.close()
conn.close()
print("\nInspection completed successfully.")
