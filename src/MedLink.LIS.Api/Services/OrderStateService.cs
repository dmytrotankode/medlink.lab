// Похідні переходи статусу замовлення (наслідок дій над пробірками/тестами) + токен верифікації бланка
using System.Security.Cryptography;
using System.Text;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;

namespace MedLink.LIS.Api.Services;

public sealed class OrderStateService
{
    private readonly IConfiguration _config;
    public OrderStateService(IConfiguration config) => _config = config;

    /// <summary>Після зміни статусів пробірок: NEW→COLLECTED, COLLECTED/IN_TRANSIT→RECEIVED, усі відбраковані → REJECTED.</summary>
    public void RecomputeFromSamples(LabOrder order)
    {
        var samples = order.Samples.Where(s => s.Status != SampleStatuses.Disposed).ToList();
        if (samples.Count == 0) return;
        var active = samples.Where(s => s.Status != SampleStatuses.Rejected).ToList();
        if (active.Count == 0 && OrderStatuses.Terminal.Contains(order.Status) == false && order.Status != OrderStatuses.Released)
        {
            order.Status = OrderStatuses.Rejected;
            return;
        }
        if (order.Status == OrderStatuses.New && active.Any(s => s.Status != SampleStatuses.Pending))
            order.Status = OrderStatuses.Collected;
        if ((order.Status == OrderStatuses.Collected || order.Status == OrderStatuses.InTransit) &&
            active.Any(s => s.Status is SampleStatuses.Received or SampleStatuses.Processing or SampleStatuses.Stored))
            order.Status = OrderStatuses.Received;
        if (order.Status == OrderStatuses.Collected && active.All(s => s.Status == SampleStatuses.InTransit))
            order.Status = OrderStatuses.InTransit;
    }

    /// <summary>Перший результат → IN_PROGRESS.</summary>
    public void MarkInProgress(LabOrder order)
    {
        if (order.Status == OrderStatuses.Received) order.Status = OrderStatuses.InProgress;
    }

    /// <summary>Усі тести у фінальному статусі (VERIFIED/AUTO_VERIFIED/REJECTED), хоча б один верифікований → COMPLETED.</summary>
    public bool RecomputeCompletion(LabOrder order)
    {
        if (!OrderStatuses.Working.Contains(order.Status)) return false;
        var tests = order.Tests.ToList();
        if (tests.Count == 0) return false;
        var verified = tests.Count(t => OrderTestStatuses.VerifiedAny.Contains(t.Status));
        if (tests.All(t => OrderTestStatuses.Final.Contains(t.Status)) && verified > 0)
        {
            order.Status = OrderStatuses.Completed;
            order.CompletedAt = DateTime.UtcNow;
            return true;
        }
        // Прогресивна видача: частина тестів верифікована → PARTIALLY_COMPLETED
        order.Status = verified > 0 ? OrderStatuses.PartiallyCompleted : OrderStatuses.InProgress;
        return false;
    }

    /// <summary>Повернення з COMPLETED у IN_PROGRESS/PARTIALLY_COMPLETED, якщо з'явився неверифікований тест.</summary>
    public void RecomputeReopen(LabOrder order)
    {
        if (order.Status == OrderStatuses.Completed && order.Tests.Any(t => !OrderTestStatuses.Final.Contains(t.Status)))
        {
            order.Status = order.Tests.Any(t => OrderTestStatuses.VerifiedAny.Contains(t.Status)) ? OrderStatuses.PartiallyCompleted : OrderStatuses.InProgress;
            order.CompletedAt = null;
        }
        else if (OrderStatuses.Working.Contains(order.Status)) RecomputeCompletion(order);
    }

    /// <summary>token = HMAC-SHA256(orderId|releasedAt, secret) base64url.</summary>
    public string VerifyToken(string orderId, DateTime releasedAt)
    {
        var secret = _config["Lab:VerifySecret"] ?? "medlink-lis";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{orderId}|{releasedAt:O}"));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
