with open("C:/__MEDLINK___/LABA/db_lab_dump.txt", "r", encoding="utf-8") as f:
    text = f.read()

import re
codes = re.findall(r"@analyzer_type_code\s*(?:IN|=)\s*\(([^)]+)\)|@analyzer_type_code\s*=\s*'([^']+)'", text, re.IGNORECASE)
flattened = set()
for c in codes:
    for item in c:
        if item:
            for piece in item.split(','):
                p = piece.strip().strip("'\" ")
                if p: flattened.add(p)
print("Analyzers in SQL:", sorted(list(flattened)))
