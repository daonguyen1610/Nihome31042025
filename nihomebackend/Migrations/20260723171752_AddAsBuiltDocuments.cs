using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAsBuiltDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "as_built_documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    DocumentCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_as_built_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_as_built_documents_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_as_built_documents_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_as_built_documents_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_as_built_documents_ApprovedByUserId",
                table: "as_built_documents",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_as_built_documents_Category",
                table: "as_built_documents",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_as_built_documents_DesignProjectId",
                table: "as_built_documents",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_as_built_documents_DesignProjectId_DocumentCode",
                table: "as_built_documents",
                columns: new[] { "DesignProjectId", "DocumentCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_as_built_documents_Status",
                table: "as_built_documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_as_built_documents_SubmittedByUserId",
                table: "as_built_documents",
                column: "SubmittedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "as_built_documents");
        }
    }
}
