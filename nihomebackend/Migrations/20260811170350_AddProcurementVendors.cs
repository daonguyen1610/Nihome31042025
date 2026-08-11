using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementVendors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procurement_vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NormalizedCompanyName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    VendorType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LicenseNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ServiceGroupCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procurement_vendors", x => x.Id);
                    table.CheckConstraint("CK_procurement_vendors_VendorType", "[VendorType] IN ('Supplier','SubContractor','Both')");
                    table.ForeignKey(
                        name: "FK_procurement_vendors_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procurement_vendor_documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procurement_vendor_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_procurement_vendor_documents_procurement_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "procurement_vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "procurement_vendor_evaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ScoreQuality = table.Column<byte>(type: "tinyint", nullable: false),
                    ScoreSchedule = table.Column<byte>(type: "tinyint", nullable: false),
                    ScoreCost = table.Column<byte>(type: "tinyint", nullable: false),
                    ScoreSafety = table.Column<byte>(type: "tinyint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvaluatedByUserId = table.Column<int>(type: "int", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procurement_vendor_evaluations", x => x.Id);
                    table.CheckConstraint("CK_vendor_evaluations_Cost", "[ScoreCost] BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_vendor_evaluations_Quality", "[ScoreQuality] BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_vendor_evaluations_Safety", "[ScoreSafety] BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_vendor_evaluations_Schedule", "[ScoreSchedule] BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "FK_procurement_vendor_evaluations_design_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_procurement_vendor_evaluations_procurement_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "procurement_vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_procurement_vendor_evaluations_users_EvaluatedByUserId",
                        column: x => x.EvaluatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_procurement_vendor_evaluations_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendor_documents_VendorId_CreatedAt",
                table: "procurement_vendor_documents",
                columns: new[] { "VendorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendor_evaluations_EvaluatedAt",
                table: "procurement_vendor_evaluations",
                column: "EvaluatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendor_evaluations_EvaluatedByUserId",
                table: "procurement_vendor_evaluations",
                column: "EvaluatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendor_evaluations_ProjectId",
                table: "procurement_vendor_evaluations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendor_evaluations_UpdatedByUserId",
                table: "procurement_vendor_evaluations",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendor_evaluations_VendorId_ProjectId",
                table: "procurement_vendor_evaluations",
                columns: new[] { "VendorId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendors_IsActive",
                table: "procurement_vendors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendors_NormalizedCompanyName",
                table: "procurement_vendors",
                column: "NormalizedCompanyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendors_OwnerUserId",
                table: "procurement_vendors",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendors_ServiceGroupCode",
                table: "procurement_vendors",
                column: "ServiceGroupCode");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendors_TaxCode",
                table: "procurement_vendors",
                column: "TaxCode",
                unique: true,
                filter: "[TaxCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendors_VendorCode",
                table: "procurement_vendors",
                column: "VendorCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procurement_vendors_VendorType",
                table: "procurement_vendors",
                column: "VendorType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_vendor_documents");

            migrationBuilder.DropTable(
                name: "procurement_vendor_evaluations");

            migrationBuilder.DropTable(
                name: "procurement_vendors");
        }
    }
}
