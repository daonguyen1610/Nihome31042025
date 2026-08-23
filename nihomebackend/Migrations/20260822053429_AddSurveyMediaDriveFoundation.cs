using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyMediaDriveFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DriveFolderId",
                table: "surveys",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriveFolderLink",
                table: "surveys",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "survey_checklist_results",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId = table.Column<int>(type: "int", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TemplateTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_checklist_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_survey_checklist_results_surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_media",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    RelativePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DriveFileId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DriveFolderId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DriveFolderLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SyncStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SyncAttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextSyncAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SyncError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SyncStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_media", x => x.Id);
                    table.ForeignKey(
                        name: "FK_survey_media_surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_survey_checklist_results_SurveyId_SortOrder",
                table: "survey_checklist_results",
                columns: new[] { "SurveyId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_checklist_results_SurveyId_TemplateCode",
                table: "survey_checklist_results",
                columns: new[] { "SurveyId", "TemplateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_media_ClaimToken",
                table: "survey_media",
                column: "ClaimToken");

            migrationBuilder.CreateIndex(
                name: "IX_survey_media_SurveyId_CreatedAt",
                table: "survey_media",
                columns: new[] { "SurveyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_media_SyncStatus_NextSyncAttemptAt",
                table: "survey_media",
                columns: new[] { "SyncStatus", "NextSyncAttemptAt" });

            migrationBuilder.Sql("""
                INSERT INTO survey_checklist_results
                    (SurveyId, TemplateCode, TemplateTitle, Status, Note, SortOrder,
                     CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId)
                SELECT survey.Id, template.Code, template.Title, NULL, NULL, template.SortOrder,
                       SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), NULL
                FROM surveys AS survey
                CROSS JOIN (VALUES
                    ('geology', N'Địa chất', 1),
                    ('electricity', N'Cấp điện', 2),
                    ('water-supply', N'Cấp nước', 3),
                    ('drainage', N'Thoát nước', 4),
                    ('site-access', N'Giao thông tiếp cận', 5),
                    ('surroundings', N'Xung quanh', 6),
                    ('preliminary-legal', N'Vướng mắc pháp lý sơ bộ', 7)
                ) AS template(Code, Title, SortOrder)
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM survey_checklist_results AS existing
                    WHERE existing.SurveyId = survey.Id
                      AND existing.TemplateCode = template.Code
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_checklist_results");

            migrationBuilder.DropTable(
                name: "survey_media");

            migrationBuilder.DropColumn(
                name: "DriveFolderId",
                table: "surveys");

            migrationBuilder.DropColumn(
                name: "DriveFolderLink",
                table: "surveys");
        }
    }
}
