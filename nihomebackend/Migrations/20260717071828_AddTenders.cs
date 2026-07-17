using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTenders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    OpeningDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmissionDeadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreparerUserId = table.Column<int>(type: "int", nullable: true),
                    InfoSource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    WonOpportunityId = table.Column<int>(type: "int", nullable: true),
                    LostReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LostNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenders_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenders_users_PreparerUserId",
                        column: x => x.PreparerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tender_checklist_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    InternalDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tender_checklist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tender_checklist_items_tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tender_checklist_items_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_items_OwnerUserId",
                table: "tender_checklist_items",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_items_TenderId",
                table: "tender_checklist_items",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_tender_checklist_items_TenderId_SortOrder",
                table: "tender_checklist_items",
                columns: new[] { "TenderId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_tenders_Code",
                table: "tenders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenders_CreatedAt",
                table: "tenders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tenders_CustomerId",
                table: "tenders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_tenders_PreparerUserId",
                table: "tenders",
                column: "PreparerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tenders_Status",
                table: "tenders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tenders_SubmissionDeadline",
                table: "tenders",
                column: "SubmissionDeadline");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tender_checklist_items");

            migrationBuilder.DropTable(
                name: "tenders");
        }
    }
}
