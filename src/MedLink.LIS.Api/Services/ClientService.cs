// =============================================================================
// FR-REF-002. Лабораторія як виконавець для інших клінік (send-in).
// Клієнт = платник kind=CLINIC (lab_payer) із договірним прайсом. Замовлення клієнта: тип направлення EXTERNAL_CLINIC,
// платник — клініка (послуги поза прайсом договору — каса пацієнта). Прийом: ручна реєстрація або реєстр клініки
// (CSV/XLSX) з попереднім переглядом; видача: список замовлень клієнта зі статусами та бланками (основа порталу клієнта);
// взаєморозрахунки: зведений рахунок/акт за період по невиставлених нарахуваннях клієнта.
// =============================================================================
using System.Globalization;
using ClosedXML.Excel;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class ClientImportRow
{
    public int Line { get; set; }
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public DateTime? BirthDate { get; set; }
    public string Gender { get; set; } = "U";
    public string? Phone { get; set; }
    /// <summary>Номер направлення в клініці.</summary>
    public string? ClinicNumber { get; set; }
    public string? DoctorName { get; set; }
    /// <summary>Коди профілів/показників через «;» або «,».</summary>
    public List<string> Codes { get; set; } = new();
    public bool IsCito { get; set; }
    public string Status { get; set; } = "READY";
    public string? Message { get; set; }
    public string? PatientId { get; set; }
    public bool NewPatient { get; set; }
    public string? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public decimal? PayerAmount { get; set; }
    public decimal? PatientAmount { get; set; }
}

public sealed class PeriodInvoiceRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public sealed class ClientService
{
    private readonly LisDbContext _db;
    private readonly OrderService _orders;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly INumeratorService _numerators;

    public ClientService(LisDbContext db, OrderService orders, IRolePolicy policy, IAuditService audit, INumeratorService numerators)
    { _db = db; _orders = orders; _policy = policy; _audit = audit; _numerators = numerators; }

    private async Task<LabPayer> ClientAsync(string id) =>
        await _db.Payers.FirstOrDefaultAsync(p => (p.Id == id || p.Code == id) && p.Kind == PayerKinds.Clinic && p.RecordState != RecordStates.Deleted)
        ?? throw NotFoundException.For("Клієнт (клініка)", id);

    public async Task<List<object>> ClientsAsync()
    {
        var clients = await _db.Payers.AsNoTracking().Include(p => p.PriceList).Where(p => p.Kind == PayerKinds.Clinic && p.RecordState != RecordStates.Deleted).OrderBy(p => p.Name).ToListAsync();
        var ids = clients.Select(c => c.Id).ToList();
        // Суми агрегуються в пам'яті: SQLite не підтримує SUM для decimal
        var stats = (await _db.Orders.AsNoTracking().Where(o => o.PayerId != null && ids.Contains(o.PayerId) && o.RecordState != RecordStates.Deleted)
                .Select(o => new { o.PayerId, o.Status, o.PayerAmount }).ToListAsync())
            .GroupBy(o => o.PayerId!).Select(g => new { g.Key, Orders = g.Count(), Open = g.Count(o => o.Status != OrderStatuses.Released && o.Status != OrderStatuses.Cancelled), Amount = g.Sum(o => o.PayerAmount) }).ToList();
        var invoiced = (await _db.Invoices.AsNoTracking().Where(i => ids.Contains(i.PayerId) && i.Status != "CANCELLED").Select(i => new { i.PayerId, i.Amount }).ToListAsync())
            .GroupBy(i => i.PayerId).Select(g => new { g.Key, Amount = g.Sum(i => i.Amount) }).ToList();
        return clients.Select(c =>
        {
            var s = stats.FirstOrDefault(x => x.Key == c.Id);
            var inv = invoiced.FirstOrDefault(x => x.Key == c.Id)?.Amount ?? 0;
            return (object)new
            {
                c.Id, c.Code, c.Name, c.OrganizationName, c.Edrpou, c.ContractNumber, c.ContractValidTo, priceListName = c.PriceList?.Name, c.ContactPerson, c.Phone, c.Email, c.IsActive,
                orders = s?.Orders ?? 0, openOrders = s?.Open ?? 0, amount = s?.Amount ?? 0, invoiced = inv, notInvoiced = (s?.Amount ?? 0) - inv
            };
        }).ToList();
    }

    // ------------------------------------------------------------------ реєстр клініки
    /// <summary>
    /// Імпорт реєстру замовлень клініки (CSV «;» / XLSX): lastName, firstName, middleName, birthDate (дд.мм.рррр або рррр-мм-дд), gender (M/F),
    /// phone, clinicNumber, doctor, tests (коди через «,» або «|»; у XLSX також «;»), cito (так/1). dryRun=true — перевірка без створення.
    /// </summary>
    public async Task<object> ImportAsync(string clientId, Stream stream, string fileName, bool dryRun)
    {
        _policy.Require("Імпорт реєстру клініки", LabRoles.Admin, LabRoles.Registrar, LabRoles.Technician);
        var client = await ClientAsync(clientId);
        var rows = Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? ParseXlsx(stream) : ParseCsv(stream);
        if (rows.Count == 0) throw new ValidationException("Реєстр порожній");
        var profiles = await _db.Profiles.AsNoTracking().Where(p => p.IsActive).Select(p => p.Code).ToListAsync();
        var tests = await _db.Tests.AsNoTracking().Where(t => t.IsActive).Select(t => t.Code).ToListAsync();
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.LastName) || string.IsNullOrWhiteSpace(r.FirstName) || r.BirthDate == null) { r.Status = "ERROR"; r.Message = "Прізвище, ім'я та дата народження обов'язкові"; continue; }
            var unknown = r.Codes.Where(c => !profiles.Contains(c) && !tests.Contains(c)).ToList();
            if (r.Codes.Count == 0 || unknown.Count > 0) { r.Status = "ERROR"; r.Message = r.Codes.Count == 0 ? "Не вказано дослідження" : $"Невідомі коди: {string.Join(", ", unknown)}"; continue; }
            var bd = r.BirthDate.Value.Date;
            var existing = await _db.Patients.AsNoTracking().Where(p => p.Person!.LastName == r.LastName && p.Person.Name == r.FirstName && p.Birthday != null && p.Birthday.Value.Date == bd).Select(p => p.Id).FirstOrDefaultAsync();
            r.PatientId = existing; r.NewPatient = existing == null;
            if (dryRun) continue;
            try
            {
                var req = new CreateOrderRequest
                {
                    PatientId = existing,
                    NewPatient = existing == null ? new NewPatientRequest { LastName = r.LastName, FirstName = r.FirstName, SecondName = r.MiddleName, BirthDate = DateTime.SpecifyKind(bd, DateTimeKind.Utc), Gender = r.Gender, Phone = r.Phone } : null,
                    ReferralType = ReferralTypes.ExternalClinic, ReferrerOrganizationName = client.OrganizationName ?? client.Name, ReferrerOrganizationEdrpou = client.Edrpou,
                    ReferrerDoctorName = r.DoctorName, ReferrerNumber = r.ClinicNumber, PayerId = client.Id, IsUrgentCito = r.IsCito,
                    ProfileIds = r.Codes.Where(c => profiles.Contains(c)).ToList(), TestIds = r.Codes.Where(c => !profiles.Contains(c)).ToList()
                };
                var order = await _orders.BuildOrderAsync(req);
                await _db.SaveChangesAsync();
                r.Status = "CREATED"; r.OrderId = order.Id; r.OrderNumber = order.OrderNumber; r.PatientId = order.PatientId;
                r.PayerAmount = order.PayerAmount; r.PatientAmount = order.PatientAmount;
            }
            catch (Exception ex) when (ex is ValidationException or ConflictException or NotFoundException)
            {
                r.Status = "ERROR"; r.Message = ex.Message;
                foreach (var e in _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList()) e.State = EntityState.Detached;
            }
        }
        if (!dryRun) _audit.Log("CLIENT_REGISTRY_IMPORT", "lab_payer", client.Id, null, new { fileName, total = rows.Count, created = rows.Count(r => r.Status == "CREATED") });
        if (!dryRun) await _db.SaveChangesAsync();
        return new { dryRun, client = client.Name, total = rows.Count, ok = rows.Count(r => r.Status is "READY" or "CREATED"), errors = rows.Count(r => r.Status == "ERROR"), rows };
    }

    private static List<ClientImportRow> ParseCsv(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 2) return new();
        var sep = lines[0].Contains(';') ? ';' : ',';
        var header = lines[0].Split(sep).Select(h => h.Trim().Trim('"').ToLowerInvariant()).ToArray();
        var rows = new List<ClientImportRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var f = SplitCsv(lines[i], sep);
            string Get(string[] n) { var idx = Array.FindIndex(header, h => n.Contains(h)); return idx >= 0 && idx < f.Length ? f[idx].Trim() : ""; }
            rows.Add(Row(i + 1, Get, sep));
        }
        return rows;
    }

    private static string[] SplitCsv(string line, char sep)
    {
        var res = new List<string>(); var sb = new System.Text.StringBuilder(); var q = false;
        foreach (var ch in line)
        {
            if (ch == '"') { q = !q; continue; }
            if (ch == sep && !q) { res.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(ch);
        }
        res.Add(sb.ToString());
        return res.ToArray();
    }

    private static List<ClientImportRow> ParseXlsx(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var header = ws.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim().ToLowerInvariant(), c => c.Address.ColumnNumber);
        var rows = new List<ClientImportRow>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            string Get(string[] n) { foreach (var x in n) if (header.TryGetValue(x, out var c)) return row.Cell(c).GetFormattedString().Trim(); return ""; }
            rows.Add(Row(row.RowNumber(), Get, ';'));
        }
        return rows;
    }

    private static ClientImportRow Row(int line, Func<string[], string> getter, char sep)
    {
        string G(params string[] n) => getter(n);
        var bdText = G("birthdate", "дата народження", "birthday");
        DateTime? bd = DateTime.TryParseExact(bdText, new[] { "dd.MM.yyyy", "yyyy-MM-dd", "d.M.yyyy", "dd/MM/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        var gender = G("gender", "стать").ToUpperInvariant();
        return new ClientImportRow
        {
            Line = line, LastName = G("lastname", "прізвище"), FirstName = G("firstname", "ім'я", "імя", "ім’я"), MiddleName = NullIf(G("middlename", "по батькові")), BirthDate = bd,
            Gender = gender is "M" or "Ч" ? "M" : gender is "F" or "Ж" ? "F" : "U", Phone = NullIf(G("phone", "телефон")), ClinicNumber = NullIf(G("clinicnumber", "номер у клініки", "номер клініки", "номер", "№")),
            DoctorName = NullIf(G("doctor", "лікар")), IsCito = G("cito", "терміново") is "1" or "так" or "true" or "yes",
            Codes = G("tests", "дослідження", "коди", "коди досліджень", "показники").Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(c => c.ToUpperInvariant()).Distinct().ToList()
        };
    }

    private static string? NullIf(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    // ------------------------------------------------------------------ замовлення клієнта (портал клієнта)
    public async Task<object> OrdersAsync(string clientId, DateTime? from, DateTime? to)
    {
        var client = await ClientAsync(clientId);
        var q = _db.Orders.AsNoTracking().Include(o => o.Patient).Include(o => o.Tests).Where(o => o.PayerId == client.Id && o.RecordState != RecordStates.Deleted);
        if (from.HasValue) q = q.Where(o => o.OrderDatetime >= from.Value);
        if (to.HasValue)
        {
            // «По» без часу — включно до кінця доби
            var toExclusive = to.Value.TimeOfDay == TimeSpan.Zero ? to.Value.AddDays(1) : to.Value;
            q = q.Where(o => o.OrderDatetime < toExclusive);
        }
        var list = await q.OrderByDescending(o => o.OrderDatetime).Take(1000).ToListAsync();
        return new
        {
            client = new { client.Id, client.Name },
            items = list.Select(o => new
            {
                o.Id, o.OrderNumber, o.OrderDatetime, o.Status, patientName = o.Patient?.Caption, clinicNumber = o.ReferrerNumber, doctor = o.ReferrerDoctorName,
                testsTotal = o.Tests.Count, testsReady = o.Tests.Count(t => OrderTestStatuses.VerifiedAny.Contains(t.Status)), o.PayerAmount, o.PatientAmount,
                reportUrl = o.Status is OrderStatuses.Released or OrderStatuses.Completed ? $"/api/v1/lab/orders/{o.Id}/report.pdf" : null
            })
        };
    }

    // ------------------------------------------------------------------ акт / рахунок за період
    public async Task<object> PeriodInvoiceAsync(string payerId, PeriodInvoiceRequest req)
    {
        _policy.Require("Рахунок/акт за період", LabRoles.Admin, LabRoles.Registrar);
        var payer = await _db.Payers.FirstOrDefaultAsync(p => (p.Id == payerId || p.Code == payerId) && p.Kind != PayerKinds.Patient) ?? throw NotFoundException.For("Платник", payerId);
        var from = DateTime.SpecifyKind(req.From.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(req.To.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        if (to < from) throw ValidationException.Field("to", "Кінець періоду раніше за початок");
        var invoicedIds = (await _db.Invoices.AsNoTracking().Where(i => i.PayerId == payer.Id && i.Status != "CANCELLED").Select(i => i.ChargeIdsJson).ToListAsync())
            .SelectMany(j => System.Text.Json.JsonSerializer.Deserialize<List<string>>(j) ?? new()).ToHashSet();
        var charges = await _db.OrderCharges.AsNoTracking().Include(c => c.Order)
            .Where(c => c.PayerId == payer.Id && c.Order!.OrderDatetime >= from && c.Order.OrderDatetime <= to && c.Order.Status != OrderStatuses.Cancelled && c.PayerAmount > 0)
            .ToListAsync();
        charges = charges.Where(c => !invoicedIds.Contains(c.Id)).ToList();
        if (charges.Count == 0) throw new ConflictException("За період немає невиставлених нарахувань цього платника");
        var number = $"ACT-{DateTime.UtcNow:yyMM}-{await _numerators.NextCounterAsync("lab_act:" + DateTime.UtcNow.ToString("yyMM")):00000}";
        var inv = new LabInvoice
        {
            Number = number, PayerId = payer.Id, PeriodFrom = from, PeriodTo = to, Amount = charges.Sum(c => c.PayerAmount),
            ChargeIdsJson = System.Text.Json.JsonSerializer.Serialize(charges.Select(c => c.Id))
        };
        _db.Invoices.Add(inv);
        _audit.Log("PERIOD_INVOICE", "lab_invoice", inv.Id, null, new { inv.Number, payer = payer.Code, inv.Amount, orders = charges.Select(c => c.OrderId).Distinct().Count() });
        await _db.SaveChangesAsync();
        return new { inv.Id, inv.Number, inv.Amount, inv.PeriodFrom, inv.PeriodTo, orders = charges.Select(c => c.OrderId).Distinct().Count(), lines = charges.Count, printUrl = $"/api/v1/lab/invoices/{inv.Id}/print" };
    }

    public async Task<List<object>> InvoicesAsync(string payerId) =>
        (await _db.Invoices.AsNoTracking().Where(i => i.PayerId == payerId || i.Payer!.Code == payerId).OrderByDescending(i => i.IssuedAt).ToListAsync())
        .Select(i => (object)new { i.Id, i.Number, i.Amount, i.Status, i.IssuedAt, i.PeriodFrom, i.PeriodTo, i.OrderId, printUrl = $"/api/v1/lab/invoices/{i.Id}/print" }).ToList();
}
