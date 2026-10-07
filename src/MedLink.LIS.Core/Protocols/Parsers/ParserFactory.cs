// =============================================================================
// MedLink LIS 4.0 — фабрика парсерів за parserKind профілю / протоколом.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LIS.Core.Contracts;

namespace MedLink.LIS.Core.Protocols.Parsers;

public static class ParserFactory
{
    private static readonly Dictionary<string, Func<IAnalyzerMessageParser>> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ASTM_GENERIC"] = () => new AstmGenericParser(),
        ["ASTM_COBAS"] = () => new AstmCobasParser(),
        ["ASTM_SYSMEX"] = () => new AstmSysmexParser(),
        ["ASTM_URISYS"] = () => new AstmUrisysParser(),
        ["ASTM_PENTRA"] = () => new AstmPentraParser(),
        ["ASTM_ABL"] = () => new AstmAblParser(),
        ["ASTM_STAGO"] = () => new AstmStagoParser(),
        ["ASTM_TOSOH"] = () => new AstmTosohParser(),
        ["ASTM_MAGLUMI"] = () => new AstmMaglumiParser(),
        ["ASTM_BIOKSEL"] = () => new AstmBioKselParser(),
        ["ASTM_MINDRAY"] = () => new AstmMindrayParser(),
        ["ASTM_HUMASTAR"] = () => new AstmHumaStarParser(),
        ["ASTM_VITROS"] = () => new AstmVitrosParser(),
        ["ASTM_IFLASH"] = () => new AstmIflashParser(),
        ["ASTM_MEK7300"] = () => new AstmMek7300Parser(),
        ["ASTM_ACLTOP"] = () => new AstmAclTopParser(),
        ["ASTM_UWAM"] = () => new AstmUwamParser(),
        ["ASTM_PHADIA"] = () => new AstmPhadiaParser(),
        ["ASTM_ERBA"] = () => new AstmErbaParser(),
        ["HL7_ORU"] = () => new Hl7OruParser(),
        ["HL7_MINDRAY"] = () => new Hl7MindrayParser(),
        ["HL7_ICHROMA"] = () => new Hl7IchromaParser(),
        ["TEXT_RAPID"] = () => new RapidParser(),
        ["TEXT_FUJI"] = () => new FujiParser(),
        ["TEXT_INTEGRA"] = () => new IntegraParser(),
        ["TEXT_CYAN"] = () => new CyanParser(),
        ["TEXT_HUMA5L"] = () => new CyanParser(),
        ["TEXT_KEYVALUE"] = () => new GenericKeyValueParser(),
        ["TEXT_UC1000"] = () => new Uc1000Parser(),
        ["TEXT_JUNIOR"] = () => new JuniorParser(),
        ["TEXT_APOTI"] = () => new ApotiParser(),
        ["TEXT_CLINTEC"] = () => new ClintecParser(),
        ["TEXT_TABULAR"] = () => new TabularParser(),
        ["TEXT_IRIS"] = () => new IrisParser(),
    };

    /// <summary>Усі зареєстровані види парсерів.</summary>
    public static IReadOnlyCollection<string> Kinds => Registry.Keys.ToList();

    public static bool IsKnown(string? kind) => kind != null && Registry.ContainsKey(kind);

    /// <summary>Створює парсер за видом; невідомий вид → ASTM_GENERIC (з урахуванням префікса HL7_/TEXT_).</summary>
    public static IAnalyzerMessageParser Create(string? kind)
    {
        if (kind != null && Registry.TryGetValue(kind, out var f)) return f();
        if (kind != null && kind.StartsWith("HL7", StringComparison.OrdinalIgnoreCase)) return new Hl7OruParser();
        if (kind != null && kind.StartsWith("TEXT", StringComparison.OrdinalIgnoreCase)) return new GenericKeyValueParser();
        return new AstmGenericParser();
    }

    /// <summary>
    /// Парсер для конфігурації аналізатора: за профілем типу (TypeCode → parserKind) із каталогу,
    /// інакше — за протоколом (HL7 → HL7_ORU, RAPID → TEXT_RAPID, FUJI → TEXT_FUJI, CYAN/HUMA5L → TEXT_CYAN, UC1000, JUNIOR, IRIS, TXT/TEXT → TEXT_KEYVALUE, інше → ASTM_GENERIC).
    /// </summary>
    public static IAnalyzerMessageParser ForConfig(AnalyzerConfigDto cfg, AnalyzerProfileCatalog? catalog = null)
    {
        catalog ??= AnalyzerProfileCatalog.Default;
        var profile = catalog.GetByCode(cfg.TypeCode) ?? catalog.GetByCode(cfg.Code);
        if (profile != null && IsKnown(profile.ParserKind)) return Create(profile.ParserKind);
        return Create(KindForProtocol(cfg.Protocol));
    }

    public static string KindForProtocol(string? protocol) => (protocol ?? "").Trim().ToUpperInvariant() switch
    {
        "HL7" => "HL7_ORU",
        "RAPID" => "TEXT_RAPID",
        "FUJI" => "TEXT_FUJI",
        "CYAN" or "HUMA5L" => "TEXT_CYAN",
        "UC1000" => "TEXT_UC1000",
        "JUNIOR" => "TEXT_JUNIOR",
        "IRIS" => "TEXT_IRIS",
        "TXT" or "TEXT" => "TEXT_KEYVALUE",
        _ => "ASTM_GENERIC",
    };
}
