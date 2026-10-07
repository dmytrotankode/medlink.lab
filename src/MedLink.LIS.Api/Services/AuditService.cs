// Аудит: кожна мутація пише lab_audit_log (userId, action, entity, entityId, before/after JSON, at, ip)
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;

namespace MedLink.LIS.Api.Services;

public interface IAuditService
{
    void Log(string action, string entity, string? entityId, object? before = null, object? after = null, string? comment = null);
}

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 8
    };

    private readonly LisDbContext _db;
    private readonly ICurrentEmployee _current;

    public AuditService(LisDbContext db, ICurrentEmployee current)
    {
        _db = db;
        _current = current;
    }

    /// <summary>Додає запис аудиту до ChangeTracker; збереження — разом із основною транзакцією.</summary>
    public void Log(string action, string entity, string? entityId, object? before = null, object? after = null, string? comment = null)
    {
        _db.AuditLog.Add(new LabAuditLog
        {
            UserId = _current.EmployeeId,
            UserName = _current.FullName,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            BeforeJson = before == null ? null : Safe(before),
            AfterJson = after == null ? null : Safe(after),
            At = DateTime.UtcNow,
            Ip = _current.Ip,
            Comment = comment
        });
    }

    private static string Safe(object o)
    {
        try { return JsonSerializer.Serialize(o, Options); }
        catch { return o.ToString() ?? ""; }
    }
}
