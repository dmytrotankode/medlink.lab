// =============================================================================
// Розрахункові показники (FR-WS-005), спроби вимірювання (повтор/розведення, FR-WS-007).
// CALCULATED-показник обчислюється формулою з інших показників замовлення після кожної зміни складових;
// при відсутніх складових тест лишається в очікуванні (PENDING), а раніше розрахований результат знімається.
// =============================================================================
using System.Globalization;
using System.Text.Json;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed partial class ResultPipelineService
{
    /// <summary>Стани складових, значення яких можна брати в розрахунок (повтор/відхилення/очікування — ні).</summary>
    private static readonly string[] UsableInputStatuses = { OrderTestStatuses.Resulted, OrderTestStatuses.NeedsReview, OrderTestStatuses.AutoVerified, OrderTestStatuses.Verified };

    private sealed class CalcSnapshot
    {
        public ParsedFormula Parsed = null!;
        public SortedDictionary<string, double> Inputs = new(StringComparer.Ordinal);
        public FormulaEvaluation Eval = null!;
        public List<string> MissingLabels = new();
    }

    private static string? ContextLabel(string name) => name switch { "AGE" => "вік пацієнта", "MALE" or "FEMALE" => "стать пацієнта", _ => null };

    private async Task<LabOrder> LoadOrderForCalcAsync(string orderId) =>
        await _db.Orders.Include(o => o.Patient).Include(o => o.Tests).ThenInclude(t => t.Test).Include(o => o.Tests).ThenInclude(t => t.Result).FirstOrDefaultAsync(o => o.Id == orderId)
        ?? throw NotFoundException.For("Замовлення", orderId);

    private static CalcSnapshot BuildCalc(LabOrder order, LabOrderTest calc, ParsedFormula parsed)
    {
        var snap = new CalcSnapshot { Parsed = parsed };
        double? Input(string code)
        {
            var src = order.Tests.FirstOrDefault(t => t.Id != calc.Id && t.TestCode.Equals(code, StringComparison.OrdinalIgnoreCase) && UsableInputStatuses.Contains(t.Status) && t.Result?.NumericValue != null);
            return src?.Result!.NumericValue;
        }
        var p = order.Patient;
        double? Context(string name) => name switch
        {
            "AGE" => p?.Birthday.HasValue == true ? Math.Floor(AgeUnits.AgeDays(p.Birthday.Value, order.OrderDatetime) / 365.25 * 100) / 100 : null,
            "MALE" => p?.Gender is "M" ? 1 : p?.Gender is "F" ? 0 : null,
            "FEMALE" => p?.Gender is "F" ? 1 : p?.Gender is "M" ? 0 : null,
            _ => null
        };
        double? Resolve(string name)
        {
            var v = FormulaContextVariables.IsContext(name) ? Context(name) : Input(name);
            if (v.HasValue) snap.Inputs[name] = v.Value;
            return v;
        }
        snap.Eval = FormulaEngine.Evaluate(parsed, Resolve);
        snap.MissingLabels = snap.Eval.Missing.Select(m => ContextLabel(m) is { } l ? l : m).Distinct().ToList();
        return snap;
    }

    private static string InputsJson(SortedDictionary<string, double> inputs) =>
        JsonSerializer.Serialize(inputs.ToDictionary(k => k.Key, k => Math.Round(k.Value, 6)));

    /// <summary>
    /// Перераховує всі розрахункові показники замовлення (до стабільного стану): нові/змінені значення застосовуються
    /// через той самий конвеєр (норми, прапорці, автоверифікація); при відсутніх складових результат знімається.
    /// Помилки розрахунку не блокують збереження складових — потрапляють у попередження й аудит.
    /// </summary>
    public async Task RecalculateOrderAsync(string orderId, List<string> warnings)
    {
        var order = await LoadOrderForCalcAsync(orderId);
        if (order.Status == OrderStatuses.Released || OrderStatuses.Terminal.Contains(order.Status)) return;
        var calcTests = order.Tests.Where(t => t.Test?.ResultType == "CALCULATED" && t.Status != OrderTestStatuses.Rejected && t.Status != OrderTestStatuses.SentOut).ToList();
        if (calcTests.Count == 0) return;

        var failed = new HashSet<string>();
        for (var pass = 0; pass <= calcTests.Count; pass++)
        {
            var changed = false;
            foreach (var ct in calcTests)
            {
                var def = ct.Test!;
                var parsed = FormulaEngine.Parse(def.Formula);
                if (!parsed.IsValid)
                {
                    if (failed.Add(ct.Id)) warnings.Add($"Формула показника {def.Code} некоректна: {parsed.Error}");
                    continue;
                }
                var snap = BuildCalc(order, ct, parsed);
                if (snap.Eval.Error != null)
                {
                    if (failed.Add(ct.Id)) warnings.Add($"Не вдалося обчислити {def.Code}: {snap.Eval.Error}");
                    continue;
                }
                var calcResult = ct.Result is { IsCalculated: true } ? ct.Result : null;
                if (!snap.Eval.Ok)
                {
                    // складових бракує: раніше розрахований результат знімаємо, тест чекає на вхідні
                    if (calcResult != null)
                    {
                        InvalidateCalculated(order, ct, calcResult, snap);
                        await _db.SaveChangesAsync();
                        changed = true;
                    }
                    continue;
                }

                var value = Math.Round(snap.Eval.Value!.Value, Math.Clamp(def.DecimalPlaces, 0, 6));
                var inputsJson = InputsJson(snap.Inputs);
                if (calcResult != null && calcResult.NumericValue == value && calcResult.CalcInputsJson == inputsJson && calcResult.CalcFormula == def.Formula) continue;
                if (ct.Result != null && !ct.Result.IsCalculated && ct.Result.NumericValue.HasValue) continue; // значення, внесене до появи розрахунку, не перезаписуємо мовчки

                try
                {
                    if (ct.Status is OrderTestStatuses.AutoVerified or OrderTestStatuses.Verified)
                    {
                        // складові змінилися — раніше верифіковане значення втрачає силу
                        ct.Status = OrderTestStatuses.Resulted; ct.ReleasedAt = null;
                        if (ct.Result != null)
                        {
                            ct.Result.IsAutoVerified = false; ct.Result.VerifiedAt = null; ct.Result.VerifiedById = null;
                            _db.ResultHistory.Add(new LabResultHistory { OrderTestId = ct.Id, ResultId = ct.Result.Id, Version = ct.Result.Version, Action = "REOPENED", NumericValue = ct.Result.NumericValue, Flag = ct.Result.Flag, Comment = "Змінилися вхідні показники розрахунку" });
                        }
                        _state.RecomputeReopen(order);
                    }
                    await ApplyAsync(new ResultEntry
                    {
                        OrderTestId = ct.Id, NumericValue = value, IsSystem = true, IsCalculated = true, CalcFormula = def.Formula, CalcInputsJson = inputsJson,
                        Comment = $"Розраховано за формулою {def.Formula}"
                    });
                    changed = true;
                }
                catch (LisException ex)
                {
                    warnings.Add($"Не вдалося записати розрахунок {def.Code}: {ex.Message}");
                    _audit.Log("CALC_FAILED", "lab_order_test", ct.Id, null, new { def.Code, def.Formula, error = ex.Message });
                    await _db.SaveChangesAsync();
                }
            }
            if (!changed) break;
        }
    }

    /// <summary>Ручний перерахунок розрахункових показників замовлення (лаборант/лікар/адмін).</summary>
    public async Task<List<string>> RecalculateByUserAsync(string orderId)
    {
        _policy.Require("Перерахунок розрахункових показників", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var warnings = new List<string>();
        await RecalculateOrderAsync(orderId, warnings);
        return warnings;
    }

    private void InvalidateCalculated(LabOrder order, LabOrderTest ct, LabTestResult result, CalcSnapshot snap)
    {
        _db.ResultHistory.Add(new LabResultHistory
        {
            OrderTestId = ct.Id, ResultId = result.Id, Version = result.Version, Action = "DELETED", NumericValue = result.NumericValue, Flag = result.Flag,
            Comment = "Результат знято: бракує вхідних показників (" + string.Join(", ", snap.MissingLabels) + ")"
        });
        _audit.Log("CALC_INVALIDATED", "lab_test_result", result.Id, new { result.NumericValue, result.CalcFormula, result.CalcInputsJson }, new { missing = snap.Eval.Missing });
        _db.Results.Remove(result);
        ct.Result = null; ct.Status = OrderTestStatuses.Pending; ct.ReleasedAt = null;
        _state.RecomputeReopen(order);
    }

    /// <summary>Склад розрахунку для екрана: формула, вхідні (значення/статус), чого бракує, поточне значення.</summary>
    public async Task<object> CalculationInfoAsync(string orderTestId)
    {
        var test = await LoadAsync(orderTestId);
        if (test.Test?.ResultType != "CALCULATED") return new { orderTestId, testCode = test.TestCode, isCalculated = false };
        var order = await LoadOrderForCalcAsync(test.OrderId);
        var ct = order.Tests.First(t => t.Id == orderTestId);
        var parsed = FormulaEngine.Parse(ct.Test!.Formula);
        if (!parsed.IsValid) return new { orderTestId, testCode = ct.TestCode, isCalculated = true, formula = ct.Test.Formula, valid = false, error = parsed.Error, status = ct.Status };
        var snap = BuildCalc(order, ct, parsed);
        var inputs = parsed.Inputs.Select(code =>
        {
            var src = order.Tests.FirstOrDefault(t => t.Id != ct.Id && t.TestCode.Equals(code, StringComparison.OrdinalIgnoreCase));
            var usable = src != null && UsableInputStatuses.Contains(src.Status) && src.Result?.NumericValue != null;
            return (object)new
            {
                code, name = src?.TestName, inOrder = src != null, value = usable ? src!.Result!.NumericValue : null, unit = src?.Result?.Unit ?? src?.Test?.Unit, status = src?.Status,
                available = usable, reason = src == null ? "показника немає в замовленні" : usable ? null : "значення ще немає або триває повтор"
            };
        }).ToList();
        var ctxVars = parsed.ContextVariables.Select(n => (object)new { name = n, label = ContextLabel(n), value = snap.Inputs.TryGetValue(n, out var v) ? (double?)v : null }).ToList();
        return new
        {
            orderTestId, testCode = ct.TestCode, isCalculated = true, formula = ct.Test.Formula, valid = true, status = ct.Status, inputs, context = ctxVars,
            missing = snap.MissingLabels, canCalculate = snap.Eval.Ok, error = snap.Eval.Error,
            value = ct.Result?.NumericValue, calculatedFrom = ct.Result?.CalcInputsJson
        };
    }

    // ------------------------------------------------------------------ спроби вимірювання (оригінал + повтори)
    public async Task<List<object>> AttemptsAsync(string orderTestId)
    {
        if (!await _db.OrderTests.AnyAsync(t => t.Id == orderTestId)) throw NotFoundException.For("Тест замовлення", orderTestId);
        var attempts = await _db.ResultAttempts.AsNoTracking().Where(a => a.OrderTestId == orderTestId).OrderBy(a => a.AttemptNo).ToListAsync();
        if (attempts.Count == 0)
        {
            // результат без збережених спроб (створений раніше): показуємо поточний як єдину спробу
            var r = await _db.Results.AsNoTracking().FirstOrDefaultAsync(x => x.OrderTestId == orderTestId);
            if (r != null)
                return new List<object> { new { id = (string?)null, attemptNo = 1, kind = "INITIAL", r.NumericValue, r.StringValue, r.Unit, r.Flag, r.DilutionFactor, r.RawValue, r.OutOfLinearity, r.AnalyzerId, r.EnteredAt, isAccepted = true, supersededByRerun = false } };
        }
        return attempts.Select(a => (object)new
        {
            a.Id, a.AttemptNo, a.Kind, a.NumericValue, a.StringValue, a.Unit, a.Flag, a.DilutionFactor, a.RawValue, a.OutOfLinearity, a.AnalyzerId, a.ReagentLotId, a.EnteredAt, a.EnteredById,
            a.IsAccepted, a.SupersededByRerun, a.AcceptedById, a.AcceptedAt, a.Comment
        }).ToList();
    }

    /// <summary>Лікар/адмін обирає, яке значення з повторів є остаточним. Потрібен коментар; тест має бути в стані «виконано / на перегляді».</summary>
    public async Task<WorklistRowDto> AcceptAttemptAsync(string orderTestId, string attemptId, string? comment)
    {
        _policy.Require("Вибір прийнятого значення", LabRoles.Admin, LabRoles.Doctor);
        if (string.IsNullOrWhiteSpace(comment)) throw ValidationException.Field("comment", "Вкажіть причину вибору значення");
        var test = await LoadAsync(orderTestId);
        if (test.Status is not (OrderTestStatuses.Resulted or OrderTestStatuses.NeedsReview))
            throw new ConflictException($"Вибір значення можливий для тесту у статусі «Виконано» або «На перегляді»; зараз «{UkLabels.Status(LisEntities.OrderTest, test.Status)}». Спочатку відкрийте результат повторно");
        var attempt = await _db.ResultAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attemptId && a.OrderTestId == orderTestId) ?? throw NotFoundException.For("Спроба вимірювання", attemptId);
        if (attempt.IsAccepted) throw new ConflictException("Це значення вже прийняте");
        _audit.Log("ACCEPT_ATTEMPT", "lab_order_test", orderTestId, null, new { attemptId, attempt.AttemptNo, attempt.NumericValue, attempt.DilutionFactor }, comment);
        return await ApplyAsync(new ResultEntry
        {
            OrderTestId = orderTestId, NumericValue = attempt.NumericValue, StringValue = attempt.StringValue, Unit = attempt.Unit, AnalyzerId = attempt.AnalyzerId,
            DilutionFactor = attempt.DilutionFactor, ValueIsFinal = true, AcceptAttemptId = attemptId, Comment = $"Прийнято значення спроби №{attempt.AttemptNo}: {comment}"
        });
    }
}
