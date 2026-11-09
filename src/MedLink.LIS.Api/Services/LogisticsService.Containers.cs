// Транспортні контейнери (термобокси, сумки): типи тари, укладка проб по місцях, запечатування (→ маніфест),
// відправка, прийом контейнера цілком (розкомплектування з розбіжностями), повернення тари, історія подій (FR-LOG-101…)
using System.Text.Json;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class ContainerTypeRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public int? Capacity { get; set; }
    public double? TempMinC { get; set; }
    public double? TempMaxC { get; set; }
    public bool? IsReusable { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class ContainerCreateRequest
{
    public string TypeId { get; set; } = "";
    public string OriginDepartmentId { get; set; } = "";
    public string DestinationDepartmentId { get; set; } = "";
    public string? Notes { get; set; }
}

public sealed class ContainerPackRequest
{
    /// <summary>Штрихкод пробірки.</summary>
    public string Barcode { get; set; } = "";
    /// <summary>Бажане місце (1..місткість); не задано → перше вільне.</summary>
    public int? Position { get; set; }
}

public sealed class ContainerSealRequest
{
    public double? TemperatureSeal { get; set; }
    public string? Notes { get; set; }
}

public sealed class ContainerDispatchRequest
{
    public string? CourierName { get; set; }
    public string? CourierPhone { get; set; }
}

public sealed class ContainerReceiveRequest
{
    public double? TemperatureReceipt { get; set; }
    /// <summary>Усі відскановані при розкомплектуванні штрихкоди (у т. ч. пошкоджені й зайві).</summary>
    public List<string> ScannedBarcodes { get; set; } = new();
    /// <summary>Підмножина відсканованих: пошкоджені пробірки (відбраковуються).</summary>
    public List<string> DamagedBarcodes { get; set; } = new();
    public string? Notes { get; set; }
}

public sealed class ContainerNoteRequest
{
    public string? Notes { get; set; }
}

public sealed partial class LogisticsService
{
    public const string ContainerBarcodeCounter = "lab_transport_container_barcode";

    // ------------------------------------------------------------------ типи тари

    public async Task<List<object>> ListContainerTypesAsync(bool includeInactive)
    {
        var types = await _db.ContainerTypes.AsNoTracking().Where(t => t.RecordState != RecordStates.Deleted && (includeInactive || t.IsActive)).OrderBy(t => t.Name).ToListAsync();
        return types.Select(TypeDto).ToList();
    }

    private static object TypeDto(LabTransportContainerType t) => new { t.Id, t.Code, t.Name, t.Capacity, t.TempMinC, t.TempMaxC, t.IsReusable, t.IsActive };

    public async Task<object> CreateContainerTypeAsync(ContainerTypeRequest req)
    {
        _policy.Require("Створення типу транспортної тари", LabRoles.Admin);
        var code = (req.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0) throw ValidationException.Field("code", "Вкажіть код типу");
        if (string.IsNullOrWhiteSpace(req.Name)) throw ValidationException.Field("name", "Вкажіть назву типу");
        if (req.Capacity is null or < 1 or > 500) throw ValidationException.Field("capacity", "Місткість — від 1 до 500 місць");
        ValidateRegime(req.TempMinC, req.TempMaxC);
        if (await _db.ContainerTypes.AnyAsync(t => t.Code == code)) throw new ConflictException($"Тип тари з кодом {code} уже існує");
        var t = new LabTransportContainerType
        {
            Code = code, Name = req.Name.Trim(), Capacity = req.Capacity.Value, TempMinC = req.TempMinC, TempMaxC = req.TempMaxC,
            IsReusable = req.IsReusable ?? false, IsActive = req.IsActive ?? true
        };
        _db.ContainerTypes.Add(t);
        _audit.Log("CREATE", "lab_transport_container_type", t.Id, null, TypeDto(t));
        await _db.SaveChangesAsync();
        return TypeDto(t);
    }

    public async Task<object> UpdateContainerTypeAsync(string id, ContainerTypeRequest req)
    {
        _policy.Require("Редагування типу транспортної тари", LabRoles.Admin);
        var t = await _db.ContainerTypes.FirstOrDefaultAsync(x => x.Id == id && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Тип транспортної тари", id);
        var before = TypeDto(t);
        if (req.Code != null)
        {
            var code = req.Code.Trim().ToUpperInvariant();
            if (code.Length == 0) throw ValidationException.Field("code", "Вкажіть код типу");
            if (code != t.Code && await _db.ContainerTypes.AnyAsync(x => x.Code == code)) throw new ConflictException($"Тип тари з кодом {code} уже існує");
            t.Code = code;
        }
        if (req.Name != null)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) throw ValidationException.Field("name", "Вкажіть назву типу");
            t.Name = req.Name.Trim();
        }
        if (req.Capacity.HasValue)
        {
            if (req.Capacity < 1 || req.Capacity > 500) throw ValidationException.Field("capacity", "Місткість — від 1 до 500 місць");
            if (req.Capacity < t.Capacity)
            {
                var maxUsed = await _db.ContainerItems.Where(i => i.Container!.TypeId == id && ContainerStatuses.Active.Contains(i.Container.Status)).Select(i => (int?)i.Position).MaxAsync() ?? 0;
                if (maxUsed > req.Capacity) throw new ConflictException($"Неможливо зменшити місткість до {req.Capacity}: у діючих контейнерах зайнято місце {maxUsed}");
            }
            t.Capacity = req.Capacity.Value;
        }
        // Температурний режим задається парою; порожня пара — «діє коридор холодового ланцюга з налаштувань»
        if (req.TempMinC.HasValue || req.TempMaxC.HasValue)
        {
            var min = req.TempMinC ?? t.TempMinC; var max = req.TempMaxC ?? t.TempMaxC;
            ValidateRegime(min, max);
            t.TempMinC = min; t.TempMaxC = max;
        }
        if (req.IsReusable.HasValue) t.IsReusable = req.IsReusable.Value;
        if (req.IsActive.HasValue) t.IsActive = req.IsActive.Value;
        _audit.Log("UPDATE", "lab_transport_container_type", t.Id, before, TypeDto(t));
        await _db.SaveChangesAsync();
        return TypeDto(t);
    }

    private static void ValidateRegime(double? min, double? max)
    {
        if (min.HasValue != max.HasValue) throw ValidationException.Field("tempMinC", "Температурний режим задається парою «мін — макс» або не задається");
        if (min.HasValue && min >= max) throw ValidationException.Field("tempMinC", "Мінімальна температура має бути меншою за максимальну");
    }

    /// <summary>Коридор контейнера: режим типу тари або (FR-LOG-001) коридор холодового ланцюга з налаштувань.</summary>
    private async Task<(double Min, double Max)> ContainerCorridorAsync(LabTransportContainerType type)
    {
        if (type.TempMinC.HasValue && type.TempMaxC.HasValue) return (type.TempMinC.Value, type.TempMaxC.Value);
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return (settings?.ColdChainMinC ?? 2.0, settings?.ColdChainMaxC ?? 8.0);
    }

    // ------------------------------------------------------------------ читання

    private IQueryable<LabTransportContainer> ContainerQuery() => _db.TransportContainers
        .Include(c => c.Type).Include(c => c.OriginDepartment).Include(c => c.DestinationDepartment).Include(c => c.Manifest)
        .Include(c => c.Items).ThenInclude(i => i.Sample).ThenInclude(s => s!.Order);

    public async Task<List<object>> ListContainersAsync(string? status, string? search, string? originDepartmentId, string? destinationDepartmentId, string? typeId, int take)
    {
        var q = ContainerQuery().AsNoTracking().Where(c => c.RecordState != RecordStates.Deleted);
        if (!string.IsNullOrWhiteSpace(status)) { var st = status.Trim().ToUpperInvariant(); q = q.Where(c => c.Status == st); }
        if (!string.IsNullOrWhiteSpace(originDepartmentId)) q = q.Where(c => c.OriginDepartmentId == originDepartmentId);
        if (!string.IsNullOrWhiteSpace(destinationDepartmentId)) q = q.Where(c => c.DestinationDepartmentId == destinationDepartmentId);
        if (!string.IsNullOrWhiteSpace(typeId)) q = q.Where(c => c.TypeId == typeId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToUpperInvariant();
            q = q.Where(c => c.Barcode.Contains(s) || c.Items.Any(i => i.Barcode.Contains(s)));
        }
        var list = await q.OrderByDescending(c => c.CreatedOn).Take(Math.Clamp(take, 1, 1000)).ToListAsync();
        return list.Select(ContainerDto).ToList();
    }

    public async Task<object> GetContainerAsync(string id) => ContainerDto(await LoadContainerAsync(id));

    public async Task<object> GetContainerByBarcodeAsync(string barcode)
    {
        var bc = (barcode ?? "").Trim().ToUpperInvariant();
        var c = await ContainerQuery().FirstOrDefaultAsync(x => x.Barcode == bc && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Контейнер", barcode ?? "");
        return ContainerDto(c);
    }

    public async Task<List<object>> ListContainerEventsAsync(string id)
    {
        if (!await _db.TransportContainers.AnyAsync(c => c.Id == id && c.RecordState != RecordStates.Deleted)) throw NotFoundException.For("Контейнер", id);
        var events = await _db.ContainerEvents.AsNoTracking().Where(e => e.ContainerId == id).OrderBy(e => e.At).ThenBy(e => e.CreatedOn).ToListAsync();
        return events.Select(e => (object)new { e.Id, e.EventType, e.At, e.EmployeeId, e.SampleBarcode, e.Position, e.Temperature, e.Details }).ToList();
    }

    private async Task<LabTransportContainer> LoadContainerAsync(string id) =>
        await ContainerQuery().FirstOrDefaultAsync(c => c.Id == id && c.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Контейнер", id);

    private object ContainerDto(LabTransportContainer c)
    {
        var t = c.Type!;
        var items = c.Items.OrderBy(i => i.Position).ToList();
        var allowed = _policy.AllowedActions(LisEntities.Container, c.Status)
            .Where(a => !(a.Action == ContainerActions.Return && !t.IsReusable) && !(a.Action == ContainerActions.Delete && items.Count > 0)
                        && !(a.Action is ContainerActions.Seal && items.Count == 0))
            .ToList();
        return new
        {
            c.Id, c.Barcode, c.TypeId, typeCode = t.Code, typeName = t.Name, capacity = t.Capacity, tempMinC = t.TempMinC, tempMaxC = t.TempMaxC, isReusable = t.IsReusable,
            c.Status, c.OriginDepartmentId, originDepartmentName = c.OriginDepartment?.Caption, c.DestinationDepartmentId, destinationDepartmentName = c.DestinationDepartment?.Caption,
            c.ManifestId, manifestNumber = c.Manifest?.ManifestNumber,
            c.PackedAt, c.PackedById, c.SealedAt, c.SealedById, c.TemperatureSeal, c.DispatchedAt, c.DispatchedById,
            c.ReceivedAt, c.ReceivedById, c.TemperatureReceipt, c.IsColdChainViolated, c.ReturnedAt, c.ReturnedById, c.Notes, c.CreatedOn,
            itemsCount = items.Count, freeSlots = Math.Max(0, t.Capacity - items.Count),
            missingCount = c.MissingCount, extraCount = c.ExtraCount, damagedCount = c.DamagedCount, hasDiscrepancies = c.MissingCount + c.ExtraCount + c.DamagedCount > 0,
            discrepancies = new
            {
                missing = items.Where(i => i.Status == ContainerItemStatuses.Missing).Select(i => i.Barcode).ToList(),
                damaged = items.Where(i => i.Status == ContainerItemStatuses.Damaged).Select(i => i.Barcode).ToList(),
                extra = ParseExtra(c.ExtraBarcodesJson)
            },
            items = items.Select(i => new { i.Id, i.SampleId, i.Barcode, i.Position, i.Status, i.PackedAt, i.PackedById, i.ReceivedAt, orderNumber = i.Sample?.Order?.OrderNumber }),
            allowedActions = allowed
        };
    }

    private static List<string> ParseExtra(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); } catch (JsonException) { return new List<string>(); }
    }

    private void AddContainerEvent(LabTransportContainer c, string type, string? details = null, string? sampleBarcode = null, int? position = null, double? temperature = null) =>
        _db.ContainerEvents.Add(new LabTransportContainerEvent
        {
            ContainerId = c.Id, EventType = type, At = DateTime.UtcNow, EmployeeId = _current.EmployeeId, SampleBarcode = sampleBarcode,
            Position = position, Temperature = temperature, Details = details is { Length: > 1000 } ? details[..1000] : details
        });

    // ------------------------------------------------------------------ створення та укладка

    public async Task<object> CreateContainerAsync(ContainerCreateRequest req)
    {
        _policy.Require("Створення транспортного контейнера", LisStateMachine.ContainerPackers);
        var type = await _db.ContainerTypes.FirstOrDefaultAsync(t => t.Id == req.TypeId && t.RecordState != RecordStates.Deleted) ?? throw ValidationException.Field("typeId", "Тип тари не знайдено");
        if (!type.IsActive) throw ValidationException.Field("typeId", "Тип тари вимкнено");
        if (string.IsNullOrWhiteSpace(req.OriginDepartmentId) || !await _db.Departments.AnyAsync(d => d.Id == req.OriginDepartmentId)) throw ValidationException.Field("originDepartmentId", "Пункт відправлення не знайдено");
        if (string.IsNullOrWhiteSpace(req.DestinationDepartmentId) || !await _db.Departments.AnyAsync(d => d.Id == req.DestinationDepartmentId)) throw ValidationException.Field("destinationDepartmentId", "Пункт призначення не знайдено");
        if (req.OriginDepartmentId == req.DestinationDepartmentId) throw ValidationException.Field("destinationDepartmentId", "Пункт призначення має відрізнятися від пункту відправлення");

        string barcode;
        do { barcode = $"TC{await _numerators.NextCounterAsync(ContainerBarcodeCounter):D8}"; }
        while (await _db.TransportContainers.AnyAsync(c => c.Barcode == barcode));

        var c = new LabTransportContainer
        {
            Barcode = barcode, TypeId = type.Id, Status = ContainerStatuses.Free, OriginDepartmentId = req.OriginDepartmentId,
            DestinationDepartmentId = req.DestinationDepartmentId, Notes = req.Notes
        };
        _db.TransportContainers.Add(c);
        AddContainerEvent(c, "CREATED", $"Тип {type.Code}, місткість {type.Capacity}");
        _audit.Log("CREATE", "lab_transport_container", c.Id, null, new { c.Barcode, type = type.Code, c.OriginDepartmentId, c.DestinationDepartmentId });
        await _db.SaveChangesAsync();
        return await GetContainerAsync(c.Id);
    }

    public async Task<object> PackContainerAsync(string id, ContainerPackRequest req)
    {
        var c = await LoadContainerAsync(id);
        var rule = _policy.Ensure(LisEntities.Container, ContainerActions.Pack, c.Status, $"Контейнер {c.Barcode}");
        var bc = (req.Barcode ?? "").Trim();
        if (bc.Length == 0) throw ValidationException.Field("barcode", "Відскануйте штрихкод пробірки");
        var sample = await _db.Samples.Include(s => s.Order).FirstOrDefaultAsync(s => s.Barcode == bc && s.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Пробірка", bc);

        if (c.Items.Any(i => i.SampleId == sample.Id)) throw new ConflictException($"Пробірка {bc} уже лежить у цьому контейнері");
        var other = await _db.ContainerItems.Include(i => i.Container)
            .FirstOrDefaultAsync(i => i.SampleId == sample.Id && i.Status == ContainerItemStatuses.Packed && ContainerStatuses.Active.Contains(i.Container!.Status));
        if (other != null) throw new ConflictException($"Пробірка {bc} уже в контейнері {other.Container!.Barcode}");
        if (sample.Status != SampleStatuses.Collected)
            throw new ConflictException(sample.Status == SampleStatuses.Pending
                ? $"Пробірка {bc} ще не забрана — спочатку заберіть її на пункті забору"
                : $"Пробірка {bc} має статус «{UkLabels.Status(LisEntities.Sample, sample.Status)}»: до контейнера кладуться лише забрані пробірки");
        if (sample.Order?.DepartmentId != c.OriginDepartmentId)
        {
            var sampleDept = sample.Order?.DepartmentId == null ? null : (await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == sample.Order.DepartmentId))?.Caption;
            throw new ConflictException($"Пробірка {bc} належить іншому пункту відправлення — «{sampleDept ?? "не вказано"}», а контейнер {c.Barcode} призначений для «{c.OriginDepartment?.Caption ?? "не вказано"}»");
        }
        if (await _db.LogisticsItems.AnyAsync(i => i.SampleId == sample.Id && i.Logistics!.Status == LisStateMachine.ManifestCreated))
            throw new ConflictException($"Пробірка {bc} уже внесена в чернетку маніфесту");

        var capacity = c.Type!.Capacity;
        var used = c.Items.Select(i => i.Position).ToHashSet();
        int position;
        if (req.Position.HasValue)
        {
            position = req.Position.Value;
            if (position < 1 || position > capacity) throw ValidationException.Field("position", $"Місце має бути в межах 1…{capacity}");
            if (used.Contains(position)) throw new ConflictException($"Місце {position} у контейнері {c.Barcode} уже зайняте");
        }
        else
        {
            if (used.Count >= capacity) throw new ConflictException($"Контейнер {c.Barcode} заповнений ({capacity} з {capacity} місць)");
            position = Enumerable.Range(1, capacity).First(p => !used.Contains(p));
        }

        var now = DateTime.UtcNow;
        var item = new LabTransportContainerItem { ContainerId = c.Id, SampleId = sample.Id, Barcode = sample.Barcode, Position = position, Status = ContainerItemStatuses.Packed, PackedAt = now, PackedById = _current.EmployeeId };
        _db.ContainerItems.Add(item); // навігація c.Items оновлюється EF автоматично
        c.Status = rule.ToStatus!;
        c.PackedAt ??= now; c.PackedById ??= _current.EmployeeId;
        AddContainerEvent(c, "PACKED", null, sample.Barcode, position);
        _audit.Log("PACK", "lab_transport_container", c.Id, null, new { barcode = sample.Barcode, position });
        await _db.SaveChangesAsync();
        return await GetContainerAsync(c.Id);
    }

    public async Task<object> UnpackContainerAsync(string id, string barcode)
    {
        var c = await LoadContainerAsync(id);
        _policy.Ensure(LisEntities.Container, ContainerActions.Unpack, c.Status, $"Контейнер {c.Barcode}");
        var bc = (barcode ?? "").Trim();
        var item = c.Items.FirstOrDefault(i => i.Barcode == bc) ?? throw NotFoundException.For("Пробірка в контейнері", bc);
        c.Items.Remove(item);
        _db.ContainerItems.Remove(item);
        if (c.Items.Count == 0) { c.Status = ContainerStatuses.Free; c.PackedAt = null; c.PackedById = null; }
        AddContainerEvent(c, "UNPACKED", null, item.Barcode, item.Position);
        _audit.Log("UNPACK", "lab_transport_container", c.Id, new { barcode = item.Barcode, item.Position }, null);
        await _db.SaveChangesAsync();
        return await GetContainerAsync(c.Id);
    }

    public async Task DeleteContainerAsync(string id)
    {
        var c = await LoadContainerAsync(id);
        _policy.Ensure(LisEntities.Container, ContainerActions.Delete, c.Status, $"Контейнер {c.Barcode}");
        if (c.Items.Count > 0) throw new ConflictException($"Контейнер {c.Barcode} не порожній — спочатку зніміть проби");
        _db.TransportContainers.Remove(c);
        _audit.Log("DELETE", "lab_transport_container", c.Id, new { c.Barcode }, null);
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ запечатування, відправка

    public async Task<object> SealContainerAsync(string id, ContainerSealRequest req)
    {
        var c = await LoadContainerAsync(id);
        var rule = _policy.Ensure(LisEntities.Container, ContainerActions.Seal, c.Status, $"Контейнер {c.Barcode}");
        if (c.Items.Count == 0) throw new ConflictException($"Контейнер {c.Barcode} порожній — нічого запечатувати");
        if (!req.TemperatureSeal.HasValue) throw ValidationException.Field("temperatureSeal", "Вкажіть температуру в контейнері при запечатуванні");
        var temp = req.TemperatureSeal.Value;

        // Проби не повинні змінитися між укладкою й запечатуванням
        var sampleIds = c.Items.Select(i => i.SampleId).ToList();
        var samples = await _db.Samples.AsNoTracking().Where(s => sampleIds.Contains(s.Id)).ToListAsync();
        var bad = samples.FirstOrDefault(s => s.Status != SampleStatuses.Collected);
        if (bad != null) throw new ConflictException($"Пробірка {bad.Barcode} має статус «{UkLabels.Status(LisEntities.Sample, bad.Status)}»: зніміть її з контейнера перед запечатуванням");

        var (min, max) = await ContainerCorridorAsync(c.Type!);
        var violated = temp < min || temp > max;
        var now = DateTime.UtcNow;
        var number = await _numerators.NextManifestNumberAsync(now);

        var m = new LabSampleLogistics
        {
            ManifestNumber = number, OriginDepartmentId = c.OriginDepartmentId, DestinationDepartmentId = c.DestinationDepartmentId,
            TemperatureDispatch = temp, Status = LisStateMachine.ManifestCreated, Notes = $"Контейнер {c.Barcode}"
        };
        foreach (var item in c.Items.OrderBy(i => i.Position))
            m.Items.Add(new LabSampleLogisticsItem { LogisticsId = m.Id, SampleId = item.SampleId, Barcode = item.Barcode, Status = "EN_ROUTE" });
        _db.Logistics.Add(m);

        c.ManifestId = m.Id; c.Manifest = m;
        c.Status = rule.ToStatus!;
        c.SealedAt = now; c.SealedById = _current.EmployeeId; c.TemperatureSeal = temp;
        c.IsColdChainViolated = violated;
        if (!string.IsNullOrWhiteSpace(req.Notes)) c.Notes = string.IsNullOrWhiteSpace(c.Notes) ? req.Notes : c.Notes + "\n" + req.Notes;
        if (violated) c.Notes = (c.Notes ?? "") + $"\n[!] Температура при запечатуванні {temp}°C поза режимом {min}..{max}°C";
        AddContainerEvent(c, "SEALED", $"Маніфест {number}; режим {min}..{max}°C" + (violated ? "; ПОРУШЕННЯ режиму" : ""), null, null, temp);
        _audit.Log("SEAL", "lab_transport_container", c.Id, null, new { c.Status, temp, manifest = number, violated });
        await _db.SaveChangesAsync();
        return await GetContainerAsync(c.Id);
    }

    public async Task<object> DispatchContainerAsync(string id, ContainerDispatchRequest? req)
    {
        var c = await LoadContainerAsync(id);
        var rule = _policy.Ensure(LisEntities.Container, ContainerActions.Dispatch, c.Status, $"Контейнер {c.Barcode}");
        var m = await LoadAsync(c.ManifestId ?? throw new ConflictException($"Контейнер {c.Barcode} не має маніфесту"));
        if (!string.IsNullOrWhiteSpace(req?.CourierName)) m.CourierName = req!.CourierName;
        if (!string.IsNullOrWhiteSpace(req?.CourierPhone)) m.CourierPhone = req!.CourierPhone;
        Dispatch(m, system: true);
        c.Status = rule.ToStatus!;
        c.DispatchedAt = m.DispatchedAt; c.DispatchedById = _current.EmployeeId;
        AddContainerEvent(c, "DISPATCHED", $"Маніфест {m.ManifestNumber}" + (string.IsNullOrWhiteSpace(m.CourierName) ? "" : $"; кур'єр {m.CourierName}"));
        _audit.Log("DISPATCH", "lab_transport_container", c.Id, null, new { c.Status, c.DispatchedAt, m.ManifestNumber });
        await _db.SaveChangesAsync();
        return await GetContainerAsync(c.Id);
    }

    // ------------------------------------------------------------------ прийом цілком (розкомплектування)

    public async Task<object> ReceiveContainerAsync(string id, ContainerReceiveRequest req)
    {
        var c = await LoadContainerAsync(id);
        var rule = _policy.Ensure(LisEntities.Container, ContainerActions.Receive, c.Status, $"Контейнер {c.Barcode}");
        if (!req.TemperatureReceipt.HasValue) throw ValidationException.Field("temperatureReceipt", "Вкажіть температуру в контейнері при прийомі");
        var temp = req.TemperatureReceipt.Value;
        var m = await LoadAsync(c.ManifestId ?? throw new ConflictException($"Контейнер {c.Barcode} не має маніфесту"));

        var expected = c.Items.Select(i => i.Barcode).ToHashSet();
        var damaged = req.DamagedBarcodes.Select(b => b.Trim()).Where(b => b.Length > 0).ToHashSet();
        var scanned = req.ScannedBarcodes.Select(b => b.Trim()).Where(b => b.Length > 0).Concat(damaged).ToHashSet();
        var damagedExpected = damaged.Where(expected.Contains).ToList();
        var received = scanned.Where(b => expected.Contains(b) && !damaged.Contains(b)).ToList();
        var missing = expected.Where(b => !scanned.Contains(b)).OrderBy(b => b).ToList();
        var extra = scanned.Where(b => !expected.Contains(b)).OrderBy(b => b).ToList();

        var (min, max) = await ContainerCorridorAsync(c.Type!);
        await ReceiveCoreAsync(m, temp, received, damagedExpected, req.Notes, (min, max), system: true);

        var now = DateTime.UtcNow;
        foreach (var item in c.Items)
        {
            item.Status = damagedExpected.Contains(item.Barcode) ? ContainerItemStatuses.Damaged : received.Contains(item.Barcode) ? ContainerItemStatuses.Received : ContainerItemStatuses.Missing;
            if (item.Status == ContainerItemStatuses.Received) item.ReceivedAt = now;
        }
        c.Status = rule.ToStatus!;
        c.ReceivedAt = now; c.ReceivedById = _current.EmployeeId; c.TemperatureReceipt = temp;
        c.IsColdChainViolated = m.IsColdChainViolated;
        c.MissingCount = missing.Count; c.ExtraCount = extra.Count; c.DamagedCount = damagedExpected.Count;
        c.ExtraBarcodesJson = extra.Count > 0 ? JsonSerializer.Serialize(extra) : null;
        var summary = new List<string>();
        if (missing.Count > 0) summary.Add("відсутні: " + string.Join(", ", missing));
        if (damagedExpected.Count > 0) summary.Add("пошкоджені: " + string.Join(", ", damagedExpected));
        if (extra.Count > 0) summary.Add("зайві: " + string.Join(", ", extra));
        var tail = new List<string>();
        if (!string.IsNullOrWhiteSpace(req.Notes)) tail.Add(req.Notes);
        if (summary.Count > 0) tail.Add("[!] Розбіжності при прийомі — " + string.Join("; ", summary));
        if (m.IsColdChainViolated) tail.Add($"[!] Порушення холодового ланцюга: запечатування {c.TemperatureSeal}°C, прийом {temp}°C (режим {min}..{max}°C)");
        if (tail.Count > 0) c.Notes = (string.IsNullOrWhiteSpace(c.Notes) ? "" : c.Notes + "\n") + string.Join("\n", tail);
        AddContainerEvent(c, "RECEIVED", summary.Count > 0 ? string.Join("; ", summary) : "Без розбіжностей" + (m.IsColdChainViolated ? "; ПОРУШЕННЯ холодового ланцюга" : ""), null, null, temp);
        _audit.Log("RECEIVE", "lab_transport_container", c.Id, null, new { c.Status, temp, c.IsColdChainViolated, missing, damaged = damagedExpected, extra });
        await _db.SaveChangesAsync();
        return await GetContainerAsync(c.Id);
    }

    // ------------------------------------------------------------------ повернення тари, втрата

    public async Task<object> ReturnContainerAsync(string id, ContainerNoteRequest? req)
    {
        var c = await LoadContainerAsync(id);
        var rule = _policy.Ensure(LisEntities.Container, ContainerActions.Return, c.Status, $"Контейнер {c.Barcode}");
        if (!c.Type!.IsReusable) throw new ConflictException($"Тип тари {c.Type.Code} одноразовий — повернення не передбачене");
        c.Status = rule.ToStatus!;
        c.ReturnedAt = DateTime.UtcNow; c.ReturnedById = _current.EmployeeId;
        if (!string.IsNullOrWhiteSpace(req?.Notes)) c.Notes = (string.IsNullOrWhiteSpace(c.Notes) ? "" : c.Notes + "\n") + req!.Notes;
        AddContainerEvent(c, "RETURNED", req?.Notes);
        _audit.Log("RETURN", "lab_transport_container", c.Id, null, new { c.Status, c.ReturnedAt });
        await _db.SaveChangesAsync();
        return await GetContainerAsync(c.Id);
    }

    public async Task<object> MarkContainerLostAsync(string id, string reason)
    {
        var c = await LoadContainerAsync(id);
        var rule = _policy.Ensure(LisEntities.Container, ContainerActions.MarkLost, c.Status, $"Контейнер {c.Barcode}");
        if (string.IsNullOrWhiteSpace(reason)) throw ValidationException.Field("reason", "Вкажіть причину");
        c.Status = rule.ToStatus!;
        c.Notes = (string.IsNullOrWhiteSpace(c.Notes) ? "" : c.Notes + "\n") + "Втрачено: " + reason;
        AddContainerEvent(c, "LOST", reason);
        _audit.Log("LOST", "lab_transport_container", c.Id, null, new { c.Status, reason });
        await _db.SaveChangesAsync();
        return await GetContainerAsync(c.Id);
    }
}
