using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoverRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "handover_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    HandoverCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PlannedHandoverDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualHandoverDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ResponsibleUserId = table.Column<int>(type: "int", nullable: false),
                    CommissioningCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CommissioningNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ChecklistItems = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    ChecklistCompleted = table.Column<bool>(type: "bit", nullable: false),
                    Documents = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Signatories = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    HandedOverAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandedOverByUserId = table.Column<int>(type: "int", nullable: true),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handover_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_handover_records_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_handover_records_users_HandedOverByUserId",
                        column: x => x.HandedOverByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_handover_records_users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_handover_records_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "handover_status_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HandoverRecordId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handover_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_handover_status_history_handover_records_HandoverRecordId",
                        column: x => x.HandoverRecordId,
                        principalTable: "handover_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_handover_status_history_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_handover_records_DesignProjectId",
                table: "handover_records",
                column: "DesignProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_handover_records_HandedOverByUserId",
                table: "handover_records",
                column: "HandedOverByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_handover_records_HandoverCode",
                table: "handover_records",
                column: "HandoverCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_handover_records_ResponsibleUserId",
                table: "handover_records",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_handover_records_Status",
                table: "handover_records",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_handover_records_SubmittedByUserId",
                table: "handover_records",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_handover_status_history_ChangedByUserId",
                table: "handover_status_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_handover_status_history_HandoverRecordId_ChangedAt",
                table: "handover_status_history",
                columns: new[] { "HandoverRecordId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "handover_status_history");

            migrationBuilder.DropTable(
                name: "handover_records");
        }
    }
}
