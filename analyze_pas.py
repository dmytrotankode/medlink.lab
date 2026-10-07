with open("C:/__MEDLINK___/LABA/aconnectastm_extracted/main.pas", "r", encoding="latin1") as f:
    lines = f.readlines()

for i, line in enumerate(lines):
    l_lower = line.lower()
    if any(k in l_lower for k in ["ac_", "analyzer", "storedproc", "exec", "call ", "sql.add", "sql.text"]):
        print(f"{i+1}: {line.strip()}")
