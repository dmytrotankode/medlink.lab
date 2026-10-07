with open("C:/__MEDLINK___/LABA/create_prototypes.py", "r", encoding="utf-8") as f:
    text = f.read()

# Replace the list comprehension that had backslash inside f-string
old_p07 = """    <div style="display:grid; grid-template-columns:repeat(10, 1fr); gap:4px; max-width:400px; margin:0 auto 16px;">
      <!-- Generate 100 sample cells -->
      {''.join([f'<div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:{"#10b981" if i in [3,14,28,45,67] else "#ef4444" if i==22 else "#f1f5f9"}; font-size:9px; display:flex; align-items:center; justify-content:center; color:{"white" if i in [3,14,22,28,45,67] else "#64748b"}; font-weight:bold; cursor:pointer;" onclick="alert(\\'Комірка #{i+1}: Зразок 1026004819 (Коваленко О.С., сироватка)\\')">{i+1}</div>' for i in range(100)])}
    </div>"""

# Pre-generate grid html outside f-string
text = text.replace(old_p07, """    <div style="display:grid; grid-template-columns:repeat(10, 1fr); gap:4px; max-width:400px; margin:0 auto 16px;">
      <!-- Matrix Cells -->
      __MATRIX_CELLS__
    </div>""")

# Pre-generate cells
cells = []
for i in range(100):
    bg = "#10b981" if i in [3,14,28,45,67] else "#ef4444" if i==22 else "#f1f5f9"
    col = "white" if i in [3,14,22,28,45,67] else "#64748b"
    cells.append(f'<div style="width:34px; height:34px; border:1px solid #cbd5e1; border-radius:4px; background:{bg}; font-size:9px; display:flex; align-items:center; justify-content:center; color:{col}; font-weight:bold; cursor:pointer;" onclick="alert(\'Комірка #{i+1}: Зразок 1026004819 (Коваленко О.С.)\')">{i+1}</div>')
matrix_html = "".join(cells)

text = text.replace("__MATRIX_CELLS__", matrix_html)

with open("C:/__MEDLINK___/LABA/create_prototypes.py", "w", encoding="utf-8") as f:
    f.write(text)

print("Fixed create_prototypes.py")
