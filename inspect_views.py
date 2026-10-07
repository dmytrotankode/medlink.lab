# -*- coding: utf-8 -*-
with open('medlink_lab_frontend/run_prototype.html', 'r', encoding='utf-8') as f:
    content = f.read()

import re
views = re.findall(r'v-if="currentView === \'([^\']+)\'"', content)
print("Views found:", views)

for v in ['qc', 'biobank', 'patient', 'analytics', 'microbiology', 'logistics']:
    target = f"currentView === '{v}'"
    idx = content.find(target, 5000) # after menu
    if idx != -1:
        print(f"\n--- VIEW: {v} (at {idx}) ---")
        print(content[idx:idx+1500])
