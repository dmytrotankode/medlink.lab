// =============================================================================
// Сідер довідників з вбудованих ресурсів db/seed/*.json (ідемпотентний: кожна таблиця
// заповнюється лише якщо порожня). Типи аналізаторів — із ресурсу MedLink.LIS.Core (analyzer_types.json).
// =============================================================================
using System.Reflection;
using System.Text.Json;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services;
using MedLink.LIS.Core.Barcodes;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Data.Seed;

public sealed class SeedService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private readonly LisDbContext _db;
    private readonly ILogger<SeedService> _logger;

    public SeedService(LisDbContext db, ILogger<SeedService> logger) { _db = db; _logger = logger; }

    public static T ReadResource<T>(string name)
    {
        var asm = typeof(SeedService).Assembly;
        var resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
                      ?? throw new FileNotFoundException($"Вбудований ресурс сіду {name} не знайдено");
        using var s = asm.GetManifestResourceStream(resName)!;
        return JsonSerializer.Deserialize<T>(s, Json)!;
    }

    public static JsonElement ReadCoreAnalyzerTypes()
    {
        var asm = typeof(MedLink.LIS.Core.Contracts.ConnectorConfigDto).Assembly;
        var resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("analyzer_types.json", StringComparison.OrdinalIgnoreCase))
                      ?? throw new FileNotFoundException("Ресурс analyzer_types.json у MedLink.LIS.Core не знайдено");
        using var s = asm.GetManifestResourceStream(resName)!;
        return JsonSerializer.Deserialize<JsonElement>(s, Json);
    }

    public async Task SeedAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await SeedSettingsAsync();
        await SeedOrgAsync();
        await SeedDictionariesAsync();
        await SeedSectionsAsync();
        await SeedTestsAndProfilesAsync();
        await SeedReferenceLayersAsync();
        await SeedReflexRulesAsync();
        await SeedMicrobiologyAsync();
        await SeedAnalyzersAsync();
        await SeedQcMaterialsAsync();
        await SeedCountersAsync();
        _logger.LogInformation("Сід довідників завершено за {Ms} мс", sw.ElapsedMilliseconds);
    }

    private async Task SeedSettingsAsync()
    {
        if (await _db.Settings.AnyAsync()) return;
        var s = ReadResource<JsonElement>("settings.json");
        var settings = JsonSerializer.Deserialize<LabSettings>(s.GetRawText(), Json) ?? new LabSettings();
        settings.Id = DeterministicGuid.For("lab-settings-default");
        if (s.TryGetProperty("reportTemplates", out var rt)) settings.ReportTemplates = JsonSerializer.Deserialize<Dictionary<string, bool>>(rt.GetRawText(), Json) ?? new();
        _db.Settings.Add(settings);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Організаційні дані у структурі MedLink: cmn_enum_record (ті самі GUID, що в evomis DbInitializer), org_organization,
    /// org_department + lab_department_settings, cmn_person + org_employee + lab_employee_settings, cmn_person + mis_patient_card,
    /// ehe_service_catalog_service + org_organization_service, ehe_incoming_medical_referral. Вхідний org.json — плоский і читабельний.
    /// </summary>
    private async Task SeedOrgAsync()
    {
        var org = ReadResource<JsonElement>("org.json");
        if (!await _db.EnumRecords.AnyAsync())
        {
            void E(string id, string type, string code, string caption) => _db.EnumRecords.Add(new CmnEnumRecord { Id = id, EnumType = type, Code = code, Caption = caption });
            E(MedLinkEnums.GenderMale, "Gender", "M", "Чоловік");
            E(MedLinkEnums.GenderFemale, "Gender", "F", "Жінка");
            E(MedLinkEnums.GenderUnknown, "Gender", "U", "Невідомий");
            E(MedLinkEnums.DeptTypeDiagnostic, "DepartmentType", "02", "Діагностичне");
            E(MedLinkEnums.DeptTypeTreatment, "DepartmentType", "03", "Лікувальне");
            E(MedLinkEnums.DeptTypeBranch, "DepartmentType", "05", "Філія");
            E(MedLinkEnums.DeptTypeUnit, "DepartmentType", "06", "Підрозділ");
            E(MedLinkEnums.PositionOther, "OrgPositionType", "0001", "Інші");
            E(MedLinkEnums.PositionDoctors, "OrgPositionType", "0002", "Лікарі");
            E(MedLinkEnums.PositionLabTechs, "OrgPositionType", "0003", "Лаборанти");
            E(MedLinkEnums.PositionNurses, "OrgPositionType", "0005", "Середній мед.персонал");
            E(MedLinkEnums.PrivacyUnknown, "PrivacyRequestType", "Uknown", "Не опитували");
            E(MedLinkDefaults.PatientCardDocumentTypeId, "MedicalDocumentType", "AmbCard", "Амбулаторна карта");
            E(MedLinkDefaults.PatientCardTypePersonId, "PatientCardType", "person", "Особа");
            E(MedLinkDefaults.ReferralStatusActiveId, "MedicalReferralStatus", "active", "Активне");
        }
        if (!await _db.Organizations.AnyAsync())
            _db.Organizations.Add(new OrgOrganization { Id = MedLinkDefaults.OrganizationId, Code = "MEDLINK-LAB", Caption = "ТОВ «МедЛінк» — Клініко-діагностична лабораторія", FullName = "Товариство з обмеженою відповідальністю «МедЛінк»" });
        if (!await _db.Departments.AnyAsync())
            foreach (var el in org.GetProperty("departments").EnumerateArray())
            {
                var kind = el.GetProperty("departmentType").GetString()!;
                var d = new OrgDepartment
                {
                    Id = el.GetProperty("id").GetString()!, Code = Str(el, "code"), Caption = Str(el, "name"), FullName = Str(el, "name"), Location = Str(el, "address"),
                    OrganizationId = MedLinkDefaults.OrganizationId, DepartmentTypeId = OrgDictionaryService.DepartmentTypeFor(kind)
                };
                d.LabSettings = new LabDepartmentSettings { DepartmentId = d.Id, LabKind = kind, Phone = Str(el, "phone") };
                _db.Departments.Add(d);
            }
        if (!await _db.Employees.AnyAsync())
            foreach (var el in org.GetProperty("employees").EnumerateArray())
            {
                var id = el.GetProperty("id").GetString()!;
                var parts = Str(el, "fullName")!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var person = new CmnPerson
                {
                    Id = DeterministicGuid.For("person:employee:" + id), LastName = parts.ElementAtOrDefault(0), Name = parts.ElementAtOrDefault(1), MiddleName = parts.ElementAtOrDefault(2),
                    Phone = Str(el, "phone"), Email = Str(el, "email"), NoIpn = true
                };
                person.Caption = CmnPerson.BuildCaption(person.LastName, person.Name, person.MiddleName);
                var role = el.GetProperty("labRole").GetString()!;
                var e = new OrgEmployee
                {
                    Id = id, Person = person, PersonId = person.Id, Caption = person.Caption, OrganizationId = MedLinkDefaults.OrganizationId,
                    DepartmentId = Str(el, "departmentId"), PositionTypeId = OrgDictionaryService.PositionTypeFor(role), WorkingStartDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };
                e.LabSettings = new LabEmployeeSettings { EmployeeId = id, LabRole = role, PositionName = Str(el, "positionName"), DigitalSignatureCertId = Str(el, "digitalSignatureCertId") };
                _db.Employees.Add(e);
            }
        if (!await _db.Patients.AnyAsync())
            foreach (var el in org.GetProperty("patients").EnumerateArray())
            {
                var req = JsonSerializer.Deserialize<NewPatientRequest>(el.GetRawText(), Json)!;
                if (req.BirthDate.HasValue) req.BirthDate = DateTime.SpecifyKind(req.BirthDate.Value, DateTimeKind.Utc);
                var card = MedLinkPeople.NewPatient(req, MedLinkDefaults.OrganizationId);
                card.Id = el.GetProperty("id").GetString()!;
                card.Person!.Id = card.PersonId = DeterministicGuid.For("person:patient:" + card.Id);
                card.RegNumber = "AMB-" + card.Id[^4..];
                _db.Patients.Add(card);
            }
        if (!await _db.ServiceCatalog.AnyAsync())
            foreach (var el in org.GetProperty("services").EnumerateArray())
            {
                var code = Str(el, "code")!;
                _db.ServiceCatalog.Add(new EheServiceCatalogService
                {
                    Id = DeterministicGuid.For("ehealth-service:" + code), Code = code, Name = Str(el, "name"), Caption = Str(el, "name"),
                    MedicalReferralCategoryId = MedLinkDefaults.ReferralCategoryLaboratoryId
                });
                _db.OrganizationServices.Add(new OrgOrganizationService
                {
                    Id = el.GetProperty("id").GetString()!, Caption = Str(el, "name"), Price = el.GetProperty("price").GetDecimal(), OrganizationId = MedLinkDefaults.OrganizationId, Duration = 15
                });
            }
        await _db.SaveChangesAsync();
        if (!await _db.Referrals.AnyAsync())
        {
            foreach (var el in org.GetProperty("referrals").EnumerateArray())
            {
                var patientId = Str(el, "patientId");
                var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId);
                _db.Referrals.Add(new EheIncomingMedicalReferral
                {
                    Id = el.GetProperty("id").GetString()!, RegNumber = Str(el, "referralCode"), Caption = Str(el, "serviceName"), OrganizationId = MedLinkDefaults.OrganizationId,
                    PatientCardId = patientId, PatientShortName = patient?.Caption, ServiceCatalogServiceId = DeterministicGuid.For("ehealth-service:" + Str(el, "serviceCode")),
                    MedicalReferralCategoryId = MedLinkDefaults.ReferralCategoryLaboratoryId, StatusId = MedLinkDefaults.ReferralStatusActiveId, PriorityId = MedLinkEnums.EmptyGuid,
                    ExpirationDate = DateTime.UtcNow.Date.AddDays(30)
                });
            }
            await _db.SaveChangesAsync();
        }
    }

    private static string? Str(JsonElement el, string name) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private async Task SeedDictionariesAsync()
    {
        if (!await _db.BiomaterialTypes.AnyAsync())
            _db.BiomaterialTypes.AddRange(ReadResource<List<LabBiomaterialType>>("biomaterials.json"));
        if (!await _db.TubeTypes.AnyAsync())
            _db.TubeTypes.AddRange(ReadResource<List<LabTubeType>>("tube_types.json"));
        if (!await _db.MethodTypes.AnyAsync())
            _db.MethodTypes.AddRange(ReadResource<List<LabMethodType>>("method_types.json"));
        if (!await _db.AnalyzerTypes.AnyAsync())
        {
            foreach (var el in ReadCoreAnalyzerTypes().EnumerateArray())
            {
                var t = JsonSerializer.Deserialize<LabAnalyzerType>(el.GetRawText(), Json)!;
                t.Manufacturer ??= GuessManufacturer(t.Name);
                _db.AnalyzerTypes.Add(t);
            }
        }
        await _db.SaveChangesAsync();
    }

    private static string? GuessManufacturer(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("sysmex")) return "Sysmex";
        if (n.Contains("cobas") || n.Contains("сobas") || n.Contains("integra") || n.Contains("urisys") || n.Contains("reflotron")) return "Roche";
        if (n.Contains("mindray")) return "Mindray";
        if (n.Contains("vitros")) return "Ortho Clinical Diagnostics";
        if (n.Contains("radiometer") || n.Contains("abl") || n.Contains("aqt")) return "Radiometer";
        if (n.Contains("huma")) return "Human";
        if (n.Contains("beckman") || n.Contains("access")) return "Beckman Coulter";
        if (n.Contains("siemens") || n.Contains("rapid")) return "Siemens";
        if (n.Contains("maglumi")) return "Snibe";
        if (n.Contains("stago")) return "Stago";
        if (n.Contains("biosystem") || n.Contains("ba200")) return "BioSystems";
        if (n.Contains("pentra")) return "Horiba";
        if (n.Contains("nihon") || n.Contains("mek")) return "Nihon Kohden";
        if (n.Contains("iris")) return "Beckman Coulter";
        if (n.Contains("fuji")) return "Fujifilm";
        if (n.Contains("phadia")) return "Thermo Fisher";
        if (n.Contains("ichroma")) return "Boditech";
        if (n.Contains("erba")) return "Erba Mannheim";
        return null;
    }

    private async Task SeedSectionsAsync()
    {
        var doc = ReadResource<JsonElement>("sections.json");
        if (!await _db.WorkflowTemplates.AnyAsync())
        {
            foreach (var el in doc.GetProperty("workflowTemplates").EnumerateArray())
            {
                var stages = JsonSerializer.Deserialize<List<WorkflowStage>>(el.GetProperty("stages").GetRawText(), Json)!;
                _db.WorkflowTemplates.Add(new LabWorkflowTemplate { Code = el.GetProperty("code").GetString()!, Name = el.GetProperty("name").GetString()!, Stages = stages });
            }
        }
        if (!await _db.Sections.AnyAsync())
            _db.Sections.AddRange(JsonSerializer.Deserialize<List<LabSection>>(doc.GetProperty("sections").GetRawText(), Json)!);
        await _db.SaveChangesAsync();
    }

    private async Task SeedTestsAndProfilesAsync()
    {
        if (await _db.Tests.AnyAsync()) return;
        var bm = await _db.BiomaterialTypes.ToDictionaryAsync(b => b.Code, b => b.Id);
        var tubes = await _db.TubeTypes.ToDictionaryAsync(t => t.Code, t => t.Id);
        var sections = await _db.Sections.ToDictionaryAsync(s => s.Code, s => s.Id);
        var tests = new Dictionary<string, LabTestDefinition>();
        foreach (var el in ReadResource<List<JsonElement>>("tests.json"))
        {
            var t = JsonSerializer.Deserialize<LabTestDefinition>(el.GetRawText(), Json)!;
            t.Id = DeterministicGuid.For("test:" + t.Code);
            t.BiomaterialTypeId = bm[el.GetProperty("biomaterialCode").GetString()!];
            t.TubeTypeId = tubes[el.GetProperty("tubeCode").GetString()!];
            if (el.TryGetProperty("labSectionCode", out var sc) && sc.ValueKind == JsonValueKind.String && sections.TryGetValue(sc.GetString()!, out var sid)) t.LabSectionId = sid;
            if (el.TryGetProperty("dropdownOptions", out var opts) && opts.ValueKind == JsonValueKind.Array) t.DropdownOptions = opts.EnumerateArray().Select(o => o.GetString() ?? "").ToList();
            tests[t.Code] = t;
            _db.Tests.Add(t);
        }
        var org = ReadResource<JsonElement>("org.json");
        var services = org.GetProperty("services").EnumerateArray().ToList();
        foreach (var el in ReadResource<List<JsonElement>>("profiles.json"))
        {
            var p = JsonSerializer.Deserialize<LabTestProfile>(el.GetRawText(), Json)!;
            p.Id = DeterministicGuid.For("profile:" + p.Code);
            p.Items = new();
            p.DefaultBiomaterialTypeId = bm[el.GetProperty("biomaterialCode").GetString()!];
            p.DefaultTubeTypeId = tubes[el.GetProperty("tubeCode").GetString()!];
            foreach (var it in el.GetProperty("items").EnumerateArray())
            {
                var code = it.GetProperty("testCode").GetString()!;
                p.Items.Add(new LabTestProfileItem { ProfileId = p.Id, TestId = tests[code].Id, DisplayOrder = it.GetProperty("displayOrder").GetInt32(), IsRequired = it.GetProperty("isRequired").GetBoolean() });
            }
            // Послуга прайсу MedLink (org_organization_service) і послуга каталогу eHealth ↔ лабораторний профіль (рішення Q-01)
            var svc = services.FirstOrDefault(s => s.GetProperty("labProfileCode").GetString() == p.Code);
            if (svc.ValueKind == JsonValueKind.Object)
            {
                p.OrganizationServiceId = svc.GetProperty("id").GetString()!;
                p.EhealthServiceCatalogServiceId = DeterministicGuid.For("ehealth-service:" + svc.GetProperty("code").GetString());
                p.Price = svc.GetProperty("price").GetDecimal();
            }
            _db.Profiles.Add(p);
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedReferenceLayersAsync()
    {
        if (await _db.ReferenceLayers.AnyAsync()) return;
        var tests = await _db.Tests.ToDictionaryAsync(t => t.Code, t => t.Id);
        foreach (var l in ReadResource<List<LabReferenceLayer>>("reference_layers.json"))
        {
            l.TestId = tests.TryGetValue(l.TestCode, out var id) ? id : null;
            if (!Guid.TryParse(l.Id, out _)) l.Id = DeterministicGuid.For("layer:" + l.Id);
            _db.ReferenceLayers.Add(l);
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedReflexRulesAsync()
    {
        if (await _db.ReflexRules.AnyAsync()) return;
        _db.ReflexRules.AddRange(ReadResource<List<LabReflexRule>>("reflex_rules.json"));
        await _db.SaveChangesAsync();
    }

    private async Task SeedMicrobiologyAsync()
    {
        var m = ReadResource<JsonElement>("microbiology.json");
        if (!await _db.Organisms.AnyAsync()) _db.Organisms.AddRange(JsonSerializer.Deserialize<List<LabMicroOrganism>>(m.GetProperty("organisms").GetRawText(), Json)!);
        if (!await _db.Antibiotics.AnyAsync()) _db.Antibiotics.AddRange(JsonSerializer.Deserialize<List<LabAntibiotic>>(m.GetProperty("antibiotics").GetRawText(), Json)!);
        await _db.SaveChangesAsync();
        if (!await _db.EucastBreakpoints.AnyAsync())
        {
            var orgs = await _db.Organisms.ToDictionaryAsync(o => o.Code, o => o.Id);
            var abs = await _db.Antibiotics.ToDictionaryAsync(a => a.Code, a => a.Id);
            foreach (var el in m.GetProperty("breakpoints").EnumerateArray())
            {
                var bp = JsonSerializer.Deserialize<LabEucastBreakpoint>(el.GetRawText(), Json)!;
                bp.OrganismId = orgs[el.GetProperty("organismCode").GetString()!];
                bp.AntibioticId = abs[el.GetProperty("antibioticCode").GetString()!];
                bp.Id = DeterministicGuid.For($"bp:{bp.OrganismId}:{bp.AntibioticId}:{bp.EucastVersion}");
                _db.EucastBreakpoints.Add(bp);
            }
            await _db.SaveChangesAsync();
        }
    }

    private async Task SeedAnalyzersAsync()
    {
        var a = ReadResource<JsonElement>("analyzers.json");
        if (!await _db.Connectors.AnyAsync())
        {
            foreach (var el in a.GetProperty("connectors").EnumerateArray())
                _db.Connectors.Add(new LabConnectorInstallation { Id = el.GetProperty("id").GetString()!, Name = el.GetProperty("name").GetString()!, InstallKey = el.GetProperty("installKey").GetString()!, Status = el.GetProperty("status").GetString()! });
            await _db.SaveChangesAsync();
        }
        if (!await _db.Analyzers.AnyAsync())
        {
            var types = await _db.AnalyzerTypes.ToDictionaryAsync(t => t.Code, t => t.Id);
            foreach (var el in a.GetProperty("analyzers").EnumerateArray())
            {
                var an = JsonSerializer.Deserialize<LabAnalyzer>(el.GetRawText(), Json)!;
                an.AnalyzerTypeId = types[el.GetProperty("analyzerTypeCode").GetString()!];
                an.ParameterMap = el.GetProperty("parameterMap").EnumerateArray().Select(m => new LabAnalyzerParameterMap
                {
                    AnalyzerId = an.Id, AnalyzerCode = m.GetProperty("analyzerCode").GetString()!, TestCode = m.GetProperty("testCode").GetString()!,
                    Factor = m.TryGetProperty("factor", out var f) ? f.GetDouble() : 1.0, Offset = m.TryGetProperty("offset", out var o) ? o.GetDouble() : 0.0,
                    UnitOverride = m.TryGetProperty("unitOverride", out var u) ? u.GetString() : null
                }).ToList();
                _db.Analyzers.Add(an);
            }
            await _db.SaveChangesAsync();
        }
    }

    private async Task SeedQcMaterialsAsync()
    {
        if (await _db.QcMaterials.AnyAsync()) return;
        foreach (var el in ReadResource<List<JsonElement>>("qc_materials.json"))
        {
            var m = JsonSerializer.Deserialize<LabQcMaterial>(el.GetRawText(), Json)!;
            m.ExpiryDate = DateTime.SpecifyKind(m.ExpiryDate, DateTimeKind.Utc);
            m.Targets = el.GetProperty("targets").EnumerateArray().Select(t => JsonSerializer.Deserialize<LabQcTarget>(t.GetRawText(), Json)!).ToList();
            foreach (var t in m.Targets) t.QcMaterialId = m.Id;
            _db.QcMaterials.Add(m);
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedCountersAsync()
    {
        if (!await _db.Counters.AnyAsync(c => c.CounterCode == TubeBarcodeGenerator.CounterCode))
            _db.Counters.Add(new LabCounter { CounterCode = TubeBarcodeGenerator.CounterCode, CounterValue = 4800 }); // продовження нумерації Simplex
        if (!await _db.Numerators.AnyAsync(n => n.Code == NumeratorService.OrderNumerator))
        {
            var mask = (await _db.Settings.AsNoTracking().FirstOrDefaultAsync())?.OrderNumberMask ?? "{yyMM}-{000000}";
            _db.Numerators.Add(new LabNumerator { Code = NumeratorService.OrderNumerator, Name = "Номер замовлення", Mask = mask, CurrentValue = 0, ResetByPeriod = true });
        }
        if (!await _db.Numerators.AnyAsync(n => n.Code == NumeratorService.ManifestNumerator))
            _db.Numerators.Add(new LabNumerator { Code = NumeratorService.ManifestNumerator, Name = "Номер маніфесту логістики", Mask = "MAN-{yyMMdd}-{0000}", ResetByPeriod = true });
        if (!await _db.Numerators.AnyAsync(n => n.Code == NumeratorService.BatchNumerator))
            _db.Numerators.Add(new LabNumerator { Code = NumeratorService.BatchNumerator, Name = "Код робочого листа", Mask = "WL-{yyMMdd}-{000}", ResetByPeriod = true });
        await _db.SaveChangesAsync();
    }
}
