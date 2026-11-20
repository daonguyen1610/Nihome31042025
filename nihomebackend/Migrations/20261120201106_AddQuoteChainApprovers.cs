using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteChainApprovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_chain_approvers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuoteId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_chain_approvers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_chain_approvers_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quote_chain_approvers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_quote_chain_approvers_QuoteId_UserId",
                table: "quote_chain_approvers",
                columns: new[] { "QuoteId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quote_chain_approvers_UserId",
                table: "quote_chain_approvers",
                column: "UserId");

            // Quotes already sent with a chain keep their approvers' access: one row per person,
            // read from the chain fixed at submission. Only existing users are linked.
            migrationBuilder.Sql(@"
INSERT INTO quote_chain_approvers (QuoteId, UserId)
SELECT DISTINCT q.Id, step.approverUserId
FROM quotes q
CROSS APPLY OPENJSON(q.ApprovalStepsJson) WITH (approverUserId int '$.approverUserId') step
WHERE q.ApprovalStepsJson IS NOT NULL AND ISJSON(q.ApprovalStepsJson) = 1
  AND step.approverUserId IS NOT NULL
  AND EXISTS (SELECT 1 FROM users u WHERE u.Id = step.approverUserId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_chain_approvers");
        }
    }
}
