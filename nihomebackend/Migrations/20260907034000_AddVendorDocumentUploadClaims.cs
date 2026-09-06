using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorDocumentUploadClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT CapabilityFileUrl
                    FROM procurement_vendors
                    WHERE CapabilityFileUrl LIKE '/files/business-documents/vendors/%'
                    GROUP BY CapabilityFileUrl
                    HAVING COUNT(*) > 1
                )
                    THROW 51031,
                        'Vendor upload claim migration blocked: a managed capability file is referenced by multiple vendors.',
                        1;
                """);

            migrationBuilder.CreateTable(
                name: "vendor_document_uploads",
                columns: table => new
                {
                    Token = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_document_uploads", x => x.Token);
                    table.ForeignKey(
                        name: "FK_vendor_document_uploads_procurement_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "procurement_vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vendor_document_uploads_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO vendor_document_uploads
                    (Token, Path, VendorId, CreatedByUserId, CreatedAt, ClaimedAt)
                SELECT NEWID(), CapabilityFileUrl, Id, CreatedByUserId, CreatedAt, SYSUTCDATETIME()
                FROM procurement_vendors
                WHERE CapabilityFileUrl LIKE '/files/business-documents/vendors/%';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_vendor_document_uploads_CreatedByUserId",
                table: "vendor_document_uploads",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_document_uploads_Path",
                table: "vendor_document_uploads",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vendor_document_uploads_VendorId",
                table: "vendor_document_uploads",
                column: "VendorId",
                unique: true,
                filter: "[VendorId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vendor_document_uploads");
        }
    }
}
