// ТЗ розд. 22: модуль «Патоморфологія» — прийом направлень ПГД (форма 014/о) з МІС, справа ПГД, вирізка й блоки, етапи обробки
// скануванням, додаткові забарвлення/ІГХ (reflex), висновок МКХ-О з версіями й доповненнями, підпис КЕП, CITO, архів блоків, друк 014/о.
using System.Globalization;
using System.Text;
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services.Ehealth;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace MedLink.LIS.Api.Services;

public sealed class PathObjectRequest
{
    public string Mark { get; set; } = "";
    public string? Description { get; set; }
    public string? Localization { get; set; }
    public int Fragments { get; set; }
    public string? Fixative { get; set; }
    public DateTime? TakenAt { get; set; }
    public DateTime? FixationStartAt { get; set; }
    /// <summary>false — на контейнері немає маркування (FR-PAT-003).</summary>
    public bool Labeled { get; set; } = true;
}

public sealed class PathExternalRequest
{
    public string? Institution { get; set; }
    public string? Number { get; set; }
    public DateTime? Date { get; set; }
    public int Slides { get; set; }
    public int Blocks { get; set; }
}

/// <summary>Блок `pathology` у POST /mis/referrals (type=PATHOLOGY): лицьова сторона 014/о і контейнери.</summary>
public sealed class PathologyReferralRequest
{
    public string? MisReferralId { get; set; }
    public string? ReferralNumber { get; set; }
    /// <summary>INPATIENT | POLYCLINIC | EHEALTH | PARTNER</summary>
    public string Source { get; set; } = ReferralSources.Inpatient;
    public string PatientId { get; set; } = "";
    /// <summary>BIOPSY | CITO | OPERATIVE | CONSULT (п. 12–14 форми)</summary>
    public string Kind { get; set; } = PathKinds.Biopsy;
    public string? OperationId { get; set; }
    public string? HospitalizationId { get; set; }
    public string? Department { get; set; }
    public string? Ward { get; set; }
    public string? MedicalCardNo { get; set; }
    public string? DoctorId { get; set; }
    public string? DoctorName { get; set; }
    /// <summary>PRIMARY | SECONDARY (п. 4–5)</summary>
    public string BiopsyKind { get; set; } = "PRIMARY";
    public string? PrimaryCaseNo { get; set; }
    public string? ClinicalData { get; set; }
    public string? ClinicalDiagnosis { get; set; }
    public string? OperationText { get; set; }
    public string? MarkingText { get; set; }
    public string? EhealthReferralNumber { get; set; }
    public string? PartnerName { get; set; }
    public string? TemplateName { get; set; }
    /// <summary>Гінекологічний анамнез (FR-PAT-120).</summary>
    public int? CycleDay { get; set; }
    public DateTime? LastMenstrualDate { get; set; }
    /// <summary>NONE | PERI | POST | AMENORRHEA</summary>
    public string? MenopauseState { get; set; }
    /// <summary>Адреса розсилки підписаного висновку (FR-PAT-119).</summary>
    public string? ResultEmail { get; set; }
    /// <summary>Цитологія (Kind=CYTO): GYN | LIQUID | ASPIRATE | DISCHARGE | SEROUS.</summary>
    public string? CytoMaterial { get; set; }
    public PathExternalRequest? External { get; set; }
    /// <summary>Повний знімок п. 1–9 форми 014/о з МІС (довільний JSON).</summary>
    public JsonElement? Form014 { get; set; }
    public List<PathObjectRequest> Objects { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
}

public sealed class PathAcceptRequest
{
    /// <summary>Відскановані штрихкоди (або маркування) усіх контейнерів (FR-PAT-003).</summary>
    public List<string> ScannedBarcodes { get; set; } = new();
}

public sealed class PathRejectRequest
{
    public string Reason { get; set; } = "";
    public string? Comment { get; set; }
}

public sealed class PathQueryRequest { public string Text { get; set; } = ""; }

public sealed class PathGrossBlock
{
    public int Pieces { get; set; } = 1;
    /// <summary>STANDARD | DECALC (FR-PAT-110); порожньо — STANDARD.</summary>
    public string? Track { get; set; }
    /// <summary>Методики для скельця блока (коди з довідника lab_stain_method, до PathLimits.MaxMethodsPerBlock); порожньо — Г-Е (FR-PAT-113).</summary>
    public List<string>? Methods { get; set; }
}
public sealed class PathGrossObject
{
    public string ObjectId { get; set; } = "";
    public List<PathGrossBlock> Blocks { get; set; } = new();
    /// <summary>NONE | KEPT | DISCARDED — залишок матеріалу в контейнері (FR-PAT-116).</summary>
    public string? RemainderState { get; set; }
    public string? RemainderNote { get; set; }
}

public sealed class PathGrossRequest
{
    public string? Macro { get; set; }
    public string? TemplateName { get; set; }
    public List<PathGrossObject> Objects { get; set; } = new();
    /// <summary>true — «Завершити вирізку»: скельця за методиками блока, вартість, перший етап обробки (FR-PAT-022).</summary>
    public bool Finish { get; set; }
    /// <summary>Лікар-патолог і асистент, що провів вирізку: обов'язкові для завершення (FR-PAT-111).</summary>
    public string? PathologistId { get; set; }
    public string? AssistantId { get; set; }
}

public sealed class PathScanRequest
{
    public string Barcode { get; set; } = "";
    /// <summary>Етап, що фіксується: PROC | EMB | SECT | STAIN; порожньо — наступний для касети.</summary>
    public string? Stage { get; set; }
    public string? Device { get; set; }
    /// <summary>Лаборант-виконавець, якщо етап фіксує не поточний користувач (FR-PAT-111).</summary>
    public string? PerformerId { get; set; }
    /// <summary>Кількість зрізів / скелець для етапу «Мікротомія» (FR-PAT-112).</summary>
    public int? Count { get; set; }
}

public sealed class PathAdditionalRequest
{
    public string BlockId { get; set; } = "";
    /// <summary>Коди або назви методик (довідник lab_stain_method): VG, PAS, ER, HER2…</summary>
    public List<string> Methods { get; set; } = new();
    /// <summary>LAB | MIS | MDK (FR-PAT-026)</summary>
    public string Source { get; set; } = "LAB";
}

public sealed class PathIhcRow
{
    public string Marker { get; set; } = "";
    public string? SlideId { get; set; }
    public string? ResultText { get; set; }
    public string? Score { get; set; }
    public string? Unit { get; set; }
}

public sealed class PathReportRequest
{
    public string? Macro { get; set; }
    public string? Micro { get; set; }
    public string? ConclusionText { get; set; }
    public string? Icd10 { get; set; }
    public string? IcdoTopo { get; set; }
    public string? IcdoMorph { get; set; }
    public string? Grade { get; set; }
    public string? Pt { get; set; }
    public string? Pn { get; set; }
    public int? LnTotal { get; set; }
    public int? LnPositive { get; set; }
    public string? Margin { get; set; }
    public bool? Lvi { get; set; }
    public bool? Pni { get; set; }
    public List<PathIhcRow>? Ihc { get; set; }
    /// <summary>Цитологія (FR-CYT-006).</summary>
    public string? Adequacy { get; set; }
    public string? CytoCategory { get; set; }
    public string? Recommendation { get; set; }
}

public sealed class PathPrepSlide { public string? Method { get; set; } }
public sealed class PathPrepObject
{
    public string ObjectId { get; set; } = "";
    /// <summary>Скельця об'єкта (1…10); кожне з власною методикою (за замовчуванням Папаніколау).</summary>
    public List<PathPrepSlide> Slides { get; set; } = new();
}
/// <summary>Приготування цитологічних препаратів (FR-CYT-003).</summary>
public sealed class PathPrepareRequest
{
    public List<PathPrepObject> Objects { get; set; } = new();
    public string? Note { get; set; }
}
public sealed class PathScreeningRequest
{
    /// <summary>NORMAL | SUSPECT</summary>
    public string Assessment { get; set; } = "";
    public string? Note { get; set; }
}
public sealed class PathRepeatRequest { public string Reason { get; set; } = ""; public string? Method { get; set; } }

public sealed class PathPreliminaryRequest { public string Text { get; set; } = ""; }

public sealed class PathSignRequest
{
    public SignerInfo? Signer { get; set; }
    /// <summary>true — лікар підтвердив підпис без результатів частини замовлених ІГХ-маркерів (їх оформлять доповненням).</summary>
    public bool AcknowledgePendingIhc { get; set; }
}

public sealed class PathAddendumRequest
{
    /// <summary>IHC | REVIEW | CORRECTION | CONSULT (висновок зовнішнього консультанта, FR-PAT-117)</summary>
    public string Type { get; set; } = "IHC";
    public string? ConsultantName { get; set; }
    public string Text { get; set; } = "";
    public string? Reason { get; set; }
    public bool ChangesDiagnosis { get; set; }
    public string? Icd10 { get; set; }
    public string? IcdoTopo { get; set; }
    public string? IcdoMorph { get; set; }
    public List<PathIhcRow>? Ihc { get; set; }
    public SignerInfo? Signer { get; set; }
}

public sealed class PathCitoNoticeRequest
{
    public string Text { get; set; } = "";
    public string NotifiedPerson { get; set; } = "";
    /// <summary>PHONE | INTERCOM | IN_PERSON</summary>
    public string Method { get; set; } = "PHONE";
    public bool ReadBack { get; set; }
    /// <summary>true — свідоме повторне повідомлення після вже зафіксованого.</summary>
    public bool Repeat { get; set; }
}

public sealed class PathIssueRequest
{
    public string Recipient { get; set; } = "";
    public string? Document { get; set; }
    public string Purpose { get; set; } = "";
    public DateTime? DueBack { get; set; }
    /// <summary>EXTERNAL (типово) | INTERNAL — внутрішня видача в підрозділ закладу (FR-PAT-118).</summary>
    public string? Scope { get; set; }
    public string? RecipientUnit { get; set; }
}

public sealed class PathDisposeRequest
{
    public string Reason { get; set; } = "";
    /// <summary>true — створити нове скельце тієї ж методики замість утилізованого (FR-PAT-118).</summary>
    public bool Recreate { get; set; }
}

public sealed class PathStorageRequest
{
    public string? RoomId { get; set; }
    public string? Location { get; set; }
}

public sealed class PathRoomRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class PathMethodPriceRequest { public decimal Price { get; set; } }
public sealed class PathDispatchRequest { public string? Address { get; set; } }

/// <summary>Обмеження процесу обробки (FR-PAT-113).</summary>
public static class PathLimits
{
    public const int MaxMethodsPerBlock = 5;
}

public sealed class PathologyService
{
    public static readonly string[] IntakeRoles = { LabRoles.Admin, LabRoles.Registrar, LabRoles.Technician };
    public static readonly string[] PathologistRoles = { LabRoles.Admin, LabRoles.Doctor };
    public static readonly string[] HistotechRoles = { LabRoles.Admin, LabRoles.Technician };
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    private readonly LisDbContext _db;
    private readonly INumeratorService _numerators;
    private readonly IAuditService _audit;
    private readonly IRolePolicy _policy;
    private readonly ICurrentEmployee _current;
    private readonly EhealthSignService _sign;

    public PathologyService(LisDbContext db, INumeratorService numerators, IAuditService audit, IRolePolicy policy, ICurrentEmployee current, EhealthSignService sign)
    {
        _db = db; _numerators = numerators; _audit = audit; _policy = policy; _current = current; _sign = sign;
    }

    private static DomainProblemException Problem(string code, string detail, int status = 422) => new(status, code, code switch
    {
        "container-unlabeled" => "Контейнер без маркування",
        "containers-not-scanned" => "Не всі контейнери відскановано",
        "icdo-required" => "Потрібні коди МКХ-О",
        "read-back-required" => "Потрібне підтвердження отримувача",
        "micro-required" => "Не заповнено опис",
        "conclusion-required" => "Не заповнено висновок",
        "placeholders-left" => "У тексті лишились заготовки",
        "ihc-results-required" => "Немає результатів ІГХ",
        "ihc-pending" => "Не всі результати ІГХ внесено",
        "report-signed" => "Висновок підписано",
        "block-issued" => "Блок видано з архіву",
        "slide-not-stained" => "Скельце ще не забарвлено",
        "duplicate-referral" => "Направлення вже прийнято",
        _ => "Помилка справи ПГД"
    }, detail);

    // ------------------------------------------------------------------ допоміжне
    /// <summary>Додавання робочих днів (SLA, FR-PAT-090): субота й неділя не враховуються.</summary>
    public static DateTime AddWorkdays(DateTime from, int days)
    {
        var d = from;
        while (days > 0) { d = d.AddDays(1); if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days--; }
        return d;
    }

    /// <summary>Вартість вирізки (FR-PAT-022): перший блок + кожен наступний.</summary>
    public static decimal GrossPrice(int blocks) => blocks <= 0 ? 0 : PathPrices.FirstBlock + PathPrices.NextBlock * (blocks - 1);

    private static bool IsMalignant(string? morph) => morph != null && (morph.EndsWith("/3") || morph.EndsWith("/6"));

    /// <summary>Клінічний діагноз уже передбачав новоутворення (C00–C97, D00–D09, D37–D48): тоді злоякісний результат не є «неочікуваним».</summary>
    private static bool IsNeoplasmDiagnosis(string? icd10)
    {
        var c = (icd10 ?? "").Trim().ToUpperInvariant();
        if (c.Length < 3 || !int.TryParse(c.AsSpan(1, 2), out var n)) return false;
        return c[0] == 'C' || (c[0] == 'D' && (n <= 9 || (n >= 37 && n <= 48)));
    }

    /// <summary>Хвилини між двома моментами за годинником, який бачить користувач (до хвилини): 03:18 → 03:44 завжди 26.</summary>
    private static int WholeMinutes(DateTime from, DateTime to) =>
        (int)((new DateTime(to.Year, to.Month, to.Day, to.Hour, to.Minute, 0, to.Kind) - new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0, from.Kind)).TotalMinutes);

    private string Who() => _current.FullName ?? _current.EmployeeId;

    // ------------------------------------------------------------------ час лабораторії (бланки й тексти для користувача)
    private static readonly TimeZoneInfo LabTz = ResolveLabTz();

    private static TimeZoneInfo ResolveLabTz()
    {
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (Exception) { /* немає такого ідентифікатора в системі */ }
        }
        return TimeZoneInfo.Local;
    }

    /// <summary>Час у часовому поясі лабораторії (Київ), а не сервера: бланк 014/о має збігатися з інтерфейсом.</summary>
    private static DateTime InLab(DateTime d) =>
        TimeZoneInfo.ConvertTimeFromUtc(d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime(), LabTz);

    /// <summary>Незаповнені заготовки шаблонів — фрагменти у фігурних дужках, наприклад {g}, {inv}.</summary>
    private static List<string> OpenPlaceholders(string? text) =>
        string.IsNullOrWhiteSpace(text) ? new() : System.Text.RegularExpressions.Regex.Matches(text, @"\{[^{}
]{1,30}\}").Select(m => m.Value).Distinct().ToList();

    private static string PlaceholdersMessage(string what, List<string> open) =>
        $"{what} містить незаповнені заготовки: {string.Join(", ", open)}. Заповніть їх або видаліть";

    /// <summary>Хвилини від надходження до повідомлення (зафіксовано) або до зараз (таймер іде).</summary>
    private static int CitoMinutes(LabPathReferral r, DateTime now) =>
        r.CitoNotifiedAt.HasValue ? WholeMinutes(r.AcceptedAt!.Value, r.CitoNotifiedAt.Value) : (int)(now - r.AcceptedAt!.Value).TotalMinutes;

    private async Task<(LabOrder Order, LabPathReferral Ref)> LoadAsync(string id)
    {
        var r = await _db.PathReferrals.Include(x => x.Order).ThenInclude(o => o!.Patient)
                    .FirstOrDefaultAsync(x => x.OrderId == id || x.Id == id || x.Order!.CaseNo == id || x.Order!.MisReferralId == id)
                ?? throw NotFoundException.For("Справа ПГД", id);
        return (r.Order!, r);
    }

    private void Log(LabOrder o, string stage, string? blockId = null, string? slideId = null, string? device = null, string? comment = null, OrgEmployee? performer = null) =>
        _db.PathStageLog.Add(new LabPathStageLog { OrderId = o.Id, BlockId = blockId, SlideId = slideId, Stage = stage, PerformedBy = performer?.Id ?? _current.EmployeeId, PerformedByName = performer?.Caption ?? Who(), Device = device, Comment = comment });

    /// <summary>Співробітник з потрібною лабораторною роллю (FR-PAT-111): патолог або асистент/лаборант.</summary>
    private async Task<OrgEmployee> StaffAsync(string id, string[] roles, string field, string what)
    {
        var e = await _db.Employees.AsNoTracking().Include(x => x.LabSettings).FirstOrDefaultAsync(x => x.Id == id) ?? throw ValidationException.Field(field, $"Співробітника не знайдено ({what})");
        if (!e.IsLabActive || e.LabRole == null || !roles.Contains(e.LabRole)) throw ValidationException.Field(field, $"{e.Caption}: немає ролі для вибору як {what}");
        return e;
    }

    private async Task<OrgEmployee?> PerformerAsync(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : await StaffAsync(id, HistotechRoles, "performerId", "виконавця етапу");

    /// <summary>Перелік для вибору виконавців (FR-PAT-111): патологи й лаборанти/асистенти з активною лабораторною роллю.</summary>
    public async Task<object> StaffAsync()
    {
        var list = await _db.Employees.AsNoTracking().Include(x => x.LabSettings).Where(x => x.LabSettings != null && x.LabSettings.IsActive).ToListAsync();
        object Pick(string[] roles) => list.Where(e => e.LabRole != null && roles.Contains(e.LabRole)).OrderBy(e => e.Caption).Select(e => new { e.Id, name = e.Caption, role = e.LabRole }).ToList();
        return new { pathologists = Pick(PathologistRoles), assistants = Pick(HistotechRoles), current = _current.EmployeeId };
    }

    private void SetStage(LabOrder o, LabPathReferral r, string stage)
    {
        if (r.Stage == stage) return;
        r.Stage = stage;
        MisReferralService.EmitFor(_db, o, MisEvents.StageChanged, new { o.CaseNo, stage, stageLabel = PathStages.LabelFor(o.PathologyKind, stage) });
    }

    // ------------------------------------------------------------------ прийом (22.3)
    /// <summary>Направлення ПГД з МІС (POST /mis/referrals з type=PATHOLOGY): справа в черзі прийому, контейнери з штрихкодами.</summary>
    public async Task<object> CreateReferralAsync(PathologyReferralRequest req)
    {
        var kind = (req.Kind ?? "").Trim().ToUpperInvariant();
        if (kind == "OPER") kind = PathKinds.Operative;
        if (!PathKinds.All.Contains(kind)) throw ValidationException.Field("kind", "Тип матеріалу: BIOPSY, CITO, OPERATIVE, CONSULT або CYTO");
        var cytoMaterial = string.IsNullOrWhiteSpace(req.CytoMaterial) ? null : req.CytoMaterial.Trim().ToUpperInvariant();
        if (kind == PathKinds.Cyto)
        {
            cytoMaterial ??= "GYN";
            if (!PathCytology.Materials.Any(m => m.Code == cytoMaterial)) throw ValidationException.Field("cytoMaterial", "Вид матеріалу: GYN, LIQUID, ASPIRATE, DISCHARGE або SEROUS");
            if (cytoMaterial == "GYN" && req.CycleDay == null && req.LastMenstrualDate == null)
                throw ValidationException.Field("cycleDay", "Для гінекологічного мазка вкажіть день циклу або дату останніх місячних");
        }
        else cytoMaterial = null;
        var source = (req.Source ?? ReferralSources.Inpatient).Trim().ToUpperInvariant();
        if (source == "AMBULATORY") source = ReferralSources.Polyclinic;
        if (!ReferralSources.All.Contains(source)) throw ValidationException.Field("source", "Джерело: INPATIENT, POLYCLINIC, EHEALTH або PARTNER");
        if (!await _db.Patients.AnyAsync(p => p.Id == req.PatientId)) throw NotFoundException.For("Пацієнт", req.PatientId);
        if (!string.IsNullOrWhiteSpace(req.MisReferralId) && !Guid.TryParse(req.MisReferralId, out _)) throw ValidationException.Field("misReferralId", "Ідентифікатор направлення МІС має бути GUID");
        if (!string.IsNullOrWhiteSpace(req.OperationId) && !Guid.TryParse(req.OperationId, out _)) throw ValidationException.Field("operationId", "Ідентифікатор протоколу операції МІС має бути GUID");
        if (kind == PathKinds.Consult ? (req.External?.Slides ?? 0) + (req.External?.Blocks ?? 0) == 0 : req.Objects.Count == 0)
            throw ValidationException.Field(kind == PathKinds.Consult ? "external" : "objects", kind == PathKinds.Consult ? "Вкажіть кількість зовнішніх скелець або блоків" : "Направлення має містити хоча б один контейнер (п. 7)");
        if (!string.IsNullOrWhiteSpace(req.MisReferralId) && await _db.Orders.AnyAsync(o => o.MisReferralId == req.MisReferralId && o.Status != OrderStatuses.Cancelled))
            throw Problem("duplicate-referral", $"Направлення МІС {req.MisReferralId} вже прийнято", 409);

        if (req.CycleDay is < 1 or > 60) throw ValidationException.Field("cycleDay", "День циклу — від 1 до 60");
        var menopause = string.IsNullOrWhiteSpace(req.MenopauseState) ? null : req.MenopauseState.Trim().ToUpperInvariant();
        if (menopause != null && menopause is not ("NONE" or "PERI" or "POST" or "AMENORRHEA")) throw ValidationException.Field("menopauseState", "Стан: NONE, PERI, POST або AMENORRHEA");
        if (!string.IsNullOrWhiteSpace(req.ResultEmail) && !System.Text.RegularExpressions.Regex.IsMatch(req.ResultEmail.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw ValidationException.Field("resultEmail", "Некоректна адреса електронної пошти");
        var at = req.CreatedAt ?? DateTime.UtcNow;
        var doctorId = !string.IsNullOrWhiteSpace(req.DoctorId) && await _db.Employees.AnyAsync(e => e.Id == req.DoctorId) ? req.DoctorId : null;
        var order = new LabOrder
        {
            OrderNumber = await _numerators.NextOrderNumberAsync(at), PatientId = req.PatientId, DoctorId = doctorId, OrderDatetime = at, Status = OrderStatuses.New,
            IsUrgentCito = kind == PathKinds.Cito, ClinicalNotes = req.ClinicalData, Icd10Code = req.ClinicalDiagnosis?.Split(' ', 2)[0],
            CreatedById = _current.EmployeeId, OrganizationId = MedLinkDefaults.OrganizationId,
            ReferralType = source == ReferralSources.Ehealth ? ReferralTypes.Ehealth : source == ReferralSources.Partner ? ReferralTypes.ExternalClinic : ReferralTypes.Internal,
            ReferrerDoctorName = req.DoctorName, ReferrerOrganizationName = source == ReferralSources.Partner ? req.PartnerName ?? req.Department : null,
            ReferrerNumber = req.EhealthReferralNumber ?? req.ReferralNumber,
            ReferralContext = source == ReferralSources.Inpatient ? ReferralContexts.Inpatient : ReferralContexts.Ambulatory,
            MisReferralId = string.IsNullOrWhiteSpace(req.MisReferralId) ? Guid.NewGuid().ToString() : req.MisReferralId, MisReferralNumber = req.ReferralNumber,
            MisHospitalizationId = req.HospitalizationId, RequestingDepartmentName = req.Department, Ward = req.Ward,
            LabDepartment = LabDepartments.Pathology, PathologyKind = kind, MisOperationId = req.OperationId
        };
        _db.Orders.Add(order);
        var r = new LabPathReferral
        {
            OrderId = order.Id, Form014Json = req.Form014?.GetRawText(), BiopsyKind = req.BiopsyKind?.ToUpperInvariant() == "SECONDARY" ? "SECONDARY" : "PRIMARY",
            PrimaryCaseNo = req.PrimaryCaseNo, ClinicalData = req.ClinicalData, ClinicalDxIcd10 = req.ClinicalDiagnosis, ReferringDoctor = req.DoctorName, ReferringDepartment = req.Department,
            MedicalCardNo = req.MedicalCardNo, OperationText = req.OperationText, MarkingText = req.MarkingText, TemplateName = req.TemplateName,
            ExternalInstitution = req.External?.Institution, ExternalNumber = req.External?.Number, ExternalDate = req.External?.Date,
            ExternalSlides = req.External?.Slides ?? 0, ExternalBlocks = req.External?.Blocks ?? 0,
            CycleDay = req.CycleDay, LastMenstrualDate = req.LastMenstrualDate, MenopauseState = menopause, ResultEmail = Clean(req.ResultEmail, null), CytoMaterial = cytoMaterial
        };
        _db.PathReferrals.Add(r);
        var objects = req.Objects.ToList();
        if (kind == PathKinds.Consult && objects.Count == 0)
            objects.Add(new PathObjectRequest { Mark = "К", Description = $"Зовнішні препарати №{req.External?.Number}".TrimEnd('№') });
        var no = 0;
        foreach (var ob in objects)
        {
            var letter = ((char)('A' + no)).ToString();
            _db.PathObjects.Add(new LabPathObject
            {
                OrderId = order.Id, Letter = letter, Mark = string.IsNullOrWhiteSpace(ob.Mark) ? (no + 1).ToString() : ob.Mark.Trim(), Description = ob.Description, Localization = ob.Localization,
                Fragments = ob.Fragments, Fixative = ob.Fixative, TakenAt = ob.TakenAt, FixationStartAt = ob.FixationStartAt, IsLabeled = ob.Labeled,
                IsExternal = kind == PathKinds.Consult, Barcode = $"PO{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}", DisplayOrder = ++no
            });
        }
        _audit.Log("PATH_REFERRAL", "lab_order", order.Id, null, new { req.MisReferralId, kind, source, containers = objects.Count });
        await _db.SaveChangesAsync();
        return await CaseAsync(order.Id);
    }

    /// <summary>Черга прийому (FR-PAT-001): направлення PATHOLOGY з усіх джерел, контроль фіксації (FR-PAT-004).</summary>
    public async Task<object> IncomingAsync()
    {
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var refs = await _db.PathReferrals.AsNoTracking().Include(r => r.Order).ThenInclude(o => o!.Patient)
            .Where(r => r.IntakeStatus == PathIntake.Incoming && r.Order!.Status != OrderStatuses.Cancelled).ToListAsync();
        var ids = refs.Select(r => r.OrderId).ToList();
        var objects = (await _db.PathObjects.AsNoTracking().Where(o => ids.Contains(o.OrderId)).ToListAsync()).ToLookup(o => o.OrderId);
        var now = DateTime.UtcNow;
        // попередні прийняті справи цього ж пацієнта: реєстратор бачить їх до прийому (повторна біопсія, цитологія після ПГД, дубль направлення)
        var patientIds = refs.Select(r => r.Order!.PatientId).Distinct().ToList();
        var prior = (await _db.PathReferrals.AsNoTracking().Include(r => r.Order)
            .Where(r => r.IntakeStatus == PathIntake.Accepted && patientIds.Contains(r.Order!.PatientId)).ToListAsync())
            .ToLookup(r => r.Order!.PatientId);
        return refs.OrderByDescending(r => r.Order!.IsUrgentCito).ThenBy(r => r.Order!.OrderDatetime).Select(r =>
        {
            var o = r.Order!;
            var objs = objects[o.Id].OrderBy(x => x.DisplayOrder).ToList();
            return new
            {
                id = o.Id, o.OrderNumber, createdAt = o.OrderDatetime, patientId = o.PatientId, patient = o.Patient?.Caption, birthday = o.Patient?.Birthday, gender = o.Patient?.Gender,
                source = ReferralSources.Of(o), sourceLabel = ReferralSources.Label(ReferralSources.Of(o)), kind = o.PathologyKind, kindLabel = PathKinds.Label(o.PathologyKind), cito = o.IsUrgentCito,
                department = o.RequestingDepartmentName, doctor = r.ReferringDoctor, ehealthReferral = o.ReferralType == ReferralTypes.Ehealth ? o.ReferrerNumber : null,
                serviceGroup = o.PathologyKind is PathKinds.Biopsy or PathKinds.Consult ? "21729" : "21730",
                material = r.CytoMaterial, materialLabel = r.CytoMaterial == null ? null : PathCytology.Materials.FirstOrDefault(m => m.Code == r.CytoMaterial).Name,
                containers = objs.Count, unlabeled = objs.Count(x => !x.IsLabeled),
                priorCases = prior[o.PatientId].Where(p => p.OrderId != o.Id).OrderByDescending(p => p.AcceptedAt).Take(5)
                    .Select(p => new { id = p.OrderId, caseNo = p.Order!.CaseNo, kindLabel = PathKinds.Label(p.Order.PathologyKind), stageLabel = PathStages.LabelFor(p.Order.PathologyKind, p.Stage), signed = p.Stage == PathStages.Signed, receivedAt = p.AcceptedAt }).ToList(),
                referral = ReferralView(r, o),
                objects = objs.Select(x => ObjectView(x, settings, now))
            };
        }).ToList();
    }

    private static object ReferralView(LabPathReferral r, LabOrder o) => new
    {
        form014 = string.IsNullOrWhiteSpace(r.Form014Json) ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(r.Form014Json),
        r.BiopsyKind, biopsyKindLabel = r.BiopsyKind == "SECONDARY" ? "вторинна" : "первинна", r.PrimaryCaseNo, r.ClinicalData, clinicalDiagnosis = r.ClinicalDxIcd10,
        r.ReferringDoctor, r.ReferringDepartment, r.MedicalCardNo, r.OperationText, r.MarkingText, r.TemplateName, o.Ward, operationId = o.MisOperationId,
        external = r.ExternalSlides + r.ExternalBlocks == 0 ? null : new { institution = r.ExternalInstitution, number = r.ExternalNumber, date = r.ExternalDate, slides = r.ExternalSlides, blocks = r.ExternalBlocks },
        gyn = r.CycleDay == null && r.LastMenstrualDate == null && r.MenopauseState == null ? null : new { r.CycleDay, r.LastMenstrualDate, r.MenopauseState },
        r.ResultEmail
    };

    private static object ObjectView(LabPathObject x, LabSettings s, DateTime now)
    {
        double? hours = x.FixationStartAt.HasValue ? Math.Round((now - x.FixationStartAt.Value).TotalHours, 1) : null;
        var fixWarn = hours == null || x.IsExternal || x.Fixative?.StartsWith("Без", StringComparison.OrdinalIgnoreCase) == true ? null
            : hours < s.PathologyFixationMinHours ? $"Фіксація {hours.Value.ToString("0.#", Uk)} год — менше за мінімум {s.PathologyFixationMinHours} год: ІГХ може бути ненадійним"
            : hours > s.PathologyFixationMaxHours ? $"Фіксація {hours.Value.ToString("0.#", Uk)} год — більше за максимум {s.PathologyFixationMaxHours} год: ІГХ може бути ненадійним" : null;
        return new
        {
            x.Id, x.Letter, x.Mark, x.Description, x.Localization, x.Fragments, x.Fixative, x.TakenAt, x.FixationStartAt, fixationHours = hours, fixationWarning = fixWarn,
            x.Barcode, labeled = x.IsLabeled, problem = x.IsLabeled ? null : "Маркування на контейнері відсутнє", x.IsExternal, x.Accepted, x.RemainderState, x.RemainderNote
        };
    }

    /// <summary>Прийом (FR-PAT-003, 006, 008): усі контейнери відскановано й марковано → № справи, SLA, вартість, RECEIVED.</summary>
    public async Task<object> AcceptAsync(string id, PathAcceptRequest req)
    {
        _policy.Require("Прийом матеріалу ПГД", IntakeRoles);
        var (o, r) = await LoadAsync(id);
        if (r.IntakeStatus != PathIntake.Incoming) throw new ConflictException($"Направлення вже {(r.IntakeStatus == PathIntake.Accepted ? "прийнято" : "відхилено")}");
        var objects = await _db.PathObjects.Where(x => x.OrderId == o.Id).OrderBy(x => x.DisplayOrder).ToListAsync();
        var unlabeled = objects.Where(x => !x.IsLabeled).ToList();
        if (unlabeled.Count > 0) throw Problem("container-unlabeled", $"Контейнери без маркування: {unlabeled.Count}. Прийом заблоковано — відмовте з причиною або запросіть направника");
        var scanned = req.ScannedBarcodes.Select(b => b.Trim()).Where(b => b != "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notScanned = objects.Where(x => !x.IsExternal && !scanned.Contains(x.Barcode) && !scanned.Contains(x.Mark)).ToList();
        if (notScanned.Count > 0) throw Problem("containers-not-scanned", $"Відскановано {objects.Count - notScanned.Count} з {objects.Count} контейнерів (п. 7). Не відскановано: {string.Join(", ", notScanned.Select(x => "№" + x.Mark))}");

        var now = DateTime.UtcNow;
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var type = await _db.PathMaterialTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == o.PathologyKind);
        var yy = now.ToString("yy");
        var cyto = o.PathologyKind == PathKinds.Cyto;
        o.CaseNo = cyto ? $"ЦИТ-{yy}-{await _numerators.NextCounterAsync("CYTO_CASE_" + yy):D4}" : $"ПГД-{yy}-{await _numerators.NextCounterAsync("PATH_CASE_" + yy):D4}";
        o.SlaDueAt = o.PathologyKind == PathKinds.Cito ? now.AddMinutes(settings.PathologyCitoTargetMinutes) : AddWorkdays(now, type?.SlaDays ?? (cyto ? 3 : o.PathologyKind == PathKinds.Operative ? 7 : 5));
        o.Status = OrderStatuses.Received;
        r.IntakeStatus = PathIntake.Accepted; r.AcceptedAt = now; r.AcceptedById = _current.EmployeeId;
        foreach (var x in objects) x.Accepted = true;
        Log(o, PathStages.Received, comment: $"Контейнерів: {objects.Count}");

        if (o.PathologyKind == PathKinds.Consult)
        {
            // FR-PAT-008: зовнішні блоки й скельця з власними штрихкодами, вартість за кількістю скелець
            var obj = objects[0];
            var blocks = Math.Max(1, r.ExternalBlocks);
            var prefix = string.IsNullOrWhiteSpace(r.ExternalNumber) ? "EXT" : r.ExternalNumber.Split('/')[0];
            var made = new List<LabPathBlock>();
            for (var b = 0; b < blocks; b++)
            {
                var block = new LabPathBlock { ObjectId = obj.Id, OrderId = o.Id, Code = $"{prefix}-{(char)('А' + b)}", Pieces = 0, IsExternal = true, Barcode = $"PB{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}", Stage = PathStages.Staining };
                _db.PathBlocks.Add(block); made.Add(block);
            }
            for (var s = 0; s < r.ExternalSlides; s++)
                _db.PathSlides.Add(new LabPathSlide
                {
                    BlockId = made[s * blocks / Math.Max(1, r.ExternalSlides)].Id, OrderId = o.Id, Code = $"{prefix}-{s + 1}", StainMethod = "Гематоксилін-еозин (Г-Е)", IsExternal = true,
                    Status = "READY", Barcode = $"PS{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}"
                });
            r.Price = PathPrices.ConsultSlide * r.ExternalSlides;
            r.Stage = PathStages.Microscopy;
        }
        else if (o.PathologyKind == PathKinds.Cito) r.Price = PathPrices.Cito;
        MisReferralService.EmitFor(_db, o, MisEvents.Received, new { o.CaseNo, o.OrderNumber, slaDueAt = o.SlaDueAt, kind = o.PathologyKind, price = r.Price });
        _audit.Log("PATH_ACCEPT", "lab_order", o.Id, null, new { o.CaseNo, containers = objects.Count, o.SlaDueAt, r.Price });
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    /// <summary>Відмова в прийомі (FR-PAT-005): причина з довідника, подія REJECTED; е-направлення не погашається.</summary>
    public async Task<object> RejectAsync(string id, PathRejectRequest req)
    {
        _policy.Require("Відмова в прийомі матеріалу ПГД", IntakeRoles);
        if (string.IsNullOrWhiteSpace(req.Reason)) throw ValidationException.Field("reason", "Оберіть причину відмови");
        var (o, r) = await LoadAsync(id);
        if (r.IntakeStatus != PathIntake.Incoming) throw new ConflictException("Відхилити можна лише направлення в черзі прийому");
        var reason = await _db.PathRejectReasons.AsNoTracking().FirstOrDefaultAsync(x => x.Code == req.Reason || x.Name == req.Reason);
        r.IntakeStatus = PathIntake.Rejected; r.RejectReason = reason?.Name ?? req.Reason.Trim(); r.RejectComment = req.Comment;
        o.Status = OrderStatuses.Rejected;
        MisReferralService.EmitFor(_db, o, MisEvents.Rejected, new { reasonCode = reason?.Code, reason = r.RejectReason, comment = req.Comment, ehealthReferralUsed = false });
        _audit.Log("PATH_REJECT", "lab_order", o.Id, null, new { r.RejectReason, req.Comment });
        await _db.SaveChangesAsync();
        return new { id = o.Id, intakeStatus = r.IntakeStatus, r.RejectReason };
    }

    /// <summary>Уточнення направнику (FR-PAT-002): подія QUERY.</summary>
    public async Task<object> QueryAsync(string id, PathQueryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Text)) throw ValidationException.Field("text", "Текст запиту обов'язковий");
        var (o, _) = await LoadAsync(id);
        MisReferralService.EmitFor(_db, o, MisEvents.Query, new { o.CaseNo, text = req.Text.Trim(), by = Who() });
        await _db.SaveChangesAsync();
        return new { ok = true };
    }

    // ------------------------------------------------------------------ журнал і картка справи
    public async Task<object> CasesAsync(string? stage, string? src, string? q, string? group = null)
    {
        var query = _db.PathReferrals.AsNoTracking().Include(r => r.Order).ThenInclude(o => o!.Patient).Where(r => r.IntakeStatus == PathIntake.Accepted);
        var all = await query.ToListAsync();
        if (group == "cyto") all = all.Where(r => r.Order!.PathologyKind == PathKinds.Cyto).ToList();
        else if (group == "hist") all = all.Where(r => r.Order!.PathologyKind != PathKinds.Cyto).ToList();
        var ids = all.Select(r => r.OrderId).ToList();
        var blocks = (await _db.PathBlocks.AsNoTracking().Where(b => ids.Contains(b.OrderId)).Select(b => new { b.OrderId, b.Barcode, b.Code }).ToListAsync()).ToLookup(b => b.OrderId);
        var slides = (await _db.PathSlides.AsNoTracking().Where(s => ids.Contains(s.OrderId)).Select(s => new { s.OrderId, s.Barcode, s.Status, s.IsIhc }).ToListAsync()).ToLookup(s => s.OrderId);
        var now = DateTime.UtcNow;
        // CITO: після повідомлення в операційну час зафіксовано — прострочення не росте
        bool Overdue(LabPathReferral r) => r.Stage != PathStages.Signed && (r.Order!.IsUrgentCito && r.CitoNotifiedAt.HasValue ? r.CitoNotifiedAt > r.Order.SlaDueAt : r.Order!.SlaDueAt < now);
        var tiles = PathStages.All.ToDictionary(s => s, s => all.Count(r => r.Stage == s));
        var filtered = all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(stage) && stage != "all")
            filtered = stage switch
            {
                "work" => filtered.Where(r => r.Stage != PathStages.Signed),
                "overdue" => filtered.Where(Overdue),
                "cito" => filtered.Where(r => r.Order!.IsUrgentCito && r.Stage != PathStages.Signed),
                "lab" => filtered.Where(r => PathStages.Lab.Contains(r.Stage)),
                _ => filtered.Where(r => r.Stage == stage)
            };
        if (!string.IsNullOrWhiteSpace(src) && src != "all") filtered = filtered.Where(r => ReferralSources.Of(r.Order!) == src.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            filtered = filtered.Where(r => (r.Order!.CaseNo ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) || (r.Order.Patient?.Caption ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                || r.Order.OrderNumber.Contains(s, StringComparison.OrdinalIgnoreCase) || blocks[r.OrderId].Any(b => b.Barcode == s || b.Code == s) || slides[r.OrderId].Any(x => x.Barcode == s));
        }
        var items = filtered
            .OrderByDescending(r => r.Order!.IsUrgentCito && r.Stage != PathStages.Signed).ThenByDescending(Overdue).ThenBy(r => r.Stage == PathStages.Signed).ThenByDescending(r => r.AcceptedAt)
            .Select(r =>
            {
                var o = r.Order!;
                return new
                {
                    id = o.Id, o.CaseNo, o.OrderNumber, patient = o.Patient?.Caption, birthday = o.Patient?.Birthday, kind = o.PathologyKind, kindLabel = PathKinds.Label(o.PathologyKind),
                    source = ReferralSources.Of(o), sourceLabel = ReferralSources.Label(ReferralSources.Of(o)), department = o.RequestingDepartmentName, cito = o.IsUrgentCito,
                    stage = r.Stage, stageLabel = PathStages.LabelFor(o.PathologyKind, r.Stage), stageIndex = Array.IndexOf(PathStages.All, r.Stage), receivedAt = r.AcceptedAt, slaDueAt = o.SlaDueAt, overdue = Overdue(r),
                    citoMinutes = o.IsUrgentCito && r.AcceptedAt.HasValue ? (int?)CitoMinutes(r, now) : null, citoNotifiedAt = r.CitoNotifiedAt, signedAt = r.Stage == PathStages.Signed ? o.CompletedAt : null,
                    screen = r.ScreenAssessment, material = r.CytoMaterial, materialLabel = r.CytoMaterial == null ? null : PathCytology.Materials.FirstOrDefault(m => m.Code == r.CytoMaterial).Name, blocks = blocks[o.Id].Count(), slides = slides[o.Id].Count(), ihcInWork = slides[o.Id].Count(x => x.Status == "IHC_IN_WORK"), price = r.Price
                };
            }).ToList();
        var incoming = await _db.PathReferrals.CountAsync(r => r.IntakeStatus == PathIntake.Incoming && r.Order!.Status != OrderStatuses.Cancelled);
        return new
        {
            tiles, total = all.Count, inWork = all.Count(r => r.Stage != PathStages.Signed), overdue = all.Count(Overdue), cito = all.Count(r => r.Order!.IsUrgentCito && r.Stage != PathStages.Signed),
            lab = all.Count(r => PathStages.Lab.Contains(r.Stage)), incoming, items
        };
    }

    /// <summary>Лічильники для меню (бейджі розд. 22.14 п. 1).</summary>
    public async Task<object> SummaryAsync()
    {
        var now = DateTime.UtcNow;
        var active = _db.PathReferrals.Where(r => r.IntakeStatus == PathIntake.Accepted && r.Stage != PathStages.Signed);
        return new
        {
            incoming = await _db.PathReferrals.CountAsync(r => r.IntakeStatus == PathIntake.Incoming && r.Order!.Status != OrderStatuses.Cancelled),
            inWork = await active.CountAsync(),
            cito = await active.CountAsync(r => r.Order!.IsUrgentCito) + await _db.PathReferrals.CountAsync(r => r.IntakeStatus == PathIntake.Incoming && r.Order!.IsUrgentCito),
            overdue = await active.CountAsync(r => r.Order!.SlaDueAt < now && !(r.Order.IsUrgentCito && r.CitoNotifiedAt != null && r.CitoNotifiedAt <= r.Order.SlaDueAt)),
            issued = await _db.PathBlocks.CountAsync(b => b.Status == "ISSUED")
        };
    }

    public async Task<object> CaseAsync(string id)
    {
        var (o, r) = await LoadAsync(id);
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var now = DateTime.UtcNow;
        var objects = await _db.PathObjects.AsNoTracking().Include(x => x.Blocks).ThenInclude(b => b.Slides).Where(x => x.OrderId == o.Id).OrderBy(x => x.DisplayOrder).ToListAsync();
        var reports = await _db.PathReports.AsNoTracking().Include(x => x.Ihc).Where(x => x.OrderId == o.Id).OrderBy(x => x.Version).ThenBy(x => x.CreatedOn).ToListAsync();
        var draft = reports.LastOrDefault(x => x.Kind == PathReportKinds.Main);
        var log = await _db.PathStageLog.AsNoTracking().Where(x => x.OrderId == o.Id).OrderBy(x => x.PerformedAt).ToListAsync();
        var events = await _db.MisEvents.AsNoTracking().Where(e => e.OrderId == o.Id).OrderBy(e => e.At).ToListAsync();
        var notices = await _db.PathCitoNotices.AsNoTracking().Where(n => n.OrderId == o.Id).OrderBy(n => n.CreatedOn).ToListAsync();
        var blockIds = objects.SelectMany(x => x.Blocks).Select(b => b.Id).ToList();
        var issues = await _db.PathIssues.AsNoTracking().Where(i => blockIds.Contains(i.BlockId)).OrderByDescending(i => i.IssuedAt).ToListAsync();
        var primary = r.PrimaryCaseNo == null ? null : await _db.Orders.AsNoTracking().Where(x => x.CaseNo == r.PrimaryCaseNo).Select(x => x.Id).FirstOrDefaultAsync();
        // FR-PAT-010: МІС позначила «первинна», а підписана справа пацієнта вже існує
        var priorSigned = r.BiopsyKind == "PRIMARY"
            ? await _db.PathReferrals.AsNoTracking().Where(x => x.Order!.PatientId == o.PatientId && x.OrderId != o.Id && x.Stage == PathStages.Signed).Select(x => x.Order!.CaseNo).ToListAsync()
            : new List<string?>();
        var slides = objects.SelectMany(x => x.Blocks).SelectMany(b => b.Slides).ToList();
        var blocks = objects.SelectMany(x => x.Blocks).ToList();
        var signed = reports.Where(x => x.SignedAt != null && x.Kind is PathReportKinds.Main or PathReportKinds.Addendum).ToList();
        return new
        {
            id = o.Id, o.CaseNo, o.OrderNumber, o.Status, intakeStatus = r.IntakeStatus, rejectReason = r.RejectReason, kind = o.PathologyKind, kindLabel = PathKinds.Label(o.PathologyKind), cito = o.IsUrgentCito,
            source = ReferralSources.Of(o), sourceLabel = ReferralSources.Label(ReferralSources.Of(o)), department = o.RequestingDepartmentName, misReferralId = o.MisReferralId, referralNumber = o.MisReferralNumber,
            ehealthReferral = o.ReferralType == ReferralTypes.Ehealth ? o.ReferrerNumber : null, o.EhealthStatus,
            patient = new { id = o.PatientId, name = o.Patient?.Caption, birthday = o.Patient?.Birthday, gender = o.Patient?.Gender },
            createdAt = o.OrderDatetime, receivedAt = r.AcceptedAt, slaDueAt = o.SlaDueAt, overdue = r.Stage != PathStages.Signed && (o.IsUrgentCito && r.CitoNotifiedAt.HasValue ? r.CitoNotifiedAt > o.SlaDueAt : o.SlaDueAt < now), releasedAt = o.ReleasedAt, signedAt = r.Stage == PathStages.Signed ? o.CompletedAt : null,
            stage = r.Stage, stageLabel = PathStages.LabelFor(o.PathologyKind, r.Stage),
            stages = PathStages.All.Where(s => o.PathologyKind == PathKinds.Cyto ? s is PathStages.Received or PathStages.Staining or PathStages.Microscopy or PathStages.Signed
                    : s != PathStages.Decalcification || blocks.Any(b => b.Track == PathTracks.Decalc))
                .Select(s => new { code = s, label = o.PathologyKind == PathKinds.Cyto ? (s == PathStages.Received ? "Прийом і приготування" : s == PathStages.Microscopy ? "Скринінг і висновок" : PathStages.Label(s)) : PathStages.Label(s), done = Array.IndexOf(PathStages.All, s) < Array.IndexOf(PathStages.All, r.Stage) || r.Stage == PathStages.Signed, current = s == r.Stage }),
            cyto = o.PathologyKind != PathKinds.Cyto ? null : new
            {
                material = r.CytoMaterial, materialLabel = PathCytology.Materials.FirstOrDefault(m => m.Code == r.CytoMaterial).Name, prepared = r.PreparedAt,
                screening = r.ScreenAssessment == null ? null : new { assessment = r.ScreenAssessment, note = r.ScreenNote, by = r.ScreenerName, at = r.ScreenedAt }
            },
            pathologist = r.PathologistId == null ? null : new { id = r.PathologistId, name = r.PathologistName }, assistant = r.AssistantId == null ? null : new { id = r.AssistantId, name = r.AssistantName },
            citoTimer = o.IsUrgentCito ? new
            {
                targetMinutes = settings.PathologyCitoTargetMinutes, startedAt = r.AcceptedAt, notifiedAt = r.CitoNotifiedAt,
                minutes = r.AcceptedAt.HasValue ? (int?)CitoMinutes(r, now) : null
            } : null,
            referral = ReferralView(r, o), primaryCaseId = primary, priorSignedCases = priorSigned,
            macro = r.Macro, templateName = r.TemplateName, grossFinishedAt = r.GrossFinishedAt, price = r.Price,
            objects = objects.Select(x => new
            {
                x.Id, x.Letter, x.Mark, x.Description, x.Localization, x.Fragments, x.Fixative, x.FixationStartAt, x.Barcode, x.IsExternal, x.Accepted, x.RemainderState, x.RemainderNote,
                blocks = x.Blocks.OrderBy(b => b.Code.Length).ThenBy(b => b.Code).Select(b => new
                {
                    b.Id, b.Code, b.Pieces, b.IsExternal, b.Barcode, b.StorageLocation, b.RoomId, b.Status, b.Stage, stageLabel = b.Stage == null ? null : PathStages.Label(b.Stage), b.Track, trackLabel = PathTracks.Label(b.Track),
                    issue = issues.Where(i => i.BlockId == b.Id && i.ReturnedAt == null).Select(i => new { i.Recipient, i.Purpose, i.IssuedAt, i.DueBack }).FirstOrDefault(),
                    slides = b.Slides.OrderBy(s => s.CreatedOn).ThenBy(s => s.Code).Select(s => new { s.Id, s.Code, s.StainMethod, s.IsIhc, s.Marker, s.Status, s.Barcode, s.IsExternal, s.OrderedFrom, s.CoveredAt, s.DisposedAt, s.DisposeReason, s.ReplacesSlideId })
                })
            }),
            totals = new { pieces = blocks.Sum(b => b.Pieces), blocks = blocks.Count, slides = slides.Count, stains = slides.Select(s => s.StainMethod).Distinct().ToList() },
            report = draft == null ? null : ReportView(draft),
            composedText = draft == null ? null : Compose(draft),
            reflex = draft == null ? new List<object>() : await ReflexAsync(draft.IcdoTopo, draft.IcdoMorph, slides),
            versions = signed.Select(x => new { x.Id, x.Version, x.Kind, kindLabel = x.Kind == PathReportKinds.Main ? "Основний висновок" : "Доповнення", x.AddendumType, x.ConsultantName, x.ConclusionText, x.ChangesDiagnosis, x.Reason, x.SignedAt, x.SignedByName, x.IcdoTopo, x.IcdoMorph }),
            preliminary = reports.Where(x => x.Kind == PathReportKinds.Preliminary).Select(x => new { x.ConclusionText, at = x.CreatedOn }).LastOrDefault(),
            citoNotices = notices.Select(n => new { n.Text, n.NotifiedPerson, n.Method, n.ReadBack, n.MinutesFromReceipt, at = n.CreatedOn }),
            stageLog = log.Select(l => new { l.Stage, stageLabel = PathStages.LabelFor(o.PathologyKind, l.Stage), l.BlockId, l.SlideId, l.PerformedByName, l.PerformedAt, l.Device, l.Comment }),
            events = events.Select(e => new { e.Event, e.At, e.Payload })
        };
    }

    private static object ReportView(LabPathReport x) => new
    {
        x.Id, x.Version, x.Kind, x.Macro, x.Micro, x.ConclusionText, x.Icd10, x.IcdoTopo, x.IcdoMorph, x.Grade, x.Pt, x.Pn, x.LnTotal, x.LnPositive, x.Margin, x.Lvi, x.Pni,
        x.Adequacy, x.CytoCategory, x.Recommendation, signed = x.SignedAt != null, x.SignedAt, x.SignedByName,
        ihc = x.Ihc.Select(i => new { i.Marker, i.SlideId, i.ResultText, i.Score, i.Unit })
    };

    /// <summary>Текст п. 18 зі структурованих полів (FR-PAT-041).</summary>
    public static string Compose(LabPathReport x)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(x.Adequacy)) parts.Add("Адекватність: " + PathCytology.Adequacy.FirstOrDefault(a => a.Code == x.Adequacy).Name + ".");
        if (!string.IsNullOrWhiteSpace(x.CytoCategory)) parts.Add("Категорія: " + PathCytology.Categories.FirstOrDefault(c => c.Code == x.CytoCategory).Name + $" ({x.CytoCategory}).");
        if (!string.IsNullOrWhiteSpace(x.Recommendation)) parts.Add("Рекомендації: " + x.Recommendation.Trim().TrimEnd('.') + ".");
        var stage = string.Join(" ", new[] { x.Grade, x.Pt, x.Pn }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (stage != "") parts.Add(stage + (string.IsNullOrWhiteSpace(x.Margin) ? "" : $", край резекції {x.Margin}") + ".");
        else if (!string.IsNullOrWhiteSpace(x.Margin)) parts.Add($"Край резекції: {x.Margin}.");
        if (x.Lvi.HasValue) parts.Add(x.Lvi.Value ? "Лімфоваскулярну інвазію виявлено." : "Лімфоваскулярної інвазії не виявлено.");
        if (x.Pni.HasValue) parts.Add(x.Pni.Value ? "Периневральну інвазію виявлено." : "Периневральної інвазії не виявлено.");
        if (x.LnTotal.HasValue) parts.Add($"Лімфатичні вузли: уражено {x.LnPositive ?? 0} з {x.LnTotal}.");
        // у висновок потрапляють лише маркери з внесеним результатом; порожні («очікується») не пишемо
        var ihcDone = x.Ihc.Where(i => !string.IsNullOrWhiteSpace(i.ResultText)).ToList();
        if (ihcDone.Count > 0) parts.Add("ІГХ: " + string.Join("; ", ihcDone.Select(i => $"{i.Marker} — {i.ResultText!.Trim()}{(string.IsNullOrWhiteSpace(i.Score) ? "" : $" ({i.Score}{i.Unit})")}")) + ".");
        if (!string.IsNullOrWhiteSpace(x.IcdoTopo) || !string.IsNullOrWhiteSpace(x.IcdoMorph)) parts.Add($"МКХ-О: {x.IcdoTopo}, {x.IcdoMorph}.");
        return string.Join(" ", parts);
    }

    /// <summary>Reflex ІГХ (FR-PAT-042): правило за топографією (діапазон) і морфологією → панель без уже замовлених маркерів.</summary>
    private async Task<List<object>> ReflexAsync(string? topo, string? morph, List<LabPathSlide> slides)
    {
        if (string.IsNullOrWhiteSpace(topo) || string.IsNullOrWhiteSpace(morph)) return new();
        var site = topo.Trim().ToUpperInvariant()[..Math.Min(3, topo.Trim().Length)];
        var ordered = slides.Where(s => s.Marker != null).Select(s => s.Marker!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = await _db.IhcReflexRules.AsNoTracking().ToListAsync();
        return rules.Where(rule => string.CompareOrdinal(site, rule.TopoFrom) >= 0 && string.CompareOrdinal(site, rule.TopoTo) <= 0 && rule.MorphCodes.Split(',').Select(m => m.Trim()).Contains(morph.Trim()))
            .Select(rule => (object)new
            {
                when = $"{rule.TopoFrom}{(rule.TopoTo != rule.TopoFrom ? "–" + rule.TopoTo : "")} + {rule.MorphCodes.Replace(",", " або ")}", rule.Why,
                panel = rule.Panel.Split(',').Select(p => p.Trim()).ToList(),
                missing = rule.Panel.Split(',').Select(p => p.Trim()).Where(p => !ordered.Contains(p)).ToList()
            }).ToList();
    }

    // ------------------------------------------------------------------ вирізка й обробка (22.4)
    /// <summary>Макроопис і блоки (FR-PAT-020…022); finish — скельця Г-Е, вартість, етап «Проводка».</summary>
    public async Task<object> SaveGrossAsync(string id, PathGrossRequest req)
    {
        _policy.Require("Вирізка", PathologistRoles);
        var pendingMethods = new Dictionary<string, List<string>>();
        var (o, r) = await LoadAsync(id);
        if (r.IntakeStatus != PathIntake.Accepted) throw new ConflictException("Вирізка можлива після прийому матеріалу");
        if (r.Stage is not (PathStages.Received or PathStages.Gross)) throw new ConflictException($"Вирізку завершено, справа на етапі «{PathStages.Label(r.Stage)}»");
        var objects = await _db.PathObjects.Include(x => x.Blocks).Where(x => x.OrderId == o.Id).OrderBy(x => x.DisplayOrder).ToListAsync();
        var methodDict = await _db.StainMethods.AsNoTracking().ToListAsync();
        if (req.Macro != null) r.Macro = req.Macro;
        if (req.TemplateName != null) r.TemplateName = req.TemplateName;
        if (!string.IsNullOrWhiteSpace(req.PathologistId)) { var e = await StaffAsync(req.PathologistId, PathologistRoles, "pathologistId", "лікаря-патолога"); r.PathologistId = e.Id; r.PathologistName = e.Caption; }
        if (!string.IsNullOrWhiteSpace(req.AssistantId)) { var e = await StaffAsync(req.AssistantId, HistotechRoles, "assistantId", "асистента"); r.AssistantId = e.Id; r.AssistantName = e.Caption; }
        foreach (var g in req.Objects)
        {
            var obj = objects.FirstOrDefault(x => x.Id == g.ObjectId || x.Mark == g.ObjectId) ?? throw NotFoundException.For("Об'єкт справи", g.ObjectId);
            if (g.Blocks.Any(b => b.Pieces < 1)) throw ValidationException.Field("pieces", "Кількість шматочків у блоці — щонайменше 1");
            if (g.RemainderState != null)
            {
                var rem = g.RemainderState.Trim().ToUpperInvariant();
                if (!PathRemainder.All.Contains(rem)) throw ValidationException.Field("remainderState", "Залишок: NONE, KEPT або DISCARDED");
                obj.RemainderState = rem; obj.RemainderNote = rem == PathRemainder.None ? null : Clean(g.RemainderNote, null);
            }
            _db.PathBlocks.RemoveRange(obj.Blocks.Where(b => !b.IsExternal));
            obj.Blocks.RemoveAll(b => !b.IsExternal);
            var n = 0;
            foreach (var b in g.Blocks)
            {
                var track = string.IsNullOrWhiteSpace(b.Track) ? PathTracks.Standard : b.Track.Trim().ToUpperInvariant();
                if (!PathTracks.All.Contains(track)) throw ValidationException.Field("track", "Маршрут блока: STANDARD або DECALC");
                var methods = (b.Methods ?? new()).Select(m => m.Trim()).Where(m => m != "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (methods.Count > PathLimits.MaxMethodsPerBlock) throw ValidationException.Field("methods", $"На блок — не більше {PathLimits.MaxMethodsPerBlock} методик");
                foreach (var m in methods)
                    if (!methodDict.Any(d => d.Code.Equals(m, StringComparison.OrdinalIgnoreCase))) throw ValidationException.Field("methods", $"Методику «{m}» не знайдено в довіднику");
                var block = new LabPathBlock { ObjectId = obj.Id, OrderId = o.Id, Code = $"{obj.Letter}{++n}", Pieces = b.Pieces, Track = track, Barcode = $"PB{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}" };
                pendingMethods[block.Id] = methods;
                obj.Blocks.Add(block); _db.PathBlocks.Add(block);
            }
        }
        var all = objects.SelectMany(x => x.Blocks).Where(b => !b.IsExternal).ToList();
        if (r.Stage == PathStages.Received) { r.Stage = PathStages.Gross; o.Status = OrderStatuses.InProgress; }
        if (req.Finish)
        {
            if (all.Count == 0) throw ValidationException.Field("objects", "Сформуйте хоча б один блок");
            if (string.IsNullOrWhiteSpace(r.Macro)) throw ValidationException.Field("macro", "Заповніть макроопис");
            var macroOpen = OpenPlaceholders(r.Macro);
            if (macroOpen.Count > 0) throw ValidationException.Field("macro", PlaceholdersMessage("Макроопис", macroOpen));
            if (string.IsNullOrWhiteSpace(r.PathologistId)) throw ValidationException.Field("pathologistId", "Оберіть лікаря-патолога");
            if (string.IsNullOrWhiteSpace(r.AssistantId)) throw ValidationException.Field("assistantId", "Вкажіть, хто провів вирізку");
            var extra = 0m;
            foreach (var b in all)
            {
                var codes = pendingMethods.TryGetValue(b.Id, out var chosen) && chosen.Count > 0 ? chosen : new List<string> { "HE" };
                var n = 0;
                foreach (var code in codes)
                {
                    var m = methodDict.FirstOrDefault(d => d.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                    // порожній вибір = Г-Е; довідник може не містити коду HE — тоді назва за замовчуванням
                    var isHe = m == null;
                    _db.PathSlides.Add(new LabPathSlide
                    {
                        BlockId = b.Id, OrderId = o.Id, Code = $"{b.Code}-{++n}", StainMethod = isHe ? "Гематоксилін-еозин (Г-Е)" : m!.IsIhc ? $"ІГХ {m.Code}" : m.Name,
                        IsIhc = m?.IsIhc == true, Marker = m?.IsIhc == true ? m.Code : null, Status = m?.IsIhc == true ? "IHC_IN_WORK" : "IN_WORK",
                        Barcode = $"PS{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}"
                    });
                    if (m != null && !m.Code.Equals("HE", StringComparison.OrdinalIgnoreCase)) extra += m.IsIhc ? (m.Price > 0 ? m.Price : PathPrices.Ihc) : m.Price;
                }
            }
            r.Price = (o.PathologyKind == PathKinds.Cito ? PathPrices.Cito : 0) + GrossPrice(all.Count) + extra;
            r.GrossFinishedAt = DateTime.UtcNow; r.GrossById = _current.EmployeeId;
            Log(o, PathStages.Gross, comment: $"Блоків: {all.Count}, шматочків: {all.Sum(b => b.Pieces)}; декальцинація: {all.Count(b => b.Track == PathTracks.Decalc)}; патолог: {r.PathologistName}; вирізку провів: {r.AssistantName}");
            SetStage(o, r, all.Any(b => b.Track == PathTracks.Decalc) ? PathStages.Decalcification : PathStages.Processing);
            _audit.Log("PATH_GROSS", "lab_order", o.Id, null, new { blocks = all.Count, pieces = all.Sum(b => b.Pieces), r.Price });
        }
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    /// <summary>Фіксація етапу обробки скануванням касети або скельця (FR-PAT-023); етап справи — найменш просунутий блок.</summary>
    public async Task<object> ScanAsync(PathScanRequest req)
    {
        _policy.Require("Етапи гістологічної обробки", HistotechRoles);
        var code = req.Barcode.Trim();
        if (code == "") throw ValidationException.Field("barcode", "Відскануйте штрихкод касети або скельця");
        var slide = await _db.PathSlides.FirstOrDefaultAsync(s => s.Barcode == code);
        var block = slide != null ? await _db.PathBlocks.FirstAsync(b => b.Id == slide.BlockId) : await _db.PathBlocks.FirstOrDefaultAsync(b => b.Barcode == code)
                    ?? throw NotFoundException.For("Касета або скельце", code);
        var (o, r) = await LoadAsync(block.OrderId);
        var performer = await PerformerAsync(req.PerformerId);
        if (string.Equals(req.Stage?.Trim(), PathStages.Coverslip, StringComparison.OrdinalIgnoreCase))
        {
            // FR-PAT-114: допоміжна відмітка «покрито покривним склом» — лише для скельця, фарбування якого виконано
            if (slide == null) throw ValidationException.Field("barcode", "Покриття фіксується скануванням скельця");
            if (block.Stage is not PathStages.Staining) throw new ConflictException($"Скельце {slide.Code}: покриття можливе після фарбування (етап блока — «{PathStages.Label(block.Stage)}»)");
            if (slide.Status == "DISPOSED") throw new ConflictException($"Скельце {slide.Code} утилізовано");
            if (slide.CoveredAt.HasValue) throw new ConflictException($"Скельце {slide.Code} уже покрито");
            // порядок: фарбування → покриття. Нове скельце (після утилізації, повторного приготування чи дозамовлення) спершу позначають «забарвлено»
            if (!IsStained(slide, r)) throw Problem("slide-not-stained", slide.IsIhc ? $"Скельце {slide.Code}: ІГХ ще в роботі — спершу позначте скельце готовим, потім покрийте склом" : $"Скельце {slide.Code} ще не забарвлено — спершу позначте «Забарвлено», потім покрийте склом", 409);
            slide.CoveredAt = DateTime.UtcNow;
            Log(o, PathStages.Coverslip, block.Id, slide.Id, req.Device, null, performer);
            await _db.SaveChangesAsync();
            return new { caseId = o.Id, o.CaseNo, blockCode = block.Code, slideCode = slide.Code, stage = PathStages.Coverslip, stageLabel = PathStages.Label(PathStages.Coverslip), coveredAt = slide.CoveredAt };
        }
        if (!PathStages.Lab.Contains(r.Stage)) throw new ConflictException($"Справа {o.CaseNo} на етапі «{PathStages.Label(r.Stage)}» — сканування етапів обробки недоступне");
        var first = o.PathologyKind == PathKinds.Cyto ? PathStages.Staining : block.Track == PathTracks.Decalc ? PathStages.Decalcification : PathStages.Processing;
        var target = string.IsNullOrWhiteSpace(req.Stage) ? (block.Stage == null ? first : PathStages.Next(block.Stage)) : req.Stage.Trim().ToUpperInvariant();
        if (target == null || !PathStages.Lab.Contains(target)) throw ValidationException.Field("stage", "Етап: DECALC, PROC, EMB, SECT або STAIN");
        CheckTrack(block, target);
        var comment = target == PathStages.Sectioning && req.Count is > 0 ? $"Зрізів: {req.Count}" : null;
        await AdvanceBlocksAsync(o, r, new[] { block }, target, req.Device, slide?.Id, comment, performer);
        await _db.SaveChangesAsync();
        return new { caseId = o.Id, o.CaseNo, blockCode = block.Code, stage = target, stageLabel = PathStages.Label(target), caseStage = r.Stage, caseStageLabel = PathStages.Label(r.Stage) };
    }

    /// <summary>Етап для всіх касет справи (робоче місце лаборанта: «Виконано для всіх»).</summary>
    public async Task<object> AdvanceAsync(string id, PathScanRequest req)
    {
        _policy.Require("Етапи гістологічної обробки", HistotechRoles);
        var (o, r) = await LoadAsync(id);
        if (!PathStages.Lab.Contains(r.Stage)) throw new ConflictException($"Справа на етапі «{PathStages.Label(r.Stage)}»");
        var blocks = await _db.PathBlocks.Where(b => b.OrderId == o.Id && !b.IsExternal).ToListAsync();
        var target = string.IsNullOrWhiteSpace(req.Stage) ? r.Stage : req.Stage.Trim().ToUpperInvariant();
        if (!PathStages.Lab.Contains(target)) throw ValidationException.Field("stage", "Етап: DECALC, PROC, EMB, SECT або STAIN");
        var performer = await PerformerAsync(req.PerformerId);
        // «Виконано для всіх»: блоки зі стандартним маршрутом пропускають декальцинацію
        var due = blocks.Where(b => (target != PathStages.Decalcification || b.Track == PathTracks.Decalc)
            && (b.Stage == null || Array.IndexOf(PathStages.All, b.Stage) < Array.IndexOf(PathStages.All, target))).ToList();
        foreach (var b in due) CheckTrack(b, target);
        var comment = target == PathStages.Sectioning && req.Count is > 0 ? $"Зрізів: {req.Count}" : null;
        await AdvanceBlocksAsync(o, r, due, target, req.Device, null, comment, performer);
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    /// <summary>Скельце забарвлено: статус «Готово», або первинне скельце при завершеному фарбуванні касети. Нові скельця (заміна, повтор, дозамовлення, ІГХ) — лише після явної відмітки.</summary>
    private static bool IsStained(LabPathSlide s, LabPathReferral r) =>
        s.Status == "READY" || (s.Status == "IN_WORK" && !s.IsIhc && s.ReplacesSlideId == null && s.OrderedFrom == null && r.Stage == PathStages.Staining);

    /// <summary>Правила маршруту (FR-PAT-110): декальцинація лише для блоків DECALC, а проводка таких блоків — лише після декальцинації.</summary>
    private static void CheckTrack(LabPathBlock b, string target)
    {
        if (target == PathStages.Decalcification && b.Track != PathTracks.Decalc)
            throw new ConflictException($"Блок {b.Code} має стандартний маршрут — декальцинація не передбачена");
        if (target == PathStages.Processing && b.Track == PathTracks.Decalc && b.Stage != PathStages.Decalcification)
            throw new ConflictException($"Блок {b.Code} потребує декальцинації перед проводкою");
    }

    private async Task AdvanceBlocksAsync(LabOrder o, LabPathReferral r, IEnumerable<LabPathBlock> blocks, string target, string? device, string? slideId, string? comment = null, OrgEmployee? performer = null)
    {
        foreach (var b in blocks) { b.Stage = target; Log(o, target, b.Id, slideId, device, comment, performer); }
        var all = await _db.PathBlocks.Where(b => b.OrderId == o.Id && !b.IsExternal).ToListAsync();
        foreach (var b in blocks) { var t = all.FirstOrDefault(x => x.Id == b.Id); if (t != null) t.Stage = target; }
        // етап касети — останній виконаний; етап справи — наступний після найменш просунутої касети
        var done = all.Select(b => Array.IndexOf(PathStages.All, b.Stage ?? PathStages.Gross)).DefaultIfEmpty(1).Min();
        var caseStage = PathStages.All[Math.Min(done + 1, Array.IndexOf(PathStages.All, PathStages.Microscopy))];
        if (caseStage == PathStages.Microscopy)
            foreach (var s in await _db.PathSlides.Where(s => s.OrderId == o.Id && s.Status == "IN_WORK" && !s.IsIhc).ToListAsync()) s.Status = "READY";
        if (Array.IndexOf(PathStages.All, caseStage) > Array.IndexOf(PathStages.All, r.Stage)) SetStage(o, r, caseStage);
    }

    /// <summary>Додаткові забарвлення / ІГХ на наявному блоці (FR-PAT-025, 026): скельця «ІГХ в роботі», +3 роб. дні, вартість, подія.</summary>
    public async Task<object> AdditionalAsync(string id, PathAdditionalRequest req)
    {
        _policy.Require("Дозамовлення забарвлень та ІГХ", PathologistRoles);
        var (o, r) = await LoadAsync(id);
        if (r.IntakeStatus != PathIntake.Accepted || r.Stage is PathStages.Received or PathStages.Gross) throw new ConflictException("Дозамовлення можливе після вирізки");
        var block = await _db.PathBlocks.FirstOrDefaultAsync(b => b.OrderId == o.Id && (b.Id == req.BlockId || b.Code == req.BlockId)) ?? throw NotFoundException.For("Блок", req.BlockId);
        if (block.Status == "ISSUED") throw Problem("block-issued", $"Блок {block.Code} видано з архіву — дозамовлення недоступне до повернення", 409);
        if (req.Methods.Count == 0) throw ValidationException.Field("methods", "Оберіть методики");
        var source = (req.Source ?? "LAB").ToUpperInvariant();
        if (source is not ("LAB" or "MIS" or "MDK")) throw ValidationException.Field("source", "Джерело: LAB, MIS або MDK");
        var dict = await _db.StainMethods.AsNoTracking().ToListAsync();
        var existing = await _db.PathSlides.CountAsync(s => s.BlockId == block.Id);
        var added = new List<LabStainMethod>();
        foreach (var m in req.Methods.Select(x => x.Trim()).Where(x => x != "").Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var method = dict.FirstOrDefault(d => d.Code.Equals(m, StringComparison.OrdinalIgnoreCase) || d.Name.Equals(m, StringComparison.OrdinalIgnoreCase))
                         ?? throw ValidationException.Field("methods", $"Методику «{m}» не знайдено в довіднику");
            _db.PathSlides.Add(new LabPathSlide
            {
                BlockId = block.Id, OrderId = o.Id, Code = $"{block.Code}-{++existing}", StainMethod = method.IsIhc ? $"ІГХ {method.Code}" : method.Name, IsIhc = method.IsIhc,
                Marker = method.IsIhc ? method.Code : null, Status = method.IsIhc ? "IHC_IN_WORK" : "IN_WORK", OrderedFrom = source, Barcode = $"PS{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}"
            });
            added.Add(method);
        }
        r.Price += added.Sum(m => m.IsIhc ? (m.Price > 0 ? m.Price : PathPrices.Ihc) : m.Price);
        if (added.Any(m => m.IsIhc) && o.SlaDueAt.HasValue) o.SlaDueAt = AddWorkdays(o.SlaDueAt.Value, PathPrices.IhcExtraWorkdays);
        if (r.Stage == PathStages.Signed) { /* після підпису — результат піде доповненням (FR-PAT-045) */ }
        Log(o, r.Stage, block.Id, comment: $"Дозамовлено ({(source == "MIS" ? "від МІС" : source == "MDK" ? "від мультидисциплінарної комісії" : "лабораторією")}): {string.Join(", ", added.Select(m => m.Code))}");
        MisReferralService.EmitFor(_db, o, MisEvents.AdditionalStudyOrdered, new { o.CaseNo, block = block.Code, methods = added.Select(m => m.Code), source, slaDueAt = o.SlaDueAt, price = r.Price });
        _audit.Log("PATH_ADDITIONAL", "lab_order", o.Id, null, new { block = block.Code, methods = added.Select(m => m.Code), source, o.SlaDueAt });
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    /// <summary>Результат ІГХ-скельця готовий (лаборант): «ІГХ в роботі» → «Готово».</summary>
    public async Task<object> SlideReadyAsync(string slideId)
    {
        _policy.Require("Етапи гістологічної обробки", HistotechRoles);
        var s = await _db.PathSlides.FirstOrDefaultAsync(x => x.Id == slideId || x.Barcode == slideId) ?? throw NotFoundException.For("Скельце", slideId);
        s.Status = "READY";
        var (o, _) = await LoadAsync(s.OrderId);
        Log(o, PathStages.Staining, s.BlockId, s.Id, comment: $"Скельце {s.Code} готове");
        await _db.SaveChangesAsync();
        return new { s.Id, s.Code, s.Status };
    }

    // ------------------------------------------------------------------ висновок (22.5)
    private static void Apply(LabPathReport x, PathReportRequest req)
    {
        x.Macro = req.Macro ?? x.Macro; x.Micro = req.Micro ?? x.Micro; x.ConclusionText = req.ConclusionText ?? x.ConclusionText;
        x.Icd10 = Clean(req.Icd10, x.Icd10); x.IcdoTopo = Clean(req.IcdoTopo, x.IcdoTopo); x.IcdoMorph = Clean(req.IcdoMorph, x.IcdoMorph);
        x.Adequacy = Clean(req.Adequacy, x.Adequacy); x.CytoCategory = Clean(req.CytoCategory, x.CytoCategory); x.Recommendation = Clean(req.Recommendation, x.Recommendation);
        if (x.Adequacy != null && !PathCytology.Adequacy.Any(a => a.Code == x.Adequacy)) throw ValidationException.Field("adequacy", "Адекватність: SATISFACTORY, LIMITED або UNSATISFACTORY");
        if (x.CytoCategory != null && !PathCytology.Categories.Any(c => c.Code == x.CytoCategory)) throw ValidationException.Field("cytoCategory", "Невідома категорія висновку");
        x.Grade = Clean(req.Grade, x.Grade); x.Pt = Clean(req.Pt, x.Pt); x.Pn = Clean(req.Pn, x.Pn); x.Margin = Clean(req.Margin, x.Margin);
        x.LnTotal = req.LnTotal ?? x.LnTotal; x.LnPositive = req.LnPositive ?? x.LnPositive; x.Lvi = req.Lvi ?? x.Lvi; x.Pni = req.Pni ?? x.Pni;
        if (x.LnTotal.HasValue && x.LnPositive > x.LnTotal) throw ValidationException.Field("lnPositive", "Уражених лімфовузлів не може бути більше, ніж досліджено");
    }

    private static string? Clean(string? v, string? old) => v == null ? old : string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private async Task<LabPathReport> DraftAsync(LabOrder o, LabPathReferral r)
    {
        var x = await _db.PathReports.Include(p => p.Ihc).Where(p => p.OrderId == o.Id && p.Kind == PathReportKinds.Main).OrderByDescending(p => p.Version).FirstOrDefaultAsync();
        if (x != null) return x;
        x = new LabPathReport { OrderId = o.Id, Version = 1, Kind = PathReportKinds.Main, Macro = r.Macro };
        _db.PathReports.Add(x);
        return x;
    }

    private void SetIhc(LabPathReport x, List<PathIhcRow>? rows)
    {
        if (rows == null) return;
        _db.PathIhcResults.RemoveRange(x.Ihc);
        x.Ihc.Clear();
        foreach (var i in rows.Where(i => !string.IsNullOrWhiteSpace(i.Marker)))
        {
            var row = new LabPathIhcResult { ReportId = x.Id, SlideId = i.SlideId, Marker = i.Marker.Trim(), ResultText = i.ResultText, Score = i.Score, Unit = i.Unit };
            x.Ihc.Add(row); _db.PathIhcResults.Add(row);
        }
    }

    /// <summary>Чернетка висновку (FR-PAT-040, 041); після підпису — 409, лише доповнення (FR-PAT-045).</summary>
    public async Task<object> SaveReportAsync(string id, PathReportRequest req)
    {
        _policy.Require("Опис і висновок", PathologistRoles);
        var (o, r) = await LoadAsync(id);
        if (r.IntakeStatus != PathIntake.Accepted) throw new ConflictException("Справу не прийнято");
        var x = await DraftAsync(o, r);
        if (x.SignedAt != null) throw Problem("report-signed", "Висновок підписано й незмінний — оформіть доповнення", 409);
        Apply(x, req);
        if (o.PathologyKind == PathKinds.Cyto && !PathCytology.CategoryAllowed(r.CytoMaterial, x.CytoCategory))
            throw ValidationException.Field("cytoCategory", $"Категорія «{x.CytoCategory}» не застосовується до матеріалу «{PathCytology.Materials.FirstOrDefault(m => m.Code == r.CytoMaterial).Name}» — оберіть іншу");
        if (req.Macro != null) r.Macro = req.Macro;
        SetIhc(x, req.Ihc);
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    /// <summary>Попередній висновок (FR-PAT-043): подія RESULT_PRELIMINARY, не є морфологічною верифікацією.</summary>
    public async Task<object> PreliminaryAsync(string id, PathPreliminaryRequest req)
    {
        _policy.Require("Попередній висновок", PathologistRoles);
        if (string.IsNullOrWhiteSpace(req.Text)) throw ValidationException.Field("text", "Текст попереднього висновку обов'язковий");
        var (o, r) = await LoadAsync(id);
        if (r.Stage == PathStages.Signed) throw Problem("report-signed", "Справу підписано — попередній висновок неактуальний", 409);
        _db.PathReports.Add(new LabPathReport { OrderId = o.Id, Version = 0, Kind = PathReportKinds.Preliminary, ConclusionText = req.Text.Trim(), IsPreliminarySent = true });
        MisReferralService.EmitFor(_db, o, MisEvents.ResultPreliminary, new { o.CaseNo, text = req.Text.Trim(), preliminary = true, label = "Попередній" });
        _audit.Log("PATH_PRELIMINARY", "lab_order", o.Id, null, new { req.Text });
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    private object FinalPayload(LabOrder o, LabPathReport x, IEnumerable<LabPathIhcResult> ihc) => new
    {
        o.CaseNo, version = x.Version, kind = x.Kind, addendumType = x.AddendumType, signedAt = x.SignedAt, signedBy = x.SignedByName, conclusionText = x.ConclusionText,
        adequacy = x.Adequacy, cytoCategory = x.CytoCategory, recommendation = x.Recommendation,
        icd10 = x.Icd10, icdoTopo = x.IcdoTopo, icdoMorph = x.IcdoMorph, grade = x.Grade, pT = x.Pt, pN = x.Pn, lnTotal = x.LnTotal, lnPositive = x.LnPositive,
        margin = x.Margin, lvi = x.Lvi, pni = x.Pni, ihc = ihc.Select(i => new { i.Marker, i.ResultText, i.Score }), changesDiagnosis = x.ChangesDiagnosis, reason = x.Reason
    };

    /// <summary>Підпис КЕП (FR-PAT-044): обов'язкові МКХ-О; справа RELEASED, RESULT_FINAL; неочікувана злоякісність — CRITICAL_RESULT (FR-PAT-047).</summary>
    public async Task<object> SignAsync(string id, PathSignRequest req)
    {
        _policy.Require("Підпис висновку", PathologistRoles);
        var (o, r) = await LoadAsync(id);
        if (r.IntakeStatus != PathIntake.Accepted) throw new ConflictException("Справу не прийнято");
        if (r.Stage == PathStages.Signed) throw Problem("report-signed", "Висновок уже підписано — оформіть доповнення", 409);
        var x = await _db.PathReports.Include(p => p.Ihc).Where(p => p.OrderId == o.Id && p.Kind == PathReportKinds.Main).OrderByDescending(p => p.Version).FirstOrDefaultAsync()
                ?? throw ValidationException.Field("report", "Заповніть опис і висновок");
        var isCyto = o.PathologyKind == PathKinds.Cyto;
        if (isCyto)
        {
            if (string.IsNullOrWhiteSpace(x.Adequacy)) throw Problem("adequacy-required", "Для підпису обов'язкова адекватність матеріалу", 422);
            if (x.Adequacy != "UNSATISFACTORY" && string.IsNullOrWhiteSpace(x.CytoCategory)) throw Problem("category-required", "Для підпису обов'язкова категорія висновку", 422);
            if (r.ScreenAssessment == null && !r.PreparedAt.HasValue) throw new ConflictException("Спершу приготуйте препарати");
        }
        else if (string.IsNullOrWhiteSpace(x.IcdoTopo) || string.IsNullOrWhiteSpace(x.IcdoMorph))
            throw Problem("icdo-required", "Для підпису обов'язкові топографія та морфологія МКХ-О");
        // Підписувати можна лише заповнений висновок без незаповнених заготовок шаблону ({g}, {inv}…)
        if (string.IsNullOrWhiteSpace(x.Micro)) throw Problem("micro-required", isCyto ? "Заповніть опис препарату перед підписом" : "Заповніть мікроскопічний опис (п. 17) перед підписом");
        if (string.IsNullOrWhiteSpace(x.ConclusionText)) throw Problem("conclusion-required", isCyto ? "Заповніть цитологічний висновок перед підписом: введіть його або натисніть «Зібрати текст»" : "Заповніть патогістологічний висновок (п. 18) перед підписом: введіть його або натисніть «Зібрати текст»");
        var openMicro = OpenPlaceholders(x.Micro); var openConclusion = OpenPlaceholders(x.ConclusionText);
        if (openMicro.Count > 0) throw Problem("placeholders-left", PlaceholdersMessage(isCyto ? "Опис препарату" : "Мікроскопічний опис", openMicro));
        if (openConclusion.Count > 0) throw Problem("placeholders-left", PlaceholdersMessage("Висновок", openConclusion));
        var warnings = new List<string>();
        var pendingIhc = (await _db.PathSlides.Where(s => s.OrderId == o.Id && s.IsIhc && s.Status != "DISPOSED").Select(s => s.Marker).ToListAsync()).Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!).Distinct().ToList();
        var noResult = pendingIhc.Where(m => !x.Ihc.Any(i => i.Marker.Equals(m, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(i.ResultText))).ToList();
        if (noResult.Count > 0 && noResult.Count == pendingIhc.Count)
            throw Problem("ihc-results-required", $"Внесіть результати ІГХ ({string.Join(", ", noResult)}) або надішліть попередній висновок у МІС — остаточний висновок без жодного результату підписати не можна");
        if (noResult.Count > 0 && !req.AcknowledgePendingIhc)
            throw Problem("ihc-pending", $"Немає результатів ІГХ: {string.Join(", ", noResult)}. Підтвердьте підпис без них — результат буде оформлено доповненням");
        var signer = await _sign.CheckSignerAsync(req.Signer);
        if (noResult.Count > 0) warnings.Add($"ІГХ без результату: {string.Join(", ", noResult)} — результат буде оформлено доповненням");
        var now = DateTime.UtcNow;
        x.SignedAt = now; x.SignedBy = _current.EmployeeId; x.SignedByName = signer.Name ?? Who(); x.SignatureRef = $"KEP:{signer.CertSerial}:{now:yyyyMMddHHmmss}";
        x.Macro ??= r.Macro;
        r.Stage = PathStages.Signed;
        o.Status = OrderStatuses.Released; o.CompletedAt = now; o.ReleasedAt = now; o.ReleasedById = _current.EmployeeId;
        var caseNum = (o.CaseNo ?? "").Split('-').LastOrDefault() ?? "";
        foreach (var b in await _db.PathBlocks.Where(b => b.OrderId == o.Id && b.Status == "IN_WORK").ToListAsync())
        {
            b.Status = "ARCHIVED";
            b.StorageLocation ??= $"Шафа 2 · полиця {(int.TryParse(caseNum, out var n) ? n % 9 + 1 : 1)} · комірка {caseNum}";
        }
        Log(o, PathStages.Signed, comment: $"КЕП: {x.SignedByName}");
        MisReferralService.EmitFor(_db, o, MisEvents.ResultFinal, FinalPayload(o, x, x.Ihc));
        var clinical = (r.ClinicalDxIcd10 ?? o.Icd10Code ?? "").Trim().ToUpperInvariant();
        if (isCyto && PathCytology.IsHigh(x.CytoCategory))
        {
            MisReferralService.EmitFor(_db, o, MisEvents.CriticalResult, new { o.CaseNo, reason = "cytology-high-grade", text = "Цитологія: " + x.ConclusionText, category = x.CytoCategory, acknowledgeRequired = true });
            warnings.Add("Високий ступінь підозри за категорією висновку — направнику передано критичний результат");
        }
        // злоякісна морфологія при клінічному діагнозі без новоутворення (не C00–C97, D00–D09, D37–D48) — критична знахідка для направника
        if (!isCyto && IsMalignant(x.IcdoMorph) && !IsNeoplasmDiagnosis(clinical))
        {
            var dx = string.IsNullOrWhiteSpace(clinical) ? "не вказано" : clinical;
            MisReferralService.EmitFor(_db, o, MisEvents.CriticalResult, new { o.CaseNo, reason = "unexpected-malignancy", text = "Виявлено злоякісну пухлину, яку не передбачав клінічний діагноз направлення: " + x.ConclusionText, clinicalDiagnosis = r.ClinicalDxIcd10, x.IcdoMorph, acknowledgeRequired = true });
            warnings.Add($"Злоякісна пухлина за клінічного діагнозу без новоутворення (діагноз у направленні: {dx}). Направнику передано критичну знахідку — він має підтвердити, що ознайомився");
        }
        _audit.Log("PATH_SIGN", "lab_order", o.Id, null, new { x.Version, x.IcdoTopo, x.IcdoMorph, signer = signer.Drfo, x.SignatureRef });
        await _db.SaveChangesAsync();
        return new { warnings, @case = await CaseAsync(o.Id) };
    }

    /// <summary>Доповнення до підписаного висновку (FR-PAT-045): нова версія vN з підписом КЕП, v1…vN-1 незмінні; подія ADDENDUM.</summary>
    public async Task<object> AddendumAsync(string id, PathAddendumRequest req)
    {
        _policy.Require("Доповнення до висновку", PathologistRoles);
        var (o, r) = await LoadAsync(id);
        if (r.Stage != PathStages.Signed) throw new ConflictException("Доповнення оформлюється лише до підписаного висновку");
        var type = (req.Type ?? "").ToUpperInvariant();
        if (type is not ("IHC" or "REVIEW" or "CORRECTION" or "CONSULT")) throw ValidationException.Field("type", "Тип доповнення: IHC, REVIEW, CORRECTION або CONSULT");
        if (type == "CONSULT" && string.IsNullOrWhiteSpace(req.ConsultantName)) throw ValidationException.Field("consultantName", "Вкажіть консультанта");
        if (string.IsNullOrWhiteSpace(req.Text)) throw ValidationException.Field("text", "Текст доповнення обов'язковий");
        var addOpen = OpenPlaceholders(req.Text);
        if (addOpen.Count > 0) throw ValidationException.Field("text", PlaceholdersMessage("Текст доповнення", addOpen));
        if (type == "CORRECTION" && string.IsNullOrWhiteSpace(req.Reason)) throw ValidationException.Field("reason", "Виправлення потребує обґрунтування");
        var signer = await _sign.CheckSignerAsync(req.Signer);
        var last = await _db.PathReports.AsNoTracking().Where(p => p.OrderId == o.Id && (p.Kind == PathReportKinds.Main || p.Kind == PathReportKinds.Addendum) && p.SignedAt != null)
            .OrderByDescending(p => p.Version).FirstAsync();
        var now = DateTime.UtcNow;
        var x = new LabPathReport
        {
            OrderId = o.Id, Version = last.Version + 1, Kind = PathReportKinds.Addendum, AddendumType = type, ConsultantName = type == "CONSULT" ? req.ConsultantName!.Trim() : null, ConclusionText = req.Text.Trim(), Reason = req.Reason, ChangesDiagnosis = req.ChangesDiagnosis,
            Icd10 = req.Icd10 ?? last.Icd10, IcdoTopo = req.IcdoTopo ?? last.IcdoTopo, IcdoMorph = req.IcdoMorph ?? last.IcdoMorph, Grade = last.Grade, Pt = last.Pt, Pn = last.Pn,
            LnTotal = last.LnTotal, LnPositive = last.LnPositive, Margin = last.Margin, Lvi = last.Lvi, Pni = last.Pni,
            SignedAt = now, SignedBy = _current.EmployeeId, SignedByName = signer.Name ?? Who(), SignatureRef = $"KEP:{signer.CertSerial}:{now:yyyyMMddHHmmss}"
        };
        _db.PathReports.Add(x);
        SetIhc(x, req.Ihc);
        if (type == "IHC")
            foreach (var s in await _db.PathSlides.Where(s => s.OrderId == o.Id && s.IsIhc && s.Status == "IHC_IN_WORK").ToListAsync())
                if (req.Ihc == null || req.Ihc.Any(i => i.Marker.Equals(s.Marker, StringComparison.OrdinalIgnoreCase))) s.Status = "READY";
        Log(o, PathStages.Signed, comment: $"Доповнення v{x.Version} ({type}){(req.ChangesDiagnosis ? ", змінює діагноз" : "")}");
        var payload = FinalPayload(o, x, x.Ihc);
        MisReferralService.EmitFor(_db, o, MisEvents.Addendum, new { payload, notifyMdk = req.ChangesDiagnosis });
        _audit.Log("PATH_ADDENDUM", "lab_order", o.Id, null, new { x.Version, type, req.ChangesDiagnosis, req.Reason });
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    /// <summary>Повідомлення результату термінової біопсії в операційну (FR-PAT-062): read-back обов'язковий; CRITICAL_RESULT із хвилинами від надходження.</summary>
    public async Task<object> CitoNoticeAsync(string id, PathCitoNoticeRequest req)
    {
        _policy.Require("Повідомлення CITO", PathologistRoles);
        var (o, r) = await LoadAsync(id);
        if (o.PathologyKind != PathKinds.Cito) throw new ConflictException("Повідомлення в операційну — лише для термінової (інтраопераційної) біопсії");
        if (r.IntakeStatus != PathIntake.Accepted) throw new ConflictException("Матеріал ще не прийнято");
        if (string.IsNullOrWhiteSpace(req.Text)) throw ValidationException.Field("text", "Внесіть інтраопераційний висновок");
        if (string.IsNullOrWhiteSpace(req.NotifiedPerson)) throw ValidationException.Field("notifiedPerson", "Вкажіть, кому повідомлено");
        if (!req.ReadBack) throw Problem("read-back-required", "Підтвердьте, що отримувач повторив висновок вголос");
        if (r.CitoNotifiedAt.HasValue && !req.Repeat) throw new ConflictException($"Результат уже повідомлено в операційну ({InLab(r.CitoNotifiedAt.Value):HH:mm}). Для повторного повідомлення підтвердьте його окремо");
        var now = DateTime.UtcNow;
        var minutes = WholeMinutes(r.AcceptedAt ?? o.OrderDatetime, now);
        _db.PathCitoNotices.Add(new LabPathCitoNotice { OrderId = o.Id, Text = req.Text.Trim(), NotifiedPerson = req.NotifiedPerson.Trim(), Method = req.Method, ReadBack = true, MinutesFromReceipt = minutes, NotifiedById = _current.EmployeeId });
        _db.PathReports.Add(new LabPathReport { OrderId = o.Id, Version = 0, Kind = PathReportKinds.Cito, ConclusionText = req.Text.Trim(), SignedAt = now, SignedBy = _current.EmployeeId, SignedByName = Who() });
        r.CitoNotifiedAt ??= now;
        MisReferralService.EmitFor(_db, o, MisEvents.CriticalResult, new { o.CaseNo, cito = true, text = req.Text.Trim(), notifiedPerson = req.NotifiedPerson, method = req.Method, readBack = true, operationId = o.MisOperationId, minutesFromReceipt = minutes });
        _audit.Log("PATH_CITO_NOTICE", "lab_order", o.Id, null, new { req.NotifiedPerson, req.Method, readBack = true, minutesFromReceipt = minutes });
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    // ------------------------------------------------------------------ архів (22.7)
    public async Task<object> ArchiveAsync(string? q, string? status)
    {
        var blocks = await _db.PathBlocks.AsNoTracking().Where(b => b.Status != "IN_WORK").ToListAsync();
        var ids = blocks.Select(b => b.OrderId).Distinct().ToList();
        var orders = await _db.Orders.AsNoTracking().Include(o => o.Patient).Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
        var blockIds = blocks.Select(b => b.Id).ToList();
        var issues = (await _db.PathIssues.AsNoTracking().Where(i => blockIds.Contains(i.BlockId)).ToListAsync()).ToLookup(i => i.BlockId);
        var slides = (await _db.PathSlides.AsNoTracking().Where(s => blockIds.Contains(s.BlockId)).Select(s => s.BlockId).ToListAsync()).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var now = DateTime.UtcNow;
        var rooms = await _db.PathStorageRooms.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);
        var rows = blocks.Select(b =>
        {
            var o = orders[b.OrderId];
            var open = issues[b.Id].Where(i => i.ReturnedAt == null).OrderByDescending(i => i.IssuedAt).FirstOrDefault();
            var lastReturn = issues[b.Id].Where(i => i.ReturnedAt != null).OrderByDescending(i => i.ReturnedAt).FirstOrDefault();
            return new
            {
                b.Id, b.Code, b.Barcode, b.IsExternal, b.Status, statusLabel = b.Status == "ISSUED" ? "Видано" : "В архіві", b.StorageLocation, b.RoomId, roomName = rooms.GetValueOrDefault(b.RoomId ?? ""), slides = slides.GetValueOrDefault(b.Id),
                caseId = o.Id, o.CaseNo, patient = o.Patient?.Caption, kind = o.PathologyKind,
                issue = open == null ? null : new { open.Id, open.Recipient, open.Document, open.Purpose, open.IssuedAt, open.DueBack, open.Scope, open.RecipientUnit, overdue = open.DueBack < now },
                returnedAt = lastReturn?.ReturnedAt
            };
        });
        if (!string.IsNullOrWhiteSpace(status) && status != "all") rows = rows.Where(x => status == "overdue" ? x.issue?.overdue == true : x.Status == status.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(q)) rows = rows.Where(x => (x.CaseNo ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) || (x.patient ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) || x.Barcode == q.Trim() || x.Code == q.Trim());
        return rows.OrderBy(x => x.CaseNo).ThenBy(x => x.Code).ToList();
    }

    /// <summary>Видача блока з архіву (FR-PAT-071): отримувач, документ, мета, строк повернення.</summary>
    public async Task<object> IssueAsync(string blockId, PathIssueRequest req)
    {
        _policy.Require("Видача блоків і скелець", IntakeRoles);
        var scope = string.IsNullOrWhiteSpace(req.Scope) ? "EXTERNAL" : req.Scope.Trim().ToUpperInvariant();
        if (scope is not ("EXTERNAL" or "INTERNAL")) throw ValidationException.Field("scope", "Видача: EXTERNAL або INTERNAL");
        if (scope == "INTERNAL" && string.IsNullOrWhiteSpace(req.RecipientUnit)) throw ValidationException.Field("recipientUnit", "Вкажіть підрозділ закладу");
        if (string.IsNullOrWhiteSpace(req.Recipient)) throw ValidationException.Field("recipient", "Вкажіть отримувача (ПІБ / заклад)");
        if (string.IsNullOrWhiteSpace(req.Purpose)) throw ValidationException.Field("purpose", "Вкажіть мету видачі");
        var b = await _db.PathBlocks.FirstOrDefaultAsync(x => x.Id == blockId || x.Barcode == blockId) ?? throw NotFoundException.For("Блок", blockId);
        if (b.Status == "ISSUED") throw Problem("block-issued", $"Блок {b.Code} уже видано", 409);
        if (b.Status != "ARCHIVED") throw new ConflictException("Видати можна лише блок з архіву (після підпису висновку)");
        b.Status = "ISSUED";
        var issue = new LabPathIssue { BlockId = b.Id, OrderId = b.OrderId, Recipient = req.Recipient.Trim(), Document = req.Document, Purpose = req.Purpose.Trim(), DueBack = req.DueBack, IssuedById = _current.EmployeeId, Scope = scope, RecipientUnit = scope == "INTERNAL" ? req.RecipientUnit!.Trim() : null };
        _db.PathIssues.Add(issue);
        _audit.Log("PATH_BLOCK_ISSUE", "lab_path_block", b.Id, null, new { b.Code, issue.Recipient, issue.Purpose, issue.DueBack, issue.Scope, issue.RecipientUnit, actNo = IssueActNo(issue) });
        await _db.SaveChangesAsync();
        return new { b.Id, b.Code, b.Status, issueId = issue.Id, actNo = IssueActNo(issue), issue.IssuedAt, issue.DueBack };
    }

    /// <summary>Номер акта видачі: похідний від ідентифікатора видачі, тож не потребує окремого поля в базі.</summary>
    public static string IssueActNo(LabPathIssue i) => $"АВ-{i.IssuedAt:yyMMdd}-{i.Id.Replace("-", "")[..Math.Min(6, i.Id.Replace("-", "").Length)].ToUpperInvariant()}";

    /// <summary>Друкований акт видачі блока/препарата (FR-PAT-071): що видано, кому, навіщо, коли повернути, підписи сторін. Друк фіксується в журналі змін.</summary>
    public async Task<byte[]> IssueActPdfAsync(string issueId)
    {
        _policy.Require("Видача блоків і скелець", IntakeRoles);
        var issue = await _db.PathIssues.AsNoTracking().FirstOrDefaultAsync(i => i.Id == issueId) ?? throw NotFoundException.For("Видача", issueId);
        var b = await _db.PathBlocks.AsNoTracking().FirstAsync(x => x.Id == issue.BlockId);
        var (o, _) = await LoadAsync(issue.OrderId);
        var slides = await _db.PathSlides.CountAsync(s => s.BlockId == b.Id && s.Status != "DISPOSED");
        var issuer = issue.IssuedById == null ? null : await _db.Employees.AsNoTracking().Where(e => e.Id == issue.IssuedById).Select(e => e.Caption).FirstOrDefaultAsync();
        var cyto = o.PathologyKind == PathKinds.Cyto;
        var rows = new List<(string, string)>
        {
            ("Справа", o.CaseNo ?? o.OrderNumber), ("Пацієнт", o.Patient?.Caption ?? "—"), (cyto ? "Препарат" : "Блок", $"{b.Code} · {b.Barcode}"), ("Скелець у складі", slides.ToString()),
            ("Вид видачі", issue.Scope == "INTERNAL" ? $"Внутрішня — підрозділ: {issue.RecipientUnit}" : "Зовнішня"), ("Отримувач", issue.Recipient), ("Підстава (документ)", string.IsNullOrWhiteSpace(issue.Document) ? "—" : issue.Document!),
            ("Мета видачі", issue.Purpose), ("Видано", InLab(issue.IssuedAt).ToString("dd.MM.yyyy HH:mm", Uk)), ("Повернути до", issue.DueBack.HasValue ? InLab(issue.DueBack.Value).ToString("dd.MM.yyyy", Uk) : "не визначено"),
            ("Місце зберігання до видачі", string.IsNullOrWhiteSpace(b.StorageLocation) ? "—" : b.StorageLocation!)
        };
        _audit.Log("PATH_BLOCK_ISSUE_ACT", "lab_path_block", b.Id, null, new { b.Code, actNo = IssueActNo(issue) });
        await _db.SaveChangesAsync();
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        return QuestPDF.Fluent.Document.Create(c => c.Page(page =>
        {
            page.Size(QuestPDF.Helpers.PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato"));
            page.Header().Column(h =>
            {
                h.Item().AlignCenter().Text($"АКТ ВИДАЧІ {(cyto ? "ЦИТОЛОГІЧНОГО ПРЕПАРАТА" : "ГІСТОЛОГІЧНОГО БЛОКА")}").Bold().FontSize(14);
                h.Item().AlignCenter().Text($"№ {IssueActNo(issue)} від {InLab(issue.IssuedAt).ToString("dd.MM.yyyy", Uk)}").FontSize(11);
            });
            page.Content().PaddingTop(16).Column(col =>
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(4); cd.RelativeColumn(8); });
                    foreach (var (k, v) in rows)
                    {
                        t.Cell().BorderBottom(0.5f).BorderColor("#9AA8B8").PaddingVertical(5).PaddingRight(6).Text(k).SemiBold();
                        t.Cell().BorderBottom(0.5f).BorderColor("#9AA8B8").PaddingVertical(5).Text(v);
                    }
                });
                col.Item().PaddingTop(16).Text("Отримувач зобов'язується зберігати матеріал належним чином і повернути його у зазначений строк.").FontSize(10);
                col.Item().PaddingTop(36).Row(r =>
                {
                    r.RelativeItem().Column(x => { x.Item().Text($"Видав: {issuer ?? "—"}"); x.Item().PaddingTop(22).LineHorizontal(0.5f); x.Item().Text("підпис").FontSize(8).FontColor("#6B7785"); });
                    r.ConstantItem(30);
                    r.RelativeItem().Column(x => { x.Item().Text($"Отримав: {issue.Recipient}"); x.Item().PaddingTop(22).LineHorizontal(0.5f); x.Item().Text("підпис").FontSize(8).FontColor("#6B7785"); });
                });
            });
            page.Footer().AlignRight().Text("MedLink ЛІС · Патоморфологія · архів");
        })).GeneratePdf();
    }

    public async Task<object> ReturnAsync(string blockId)
    {
        _policy.Require("Повернення блоків і скелець", IntakeRoles);
        var b = await _db.PathBlocks.FirstOrDefaultAsync(x => x.Id == blockId || x.Barcode == blockId) ?? throw NotFoundException.For("Блок", blockId);
        if (b.Status != "ISSUED") throw new ConflictException($"Блок {b.Code} не видавався");
        var issue = await _db.PathIssues.Where(i => i.BlockId == b.Id && i.ReturnedAt == null).OrderByDescending(i => i.IssuedAt).FirstOrDefaultAsync();
        if (issue != null) issue.ReturnedAt = DateTime.UtcNow;
        b.Status = "ARCHIVED";
        _audit.Log("PATH_BLOCK_RETURN", "lab_path_block", b.Id, null, new { b.Code });
        await _db.SaveChangesAsync();
        return new { b.Id, b.Code, b.Status, returnedAt = issue?.ReturnedAt };
    }

    /// <summary>Утилізація скельця (FR-PAT-118): причина обов'язкова; за потреби створюється нове скельце тієї ж методики.</summary>
    public async Task<object> DisposeSlideAsync(string slideId, PathDisposeRequest req)
    {
        _policy.Require("Утилізація скелець", HistotechRoles);
        if (string.IsNullOrWhiteSpace(req.Reason)) throw ValidationException.Field("reason", "Вкажіть причину утилізації");
        var sl = await _db.PathSlides.FirstOrDefaultAsync(x => x.Id == slideId || x.Barcode == slideId) ?? throw NotFoundException.For("Скельце", slideId);
        if (sl.Status == "DISPOSED") throw new ConflictException($"Скельце {sl.Code} уже утилізовано");
        var (o, _) = await LoadAsync(sl.OrderId);
        sl.Status = "DISPOSED"; sl.DisposedAt = DateTime.UtcNow; sl.DisposeReason = req.Reason.Trim();
        object? created = null;
        if (req.Recreate)
        {
            var count = await _db.PathSlides.CountAsync(x => x.BlockId == sl.BlockId);
            var n = new LabPathSlide
            {
                BlockId = sl.BlockId, OrderId = sl.OrderId, Code = $"{(sl.Code.Contains('-') ? sl.Code[..sl.Code.LastIndexOf('-')] : sl.Code)}-{count + 1}", StainMethod = sl.StainMethod, IsIhc = sl.IsIhc, Marker = sl.Marker,
                Status = sl.IsIhc ? "IHC_IN_WORK" : "IN_WORK", ReplacesSlideId = sl.Id, Barcode = $"PS{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}"
            };
            _db.PathSlides.Add(n);
            created = new { n.Id, n.Code, n.Barcode };
        }
        Log(o, PathStages.Staining, sl.BlockId, sl.Id, null, $"Утилізовано скельце {sl.Code}: {sl.DisposeReason}");
        _audit.Log("PATH_SLIDE_DISPOSE", "lab_path_slide", sl.Id, null, new { sl.Code, sl.DisposeReason, req.Recreate });
        await _db.SaveChangesAsync();
        return new { sl.Id, sl.Code, sl.Status, sl.DisposedAt, created };
    }

    /// <summary>Місце зберігання блока: приміщення + полиця/комірка (FR-PAT-118).</summary>
    public async Task<object> SetStorageAsync(string blockId, PathStorageRequest req)
    {
        _policy.Require("Видача блоків і скелець", IntakeRoles);
        var b = await _db.PathBlocks.FirstOrDefaultAsync(x => x.Id == blockId || x.Barcode == blockId) ?? throw NotFoundException.For("Блок", blockId);
        if (!string.IsNullOrWhiteSpace(req.RoomId) && !await _db.PathStorageRooms.AnyAsync(x => x.Id == req.RoomId && x.IsActive)) throw ValidationException.Field("roomId", "Приміщення не знайдено");
        b.RoomId = string.IsNullOrWhiteSpace(req.RoomId) ? null : req.RoomId;
        b.StorageLocation = Clean(req.Location, null);
        _audit.Log("PATH_BLOCK_STORAGE", "lab_path_block", b.Id, null, new { b.Code, b.RoomId, b.StorageLocation });
        await _db.SaveChangesAsync();
        return new { b.Id, b.Code, b.RoomId, b.StorageLocation };
    }

    /// <summary>Типові приміщення архіву, якщо довідник порожній (існуючі бази після оновлення).</summary>
    private async Task EnsureRoomsAsync()
    {
        if (await _db.PathStorageRooms.AnyAsync()) return;
        _db.PathStorageRooms.AddRange(new LabPathStorageRoom { Code = "ARC-1", Name = "Архів блоків" }, new LabPathStorageRoom { Code = "ARC-2", Name = "Архів скелець" });
        await _db.SaveChangesAsync();
    }

    public async Task<object> RoomsAsync()
    {
        await EnsureRoomsAsync();
        return await _db.PathStorageRooms.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync();
    }

    public async Task<object> AddRoomAsync(PathRoomRequest req)
    {
        _policy.Require("Довідник приміщень архіву", new[] { LabRoles.Admin });
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) throw ValidationException.Field("name", "Код і назва обов'язкові");
        var code = req.Code.Trim();
        if (await _db.PathStorageRooms.AnyAsync(x => x.Code == code)) throw new ConflictException($"Приміщення з кодом {code} уже існує");
        var x = new LabPathStorageRoom { Code = code, Name = req.Name.Trim() };
        _db.PathStorageRooms.Add(x);
        await _db.SaveChangesAsync();
        return new { x.Id, x.Code, x.Name };
    }

    /// <summary>Відправка підписаного висновку на e-mail (FR-PAT-119): запис у чергу розсилки; лише після підпису.</summary>
    public async Task<object> DispatchAsync(string id, PathDispatchRequest req)
    {
        _policy.Require("Відправка висновку", PathologistRoles.Concat(IntakeRoles).ToArray());
        var (o, r) = await LoadAsync(id);
        if (r.Stage != PathStages.Signed) throw new ConflictException("Відправка можлива лише для підписаного висновку");
        var address = (string.IsNullOrWhiteSpace(req.Address) ? r.ResultEmail : req.Address)?.Trim();
        if (string.IsNullOrWhiteSpace(address) || !System.Text.RegularExpressions.Regex.IsMatch(address, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw ValidationException.Field("address", "Вкажіть коректну адресу електронної пошти");
        var last = await _db.PathReports.AsNoTracking().Where(p => p.OrderId == o.Id && p.SignedAt != null && (p.Kind == PathReportKinds.Main || p.Kind == PathReportKinds.Addendum))
            .OrderByDescending(p => p.Version).FirstAsync();
        var d = new LabPathDispatch { OrderId = o.Id, ReportId = last.Id, Address = address, RequestedById = _current.EmployeeId };
        _db.PathDispatches.Add(d);
        Log(o, PathStages.Signed, comment: $"Висновок v{last.Version} поставлено в розсилку: {address}");
        _audit.Log("PATH_DISPATCH", "lab_order", o.Id, null, new { last.Version, address });
        await _db.SaveChangesAsync();
        return new { d.Id, d.Address, d.Status, version = last.Version };
    }

    public async Task<object> DispatchesAsync(string id)
    {
        var (o, _) = await LoadAsync(id);
        return await _db.PathDispatches.AsNoTracking().Where(x => x.OrderId == o.Id).OrderByDescending(x => x.CreatedOn).Select(x => new { x.Id, x.Address, x.Status, x.Error, x.CreatedOn }).ToListAsync();
    }

    // ------------------------------------------------------------------ цитологія (22.19)
    /// <summary>Методики цитології, якщо їх немає в довіднику (бази, створені до появи цитології).</summary>
    private async Task EnsureCytoMethodsAsync()
    {
        if (!await _db.StainMethods.AnyAsync(m => m.Code == "PAP")) _db.StainMethods.Add(new LabStainMethod { Code = "PAP", Name = "Папаніколау", Price = PathPrices.CytoSlide });
        if (!await _db.StainMethods.AnyAsync(m => m.Code == "MGG")) _db.StainMethods.Add(new LabStainMethod { Code = "MGG", Name = "Май-Грюнвальд-Гімза", Price = PathPrices.CytoSlide });
        await _db.SaveChangesAsync();
    }

    private static string CytoSlideCode(string? caseNo, int n)
    {
        return $"{caseNo}-{n}"; // ЦИТ-26-0001-1: той самий префікс, що й у номера справи
    }

    /// <summary>Приготування препаратів (FR-CYT-003): скельця за методиками, вартість, справа переходить до фарбування.</summary>
    public async Task<object> PrepareAsync(string id, PathPrepareRequest req)
    {
        _policy.Require("Приготування цитологічних препаратів", HistotechRoles);
        var (o, r) = await LoadAsync(id);
        if (o.PathologyKind != PathKinds.Cyto) throw new ConflictException("Приготування препаратів — лише для цитологічних справ");
        if (r.IntakeStatus != PathIntake.Accepted) throw new ConflictException("Справу не прийнято");
        if (r.PreparedAt.HasValue) throw new ConflictException("Препарати вже приготовано; повторне приготування — окремою дією");
        await EnsureCytoMethodsAsync();
        var methods = await _db.StainMethods.AsNoTracking().ToListAsync();
        var objects = await _db.PathObjects.Where(x => x.OrderId == o.Id).OrderBy(x => x.DisplayOrder).ToListAsync();
        if (req.Objects.Count == 0 || req.Objects.All(x => x.Slides.Count == 0)) throw ValidationException.Field("objects", "Вкажіть хоча б одне скельце");
        var n = 0; var cytoPrice = 0m;
        foreach (var g in req.Objects)
        {
            var obj = objects.FirstOrDefault(x => x.Id == g.ObjectId || x.Mark == g.ObjectId) ?? throw NotFoundException.For("Контейнер", g.ObjectId);
            if (g.Slides.Count > 10) throw ValidationException.Field("slides", "На контейнер — не більше 10 скелець");
            if (g.Slides.Count == 0) continue;
            var block = new LabPathBlock { ObjectId = obj.Id, OrderId = o.Id, Code = $"{obj.Letter}1", Pieces = 0, Barcode = $"PB{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}" };
            _db.PathBlocks.Add(block);
            foreach (var sl in g.Slides)
            {
                var code = string.IsNullOrWhiteSpace(sl.Method) ? "PAP" : sl.Method.Trim();
                var m = methods.FirstOrDefault(d => d.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) ?? throw ValidationException.Field("method", $"Методику «{code}» не знайдено в довіднику");
                cytoPrice += m.Price > 0 ? m.Price : PathPrices.CytoSlide;
                _db.PathSlides.Add(new LabPathSlide
                {
                    BlockId = block.Id, OrderId = o.Id, Code = CytoSlideCode(o.CaseNo, ++n), StainMethod = m.Name, IsIhc = false, Status = "IN_WORK",
                    Barcode = $"PS{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}"
                });
            }
        }
        r.PreparedAt = DateTime.UtcNow; r.Price = cytoPrice;
        Log(o, PathStages.Gross, comment: $"Приготовано скелець: {n}{(string.IsNullOrWhiteSpace(req.Note) ? "" : "; " + req.Note.Trim())}");
        SetStage(o, r, PathStages.Staining);
        _audit.Log("CYTO_PREPARE", "lab_order", o.Id, null, new { slides = n });
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    /// <summary>Повторне приготування (FR-CYT-003): додаткове скельце з обов'язковою причиною.</summary>
    /// <summary>Довідник методик: ціна скельця (у т. ч. цитології) задається адміністратором, 0 — типова ціна.</summary>
    public async Task<object> SetMethodPriceAsync(string code, PathMethodPriceRequest req)
    {
        _policy.Require("Довідник методик фарбування", new[] { LabRoles.Admin });
        if (req.Price < 0) throw ValidationException.Field("price", "Ціна не може бути від'ємною");
        var m = await _db.StainMethods.FirstOrDefaultAsync(d => d.Code == code) ?? throw NotFoundException.For("Методика", code);
        m.Price = req.Price;
        _audit.Log("STAIN_PRICE", "lab_stain_method", m.Id, null, new { code, req.Price });
        await _db.SaveChangesAsync();
        return new { m.Code, m.Name, m.IsIhc, m.Price };
    }

    public async Task<object> RepeatPrepAsync(string id, PathRepeatRequest req)
    {
        _policy.Require("Приготування цитологічних препаратів", HistotechRoles);
        if (string.IsNullOrWhiteSpace(req.Reason)) throw ValidationException.Field("reason", "Вкажіть причину повторного приготування");
        var (o, r) = await LoadAsync(id);
        if (o.PathologyKind != PathKinds.Cyto || !r.PreparedAt.HasValue) throw new ConflictException("Повторне приготування можливе після первинного");
        if (r.Stage == PathStages.Signed) throw Problem("report-signed", "Справу підписано", 409);
        await EnsureCytoMethodsAsync();
        var code = string.IsNullOrWhiteSpace(req.Method) ? "PAP" : req.Method.Trim();
        var m = await _db.StainMethods.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code) ?? throw ValidationException.Field("method", $"Методику «{code}» не знайдено");
        var block = await _db.PathBlocks.Where(b => b.OrderId == o.Id).OrderBy(b => b.Code).FirstAsync();
        var count = await _db.PathSlides.CountAsync(s => s.OrderId == o.Id);
        var sl = new LabPathSlide { BlockId = block.Id, OrderId = o.Id, Code = CytoSlideCode(o.CaseNo, count + 1), StainMethod = m.Name, Status = "IN_WORK", Barcode = $"PS{await _numerators.NextCounterAsync("PATH_BARCODE"):D8}" };
        _db.PathSlides.Add(sl);
        r.Price += m.Price > 0 ? m.Price : PathPrices.CytoSlide;
        Log(o, PathStages.Gross, block.Id, sl.Id, null, $"Повторне приготування: {req.Reason.Trim()}");
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    /// <summary>Скринінг (FR-CYT-005): попередня оцінка лаборанта-скринера; остаточний висновок виносить лікар.</summary>
    public async Task<object> ScreeningAsync(string id, PathScreeningRequest req)
    {
        _policy.Require("Скринінг цитологічних препаратів", HistotechRoles.Concat(PathologistRoles).ToArray());
        var a = (req.Assessment ?? "").Trim().ToUpperInvariant();
        if (!PathCytology.Assessments.Contains(a)) throw ValidationException.Field("assessment", "Оцінка: NORMAL або SUSPECT");
        var (o, r) = await LoadAsync(id);
        if (o.PathologyKind != PathKinds.Cyto) throw new ConflictException("Скринінг — лише для цитологічних справ");
        if (r.Stage != PathStages.Microscopy) throw new ConflictException($"Скринінг можливий на етапі «{PathStages.LabelFor(PathKinds.Cyto, PathStages.Microscopy)}» (зараз «{PathStages.LabelFor(PathKinds.Cyto, r.Stage)}»)");
        r.ScreenAssessment = a; r.ScreenNote = Clean(req.Note, null); r.ScreenerId = _current.EmployeeId; r.ScreenerName = Who(); r.ScreenedAt = DateTime.UtcNow;
        Log(o, PathStages.Microscopy, comment: $"Скринінг: {(a == "NORMAL" ? "норма" : "підозра")}{(r.ScreenNote == null ? "" : " — " + r.ScreenNote)}");
        _audit.Log("CYTO_SCREEN", "lab_order", o.Id, null, new { a });
        await _db.SaveChangesAsync();
        return await CaseAsync(o.Id);
    }

    /// <summary>Аналітика цитології (FR-CYT-011): непридатні, розбіжність скринер/лікар, TAT, навантаження.</summary>
    public async Task<object> CytoAnalyticsAsync()
    {
        var refs = await _db.PathReferrals.AsNoTracking().Include(r => r.Order).Where(r => r.Order!.PathologyKind == PathKinds.Cyto && r.IntakeStatus == PathIntake.Accepted).ToListAsync();
        var ids = refs.Select(r => r.OrderId).ToList();
        var reports = (await _db.PathReports.AsNoTracking().Where(p => ids.Contains(p.OrderId) && p.Kind == PathReportKinds.Main && p.SignedAt != null).ToListAsync()).ToDictionary(p => p.OrderId);
        var signed = refs.Where(r => reports.ContainsKey(r.OrderId)).ToList();
        int Mismatch(LabPathReferral r) { var c = reports[r.OrderId].CytoCategory; return r.ScreenAssessment == null || c == null ? 0 : (r.ScreenAssessment == "NORMAL" && !PathCytology.IsNormal(c)) || (r.ScreenAssessment == "SUSPECT" && PathCytology.IsNormal(c)) ? 1 : 0; }
        var screened = signed.Where(r => r.ScreenAssessment != null && reports[r.OrderId].CytoCategory != null).ToList();
        var tat = signed.Where(r => r.AcceptedAt.HasValue).Select(r => (reports[r.OrderId].SignedAt!.Value - r.AcceptedAt!.Value).TotalHours).OrderBy(x => x).ToList();
        return new
        {
            total = refs.Count, signed = signed.Count, inWork = refs.Count - signed.Count,
            unsatisfactory = signed.Count(r => reports[r.OrderId].Adequacy == "UNSATISFACTORY"),
            unsatisfactoryShare = signed.Count == 0 ? 0 : Math.Round(100.0 * signed.Count(r => reports[r.OrderId].Adequacy == "UNSATISFACTORY") / signed.Count, 1),
            screened = screened.Count, mismatches = screened.Sum(Mismatch),
            mismatchShare = screened.Count == 0 ? 0 : Math.Round(100.0 * screened.Sum(Mismatch) / screened.Count, 1),
            tatMedianHours = tat.Count == 0 ? (double?)null : Math.Round(tat[tat.Count / 2], 1),
            byCategory = signed.GroupBy(r => reports[r.OrderId].CytoCategory ?? "—").Select(g => new { category = g.Key, count = g.Count() }).OrderByDescending(x => x.count),
            byScreener = refs.Where(r => r.ScreenerName != null).GroupBy(r => r.ScreenerName!).Select(g => new { name = g.Key, count = g.Count() }),
            byPathologist = signed.GroupBy(r => reports[r.OrderId].SignedByName ?? "—").Select(g => new { name = g.Key, count = g.Count() })
        };
    }

    // ------------------------------------------------------------------ довідники (22.10)
    public async Task<object> DictionariesAsync()
    {
        await EnsureRoomsAsync();
        await EnsureCytoMethodsAsync();
        return new
    {
        templates = await _db.PathTemplates.AsNoTracking().OrderBy(t => t.Name).Select(t => new { t.Id, t.Name, t.Macro, t.Micro }).ToListAsync(),
        topo = await _db.Icdo.AsNoTracking().Where(i => i.Axis == "TOPO").OrderBy(i => i.Code).Select(i => new { i.Code, i.Name }).ToListAsync(),
        morph = await _db.Icdo.AsNoTracking().Where(i => i.Axis == "MORPH").OrderBy(i => i.Code).Select(i => new { i.Code, i.Name }).ToListAsync(),
        stains = await _db.StainMethods.AsNoTracking().OrderBy(s => s.IsIhc).ThenBy(s => s.Code).Select(s => new { s.Code, s.Name, s.IsIhc, s.Price }).ToListAsync(),
        reflex = await _db.IhcReflexRules.AsNoTracking().Select(r => new { r.TopoFrom, r.TopoTo, r.MorphCodes, r.Panel, r.Why }).ToListAsync(),
        materialTypes = await _db.PathMaterialTypes.AsNoTracking().Select(t => new { t.Code, t.Name, t.SlaDays, t.ServiceGroup, t.BasePrice }).ToListAsync(),
        fixatives = await _db.Fixatives.AsNoTracking().Select(f => new { f.Name, f.MinHours, f.MaxHours }).ToListAsync(),
        rejectReasons = await _db.PathRejectReasons.AsNoTracking().Select(r => new { r.Code, r.Name }).ToListAsync(),
        rooms = await _db.PathStorageRooms.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(),
        tracks = PathTracks.All.Select(t => new { code = t, name = PathTracks.Label(t) }),
        maxMethodsPerBlock = PathLimits.MaxMethodsPerBlock,
        // стартовий набір кодів МКХ-10 для швидкого вибору (не повний довідник; інший код вводиться вручну)
        icd10 = PathIcd10Starter.Items.Select(i => new { code = i.Code, name = i.Name }),
        cytology = new
        {
            materials = PathCytology.Materials.Select(m => new { code = m.Code, name = m.Name }),
            adequacy = PathCytology.Adequacy.Select(a => new { code = a.Code, name = a.Name }),
            categories = PathCytology.Categories.Select(c => new { code = c.Code, name = c.Name, high = c.High, materials = PathCytology.Materials.Where(m => PathCytology.CategoryAllowed(m.Code, c.Code)).Select(m => m.Code) }),
            slidePrice = PathPrices.CytoSlide // типова; фактична — з довідника методик (stains[].price)
        },
        prices = new { firstBlock = PathPrices.FirstBlock, nextBlock = PathPrices.NextBlock, ihc = PathPrices.Ihc, consultSlide = PathPrices.ConsultSlide, cito = PathPrices.Cito, ihcExtraDays = PathPrices.IhcExtraWorkdays }
    };
    }

    // ------------------------------------------------------------------ друк 014/о (22.8)
    /// <summary>Поля форми 014/о (FR-PAT-081): сторона 1 — з МІС, сторона 2 — з ЛІС; до підпису п. 18 і підписи порожні.</summary>
    public async Task<Form014> Form014Async(string id)
    {
        var (o, r) = await LoadAsync(id);
        var objects = await _db.PathObjects.AsNoTracking().Include(x => x.Blocks).ThenInclude(b => b.Slides).Where(x => x.OrderId == o.Id).OrderBy(x => x.DisplayOrder).ToListAsync();
        var reports = await _db.PathReports.AsNoTracking().Where(x => x.OrderId == o.Id && x.SignedAt != null && (x.Kind == PathReportKinds.Main || x.Kind == PathReportKinds.Addendum)).OrderBy(x => x.Version).ToListAsync();
        var blocks = objects.SelectMany(x => x.Blocks).ToList();
        var slides = blocks.SelectMany(b => b.Slides).ToList();
        var lastLab = await _db.PathStageLog.AsNoTracking().Where(l => l.OrderId == o.Id && (l.Stage == PathStages.Staining || l.Stage == PathStages.Sectioning)).OrderByDescending(l => l.PerformedAt).Select(l => l.PerformedByName).FirstOrDefaultAsync();
        var main = reports.FirstOrDefault(x => x.Kind == PathReportKinds.Main);
        var draft = main ?? await _db.PathReports.AsNoTracking().Where(x => x.OrderId == o.Id && x.Kind == PathReportKinds.Main).FirstOrDefaultAsync();
        var signed = main != null;
        string D(DateTime? d) => d.HasValue ? InLab(d.Value).ToString("dd.MM.yyyy", Uk) : "";
        string Dt(DateTime? d) => d.HasValue ? InLab(d.Value).ToString("dd.MM.yyyy HH:mm", Uk) : "";
        string Join(string sep, params string?[] parts) => string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        var lastSigned = reports.LastOrDefault();
        return new Form014
        {
            Signed = signed,
            Side1 = new()
            {
                ["1. Заклад, що направляє"] = string.Join(", ", new[] { Snapshot(r, "facility"), r.ReferringDepartment ?? o.RequestingDepartmentName }.Where(x => !string.IsNullOrWhiteSpace(x))),
                ["2. Дата і час направлення"] = Dt(o.OrderDatetime),
                ["3. Пацієнт (ПІБ, стать, дата народження)"] = $"{o.Patient?.Caption}, {(o.Patient?.Gender == "F" ? "жін." : o.Patient?.Gender == "M" ? "чол." : "")}, {D(o.Patient?.Birthday)}",
                ["4–5. Біопсія первинна / вторинна"] = r.BiopsyKind == "SECONDARY" ? $"вторинна{(r.PrimaryCaseNo != null ? $" (первинна: {r.PrimaryCaseNo})" : "")}" : "первинна",
                ["6. Дата й вид операції / маніпуляції"] = r.OperationText ?? (r.ExternalInstitution != null ? $"Консультація препаратів {r.ExternalInstitution} №{r.ExternalNumber} від {D(r.ExternalDate)}" : ""),
                ["7. Маркування матеріалу (кількість об'єктів)"] = r.MarkingText ?? string.Join("; ", objects.Select(x => $"№{x.Mark} — {x.Description}")),
                ["8. Клінічні дані"] = r.ClinicalData ?? "",
                ["9. Клінічний діагноз; лікар, що направив"] = Join("; ", r.ClinicalDxIcd10, r.ReferringDoctor)
            },
            Side2 = new()
            {
                ["10. № справи"] = o.CaseNo ?? "",
                ["11. Дата й час надходження"] = Dt(r.AcceptedAt),
                ["12–14. Тип матеріалу"] = PathKinds.Label(o.PathologyKind),
                ["15. Кількість шматочків / блоків"] = $"{blocks.Sum(b => b.Pieces)} / {blocks.Count}",
                ["16. Методики забарвлення"] = string.Join(", ", slides.Select(s => s.StainMethod).Distinct()),
                ["17. Макро- і мікроскопічний опис"] = Join("\n", string.IsNullOrWhiteSpace(r.Macro) ? null : "Макроопис: " + r.Macro.Trim(), string.IsNullOrWhiteSpace(draft?.Micro) ? null : "Мікроопис: " + draft!.Micro!.Trim()),
                ["18. Патогістологічний висновок"] = signed
                    ? Join("\n", string.Join("\n", reports.Select(x => (x.Kind == PathReportKinds.Addendum ? $"Доповнення v{x.Version} від {D(x.SignedAt)}: " : "") + x.ConclusionText)),
                        Join("; ", string.IsNullOrWhiteSpace(lastSigned!.Icd10) ? null : $"МКХ-10: {lastSigned.Icd10}", Join(", ", lastSigned.IcdoTopo, lastSigned.IcdoMorph) is { Length: > 0 } icdo ? $"МКХ-О: {icdo}" : null))
                    : "",
                ["Дата дослідження"] = signed ? D(main!.SignedAt) : "",
                ["Лікар-патологоанатом"] = signed ? $"{main!.SignedByName} (КЕП)" : "",
                ["Лаборант"] = signed ? lastLab ?? "" : ""
            }
        };
    }

    private static string? Snapshot(LabPathReferral r, string key)
    {
        if (string.IsNullOrWhiteSpace(r.Form014Json)) return null;
        try { using var d = JsonDocument.Parse(r.Form014Json); return d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null; }
        catch (JsonException) { return null; }
    }

    public async Task<byte[]> Form014PdfAsync(string id, string? sides)
    {
        var f = await Form014Async(id);
        var want = (sides ?? "1,2").Split(',').Select(s => s.Trim()).ToHashSet();
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        return QuestPDF.Fluent.Document.Create(c =>
        {
            void Side(string title, Dictionary<string, string> fields) => c.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Lato"));
                page.Header().Column(h =>
                {
                    h.Item().AlignRight().Text("Медична документація · Форма № 014/о · затверджено наказом МОЗ України 29.05.2013 № 435").FontSize(7.5f);
                    h.Item().AlignCenter().Text("НАПРАВЛЕННЯ НА ПАТОГІСТОЛОГІЧНЕ ДОСЛІДЖЕННЯ БІОПСІЙНОГО (ОПЕРАЦІЙНОГО) МАТЕРІАЛУ").Bold().FontSize(11);
                    h.Item().AlignCenter().Text(title).FontSize(9).FontColor("#4274A7");
                });
                page.Content().PaddingTop(10).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(9); });
                    foreach (var (k, v) in fields)
                    {
                        t.Cell().BorderBottom(0.5f).BorderColor("#9AA8B8").PaddingVertical(4).PaddingRight(6).Text(k).SemiBold();
                        t.Cell().BorderBottom(0.5f).BorderColor("#9AA8B8").PaddingVertical(4).Text(string.IsNullOrWhiteSpace(v) ? " " : v);
                    }
                });
                page.Footer().AlignRight().Text(x => { x.Span("MedLink ЛІС · Патоморфологія · сторінка "); x.CurrentPageNumber(); });
            });
            if (want.Contains("1")) Side("Сторона 1 (заповнює заклад, що направляє; дані з МІС)", f.Side1);
            if (want.Contains("2")) Side("Сторона 2 (заповнює патологоанатомічне відділення)", f.Side2);
        }).GeneratePdf();
    }
}

public sealed class Form014
{
    public bool Signed { get; set; }
    public Dictionary<string, string> Side1 { get; set; } = new();
    public Dictionary<string, string> Side2 { get; set; } = new();
}
