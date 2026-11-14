using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBoqWorkbooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "project_boq_revisions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSha256",
                table: "project_boq_revisions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceWorkbookPath",
                table: "project_boq_revisions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateCode",
                table: "project_boq_revisions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "project_boq_lines",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PriceMissing",
                table: "project_boq_lines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SheetName",
                table: "project_boq_lines",
                type: "nvarchar(31)",
                maxLength: 31,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceRow",
                table: "project_boq_lines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stt",
                table: "project_boq_lines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_boq_sheets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectBoqRevisionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(31)", maxLength: 31, nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    MissingPriceCount = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_boq_sheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_boq_sheets_project_boq_revisions_ProjectBoqRevisionId",
                        column: x => x.ProjectBoqRevisionId,
                        principalTable: "project_boq_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_sheets_ProjectBoqRevisionId_SortOrder",
                table: "project_boq_sheets",
                columns: new[] { "ProjectBoqRevisionId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_boq_sheets");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "project_boq_revisions");

            migrationBuilder.DropColumn(
                name: "SourceSha256",
                table: "project_boq_revisions");

            migrationBuilder.DropColumn(
                name: "SourceWorkbookPath",
                table: "project_boq_revisions");

            migrationBuilder.DropColumn(
                name: "TemplateCode",
                table: "project_boq_revisions");

            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "project_boq_lines");

            migrationBuilder.DropColumn(
                name: "PriceMissing",
                table: "project_boq_lines");

            migrationBuilder.DropColumn(
                name: "SheetName",
                table: "project_boq_lines");

            migrationBuilder.DropColumn(
                name: "SourceRow",
                table: "project_boq_lines");

            migrationBuilder.DropColumn(
                name: "Stt",
                table: "project_boq_lines");
        }
    }
}
