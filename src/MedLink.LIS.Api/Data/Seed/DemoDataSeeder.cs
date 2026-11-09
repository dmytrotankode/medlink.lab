// =============================================================================
// Демо-дані робочого процесу (виконується лише якщо lab_order порожня та Lab:SeedDemoData=true).
// Використовує ті самі сервіси, що й API (конвеєр результатів, машина станів) від імені адміністратора.
// =============================================================================
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Data.Seed;

public sealed class DemoDataSeeder
{
    private readonly LisDbContext _db;
    private readonly OrderService _orders;
    private readonly SampleService _samples;
    private readonly ResultPipelineService _pipeline;
    private readonly QcService _qc;
    private readonly LogisticsService _logistics;
    private readonly BiobankService _biobank;
    private readonly ReagentService _reagents;
    private readonly MicrobiologyService _micro;
    private readonly WorklistService _worklist;
    private readonly MisReferralService _mis;
    private readonly ICurrentEmployee _current;
    private readonly ILogger<DemoDataSeeder> _logger;

    public const string Admin = "0e000000-0000-0000-0000-000000000001", Doctor = "0e000000-0000-0000-0000-000000000002", Technician = "0e000000-0000-0000-0000-000000000003",
        Nurse = "0e000000-0000-0000-0000-000000000004", Courier = "0e000000-0000-0000-0000-000000000005", Registrar = "0e000000-0000-0000-0000-000000000006";
    public const string Dept = "0d000000-0000-0000-0000-000000000001", CollectionPoint = "0d000000-0000-0000-0000-000000000002";
    public const string Sysmex = "0a2a0000-0000-0000-0000-000000000001", Mindray = "0a2a0000-0000-0000-0000-000000000002";
    private static string Pat(int n) => $"0b000000-0000-0000-0000-00000000000{n}";

    public DemoDataSeeder(LisDbContext db, OrderService orders, SampleService samples, ResultPipelineService pipeline, QcService qc, LogisticsService logistics,
        BiobankService biobank, ReagentService reagents, MicrobiologyService micro, WorklistService worklist, MisReferralService mis, ICurrentEmployee current, ILogger<DemoDataSeeder> logger)
    {
        _db = db; _orders = orders; _samples = samples; _pipeline = pipeline; _qc = qc; _logistics = logistics; _biobank = biobank; _reagents = reagents; _micro = micro; _worklist = worklist; _mis = mis; _current = current; _logger = logger;
    }

    public async Task SeedAsync()
    {
        var hasOrders = await _db.Orders.AnyAsync();
        var admin = await _db.Employees.AsNoTracking().FirstAsync(e => e.Id == Admin);
        _current.Set(admin, Admin, "seed");
        _db.CurrentUserId = Admin;
        if (hasOrders)
        {
            // Наявна БД: дооновлення демо-сценарію розд. 23 (направлення МІС), якщо його ще немає
            await SeedMisDemoAsync();
            await SeedSendOutDemoAsync();
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. CITO біохімія + електроліти: критичний калій, верифіковано, видано
        var o1 = await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(1), DoctorId = Doctor, DepartmentId = Dept, IsUrgentCito = true, ClinicalNotes = "Хронічна хвороба нирок, контроль електролітів", Icd10Code = "N18.3", ProfileIds = { "PROF_BIOCHEM_BASE" }, TestIds = { "K", "NA" } });
        await CollectReceive(o1);
        await Enter(o1, new() { ["GLU"] = 5.4, ["CREAT"] = 168, ["UREA"] = 12.1, ["ALT"] = 28, ["AST"] = 24, ["BIL_TOT"] = 11.2, ["PROT_TOT"] = 71, ["K"] = 6.8, ["NA"] = 134 }, Mindray);
        await VerifyAll(o1, "Критична гіперкаліємія 6.8 ммоль/л — повідомлено лікуючого лікаря телефоном");
        var kResult = (await _orders.LoadAsync(o1.Id)).Tests.First(t => t.TestCode == "K").Result!;
        await _worklist.CreatePanicCallAsync(new PanicCallRequest { ResultId = kResult.Id, DoctorName = "Іванчук М.П. (нефролог)", Phone = "+380501234567", Department = "Нефрологічне відділення", ReadbackConfirmed = true, Comments = "Повідомлено про К 6.8 ммоль/л, призначено ЕКГ та корекцію" });
        o1 = await _orders.ReleaseAsync(o1.Id);
        await Backdate(o1.Id, hoursAgo: 30);

        // 2. Біохімія 5 днів тому (база для delta-check), видано
        var o2 = await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(2), DoctorId = Doctor, DepartmentId = CollectionPoint, ClinicalNotes = "Плановий профілактичний огляд", ProfileIds = { "PROF_BIOCHEM_BASE" } });
        await CollectReceive(o2);
        await Enter(o2, new() { ["GLU"] = 5.1, ["CREAT"] = 74, ["UREA"] = 4.9, ["ALT"] = 22, ["AST"] = 20, ["BIL_TOT"] = 9.8, ["PROT_TOT"] = 73 }, Mindray);
        await VerifyAll(o2, null);
        await _orders.ReleaseAsync(o2.Id);
        await Backdate(o2.Id, hoursAgo: 24 * 5);

        // 3. Повторна біохімія сьогодні → delta-check GLU (+55% > 20%) → NEEDS_REVIEW
        var o3 = await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(2), DoctorId = Doctor, DepartmentId = CollectionPoint, ClinicalNotes = "Контроль глюкози через 5 днів", ProfileIds = { "PROF_BIOCHEM_BASE" }, EhealthReferralId = "0c0f0000-0000-0000-0000-000000000002" });
        await CollectReceive(o3);
        await Enter(o3, new() { ["GLU"] = 7.9, ["CREAT"] = 76, ["UREA"] = 5.2, ["ALT"] = 24, ["AST"] = 21 }, Mindray);
        await Backdate(o3.Id, hoursAgo: 3);

        // 4. Коагулограма + PSA → reflex FPSA
        var o4 = await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(3), DoctorId = Doctor, DepartmentId = Dept, ClinicalNotes = "Контроль антикоагулянтної терапії, скринінг ПЗ", ProfileIds = { "PROF_COAG" }, TestIds = { "PSA" } });
        await CollectReceive(o4);
        await Enter(o4, new() { ["PT"] = 13.1, ["INR"] = 2.6, ["APTT"] = 31.0, ["FIBRINOGEN"] = 3.1, ["PSA"] = 5.2 }, null);
        await Backdate(o4.Id, hoursAgo: 5);

        // 5. Вагітна, 24 тиждень: каскад PREGNANCY для GLU і TSH
        var o5 = await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(4), DoctorId = Doctor, DepartmentId = CollectionPoint, ClinicalNotes = "Вагітність 24 тиж., скринінг", IsPregnant = true, PregnancyWeek = 24, ProfileIds = { "PROF_THYROID" }, TestIds = { "GLU", "HGB" } });
        await CollectReceive(o5);
        await Enter(o5, new() { ["GLU"] = 5.2, ["TSH"] = 3.5 }, null);
        await Backdate(o5.Id, hoursAgo: 2);

        // 6. Дитина 2 роки, ЗАК — зібрано у пункті забору, у дорозі (маніфест)
        var o6 = await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(5), DoctorId = Doctor, DepartmentId = CollectionPoint, ClinicalNotes = "Субфебрилітет 3 дні", ProfileIds = { "PROF_CBC" } });
        foreach (var s in o6.Samples) await _samples.CollectAsync(s.Barcode, new CollectSampleRequest { Checklist = new CollectChecklist(), VolumeMl = 1.5 });
        await _logistics.CreateAsync(new ManifestRequest { OriginDepartmentId = CollectionPoint, DestinationDepartmentId = Dept, CourierName = "Ткаченко А.М.", CourierPhone = "+380671000005", TemperatureDispatch = 4.5, Barcodes = o6.Samples.Select(s => s.Barcode).ToList() });

        // 7. Літній пацієнт: ЗАС + посів сечі (мікробіологія — E. coli ESBL)
        var o7 = await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(6), DoctorId = Doctor, DepartmentId = Dept, ClinicalNotes = "Дизурія, підозра на ІСШ", ProfileIds = { "PROF_URINE" }, TestIds = { "URINE_CULTURE" } });
        await CollectReceive(o7);
        await EnterText(o7, new() { ["U_COLOR"] = "жовтий", ["U_CLARITY"] = "мутна", ["U_PRO"] = "сліди", ["U_GLU"] = "не виявлено", ["U_KET"] = "не виявлено", ["U_LEU"] = "+++", ["U_ERY"] = "+", ["U_NIT"] = "позитивно" });
        await Enter(o7, new() { ["U_SG"] = 1.018, ["U_PH"] = 6.0 }, null);
        var cultureTest = (await _orders.LoadAsync(o7.Id)).Tests.First(t => t.TestCode == "URINE_CULTURE");
        var culture = await _micro.CreateAsync(new CultureRequest { OrderTestId = cultureTest.Id, SpecimenLocus = "Сеча (середня порція)", CultureMedium = "CLED / MacConkey", IncubationHoursRecommended = 24, IncubationStart = DateTime.UtcNow.AddHours(-26) });
        var cultureId = (string)culture.GetType().GetProperty("Id")!.GetValue(culture)!;
        await _micro.UpdateAsync(cultureId, new CultureRequest { GrowthDetected = true, GrowthIntensity = "HEAVY_3+", CfuPerMl = "10^6 КУО/мл", PreliminaryReport = "Ріст грамнегативних паличок, лактозопозитивні колонії", FinalMicroscopyDescription = "Лейкоцити 30–40 у п/з, бактерії +++" });
        var ecol = await _db.Organisms.FirstAsync(o => o.Code == "ECOL");
        var withIsolate = await _micro.AddIsolateAsync(cultureId, new IsolateRequest { OrganismId = ecol.Id, QuantitativeCount = "10^6 КУО/мл", ColonyMorphology = "Рожеві лактозопозитивні колонії на MacConkey" });
        var isolateId = await _db.Isolates.Where(i => i.CultureOrderId == cultureId).Select(i => i.Id).FirstAsync();
        var ab = await _db.Antibiotics.ToDictionaryAsync(a => a.Code, a => a.Id);
        await _micro.AddSusceptibilityAsync(isolateId, new SusceptibilityRequest { AntibioticId = ab["CIP"], ZoneMm = 18 });
        await _micro.AddSusceptibilityAsync(isolateId, new SusceptibilityRequest { AntibioticId = ab["CTX"], ZoneMm = 14 });
        await _micro.AddSusceptibilityAsync(isolateId, new SusceptibilityRequest { AntibioticId = ab["CAZ"], ZoneMm = 16 });
        await _micro.AddSusceptibilityAsync(isolateId, new SusceptibilityRequest { AntibioticId = ab["MEM"], ZoneMm = 27 });
        await _micro.AddSusceptibilityAsync(isolateId, new SusceptibilityRequest { AntibioticId = ab["NIT"], ZoneMm = 20 });
        await _micro.AddSusceptibilityAsync(isolateId, new SusceptibilityRequest { AntibioticId = ab["GEN"], ZoneMm = 19 });
        await _micro.TransitionAsync(cultureId, CultureActions.Isolate, null);
        await Backdate(o7.Id, hoursAgo: 28);

        // 8. Ліпідограма — щойно зареєстровано
        await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(1), DoctorId = Doctor, DepartmentId = Dept, ClinicalNotes = "Дисліпідемія, контроль статинотерапії", ProfileIds = { "PROF_LIPID" } });

        // QC: Sysmex L2 WBC — 10 нормальних точок + 1_3s (lockout); Mindray L1 GLU — 8 точок
        var xn2 = await _db.QcMaterials.FirstAsync(m => m.Id == "09c00000-0000-0000-0000-000000000002");
        var ccm1 = await _db.QcMaterials.FirstAsync(m => m.Id == "09c00000-0000-0000-0000-000000000003");
        var wbcSeries = new[] { 7.18, 7.22, 7.15, 7.28, 7.20, 7.32, 7.19, 7.25, 7.10, 7.27 };
        for (var i = 0; i < wbcSeries.Length; i++)
            await _qc.AddResultAsync(new QcResultRequest { QcMaterialId = xn2.Id, TestCode = "WBC", MeasuredValue = wbcSeries[i], RunAt = DateTime.UtcNow.AddDays(-(wbcSeries.Length - i)).Date.AddHours(7).AddMinutes(45) });
        await _qc.AddResultAsync(new QcResultRequest { QcMaterialId = xn2.Id, TestCode = "WBC", MeasuredValue = 8.3, RunAt = DateTime.UtcNow.AddMinutes(-40) }); // z=+3.67 → 1_3s → lockout
        var hgbSeries = new[] { 137.0, 139.0, 138.5, 136.8, 138.0, 139.5, 137.2, 138.8 };
        for (var i = 0; i < hgbSeries.Length; i++)
            await _qc.AddResultAsync(new QcResultRequest { QcMaterialId = xn2.Id, TestCode = "HGB", MeasuredValue = hgbSeries[i], RunAt = DateTime.UtcNow.AddDays(-(hgbSeries.Length - i)).Date.AddHours(7).AddMinutes(50) });
        var gluSeries = new[] { 4.21, 4.18, 4.25, 4.30, 4.15, 4.22, 4.19, 4.24 };
        for (var i = 0; i < gluSeries.Length; i++)
            await _qc.AddResultAsync(new QcResultRequest { QcMaterialId = ccm1.Id, TestCode = "GLU", MeasuredValue = gluSeries[i], RunAt = DateTime.UtcNow.AddDays(-(gluSeries.Length - i)).Date.AddHours(8) });

        // Біобанк: штатив 8×12 (-20°C), розміщено сироватки виданих замовлень
        var rack = await _biobank.CreateRackAsync(new RackRequest { Code = "RACK-A1", Name = "Штатив A1 (сироватки, 6 міс.)", RoomNumber = "104", FreezerName = "Морозильник Liebherr LGex-3410 №1", ShelfNumber = "2", TemperatureCelsius = -20, RowsCount = 8, ColsCount = 12 });
        var rackId = (string)rack.GetType().GetProperty("Id")!.GetValue(rack)!;
        var serumBarcodes = (await _orders.LoadAsync(o1.Id)).Samples.Select(s => s.Barcode).Concat((await _orders.LoadAsync(o2.Id)).Samples.Select(s => s.Barcode)).ToList();
        var coords = new[] { ("A", 1), ("A", 2), ("B", 5), ("C", 7) };
        for (var i = 0; i < serumBarcodes.Count && i < coords.Length; i++)
            await _biobank.PlaceAsync(new PlaceCellRequest { RackId = rackId, Row = coords[i].Item1, Col = coords[i].Item2, Barcode = serumBarcodes[i], ExpiryAt = DateTime.UtcNow.AddDays(180) });
        await _biobank.CreateRackAsync(new RackRequest { Code = "RACK-B1", Name = "Штатив B1 (кріоархів, -80°C)", RoomNumber = "104", FreezerName = "Ультранизькотемпературний морозильник Haier DW-86L", ShelfNumber = "1", TemperatureCelsius = -80, RowsCount = 8, ColsCount = 12 });

        // Реагенти
        await _reagents.CreateAsync(new ReagentLotRequest { AnalyzerId = Mindray, TestCode = "GLU", ReagentName = "Glucose HK Gen.3", LotNumber = "GLU-77812", Manufacturer = "Mindray", TestsInitial = 800, TestsRemaining = 612, MinimumTests = 100, ExpiryDate = DateTime.UtcNow.AddMonths(8), OpenedAt = DateTime.UtcNow.AddDays(-12), OnboardStabilityDays = 60 });
        await _reagents.CreateAsync(new ReagentLotRequest { AnalyzerId = Mindray, TestCode = "CREAT", ReagentName = "Creatinine (Jaffe compensated)", LotNumber = "CRE-55120", Manufacturer = "Mindray", TestsInitial = 400, TestsRemaining = 38, MinimumTests = 50, ExpiryDate = DateTime.UtcNow.AddMonths(5), OpenedAt = DateTime.UtcNow.AddDays(-40), OnboardStabilityDays = 45 });
        await _reagents.CreateAsync(new ReagentLotRequest { AnalyzerId = Sysmex, TestCode = null, ReagentName = "CELLPACK DCL", LotNumber = "CP-24091A", Manufacturer = "Sysmex", TestsInitial = 2000, TestsRemaining = 1420, MinimumTests = 300, ExpiryDate = DateTime.UtcNow.AddDays(10), OpenedAt = DateTime.UtcNow.AddDays(-20) });
        await _reagents.CreateAsync(new ReagentLotRequest { AnalyzerId = Sysmex, TestCode = null, ReagentName = "SULFOLYSER", LotNumber = "SL-24110", Manufacturer = "Sysmex", TestsInitial = 1500, TestsRemaining = 1500, MinimumTests = 200, ExpiryDate = DateTime.UtcNow.AddMonths(14) });

        await SeedMisDemoAsync();
        await SeedSendOutDemoAsync();
        _logger.LogInformation("Демо-дані створено за {Ms} мс", sw.ElapsedMilliseconds);
    }

    /// <summary>Демо відправки у зовнішню лабораторію: замовлення з показниками, що за прайсом виконавця йдуть у зовнішню лабораторію; проба в лабораторії → черга «До відправки».</summary>
    private async Task SeedSendOutDemoAsync()
    {
        if (await _db.OrderTests.AnyAsync(t => t.PerformerId != null)) return;
        var o = await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(3), DoctorId = Doctor, DepartmentId = Dept, ClinicalNotes = "Гормональний скринінг, дослідження виконує зовнішня лабораторія", TestIds = { "VITD", "AMH" } });
        await CollectReceive(o);
    }

    /// <summary>Демо розд. 21–23: вхідні направлення МІС (пакети, е-направлення, «потрібен вибір»), робота за направленнями, кумулятивний звіт.</summary>
    private async Task SeedMisDemoAsync()
    {
        if (await _db.Orders.AnyAsync(o => o.MisReferralId != null) || !await _db.Interventions.AnyAsync()) return;
        // Е-направлення ЕСОЗ для амбулаторного контуру
        async Task<string> EReferral(string number, string patientId, string caption)
        {
            _db.Referrals.Add(new EheIncomingMedicalReferral
            {
                RegNumber = number, Caption = caption, OrganizationId = MedLinkDefaults.OrganizationId, PatientCardId = patientId,
                ServiceCatalogServiceId = await _db.ServiceCatalog.Where(x => x.Code == "LAB-CBC").Select(x => x.Id).FirstOrDefaultAsync() ?? await _db.ServiceCatalog.Select(x => x.Id).FirstAsync(),
                MedicalReferralCategoryId = MedLinkDefaults.ReferralCategoryLaboratoryId, StatusId = MedLinkEnums.ReferralStatusActive,
                ProcessingStatusInEhealthId = MedLinkEnums.ProcessingNew, PriorityId = MedLinkEnums.EmptyGuid, ExpirationDate = DateTime.UtcNow.AddDays(30)
            });
            await _db.SaveChangesAsync();
            return number;
        }
        static MisReferralItemRequest It(string code, params string[] ind) => new() { InterventionCode = code, Indicators = ind.ToList() };

        // Кумулятивний звіт (S-59): ЗАК пацієнта за тиждень з BC-780 і BC-20S, гемоглобін 62 г/л — критичне
        const string Bc780 = "0a2a0000-0000-0000-0000-000000000003", Bc20s = "0a2a0000-0000-0000-0000-000000000004", Ms380 = "0a2a0000-0000-0000-0000-000000000005";
        foreach (var (days, analyzer, hgb, rbc, wbc) in new[] { (6.0, Bc780, 94.0, 3.38, 16.8), (4.0, Bc20s, 62.0, 2.11, 12.1), (2.0, Bc780, 99.0, 3.26, 25.0) })
        {
            var o = await _orders.CreateAsync(new CreateOrderRequest { PatientId = Pat(1), DoctorId = Doctor, DepartmentId = Dept, ClinicalNotes = "Післяопераційний контроль ЗАК", ProfileIds = { "PROF_CBC" } });
            await CollectReceive(o);
            await Enter(o, new()
            {
                ["WBC"] = wbc, ["RBC"] = rbc, ["HGB"] = hgb, ["HCT"] = Math.Round(hgb * 0.3, 1), ["PLT"] = 240,
                // MCV/MCH/MCHC — розрахункові показники, рахуються з RBC/HGB/HCT автоматично
                ["NEUT_PCT"] = 82, ["LYMPH_PCT"] = 11, ["ESR"] = 38
            }, analyzer);
            await VerifyAll(o, "Критичний гемоглобін — повідомлено лікаря ВРІТ телефоном");
            foreach (var crit in (await _orders.LoadAsync(o.Id)).Tests.Where(t => t.Result != null && Core.Clinical.ResultFlags.IsCritical(t.Result.Flag)))
                await _worklist.CreatePanicCallAsync(new PanicCallRequest { ResultId = crit.Result!.Id, DoctorName = "Шевченко О. А. (анестезіолог)", Phone = "+380501112233", Department = "ВРІТ", ReadbackConfirmed = true, Comments = "Критичне значення передано телефоном" });
            await _orders.ReleaseAsync(o.Id);
            await Backdate(o.Id, hoursAgo: 24 * days);
        }

        // Пакет ВРІТ CITO: пропозиція розподілу застосовується одразу (FR-DST-009)
        await _mis.CreateAsync(new MisReferralRequest
        {
            MisReferralId = "0c1a0000-0000-0000-0000-000000000107", ReferralNumber = "НП-2611-0107", Context = ReferralContexts.Inpatient, PatientId = Pat(1), DoctorId = Doctor, HospitalizationId = "0c1b0000-0000-0000-0000-000000000412",
            Department = "ВРІТ", Ward = "1", Bed = "3", DoctorName = "Шевченко О. А. (анестезіолог)", Priority = "CITO",
            Package = new MisPackageRef { Code = "PKG-POSTOP-ICU", Name = "Післяопераційний ВРІТ (CITO)", Cito = true },
            Items = { It("DEMO-LAB-CBC", "WBC", "RBC", "HGB", "HCT", "PLT"), It("DEMO-LAB-ABG", "PH", "PCO2", "PO2", "HCO3", "BE", "SO2", "LACTATE"), It("DEMO-LAB-ELEC", "K", "NA", "ICA"),
                      It("DEMO-LAB-COAG", "PT", "INR", "APTT"), It("DEMO-LAB-PCT"), It("DEMO-LAB-BIO", "GLU", "CREAT", "UREA") }
        });
        // Післяопераційний пакет, планове взяття — чекає підтвердження розподілу у «Вхідних направленнях»
        await _mis.CreateAsync(new MisReferralRequest
        {
            MisReferralId = "0c1a0000-0000-0000-0000-000000000108", ReferralNumber = "НП-2611-0108", Context = ReferralContexts.Inpatient, PatientId = Pat(2), DoctorId = Doctor, HospitalizationId = "0c1b0000-0000-0000-0000-000000000415",
            Department = "Онкохірургія (мамологія)", Ward = "6", Bed = "1", DoctorName = "Ткаченко В. Ю.", PlannedCollectionAt = DateTime.UtcNow.Date.AddDays(1).AddHours(4).AddMinutes(30),
            Package = new MisPackageRef { Code = "PKG-POSTOP-1", Name = "Післяопераційний (стандартний)" },
            Items = { It("DEMO-LAB-CBC", "WBC", "NEUT_ABS", "LYMPH_ABS", "RBC", "HGB", "HCT", "PLT"), It("DEMO-LAB-BIO", "PROT_TOT", "ALB", "UREA", "CREAT", "GLU", "BIL_TOT", "CRP"),
                      It("DEMO-LAB-COAG", "PT", "INR", "APTT", "FIBRINOGEN"), It("DEMO-LAB-ELEC", "K", "NA", "CL") }
        });
        // Е-направлення: частковий вибір показників і онкомаркер без обладнання
        await _mis.CreateAsync(new MisReferralRequest
        {
            MisReferralId = "0c1a0000-0000-0000-0000-000000000202", ReferralNumber = "0002-2026-K4P9", Context = ReferralContexts.Ambulatory, Source = ReferralSources.Ehealth, PatientId = Pat(3),
            EhealthReferralNumber = await EReferral("0002-2026-K4P9", Pat(3), "Лабораторні дослідження (онкогінеколог)"), Department = "Поліклініка · онкогінеколог", DoctorName = "Іваненко Л. П.",
            Items = { It("DEMO-LAB-CBC", "WBC", "RBC", "HGB", "HCT", "PLT", "ESR"), It("DEMO-LAB-BIO", "GLU", "CREAT", "ALT", "AST"), It("DEMO-LAB-TM", "CA125") }
        });
        // Внутрішнє направлення поліклініки
        await _mis.CreateAsync(new MisReferralRequest
        {
            MisReferralId = "0c1a0000-0000-0000-0000-000000000111", ReferralNumber = "НП-2611-0111", Context = ReferralContexts.Ambulatory, Source = ReferralSources.Polyclinic, PatientId = Pat(5), DoctorId = Doctor,
            Department = "Поліклініка · онкоуролог", DoctorName = "Кушнаренко М. В.", Items = { It("DEMO-LAB-URINE"), It("DEMO-LAB-BIO", "CREAT", "UREA") }
        });
        // Е-направлення іншого закладу без переданих показників — «Потрібен вибір показників» (FR-MIS-022)
        await _mis.CreateAsync(new MisReferralRequest
        {
            MisReferralId = "0c1a0000-0000-0000-0000-000000000203", ReferralNumber = "0003-2026-D2X7", Context = ReferralContexts.Ambulatory, Source = ReferralSources.Ehealth, PatientId = Pat(4),
            EhealthReferralNumber = await EReferral("0003-2026-D2X7", Pat(4), "Лабораторні дослідження (АЗПСМ)"), Department = "АЗПСМ (інший заклад)", DoctorName = "Мартинюк С. Р.",
            Items = { It("DEMO-LAB-CBC"), It("DEMO-LAB-COAG") }
        });
        // Стаціонар: розподіл підтверджено, проби прийнято, частина результатів — для розрізів «аналізатор / пацієнт / дослідження»
        await _mis.CreateAsync(new MisReferralRequest
        {
            MisReferralId = "0c1a0000-0000-0000-0000-000000000099", ReferralNumber = "НП-2611-0099", Context = ReferralContexts.Inpatient, PatientId = Pat(6), DoctorId = Doctor, Department = "Онкохірургія (колопроктологія)",
            Ward = "2", Bed = "4", DoctorName = "Осокін А. Е.", Items = { It("DEMO-LAB-BIO", "PROT_TOT", "ALB", "CREAT", "UREA", "GLU", "CRP"), It("DEMO-LAB-COAG", "PT", "INR", "APTT", "FIBRINOGEN") }
        });
        var confirmedId = await _db.Orders.Where(o => o.MisReferralId == "0c1a0000-0000-0000-0000-000000000099").Select(o => o.Id).FirstAsync();
        await _mis.ConfirmAsync(confirmedId, new DistributionConfirmRequest());
        var confirmed = await _orders.GetAsync(confirmedId);
        await CollectReceive(confirmed);
        await Enter(confirmed, new() { ["PROT_TOT"] = 64, ["ALB"] = 36, ["CREAT"] = 88 }, Ms380);
        _logger.LogInformation("Демо розд. 21–23: направлення МІС створено");
    }

    private async Task CollectReceive(OrderDto o)
    {
        foreach (var s in o.Samples)
        {
            await _samples.CollectAsync(s.Barcode, new CollectSampleRequest { Checklist = new CollectChecklist(), VolumeMl = 3.0 });
            await _samples.ReceiveAsync(s.Barcode, new ReceiveSampleRequest());
        }
    }

    private async Task Enter(OrderDto o, Dictionary<string, double> values, string? analyzerId)
    {
        var order = await _orders.LoadAsync(o.Id);
        foreach (var (code, value) in values)
        {
            var test = order.Tests.FirstOrDefault(t => t.TestCode == code);
            if (test == null) continue;
            await _pipeline.ApplyAsync(new ResultEntry { OrderTestId = test.Id, NumericValue = value, AnalyzerId = analyzerId, AnalyzerFlags = analyzerId == null ? null : "N", IsSystem = analyzerId != null });
        }
    }

    private async Task EnterText(OrderDto o, Dictionary<string, string> values)
    {
        var order = await _orders.LoadAsync(o.Id);
        foreach (var (code, value) in values)
        {
            var test = order.Tests.FirstOrDefault(t => t.TestCode == code);
            if (test == null) continue;
            await _pipeline.ApplyAsync(new ResultEntry { OrderTestId = test.Id, StringValue = value });
        }
    }

    private async Task VerifyAll(OrderDto o, string? criticalComment)
    {
        var doctor = await _db.Employees.AsNoTracking().FirstAsync(e => e.Id == Doctor);
        _current.Set(doctor, Doctor, "seed");
        _db.CurrentUserId = Doctor;
        var order = await _orders.LoadAsync(o.Id);
        foreach (var t in order.Tests.Where(t => t.Status is OrderTestStatuses.Resulted or OrderTestStatuses.NeedsReview))
            await _pipeline.VerifyAsync(t.Id, new VerifyRequest { Comment = Core.Clinical.ResultFlags.IsCritical(t.Result?.Flag) ? criticalComment ?? "Критичне значення підтверджено" : null });
        var admin = await _db.Employees.AsNoTracking().FirstAsync(e => e.Id == Admin);
        _current.Set(admin, Admin, "seed");
        _db.CurrentUserId = Admin;
    }

    /// <summary>Зсуває часові мітки замовлення в минуле для реалістичної TAT-аналітики.</summary>
    private async Task Backdate(string orderId, double hoursAgo)
    {
        var order = await _db.Orders.Include(o => o.Samples).Include(o => o.Tests).ThenInclude(t => t.Result).FirstAsync(o => o.Id == orderId);
        var t0 = DateTime.UtcNow.AddHours(-hoursAgo);
        order.OrderDatetime = t0;
        foreach (var s in order.Samples)
        {
            if (s.CollectedAt != null) s.CollectedAt = t0.AddMinutes(12);
            if (s.ReceivedAt != null) s.ReceivedAt = t0.AddMinutes(order.IsUrgentCito ? 25 : 70);
        }
        var i = 0;
        foreach (var r in order.Tests.Where(t => t.Result != null).Select(t => t.Result!))
        {
            r.EnteredAt = t0.AddMinutes((order.IsUrgentCito ? 50 : 130) + i++);
            if (r.VerifiedAt != null) r.VerifiedAt = r.EnteredAt.AddMinutes(order.IsUrgentCito ? 10 : 35);
            if (r.PreviousAt != null && r.PreviousAt > r.EnteredAt) r.PreviousAt = r.EnteredAt.AddDays(-5);
        }
        if (order.CompletedAt != null) order.CompletedAt = t0.AddMinutes(order.IsUrgentCito ? 70 : 190);
        if (order.ReleasedAt != null) order.ReleasedAt = t0.AddMinutes(order.IsUrgentCito ? 80 : 210);
        await _db.SaveChangesAsync();
    }
}
