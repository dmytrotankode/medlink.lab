// =============================================================================
// Демо-дані модуля «Патоморфологія» (ТЗ 22.14 п. 10): 6 справ різних типів і етапів і 3 вхідні направлення,
// одне — з контейнерами без маркування. Сценарій відповідає прототипу ред. 4.3 (справи ПГД-26-0398…0421).
// =============================================================================
using System.Text.Json;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services;
using MedLink.LIS.Api.Services.Ehealth;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Data.Seed;

public sealed class PathologyDemoSeeder
{
    private readonly LisDbContext _db;
    private readonly PathologyService _path;
    private readonly EhealthSignService _sign;
    private readonly ICurrentEmployee _current;

    public PathologyDemoSeeder(LisDbContext db, PathologyService path, EhealthSignService sign, ICurrentEmployee current)
    {
        _db = db; _path = path; _sign = sign; _current = current;
    }

    private static string Pat(int n) => $"0b000000-0000-0000-0000-00000000000{n}";
    private const string Facility = "КНП «Обласний онкологічний центр», м. Харків, вул. Прикладна, 1";

    private static JsonElement Form(string date, string time) =>
        JsonSerializer.SerializeToElement(new { parent = "Департамент охорони здоров'я обласної державної адміністрації", facility = Facility, edrpou = "01234567", date, time });

    private static PathObjectRequest Obj(string mark, string what, double fixHoursAgo, bool labeled = true, string fix = "10% нейтральний формалін") =>
        new() { Mark = mark, Description = what, Fixative = fix, FixationStartAt = DateTime.UtcNow.AddHours(-fixHoursAgo), TakenAt = DateTime.UtcNow.AddHours(-fixHoursAgo - 0.2), Labeled = labeled };

    public async Task SeedAsync()
    {
        if (await _db.PathReferrals.AnyAsync() || !await _db.PathMaterialTypes.AnyAsync()) return;
        var admin = await _db.Employees.AsNoTracking().FirstAsync(e => e.Id == DemoDataSeeder.Admin);
        var doctor = await _db.Employees.AsNoTracking().FirstAsync(e => e.Id == DemoDataSeeder.Doctor);
        void As(OrgEmployee e) { _current.Set(e, e.Id, "seed"); _db.CurrentUserId = e.Id; }
        As(admin);

        async Task<string> Create(PathologyReferralRequest req)
        {
            var created = JsonSerializer.SerializeToElement(await _path.CreateReferralAsync(req), JsonDefaults.Options);
            return created.GetProperty("id").GetString()!;
        }
        async Task<string> Accept(string id)
        {
            var codes = await _db.PathObjects.Where(o => o.OrderId == id).Select(o => o.Barcode).ToListAsync();
            await _path.AcceptAsync(id, new PathAcceptRequest { ScannedBarcodes = codes });
            return id;
        }
        async Task Gross(string id, string macro, string template, params int[][] blocksPerObject)
        {
            As(doctor);
            var objects = await _db.PathObjects.Where(o => o.OrderId == id).OrderBy(o => o.DisplayOrder).Select(o => o.Id).ToListAsync();
            await _path.SaveGrossAsync(id, new PathGrossRequest
            {
                Macro = macro, TemplateName = template, Finish = true,
                Objects = objects.Select((oid, i) => new PathGrossObject { ObjectId = oid, Blocks = blocksPerObject[i].Select(p => new PathGrossBlock { Pieces = p }).ToList() }).ToList()
            });
            As(admin);
        }
        async Task Advance(string id, params string[] stages) { foreach (var s in stages) await _path.AdvanceAsync(id, new PathScanRequest { Stage = s, Device = "Гістопроцесор Leica TP1020" }); }
        async Task Sign(string id, PathReportRequest report)
        {
            As(doctor);
            await _path.SaveReportAsync(id, report);
            await _path.SignAsync(id, new PathSignRequest { Signer = await _sign.TestCertificateAsync() });
            As(admin);
        }
        string[] all = { PathStages.Processing, PathStages.Embedding, PathStages.Sectioning, PathStages.Staining };

        // --- справи в роботі ---
        var c418 = await Accept(await Create(new PathologyReferralRequest
        {
            PatientId = Pat(2), Source = ReferralSources.Inpatient, Kind = PathKinds.Operative, Department = "Онкохірургія (мамологія)", MedicalCardNo = "ст-2026-1172", DoctorName = "Ткаченко В. Ю.",
            BiopsyKind = "SECONDARY", PrimaryCaseNo = "ПГД-26-0351", OperationId = "0c2a0000-0000-0000-0000-000000000058",
            OperationText = "Секторальна резекція лівої молочної залози з біопсією сторожового лімфовузла (протокол операції №58)",
            MarkingText = "2 контейнери: №1 — сектор лівої молочної залози (нитка — верхній край); №2 — сторожовий лімфовузол",
            ClinicalData = "Пухлина лівої молочної залози 2 см, виявлена при скринінгу 09.2026. Core-біопсія 02.10.2026: інвазивна карцинома (ПГД-26-0351). Постменопауза.",
            ClinicalDiagnosis = "C50.4 Злоякісне новоутворення верхньо-зовнішнього квадранта лівої молочної залози, cT1cN0M0", TemplateName = "Молочна залоза · секторальна резекція",
            Form014 = Form("27.10.2026", "12:20"), Objects = { Obj("1", "Сектор лівої молочної залози", 30), Obj("2", "Сторожовий лімфовузол", 30) }
        }));
        await Gross(c418, "Сектор молочної залози розмірами 7,5×5,0×3,2 см, маркований ниткою (верхній край). На розрізі — вузол сірувато-білого кольору щільної консистенції, з нечіткими контурами, розмірами 2,1×1,8 см; відстань до найближчого (латерального) краю резекції 0,6 см.\nЛімфатичний вузол розмірами 1,2×0,8×0,6 см, на розрізі сірувато-рожевий, однорідний. Вузол розрізано повністю.",
            "Молочна залоза · секторальна резекція", new[] { 2, 2, 1, 2 }, new[] { 2 });
        await Advance(c418, all);
        As(doctor);
        await _path.SaveReportAsync(c418, new PathReportRequest
        {
            Micro = "Інвазивна карцинома неспецифічного типу (протокова), ступінь злоякісності за Ноттінгемською системою: тубулярні структури 3, ядерний поліморфізм 2, мітози 1 — сума 6. Лімфоваскулярна інвазія не виявлена. Краї резекції без пухлини. У лімфатичному вузлі метастазів не виявлено",
            Icd10 = "C50.4", IcdoTopo = "C50.4", IcdoMorph = "8500/3", Grade = "G2", Pt = "pT1c", Pn = "pN0(sn)", Margin = "R0, 6 мм", LnTotal = 1, LnPositive = 0, Lvi = false, Pni = false
        });
        As(admin);

        var c412 = await Accept(await Create(new PathologyReferralRequest
        {
            PatientId = Pat(3), Source = ReferralSources.Inpatient, Kind = PathKinds.Biopsy, Department = "Онкоурологія", MedicalCardNo = "ст-2026-1160", DoctorName = "Кушнаренко М. В.",
            BiopsyKind = "SECONDARY", PrimaryCaseNo = "ПГД-26-0242", OperationText = "Повторна трансуретральна резекція сечового міхура (протокол операції №57)",
            MarkingText = "3 контейнери: №1 — ложе пухлини; №2 — дно пухлини (м'язовий шар); №3 — біопсія з лівої бічної стінки",
            ClinicalData = "Стан після ТУР 19.05.2026 (ПГД-26-0242: папілярна уротеліальна карцинома G3) та 6 курсів внутрішньоміхурової ХТ.",
            ClinicalDiagnosis = "C67.9 Злоякісне новоутворення сечового міхура", TemplateName = "Сечовий міхур · ТУР-біопсія", Form014 = Form("28.10.2026", "14:50"),
            Objects = { Obj("1", "Ложе пухлини", 20), Obj("2", "Дно пухлини", 20), Obj("3", "Ліва бічна стінка", 20) }
        }));
        await Gross(c412, "Фрагменти тканини сечового міхура загальним об'ємом 1,5 см³, сірувато-рожевого кольору, м'якої консистенції. Весь матеріал взято на дослідження.",
            "Сечовий міхур · ТУР-біопсія", new[] { 4 }, new[] { 3 }, new[] { 2 });
        await Advance(c412, PathStages.Processing, PathStages.Embedding, PathStages.Sectioning);

        var c420 = await Accept(await Create(new PathologyReferralRequest
        {
            PatientId = Pat(1), Source = ReferralSources.Inpatient, Kind = PathKinds.Cito, Department = "Операційна №2 · Онкохірургія", MedicalCardNo = "ст-2026-1190", DoctorName = "Ельшаавер А. А.",
            OperationId = "0c2a0000-0000-0000-0000-000000000062", OperationText = "Лапароскопічна ревізія; термінове дослідження лімфовузла (протокол операції №62, триває)",
            MarkingText = "1 контейнер без фіксатора: №1 — лімфовузол біля чревного стовбура",
            ClinicalData = "Рак підшлункової залози, cT2. Інтраопераційно — збільшений щільний лімфовузол 1,5 см. Питання: метастаз?",
            ClinicalDiagnosis = "C25.0 Злоякісне новоутворення головки підшлункової залози", TemplateName = "Лімфатичний вузол · сторожовий", Form014 = Form("30.10.2026", "10:35"),
            Objects = { Obj("1", "Лімфовузол (нативний, без фіксатора)", 0.2, fix: "Без фіксатора (нативний)") }
        }));

        var c421 = await Accept(await Create(new PathologyReferralRequest
        {
            PatientId = Pat(3), Source = ReferralSources.Partner, PartnerName = "КНП «Міська лікарня №7» (договір)", Kind = PathKinds.Consult, Department = "КНП «Міська лікарня №7» (договір)",
            MedicalCardNo = "амб-190233", DoctorName = "Сидоренко П. П.", MarkingText = "Скельця 1187-1…6, блоки 1187-А, 1187-Б",
            ClinicalData = "Висновок закладу-відправника: «Меланома шкіри? Невус з атипією?» Потрібна консультація.",
            ClinicalDiagnosis = "D48.5 Новоутворення шкіри невизначеного характеру", TemplateName = "Шкіра · ексцизійна біопсія", Form014 = Form("29.10.2026", "10:30"),
            External = new PathExternalRequest { Institution = "КНП «Міська лікарня №7»", Number = "1187/26", Date = DateTime.UtcNow.Date.AddDays(-18), Slides = 6, Blocks = 2 }
        }));

        // --- підписані справи ---
        var c405 = await Accept(await Create(new PathologyReferralRequest
        {
            PatientId = Pat(4), Source = ReferralSources.Inpatient, Kind = PathKinds.Operative, Department = "Онкохірургія (колопроктологія)", MedicalCardNo = "ст-2026-1121", DoctorName = "Постолов О. М.",
            OperationText = "Резекція сигмоподібної кишки (протокол операції №49)", MarkingText = "2 контейнери: №1 — сегмент сигмоподібної кишки; №2 — лімфовузли брижі",
            ClinicalData = "Пухлина сигмоподібної кишки, колоноскопія 09.2026.", ClinicalDiagnosis = "C18.7 Злоякісне новоутворення сигмоподібної кишки",
            TemplateName = "Шлунок · резекція", Form014 = Form("14.10.2026", "11:30"), Objects = { Obj("1", "Сегмент сигмоподібної кишки", 24), Obj("2", "Лімфовузли брижі", 24) }
        }));
        await Gross(c405, "Сегмент сигмоподібної кишки довжиною 18 см з циркулярною пухлиною 4,5 см. У брижі виділено 16 лімфатичних вузлів.", "Шлунок · резекція", new[] { 2, 2, 2 }, new[] { 6, 10 });
        await Advance(c405, all);
        await Sign(c405, new PathReportRequest
        {
            Micro = "Аденокарцинома, помірно диференційована (G2), інвазія в субсерозну основу. Метастази в 2 з 16 лімфатичних вузлів. Краї резекції без пухлини",
            ConclusionText = "Аденокарцинома сигмоподібної кишки, помірно диференційована (G2), pT3 pN1b (2/16) L1 V0 Pn0 R0. МКХ-О: C18.7, 8140/3.",
            Icd10 = "C18.7", IcdoTopo = "C18.7", IcdoMorph = "8140/3", Grade = "G2", Pt = "pT3", Pn = "pN1b", Margin = "R0", LnTotal = 16, LnPositive = 2, Lvi = true, Pni = false
        });

        var c398 = await Accept(await Create(new PathologyReferralRequest
        {
            PatientId = Pat(5), Source = ReferralSources.Ehealth, EhealthReferralNumber = "0002-2026-B8M4", Kind = PathKinds.Biopsy, Department = "Поліклініка (інший заклад)",
            MedicalCardNo = "амб-330912", DoctorName = "Ніколенко Т. В.", OperationText = "Прицільна біопсія шийки матки", MarkingText = "1 контейнер: №1 — біоптат шийки матки (6 год.)",
            ClinicalData = "Цитологія: HSIL. Кольпоскопія: ацетобіле поле на 6 год.", ClinicalDiagnosis = "N87.2 Виражена дисплазія шийки матки",
            TemplateName = "Ендометрій · зскрібок", Form014 = Form("10.10.2026", "16:00"), Objects = { Obj("1", "Біоптат шийки матки", 24) }
        }));
        await Gross(c398, "2 фрагменти тканини 0,4 і 0,3 см.", "Ендометрій · зскрібок", new[] { 2 });
        await Advance(c398, all);
        await Sign(c398, new PathReportRequest
        {
            Micro = "Плоскоклітинне інтраепітеліальне ураження високого ступеня (HSIL/CIN 3). Ознак інвазії не виявлено",
            ConclusionText = "Плоскоклітинне інтраепітеліальне ураження високого ступеня (HSIL/CIN 3) шийки матки. Ознак інвазії не виявлено.",
            Icd10 = "N87.2", IcdoTopo = "C53.9", IcdoMorph = "8077/2"
        });

        // --- вхідні направлення (одне — без маркування) ---
        var in1 = await Create(new PathologyReferralRequest
        {
            PatientId = Pat(6), Source = ReferralSources.Inpatient, Kind = PathKinds.Operative, Department = "Онкохірургія (абдомінальна)", Ward = "Палата 4, ліжко 2", MedicalCardNo = "ст-2026-1187",
            DoctorName = "Осокін А. Е.", BiopsyKind = "SECONDARY", PrimaryCaseNo = "ПГД-26-0377", OperationId = "0c2a0000-0000-0000-0000-000000000061",
            OperationText = "Дистальна субтотальна резекція шлунка з D2-лімфодисекцією (протокол операції №61)",
            MarkingText = "2 контейнери: №1 — препарат шлунка; №2 — лімфатичні вузли (D2), 14 шт.",
            ClinicalData = "Скарги 6 міс., схуднення на 8 кг. ФГДС 12.10.2026: виразкоподібна пухлина антрального відділу 4 см; біопсія — аденокарцинома (ПГД-26-0377).",
            ClinicalDiagnosis = "C16.3 Злоякісне новоутворення антрального відділу шлунка, cT3N1M0", TemplateName = "Шлунок · резекція", Form014 = Form("30.10.2026", "09:15"),
            Objects = { Obj("1", "Препарат шлунка", 2.4), Obj("2", "Лімфатичні вузли D2 (14 шт.)", 2.3) }
        });
        var in2 = await Create(new PathologyReferralRequest
        {
            PatientId = Pat(3), Source = ReferralSources.Polyclinic, Kind = PathKinds.Biopsy, Department = "Поліклініка · онкодерматолог", MedicalCardNo = "амб-352110", DoctorName = "Литвин О. С.",
            OperationText = "Ексцизійна біопсія новоутворення шкіри спини (амбулаторно)", MarkingText = "1 контейнер: №1 — шкірно-жировий клапоть, маркування шовком (12 год.)",
            ClinicalData = "Пігментне новоутворення шкіри спини, зростання за 6 міс., дерматоскопічно ABCD 5,8.", ClinicalDiagnosis = "D48.5 Новоутворення шкіри невизначеного характеру",
            TemplateName = "Шкіра · ексцизійна біопсія", Form014 = Form("30.10.2026", "10:05"), Objects = { Obj("1", "Шкірно-жировий клапоть", 1.5) }
        });
        var in3 = await Create(new PathologyReferralRequest
        {
            PatientId = Pat(4), Source = ReferralSources.Ehealth, EhealthReferralNumber = "0002-2026-H7K2", Kind = PathKinds.Biopsy, Department = "Жіноча консультація (інший заклад)",
            MedicalCardNo = "амб-298744", DoctorName = "Іваненко Л. П.", OperationText = "Роздільне діагностичне вишкрібання", MarkingText = "Контейнери без маркування",
            ClinicalData = "Кровотеча в постменопаузі. Остання менструація — 2019 р. Початок кровотечі — 21.10.2026.", ClinicalDiagnosis = "N95.0 Постменопаузальна кровотеча",
            TemplateName = "Ендометрій · зскрібок", Form014 = Form("29.10.2026", "16:20"),
            Objects = { Obj("?", "Зскрібок ендометрію", 17, labeled: false), Obj("?", "Зскрібок цервікального каналу", 17, labeled: false) }
        });

        // --- реалістичні номери й дати (прототип: справи ПГД-26-0398…0421, наступний № — 0422) ---
        async Task Shape(string id, string caseNo, double daysAgo, double? signedDaysAgo = null)
        {
            var o = await _db.Orders.FirstAsync(x => x.Id == id);
            var r = await _db.PathReferrals.FirstAsync(x => x.OrderId == id);
            var shift = TimeSpan.FromDays(daysAgo);
            o.CaseNo = caseNo;
            o.OrderDatetime -= shift + TimeSpan.FromMinutes(40);
            r.AcceptedAt -= shift;
            o.SlaDueAt = o.PathologyKind == PathKinds.Cito ? r.AcceptedAt!.Value.AddMinutes(20) : PathologyService.AddWorkdays(r.AcceptedAt!.Value, o.PathologyKind == PathKinds.Operative ? 7 : 5);
            if (r.GrossFinishedAt.HasValue) r.GrossFinishedAt -= shift - TimeSpan.FromHours(20);
            foreach (var l in await _db.PathStageLog.Where(x => x.OrderId == id).ToListAsync()) l.PerformedAt -= shift - TimeSpan.FromHours(l.Stage == PathStages.Received ? 0 : 20 + Array.IndexOf(PathStages.All, l.Stage) * 4);
            foreach (var e in await _db.MisEvents.Where(x => x.OrderId == id).ToListAsync()) e.At -= shift;
            if (signedDaysAgo.HasValue)
            {
                var signedAt = DateTime.UtcNow.AddDays(-signedDaysAgo.Value);
                o.ReleasedAt = o.CompletedAt = signedAt;
                foreach (var rep in await _db.PathReports.Where(x => x.OrderId == id && x.SignedAt != null).ToListAsync()) rep.SignedAt = signedAt;
                foreach (var l in await _db.PathStageLog.Where(x => x.OrderId == id && x.Stage == PathStages.Signed).ToListAsync()) l.PerformedAt = signedAt;
                foreach (var b in await _db.PathBlocks.Where(x => x.OrderId == id).ToListAsync()) b.StorageLocation = $"Шафа 2 · полиця {(caseNo.EndsWith("05") ? 4 : 3)} · комірка {caseNo[^3..]}";
            }
        }
        await Shape(c398, "ПГД-26-0398", 22, 17);
        await Shape(c405, "ПГД-26-0405", 19, 13);
        await Shape(c412, "ПГД-26-0412", 9);
        await Shape(c418, "ПГД-26-0418", 6);
        await Shape(c421, "ПГД-26-0421", 4);
        await Shape(c420, "ПГД-26-0420", 7 / 1440.0);
        (await _db.Orders.FirstAsync(x => x.Id == c398)).EhealthStatus = EhealthSubmitStatuses.Completed;
        foreach (var (id, hours) in new[] { (in1, 3.0), (in2, 2.0), (in3, 18.0) })
            (await _db.Orders.FirstAsync(x => x.Id == id)).OrderDatetime = DateTime.UtcNow.AddHours(-hours);
        var counter = "PATH_CASE_" + DateTime.UtcNow.ToString("yy");
        var c = await _db.Counters.FirstOrDefaultAsync(x => x.CounterCode == counter);
        if (c == null) _db.Counters.Add(new LabCounter { CounterCode = counter, CounterValue = 421 });
        else c.CounterValue = Math.Max(c.CounterValue, 421);
        await _db.SaveChangesAsync();

        // Видача блока на молекулярне дослідження (архів, FR-PAT-071)
        var b1 = await _db.PathBlocks.FirstAsync(b => b.OrderId == c405 && b.Code == "B1");
        await _path.IssueAsync(b1.Id, new PathIssueRequest { Recipient = "ДУ «Національний інститут раку», лабораторія молекулярної онкології", Document = "Акт видачі №12/26", Purpose = "Молекулярне дослідження (MSI, KRAS/NRAS)", DueBack = DateTime.UtcNow.Date.AddDays(10) });
    }
}
