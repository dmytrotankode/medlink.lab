import json
import pymysql

conn = pymysql.connect(
    host="46.224.52.186",
    port=3309,
    user="update_user",
    password="12FrrehfcbVtl#$",
    database="hospital_etalon",
    charset="utf8mb4"
)

# 1. Biomaterials
biomaterials = []
with conn.cursor(pymysql.cursors.DictCursor) as cur:
    cur.execute("SELECT biomaterial_type_id, biomaterial_type_code, biomaterial_type_name, biomaterial_type_config FROM dct_lab_biomaterial_type ORDER BY biomaterial_type_id")
    for r in cur.fetchall():
        biomaterials.append({
            "id": r["biomaterial_type_id"],
            "code": r["biomaterial_type_code"] or f"BM_{r['biomaterial_type_id']:02d}",
            "name": r["biomaterial_type_name"],
            "config": json.loads(r["biomaterial_type_config"]) if r["biomaterial_type_config"] else {}
        })

with open("C:/__MEDLINK___/LABA/dictionaries/01_biomaterials.json", "w", encoding="utf-8") as f:
    json.dump(biomaterials, f, ensure_ascii=False, indent=2)

with open("C:/__MEDLINK___/LABA/dictionaries/01_biomaterials.sql", "w", encoding="utf-8") as f:
    f.write("-- MedLink LIS: Dictionaries - Biomaterial Types\n")
    f.write("-- Generated for PostgreSQL (MedLink evomis)\n\n")
    f.write("INSERT INTO lab_biomaterial_types (id, code, name, config, is_active) VALUES\n")
    val_strs = []
    for bm in biomaterials:
        conf_escaped = json.dumps(bm["config"]).replace("'", "''")
        name_esc = bm['name'].replace("'", "''")
        val_strs.append(f"  ({bm['id']}, '{bm['code']}', '{name_esc}', '{conf_escaped}'::jsonb, true)")
    f.write(",\n".join(val_strs))
    f.write("\nON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, code = EXCLUDED.code, config = EXCLUDED.config;\n")

# 2. Tube types / Containers
tubes = []
with conn.cursor(pymysql.cursors.DictCursor) as cur:
    cur.execute("SELECT tube_type_id, tube_type_code, tube_type_name, tube_type_color, tube_type_volume FROM dct_lab_tube_type ORDER BY tube_type_id")
    for r in cur.fetchall():
        tubes.append({
            "id": r["tube_type_id"],
            "code": r["tube_type_code"] or f"TUBE_{r['tube_type_id']:02d}",
            "name": r["tube_type_name"],
            "color_code": r["tube_type_color"],
            "volume_ml": r["tube_type_volume"]
        })

with open("C:/__MEDLINK___/LABA/dictionaries/02_tube_types.json", "w", encoding="utf-8") as f:
    json.dump(tubes, f, ensure_ascii=False, indent=2)

with open("C:/__MEDLINK___/LABA/dictionaries/02_tube_types.sql", "w", encoding="utf-8") as f:
    f.write("-- MedLink LIS: Dictionaries - Tube & Container Types\n")
    f.write("-- Generated for PostgreSQL (MedLink evomis)\n\n")
    f.write("INSERT INTO lab_tube_types (id, code, name, color_code, volume_ml, is_active) VALUES\n")
    val_strs = []
    for t in tubes:
        vol = f"'{t['volume_ml']}'" if t['volume_ml'] else "NULL"
        name_esc = t['name'].replace("'", "''")
        code_esc = t['code'].replace("'", "''")
        val_strs.append(f"  ({t['id']}, '{code_esc}', '{name_esc}', '{t['color_code']}', {vol}, true)")
    f.write(",\n".join(val_strs))
    f.write("\nON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, code = EXCLUDED.code, color_code = EXCLUDED.color_code, volume_ml = EXCLUDED.volume_ml;\n")

# 3. Method types
methods = []
with conn.cursor(pymysql.cursors.DictCursor) as cur:
    cur.execute("SELECT method_type_id, method_type_code, method_type_name FROM dct_lab_method_type ORDER BY method_type_id")
    for r in cur.fetchall():
        methods.append({
            "id": r["method_type_id"],
            "code": r["method_type_code"] or f"METH_{r['method_type_id']:02d}",
            "name": r["method_type_name"]
        })

with open("C:/__MEDLINK___/LABA/dictionaries/03_method_types.json", "w", encoding="utf-8") as f:
    json.dump(methods, f, ensure_ascii=False, indent=2)

with open("C:/__MEDLINK___/LABA/dictionaries/03_method_types.sql", "w", encoding="utf-8") as f:
    f.write("-- MedLink LIS: Dictionaries - Analytical Method Types\n")
    f.write("-- Generated for PostgreSQL (MedLink evomis)\n\n")
    f.write("INSERT INTO lab_method_types (id, code, name, is_active) VALUES\n")
    val_strs = []
    for m in methods:
        name_esc = m['name'].replace("'", "''")
        code_esc = m['code'].replace("'", "''")
        val_strs.append(f"  ({m['id']}, '{code_esc}', '{name_esc}', true)")
    f.write(",\n".join(val_strs))
    f.write("\nON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, code = EXCLUDED.code;\n")

# 4. Analyzer types
analyzer_types = []
with conn.cursor(pymysql.cursors.DictCursor) as cur:
    cur.execute("SELECT analyzer_type_id, analyzer_type_code, analyzer_type_name, analyzer_type_note, analyzer_type_config FROM ac_analyzer_type ORDER BY analyzer_type_id")
    for r in cur.fetchall():
        conf = {}
        if r["analyzer_type_config"]:
            try: conf = json.loads(r["analyzer_type_config"])
            except Exception: pass
        analyzer_types.append({
            "id": r["analyzer_type_id"],
            "code": r["analyzer_type_code"] or f"ANALYZER_{r['analyzer_type_id']:02d}",
            "name": r["analyzer_type_name"],
            "note": r["analyzer_type_note"],
            "config": conf
        })

with open("C:/__MEDLINK___/LABA/dictionaries/04_analyzer_types.json", "w", encoding="utf-8") as f:
    json.dump(analyzer_types, f, ensure_ascii=False, indent=2)

with open("C:/__MEDLINK___/LABA/dictionaries/04_analyzer_types.sql", "w", encoding="utf-8") as f:
    f.write("-- MedLink LIS: Dictionaries - Supported Analyzer Types & Protocols\n")
    f.write("-- Generated for PostgreSQL (MedLink evomis)\n\n")
    f.write("INSERT INTO lab_analyzer_types (id, code, name, note, config, is_active) VALUES\n")
    val_strs = []
    for at in analyzer_types:
        note_val = at['note'].replace("'", "''") if at['note'] else None
        note_esc = f"'{note_val}'" if note_val is not None else "NULL"
        conf_esc = json.dumps(at['config']).replace("'", "''")
        name_esc = at['name'].replace("'", "''")
        code_esc = at['code'].replace("'", "''")
        val_strs.append(f"  ({at['id']}, '{code_esc}', '{name_esc}', {note_esc}, '{conf_esc}'::jsonb, true)")
    f.write(",\n".join(val_strs))
    f.write("\nON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, code = EXCLUDED.code, note = EXCLUDED.note, config = EXCLUDED.config;\n")

conn.close()
print("Dictionaries 1-4 successfully created in C:/__MEDLINK___/LABA/dictionaries/")
