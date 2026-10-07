// =============================================================================
// MedLink LIS 4.0 — DbContext. Іменування як в evomis: snake_case таблиць/колонок,
// ключі — рядкові GUID, службові колонки created_on/created_by/modified_on/modified_by/is_deleted.
// Усі типи сумісні з PostgreSQL: перехід на evomis = UseNpgsql + вказати DbSet на реальні таблиці.
// =============================================================================
using System.Text;
using System.Text.RegularExpressions;
using MedLink.LIS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MedLink.LIS.Api.Data;

public class LisDbContext : DbContext
{
    public LisDbContext(DbContextOptions<LisDbContext> options) : base(options) { }

    // --- [MedLink] дзеркало таблиць evomis (структура як в evomis) ---
    public DbSet<CmnEnumRecord> EnumRecords => Set<CmnEnumRecord>();
    public DbSet<CmnPerson> Persons => Set<CmnPerson>();
    public DbSet<OrgOrganization> Organizations => Set<OrgOrganization>();
    public DbSet<MisPatientCard> Patients => Set<MisPatientCard>();
    public DbSet<MisDiagnosticReport> DiagnosticReports => Set<MisDiagnosticReport>();
    public DbSet<OrgEmployee> Employees => Set<OrgEmployee>();
    public DbSet<OrgDepartment> Departments => Set<OrgDepartment>();
    public DbSet<EheIncomingMedicalReferral> Referrals => Set<EheIncomingMedicalReferral>();
    public DbSet<EhePaperMedicalReferral> PaperReferrals => Set<EhePaperMedicalReferral>();
    public DbSet<EheServiceCatalogService> ServiceCatalog => Set<EheServiceCatalogService>();
    public DbSet<OrgOrganizationService> OrganizationServices => Set<OrgOrganizationService>();

    // --- [ЛІС] лабораторні атрибути сутностей MedLink ---
    public DbSet<LabEmployeeSettings> EmployeeSettings => Set<LabEmployeeSettings>();
    public DbSet<LabDepartmentSettings> DepartmentSettings => Set<LabDepartmentSettings>();

    // --- dictionaries ---
    public DbSet<LabBiomaterialType> BiomaterialTypes => Set<LabBiomaterialType>();
    public DbSet<LabTubeType> TubeTypes => Set<LabTubeType>();
    public DbSet<LabMethodType> MethodTypes => Set<LabMethodType>();
    public DbSet<LabAnalyzerType> AnalyzerTypes => Set<LabAnalyzerType>();
    public DbSet<LabTestDefinition> Tests => Set<LabTestDefinition>();
    public DbSet<LabTestProfile> Profiles => Set<LabTestProfile>();
    public DbSet<LabTestProfileItem> ProfileItems => Set<LabTestProfileItem>();
    public DbSet<LabReferenceLayer> ReferenceLayers => Set<LabReferenceLayer>();
    public DbSet<LabReflexRule> ReflexRules => Set<LabReflexRule>();
    public DbSet<LabMicroOrganism> Organisms => Set<LabMicroOrganism>();
    public DbSet<LabAntibiotic> Antibiotics => Set<LabAntibiotic>();
    public DbSet<LabEucastBreakpoint> EucastBreakpoints => Set<LabEucastBreakpoint>();

    // --- workflow ---
    public DbSet<LabOrder> Orders => Set<LabOrder>();
    public DbSet<LabOrderSample> Samples => Set<LabOrderSample>();
    public DbSet<LabOrderTest> OrderTests => Set<LabOrderTest>();
    public DbSet<LabTestResult> Results => Set<LabTestResult>();
    public DbSet<LabResultHistory> ResultHistory => Set<LabResultHistory>();
    public DbSet<LabPanicCall> PanicCalls => Set<LabPanicCall>();
    public DbSet<LabWorklistBatch> Batches => Set<LabWorklistBatch>();
    public DbSet<LabUnmatchedResult> UnmatchedResults => Set<LabUnmatchedResult>();
    public DbSet<LabPatientNotification> Notifications => Set<LabPatientNotification>();

    // --- QC ---
    public DbSet<LabQcMaterial> QcMaterials => Set<LabQcMaterial>();
    public DbSet<LabQcTarget> QcTargets => Set<LabQcTarget>();
    public DbSet<LabQcResult> QcResults => Set<LabQcResult>();
    public DbSet<LabAnalyzerLockout> Lockouts => Set<LabAnalyzerLockout>();

    // --- logistics / biobank / reagents ---
    public DbSet<LabSampleLogistics> Logistics => Set<LabSampleLogistics>();
    public DbSet<LabSampleLogisticsItem> LogisticsItems => Set<LabSampleLogisticsItem>();
    public DbSet<LabArchiveRack> Racks => Set<LabArchiveRack>();
    public DbSet<LabArchiveCell> Cells => Set<LabArchiveCell>();
    public DbSet<LabReagentLot> ReagentLots => Set<LabReagentLot>();

    // --- microbiology ---
    public DbSet<LabCultureOrder> Cultures => Set<LabCultureOrder>();
    public DbSet<LabIsolate> Isolates => Set<LabIsolate>();
    public DbSet<LabSusceptibilityResult> Susceptibilities => Set<LabSusceptibilityResult>();

    // --- connector ---
    public DbSet<LabConnectorInstallation> Connectors => Set<LabConnectorInstallation>();
    public DbSet<LabAnalyzer> Analyzers => Set<LabAnalyzer>();
    public DbSet<LabAnalyzerParameterMap> AnalyzerParameters => Set<LabAnalyzerParameterMap>();
    public DbSet<LabAnalyzerMessage> AnalyzerMessages => Set<LabAnalyzerMessage>();
    public DbSet<LabConnectorLog> ConnectorLogs => Set<LabConnectorLog>();
    public DbSet<LabConnectorCommand> ConnectorCommands => Set<LabConnectorCommand>();

    // --- sections / journals / processing ---
    public DbSet<LabSection> Sections => Set<LabSection>();
    public DbSet<LabSectionJournalEntry> JournalEntries => Set<LabSectionJournalEntry>();
    public DbSet<LabWorkflowTemplate> WorkflowTemplates => Set<LabWorkflowTemplate>();
    public DbSet<LabSampleStageEvent> StageEvents => Set<LabSampleStageEvent>();
    public DbSet<LabOrderFavorite> OrderFavorites => Set<LabOrderFavorite>();

    // --- зовнішні лабораторії (send-out) ---
    public DbSet<LabPerformer> Performers => Set<LabPerformer>();
    public DbSet<LabPerformerTest> PerformerTests => Set<LabPerformerTest>();
    public DbSet<LabSendOut> SendOuts => Set<LabSendOut>();
    public DbSet<LabSendOutItem> SendOutItems => Set<LabSendOutItem>();
    public DbSet<LabOrderAttachment> Attachments => Set<LabOrderAttachment>();
    public DbSet<LabEhealthExchangeLog> EhealthExchangeLog => Set<LabEhealthExchangeLog>();

    // --- system ---
    public DbSet<LabAuditLog> AuditLog => Set<LabAuditLog>();
    public DbSet<LabSettings> Settings => Set<LabSettings>();
    public DbSet<LabNumerator> Numerators => Set<LabNumerator>();
    public DbSet<LabCounter> Counters => Set<LabCounter>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Унікальні індекси та індекси для швидких вибірок робочого листа
        mb.Entity<LabOrder>().HasIndex(o => o.OrderNumber).IsUnique();
        mb.Entity<LabOrder>().HasIndex(o => o.Status);
        mb.Entity<LabOrder>().HasIndex(o => o.PatientId);
        mb.Entity<LabOrder>().HasIndex(o => o.OrderDatetime);
        mb.Entity<LabOrder>().HasIndex(o => o.VerifyToken);
        mb.Entity<LabOrderSample>().HasIndex(s => s.Barcode).IsUnique();
        mb.Entity<LabOrderSample>().HasIndex(s => s.OrderId);
        mb.Entity<LabOrderTest>().HasIndex(t => t.OrderId);
        mb.Entity<LabOrderTest>().HasIndex(t => t.Status);
        mb.Entity<LabOrderTest>().HasIndex(t => t.TestCode);
        mb.Entity<LabTestResult>().HasIndex(r => r.OrderTestId).IsUnique();
        mb.Entity<LabTestResult>().HasIndex(r => r.Flag);
        mb.Entity<LabTestResult>().HasIndex(r => r.EnteredAt);
        mb.Entity<LabTestDefinition>().HasIndex(t => t.Code).IsUnique();
        mb.Entity<LabTestProfile>().HasIndex(p => p.Code).IsUnique();
        mb.Entity<LabReferenceLayer>().HasIndex(l => new { l.TestCode, l.MethodCode, l.PriorityOrder });
        mb.Entity<LabQcResult>().HasIndex(q => new { q.QcMaterialId, q.TestCode, q.RunAt });
        mb.Entity<LabAnalyzerLockout>().HasIndex(l => new { l.AnalyzerId, l.ResolvedAt });
        mb.Entity<LabAnalyzer>().HasIndex(a => a.Code).IsUnique();
        mb.Entity<LabConnectorInstallation>().HasIndex(c => c.ApiKeyHash);
        mb.Entity<LabConnectorInstallation>().HasIndex(c => c.InstallKey).IsUnique();
        mb.Entity<LabAuditLog>().HasIndex(a => new { a.Entity, a.EntityId });
        mb.Entity<LabAuditLog>().HasIndex(a => a.At);
        mb.Entity<LabArchiveCell>().HasIndex(c => new { c.RackId, c.RowNum, c.ColNum }).IsUnique();
        mb.Entity<LabEucastBreakpoint>().HasIndex(b => new { b.OrganismId, b.AntibioticId, b.EucastVersion }).IsUnique();
        mb.Entity<LabSection>().HasIndex(s => s.Code).IsUnique();
        mb.Entity<LabSectionJournalEntry>().HasIndex(j => new { j.LabSectionId, j.RegisteredAt });
        mb.Entity<LabSectionJournalEntry>().HasIndex(j => new { j.OrderId, j.LabSectionId, j.SampleId }).IsUnique();
        mb.Entity<LabSampleStageEvent>().HasIndex(e => new { e.SampleId, e.At });
        mb.Entity<LabOrderFavorite>().HasIndex(f => f.EmployeeId);
        mb.Entity<LabOrderSample>().HasOne(s => s.ParentSample).WithMany().HasForeignKey(s => s.ParentSampleId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LabOrderTest>().HasIndex(t => t.ReleasedAt);

        // [MedLink] персона та лабораторні атрибути завантажуються разом із карткою/співробітником/підрозділом
        mb.Entity<LabEhealthExchangeLog>().HasIndex(l => l.At);
        mb.Entity<LabOrder>().HasIndex(o => o.EhealthReferralId);
        mb.Entity<LabOrder>().HasIndex(o => o.ReferralType);
        mb.Entity<EheIncomingMedicalReferral>().HasIndex(r => r.RegNumber);
        mb.Entity<LabPerformer>().HasIndex(p => p.Code).IsUnique();
        mb.Entity<LabPerformerTest>().HasIndex(p => new { p.PerformerId, p.TestId }).IsUnique();
        mb.Entity<LabSendOut>().HasIndex(s => s.Number).IsUnique();
        mb.Entity<LabSendOutItem>().HasIndex(i => i.OrderTestId);
        mb.Entity<LabOrderAttachment>().HasIndex(a => a.OrderId);
        mb.Entity<LabOrderTest>().HasIndex(t => t.PerformerId);

        mb.Entity<OrgEmployee>().HasOne(e => e.LabSettings).WithOne(s => s.Employee).HasForeignKey<LabEmployeeSettings>(s => s.EmployeeId);
        mb.Entity<OrgDepartment>().HasOne(d => d.LabSettings).WithOne(s => s.Department).HasForeignKey<LabDepartmentSettings>(s => s.DepartmentId);
        mb.Entity<MisPatientCard>().Navigation(p => p.Person).AutoInclude();
        mb.Entity<OrgEmployee>().Navigation(e => e.Person).AutoInclude();
        mb.Entity<OrgEmployee>().Navigation(e => e.LabSettings).AutoInclude();
        mb.Entity<OrgDepartment>().Navigation(d => d.LabSettings).AutoInclude();
        mb.Entity<CmnPerson>().HasIndex(p => new { p.LastName, p.Name });
        mb.Entity<MisPatientCard>().HasIndex(p => p.PersonId);
        mb.Entity<MisPatientCard>().HasIndex(p => p.Caption);

        mb.Entity<LabOrderTest>().HasOne(t => t.Result).WithOne(r => r.OrderTest).HasForeignKey<LabTestResult>(r => r.OrderTestId);
        mb.Entity<LabOrder>().HasMany(o => o.Samples).WithOne(s => s.Order).HasForeignKey(s => s.OrderId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<LabOrder>().HasMany(o => o.Tests).WithOne(t => t.Order).HasForeignKey(t => t.OrderId).OnDelete(DeleteBehavior.Cascade);

        // Довідники з явними int-ключами (сід задає id)
        mb.Entity<LabBiomaterialType>().Property(e => e.Id).ValueGeneratedNever();
        mb.Entity<LabTubeType>().Property(e => e.Id).ValueGeneratedNever();
        mb.Entity<LabMethodType>().Property(e => e.Id).ValueGeneratedNever();
        mb.Entity<LabAnalyzerType>().Property(e => e.Id).ValueGeneratedNever();
        mb.Entity<LabMicroOrganism>().Property(e => e.Id).ValueGeneratedNever();
        mb.Entity<LabAntibiotic>().Property(e => e.Id).ValueGeneratedNever();

        var utcConverter = new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entity in mb.Model.GetEntityTypes())
        {
            // snake_case для таблиць без атрибуту [Table] та для всіх колонок
            var tableAttr = entity.ClrType.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.TableAttribute), false);
            if (tableAttr.Length == 0) entity.SetTableName(ToSnakeCase(entity.ClrType.Name));

            foreach (var prop in entity.GetProperties())
            {
                var colAttr = prop.PropertyInfo?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.ColumnAttribute), false);
                if (colAttr == null || colAttr.Length == 0) prop.SetColumnName(ToSnakeCase(prop.Name));
                if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utcConverter);
                if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNullableConverter);
                if (prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?)) prop.SetColumnType("NUMERIC(18,2)");
            }
            foreach (var key in entity.GetKeys()) key.SetName(ToSnakeCase($"pk_{entity.GetTableName()}"));
            foreach (var fk in entity.GetForeignKeys())
            {
                fk.SetConstraintName(ToSnakeCase($"fk_{entity.GetTableName()}_{string.Join("_", fk.Properties.Select(p => p.Name))}"));
                // Для довідкових зв'язків не каскадувати видалення
                if (fk.DeleteBehavior == DeleteBehavior.Cascade && !fk.IsRequired) fk.DeleteBehavior = DeleteBehavior.SetNull;
            }
            foreach (var ix in entity.GetIndexes())
                ix.SetDatabaseName(ToSnakeCase($"ix_{entity.GetTableName()}_{string.Join("_", ix.Properties.Select(p => p.Name))}"));
        }
    }

    public override int SaveChanges()
    {
        StampAudit();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Поточний користувач для службових колонок (виставляється middleware через CurrentEmployee).</summary>
    public string? CurrentUserId { get; set; }

    /// <summary>created_by/modified_by — uuid як в evomis: поточний співробітник або нульовий GUID для системних дій.</summary>
    private string AuditUserId => Guid.TryParse(CurrentUserId, out _) ? CurrentUserId! : MedLinkEnums.EmptyGuid;

    private void StampAudit()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedOn == default) entry.Entity.CreatedOn = now;
                if (string.IsNullOrEmpty(entry.Entity.CreatedBy) || entry.Entity.CreatedBy == MedLinkEnums.EmptyGuid) entry.Entity.CreatedBy = AuditUserId;
                if (string.IsNullOrEmpty(entry.Entity.ModifiedBy) || entry.Entity.ModifiedBy == MedLinkEnums.EmptyGuid) entry.Entity.ModifiedBy = AuditUserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedOn = now;
                entry.Entity.ModifiedBy = AuditUserId;
            }
        }
    }

    private static readonly Regex SnakeRegex = new("(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])", RegexOptions.Compiled);

    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var s = SnakeRegex.Replace(name, m => "_" + m.Value);
        return s.ToLowerInvariant().Replace("__", "_");
    }
}
