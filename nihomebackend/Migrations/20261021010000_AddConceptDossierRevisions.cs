using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddConceptDossierRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM concept_options
                    WHERE Status = 'Finalized'
                    GROUP BY DesignProjectId HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Concept migration blocked: a project has multiple finalized options.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_concept_options_DesignProjectId",
                table: "concept_options");

            migrationBuilder.AddColumn<long>(
                name: "ClientAcceptanceDocumentId",
                table: "design_reviews",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClientAcceptanceSignedAt",
                table: "design_reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientAcceptanceSignedBy",
                table: "design_reviews",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConceptRevisionId",
                table: "design_reviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "concept_revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConceptOptionId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    FrozenAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concept_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_concept_revisions_concept_options_ConceptOptionId",
                        column: x => x.ConceptOptionId,
                        principalTable: "concept_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO concept_revisions (ConceptOptionId, Number, CreatedAt, CreatedByUserId, FrozenAt)
                SELECT option_row.Id, 0, option_row.CreatedAt, option_row.CreatedByUserId,
                    (
                        SELECT MIN(COALESCE(review.InternalSubmittedAt,
                            review.ClientSubmittedAt, review.ClientDecidedAt, review.CreatedAt))
                        FROM design_reviews AS review
                        JOIN design_review_documents AS document_row ON document_row.DesignReviewId = review.Id
                        WHERE review.Stage = 'Concept'
                          AND review.DesignProjectId = option_row.DesignProjectId
                          AND document_row.SourceEntityTypeSnapshot = 'ConceptOption'
                          AND document_row.SourceRecordIdSnapshot = option_row.Id
                          AND review.Status <> 'Draft'
                    )
                FROM concept_options AS option_row;

                UPDATE review
                SET ConceptRevisionId = revision.Id
                FROM design_reviews AS review
                CROSS APPLY (
                    SELECT COUNT(DISTINCT document_row.SourceRecordIdSnapshot) AS OptionCount,
                           MIN(document_row.SourceRecordIdSnapshot) AS OptionId
                    FROM design_review_documents AS document_row
                    WHERE document_row.DesignReviewId = review.Id
                      AND document_row.SourceEntityTypeSnapshot = 'ConceptOption'
                ) AS source_row
                JOIN concept_revisions AS revision ON revision.ConceptOptionId = source_row.OptionId
                    AND revision.Number = 0
                WHERE review.Stage = 'Concept' AND source_row.OptionCount = 1
                  AND NOT EXISTS (
                    SELECT 1 FROM design_review_documents AS other_row
                    WHERE other_row.DesignReviewId = review.Id
                      AND other_row.SourceEntityTypeSnapshot <> 'ConceptOption'
                  );
                """);

            migrationBuilder.CreateTable(
                name: "concept_artifacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConceptRevisionId = table.Column<int>(type: "int", nullable: false),
                    ProjectDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FloorLabel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concept_artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_concept_artifacts_concept_revisions_ConceptRevisionId",
                        column: x => x.ConceptRevisionId,
                        principalTable: "concept_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_concept_artifacts_project_documents_ProjectDocumentId",
                        column: x => x.ProjectDocumentId,
                        principalTable: "project_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "concept_checklist_waivers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConceptRevisionId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concept_checklist_waivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_concept_checklist_waivers_concept_revisions_ConceptRevisionId",
                        column: x => x.ConceptRevisionId,
                        principalTable: "concept_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "concept_feedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConceptRevisionId = table.Column<int>(type: "int", nullable: false),
                    ProjectDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Response = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concept_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_concept_feedback_concept_revisions_ConceptRevisionId",
                        column: x => x.ConceptRevisionId,
                        principalTable: "concept_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_concept_feedback_project_documents_ProjectDocumentId",
                        column: x => x.ProjectDocumentId,
                        principalTable: "project_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_design_reviews_ClientAcceptanceDocumentId",
                table: "design_reviews",
                column: "ClientAcceptanceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_design_reviews_ConceptRevisionId",
                table: "design_reviews",
                column: "ConceptRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_concept_options_DesignProjectId",
                table: "concept_options",
                column: "DesignProjectId",
                unique: true,
                filter: "[Status] = 'Finalized'");

            migrationBuilder.CreateIndex(
                name: "IX_concept_artifacts_ConceptRevisionId_ProjectDocumentId",
                table: "concept_artifacts",
                columns: new[] { "ConceptRevisionId", "ProjectDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_concept_artifacts_ProjectDocumentId",
                table: "concept_artifacts",
                column: "ProjectDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_concept_checklist_waivers_ConceptRevisionId_Kind",
                table: "concept_checklist_waivers",
                columns: new[] { "ConceptRevisionId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_concept_feedback_ConceptRevisionId_CreatedAt",
                table: "concept_feedback",
                columns: new[] { "ConceptRevisionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_concept_feedback_ProjectDocumentId",
                table: "concept_feedback",
                column: "ProjectDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_concept_revisions_ConceptOptionId_Number",
                table: "concept_revisions",
                columns: new[] { "ConceptOptionId", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_design_reviews_concept_revisions_ConceptRevisionId",
                table: "design_reviews",
                column: "ConceptRevisionId",
                principalTable: "concept_revisions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_design_reviews_project_documents_ClientAcceptanceDocumentId",
                table: "design_reviews",
                column: "ClientAcceptanceDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM concept_revisions WHERE Number > 0)
                   OR EXISTS (SELECT 1 FROM concept_artifacts)
                   OR EXISTS (SELECT 1 FROM concept_checklist_waivers)
                   OR EXISTS (SELECT 1 FROM concept_feedback)
                   OR EXISTS (SELECT 1 FROM design_reviews WHERE ClientAcceptanceDocumentId IS NOT NULL)
                    THROW 51000, 'Rollback blocked while Concept revision or acceptance data exists.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_design_reviews_concept_revisions_ConceptRevisionId",
                table: "design_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_design_reviews_project_documents_ClientAcceptanceDocumentId",
                table: "design_reviews");

            migrationBuilder.DropTable(
                name: "concept_artifacts");

            migrationBuilder.DropTable(
                name: "concept_checklist_waivers");

            migrationBuilder.DropTable(
                name: "concept_feedback");

            migrationBuilder.DropTable(
                name: "concept_revisions");

            migrationBuilder.DropIndex(
                name: "IX_design_reviews_ClientAcceptanceDocumentId",
                table: "design_reviews");

            migrationBuilder.DropIndex(
                name: "IX_design_reviews_ConceptRevisionId",
                table: "design_reviews");

            migrationBuilder.DropIndex(
                name: "IX_concept_options_DesignProjectId",
                table: "concept_options");

            migrationBuilder.DropColumn(
                name: "ClientAcceptanceDocumentId",
                table: "design_reviews");

            migrationBuilder.DropColumn(
                name: "ClientAcceptanceSignedAt",
                table: "design_reviews");

            migrationBuilder.DropColumn(
                name: "ClientAcceptanceSignedBy",
                table: "design_reviews");

            migrationBuilder.DropColumn(
                name: "ConceptRevisionId",
                table: "design_reviews");

            migrationBuilder.CreateIndex(
                name: "IX_concept_options_DesignProjectId",
                table: "concept_options",
                column: "DesignProjectId");
        }
    }
}
