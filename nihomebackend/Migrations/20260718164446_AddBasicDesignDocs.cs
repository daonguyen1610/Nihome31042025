using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBasicDesignDocs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "basic_design_docs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    DisciplineCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DocumentCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_basic_design_docs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_basic_design_docs_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_basic_design_docs_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_basic_design_docs_DesignProjectId",
                table: "basic_design_docs",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_basic_design_docs_DesignProjectId_DocumentCode",
                table: "basic_design_docs",
                columns: new[] { "DesignProjectId", "DocumentCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_basic_design_docs_DisciplineCode",
                table: "basic_design_docs",
                column: "DisciplineCode");

            migrationBuilder.CreateIndex(
                name: "IX_basic_design_docs_OwnerUserId",
                table: "basic_design_docs",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_basic_design_docs_Status",
                table: "basic_design_docs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "basic_design_docs");
        }
    }
}
