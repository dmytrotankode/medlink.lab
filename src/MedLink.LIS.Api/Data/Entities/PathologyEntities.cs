// =============================================================================
// ТЗ розд. 22 (ред. 4.2): модуль «Патоморфологія» — справа ПГД (lab_orders з lab_department=PATHOLOGY) → об'єкт (контейнер)
// → блок (касета) → скельце; журнал етапів, висновок з версіями й доповненнями, ІГХ, повідомлення CITO, видача з архіву.
// Довідники: шаблони описів, МКХ-О, методики забарвлення/ІГХ, reflex-правила ІГХ, типи матеріалу (SLA), фіксатори, причини відмови.
// Усі таблиці — [ЛІС]; таблиці MedLink не змінюються.
// =============================================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>Підрозділ лабораторії для замовлень окремих модулів (lab_orders.lab_department).</summary>
public static class LabDepartments
{
    public const string Pathology = "PATHOLOGY";
}

/// <summary>Тип матеріалу (п. 12–14 форми 014/о) і SLA в робочих днях (FR-PAT-090).</summary>
public static class PathKinds
{
    public const string Operative = "OPERATIVE", Biopsy = "BIOPSY", Cito = "CITO", Consult = "CONSULT", Cyto = "CYTO";
    public static readonly string[] All = { Operative, Biopsy, Cito, Consult, Cyto };
    public static string Label(string? k) => k switch
    {
        Operative => "Операційний матеріал", Biopsy => "Біопсія діагностична", Cito => "Термінова інтраопераційна (CITO)", Consult => "Консультація готових препаратів", Cyto => "Цитологічне дослідження", _ => k ?? "—"
    };
}

/// <summary>Тарифи за замовчуванням (прайс розд. 20.7 може перевизначити): вирізка — перший блок + кожен наступний, ІГХ, консультація за скельце.</summary>
public static class PathPrices
{
    public const decimal FirstBlock = 420m, NextBlock = 140m, Ihc = 650m, ConsultSlide = 210m, Cito = 900m, CytoSlide = 160m;
    public const int IhcExtraWorkdays = 3;
}

/// <summary>Етапи справи (FR-PAT-023): прийнято → вирізка → проводка → заливка → мікротомія → фарбування → мікроскопія й опис → підписано.</summary>
public static class PathStages
{
    public const string Received = "RCV", Gross = "GROSS", Decalcification = "DECALC", Processing = "PROC", Embedding = "EMB", Sectioning = "SECT", Staining = "STAIN", Microscopy = "MICRO", Signed = "SIGNED";
    /// <summary>Допоміжна відмітка «покрито покривним склом» (FR-PAT-114): фіксується скануванням скельця, не входить до лінійного потоку етапів.</summary>
    public const string Coverslip = "COVER";
    public static readonly string[] All = { Received, Gross, Decalcification, Processing, Embedding, Sectioning, Staining, Microscopy, Signed };
    /// <summary>Етапи лабораторної обробки, що фіксуються скануванням касет/скелець. Декальцинація — лише для блоків із маршрутом DECALC.</summary>
    public static readonly string[] Lab = { Decalcification, Processing, Embedding, Sectioning, Staining };
    public static string Label(string? s) => s switch
    {
        Received => "Прийнято", Gross => "Вирізка", Decalcification => "Декальцинація", Coverslip => "Покриття скла", Processing => "Проводка", Embedding => "Заливка", Sectioning => "Мікротомія", Staining => "Фарбування",
        Microscopy => "Мікроскопія й опис", Signed => "Підписано", _ => s ?? "—"
    };
    /// <summary>Назва етапу для виду справи: у цитології немає «вирізки» й «мікроскопії й опису» — приготування препаратів і скринінг із висновком.</summary>
    public static string LabelFor(string? kind, string? s) => kind == "CYTO"
        ? s switch { Received => "Прийом і приготування", Gross => "Приготування препаратів", Microscopy => "Скринінг і висновок", _ => Label(s) }
        : Label(s);
    public static string? Next(string stage) { var i = Array.IndexOf(All, stage); return i >= 0 && i < All.Length - 1 ? All[i + 1] : null; }
}

/// <summary>Довідники цитології (FR-CYT-001, 006): види матеріалу, адекватність, категорії висновку, оцінка скринера.</summary>
public static class PathCytology
{
    public static readonly (string Code, string Name)[] Materials =
    {
        ("GYN", "Гінекологічний мазок"), ("LIQUID", "Рідинна цитологія"), ("ASPIRATE", "Пунктат / аспірат"),
        ("DISCHARGE", "Виділення і змиви"), ("SEROUS", "Серозна рідина")
    };
    public static readonly (string Code, string Name)[] Adequacy =
    {
        ("SATISFACTORY", "Задовільний для оцінки"), ("LIMITED", "Обмежено придатний"), ("UNSATISFACTORY", "Непридатний для оцінки")
    };
    /// <summary>Категорії висновку; High — випадки, що потребують повідомлення направнику (FR-CYT-008).</summary>
    public static readonly (string Code, string Name, bool High)[] Categories =
    {
        ("NILM", "Без інтраепітеліальних уражень і злоякісності", false), ("ASC-US", "Атипові клітини плоского епітелію невизначеного значення", false),
        ("LSIL", "Плоскоклітинне інтраепітеліальне ураження низького ступеня", false), ("ASC-H", "Атипові клітини, не виключено ураження високого ступеня", true),
        ("HSIL", "Плоскоклітинне інтраепітеліальне ураження високого ступеня", true), ("AGC", "Атипові залозисті клітини", true),
        ("BENIGN", "Доброякісні зміни", false), ("ATYPICAL", "Атипові клітини", false), ("SUSPICIOUS", "Підозра на злоякісність", true), ("MALIGNANT", "Злоякісні клітини", true)
    };
    /// <summary>Категорії за видом матеріалу: гінекологічний мазок — шкала шийки матки + карцинома; пунктат, виділення, серозна рідина — загальні; рідинна цитологія — усі.</summary>
    private static readonly string[] GynCategories = { "NILM", "ASC-US", "LSIL", "ASC-H", "HSIL", "AGC", "MALIGNANT" };
    private static readonly string[] GeneralCategories = { "BENIGN", "ATYPICAL", "SUSPICIOUS", "MALIGNANT" };
    public static bool CategoryAllowed(string? material, string? category) =>
        category == null || material switch { null or "" or "LIQUID" => true, "GYN" => GynCategories.Contains(category), _ => GeneralCategories.Contains(category) };
    public static readonly string[] Assessments = { "NORMAL", "SUSPECT" };
    public static bool IsHigh(string? c) => Categories.Any(x => x.High && x.Code == c);
    public static bool IsNormal(string? c) => c is "NILM" or "BENIGN";
}

/// <summary>Маршрут обробки блока (FR-PAT-110): стандартна проводка або з попередньою декальцинацією (кісткова та кальцинована тканина).</summary>
public static class PathTracks
{
    public const string Standard = "STANDARD", Decalc = "DECALC";
    public static readonly string[] All = { Standard, Decalc };
    public static string Label(string? t) => t == Decalc ? "Декальцинація → проводка" : "Проводка";
}

/// <summary>Залишок матеріалу в контейнері після вирізки (FR-PAT-116).</summary>
public static class PathRemainder
{
    public const string None = "NONE", Kept = "KEPT", Discarded = "DISCARDED";
    public static readonly string[] All = { None, Kept, Discarded };
}

/// <summary>Стан прийому направлення ПГД (FR-PAT-001…005).</summary>
public static class PathIntake
{
    public const string Incoming = "INCOMING", Accepted = "ACCEPTED", Rejected = "REJECTED";
}

/// <summary>Вид версії висновку (FR-PAT-043…045).</summary>
public static class PathReportKinds
{
    public const string Main = "MAIN", Preliminary = "PRELIMINARY", Addendum = "ADDENDUM", Cito = "CITO";
}

/// <summary>[ЛІС] Направлення ПГД і шапка справи: знімок лицьової сторони 014/о (п. 1–9), стан прийому, етап, вартість.</summary>
[Table("lab_path_referral")]
public class LabPathReferral : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    public LabOrder? Order { get; set; }
    /// <summary>Знімок п. 1–9 форми 014/о з МІС (JSON), лише для перегляду (FR-PAT-002).</summary>
    public string? Form014Json { get; set; }
    /// <summary>PRIMARY | SECONDARY (п. 4–5 форми)</summary>
    [MaxLength(16)] public string BiopsyKind { get; set; } = "PRIMARY";
    [MaxLength(64)] public string? PrimaryCaseNo { get; set; }
    public string? ClinicalData { get; set; }
    [MaxLength(256)] public string? ClinicalDxIcd10 { get; set; }
    [MaxLength(256)] public string? ReferringDoctor { get; set; }
    [MaxLength(256)] public string? ReferringDepartment { get; set; }
    [MaxLength(64)] public string? MedicalCardNo { get; set; }
    [MaxLength(1024)] public string? OperationText { get; set; }
    [MaxLength(1024)] public string? MarkingText { get; set; }
    /// <summary>Консультація препаратів (FR-PAT-008): заклад-відправник, №, дата, кількість скелець і блоків.</summary>
    [MaxLength(256)] public string? ExternalInstitution { get; set; }
    [MaxLength(64)] public string? ExternalNumber { get; set; }
    public DateTime? ExternalDate { get; set; }
    public int ExternalSlides { get; set; }
    public int ExternalBlocks { get; set; }
    [MaxLength(128)] public string? TemplateName { get; set; }
    [MaxLength(16)] public string IntakeStatus { get; set; } = PathIntake.Incoming;
    [MaxLength(16)] public string Stage { get; set; } = PathStages.Received;
    public DateTime? AcceptedAt { get; set; }
    [MaxLength(64)] public string? AcceptedById { get; set; }
    [MaxLength(256)] public string? RejectReason { get; set; }
    [MaxLength(1024)] public string? RejectComment { get; set; }
    public string? Macro { get; set; }
    public DateTime? GrossFinishedAt { get; set; }
    [MaxLength(64)] public string? GrossById { get; set; }
    /// <summary>Лікар-патолог справи (обов'язковий до завершення вирізки, FR-PAT-111).</summary>
    [MaxLength(64)] public string? PathologistId { get; set; }
    [MaxLength(256)] public string? PathologistName { get; set; }
    /// <summary>Асистент, який провів вирізку (обов'язковий до завершення вирізки, FR-PAT-111).</summary>
    [MaxLength(64)] public string? AssistantId { get; set; }
    [MaxLength(256)] public string? AssistantName { get; set; }
    public decimal Price { get; set; }
    public DateTime? CitoNotifiedAt { get; set; }
    /// <summary>Гінекологічний анамнез (FR-PAT-120): день менструального циклу, дата останніх місячних, стан менопаузи.</summary>
    public int? CycleDay { get; set; }
    public DateTime? LastMenstrualDate { get; set; }
    /// <summary>NONE | PERI | POST | AMENORRHEA</summary>
    [MaxLength(16)] public string? MenopauseState { get; set; }
    /// <summary>Адреса для розсилки підписаного висновку (FR-PAT-119).</summary>
    [MaxLength(256)] public string? ResultEmail { get; set; }
    /// <summary>Цитологія: вид матеріалу (GYN | LIQUID | ASPIRATE | DISCHARGE | SEROUS), FR-CYT-001.</summary>
    [MaxLength(16)] public string? CytoMaterial { get; set; }
    /// <summary>Цитологія: приготування препаратів завершено (FR-CYT-003).</summary>
    public DateTime? PreparedAt { get; set; }
    /// <summary>Цитологія: попередня оцінка скринера NORMAL | SUSPECT (FR-CYT-005).</summary>
    [MaxLength(16)] public string? ScreenAssessment { get; set; }
    [MaxLength(512)] public string? ScreenNote { get; set; }
    [MaxLength(64)] public string? ScreenerId { get; set; }
    [MaxLength(256)] public string? ScreenerName { get; set; }
    public DateTime? ScreenedAt { get; set; }
}

/// <summary>[ЛІС] Об'єкт (контейнер) з маркуванням МІС (п. 7 форми); аналог проби.</summary>
[Table("lab_path_object")]
public class LabPathObject : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    /// <summary>Літера об'єкта для блоків (A, B, …).</summary>
    [MaxLength(4)] public string Letter { get; set; } = "A";
    [MaxLength(32)] public string Mark { get; set; } = "";
    [MaxLength(512)] public string? Description { get; set; }
    [MaxLength(256)] public string? Localization { get; set; }
    public int Fragments { get; set; }
    [MaxLength(64)] public string? Fixative { get; set; }
    public DateTime? TakenAt { get; set; }
    public DateTime? FixationStartAt { get; set; }
    [MaxLength(32)] public string Barcode { get; set; } = "";
    /// <summary>Маркування присутнє на контейнері (FR-PAT-003).</summary>
    public bool IsLabeled { get; set; } = true;
    public bool IsExternal { get; set; }
    public bool Accepted { get; set; }
    [MaxLength(256)] public string? RejectReason { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Залишок матеріалу після вирізки: NONE | KEPT | DISCARDED (FR-PAT-116).</summary>
    [MaxLength(16)] public string RemainderState { get; set; } = PathRemainder.None;
    [MaxLength(256)] public string? RemainderNote { get; set; }
    public List<LabPathBlock> Blocks { get; set; } = new();
}

/// <summary>[ЛІС] Блок (касета): A1, A2, B1…; для консультацій — зовнішній блок закладу-відправника.</summary>
[Table("lab_path_block")]
public class LabPathBlock : GuidEntity
{
    [MaxLength(64)] public string ObjectId { get; set; } = "";
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(16)] public string Code { get; set; } = "";
    public int Pieces { get; set; } = 1;
    public bool IsExternal { get; set; }
    [MaxLength(32)] public string Barcode { get; set; } = "";
    [MaxLength(128)] public string? StorageLocation { get; set; }
    /// <summary>Приміщення архіву (довідник lab_path_storage_room); полиця/комірка — у StorageLocation.</summary>
    [MaxLength(64)] public string? RoomId { get; set; }
    /// <summary>IN_WORK | ARCHIVED | ISSUED</summary>
    [MaxLength(16)] public string Status { get; set; } = "IN_WORK";
    [MaxLength(16)] public string? Stage { get; set; }
    /// <summary>STANDARD | DECALC (FR-PAT-110).</summary>
    [MaxLength(16)] public string Track { get; set; } = PathTracks.Standard;
    public List<LabPathSlide> Slides { get; set; } = new();
}

/// <summary>[ЛІС] Скельце: зріз блока з одним методом забарвлення (Г-Е, гістохімія, ІГХ).</summary>
[Table("lab_path_slide")]
public class LabPathSlide : GuidEntity
{
    [MaxLength(64)] public string BlockId { get; set; } = "";
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(24)] public string Code { get; set; } = "";
    [MaxLength(128)] public string StainMethod { get; set; } = "Гематоксилін-еозин (Г-Е)";
    public bool IsIhc { get; set; }
    [MaxLength(32)] public string? Marker { get; set; }
    [MaxLength(64)] public string? AntibodyLot { get; set; }
    public bool IsExternal { get; set; }
    /// <summary>IN_WORK | IHC_IN_WORK | READY</summary>
    [MaxLength(16)] public string Status { get; set; } = "IN_WORK";
    [MaxLength(32)] public string Barcode { get; set; } = "";
    [MaxLength(512)] public string? WsiUrl { get; set; }
    /// <summary>LAB | MIS | MDK — джерело дозамовлення (FR-PAT-026).</summary>
    [MaxLength(8)] public string? OrderedFrom { get; set; }
    /// <summary>Скельце покрито покривним склом (FR-PAT-114).</summary>
    public DateTime? CoveredAt { get; set; }
    /// <summary>Утилізація скельця (FR-PAT-118): статус DISPOSED, причина.</summary>
    public DateTime? DisposedAt { get; set; }
    [MaxLength(256)] public string? DisposeReason { get; set; }
    /// <summary>Скельце, замість якого створено це (після утилізації).</summary>
    [MaxLength(64)] public string? ReplacesSlideId { get; set; }
}

/// <summary>[ЛІС] Журнал етапів обробки (FR-PAT-023): хто, коли, на якому пристрої.</summary>
[Table("lab_path_stage_log")]
public class LabPathStageLog : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(64)] public string? BlockId { get; set; }
    [MaxLength(64)] public string? SlideId { get; set; }
    [MaxLength(16)] public string Stage { get; set; } = "";
    [MaxLength(64)] public string? PerformedBy { get; set; }
    [MaxLength(256)] public string? PerformedByName { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(128)] public string? Device { get; set; }
    [MaxLength(512)] public string? Comment { get; set; }
}

/// <summary>[ЛІС] Висновок (версія): основний, попередній, доповнення; після підпису незмінний (FR-PAT-045).</summary>
[Table("lab_path_report")]
public class LabPathReport : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    public int Version { get; set; } = 1;
    [MaxLength(16)] public string Kind { get; set; } = PathReportKinds.Main;
    /// <summary>Доповнення: IHC | REVIEW | CORRECTION</summary>
    [MaxLength(16)] public string? AddendumType { get; set; }
    public string? Macro { get; set; }
    public string? Micro { get; set; }
    public string? ConclusionText { get; set; }
    [MaxLength(16)] public string? Icd10 { get; set; }
    [MaxLength(16)] public string? IcdoTopo { get; set; }
    [MaxLength(16)] public string? IcdoMorph { get; set; }
    [MaxLength(8)] public string? Grade { get; set; }
    [MaxLength(16)] public string? Pt { get; set; }
    [MaxLength(16)] public string? Pn { get; set; }
    public int? LnTotal { get; set; }
    public int? LnPositive { get; set; }
    [MaxLength(64)] public string? Margin { get; set; }
    public bool? Lvi { get; set; }
    public bool? Pni { get; set; }
    public bool ChangesDiagnosis { get; set; }
    [MaxLength(1024)] public string? Reason { get; set; }
    /// <summary>Цитологія: адекватність матеріалу, категорія висновку, рекомендації (FR-CYT-006).</summary>
    [MaxLength(16)] public string? Adequacy { get; set; }
    [MaxLength(16)] public string? CytoCategory { get; set; }
    [MaxLength(1024)] public string? Recommendation { get; set; }
    /// <summary>Доповнення CONSULT: ПІБ консультанта зовнішньої консультації (FR-PAT-117).</summary>
    [MaxLength(256)] public string? ConsultantName { get; set; }
    public bool IsPreliminarySent { get; set; }
    [MaxLength(64)] public string? SignedBy { get; set; }
    [MaxLength(256)] public string? SignedByName { get; set; }
    public DateTime? SignedAt { get; set; }
    [MaxLength(128)] public string? SignatureRef { get; set; }
    public List<LabPathIhcResult> Ihc { get; set; } = new();
}

/// <summary>[ЛІС] Результат ІГХ-маркера (таблиця ІГХ висновку).</summary>
[Table("lab_path_ihc_result")]
public class LabPathIhcResult : GuidEntity
{
    [MaxLength(64)] public string ReportId { get; set; } = "";
    [MaxLength(64)] public string? SlideId { get; set; }
    [MaxLength(32)] public string Marker { get; set; } = "";
    [MaxLength(256)] public string? ResultText { get; set; }
    [MaxLength(32)] public string? Score { get; set; }
    [MaxLength(16)] public string? Unit { get; set; }
}

/// <summary>[ЛІС] Повідомлення результату термінової біопсії в операційну (FR-PAT-062): read-back обов'язковий.</summary>
[Table("lab_path_cito_notice")]
public class LabPathCitoNotice : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    public string Text { get; set; } = "";
    [MaxLength(256)] public string NotifiedPerson { get; set; } = "";
    [MaxLength(32)] public string Method { get; set; } = "PHONE";
    public bool ReadBack { get; set; }
    public int MinutesFromReceipt { get; set; }
    [MaxLength(64)] public string? NotifiedById { get; set; }
}

/// <summary>[ЛІС] Видача блока/скелець з архіву (FR-PAT-071).</summary>
[Table("lab_path_issue")]
public class LabPathIssue : GuidEntity
{
    [MaxLength(64)] public string BlockId { get; set; } = "";
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(256)] public string Recipient { get; set; } = "";
    [MaxLength(128)] public string? Document { get; set; }
    [MaxLength(128)] public string Purpose { get; set; } = "";
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueBack { get; set; }
    public DateTime? ReturnedAt { get; set; }
    [MaxLength(64)] public string? IssuedById { get; set; }
    /// <summary>EXTERNAL — зовнішній отримувач; INTERNAL — внутрішня видача в підрозділ закладу (FR-PAT-118).</summary>
    [MaxLength(16)] public string Scope { get; set; } = "EXTERNAL";
    [MaxLength(256)] public string? RecipientUnit { get; set; }
}

/// <summary>[ЛІС] Відправка підписаного висновку електронною поштою (FR-PAT-119): черга повідомлень, адреса, результат.</summary>
[Table("lab_path_dispatch")]
public class LabPathDispatch : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(64)] public string ReportId { get; set; } = "";
    [MaxLength(16)] public string Channel { get; set; } = "EMAIL";
    [MaxLength(256)] public string Address { get; set; } = "";
    /// <summary>QUEUED | SENT | FAILED</summary>
    [MaxLength(16)] public string Status { get; set; } = "QUEUED";
    [MaxLength(512)] public string? Error { get; set; }
    [MaxLength(64)] public string? RequestedById { get; set; }
}

/// <summary>[ЛІС] Приміщення архіву блоків і скелець (довідник; FR-PAT-118).</summary>
[Table("lab_path_storage_room")]
public class LabPathStorageRoom : GuidEntity
{
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(128)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

// ----------------------------------------------------------------- довідники (FR-PAT-100…106)

/// <summary>[ЛІС] Шаблон макро- і мікроопису: орган · вид матеріалу.</summary>
[Table("lab_path_template")]
public class LabPathTemplate : GuidEntity
{
    [MaxLength(128)] public string Name { get; set; } = "";
    public string? Macro { get; set; }
    public string? Micro { get; set; }
}

/// <summary>[ЛІС] Класифікатор МКХ-О: топографія (C00–C80) і морфологія (код/поведінка).</summary>
[Table("dct_icdo")]
public class LabIcdo : GuidEntity
{
    /// <summary>TOPO | MORPH</summary>
    [MaxLength(8)] public string Axis { get; set; } = "MORPH";
    [MaxLength(16)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    [MaxLength(16)] public string Version { get; set; } = "ICD-O-3.2";
}

/// <summary>[ЛІС] Методика забарвлення або ІГХ-антитіло з ціною.</summary>
[Table("lab_stain_method")]
public class LabStainMethod : GuidEntity
{
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(128)] public string Name { get; set; } = "";
    public bool IsIhc { get; set; }
    public decimal Price { get; set; }
}

/// <summary>[ЛІС] Reflex-правило ІГХ (FR-PAT-042): топографія (діапазон префіксів) + морфологія → панель маркерів.</summary>
[Table("lab_ihc_reflex_rule")]
public class LabIhcReflexRule : GuidEntity
{
    [MaxLength(8)] public string TopoFrom { get; set; } = "";
    [MaxLength(8)] public string TopoTo { get; set; } = "";
    [MaxLength(128)] public string MorphCodes { get; set; } = "";
    [MaxLength(256)] public string Panel { get; set; } = "";
    [MaxLength(512)] public string? Why { get; set; }
}

/// <summary>[ЛІС] Тип матеріалу: SLA (робочі дні), група послуг, ціни вирізки.</summary>
[Table("lab_path_material_type")]
public class LabPathMaterialType : GuidEntity
{
    [MaxLength(16)] public string Code { get; set; } = "";
    [MaxLength(128)] public string Name { get; set; } = "";
    public int SlaDays { get; set; }
    [MaxLength(32)] public string? ServiceGroup { get; set; }
    public decimal BasePrice { get; set; }
}

/// <summary>[ЛІС] Фіксатор з мінімальною й максимальною тривалістю фіксації.</summary>
[Table("lab_fixative")]
public class LabFixative : GuidEntity
{
    [MaxLength(64)] public string Name { get; set; } = "";
    public int MinHours { get; set; }
    public int MaxHours { get; set; }
}

/// <summary>[ЛІС] Причина відмови в прийомі матеріалу.</summary>
[Table("lab_path_reject_reason")]
public class LabPathRejectReason : GuidEntity
{
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
}
