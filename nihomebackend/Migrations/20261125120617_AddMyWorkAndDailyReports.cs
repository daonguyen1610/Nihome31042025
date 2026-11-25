using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMyWorkAndDailyReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPriority",
                table: "department_work_tasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "daily_work_reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DepartmentRoleGroupId = table.Column<int>(type: "int", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Proposals = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TomorrowPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewComment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_work_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_daily_work_reports_role_groups_DepartmentRoleGroupId",
                        column: x => x.DepartmentRoleGroupId,
                        principalTable: "role_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_daily_work_reports_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_daily_work_reports_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_work_report_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_work_report_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_daily_work_report_items_daily_work_reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "daily_work_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_work_report_items_ReportId",
                table: "daily_work_report_items",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_daily_work_reports_DepartmentRoleGroupId_ReportDate",
                table: "daily_work_reports",
                columns: new[] { "DepartmentRoleGroupId", "ReportDate" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_work_reports_ReviewedByUserId",
                table: "daily_work_reports",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_daily_work_reports_UserId_ReportDate",
                table: "daily_work_reports",
                columns: new[] { "UserId", "ReportDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_work_report_items");

            migrationBuilder.DropTable(
                name: "daily_work_reports");

            migrationBuilder.DropColumn(
                name: "IsPriority",
                table: "department_work_tasks");
        }
    }
}
