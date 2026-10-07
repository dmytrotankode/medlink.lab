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
    cur.execute("DESCRIBE dct_service_attribute;")
    print("dct_service_attribute columns:")
    for col in cur.fetchall():
        print(" ", col[0], col[1])

conn.close()
