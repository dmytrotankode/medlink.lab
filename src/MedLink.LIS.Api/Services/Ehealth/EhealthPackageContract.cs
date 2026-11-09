// =============================================================================
// Структурна перевірка пакета Submit/Cancel Diagnostic Report Package за OpenAPI eHealth
// (схема Submit_DR_Package_DecodedPayload: diagnostic_report + observations[]) і ТВ МІС 3.19/3.21.
// Використовується: будівником (перед підписом), мок-ЕСОЗ (відповідь 422 з переліком invalid) і тестами.
// Повертає шляхи у форматі JSON-path ($.diagnostic_report.performer) — як у відповіді 422 ЕСОЗ.
// =============================================================================
using System.Text.Json.Nodes;

namespace MedLink.LIS.Api.Services.Ehealth;

public static class EhealthPackageContract
{
    public const string StatusFinal = "final", StatusEnteredInError = "entered_in_error", ObservationValid = "valid";

    private static readonly string[] ValueKeys =
        { "value_quantity", "value_codeable_concept", "value_sampled_data", "value_string", "value_boolean", "value_range", "value_ratio", "value_time", "value_date_time", "value_period" };

    /// <param name="cancel">true — пакет скасування (PATCH): усі статуси entered_in_error, причина скасування.</param>
    public static List<string> Validate(JsonNode? package, bool cancel = false)
    {
        var e = new List<string>();
        if (package is not JsonObject root) { e.Add("$: пакет має бути JSON-об'єктом"); return e; }
        if (root["diagnostic_report"] is not JsonObject dr) { e.Add("$.diagnostic_report: обов'язкове поле"); return e; }

        // --- diagnostic_report
        UuidV1(e, dr["id"], "$.diagnostic_report.id");
        var status = Str(dr["status"]);
        if (cancel ? status != StatusEnteredInError : status != StatusFinal) e.Add($"$.diagnostic_report.status: очікується {(cancel ? StatusEnteredInError : StatusFinal)}");
        Ref(e, dr["code"], "$.diagnostic_report.code", "service");
        if (dr["category"] is not JsonArray cat || cat.Count == 0 || !HasCoding(cat[0])) e.Add("$.diagnostic_report.category: потрібна категорія з eHealth/diagnostic_report_categories");
        else if (cat.Count != 1) e.Add("$.diagnostic_report.category: рівно одне значення");
        if (!IsIso(Str(dr["issued"]))) e.Add("$.diagnostic_report.issued: дата-час ISO 8601 (UTC)");
        if (dr["primary_source"] is not JsonValue ps || !ps.TryGetValue<bool>(out var psv) || !psv) e.Add("$.diagnostic_report.primary_source: у цьому методі лише true");
        var perf = dr["performer"] as JsonObject;
        if (perf == null || (perf["reference"] == null && string.IsNullOrWhiteSpace(Str(perf["text"])))) e.Add("$.diagnostic_report.performer: reference або text");
        else if (perf["reference"] != null) Ref(e, perf["reference"], "$.diagnostic_report.performer.reference", "employee");
        Ref(e, dr["recorded_by"], "$.diagnostic_report.recorded_by", "employee");
        Ref(e, dr["managing_organization"], "$.diagnostic_report.managing_organization", "legal_entity");
        if (dr["division"] != null) Ref(e, dr["division"], "$.diagnostic_report.division", "division");
        var effCount = (dr["effective_date_time"] != null ? 1 : 0) + (dr["effective_period"] != null ? 1 : 0);
        if (effCount != 1) e.Add("$.diagnostic_report: потрібне рівно одне з effective_date_time | effective_period");
        if (dr["based_on"] != null && dr["paper_referral"] != null) e.Add("$.diagnostic_report: based_on і paper_referral взаємовиключні");
        if (dr["based_on"] != null) Ref(e, dr["based_on"], "$.diagnostic_report.based_on", "service_request");
        if (dr["paper_referral"] is JsonObject pr)
        {
            foreach (var f in new[] { "requester_legal_entity_edrpou", "requester_employee_name", "service_request_date" })
                if (string.IsNullOrWhiteSpace(Str(pr[f]))) e.Add($"$.diagnostic_report.paper_referral.{f}: обов'язкове поле");
        }
        if (cancel)
        {
            if (!HasCoding(dr["cancellation_reason"])) e.Add("$.diagnostic_report.cancellation_reason: обов'язкове (eHealth/cancellation_reasons)");
        }

        // --- observations
        var drId = Str(dr["id"]);
        var seen = new HashSet<string>();
        if (root["observations"] is JsonArray obs)
        {
            if (!cancel && obs.Count == 0) e.Add("$.observations: потрібне хоча б одне спостереження");
            for (var i = 0; i < obs.Count; i++)
            {
                var p = $"$.observations[{i}]";
                if (obs[i] is not JsonObject o) { e.Add($"{p}: має бути об'єктом"); continue; }
                UuidV1(e, o["id"], $"{p}.id");
                if (Str(o["id"]) is { } oid && !seen.Add(oid)) e.Add($"{p}.id: дубль ідентифікатора");
                var st = Str(o["status"]);
                if (cancel ? st != StatusEnteredInError : st != ObservationValid) e.Add($"{p}.status: очікується {(cancel ? StatusEnteredInError : ObservationValid)}");
                Ref(e, o["diagnostic_report"], $"{p}.diagnostic_report", "diagnostic_report");
                if (Str(o["diagnostic_report"]?["identifier"]?["value"]) is { } rv && rv != drId) e.Add($"{p}.diagnostic_report: посилання не на звіт цього пакета");
                if (o["categories"] is not JsonArray c || c.Count == 0 || !HasCoding(c[0])) e.Add($"{p}.categories: обов'язкове поле");
                if (!HasCoding(o["code"])) e.Add($"{p}.code: потрібен код спостереження (LOINC)");
                if (!IsIso(Str(o["issued"]))) e.Add($"{p}.issued: дата-час ISO 8601");
                if (o["primary_source"] is not JsonValue ops || !ops.TryGetValue<bool>(out var opv) || !opv) e.Add($"{p}.primary_source: лише true");
                Ref(e, o["performer"], $"{p}.performer", "employee");
                var eff = (o["effective_date_time"] != null ? 1 : 0) + (o["effective_period"] != null ? 1 : 0);
                if (eff != 1) e.Add($"{p}: потрібне рівно одне з effective_date_time | effective_period");
                var values = ValueKeys.Count(k => o[k] != null);
                if (values != 1) e.Add($"{p}: потрібне рівно одне значення value_*");
                if (o["value_quantity"] is JsonObject q)
                {
                    if (q["value"] == null) e.Add($"{p}.value_quantity.value: обов'язкове поле");
                    if (string.IsNullOrWhiteSpace(Str(q["system"])) || string.IsNullOrWhiteSpace(Str(q["code"]))) e.Add($"{p}.value_quantity: потрібні system і code (eHealth/ucum/units)");
                }
                if (o["reference_ranges"] is JsonArray rr)
                    for (var j = 0; j < rr.Count; j++)
                        foreach (var side in new[] { "low", "high" })
                            if (rr[j]?[side] is JsonObject b && (b["value"] == null || string.IsNullOrWhiteSpace(Str(b["system"])) || string.IsNullOrWhiteSpace(Str(b["code"]))))
                                e.Add($"{p}.reference_ranges[{j}].{side}: потрібні value, system, code");
            }
        }
        else if (cancel) e.Add("$.observations: у пакеті скасування мають бути всі спостереження звіту (entered_in_error)");
        else e.Add("$.observations: обов'язкове поле");
        return e;
    }

    private static string? Str(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.ToString()) : null;
    private static bool IsIso(string? s) => s != null && DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out _) && s.Contains('T');
    private static bool HasCoding(JsonNode? n) => n?["coding"] is JsonArray a && a.Count > 0 && !string.IsNullOrWhiteSpace(Str(a[0]?["system"])) && !string.IsNullOrWhiteSpace(Str(a[0]?["code"]));

    private static void UuidV1(List<string> e, JsonNode? n, string path)
    {
        if (!Uuid1.IsV1(Str(n))) e.Add($"{path}: очікується UUID версії 1 (ТВ 1.1.7)");
    }

    private static void Ref(List<string> e, JsonNode? n, string path, string expectedType)
    {
        var idf = n?["identifier"];
        var code = Str(idf?["type"]?["coding"]?[0]?["code"]);
        if (idf == null || string.IsNullOrWhiteSpace(Str(idf["value"])) || code == null) e.Add($"{path}: потрібне посилання identifier.type.coding + identifier.value");
        else if (code != expectedType) e.Add($"{path}.identifier.type.coding[0].code: очікується {expectedType}");
    }
}
