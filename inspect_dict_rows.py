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
    for tbl in ['dct_lab_biomaterial_type', 'dct_lab_tube_type', 'dct_lab_method_type', 
                'dct_lab_equipment_type', 'ac_analyzer_type', 'ac_analyzer']:
        cur.execute(f"SELECT COUNT(*) as cnt FROM `{tbl}`")
        cnt = cur.fetchone()['cnt']
        cur.execute(f"SELECT * FROM `{tbl}` LIMIT 5")
        rows = cur.fetchall()
        print(f"=== {tbl} (count: {cnt}) ===")
        for r in rows:
            print(" ", r)

conn.close()
