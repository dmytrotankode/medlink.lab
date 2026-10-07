// FR-GAP-060: платники, прайс-листи й пакети, поліси пацієнтів, розрахунок, оплата, рахунки, друк документів
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class BillingController : LisControllerBase
{
    private readonly BillingService _svc;
    private readonly TubePlanService _tubes;
    public BillingController(BillingService svc, TubePlanService tubes) { _svc = svc; _tubes = tubes; }

    [HttpGet("payers")] public async Task<IActionResult> Payers() => Ok(await _svc.PayersAsync());
    [HttpPost("payers")] public async Task<IActionResult> CreatePayer([FromBody] LabPayer req) => StatusCode(201, await _svc.SavePayerAsync(null, req));
    [HttpPut("payers/{id}")] public async Task<IActionResult> UpdatePayer(string id, [FromBody] LabPayer req) => Ok(await _svc.SavePayerAsync(id, req));

    [HttpGet("price-lists")] public async Task<IActionResult> PriceLists() => Ok(await _svc.PriceListsAsync());
    [HttpPost("price-lists")] public async Task<IActionResult> CreatePriceList([FromBody] PriceListRequest req) => StatusCode(201, await _svc.SavePriceListAsync(null, req));
    [HttpPut("price-lists/{id}")] public async Task<IActionResult> UpdatePriceList(string id, [FromBody] PriceListRequest req) => Ok(await _svc.SavePriceListAsync(id, req));

    /// <summary>Страхові поліси пацієнта ([MedLink-new] mis_patient_insurance).</summary>
    [HttpGet("patients/{patientId}/insurances")] public async Task<IActionResult> Insurances(string patientId) => Ok(await _svc.PatientInsurancesAsync(patientId));
    [HttpPost("patients/{patientId}/insurances")] public async Task<IActionResult> AddInsurance(string patientId, [FromBody] PatientInsuranceRequest req) => StatusCode(201, await _svc.SavePatientInsuranceAsync(patientId, null, req));
    [HttpPut("patients/{patientId}/insurances/{id}")] public async Task<IActionResult> UpdateInsurance(string patientId, string id, [FromBody] PatientInsuranceRequest req) => Ok(await _svc.SavePatientInsuranceAsync(patientId, id, req));

    /// <summary>Попередній розрахунок вартості для діалогу замовлення (платник, покриття, пакети, франшиза).</summary>
    [HttpPost("billing/quote")] public async Task<IActionResult> Quote([FromBody] QuoteRequest req) => Ok(await _svc.QuoteAsync(req, _tubes));

    [HttpGet("orders/{id}/billing")] public async Task<IActionResult> Billing(string id) => Ok(await _svc.BillingAsync(id));
    [HttpPut("orders/{id}/payer")] public async Task<IActionResult> SetPayer(string id, [FromBody] SetPayerRequest req) => Ok(await _svc.SetPayerAsync(id, req));
    [HttpPut("orders/{id}/charges/{chargeId}/patient-choice")] public async Task<IActionResult> PatientChoice(string id, string chargeId, [FromQuery] bool patientPays = true) => Ok(await _svc.SetPatientChoiceAsync(id, chargeId, patientPays));
    [HttpPost("orders/{id}/payments")] public async Task<IActionResult> Pay(string id, [FromBody] PaymentRequest req) => Ok(await _svc.AddPaymentAsync(id, req));
    [HttpPost("orders/{id}/invoice")] public async Task<IActionResult> Invoice(string id) => StatusCode(201, await _svc.IssueInvoiceAsync(id));

    /// <summary>Друк: замовлення-квитанція пацієнту (HTML).</summary>
    [HttpGet("orders/{id}/receipt")] public async Task<IActionResult> Receipt(string id) => Content(await _svc.OrderReceiptHtmlAsync(id), "text/html; charset=utf-8");
    /// <summary>Друк: рахунок платнику (HTML).</summary>
    [HttpGet("invoices/{id}/print")] public async Task<IActionResult> InvoicePrint(string id) => Content(await _svc.InvoiceHtmlAsync(id), "text/html; charset=utf-8");
}
