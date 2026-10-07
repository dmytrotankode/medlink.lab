with open("C:/Desktop/src/fBarcode/fBarcode.pas", "r", encoding="latin1") as f:
    text = f.read()
import re
print("Functions/Procedures in fBarcode:")
for m in re.findall(r"(?:procedure|function)\s+([^\r\n;]+);", text):
    print("  ", m)
