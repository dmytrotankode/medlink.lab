// Прогресивна видача результатів пацієнту: тест відкривається одразу після верифікації (autoReleaseVerified секції),
// сповіщення — одне на замовлення за 10 хвилин (debounce). Портал бачить лише released тести + прогрес.
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class ProgressiveReleaseService
{
    public static readonly TimeSpan NotificationDebounce = TimeSpan.FromMinutes(10);
    private readonly LisDbContext _db;
    private readonly IAuditService _audit;

    public ProgressiveReleaseService(LisDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    /// <summary>Викликається після VERIFIED/AUTO_VERIFIED. Без секції — автовидача увімкнена за замовчуванням.</summary>
    public async Task<bool> ReleaseVerifiedTestAsync(LabOrderTest test, LabOrder order)
    {
        if (test.ReleasedAt != null) return false;
        var sectionId = test.Test?.LabSectionId ?? await _db.Tests.AsNoTracking().Where(t => t.Id == test.TestId).Select(t => t.LabSectionId).FirstOrDefaultAsync();
        var autoRelease = true;
        if (sectionId != null)
        {
            var section = _db.Sections.Local.FirstOrDefault(s => s.Id == sectionId) ?? await _db.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sectionId);
            autoRelease = section?.AutoReleaseVerified ?? true;
        }
        if (!autoRelease) return false;
        var now = DateTime.UtcNow;
        test.ReleasedAt = now;
        _audit.Log("TEST_RELEASED", "lab_order_test", test.Id, null, new { test.TestCode, releasedAt = now }, "Прогресивна видача результату пацієнту");
        await QueueNotificationAsync(order, test, now);
        return true;
    }

    private async Task QueueNotificationAsync(LabOrder order, LabOrderTest test, DateTime now)
    {
        var since = now - NotificationDebounce;
        var recent = _db.Notifications.Local.Any(n => n.OrderId == order.Id && n.CreatedOn >= since)
                     || await _db.Notifications.AsNoTracking().AnyAsync(n => n.OrderId == order.Id && n.CreatedOn >= since);
        if (recent) return;
        var patient = order.Patient ?? await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == order.PatientId);
        _db.Notifications.Add(new LabPatientNotification
        {
            PatientId = order.PatientId, OrderId = order.Id, Channel = !string.IsNullOrWhiteSpace(patient?.Person?.Email) ? "EMAIL" : "SMS", Status = "QUEUED", CreatedOn = now,
            Payload = $"Готові нові результати за замовленням №{order.OrderNumber} ({test.TestName}). Перегляд: /portal/{order.PatientId}/orders/{order.Id}"
        });
    }

    // ------------------------------------------------------------------ portal projection
    public static object Progress(LabOrder order)
    {
        var tests = order.Tests.Where(t => t.Status != OrderTestStatuses.Rejected).ToList();
        var released = tests.Count(t => t.ReleasedAt != null);
        return new { totalTests = tests.Count, releasedTests = released, pendingTests = tests.Count - released, percent = tests.Count == 0 ? 0 : (int)Math.Round(100.0 * released / tests.Count) };
    }

    public static string PortalStatus(LabOrder order)
    {
        if (order.Status == OrderStatuses.Released) return "RELEASED";
        if (OrderStatuses.Terminal.Contains(order.Status)) return order.Status;
        var tests = order.Tests.Where(t => t.Status != OrderTestStatuses.Rejected).ToList();
        var released = tests.Count(t => t.ReleasedAt != null);
        if (released == 0) return order.Status is OrderStatuses.New or OrderStatuses.Collected ? "REGISTERED" : "IN_PROGRESS";
        return released == tests.Count ? "COMPLETED" : "PARTIALLY_COMPLETED";
    }

    public static object PortalOrder(LabOrder order, bool includeTests)
    {
        var releasedTests = order.Tests.Where(t => t.ReleasedAt != null && t.Status != OrderTestStatuses.Rejected).OrderBy(t => t.DisplayOrder).ToList();
        return new
        {
            id = order.Id, orderNumber = order.OrderNumber, orderDatetime = order.OrderDatetime, status = PortalStatus(order), labStatus = order.Status, isUrgentCito = order.IsUrgentCito,
            releasedAt = order.ReleasedAt, progress = Progress(order),
            profiles = order.Tests.Select(t => t.Profile?.Name).Where(n => n != null).Distinct(),
            tests = includeTests ? releasedTests.Select(t => new
            {
                t.Id, t.TestCode, t.TestName, status = t.Status, t.ReleasedAt, profile = t.Profile?.Name,
                value = t.Result == null ? null : DtoMapper.FormatValue(t.Result.NumericValue, t.Result.StringValue, t.Test?.DecimalPlaces ?? 2),
                unit = t.Result?.Unit ?? t.Test?.Unit, referenceDisplay = t.Result?.ReferenceDisplay, flag = t.Result?.Flag, isAbnormal = Core.Clinical.ResultFlags.IsAbnormal(t.Result?.Flag),
                previousValue = t.Result?.PreviousValue, previousAt = t.Result?.PreviousAt, deltaPercent = t.Result?.DeltaPercent, comment = t.Result?.VerificationComment, reportText = t.Result?.ReportText, verifiedAt = t.Result?.VerifiedAt
            }) : null,
            pendingTests = includeTests ? order.Tests.Where(t => t.ReleasedAt == null && t.Status != OrderTestStatuses.Rejected).Select(t => new { t.Id, t.TestCode, t.TestName, status = "PENDING" }) : null
        };
    }
}
