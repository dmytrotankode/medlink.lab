// =============================================================================
// FR-GAP-060. Оплата замовлення: платник (каса / страхова програма за полісом / підприємство / клініка / НСЗУ / внутрішнє),
// прайс-листи і пакети, нарахування по позиціях (частка платника і пацієнта), оплати в касі, рахунки платникам,
// друковані документи (замовлення-квитанція пацієнту, рахунок платнику). Платні послуги не потребують ЕСОЗ —
// е-направлення лише визначає програму НСЗУ, якщо вона в ньому вказана.
// =============================================================================
using System.Globalization;
using System.Net;
using System.Text;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Core.Billing;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class QuoteRequest
{
    public List<string> ProfileIds { get; set; } = new();
    public List<string> TestIds { get; set; } = new();
    public string? PayerId { get; set; }
    public string? PatientId { get; set; }
    public string? EhealthReferralNumber { get; set; }
}

public sealed class SetPayerRequest
{
    public string? PayerId { get; set; }
    public string? PatientInsuranceId { get; set; }
    public string? AuthorizationNumber { get; set; }
}

public sealed class PaymentRequest
{
    public decimal Amount { get; set; }
    /// <summary>CASH | CARD | TRANSFER | REFUND</summary>
    public string Method { get; set; } = "CASH";
    public string? Note { get; set; }
}

public sealed class PatientInsuranceRequest
{
    public string PayerId { get; set; } = "";
    public string PolicyNumber { get; set; } = "";
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PriceListRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Ціни: id або код профілю/показника → ціна.</summary>
    public Dictionary<string, decimal>? Items { get; set; }
    public List<PackageRequest>? Packages { get; set; }
}

public sealed class PackageRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public List<string> Members { get; set; } = new();
}

public sealed class BillingService
{
    public const string PatientPayerCode = "PATIENT";
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;
    private readonly INumeratorService _numerators;

    public static readonly string[] Cashiers = { LabRoles.Admin, LabRoles.Registrar, LabRoles.Phlebotomist };

    public BillingService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current, INumeratorService numerators)
    { _db = db; _policy = policy; _audit = audit; _current = current; _numerators = numerators; }

    // ------------------------------------------------------------------ умови
    private async Task<LabPayer> PatientPayerAsync() =>
        await _db.Payers.FirstOrDefaultAsync(p => p.Code == PatientPayerCode) ?? throw new ConflictException("Не налаштовано платника «Пацієнт» (код PATIENT)");

    private static bool ActiveAt(LabPriceList? pl, DateTime at) => pl != null && pl.IsActive && pl.RecordState != RecordStates.Deleted
        && (pl.ValidFrom == null || pl.ValidFrom.Value.Date <= at.Date) && (pl.ValidTo == null || pl.ValidTo.Value.Date >= at.Date);

    private async Task<(PayerTerms Payer, RetailTerms Retail)> TermsAsync(LabPayer payer, DateTime at)
    {
        var patient = await PatientPayerAsync();
        var lists = await _db.PriceLists.AsNoTracking().Include(l => l.Items).Include(l => l.Packages).Where(l => l.RecordState != RecordStates.Deleted).ToListAsync();
        var retailList = lists.FirstOrDefault(l => l.IsDefault && ActiveAt(l, at));
        var fallback = (await _db.Profiles.AsNoTracking().Include(p => p.OrganizationService).Select(p => new { p.Id, Price = p.OrganizationService != null && p.OrganizationService.Price != null ? p.OrganizationService.Price.Value : p.Price }).ToListAsync())
            .ToDictionary(x => x.Id, x => x.Price);
        foreach (var t in await _db.Tests.AsNoTracking().Select(t => new { t.Id, t.Price }).ToListAsync()) fallback[t.Id] = t.Price;
        var retail = new RetailTerms
        {
            PatientPayerId = patient.Id, PriceListId = retailList?.Id, FallbackPrices = fallback,
            Prices = retailList?.Items.Where(i => i.RecordState != RecordStates.Deleted).ToDictionary(i => i.ProfileId ?? i.TestId!, i => i.Price) ?? new Dictionary<string, decimal>(),
            Packages = retailList?.Packages.Where(p => p.IsActive && p.RecordState != RecordStates.Deleted).Select(p => Pkg(p)).ToList() ?? new()
        };
        var payerList = payer.PriceListId == null ? null : lists.FirstOrDefault(l => l.Id == payer.PriceListId);
        if (payer.Kind != PayerKinds.Patient && payerList != null && !ActiveAt(payerList, at)) payerList = null; // прайс не діє — нічого не покривається
        var terms = new PayerTerms
        {
            PayerId = payer.Id, Kind = payer.Kind, PriceListId = payerList?.Id, CoveragePct = payer.CoveragePct, FranchiseAmount = payer.FranchiseAmount,
            CoverAllAtRetail = payer.Kind == PayerKinds.Internal && payerList == null,
            Prices = payerList?.Items.Where(i => i.RecordState != RecordStates.Deleted).ToDictionary(i => i.ProfileId ?? i.TestId!, i => i.Price) ?? new Dictionary<string, decimal>(),
            Packages = payerList?.Packages.Where(p => p.IsActive && p.RecordState != RecordStates.Deleted).Select(p => Pkg(p)).ToList() ?? new()
        };
        return (terms, retail);
    }

    private static PricePackageDef Pkg(LabPricePackage p) => new() { Id = p.Id, Code = p.Code, Name = p.Name, Price = p.Price, MemberIds = p.MemberIds, PriceListId = p.PriceListId };

    /// <summary>Позиції для тарифікації: обрані профілі + показники поза профілями (у т. ч. reflex/дозамовлені).</summary>
    private async Task<List<BillableItem>> ItemsAsync(IEnumerable<LabOrderTest> tests, ISet<string> patientChoice)
    {
        var list = tests.Where(t => t.Status != OrderTestStatuses.Rejected || t.Result != null).ToList();
        var profileIds = list.Where(t => t.ProfileId != null).Select(t => t.ProfileId!).Distinct().ToList();
        var profiles = await _db.Profiles.AsNoTracking().Where(p => profileIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var items = profileIds.Select(id => new BillableItem { Id = id, IsProfile = true, Code = profiles[id].Code, Name = profiles[id].Name, PatientChoice = patientChoice.Contains(id) }).ToList();
        items.AddRange(list.Where(t => t.ProfileId == null).GroupBy(t => t.TestId).Select(g => new BillableItem { Id = g.Key, Code = g.First().TestCode, Name = g.First().TestName, PatientChoice = patientChoice.Contains(g.Key) }));
        return items;
    }

    // ------------------------------------------------------------------ платник замовлення
    /// <summary>Визначає платника замовлення: явний → поліс → програма НСЗУ з е-направлення → пацієнт (каса).</summary>
    public async Task ApplyPayerAsync(LabOrder order, string? payerId, string? patientInsuranceId, string? authorizationNumber)
    {
        LabPayer? payer = null;
        MisPatientInsurance? policy = null;
        if (!string.IsNullOrWhiteSpace(patientInsuranceId))
        {
            policy = await _db.PatientInsurances.Include(p => p.Payer).FirstOrDefaultAsync(p => p.Id == patientInsuranceId && p.PatientCardId == order.PatientId)
                     ?? throw ValidationException.Field("patientInsuranceId", "Поліс пацієнта не знайдено");
            payer = policy.Payer;
        }
        if (payer == null && !string.IsNullOrWhiteSpace(payerId))
            payer = await _db.Payers.FirstOrDefaultAsync(p => (p.Id == payerId || p.Code == payerId) && p.RecordState != RecordStates.Deleted) ?? throw ValidationException.Field("payerId", "Платника не знайдено");
        if (payer == null && order.EhealthReferralId != null)
        {
            var programId = await _db.Referrals.Where(r => r.Id == order.EhealthReferralId).Select(r => r.MedicalServiceProgramId).FirstOrDefaultAsync();
            if (programId != null) payer = await _db.Payers.FirstOrDefaultAsync(p => p.Kind == PayerKinds.Nszu && p.MedicalProgramId == programId && p.IsActive);
        }
        payer ??= await PatientPayerAsync();
        if (!payer.IsActive) throw new ConflictException($"Платник «{payer.Name}» неактивний");
        if (payer.ContractValidTo != null && payer.ContractValidTo.Value.Date < order.OrderDatetime.Date) throw new ConflictException($"Договір з «{payer.Name}» закінчився {payer.ContractValidTo:dd.MM.yyyy}");
        if (payer.RequiresPolicy)
        {
            policy ??= await _db.PatientInsurances.Where(p => p.PatientCardId == order.PatientId && p.PayerId == payer.Id && p.IsActive && p.RecordState != RecordStates.Deleted).OrderByDescending(p => p.ValidTo).FirstOrDefaultAsync();
            if (policy == null || !policy.IsValidAt(order.OrderDatetime)) throw new ConflictException($"Для програми «{payer.Name}» потрібен чинний поліс пацієнта");
        }
        if (payer.RequiresAuthorization && string.IsNullOrWhiteSpace(authorizationNumber))
            throw ValidationException.Field("authorizationNumber", $"Програма «{payer.Name}» вимагає номер гарантійного листа / погодження");
        order.PayerId = payer.Id;
        order.PatientInsuranceId = policy?.Id;
        order.InsurancePolicyNumber = policy?.PolicyNumber;
        order.AuthorizationNumber = string.IsNullOrWhiteSpace(authorizationNumber) ? null : authorizationNumber.Trim();
    }

    /// <summary>Перераховує нарахування замовлення (зберігає вибір пацієнта «сплачу сам»).</summary>
    public async Task RecalculateAsync(LabOrder order)
    {
        var payer = order.PayerId == null ? await PatientPayerAsync() : await _db.Payers.FirstAsync(p => p.Id == order.PayerId);
        var existing = await _db.OrderCharges.Where(c => c.OrderId == order.Id).ToListAsync();
        var choice = existing.Where(c => c.IsPatientChoice).Select(c => c.ProfileId ?? c.TestId).Where(x => x != null).Select(x => x!).ToHashSet();
        var (terms, retail) = await TermsAsync(payer, order.OrderDatetime);
        var quote = PriceCalculator.Calculate(await ItemsAsync(order.Tests, choice), terms, retail);
        _db.OrderCharges.RemoveRange(existing);
        order.Charges.Clear();
        foreach (var l in quote.Lines)
            order.Charges.Add(new LabOrderCharge
            {
                OrderId = order.Id, ProfileId = l.ProfileId, TestId = l.TestId, PackageId = l.PackageId, Code = l.Code,
                Name = l.PackageMembers.Count > 0 ? $"{l.Name} ({string.Join(", ", l.PackageMembers)})" : l.Name,
                PayerId = l.PayerId, PayerKind = l.PayerKind, PriceListId = l.PriceListId, Amount = l.Amount, PayerAmount = l.PayerAmount,
                PatientAmount = l.PatientAmount, IsNotCovered = l.IsNotCovered, IsPatientChoice = l.IsPatientChoice
            });
        foreach (var c in order.Charges) if (_db.Entry(c).State == EntityState.Detached) _db.OrderCharges.Add(c);
        order.TotalPrice = quote.Total;
        order.PayerAmount = quote.PayerTotal;
        order.PatientAmount = quote.PatientTotal;
    }

    /// <summary>Попередній розрахунок для діалогу замовлення (нічого не зберігає).</summary>
    public async Task<object> QuoteAsync(QuoteRequest req, TubePlanService tubes)
    {
        var (selected, profiles, _) = await tubes.ResolveAsync(req.ProfileIds, req.TestIds, new HashSet<string>());
        var fake = new LabOrder { PatientId = req.PatientId ?? "", OrderDatetime = DateTime.UtcNow };
        if (!string.IsNullOrWhiteSpace(req.EhealthReferralNumber))
            fake.EhealthReferralId = await _db.Referrals.Where(r => r.RegNumber == req.EhealthReferralNumber || r.Id == req.EhealthReferralNumber).Select(r => r.Id).FirstOrDefaultAsync();
        string? warning = null;
        try { await ApplyPayerAsync(fake, req.PayerId, null, "preview"); }
        catch (Exception ex) when (ex is ConflictException or ValidationException) { warning = ex.Message; fake.PayerId = (await PatientPayerAsync()).Id; }
        var payer = await _db.Payers.AsNoTracking().FirstAsync(p => p.Id == fake.PayerId);
        var (terms, retail) = await TermsAsync(payer, fake.OrderDatetime);
        var tests = selected.Select(s => new LabOrderTest { TestId = s.Test.Id, TestCode = s.Test.Code, TestName = s.Test.Name, ProfileId = s.Profile?.Id }).ToList();
        var quote = PriceCalculator.Calculate(await ItemsAsync(tests, new HashSet<string>()), terms, retail);
        return new
        {
            payerId = payer.Id, payerName = payer.Name, payerKind = payer.Kind, policyNumber = fake.InsurancePolicyNumber, warning,
            total = quote.Total, payerAmount = quote.PayerTotal, patientAmount = quote.PatientTotal, franchise = quote.FranchiseApplied,
            lines = quote.Lines.Select(l => new { l.Code, l.Name, l.Amount, l.PayerAmount, l.PatientAmount, l.IsNotCovered, l.IsPatientChoice, isPackage = l.PackageId != null, packageMembers = l.PackageMembers })
        };
    }

    // ------------------------------------------------------------------ операції із замовленням
    private async Task<LabOrder> LoadOrderAsync(string orderId) =>
        await _db.Orders.Include(o => o.Tests).Include(o => o.Charges).FirstOrDefaultAsync(o => o.Id == orderId && o.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Замовлення", orderId);

    public async Task<object> SetPayerAsync(string orderId, SetPayerRequest req)
    {
        _policy.Require("Зміна платника замовлення", Cashiers.Append(LabRoles.Doctor).ToArray());
        var order = await LoadOrderAsync(orderId);
        if (order.Status == OrderStatuses.Cancelled) throw new ConflictException("Замовлення скасовано");
        if (await _db.Invoices.AnyAsync(i => i.OrderId == order.Id && i.Status != "CANCELLED")) throw new ConflictException("За замовленням виставлено рахунок платнику — спершу анулюйте рахунок");
        var before = new { order.PayerId, order.PatientAmount };
        await ApplyPayerAsync(order, req.PayerId, req.PatientInsuranceId, req.AuthorizationNumber);
        await RecalculateAsync(order);
        _audit.Log("SET_PAYER", "lab_order", order.Id, before, new { order.PayerId, order.InsurancePolicyNumber, order.PatientAmount, order.PayerAmount });
        await _db.SaveChangesAsync();
        return await BillingAsync(orderId);
    }

    /// <summary>Пацієнт обрав сплатити позицію сам (або повернути на платника).</summary>
    public async Task<object> SetPatientChoiceAsync(string orderId, string chargeId, bool patientPays)
    {
        _policy.Require("Зміна оплати позиції", Cashiers);
        var order = await LoadOrderAsync(orderId);
        var c = order.Charges.FirstOrDefault(x => x.Id == chargeId) ?? throw NotFoundException.For("Нарахування", chargeId);
        if (c.PackageId != null) throw new ConflictException("Пакет не можна розділити — оберіть окремі послуги");
        c.IsPatientChoice = patientPays;
        await _db.SaveChangesAsync();
        await RecalculateAsync(order);
        _audit.Log("PATIENT_CHOICE", "lab_order_charge", chargeId, null, new { c.Code, patientPays });
        await _db.SaveChangesAsync();
        return await BillingAsync(orderId);
    }

    public async Task<object> AddPaymentAsync(string orderId, PaymentRequest req)
    {
        _policy.Require("Прийом оплати", Cashiers);
        var order = await LoadOrderAsync(orderId);
        var method = (req.Method ?? "CASH").ToUpperInvariant();
        if (method is not ("CASH" or "CARD" or "TRANSFER" or "REFUND")) throw ValidationException.Field("method", "CASH | CARD | TRANSFER | REFUND");
        if (req.Amount <= 0) throw ValidationException.Field("amount", "Сума має бути більшою за 0");
        var signed = method == "REFUND" ? -req.Amount : req.Amount;
        if (signed > 0 && order.PaidAmount + signed > order.PatientAmount) throw new ConflictException($"Сума перевищує борг пацієнта ({order.PatientAmount - order.PaidAmount:0.00} грн)");
        if (signed < 0 && -signed > order.PaidAmount) throw new ConflictException("Сума повернення перевищує сплачене");
        var now = DateTime.UtcNow;
        var p = new LabPayment { OrderId = order.Id, Amount = signed, Method = method, PaidAt = now, CashierId = _current.EmployeeId, Note = req.Note, ReceiptNumber = await NumberAsync("Q", now) };
        _db.Payments.Add(p);
        order.PaidAmount += signed;
        _audit.Log("PAYMENT", "lab_payment", p.Id, null, new { order.OrderNumber, p.Amount, p.Method, p.ReceiptNumber });
        await _db.SaveChangesAsync();
        return await BillingAsync(orderId);
    }

    /// <summary>Рахунок платнику за замовленням (страхова/підприємство/клініка; для НСЗУ — реєстр, сума може бути 0).</summary>
    public async Task<object> IssueInvoiceAsync(string orderId)
    {
        _policy.Require("Виставлення рахунку платнику", Cashiers.Append(LabRoles.Doctor).ToArray());
        var order = await LoadOrderAsync(orderId);
        if (order.PayerId == null) throw new ConflictException("Замовлення без платника");
        var payer = await _db.Payers.FirstAsync(p => p.Id == order.PayerId);
        if (payer.Kind == PayerKinds.Patient) throw new ConflictException("Платник — пацієнт: рахунок не потрібен (замовлення-квитанція)");
        if (await _db.Invoices.AnyAsync(i => i.OrderId == order.Id && i.Status != "CANCELLED")) throw new ConflictException("Рахунок уже виставлено");
        var charges = order.Charges.Where(c => c.PayerId == payer.Id).ToList();
        var inv = new LabInvoice
        {
            Number = await NumberAsync("INV", DateTime.UtcNow), PayerId = payer.Id, OrderId = order.Id, Amount = charges.Sum(c => c.PayerAmount),
            ChargeIdsJson = System.Text.Json.JsonSerializer.Serialize(charges.Select(c => c.Id))
        };
        _db.Invoices.Add(inv);
        _audit.Log("INVOICE", "lab_invoice", inv.Id, null, new { inv.Number, payer = payer.Code, inv.Amount, order.OrderNumber });
        await _db.SaveChangesAsync();
        return new { inv.Id, inv.Number, inv.Amount, inv.Status, inv.IssuedAt, payerName = payer.Name };
    }

    private async Task<string> NumberAsync(string prefix, DateTime at) => $"{prefix}-{at:yyMM}-{await _numerators.NextCounterAsync("lab_" + prefix.ToLowerInvariant() + ":" + at.ToString("yyMM")):00000}";

    public async Task<object> BillingAsync(string orderId)
    {
        var order = await _db.Orders.AsNoTracking().Include(o => o.Payer).Include(o => o.Charges).ThenInclude(c => c.Payer).FirstOrDefaultAsync(o => o.Id == orderId) ?? throw NotFoundException.For("Замовлення", orderId);
        var payments = await _db.Payments.AsNoTracking().Where(p => p.OrderId == orderId).OrderBy(p => p.PaidAt).ToListAsync();
        var invoices = await _db.Invoices.AsNoTracking().Where(i => i.OrderId == orderId).ToListAsync();
        return new
        {
            order.Id, order.OrderNumber, payerId = order.PayerId, payerName = order.Payer?.Name, payerKind = order.Payer?.Kind, payerKindName = order.Payer == null ? null : PayerKinds.Label(order.Payer.Kind),
            order.InsurancePolicyNumber, order.AuthorizationNumber, total = order.TotalPrice, order.PayerAmount, order.PatientAmount, order.PaidAmount, due = order.PatientAmount - order.PaidAmount,
            charges = order.Charges.OrderBy(c => c.Code).Select(c => new { c.Id, c.Code, c.Name, c.Amount, c.PayerAmount, c.PatientAmount, payerName = c.Payer?.Name, c.PayerKind, c.IsNotCovered, c.IsPatientChoice, isPackage = c.PackageId != null }),
            payments = payments.Select(p => new { p.Id, p.Amount, p.Method, p.ReceiptNumber, p.PaidAt }),
            invoices = invoices.Select(i => new { i.Id, i.Number, i.Amount, i.Status, i.IssuedAt })
        };
    }

    // ------------------------------------------------------------------ друковані документи
    /// <summary>Замовлення-квитанція пацієнту: усі позиції, частка платника, до сплати, сплачено.</summary>
    public async Task<string> OrderReceiptHtmlAsync(string orderId)
    {
        var o = await _db.Orders.AsNoTracking().Include(x => x.Patient).Include(x => x.Payer).Include(x => x.Charges).Include(x => x.Referral).Include(x => x.PaperReferral).FirstOrDefaultAsync(x => x.Id == orderId) ?? throw NotFoundException.For("Замовлення", orderId);
        var lab = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var payments = await _db.Payments.AsNoTracking().Where(p => p.OrderId == orderId).ToListAsync();
        var sb = Head($"Замовлення {o.OrderNumber}", lab);
        sb.Append($"<h2>Замовлення № {H(o.OrderNumber)} від {o.OrderDatetime.ToLocalTime():dd.MM.yyyy HH:mm}</h2>");
        sb.Append($"<p><b>Пацієнт:</b> {H(o.Patient?.Caption)} · <b>Направлення:</b> {H(ReferralTypes.Label(o.ReferralType))} {H(o.Referral?.RegNumber ?? o.PaperReferral?.RegNumber ?? o.ReferrerNumber)}<br>");
        sb.Append($"<b>Платник:</b> {H(o.Payer?.Name ?? "Пацієнт")}{(o.InsurancePolicyNumber != null ? $" · поліс № {H(o.InsurancePolicyNumber)}" : "")}{(o.AuthorizationNumber != null ? $" · гарантійний лист № {H(o.AuthorizationNumber)}" : "")}</p>");
        sb.Append("<table><tr><th>Код</th><th>Послуга</th><th>Вартість</th><th>Сплачує платник</th><th>До сплати пацієнтом</th><th>Примітка</th></tr>");
        foreach (var c in o.Charges.OrderBy(c => c.Code))
            sb.Append($"<tr><td>{H(c.Code)}</td><td>{H(c.Name)}</td><td class=\"r\">{M(c.Amount)}</td><td class=\"r\">{M(c.PayerAmount)}</td><td class=\"r\">{M(c.PatientAmount)}</td><td>{(c.IsNotCovered ? "не покривається програмою" : c.IsPatientChoice ? "за власним бажанням" : c.PayerKind is PayerKinds.Nszu ? "безоплатно (ПМГ)" : "")}</td></tr>");
        sb.Append($"<tr class=\"t\"><td></td><td>Разом</td><td class=\"r\">{M(o.TotalPrice)}</td><td class=\"r\">{M(o.PayerAmount)}</td><td class=\"r\">{M(o.PatientAmount)}</td><td></td></tr></table>");
        sb.Append($"<p><b>Сплачено:</b> {M(o.PaidAmount)} грн{string.Concat(payments.Select(p => $" · {H(p.ReceiptNumber)} {M(p.Amount)} ({H(p.Method)})"))}<br><b>До сплати:</b> {M(o.PatientAmount - o.PaidAmount)} грн</p>");
        sb.Append("<p class=\"s\">Документ не є фіскальним чеком. Фіскальний чек видає РРО/ПРРО каси закладу.</p></body></html>");
        return sb.ToString();
    }

    /// <summary>Рахунок платнику (страхова/підприємство/клініка) за замовленням.</summary>
    public async Task<string> InvoiceHtmlAsync(string invoiceId)
    {
        var inv = await _db.Invoices.AsNoTracking().Include(i => i.Payer).FirstOrDefaultAsync(i => i.Id == invoiceId || i.Number == invoiceId) ?? throw NotFoundException.For("Рахунок", invoiceId);
        var ids = System.Text.Json.JsonSerializer.Deserialize<List<string>>(inv.ChargeIdsJson) ?? new();
        var charges = await _db.OrderCharges.AsNoTracking().Include(c => c.Order).ThenInclude(o => o!.Patient).Where(c => ids.Contains(c.Id)).OrderBy(c => c.Order!.OrderNumber).ToListAsync();
        var lab = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var sb = Head($"Рахунок {inv.Number}", lab);
        sb.Append($"<h2>Рахунок № {H(inv.Number)} від {inv.IssuedAt.ToLocalTime():dd.MM.yyyy}</h2>");
        sb.Append($"<p><b>Платник:</b> {H(inv.Payer?.OrganizationName ?? inv.Payer?.Name)} (ЄДРПОУ {H(inv.Payer?.Edrpou)}) · програма/договір: {H(inv.Payer?.Name)} {H(inv.Payer?.ContractNumber)}</p>");
        sb.Append("<table><tr><th>Замовлення</th><th>Пацієнт</th><th>Поліс</th><th>Код</th><th>Послуга</th><th>Сума</th></tr>");
        foreach (var c in charges)
            sb.Append($"<tr><td>{H(c.Order?.OrderNumber)}</td><td>{H(c.Order?.Patient?.Caption)}</td><td>{H(c.Order?.InsurancePolicyNumber)}</td><td>{H(c.Code)}</td><td>{H(c.Name)}</td><td class=\"r\">{M(c.PayerAmount)}</td></tr>");
        sb.Append($"<tr class=\"t\"><td colspan=\"5\">Разом до сплати</td><td class=\"r\">{M(inv.Amount)}</td></tr></table>");
        sb.Append($"<p>Отримувач: {H(lab.Name)}, ЄДРПОУ {H(lab.Edrpou)}. Без ПДВ (медичні послуги, ст. 197 ПКУ).</p></body></html>");
        return sb.ToString();
    }

    private static StringBuilder Head(string title, LabSettings lab)
    {
        var sb = new StringBuilder();
        sb.Append($"<!doctype html><html><head><meta charset=\"utf-8\"><title>{H(title)}</title><style>body{{font-family:Arial,sans-serif;font-size:12px;margin:24px}}table{{border-collapse:collapse;width:100%}}td,th{{border:1px solid #999;padding:4px}}.r{{text-align:right}}.t td{{font-weight:bold}}.s{{color:#666;font-size:10px}}</style></head><body>");
        sb.Append($"<div><b>{H(lab.Name)}</b><br><span class=\"s\">{H(lab.Address)} · {H(lab.Phone)} · ЄДРПОУ {H(lab.Edrpou)}</span></div>");
        return sb;
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string M(decimal v) => v.ToString("N2", CultureInfo.GetCultureInfo("uk-UA"));

    // ------------------------------------------------------------------ довідники: платники, прайси, поліси
    public async Task<List<object>> PayersAsync() =>
        (await _db.Payers.AsNoTracking().Include(p => p.PriceList).Where(p => p.RecordState != RecordStates.Deleted).OrderBy(p => p.Kind).ThenBy(p => p.Name).ToListAsync())
        .Select(p => (object)new
        {
            p.Id, p.Code, p.Name, p.Kind, kindName = PayerKinds.Label(p.Kind), p.OrganizationName, p.Edrpou, p.ContractNumber, p.ContractDate, p.ContractValidTo, p.PriceListId,
            priceListName = p.PriceList?.Name, p.CoveragePct, p.FranchiseAmount, p.RequiresPolicy, p.RequiresAuthorization, p.MedicalProgramId, p.MedlinkContractId,
            p.ContactPerson, p.Phone, p.Email, p.IsActive
        }).ToList();

    public async Task<object> SavePayerAsync(string? id, LabPayer req)
    {
        _policy.Require("Довідник платників", LabRoles.Admin);
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Код і назва платника обов'язкові");
        if (!PayerKinds.All.Contains(req.Kind)) throw ValidationException.Field("kind", string.Join(", ", PayerKinds.All));
        if (req.CoveragePct < 0 || req.CoveragePct > 100) throw ValidationException.Field("coveragePct", "0–100");
        var code = req.Code.Trim().ToUpperInvariant();
        if (await _db.Payers.AnyAsync(p => p.Code == code && p.Id != id && p.RecordState != RecordStates.Deleted)) throw new ConflictException($"Платник з кодом {code} вже існує");
        if (req.PriceListId != null && !await _db.PriceLists.AnyAsync(l => l.Id == req.PriceListId)) throw ValidationException.Field("priceListId", "Прайс-лист не знайдено");
        LabPayer p;
        if (id == null) { p = new LabPayer(); _db.Payers.Add(p); }
        else p = await _db.Payers.FirstOrDefaultAsync(x => (x.Id == id || x.Code == id) && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Платник", id);
        p.Code = code; p.Name = req.Name.Trim(); p.Kind = req.Kind; p.OrganizationName = req.OrganizationName; p.Edrpou = req.Edrpou; p.ContractNumber = req.ContractNumber;
        p.ContractDate = req.ContractDate; p.ContractValidTo = req.ContractValidTo; p.PriceListId = req.PriceListId; p.CoveragePct = req.CoveragePct; p.FranchiseAmount = req.FranchiseAmount;
        p.RequiresPolicy = req.RequiresPolicy; p.RequiresAuthorization = req.RequiresAuthorization; p.MedicalProgramId = req.MedicalProgramId; p.MedlinkContractId = req.MedlinkContractId;
        p.ContactPerson = req.ContactPerson; p.Phone = req.Phone; p.Email = req.Email; p.IsActive = req.IsActive;
        _audit.Log(id == null ? "CREATE" : "UPDATE", "lab_payer", p.Id, null, new { p.Code, p.Name, p.Kind, p.CoveragePct, p.FranchiseAmount });
        await _db.SaveChangesAsync();
        return (await PayersAsync()).First(x => ((dynamic)x).Id == p.Id);
    }

    public async Task<List<object>> PriceListsAsync() =>
        (await _db.PriceLists.AsNoTracking().Include(l => l.Items).Include(l => l.Packages).Where(l => l.RecordState != RecordStates.Deleted).OrderByDescending(l => l.IsDefault).ThenBy(l => l.Name).ToListAsync())
        .Select(l => (object)new
        {
            l.Id, l.Code, l.Name, l.ValidFrom, l.ValidTo, l.IsDefault, l.Currency, l.IsActive, itemsCount = l.Items.Count,
            items = l.Items.Select(i => new { i.Id, i.ProfileId, i.TestId, i.Price }),
            packages = l.Packages.Where(p => p.RecordState != RecordStates.Deleted).Select(p => new { p.Id, p.Code, p.Name, p.Price, members = p.MemberIds, p.IsActive })
        }).ToList();

    public async Task<object> SavePriceListAsync(string? id, PriceListRequest req)
    {
        _policy.Require("Прайс-листи", LabRoles.Admin);
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Код і назва прайс-листа обов'язкові");
        var code = req.Code.Trim().ToUpperInvariant();
        if (await _db.PriceLists.AnyAsync(l => l.Code == code && l.Id != id && l.RecordState != RecordStates.Deleted)) throw new ConflictException($"Прайс-лист {code} вже існує");
        LabPriceList l;
        if (id == null) { l = new LabPriceList(); _db.PriceLists.Add(l); }
        else l = await _db.PriceLists.Include(x => x.Items).Include(x => x.Packages).FirstOrDefaultAsync(x => (x.Id == id || x.Code == id) && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Прайс-лист", id);
        l.Code = code; l.Name = req.Name.Trim(); l.ValidFrom = req.ValidFrom; l.ValidTo = req.ValidTo; l.IsActive = req.IsActive;
        if (req.IsDefault)
        {
            foreach (var other in await _db.PriceLists.Where(x => x.IsDefault && x.Id != l.Id).ToListAsync()) other.IsDefault = false;
            l.IsDefault = true;
        }
        else l.IsDefault = false;
        var ids = await ResolveIdsAsync((req.Items?.Keys ?? Enumerable.Empty<string>()).Concat(req.Packages?.SelectMany(p => p.Members) ?? Enumerable.Empty<string>()));
        if (req.Items != null)
        {
            _db.PriceListItems.RemoveRange(l.Items);
            l.Items = req.Items.Select(kv => { var (pid, tid) = ids[kv.Key]; return new LabPriceListItem { PriceListId = l.Id, ProfileId = pid, TestId = tid, Price = kv.Value }; }).ToList();
        }
        if (req.Packages != null)
        {
            _db.PricePackages.RemoveRange(l.Packages);
            l.Packages = req.Packages.Select(p => new LabPricePackage
            {
                PriceListId = l.Id, Code = p.Code.Trim().ToUpperInvariant(), Name = p.Name.Trim(), Price = p.Price, MemberIds = p.Members.Select(m => ids[m].ProfileId ?? ids[m].TestId!).ToList()
            }).ToList();
            if (l.Packages.Any(p => p.MemberIds.Count < 2)) throw new ValidationException("Пакет має містити щонайменше дві послуги");
        }
        _audit.Log(id == null ? "CREATE" : "UPDATE", "lab_price_list", l.Id, null, new { l.Code, items = l.Items.Count, packages = l.Packages.Count });
        await _db.SaveChangesAsync();
        return (await PriceListsAsync()).First(x => ((dynamic)x).Id == l.Id);
    }

    /// <summary>id/код → (profileId, testId).</summary>
    private async Task<Dictionary<string, (string? ProfileId, string? TestId)>> ResolveIdsAsync(IEnumerable<string> keys)
    {
        var list = keys.Distinct().ToList();
        var profiles = await _db.Profiles.AsNoTracking().Where(p => list.Contains(p.Id) || list.Contains(p.Code)).Select(p => new { p.Id, p.Code }).ToListAsync();
        var tests = await _db.Tests.AsNoTracking().Where(t => list.Contains(t.Id) || list.Contains(t.Code)).Select(t => new { t.Id, t.Code }).ToListAsync();
        var map = new Dictionary<string, (string?, string?)>();
        foreach (var k in list)
        {
            var p = profiles.FirstOrDefault(x => x.Id == k || x.Code == k);
            if (p != null) { map[k] = (p.Id, null); continue; }
            var t = tests.FirstOrDefault(x => x.Id == k || x.Code == k) ?? throw ValidationException.Field("items", $"Послугу/показник «{k}» не знайдено");
            map[k] = (null, t.Id);
        }
        return map;
    }

    public async Task<List<object>> PatientInsurancesAsync(string patientId) =>
        (await _db.PatientInsurances.AsNoTracking().Include(p => p.Payer).Where(p => p.PatientCardId == patientId && p.RecordState != RecordStates.Deleted).OrderByDescending(p => p.ValidTo).ToListAsync())
        .Select(p => (object)new { p.Id, p.PayerId, payerName = p.Payer?.Name, p.PolicyNumber, p.ValidFrom, p.ValidTo, p.Note, p.IsActive, isValid = p.IsValidAt(DateTime.UtcNow) }).ToList();

    public async Task<object> SavePatientInsuranceAsync(string patientId, string? id, PatientInsuranceRequest req)
    {
        _policy.Require("Поліси пацієнта", Cashiers.Append(LabRoles.Doctor).ToArray());
        if (string.IsNullOrWhiteSpace(req.PolicyNumber)) throw ValidationException.Field("policyNumber", "Вкажіть номер полісу");
        var payer = await _db.Payers.FirstOrDefaultAsync(p => (p.Id == req.PayerId || p.Code == req.PayerId) && p.Kind == PayerKinds.Insurance) ?? throw ValidationException.Field("payerId", "Страхову програму не знайдено");
        if (!await _db.Patients.AnyAsync(p => p.Id == patientId)) throw NotFoundException.For("Пацієнт", patientId);
        MisPatientInsurance ins;
        if (id == null) { ins = new MisPatientInsurance { PatientCardId = patientId }; _db.PatientInsurances.Add(ins); }
        else ins = await _db.PatientInsurances.FirstOrDefaultAsync(x => x.Id == id && x.PatientCardId == patientId) ?? throw NotFoundException.For("Поліс", id);
        ins.PayerId = payer.Id; ins.PolicyNumber = req.PolicyNumber.Trim(); ins.ValidFrom = req.ValidFrom; ins.ValidTo = req.ValidTo; ins.Note = req.Note; ins.IsActive = req.IsActive;
        _audit.Log(id == null ? "CREATE" : "UPDATE", "mis_patient_insurance", ins.Id, null, new { ins.PolicyNumber, payer = payer.Code });
        await _db.SaveChangesAsync();
        return (await PatientInsurancesAsync(patientId)).First(x => ((dynamic)x).Id == ins.Id);
    }
}
