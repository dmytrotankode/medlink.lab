with open("C:/__MEDLINK___/LABA/MedLink.LabConnector/Core/Hl7V2Driver.cs", "r", encoding="utf-8") as f:
    text = f.read()

text = text.replace(" MSA|AA|{msgId}\n", "")
text = text.replace(" MSA|AA|{msgId}\r\n", "")

with open("C:/__MEDLINK___/LABA/MedLink.LabConnector/Core/Hl7V2Driver.cs", "w", encoding="utf-8") as f:
    f.write(text)

print("Cleaned comment line in Hl7V2Driver.cs")
