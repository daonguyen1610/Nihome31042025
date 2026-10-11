using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteBoqImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_boq_imports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuoteId = table.Column<int>(type: "int", nullable: false),
                    QuoteVersion = table.Column<int>(type: "int", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParserVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImportedItemCount = table.Column<int>(type: "int", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_boq_imports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_boq_imports_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quote_boq_imports_users_ImportedByUserId",
                        column: x => x.ImportedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quote_boq_imports_ImportedByUserId",
                table: "quote_boq_imports",
                column: "ImportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_boq_imports_QuoteId_QuoteVersion",
                table: "quote_boq_imports",
                columns: new[] { "QuoteId", "QuoteVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_boq_imports_QuoteId_SourceHash_ParserVersion",
                table: "quote_boq_imports",
                columns: new[] { "QuoteId", "SourceHash", "ParserVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_boq_imports");
        }
    }
}
