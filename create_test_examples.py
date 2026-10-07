import json

base_dir = "C:/__MEDLINK___/LABA/test_examples"

# 1. ASTM Query
astm_q = """<ENQ>
<STX>1H|\^&|||Sysmex^XN-1000^01-02|||||||P|E1394-97|20261006153000<CR><ETX>4D<CR><LF>
<STX>2Q|1|^1026004819||ALL||||||||O<CR><ETX>7C<CR><LF>
<STX>3L|1|N<CR><ETX>06<CR><LF>
<EOT>"""

# 2. ASTM Order Response
astm_o = """<ENQ>
<STX>1H|\^&|||MedLinkLIS|||||||P|E1394-97|20261006153001<CR><ETX>F2<CR><LF>
<STX>2P|1||108291||Коваленко^Олександр^Сергійович||19850412|M|||||Dr.Іваненко<CR><ETX>A3<CR><LF>
<STX>3O|1|1026004819||^^^WBC\\^^^RBC\\^^^HGB\\^^^HCT\\^^^PLT|R|20261006083000|||||A||||||||||||||O<CR><ETX>5B<CR><LF>
<STX>4L|1|N<CR><ETX>07<CR><LF>
<EOT>"""

# 3. ASTM Results Cobas
astm_cobas = """<ENQ>
<STX>1H|\^&|||Roche^Cobas-e411^01|||||||P|E1394-97|20261006154500<CR><ETX>B0<CR><LF>
<STX>2P|1||108291||Коваленко^Олександр^Сергійович||19850412|M<CR><ETX>6E<CR><LF>
<STX>3O|1|1026004819||^^^TSH\\^^^FT4|R||||||A||||||||||||||F<CR><ETX>2A<CR><LF>
<STX>4R|1|^^^TSH^|1.45|uIU/mL|0.4^4.0|N|N|F||admin||20261006154012|Cobas_e411<CR><ETX>1D<CR><LF>
<STX>5R|2|^^^FT4^|16.2|pmol/L|10.0^23.2|N|N|F||admin||20261006154230|Cobas_e411<CR><ETX>48<CR><LF>
<STX>6L|1|N<CR><ETX>09<CR><LF>
<EOT>"""

# 4. ASTM Results Sysmex
astm_sysmex = """<ENQ>
<STX>1H|\^&|||Sysmex^XN-1000|||||||P|E1394-97|20261006155000<CR><ETX>94<CR><LF>
<STX>2P|1||108291||Коваленко^Олександр^Сергійович||19850412|M<CR><ETX>6E<CR><LF>
<STX>3O|1|1026004819||^^^CBC|R||||||A||||||||||||||F<CR><ETX>D9<CR><LF>
<STX>4R|1|^^^WBC^|7.45|10*9/L|4.0^9.0|N|N|F||tech||20261006154800|XN1000<CR><ETX>90<CR><LF>
<STX>5R|2|^^^RBC^|4.62|10*12/L|4.0^5.0|N|N|F||tech||20261006154800|XN1000<CR><ETX>8A<CR><LF>
<STX>6R|3|^^^HGB^|148|g/L|130^160|N|N|F||tech||20261006154800|XN1000<CR><ETX>38<CR><LF>
<STX>7R|4|^^^HCT^|43.5|%|40.0^48.0|N|N|F||tech||20261006154800|XN1000<CR><ETX>DE<CR><LF>
<STX>8R|5|^^^PLT^|235|10*9/L|180^320|N|N|F||tech||20261006154800|XN1000<CR><ETX>C1<CR><LF>
<STX>9L|1|N<CR><ETX>0C<CR><LF>
<EOT>"""

# 5. HL7 v2 ORU^R01 Mindray
hl7_mindray = """MSH|^~\\&|Mindray|BS-240|MedLinkLIS|MedLink|20261006155500||ORU^R01|MSG009841|P|2.3.1
PID|1||108291^^^MedLink||Коваленко^Олександр^Сергійович||19850412|M
PV1|1|O|LAB^^^Отделение1
OBR|1||1026004819|BIOCHEM^Базовий біохімічний профіль|||20261006083000|||||||||Dr.Іваненко
OBX|1|NM|GLU^Глюкоза сироватки|1|5.2|mmol/L|4.1-5.9|N|||F|||20261006155200|BS240
OBX|2|NM|ALT^Аланінамінотрансфераза|1|32.4|U/L|0-41|N|||F|||20261006155230|BS240
OBX|3|NM|AST^Аспартатамінотрансфераза|1|28.1|U/L|0-37|N|||F|||20261006155300|BS240
OBX|4|NM|CREAT^Креатинін|1|84.0|umol/L|62-115|N|||F|||20261006155330|BS240
OBX|5|NM|UREA^Сечовина|1|5.4|mmol/L|2.5-8.3|N|||F|||20261006155400|BS240
"""

# 6. HL7 ACK
hl7_ack = """MSH|^~\\&|MedLinkLIS|MedLink|Mindray|BS-240|20261006155502||ACK|ACK009841|P|2.3.1
MSA|AA|MSG009841|Message accepted successfully
"""

# 7. FHIR R4 DiagnosticReport Bundle
fhir_bundle = {
  "resourceType": "Bundle",
  "id": "bundle-lab-report-1026004819",
  "type": "collection",
  "timestamp": "2026-10-06T15:56:00Z",
  "entry": [
    {
      "fullUrl": "urn:uuid:patient-108291",
      "resource": {
        "resourceType": "Patient",
        "id": "patient-108291",
        "identifier": [{"system": "https://ehealth.gov.ua/patients", "value": "018f4a12-8812-7001-a1b2-c3d4e5f60001"}],
        "name": [{"use": "official", "family": "Коваленко", "given": ["Олександр", "Сергійович"]}],
        "gender": "male",
        "birthDate": "1985-04-12"
      }
    },
    {
      "fullUrl": "urn:uuid:specimen-1026004819",
      "resource": {
        "resourceType": "Specimen",
        "id": "specimen-1026004819",
        "identifier": [{"system": "https://medlink.ua/barcodes", "value": "1026004819"}],
        "type": {
          "coding": [{"system": "http://snomed.info/sct", "code": "119364003", "display": "Serum specimen"}]
        },
        "collection": {
          "collectedDateTime": "2026-10-06T08:30:00Z",
          "bodySite": {"text": "Ліктьова вена"}
        }
      }
    },
    {
      "fullUrl": "urn:uuid:obs-glu",
      "resource": {
        "resourceType": "Observation",
        "id": "obs-glu",
        "status": "final",
        "category": [{"coding": [{"system": "http://terminology.hl7.org/CodeSystem/observation-category", "code": "laboratory"}]}],
        "code": {"coding": [{"system": "http://loinc.org", "code": "2345-7", "display": "Glucose [Moles/volume] in Serum"}]},
        "subject": {"reference": "urn:uuid:patient-108291"},
        "effectiveDateTime": "2026-10-06T15:52:00Z",
        "valueQuantity": {"value": 5.2, "unit": "ммоль/л", "system": "http://unitsofmeasure.org", "code": "mmol/L"},
        "interpretation": [{"coding": [{"system": "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation", "code": "N", "display": "Normal"}]}],
        "referenceRange": [{"low": {"value": 4.1, "unit": "mmol/L"}, "high": {"value": 5.9, "unit": "mmol/L"}}]
      }
    },
    {
      "fullUrl": "urn:uuid:report-1026004819",
      "resource": {
        "resourceType": "DiagnosticReport",
        "id": "report-1026004819",
        "identifier": [{"system": "https://medlink.ua/reports", "value": "REP-2026-1026004819"}],
        "status": "final",
        "category": [{"coding": [{"system": "http://terminology.hl7.org/CodeSystem/v2-0074", "code": "CH", "display": "Chemistry"}]}],
        "code": {"coding": [{"system": "http://loinc.org", "code": "24323-8", "display": "Comprehensive metabolic panel"}]},
        "subject": {"reference": "urn:uuid:patient-108291"},
        "effectiveDateTime": "2026-10-06T15:55:00Z",
        "issued": "2026-10-06T15:56:00Z",
        "performer": [{"display": "Клініко-діагностична лабораторія MedLink"}],
        "specimen": [{"reference": "urn:uuid:specimen-1026004819"}],
        "result": [{"reference": "urn:uuid:obs-glu"}],
        "conclusion": "Показники вуглеводного та білкового обміну в межах фізіологічної норми."
      }
    }
  ]
}

with open(f"{base_dir}/01_astm_query_sysmex.txt", "w", encoding="utf-8") as f: f.write(astm_q)
with open(f"{base_dir}/02_astm_order_response.txt", "w", encoding="utf-8") as f: f.write(astm_o)
with open(f"{base_dir}/03_astm_results_cobas.txt", "w", encoding="utf-8") as f: f.write(astm_cobas)
with open(f"{base_dir}/04_astm_results_sysmex_xn.txt", "w", encoding="utf-8") as f: f.write(astm_sysmex)
with open(f"{base_dir}/05_hl7_oru_r01_mindray.hl7", "w", encoding="utf-8") as f: f.write(hl7_mindray)
with open(f"{base_dir}/06_hl7_ack_response.hl7", "w", encoding="utf-8") as f: f.write(hl7_ack)
with open(f"{base_dir}/07_fhir_diagnostic_report_bundle.json", "w", encoding="utf-8") as f: json.dump(fhir_bundle, f, ensure_ascii=False, indent=2)

print("Test examples 01-07 generated successfully in test_examples/")
