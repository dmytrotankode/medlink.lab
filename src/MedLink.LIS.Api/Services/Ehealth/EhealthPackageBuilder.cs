// =============================================================================
// Будівник пакета Submit Diagnostic Report Package: LabOrder → {diagnostic_report, observations[]} за схемою OpenAPI eHealth
// (Submit_DR_Package_DecodedPayload) і ТВ МІС 3.19/3.21. Ідентифікатори DR та спостережень — UUID v1, створюються при побудові
// й після цього НЕ змінюються (ідемпотентність повтору). Канонічний JSON — UTF-8 без BOM (ТВ 1.3.3).
// Що ще НЕ мапиться (етап 3 плану, потребує довідників preprod): specimens, used_references (equipment), components, results_interpreter.
// =============================================================================
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services.Ehealth;

/// <summary>Ідентифікатори закладу/підрозділу/працівника в ЕСОЗ. У Mock/Replay генеруються детерміновано; у Live беруться з конфігурації.</summary>
public sealed class EhealthIdentity
{
    private readonly EhealthOptions _o;
    public EhealthIdentity(EhealthOptions o) => _o = o;

    public string? LegalEntityId(string? organizationId) =>
        !string.IsNullOrWhiteSpace(_o.LegalEntityId) ? _o.LegalEntityId : _o.IsLive ? null : DeterministicGuid.For("ehealth-legal-entity:" + (organizationId ?? MedLinkDefaults.OrganizationId));

    public string? DivisionId() => string.IsNullOrWhiteSpace(_o.DivisionId) ? null : _o.DivisionId;

    public string? EmployeeId(string? localEmployeeId) =>
        !string.IsNullOrWhiteSpace(_o.DefaultEmployeeId) ? _o.DefaultEmployeeId : _o.IsLive ? null : DeterministicGuid.For("ehealth-employee:" + (localEmployeeId ?? MedLinkEnums.EmptyGuid));
}

public sealed class EhealthPackage
{
    /// <summary>Канонічний JSON пакета підпису (компактний, UTF-8 без BOM).</summary>
    public string Json { get; init; } = "";
    public string Hash { get; init; } = "";
    public string ReportId { get; init; } = "";
    public string PatientId { get; init; } = "";
    public string? ServiceRequestId { get; init; }
    public List<string> ObservationIds { get; init; } = new();
    public byte[] Bytes => new UTF8Encoding(false).GetBytes(Json);
}

public sealed class EhealthPackageBuilder
{
    private readonly LisDbContext _db;
    private readonly EhealthOptions _options;
    private readonly EhealthIdentity _identity;

    public EhealthPackageBuilder(LisDbContext db, EhealthOptions options, EhealthIdentity identity) { _db = db; _options = options; _identity = identity; }

    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Маршрутизація пакета: id пацієнта в ЕСОЗ (шлях виклику), id направлення в ЕСОЗ і id рядка дзеркала.</summary>
    public async Task<(string PatientId, string? ServiceRequestId, string? ReferralId)> RoutingAsync(LabOrder o)
    {
        var referral = o.EhealthReferralId == null ? null : await _db.Referrals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == o.EhealthReferralId);
        return (referral?.EhealthPatientId ?? referral?.PatientCardId ?? o.PatientId, referral == null ? null : referral.EhealthId ?? referral.Id, referral?.Id);
    }

    /// <summary>Будує пакет відправки. Помилки готовності повертає списком (порожньо — можна підписувати).</summary>
    public async Task<(EhealthPackage? Package, List<string> Problems)> BuildSubmitAsync(LabOrder o, string signerEmployeeId)
    {
        var problems = new List<string>();
        var report = o.DiagnosticReportId == null ? null : await _db.DiagnosticReports.FirstOrDefaultAsync(r => r.Id == o.DiagnosticReportId);
        if (report == null) { problems.Add("немає медичного висновку (mis_diagnostic_report)"); return (null, problems); }
        var referral = o.EhealthReferralId == null ? null : await _db.Referrals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == o.EhealthReferralId);
        var paper = o.PaperReferralId == null ? null : await _db.PaperReferrals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == o.PaperReferralId);

        var legalEntity = _identity.LegalEntityId(o.OrganizationId);
        var employee = _identity.EmployeeId(signerEmployeeId);
        if (legalEntity == null) problems.Add("не налаштовано ідентифікатор закладу в ЕСОЗ (Ehealth:LegalEntityId)");
        if (employee == null) problems.Add("не налаштовано ідентифікатор працівника в ЕСОЗ (Ehealth:DefaultEmployeeId)");

        // послуга каталогу: id у ЕСОЗ (ehe_service_catalog_service.ehealth_id), інакше локальний id
        var service = await _db.ServiceCatalog.AsNoTracking().FirstOrDefaultAsync(s => s.Id == report.EhealthServiceCatalogServiceId);
        var serviceId = service == null ? null : service.EhealthId ?? service.Id;
        if (serviceId == null || report.EhealthServiceCatalogServiceId == MedLinkEnums.EmptyGuid) problems.Add("послуга не знайдена в каталозі eHealth (code діагностичного звіту)");

        var patientId = referral?.EhealthPatientId ?? referral?.PatientCardId ?? o.PatientId;
        // UUID v1 створюється при побудові пакета; підписаний пакет більше не перебудовується, тому id фіксується підписом
        var drId = Uuid1.NewString();
        var issued = Iso(report.IssuedAt ?? o.ReleasedAt ?? DateTime.UtcNow);

        var tests = o.Tests.Where(t => OrderTestStatuses.VerifiedAny.Contains(t.Status) || t.ReleasedAt != null).OrderBy(t => t.DisplayOrder).ToList();
        if (tests.Count == 0) problems.Add("немає верифікованих результатів для передачі");

        var observations = new JsonArray();
        var obsIds = new List<string>();
        foreach (var t in tests)
        {
            var res = t.Result;
            var def = t.Test ?? await _db.Tests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == t.TestId);
            // Live: код LOINC обов'язковий. Mock/Replay: тестовий код-замінник, щоб демо-замовлення без LOINC проходили повний цикл
            var loinc = string.IsNullOrWhiteSpace(def?.LoincCode) ? (_options.Simulated ? "MOCK-" + t.TestCode : null) : def!.LoincCode;
            if (loinc == null) { problems.Add($"показник {t.TestCode} не має коду LOINC"); continue; }
            if (res == null || (res.NumericValue == null && string.IsNullOrWhiteSpace(res.StringValue))) { problems.Add($"показник {t.TestCode} без значення"); continue; }
            var obsId = Uuid1.NewString();
            obsIds.Add(obsId);
            var unit = string.IsNullOrWhiteSpace(res.Unit) ? def!.Unit : res.Unit;
            var ob = new JsonObject
            {
                ["id"] = obsId, ["status"] = EhealthPackageContract.ObservationValid,
                ["diagnostic_report"] = EhealthRef.Identifier("diagnostic_report", drId),
                ["categories"] = new JsonArray(EhealthRef.Coding("eHealth/observation_categories", "laboratory")),
                ["code"] = EhealthRef.Coding("eHealth/LOINC/observation_codes", loinc),
                ["issued"] = Iso(res.VerifiedAt ?? res.EnteredAt),
                ["effective_date_time"] = Iso(res.MeasuredAt ?? res.EnteredAt),
                ["primary_source"] = true,
                ["performer"] = employee == null ? null : EhealthRef.Identifier("employee", employee)
            };
            if (res.NumericValue != null)
            {
                ob["value_quantity"] = new JsonObject { ["value"] = res.NumericValue, ["comparator"] = "=", ["unit"] = unit, ["system"] = "eHealth/ucum/units", ["code"] = string.IsNullOrWhiteSpace(unit) ? "1" : unit };
                if (res.NormLow != null || res.NormHigh != null)
                {
                    var range = new JsonObject();
                    if (res.NormLow != null) range["low"] = Quantity(res.NormLow.Value, unit, ">=");
                    if (res.NormHigh != null) range["high"] = Quantity(res.NormHigh.Value, unit, "<=");
                    ob["reference_ranges"] = new JsonArray(range);
                }
            }
            else ob["value_string"] = res.StringValue;
            var interpretation = res.Flag switch { "NORMAL" => "N", "LOW" => "L", "HIGH" => "H", "CRIT_LOW" => "LL", "CRIT_HIGH" => "HH", "ABNORMAL" => "A", _ => null };
            if (interpretation != null) ob["interpretation"] = EhealthRef.Coding("eHealth/observation_interpretations", interpretation);
            if (!string.IsNullOrWhiteSpace(res.OperatorComment)) ob["comment"] = res.OperatorComment;
            observations.Add(ob);
        }

        var dr = new JsonObject
        {
            ["id"] = drId, ["status"] = EhealthPackageContract.StatusFinal,
            ["code"] = serviceId == null ? null : EhealthRef.Identifier("service", serviceId),
            ["category"] = new JsonArray(EhealthRef.Coding("eHealth/diagnostic_report_categories", _options.DiagnosticReportCategoryCode)),
            ["effective_date_time"] = Iso(report.EffectiveDateTimeStart ?? report.EffectiveDateTimeEnd ?? o.OrderDatetime),
            ["issued"] = issued, ["primary_source"] = true,
            ["performer"] = employee != null ? new JsonObject { ["reference"] = EhealthRef.Identifier("employee", employee) }
                : new JsonObject { ["text"] = report.PerformerString ?? "Лаборант" },
            ["recorded_by"] = employee == null ? null : EhealthRef.Identifier("employee", employee),
            ["managing_organization"] = legalEntity == null ? null : EhealthRef.Identifier("legal_entity", legalEntity)
        };
        if (!string.IsNullOrWhiteSpace(report.Description)) dr["conclusion"] = report.Description!.Length > 4000 ? report.Description[..4000] : report.Description;
        if (_identity.DivisionId() is { } div) dr["division"] = EhealthRef.Identifier("division", div);

        string? srId = null;
        if (referral != null)
        {
            srId = referral.EhealthId ?? referral.Id;
            dr["based_on"] = EhealthRef.Identifier("service_request", srId);
        }
        else if (paper != null)
        {
            if (string.IsNullOrWhiteSpace(paper.RequesterLegalEntityEdrpou)) problems.Add("паперове направлення: не вказано ЄДРПОУ закладу-направника");
            if (string.IsNullOrWhiteSpace(paper.RequesterEmployeeName)) problems.Add("паперове направлення: не вказано ПІБ лікаря-направника");
            dr["paper_referral"] = new JsonObject
            {
                ["requisition"] = paper.RegNumber, ["requester_legal_entity_name"] = paper.RequesterLegalEntityName,
                ["requester_legal_entity_edrpou"] = paper.RequesterLegalEntityEdrpou, ["requester_employee_name"] = paper.RequesterEmployeeName,
                ["service_request_date"] = paper.RegDate.ToString("yyyy-MM-dd")
            };
        }

        var package = new JsonObject { ["diagnostic_report"] = dr, ["observations"] = observations };
        StripNulls(package);
        problems.AddRange(EhealthPackageContract.Validate(package).Select(x => "схема пакета: " + x));
        if (problems.Count > 0) return (null, problems);

        var json = package.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return (new EhealthPackage
        {
            Json = json, Hash = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(json))).ToLowerInvariant(), ReportId = drId, PatientId = patientId,
            ServiceRequestId = srId, ObservationIds = obsIds
        }, problems);
    }

    /// <summary>Пакет скасування: той самий пакет, де діагностичний звіт і ВСІ спостереження мають статус entered_in_error (ТВ 3.21.3.3).</summary>
    public (EhealthPackage? Package, List<string> Problems) BuildCancel(string submittedPackageJson, string patientId, string reasonCode, string? explanatoryLetter)
    {
        var problems = new List<string>();
        JsonNode? node;
        try { node = JsonNode.Parse(submittedPackageJson); } catch (JsonException) { return (null, new List<string> { "збережений пакет відправки пошкоджено" }); }
        var dr = node?["diagnostic_report"] as JsonObject; var obs = node?["observations"] as JsonArray;
        if (dr == null || obs == null) return (null, new List<string> { "збережений пакет відправки не містить звіту/спостережень" });
        dr["status"] = EhealthPackageContract.StatusEnteredInError;
        dr["cancellation_reason"] = EhealthRef.Coding("eHealth/cancellation_reasons", reasonCode);
        if (!string.IsNullOrWhiteSpace(explanatoryLetter)) dr["explanatory_letter"] = explanatoryLetter;
        foreach (var o in obs.OfType<JsonObject>()) o["status"] = EhealthPackageContract.StatusEnteredInError;
        problems.AddRange(EhealthPackageContract.Validate(node, cancel: true).Select(x => "схема пакета скасування: " + x));
        if (problems.Count > 0) return (null, problems);
        var json = node!.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return (new EhealthPackage
        {
            Json = json, Hash = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(json))).ToLowerInvariant(), ReportId = dr["id"]!.GetValue<string>(), PatientId = patientId,
            ObservationIds = obs.OfType<JsonObject>().Select(o => o["id"]!.GetValue<string>()).ToList()
        }, problems);
    }

    /// <summary>Прибирає null-властивості (ЕСОЗ не приймає порожні поля).</summary>
    public static void StripNulls(JsonNode? n)
    {
        if (n is JsonObject o)
        {
            foreach (var k in o.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) o.Remove(k);
            foreach (var v in o.Select(kv => kv.Value).ToList()) StripNulls(v);
        }
        else if (n is JsonArray a) foreach (var v in a) StripNulls(v);
    }

    private static JsonObject Quantity(double v, string? unit, string comparator) =>
        new() { ["value"] = v, ["comparator"] = comparator, ["unit"] = unit, ["system"] = "eHealth/ucum/units", ["code"] = string.IsNullOrWhiteSpace(unit) ? "1" : unit };

    private static string Iso(DateTime d) => DateTime.SpecifyKind(d, d.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : d.Kind).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
}
