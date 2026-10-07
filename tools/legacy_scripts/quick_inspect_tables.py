# -*- coding: utf-8 -*-
"""
Quick inspection of evomis-test database tables (Read-Only).
Safe, fast, statement_timeout=5000, sys.stdout.flush().
"""

import psycopg2
import sys

conn_params = {
    'host': '192.168.255.1',
    'port': 5432,
    'dbname': 'evomis-test',
    'user': 'd.tanko',
    'password': r'u37[kDm4f=.*{49j=S\!.O',
    'connect_timeout': 5
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

try:
    conn = psycopg2.connect(**conn_params)
    conn.set_session(readonly=True)
    cur = conn.cursor()
    cur.execute("SET statement_timeout = 5000;")
    print("Connected to evomis-test. Inspecting tables...")
    sys.stdout.flush()

    for tbl in target_tables:
        cur.execute("""
            SELECT table_name 
            FROM information_schema.tables 
            WHERE table_schema = 'public' AND table_name ILIKE %s
        """, (tbl,))
        found = cur.fetchone()
        if not found:
            print(f"\n[TABLE NOT FOUND: {tbl}]")
            sys.stdout.flush()
            continue

        actual_name = found[0]
        print(f"\n=== TABLE: public.\"{actual_name}\" ===")
        
        cur.execute("""
            SELECT column_name, data_type, is_nullable, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = %s
            ORDER BY ordinal_position;
        """, (actual_name,))
        cols = cur.fetchall()
        for c in cols:
            max_len = f"({c[3]})" if c[3] else ""
            null = "NULL" if c[2] == 'YES' else "NOT NULL"
            print(f"  {c[0]:32} {c[1]}{max_len:8} {null}")
        
        # Fetch up to 1 sample row using LIMIT 1
        try:
            cur.execute(f'SELECT * FROM public."{actual_name}" LIMIT 1;')
            sample = cur.fetchone()
            if sample:
                col_names = [d[0] for d in cur.description]
                print("  Sample row:")
                for k, v in zip(col_names[:6], sample[:6]):
                    print(f"    {k}: {str(v)[:40]}")
            else:
                print("  (Table is currently empty)")
        except Exception as err:
            print(f"  (Could not read sample row: {err})")
            conn.rollback()
            cur.execute("SET statement_timeout = 5000;")
        sys.stdout.flush()

    cur.close()
    conn.close()
    print("\nInspection finished successfully.")
    sys.stdout.flush()

except Exception as e:
    print("Error:", e)
    sys.stdout.flush()
