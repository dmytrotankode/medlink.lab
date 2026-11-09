// Транспортні контейнери (термобокси, сумки): типи тари, укладка, запечатування, відправка, прийом цілком, повернення тари
using MedLink.LIS.Api.Models;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class TransportContainersController : LisControllerBase
{
    private readonly LogisticsService _logistics;
    public TransportContainersController(LogisticsService logistics) => _logistics = logistics;

    // ---------- довідник типів тари ----------

    [HttpGet("logistics/container-types")]
    public async Task<IActionResult> ListTypes([FromQuery] bool includeInactive = false) => Ok(await _logistics.ListContainerTypesAsync(includeInactive));

    [HttpPost("logistics/container-types")]
    public async Task<IActionResult> CreateType([FromBody] ContainerTypeRequest req) => StatusCode(201, await _logistics.CreateContainerTypeAsync(req));

    [HttpPut("logistics/container-types/{id}")]
    public async Task<IActionResult> UpdateType(string id, [FromBody] ContainerTypeRequest req) => Ok(await _logistics.UpdateContainerTypeAsync(id, req));

    /// <summary>Тип тари у архів (є контейнери цього типу) або видалення.</summary>
    [HttpDelete("logistics/container-types/{id}")]
    public async Task<IActionResult> DeleteType(string id) => Ok(await _logistics.DeleteContainerTypeAsync(id));

    // ---------- контейнери ----------

    [HttpGet("logistics/containers")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? search, [FromQuery] string? originDepartmentId, [FromQuery] string? destinationDepartmentId, [FromQuery] string? typeId, [FromQuery] int take = 200)
        => Ok(await _logistics.ListContainersAsync(status, search, originDepartmentId, destinationDepartmentId, typeId, take));

    [HttpGet("logistics/containers/by-barcode/{barcode}")]
    public async Task<IActionResult> ByBarcode(string barcode) => Ok(await _logistics.GetContainerByBarcodeAsync(barcode));

    [HttpGet("logistics/containers/{id}")]
    public async Task<IActionResult> Get(string id) => Ok(await _logistics.GetContainerAsync(id));

    [HttpGet("logistics/containers/{id}/events")]
    public async Task<IActionResult> Events(string id) => Ok(await _logistics.ListContainerEventsAsync(id));

    /// <summary>Створення порожнього контейнера: штрихкод TC + 8 цифр генерується нумератором.</summary>
    [HttpPost("logistics/containers")]
    public async Task<IActionResult> Create([FromBody] ContainerCreateRequest req) => StatusCode(201, await _logistics.CreateContainerAsync(req));

    [HttpDelete("logistics/containers/{id}")]
    public async Task<IActionResult> Delete(string id) { await _logistics.DeleteContainerAsync(id); return NoContent(); }

    /// <summary>Укладка: скан штрихкода пробірки → місце (задане або перше вільне).</summary>
    [HttpPost("logistics/containers/{id}/pack")]
    public async Task<IActionResult> Pack(string id, [FromBody] ContainerPackRequest req) => Ok(await _logistics.PackContainerAsync(id, req));

    /// <summary>Зняти пробірку з місця (лише до запечатування).</summary>
    [HttpDelete("logistics/containers/{id}/items/{barcode}")]
    public async Task<IActionResult> Unpack(string id, string barcode) => Ok(await _logistics.UnpackContainerAsync(id, barcode));

    /// <summary>Запечатування: фіксує температуру, формує маніфест (CREATED).</summary>
    [HttpPost("logistics/containers/{id}/seal")]
    public async Task<IActionResult> Seal(string id, [FromBody] ContainerSealRequest req) => Ok(await _logistics.SealContainerAsync(id, req));

    /// <summary>Відправка: маніфест → DISPATCHED, проби → IN_TRANSIT.</summary>
    [HttpPost("logistics/containers/{id}/dispatch")]
    public async Task<IActionResult> Dispatch(string id, [FromBody] ContainerDispatchRequest? req) => Ok(await _logistics.DispatchContainerAsync(id, req));

    /// <summary>Прийом контейнера цілком: розбіжності (відсутні / зайві / пошкоджені), холодовий ланцюг.</summary>
    [HttpPost("logistics/containers/{id}/receive")]
    public async Task<IActionResult> Receive(string id, [FromBody] ContainerReceiveRequest req) => Ok(await _logistics.ReceiveContainerAsync(id, req));

    /// <summary>Повернення багаторазової тари після прийому.</summary>
    [HttpPost("logistics/containers/{id}/return")]
    public async Task<IActionResult> Return(string id, [FromBody] ContainerNoteRequest? req) => Ok(await _logistics.ReturnContainerAsync(id, req));

    [HttpPost("logistics/containers/{id}/lost")]
    public async Task<IActionResult> Lost(string id, [FromBody] ReasonRequest req) => Ok(await _logistics.MarkContainerLostAsync(id, req.Reason));
}
