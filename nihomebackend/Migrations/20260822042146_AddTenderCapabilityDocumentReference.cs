using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTenderCapabilityDocumentReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CapabilityDocumentId",
                table: "tender_checklist_items",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE checklist
                SET CapabilityDocumentId = matched.Id
                FROM tender_checklist_items AS checklist
                CROSS APPLY
                (
                    SELECT TOP (1) document.Id
                    FROM capability_documents AS document
                    LEFT JOIN capability_document_versions AS version
                        ON version.CapabilityDocumentId = document.Id
                    WHERE document.FilePath = checklist.FilePath
                        OR version.FilePath = checklist.FilePath
                    ORDER BY
                        CASE WHEN document.FilePath = checklist.FilePath THEN 0 ELSE 1 END,
                        document.Id
                ) AS matched
                WHERE checklist.FilePath LIKE '/files/capability/%';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_items_CapabilityDocumentId",
                table: "tender_checklist_items",
                column: "CapabilityDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_tender_checklist_items_capability_documents_CapabilityDocumentId",
                table: "tender_checklist_items",
                column: "CapabilityDocumentId",
                principalTable: "capability_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tender_checklist_items_capability_documents_CapabilityDocumentId",
                table: "tender_checklist_items");

            migrationBuilder.DropIndex(
                name: "IX_tender_checklist_items_CapabilityDocumentId",
                table: "tender_checklist_items");

            migrationBuilder.DropColumn(
                name: "CapabilityDocumentId",
                table: "tender_checklist_items");
        }
    }
}
