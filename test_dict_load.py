import os
import json

output_file = "C:/__MEDLINK___/LABA/ТЗ_ЛІС_MedLink_v3_Повне.html"

# Load dictionaries summary for embedding in the specification
with open("C:/__MEDLINK___/LABA/dictionaries/01_biomaterials.json", "r", encoding="utf-8") as f:
    biomaterials = json.load(f)
with open("C:/__MEDLINK___/LABA/dictionaries/02_tube_types.json", "r", encoding="utf-8") as f:
    tubes = json.load(f)
with open("C:/__MEDLINK___/LABA/dictionaries/03_method_types.json", "r", encoding="utf-8") as f:
    methods = json.load(f)
with open("C:/__MEDLINK___/LABA/dictionaries/04_analyzer_types.json", "r", encoding="utf-8") as f:
    analyzer_types = json.load(f)
with open("C:/__MEDLINK___/LABA/dictionaries/05_lab_parameters_and_profiles.json", "r", encoding="utf-8") as f:
    profiles = json.load(f)

print(f"Loaded dicts: BM={len(biomaterials)}, Tubes={len(tubes)}, Methods={len(methods)}, Analyzers={len(analyzer_types)}, Profiles={len(profiles)}")
