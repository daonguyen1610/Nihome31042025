using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using nihomebackend.Migrations;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public class OperationalProjectBackfillMigrationTests
{
    [Fact]
    public void Migration_PreflightsAmbiguousMappingsBeforeChangingData()
    {
        var sql = GetSql();

        sql.IndexOf("THROW 51020", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("UPDATE designProject", StringComparison.Ordinal));
        sql.Should().Contain("COUNT(DISTINCT contract.OperationalProjectId) > 1");
        sql.Should().Contain("COUNT(DISTINCT candidate.OperationalProjectId) > 1");
        sql.Should().Contain("SELECT quote.OpportunityId, quote.OperationalProjectId");
        sql.Should().Contain("THROW 51024");
        sql.Should().Contain("designProject.CustomerId <> contract.CustomerId");
        sql.Should().Contain("contract.CustomerId <> opportunity.CustomerId");
        sql.Should().Contain("THROW 51028");
        sql.Should().Contain("designProject.ContractId IS NOT NULL AND contract.Id IS NULL");
        sql.Should().Contain("contract.QuoteId IS NOT NULL AND quote.Id IS NULL");
        sql.Should().Contain("THROW 51029");
    }

    [Fact]
    public void Migration_UsesStableCodesAndIdempotentInsertGuards()
    {
        var sql = GetSql();

        sql.Should().Contain("PJ-MIG-DP-");
        sql.Should().Contain("PJ-MIG-OP-");
        sql.Should().Contain("PJ-MIG-CT-");
        sql.Should().Contain("MIGRATION:NIH-465:DesignProject:");
        sql.Should().Contain("MIGRATION:NIH-465:Opportunity:");
        sql.Should().Contain("MIGRATION:NIH-465:Contract:");
        sql.Split("NOT EXISTS", StringSplitOptions.None).Length.Should().BeGreaterThan(3);
    }

    [Fact]
    public void Migration_VerifiesCountsCompletenessAndCrossEntityIntegrity()
    {
        var sql = GetSql();

        sql.Should().Contain("@DesignProjectCount");
        sql.Should().Contain("@ContractCount");
        sql.Should().Contain("@OpportunityCount");
        sql.Should().Contain("@QuoteCount");
        sql.Should().Contain("one or more historical records remain unmapped");
        sql.Should().Contain("post-migration integrity validation failed");
        sql.Should().Contain("designProject.OperationalProjectId <> contract.OperationalProjectId");
        sql.Should().Contain("contract.OperationalProjectId <> quote.OperationalProjectId");
        sql.Should().Contain("quote.OperationalProjectId <> opportunity.OperationalProjectId");
    }

    private static string GetSql()
    {
        var migration = new ReconcileOperationalProjectBackfill();
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(ReconcileOperationalProjectBackfill)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [migrationBuilder]);
        return string.Join('\n', migrationBuilder.Operations
            .OfType<SqlOperation>()
            .Select(operation => operation.Sql));
    }
}
