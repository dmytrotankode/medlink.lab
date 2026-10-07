with open("C:/__MEDLINK___/LABA/aconnectastm_extracted/main.dfm", "r", encoding="latin1") as f:
    text = f.read()

import re
matches = re.findall(r'object (genASTM|qryParse|qryGet|AccuracyMedConnect)[\s\S]*?end\r?\n', text)
for m in matches:
    print("--- MATCH ---")
    print(m[:1000])
