import pymysql

try:
    conn = pymysql.connect(
        host="46.224.52.186",
        port=3309,
        user="update_user",
        password="12FrrehfcbVtl#$",
        charset="utf8mb4",
        connect_timeout=10
    )
    with conn.cursor() as cursor:
        cursor.execute("SHOW DATABASES;")
        dbs = cursor.fetchall()
        print("Databases:", [d[0] for d in dbs])
    conn.close()
except Exception as e:
    print("Error:", e)
