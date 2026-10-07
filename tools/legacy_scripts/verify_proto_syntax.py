import re

with open("medlink_lab_frontend/run_prototype.html", "r", encoding="utf-8") as f:
    text = f.read()

print("File size:", len(text))
print("q-dialog showServiceDetailDialog present:", '<q-dialog v-model="showServiceDetailDialog"' in text)
print("q-dialog showBiomaterialModal present:", '<q-dialog v-model="showBiomaterialModal"' in text)
print("q-dialog showTubeModal present:", '<q-dialog v-model="showTubeModal"' in text)
print("q-dialog showAnalyzerModal present:", '<q-dialog v-model="showAnalyzerModal"' in text)
print("q-dialog showParamModal present:", '<q-dialog v-model="showParamModal"' in text)

# Check script balance
script_match = re.search(r'<script>(.*?)</script>', text, flags=re.DOTALL)
if script_match:
    js_code = script_match.group(1)
    open_curlies = js_code.count('{')
    close_curlies = js_code.count('}')
    open_parens = js_code.count('(')
    close_parens = js_code.count(')')
    print(f"Curlys balance: open={open_curlies}, close={close_curlies}, diff={open_curlies - close_curlies}")
    print(f"Parens balance: open={open_parens}, close={close_parens}, diff={open_parens - close_parens}")
