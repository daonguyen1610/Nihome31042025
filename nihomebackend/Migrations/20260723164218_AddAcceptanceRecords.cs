using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAcceptanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acceptance_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    AcceptanceCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ConstructionTaskId = table.Column<int>(type: "int", nullable: true),
                    AcceptanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Participants = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Findings = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Documents = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    RevisionCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acceptance_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_acceptance_records_construction_tasks_ConstructionTaskId",
                        column: x => x.ConstructionTaskId,
                        principalTable: "construction_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_acceptance_records_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_acceptance_records_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_acceptance_records_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_acceptance_records_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_acceptance_records_ApprovedByUserId",
                table: "acceptance_records",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_acceptance_records_ConstructionTaskId",
                table: "acceptance_records",
                column: "ConstructionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_acceptance_records_DesignProjectId",
                table: "acceptance_records",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_acceptance_records_DesignProjectId_AcceptanceCode",
                table: "acceptance_records",
                columns: new[] { "DesignProjectId", "AcceptanceCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_acceptance_records_RejectedByUserId",
                table: "acceptance_records",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_acceptance_records_Status",
                table: "acceptance_records",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_acceptance_records_SubmittedByUserId",
                table: "acceptance_records",
                column: "SubmittedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acceptance_records");
        }
    }
}
