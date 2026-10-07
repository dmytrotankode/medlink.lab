with open("C:/__MEDLINK___/LABA/MedLink.LabConnector/MedLink.LabConnector.csproj", "r", encoding="utf-8") as f:
    text = f.read()

text = text.replace("<TargetFramework>net6.0</TargetFramework>", "<TargetFramework>net8.0</TargetFramework>")
text = text.replace('Version="6.0.1"', 'Version="8.0.0"')
text = text.replace('Version="6.0.0"', 'Version="8.0.0"')
text = text.replace('Version="6.0.30"', 'Version="8.0.10"')

with open("C:/__MEDLINK___/LABA/MedLink.LabConnector/MedLink.LabConnector.csproj", "w", encoding="utf-8") as f:
    f.write(text)

with open("C:/__MEDLINK___/LABA/MedLink.LabConnector/Core/Hl7V2Driver.cs", "r", encoding="utf-8") as f:
    code = f.read()

old_snippet = """        // Send HL7 ACK back to analyzer: MSH|^~\\&|MedLink|LIS|...  MSA|AA|{msgId}
        var ack = $\"MSH|^~\\\\&|MedLinkLIS|MedLink|{AnalyzerCode}|Instrument|{DateTime.UtcNow:yyyyMMddHHmmss}||ACK|{messageControlId}|P|2.3.1\\rMSA|AA|{messageControlId}\\r\";"""

new_snippet = """        // Send HL7 ACK back to analyzer
        string nowStr = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string ack = "MSH|^~\\\\&|MedLinkLIS|MedLink|" + AnalyzerCode + "|Instrument|" + nowStr + "||ACK|" + messageControlId + "|P|2.3.1\\rMSA|AA|" + messageControlId + "\\r";"""

code = code.replace(old_snippet, new_snippet)
with open("C:/__MEDLINK___/LABA/MedLink.LabConnector/Core/Hl7V2Driver.cs", "w", encoding="utf-8") as f:
    f.write(code)

print("Updated csproj to net8.0 and fixed Hl7V2Driver.cs")
