using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OpportunityId = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    AreaSqm = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitPricePerSqm = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PackageDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    VatPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentByUserId = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quotes_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotes_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "quote_approval_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuoteId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ByUserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_approval_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_approval_logs_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quote_approval_logs_users_ByUserId",
                        column: x => x.ByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "quote_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuoteId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_items_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_version_snapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuoteId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AreaSqm = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UnitPricePerSqm = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PackageDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    VatPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_version_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_version_snapshots_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quote_approval_logs_ByUserId",
                table: "quote_approval_logs",
                column: "ByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_approval_logs_CreatedAt",
                table: "quote_approval_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_quote_approval_logs_QuoteId",
                table: "quote_approval_logs",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_items_QuoteId",
                table: "quote_items",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_version_snapshots_QuoteId",
                table: "quote_version_snapshots",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_version_snapshots_QuoteId_VersionNumber",
                table: "quote_version_snapshots",
                columns: new[] { "QuoteId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_Code",
                table: "quotes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_CreatedAt",
                table: "quotes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_OpportunityId",
                table: "quotes",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_OwnerUserId",
                table: "quotes",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_Status",
                table: "quotes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_ValidUntil",
                table: "quotes",
                column: "ValidUntil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_approval_logs");

            migrationBuilder.DropTable(
                name: "quote_items");

            migrationBuilder.DropTable(
                name: "quote_version_snapshots");

            migrationBuilder.DropTable(
                name: "quotes");
        }
    }
}
