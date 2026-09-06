using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivableAccountability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "contract_payment_milestones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponsibleAccountantUserId",
                table: "contract_payment_milestones",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contract_payment_milestone_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractPaymentMilestoneId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_payment_milestone_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_payment_milestone_events_contract_payment_milestones_ContractPaymentMilestoneId",
                        column: x => x.ContractPaymentMilestoneId,
                        principalTable: "contract_payment_milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_payment_milestone_events_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_contract_payment_milestones_ResponsibleAccountantUserId_DueDate",
                table: "contract_payment_milestones",
                columns: new[] { "ResponsibleAccountantUserId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_payment_milestone_events_ChangedByUserId",
                table: "contract_payment_milestone_events",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_payment_milestone_events_ContractPaymentMilestoneId_ChangedAt",
                table: "contract_payment_milestone_events",
                columns: new[] { "ContractPaymentMilestoneId", "ChangedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_contract_payment_milestones_users_ResponsibleAccountantUserId",
                table: "contract_payment_milestones",
                column: "ResponsibleAccountantUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contract_payment_milestones_users_ResponsibleAccountantUserId",
                table: "contract_payment_milestones");

            migrationBuilder.DropTable(
                name: "contract_payment_milestone_events");

            migrationBuilder.DropIndex(
                name: "IX_contract_payment_milestones_ResponsibleAccountantUserId_DueDate",
                table: "contract_payment_milestones");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "contract_payment_milestones");

            migrationBuilder.DropColumn(
                name: "ResponsibleAccountantUserId",
                table: "contract_payment_milestones");
        }
    }
}
