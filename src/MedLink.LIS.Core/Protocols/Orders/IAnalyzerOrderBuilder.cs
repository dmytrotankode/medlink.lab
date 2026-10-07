// =============================================================================
// MedLink LIS 4.0 — контракт побудовника замовлень (worklist download) для приладу
// та фабрика за orderTemplate профілю. Порт gen_lab_order (Simplex MySQL).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Globalization;
using MedLink.LIS.Core.Contracts;

namespace MedLink.LIS.Core.Protocols.Orders;

/// <summary>Форма результату побудовника: як сесія має відправити побудовані рядки.</summary>
public enum OrderOutputKind
{
    /// <summary>Записи ASTM (H/P/O/L …): сесія кадрує їх (STX fn … CR ETX cs CR LF) і веде ENQ/ACK/EOT-сесію.</summary>
    AstmRecords = 0,
    /// <summary>Повні повідомлення HL7 (сегменти розділені CR): кожен елемент загортається у MLLP і відправляється окремо.</summary>
    Hl7Messages = 1,
    /// <summary>Готові текстові кадри (із керуючими символами та CRC): відправляються як є, один за одним.</summary>
    RawFrames = 2,
    /// <summary>Рядки тексту, що з'єднуються <see cref="IAnalyzerOrderBuilder.RecordTerminator"/> та відправляються одним блоком.</summary>
    TextLines = 3,
}

/// <summary>Контекст побудови: дані із запиту приладу, яких немає у AnalyzerOrderDto.</summary>
public sealed class OrderBuildContext
{
    public static OrderBuildContext Empty { get; } = new();

    /// <summary>«Сирий» токен штрихкоду із Q-3 / QRD (напр. Cobas "1^12345678^S1^SC", Sysmex "^^  1^1026004819^B").</summary>
    public string? RawQueryToken { get; init; }
    /// <summary>Позиція/тип проби: Cobas — хвіст токена ("S1^SC"), Mindray HL7 — MSH-10, RAPID — "iIID^aMOD".</summary>
    public string? SamplePosition { get; init; }
    /// <summary>Порядковий номер (Mindray DSP|22 sampleId, нумерація P-записів у пакетах).</summary>
    public int SequenceNumber { get; init; } = 1;
    /// <summary>Чи відправляти відповідь «замовлення відсутнє» (O-26 = Y), коли сервер не знайшов пробу. Legacy не відповідав.</summary>
    public bool SendNoOrderReply { get; init; }
}

/// <summary>Побудовник повідомлення-замовлення (worklist) для конкретного сімейства приладів.</summary>
public interface IAnalyzerOrderBuilder
{
    /// <summary>Код шаблону (orderTemplate): ASTM_GENERIC, COBAS_E411, MINDRAY_HL7 …</summary>
    string Template { get; }
    OrderOutputKind Output { get; }
    /// <summary>Роздільник рядків для <see cref="OrderOutputKind.TextLines"/>.</summary>
    string RecordTerminator { get; }

    /// <summary>
    /// Будує записи замовлення для однієї проби. Для ASTM кожен елемент — один запис без кадрування та без CR;
    /// символ ETB (0x17) усередині запису — примусова межа кадру (Cobas 165/170).
    /// </summary>
    IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now);

    /// <summary>Те саме з контекстом запиту.</summary>
    IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx);

    /// <summary>Пакет замовлень (batch download для приладів без запиту або багато-штрихкодовий запит ACL TOP). Кожен елемент — окреме повідомлення.</summary>
    IReadOnlyList<IReadOnlyList<string>> BuildBatch(IReadOnlyList<AnalyzerOrderDto> orders, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx);

    /// <summary>Відповідь «замовлення не знайдено» (порожній список = не відповідати, як у legacy).</summary>
    IReadOnlyList<string> BuildNoOrderRecords(string barcode, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx);
}

/// <summary>Базовий клас побудовників зі спільними допоміжними функціями (дата, пацієнт, коди тестів).</summary>
public abstract class OrderBuilderBase : IAnalyzerOrderBuilder
{
    public abstract string Template { get; }
    public virtual OrderOutputKind Output => OrderOutputKind.AstmRecords;
    public virtual string RecordTerminator => "\r";

    public IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now)
        => BuildRecords(order, cfg, now, OrderBuildContext.Empty);

    public abstract IReadOnlyList<string> BuildRecords(AnalyzerOrderDto order, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx);

    public virtual IReadOnlyList<IReadOnlyList<string>> BuildBatch(IReadOnlyList<AnalyzerOrderDto> orders, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        var list = new List<IReadOnlyList<string>>();
        int seq = ctx.SequenceNumber;
        foreach (var o in orders)
        {
            list.Add(BuildRecords(o, cfg, now, new OrderBuildContext { RawQueryToken = ctx.RawQueryToken, SamplePosition = ctx.SamplePosition, SequenceNumber = seq++, SendNoOrderReply = ctx.SendNoOrderReply }));
        }
        return list;
    }

    public virtual IReadOnlyList<string> BuildNoOrderRecords(string barcode, AnalyzerConfigDto cfg, DateTime now, OrderBuildContext ctx)
    {
        if (!ctx.SendNoOrderReply) return Array.Empty<string>();
        return new[]
        {
            $"H|\\^&|||MedLinkLIS|||||||P|E1394-97|{Ts(now)}",
            "P|1",
            $"O|1|{barcode}||||||||||||||||||||||||Y",
            "L|1|N",
        };
    }

    // ---------- допоміжні ----------

    /// <summary>yyyyMMddHHmmss</summary>
    protected static string Ts(DateTime dt) => dt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    /// <summary>Legacy gen_lab_order використовував now()+3 хв у більшості шаблонів.</summary>
    protected static string TsLegacy(DateTime now) => Ts(now.AddMinutes(3));

    protected static string Birth(AnalyzerOrderDto o) => o.Patient?.BirthDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "";
    protected static string BirthFull(AnalyzerOrderDto o) => o.Patient?.BirthDate?.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) ?? "";

    protected static string Gender(AnalyzerOrderDto o) => (o.Patient?.Gender ?? "U").ToUpperInvariant() switch { "M" => "M", "F" => "F", _ => "" };

    protected static string LastNameLatin(AnalyzerOrderDto o) => Transliteration.LatinOrTransliterate(o.Patient?.LastNameLatin, o.Patient?.LastName);
    protected static string FirstNameLatin(AnalyzerOrderDto o) => Transliteration.LatinOrTransliterate(o.Patient?.FirstNameLatin, o.Patient?.FirstName);

    /// <summary>"Last^First" латиницею.</summary>
    protected static string NameLastFirst(AnalyzerOrderDto o) => $"{LastNameLatin(o)}^{FirstNameLatin(o)}";
    /// <summary>"First^Last" латиницею (Beckman, Mindray HL7 у legacy).</summary>
    protected static string NameFirstLast(AnalyzerOrderDto o) => $"{FirstNameLatin(o)}^{LastNameLatin(o)}";

    protected static string PatientId(AnalyzerOrderDto o) => o.Patient?.Id ?? "";

    /// <summary>Код тесту для приладу: analyzerCode, інакше testCode. Без дублікатів, із збереженням порядку.</summary>
    protected static List<string> Codes(AnalyzerOrderDto o)
    {
        var list = new List<string>();
        foreach (var t in o.Tests ?? new List<AnalyzerOrderTestDto>())
        {
            var c = string.IsNullOrWhiteSpace(t.AnalyzerCode) ? t.TestCode : t.AnalyzerCode;
            c = (c ?? "").Trim();
            if (c.Length > 0 && !list.Contains(c)) list.Add(c);
        }
        return list;
    }

    protected static string Priority(AnalyzerOrderDto o) => string.Equals(o.Priority, "S", StringComparison.OrdinalIgnoreCase) ? "S" : "R";

    /// <summary>Штрихкод без службових символів.</summary>
    protected static string Barcode(AnalyzerOrderDto o) => (o.Barcode ?? "").Trim().Trim('^', '!', ' ');

    /// <summary>Розбиває довгий список тестів у O-записі на два кадри за legacy-правилом (165/170 символів), вставляючи ETB.</summary>
    protected static string SplitOrderList(string prefix, string orderList, string suffix, int splitAt)
    {
        if (orderList.Length < splitAt) return prefix + orderList + suffix;
        return prefix + orderList[..splitAt] + AstmControl.EtbChar + orderList[splitAt..] + suffix;
    }
}

/// <summary>Фабрика побудовників за orderTemplate.</summary>
public static class OrderBuilderFactory
{
    private static readonly Dictionary<string, Func<IAnalyzerOrderBuilder>> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ASTM_GENERIC"] = () => new AstmGenericOrderBuilder(),
        ["COBAS_C111"] = () => new CobasC111OrderBuilder(),
        ["COBAS_C311"] = () => new CobasC311OrderBuilder(),
        ["COBAS_E411"] = () => new CobasE411OrderBuilder(),
        ["SYSMEX_XN"] = () => new SysmexXnOrderBuilder(),
        ["SYSMEX_CS2500"] = () => new SysmexCs2500OrderBuilder(),
        ["SYSMEX_CA600"] = () => new SysmexCa600OrderBuilder(),
        ["PENTRA"] = () => new PentraOrderBuilder(),
        ["TOSOH"] = () => new TosohOrderBuilder(),
        ["BIOKSEL"] = () => new BioKselOrderBuilder(),
        ["STAGO"] = () => new StagoOrderBuilder(),
        ["MAGLUMI"] = () => new MaglumiOrderBuilder(),
        ["HUMASTAR"] = () => new HumaStarOrderBuilder(),
        ["BECKMAN_ACCESS"] = () => new BeckmanAccessOrderBuilder(),
        ["ACCESS_ASTM"] = () => new AccessAstmOrderBuilder(),
        ["MINDRAY_ASTM"] = () => new MindrayAstmOrderBuilder(),
        ["MINDRAY_ASTM_BS"] = () => new MindrayAstmBsOrderBuilder(),
        ["MINDRAY_HL7"] = () => new MindrayHl7OrderBuilder(),
        ["INTEGRA"] = () => new IntegraOrderBuilder(),
        ["PRESTIGE"] = () => new PrestigeOrderBuilder(),
        ["RAPID"] = () => new RapidOrderBuilder(),
        ["ACLTOP"] = () => new AclTopOrderBuilder(),
        ["HL7_ORM"] = () => new Hl7OrmOrderBuilder(),
        ["HL7_OML"] = () => new Hl7OmlOrderBuilder(),
        ["NONE"] = () => new NoneOrderBuilder(),
    };

    public static IReadOnlyCollection<string> Templates => Registry.Keys.ToList();
    public static bool IsKnown(string? template) => template != null && Registry.ContainsKey(template);

    /// <summary>Побудовник за шаблоном; невідомий → ASTM_GENERIC.</summary>
    public static IAnalyzerOrderBuilder Create(string? template)
        => template != null && Registry.TryGetValue(template, out var f) ? f() : new AstmGenericOrderBuilder();

    /// <summary>Побудовник для конфігурації: cfg.OrderTemplate, інакше профіль типу, інакше за протоколом.</summary>
    public static IAnalyzerOrderBuilder ForConfig(AnalyzerConfigDto cfg, AnalyzerProfileCatalog? catalog = null)
    {
        if (IsKnown(cfg.OrderTemplate) && !string.Equals(cfg.OrderTemplate, "ASTM_GENERIC", StringComparison.OrdinalIgnoreCase))
            return Create(cfg.OrderTemplate);
        catalog ??= AnalyzerProfileCatalog.Default;
        var profile = catalog.GetByCode(cfg.TypeCode) ?? catalog.GetByCode(cfg.Code);
        if (profile != null && IsKnown(profile.OrderTemplate)) return Create(profile.OrderTemplate);
        if (string.Equals(cfg.Protocol, "HL7", StringComparison.OrdinalIgnoreCase)) return new Hl7OrmOrderBuilder();
        return Create(cfg.OrderTemplate);
    }
}
