import sys
sys.path.append("C:/__MEDLINK___/LABA/tz_parts")
import part3_workflows
import part4_connector
import part5_architecture

# Read part3 text
with open("C:/__MEDLINK___/LABA/tz_parts/part3_workflows.py", "r", encoding="utf-8") as f:
    t3 = f.read()

# Add prominent prototype links
t3 = t3.replace(
    '<h2>4. Кабінет забору біоматеріалу (Phlebotomy Station) та маркування</h2>',
    '<h2>4. Кабінет забору біоматеріалу (Phlebotomy Station) та маркування</h2>\n      <div style="margin-bottom:16px;"><a href="prototypes/01_phlebotomy_station.html" target="_blank" class="btn btn-outline" style="border-color:#3b82f6; color:#2563eb; font-weight:bold;">🖥 Відкрити повноцінний екранний прототип Кабінету забору &rarr;</a></div>'
)

t3 = t3.replace(
    '<h2>5. Логістика біоматеріалу, холодовий ланцюг та лабораторний бракераж</h2>',
    '<h2>5. Логістика біоматеріалу, холодовий ланцюг та лабораторний бракераж</h2>\n      <div style="margin-bottom:16px;"><a href="prototypes/02_specimen_logistics.html" target="_blank" class="btn btn-outline" style="border-color:#3b82f6; color:#2563eb; font-weight:bold;">🚚 Відкрити прототип Логістики та бракеражу &rarr;</a></div>'
)

t3 = t3.replace(
    '<h2>6. Робоче місце «Дослідження» та обробка результатів</h2>',
    '<h2>6. Робоче місце «Дослідження» та обробка результатів</h2>\n      <div style="margin-bottom:16px; display:flex; gap:10px;"><a href="prototypes/03_lab_workstation.html" target="_blank" class="btn btn-outline" style="border-color:#3b82f6; color:#2563eb; font-weight:bold;">🔬 Відкрити робоче місце лаборанта &rarr;</a><a href="prototypes/04_validation_and_panic.html" target="_blank" class="btn btn-outline" style="border-color:#ef4444; color:#dc2626; font-weight:bold;">🚨 Прототип верифікації лікаря & Panic Alert &rarr;</a></div>'
)

t3 = t3.replace(
    '<h2>7. Внутрішній контроль якості (ВЯК): Карти Леві-Дженнінгса та правила Вестгарда</h2>',
    '<h2>7. Внутрішній контроль якості (ВЯК): Карти Леві-Дженнінгса та правила Вестгарда</h2>\n      <div style="margin-bottom:16px;"><a href="prototypes/05_quality_control.html" target="_blank" class="btn btn-outline" style="border-color:#10b981; color:#059669; font-weight:bold;">📊 Відкрити прототип ВЯК & Levey-Jennings &rarr;</a></div>'
)

with open("C:/__MEDLINK___/LABA/tz_parts/part3_workflows.py", "w", encoding="utf-8") as f:
    f.write(t3)

# Read part4 text
with open("C:/__MEDLINK___/LABA/tz_parts/part4_connector.py", "r", encoding="utf-8") as f:
    t4 = f.read()

t4 = t4.replace(
    '<h2>10. Кабінет пацієнта: Live-моніторинг етапів та графіки динаміки</h2>',
    '<h2>10. Кабінет пацієнта: Live-моніторинг етапів та графіки динаміки</h2>\n      <div style="margin-bottom:16px;"><a href="prototypes/06_patient_portal.html" target="_blank" class="btn btn-outline" style="border-color:#3b82f6; color:#2563eb; font-weight:bold;">📱 Відкрити прототип Кабінету пацієнта &rarr;</a></div>'
)

with open("C:/__MEDLINK___/LABA/tz_parts/part4_connector.py", "w", encoding="utf-8") as f:
    f.write(t4)

# Re-assemble master HTML
from assemble_tz import *
print("Reassembled master HTML with interactive prototype links!")
