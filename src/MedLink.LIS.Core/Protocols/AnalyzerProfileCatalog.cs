// =============================================================================
// MedLink LIS 4.0 — каталог профілів типів аналізаторів (ac_analyzer_type Simplex),
// вбудований ресурс Resources/analyzer_types.json.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedLink.LIS.Core.Contracts;

namespace MedLink.LIS.Core.Protocols;

/// <summary>Профіль типу аналізатора (рядок ac_analyzer_type + призначені парсер/побудовник).</summary>
public sealed class AnalyzerTypeProfile
{
    public int Id { get; set; }
    /// <summary>Код типу (analyzer_type_code), напр. SYSMEXXN, COBAS411. Може містити пробіли/кирилицю (legacy).</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Manufacturer { get; set; }
    /// <summary>HEMATOLOGY | BIOCHEM | IMMUNO | COAG | URINE | BLOODGAS | OTHER</summary>
    public string Category { get; set; } = "OTHER";
    /// <summary>ASTM | ASTM_ASK | ASTM2 | HL7 | TEXT | HUMA5L | UC1000 | CYAN | JUNIOR | IRIS | RAPID | FUJI | TXT</summary>
    public string ExchType { get; set; } = "ASTM";
    public string? BopBase64 { get; set; }
    public string? EopBase64 { get; set; }
    public bool ControlSum { get; set; } = true;
    public int SleepMs { get; set; } = 100;
    public bool FullText { get; set; }
    /// <summary>Ключ побудовника замовлень (OrderBuilderFactory).</summary>
    public string OrderTemplate { get; set; } = "ASTM_GENERIC";
    /// <summary>Ключ парсера (ParserFactory).</summary>
    public string ParserKind { get; set; } = "ASTM_GENERIC";
    /// <summary>Максимальна довжина кадру ASTM (240 за замовчуванням).</summary>
    public int MaxFrameLen { get; set; } = AstmFrameCodec.DefaultMaxFrameLen;
    /// <summary>Чи підтримує прилад запит замовлень (host query).</summary>
    public bool SupportsQuery { get; set; } = true;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Байти початку пакета (null — ENQ для ASTM).</summary>
    [JsonIgnore] public byte[]? Bop => TextProtocolFramer.DecodeBase64(BopBase64);
    /// <summary>Байти кінця пакета (null — EOT для ASTM).</summary>
    [JsonIgnore] public byte[]? Eop => TextProtocolFramer.DecodeBase64(EopBase64);

    /// <summary>Застосовує профіль до конфігурації аналізатора (там, де поля не задані сервером).</summary>
    public void ApplyDefaults(AnalyzerConfigDto cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.Protocol)) cfg.Protocol = ExchType;
        if (string.IsNullOrWhiteSpace(cfg.OrderTemplate) || cfg.OrderTemplate == "ASTM_GENERIC") cfg.OrderTemplate = OrderTemplate;
        cfg.Framing ??= new AnalyzerFramingDto();
        if (cfg.Framing.BopBase64 == null && BopBase64 != null) cfg.Framing.BopBase64 = BopBase64;
        if (cfg.Framing.EopBase64 == null && EopBase64 != null) cfg.Framing.EopBase64 = EopBase64;
        if (cfg.Framing.MaxFrameLen <= 0 || cfg.Framing.MaxFrameLen == AstmFrameCodec.DefaultMaxFrameLen) cfg.Framing.MaxFrameLen = MaxFrameLen;
        if (cfg.Framing.SleepMs <= 0) cfg.Framing.SleepMs = SleepMs;
    }
}

/// <summary>Каталог профілів типів аналізаторів із вбудованого JSON.</summary>
public sealed class AnalyzerProfileCatalog
{
    public const string ResourceName = "MedLink.LIS.Core.Resources.analyzer_types.json";

    private static readonly Lazy<AnalyzerProfileCatalog> _default = new(LoadEmbedded);
    private readonly List<AnalyzerTypeProfile> _profiles;
    private readonly Dictionary<string, AnalyzerTypeProfile> _byCode;

    public AnalyzerProfileCatalog(IEnumerable<AnalyzerTypeProfile> profiles)
    {
        _profiles = profiles.ToList();
        _byCode = new Dictionary<string, AnalyzerTypeProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _profiles)
        {
            _byCode[p.Code] = p;
            _byCode.TryAdd(NormalizeCode(p.Code), p);
        }
    }

    /// <summary>Каталог із вбудованого ресурсу (кешується).</summary>
    public static AnalyzerProfileCatalog Default => _default.Value;

    public IReadOnlyList<AnalyzerTypeProfile> All => _profiles;
    public int Count => _profiles.Count;

    /// <summary>Пошук за кодом (без урахування регістру; пробіли/дефіси/підкреслення ігноруються).</summary>
    public AnalyzerTypeProfile? GetByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (_byCode.TryGetValue(code.Trim(), out var p)) return p;
        return _byCode.TryGetValue(NormalizeCode(code), out p) ? p : null;
    }

    public AnalyzerTypeProfile? GetById(int id) => _profiles.FirstOrDefault(p => p.Id == id);

    /// <summary>Нормалізація коду: верхній регістр, без пробілів, дефісів, підкреслень; кирилична «С» → латинська «C».</summary>
    public static string NormalizeCode(string code)
    {
        var chars = code.Trim().ToUpperInvariant().Where(c => c != ' ' && c != '-' && c != '_').Select(c => c == 'С' ? 'C' : c).ToArray();
        return new string(chars);
    }

    public static AnalyzerProfileCatalog LoadEmbedded()
    {
        var asm = typeof(AnalyzerProfileCatalog).GetTypeInfo().Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Вбудований ресурс {ResourceName} не знайдено");
        return LoadFromStream(stream);
    }

    public static AnalyzerProfileCatalog LoadFromStream(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var list = JsonSerializer.Deserialize<List<AnalyzerTypeProfile>>(stream, options) ?? new List<AnalyzerTypeProfile>();
        return new AnalyzerProfileCatalog(list);
    }

    public static AnalyzerProfileCatalog LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var list = JsonSerializer.Deserialize<List<AnalyzerTypeProfile>>(json, options) ?? new List<AnalyzerTypeProfile>();
        return new AnalyzerProfileCatalog(list);
    }

    /// <summary>Будує конфігурацію аналізатора «за замовчуванням» для профілю (для CLI-команд test / preview-order без сервера).</summary>
    public AnalyzerConfigDto CreateDefaultConfig(string typeCode, string? analyzerId = null)
    {
        var profile = GetByCode(typeCode) ?? throw new KeyNotFoundException($"Профіль типу аналізатора «{typeCode}» не знайдено у каталозі");
        var cfg = new AnalyzerConfigDto
        {
            AnalyzerId = analyzerId ?? Guid.NewGuid().ToString(),
            Code = NormalizeCode(profile.Code),
            Name = profile.Name,
            TypeCode = profile.Code,
            Protocol = profile.ExchType,
            OrderTemplate = profile.OrderTemplate,
            AutoQueryOrders = profile.SupportsQuery,
            Framing = new AnalyzerFramingDto
            {
                BopBase64 = profile.BopBase64,
                EopBase64 = profile.EopBase64,
                Checksum = profile.ControlSum,
                SleepMs = profile.SleepMs,
                MaxFrameLen = profile.MaxFrameLen,
            },
        };
        return cfg;
    }
}
