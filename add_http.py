with open("C:/__MEDLINK___/LABA/MedLink.LabConnector/MedLink.LabConnector.csproj", "r", encoding="utf-8") as f:
    text = f.read()

if "Microsoft.Extensions.Http" not in text:
    text = text.replace(
        '<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />',
        '<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />\n    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />'
    )

with open("C:/__MEDLINK___/LABA/MedLink.LabConnector/MedLink.LabConnector.csproj", "w", encoding="utf-8") as f:
    f.write(text)

print("Added Microsoft.Extensions.Http")
