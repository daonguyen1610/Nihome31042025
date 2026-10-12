using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class CompleteTenderSubmissionEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationReceivedAt",
                table: "tenders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationReference",
                table: "tenders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "tenders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "tender_checklist_items",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletedByUserId",
                table: "tender_checklist_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "tender_checklist_items",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "NotApplicableReason",
                table: "tender_checklist_items",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Existing packages predate explicit invitation-receipt metadata. CreatedAt is
            // the only auditable receipt proxy available; clamp it below the deadline for
            // malformed legacy records instead of inventing a customer reference number.
            migrationBuilder.Sql("""
                UPDATE [tenders]
                SET [InvitationReceivedAt] = CASE
                    WHEN [CreatedAt] < [SubmissionDeadline] THEN [CreatedAt]
                    ELSE DATEADD(second, -1, [SubmissionDeadline])
                END
                WHERE [InvitationReceivedAt] IS NULL;
                """);

            // Add the newly standardised, optional tender-drawing row to open packages.
            // Append it after any existing custom rows so the legacy unique sort index is
            // never disturbed.
            migrationBuilder.Sql("""
                INSERT INTO [tender_checklist_items]
                    ([TenderId], [TemplateCode], [Title], [Status], [IsRequired],
                     [OwnerUserId], [InternalDeadline], [FilePath], [OriginalFileName],
                     [SortOrder], [NotApplicableReason], [CompletedAt],
                     [CompletedByUserId], [CreatedAt], [UpdatedAt])
                SELECT t.[Id], N'drawings', N'Bản vẽ dự thầu', N'NotStarted', 0,
                       NULL, NULL, NULL, NULL,
                       COALESCE(MAX(ci.[SortOrder]), 0) + 1, NULL, NULL, NULL,
                       SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [tenders] t
                LEFT JOIN [tender_checklist_items] ci ON ci.[TenderId] = t.[Id]
                WHERE t.[Status] = N'Preparing'
                  AND NOT EXISTS (
                      SELECT 1 FROM [tender_checklist_items] existing
                      WHERE existing.[TenderId] = t.[Id]
                        AND existing.[TemplateCode] = N'drawings')
                GROUP BY t.[Id];
                """);

            migrationBuilder.CreateTable(
                name: "tender_submissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedEstimateRevisionId = table.Column<int>(type: "int", nullable: false),
                    FinanceReviewWorkPackageId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tender_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tender_submissions_department_work_packages_FinanceReviewWorkPackageId",
                        column: x => x.FinanceReviewWorkPackageId,
                        principalTable: "department_work_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tender_submissions_tender_estimate_revisions_ApprovedEstimateRevisionId",
                        column: x => x.ApprovedEstimateRevisionId,
                        principalTable: "tender_estimate_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tender_submissions_tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tender_submissions_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tender_submission_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderSubmissionId = table.Column<int>(type: "int", nullable: false),
                    ChecklistItemId = table.Column<int>(type: "int", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CapabilityDocumentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tender_submission_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tender_submission_items_tender_submissions_TenderSubmissionId",
                        column: x => x.TenderSubmissionId,
                        principalTable: "tender_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_items_CompletedByUserId",
                table: "tender_checklist_items",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_submission_items_TenderSubmissionId_ChecklistItemId",
                table: "tender_submission_items",
                columns: new[] { "TenderSubmissionId", "ChecklistItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tender_submissions_ApprovedEstimateRevisionId",
                table: "tender_submissions",
                column: "ApprovedEstimateRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_submissions_FinanceReviewWorkPackageId",
                table: "tender_submissions",
                column: "FinanceReviewWorkPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_submissions_SubmittedAt",
                table: "tender_submissions",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tender_submissions_SubmittedByUserId",
                table: "tender_submissions",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_submissions_TenderId_Sequence",
                table: "tender_submissions",
                columns: new[] { "TenderId", "Sequence" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tender_checklist_items_users_CompletedByUserId",
                table: "tender_checklist_items",
                column: "CompletedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tender_checklist_items_users_CompletedByUserId",
                table: "tender_checklist_items");

            migrationBuilder.DropTable(
                name: "tender_submission_items");

            migrationBuilder.DropTable(
                name: "tender_submissions");

            migrationBuilder.DropIndex(
                name: "IX_tender_checklist_items_CompletedByUserId",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "InvitationReceivedAt",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "InvitationReference",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "tenders");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "NotApplicableReason",
                table: "tender_checklist_items");
        }
    }
}
