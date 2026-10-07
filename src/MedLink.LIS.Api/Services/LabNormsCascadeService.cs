using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Models;

namespace MedLink.LIS.Api.Services
{
    public class CascadeResolveRequest
    {
        public string TestCode { get; set; } = "GLU";
        public string MethodCode { get; set; } = "HEX_IFCC";
        public string Gender { get; set; } = "ANY"; // M, F, ANY
        public double Age { get; set; } = 30;
        public string AgeUnit { get; set; } = "YEARS"; // DAYS, MONTHS, YEARS
        public bool IsPregnant { get; set; } = false;
        public int? PregnancyWeek { get; set; }
        public string? MenstrualPhase { get; set; } // FOLLICULAR, OVULATORY, LUTEAL, POSTMENOPAUSE
        public string? Icd10Code { get; set; } // e.g. E11
        public double? MeasuredValue { get; set; }
        public double? PreviousValue { get; set; }
    }

    public class LayerAuditStep
    {
        public int Priority { get; set; }
        public string LayerType { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public bool IsMatched { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class CascadeResolveResult
    {
        public bool Success { get; set; } = true;
        public string TestCode { get; set; } = string.Empty;
        public string MethodCode { get; set; } = string.Empty;
        public LabReferenceLayer? AppliedLayer { get; set; }
        public double NormLow { get; set; }
        public double NormHigh { get; set; }
        public double? CritLow { get; set; }
        public double? CritHigh { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Interpretation { get; set; } = string.Empty;
        public string StatusFlag { get; set; } = "NORMAL"; // NORMAL, LOW, HIGH, CRIT_LOW, CRIT_HIGH
        public bool IsPanicCito { get; set; }
        public bool IsDeltaAlert { get; set; }
        public double? DeltaPercent { get; set; }
        public List<LayerAuditStep> AuditTrace { get; set; } = new();
    }

    public interface ILabNormsCascadeService
    {
        Task<CascadeResolveResult> ResolveCascadeAsync(CascadeResolveRequest request);
        Task<List<LabReferenceLayer>> GetLayersAsync(string testCode, string methodCode);
    }

    public class LabNormsCascadeService : ILabNormsCascadeService
    {
        private readonly MedLinkLabDbContext _db;

        public LabNormsCascadeService(MedLinkLabDbContext db)
        {
            _db = db;
        }

        public async Task<List<LabReferenceLayer>> GetLayersAsync(string testCode, string methodCode)
        {
            return await _db.ReferenceLayers
                .Where(r => r.TestCode == testCode && (r.MethodCode == methodCode || r.MethodCode == "HEX_IFCC") && r.IsActive == 1)
                .OrderByDescending(r => r.PriorityOrder)
                .ToListAsync();
        }

        public async Task<CascadeResolveResult> ResolveCascadeAsync(CascadeResolveRequest req)
        {
            var result = new CascadeResolveResult
            {
                TestCode = req.TestCode,
                MethodCode = req.MethodCode
            };

            // 1. Fetch all candidate layers sorted by priority descending (Delphi Layer Stack)
            var layers = await _db.ReferenceLayers
                .Where(r => r.TestCode == req.TestCode && (r.MethodCode == req.MethodCode || r.MethodCode == "HEX_IFCC") && r.IsActive == 1)
                .OrderByDescending(r => r.PriorityOrder)
                .ToListAsync();

            if (!layers.Any())
            {
                // Fallback default
                result.NormLow = 4.10;
                result.NormHigh = 5.90;
                result.Unit = "ммоль/л";
                result.Interpretation = "Базовий референтний інтервал за замовчуванням (шари не знайдено)";
                return result;
            }

            LabReferenceLayer? winningLayer = null;

            // 2. Step-by-step cascade evaluation
            foreach (var layer in layers)
            {
                var audit = new LayerAuditStep
                {
                    Priority = layer.PriorityOrder,
                    LayerType = layer.LayerType,
                    LayerName = layer.NormName
                };

                // Check layer conditions
                bool match = true;
                string reason = "Умови співпали";

                switch (layer.LayerType.ToUpperInvariant())
                {
                    case "PREGNANCY":
                        if (!req.IsPregnant || !req.PregnancyWeek.HasValue)
                        {
                            match = false;
                            reason = "Пацієнт не вагітна або термін не вказано";
                        }
                        else if (layer.PregnancyWeekFrom.HasValue && layer.PregnancyWeekTo.HasValue &&
                                 (req.PregnancyWeek.Value < layer.PregnancyWeekFrom.Value || req.PregnancyWeek.Value > layer.PregnancyWeekTo.Value))
                        {
                            match = false;
                            reason = $"Термін вагітності {req.PregnancyWeek} т. поза межами [{layer.PregnancyWeekFrom}-{layer.PregnancyWeekTo}] т.";
                        }
                        break;

                    case "MENSTRUAL_PHASE":
                        if (req.Gender != "F")
                        {
                            match = false;
                            reason = "Застосовується виключно до жінок";
                        }
                        else if (req.IsPregnant)
                        {
                            match = false;
                            reason = "Перекрито вищим пріоритетом вагітності";
                        }
                        else if (string.IsNullOrEmpty(req.MenstrualPhase) || !string.Equals(layer.MenstrualPhase, req.MenstrualPhase, StringComparison.OrdinalIgnoreCase))
                        {
                            match = false;
                            reason = $"Фаза циклу '{req.MenstrualPhase ?? "не вказано"}' не відповідає шару '{layer.MenstrualPhase}'";
                        }
                        break;

                    case "CLINICAL_ICD10":
                        if (string.IsNullOrEmpty(req.Icd10Code) || !string.Equals(layer.Icd10Code, req.Icd10Code, StringComparison.OrdinalIgnoreCase))
                        {
                            match = false;
                            reason = $"Діагноз МКХ-10 '{req.Icd10Code ?? "немає"}' не відповідає цільовій когорті '{layer.Icd10Code}'";
                        }
                        break;

                    case "DEMOGRAPHIC":
                        if (layer.IsGender == 1 && layer.Gender != "ANY" && !string.Equals(layer.Gender, req.Gender, StringComparison.OrdinalIgnoreCase))
                        {
                            match = false;
                            reason = $"Стать '{req.Gender}' не відповідає фільтру шару '{layer.Gender}'";
                        }
                        else if (layer.IsAge == 1)
                        {
                            double ToDays(double val, string unit) => (unit?.ToUpperInvariant()) switch
                            {
                                "DAYS" => val,
                                "MONTHS" => val * 30.4375,
                                _ => val * 365.25
                            };

                            double pDays = ToDays(req.Age, req.AgeUnit);
                            double lFromDays = ToDays(layer.AgeFrom, layer.AgeUnit);
                            double lToDays = ToDays(layer.AgeTo, layer.AgeUnit);

                            if (pDays < lFromDays || pDays > lToDays)
                            {
                                match = false;
                                reason = $"Вік {req.Age} {req.AgeUnit} поза межами діапазону [{layer.AgeFrom}-{layer.AgeTo} {layer.AgeUnit}]";
                            }
                        }
                        break;

                    case "BASELINE":
                        // Baseline always matches
                        match = true;
                        reason = "Універсальний базовий референс методики (Запасний рівень)";
                        break;
                }

                audit.IsMatched = match;
                audit.Reason = reason;
                result.AuditTrace.Add(audit);

                if (match && winningLayer == null)
                {
                    winningLayer = layer;
                }
            }

            // 3. Apply winning layer or fallback to first
            winningLayer ??= layers.Last();
            result.AppliedLayer = winningLayer;
            result.NormLow = winningLayer.NormLow;
            result.NormHigh = winningLayer.NormHigh;
            result.CritLow = winningLayer.CritLow;
            result.CritHigh = winningLayer.CritHigh;
            result.Unit = winningLayer.Unit;
            result.Interpretation = winningLayer.NormText ?? winningLayer.NormName;

            // 4. Evaluate measured value against boundaries
            if (req.MeasuredValue.HasValue)
            {
                double v = req.MeasuredValue.Value;
                if ((result.CritLow.HasValue && v <= result.CritLow.Value) ||
                    (result.CritHigh.HasValue && v >= result.CritHigh.Value))
                {
                    result.StatusFlag = v >= (result.CritHigh ?? double.MaxValue) ? "CRIT_HIGH" : "CRIT_LOW";
                    result.IsPanicCito = true;
                }
                else if (v < result.NormLow)
                {
                    result.StatusFlag = "LOW";
                }
                else if (v > result.NormHigh)
                {
                    result.StatusFlag = "HIGH";
                }
                else
                {
                    result.StatusFlag = "NORMAL";
                }

                // 5. Delta-check
                if (req.PreviousValue.HasValue && req.PreviousValue.Value > 0)
                {
                    double prev = req.PreviousValue.Value;
                    double deltaPct = Math.Round(((v - prev) / prev) * 100.0, 1);
                    result.DeltaPercent = deltaPct;
                    if (Math.Abs(deltaPct) > winningLayer.DeltaCheckMaxPct)
                    {
                        result.IsDeltaAlert = true;
                    }
                }
            }

            return result;
        }
    }
}
