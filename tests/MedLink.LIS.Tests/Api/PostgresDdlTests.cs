// DDL PostgreSQL для перенесення в MedLink: [ЛІС]-таблиці створюються, [MedLink]-таблиці — ні; типи як в evomis;
// закомічений db/postgres/medlink_lis_schema.sql відповідає поточній EF-моделі
using MedLink.LIS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Tests.Api;

public class PostgresDdlTests
{
    private static string Generate()
    {
        var options = new DbContextOptionsBuilder<LisDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var db = new LisDbContext(options);
        return PostgresDdlGenerator.Generate(db);
    }

    private static string WithoutDate(string ddl) =>
        string.Join("\n", ddl.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith("-- Дата генерації")));

    [Fact]
    public void Lis_tables_are_created_medlink_tables_are_only_referenced()
    {
        var ddl = Generate();
        Assert.Contains("CREATE TABLE IF NOT EXISTS lab_order (", ddl);
        Assert.Contains("CREATE TABLE IF NOT EXISTS lab_employee_settings (", ddl);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS mis_patient_card", ddl);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS mis_diagnostic_report", ddl);
        Assert.Contains("-- [MedLink-new] mis_patient_insurance", ddl); // нова загальна сутність МІС — створюється
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS org_", ddl);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS cmn_", ddl);
        Assert.Contains("ALTER TABLE mis_patient_card ADD COLUMN IF NOT EXISTS last_name_latin varchar(100); -- [MedLink+]", ddl);
        Assert.Contains("-- [MedLink] cmn_person:", ddl);
        // Типи evomis
        Assert.Contains("    patient_id uuid NOT NULL", ddl);
        Assert.Contains("    created_by uuid NOT NULL", ddl);
        Assert.Contains("    record_state integer NOT NULL DEFAULT 2", ddl);
        Assert.Contains("    order_datetime timestamp without time zone NOT NULL", ddl);
        Assert.Contains("REFERENCES mis_patient_card (id)", ddl);
        Assert.Contains("REFERENCES org_employee (id) ON DELETE CASCADE", ddl);
        Assert.DoesNotContain("is_deleted", ddl);
    }

    [Fact]
    public void Committed_schema_matches_model()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MedLink.LIS.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var committed = File.ReadAllText(Path.Combine(dir!.FullName, "db", "postgres", "medlink_lis_schema.sql"));
        Assert.True(WithoutDate(committed) == WithoutDate(Generate()),
            "db/postgres/medlink_lis_schema.sql застарів — перегенеруйте: dotnet run --project src/MedLink.LIS.Api -- --export-pg-ddl <repo>/db/postgres/medlink_lis_schema.sql");
    }
}
