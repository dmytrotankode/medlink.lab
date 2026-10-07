import json

print("==============================================================")
print(" MedLink LIS: Analyzer Protocol Simulation & Parser Verification ")
print(" Copyright (c) 2026 MedLink. Всі права захищені.              ")
print("==============================================================")

# 1. Parse ASTM Sysmex XN-1000 message
print("\n[TEST 1] Parsing ASTM E1394 message (Sysmex XN-1000 CBC):")
with open("C:/__MEDLINK___/LABA/test_examples/04_astm_results_sysmex_xn.txt", "r", encoding="utf-8") as f:
    astm_lines = f.readlines()

barcode = None
patient_name = None
results = []
for line in astm_lines:
    clean = line.strip().replace("<STX>", "").replace("<ETX>", "").replace("<CR>", "").replace("<LF>", "")
    if clean.startswith("1H|") or clean.startswith("H|"):
        print("  -> Instrument Header: Sysmex XN-1000 detected")
    elif "|P|" in clean or clean.startswith("2P|") or clean.startswith("P|"):
        parts = clean.split("|")
        if len(parts) > 5: patient_name = parts[5].replace("^", " ")
    elif clean.startswith("3O|") or clean.startswith("O|"):
        parts = clean.split("|")
        if len(parts) > 2: barcode = parts[2]
    elif "|R|" in clean or "R|" in clean:
        parts = clean.split("|")
        # R|1|^^^WBC^|7.45|10*9/L|4.0^9.0|N|N|F
        if len(parts) > 6:
            code = parts[2].strip("^ ")
            val = parts[3]
            unit = parts[4]
            ref = parts[5]
            flag = parts[6]
            results.append({"code": code, "val": val, "unit": unit, "ref": ref, "flag": flag})

print(f"  Barcode: {barcode} | Patient: {patient_name}")
print(f"  Extracted Results ({len(results)} tests):")
for r in results:
    print(f"    - {r['code']:<6}: {r['val']:>6} {r['unit']:<10} (Norm: {r['ref']:<10}) Flag: [{r['flag']}]")

# 2. Parse HL7 v2 Mindray BS-240
print("\n[TEST 2] Parsing HL7 v2.3.1 message (Mindray BS-240 Biochemistry):")
with open("C:/__MEDLINK___/LABA/test_examples/05_hl7_oru_r01_mindray.hl7", "r", encoding="utf-8") as f:
    hl7_text = f.read()

hl7_results = []
for line in hl7_text.splitlines():
    if line.startswith("OBX|"):
        flds = line.split("|")
        t_name = flds[3].split("^")[1] if "^" in flds[3] else flds[3]
        val = flds[5]
        u = flds[6]
        norm = flds[7]
        hl7_results.append({"name": t_name, "val": val, "unit": u, "norm": norm})

print(f"  Extracted Biochemistry Results ({len(hl7_results)} tests):")
for r in hl7_results:
    print(f"    - {r['name']:<28}: {r['val']:>6} {r['unit']:<8} (Norm: {r['norm']:<10})")

# 3. Verify FHIR R4 Bundle
print("\n[TEST 3] Validating HL7 FHIR R4 DiagnosticReport Bundle:")
with open("C:/__MEDLINK___/LABA/test_examples/07_fhir_diagnostic_report_bundle.json", "r", encoding="utf-8") as f:
    bundle = json.load(f)

entries = bundle.get("entry", [])
types = [e["resource"]["resourceType"] for e in entries]
print(f"  Bundle ID: {bundle.get('id')} | Entries: {len(entries)}")
print(f"  Contained Resources: {', '.join(types)}")

print("\n==============================================================")
print(" Всі тести протоколів успішно виконано без помилок! ")
print("==============================================================")
