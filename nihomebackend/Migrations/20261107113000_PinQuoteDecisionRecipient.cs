using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107113000_PinQuoteDecisionRecipient")]
public partial class PinQuoteDecisionRecipient : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RecipientUserId",
            table: "quote_approval_emails",
            type: "int",
            nullable: true);

        // A stored address cannot establish the original recipient's identity.
        // Preserve the snapshot for manual review instead of guessing a user ID.
        migrationBuilder.Sql("""
            UPDATE [quote_approval_emails]
            SET [Status] = N'NeedsReview'
            WHERE [Kind] = N'Decision' AND [Status] = N'Pending'
                AND [RecipientUserId] IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RecipientUserId", table: "quote_approval_emails");
    }
}
