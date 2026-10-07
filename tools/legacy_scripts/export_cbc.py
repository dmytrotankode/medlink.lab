import pymysql

conn = pymysql.connect(
    host="46.224.52.186",
    port=3309,
    user="update_user",
    password="12FrrehfcbVtl#$",
    database="hospital_etalon",
    charset="utf8mb4"
)

with open("C:/__MEDLINK___/LABA/sample_cbc.txt", "w", encoding="utf-8") as f:
    with conn.cursor(pymysql.cursors.DictCursor) as cur:
        cur.execute("""
            SELECT ds.service_id, ds.service_code, ds.service_name,
                   da.attribute_id, da.attribute_code, da.attribute_name, da.unit_measure_shname,
                   dsla.norm_low, dsla.norm_high, dsla.norm_low_critical, dsla.norm_high_critical,
                   dsla.norm_text
            FROM dct_service ds
            JOIN dct_service_attribute dsa ON ds.service_id = dsa.service_id
            JOIN dct_attribute da ON dsa.attribute_id = da.attribute_id
            LEFT JOIN dct_service_lab_attribute dsla ON dsa.service_attribute_id = dsla.service_attribute_id
            WHERE ds.service_id = 28086;
        """)
        rows = cur.fetchall()
        for r in rows:
            f.write(f"{r['service_code']} | {r['service_name']} | {r['attribute_code']} | {r['attribute_name']} | {r['unit_measure_shname']} | [{r['norm_low']} - {r['norm_high']}] | Crit: [{r['norm_low_critical']} - {r['norm_high_critical']}]\n")

conn.close()
print("Wrote sample_cbc.txt")
