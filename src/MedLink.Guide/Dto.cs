// DTO API модуля Guide (camelCase JSON). JSON-поля (стратегії, відбиток, rect, options) передаються як вкладений JSON.
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MedLink.Guide;

public static class GuideJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    public static JsonNode? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json); } catch (JsonException) { return null; }
    }

    public static string Stringify(JsonNode? node, string fallback) => node == null ? fallback : node.ToJsonString();
    public static string Stringify<T>(IEnumerable<T>? list) => JsonSerializer.Serialize(list ?? Enumerable.Empty<T>(), Options);
    public static List<string> ParseList(string? json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<List<string>>(json, Options) ?? new(); }
        catch (JsonException) { return new(); }
    }
}

public sealed class AppDto
{
    public string? Id { get; set; }
    public string Code { get; set; } = "";
    public string? Caption { get; set; }
    public string? BaseUrl { get; set; }
    public string? DefaultLocale { get; set; }
}

public sealed class FolderDto
{
    public string? Id { get; set; }
    public string? AppCode { get; set; }
    public string? ParentId { get; set; }
    /// <summary>Лише для імпорту/експорту: код батьківської папки.</summary>
    public string? ParentCode { get; set; }
    public string? TenantId { get; set; }
    public string? Code { get; set; }
    public string Caption { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
}

public sealed class MoveFolderDto
{
    public string? ParentId { get; set; }
    public int? SortOrder { get; set; }
}

public sealed class AnchorDto
{
    public string? Id { get; set; }
    public string? Code { get; set; }
    public string? Caption { get; set; }
    public string? RoutePattern { get; set; }
    public JsonNode? Strategies { get; set; }
    public JsonNode? Fingerprint { get; set; }
    public int? ParentLevels { get; set; }
    public string? ScreenshotId { get; set; }
    public string? HealthStatus { get; set; }
    public DateTime? HealthCheckedOn { get; set; }
    public string? LastFoundHow { get; set; }
}

public sealed class StepDto
{
    public string? Id { get; set; }
    public string? AnchorId { get; set; }
    public AnchorDto? Anchor { get; set; }
    public int SortOrder { get; set; }
    public string StepType { get; set; } = "info";
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Route { get; set; }
    public string Highlight { get; set; } = "element";
    public int ParentLevels { get; set; }
    public JsonNode? Rect { get; set; }
    public int Padding { get; set; } = 6;
    public string Placement { get; set; } = "auto";
    public string? InputValue { get; set; }
    public string InputMode { get; set; } = "show";
    public string Expect { get; set; } = "next";
    public string? WaitRoute { get; set; }
    public int TimeoutMs { get; set; } = 8000;
    public bool Optional { get; set; }
    public bool BlockOutside { get; set; } = true;
    public string? NarrationMediaId { get; set; }
    public string? Transcript { get; set; }
}

public sealed class VariableDto
{
    public string? Id { get; set; }
    public string Name { get; set; } = "";
    public string? Caption { get; set; }
    public string Kind { get; set; } = "fixed";
    public string? DefaultValue { get; set; }
    public JsonNode? Options { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ScenarioDto
{
    public string? Id { get; set; }
    public string? AppCode { get; set; }
    public string? FolderId { get; set; }
    /// <summary>Лише для імпорту/експорту: код папки.</summary>
    public string? FolderCode { get; set; }
    public string? TenantId { get; set; }
    public string? Code { get; set; }
    public string Caption { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string Status { get; set; } = GuideConst.Draft;
    public string? StartRoute { get; set; }
    public List<string> RequiredRoles { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int? EstMinutes { get; set; }
    public string Locale { get; set; } = "uk";
    public int SortOrder { get; set; }
    public int DraftVersion { get; set; } = 1;
    public int? PublishedVersion { get; set; }
    public DateTime? PublishedOn { get; set; }
    /// <summary>Номер версії знімка (для /play).</summary>
    public int? VersionNo { get; set; }
    /// <summary>Лише імпорт: опублікувати після імпорту.</summary>
    public bool? Publish { get; set; }
    public List<StepDto> Steps { get; set; } = new();
    public List<VariableDto> Variables { get; set; } = new();
}

public sealed class PublishDto
{
    public string? Note { get; set; }
}

public sealed class GuideBundle
{
    public string Format { get; set; } = "medlink-guide-bundle";
    public int Version { get; set; } = 1;
    public AppDto? App { get; set; }
    public List<FolderDto> Folders { get; set; } = new();
    public List<ScenarioDto> Scenarios { get; set; } = new();
}

public sealed class ImportResult
{
    public string AppCode { get; set; } = "";
    public int FoldersCreated { get; set; }
    public int ScenariosCreated { get; set; }
    public int ScenariosUpdated { get; set; }
    public int ScenariosPublished { get; set; }
}

public sealed class AnchorReportDto
{
    public string AnchorId { get; set; } = "";
    public string? ScenarioId { get; set; }
    public string? StepId { get; set; }
    /// <summary>ok | healed | missing</summary>
    public string Outcome { get; set; } = "ok";
    public string? How { get; set; }
    public decimal? Confidence { get; set; }
    public string? Route { get; set; }
    public JsonNode? NewStrategies { get; set; }
    public JsonNode? NewFingerprint { get; set; }
}

public sealed class MediaUploadDto
{
    public string Kind { get; set; } = "image";
    public string Mime { get; set; } = "application/octet-stream";
    public string? FileName { get; set; }
    public string Base64 { get; set; } = "";
    public string? Transcript { get; set; }
    public int? DurationMs { get; set; }
    public string? AppCode { get; set; }
}

public sealed class ProgressDto
{
    public string ScenarioId { get; set; } = "";
    public int? VersionNo { get; set; }
    public string Status { get; set; } = "started";
    public int LastStep { get; set; }
    public string Mode { get; set; } = "guided";
}

public sealed class GuideException : Exception
{
    public int Status { get; }
    public GuideException(int status, string message) : base(message) => Status = status;
}
