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
    cur.execute("SELECT biomaterial_type_name FROM dct_lab_biomaterial_type")
    rows = cur.fetchall()
    print("Direct UTF-8:")
    for r in rows[:5]:
        val = r['biomaterial_type_name']
        try:
            fixed = val.encode('latin1').decode('cp1251')
        except Exception:
            fixed = val
        print(f"  raw: {val}  --> fixed: {fixed}")

conn.close()
