// =============================================================================
// FR-GAP-060. Розрахунок вартості замовлення за платником.
// Позиції — обрані послуги (профілі) та окремі показники. Алгоритм:
//  1) пакети платника (потім роздрібні): якщо в замовленні є всі складові пакета — складові замінюються пакетом;
//  2) платник PATIENT → ціна з роздрібного прайсу, усе сплачує пацієнт;
//  3) інший платник: позиція є в його прайсі → покрита: сума = ціна прайсу платника, платник сплачує coverage %,
//     пацієнт — решту; позиції немає → не покрита: роздрібна ціна, сплачує пацієнт (IsNotCovered);
//     позиція, яку пацієнт обрав сплатити сам → роздрібна ціна, сплачує пацієнт (IsPatientChoice);
//  4) франшиза платника на замовлення: перші N грн покритої частини переходять на пацієнта.
// Суми округлюються до копійок; частка платника по позиціях узгоджується з підсумком.
// =============================================================================
namespace MedLink.LIS.Core.Billing;

public sealed class BillableItem
{
    /// <summary>Id профілю або показника.</summary>
    public string Id { get; init; } = "";
    public bool IsProfile { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>Пацієнт обрав сплатити цю позицію сам.</summary>
    public bool PatientChoice { get; init; }
}

public sealed class PricePackageDef
{
    public string Id { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public decimal Price { get; init; }
    public IReadOnlyCollection<string> MemberIds { get; init; } = Array.Empty<string>();
    public string? PriceListId { get; init; }
}

public sealed class PayerTerms
{
    public string PayerId { get; init; } = "";
    public string Kind { get; init; } = "PATIENT";
    public string? PriceListId { get; init; }
    /// <summary>Ціни прайсу платника (id профілю/показника → ціна). Наявність = покриття.</summary>
    public IReadOnlyDictionary<string, decimal> Prices { get; init; } = new Dictionary<string, decimal>();
    public IReadOnlyList<PricePackageDef> Packages { get; init; } = Array.Empty<PricePackageDef>();
    public decimal CoveragePct { get; init; } = 100;
    public decimal FranchiseAmount { get; init; }
    /// <summary>Покриває всі послуги за роздрібною ціною (внутрішній платник — власні потреби закладу).</summary>
    public bool CoverAllAtRetail { get; init; }
}

public sealed class RetailTerms
{
    public string PatientPayerId { get; init; } = "";
    public string? PriceListId { get; init; }
    public IReadOnlyDictionary<string, decimal> Prices { get; init; } = new Dictionary<string, decimal>();
    public IReadOnlyList<PricePackageDef> Packages { get; init; } = Array.Empty<PricePackageDef>();
    /// <summary>Базова ціна з довідника (org_organization_service / профіль / показник), якщо немає в роздрібному прайсі.</summary>
    public IReadOnlyDictionary<string, decimal> FallbackPrices { get; init; } = new Dictionary<string, decimal>();
}

public sealed class ChargeLine
{
    public string? ProfileId { get; init; }
    public string? TestId { get; init; }
    public string? PackageId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string PayerId { get; set; } = "";
    public string PayerKind { get; set; } = "PATIENT";
    public string? PriceListId { get; set; }
    public decimal Amount { get; set; }
    public decimal PayerAmount { get; set; }
    public decimal PatientAmount { get; set; }
    public bool IsNotCovered { get; set; }
    public bool IsPatientChoice { get; set; }
    /// <summary>Складові пакета (для документів).</summary>
    public IReadOnlyList<string> PackageMembers { get; init; } = Array.Empty<string>();
}

public sealed class PriceQuote
{
    public List<ChargeLine> Lines { get; } = new();
    public decimal Total => Lines.Sum(l => l.Amount);
    public decimal PayerTotal => Lines.Sum(l => l.PayerAmount);
    public decimal PatientTotal => Lines.Sum(l => l.PatientAmount);
    public decimal FranchiseApplied { get; set; }
}

public static class PriceCalculator
{
    public static PriceQuote Calculate(IReadOnlyList<BillableItem> items, PayerTerms payer, RetailTerms retail)
    {
        var quote = new PriceQuote();
        var isPatient = payer.Kind == "PATIENT";
        var remaining = items.ToList();

        // 1. Пакети: спершу пакети платника (для покритих позицій), потім роздрібні
        IEnumerable<(PricePackageDef Pkg, bool Covered)> packages = (isPatient ? Enumerable.Empty<(PricePackageDef, bool)>() : payer.Packages.Select(p => (p, true)))
            .Concat(retail.Packages.Select(p => (p, false)))
            .OrderByDescending(p => p.Item1.MemberIds.Count);
        foreach (var (pkg, covered) in packages)
        {
            if (pkg.MemberIds.Count == 0) continue;
            var members = remaining.Where(i => pkg.MemberIds.Contains(i.Id) && (!covered || !i.PatientChoice)).ToList();
            if (members.Select(m => m.Id).Distinct().Count() != pkg.MemberIds.Count) continue;
            remaining.RemoveAll(i => members.Contains(i));
            var line = new ChargeLine
            {
                PackageId = pkg.Id, Code = pkg.Code, Name = pkg.Name, Amount = Money(pkg.Price), PackageMembers = members.Select(m => m.Code).ToList(),
                PriceListId = pkg.PriceListId
            };
            if (covered) Cover(line, payer); else ToPatient(line, retail, notCovered: !isPatient, choice: false);
            quote.Lines.Add(line);
        }

        // 2–3. Окремі позиції
        foreach (var i in remaining)
        {
            var line = new ChargeLine { ProfileId = i.IsProfile ? i.Id : null, TestId = i.IsProfile ? null : i.Id, Code = i.Code, Name = i.Name };
            if (!isPatient && !i.PatientChoice && payer.Prices.TryGetValue(i.Id, out var p))
            {
                line.Amount = Money(p); line.PriceListId = payer.PriceListId;
                Cover(line, payer);
            }
            else if (!isPatient && !i.PatientChoice && payer.CoverAllAtRetail)
            {
                line.Amount = Money(RetailPrice(i.Id, retail)); line.PriceListId = retail.PriceListId;
                Cover(line, payer);
            }
            else
            {
                line.Amount = Money(RetailPrice(i.Id, retail)); line.PriceListId = retail.PriceListId;
                ToPatient(line, retail, notCovered: !isPatient && !i.PatientChoice, choice: !isPatient && i.PatientChoice);
            }
            quote.Lines.Add(line);
        }

        // 4. Франшиза: перші N грн покритої частини — пацієнту
        var franchise = Math.Min(payer.FranchiseAmount, quote.PayerTotal);
        if (franchise > 0)
        {
            var left = franchise;
            foreach (var l in quote.Lines.Where(l => l.PayerAmount > 0))
            {
                var shift = Math.Min(left, l.PayerAmount);
                l.PayerAmount -= shift; l.PatientAmount += shift; left -= shift;
                if (left <= 0) break;
            }
            quote.FranchiseApplied = franchise;
        }
        return quote;
    }

    public static decimal RetailPrice(string id, RetailTerms retail) =>
        retail.Prices.TryGetValue(id, out var p) ? p : retail.FallbackPrices.TryGetValue(id, out var f) ? f : 0m;

    private static void Cover(ChargeLine line, PayerTerms payer)
    {
        line.PayerId = payer.PayerId; line.PayerKind = payer.Kind;
        line.PayerAmount = Money(line.Amount * Math.Clamp(payer.CoveragePct, 0, 100) / 100m);
        line.PatientAmount = line.Amount - line.PayerAmount;
    }

    private static void ToPatient(ChargeLine line, RetailTerms retail, bool notCovered, bool choice)
    {
        line.PayerId = retail.PatientPayerId; line.PayerKind = "PATIENT";
        line.PayerAmount = 0; line.PatientAmount = line.Amount;
        line.IsNotCovered = notCovered; line.IsPatientChoice = choice;
    }

    private static decimal Money(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
