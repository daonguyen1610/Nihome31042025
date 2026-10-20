using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddContractReminderEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractEndEmailBodyTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractEndEmailSubjectTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractRenewalEmailBodyTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractRenewalEmailSubjectTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contract_reminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReminderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContactId = table.Column<int>(type: "int", nullable: false),
                    ToEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    ContextHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PreviewHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_reminders_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contract_reminders_ContractId_Kind_Status",
                table: "contract_reminders",
                columns: new[] { "ContractId", "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_reminders_PreviewHash",
                table: "contract_reminders",
                column: "PreviewHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_reminders_Status_ReminderDate",
                table: "contract_reminders",
                columns: new[] { "Status", "ReminderDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM contract_reminders)
                    OR EXISTS (SELECT 1 FROM site_settings WHERE
                        ContractRenewalEmailSubjectTemplate IS NOT NULL OR ContractRenewalEmailBodyTemplate IS NOT NULL OR
                        ContractEndEmailSubjectTemplate IS NOT NULL OR ContractEndEmailBodyTemplate IS NOT NULL)
                BEGIN
                    ;THROW 51000, 'Export and explicitly remove contract reminder history/templates before rollback.', 1;
                END
                """);
            migrationBuilder.DropTable(
                name: "contract_reminders");

            migrationBuilder.DropColumn(
                name: "ContractEndEmailBodyTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "ContractEndEmailSubjectTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "ContractRenewalEmailBodyTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "ContractRenewalEmailSubjectTemplate",
                table: "site_settings");
        }
    }
}
