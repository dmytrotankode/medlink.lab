// =============================================================================
// FR-REF-001. Типи направлень і облік: е-направлення ЕСОЗ, паперове, внутрішнє, клініка-партнер, самозвернення.
// Життєвий цикл е-направлення: пошук у ЕСОЗ за номером (дзеркало ehe_incoming_medical_referral — лише кеш) → взяти в роботу (use, якщо є програма)
// → звільнити при скасуванні. Погашення (complete) виконується НЕ тут, а задачею outbox COMPLETE_SR після реєстрації звіту в ЕСОЗ.
// Операції ЕСОЗ інкапсульовано в IEhealthReferralGateway (ТЗ 20.3.2, 21.13).
// =============================================================================
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

/// <summary>Операції з е-направленням в ЕСОЗ (виконавець). Погашення (complete) тут НЕМАЄ: воно виконується окремою задачею outbox
/// COMPLETE_SR лише після того, як діагностичний звіт зареєстровано в ЕСОЗ (див. Services/Ehealth/EhealthOutbox.cs).</summary>
public interface IEhealthReferralGateway
{
    /// <summary>Чи можна опитувати реєстр ЕСОЗ (Mock/Replay — завжди; Live — прапорець SearchRead).</summary>
    bool SearchEnabled { get; }
    /// <summary>Пошук за номером у реєстрі ЕСОЗ: GET /api/service_requests?requisition=… Повертає null, якщо ЕСОЗ направлення не знає.</summary>
    Task<MedLink.LIS.Api.Services.Ehealth.EhealthServiceRequest?> FindAsync(string requisition);
    /// <summary>Взяти в роботу: PATCH /api/service_requests/{id}/actions/use (для направлень без програми — лише якщо Ehealth:UseWithoutProgram).</summary>
    Task TakeInWorkAsync(EheIncomingMedicalReferral r, string employeeId, string organizationId, DateTime at);
    /// <summary>Звільнити: PATCH /api/service_requests/{id}/actions/release (лише якщо use виконувався).</summary>
    Task ReleaseAsync(EheIncomingMedicalReferral r);
}

public sealed class EhealthReferralGateway : IEhealthReferralGateway
{
    private readonly MedLink.LIS.Api.Services.Ehealth.IEhealthClient _client;
    private readonly MedLink.LIS.Api.Services.Ehealth.EhealthOptions _options;
    private readonly MedLink.LIS.Api.Services.Ehealth.EhealthIdentity _identity;
    public EhealthReferralGateway(MedLink.LIS.Api.Services.Ehealth.IEhealthClient client, MedLink.LIS.Api.Services.Ehealth.EhealthOptions options, MedLink.LIS.Api.Services.Ehealth.EhealthIdentity identity)
    { _client = client; _options = options; _identity = identity; }

    private static string EhealthId(EheIncomingMedicalReferral r) => r.EhealthId ?? r.Id;
    public bool SearchEnabled => _options.IsAllowed(MedLink.LIS.Api.Services.Ehealth.EhealthOperation.SearchRead);

    public async Task<MedLink.LIS.Api.Services.Ehealth.EhealthServiceRequest?> FindAsync(string requisition)
    {
        var found = await Call(() => _client.SearchServiceRequestsAsync(requisition, status: ""));
        return found.FirstOrDefault(x => x.Requisition == requisition) ?? found.FirstOrDefault();
    }

    public async Task TakeInWorkAsync(EheIncomingMedicalReferral r, string employeeId, string organizationId, DateTime at)
    {
        // За схемою OpenAPI program обов'язкова в use; для направлень без програми use імовірно не застосовується (потребує перевірки на preprod)
        if (r.EhealthProgramId != null || _options.UseWithoutProgram)
        {
            if (!_options.IsAllowed(MedLink.LIS.Api.Services.Ehealth.EhealthOperation.Use)) throw new ConflictException("Операцію ЕСОЗ «взяти в роботу» вимкнено (Ehealth:Live:Operations:Use)");
            var emp = _identity.EmployeeId(employeeId) ?? throw new ConflictException("Не налаштовано ідентифікатор працівника в ЕСОЗ (Ehealth:DefaultEmployeeId)");
            var le = _identity.LegalEntityId(organizationId) ?? throw new ConflictException("Не налаштовано ідентифікатор закладу в ЕСОЗ (Ehealth:LegalEntityId)");
            var job = await Call(async () => await _client.WaitForJobAsync(await _client.UseServiceRequestAsync(EhealthId(r), emp, le, r.EhealthProgramId, _identity.DivisionId())));
            if (job.IsFailed) throw new ConflictException($"ЕСОЗ не взяла направлення в роботу: {job.Error ?? job.RawStatus}");
            r.UsedInEhealth = true;
        }
        r.ProcessingStatusInEhealthId = MedLinkEnums.ProcessingInProgress; r.TakeInWorkDate = at;
    }

    public async Task ReleaseAsync(EheIncomingMedicalReferral r)
    {
        if (r.UsedInEhealth)
        {
            var job = await Call(async () => await _client.WaitForJobAsync(await _client.ReleaseServiceRequestAsync(EhealthId(r))));
            if (job.IsFailed) throw new ConflictException($"ЕСОЗ не звільнила направлення: {job.Error ?? job.RawStatus}");
            r.UsedInEhealth = false;
        }
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
    private readonly MedLink.LIS.Api.Services.Ehealth.EhealthIdentity _identity;

    public ReferralService(LisDbContext db, IEhealthReferralGateway gateway, IAuditService audit, ICurrentEmployee current, MedLink.LIS.Api.Services.Ehealth.EhealthIdentity identity)
    { _db = db; _gateway = gateway; _audit = audit; _current = current; _identity = identity; }

    // ------------------------------------------------------------------ пошук і перевірка е-направлення
    /// <summary>Пошук у локальному дзеркалі (кеш) за id або номером.</summary>
    public async Task<EheIncomingMedicalReferral?> FindEhealthAsync(string idOrNumber) =>
        await _db.Referrals.Include(r => r.ServiceCatalogService).Include(r => r.PatientCard).Include(r => r.Status).Include(r => r.ProcessingStatus)
            .FirstOrDefaultAsync(r => (r.Id == idOrNumber || r.RegNumber == idOrNumber) && r.RecordState != RecordStates.Deleted);

    /// <summary>Результат пошуку: рядок дзеркала, відповідь ЕСОЗ (null — кеш без перевірки) і причини, що перешкоджають використанню.</summary>
    public sealed record ResolvedReferral(EheIncomingMedicalReferral Referral, MedLink.LIS.Api.Services.Ehealth.EhealthServiceRequest? Remote, List<string> Problems);

    /// <summary>
    /// Спочатку запит у ЕСОЗ (GET /api/service_requests?requisition=…), далі upsert дзеркала. Дзеркало — кеш: направлення, якого ще немає в БД,
    /// знаходиться через ЕСОЗ. Якщо опитування реєстру вимкнено (Live без SearchRead) — працюємо з кешем.
    /// </summary>
    public async Task<ResolvedReferral> ResolveAsync(string idOrNumber)
    {
        idOrNumber = idOrNumber.Trim();
        var local = await FindEhealthAsync(idOrNumber);
        var number = local?.RegNumber ?? idOrNumber;
        var problems = new List<string>();
        if (_gateway.SearchEnabled)
        {
            var remote = await _gateway.FindAsync(number);
            if (remote != null)
            {
                var r = await UpsertAsync(local, remote, problems);
                problems.AddRange(RemoteProblems(remote));
                return new ResolvedReferral(r, remote, problems);
            }
            if (local == null) throw new NotFoundException($"Е-направлення {number} не знайдено в реєстрі ЕСОЗ");
            problems.Add("Направлення не знайдено в реєстрі ЕСОЗ");
            return new ResolvedReferral(local, null, problems);
        }
        if (local == null) throw NotFoundException.For("Е-направлення", number);
        return new ResolvedReferral(local, null, problems);
    }

    private IEnumerable<string> RemoteProblems(MedLink.LIS.Api.Services.Ehealth.EhealthServiceRequest sr)
    {
        if (sr.Status != "active") yield return $"У ЕСОЗ направлення має статус «{sr.Status}»";
        if (sr.Category != null && sr.Category is not ("laboratory_procedure" or "108252007")) yield return "Категорія направлення в ЕСОЗ не «лабораторна процедура»";
        if (sr.RemainingQuantity is <= 0) yield return "Залишок послуг за направленням вичерпано";
        var me = _identity.LegalEntityId(null);
        if (sr.UsedByLegalEntity != null && sr.UsedByLegalEntity != me && sr.Status == "active") yield return "Направлення вже використовується іншим закладом";
    }

    /// <summary>Знеособлена перевірка пацієнта: лаборант не має доступу до персональних даних, лише до identity (прізвище) з направлення.</summary>
    private static string? IdentityProblem(MedLink.LIS.Api.Services.Ehealth.EhealthServiceRequest? remote, string? patientCaption)
    {
        var last = remote?.IdentityLastName?.Trim();
        if (string.IsNullOrEmpty(last) || string.IsNullOrWhiteSpace(patientCaption)) return null;
        return patientCaption.Contains(last, StringComparison.OrdinalIgnoreCase) ? null : "Прізвище в направленні не збігається з пацієнтом замовлення";
    }

    /// <summary>Оновлює (або створює) рядок дзеркала за відповіддю ЕСОЗ.</summary>
    private async Task<EheIncomingMedicalReferral> UpsertAsync(EheIncomingMedicalReferral? local, MedLink.LIS.Api.Services.Ehealth.EhealthServiceRequest sr, List<string> problems)
    {
        var r = local ?? await _db.Referrals.Include(x => x.ServiceCatalogService).Include(x => x.PatientCard).Include(x => x.Status).Include(x => x.ProcessingStatus)
            .FirstOrDefaultAsync(x => x.EhealthId == sr.Id && x.RecordState != RecordStates.Deleted);
        var service = string.IsNullOrWhiteSpace(sr.ServiceCode) ? null : await _db.ServiceCatalog.FirstOrDefaultAsync(x => x.Code == sr.ServiceCode);
        if (service == null && !string.IsNullOrWhiteSpace(sr.ServiceCode)) problems.Add($"Послугу направлення «{sr.ServiceCode}» не знайдено в каталозі ЛІС");
        var statusId = sr.Status switch { "completed" => MedLinkEnums.ReferralStatusCompleted, "active" => MedLinkEnums.ReferralStatusActive, _ => MedLinkEnums.ReferralStatusRecalled };
        if (r == null)
        {
            var patientCard = string.IsNullOrWhiteSpace(sr.PatientId) ? null : await _db.Patients.FirstOrDefaultAsync(p => p.Id == sr.PatientId);
            r = new EheIncomingMedicalReferral
            {
                RegNumber = sr.Requisition, Caption = sr.ServiceName ?? service?.Name, OrganizationId = MedLinkDefaults.OrganizationId, PatientCardId = patientCard?.Id,
                PatientShortName = sr.IdentityLastName, ServiceCatalogServiceId = service?.Id, MedicalReferralCategoryId = MedLinkDefaults.ReferralCategoryLaboratoryId,
                StatusId = statusId, PriorityId = MedLinkEnums.EmptyGuid, ProcessingStatusInEhealthId = MedLinkEnums.ProcessingNew, ExpirationDate = sr.ExpiresAt, EhealthId = sr.Id
            };
            _db.Referrals.Add(r);
            r.ServiceCatalogService = service; r.PatientCard = patientCard;
            r.Status = await _db.EnumRecords.FirstOrDefaultAsync(e => e.Id == statusId);
        }
        else
        {
            r.EhealthId ??= sr.Id;
            if (sr.ExpiresAt != null) r.ExpirationDate = sr.ExpiresAt;
            if (service != null && r.ServiceCatalogServiceId == null) r.ServiceCatalogServiceId = service.Id;
            // ЕСОЗ — джерело істини про статус; локальне погашення без підтвердження ЕСОЗ не скасовуємо
            if (r.StatusId != statusId && !(r.StatusId == MedLinkEnums.ReferralStatusCompleted && sr.Status == "active" && r.CompleteDateInEhealth != null))
            { r.StatusId = statusId; r.Status = await _db.EnumRecords.FirstOrDefaultAsync(e => e.Id == statusId); }
        }
        r.EhealthStatus = sr.Status; r.EhealthPatientId = sr.PatientId; r.EhealthProgramId = sr.ProgramId; r.LastSyncedAt = DateTime.UtcNow;
        return r;
    }

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
        var res = await ResolveAsync(number.Trim());
        var r = res.Referral;
        var problems = await ProblemsAsync(r, string.IsNullOrWhiteSpace(patientId) ? null : patientId);
        problems.AddRange(res.Problems.Where(p => !problems.Contains(p)));
        if (!string.IsNullOrWhiteSpace(patientId) && IdentityProblem(res.Remote, (await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId))?.Caption) is { } idp) problems.Add(idp);
        await _db.SaveChangesAsync(); // кеш: дзеркало оновлено відповіддю ЕСОЗ
        var profiles = r.ServiceCatalogServiceId == null ? new() :
            await _db.Profiles.AsNoTracking().Where(p => p.EhealthServiceCatalogServiceId == r.ServiceCatalogServiceId && p.IsActive)
                .Select(p => new { p.Id, p.Code, p.Name }).ToListAsync();
        return new
        {
            r.Id, number = r.RegNumber, r.RegDate, r.ExpirationDate, status = r.Status?.Code, statusName = r.Status?.Caption, ehealthStatus = r.EhealthStatus, source = res.Remote != null ? "ESOZ" : "CACHE",
            processingStatus = r.ProcessingStatus?.Code, r.TakeInWorkDate, r.CompleteDateInEhealth, programId = r.EhealthProgramId,
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
                ResolvedReferral res;
                try { res = await ResolveAsync(ehealthRef); }
                catch (NotFoundException) { throw ValidationException.Field("ehealthReferralNumber", $"Е-направлення {ehealthRef} не знайдено"); }
                var r = res.Referral;
                var problems = await ProblemsAsync(r, patient.Id);
                problems.AddRange(res.Problems.Where(p => !problems.Contains(p))); // перевірка в реєстрі ЕСОЗ перед use
                if (IdentityProblem(res.Remote, patient.Caption) is { } idp) problems.Add(idp);
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
                // лікар закладу: співробітник ЛІС або ПІБ лікаря з направлення МІС
                if (string.IsNullOrWhiteSpace(req.DoctorId) && string.IsNullOrWhiteSpace(req.ReferrerDoctorName)) throw ValidationException.Field("doctorId", "Для внутрішнього направлення вкажіть лікаря закладу");
                break;
            case ReferralTypes.ExternalClinic:
                if (string.IsNullOrWhiteSpace(order.ReferrerOrganizationName)) throw ValidationException.Field("referrerOrganizationName", "Вкажіть клініку-направника");
                break;
        }
    }

    /// <summary>
    /// Дії з направленням при видачі замовлення. Е-направлення тут НЕ погашається: ЕСОЗ погашає його лише записом, який уже існує,
    /// тож complete виконує задача outbox COMPLETE_SR після реєстрації діагностичного звіту (job processed). Паперове — погашається локально.
    /// </summary>
    public async Task CompleteAsync(LabOrder order, MisDiagnosticReport report, DateTime at)
    {
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
        if (to.HasValue)
        {
            // «По» без часу — включно до кінця доби
            var toExclusive = to.Value.TimeOfDay == TimeSpan.Zero ? to.Value.AddDays(1) : to.Value;
            q = q.Where(o => o.OrderDatetime < toExclusive);
        }
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
                ReferralTypes.Self => null,
                _ => string.Join(", ", new[] { o.ReferrerOrganizationName, o.ReferrerDoctorName }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } who ? who : "Невідомо"
            },
            ehealthStatus = o.Referral?.Status?.Caption, ehealthCompletedAt = o.Referral?.CompleteDateInEhealth, o.TotalPrice
        }).ToList();
        var summary = ReferralTypes.All.Select(t => new { type = t, name = ReferralTypes.Label(t), count = orders.Count(o => o.ReferralType == t), amount = orders.Where(o => o.ReferralType == t).Sum(o => o.TotalPrice) });
        return new { total = items.Count, summary, items };
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
