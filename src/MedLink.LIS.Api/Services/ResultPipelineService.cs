// =============================================================================
// Єдиний конвеєр результатів (ручне введення, коннектор, імпорт): контекст пацієнта →
// каскад норм → прапорець → delta-check → reflex → автоверифікація (lockout блокує) →
// історія + аудит. Також верифікація/відхилення/повтор/reopen/видалення результату.
// =============================================================================
using System.Globalization;
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class ResultEntry
{
    public string OrderTestId { get; set; } = "";
    public double? NumericValue { get; set; }
    public string? StringValue { get; set; }
    public string? Unit { get; set; }
    public string? Comment { get; set; }
    public string? ReportText { get; set; }
    public string? AnalyzerId { get; set; }
    public string? AnalyzerFlags { get; set; }
    public string? RawMessageId { get; set; }
    public DateTime? MeasuredAt { get; set; }
    /// <summary>Технічний актор (коннектор/імпорт) — без рольових обмежень, з автоприйомом проби.</summary>
    public bool IsSystem { get; set; }
    /// <summary>Результат зовнішньої лабораторії (send-out): виконавець і номер її бланка.</summary>
    public string? PerformerId { get; set; }
    public string? ExternalReference { get; set; }
    /// <summary>Референсний інтервал зовнішньої лабораторії (якщо в довіднику норм ЛІС немає шару для показника).</summary>
    public string? ReferenceText { get; set; }
}

public sealed class ResultPipelineService
{
    private readonly LisDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRolePolicy _policy;
    private readonly ICurrentEmployee _current;
    private readonly OrderStateService _state;
    private readonly SampleService _samples;
    private readonly ProgressiveReleaseService _release;
    private readonly ILogger<ResultPipelineService> _logger;

    public ResultPipelineService(LisDbContext db, IAuditService audit, IRolePolicy policy, ICurrentEmployee current, OrderStateService state,
        SampleService samples, ProgressiveReleaseService release, ILogger<ResultPipelineService> logger)
    {
        _db = db; _audit = audit; _policy = policy; _current = current; _state = state; _samples = samples; _release = release; _logger = logger;
    }

    private IRolePolicy PolicyFor(bool isSystem) => isSystem ? new SystemRolePolicy() : _policy;

    public IQueryable<LabOrderTest> RowQuery() => _db.OrderTests
        .Include(t => t.Test).ThenInclude(d => d!.LabSection).Include(t => t.Sample).Include(t => t.Profile).Include(t => t.AssignedAnalyzer)
        .Include(t => t.Order).ThenInclude(o => o!.Patient)
        .Include(t => t.Order).ThenInclude(o => o!.Tests)
        .Include(t => t.Order).ThenInclude(o => o!.Samples)
        .Include(t => t.Result).ThenInclude(r => r!.Analyzer);

    public async Task<LabOrderTest> LoadAsync(string orderTestId) =>
        await RowQuery().FirstOrDefaultAsync(t => t.Id == orderTestId) ?? throw NotFoundException.For("Тест замовлення", orderTestId);

    public async Task<WorklistRowDto> RowAsync(string orderTestId)
    {
        var t = await LoadAsync(orderTestId);
        var locked = await IsLockedOutAsync(t.Result?.AnalyzerId ?? t.AssignedAnalyzerId, t.TestCode);
        return DtoMapper.ToWorklistRow(t, locked, _policy);
    }

    public async Task<bool> IsLockedOutAsync(string? analyzerId, string testCode)
    {
        if (string.IsNullOrEmpty(analyzerId)) return false;
        return await _db.Lockouts.AnyAsync(l => l.AnalyzerId == analyzerId && l.ResolvedAt == null && (l.TestCode == null || l.TestCode == testCode));
    }

    // ------------------------------------------------------------------ pipeline
    public async Task<WorklistRowDto> ApplyAsync(ResultEntry entry)
    {
        var test = await LoadAsync(entry.OrderTestId);
        var order = test.Order!;
        var def = test.Test ?? throw new ValidationException($"Тест {test.TestCode} не має визначення у довіднику");
        var policy = PolicyFor(entry.IsSystem);

        if (OrderStatuses.Terminal.Contains(order.Status) || order.Status == OrderStatuses.Released)
            throw new ConflictException($"Замовлення {order.OrderNumber} у статусі {order.Status} — введення результатів неможливе");

        // Коннектор: проба на приладі = фактично прийнята
        if (entry.IsSystem && test.Sample != null) _samples.SystemReceive(test.Sample);
        if (entry.IsSystem && order.Status == OrderStatuses.New)
            throw new ConflictException($"Замовлення {order.OrderNumber} ще не має відмітки забору біоматеріалу");

        var testAction = test.Status is OrderTestStatuses.Resulted or OrderTestStatuses.NeedsReview ? TestActions.EditResult : TestActions.EnterResult;
        policy.Ensure(LisEntities.OrderTest, testAction, test.Status, $"Тест {test.TestCode}");
        policy.Ensure(LisEntities.Order, OrderActions.EnterResult, order.Status, $"Замовлення {order.OrderNumber}");

        // Значення
        double? numeric = entry.NumericValue;
        string? text = entry.StringValue?.Trim();
        if (numeric == null && !string.IsNullOrEmpty(text) && def.ResultType == "NUMERIC" &&
            double.TryParse(text.Replace(',', '.').TrimStart('<', '>', ' '), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            numeric = parsed;
        if (def.ResultType == "REPORT" && string.IsNullOrWhiteSpace(entry.ReportText) && string.IsNullOrWhiteSpace(text)) throw new ValidationException("Для патогістологічного/цитологічного дослідження вкажіть reportText (висновок)");
        if (def.ResultType == "REPORT" && string.IsNullOrEmpty(text)) text = entry.ReportText!.Length > 120 ? entry.ReportText.Substring(0, 117) + "..." : entry.ReportText;
        if (numeric == null && string.IsNullOrEmpty(text)) throw new ValidationException("Результат порожній: вкажіть numericValue або stringValue");
        if (def.ResultType == "DROPDOWN" && text != null && def.DropdownOptions.Count > 0 && !def.DropdownOptions.Contains(text, StringComparer.OrdinalIgnoreCase))
            throw ValidationException.Field("stringValue", $"Значення має бути одним із: {string.Join(", ", def.DropdownOptions)}");
        if (numeric.HasValue) numeric = Math.Round(numeric.Value, Math.Clamp(def.DecimalPlaces, 0, 6));

        var now = DateTime.UtcNow;
        var ctx = BuildPatientContext(order, order.Patient, def);

        // Каскад норм
        var layers = await _db.ReferenceLayers.AsNoTracking().Where(l => l.TestCode == def.Code && l.IsActive).ToListAsync();
        var resolution = NormsCascadeResolver.Resolve(layers.Select(ToInput), ctx);

        // Прапорець
        string flag;
        if (numeric.HasValue) flag = ResultFlagger.Flag(numeric.Value, resolution.NormLow, resolution.NormHigh, resolution.CritLow, resolution.CritHigh);
        else flag = ResultFlagger.FlagText(text, resolution.NormText ?? FallbackNormText(def));

        // Delta-check
        DeltaCheckResult delta = new() { HasPrevious = false };
        if (numeric.HasValue)
        {
            var prev = await _db.Results.AsNoTracking()
                .Where(r => r.OrderTestId != test.Id && r.NumericValue != null && r.OrderTest!.TestCode == def.Code
                            && r.OrderTest.Order!.PatientId == order.PatientId && r.OrderTest.Status != OrderTestStatuses.Rejected)
                .OrderByDescending(r => r.EnteredAt).Select(r => new { r.NumericValue, r.EnteredAt }).FirstOrDefaultAsync();
            var maxPct = def.DeltaCheckMaxPct ?? resolution.DeltaCheckMaxPct;
            delta = DeltaCheckEvaluator.Evaluate(numeric.Value, prev?.NumericValue, prev?.EnteredAt, now, def.DeltaCheckHours, maxPct);
        }

        // Lockout
        var analyzerId = entry.AnalyzerId ?? test.AssignedAnalyzerId;
        var locked = await IsLockedOutAsync(analyzerId, def.Code);

        // Автоверифікація
        var decision = AutoVerificationEngine.Evaluate(new AutoVerificationInput
        {
            Flag = flag, AnalyzerFlags = entry.AnalyzerFlags, DeltaAlert = delta.IsAlert,
            RequiresManualVerification = def.RequiresManualVerification || (entry.PerformerId ?? test.PerformerId) != null, HasActiveLockout = locked, HasValue = numeric.HasValue || !string.IsNullOrEmpty(text)
        });

        // Результат (одна версія на тест, попередні — в історії)
        var result = test.Result;
        var isNew = result == null;
        if (result == null)
        {
            result = new LabTestResult { OrderTestId = test.Id };
            _db.Results.Add(result);
            test.Result = result;
        }
        else
        {
            _db.ResultHistory.Add(new LabResultHistory
            {
                OrderTestId = test.Id, ResultId = result.Id, Version = result.Version, Action = "SUPERSEDED", NumericValue = result.NumericValue,
                StringValue = result.StringValue, Flag = result.Flag, ActorId = result.EnteredById, At = result.EnteredAt, SnapshotJson = Snapshot(result)
            });
            result.Version++;
        }

        result.NumericValue = numeric;
        result.StringValue = numeric.HasValue ? null : text;
        result.Unit = entry.Unit ?? resolution.Unit ?? def.Unit;
        result.NormLow = resolution.NormLow; result.NormHigh = resolution.NormHigh; result.CritLow = resolution.CritLow; result.CritHigh = resolution.CritHigh;
        result.ReferenceDisplay = resolution.Found ? resolution.ReferenceDisplay : (entry.ReferenceText?.Trim() ?? "");
        result.Flag = flag;
        result.AppliedLayerId = resolution.WinningLayer?.Id;
        result.AppliedLayerName = resolution.WinningLayer?.NormName;
        result.DeltaPercent = delta.DeltaPercent;
        result.DeltaAlert = delta.IsAlert;
        result.PreviousValue = delta.HasPrevious ? delta.PreviousValue : null;
        result.PreviousAt = delta.HasPrevious ? delta.PreviousAt : null;
        result.AnalyzerId = analyzerId;
        result.AnalyzerFlags = entry.AnalyzerFlags;
        result.RawMessageId = entry.RawMessageId;
        result.OperatorComment = entry.Comment;
        result.ReportText = entry.ReportText;
        result.EnteredById = entry.IsSystem ? null : _current.EmployeeId;
        result.EnteredAt = now;
        result.MeasuredAt = entry.MeasuredAt;
        result.PerformerId = entry.PerformerId ?? test.PerformerId;
        result.ExternalReference = entry.ExternalReference;
        result.IsAutoVerified = decision.Approved;
        result.AutoVerifyBlockReason = decision.Approved ? null : decision.Summary;
        result.VerifiedById = null; result.VerifiedAt = null; result.VerificationComment = null;

        if (decision.Approved)
        {
            test.Status = OrderTestStatuses.AutoVerified;
            result.VerifiedAt = now;
            await _release.ReleaseVerifiedTestAsync(test, order);
        }
        else
        {
            test.ReleasedAt = null;
            test.Status = ResultFlags.IsCritical(flag) || delta.IsAlert ? OrderTestStatuses.NeedsReview : OrderTestStatuses.Resulted;
        }
        test.RejectReason = null;
        if (analyzerId != null) test.AssignedAnalyzerId = analyzerId;

        _db.ResultHistory.Add(new LabResultHistory
        {
            OrderTestId = test.Id, ResultId = result.Id, Version = result.Version, Action = isNew ? "ENTERED" : "CORRECTED", NumericValue = numeric,
            StringValue = result.StringValue, Flag = flag, ActorId = result.EnteredById ?? (entry.IsSystem ? "SYSTEM:" + analyzerId : null), At = now,
            Comment = entry.Comment, SnapshotJson = Snapshot(result)
        });

        // Reflex
        var rules = await _db.ReflexRules.AsNoTracking().Where(r => r.IsActive && r.TriggerTestCode == def.Code).ToListAsync();
        var triggered = ReflexRuleEngine.Evaluate(rules.Select(r => new ReflexRuleInput
        {
            Id = r.Id, TriggerTestCode = r.TriggerTestCode, ConditionOperator = r.ConditionOperator, ThresholdValue = r.ThresholdValue,
            ReflexTestCode = r.ReflexTestCode, AutoApprove = r.AutoApprove, RequiresSameSample = r.RequiresSameSample, Description = r.Description, IsActive = r.IsActive
        }), def.Code, numeric, flag, order.Tests.Select(t => t.TestCode));
        foreach (var trig in triggered)
        {
            var reflexDef = await _db.Tests.AsNoTracking().FirstOrDefaultAsync(x => x.Code == trig.Rule.ReflexTestCode && x.IsActive);
            if (reflexDef == null) { _logger.LogWarning("Reflex: тест {Code} не знайдено", trig.Rule.ReflexTestCode); continue; }
            string? sampleId = trig.Rule.RequiresSameSample ? test.SampleId
                : order.Samples.FirstOrDefault(s => s.BiomaterialTypeId == reflexDef.BiomaterialTypeId && (reflexDef.TubeTypeId == null || s.TubeTypeId == reflexDef.TubeTypeId)
                                                  && s.Status != SampleStatuses.Rejected)?.Id;
            var reflexTest = new LabOrderTest
            {
                OrderId = order.Id, SampleId = sampleId, TestId = reflexDef.Id, TestCode = reflexDef.Code, TestName = reflexDef.Name,
                Status = OrderTestStatuses.Pending, IsReflex = true, ReflexFromTestId = test.Id,
                ReflexNeedsConfirmation = !trig.Rule.RequiresSameSample || sampleId == null, DisplayOrder = order.Tests.Max(t => t.DisplayOrder) + 1
            };
            order.Tests.Add(reflexTest);
            _audit.Log("REFLEX_CREATED", "lab_order_test", reflexTest.Id, null, new { trig.Rule.Id, trig.Reason, reflexTest.TestCode, reflexTest.ReflexNeedsConfirmation }, trig.Rule.Description);
        }

        _state.MarkInProgress(order);
        _state.RecomputeReopen(order);
        _state.RecomputeCompletion(order);

        _audit.Log(isNew ? "RESULT_ENTERED" : "RESULT_CORRECTED", "lab_test_result", result.Id, null,
            new { test.TestCode, numeric, text, flag, delta.DeltaPercent, delta.IsAlert, autoVerified = decision.Approved, block = result.AutoVerifyBlockReason, layer = result.AppliedLayerName });
        await _db.SaveChangesAsync();
        return await RowAsync(test.Id);
    }

    internal static PatientContext BuildPatientContext(LabOrder order, MisPatientCard? patient, LabTestDefinition def)
    {
        var at = order.OrderDatetime;
        return new PatientContext
        {
            Gender = patient?.Gender ?? "U",
            AgeDays = patient?.Birthday.HasValue == true ? AgeUnits.AgeDays(patient.Birthday.Value, at) : 30 * 365.25,
            IsPregnant = order.IsPregnant, PregnancyWeek = order.PregnancyWeek, MenstrualPhase = order.MenstrualPhase, Icd10Code = order.Icd10Code,
            MethodCode = def.MethodCode
        };
    }

    internal static ReferenceLayerInput ToInput(LabReferenceLayer l) => new()
    {
        Id = l.Id, TestCode = l.TestCode, MethodCode = l.MethodCode, LayerType = l.LayerType, PriorityOrder = l.PriorityOrder, NormName = l.NormName,
        Gender = l.Gender, IsGender = l.IsGender, AgeUnit = l.AgeUnit, AgeFrom = l.AgeFrom, AgeTo = l.AgeTo, IsAge = l.IsAge, IsMenstrualPhase = l.IsMenstrualPhase,
        MenstrualPhase = l.MenstrualPhase, IsPregnancy = l.IsPregnancy, PregnancyWeekFrom = l.PregnancyWeekFrom, PregnancyWeekTo = l.PregnancyWeekTo,
        Icd10Code = l.Icd10Code, NormLow = l.NormLow, NormHigh = l.NormHigh, CritLow = l.CritLow, CritHigh = l.CritHigh, NormText = l.NormText, Unit = l.Unit,
        DeltaCheckMaxPct = l.DeltaCheckMaxPct
    };

    private static string? FallbackNormText(LabTestDefinition def) => def.ResultType is "TEXT" or "DROPDOWN" && def.DropdownOptions.Count > 0 ? def.DropdownOptions[0] : null;

    private static string Snapshot(LabTestResult r) => JsonSerializer.Serialize(new
    {
        r.NumericValue, r.StringValue, r.Unit, r.Flag, r.NormLow, r.NormHigh, r.CritLow, r.CritHigh, r.DeltaPercent, r.DeltaAlert, r.AnalyzerId, r.AnalyzerFlags,
        r.IsAutoVerified, r.VerifiedById, r.VerifiedAt, r.VerificationComment, r.OperatorComment, r.EnteredById, r.EnteredAt, r.Version
    });

    // ------------------------------------------------------------------ verification & lifecycle
    public async Task<WorklistRowDto> VerifyAsync(string orderTestId, VerifyRequest req)
    {
        var test = await LoadAsync(orderTestId);
        var result = test.Result ?? throw new ConflictException($"Тест {test.TestCode} не має результату");
        _policy.Ensure(LisEntities.OrderTest, TestActions.Verify, test.Status, $"Тест {test.TestCode}");
        if (ResultFlags.IsCritical(result.Flag) && string.IsNullOrWhiteSpace(req.Comment))
            throw ValidationException.Field("comment", $"Критичне значення {test.TestCode} ({result.Flag}): коментар лікаря обов'язковий");
        var locked = await IsLockedOutAsync(result.AnalyzerId ?? test.AssignedAnalyzerId, test.TestCode);
        if (locked && !req.Override) throw new ConflictException($"Аналізатор заблоковано (lockout ВКЯ) для {test.TestCode}: верифікація можлива лише з override та коментарем");
        if (locked && string.IsNullOrWhiteSpace(req.Comment)) throw ValidationException.Field("comment", "Override lockout потребує коментаря");

        var before = test.Status;
        test.Status = OrderTestStatuses.Verified;
        result.VerifiedById = _current.EmployeeId;
        result.VerifiedAt = DateTime.UtcNow;
        result.VerificationComment = req.Comment;
        result.IsAutoVerified = false;
        await _release.ReleaseVerifiedTestAsync(test, test.Order!);
        _db.ResultHistory.Add(new LabResultHistory { OrderTestId = test.Id, ResultId = result.Id, Version = result.Version, Action = "VERIFIED", NumericValue = result.NumericValue, StringValue = result.StringValue, Flag = result.Flag, ActorId = _current.EmployeeId, Comment = req.Comment, SnapshotJson = Snapshot(result) });
        _state.RecomputeCompletion(test.Order!);
        _audit.Log("VERIFY", "lab_test_result", result.Id, new { status = before }, new { test.Status, req.Comment, req.Override, lockoutOverride = locked });
        await _db.SaveChangesAsync();
        return await RowAsync(test.Id);
    }

    public async Task<object> VerifyBatchAsync(VerifyBatchRequest req)
    {
        var verified = new List<string>();
        var skipped = new List<object>();
        foreach (var id in req.OrderTestIds.Distinct())
        {
            try { await VerifyAsync(id, new VerifyRequest { Comment = req.Comment }); verified.Add(id); }
            catch (LisException ex) { skipped.Add(new { id, reason = ex.Message }); }
        }
        return new { verified = verified.Count, verifiedIds = verified, skipped };
    }

    public async Task<WorklistRowDto> RejectAsync(string orderTestId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw ValidationException.Field("reason", "Вкажіть причину відхилення");
        var test = await LoadAsync(orderTestId);
        var rule = _policy.Ensure(LisEntities.OrderTest, TestActions.Reject, test.Status, $"Тест {test.TestCode}");
        var before = test.Status;
        test.Status = rule.ToStatus!;
        test.RejectReason = reason;
        if (test.Result != null)
            _db.ResultHistory.Add(new LabResultHistory { OrderTestId = test.Id, ResultId = test.Result.Id, Version = test.Result.Version, Action = "REJECTED", Flag = test.Result.Flag, ActorId = _current.EmployeeId, Comment = reason });
        _state.RecomputeCompletion(test.Order!);
        _audit.Log("REJECT", "lab_order_test", test.Id, new { status = before }, new { test.Status, reason });
        await _db.SaveChangesAsync();
        return await RowAsync(test.Id);
    }

    public async Task<WorklistRowDto> RerunAsync(string orderTestId, string? reason)
    {
        var test = await LoadAsync(orderTestId);
        var rule = _policy.Ensure(LisEntities.OrderTest, TestActions.Rerun, test.Status, $"Тест {test.TestCode}");
        var before = test.Status;
        test.Status = rule.ToStatus!;
        test.ReleasedAt = null;
        if (test.Result != null)
            _db.ResultHistory.Add(new LabResultHistory { OrderTestId = test.Id, ResultId = test.Result.Id, Version = test.Result.Version, Action = "RERUN", NumericValue = test.Result.NumericValue, Flag = test.Result.Flag, ActorId = _current.EmployeeId, Comment = reason });
        _state.RecomputeReopen(test.Order!);
        _audit.Log("RERUN", "lab_order_test", test.Id, new { status = before }, new { test.Status, reason });
        await _db.SaveChangesAsync();
        return await RowAsync(test.Id);
    }

    public async Task<WorklistRowDto> ReopenAsync(string orderTestId, string? comment)
    {
        var test = await LoadAsync(orderTestId);
        var rule = _policy.Ensure(LisEntities.OrderTest, TestActions.Reopen, test.Status, $"Тест {test.TestCode}");
        var order = test.Order!;
        if (order.Status == OrderStatuses.Released) throw new ConflictException($"Замовлення {order.OrderNumber} видано — спочатку поверніть замовлення в роботу (REOPEN)");
        var before = test.Status;
        test.Status = test.Result == null ? OrderTestStatuses.Pending : rule.ToStatus!;
        test.RejectReason = null;
        test.ReleasedAt = null;
        if (test.Result != null)
        {
            test.Result.IsAutoVerified = false; test.Result.VerifiedAt = null; test.Result.VerifiedById = null;
            _db.ResultHistory.Add(new LabResultHistory { OrderTestId = test.Id, ResultId = test.Result.Id, Version = test.Result.Version, Action = "REOPENED", NumericValue = test.Result.NumericValue, Flag = test.Result.Flag, ActorId = _current.EmployeeId, Comment = comment });
        }
        _state.RecomputeReopen(order);
        _audit.Log("REOPEN", "lab_order_test", test.Id, new { status = before }, new { test.Status }, comment);
        await _db.SaveChangesAsync();
        return await RowAsync(test.Id);
    }

    public async Task<WorklistRowDto> DeleteResultAsync(string orderTestId, string? reason)
    {
        var test = await LoadAsync(orderTestId);
        var rule = _policy.Ensure(LisEntities.OrderTest, TestActions.DeleteResult, test.Status, $"Тест {test.TestCode}");
        var result = test.Result ?? throw new ConflictException("Результат відсутній");
        _db.ResultHistory.Add(new LabResultHistory { OrderTestId = test.Id, ResultId = result.Id, Version = result.Version, Action = "DELETED", NumericValue = result.NumericValue, StringValue = result.StringValue, Flag = result.Flag, ActorId = _current.EmployeeId, Comment = reason, SnapshotJson = Snapshot(result) });
        _db.Results.Remove(result);
        test.Result = null;
        test.Status = rule.ToStatus!;
        _state.RecomputeReopen(test.Order!);
        _audit.Log("RESULT_DELETED", "lab_test_result", result.Id, new { result.NumericValue, result.StringValue, result.Flag }, null, reason);
        await _db.SaveChangesAsync();
        return await RowAsync(test.Id);
    }

    public async Task<WorklistRowDto> AssignAnalyzerAsync(string orderTestId, string? analyzerId)
    {
        var test = await LoadAsync(orderTestId);
        _policy.Ensure(LisEntities.OrderTest, TestActions.AssignAnalyzer, test.Status, $"Тест {test.TestCode}");
        if (analyzerId != null && !await _db.Analyzers.AnyAsync(a => a.Id == analyzerId)) throw NotFoundException.For("Аналізатор", analyzerId);
        var before = test.AssignedAnalyzerId;
        test.AssignedAnalyzerId = analyzerId;
        if (analyzerId != null && test.Status == OrderTestStatuses.Pending) test.Status = OrderTestStatuses.InAnalysis;
        _audit.Log("ASSIGN_ANALYZER", "lab_order_test", test.Id, new { analyzerId = before }, new { analyzerId });
        await _db.SaveChangesAsync();
        return await RowAsync(test.Id);
    }

    public async Task<WorklistRowDto> TransitionAsync(string orderTestId, string action, string? comment)
    {
        return action.ToUpperInvariant() switch
        {
            TestActions.Verify => await VerifyAsync(orderTestId, new VerifyRequest { Comment = comment }),
            TestActions.Reject => await RejectAsync(orderTestId, comment ?? ""),
            TestActions.Rerun => await RerunAsync(orderTestId, comment),
            TestActions.Reopen => await ReopenAsync(orderTestId, comment),
            TestActions.DeleteResult => await DeleteResultAsync(orderTestId, comment),
            TestActions.StartAnalysis => await StartAnalysisAsync(orderTestId),
            _ => throw new ValidationException($"Невідома дія '{action}' для тесту")
        };
    }

    public async Task<WorklistRowDto> StartAnalysisAsync(string orderTestId)
    {
        var test = await LoadAsync(orderTestId);
        var rule = _policy.Ensure(LisEntities.OrderTest, TestActions.StartAnalysis, test.Status, $"Тест {test.TestCode}");
        test.Status = rule.ToStatus!;
        _audit.Log("START_ANALYSIS", "lab_order_test", test.Id, null, new { test.Status });
        await _db.SaveChangesAsync();
        return await RowAsync(test.Id);
    }

    public async Task<List<LabResultHistory>> HistoryAsync(string orderTestId)
    {
        if (!await _db.OrderTests.AnyAsync(t => t.Id == orderTestId)) throw NotFoundException.For("Тест замовлення", orderTestId);
        return await _db.ResultHistory.AsNoTracking().Where(h => h.OrderTestId == orderTestId).OrderByDescending(h => h.At).ToListAsync();
    }

    /// <summary>Повторна спроба автоверифікації для вказаних (або всіх RESULTED) тестів.</summary>
    public async Task<object> AutoVerifyAsync(List<string>? ids)
    {
        _policy.Require("Автоверифікація", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var q = RowQuery().Where(t => t.Result != null && (t.Status == OrderTestStatuses.Resulted || t.Status == OrderTestStatuses.NeedsReview));
        if (ids != null && ids.Count > 0) q = q.Where(t => ids.Contains(t.Id));
        var tests = await q.ToListAsync();
        var verified = new List<string>();
        var blocked = new List<object>();
        foreach (var t in tests)
        {
            var r = t.Result!;
            var locked = await IsLockedOutAsync(r.AnalyzerId ?? t.AssignedAnalyzerId, t.TestCode);
            var decision = AutoVerificationEngine.Evaluate(new AutoVerificationInput
            {
                Flag = r.Flag, AnalyzerFlags = r.AnalyzerFlags, DeltaAlert = r.DeltaAlert, RequiresManualVerification = t.Test?.RequiresManualVerification ?? false,
                HasActiveLockout = locked, HasValue = r.NumericValue.HasValue || !string.IsNullOrEmpty(r.StringValue)
            });
            if (decision.Approved)
            {
                t.Status = OrderTestStatuses.AutoVerified; r.IsAutoVerified = true; r.VerifiedAt = DateTime.UtcNow; r.AutoVerifyBlockReason = null;
                await _release.ReleaseVerifiedTestAsync(t, t.Order!);
                _db.ResultHistory.Add(new LabResultHistory { OrderTestId = t.Id, ResultId = r.Id, Version = r.Version, Action = "AUTO_VERIFIED", NumericValue = r.NumericValue, Flag = r.Flag, ActorId = _current.EmployeeId });
                _state.RecomputeCompletion(t.Order!);
                verified.Add(t.Id);
            }
            else { r.AutoVerifyBlockReason = decision.Summary; blocked.Add(new { id = t.Id, t.TestCode, reason = decision.Summary }); }
        }
        _audit.Log("AUTOVERIFY_BATCH", "lab_order_test", null, null, new { verified = verified.Count, blocked = blocked.Count });
        await _db.SaveChangesAsync();
        return new { verified = verified.Count, verifiedIds = verified, blocked };
    }
}
