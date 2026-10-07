# -*- coding: utf-8 -*-
"""
Inspect evomis-test PostgreSQL database (Read-Only).
Host: 192.168.255.1:5432
DB: evomis-test
User: d.tanko
"""

import psycopg2
import sys

conn_params = {
    'host': '192.168.255.1',
    'port': 5432,
    'dbname': 'evomis-test',
    'user': 'd.tanko',
    'password': r'u37[kDm4f=.*{49j=S\!.O',
    'connect_timeout': 10
}

try:
    print("Attempting to connect to evomis-test...")
    conn = psycopg2.connect(**conn_params)
    conn.set_session(readonly=True)
    cur = conn.cursor()
    cur.execute("SELECT current_database(), current_user, version();")
    row = cur.fetchone()
    print("CONNECTED SUCCESSFULLY:")
    print("DB:", row[0])
    print("User:", row[1])
    print("Version:", row[2][:50])

    # Schemas
    cur.execute("SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('information_schema', 'pg_catalog') ORDER BY schema_name;")
    schemas = [r[0] for r in cur.fetchall()]
    print("\nSchemas found:", schemas)

    # Tables in all non-system schemas
    cur.execute("""
        SELECT table_schema, table_name 
        FROM information_schema.tables 
        WHERE table_schema NOT IN ('information_schema', 'pg_catalog') 
          AND table_type = 'BASE TABLE'
        ORDER BY table_schema, table_name;
    """)
    tables = cur.fetchall()
    print(f"\nTotal tables found: {len(tables)}")
    
    # Check for any existing lab or analyzer or examination tables
    print("\nInteresting tables matching lab/analyzer/order/patient/service/examination/diagnostic:")
    keywords = ['lab', 'anal', 'order', 'patient', 'service', 'exam', 'diagnos', 'referral', 'specimen', 'sample', 'device', 'equip']
    for schema, table in tables:
        t_lower = table.lower()
        if any(k in t_lower for k in keywords):
            print(f" - {schema}.{table}")

    cur.close()
    conn.close()
    print("\nConnection closed cleanly.")
except Exception as e:
    print("Connection failed:", e)
