// =============================================================================
// FR-REF-001. Типи направлень і облік: е-направлення ЕСОЗ, паперове, внутрішнє, клініка-партнер, самозвернення.
// Життєвий цикл е-направлення у статусах evomis: перевірка → взяти в роботу → погасити висновком → звільнити при скасуванні.
// Операції ЕСОЗ інкапсульовано в IEhealthReferralGateway: в автономній ЛІС — емулятор, що оновлює дзеркальні колонки
// ehe_incoming_medical_referral; у MedLink — штатні EhealthIncomingMedicalReferralService.Use/Complete/Release (ТЗ 20.3.2, п. 5–6).
// =============================================================================
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

/// <summary>Операції з е-направленням в ЕСОЗ (виконавець): виклик API ЕСОЗ + оновлення дзеркала МІС після успіху (як в evomis).</summary>
public interface IEhealthReferralGateway
{
    /// <summary>Перевірити направлення в реєстрі ЕСОЗ: GET patients/service_requests?requisition=…</summary>
    Task<List<string>> CheckInRegistryAsync(EheIncomingMedicalReferral r);
    /// <summary>Взяти в роботу: PATCH patients/service_requests/{id}/actions/use.</summary>
    Task TakeInWorkAsync(EheIncomingMedicalReferral r, string employeeId, string organizationId, DateTime at);
    /// <summary>Погасити документом: PATCH patients/service_requests/{id}/actions/complete.</summary>
    Task CompleteAsync(EheIncomingMedicalReferral r, string entityId, string entityName, DateTime at);
    /// <summary>Звільнити: PATCH patients/service_requests/{id}/actions/release.</summary>
    Task ReleaseAsync(EheIncomingMedicalReferral r);
}

public sealed class EhealthReferralGateway : IEhealthReferralGateway
{
    private readonly MedLink.LIS.Api.Services.Ehealth.IEhealthClient _client;
    public EhealthReferralGateway(MedLink.LIS.Api.Services.Ehealth.IEhealthClient client) => _client = client;

    private static string EhealthId(EheIncomingMedicalReferral r) => r.EhealthId ?? r.Id;

    public async Task<List<string>> CheckInRegistryAsync(EheIncomingMedicalReferral r)
    {
        var found = await Call(() => _client.SearchServiceRequestsAsync(r.RegNumber ?? "", status: ""));
        var sr = found.FirstOrDefault(x => x.Id == EhealthId(r));
        if (sr == null) return new() { "Направлення не знайдено в реєстрі ЕСОЗ" };
        var problems = new List<string>();
        if (sr.Status != "active") problems.Add($"У ЕСОЗ направлення має статус «{sr.Status}»");
        return problems;
    }

    public async Task TakeInWorkAsync(EheIncomingMedicalReferral r, string employeeId, string organizationId, DateTime at)
    {
        await Call(() => _client.UseServiceRequestAsync(EhealthId(r), employeeId, organizationId));
        r.ProcessingStatusInEhealthId = MedLinkEnums.ProcessingInProgress; r.TakeInWorkDate = at;
    }

    public async Task CompleteAsync(EheIncomingMedicalReferral r, string entityId, string entityName, DateTime at)
    {
        await Call(() => _client.CompleteServiceRequestAsync(EhealthId(r), entityName == "DiagnosticReport" ? "diagnostic_report" : entityName.ToLowerInvariant(), entityId));
        r.StatusId = MedLinkEnums.ReferralStatusCompleted; r.ProcessingStatusInEhealthId = MedLinkEnums.ProcessingCompleted;
        r.CompleteDateInEhealth = at; r.CompletedWithItemEntityId = entityId; r.CompletedWithItemEntityName = entityName;
    }

    public async Task ReleaseAsync(EheIncomingMedicalReferral r)
    {
        await Call(() => _client.ReleaseServiceRequestAsync(EhealthId(r)));
        r.ProcessingStatusInEhealthId = MedLinkEnums.ProcessingNew; r.TakeInWorkDate = null;
    }

    private static async Task<T> Call<T>(Func<Task<T>> f)
    {
        try { return await f(); }
        catch (MedLink.LIS.Api.Services.Ehealth.EhealthException ex) { throw new ConflictException(ex.Message); }
    }
}

public sealed class PaperReferralRequest
{
    public string Number { get; set; } = "";
    public DateTime? Date { get; set; }
    public string? RequesterEmployeeName { get; set; }
    public string? RequesterLegalEntityName { get; set; }
    public string? RequesterLegalEntityEdrpou { get; set; }
    public string? Description { get; set; }
}

public sealed class ReferralService
{
    private readonly LisDbContext _db;
    private readonly IEhealthReferralGateway _gateway;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    public ReferralService(LisDbContext db, IEhealthReferralGateway gateway, IAuditService audit, ICurrentEmployee current) { _db = db; _gateway = gateway; _audit = audit; _current = current; }

    // ------------------------------------------------------------------ пошук і перевірка е-направлення
    public async Task<EheIncomingMedicalReferral?> FindEhealthAsync(string idOrNumber) =>
        await _db.Referrals.Include(r => r.ServiceCatalogService).Include(r => r.PatientCard).Include(r => r.Status).Include(r => r.ProcessingStatus)
            .FirstOrDefaultAsync(r => (r.Id == idOrNumber || r.RegNumber == idOrNumber) && r.RecordState != RecordStates.Deleted);

    /// <summary>Причини, з яких направлення не можна використати (порожньо — можна).</summary>
    public async Task<List<string>> ProblemsAsync(EheIncomingMedicalReferral r, string? patientId, string? exceptOrderId = null)
    {
        var problems = new List<string>();
        if (r.StatusId != MedLinkEnums.ReferralStatusActive) problems.Add($"Направлення не активне (статус: {r.Status?.Caption ?? r.StatusId})");
        if (r.ProcessingStatusInEhealthId == MedLinkEnums.ProcessingCompleted || r.CompleteDateInEhealth != null) problems.Add("Направлення вже погашено");
        if (r.ExpirationDate != null && r.ExpirationDate.Value.Date < DateTime.UtcNow.Date) problems.Add($"Термін дії направлення минув {r.ExpirationDate:dd.MM.yyyy}");
        if (patientId != null && r.PatientCardId != null && r.PatientCardId != patientId) problems.Add("Направлення виписано іншому пацієнту");
        if (r.MedicalReferralCategoryId != MedLinkDefaults.ReferralCategoryLaboratoryId) problems.Add("Категорія направлення не «лабораторне дослідження»");
        var used = await _db.Orders.AsNoTracking().Where(o => o.EhealthReferralId == r.Id && o.Id != exceptOrderId && o.Status != OrderStatuses.Cancelled && o.RecordState != RecordStates.Deleted)
            .Select(o => o.OrderNumber).FirstOrDefaultAsync();
        if (used != null) problems.Add($"Направлення вже використано в замовленні {used}");
        return problems;
    }

    /// <summary>Пошук е-направлення за номером для реєстратора: дані, придатність, профілі ЛІС для послуги направлення.</summary>
    public async Task<object> LookupAsync(string number, string? patientId)
    {
        var r = await FindEhealthAsync(number.Trim()) ?? throw NotFoundException.For("Е-направлення", number);
        var problems = await ProblemsAsync(r, string.IsNullOrWhiteSpace(patientId) ? null : patientId);
        problems.AddRange(await _gateway.CheckInRegistryAsync(r));
        var profiles = r.ServiceCatalogServiceId == null ? new() :
            await _db.Profiles.AsNoTracking().Where(p => p.EhealthServiceCatalogServiceId == r.ServiceCatalogServiceId && p.IsActive)
                .Select(p => new { p.Id, p.Code, p.Name }).ToListAsync();
        return new
        {
            r.Id, number = r.RegNumber, r.RegDate, r.ExpirationDate, status = r.Status?.Code, statusName = r.Status?.Caption,
            processingStatus = r.ProcessingStatus?.Code, r.TakeInWorkDate, r.CompleteDateInEhealth,
            patientId = r.PatientCardId, patientName = r.PatientCard?.Caption ?? r.PatientShortName,
            serviceCode = r.ServiceCatalogService?.Code, serviceName = r.ServiceCatalogService?.Name ?? r.Caption,
            canUse = problems.Count == 0, problems, matchingProfiles = profiles
        };
    }

    // ------------------------------------------------------------------ застосування до замовлення
    /// <summary>Визначає тип направлення, перевіряє обов'язкові реквізити, бере е-направлення в роботу / створює паперове.</summary>
    public async Task ApplyAsync(LabOrder order, CreateOrderRequest req, MisPatientCard patient)
    {
        var ehealthRef = !string.IsNullOrWhiteSpace(req.EhealthReferralId) ? req.EhealthReferralId : req.EhealthReferralNumber;
        var type = string.IsNullOrWhiteSpace(req.ReferralType)
            ? (ehealthRef != null ? ReferralTypes.Ehealth
               : req.PaperReferral != null ? ReferralTypes.Paper
               : !string.IsNullOrWhiteSpace(req.ReferrerOrganizationName) ? ReferralTypes.ExternalClinic
               : !string.IsNullOrWhiteSpace(req.DoctorId) ? ReferralTypes.Internal : ReferralTypes.Self)
            : req.ReferralType.Trim().ToUpperInvariant();
        if (!ReferralTypes.All.Contains(type)) throw ValidationException.Field("referralType", $"Тип направлення: {string.Join(", ", ReferralTypes.All)}");
        order.ReferralType = type;
        order.ReferrerOrganizationName = Clean(req.ReferrerOrganizationName);
        order.ReferrerOrganizationEdrpou = Clean(req.ReferrerOrganizationEdrpou);
        order.ReferrerDoctorName = Clean(req.ReferrerDoctorName);
        order.ReferrerNumber = Clean(req.ReferrerNumber);
        order.EhealthReferralId = null;

        switch (type)
        {
            case ReferralTypes.Ehealth:
            {
                if (ehealthRef == null) throw ValidationException.Field("ehealthReferralNumber", "Вкажіть номер е-направлення");
                var r = await FindEhealthAsync(ehealthRef) ?? throw ValidationException.Field("ehealthReferralNumber", $"Е-направлення {ehealthRef} не знайдено");
                var problems = await ProblemsAsync(r, patient.Id);
                if (problems.Count == 0) problems.AddRange(await _gateway.CheckInRegistryAsync(r)); // пошук у реєстрі ЕСОЗ перед use (як в evomis)
                if (problems.Count > 0) throw new ConflictException($"Е-направлення {r.RegNumber}: {string.Join("; ", problems)}");
                order.EhealthReferralId = r.Id;
                await _gateway.TakeInWorkAsync(r, Guid.TryParse(_current.EmployeeId, out _) ? _current.EmployeeId : MedLinkEnums.EmptyGuid, order.OrganizationId ?? MedLinkDefaults.OrganizationId, DateTime.UtcNow);
                _audit.Log("EHEALTH_REFERRAL_USE", "ehe_incoming_medical_referral", r.Id, null, new { r.RegNumber, order.OrderNumber });
                break;
            }
            case ReferralTypes.Paper:
            {
                var p = req.PaperReferral;
                if (p == null || string.IsNullOrWhiteSpace(p.Number)) throw ValidationException.Field("paperReferral.number", "Вкажіть номер паперового направлення");
                if (string.IsNullOrWhiteSpace(p.RequesterLegalEntityName) && string.IsNullOrWhiteSpace(p.RequesterEmployeeName))
                    throw ValidationException.Field("paperReferral.requesterLegalEntityName", "Вкажіть заклад або лікаря, що видав направлення");
                var paper = new EhePaperMedicalReferral
                {
                    RegNumber = p.Number.Trim(), RegDate = p.Date.HasValue ? DateTime.SpecifyKind(p.Date.Value, DateTimeKind.Utc) : DateTime.UtcNow, Caption = "Лабораторне дослідження",
                    OrganizationId = order.OrganizationId ?? MedLinkDefaults.OrganizationId, PatientCardId = patient.Id,
                    RequesterEmployeeName = Clean(p.RequesterEmployeeName), RequesterLegalEntityName = Clean(p.RequesterLegalEntityName),
                    RequesterLegalEntityEdrpou = Clean(p.RequesterLegalEntityEdrpou), Description = Clean(p.Description), Status = PaperReferralStatuses.Active
                };
                _db.PaperReferrals.Add(paper);
                order.PaperReferralId = paper.Id;
                order.ReferrerOrganizationName ??= paper.RequesterLegalEntityName;
                order.ReferrerOrganizationEdrpou ??= paper.RequesterLegalEntityEdrpou;
                order.ReferrerDoctorName ??= paper.RequesterEmployeeName;
                break;
            }
            case ReferralTypes.Internal:
                if (string.IsNullOrWhiteSpace(req.DoctorId)) throw ValidationException.Field("doctorId", "Для внутрішнього направлення вкажіть лікаря закладу");
                break;
            case ReferralTypes.ExternalClinic:
                if (string.IsNullOrWhiteSpace(order.ReferrerOrganizationName)) throw ValidationException.Field("referrerOrganizationName", "Вкажіть клініку-направника");
                break;
        }
    }

    /// <summary>Погашення направлення висновком при видачі замовлення.</summary>
    public async Task CompleteAsync(LabOrder order, MisDiagnosticReport report, DateTime at)
    {
        if (order.EhealthReferralId != null)
        {
            var r = await _db.Referrals.FirstOrDefaultAsync(x => x.Id == order.EhealthReferralId);
            if (r != null && r.CompleteDateInEhealth == null)
            {
                await _gateway.CompleteAsync(r, report.Id, "DiagnosticReport", at);
                _audit.Log("EHEALTH_REFERRAL_COMPLETE", "ehe_incoming_medical_referral", r.Id, null, new { r.RegNumber, reportId = report.Id });
            }
        }
        if (order.PaperReferralId != null)
        {
            var p = await _db.PaperReferrals.FirstOrDefaultAsync(x => x.Id == order.PaperReferralId);
            if (p != null) { p.Status = PaperReferralStatuses.Completed; p.ProcessedDate = at; }
        }
    }

    /// <summary>Звільнення е-направлення при скасуванні замовлення (можна використати повторно).</summary>
    public async Task ReleaseAsync(LabOrder order, string reason)
    {
        if (order.EhealthReferralId == null) return;
        var r = await _db.Referrals.FirstOrDefaultAsync(x => x.Id == order.EhealthReferralId);
        if (r == null || r.CompleteDateInEhealth != null) return;
        await _gateway.ReleaseAsync(r);
        _audit.Log("EHEALTH_REFERRAL_RELEASE", "ehe_incoming_medical_referral", r.Id, null, new { r.RegNumber, order.OrderNumber }, reason);
    }

    // ------------------------------------------------------------------ журнал
    public async Task<object> JournalAsync(DateTime? from, DateTime? to, string? type, string? search)
    {
        var q = _db.Orders.AsNoTracking().Include(o => o.Patient).Include(o => o.Doctor).Include(o => o.Department).Include(o => o.Referral).ThenInclude(r => r!.Status)
            .Include(o => o.PaperReferral).Where(o => o.RecordState != RecordStates.Deleted);
        if (from.HasValue) q = q.Where(o => o.OrderDatetime >= from.Value);
        if (to.HasValue) q = q.Where(o => o.OrderDatetime <= to.Value);
        if (!string.IsNullOrWhiteSpace(type)) { var t = type.Trim().ToUpperInvariant(); q = q.Where(o => o.ReferralType == t); }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(s) || (o.Patient!.Caption != null && o.Patient.Caption.Contains(s)) || (o.Referral != null && o.Referral.RegNumber != null && o.Referral.RegNumber.Contains(s))
                             || (o.PaperReferral != null && o.PaperReferral.RegNumber != null && o.PaperReferral.RegNumber.Contains(s)) || (o.ReferrerOrganizationName != null && o.ReferrerOrganizationName.Contains(s)));
        }
        var orders = await q.OrderByDescending(o => o.OrderDatetime).Take(1000).ToListAsync();
        var items = orders.Select(o => new
        {
            orderId = o.Id, o.OrderNumber, o.OrderDatetime, o.Status, o.ReferralType, referralTypeName = ReferralTypes.Label(o.ReferralType),
            patientName = o.Patient?.Caption, referralNumber = o.Referral?.RegNumber ?? o.PaperReferral?.RegNumber ?? o.ReferrerNumber,
            referralDate = o.Referral?.RegDate ?? o.PaperReferral?.RegDate,
            referrer = o.ReferralType switch
            {
                ReferralTypes.Internal => o.Doctor?.Caption,
                ReferralTypes.Ehealth => o.Referral?.PatientShortName == null ? "ЕСОЗ" : "ЕСОЗ",
                _ => string.Join(", ", new[] { o.ReferrerOrganizationName, o.ReferrerDoctorName }.Where(x => !string.IsNullOrWhiteSpace(x)))
            },
            ehealthStatus = o.Referral?.Status?.Caption, ehealthCompletedAt = o.Referral?.CompleteDateInEhealth, o.TotalPrice
        }).ToList();
        var summary = ReferralTypes.All.Select(t => new { type = t, name = ReferralTypes.Label(t), count = orders.Count(o => o.ReferralType == t), amount = orders.Where(o => o.ReferralType == t).Sum(o => o.TotalPrice) });
        return new { total = items.Count, summary, items };
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
