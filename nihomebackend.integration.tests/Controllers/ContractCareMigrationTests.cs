using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ContractCareMigrationTests
{
    [Fact]
    public void Migration_GeneratesAdditiveSqlAndProtectsHistoryOnRollback()
    {
        // SQL generation does not connect to or modify a database.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=migration-script-only;Database=NiconMigrationReview;Integrated Security=true;TrustServerCertificate=true")
            .Options);
        var migrator = db.GetService<IMigrator>();
        var up = migrator.GenerateScript("20261020183000_AddPaymentRequestRevisions", "20261020200000_AddContractCareActivities");
        up.Should().Contain("CREATE TABLE [contract_care_activities]");
        up.Should().Contain("REFERENCES [contracts] ([Id]) ON DELETE CASCADE");
        up.Should().Contain("REFERENCES [users] ([Id])");
        up.Should().Contain("IX_contract_care_activities_ContractId_OccurredAt_Id");
        up.Should().NotContain("DROP TABLE");
        var down = migrator.GenerateScript("20261020200000_AddContractCareActivities", "20261020183000_AddPaymentRequestRevisions");
        down.Should().Contain("IF EXISTS (SELECT 1 FROM contract_care_activities)");
        down.Should().Contain("THROW 51000");
        down.IndexOf("THROW 51000", StringComparison.Ordinal).Should().BeLessThan(down.IndexOf("DROP TABLE", StringComparison.Ordinal));
    }
}
