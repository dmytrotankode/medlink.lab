import pymysql

conn = pymysql.connect(
    host="46.224.52.186",
    port=3309,
    user="update_user",
    password="12FrrehfcbVtl#$",
    database="hospital_etalon",
    charset="utf8mb4"
)

with conn.cursor(pymysql.cursors.DictCursor) as cur:
    cur.execute("""
        SELECT service_id, service_code, service_name 
        FROM dct_service 
        WHERE service_name LIKE '%кров%' 
        LIMIT 10;
    """)
    for r in cur.fetchall():
        print(r['service_id'], r['service_code'], r['service_name'].encode('unicode_escape').decode())

conn.close()
