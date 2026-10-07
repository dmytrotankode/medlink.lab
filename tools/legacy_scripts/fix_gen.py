with open("C:/__MEDLINK___/LABA/generate_profiles.py", "r", encoding="utf-8") as f:
    text = f.read()

text = text.replace(
    'meth = f"\'{test[\'method\'].replace(\\"\'\\", \\"\'\'\\")}\'" if test.get("method") else "NULL"',
    'm_val = test["method"].replace("\'", "\'\'") if test.get("method") else None\n            meth = f"\'{m_val}\'" if m_val else "NULL"'
)

with open("C:/__MEDLINK___/LABA/generate_profiles.py", "w", encoding="utf-8") as f:
    f.write(text)

