# -*- coding: utf-8 -*-
with open(r"C:\__MEDLINK___\LABA\ТЗ_ЛІС_MedLink_v3_Повне.html", "r", encoding="utf-8") as f:
    text = f.read()

import re
m = re.search(r'<section[^>]*id=["\']sec-appendix["\'][^>]*>(.*?)</section>', text, re.DOTALL)
if m:
    with open(r"C:\__MEDLINK___\LABA\sec_appendix_dump.html", "w", encoding="utf-8") as out:
        out.write(m.group(0))
    print("Dumped sec-appendix successfully. Size:", len(m.group(0)))
else:
    print("Not found sec-appendix")
