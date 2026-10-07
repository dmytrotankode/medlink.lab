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
        SELECT ds.service_id, ds.service_code, ds.service_name, 
               COUNT(dsa.service_attribute_id) as attr_count
        FROM dct_service_lab dsl
        JOIN dct_service ds ON dsl.service_id = ds.service_id
        LEFT JOIN dct_service_attribute dsa ON ds.service_id = dsa.service_id
        GROUP BY ds.service_id
        ORDER BY attr_count DESC
        LIMIT 10;
    """)
    for r in cur.fetchall():
        print(r)

conn.close()
