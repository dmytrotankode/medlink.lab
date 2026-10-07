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
        SELECT da.attribute_id, da.attribute_code, da.attribute_name, da.unit_measure_shname,
               dsla.norm_low, dsla.norm_high, dsla.norm_low_critical, dsla.norm_high_critical,
               dsla.norm_text
        FROM dct_service_attribute dsa
        JOIN dct_attribute da ON dsa.attribute_id = da.attribute_id
        LEFT JOIN dct_service_lab_attribute dsla ON dsa.service_attribute_id = dsla.service_attribute_id
        WHERE dsa.service_id = 20052;
    """)
    for r in cur.fetchall():
        print(f"{r['attribute_code']} | {r['attribute_name']} | {r['unit_measure_shname']} | [{r['norm_low']} - {r['norm_high']}]")

conn.close()
