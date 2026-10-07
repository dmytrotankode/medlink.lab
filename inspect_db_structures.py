import pymysql

conn = pymysql.connect(
    host="46.224.52.186",
    port=3309,
    user="update_user",
    password="12FrrehfcbVtl#$",
    database="hospital_etalon",
    charset="utf8mb4"
)

with conn.cursor() as cur:
    cur.execute("""
        SELECT table_name 
        FROM information_schema.tables 
        WHERE table_schema = 'hospital_etalon' 
          AND (table_name LIKE '%lab%' OR table_name LIKE 'ac_%' OR table_name LIKE '%anal%')
        ORDER BY table_name;
    """)
    tables = [r[0] for r in cur.fetchall()]
    print(f"Matching tables ({len(tables)}):", tables)

    cur.execute("""
        SELECT routine_name, routine_type 
        FROM information_schema.routines 
        WHERE routine_schema = 'hospital_etalon' 
          AND (routine_name LIKE '%lab%' OR routine_name LIKE '%anal%')
        ORDER BY routine_name;
    """)
    routines = cur.fetchall()
    print(f"\nMatching routines ({len(routines)}):")
    for r in routines:
        print(f"  {r[1]}: {r[0]}")

conn.close()
