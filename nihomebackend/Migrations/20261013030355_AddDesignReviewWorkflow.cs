using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "design_reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DesignManagerUserId = table.Column<int>(type: "int", nullable: false),
                    InternalSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InternalSubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientSubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientDecisionNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ClientDecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientDecisionRecordedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_design_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_design_reviews_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_design_reviews_users_DesignManagerUserId",
                        column: x => x.DesignManagerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "design_review_comments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignReviewId = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_design_review_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_design_review_comments_design_reviews_DesignReviewId",
                        column: x => x.DesignReviewId,
                        principalTable: "design_reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_design_review_comments_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "design_review_documents",
                columns: table => new
                {
                    DesignReviewId = table.Column<int>(type: "int", nullable: false),
                    ProjectDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    SourceEntityTypeSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceRecordIdSnapshot = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Snapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_design_review_documents", x => new { x.DesignReviewId, x.ProjectDocumentId });
                    table.ForeignKey(
                        name: "FK_design_review_documents_design_reviews_DesignReviewId",
                        column: x => x.DesignReviewId,
                        principalTable: "design_reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_design_review_documents_project_documents_ProjectDocumentId",
                        column: x => x.ProjectDocumentId,
                        principalTable: "project_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "design_review_recipients",
                columns: table => new
                {
                    DesignReviewId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_design_review_recipients", x => new { x.DesignReviewId, x.UserId });
                    table.ForeignKey(
                        name: "FK_design_review_recipients_design_reviews_DesignReviewId",
                        column: x => x.DesignReviewId,
                        principalTable: "design_reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_design_review_recipients_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_design_review_comments_AuthorUserId",
                table: "design_review_comments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_design_review_comments_DesignReviewId_CreatedAt",
                table: "design_review_comments",
                columns: new[] { "DesignReviewId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_design_review_documents_ProjectDocumentId",
                table: "design_review_documents",
                column: "ProjectDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_design_review_documents_SourceEntityTypeSnapshot_SourceRecordIdSnapshot",
                table: "design_review_documents",
                columns: new[] { "SourceEntityTypeSnapshot", "SourceRecordIdSnapshot" });

            migrationBuilder.CreateIndex(
                name: "IX_design_review_recipients_UserId",
                table: "design_review_recipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_design_reviews_DesignManagerUserId",
                table: "design_reviews",
                column: "DesignManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_design_reviews_DesignProjectId_Stage_RevisionNumber",
                table: "design_reviews",
                columns: new[] { "DesignProjectId", "Stage", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_design_reviews_DesignProjectId_Status",
                table: "design_reviews",
                columns: new[] { "DesignProjectId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "design_review_comments");

            migrationBuilder.DropTable(
                name: "design_review_documents");

            migrationBuilder.DropTable(
                name: "design_review_recipients");

            migrationBuilder.DropTable(
                name: "design_reviews");
        }
    }
}
