using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using nihomebackend.Migrations;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public class VendorManagementMigrationTests
{
    [Fact]
    public void Hardening_PreflightsDuplicatesBeforeCreatingUniqueIndex()
    {
        var sql = GetSql(new HardenVendorManagement());

        sql.Should().Contain("THROW 51030");
        sql.Should().Contain("COL_LENGTH('procurement_vendors', 'RowVersion')");
        sql.IndexOf("THROW 51030", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("CREATE UNIQUE INDEX IX_procurement_vendors_CompanyName", StringComparison.Ordinal));
    }

    [Fact]
    public void UploadClaims_PreflightSharedPathsBeforeBackfill()
    {
        var sql = GetSql(new AddVendorDocumentUploadClaims());

        sql.Should().Contain("THROW 51031");
        sql.Should().Contain("INSERT INTO vendor_document_uploads");
        sql.Should().Contain("NEWID(), CapabilityFileUrl, Id, CreatedByUserId");
        sql.IndexOf("THROW 51031", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("INSERT INTO vendor_document_uploads", StringComparison.Ordinal));
    }

    private static string GetSql(Migration migration)
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [migrationBuilder]);
        return string.Join('\n', migrationBuilder.Operations.Select(operation => operation switch
        {
            SqlOperation sql => sql.Sql,
            _ => operation.GetType().Name,
        }));
    }
}