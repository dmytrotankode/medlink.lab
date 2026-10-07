# -*- coding: utf-8 -*-
import sys
sys.stdout.reconfigure(encoding='utf-8')

with open(r"C:\__MEDLINK___\LABA\extracted_docx_text.txt", "r", encoding="utf-8") as f:
    text = f.read()

lines = text.split("\n")

print(f"Total lines: {len(lines)}")

# Find all headings / numbered sections
headings = []
for i, line in enumerate(lines):
    line_s = line.strip()
    if not line_s:
        continue
    # Check if line looks like heading
    if any(line_s.startswith(f"{n}.") for n in range(1, 20)) or any(line_s.startswith(f"{n}.{m}") for n in range(1, 20) for m in range(1, 20)):
        headings.append((i, line_s))
    elif line_s.isupper() and len(line_s) > 4:
        headings.append((i, line_s))

print(f"Found {len(headings)} headings/sections:")
for idx, h in headings:
    print(f"  Line {idx}: {h}")
