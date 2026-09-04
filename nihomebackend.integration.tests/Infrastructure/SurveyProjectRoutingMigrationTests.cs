using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using nihomebackend.Migrations;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public class SurveyProjectRoutingMigrationTests
{
    [Theory]
    [InlineData(typeof(CompleteModule1CrmPreDesign))]
    [InlineData(typeof(FinalizeModule1NewOnly))]
    public void Upgrade_BackfillsPreLeadSurveyIntoIsolatedLegacyAggregate(Type migrationType)
    {
        var sql = GetSql(migrationType);

        sql.Should().Contain("PJ-LEGACY-SV-");
        sql.Should().Contain("MIGRATION:CompleteModule1:Survey:");
        sql.Should().Contain("INSERT INTO customers");
        sql.Should().Contain("INSERT INTO operational_projects");
        sql.Should().Contain("WHERE survey.OperationalProjectId IS NULL");
        sql.Should().Contain("NOT EXISTS");
        sql.Should().Contain("SET OperationalProjectId = project.Id");
        sql.Should().Contain("project.Note LIKE CONCAT(");
    }

    [Fact]
    public void HistoricalValidation_AllowsPreLeadSurveyButRejectsInvalidProjectRouting()
    {
        var sql = GetSql(typeof(ValidateHistoricalSurveyProjectRouting));

        sql.Should().Contain("project.Id IS NULL");
        sql.Should().Contain("survey.LinkedOpportunityId IS NOT NULL");
        sql.Should().Contain("survey.OperationalProjectId <> opportunity.OperationalProjectId");
        sql.Should().Contain("project.CustomerId <> opportunity.CustomerId");
        sql.Should().NotContain("WHERE opportunity.Id IS NULL");
    }

    private static string GetSql(Type migrationType)
    {
        var migration = (Migration)Activator.CreateInstance(migrationType)!;
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migrationType.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [migrationBuilder]);
        return string.Join('\n', migrationBuilder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
    }
}
