// ТЗ 21.13 (FR-SGN-001…014): сесія ЕСОЗ у два кроки (L1 — логін/пароль, L2 — КЕП або TOTP), пакет DiagnosticReport для підпису,
// підпис КЕП, відправка (job → REGISTERED → complete е-направлення → COMPLETED), черга повторів, AUTH_REQUIRED, анулювання.
// Режим Ehealth:Mode=Mock — імітація ЕСОЗ: пароль будь-який, код 2FA 123456, тестовий сертифікат підписувача — з GET /ehealth/session.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services.Ehealth;

public sealed class EhealthLoginRequest
{
    public string Login { get; set; } = "";
    /// <summary>Пароль передається лише на шлюз ЕСОЗ і ніде не зберігається (FR-SGN-002).</summary>
    public string Password { get; set; } = "";
}

public sealed class SignerInfo
{
    public string? Drfo { get; set; }
    public string? Name { get; set; }
    public string? CertSerial { get; set; }
    public DateTime? ValidTo { get; set; }
}

public sealed class EhealthConfirmRequest
{
    /// <summary>KEP | TOTP</summary>
    public string Method { get; set; } = "KEP";
    public string? Code { get; set; }
    public SignerInfo? Signer { get; set; }
}

public sealed class EhealthSubmitRequest
{
    /// <summary>Підписані дані (CAdES/base64) з віджета підпису; приватний ключ не залишає пристрій (FR-SGN-005).</summary>
    public string? SignedData { get; set; }
    public SignerInfo? Signer { get; set; }
    /// <summary>Лише в режимі Mock: імітувати мережеву помилку (S-35).</summary>
    public bool SimulateNetworkError { get; set; }
    public string? Reason { get; set; }
}

public sealed class EhealthSignService
{
    public static readonly string[] SubmitRoles = { LabRoles.Doctor, LabRoles.Admin };
    public const string DemoTotpCode = "123456";

    private readonly LisDbContext _db;
    private readonly ICurrentEmployee _current;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly EhealthOptions _options;

    public EhealthSignService(LisDbContext db, ICurrentEmployee current, IRolePolicy policy, IAuditService audit, EhealthOptions options)
    {
        _db = db; _current = current; _policy = policy; _audit = audit; _options = options;
    }

    private static DomainProblemException Problem(int status, string code, string title, string detail) => new(status, code, title, detail);

    // ------------------------------------------------------------------ сесія
    private async Task<LabSettings> SettingsAsync() => await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();

    /// <summary>Активна сесія поточного користувача (прострочені й неактивні довше IdleMinutes — закриваються, FR-SGN-004).</summary>
    public async Task<LabEhealthSession?> ActiveAsync(bool touch = false)
    {
        var now = DateTime.UtcNow;
        var settings = await SettingsAsync();
        var s = await _db.EhealthSessions.Where(x => x.EmployeeId == _current.EmployeeId && x.ClosedAt == null).OrderByDescending(x => x.CreatedOn).FirstOrDefaultAsync();
        if (s == null) return null;
        if (s.ExpiresAt <= now || (now - s.LastActivityOn).TotalMinutes > settings.EhealthSessionIdleMinutes)
        {
            s.ClosedAt = now;
            await _db.SaveChangesAsync();
            return null;
        }
        if (touch) s.LastActivityOn = now;
        return s;
    }

    /// <summary>Тестовий сертифікат КЕП користувача (Mock): ДРФО з картки персони або детермінований демо-номер.</summary>
    public async Task<SignerInfo> TestCertificateAsync()
    {
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == _current.EmployeeId);
        var drfo = e?.Person?.Ipn;
        if (string.IsNullOrWhiteSpace(drfo))
            drfo = "3" + (BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(_current.EmployeeId)), 0) % 1_000_000_000).ToString("D9");
        return new SignerInfo { Drfo = drfo, Name = e?.Caption ?? _current.FullName, CertSerial = "DEMO-" + drfo[^4..] + "-KEP", ValidTo = DateTime.UtcNow.Date.AddYears(1) };
    }

    public async Task<object> StatusAsync()
    {
        var s = await ActiveAsync();
        return new
        {
            mode = _options.IsMock ? "MOCK" : "LIVE",
            level = s == null ? "L0" : s.AuthLevel, login = s?.Login, method = s?.AuthMethod, expiresAt = s?.ExpiresAt, totpEnabled = s?.TotpEnabled ?? false,
            signerName = s?.SignerName, certSerial = s?.CertSerial,
            label = s == null ? "ЕСОЗ: не підключено" : s.AuthLevel == "L1" ? "ЕСОЗ: пароль введено" : $"ЕСОЗ: підтверджено до {s.ExpiresAt.ToLocalTime():HH:mm}",
            testCertificate = _options.IsMock ? await TestCertificateAsync() : null,
            demoTotpCode = _options.IsMock ? DemoTotpCode : null
        };
    }

    private void Log(string method, string url, object? request, int status, object? response) => _db.EhealthExchangeLog.Add(new LabEhealthExchangeLog
    {
        Mode = _options.IsMock ? "MOCK" : "LIVE", Method = method, Url = url, StatusCode = status, DurationMs = 0,
        RequestJson = request == null ? null : JsonSerializer.Serialize(request, JsonDefaults.Options),
        ResponseJson = response == null ? null : JsonSerializer.Serialize(response, JsonDefaults.Options)
    });

    private void EnsureMock()
    {
        if (!_options.IsMock) throw Problem(503, "ehealth-live-not-configured", "ЕСОЗ недоступна", "Робочий режим ЕСОЗ для підпису й відправки звітів не налаштовано (Ehealth:Mode=Live)");
    }

    /// <summary>Крок 1 — вхід логіном і паролем (L1). Пароль не зберігається й не журналюється.</summary>
    public async Task<object> LoginAsync(EhealthLoginRequest req)
    {
        EnsureMock();
        if (string.IsNullOrWhiteSpace(req.Login) || string.IsNullOrWhiteSpace(req.Password))
            throw Problem(401, "ehealth-invalid-credentials", "Невірні дані входу", "Вкажіть ел. пошту або логін і пароль ЕСОЗ");
        var now = DateTime.UtcNow;
        foreach (var old in await _db.EhealthSessions.Where(x => x.EmployeeId == _current.EmployeeId && x.ClosedAt == null).ToListAsync()) old.ClosedAt = now;
        var settings = await SettingsAsync();
        var prevTotp = await _db.EhealthSessions.AsNoTracking().Where(x => x.EmployeeId == _current.EmployeeId).OrderByDescending(x => x.CreatedOn).Select(x => (bool?)x.TotpEnabled).FirstOrDefaultAsync();
        var s = new LabEhealthSession
        {
            EmployeeId = _current.EmployeeId, Login = req.Login.Trim(), AuthLevel = "L1", AuthMethod = "PASSWORD", ExpiresAt = now.AddMinutes(settings.EhealthSessionTtlMinutes),
            TokenEnc = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)), TotpEnabled = prevTotp ?? false, LastActivityOn = now
        };
        _db.EhealthSessions.Add(s);
        Log("POST", "oauth/tokens (password)", new { login = s.Login }, 201, new { level = "L1" });
        _audit.Log("EHEALTH_LOGIN", "lab_ehealth_session", s.Id, null, new { s.Login, s.AuthLevel });
        await _db.SaveChangesAsync();
        return await StatusAsync();
    }

    /// <summary>Крок 2 — підтвердження особи (L2): КЕП (збіг ДРФО, чинний сертифікат) або код TOTP, якщо 2FA увімкнена (FR-SGN-003).</summary>
    public async Task<object> ConfirmAsync(EhealthConfirmRequest req)
    {
        EnsureMock();
        var s = await ActiveAsync(true) ?? throw Problem(401, "ehealth-auth-required", "Потрібен вхід в ЕСОЗ", "Спершу увійдіть в ЕСОЗ логіном і паролем");
        var method = req.Method.ToUpperInvariant();
        if (method == "KEP")
        {
            var signer = await CheckSignerAsync(req.Signer);
            s.CertSerial = signer.CertSerial; s.SignerName = signer.Name; s.SignerDrfo = signer.Drfo;
        }
        else if (method == "TOTP")
        {
            if (!s.TotpEnabled) throw Problem(409, "ehealth-totp-disabled", "Двофакторна автентифікація вимкнена", "Двофакторна автентифікація вимкнена. Увімкніть 2FA (ТОТР) у налаштуваннях облікового запису ЕСОЗ або увійдіть через КЕП");
            if (req.Code?.Trim() != DemoTotpCode) throw Problem(422, "ehealth-invalid-code", "Невірний код", "Невірний код з додатка автентифікації");
        }
        else throw ValidationException.Field("method", "Метод: KEP або TOTP");
        s.AuthLevel = "L2";
        s.AuthMethod = "PASSWORD+" + method;
        Log("POST", $"oauth/approve ({method})", new { method, certSerial = s.CertSerial }, 200, new { level = "L2" });
        _audit.Log("EHEALTH_CONFIRM", "lab_ehealth_session", s.Id, null, new { s.AuthMethod, s.CertSerial });
        await _db.SaveChangesAsync();
        return await StatusAsync();
    }

    /// <summary>«Увімкнути 2FA (ТОТР)» — перехід до налаштувань облікового запису ЕСОЗ (Mock — вмикається для демо-облікового запису).</summary>
    public async Task<object> EnableTotpAsync()
    {
        EnsureMock();
        var s = await ActiveAsync(true) ?? throw Problem(401, "ehealth-auth-required", "Потрібен вхід в ЕСОЗ", "Спершу увійдіть в ЕСОЗ логіном і паролем");
        s.TotpEnabled = true;
        await _db.SaveChangesAsync();
        return new { totpEnabled = true, otpauth = $"otpauth://totp/ESOZ:{Uri.EscapeDataString(s.Login)}?secret=DEMODEMODEMO&issuer=ESOZ", demoCode = DemoTotpCode };
    }

    public async Task LogoutAsync()
    {
        var now = DateTime.UtcNow;
        foreach (var s in await _db.EhealthSessions.Where(x => x.EmployeeId == _current.EmployeeId && x.ClosedAt == null).ToListAsync()) s.ClosedAt = now;
        await _db.SaveChangesAsync();
    }

    private async Task<SignerInfo> CheckSignerAsync(SignerInfo? signer)
    {
        if (signer == null || string.IsNullOrWhiteSpace(signer.Drfo)) throw ValidationException.Field("signer", "Дані підписувача з контейнера КЕП не передано");
        if (signer.ValidTo.HasValue && signer.ValidTo.Value < DateTime.UtcNow) throw Problem(422, "certificate-expired", "Сертифікат КЕП недійсний", "Строк дії сертифіката КЕП минув");
        var expected = await TestCertificateAsync();
        if (signer.Drfo.Trim() != expected.Drfo)
            throw Problem(422, "signer-mismatch", "Підписувач не збігається", "ДРФО підписувача з КЕП не збігається з обліковим записом користувача (підпис особистий, FR-SGN-011)");
        return new SignerInfo { Drfo = signer.Drfo.Trim(), Name = signer.Name ?? expected.Name, CertSerial = signer.CertSerial ?? expected.CertSerial, ValidTo = signer.ValidTo };
    }

    private async Task<LabEhealthSession> RequireL2Async()
    {
        var s = await ActiveAsync(true) ?? throw Problem(401, "ehealth-auth-required", "Потрібен вхід в ЕСОЗ", "Увійдіть в ЕСОЗ (логін і пароль, потім КЕП або 2FA)");
        if (s.AuthLevel != "L2") throw Problem(403, "ehealth-strong-auth-required", "Потрібне підтвердження особи", "Відправка доступна після підтвердження особи (КЕП або 2FA)");
        return s;
    }

    // ------------------------------------------------------------------ підготовка, підпис, відправка
    private async Task<LabOrder> LoadOrderAsync(string orderId) =>
        await _db.Orders.Include(o => o.Patient).Include(o => o.Items).Include(o => o.Tests).ThenInclude(t => t.Result).Include(o => o.Tests).ThenInclude(t => t.Profile)
            .FirstOrDefaultAsync(o => o.Id == orderId) ?? throw NotFoundException.For("Замовлення", orderId);

    /// <summary>Перевірки перед підписом (FR-SGN-006, FR-SGN-013).</summary>
    private async Task<List<string>> ProblemsAsync(LabOrder o)
    {
        var problems = new List<string>();
        if (o.Status != OrderStatuses.Released) problems.Add("замовлення ще не видано (RELEASED)");
        if (o.DiagnosticReportId == null) problems.Add("немає медичного висновку (mis_diagnostic_report)");
        var notFinal = o.Tests.Where(t => !OrderTestStatuses.Final.Contains(t.Status)).Select(t => t.TestCode).ToList();
        if (notFinal.Count > 0) problems.Add("не валідовані показники: " + string.Join(", ", notFinal));
        if (!o.Items.Any(i => i.InterventionCode != null) && !o.Tests.Any(t => t.Profile?.EhealthServiceCatalogServiceId != null) && o.EhealthReferralId == null)
            problems.Add("послуга не має коду інтервенції ЕСОЗ");
        if (o.ReferralContext == ReferralContexts.Inpatient && !(await SettingsAsync()).EhealthSubmitInpatient)
            problems.Add("відправку внутрішніх (стаціонар) призначень вимкнено — Ehealth:SubmitInpatient (FR-SGN-013)");
        return problems;
    }

    public async Task<object> PrepareAsync(string orderId)
    {
        _policy.Require("Відправка звіту в ЕСОЗ", SubmitRoles);
        EnsureMock();
        var o = await LoadOrderAsync(orderId);
        var problems = await ProblemsAsync(o);
        if (problems.Count > 0) throw Problem(409, "report-not-ready", "Звіт не готовий до відправки", string.Join("; ", problems));
        await RequireL2Async();
        var package = new
        {
            resourceType = "diagnostic_report_package", diagnosticReportId = o.DiagnosticReportId, orderNumber = o.OrderNumber, patientId = o.PatientId, patient = o.Patient?.Caption,
            basedOn = o.EhealthReferralId, code = o.Items.Select(i => i.InterventionCode).FirstOrDefault(c => c != null),
            issued = o.ReleasedAt, observations = o.Tests.Where(t => OrderTestStatuses.VerifiedAny.Contains(t.Status) || t.ReleasedAt != null)
                .Select(t => new { code = t.TestCode, name = t.TestName, value = t.Result?.NumericValue, valueString = t.Result?.StringValue, unit = t.Result?.Unit, interpretation = t.Result?.Flag })
        };
        var json = JsonSerializer.Serialize(package, JsonDefaults.Options);
        var sub = await _db.EhealthSubmissions.Where(x => x.OrderId == o.Id && x.Kind == "SUBMIT" && x.Status != EhealthSubmitStatuses.Completed && x.Status != EhealthSubmitStatuses.Annulled)
                      .OrderByDescending(x => x.CreatedOn).FirstOrDefaultAsync();
        if (sub == null) { sub = new LabEhealthSubmission { OrderId = o.Id, DiagnosticReportId = o.DiagnosticReportId }; _db.EhealthSubmissions.Add(sub); }
        if (sub.Status is EhealthSubmitStatuses.ReadyToSign or EhealthSubmitStatuses.NotSent or EhealthSubmitStatuses.Rejected)
        {
            sub.PackageJson = json; sub.PackageHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(); sub.Status = EhealthSubmitStatuses.ReadyToSign;
            o.EhealthStatus = EhealthSubmitStatuses.ReadyToSign;
        }
        await _db.SaveChangesAsync();
        return Dto(o, sub);
    }

    /// <summary>Підпис КЕП і відправка (FR-SGN-005, FR-SGN-007, FR-SGN-008). Підписаний пакет зберігається для повторів без повторного підпису.</summary>
    public async Task<object> SubmitAsync(string orderId, EhealthSubmitRequest req)
    {
        _policy.Require("Відправка звіту в ЕСОЗ", SubmitRoles);
        EnsureMock();
        var o = await LoadOrderAsync(orderId);
        var sub = await _db.EhealthSubmissions.Where(x => x.OrderId == o.Id && x.Kind == "SUBMIT").OrderByDescending(x => x.CreatedOn).FirstOrDefaultAsync()
                  ?? throw Problem(409, "report-not-ready", "Звіт не підготовлено", "Спершу підготуйте пакет для підпису (prepare)");
        if (sub.Status is EhealthSubmitStatuses.Completed or EhealthSubmitStatuses.Registered or EhealthSubmitStatuses.Processing)
            throw new ConflictException($"Звіт уже відправлено: {EhealthSubmitStatuses.Label(sub.Status)}");
        LabEhealthSession session;
        try { session = await RequireL2Async(); }
        catch (DomainProblemException) when (sub.SignedData != null)
        {
            sub.Status = EhealthSubmitStatuses.AuthRequired; o.EhealthStatus = EhealthSubmitStatuses.AuthRequired; // FR-SGN-008
            await _db.SaveChangesAsync();
            throw;
        }
        if (sub.SignedData == null || sub.Status == EhealthSubmitStatuses.ReadyToSign)
        {
            if (string.IsNullOrWhiteSpace(req.SignedData)) throw ValidationException.Field("signedData", "Підпишіть пакет КЕП (окремий крок навіть після входу через 2FA)");
            var signer = await CheckSignerAsync(req.Signer);
            sub.SignedData = req.SignedData; sub.SignedBy = _current.EmployeeId; sub.SignedByName = signer.Name; sub.SignedOn = DateTime.UtcNow; sub.CertSerial = signer.CertSerial;
            sub.Status = EhealthSubmitStatuses.Signed; o.EhealthStatus = EhealthSubmitStatuses.Signed;
            _audit.Log("EHEALTH_SIGN", "lab_ehealth_submission", sub.Id, null, new { sub.PackageHash, sub.CertSerial, method = session.AuthMethod });
        }
        await SendAsync(o, sub, req.SimulateNetworkError);
        await _db.SaveChangesAsync();
        return Dto(o, sub);
    }

    /// <summary>Повтор відправки збереженого підписаного пакета (черга повторів, після AUTH_REQUIRED/ERROR).</summary>
    public async Task<object> RetryAsync(string orderId) => await SubmitAsync(orderId, new EhealthSubmitRequest());

    private async Task SendAsync(LabOrder o, LabEhealthSubmission sub, bool fail)
    {
        sub.Attempts++;
        sub.Status = EhealthSubmitStatuses.Processing; o.EhealthStatus = EhealthSubmitStatuses.Processing;
        var jobId = Guid.NewGuid().ToString();
        if (fail)
        {
            sub.Status = EhealthSubmitStatuses.Error; o.EhealthStatus = EhealthSubmitStatuses.Error;
            sub.LastError = "Мережева помилка: ЕСОЗ не відповідає (503). Пакет збережено, автоматичний повтор"; sub.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(60, 2 * sub.Attempts));
            Log("POST", $"patients/{o.PatientId}/diagnostic_report_package", new { sub.PackageHash, sub.CertSerial }, 503, new { error = sub.LastError });
            return;
        }
        Log("POST", $"patients/{o.PatientId}/diagnostic_report_package", new { sub.PackageHash, sub.CertSerial }, 202, new { job = jobId });
        sub.JobId = jobId;
        sub.EhealthReportId = Guid.NewGuid().ToString();
        Log("GET", $"jobs/{jobId}", null, 200, new { status = "processed", entity = sub.EhealthReportId });
        sub.Status = EhealthSubmitStatuses.Registered; o.EhealthStatus = EhealthSubmitStatuses.Registered;
        sub.LastError = null; sub.NextAttemptAt = null;
        var report = o.DiagnosticReportId == null ? null : await _db.DiagnosticReports.FirstOrDefaultAsync(r => r.Id == o.DiagnosticReportId);
        if (report != null) report.EhealthId = sub.EhealthReportId;
        // Погашення е-направлення (completed_with = звіт ЕСОЗ); якщо направлення вже погашено при видачі — лише фіксуємо результат
        if (o.EhealthReferralId != null || o.ReferralType == ReferralTypes.Ehealth)
        {
            Log("PATCH", $"service_requests/{o.EhealthReferralId}/actions/complete", new { completed_with = new { type = "diagnostic_report", id = sub.EhealthReportId } }, 202, new { status = "completed" });
            sub.Status = EhealthSubmitStatuses.Completed; o.EhealthStatus = EhealthSubmitStatuses.Completed;
        }
        sub.CompletedAt = DateTime.UtcNow;
        _audit.Log("EHEALTH_SUBMIT", "lab_ehealth_submission", sub.Id, null, new { sub.JobId, sub.EhealthReportId, sub.Status });
    }

    /// <summary>Анулювання звіту (FR-SGN-009): підпис КЕП обов'язковий; створюється новий звіт (mis_diagnostic_report) для повторної відправки.</summary>
    public async Task<object> AnnulAsync(string orderId, EhealthSubmitRequest req)
    {
        _policy.Require("Анулювання звіту в ЕСОЗ", SubmitRoles);
        EnsureMock();
        var o = await LoadOrderAsync(orderId);
        var sub = await _db.EhealthSubmissions.Where(x => x.OrderId == o.Id && x.Kind == "SUBMIT" && (x.Status == EhealthSubmitStatuses.Completed || x.Status == EhealthSubmitStatuses.Registered))
                      .OrderByDescending(x => x.CreatedOn).FirstOrDefaultAsync() ?? throw new ConflictException("Немає зареєстрованого в ЕСОЗ звіту для анулювання");
        await RequireL2Async();
        if (string.IsNullOrWhiteSpace(req.SignedData)) throw ValidationException.Field("signedData", "Анулювання підписується КЕП");
        if (string.IsNullOrWhiteSpace(req.Reason)) throw ValidationException.Field("reason", "Вкажіть причину анулювання");
        var signer = await CheckSignerAsync(req.Signer);
        var annul = new LabEhealthSubmission
        {
            OrderId = o.Id, DiagnosticReportId = sub.DiagnosticReportId, Kind = "ANNUL", Status = EhealthSubmitStatuses.Annulled, SignedData = req.SignedData, SignedBy = _current.EmployeeId,
            SignedByName = signer.Name, SignedOn = DateTime.UtcNow, CertSerial = signer.CertSerial, EhealthReportId = sub.EhealthReportId, CompletedAt = DateTime.UtcNow, LastError = req.Reason, Attempts = 1
        };
        _db.EhealthSubmissions.Add(annul);
        sub.Status = EhealthSubmitStatuses.Annulled;
        Log("PATCH", $"patients/{o.PatientId}/diagnostic_reports/{sub.EhealthReportId}/actions/cancel", new { status = "entered_in_error", reason = req.Reason }, 202, new { status = "entered_in_error" });
        var old = o.DiagnosticReportId == null ? null : await _db.DiagnosticReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == o.DiagnosticReportId);
        if (old != null)
        {
            var copy = new MisDiagnosticReport
            {
                RegNumber = old.RegNumber + "-1", RegDate = DateTime.UtcNow, Caption = old.Caption, Status = 0, PatientCardId = old.PatientCardId, OrganizationId = old.OrganizationId,
                LegalEntityId = old.LegalEntityId, DivisionId = old.DivisionId, DocumentTypeId = old.DocumentTypeId, EhealthServiceCatalogServiceId = old.EhealthServiceCatalogServiceId,
                EhealthIncomingMedicalReferralId = old.EhealthIncomingMedicalReferralId, Description = old.Description, EffectiveDateTimeStart = old.EffectiveDateTimeStart,
                EffectiveDateTimeEnd = old.EffectiveDateTimeEnd, IssuedAt = DateTime.UtcNow, PerformerString = old.PerformerString, IsPerformerString = true, IsPrimarySource = true,
                Comment = "Виправлений звіт після анулювання: " + req.Reason
            };
            _db.DiagnosticReports.Add(copy);
            o.DiagnosticReportId = copy.Id;
        }
        o.EhealthStatus = EhealthSubmitStatuses.Annulled;
        _audit.Log("EHEALTH_ANNUL", "lab_ehealth_submission", annul.Id, null, new { req.Reason, annul.CertSerial });
        await _db.SaveChangesAsync();
        return Dto(o, annul);
    }

    public async Task<object> OrderStatusAsync(string orderId)
    {
        var o = await LoadOrderAsync(orderId);
        var subs = await _db.EhealthSubmissions.AsNoTracking().Where(x => x.OrderId == o.Id).OrderByDescending(x => x.CreatedOn).ToListAsync();
        var problems = await ProblemsAsync(o);
        return new
        {
            orderId = o.Id, o.OrderNumber, status = o.EhealthStatus, label = EhealthSubmitStatuses.Label(o.EhealthStatus),
            canSubmit = problems.Count == 0 && (EhealthSubmitStatuses.CanSubmit.Contains(o.EhealthStatus) || o.EhealthStatus == EhealthSubmitStatuses.Annulled),
            canAnnul = o.EhealthStatus is EhealthSubmitStatuses.Completed or EhealthSubmitStatuses.Registered, problems,
            submissions = subs.Select(s => new { s.Id, s.Kind, s.Status, label = EhealthSubmitStatuses.Label(s.Status), s.PackageHash, s.SignedByName, s.SignedOn, s.CertSerial, s.JobId, s.EhealthReportId, s.Attempts, s.LastError, s.NextAttemptAt, s.CompletedAt, s.CreatedOn })
        };
    }

    public async Task<object> SubmissionsAsync(string? status)
    {
        var q = _db.EhealthSubmissions.AsNoTracking().Include(s => s.Order).ThenInclude(o => o!.Patient).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(s => s.Status == status.ToUpperInvariant());
        var rows = await q.OrderByDescending(s => s.CreatedOn).Take(500).ToListAsync();
        // Видані замовлення, що ще не надсилались (NOT_SENT)
        var notSent = await _db.Orders.AsNoTracking().Include(o => o.Patient).Where(o => o.Status == OrderStatuses.Released && o.EhealthStatus == EhealthSubmitStatuses.NotSent && o.DiagnosticReportId != null)
            .OrderByDescending(o => o.ReleasedAt).Take(200).Select(o => new { o.Id, o.OrderNumber, patientName = o.Patient!.Caption, o.ReleasedAt, o.ReferralContext }).ToListAsync();
        return new
        {
            items = rows.Select(s => new
            {
                s.Id, s.OrderId, orderNumber = s.Order?.OrderNumber, patientName = s.Order?.Patient?.Caption, s.Kind, s.Status, label = EhealthSubmitStatuses.Label(s.Status),
                s.SignedByName, s.SignedOn, s.Attempts, s.LastError, s.NextAttemptAt, s.EhealthReportId, s.CreatedOn
            }),
            notSent,
            summary = rows.GroupBy(s => s.Status).ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private static object Dto(LabOrder o, LabEhealthSubmission s) => new
    {
        orderId = o.Id, o.OrderNumber, ehealthStatus = o.EhealthStatus, label = EhealthSubmitStatuses.Label(o.EhealthStatus),
        submission = new { s.Id, s.Kind, s.Status, s.PackageHash, package = s.PackageJson == null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(s.PackageJson), s.SignedByName, s.SignedOn, s.CertSerial, s.JobId, s.EhealthReportId, s.Attempts, s.LastError, s.NextAttemptAt }
    };
}
