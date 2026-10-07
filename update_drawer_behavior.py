# -*- coding: utf-8 -*-
with open(r"C:\__MEDLINK___\LABA\generate_runnable_prototype.py", "r", encoding="utf-8") as f:
    content = f.read()

# Replace :breakpoint="980" with behavior="desktop"
content = content.replace(':breakpoint="980"', 'behavior="desktop"')

with open(r"C:\__MEDLINK___\LABA\generate_runnable_prototype.py", "w", encoding="utf-8") as f:
    f.write(content)

print("Updated generate_runnable_prototype.py with behavior='desktop'")
