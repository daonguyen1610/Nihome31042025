using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectInvestmentRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectInvestmentRateRevisionId",
                table: "quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectInvestmentRateRevisionId",
                table: "quote_version_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_investment_rate_revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    SourceWorkPackageId = table.Column<int>(type: "int", nullable: true),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceWorkbookPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ConvertedAreaSqm = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RatePerSqm = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AreaLinesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GroupLinesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreparedByUserId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_investment_rate_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_investment_rate_revisions_department_work_packages_SourceWorkPackageId",
                        column: x => x.SourceWorkPackageId,
                        principalTable: "department_work_packages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_project_investment_rate_revisions_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_investment_rate_revisions_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_project_investment_rate_revisions_users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_project_investment_rate_revisions_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_project_investment_rate_revisions_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_quotes_ProjectInvestmentRateRevisionId",
                table: "quotes",
                column: "ProjectInvestmentRateRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_project_investment_rate_revisions_ApprovedByUserId",
                table: "project_investment_rate_revisions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_investment_rate_revisions_OperationalProjectId_RevisionNumber",
                table: "project_investment_rate_revisions",
                columns: new[] { "OperationalProjectId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_investment_rate_revisions_OperationalProjectId_Status_RevisionNumber",
                table: "project_investment_rate_revisions",
                columns: new[] { "OperationalProjectId", "Status", "RevisionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_project_investment_rate_revisions_PreparedByUserId",
                table: "project_investment_rate_revisions",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_investment_rate_revisions_RejectedByUserId",
                table: "project_investment_rate_revisions",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_investment_rate_revisions_SourceWorkPackageId",
                table: "project_investment_rate_revisions",
                column: "SourceWorkPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_project_investment_rate_revisions_SubmittedByUserId",
                table: "project_investment_rate_revisions",
                column: "SubmittedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_quotes_project_investment_rate_revisions_ProjectInvestmentRateRevisionId",
                table: "quotes",
                column: "ProjectInvestmentRateRevisionId",
                principalTable: "project_investment_rate_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quotes_project_investment_rate_revisions_ProjectInvestmentRateRevisionId",
                table: "quotes");

            migrationBuilder.DropTable(
                name: "project_investment_rate_revisions");

            migrationBuilder.DropIndex(
                name: "IX_quotes_ProjectInvestmentRateRevisionId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ProjectInvestmentRateRevisionId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ProjectInvestmentRateRevisionId",
                table: "quote_version_snapshots");
        }
    }
}
