// Підрозділи лабораторії (секції), журнали відділень, шаблони робочих процесів проби, етапи обробки, обрані набори замовлення
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MedLink.LIS.Api.Data.Entities;

public static class SectionTypes
{
    public static readonly string[] All = { "CLINICAL_BIOCHEM", "HEMATOLOGY", "IMMUNO", "COAG", "URINALYSIS", "MICROBIOLOGY", "PATHOLOGY", "CYTOLOGY", "GENETICS", "OTHER" };
}

public static class JournalResetPeriods
{
    public const string Year = "YEAR", Month = "MONTH", Day = "DAY", Never = "NEVER";
    public static readonly string[] All = { Year, Month, Day, Never };
}

public static class DerivationTypes
{
    public const string Primary = "PRIMARY", Aliquot = "ALIQUOT", Cassette = "CASSETTE", Block = "BLOCK", Slide = "SLIDE", CulturePlate = "CULTURE_PLATE", Dilution = "DILUTION";
    public static readonly string[] All = { Primary, Aliquot, Cassette, Block, Slide, CulturePlate, Dilution };
}

[Table("lab_section")]
public class LabSection : GuidEntity
{
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>CLINICAL_BIOCHEM|HEMATOLOGY|IMMUNO|COAG|URINALYSIS|MICROBIOLOGY|PATHOLOGY|CYTOLOGY|GENETICS|OTHER</summary>
    [MaxLength(32)] public string SectionType { get; set; } = "OTHER";
    [MaxLength(64)] public string? DepartmentId { get; set; }
    /// <summary>Маска номера журналу: {yyyy} {yy} {MM} {dd} {seq6} {seq} {dayseq3} {dayseq}; напр. "{yyyy}-{seq6}", "{yy}{MM}{dd}/{dayseq3}", "S{yy}-{seq5}"</summary>
    [MaxLength(64)] public string JournalMask { get; set; } = "{yyyy}-{seq6}";
    /// <summary>YEAR | MONTH | DAY | NEVER</summary>
    [MaxLength(8)] public string JournalResetPeriod { get; set; } = "YEAR";
    /// <summary>Додатковий номер (напр. денний "{dayseq}").</summary>
    [MaxLength(64)] public string? SecondaryMask { get; set; }
    /// <summary>Автоматичне відкриття результату пацієнту одразу після верифікації.</summary>
    public bool AutoReleaseVerified { get; set; } = true;
    [MaxLength(32)] public string WorkflowTemplateCode { get; set; } = "CLINICAL";
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(DepartmentId))] public OrgDepartment? Department { get; set; }
}

[Table("lab_section_journal_entry")]
public class LabSectionJournalEntry : GuidEntity
{
    [MaxLength(64)] public string LabSectionId { get; set; } = "";
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(64)] public string? SampleId { get; set; }
    public string OrderTestIdsJson { get; set; } = "[]";
    [MaxLength(64)] public string JournalNumber { get; set; } = "";
    public long SequenceValue { get; set; }
    public int DayNumber { get; set; }
    [MaxLength(64)] public string? SecondaryNumber { get; set; }
    [MaxLength(16)] public string PeriodKey { get; set; } = "";
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? RegisteredById { get; set; }
    /// <summary>REGISTERED | IN_PROGRESS | COMPLETED | CANCELLED</summary>
    [MaxLength(16)] public string Status { get; set; } = "REGISTERED";

    [ForeignKey(nameof(LabSectionId))] public LabSection? Section { get; set; }
    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
    [ForeignKey(nameof(SampleId))] public LabOrderSample? Sample { get; set; }

    [NotMapped]
    public List<string> OrderTestIds
    {
        get => JsonSerializer.Deserialize<List<string>>(OrderTestIdsJson) ?? new();
        set => OrderTestIdsJson = JsonSerializer.Serialize(value);
    }
}

public sealed class WorkflowStage
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsRequired { get; set; } = true;
    public List<string> AllowedNext { get; set; } = new();
}

[Table("lab_workflow_template")]
public class LabWorkflowTemplate : AuditableEntity
{
    [Key, MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    public string StagesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;

    [NotMapped]
    public List<WorkflowStage> Stages
    {
        get => JsonSerializer.Deserialize<List<WorkflowStage>>(StagesJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();
        set => StagesJson = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}

[Table("lab_sample_stage_event")]
public class LabSampleStageEvent : GuidEntity
{
    [MaxLength(64)] public string SampleId { get; set; } = "";
    [MaxLength(32)] public string StageCode { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? ById { get; set; }
    [MaxLength(1024)] public string? Note { get; set; }
    public double? Temperature { get; set; }
    [MaxLength(64)] public string? InstrumentId { get; set; }
    public string? DataJson { get; set; }

    [ForeignKey(nameof(SampleId))] public LabOrderSample? Sample { get; set; }
}

[Table("lab_order_favorite")]
public class LabOrderFavorite : GuidEntity
{
    [MaxLength(64)] public string EmployeeId { get; set; } = "";
    [MaxLength(128)] public string Name { get; set; } = "";
    public string ProfileIdsJson { get; set; } = "[]";
    public string TestIdsJson { get; set; } = "[]";
    public int DisplayOrder { get; set; }

    [NotMapped] public List<string> ProfileIds { get => JsonSerializer.Deserialize<List<string>>(ProfileIdsJson) ?? new(); set => ProfileIdsJson = JsonSerializer.Serialize(value); }
    [NotMapped] public List<string> TestIds { get => JsonSerializer.Deserialize<List<string>>(TestIdsJson) ?? new(); set => TestIdsJson = JsonSerializer.Serialize(value); }
}
