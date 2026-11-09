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
    // --- Розрахункові показники (FR-WS-005): внутрішній запис результату після обчислення формули ---
    public bool IsCalculated { get; set; }
    public string? CalcFormula { get; set; }
    public string? CalcInputsJson { get; set; }
    // --- Повтор і розведення ---
    /// <summary>Коефіцієнт розведення зразка (≥ 1). null — береться заплановане розведення повтору.</summary>
    public double? DilutionFactor { get; set; }
    /// <summary>true — NumericValue уже перераховано на вихідний зразок (так віддають прилади); false — це вимір розведеного зразка, його множимо на коефіцієнт.</summary>
    public bool ValueIsFinal { get; set; }
    /// <summary>Прилад повідомив «більше за верхню межу» (значення вийшло за діапазон).</summary>
    public bool AboveRange { get; set; }
    /// <summary>Прийняття іншої спроби вимірювання (без нового вимірювання).</summary>
    public string? AcceptAttemptId { get; set; }
}

public sealed partial class ResultPipelineService
{
    private readonly LisDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRolePolicy _policy;
    private readonly ICurrentEmployee _current;
    private readonly OrderStateService _state;
    private readonly SampleService _samples;
    private readonly ProgressiveReleaseService _release;
    private readonly ILogger<ResultPipelineService> _logger;

    private readonly BillingService _billing;
    private readonly ReagentConsumptionService _reagents;

    public ResultPipelineService(LisDbContext db, IAuditService audit, IRolePolicy policy, ICurrentEmployee current, OrderStateService state,
        SampleService samples, ProgressiveReleaseService release, ILogger<ResultPipelineService> logger, BillingService billing, ReagentConsumptionService reagents)
    {
        _db = db; _audit = audit; _policy = policy; _current = current; _state = state; _samples = samples; _release = release; _logger = logger; _billing = billing; _reagents = reagents;
    }

    /// <summary>Максимальна кількість автоматичних повторів з розведенням для одного тесту.</summary>
    private const int MaxAutoReruns = 3;
    private const double MaxDilution = 1000;

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

    private async Task<bool> BlockOnCalibrationAsync() =>
        (await _db.Settings.AsNoTracking().Select(s => (bool?)s.BlockOnExpiredCalibration).FirstOrDefaultAsync()) ?? false;

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
        if (def.ResultType == "CALCULATED" && !entry.IsCalculated)
            throw new ValidationException($"Показник {def.Code} розрахунковий: його значення обчислюється автоматично за формулою, ручне введення заблоковане");
        if (entry.DilutionFactor is { } df0 && (df0 < 1 || df0 > MaxDilution || double.IsNaN(df0))) throw ValidationException.Field("dilutionFactor", $"Коефіцієнт розведення має бути від 1 до {MaxDilution:0}");
        var prevStatus = test.Status;
        var warnings = new List<string>();

        if (OrderStatuses.Terminal.Contains(order.Status) || order.Status == OrderStatuses.Released)
            throw new ConflictException($"Замовлення {order.OrderNumber} у статусі «{UkLabels.Status(LisEntities.Order, order.Status)}» — введення результатів неможливе");

        // Коннектор: проба на приладі = фактично прийнята
        if (entry.IsSystem && !entry.IsCalculated && test.Sample != null) _samples.SystemReceive(test.Sample);
        if (entry.IsSystem && !entry.IsCalculated && order.Status == OrderStatuses.New)
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
        var analyzerId = entry.AnalyzerId ?? test.AssignedAnalyzerId;

        // Розведення: множимо вимір розведеного зразка на коефіцієнт (або беремо вже перераховане значення приладу)
        double? dilution = entry.DilutionFactor ?? (prevStatus == OrderTestStatuses.Rerun ? test.DilutionFactor : null);
        if (dilution is <= 1) dilution = null;
        double? rawValue = null;
        if (numeric.HasValue && dilution.HasValue)
        {
            if (entry.ValueIsFinal) rawValue = numeric.Value / dilution.Value;
            else { rawValue = numeric.Value; numeric = numeric.Value * dilution.Value; }
            rawValue = Math.Round(rawValue.Value, 6);
        }

        // Лінійність приладу: вихід за верхню межу → авто-повтор з розведенням; за нижню → на перегляд
        string? linearityMsg = null; bool outOfLinearity = false; double? autoFactor = null;
        if (numeric.HasValue && !entry.IsCalculated && entry.AcceptAttemptId == null && analyzerId != null)
        {
            var map = await _db.AnalyzerParameters.AsNoTracking().FirstOrDefaultAsync(m => m.AnalyzerId == analyzerId && m.TestCode == def.Code);
            var measured = rawValue ?? numeric.Value;
            var high = entry.AboveRange || (map?.LinearityHigh != null && measured > map.LinearityHigh);
            var low = map?.LinearityLow != null && measured < map.LinearityLow;
            if (high || low)
            {
                outOfLinearity = true;
                var inv = CultureInfo.InvariantCulture;
                var next = (dilution ?? 1) * (map?.AutoDilutionFactor is > 1 ? map.AutoDilutionFactor.Value : 10);
                if (high && test.RerunCount < MaxAutoReruns && next <= MaxDilution)
                {
                    autoFactor = next;
                    linearityMsg = $"Значення поза лінійністю приладу (вище {map?.LinearityHigh?.ToString("0.##", inv) ?? "діапазону"}): призначено повтор з розведенням 1:{next:0.##}";
                }
                else if (high) linearityMsg = $"Значення поза лінійністю приладу (вище {map?.LinearityHigh?.ToString("0.##", inv) ?? "діапазону"}); авто-розведення вичерпано — потрібне рішення лікаря";
                else linearityMsg = $"Значення поза лінійністю приладу (нижче {map!.LinearityLow!.Value.ToString("0.##", inv)}) — перевірте вручну";
                warnings.Add(linearityMsg);
            }
        }
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
        var locked = await IsLockedOutAsync(analyzerId, def.Code);

        // Правдоподібність (FR-RPT-018): фізіологічні межі й співвідношення з іншими результатами замовлення
        string? implausible = null;
        if (numeric.HasValue)
        {
            var siblings = await _db.Results.AsNoTracking().Where(r => r.OrderTest!.OrderId == order.Id && r.OrderTestId != test.Id && r.NumericValue != null && r.OrderTest.Status != OrderTestStatuses.Rejected)
                .Select(r => new { r.OrderTest!.TestCode, r.NumericValue }).ToListAsync();
            var check = PlausibilityChecker.Check(def.Code, numeric.Value, siblings.GroupBy(x => x.TestCode).ToDictionary(g => g.Key, g => g.First().NumericValue!.Value));
            if (!check.IsPlausible) implausible = check.Summary;
        }
        if (linearityMsg != null) implausible = implausible == null ? linearityMsg : linearityMsg + "; " + implausible;

        // Калібрування приладу (FR-GAP-031): прострочене/відхилене → попередження; за налаштуванням — заборона ручного введення
        var calib = await CalibrationService.CheckAsync(_db, analyzerId, def.Code);
        if (calib.IsProblem && !entry.IsSystem && await BlockOnCalibrationAsync())
            throw new ConflictException($"Введення результату неможливе: {calib.Message}") { Code = "CALIBRATION_EXPIRED" };
        var calibWarning = calib.IsProblem ? calib.Message : null;
        if (calibWarning != null && calibWarning.Length > 500) calibWarning = calibWarning.Substring(0, 497) + "...";

        // Автоверифікація
        var decision = AutoVerificationEngine.Evaluate(new AutoVerificationInput
        {
            Flag = flag, AnalyzerFlags = entry.AnalyzerFlags, DeltaAlert = delta.IsAlert,
            RequiresManualVerification = def.RequiresManualVerification || (entry.PerformerId ?? test.PerformerId) != null, HasActiveLockout = locked, HasValue = numeric.HasValue || !string.IsNullOrEmpty(text),
            ImplausibleReason = implausible ?? calibWarning
        });

        // Результат (одна версія на тест, попередні — в історії)
        var result = test.Result;
        var isNew = result == null;
        var isNewMeasurement = !entry.IsCalculated && entry.AcceptAttemptId == null && (isNew || prevStatus == OrderTestStatuses.Rerun);
        var attempts = entry.IsCalculated ? new List<LabResultAttempt>() : await _db.ResultAttempts.Where(a => a.OrderTestId == test.Id).ToListAsync();
        if (!entry.IsCalculated && result != null && attempts.Count == 0)
        {
            // результат створено до появи історії спроб — зберігаємо його як першу (оригінальну) спробу
            var legacy = new LabResultAttempt
            {
                OrderTestId = test.Id, AttemptNo = 1, Kind = "INITIAL", NumericValue = result.NumericValue, StringValue = result.StringValue, Unit = result.Unit, Flag = result.Flag,
                DilutionFactor = result.DilutionFactor, RawValue = result.RawValue, OutOfLinearity = result.OutOfLinearity, AnalyzerId = result.AnalyzerId, ReagentLotId = result.ReagentLotId,
                EnteredById = result.EnteredById, EnteredAt = result.EnteredAt, IsAccepted = true
            };
            attempts.Add(legacy); _db.ResultAttempts.Add(legacy);
        }
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
        result.DeltaPercent = delta.HasPrevious ? delta.DeltaPercent : null; // без попереднього результату дельти немає
        result.DeltaAlert = delta.HasPrevious && delta.IsAlert;
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
        result.AutoVerifyBlockReason = decision.Approved ? null : (decision.Summary.Length > 500 ? decision.Summary.Substring(0, 497) + "..." : decision.Summary);
        result.CalibrationWarning = calibWarning;
        result.CalibrationId = calib.Status is CalibrationStatuses.None ? null : calib.Calibration?.Id;
        if (calibWarning != null) _audit.Log("CALIBRATION_WARNING", "lab_order_test", test.Id, null, new { test.TestCode, analyzerId, calib.Status, calibrationId = calib.Calibration?.Id });
        result.VerifiedById = null; result.VerifiedAt = null; result.VerificationComment = null;
        result.IsCalculated = entry.IsCalculated;
        result.CalcFormula = entry.IsCalculated ? entry.CalcFormula : null;
        result.CalcInputsJson = entry.IsCalculated ? entry.CalcInputsJson : null;
        result.DilutionFactor = dilution;
        result.RawValue = rawValue;
        result.OutOfLinearity = outOfLinearity;

        if (autoFactor.HasValue)
        {
            // Авто-розведення: значення збережено як спробу, тест повертається в чергу з розведенням
            test.Status = OrderTestStatuses.Rerun; test.ReleasedAt = null; test.DilutionFactor = autoFactor; test.RerunCount++;
            result.IsAutoVerified = false; result.AutoVerifyBlockReason = linearityMsg;
        }
        else if (decision.Approved)
        {
            test.Status = OrderTestStatuses.AutoVerified;
            result.VerifiedAt = now;
            await _release.ReleaseVerifiedTestAsync(test, order);
        }
        else
        {
            test.ReleasedAt = null;
            test.Status = ResultFlags.IsCritical(flag) || delta.IsAlert || implausible != null || calibWarning != null ?OrderTestStatuses.NeedsReview : OrderTestStatuses.Resulted;
        }
        test.RejectReason = null;
        if (autoFactor == null && entry.AcceptAttemptId == null) test.DilutionFactor = null; // заплановане розведення використано
        if (analyzerId != null) test.AssignedAnalyzerId = analyzerId;

        // Спроби вимірювання: оригінал + кожен повтор; прийнята — та, що в результаті
        if (!entry.IsCalculated)
        {
            if (entry.AcceptAttemptId != null)
            {
                var chosen = attempts.FirstOrDefault(a => a.Id == entry.AcceptAttemptId) ?? throw NotFoundException.For("Спроба вимірювання", entry.AcceptAttemptId);
                foreach (var a in attempts) a.IsAccepted = a.Id == chosen.Id;
                chosen.Flag = flag; chosen.AcceptedById = _current.EmployeeId; chosen.AcceptedAt = now; chosen.Comment = entry.Comment ?? chosen.Comment;
            }
            else if (isNewMeasurement)
            {
                var prev = attempts.OrderBy(a => a.AttemptNo).LastOrDefault();
                foreach (var a in attempts) { a.IsAccepted = false; if (prevStatus == OrderTestStatuses.Rerun) a.SupersededByRerun = true; }
                var att = new LabResultAttempt
                {
                    OrderTestId = test.Id, AttemptNo = attempts.Count + 1, NumericValue = numeric, StringValue = result.StringValue, Unit = result.Unit, Flag = flag,
                    Kind = isNew ? "INITIAL" : dilution.HasValue ? (prev?.OutOfLinearity == true ? "AUTO_DILUTION" : "DILUTION") : "RERUN",
                    DilutionFactor = dilution, RawValue = rawValue, OutOfLinearity = outOfLinearity, AnalyzerId = analyzerId, EnteredById = result.EnteredById ?? (entry.IsSystem ? "SYSTEM:" + analyzerId : null),
                    EnteredAt = now, IsAccepted = true, Comment = entry.Comment ?? linearityMsg
                };
                attempts.Add(att); _db.ResultAttempts.Add(att);
            }
            else
            {
                // виправлення поточного значення (не нове вимірювання) — оновлюємо прийняту спробу
                var cur = attempts.FirstOrDefault(a => a.IsAccepted) ?? attempts.OrderBy(a => a.AttemptNo).Last();
                cur.NumericValue = numeric; cur.StringValue = result.StringValue; cur.Unit = result.Unit; cur.Flag = flag; cur.DilutionFactor = dilution; cur.RawValue = rawValue; cur.OutOfLinearity = outOfLinearity;
            }
        }

        // Автосписання реагенту за нормою витрати (нестача не блокує результат, FR-REA-002)
        if (isNewMeasurement && analyzerId != null && entry.PerformerId == null && test.PerformerId == null)
            warnings.AddRange(await _reagents.ConsumeForResultAsync(test, result, analyzerId, isRerun: !isNew));

        _db.ResultHistory.Add(new LabResultHistory
        {
            OrderTestId = test.Id, ResultId = result.Id, Version = result.Version, Action = isNew ? "ENTERED" : "CORRECTED", NumericValue = numeric,
            StringValue = result.StringValue, Flag = flag, ActorId = result.EnteredById ?? (entry.IsSystem ? "SYSTEM:" + analyzerId : null), At = now,
            Comment = entry.Comment, SnapshotJson = Snapshot(result)
        });
        if (autoFactor.HasValue)
            _db.ResultHistory.Add(new LabResultHistory { OrderTestId = test.Id, ResultId = result.Id, Version = result.Version, Action = "RERUN", NumericValue = numeric, Flag = flag, ActorId = "SYSTEM:AUTO_DILUTION", At = now, Comment = linearityMsg });

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
            new
            {
                test.TestCode, numeric, text, flag, delta.DeltaPercent, delta.IsAlert, autoVerified = decision.Approved && autoFactor == null, block = result.AutoVerifyBlockReason, layer = result.AppliedLayerName,
                dilution, rawValue, outOfLinearity, autoDilution = autoFactor, reagentLot = result.ReagentLotNumber, acceptedAttempt = entry.AcceptAttemptId,
                calculated = entry.IsCalculated, formula = result.CalcFormula, inputs = result.CalcInputsJson
            });
        await _db.SaveChangesAsync();
        if (!entry.IsCalculated) await RecalculateOrderAsync(order.Id, warnings);
        var row = await RowAsync(test.Id);
        row.Warnings = warnings.Distinct().ToList();
        return row;
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
        // Калібрування (FR-GAP-031): при увімкненому блокуванні верифікація результату з простроченим калібруванням — лише з override і коментарем
        var calib = await CalibrationService.CheckAsync(_db, result.AnalyzerId ?? test.AssignedAnalyzerId, test.TestCode);
        var calibBlocked = calib.IsProblem && await BlockOnCalibrationAsync();
        if (calibBlocked && !req.Override) throw new ConflictException($"Верифікація неможлива: {calib.Message} Для виняткового підтвердження вкажіть коментар і позначте «Підтвердити попри прострочене калібрування».") { Code = "CALIBRATION_EXPIRED" };
        if (calibBlocked && string.IsNullOrWhiteSpace(req.Comment)) throw ValidationException.Field("comment", "Підтвердження при простроченому калібруванні потребує коментаря");

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
        reason = (await ReasonCatalog.ResolveRejectionAsync(_db, reason)).Text;
        var test = await LoadAsync(orderTestId);
        var rule = _policy.Ensure(LisEntities.OrderTest, TestActions.Reject, test.Status, $"Тест {test.TestCode}");
        var before = test.Status;
        test.Status = rule.ToStatus!;
        test.RejectReason = reason;
        if (test.Result != null)
            _db.ResultHistory.Add(new LabResultHistory { OrderTestId = test.Id, ResultId = test.Result.Id, Version = test.Result.Version, Action = "REJECTED", Flag = test.Result.Flag, ActorId = _current.EmployeeId, Comment = reason });
        _state.RecomputeCompletion(test.Order!);
        // Відхилений до отримання результату тест не тарифікується (FR-GAP-060)
        if (test.Result == null && !await _db.Invoices.AnyAsync(i => i.OrderId == test.OrderId && i.Status != "CANCELLED")) await _billing.RecalculateAsync(test.Order!);
        _audit.Log("REJECT", "lab_order_test", test.Id, new { status = before }, new { test.Status, reason });
        await _db.SaveChangesAsync();
        await RecalculateOrderAsync(test.OrderId, new List<string>());
        return await RowAsync(test.Id);
    }

    /// <summary>Повторне дослідження (за потреби — з розведенням). Попереднє значення залишається спробою; нове вимірювання стане наступною.</summary>
    public async Task<WorklistRowDto> RerunAsync(string orderTestId, string? reason, double? dilutionFactor = null)
    {
        var test = await LoadAsync(orderTestId);
        if (test.Test?.ResultType == "CALCULATED") throw new ConflictException($"Показник {test.TestCode} розрахунковий — повторіть вхідні показники, результат перерахується автоматично");
        var rule = _policy.Ensure(LisEntities.OrderTest, TestActions.Rerun, test.Status, $"Тест {test.TestCode}");
        if (dilutionFactor is { } df && (df < 1 || df > MaxDilution || double.IsNaN(df))) throw ValidationException.Field("dilutionFactor", $"Коефіцієнт розведення має бути від 1 до {MaxDilution:0}");
        var before = test.Status;
        test.Status = rule.ToStatus!;
        test.ReleasedAt = null;
        test.RerunCount++;
        test.DilutionFactor = dilutionFactor is > 1 ? dilutionFactor : null;
        var note = string.IsNullOrWhiteSpace(reason) ? null : reason;
        if (test.DilutionFactor.HasValue) note = (note == null ? "" : note + ". ") + $"Розведення 1:{test.DilutionFactor:0.##}";
        if (test.Result != null)
        {
            test.Result.IsAutoVerified = false; test.Result.VerifiedAt = null; test.Result.VerifiedById = null;
            _db.ResultHistory.Add(new LabResultHistory { OrderTestId = test.Id, ResultId = test.Result.Id, Version = test.Result.Version, Action = "RERUN", NumericValue = test.Result.NumericValue, Flag = test.Result.Flag, ActorId = _current.EmployeeId, Comment = note });
        }
        _state.RecomputeReopen(test.Order!);
        _audit.Log("RERUN", "lab_order_test", test.Id, new { status = before }, new { test.Status, reason, dilutionFactor = test.DilutionFactor, test.RerunCount });
        await _db.SaveChangesAsync();
        var warnings = new List<string>();
        await RecalculateOrderAsync(test.OrderId, warnings);
        var row = await RowAsync(test.Id);
        row.Warnings = warnings;
        return row;
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
        await RecalculateOrderAsync(test.OrderId, new List<string>());
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
            var calib = await CalibrationService.CheckAsync(_db, r.AnalyzerId ?? t.AssignedAnalyzerId, t.TestCode);
            var decision = AutoVerificationEngine.Evaluate(new AutoVerificationInput
            {
                Flag = r.Flag, AnalyzerFlags = r.AnalyzerFlags, DeltaAlert = r.DeltaAlert, RequiresManualVerification = t.Test?.RequiresManualVerification ?? false,
                HasActiveLockout = locked, HasValue = r.NumericValue.HasValue || !string.IsNullOrEmpty(r.StringValue),
                ImplausibleReason = calib.IsProblem ? calib.Message : null
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
