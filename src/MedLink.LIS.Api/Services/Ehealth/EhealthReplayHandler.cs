// =============================================================================
// Replay (Ehealth:Mode = Replay): відповідає ЗАПИСАНИМИ фікстурами замість реальної ЕСОЗ — для CI та відтворення збоїв.
// Фікстури: tests/MedLink.LIS.Tests/Fixtures/ehealth/*.json (приклади з OpenAPI eHealth та знімки відповідей preprod).
// Формат файлу — один запис або масив записів:
//   { "name": "…", "match": { "method": "GET", "pathRegex": "/api/service_requests$", "query": { "requisition": "AX654654T" }, "bodyContains": "…" },
//     "responses": [ { "status": 202, "body": {…} }, { "status": 303, "body": {…} } ] }
// Запис вибирається першим за збігом; N-й виклик цього запису отримує responses[N] (останній повторюється) — так моделюється
// послідовність pending → processed. Немає фікстури — 501 {"error":{"type":"replay_fixture_missing"}} (клас помилки Config).
// У значеннях body рядок "{{now}}" замінюється на поточний час UTC (ISO 8601).
// =============================================================================
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MedLink.LIS.Api.Services.Ehealth;

public sealed class EhealthReplayState
{
    internal sealed class Fixture
    {
        public string Name = "";
        public string? Method;
        public Regex? PathRegex;
        public Dictionary<string, string> Query = new();
        public string? BodyContains;
        public List<(int Status, string Body)> Responses = new();
        public int Hits;
    }

    private readonly object _gate = new();
    private List<Fixture>? _fixtures;
    public List<string> Calls { get; } = new();

    internal List<Fixture> Load(string? path)
    {
        lock (_gate)
        {
            if (_fixtures != null) return _fixtures;
            var dir = ResolveDir(path);
            var list = new List<Fixture>();
            if (dir != null)
                foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                {
                    var node = JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8));
                    foreach (var item in node is JsonArray a ? a.OfType<JsonObject>() : node is JsonObject o ? new[] { o } : Array.Empty<JsonObject>())
                    {
                        var m = item["match"] as JsonObject; if (m == null) continue;
                        var f = new Fixture { Name = item["name"]?.GetValue<string>() ?? Path.GetFileName(file), Method = m["method"]?.GetValue<string>()?.ToUpperInvariant(), BodyContains = m["bodyContains"]?.GetValue<string>() };
                        if (m["pathRegex"]?.GetValue<string>() is { } rx) f.PathRegex = new Regex(rx, RegexOptions.IgnoreCase);
                        if (m["query"] is JsonObject q) foreach (var kv in q) f.Query[kv.Key] = kv.Value?.GetValue<string>() ?? "";
                        foreach (var r in (item["responses"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                            f.Responses.Add((r["status"]?.GetValue<int>() ?? 200, r["body"]?.ToJsonString() ?? ""));
                        if (f.Responses.Count > 0) list.Add(f);
                    }
                }
            return _fixtures = list;
        }
    }

    private static string? ResolveDir(string? configured)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ehealth"));
        return candidates.FirstOrDefault(Directory.Exists);
    }

    public void Reset() { lock (_gate) { if (_fixtures != null) foreach (var f in _fixtures) f.Hits = 0; Calls.Clear(); } }
    public IReadOnlyList<string> CallLog() { lock (_gate) return Calls.ToList(); }
    internal void Record(string call) { lock (_gate) Calls.Add(call); }
    internal (int Status, string Body)? Next(Fixture f) { lock (_gate) { var r = f.Responses[Math.Min(f.Hits, f.Responses.Count - 1)]; f.Hits++; return r; } }
}

public sealed class EhealthReplayHandler : HttpMessageHandler
{
    private readonly EhealthReplayState _state;
    private readonly EhealthOptions _options;
    public EhealthReplayHandler(EhealthReplayState state, EhealthOptions options) { _state = state; _options = options; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var method = request.Method.Method;
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
        _state.Record($"{method} {path}{request.RequestUri.Query}");

        var fixtures = _state.Load(_options.Replay.FixturesPath);
        foreach (var f in fixtures)
        {
            if (f.Method != null && f.Method != method) continue;
            if (f.PathRegex != null && !f.PathRegex.IsMatch(path)) continue;
            if (f.Query.Any(kv => query[kv.Key] != kv.Value)) continue;
            if (f.BodyContains != null && !body.Contains(f.BodyContains, StringComparison.Ordinal)) continue;
            var (status, text) = _state.Next(f)!.Value;
            text = text.Replace("{{now}}", DateTime.UtcNow.ToString("O"));
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(text, new UTF8Encoding(false), "application/json") };
        }
        var err = new JsonObject { ["error"] = new JsonObject { ["type"] = "replay_fixture_missing", ["message"] = $"Replay: немає фікстури для {method} {path}{request.RequestUri.Query}" } };
        return new HttpResponseMessage(HttpStatusCode.NotImplemented) { Content = new StringContent(err.ToJsonString(), new UTF8Encoding(false), "application/json") };
    }
}
