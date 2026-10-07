with open("C:/__MEDLINK___/LABA/ТЗ_ЛІС_MedLink_v3_Повне.html", "r", encoding="utf-8") as f:
    text = f.read()

import re
sections = re.findall(r'<section[^>]*id="([^"]+)"', text)
headers = re.findall(r'<h2[^>]*>(.*?)</h2>', text)
print("Sections found:", sections)
print(f"Total H2 headers found ({len(headers)}):")
for h in headers:
    print("  -", h)
print("Total length:", len(text))
print("Ends with </html>:", text.strip().endswith("</html>"))
