import sys
import os

sys.path.append("C:/__MEDLINK___/LABA/tz_parts")
from part1_intro import get_part1
from part2_modules import get_part2
from part3_workflows import get_part3
from part4_connector import get_part4
from part5_architecture import get_part5
from part6_appendix import get_part6

full_html = get_part1() + get_part2() + get_part3() + get_part4() + get_part5() + get_part6()

target_path = "C:/__MEDLINK___/LABA/ТЗ_ЛІС_MedLink_v3_Повне.html"
with open(target_path, "w", encoding="utf-8") as f:
    f.write(full_html)

file_size = os.path.getsize(target_path)
print(f"Generated {target_path} successfully! Size: {file_size} bytes ({file_size/1024:.1f} KB)")
