using Microsoft.EntityFrameworkCore;
using MedLink.LIS.Api.Models;

namespace MedLink.LIS.Api.Data
{
    public class MedLinkLabDbContext : DbContext
    {
        public MedLinkLabDbContext(DbContextOptions<MedLinkLabDbContext> options)
            : base(options)
        {
        }

        public DbSet<LabOrder> Orders => Set<LabOrder>();
        public DbSet<LabOrderSample> Samples => Set<LabOrderSample>();
        public DbSet<LabTestResult> Results => Set<LabTestResult>();
        public DbSet<LabReferenceLayer> ReferenceLayers => Set<LabReferenceLayer>();
        public DbSet<LabQcResult> QcResults => Set<LabQcResult>();
        public DbSet<LabSampleArchiveCell> ArchiveCells => Set<LabSampleArchiveCell>();
        public DbSet<LabPanicCallLog> PanicCallLogs => Set<LabPanicCallLog>();
        public DbSet<LabBiomaterialType> BiomaterialTypes => Set<LabBiomaterialType>();
        public DbSet<LabTubeType> TubeTypes => Set<LabTubeType>();
        public DbSet<LabAnalyzer> Analyzers => Set<LabAnalyzer>();
        public DbSet<LabTestDefinition> TestDefinitions => Set<LabTestDefinition>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indexes for fast Delphi multi-layer queries
            modelBuilder.Entity<LabReferenceLayer>()
                .HasIndex(r => new { r.TestCode, r.MethodCode, r.PriorityOrder });

            modelBuilder.Entity<LabOrderSample>()
                .HasIndex(s => s.Barcode)
                .IsUnique();

            modelBuilder.Entity<LabQcResult>()
                .HasIndex(q => new { q.TestCode, q.IsLockout });
        }
    }
}
