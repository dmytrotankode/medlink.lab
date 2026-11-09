// EF-контекст модуля Guide: окрема схема "guide", таблиці gd_*, колонки snake_case.
// Провайдер-незалежний: jsonb/uuid/timestamp-конвертери застосовуються лише для Npgsql.
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MedLink.Guide;

public class GuideDbContext : DbContext
{
    private readonly GuideOptions _options;

    public GuideDbContext(DbContextOptions<GuideDbContext> options, GuideOptions guideOptions) : base(options) => _options = guideOptions;

    /// <summary>uuid поточного користувача для аудит-колонок (задається сервісом/middleware).</summary>
    public string CurrentUserId { get; set; } = GuideConst.EmptyGuid;

    public DbSet<GuideApp> Apps => Set<GuideApp>();
    public DbSet<GuideFolder> Folders => Set<GuideFolder>();
    public DbSet<GuideSet> Sets => Set<GuideSet>();
    public DbSet<GuideSetItem> SetItems => Set<GuideSetItem>();
    public DbSet<GuideAnchor> Anchors => Set<GuideAnchor>();
    public DbSet<GuideScenario> Scenarios => Set<GuideScenario>();
    public DbSet<GuideScenarioVariable> Variables => Set<GuideScenarioVariable>();
    public DbSet<GuideStep> Steps => Set<GuideStep>();
    public DbSet<GuideScenarioVersion> Versions => Set<GuideScenarioVersion>();
    public DbSet<GuideMedia> Media => Set<GuideMedia>();
    public DbSet<GuideProgress> Progress => Set<GuideProgress>();
    public DbSet<GuideAnchorReport> AnchorReports => Set<GuideAnchorReport>();

    public static string ToSnake(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c)) { if (i > 0) sb.Append('_'); sb.Append(char.ToLowerInvariant(c)); }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        var npgsql = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        // SQLite не підтримує схеми — лише PostgreSQL та інші реляційні СУБД
        if (!string.IsNullOrWhiteSpace(_options.Schema) && !(Database.ProviderName ?? "").Contains("Sqlite", StringComparison.OrdinalIgnoreCase)) b.HasDefaultSchema(_options.Schema);

        b.Entity<GuideApp>().ToTable("gd_app");
        b.Entity<GuideFolder>().ToTable("gd_folder");
        b.Entity<GuideSet>().ToTable("gd_set");
        b.Entity<GuideSetItem>().ToTable("gd_set_item");
        b.Entity<GuideAnchor>().ToTable("gd_anchor");
        b.Entity<GuideScenario>().ToTable("gd_scenario");
        b.Entity<GuideScenarioVariable>().ToTable("gd_scenario_variable");
        b.Entity<GuideStep>().ToTable("gd_step");
        b.Entity<GuideScenarioVersion>().ToTable("gd_scenario_version");
        b.Entity<GuideMedia>().ToTable("gd_media");
        b.Entity<GuideProgress>().ToTable("gd_progress");
        b.Entity<GuideAnchorReport>().ToTable("gd_anchor_report");

        // Soft delete: record_state = 4 ховається глобальним фільтром
        b.Entity<GuideApp>().HasQueryFilter(e => e.RecordState == RecordStates.Active);
        b.Entity<GuideFolder>().HasQueryFilter(e => e.RecordState == RecordStates.Active);
        b.Entity<GuideSet>().HasQueryFilter(e => e.RecordState == RecordStates.Active);
        b.Entity<GuideAnchor>().HasQueryFilter(e => e.RecordState == RecordStates.Active);
        b.Entity<GuideScenario>().HasQueryFilter(e => e.RecordState == RecordStates.Active);
        b.Entity<GuideScenarioVariable>().HasQueryFilter(e => e.RecordState == RecordStates.Active);
        b.Entity<GuideStep>().HasQueryFilter(e => e.RecordState == RecordStates.Active);
        b.Entity<GuideMedia>().HasQueryFilter(e => e.RecordState == RecordStates.Active);

        b.Entity<GuideApp>().HasIndex(e => e.Code).IsUnique().HasDatabaseName("ux_gd_app_code");
        b.Entity<GuideFolder>().HasIndex(e => new { e.AppId, e.ParentId, e.SortOrder }).HasDatabaseName("ix_gd_folder_app_parent");
        b.Entity<GuideSet>().HasIndex(e => new { e.AppId, e.SortOrder }).HasDatabaseName("ix_gd_set_app");
        b.Entity<GuideSetItem>().HasIndex(e => new { e.SetId, e.SortOrder }).HasDatabaseName("ix_gd_set_item_set");
        b.Entity<GuideAnchor>().HasIndex(e => new { e.AppId, e.RecordState }).HasDatabaseName("ix_gd_anchor_app");
        b.Entity<GuideScenario>().HasIndex(e => new { e.AppId, e.FolderId, e.SortOrder }).HasDatabaseName("ix_gd_scenario_app_folder");
        b.Entity<GuideStep>().HasIndex(e => new { e.ScenarioId, e.SortOrder }).HasDatabaseName("ix_gd_step_scenario");
        b.Entity<GuideScenarioVersion>().HasIndex(e => new { e.ScenarioId, e.VersionNo }).IsUnique().HasDatabaseName("ux_gd_version");
        b.Entity<GuideProgress>().HasIndex(e => new { e.EmployeeId, e.ScenarioId }).HasDatabaseName("ix_gd_progress_user");
        b.Entity<GuideAnchorReport>().HasIndex(e => new { e.AnchorId, e.Resolved }).HasDatabaseName("ix_gd_report_anchor");

        b.Entity<GuideStep>().HasOne<GuideScenario>().WithMany().HasForeignKey(e => e.ScenarioId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<GuideScenarioVariable>().HasOne<GuideScenario>().WithMany().HasForeignKey(e => e.ScenarioId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<GuideStep>().HasOne<GuideAnchor>().WithMany().HasForeignKey(e => e.AnchorId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<GuideAnchorReport>().Property(e => e.Confidence).HasPrecision(4, 3);

        var jsonColumns = new HashSet<string>
        {
            "gd_anchor.Strategies", "gd_anchor.Fingerprint", "gd_scenario.RequiredRoles", "gd_scenario.Tags",
            "gd_scenario_variable.Options", "gd_step.Rect", "gd_scenario_version.Snapshot",
            "gd_anchor_report.NewStrategies", "gd_anchor_report.NewFingerprint"
        };
        var toGuid = new ValueConverter<string, Guid>(s => Guid.Parse(s), g => g.ToString());
        var toDbTime = new ValueConverter<DateTime, DateTime>(d => DateTime.SpecifyKind(d, DateTimeKind.Unspecified), d => DateTime.SpecifyKind(d, DateTimeKind.Utc));

        foreach (var et in b.Model.GetEntityTypes())
        {
            var table = et.GetTableName()!;
            foreach (var p in et.GetProperties())
            {
                p.SetColumnName(ToSnake(p.Name));
                if (!npgsql) continue;
                if (p.ClrType == typeof(string) && (p.Name == "Id" || p.Name.EndsWith("Id", StringComparison.Ordinal) || p.Name is "CreatedBy" or "ModifiedBy" or "PublishedBy"))
                {
                    p.SetColumnType("uuid");
                    p.SetValueConverter(toGuid);
                }
                else if (p.ClrType == typeof(string) && jsonColumns.Contains($"{table}.{p.Name}")) p.SetColumnType("jsonb");
                else if (p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
                {
                    p.SetColumnType("timestamp without time zone");
                    p.SetValueConverter(toDbTime);
                }
                else if (p.ClrType == typeof(byte[])) p.SetColumnType("bytea");
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var e in ChangeTracker.Entries<GuideAuditable>())
        {
            if (e.State == EntityState.Added)
            {
                e.Entity.CreatedOn = now;
                if (e.Entity.CreatedBy == GuideConst.EmptyGuid) e.Entity.CreatedBy = CurrentUserId;
                if (e.Entity.ModifiedBy == GuideConst.EmptyGuid) e.Entity.ModifiedBy = CurrentUserId;
            }
            else if (e.State == EntityState.Modified)
            {
                e.Entity.ModifiedOn = now;
                e.Entity.ModifiedBy = CurrentUserId;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
