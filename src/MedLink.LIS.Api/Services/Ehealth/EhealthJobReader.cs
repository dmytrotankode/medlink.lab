// =============================================================================
// Читання відповідей ЕСОЗ про асинхронні операції. Форма 202 за OpenAPI/preprod: {status, eta, links:[{entity:"job", href:"/Jobs/<id>"}]},
// поля data.id може не бути — id беремо з останнього сегмента href; запасний варіант — data.id. status_code у job буває рядком ("409").
// =============================================================================
using System.Text.Json.Nodes;

namespace MedLink.LIS.Api.Services.Ehealth;

public static class EhealthJobReader
{
    /// <summary>Відповідь на ініціювання операції (use/complete/submit/cancel): 202 → job; 200/201 → виконано синхронно.</summary>
    public static EhealthJob ParseAccepted(JsonNode? json, int httpStatus)
    {
        // Проксі MedLink повертає тіло job {data:{status:"pending", links:[{entity:"job",…}]}} з кодом 200/201 (SDK МІС перевіряє лише data.status),
        // прямий ЕСОЗ — 202. Тому job визначається за вмістом: посилання entity=job або status pending/processed/failed у data.
        if (httpStatus != 202 && !LooksLikeJob(json)) return new EhealthJob("", EhealthJobStatus.Processed, null, "sync", httpStatus); // 200/201: виконано синхронно, у data — сутність
        if (httpStatus != 202 && Parse(json, httpStatus) is { } direct && (direct.IsProcessed || direct.IsFailed) && string.IsNullOrEmpty(direct.Id)) return direct;
        var job = Parse(json, httpStatus);
        if (string.IsNullOrEmpty(job.Id))
            throw new EhealthException(502, "Відповідь ЕСОЗ 202 не містить посилання на job (links[entity=job] або data.id)", EhealthErrorKind.Transient, json?.ToJsonString());
        return job;
    }

    private static bool LooksLikeJob(JsonNode? json)
    {
        var data = ((json as JsonObject)?["data"] as JsonObject) ?? json as JsonObject;
        if (data == null) return false;
        if ((data["links"] as JsonArray)?.OfType<JsonObject>().Any(l => string.Equals(Str(l["entity"]), "job", StringComparison.OrdinalIgnoreCase)) == true) return true;
        return Str(data["status"])?.ToLowerInvariant() is "pending" or "processed" or "failed" or "failed_with_error";
    }

    /// <summary>Відповідь jobs/{id} (проксі) або GET /api/jobs/{id} (прямий ЕСОЗ), а також відповідь-прийняття операції: нормалізує стан, id, посилання на створені сутності й помилку.</summary>
    public static EhealthJob Parse(JsonNode? json, int httpStatus)
    {
        var root = json as JsonObject;
        var data = (root?["data"] as JsonObject) ?? root;
        var links = new List<EhealthLink>();
        foreach (var arr in new[] { data?["links"] as JsonArray, root?["links"] as JsonArray })
            if (arr != null)
                foreach (var l in arr.OfType<JsonObject>())
                {
                    var entity = Str(l["entity"]); var href = Str(l["href"]);
                    if (entity != null && href != null && !links.Any(x => x.Entity == entity && x.Href == href)) links.Add(new EhealthLink(entity, href));
                }

        var jobLink = links.FirstOrDefault(l => l.Entity.Equals("job", StringComparison.OrdinalIgnoreCase));
        var id = jobLink != null ? LastSegment(jobLink.Href) : Str(data?["id"]) ?? Str(root?["id"]) ?? "";
        var entityLink = links.FirstOrDefault(l => !l.Entity.Equals("job", StringComparison.OrdinalIgnoreCase));
        var raw = Str(data?["status"]) ?? (httpStatus == 303 ? "processed" : httpStatus == 202 ? "pending" : "");
        int? statusCode = null;
        var sc = data?["status_code"];
        if (sc != null) { if (int.TryParse(sc.ToString(), out var n)) statusCode = n; }

        string? error = null; List<string>? invalid = null;
        var err = data?["error"];
        if (err is JsonObject eo)
        {
            error = Str(eo["message"]) ?? Str(eo["type"]);
            invalid = ReadInvalid(eo);
        }
        else if (err != null) error = Str(err);

        return new EhealthJob(id, Normalize(raw, httpStatus), entityLink == null ? Str(data?["entity_id"]) : LastSegment(entityLink.Href), raw, statusCode, error, links, invalid);
    }

    public static string Normalize(string raw, int httpStatus)
    {
        var s = raw.Trim().ToLowerInvariant();
        if (s == "processed" || s == "done" || s == "success" || s == "completed") return EhealthJobStatus.Processed;
        if (s.Contains("fail") || s.Contains("error") || s == "rejected") return EhealthJobStatus.Failed;
        if (s == "" && httpStatus is 200 or 201 or 303) return EhealthJobStatus.Processed;
        return EhealthJobStatus.Pending;
    }

    /// <summary>Перелік порушень 422: error.invalid[].entry + rules[].description.</summary>
    public static List<string> ReadInvalid(JsonNode? error)
    {
        var res = new List<string>();
        if (error?["invalid"] is not JsonArray arr) return res;
        foreach (var i in arr.OfType<JsonObject>())
        {
            var entry = Str(i["entry"]) ?? "";
            var rules = i["rules"] as JsonArray;
            var descr = rules == null ? null : string.Join("; ", rules.OfType<JsonObject>().Select(r => Str(r["description"]) ?? Str(r["rule"])).Where(x => x != null));
            res.Add(string.IsNullOrEmpty(descr) ? entry : $"{entry}: {descr}");
        }
        return res;
    }

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n is JsonValue v2 ? v2.ToString() : null;

    public static string LastSegment(string href)
    {
        var h = href.Split('?', '#')[0].TrimEnd('/');
        var i = h.LastIndexOf('/');
        return i >= 0 ? h[(i + 1)..] : h;
    }
}
