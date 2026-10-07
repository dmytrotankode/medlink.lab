// =============================================================================
// MedLink LIS 4.0 — побудовники замовлень за сімействами приладів.
// Формати записів перенесено з gen_lab_order (Simplex MySQL) з точним збереженням
// позицій полів (TSDWN^REPLY, |R||||||A||||1||||||||||O, 165/170-символьний поділ тощо).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols.Parsers;

namespace MedLink.LIS.Core.Protocols.Orders;

/// <summary>Нічого не відправляти (прилад без завантаження замовлень).</summary>
public sealed class NoneOrderBuilder : OrderBuilderBase
{
    public override string Template => "NONE";
    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx) => Array.Empty<string>();
    public override IReadOnlyList<string> BuildNoOrderRecords(string barcode, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx) => Array.Empty<string>();
}

/// <summary>Узагальнений ASTM E1394 (як зразок 02_astm_order_response): ^^^CODE, пріоритет R/S, тип проби.</summary>
public sealed class AstmGenericOrderBuilder : OrderBuilderBase
{
    public override string Template => "ASTM_GENERIC";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var tests = string.Join("\\", Codes(order).Select(c => "^^^" + c));
        var p = order.Patient ?? new AnalyzerOrderPatientDto();
        var name = $"{LastNameLatin(order)}^{FirstNameLatin(order)}";
        return new[]
        {
            $"H|\\^&|||MedLinkLIS|||||||P|E1394-97|{Ts(now)}",
            $"P|1||{PatientId(order)}||{name}||{Birth(order)}|{Gender(order)}",
            $"O|1|{Barcode(order)}||{tests}|{Priority(order)}|{Ts(now)}|||||A||||{order.SampleType}||||||||||O",
            "L|1|N",
        };
    }
}

/// <summary>Roche Cobas c111 (legacy 'СOBAS 411'/'COBAS111'): H|\^&amp;|||HIS01|||||C111||P|1|date.</summary>
public sealed class CobasC111OrderBuilder : OrderBuilderBase
{
    public override string Template => "COBAS_C111";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var tests = string.Join("\\", Codes(order).Select(c => "^^^" + c));
        return new[]
        {
            $"H|\\^&|||HIS01|||||C111||P|1|{TsLegacy(now)}",
            $"P|1||||{PatientId(order)}||{Birth(order)}|{Gender(order)}||||||||||||||||||||||||||",
            $"O|1|{Barcode(order)}||{tests}|R||||||A||||||||||||||O|||||",
            "L|1|N",
        };
    }
}

/// <summary>Спільне для Roche TSDWN^REPLY (c311 / e411 / CS-2500): позиція проби з токена запиту та поділ довгого списку тестів.</summary>
public abstract class RocheTsdwnOrderBuilderBase : OrderBuilderBase
{
    /// <summary>Поріг довжини списку тестів, після якого O-запис ділиться на два кадри (165 для e411, 170 для c311).</summary>
    protected abstract int SplitThreshold { get; }
    /// <summary>Заголовок для короткого варіанту (із назвою приладу).</summary>
    protected abstract string ShortHeader(AnalyzerConfigDto cfg);
    /// <summary>Заголовок для довгого (поділеного) варіанту — у legacy без назви приладу.</summary>
    protected virtual string LongHeader(AnalyzerConfigDto cfg) => "H|\\^&|||||||||TSDWN^REPLY|P|1";
    protected virtual string OrderSuffix => "|R||||||A||||1||||||||||O";
    protected virtual string TestId(string code) => $"^^^{code}^";

    /// <summary>Позиція/тип проби (O-4) із контексту запиту: хвіст токена після штрихкоду; S0 → S1 (як у legacy c311).</summary>
    protected virtual string Position(OrderBuildContext ctx)
    {
        var pos = ctx.SamplePosition;
        if (string.IsNullOrWhiteSpace(pos) && !string.IsNullOrWhiteSpace(ctx.RawQueryToken))
            pos = AstmParseHelpers.SplitBarcodeToken(ctx.RawQueryToken).Tail;
        pos = (pos ?? "").Trim().Trim('^');
        return pos.Replace("S0", "S1");
    }

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var orderList = string.Join("\\", Codes(order).Select(TestId));
        var barcode = Barcode(order);
        var pos = Position(ctx);
        bool longVariant = orderList.Length >= SplitThreshold;
        var o = SplitOrderList($"O|1|{barcode}|{pos}|", orderList, OrderSuffix, SplitThreshold);
        return new[]
        {
            longVariant ? LongHeader(cfg) : ShortHeader(cfg),
            "P|1",
            o,
            "L|1|N",
        };
    }

    public override IReadOnlyList<string> BuildNoOrderRecords(string barcode, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        if (!ctx.SendNoOrderReply) return Array.Empty<string>();
        return new[] { ShortHeader(cfg), "P|1", $"O|1|{barcode}|{Position(ctx)}||R||||||A||||1||||||||||Y", "L|1|N" };
    }
}

/// <summary>Roche Cobas c311: TSDWN^REPLY, поділ списку тестів на 170 символів.</summary>
public sealed class CobasC311OrderBuilder : RocheTsdwnOrderBuilderBase
{
    public override string Template => "COBAS_C311";
    protected override int SplitThreshold => 170;
    protected override string ShortHeader(AnalyzerConfigDto cfg) => "H|\\^&|||||||||TSDWN^REPLY|P|1";

    protected override string Position(OrderBuildContext ctx)
    {
        var pos = base.Position(ctx);
        // legacy: якщо немає ^SC — додати контейнер SC
        if (pos.Length > 0 && !pos.Contains("SC", StringComparison.OrdinalIgnoreCase)) pos += "^SC";
        return pos;
    }
}

/// <summary>Roche Cobas e411: TSDWN^REPLY, host^1 / cobas-e411, поділ списку тестів на 165 символів.</summary>
public sealed class CobasE411OrderBuilder : RocheTsdwnOrderBuilderBase
{
    public override string Template => "COBAS_E411";
    protected override int SplitThreshold => 165;
    protected override string ShortHeader(AnalyzerConfigDto cfg) => "H|\\^&|||host^1|||||cobas-e411|TSDWN^REPLY|P|1";
}

/// <summary>Sysmex CS-2500 (коагуляція, протокол Roche-подібний TSDWN^REPLY, поріг 165).</summary>
public sealed class SysmexCs2500OrderBuilder : RocheTsdwnOrderBuilderBase
{
    public override string Template => "SYSMEX_CS2500";
    protected override int SplitThreshold => 165;
    protected override string ShortHeader(AnalyzerConfigDto cfg) => "H|\\^&|||host^1|||||CS-2500|TSDWN^REPLY|P|1";
}

/// <summary>Sysmex CA-600 (CA-660): O-3 — токен запиту як є; тести ^^^code^name.</summary>
public sealed class SysmexCa600OrderBuilder : OrderBuilderBase
{
    public override string Template => "SYSMEX_CA600";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var tests = string.Join("\\", (order.Tests ?? new()).Select(t =>
        {
            var code = string.IsNullOrWhiteSpace(t.AnalyzerCode) ? t.TestCode : t.AnalyzerCode;
            var name = (t.TestCode ?? "").Trim();
            if (name.Length > 8) name = name[..8];
            return $"^^^{code.Trim()}^{name.PadRight(8)}";
        }).Distinct());
        var specimen = string.IsNullOrWhiteSpace(ctx.RawQueryToken) ? Barcode(order) : ctx.RawQueryToken.Trim();
        return new[]
        {
            "H|\\^&|||host^1|||||CA-600",
            "P|1",
            $"O|1|{specimen}||{tests}|R|{Ts(now)}|||||N",
            "L|1|N",
        };
    }
}

/// <summary>Sysmex XN/XS/XT (відповідь на запит хоста): тест-ID ^^^^CODE, O-26 = Q (відповідь на запит).</summary>
public sealed class SysmexXnOrderBuilder : OrderBuilderBase
{
    public override string Template => "SYSMEX_XN";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var tests = string.Join("\\", (order.Tests ?? new()).Select(t =>
        {
            var code = (string.IsNullOrWhiteSpace(t.AnalyzerCode) ? t.TestCode : t.AnalyzerCode).Trim();
            return string.IsNullOrWhiteSpace(t.Dilution) ? $"^^^^{code}" : $"^^^^{code}^{t.Dilution.Trim()}";
        }).Distinct());
        var specimen = string.IsNullOrWhiteSpace(ctx.RawQueryToken) ? Barcode(order) : ctx.RawQueryToken.Trim();
        return new[]
        {
            $"H|\\^&|||MedLinkLIS^^|||||||P|E1394-97|{Ts(now)}",
            $"P|1||{PatientId(order)}||{NameLastFirst(order)}||{Birth(order)}|{Gender(order)}",
            $"O|1|{specimen}||{tests}|{Priority(order)}|{Ts(now)}|||||N||||||||||||||Q",
            "L|1|N",
        };
    }

    public override IReadOnlyList<string> BuildNoOrderRecords(string barcode, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        if (!ctx.SendNoOrderReply) return Array.Empty<string>();
        var specimen = string.IsNullOrWhiteSpace(ctx.RawQueryToken) ? barcode : ctx.RawQueryToken.Trim();
        return new[] { $"H|\\^&|||MedLinkLIS^^|||||||P|E1394-97|{Ts(now)}", "P|1", $"O|1|{specimen}|||R|{Ts(now)}|||||N||||||||||||||Y", "L|1|N" };
    }
}

/// <summary>Horiba ABX Pentra: H|\^&amp;|||ABX|…|P|E1394-97; P з Prescriptor/Location; O …|R|date|||||N||||1.</summary>
public sealed class PentraOrderBuilder : OrderBuilderBase
{
    public override string Template => "PENTRA";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var tests = string.Join("\\", Codes(order).Select(c => "^^^" + c));
        var d = TsLegacy(now);
        return new[]
        {
            $"H|\\^&|||ABX|||||||P|E1394-97|{d}",
            $"P|1||{PatientId(order)}||{NameLastFirst(order)}||{Birth(order)}|{Gender(order)}|||||Prescriptor||||||||||||Location",
            $"O|1|{Barcode(order)}||{tests}|R|{d}|||||N||||1",
            "L|1|N",
        };
    }
}

/// <summary>Tosoh AIA: H|\^&amp;|||HOST|||||||||date; P|1|||||||; O|1|barcode||tests|||||||||||Sp.1; L|1.</summary>
public sealed class TosohOrderBuilder : OrderBuilderBase
{
    public override string Template => "TOSOH";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var tests = string.Join("\\", Codes(order).Select(c => "^^^" + c));
        return new[]
        {
            $"H|\\^&|||HOST|||||||||{TsLegacy(now)}",
            "P|1|||||||",
            $"O|1|{Barcode(order)}||{tests}|||||||||||Sp.1",
            "L|1",
        };
    }
}

/// <summary>Bio-Ksel 6000 (коагуляція): окремий O-запис на кожен тест; код «n-0001» → «0001».</summary>
public sealed class BioKselOrderBuilder : OrderBuilderBase
{
    public override string Template => "BIOKSEL";
    private static readonly Regex ChannelCode = new(@"^\d-(\d{4})$", RegexOptions.Compiled);

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var d = TsLegacy(now);
        var records = new List<string>
        {
            $"H|\\^&|||HOST|||||bioksel6000||P|1|{d}",
            "P|1|||||||||||||||||||||||||||||||||",
        };
        int n = 1;
        foreach (var raw in Codes(order))
        {
            var m = ChannelCode.Match(raw);
            var code = m.Success ? m.Groups[1].Value : (raw.Length > 2 && raw[1] == '-' ? raw[2..] : raw);
            records.Add($"O|{n++}|{Barcode(order)}||{code}|R|{d}||||||||||Bio-Ksel|||||||||O|||||");
        }
        records.Add("L|1|N");
        return records;
    }
}

/// <summary>Diagnostica Stago (STA Compact Max / Start): H|\^&amp;|||SCE^99^3.00|…|P|LIS2-A2.</summary>
public sealed class StagoOrderBuilder : OrderBuilderBase
{
    public override string Template => "STAGO";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var tests = string.Join("\\", Codes(order).Select(c => "^^^" + c));
        return new[]
        {
            $"H|\\^&|||SCE^99^3.00|||||||P|LIS2-A2|{TsLegacy(now)}",
            $"P|1||{PatientId(order)}||{NameLastFirst(order)}||{Birth(order)}|{Gender(order)}|||||Prescriptor||||||||||||Location",
            $"O|1|{Barcode(order)}||{tests}|R",
            "L|1|N",
        };
    }
}

/// <summary>Snibe Maglumi: H|\^&amp;||PSWD|Maglumi 1000|||||Lis||P|E1394-97|yyyyMMdd; O на кожен тест.</summary>
public sealed class MaglumiOrderBuilder : OrderBuilderBase
{
    public override string Template => "MAGLUMI";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var records = new List<string>
        {
            $"H|\\^&||PSWD|Maglumi 1000|||||Lis||P|E1394-97|{now:yyyyMMdd}",
            "P|1",
        };
        int n = 1;
        foreach (var code in Codes(order)) records.Add($"O|{n++}|{Barcode(order)}||^^^{code}|R");
        records.Add("L|1|N");
        return records;
    }
}

/// <summary>
/// Human HumaStar 100/300 (пакетне завантаження): H|\^&amp;|||HS100^V1.0|||||Host||P|1|date; на кожного пацієнта P/C та O на тест; L||N.
/// Рядки з'єднуються LF (як у legacy).
/// </summary>
public sealed class HumaStarOrderBuilder : OrderBuilderBase
{
    public override string Template => "HUMASTAR";
    public override OrderOutputKind Output => OrderOutputKind.TextLines;
    public override string RecordTerminator => "\n";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
        => BuildBatch(new[] { order }, cfg, now, ctx)[0];

    public override IReadOnlyList<IReadOnlyList<string>> BuildBatch(IReadOnlyList<AnalyzerOrderDto> orders, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var lines = new List<string> { $"H|\\^&|||HS100^V1.0|||||Host||P|1|{TsLegacy(now)}" };
        int p = 1;
        foreach (var order in orders)
        {
            var gender = Gender(order) switch { "M" => "MALE", "F" => "FEMALE", _ => "" };
            lines.Add($"P|{p}||{Barcode(order)}|Department1|{LastNameLatin(order)}|{FirstNameLatin(order)}|{Birth(order)}|{gender}|||||||||||||||||||||||||");
            lines.Add($"C|{p}|||");
            int n = 1;
            foreach (var code in Codes(order)) lines.Add($"O|{n++}|||{code}|False||||||||||Serum|||||||||||||||");
            p++;
        }
        lines.Add("L||N");
        return new List<IReadOnlyList<string>> { lines };
    }
}

/// <summary>Beckman Coulter Access 2 / DxI (ASTM): H|\^&amp;|||LIS|||||ACCESS^573061||P|1|now; P|1|First^Last||birth|gender||; O …|R||||||A||||Serum; L|1|F. Поділ на 170.</summary>
public sealed class BeckmanAccessOrderBuilder : OrderBuilderBase
{
    public override string Template => "BECKMAN_ACCESS";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var orderList = string.Join("\\", Codes(order).Select(c => "^^^" + c));
        if (orderList.Length < 170)
        {
            return new[]
            {
                $"H|\\^&|||LIS|||||ACCESS^573061||P|1|{Ts(now)}",
                $"P|1|{NameFirstLast(order)}||{Birth(order)}|{Gender(order)}||",
                $"O|1|{Barcode(order)}||{orderList}|R||||||A||||Serum",
                "L|1|F",
            };
        }
        return new[]
        {
            "H|\\^&|||LIS|||||ACCESS^500001||P|1|",
            "P|1",
            SplitOrderList($"O|1|{Barcode(order)}||", orderList, "|R||||||A||||Serum", 170),
            "L|1|F",
        };
    }
}

/// <summary>Шаблон 'ACCESS' із legacy (фактично Mindray BS-240 Pro ASTM): H|\^&amp;||| Product Model ^01.03.07.03^123456|…|SA|1394-97.</summary>
public sealed class AccessAstmOrderBuilder : OrderBuilderBase
{
    public override string Template => "ACCESS_ASTM";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var d = TsLegacy(now);
        var tests = string.Join("\\", Codes(order).Select(c => $"^{c}^2^1"));
        return new[]
        {
            $"H|\\^&||| Product Model ^01.03.07.03^123456|||||||SA|1394-97|{d}",
            $"P|1||{PatientId(order)}||{NameFirstLast(order)}||{Birth(order)}|{Gender(order)}||||||||||||||||||||||||||",
            $"O|1|1^1^1|{Barcode(order)}|{tests}|R|{d}|{d}|||John||||||Dr.Who|Department1|1|Dr.Tom||||||Q|||||",
            "L|1|N",
        };
    }
}

/// <summary>Mindray BS-240/BS-360 (ASTM, legacy MINDRAY240 без HL7): O|1|1^1^1|barcode|n^code^2^1\…|R|date|date||||||||Serum||||||||||Q|||||.</summary>
public sealed class MindrayAstmOrderBuilder : OrderBuilderBase
{
    public override string Template => "MINDRAY_ASTM";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var d = TsLegacy(now);
        int n = 0;
        var tests = string.Join("\\", Codes(order).Select(c => $"{++n}^{c}^2^1"));
        return new[]
        {
            $"H|\\^&|||MINDRAY240^01.03.07.03^123456|||||||SA|1394-97|{d}",
            $"P|1|||{PatientId(order)}|{NameLastFirst(order)}||{Birth(order)}|{Gender(order)}||||||||||||||||||||||||||",
            $"O|1|1^1^1|{Barcode(order)}|{tests}|R|{d}|{d}||||||||Serum||||||||||Q|||||",
            "L|1|N",
        };
    }
}

/// <summary>Mindray BS-серії (ASTM, відповідь на |RQ|): H|\^&amp;|||Mindry^^|…|SA|1394-97; O|1||barcode|^code^^\…|R|date|date||||||||serum||||||||||O|||||.</summary>
public sealed class MindrayAstmBsOrderBuilder : OrderBuilderBase
{
    public override string Template => "MINDRAY_ASTM_BS";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var d = TsLegacy(now);
        var tests = string.Join("\\", Codes(order).Select(c => $"^{c}^^"));
        return new[]
        {
            $"H|\\^&|||Mindry^^|||||||SA|1394-97|{d}",
            $"P|1|||{PatientId(order)}|{NameLastFirst(order)}||{Birth(order)}|{Gender(order)}||||||||||||||||||||||||||",
            $"O|1||{Barcode(order)}|{tests}|R|{d}|{d}||||||||serum||||||||||O|||||",
            "L|1|N",
        };
    }
}

/// <summary>
/// Mindray BS-240 (HL7 2.3.1): відповідь на QRY^Q02 — два повідомлення: QCK^Q02 (підтвердження запиту) та DSR^Q03
/// (QRD/QRF + DSP|1..28 демографія, DSP|21 штрихкод, DSP|22 sampleId, DSP|26 тип проби, DSP|29+ тести, DSC).
/// </summary>
public sealed class MindrayHl7OrderBuilder : OrderBuilderBase
{
    public override string Template => "MINDRAY_HL7";
    public override OrderOutputKind Output => OrderOutputKind.Hl7Messages;

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var d = TsLegacy(now);
        var pos = (ctx.SamplePosition ?? "1").Trim();
        if (pos.Contains('^')) pos = pos.Split('^')[0];
        var barcode = Barcode(order);
        var sampleType = string.IsNullOrWhiteSpace(order.SampleType) ? "serum" : order.SampleType.ToLowerInvariant();

        var qck = $"MSH|^~\\&|||||{d}||QCK^Q02|{pos}|P|2.3.1||||||ASCII|||\r" +
                  "MSA|AA|1|Message accepted||||0|\r" +
                  "ERR|0|\r" +
                  "QAK|SR|OK|\r";

        var sb = new StringBuilder();
        sb.Append($"MSH|^~\\&|||||{d}||DSR^Q03|{pos}|P|2.3.1||||||ASCII|||\r");
        sb.Append("MSA|AA|1|Message accepted||||0|\r");
        sb.Append("ERR|0|\r");
        sb.Append("QAK|SR|OK|\r");
        sb.Append($"QRD|{d}|R|D|1|||RD|{barcode}|OTH|||T|\r");
        sb.Append("QRF||||||RCT|COR|ALL||\r");
        for (int i = 1; i <= 28; i++)
        {
            string value = i switch
            {
                3 => NameFirstLast(order),
                4 => BirthFull(order),
                5 => Gender(order),
                21 => barcode,
                22 => ctx.SequenceNumber.ToString(CultureInfo.InvariantCulture),
                _ => "",
            };
            if (i == 26) sb.Append($"DSP|26|||{sampleType}||\r");
            else sb.Append($"DSP|{i}||{value}|||\r");
        }
        int n = 28;
        foreach (var code in Codes(order)) sb.Append($"DSP|{++n}||^{code}^^|||\r");
        sb.Append("DSC||\r");
        return new[] { qck, sb.ToString() };
    }

    public override IReadOnlyList<string> BuildNoOrderRecords(string barcode, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        // Mindray очікує принаймні QCK^Q02; DSR без тестів = «немає замовлення»
        var empty = new AnalyzerOrderDto { Barcode = barcode, SampleType = "serum" };
        var msgs = BuildRecords(empty, cfg, now, ctx);
        return ctx.SendNoOrderReply ? msgs : new[] { msgs[0] };
    }
}

/// <summary>Roche Cobas Integra 400 (текстовий протокол, LF): SOH / 09 CBINTEGRA 400 / STX / 53 barcode date SER / 54 000 00 A / 55 code … / ETX / EOT.</summary>
public sealed class IntegraOrderBuilder : OrderBuilderBase
{
    public override string Template => "INTEGRA";
    public override OrderOutputKind Output => OrderOutputKind.TextLines;
    public override string RecordTerminator => "\n";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var lines = new List<string>
        {
            ((char)AstmControl.SOH).ToString(),
            "09 CBINTEGRA 400    10",
            AstmControl.StxChar.ToString(),
            $"53 {Barcode(order).PadRight(15)} {now:dd/MM/yyyy} SER",
            "54 000 00 A",
        };
        foreach (var code in Codes(order)) lines.Add($"55 {code}");
        lines.Add(AstmControl.EtxChar.ToString());
        lines.Add(AstmControl.EotChar.ToString());
        return lines;
    }
}

/// <summary>Prestige 24i / BiOLiS: H|\^&amp;|||HOST^P_1|||||BiOLiS NEO^SYSTEM1||P|1|now; O|1|^barcode||^^^n^code^0\…|R||||||||||Serum||||||||||O. Поділ на 170.</summary>
public sealed class PrestigeOrderBuilder : OrderBuilderBase
{
    public override string Template => "PRESTIGE";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        int n = 0;
        var orderList = string.Join("\\", Codes(order).Select(c => $"^^^{++n}^{c}^0"));
        var barcode = Barcode(order).Replace("^", "");
        return new[]
        {
            $"H|\\^&|||HOST^P_1|||||BiOLiS NEO^SYSTEM1||P|1|{Ts(now)}",
            "P|1",
            SplitOrderList($"O|1|^{barcode}||", orderList, "|R||||||||||Serum||||||||||O", 170),
            "L|1|N",
        };
    }
}

/// <summary>Siemens RAPIDPoint: відповідь PAT_DEMOG_DATA на PAT_DEMOG_REQ (кадр із CRC). Також SMP_REQ будує RapidParser.</summary>
public sealed class RapidOrderBuilder : OrderBuilderBase
{
    public override string Template => "RAPID";
    public override OrderOutputKind Output => OrderOutputKind.RawFrames;

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var (iid, mod) = SplitIidMod(ctx.SamplePosition);
        var dob = order.Patient?.BirthDate?.ToString("ddMMMyyyy", CultureInfo.InvariantCulture) ?? "";
        var frame = RapidParser.BuildFrame("PAT_DEMOG_DATA", new[]
        {
            ("aMOD", mod), ("iIID", iid), ("iPID", Barcode(order)),
            ("iFNAME", FirstNameLatin(order)), ("iLNAME", LastNameLatin(order)),
            ("iSEX", Gender(order)), ("iDOB", dob),
        });
        return new[] { frame };
    }

    public override IReadOnlyList<string> BuildNoOrderRecords(string barcode, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        // Невідомий пацієнт — відправляємо порожню демографію, щоб прилад не чекав
        var (iid, mod) = SplitIidMod(ctx.SamplePosition);
        return new[] { RapidParser.BuildFrame("PAT_DEMOG_DATA", new[] { ("aMOD", mod), ("iIID", iid), ("iPID", barcode), ("iFNAME", ""), ("iLNAME", ""), ("iSEX", ""), ("iDOB", "") }) };
    }

    private static (string Iid, string Mod) SplitIidMod(string? pos)
    {
        if (string.IsNullOrWhiteSpace(pos)) return ("", "0500");
        var p = pos.Split('^');
        return (p[0].Trim(), p.Length > 1 && p[1].Trim().Length > 0 ? p[1].Trim() : "0500");
    }
}

/// <summary>Werfen ACL TOP: роздільник повторів «@»; на кожен штрихкод — P/O; L|1|F.</summary>
public sealed class AclTopOrderBuilder : OrderBuilderBase
{
    public override string Template => "ACLTOP";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
        => BuildBatch(new[] { order }, cfg, now, ctx)[0];

    public override IReadOnlyList<IReadOnlyList<string>> BuildBatch(IReadOnlyList<AnalyzerOrderDto> orders, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var d = TsLegacy(now);
        var records = new List<string> { $"H|@^\\|||1|||||1||P|1394-97|{d}" };
        int i = 1;
        foreach (var order in orders)
        {
            var tests = string.Join("@", Codes(order).Select(c => "^^^" + c));
            if (tests.Length == 0) continue;
            records.Add($"P|{i}||{PatientId(order)}||{NameFirstLast(order)}||{Birth(order)}|{Gender(order)}|||||");
            records.Add($"O|1|{Barcode(order)}||{tests}|R|{d}|||||A||||P||||||||||Q");
            i++;
        }
        records.Add("L|1|F");
        return new List<IReadOnlyList<string>> { records };
    }
}

/// <summary>Узагальнений HL7 ORM^O01 (MSH/PID/PV1/ORC/OBR на кожен тест).</summary>
public class Hl7OrmOrderBuilder : OrderBuilderBase
{
    public override string Template => "HL7_ORM";
    public override OrderOutputKind Output => OrderOutputKind.Hl7Messages;

    protected virtual string MessageType => "ORM^O01";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var d = Ts(now);
        var ctl = $"ML{now:yyyyMMddHHmmssfff}";
        var sb = new StringBuilder();
        sb.Append($"MSH|^~\\&|MedLinkLIS|MedLink|{cfg.Name}|{cfg.Code}|{d}||{MessageType}|{ctl}|P|2.3.1||||||UNICODE UTF-8\r");
        sb.Append($"PID|1||{PatientId(order)}||{NameLastFirst(order)}||{Birth(order)}|{Gender(order)}\r");
        sb.Append("PV1|1|O\r");
        int n = 1;
        var prio = Priority(order) == "S" ? "S" : "R";
        foreach (var t in order.Tests ?? new())
        {
            var code = string.IsNullOrWhiteSpace(t.AnalyzerCode) ? t.TestCode : t.AnalyzerCode;
            sb.Append($"ORC|NW|{order.OrderNumber}|{Barcode(order)}||||^^^{d}^^{prio}\r");
            sb.Append($"OBR|{n++}|{order.OrderNumber}|{Barcode(order)}|{code}^{t.TestCode}|{prio}|{d}|||||||||||||||||||{order.SampleType}\r");
        }
        return new[] { sb.ToString() };
    }
}

/// <summary>Узагальнений HL7 OML^O21 (MSH/PID/SPM/ORC/OBR).</summary>
public sealed class Hl7OmlOrderBuilder : Hl7OrmOrderBuilder
{
    public override string Template => "HL7_OML";
    protected override string MessageType => "OML^O21";

    public override IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var d = Ts(now);
        var ctl = $"ML{now:yyyyMMddHHmmssfff}";
        var sb = new StringBuilder();
        sb.Append($"MSH|^~\\&|MedLinkLIS|MedLink|{cfg.Name}|{cfg.Code}|{d}||OML^O21^OML_O21|{ctl}|P|2.5.1||||||UNICODE UTF-8\r");
        sb.Append($"PID|1||{PatientId(order)}||{NameLastFirst(order)}||{Birth(order)}|{Gender(order)}\r");
        sb.Append($"SPM|1|{Barcode(order)}||{order.SampleType}\r");
        int n = 1;
        var prio = Priority(order);
        foreach (var t in order.Tests ?? new())
        {
            var code = string.IsNullOrWhiteSpace(t.AnalyzerCode) ? t.TestCode : t.AnalyzerCode;
            sb.Append($"ORC|NW|{order.OrderNumber}|{Barcode(order)}\r");
            sb.Append($"OBR|{n++}|{order.OrderNumber}|{Barcode(order)}|{code}^{t.TestCode}|{prio}|{d}\r");
        }
        return new[] { sb.ToString() };
    }
}
