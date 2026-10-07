# -*- coding: utf-8 -*-
import re

with open(r'C:\__MEDLINK___\LABA\medlink_lab_frontend\run_prototype.html', 'r', encoding='utf-8') as f:
    content = f.read()

# Let's inspect click handlers
clicks = re.findall(r'@click="([^"]+)"', content)
print(f"Total @click attributes found: {len(clicks)}")

errors = []
for c in clicks:
    # Check if there are unbalanced single quotes or problematic escapes
    sq_count = c.count("'")
    if sq_count % 2 != 0:
        print(f"ERROR UNBALANCED SINGLE QUOTE in @click: {c}")
        errors.append(c)

print(f"Unbalanced click handlers: {len(errors)}")

# Also look for any template expressions {{ ... }}
mustaches = re.findall(r'\{\{(.*?)\}\}', content)
print(f"Total mustaches found: {len(mustaches)}")
for m in mustaches:
    if "'" in m:
        sq_count = m.count("'")
        if sq_count % 2 != 0:
            print(f"ERROR UNBALANCED SINGLE QUOTE in mustache: {m}")

# Now let's check for Quasar initialization
print("\nChecking Quasar / Vue initialization:")
script_match = re.search(r'<script>(.*?)</script>', content, re.DOTALL)
if script_match:
    js = script_match.group(1)
    print("Has Quasar.lang.set:", "Quasar.lang.set" in js)
    print("Has new Vue:", "new Vue" in js)
    print("Has Quasar CDN in head:", "quasar.umd.min.js" in content)
