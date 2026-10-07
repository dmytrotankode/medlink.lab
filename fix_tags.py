# -*- coding: utf-8 -*-
import re
import os

files = [
    r"C:\__MEDLINK___\LABA\medlink_lab_frontend\run_prototype.html",
    r"C:\__MEDLINK___\LABA\medlink_lab_frontend\index.html"
]

def expand_match(m):
    tag = m.group(1)
    attrs = m.group(2)
    # Don't expand void HTML elements like input, img, br, hr
    if tag.lower() in ["input", "img", "br", "hr", "meta", "link"]:
        return m.group(0)
    return f"<{tag}{attrs}></{tag}>"

pattern = re.compile(r'<(q-[a-zA-Z0-9-]+)([^>]*?)\s*/>')

for filepath in files:
    if not os.path.exists(filepath):
        continue
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    new_content, count = pattern.subn(expand_match, content)
    print(f"File {filepath}: replaced {count} self-closing q-* tags.")

    with open(filepath, "w", encoding="utf-8") as f:
        f.write(new_content)

print("Replacement complete.")
