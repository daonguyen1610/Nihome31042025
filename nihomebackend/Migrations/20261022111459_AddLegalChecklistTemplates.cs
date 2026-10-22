using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalChecklistTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "permit_checklist_items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LegalChecklistAppliedAt",
                table: "department_work_packages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegalChecklistAppliedByUserId",
                table: "department_work_packages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalChecklistSnapshotJson",
                table: "department_work_packages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegalChecklistTemplateId",
                table: "department_work_packages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "legal_checklist_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    NameVi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameZh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameJa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScopeVi = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ScopeZh = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ScopeJa = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedByUserId = table.Column<int>(type: "int", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_checklist_templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_department_work_packages_LegalChecklistTemplateId",
                table: "department_work_packages",
                column: "LegalChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_checklist_templates_Code_Version",
                table: "legal_checklist_templates",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_department_work_packages_legal_checklist_templates_LegalChecklistTemplateId",
                table: "department_work_packages",
                column: "LegalChecklistTemplateId",
                principalTable: "legal_checklist_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_department_work_packages_legal_checklist_templates_LegalChecklistTemplateId",
                table: "department_work_packages");

            migrationBuilder.DropTable(
                name: "legal_checklist_templates");

            migrationBuilder.DropIndex(
                name: "IX_department_work_packages_LegalChecklistTemplateId",
                table: "department_work_packages");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "permit_checklist_items");

            migrationBuilder.DropColumn(
                name: "LegalChecklistAppliedAt",
                table: "department_work_packages");

            migrationBuilder.DropColumn(
                name: "LegalChecklistAppliedByUserId",
                table: "department_work_packages");

            migrationBuilder.DropColumn(
                name: "LegalChecklistSnapshotJson",
                table: "department_work_packages");

            migrationBuilder.DropColumn(
                name: "LegalChecklistTemplateId",
                table: "department_work_packages");
        }
    }
}
