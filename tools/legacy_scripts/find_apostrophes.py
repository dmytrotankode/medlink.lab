# -*- coding: utf-8 -*-
with open(r'C:\__MEDLINK___\LABA\generate_runnable_prototype.py', 'r', encoding='utf-8') as f:
    lines = f.readlines()

print("Scanning generate_runnable_prototype.py for Ukrainian apostrophes...")
for i, line in enumerate(lines, 1):
    for token in ["'я", "'є", "'ї", "'ю", "\\'"]:
        if token in line:
            print(f"Line {i}: {line.strip()[:100]}")
            break
