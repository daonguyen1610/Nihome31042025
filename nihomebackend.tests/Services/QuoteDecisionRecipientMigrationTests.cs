using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

namespace nihomebackend.tests.Services;

public sealed class QuoteDecisionRecipientMigrationTests
{
    [Fact]
    public void Migration_PinsNewRecipientsAndHoldsUnidentifiedPendingDecisions()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True")
            .Options);
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261107103000_AddDesignScheduleDisciplineCode";
        const string current = "20261107113000_PinQuoteDecisionRecipient";

        var upgrade = migrator.GenerateScript(previous, current);
        Assert.Contains("ADD [RecipientUserId] int NULL", upgrade);
        Assert.Contains("[Kind] = N'Decision' AND [Status] = N'Pending'", upgrade);
        Assert.Contains("[RecipientUserId] IS NULL", upgrade);
        Assert.Contains("SET [Status] = N'NeedsReview'", upgrade);
        Assert.False(db.Database.HasPendingModelChanges());

        var rollback = migrator.GenerateScript(current, previous);
        Assert.Contains("DROP COLUMN [RecipientUserId]", rollback);
        Assert.DoesNotContain("SET [Status]", rollback);
    }
}
