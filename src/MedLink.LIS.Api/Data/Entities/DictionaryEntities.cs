// Довідники ЛІС (біоматеріали, пробірки, методики, типи аналізаторів, тести, профілі, норми, reflex)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MedLink.LIS.Api.Data.Entities;

[Table("lab_biomaterial_type")]
public class LabBiomaterialType : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    [MaxLength(128)] public string? DefaultContainer { get; set; }
    public int StabilityHours { get; set; } = 24;
    [MaxLength(32)] public string TemperatureRegime { get; set; } = "+2..+8°C";
    public bool IsActive { get; set; } = true;
}

[Table("lab_tube_type")]
public class LabTubeType : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>#hex (ISO 6710)</summary>
    [MaxLength(16)] public string ColorCode { get; set; } = "#4274A7";
    [MaxLength(128)] public string? Anticoagulant { get; set; }
    public double VolumeMl { get; set; } = 4.0;
    /// <summary>Порядок забору CLSI</summary>
    public int OrderOfDrawIndex { get; set; } = 99;
    public int InversionsCount { get; set; } = 8;
    public bool IsActive { get; set; } = true;
}

[Table("lab_method_type")]
public class LabMethodType : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

[Table("lab_analyzer_type")]
public class LabAnalyzerType : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    [MaxLength(128)] public string? Manufacturer { get; set; }
    /// <summary>HEMATOLOGY|BIOCHEM|IMMUNO|COAG|URINE|BLOODGAS|OTHER</summary>
    [MaxLength(32)] public string Category { get; set; } = "OTHER";
    /// <summary>ASTM|ASTM_ASK|ASTM2|HL7|TEXT|HUMA5L|UC1000|CYAN|JUNIOR|IRIS|RAPID|FUJI|TXT</summary>
    [MaxLength(16)] public string ExchType { get; set; } = "ASTM";
    [MaxLength(64)] public string? BopBase64 { get; set; }
    [MaxLength(64)] public string? EopBase64 { get; set; }
    public bool ControlSum { get; set; } = true;
    public int SleepMs { get; set; } = 100;
    public bool FullText { get; set; }
    [MaxLength(64)] public string OrderTemplate { get; set; } = "ASTM_GENERIC";
    [MaxLength(64)] public string ParserKind { get; set; } = "ASTM_GENERIC";
    public bool IsActive { get; set; } = true;
}

[Table("lab_test_definition")]
public class LabTestDefinition : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    [MaxLength(64)] public string? ShortName { get; set; }
    [MaxLength(32)] public string? LoincCode { get; set; }
    [MaxLength(64)] public string Unit { get; set; } = "";
    public int DecimalPlaces { get; set; } = 2;
    /// <summary>NUMERIC|TEXT|DROPDOWN|CALCULATED</summary>
    [MaxLength(16)] public string ResultType { get; set; } = "NUMERIC";
    public string? DropdownOptionsJson { get; set; }
    public string? Formula { get; set; }
    [MaxLength(64)] public string Category { get; set; } = "BIOCHEM";
    public int BiomaterialTypeId { get; set; }
    public int? TubeTypeId { get; set; }
    public int? MethodId { get; set; }
    /// <summary>Код методики для каскаду норм (напр. HEX_IFCC).</summary>
    [MaxLength(64)] public string? MethodCode { get; set; }
    /// <summary>Підрозділ лабораторії (секція), що виконує тест.</summary>
    [MaxLength(64)] public string? LabSectionId { get; set; }
    public double? DeltaCheckMaxPct { get; set; }
    public int DeltaCheckHours { get; set; } = 72;
    public bool RequiresManualVerification { get; set; }
    public bool IsQcTracked { get; set; } = true;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(BiomaterialTypeId))] public LabBiomaterialType? BiomaterialType { get; set; }
    [ForeignKey(nameof(TubeTypeId))] public LabTubeType? TubeType { get; set; }
    [ForeignKey(nameof(MethodId))] public LabMethodType? Method { get; set; }
    [ForeignKey(nameof(LabSectionId))] public LabSection? LabSection { get; set; }

    [NotMapped]
    public List<string> DropdownOptions
    {
        get => string.IsNullOrEmpty(DropdownOptionsJson) ? new() : JsonSerializer.Deserialize<List<string>>(DropdownOptionsJson) ?? new();
        set => DropdownOptionsJson = value == null || value.Count == 0 ? null : JsonSerializer.Serialize(value);
    }
}

[Table("lab_test_profile")]
public class LabTestProfile : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(512)] public string Name { get; set; } = "";
    [MaxLength(128)] public string Category { get; set; } = "";
    public int TurnaroundHours { get; set; } = 24;
    public bool FastingRequired { get; set; } = true;
    public decimal Price { get; set; }
    public int? DefaultBiomaterialTypeId { get; set; }
    public int? DefaultTubeTypeId { get; set; }
    /// <summary>Послуга МІС evomis (dct_service): код/назва/ціна беруться звідти, ЛІС зберігає лише лабораторні атрибути (як dct_service_lab у Simplex).</summary>
    [MaxLength(64)] public string? MisServiceId { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(MisServiceId))] public DctService? MisService { get; set; }
    public List<LabTestProfileItem> Items { get; set; } = new();
}

[Table("lab_test_profile_item")]
public class LabTestProfileItem : GuidEntity
{
    [MaxLength(64)] public string ProfileId { get; set; } = "";
    [MaxLength(64)] public string TestId { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsRequired { get; set; } = true;

    [ForeignKey(nameof(ProfileId))] public LabTestProfile? Profile { get; set; }
    [ForeignKey(nameof(TestId))] public LabTestDefinition? Test { get; set; }
}

/// <summary>Каскад норм Simplex: dct_service_lab_norm + dct_service_lab_nv + dct_service_lab_attribute.</summary>
[Table("lab_reference_layer")]
public class LabReferenceLayer : GuidEntity
{
    [MaxLength(64)] public string? TestId { get; set; }
    [MaxLength(64)] public string TestCode { get; set; } = "";
    [MaxLength(64)] public string? MethodCode { get; set; }
    [MaxLength(256)] public string? MethodName { get; set; }
    /// <summary>BASELINE|DEMOGRAPHIC|CLINICAL_ICD10|MENSTRUAL_PHASE|PREGNANCY</summary>
    [MaxLength(32)] public string LayerType { get; set; } = "DEMOGRAPHIC";
    public int PriorityOrder { get; set; } = 40;
    [MaxLength(256)] public string NormName { get; set; } = "";
    [MaxLength(8)] public string Gender { get; set; } = "ANY";
    public bool IsGender { get; set; }
    [MaxLength(8)] public string AgeUnit { get; set; } = "YEARS";
    public double AgeFrom { get; set; }
    public double AgeTo { get; set; } = 120;
    public bool IsAge { get; set; }
    public bool IsMenstrualPhase { get; set; }
    [MaxLength(32)] public string? MenstrualPhase { get; set; }
    public bool IsPregnancy { get; set; }
    public int? PregnancyWeekFrom { get; set; }
    public int? PregnancyWeekTo { get; set; }
    [MaxLength(16)] public string? Icd10Code { get; set; }
    public double? NormLow { get; set; }
    public double? NormHigh { get; set; }
    public double? CritLow { get; set; }
    public double? CritHigh { get; set; }
    public string? NormText { get; set; }
    [MaxLength(64)] public string? Unit { get; set; }
    public double? DeltaCheckMaxPct { get; set; }
    /// <summary>norm_code — код показника на приладі</summary>
    [MaxLength(64)] public string? AnalyzerCode { get; set; }
    public double? Dilution { get; set; }
    public bool IsActive { get; set; } = true;
}

[Table("lab_reflex_rule")]
public class LabReflexRule : GuidEntity
{
    [MaxLength(64)] public string TriggerTestCode { get; set; } = "";
    /// <summary>&lt;, &lt;=, &gt;, &gt;=, ==, OUT_OF_RANGE, CRITICAL</summary>
    [MaxLength(16)] public string ConditionOperator { get; set; } = ">";
    public double? ThresholdValue { get; set; }
    [MaxLength(64)] public string ReflexTestCode { get; set; } = "";
    public bool AutoApprove { get; set; }
    public bool RequiresSameSample { get; set; } = true;
    [MaxLength(512)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
