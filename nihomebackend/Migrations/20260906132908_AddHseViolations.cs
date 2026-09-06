using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddHseViolations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hse_violations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OfflineClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RegulatoryReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvidenceDocumentsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    ResponsibleSiteUserId = table.Column<int>(type: "int", nullable: false),
                    RemediationOwnerUserId = table.Column<int>(type: "int", nullable: true),
                    RemediationDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemediationNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PenaltyReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportedByUserId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hse_violations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hse_violations_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hse_violations_users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_hse_violations_users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_hse_violations_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_hse_violations_users_RemediationOwnerUserId",
                        column: x => x.RemediationOwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_hse_violations_users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_hse_violations_users_ResponsibleSiteUserId",
                        column: x => x.ResponsibleSiteUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_hse_violations_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "hse_violation_events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HseViolationId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hse_violation_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hse_violation_events_hse_violations_HseViolationId",
                        column: x => x.HseViolationId,
                        principalTable: "hse_violations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hse_violation_events_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_hse_violation_events_CreatedByUserId",
                table: "hse_violation_events",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hse_violation_events_HseViolationId_CreatedAt",
                table: "hse_violation_events",
                columns: new[] { "HseViolationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_hse_violations_ClosedByUserId",
                table: "hse_violations",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hse_violations_ConfirmedByUserId",
                table: "hse_violations",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hse_violations_CreatedByUserId",
                table: "hse_violations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hse_violations_OperationalProjectId_Code",
                table: "hse_violations",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hse_violations_OperationalProjectId_OfflineClientId",
                table: "hse_violations",
                columns: new[] { "OperationalProjectId", "OfflineClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hse_violations_RemediationOwnerUserId",
                table: "hse_violations",
                column: "RemediationOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hse_violations_ReportedByUserId",
                table: "hse_violations",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_hse_violations_ResponsibleSiteUserId_ConfirmedAt_Status",
                table: "hse_violations",
                columns: new[] { "ResponsibleSiteUserId", "ConfirmedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_hse_violations_UpdatedByUserId",
                table: "hse_violations",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hse_violation_events");

            migrationBuilder.DropTable(
                name: "hse_violations");
        }
    }
}
