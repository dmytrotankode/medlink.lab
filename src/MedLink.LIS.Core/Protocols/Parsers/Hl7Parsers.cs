// =============================================================================
// MedLink LIS 4.0 — парсери HL7 v2 (ORU^R01 узагальнений; Mindray BS/BC/CL + Dymind +
// HumaCount 5D; iChroma III OUL^R24). Порт HL7-гілок parse_lab_message.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text.RegularExpressions;
using MedLink.LIS.Core.Contracts;

namespace MedLink.LIS.Core.Protocols.Parsers;

/// <summary>Узагальнений парсер HL7 ORU^R01 / OUL^R21/R22 та запитів QRY^Q02 (QRD).</summary>
public class Hl7OruParser : IAnalyzerMessageParser
{
    private static readonly Regex LoincLike = new(@"^\d{3,6}-\d$", RegexOptions.Compiled);

    public virtual string Kind => "HL7_ORU";

    public AnalyzerInboundMessage Parse(AnalyzerConfigDto cfg, string raw)
    {
        raw ??= "";
        try
        {
            var msg = Hl7Parser.Parse(raw);
            var result = new AnalyzerInboundMessage { Raw = MllpCodec.Unwrap(raw), MessageControlId = msg.MessageControlId };
            if (msg.Segments.Count == 0 || msg.Msh == null)
            {
                var t = raw.Trim('\0', ' ', '\r', '\n', (char)0x0B, (char)0x1C);
                if (t.Length == 1 && t[0] == AstmControl.AckChar) { result.Kind = InboundMessageKind.Ack; return result; }
                result.Warnings.Add("Відсутній сегмент MSH");
                return result;
            }
            if (msg.Has("MSA") && msg.MessageCode == "ACK")
            {
                result.Kind = InboundMessageKind.Ack;
                return result;
            }
            if (msg.Has("QRD") || msg.MessageCode == "QRY")
            {
                ParseQuery(msg, result);
                return result;
            }
            if (msg.Has("OBX") || msg.Has("OBR"))
            {
                result.Kind = InboundMessageKind.Results;
                ParseResults(cfg, msg, result);
                if (result.Results.Count == 0) result.Warnings.Add("OBX-сегменти відсутні або не розпізнані");
                return result;
            }
            result.Kind = InboundMessageKind.Other;
            return result;
        }
        catch (Exception ex)
        {
            return AnalyzerInboundMessage.Other(raw, "Помилка розбору HL7: " + ex.Message);
        }
    }

    protected virtual void ParseQuery(Hl7Message msg, AnalyzerInboundMessage result)
    {
        result.Kind = InboundMessageKind.Query;
        var qrd = msg.First("QRD");
        if (qrd != null)
        {
            // QRD-8 "Who subject filter" = штрихкод (Mindray: QRD|ts|R|D|1|||RD|barcode|OTH|||T)
            foreach (var rep in qrd.Repeats(8))
            {
                var b = rep.Split('^')[0].Trim();
                if (b.Length > 0 && !result.Barcodes.Contains(b)) result.Barcodes.Add(b);
            }
            if (result.Barcodes.Count == 0)
            {
                var b = qrd.Component(8, 1);
                if (b.Length > 0) result.Barcodes.Add(b);
            }
            result.RawQueryToken = qrd.Field(8);
        }
        // Mindray BS-240: MSH-10 (Message Control ID) несе номер позиції проби
        var pos = msg.MessageControlId;
        if (!string.IsNullOrWhiteSpace(pos)) result.SamplePosition = pos.Trim();
        if (result.Barcodes.Count == 0) result.Warnings.Add("QRD без штрихкоду");
    }

    protected virtual void ParseResults(AnalyzerConfigDto cfg, Hl7Message msg, AnalyzerInboundMessage result)
    {
        var pid = msg.First("PID");
        if (pid != null) result.Patient = PatientFromPid(pid);
        string barcode = "";
        Hl7Segment? currentObr = null;
        foreach (var s in msg.Segments)
        {
            switch (s.Name)
            {
                case "OBR":
                    currentObr = s;
                    barcode = BarcodeFromObr(s, msg);
                    if (IsQc(s)) result.IsQc = true;
                    break;
                case "SPM":
                    if (barcode.Length == 0) barcode = s.Component(2, 1).Trim();
                    break;
                case "OBX":
                    if (barcode.Length == 0) barcode = FallbackBarcode(msg);
                    if (barcode.Length > 0 && !result.Barcodes.Contains(barcode)) result.Barcodes.Add(barcode);
                    AddObx(s, barcode, result);
                    break;
            }
        }
        if (result.Barcodes.Count == 0)
        {
            var fb = FallbackBarcode(msg);
            if (fb.Length > 0) result.Barcodes.Add(fb);
        }
    }

    protected virtual bool IsQc(Hl7Segment obr)
    {
        // Mindray: OBR-? / OBX-? не стандартизовано; типово — ім'я пацієнта QC або OBR-4 містить QC
        var t = obr.RawText.ToUpperInvariant();
        return t.Contains("|QC|") || t.Contains("^QC^") || t.Contains("QC^SAMPLE");
    }

    /// <summary>Штрихкод із OBR: OBR-3 (filler), потім OBR-2 (placer), SAC-3/SPM-2, PID-3.</summary>
    protected virtual string BarcodeFromObr(Hl7Segment obr, Hl7Message msg)
    {
        foreach (var f in new[] { 3, 2 })
        {
            var v = obr.Component(f, 1).Trim();
            if (v.Length > 0) return v;
        }
        return FallbackBarcode(msg);
    }

    protected virtual string FallbackBarcode(Hl7Message msg)
    {
        var sac = msg.First("SAC");
        if (sac != null)
        {
            var v = sac.Component(3, 1).Trim();
            if (v.Length > 0) return v;
        }
        var spm = msg.First("SPM");
        if (spm != null)
        {
            var v = spm.Component(2, 1).Trim();
            if (v.Length > 0) return v;
        }
        var pid = msg.First("PID");
        if (pid != null)
        {
            var v = pid.Component(3, 1).Trim();
            if (v.Length > 0) return v;
            v = pid.Component(2, 1).Trim();
            if (v.Length > 0) return v;
        }
        return "";
    }

    protected virtual void AddObx(Hl7Segment obx, string barcode, AnalyzerInboundMessage result)
    {
        var valueType = obx.Field(2).Trim().ToUpperInvariant();
        var code = Code(obx);
        if (code.Length == 0) return;
        var value = Value(obx, valueType);
        var unit = obx.Component(6, 1).Trim();
        var reference = obx.Value(7).Trim();
        var flags = obx.Component(8, 1).Trim();
        var measuredAt = Hl7Parser.ParseTimestamp(obx.Field(14));
        if (string.IsNullOrWhiteSpace(value) && valueType != "TX" && valueType != "ST") return;
        result.AddResult(barcode, code, value, unit, flags, reference, measuredAt);
    }

    /// <summary>Код тесту з OBX-3: компонент 1; якщо він LOINC-подібний (6690-2) або порожній — компонент 2; якщо все порожнє — OBX-4.</summary>
    protected virtual string Code(Hl7Segment obx)
    {
        var c1 = obx.Component(3, 1).Trim();
        var c2 = obx.Component(3, 2).Trim();
        if (c1.Length == 0 && c2.Length > 0) return c2;
        if (c1.Length > 0 && c2.Length > 0 && LoincLike.IsMatch(c1)) return c2;
        if (c1.Length > 0) return c1;
        return obx.Component(4, 1).Trim();
    }

    protected virtual string Value(Hl7Segment obx, string valueType)
    {
        var v = obx.Value(5).Trim();
        // "5.2^^" → 5.2 ; для TX/ST залишаємо повний текст
        if (valueType is "NM" or "SN") v = v.Split('^')[0].Trim();
        return v;
    }

    public static InboundPatientInfo? PatientFromPid(Hl7Segment pid)
    {
        var info = new InboundPatientInfo
        {
            Id = AstmParseHelpers.FirstNonEmpty(pid.Component(3, 1), pid.Component(2, 1), pid.Component(4, 1)),
            LastName = pid.Component(5, 1),
            FirstName = pid.Component(5, 2),
            MiddleName = pid.Component(5, 3),
            BirthDate = Hl7Parser.ParseTimestamp(pid.Field(7)),
            Gender = AstmParseHelpers.NormalizeGender(pid.Field(8)),
        };
        if (string.IsNullOrEmpty(info.Id) && string.IsNullOrEmpty(info.LastName) && info.BirthDate == null) return null;
        return info;
    }
}

/// <summary>
/// Mindray BS-240/BS-30/C3100 (+ Dymind, HumaCount 5D): HL7 2.3.1; штрихкод OBR-3 (BS-240 — OBR-3 після пропуску OBR-2;
/// BS-30 — PID-3); код OBX-3 компонент 2 для LOINC-подібних; для коагулометра C3100 код PT доповнюється одиницею (PT_%, PT_s, PT_INR).
/// Запит QRY^Q02 → Query з позицією проби у MSH-10.
/// </summary>
public sealed class Hl7MindrayParser : Hl7OruParser
{
    public override string Kind => "HL7_MINDRAY";

    protected override string BarcodeFromObr(Hl7Segment obr, Hl7Message msg)
    {
        var b = base.BarcodeFromObr(obr, msg);
        return b.Trim('^', ' ');
    }

    protected override void AddObx(Hl7Segment obx, string barcode, AnalyzerInboundMessage result)
    {
        var valueType = obx.Field(2).Trim().ToUpperInvariant();
        var code = Code(obx);
        if (code.Length == 0) return;
        var value = Value(obx, valueType);
        var unit = obx.Component(6, 1).Trim();
        // Mindray C3100 (коагуляція): PT приходить трьома OBX з різними одиницями → PT_s / PT_% / PT_INR
        if (string.Equals(code, "PT", StringComparison.OrdinalIgnoreCase) && unit.Length > 0) code = $"{code}_{unit}";
        if (string.IsNullOrWhiteSpace(value) && valueType is not ("TX" or "ST")) return;
        result.AddResult(barcode, code, value, unit, obx.Component(8, 1), obx.Value(7), Hl7Parser.ParseTimestamp(obx.Field(14)));
    }

    protected override bool IsQc(Hl7Segment obr)
    {
        // Mindray: OBR-? не стандартизовано; QC-результати мають ім'я пацієнта/ID з префіксом QC
        return base.IsQc(obr);
    }
}

/// <summary>Boditech iChroma III (OUL^R24): PID-2 — штрихкод; OBX|n|TX|code||"value unit"|unit.</summary>
public sealed class Hl7IchromaParser : Hl7OruParser
{
    public override string Kind => "HL7_ICHROMA";

    protected override string BarcodeFromObr(Hl7Segment obr, Hl7Message msg)
    {
        var pid = msg.First("PID");
        if (pid != null)
        {
            var b = AstmParseHelpers.FirstNonEmpty(pid.Component(2, 1), pid.Component(3, 1));
            if (b.Length > 0) return b;
        }
        return base.BarcodeFromObr(obr, msg);
    }

    protected override void AddObx(Hl7Segment obx, string barcode, AnalyzerInboundMessage result)
    {
        var code = obx.Value(3).Trim();
        if (code.Length == 0) return; // OBX|3|TX||||… — порожній рядок-термінатор
        var raw = obx.Value(5).Trim();
        var unit = obx.Component(6, 1).Trim();
        var value = raw;
        int sp = raw.IndexOf(' ');
        if (sp > 0)
        {
            value = raw[..sp];
            if (unit.Length == 0) unit = raw[(sp + 1)..].Trim();
        }
        result.AddResult(barcode, code, value, unit, obx.Component(8, 1), obx.Value(7), Hl7Parser.ParseTimestamp(obx.Field(14)));
    }
}
