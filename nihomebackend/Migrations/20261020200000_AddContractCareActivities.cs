using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261020200000_AddContractCareActivities")]
public sealed class AddContractCareActivities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "contract_care_activities",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ContractId = table.Column<int>(type: "int", nullable: false),
                Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_contract_care_activities", item => item.Id);
                table.ForeignKey("FK_contract_care_activities_contracts_ContractId", item => item.ContractId,
                    "contracts", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_contract_care_activities_users_CreatedByUserId", item => item.CreatedByUserId,
                    "users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(
            name: "IX_contract_care_activities_ContractId_OccurredAt_Id", table: "contract_care_activities",
            columns: new[] { "ContractId", "OccurredAt", "Id" });
        migrationBuilder.CreateIndex(
            name: "IX_contract_care_activities_CreatedByUserId", table: "contract_care_activities",
            column: "CreatedByUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM contract_care_activities)
            BEGIN
                ;THROW 51000, 'Rollback blocked while contract customer-care history exists.', 1;
            END
            """);
        migrationBuilder.DropTable(name: "contract_care_activities");
    }
}
