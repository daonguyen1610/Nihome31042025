using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCmScheduleBaselines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CmScheduleDelayThresholdPercent",
                table: "site_settings",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<string>(
                name: "CmScheduleEmailBodyTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CmScheduleEmailSubjectTemplate",
                table: "site_settings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CmScheduleBaselineId",
                table: "project_schedule_emails",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cm_schedule_baselines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CrmRevisionId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SiteManagerUserId = table.Column<int>(type: "int", nullable: false),
                    ApproverUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cm_schedule_baselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cm_schedule_baselines_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "cm_progress_snapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    BaselineId = table.Column<int>(type: "int", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CapturedByUserId = table.Column<int>(type: "int", nullable: true),
                    InputsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CrmPercent = table.Column<decimal>(type: "decimal(28,20)", precision: 28, scale: 20, nullable: false),
                    PlanPercent = table.Column<decimal>(type: "decimal(28,20)", precision: 28, scale: 20, nullable: false),
                    ActualPercent = table.Column<decimal>(type: "decimal(28,20)", precision: 28, scale: 20, nullable: false),
                    DelayPercent = table.Column<decimal>(type: "decimal(28,20)", precision: 28, scale: 20, nullable: false),
                    ThresholdPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    IsDelayed = table.Column<bool>(type: "bit", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecoveryPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RecoveryDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecoveryByUserId = table.Column<int>(type: "int", nullable: true),
                    RecoveryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cm_progress_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cm_progress_snapshots_cm_schedule_baselines_BaselineId",
                        column: x => x.BaselineId,
                        principalTable: "cm_schedule_baselines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cm_progress_snapshots_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_schedule_emails_CmScheduleBaselineId",
                table: "project_schedule_emails",
                column: "CmScheduleBaselineId");

            migrationBuilder.CreateIndex(
                name: "IX_cm_progress_snapshots_BaselineId_AsOfDate",
                table: "cm_progress_snapshots",
                columns: new[] { "BaselineId", "AsOfDate" });

            migrationBuilder.CreateIndex(
                name: "IX_cm_progress_snapshots_OperationalProjectId",
                table: "cm_progress_snapshots",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_cm_schedule_baselines_OperationalProjectId_Version",
                table: "cm_schedule_baselines",
                columns: new[] { "OperationalProjectId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_project_schedule_emails_cm_schedule_baselines_CmScheduleBaselineId",
                table: "project_schedule_emails",
                column: "CmScheduleBaselineId",
                principalTable: "cm_schedule_baselines",
                principalColumn: "Id");
            migrationBuilder.Sql("""
                UPDATE site_settings SET CmScheduleEmailSubjectTemplate = N'NICON · {{eventName}} · {{projectCode}} · CM v{{version}}', CmScheduleEmailBodyTemplate = N'<div style="font-family:Arial,sans-serif;max-width:640px;margin:auto;padding:24px;color:#17233b">
                <div style="font-size:20px;font-weight:700">NICON · KẾ HOẠCH CM · {{eventName}}</div>
                <p>Kính gửi {{recipientName}},</p>
                <p>Kế hoạch CM dự án <strong>{{projectCode}} · {{projectName}}</strong>, phiên bản {{version}} cần được duyệt.</p>
                <p><strong>Lý do:</strong> {{changeReason}}</p>
                <p style="white-space:pre-line">{{changeSummary}}</p>
                <p>Đăng nhập NICON, mở Tiến độ CRM – CM – BCH của dự án để kiểm tra và duyệt. Email chỉ thông báo, không thay thế phê duyệt trên hệ thống.</p>
                <p>Trân trọng,<br />NICON</p>
                </div>';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_project_schedule_emails_cm_schedule_baselines_CmScheduleBaselineId",
                table: "project_schedule_emails");

            migrationBuilder.DropTable(
                name: "cm_progress_snapshots");

            migrationBuilder.DropTable(
                name: "cm_schedule_baselines");

            migrationBuilder.DropIndex(
                name: "IX_project_schedule_emails_CmScheduleBaselineId",
                table: "project_schedule_emails");

            migrationBuilder.DropColumn(
                name: "CmScheduleDelayThresholdPercent",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "CmScheduleEmailBodyTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "CmScheduleEmailSubjectTemplate",
                table: "site_settings");

            migrationBuilder.DropColumn(
                name: "CmScheduleBaselineId",
                table: "project_schedule_emails");
        }
    }
}
