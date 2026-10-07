import pymysql

conn = pymysql.connect(
    host="46.224.52.186",
    port=3309,
    user="update_user",
    password="12FrrehfcbVtl#$",
    database="hospital_etalon",
    charset="utf8mb4"
)

with open("C:/__MEDLINK___/LABA/db_lab_dump.txt", "w", encoding="utf-8") as out:
    with conn.cursor() as cur:
        tables = ['ac_analyzer', 'ac_analyzer_attribute', 'ac_analyzer_type', 'ac_status', 
                  'dct_lab_biomaterial_type', 'dct_lab_tube_type', 'dct_lab_method_type', 
                  'dct_lab_equipment_type', 'dct_service_lab', 'dct_service_lab_attribute', 
                  'dct_service_lab_norm', 'dct_service_lab_method', 'med_exec_service_lab']
        for t in tables:
            cur.execute(f"SHOW CREATE TABLE `{t}`;")
            row = cur.fetchone()
            out.write(f"\n==================== TABLE {t} ====================\n")
            out.write(row[1] + "\n")

        routines = ['parse_lab_message', 'gen_lab_order', 'get_message_for_analyzer', 
                    'list_analyzer', 'gen_lab_tube_barcode', 'list_exec_service_lab']
        for r in routines:
            try:
                cur.execute(f"SHOW CREATE PROCEDURE `{r}`;")
                row = cur.fetchone()
                out.write(f"\n==================== PROCEDURE {r} ====================\n")
                out.write(row[2] + "\n")
            except Exception:
                try:
                    cur.execute(f"SHOW CREATE FUNCTION `{r}`;")
                    row = cur.fetchone()
                    out.write(f"\n==================== FUNCTION {r} ====================\n")
                    out.write(row[2] + "\n")
                except Exception as e:
                    out.write(f"\nCould not fetch routine {r}: {e}\n")

conn.close()
print("Export complete to C:/__MEDLINK___/LABA/db_lab_dump.txt")
