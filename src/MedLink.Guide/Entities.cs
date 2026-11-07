// Сутності модуля Guide. Конвенції ЛІС/evomis: string-GUID, аудит-колонки, record_state (2 — активний, 4 — видалений).
// Таблиці gd_*, колонки snake_case (відповідають db/postgres/guide/001_guide_schema.sql).
using System.ComponentModel.DataAnnotations;

namespace MedLink.Guide;

public static class RecordStates
{
    public const int Active = 2;
    public const int Deleted = 4;
}

public static class GuideConst
{
    public const string EmptyGuid = "00000000-0000-0000-0000-000000000000";
    public const string Draft = "draft", Published = "published", Archived = "archived";
}

public abstract class GuideAuditable
{
    [Key, MaxLength(64)] public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string CreatedBy { get; set; } = GuideConst.EmptyGuid;
    public DateTime? ModifiedOn { get; set; }
    [MaxLength(64)] public string ModifiedBy { get; set; } = GuideConst.EmptyGuid;
    public int RecordState { get; set; } = RecordStates.Active;
}

public class GuideApp : GuideAuditable
{
    public string Code { get; set; } = "";
    public string Caption { get; set; } = "";
    public string? BaseUrl { get; set; }
    public string DefaultLocale { get; set; } = "uk";
}

public class GuideFolder : GuideAuditable
{
    public string AppId { get; set; } = "";
    public string? ParentId { get; set; }
    public string? TenantId { get; set; }
    public string? Code { get; set; }
    public string Caption { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
}

public class GuideAnchor : GuideAuditable
{
    public string AppId { get; set; } = "";
    public string? TenantId { get; set; }
    public string? Code { get; set; }
    public string Caption { get; set; } = "";
    public string? RoutePattern { get; set; }
    /// <summary>jsonb: [{type,value,weight}].</summary>
    public string Strategies { get; set; } = "[]";
    /// <summary>jsonb: {tag,text,classes,attrs,role,label,ancestors,rectRatio}.</summary>
    public string Fingerprint { get; set; } = "{}";
    public int ParentLevels { get; set; }
    public string? ScreenshotId { get; set; }
    public string HealthStatus { get; set; } = "unknown";
    public DateTime? HealthCheckedOn { get; set; }
    public string? LastFoundHow { get; set; }
}

public class GuideScenario : GuideAuditable
{
    public string AppId { get; set; } = "";
    public string? FolderId { get; set; }
    public string? TenantId { get; set; }
    public string? Code { get; set; }
    public string Caption { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string Status { get; set; } = GuideConst.Draft;
    public string? StartRoute { get; set; }
    public string RequiredRoles { get; set; } = "[]";
    public string Tags { get; set; } = "[]";
    public int? EstMinutes { get; set; }
    public string Locale { get; set; } = "uk";
    public int SortOrder { get; set; }
    public int DraftVersion { get; set; } = 1;
    public int? PublishedVersion { get; set; }
    public DateTime? PublishedOn { get; set; }
    public string? PublishedBy { get; set; }
}

public class GuideScenarioVariable : GuideAuditable
{
    public string ScenarioId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Caption { get; set; }
    public string Kind { get; set; } = "fixed";
    public string? DefaultValue { get; set; }
    public string Options { get; set; } = "{}";
    public int SortOrder { get; set; }
}

public class GuideStep : GuideAuditable
{
    public string ScenarioId { get; set; } = "";
    public string? AnchorId { get; set; }
    public int SortOrder { get; set; }
    public string StepType { get; set; } = "info";
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Route { get; set; }
    public string Highlight { get; set; } = "element";
    public int ParentLevels { get; set; }
    public string? Rect { get; set; }
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

public class GuideScenarioVersion
{
    [Key, MaxLength(64)] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ScenarioId { get; set; } = "";
    public int VersionNo { get; set; }
    public string Snapshot { get; set; } = "{}";
    public string? Note { get; set; }
    public string PublishedBy { get; set; } = GuideConst.EmptyGuid;
    public DateTime PublishedOn { get; set; } = DateTime.UtcNow;
}

public class GuideMedia : GuideAuditable
{
    public string? AppId { get; set; }
    public string Kind { get; set; } = "image";
    public string Mime { get; set; } = "application/octet-stream";
    public string? FileName { get; set; }
    public int SizeBytes { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string? Transcript { get; set; }
    public int? DurationMs { get; set; }
}

public class GuideProgress
{
    [Key, MaxLength(64)] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ScenarioId { get; set; } = "";
    public int VersionNo { get; set; }
    public string EmployeeId { get; set; } = GuideConst.EmptyGuid;
    public string Status { get; set; } = "started";
    public int LastStep { get; set; }
    public DateTime StartedOn { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedOn { get; set; }
    public string Mode { get; set; } = "guided";
}

public class GuideAnchorReport
{
    [Key, MaxLength(64)] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AnchorId { get; set; } = "";
    public string? ScenarioId { get; set; }
    public string? StepId { get; set; }
    public string Outcome { get; set; } = "ok";
    public string? How { get; set; }
    public decimal? Confidence { get; set; }
    public string? Route { get; set; }
    public string? NewStrategies { get; set; }
    public string? NewFingerprint { get; set; }
    public string? EmployeeId { get; set; }
    public DateTime ReportedOn { get; set; } = DateTime.UtcNow;
    public bool Resolved { get; set; }
}
