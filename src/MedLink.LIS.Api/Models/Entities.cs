using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Models
{
    // =========================================================================
    // 1. ORDERS, SAMPLES & TESTING WORKFLOW (WITH MEDLINK RELATIONS)
    // =========================================================================

    [Table("lab_orders")]
    public class LabOrder
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Column("order_number")]
        [Required]
        [MaxLength(64)]
        public string OrderNumber { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to MedLink MIS Patient Card (mis_patient_card / ehp_patients)
        /// </summary>
        [Column("patient_id")]
        [Required]
        public string PatientId { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to eHealth Referral (ehe_incoming_medical_referral)
        /// </summary>
        [Column("referral_id")]
        public string? ReferralId { get; set; }

        /// <summary>
        /// Ordering physician foreign key (org_employee / ehe_employees)
        /// </summary>
        [Column("doctor_id")]
        public string? DoctorId { get; set; }

        /// <summary>
        /// Clinical department / collection site (org_department)
        /// </summary>
        [Column("department_id")]
        public string? DepartmentId { get; set; }

        [Column("order_datetime")]
        public string OrderDatetime { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [Column("status")]
        public string Status { get; set; } = "NEW"; // NEW, COLLECTED, IN_TRANSIT, ANALYZING, COMPLETED

        [Column("is_urgent_cito")]
        public int IsUrgentCito { get; set; } = 0;

        [Column("clinical_notes")]
        public string? ClinicalNotes { get; set; }

        public List<LabOrderSample> Samples { get; set; } = new();
    }

    [Table("lab_order_samples")]
    public class LabOrderSample
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Column("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [ForeignKey("OrderId")]
        public LabOrder? Order { get; set; }

        [Column("barcode")]
        [Required]
        [MaxLength(64)]
        public string Barcode { get; set; } = string.Empty;

        [Column("tube_type_id")]
        public int TubeTypeId { get; set; }

        [Column("biomaterial_type_id")]
        public int BiomaterialTypeId { get; set; }

        [Column("collected_at")]
        public string? CollectedAt { get; set; }

        [Column("collected_by_id")]
        public string? CollectedById { get; set; }

        [Column("status")]
        public string Status { get; set; } = "PENDING"; // PENDING, COLLECTED, IN_TRANSIT, RECEIVED, COMPLETED

        [Column("is_hemolyzed")]
        public int IsHemolyzed { get; set; } = 0;

        [Column("is_lipemic")]
        public int IsLipemic { get; set; } = 0;

        [Column("is_clotted")]
        public int IsClotted { get; set; } = 0;
    }

    [Table("lab_test_results")]
    public class LabTestResult
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Column("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [Column("sample_id")]
        public string SampleId { get; set; } = string.Empty;

        [Column("test_code")]
        public string TestCode { get; set; } = string.Empty;

        [Column("test_name")]
        public string TestName { get; set; } = string.Empty;

        [Column("numeric_value")]
        public double? NumericValue { get; set; }

        [Column("string_value")]
        public string? StringValue { get; set; }

        [Column("unit")]
        public string? Unit { get; set; }

        [Column("norm_min")]
        public double? NormMin { get; set; }

        [Column("norm_max")]
        public double? NormMax { get; set; }

        [Column("flag")]
        public string Flag { get; set; } = "NORMAL"; // NORMAL, LOW, HIGH, CRIT_LOW, CRIT_HIGH, DELTA_ALERT

        [Column("delta_percent")]
        public double? DeltaPercent { get; set; }

        [Column("analyzer_id")]
        public string? AnalyzerId { get; set; }

        [Column("is_auto_verified")]
        public int IsAutoVerified { get; set; } = 0;

        /// <summary>
        /// Validating Doctor Employee ID (org_employee)
        /// </summary>
        [Column("verified_by_id")]
        public string? VerifiedById { get; set; }

        [Column("verified_at")]
        public string? VerifiedAt { get; set; }

        [Column("status")]
        public string Status { get; set; } = "PENDING"; // PENDING, NEEDS_DOCTOR, AUTO_VERIFIED, MANUAL_VERIFIED
    }

    // =========================================================================
    // 2. DELPHI MULTI-LAYER REFERENCE NORMS ARCHITECTURE
    // =========================================================================

    [Table("lab_reference_ranges")]
    public class LabReferenceLayer
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Column("test_code")]
        [Required]
        public string TestCode { get; set; } = string.Empty;

        [Column("method_code")]
        public string MethodCode { get; set; } = "HEX_IFCC";

        [Column("method_name")]
        public string MethodName { get; set; } = "Стандартна";

        /// <summary>
        /// Delphi Layer Type: BASELINE (10), DEMOGRAPHIC (40), CLINICAL_ICD10 (60), MENSTRUAL_PHASE (80), PREGNANCY (100)
        /// </summary>
        [Column("layer_type")]
        public string LayerType { get; set; } = "DEMOGRAPHIC";

        /// <summary>
        /// Priority weight in the cascade: Higher numbers override lower numbers.
        /// </summary>
        [Column("priority_order")]
        public int PriorityOrder { get; set; } = 40;

        [Column("norm_name")]
        public string NormName { get; set; } = string.Empty;

        [Column("gender")]
        public string Gender { get; set; } = "ANY"; // M, F, ANY

        [Column("is_gender")]
        public int IsGender { get; set; } = 0;

        [Column("age_unit")]
        public string AgeUnit { get; set; } = "YEARS"; // DAYS, MONTHS, YEARS

        [Column("age_from")]
        public int AgeFrom { get; set; } = 0;

        [Column("age_to")]
        public int AgeTo { get; set; } = 120;

        [Column("is_age")]
        public int IsAge { get; set; } = 1;

        [Column("is_menstrual_phase")]
        public int IsMenstrualPhase { get; set; } = 0;

        [Column("menstrual_phase")]
        public string? MenstrualPhase { get; set; } // FOLLICULAR, OVULATORY, LUTEAL, POSTMENOPAUSE

        [Column("is_pregnancy")]
        public int IsPregnancy { get; set; } = 0;

        [Column("pregnancy_week_from")]
        public int? PregnancyWeekFrom { get; set; }

        [Column("pregnancy_week_to")]
        public int? PregnancyWeekTo { get; set; }

        [Column("icd10_code")]
        public string? Icd10Code { get; set; }

        [Column("norm_low")]
        public double NormLow { get; set; }

        [Column("norm_high")]
        public double NormHigh { get; set; }

        [Column("crit_low")]
        public double? CritLow { get; set; }

        [Column("crit_high")]
        public double? CritHigh { get; set; }

        [Column("norm_text")]
        public string? NormText { get; set; }

        [Column("unit")]
        public string Unit { get; set; } = string.Empty;

        [Column("delta_check_max_pct")]
        public double DeltaCheckMaxPct { get; set; } = 25.0;

        [Column("is_active")]
        public int IsActive { get; set; } = 1;

        [Column("created_at")]
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // =========================================================================
    // 3. QUALITY CONTROL (LEVEY-JENNINGS & WESTGARD RULES)
    // =========================================================================

    [Table("lab_qc_results")]
    public class LabQcResult
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Column("analyzer_id")]
        public string AnalyzerId { get; set; } = string.Empty;

        [Column("control_material")]
        public string ControlMaterial { get; set; } = string.Empty;

        [Column("lot_number")]
        public string LotNumber { get; set; } = string.Empty;

        [Column("test_code")]
        public string TestCode { get; set; } = string.Empty;

        [Column("measured_value")]
        public double MeasuredValue { get; set; }

        [Column("target_mean")]
        public double TargetMean { get; set; }

        [Column("target_sd")]
        public double TargetSd { get; set; }

        [Column("z_score")]
        public double ZScore { get; set; }

        [Column("is_violation")]
        public int IsViolation { get; set; } = 0;

        [Column("violated_rule")]
        public string? ViolatedRule { get; set; }

        [Column("is_lockout")]
        public int IsLockout { get; set; } = 0;

        [Column("lockout_resolved_at")]
        public string? LockoutResolvedAt { get; set; }

        [Column("resolution_action")]
        public string? ResolutionAction { get; set; }

        [Column("created_at")]
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // =========================================================================
    // 4. BIOBANK & ARCHIVE CELLS (8x12 GRID)
    // =========================================================================

    [Table("lab_sample_archive_cells")]
    public class LabSampleArchiveCell
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Column("rack_code")]
        public string RackCode { get; set; } = "RACK-A1";

        [Column("box_number")]
        public string BoxNumber { get; set; } = "BOX-01";

        [Column("cell_coordinate")]
        public string CellCoordinate { get; set; } = string.Empty; // e.g. A-01..H-12

        [Column("sample_id")]
        public string SampleId { get; set; } = string.Empty;

        [Column("stored_at")]
        public string StoredAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [Column("expiry_at")]
        public string ExpiryAt { get; set; } = DateTime.Now.AddDays(180).ToString("yyyy-MM-dd HH:mm:ss");
    }

    // =========================================================================
    // 5. PANIC CALL AUDIT LOG
    // =========================================================================

    [Table("lab_panic_call_log")]
    public class LabPanicCallLog
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Column("result_id")]
        public string ResultId { get; set; } = string.Empty;

        [Column("doctor_notified_name")]
        public string DoctorNotifiedName { get; set; } = string.Empty;

        [Column("phone_called")]
        public string PhoneCalled { get; set; } = string.Empty;

        [Column("notified_at")]
        public string NotifiedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [Column("notified_by_id")]
        public string NotifiedById { get; set; } = string.Empty;

        [Column("comments")]
        public string? Comments { get; set; }
    }

    // =========================================================================
    // 6. DICTIONARIES
    // =========================================================================

    [Table("lab_biomaterial_types")]
    public class LabBiomaterialType
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("default_container")]
        public string? DefaultContainer { get; set; }

        [Column("stability_hours")]
        public int StabilityHours { get; set; } = 24;

        [Column("temperature_regime")]
        public string TemperatureRegime { get; set; } = "+2..+8°C";
    }

    [Table("lab_tube_types")]
    public class LabTubeType
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("color_code")]
        public string ColorCode { get; set; } = "#4274A7";

        [Column("volume_ml")]
        public double VolumeMl { get; set; } = 4.0;

        [Column("anticoagulant")]
        public string? Anticoagulant { get; set; }

        [Column("inversions_count")]
        public int InversionsCount { get; set; } = 8;

        [Column("centrifuge_rpm")]
        public int CentrifugeRpm { get; set; } = 3000;

        [Column("centrifuge_minutes")]
        public int CentrifugeMinutes { get; set; } = 10;
    }

    [Table("lab_analyzers")]
    public class LabAnalyzer
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = string.Empty;

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("analyzer_type_id")]
        public int AnalyzerTypeId { get; set; } = 1;

        [Column("connection_mode")]
        public string ConnectionMode { get; set; } = "TCP";

        [Column("ip_host")]
        public string? IpHost { get; set; }

        [Column("ip_port")]
        public int? IpPort { get; set; }

        [Column("is_online")]
        public int IsOnline { get; set; } = 1;
    }

    [Table("lab_test_definitions")]
    public class LabTestDefinition
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("loinc_code")]
        public string? LoincCode { get; set; }

        [Column("biomaterial_type_id")]
        public int BiomaterialTypeId { get; set; } = 1;

        [Column("unit")]
        public string Unit { get; set; } = string.Empty;

        [Column("decimal_places")]
        public int DecimalPlaces { get; set; } = 2;

        [Column("category")]
        public string Category { get; set; } = "BIOCHEM";

        [Column("is_active")]
        public int IsActive { get; set; } = 1;
    }
}
