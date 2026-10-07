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
        SELECT ds.service_name, da.attribute_code, da.attribute_name, da.unit_measure_shname,
               dsla.norm_low, dsla.norm_high
        FROM dct_service ds
        JOIN dct_service_attribute dsa ON ds.service_id = dsa.service_id
        JOIN dct_attribute da ON dsa.attribute_id = da.attribute_id
        LEFT JOIN dct_service_lab_attribute dsla ON dsa.service_attribute_id = dsla.service_attribute_id
        WHERE ds.service_id = 27632;
    """)
    rows = cur.fetchall()
    print("Service:", rows[0]['service_name'] if rows else "None")
    for r in rows:
        print(f"  {r['attribute_code']} | {r['attribute_name']} | {r['unit_measure_shname']} | [{r['norm_low']} - {r['norm_high']}]")
conn.close()
