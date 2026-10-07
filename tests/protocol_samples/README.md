# Зразки повідомлень аналізаторів (`tests/protocol_samples`)

Файли використовуються xUnit-тестами (`tests/MedLink.LIS.Tests/Protocols`) та командою
`MedLink.LabConnector test --analyzer CODE --file <файл>` для перевірки парсерів без приладу.

## Нотація

Керуючі символи записано текстовими мітками, які розгортає `ProtocolText.LoadSample`:
`<ENQ>`=0x05, `<STX>`=0x02, `<ETX>`=0x03, `<ETB>`=0x17, `<EOT>`=0x04, `<ACK>`=0x06, `<NAK>`=0x15,
`<CR>`=0x0D, `<LF>`=0x0A, `<SOH>`=0x01, `<VT>`=0x0B, `<FS>`=0x1C, `<GS>`=0x1D, `<RS>`=0x1E.
Якщо файл містить мітки `<CR>`/`<LF>`, фізичні переводи рядків файлу — лише форматування і при завантаженні
ігноруються. Файли без таких міток (Integra, Cyan, DIRUI, ElytePlus, HL7) читаються як є.

Контрольна сума ASTM — два hex-символи після `<ETX>`/`<ETB>`: сума байтів (UTF-8) від номера кадру до
ETX/ETB включно, mod 256 (алгоритм `CalcCRC` Delphi-коннектора). У зразках 01–04 первинні значення були
хибними й перераховані; тест `AstmFrameCodecTests.Sample_frames_have_valid_checksums` перевіряє всі ASTM-зразки.
RAPID: сума байтів від `<STX>` до `<ETX>` включно (функція `get_crc` Simplex; ACK-кадр `<STX><ACK><ETX>0B<EOT>`).

## Перелік

| Файл | Прилад / протокол | Що демонструє | Парсер / шаблон |
|---|---|---|---|
| `01_astm_query_sysmex.txt` | Sysmex XN-1000, ASTM E1394 | запит замовлення `Q\|1\|^1026004819` (host query) | `ASTM_SYSMEX` → відповідь `SYSMEX_XN` |
| `02_astm_order_response.txt` | ЛІС → прилад, ASTM | еталонна відповідь-замовлення (H/P/O/L) з кирилицею у P | `ASTM_GENERIC` |
| `03_astm_results_cobas.txt` | Roche cobas e411, ASTM | результати TSH/FT4 з одиницями, нормами, прапорцями | `ASTM_COBAS` |
| `04_astm_results_sysmex_xn.txt` | Sysmex XN-1000, ASTM | 5 результатів CBC (WBC, RBC, HGB, HCT, PLT) | `ASTM_SYSMEX` |
| `05_hl7_oru_r01_mindray.hl7` | Mindray BS-240, HL7 2.3.1 | ORU^R01 з PID/PV1/OBR та 5 OBX (біохімія) | `HL7_MINDRAY` |
| `06_hl7_ack_response.hl7` | ЛІС → прилад, HL7 | ACK / MSA\|AA | — |
| `07_fhir_diagnostic_report_bundle.json` | FHIR R4 | Bundle (Patient, Specimen, DiagnosticReport) — для серверної частини | — |
| `08_astm_cobas_e411_query.txt` | cobas e411, ASTM (TSREQ^REAL) | запит з токеном `1^10260048^S1^SC` (seq^штрихкод^тип^контейнер) | `ASTM_COBAS` → `COBAS_E411` |
| `09_astm_cobas_e411_tsdwn_order.txt` | ЛІС → cobas e411 | відповідь `TSDWN^REPLY`, `O\|1\|barcode\|S1^SC\|^^^TSH^\…\|R\|\|\|\|\|\|A\|\|\|\|1\|…\|O` (як `gen_lab_order`) | `COBAS_E411` |
| `10_astm_urisys_1100_results.txt` | Roche Urisys 1100, ASTM-подібний | `R\|n\|01^SG\|1.020`, значення `neg^…`, 10 параметрів сечі | `ASTM_URISYS` |
| `11_rapid_smp_new_data.txt` | Siemens RAPIDPoint 500, RAPID | `SMP_NEW_DATA` з полями `m*`/`c*` (FS/GS/RS), CRC | `TEXT_RAPID` |
| `12_fuji_results.txt` | Fujifilm DRI-CHEM, CSV | `R,NORMAL,…,код,=,"значення одиниця",…` ×3 | `TEXT_FUJI` |
| `13_integra_results.txt` | Roche Cobas Integra 400, текст | `53 штрихкод`, `55 код`, `00 значення` (SOH/STX/ETX/EOT, LF) | `TEXT_INTEGRA` |
| `14_hl7_mindray_qry_q02.hl7` | Mindray BS-240, HL7 | запит `QRY^Q02`: штрихкод у QRD-8, позиція проби у MSH-10 | `HL7_MINDRAY` → `MINDRAY_HL7` |
| `15_hl7_mindray_dsr_q03.hl7` | ЛІС → Mindray BS-240 | відповідь: `QCK^Q02` + `DSR^Q03` (QRD/QRF/DSP\|1..28, DSP\|21 штрихкод, DSP\|29+ тести, DSC) | `MINDRAY_HL7` |
| `16_cyan_humacount5l_results.txt` | Cypress Cyan / HumaCount 5L, текст | табличний звіт `Param\tFlags\tValue\tUnit\t[min-max]`, `Sample ID:` | `TEXT_CYAN` |
| `17_hl7_ichroma_oul_r24.hl7` | Boditech iChroma III, HL7 2.6 | `OUL^R24`, штрихкод у PID-2, OBX TX `"14.24 ng/mL "` | `HL7_ICHROMA` |
| `18_text_dirui_h100.txt` | DIRUI H-100, текст | рядки `ID:…`, `UBG Normal 3.4umol/L`, `pH 7.5` | `TEXT_KEYVALUE` |
| `19_text_elyteplus.txt` | ElytePlus (ISE), текст | `Sample Report … Barcode 10042014 K 4.10 Na 138.6 … End` (bop/eop = текст) | `TEXT_KEYVALUE` |
| `20_astm_sysmex_cs2500_results.txt` | Sysmex CS-2500, ASTM (Roche-подібний) | результати PT/APTT, токен `2^10260056^S1^SC` у O-3 | `ASTM_SYSMEX` → `SYSMEX_CS2500` |
| `run_analyzer_simulation.py` | — | історичний Python-скрипт перевірки (legacy, шляхи Windows) | — |

## Швидка перевірка через CLI

```bash
dotnet run --project src/MedLink.LabConnector -- test --analyzer SYSMEXXN --file tests/protocol_samples/01_astm_query_sysmex.txt
dotnet run --project src/MedLink.LabConnector -- test --analyzer COBAS411 --file tests/protocol_samples/03_astm_results_cobas.txt --json
dotnet run --project src/MedLink.LabConnector -- test --analyzer RAPID    --file tests/protocol_samples/11_rapid_smp_new_data.txt
dotnet run --project src/MedLink.LabConnector -- preview-order --analyzer COBAS411 --barcode 10260048 --query-token "1^10260048^S1^SC"
dotnet run --project src/MedLink.LabConnector -- preview-order --analyzer MINDRAY240 --barcode 10260048 --position 7
```
