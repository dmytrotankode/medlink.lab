// ТЗ 21.13 (FR-SGN-001…014): сесія ЕСОЗ, пакет DiagnosticReport для підпису, підпис КЕП, постановка відправки в чергу (outbox), повтор, анулювання.
// Реальну відправку виконує воркер (EhealthOutbox.cs): SUBMIT_DR → job processed → окрема задача COMPLETE_SR; виправлення: CANCEL_DR → новий пакет.
// Режими: Mock/Replay — імітація ЕСОЗ (вхід паролем + 2FA 123456, тестовий підписувач, мок-реєстр/фікстури);
// Live — вхід паролем недоступний (ТВ 1.2.2: лише сторінка auth.ehealth.gov.ua; OAuth redirect потребує preprod і не реалізований),
// відправка можлива зі статичним токеном (Ehealth:AccessToken) і підписом, що надходить з браузерного віджета КЕП.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    /// <summary>Підписані дані (base64 від CAdES-X Long) з віджета підпису; приватний ключ не залишає пристрій (FR-SGN-005).</summary>
    public string? SignedData { get; set; }
    public SignerInfo? Signer { get; set; }
    /// <summary>Лише в режимі Mock: імітувати мережеву помилку (503) при першій спробі відправки (S-35).</summary>
    public bool SimulateNetworkError { get; set; }
    public string? Reason { get; set; }
}

public sealed class EhealthMockSignRequest
{
    /// <summary>submit | annul</summary>
    public string Kind { get; set; } = "submit";
    public string? Reason { get; set; }
}

public sealed class EhealthSignService
{
    public static readonly string[] SubmitRoles = { LabRoles.Doctor, LabRoles.Admin };
    public const string DemoTotpCode = "123456";
    /// <summary>Код причини скасування з eHealth/cancellation_reasons (звірити з GET /api/dictionaries на preprod).</summary>
    public const string CancellationReasonCode = "misspelling";

    private readonly LisDbContext _db;
    private readonly ICurrentEmployee _current;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly EhealthOptions _options;
    private readonly EhealthPackageBuilder _builder;
    private readonly EhealthOutboxService _outbox;
    private readonly EhealthOutboxEngine _engine;
    private readonly EhealthMockState _mock;
    private readonly IEhealthTokenProvider _tokens;

    public EhealthSignService(LisDbContext db, ICurrentEmployee current, IRolePolicy policy, IAuditService audit, EhealthOptions options, EhealthPackageBuilder builder,
        EhealthOutboxService outbox, EhealthOutboxEngine engine, EhealthMockState mock, IEhealthTokenProvider tokens)
    {
        _db = db; _current = current; _policy = policy; _audit = audit; _options = options; _builder = builder; _outbox = outbox; _engine = engine; _mock = mock; _tokens = tokens;
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

    /// <summary>Тестовий сертифікат КЕП користувача (Mock/Replay): ДРФО з картки персони або детермінований демо-номер.</summary>
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
        var sim = _options.Simulated;
        return new
        {
            mode = _options.ModeName, simulated = sim,
            level = s == null ? "L0" : s.AuthLevel, login = s?.Login, method = s?.AuthMethod, expiresAt = s?.ExpiresAt, totpEnabled = s?.TotpEnabled ?? false,
            signerName = s?.SignerName, certSerial = s?.CertSerial,
            label = s == null ? "ЕСОЗ: не підключено" : s.AuthLevel == "L1" ? "ЕСОЗ: пароль введено" : $"ЕСОЗ: підтверджено до {s.ExpiresAt.ToLocalTime():HH:mm}",
            testCertificate = sim ? await TestCertificateAsync() : null,
            demoTotpCode = sim ? DemoTotpCode : null,
            liveOperations = _options.IsLive ? _options.Live.Operations : null
        };
    }

    private void Log(string method, string url, object? request, int status, object? response) => _db.EhealthExchangeLog.Add(new LabEhealthExchangeLog
    {
        Mode = _options.ModeName, Method = method, Url = url, StatusCode = status, DurationMs = 0,
        RequestJson = request == null ? null : JsonSerializer.Serialize(request, JsonDefaults.Options),
        ResponseJson = response == null ? null : JsonSerializer.Serialize(response, JsonDefaults.Options)
    });

    /// <summary>Вхід паролем/2FA — лише імітація (Mock/Replay). У Live вхід виконує сторінка auth.ehealth.gov.ua (OAuth), який тут не реалізовано.</summary>
    private void EnsureSimulatedSession()
    {
        if (_options.IsLive) throw Problem(503, "ehealth-live-oauth-not-implemented", "Вхід в ЕСОЗ недоступний",
            "У режимі Live вхід виконується через сторінку auth.ehealth.gov.ua (OAuth), що ще не підключено; тимчасово використовується токен із Ehealth:AccessToken");
    }

    /// <summary>Крок 1 — вхід логіном і паролем (L1). Пароль не зберігається й не журналюється.</summary>
    public async Task<object> LoginAsync(EhealthLoginRequest req)
    {
        EnsureSimulatedSession();
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
        EnsureSimulatedSession();
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

    /// <summary>«Увімкнути 2FA (ТОТР)» — перехід до налаштувань облікового запису ЕСОЗ (Mock/Replay — вмикається для демо-облікового запису).</summary>
    public async Task<object> EnableTotpAsync()
    {
        EnsureSimulatedSession();
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

    public async Task<SignerInfo> CheckSignerAsync(SignerInfo? signer)
    {
        if (signer == null || string.IsNullOrWhiteSpace(signer.Drfo)) throw ValidationException.Field("signer", "Дані підписувача з контейнера КЕП не передано");
        if (signer.ValidTo.HasValue && signer.ValidTo.Value < DateTime.UtcNow) throw Problem(422, "certificate-expired", "Сертифікат КЕП недійсний", "Строк дії сертифіката КЕП минув");
        var expected = await TestCertificateAsync();
        if (signer.Drfo.Trim() != expected.Drfo)
            throw Problem(422, "signer-mismatch", "Підписувач не збігається", "ДРФО підписувача з КЕП не збігається з обліковим записом користувача (підпис особистий, FR-SGN-011)");
        return new SignerInfo { Drfo = signer.Drfo.Trim(), Name = signer.Name ?? expected.Name, CertSerial = signer.CertSerial ?? expected.CertSerial, ValidTo = signer.ValidTo };
    }

    /// <summary>Mock/Replay: сесія L2 (пароль + КЕП/2FA). Live: достатньо налаштованого токена доступу (сесії користувача поки немає).</summary>
    private async Task RequireAuthAsync()
    {
        if (_options.IsLive)
        {
            if (string.IsNullOrEmpty(await _tokens.GetAccessTokenAsync())) throw Problem(503, "ehealth-live-not-configured", "ЕСОЗ не налаштовано", "Не налаштовано токен доступу ЕСОЗ (Ehealth:AccessToken)");
            return;
        }
        var s = await ActiveAsync(true) ?? throw Problem(401, "ehealth-auth-required", "Потрібен вхід в ЕСОЗ", "Увійдіть в ЕСОЗ (логін і пароль, потім КЕП або 2FA)");
        if (s.AuthLevel != "L2") throw Problem(403, "ehealth-strong-auth-required", "Потрібне підтвердження особи", "Відправка доступна після підтвердження особи (КЕП або 2FA)");
    }

    // ------------------------------------------------------------------ підготовка пакета
    private async Task<LabOrder> LoadOrderAsync(string orderId) =>
        await _db.Orders.Include(o => o.Patient).Include(o => o.Items).Include(o => o.Tests).ThenInclude(t => t.Result).Include(o => o.Tests).ThenInclude(t => t.Profile)
            .Include(o => o.Tests).ThenInclude(t => t.Test)
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

    private static string Hash(string json) => Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false).GetBytes(json))).ToLowerInvariant();

    public async Task<object> PrepareAsync(string orderId)
    {
        _policy.Require("Відправка звіту в ЕСОЗ", SubmitRoles);
        var o = await LoadOrderAsync(orderId);
        var problems = await ProblemsAsync(o);
        if (problems.Count > 0) throw Problem(409, "report-not-ready", "Звіт не готовий до відправки", string.Join("; ", problems));
        await RequireAuthAsync();
        var (package, buildProblems) = await _builder.BuildSubmitAsync(o, _current.EmployeeId);
        if (package == null) throw Problem(409, "report-not-ready", "Звіт не готовий до відправки", string.Join("; ", buildProblems));
        var sub = await _db.EhealthSubmissions.Where(x => x.OrderId == o.Id && x.Kind == "SUBMIT" && x.Status != EhealthSubmitStatuses.Completed && x.Status != EhealthSubmitStatuses.Annulled)
                      .OrderByDescending(x => x.CreatedOn).FirstOrDefaultAsync();
        if (sub == null) { sub = new LabEhealthSubmission { OrderId = o.Id, DiagnosticReportId = o.DiagnosticReportId }; _db.EhealthSubmissions.Add(sub); }
        if (sub.Status is EhealthSubmitStatuses.ReadyToSign or EhealthSubmitStatuses.NotSent or EhealthSubmitStatuses.Rejected)
        {
            // новий пакет = нові UUID v1 і новий підпис; попередній підпис (якщо був) недійсний для змінених даних
            sub.PackageJson = package.Json; sub.PackageHash = package.Hash; sub.Status = EhealthSubmitStatuses.ReadyToSign;
            sub.SignedData = null; sub.SignedBy = null; sub.SignedByName = null; sub.SignedOn = null; sub.CertSerial = null; sub.JobId = null; sub.LastError = null; sub.NextAttemptAt = null; sub.Attempts = 0;
            o.EhealthStatus = EhealthSubmitStatuses.ReadyToSign;
        }
        await _db.SaveChangesAsync();
        return Dto(o, sub);
    }

    /// <summary>Тестовий підпис (лише Mock/Replay): структурно коректний CAdES-X Long із вкладеним пакетом. НЕ є криптографічним підписом.</summary>
    public async Task<object> MockSignAsync(string orderId, EhealthMockSignRequest req)
    {
        _policy.Require("Відправка звіту в ЕСОЗ", SubmitRoles);
        if (_options.IsLive) throw Problem(409, "mock-sign-disabled", "Тестовий підпис недоступний", "У режимі Live підпис виконує браузерний віджет КЕП");
        var o = await LoadOrderAsync(orderId);
        byte[] content;
        if (string.Equals(req.Kind, "annul", StringComparison.OrdinalIgnoreCase))
        {
            var (_, cancel, _) = await BuildCancelAsync(o, req.Reason);
            content = cancel.Bytes;
        }
        else
        {
            var sub = await LatestSubmitAsync(o.Id) ?? throw Problem(409, "report-not-ready", "Звіт не підготовлено", "Спершу підготуйте пакет для підпису (prepare)");
            if (sub.PackageJson == null) throw Problem(409, "report-not-ready", "Звіт не підготовлено", "Спершу підготуйте пакет для підпису (prepare)");
            content = new UTF8Encoding(false).GetBytes(sub.PackageJson);
        }
        return new { signedData = EhealthMockSigner.Sign(content), signer = await TestCertificateAsync(), mock = true };
    }

    private Task<LabEhealthSubmission?> LatestSubmitAsync(string orderId) =>
        _db.EhealthSubmissions.Where(x => x.OrderId == orderId && x.Kind == "SUBMIT").OrderByDescending(x => x.CreatedOn).FirstOrDefaultAsync();

    // ------------------------------------------------------------------ підпис і постановка в чергу
    /// <summary>Підписаний пакет → перевірка структури підпису → задача SUBMIT_DR в outbox. Сама відправка й опитування job — у воркері.</summary>
    public async Task<object> SubmitAsync(string orderId, EhealthSubmitRequest req)
    {
        _policy.Require("Відправка звіту в ЕСОЗ", SubmitRoles);
        var o = await LoadOrderAsync(orderId);
        var sub = await LatestSubmitAsync(o.Id) ?? throw Problem(409, "report-not-ready", "Звіт не підготовлено", "Спершу підготуйте пакет для підпису (prepare)");
        if (sub.Status is EhealthSubmitStatuses.Completed or EhealthSubmitStatuses.Registered or EhealthSubmitStatuses.Processing)
            throw new ConflictException($"Звіт уже відправлено: {EhealthSubmitStatuses.Label(sub.Status)}");
        if (sub.Status == EhealthSubmitStatuses.Annulled) throw new ConflictException("Звіт анульовано: підготуйте новий пакет");
        try { await RequireAuthAsync(); }
        catch (DomainProblemException) when (sub.SignedData != null)
        {
            sub.Status = EhealthSubmitStatuses.AuthRequired; o.EhealthStatus = EhealthSubmitStatuses.AuthRequired; // FR-SGN-008
            await _db.SaveChangesAsync();
            throw;
        }
        if (sub.PackageJson == null) throw Problem(409, "report-not-ready", "Звіт не підготовлено", "Спершу підготуйте пакет для підпису (prepare)");
        if (sub.SignedData == null || sub.Status == EhealthSubmitStatuses.ReadyToSign)
        {
            if (string.IsNullOrWhiteSpace(req.SignedData)) throw ValidationException.Field("signedData", "Підпишіть пакет КЕП (окремий крок навіть після входу через 2FA)");
            var signer = await CheckSignerAsync(req.Signer);
            // Структурна перевірка (формат, CAdES-X Long, збіг підписаного вмісту з пакетом); криптографічно підпис перевіряє ЕСОЗ
            var inspection = CadesInspector.Inspect(req.SignedData, new UTF8Encoding(false).GetBytes(sub.PackageJson));
            if (!inspection.Valid) throw Problem(422, "signature-invalid", "Підпис не придатний до відправки", string.Join("; ", inspection.Errors));
            sub.SignedData = req.SignedData.Trim(); sub.SignedBy = _current.EmployeeId; sub.SignedByName = signer.Name; sub.SignedOn = DateTime.UtcNow; sub.CertSerial = signer.CertSerial;
            sub.Status = EhealthSubmitStatuses.Signed; o.EhealthStatus = EhealthSubmitStatuses.Signed;
            _audit.Log("EHEALTH_SIGN", "lab_ehealth_submission", sub.Id, null, new { sub.PackageHash, sub.CertSerial, profile = inspection.Profile });
        }
        await EnqueueSubmitAsync(o, sub);
        if (req.SimulateNetworkError && _options.IsMock) _mock.QueueFailure("POST", "diagnostic_report_package", 503, "ЕСОЗ не відповідає");
        _audit.Log("EHEALTH_SUBMIT_QUEUED", "lab_ehealth_submission", sub.Id, null, new { sub.PackageHash });
        await _db.SaveChangesAsync();
        return Dto(o, sub);
    }

    private async Task EnqueueSubmitAsync(LabOrder o, LabEhealthSubmission sub)
    {
        var pkg = JsonNode.Parse(sub.PackageJson!)!;
        var reportId = pkg["diagnostic_report"]!["id"]!.GetValue<string>();
        var (patientId, srId, referralId) = await _builder.RoutingAsync(o);
        await _outbox.EnqueueAsync(EhealthOutboxKinds.SubmitDr, sub.Id, o.Id, $"SUBMIT_DR:{sub.Id}:{sub.PackageHash![..16]}", new { patientId, reportId, serviceRequestId = srId, referralId });
        sub.Status = EhealthSubmitStatuses.Processing; o.EhealthStatus = EhealthSubmitStatuses.Processing; sub.LastError = null;
    }

    /// <summary>Повторити останню зупинену задачу замовлення (помилка / вхід / відхилений job / скасована користувачем).</summary>
    public async Task<object> RetryAsync(string orderId)
    {
        _policy.Require("Відправка звіту в ЕСОЗ", SubmitRoles);
        var o = await LoadOrderAsync(orderId);
        await RequireAuthAsync();
        var row = await _db.EhealthOutbox.Where(x => x.OrderId == o.Id && (x.Status == EhealthOutboxStatuses.Failed || x.Status == EhealthOutboxStatuses.Dead))
                      .OrderByDescending(x => x.CreatedOn).FirstOrDefaultAsync()
                  ?? throw new ConflictException("Немає зупиненої задачі відправки в ЕСОЗ, яку можна повторити");
        await _outbox.RetryAsync(row.Id);
        var sub = await _db.EhealthSubmissions.FirstAsync(s => s.Id == row.EntityId);
        return Dto(o, sub);
    }

    // ------------------------------------------------------------------ анулювання
    private async Task<(LabEhealthSubmission Sub, EhealthPackage Cancel, string PatientId)> BuildCancelAsync(LabOrder o, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw ValidationException.Field("reason", "Вкажіть причину анулювання");
        var sub = await _db.EhealthSubmissions.Where(x => x.OrderId == o.Id && x.Kind == "SUBMIT" && (x.Status == EhealthSubmitStatuses.Completed || x.Status == EhealthSubmitStatuses.Registered))
                      .OrderByDescending(x => x.CreatedOn).FirstOrDefaultAsync() ?? throw new ConflictException("Немає зареєстрованого в ЕСОЗ звіту для анулювання");
        if (sub.PackageJson == null) throw new ConflictException("Немає збереженого пакета відправки");
        var (patientId, _, _) = await _builder.RoutingAsync(o);
        var (cancel, problems) = _builder.BuildCancel(sub.PackageJson, patientId, CancellationReasonCode, reason.Trim());
        if (cancel == null) throw Problem(409, "cancel-not-ready", "Пакет скасування не сформовано", string.Join("; ", problems));
        return (sub, cancel, patientId);
    }

    /// <summary>Пакет скасування для підпису (діагностичний звіт і всі спостереження — entered_in_error).</summary>
    public async Task<object> AnnulPrepareAsync(string orderId, EhealthSubmitRequest req)
    {
        _policy.Require("Анулювання звіту в ЕСОЗ", SubmitRoles);
        var o = await LoadOrderAsync(orderId);
        await RequireAuthAsync();
        var (_, cancel, patientId) = await BuildCancelAsync(o, req.Reason);
        return new { orderId = o.Id, packageHash = cancel.Hash, patientId, package = JsonSerializer.Deserialize<JsonElement>(cancel.Json) };
    }

    /// <summary>Анулювання звіту (FR-SGN-009): підписаний КЕП пакет скасування → задача CANCEL_DR; після job processed створюється новий висновок для повторної відправки.</summary>
    public async Task<object> AnnulAsync(string orderId, EhealthSubmitRequest req)
    {
        _policy.Require("Анулювання звіту в ЕСОЗ", SubmitRoles);
        var o = await LoadOrderAsync(orderId);
        var (sub, cancel, patientId) = await BuildCancelAsync(o, req.Reason);
        await RequireAuthAsync();
        if (string.IsNullOrWhiteSpace(req.SignedData)) throw ValidationException.Field("signedData", "Анулювання підписується КЕП");
        var active = await _db.EhealthOutbox.AnyAsync(x => x.OrderId == o.Id && EhealthOutboxStatuses.Active.Contains(x.Status));
        if (active) throw new ConflictException("Є незавершена відправка/погашення за цим замовленням: дочекайтесь завершення або скасуйте задачу в черзі");
        var signer = await CheckSignerAsync(req.Signer);
        var inspection = CadesInspector.Inspect(req.SignedData, cancel.Bytes);
        if (!inspection.Valid) throw Problem(422, "signature-invalid", "Підпис не придатний до відправки", string.Join("; ", inspection.Errors));
        var annul = new LabEhealthSubmission
        {
            OrderId = o.Id, DiagnosticReportId = sub.DiagnosticReportId, Kind = "ANNUL", Status = EhealthSubmitStatuses.Processing, SignedData = req.SignedData.Trim(), SignedBy = _current.EmployeeId,
            SignedByName = signer.Name, SignedOn = DateTime.UtcNow, CertSerial = signer.CertSerial, EhealthReportId = sub.EhealthReportId, PackageJson = cancel.Json, PackageHash = cancel.Hash
        };
        _db.EhealthSubmissions.Add(annul);
        // зупинені задачі погашення цього звіту втрачають сенс
        foreach (var stale in await _db.EhealthOutbox.Where(x => x.EntityId == sub.Id && x.Kind == EhealthOutboxKinds.CompleteSr && (x.Status == EhealthOutboxStatuses.Failed || x.Status == EhealthOutboxStatuses.Dead)).ToListAsync())
        { stale.Status = EhealthOutboxStatuses.Dead; stale.ErrorCode = EhealthOutboxErrors.CancelledByUser; stale.LastError = "Звіт анульовано"; stale.NextAttemptAt = null; }
        await _outbox.EnqueueAsync(EhealthOutboxKinds.CancelDr, annul.Id, o.Id, $"CANCEL_DR:{cancel.ReportId}:{annul.Id}",
            new { patientId, reportId = cancel.ReportId, submissionId = sub.Id, reason = req.Reason!.Trim() });
        _audit.Log("EHEALTH_ANNUL_QUEUED", "lab_ehealth_submission", annul.Id, null, new { req.Reason, annul.CertSerial });
        await _db.SaveChangesAsync();
        return Dto(o, annul);
    }

    // ------------------------------------------------------------------ стан і перелік
    public async Task<object> OrderStatusAsync(string orderId)
    {
        var o = await LoadOrderAsync(orderId);
        var subs = await _db.EhealthSubmissions.AsNoTracking().Where(x => x.OrderId == o.Id).OrderByDescending(x => x.CreatedOn).ToListAsync();
        var rows = await _db.EhealthOutbox.AsNoTracking().Where(x => x.OrderId == o.Id).OrderByDescending(x => x.CreatedOn).ToListAsync();
        var problems = await ProblemsAsync(o);
        var cancelPending = rows.Any(r => r.Kind == EhealthOutboxKinds.CancelDr && EhealthOutboxStatuses.Active.Contains(r.Status));
        return new
        {
            orderId = o.Id, o.OrderNumber, status = o.EhealthStatus, label = EhealthSubmitStatuses.Label(o.EhealthStatus), mode = _options.ModeName, simulated = _options.Simulated,
            canSubmit = problems.Count == 0 && (EhealthSubmitStatuses.CanSubmit.Contains(o.EhealthStatus) || o.EhealthStatus == EhealthSubmitStatuses.Annulled),
            canAnnul = o.EhealthStatus is EhealthSubmitStatuses.Completed or EhealthSubmitStatuses.Registered && !cancelPending, cancelPending,
            canRetry = rows.Any(r => EhealthOutboxService.CanRetry(r)), problems,
            submissions = subs.Select(s => new { s.Id, s.Kind, s.Status, label = EhealthSubmitStatuses.Label(s.Status), s.PackageHash, s.SignedByName, s.SignedOn, s.CertSerial, s.JobId, s.EhealthReportId, s.Attempts, s.LastError, s.NextAttemptAt, s.CompletedAt, s.CreatedOn }),
            outbox = rows.Select(r => OutboxDto(r))
        };
    }

    private static object OutboxDto(EheOutbox r) => new
    {
        r.Id, r.Kind, kindLabel = EhealthOutboxKinds.Label(r.Kind), r.Status, statusLabel = EhealthOutboxStatuses.Label(r.Status), r.Attempts, r.PollAttempts, r.NextAttemptAt,
        r.LastError, r.ErrorCode, r.JobId, r.CreatedOn, r.UpdatedAt, r.DoneAt, canRetry = EhealthOutboxService.CanRetry(r), canCancel = EhealthOutboxService.CanCancel(r)
    };

    /// <summary>Вихідні задачі відправки (грід «Відправка в ЕСОЗ»): фільтри за станом, датою, пошук за номером замовлення/направлення/пацієнтом.</summary>
    public async Task<object> SubmissionsAsync(string? status, DateTime? from, DateTime? to, string? search, string? kind)
    {
        var st = status?.Trim().ToUpperInvariant();
        var q = from x in _db.EhealthOutbox.AsNoTracking()
                join o in _db.Orders.AsNoTracking() on x.OrderId equals o.Id into og
                from o in og.DefaultIfEmpty()
                select new { x, o };
        if (!string.IsNullOrEmpty(st) && st != "NOT_SENT") q = q.Where(t => t.x.Status == st);
        if (!string.IsNullOrWhiteSpace(kind)) { var k = kind.Trim().ToUpperInvariant(); q = q.Where(t => t.x.Kind == k); }
        if (from.HasValue) q = q.Where(t => t.x.CreatedOn >= from.Value);
        if (to.HasValue) { var end = to.Value.Date.AddDays(1); q = q.Where(t => t.x.CreatedOn < end); }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(t => t.o != null && (t.o.OrderNumber.Contains(s) || (t.o.Patient!.Caption != null && t.o.Patient.Caption.Contains(s))
                             || (t.o.Referral != null && t.o.Referral.RegNumber != null && t.o.Referral.RegNumber.Contains(s))
                             || (t.o.PaperReferral != null && t.o.PaperReferral.RegNumber != null && t.o.PaperReferral.RegNumber.Contains(s))));
        }
        var rows = st == "NOT_SENT" ? new() : await q.OrderByDescending(t => t.x.CreatedOn).Take(500)
            .Select(t => new
            {
                t.x, orderNumber = t.o == null ? null : t.o.OrderNumber, patientName = t.o == null ? null : t.o.Patient!.Caption,
                referralNumber = t.o == null ? null : (t.o.Referral != null ? t.o.Referral.RegNumber : t.o.PaperReferral != null ? t.o.PaperReferral.RegNumber : null)
            }).ToListAsync();
        var subIds = rows.Select(r => r.x.EntityId).Distinct().ToList();
        var subs = await _db.EhealthSubmissions.AsNoTracking().Where(s => subIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);

        var nq = _db.Orders.AsNoTracking().Include(o => o.Patient).Include(o => o.Referral).Include(o => o.PaperReferral)
            .Where(o => o.Status == OrderStatuses.Released && o.EhealthStatus == EhealthSubmitStatuses.NotSent && o.DiagnosticReportId != null);
        if (from.HasValue) nq = nq.Where(o => o.ReleasedAt >= from.Value);
        if (to.HasValue) { var end = to.Value.Date.AddDays(1); nq = nq.Where(o => o.ReleasedAt < end); }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            nq = nq.Where(o => o.OrderNumber.Contains(s) || (o.Patient!.Caption != null && o.Patient.Caption.Contains(s)) || (o.Referral != null && o.Referral.RegNumber != null && o.Referral.RegNumber.Contains(s))
                               || (o.PaperReferral != null && o.PaperReferral.RegNumber != null && o.PaperReferral.RegNumber.Contains(s)));
        }
        var notSent = string.IsNullOrEmpty(st) || st == "NOT_SENT"
            ? await nq.OrderByDescending(o => o.ReleasedAt).Take(200).Select(o => new { o.Id, o.OrderNumber, patientName = o.Patient!.Caption, o.ReleasedAt, o.ReferralContext }).ToListAsync()
            : null;

        var summary = await _db.EhealthOutbox.AsNoTracking().GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        return new
        {
            items = rows.Select(r =>
            {
                subs.TryGetValue(r.x.EntityId, out var sub);
                return new
                {
                    r.x.Id, r.x.OrderId, r.orderNumber, r.patientName, r.referralNumber, r.x.Kind, kindLabel = EhealthOutboxKinds.Label(r.x.Kind), r.x.Status, statusLabel = EhealthOutboxStatuses.Label(r.x.Status),
                    submissionStatus = sub?.Status, submissionLabel = sub == null ? null : EhealthSubmitStatuses.Label(sub.Status), r.x.Attempts, r.x.PollAttempts, r.x.NextAttemptAt, r.x.LastError, r.x.ErrorCode, r.x.JobId,
                    ehealthReportId = sub?.EhealthReportId, signedByName = sub?.SignedByName, signedOn = sub?.SignedOn, r.x.CreatedOn, r.x.UpdatedAt,
                    canRetry = EhealthOutboxService.CanRetry(r.x), canCancel = EhealthOutboxService.CanCancel(r.x)
                };
            }),
            notSent,
            summary,
            mode = _options.ModeName
        };
    }

    /// <summary>Виконати прохід черги негайно (кнопка «Обробити зараз»; зазвичай воркер робить це сам кожні кілька секунд).</summary>
    public async Task<object> RunNowAsync()
    {
        _policy.Require("Відправка звіту в ЕСОЗ", SubmitRoles);
        var n = await _engine.RunDueAsync();
        return new { processed = n };
    }

    public async Task<object> RetryTaskAsync(string id)
    {
        _policy.Require("Відправка звіту в ЕСОЗ", SubmitRoles);
        await RequireAuthAsync();
        var r = await _outbox.RetryAsync(id);
        return OutboxDto(r);
    }

    public async Task<object> CancelTaskAsync(string id)
    {
        _policy.Require("Відправка звіту в ЕСОЗ", SubmitRoles);
        var r = await _outbox.CancelAsync(id);
        return OutboxDto(r);
    }

    private static object Dto(LabOrder o, LabEhealthSubmission s) => new
    {
        orderId = o.Id, o.OrderNumber, ehealthStatus = o.EhealthStatus, label = EhealthSubmitStatuses.Label(o.EhealthStatus),
        submission = new { s.Id, s.Kind, s.Status, s.PackageHash, package = s.PackageJson == null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(s.PackageJson), s.SignedByName, s.SignedOn, s.CertSerial, s.JobId, s.EhealthReportId, s.Attempts, s.LastError, s.NextAttemptAt }
    };
}
