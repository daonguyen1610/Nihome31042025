using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261020160000_LinkDesignReviewCommentsToDocuments")]
public sealed class LinkDesignReviewCommentsToDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ProjectDocumentId",
            table: "design_review_comments",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_design_review_comments_DesignReviewId_ProjectDocumentId",
            table: "design_review_comments",
            columns: new[] { "DesignReviewId", "ProjectDocumentId" });

        migrationBuilder.AddForeignKey(
            name: "FK_design_review_comments_design_review_documents_DesignReviewId_ProjectDocumentId",
            table: "design_review_comments",
            columns: new[] { "DesignReviewId", "ProjectDocumentId" },
            principalTable: "design_review_documents",
            principalColumns: new[] { "DesignReviewId", "ProjectDocumentId" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_design_review_comments_design_review_documents_DesignReviewId_ProjectDocumentId",
            table: "design_review_comments");
        migrationBuilder.DropIndex(
            name: "IX_design_review_comments_DesignReviewId_ProjectDocumentId",
            table: "design_review_comments");
        migrationBuilder.DropColumn(
            name: "ProjectDocumentId",
            table: "design_review_comments");
    }
}
