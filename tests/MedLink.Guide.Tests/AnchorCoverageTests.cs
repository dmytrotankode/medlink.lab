// Автоперевірка якорів інструкцій: для КОЖНОГО кроку КОЖНОГО сценарію з Seed/*.json якір має існувати і знаходитись у коді Vue
// (data-testid, текстовий літерал у шаблоні, aria-label, id/клас). Така сама логіка в tools/guide/check_anchors.py
// (там додатково перевіряється сторінка кроку за маршрутом і формується звіт). Тест падає, якщо знайдено хоча б один крок без валідного якоря.
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace MedLink.Guide.Tests;

public class AnchorCoverageTests
{
    private readonly ITestOutputHelper _out;
    public AnchorCoverageTests(ITestOutputHelper output) => _out = output;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MedLink.LIS.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Не знайдено корінь репозиторію (MedLink.LIS.sln)");
    }

    private static string Norm(string? s) => Regex.Replace(s ?? "", @"\s+", " ").Trim().ToLowerInvariant();

    /// <summary>Індекс коду фронтенду: що є в шаблонах і скриптах.</summary>
    private sealed class CodeIndex
    {
        public readonly string All;
        public readonly string AllLower;
        public readonly HashSet<string> TestIds = new();
        public readonly HashSet<string> TestIdPrefixes = new();
        public readonly HashSet<string> Texts = new();
        public readonly List<Regex> TextPatterns = new();

        public CodeIndex(IEnumerable<string> sources)
        {
            var list = sources.ToList();
            All = string.Join("\n", list);
            AllLower = All.ToLowerInvariant();
            foreach (Match m in Regex.Matches(All, "data-(?:testid|guide-id)\\s*=\\s*\"([^\"]*)\"")) TestIds.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(All, "(?::|v-bind:)data-(?:testid|guide-id)\\s*=\\s*\"([^\"]*)\""))
                foreach (Match l in Regex.Matches(m.Groups[1].Value, "'([^']*)'|`([^`$]*)"))
                {
                    var v = l.Groups[1].Success && l.Groups[1].Length > 0 ? l.Groups[1].Value : l.Groups[2].Value;
                    if (v.Length >= 2) TestIdPrefixes.Add(v);
                }
            foreach (var src in list)
            {
                foreach (var seg in Regex.Split(src, "<[^>]*>")) AddText(seg);
                foreach (Match m in Regex.Matches(src, "(?:label|title|caption|placeholder|aria-label|text)\\s*=\\s*\"([^\"{]*)\"")) AddText(m.Groups[1].Value);
                foreach (var q in new[] { "'", "\"", "`" })
                    foreach (Match m in Regex.Matches(src, Regex.Escape(q) + "([^" + Regex.Escape(q) + "\\n$]{2,120})" + Regex.Escape(q))) AddText(m.Groups[1].Value);
            }
        }

        private void AddText(string seg)
        {
            var t = Norm(seg.Replace("&nbsp;", " "));
            if (t.Length == 0) return;
            if (t.Contains("{{"))
            {
                var parts = Regex.Split(t, @"\{\{.*?\}\}");
                if (string.Concat(parts).Trim().Length >= 2)
                    TextPatterns.Add(new Regex("^" + string.Join(".*", parts.Select(p => Regex.Escape(p.Trim()))) + "$"));
            }
            else Texts.Add(t);
        }

        public bool HasText(string text) { var t = Norm(text); return Texts.Contains(t) || TextPatterns.Any(r => r.IsMatch(t)); }

        public bool HasTestId(string v, string op = "")
        {
            if (op == "^")
                return TestIds.Any(x => x.StartsWith(v)) || TestIdPrefixes.Any(p => v.StartsWith(p) || p.StartsWith(v))
                    || All.Contains("\"" + v) || All.Contains("'" + v) || All.Contains("`" + v);
            if (op == "$" || op == "*") return TestIds.Any(x => x.Contains(v)) || TestIdPrefixes.Any(p => p.Contains(v)) || All.Contains(v);
            return TestIds.Contains(v) || TestIdPrefixes.Any(p => v.StartsWith(p)) || All.Contains("\"" + v + "\"") || All.Contains("'" + v + "'") || All.Contains("`" + v + "`");
        }

        public bool HasToken(string tok) => AllLower.Contains(tok.ToLowerInvariant());
    }

    private static (bool ok, string why) CheckStrategy(CodeIndex ix, JsonNode st)
    {
        var type = st["type"]?.GetValue<string>();
        var v = st["value"]?.GetValue<string>() ?? "";
        if (type == "text")
        {
            var i = v.IndexOf('|');
            var text = i >= 0 ? v[(i + 1)..] : v;
            return (ix.HasText(text), $"текст «{text}» не знайдено в шаблонах");
        }
        if (type is "testid" or "guide")
        {
            var m = Regex.Match(v, "\\[data-[a-z-]+\\s*([\\^$*]?)=\\s*\"([^\"]+)\"\\]");
            if (!m.Success) return (false, "незрозумілий селектор " + v);
            return (ix.HasTestId(m.Groups[2].Value, m.Groups[1].Value), $"data-testid «{m.Groups[1].Value}={m.Groups[2].Value}» не знайдено");
        }
        var only = Regex.Match(v, "^\\s*\\w*\\[data-[a-z-]+\\s*=\\s*\"([^\"]+)\"\\]\\s*$");
        if (only.Success) return (ix.HasTestId(only.Groups[1].Value), $"data-testid «{only.Groups[1].Value}» не знайдено");
        var toks = Regex.Matches(v, "\\[[a-z-]+[\\^$*~|]?=\\s*\"([^\"]*)\"\\]").Select(m => m.Groups[1].Value).ToList();
        var rest = Regex.Replace(v, @"\[[^\]]*\]", " ");
        toks.AddRange(Regex.Matches(rest, @"#([\w-]+)").Select(m => m.Groups[1].Value));
        toks.AddRange(Regex.Matches(rest, @"\.([\w-]+)").Select(m => m.Groups[1].Value).Where(c => !c.StartsWith("q-")));
        toks = toks.Where(t => t.Length > 0).ToList();
        if (toks.Count == 0) return (true, "");
        var miss = toks.Where(t => !ix.HasToken(t)).ToList();
        return (miss.Count == 0, "у коді немає: " + string.Join(", ", miss));
    }

    [Fact]
    public void Every_step_of_every_scenario_has_valid_anchor_in_vue_code()
    {
        var root = RepoRoot();
        var srcDirs = new[] { Path.Combine(root, "src", "MedLink.LIS.Web", "src"), Path.Combine(root, "src", "MedLink.Guide.Web", "src") };
        var sources = srcDirs.Where(Directory.Exists).SelectMany(d => Directory.EnumerateFiles(d, "*.*", SearchOption.AllDirectories))
            .Where(f => f.EndsWith(".vue") || f.EndsWith(".js") || f.EndsWith(".html")).Select(File.ReadAllText).ToList();
        Assert.True(sources.Count > 50, "Не знайдено вихідні файли фронтенду");
        var ix = new CodeIndex(sources);

        var bundles = Directory.GetFiles(Path.Combine(root, "src", "MedLink.Guide", "Seed"), "*.json").OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Name: Path.GetFileName(f), Json: JsonNode.Parse(File.ReadAllText(f))!)).ToList();

        // ефективні визначення якорів: як при імпорті, остання повна визначена версія за кодом перемагає
        var eff = new Dictionary<string, JsonNode>();
        foreach (var (_, json) in bundles)
            foreach (var sc in json["scenarios"]?.AsArray() ?? new JsonArray())
                foreach (var st in sc!["steps"]!.AsArray())
                {
                    var a = st!["anchor"];
                    var code = a?["code"]?.GetValue<string>();
                    if (code != null && a!["strategies"] != null) eff[code] = a;
                }

        int scenarios = 0, steps = 0;
        var bad = new List<string>();
        foreach (var (name, json) in bundles)
            foreach (var sc in json["scenarios"]?.AsArray() ?? new JsonArray())
            {
                scenarios++;
                var scCode = sc!["code"]!.GetValue<string>();
                var i = 0;
                foreach (var st in sc["steps"]!.AsArray())
                {
                    i++; steps++;
                    var a = st!["anchor"];
                    var title = st["title"]?.GetValue<string>();
                    if (a == null)
                    {
                        if (st["highlight"]?.GetValue<string>() == "element") bad.Add($"{scCode} #{i} «{title}»: highlight=element без якоря (поставте якір або highlight=none)");
                        continue;
                    }
                    var code = a["code"]?.GetValue<string>();
                    var e = code != null ? eff.GetValueOrDefault(code) : (a["strategies"] != null ? a : null);
                    if (e == null) { bad.Add($"{scCode} #{i} «{title}»: якір {code} без визначення (немає strategies)"); continue; }
                    var strategies = e["strategies"]!.AsArray();
                    if (strategies.Count == 0) { bad.Add($"{scCode} #{i} «{title}»: порожній список стратегій якоря {code}"); continue; }
                    var results = strategies.Select(s => CheckStrategy(ix, s!)).ToList();
                    if (!results.Any(r => r.ok)) bad.Add($"{scCode} #{i} «{title}» [{code}]: " + string.Join("; ", results.Select(r => r.why)));
                }
            }

        _out.WriteLine($"Сценаріїв: {scenarios}, кроків: {steps}, кроків без валідного якоря: {bad.Count}");
        foreach (var b in bad) _out.WriteLine("  " + b);
        Assert.True(scenarios >= 296, $"Очікувалось щонайменше 296 сценаріїв, знайдено {scenarios}");
        Assert.True(bad.Count == 0, $"Кроків без валідного якоря: {bad.Count}\n" + string.Join("\n", bad.Take(40)));
    }
}
