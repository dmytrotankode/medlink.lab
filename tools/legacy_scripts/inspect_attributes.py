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
        SELECT sa.service_attribute_id, sa.service_attribute_name, sa.unit_id,
               dsa.norm_low, dsa.norm_high, dsa.norm_low_critical, dsa.norm_high_critical,
               dsa.norm_text
        FROM dct_service_attribute sa
        LEFT JOIN dct_service_lab_attribute dsa ON sa.service_attribute_id = dsa.service_attribute_id
        LIMIT 20;
    """)
    rows = cur.fetchall()
    print(f"Sample service attributes ({len(rows)}):")
    for r in rows[:10]:
        print(" ", r)

conn.close()
