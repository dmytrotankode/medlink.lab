using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed partial class LogisticsService
{
    /// <summary>Тип транспортної тари: є контейнери цього типу → архів (isActive=false), інакше видалення.</summary>
    public async Task<object> DeleteContainerTypeAsync(string id)
    {
        _policy.Require("Видалення типу транспортної тари", LabRoles.Admin);
        var t = await _db.ContainerTypes.FirstOrDefaultAsync(x => (x.Id == id || x.Code == id) && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Тип транспортної тари", id);
        var deps = await _db.TransportContainers.CountAsync(c => c.TypeId == t.Id);
        if (deps > 0)
        {
            t.IsActive = false;
            _audit.Log("SOFT_DELETE", "lab_transport_container_type", t.Id, null, new { isActive = false, dependencies = deps });
            await _db.SaveChangesAsync();
            return new { id = t.Id, deleted = false, deactivated = true, dependencies = deps, message = $"Тип використовується в {deps} контейнерах — переведено в архів" };
        }
        _db.ContainerTypes.Remove(t);
        _audit.Log("DELETE", "lab_transport_container_type", t.Id, new { t.Code, t.Name }, null);
        await _db.SaveChangesAsync();
        return new { id = t.Id, deleted = true };
    }
}
