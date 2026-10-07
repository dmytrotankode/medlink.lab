// Мікробіологія: збудники, антибіотики, EUCAST breakpoints, посіви, ізоляти, антибіотикограма
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

[Table("lab_micro_organism")]
public class LabMicroOrganism : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string LatinName { get; set; } = "";
    [MaxLength(256)] public string? CommonName { get; set; }
    /// <summary>BACTERIA | FUNGI | VIRUS | PARASITE</summary>
    [MaxLength(16)] public string Kingdom { get; set; } = "BACTERIA";
    [MaxLength(16)] public string? GramStain { get; set; }
    [MaxLength(32)] public string? Morphology { get; set; }
    public bool IsPathogen { get; set; } = true;
    public bool IsOpportunistic { get; set; }
    public bool AlertCriticalOrganism { get; set; }
    public bool IsActive { get; set; } = true;
}

[Table("lab_antibiotic")]
public class LabAntibiotic : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    [MaxLength(128)] public string GroupName { get; set; } = "";
    [MaxLength(16)] public string? AtcCode { get; set; }
    [MaxLength(32)] public string? EucastCode { get; set; }
    public double? DefaultDiskContentMcg { get; set; }
    public bool IsActive { get; set; } = true;
}

[Table("lab_eucast_breakpoint")]
public class LabEucastBreakpoint : GuidEntity
{
    public int OrganismId { get; set; }
    public int AntibioticId { get; set; }
    [MaxLength(32)] public string EucastVersion { get; set; } = "v14.0_2024";
    public double? MicSusceptibleLe { get; set; }
    public double? MicResistantGt { get; set; }
    public double? ZoneSusceptibleGe { get; set; }
    public double? ZoneResistantLt { get; set; }
    public bool IntrinsicResistance { get; set; }
    public string? GuidanceNotes { get; set; }

    [ForeignKey(nameof(OrganismId))] public LabMicroOrganism? Organism { get; set; }
    [ForeignKey(nameof(AntibioticId))] public LabAntibiotic? Antibiotic { get; set; }
}

[Table("lab_culture_order")]
public class LabCultureOrder : GuidEntity
{
    [MaxLength(64)] public string OrderTestId { get; set; } = "";
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(128)] public string? SpecimenLocus { get; set; }
    public DateTime IncubationStart { get; set; } = DateTime.UtcNow;
    public int IncubationHoursRecommended { get; set; } = 48;
    [MaxLength(128)] public string CultureMedium { get; set; } = "Blood Agar";
    public bool? GrowthDetected { get; set; }
    /// <summary>NO_GROWTH | SCANTY_1+ | MODERATE_2+ | HEAVY_3+ | PROFUSE_4+</summary>
    [MaxLength(16)] public string? GrowthIntensity { get; set; }
    [MaxLength(64)] public string? CfuPerMl { get; set; }
    public string? PreliminaryReport { get; set; }
    public string? FinalMicroscopyDescription { get; set; }
    /// <summary>INCUBATING | PRELIMINARY | ISOLATED | COMPLETED</summary>
    [MaxLength(16)] public string Status { get; set; } = "INCUBATING";

    [ForeignKey(nameof(OrderTestId))] public LabOrderTest? OrderTest { get; set; }
    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
    public List<LabIsolate> Isolates { get; set; } = new();
}

[Table("lab_isolate")]
public class LabIsolate : GuidEntity
{
    [MaxLength(64)] public string CultureOrderId { get; set; } = "";
    public int IsolateNumber { get; set; } = 1;
    public int OrganismId { get; set; }
    [MaxLength(64)] public string? QuantitativeCount { get; set; }
    public bool IsClinicallySignificant { get; set; } = true;
    public string? ColonyMorphology { get; set; }
    /// <summary>Виявлені фенотипи (MRSA, ESBL, CRE, VRE) через кому.</summary>
    [MaxLength(128)] public string? ResistancePhenotypes { get; set; }

    [ForeignKey(nameof(CultureOrderId))] public LabCultureOrder? Culture { get; set; }
    [ForeignKey(nameof(OrganismId))] public LabMicroOrganism? Organism { get; set; }
    public List<LabSusceptibilityResult> Susceptibilities { get; set; } = new();
}

[Table("lab_susceptibility_result")]
public class LabSusceptibilityResult : GuidEntity
{
    [MaxLength(64)] public string IsolateId { get; set; } = "";
    public int AntibioticId { get; set; }
    /// <summary>DISK_DIFFUSION | MIC_E_TEST | VITEK_AUTOMATED</summary>
    [MaxLength(32)] public string Method { get; set; } = "DISK_DIFFUSION";
    public double? ZoneDiameterMm { get; set; }
    public double? MicValueMgL { get; set; }
    /// <summary>S | I | R</summary>
    [MaxLength(4)] public string Interpretation { get; set; } = "";
    public bool IsIntrinsicResistance { get; set; }
    [MaxLength(64)] public string? BreakpointId { get; set; }
    [MaxLength(512)] public string? OverrideReason { get; set; }
    [MaxLength(64)] public string? OverriddenById { get; set; }

    [ForeignKey(nameof(IsolateId))] public LabIsolate? Isolate { get; set; }
    [ForeignKey(nameof(AntibioticId))] public LabAntibiotic? Antibiotic { get; set; }
}
